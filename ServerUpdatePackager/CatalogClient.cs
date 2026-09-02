using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ServerUpdatePackager;

internal sealed class CatalogClient : IDisposable
{
    private const string CatalogRoot = "https://www.catalog.update.microsoft.com/";
    private readonly HttpClient _http;

    public CatalogClient(HttpMessageHandler? handler = null)
    {
        handler ??= new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = true,
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            UseProxy = true,
            DefaultProxyCredentials = CredentialCache.DefaultCredentials
        };

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Server-Update-Packager/2.0 (@emrahtolu)");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
    }

    public async Task<CatalogEntry?> FindMonthlyUpdateAsync(
        UpdateTargetDefinition target, int year, int month, CancellationToken cancellationToken)
    {
        var entries = await SearchAsync(target.Query(year, month), cancellationToken);
        return entries
            .Where(target.EntryMatcher)
            .Where(e => !IsExcludedMonthlyResult(e.Title))
            .Select(e => (Entry: e, Score: ScoreMonthlyEntry(e, year, month)))
            .Where(x => x.Score >= 100)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Entry.LastUpdated)
            .Select(x => x.Entry)
            .FirstOrDefault();
    }

    public async Task<CatalogEntry?> FindMonthlySsuAsync(
        UpdateTargetDefinition target, int year, int month, CancellationToken cancellationToken)
    {
        var entries = await SearchAsync(target.SsuQuery(year, month), cancellationToken);
        return entries
            .Where(target.EntryMatcher)
            .Where(e => IsServicingStackTitle(e.Title))
            .Where(e => IsX64Title(e.Title))
            .Where(e => e.LastUpdated?.Year == year && e.LastUpdated?.Month == month)
            .OrderByDescending(e => e.LastUpdated)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<CatalogEntry>> FindByKbAsync(
        string kb, CancellationToken cancellationToken)
    {
        var normalized = CatalogText.ExtractKb(kb);
        if (normalized == "KB?") throw new ArgumentException("Geçerli bir KB numarası girin.", nameof(kb));

        var entries = await SearchAsync(normalized + " x64", cancellationToken);
        return entries
            .Where(e => e.Kb.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .Where(e => IsX64Title(e.Title))
            .GroupBy(e => e.UpdateId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(e => e.LastUpdated)
            .ThenBy(e => e.Product)
            .Take(25)
            .ToList();
    }

    public async Task<CatalogEntry?> FindMsrtAsync(int year, int month, CancellationToken cancellationToken)
    {
        var entries = await SearchAsync("KB890830 x64", cancellationToken);
        return entries
            .Where(e => e.Title.Contains("KB890830", StringComparison.OrdinalIgnoreCase))
            .Where(e => IsX64Title(e.Title))
            .Where(e => e.LastUpdated?.Year == year && e.LastUpdated?.Month == month)
            .OrderByDescending(e => ExtractMsrtVersion(e.Title))
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<CatalogEntry>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var url = CatalogRoot + "Search.aspx?q=" + Uri.EscapeDataString(query);
        var html = await SendStringWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
        return ParseSearchResults(html);
    }

    public async Task<IReadOnlyList<CatalogFile>> ResolveDownloadFilesAsync(
        string updateId, CancellationToken cancellationToken)
    {
        var safeId = updateId.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var payload = "[{\"size\":0,\"updateID\":\"" + safeId + "\",\"uidInfo\":\"" + safeId + "\"}]";

        var html = await SendStringWithRetryAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, CatalogRoot + "DownloadDialog.aspx")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["updateIDs"] = payload })
            };
            request.Headers.Referrer = new Uri(CatalogRoot + "Search.aspx");
            return request;
        }, cancellationToken);
        var files = ParseDownloadFiles(html);
        if (files.Count == 0)
            throw new InvalidOperationException("Catalog indirme bağlantısı döndürmedi. Microsoft sayfa yapısı değişmiş veya paket kaldırılmış olabilir.");
        return files;
    }

    public async Task<string> DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<(long Received, long? Total)>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var partialPath = destinationPath + ".partial";

        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;

            if (File.Exists(destinationPath) && total.HasValue && new FileInfo(destinationPath).Length == total.Value)
            {
                progress?.Report((total.Value, total));
                return await CalculateSha256Async(destinationPath, cancellationToken);
            }

            using (var source = await response.Content.ReadAsStreamAsync())
            using (var target = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None,
                       1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[1024 * 1024];
                long received = 0;
                while (true)
                {
                    var read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (read == 0) break;
                    await target.WriteAsync(buffer, 0, read, cancellationToken);
                    received += read;
                    progress?.Report((received, total));
                }
                await target.FlushAsync(cancellationToken);
            }

            if (File.Exists(destinationPath)) File.Replace(partialPath, destinationPath, null);
            else File.Move(partialPath, destinationPath);
            return await CalculateSha256Async(destinationPath, cancellationToken);
        }
        catch
        {
            if (File.Exists(partialPath)) File.Delete(partialPath);
            throw;
        }
    }

    internal static IReadOnlyList<CatalogEntry> ParseSearchResults(string html)
    {
        var results = new List<CatalogEntry>();
        var rowPattern = new Regex(
            @"<tr\b[^>]*\bid\s*=\s*[""'](?<id>[0-9a-fA-F-]{36})_R\d+[""'][^>]*>(?<body>.*?)</tr>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var cellPattern = new Regex(@"<td\b[^>]*>(?<body>.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match row in rowPattern.Matches(html))
        {
            var cells = cellPattern.Matches(row.Groups["body"].Value)
                .Cast<Match>()
                .Select(m => m.Groups["body"].Value)
                .ToList();
            if (cells.Count < 7) continue;

            var id = row.Groups["id"].Value;
            var originalSize = Regex.Match(cells[6], @"originalSize[^>]*>\s*(\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            results.Add(new CatalogEntry
            {
                UpdateId = id,
                Title = StripHtml(cells[1]),
                Product = StripHtml(cells[2]),
                Classification = StripHtml(cells[3]),
                LastUpdated = ParseCatalogDate(StripHtml(cells[4])),
                SizeText = StripHtml(cells[6]),
                SizeBytes = originalSize.Success && long.TryParse(originalSize.Groups[1].Value, out var size) ? size : null
            });
        }
        return results;
    }

    internal static IReadOnlyList<CatalogFile> ParseDownloadFiles(string html)
    {
        var decoded = WebUtility.HtmlDecode(html)
            .Replace("\\/", "/")
            .ReplaceOrdinalIgnoreCase("\\u0026", "&")
            .ReplaceOrdinalIgnoreCase("\\x26", "&");

        var urlPattern = new Regex(@"https?://[^'""<>\s]+", RegexOptions.IgnoreCase);
        var files = new List<CatalogFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in urlPattern.Matches(decoded))
        {
            var url = match.Value.TrimEnd(')', ']', '}', ';', ',');
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) continue;
            if (!IsTrustedDownloadHost(uri.Host)) continue;
            var extension = Path.GetExtension(uri.AbsolutePath);
            if (!extension.Equals(".msu", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".cab", StringComparison.OrdinalIgnoreCase)) continue;
            if (!seen.Add(url)) continue;

            var fileName = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
            if (string.IsNullOrWhiteSpace(fileName)) continue;
            files.Add(new CatalogFile(url, fileName, files.Count));
        }
        return files;
    }

    private static bool IsTrustedDownloadHost(string host) =>
        host.Equals("microsoft.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("windowsupdate.com", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".windowsupdate.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsExcludedMonthlyResult(string title)
    {
        string[] excluded =
        [
            ".NET Framework", "Preview", "Önizleme", "Dynamic Update", "Dinamik Güncelleştirme",
            "Servicing Stack", "Hizmet Yığını", "Safe OS", "Setup Dynamic"
        ];
        return excluded.Any(x => title.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsServicingStackTitle(string title) =>
        title.Contains("Servicing Stack", StringComparison.OrdinalIgnoreCase)
        || title.Contains("Hizmet Yığını", StringComparison.OrdinalIgnoreCase);

    private static bool IsX64Title(string title) =>
        title.Contains("x64", StringComparison.OrdinalIgnoreCase)
        && !title.Contains("ARM64", StringComparison.OrdinalIgnoreCase)
        && !Regex.IsMatch(title, @"\bx86\b", RegexOptions.IgnoreCase);

    private static int ScoreMonthlyEntry(CatalogEntry entry, int year, int month)
    {
        var score = 0;
        if (entry.LastUpdated?.Year == year && entry.LastUpdated?.Month == month) score += 100;
        if (entry.Title.Contains("Cumulative Update", StringComparison.OrdinalIgnoreCase)
            || entry.Title.Contains("Toplu Güncelleştirme", StringComparison.OrdinalIgnoreCase)) score += 30;
        if (entry.Title.Contains("x64", StringComparison.OrdinalIgnoreCase)) score += 20;
        if (entry.Classification.Contains("Security", StringComparison.OrdinalIgnoreCase)
            || entry.Classification.Contains("Güvenlik", StringComparison.OrdinalIgnoreCase)) score += 10;
        return score;
    }

    private static Version ExtractMsrtVersion(string title)
    {
        var match = Regex.Match(title, @"v(\d+(?:\.\d+)*)", RegexOptions.IgnoreCase);
        return match.Success && Version.TryParse(match.Groups[1].Value, out var version) ? version : new Version(0, 0);
    }

    private static DateTime? ParseCatalogDate(string value)
    {
        var cultures = new[] { CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("tr-TR"), CultureInfo.InvariantCulture };
        foreach (var culture in cultures)
            if (DateTime.TryParse(value, culture, DateTimeStyles.AllowWhiteSpaces, out var date)) return date.Date;
        return null;
    }

    private static string StripHtml(string value)
    {
        var noTags = Regex.Replace(value, "<.*?>", " ", RegexOptions.Singleline);
        var decoded = WebUtility.HtmlDecode(noTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    private static async Task<string> CalculateSha256Async(string path, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                       1024 * 1024, FileOptions.SequentialScan))
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
        }, cancellationToken);
    }

    private async Task<string> SendStringWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (var request = requestFactory())
                {
                    request.Version = HttpVersion.Version11;
                    // Bazı kurumsal proxy'ler Catalog'un kapattığı keep-alive
                    // bağlantısını havuzda tutar; her küçük HTML isteğini temiz aç.
                    request.Headers.ConnectionClose = true;
                    using (var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                lastError = ex;
                if (attempt == 3) break;
                await Task.Delay(500 * attempt, cancellationToken);
            }
            catch (IOException ex)
            {
                lastError = ex;
                if (attempt == 3) break;
                await Task.Delay(500 * attempt, cancellationToken);
            }
        }
        throw new HttpRequestException("Microsoft Update Catalog bağlantısı 3 denemeden sonra kurulamadı.", lastError);
    }

    public void Dispose() => _http.Dispose();
}
