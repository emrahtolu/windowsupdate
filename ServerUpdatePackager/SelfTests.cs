namespace ServerUpdatePackager;

internal static class SelfTests
{
    public static int Run()
    {
        var failures = new List<string>();
        Check("Catalog arama HTML ayrıştırma", TestSearchParser, failures);
        Check("Catalog çoklu checkpoint bağlantıları", TestDownloadParser, failures);
        Check("Hedef tanımları", TestTargetDefinitions, failures);
        Check("PowerShell üretimi, SSU sırası ve UNC düzeltme", TestScriptGenerator, failures);

        if (failures.Count == 0)
        {
            Console.WriteLine("SELF-TEST OK (4/4)");
            return 0;
        }
        foreach (var failure in failures) Console.Error.WriteLine("FAIL: " + failure);
        return 1;
    }

    private static void TestSearchParser()
    {
        const string html = """
        <table><tr id="7d57a79b-cbbb-4183-a105-0fc7322208d1_R0">
        <td></td><td><a>2026-08 Cumulative Update for Windows Server 2019 for x64-based Systems (KB5120238)</a></td>
        <td>Windows Server 2019</td><td>Security Updates</td><td>8/11/2026</td><td>n/a</td>
        <td><span>860.4 MB</span><span id="7d57a79b-cbbb-4183-a105-0fc7322208d1_originalSize">902190172</span></td>
        <td><input type="button"></td></tr></table>
        """;
        var entries = CatalogClient.ParseSearchResults(html);
        Require(entries.Count == 1, "tek sonuç bekleniyordu");
        Require(entries[0].Kb == "KB5120238", "KB ayrıştırılamadı");
        Require(entries[0].SizeBytes == 902190172, "boyut ayrıştırılamadı");
        Require(entries[0].LastUpdated == new DateTime(2026, 8, 11), "tarih ayrıştırılamadı");
    }

    private static void TestDownloadParser()
    {
        const string html = """
        <script>
        downloadInformation[0].files[0].url = 'https://catalog.sf.dl.delivery.mp.microsoft.com/a/windows11.0-kb5043080-x64_hash.msu';
        downloadInformation[0].files[1].url = 'https:\/\/catalog.sf.dl.delivery.mp.microsoft.com\/b\/windows11.0-kb5120233-x64_hash.msu?x=1\u0026y=2';
        downloadInformation[0].files[1].url2 = 'https://catalog.sf.dl.delivery.mp.microsoft.com/b/windows11.0-kb5120233-x64_hash.msu?x=1&amp;y=2';
        </script>
        """;
        var files = CatalogClient.ParseDownloadFiles(html);
        Require(files.Count == 2, "iki benzersiz MSU bekleniyordu");
        Require(files[0].FileName.Contains("kb5043080"), "checkpoint ilk sırada değil");
        Require(files[1].Url.Contains("&y=2"), "URL escape çözülmedi");
    }

    private static void TestScriptGenerator()
    {
        var rows = new[]
        {
            Row("server-2025", "Server 2025", "Windows Server 2025", "KB5000001", "ssu-kb5000001-x64.msu", -100, ssu: true),
            Row("server-2025", "Server 2025", "Windows Server 2025", "KB5043080", "windows11.0-kb5043080-x64_hash.msu", 0, checkpoint: true),
            Row("server-2025", "Server 2025", "Windows Server 2025", "KB5120233", "windows11.0-kb5120233-x64_hash.msu", 1),
            Row("windows-11-24h2", "Windows 11 24H2", "Windows 11 24H2", "KB5121003", "windows11.0-kb5121003-x64_hash.msu", 0),
            new UpdateFileRow
            {
                Target = "Tüm seçili sistemler", TargetKey = "common", FolderName = "Common", Kb = "KB890830", Title = "MSRT",
                FileName = "windows-kb890830-x64-v5.144_hash.exe", DownloadUrl = "https://example/msrt.exe",
                UpdateId = Guid.Empty.ToString(), InstallOrder = 10000, IsCommon = true
            },
            new UpdateFileRow
            {
                Target = "Manual", TargetKey = "manual", FolderName = "Manual KB", Kb = "KB1234567", Title = "Manual",
                FileName = "manual-kb1234567-x64.msu", DownloadUrl = "https://example/manual.msu",
                UpdateId = Guid.Empty.ToString(), InstallOrder = 20000, IsManual = true
            }
        };
        var script = ScriptGenerator.Generate(@"\SUNUCU\PAKETLER", rows, true);
        Require(script.Contains(@"$BasePath = '\\SUNUCU\PAKETLER"), "tek ters eğik çizgili UNC düzeltilmedi");
        var ssu = script.IndexOf("kb5000001", StringComparison.OrdinalIgnoreCase);
        var checkpoint = script.IndexOf("kb5043080", StringComparison.OrdinalIgnoreCase);
        var latest = script.IndexOf("kb5120233", StringComparison.OrdinalIgnoreCase);
        Require(ssu >= 0 && checkpoint > ssu && latest > checkpoint, "SSU/checkpoint/CU kurulum sırası yanlış");
        Require(script.Contains(@"Server 2025\windows11.0-kb5120233"), "alt klasör yolu yok");
        Require(script.Contains("$displayVersion -eq '24H2'"), "Windows 11 DisplayVersion koşulu yok");
        Require(script.Contains(@"Windows 11 24H2\windows11.0-kb5121003"), "Windows 11 alt klasörü yok");
        Require(script.Contains(@"Common\windows-kb890830"), "MSRT ortak klasörü yok");
        Require(!script.Contains("manual-kb1234567"), "manuel KB otomatik scriptte olmamalı");
        Require(!script.Contains("```"), "scriptte markdown artığı var");
    }

    private static void TestTargetDefinitions()
    {
        Require(UpdateTargetDefinition.All.Count == 8, "sekiz hedef bekleniyordu");
        Require(UpdateTargetDefinition.All.Count(x => x.DefaultSelected) == 4, "yalnız sunucular varsayılan seçili olmalı");
        Require(UpdateTargetDefinition.All.Any(x => x.Key == "windows-11-25h2"), "Windows 11 25H2 hedefi yok");
        Require(UpdateTargetDefinition.All.Any(x => x.Key == "windows-11-26h1"), "Windows 11 26H1 hedefi yok");
        Require(UpdateTargetDefinition.All.Any(x => x.Key == "server-2025" && x.SupportsCheckpoints), "Server 2025 checkpoint işareti yok");
    }

    private static UpdateFileRow Row(
        string key, string folder, string target, string kb, string file, int order,
        bool checkpoint = false, bool ssu = false) => new()
    {
        Target = target,
        TargetKey = key,
        FolderName = folder,
        Kb = kb,
        Title = kb,
        FileName = file,
        DownloadUrl = "https://example/" + file,
        UpdateId = Guid.Empty.ToString(),
        InstallOrder = order,
        IsCheckpoint = checkpoint,
        IsSsu = ssu
    };

    private static void Check(string name, Action test, ICollection<string> failures)
    {
        try { test(); Console.WriteLine("PASS: " + name); }
        catch (Exception ex) { failures.Add(name + " — " + ex.Message); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
