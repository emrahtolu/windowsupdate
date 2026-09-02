using System.Text;

namespace ServerUpdatePackager;

internal static class ScriptGenerator
{
    public static string Generate(string basePath, IEnumerable<UpdateFileRow> rows, bool useSubfolders)
    {
        // Manuel KB aramasından eklenen paketler hedef işletim sistemiyle güvenli
        // biçimde eşleştirilemediği için otomatik kurulum betiğine dahil edilmez.
        var selected = rows.Where(x => x.Selected && !x.IsManual).OrderBy(x => x.InstallOrder).ToList();
        var common = selected.Where(x => x.IsCommon).ToList();
        var targetRows = selected.Where(x => !x.IsCommon)
            .GroupBy(x => x.TargetKey)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.InstallOrder).ToList());

        basePath = NormalizeUnc(basePath.Trim());
        var sb = new StringBuilder();
        sb.AppendLine("#requires -version 5.1");
        sb.AppendLine("# Server Update Packager tarafindan olusturulmustur.");
        sb.AppendLine("# Gelistiren: @emrahtolu");
        sb.AppendLine("# Paket klasor yapisini paylasima aynen kopyalayin.");
        sb.AppendLine();
        sb.AppendLine("# Dosyalarin bulundugu ana klasor yolu");
        sb.AppendLine($"$BasePath = '{EscapePowerShell(basePath)}'");
        sb.AppendLine();
        sb.AppendLine("# 1. Asama: Isletim sistemini tespit et");
        sb.AppendLine("$osInfo = (Get-CimInstance Win32_OperatingSystem).Caption");
        sb.AppendLine("$displayVersion = (Get-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion' -ErrorAction SilentlyContinue).DisplayVersion");
        sb.AppendLine("$UpdateFiles = @()");
        sb.AppendLine();

        if (targetRows.Count == 0)
        {
            sb.AppendLine("Write-Error 'Otomatik kuruluma uygun hedef paket secilmedi. Manuel KB paketleri script listesine eklenmez.'");
            sb.AppendLine("exit 1");
            return sb.ToString();
        }

        var first = true;
        foreach (var target in UpdateTargetDefinition.All)
        {
            if (!targetRows.TryGetValue(target.Key, out var packages) || packages.Count == 0) continue;
            sb.AppendLine($"{(first ? "if" : "elseif")} ({target.ScriptCondition}) {{");
            sb.AppendLine("    $UpdateFiles = @(");
            var allFiles = packages.Concat(common).ToList();
            for (var i = 0; i < allFiles.Count; i++)
            {
                var relative = allFiles[i].RelativePath(useSubfolders).Replace('\\', '/').Replace('/', '\\');
                sb.Append($"        '{EscapePowerShell(relative)}'");
                if (i < allFiles.Count - 1) sb.Append(',');
                sb.AppendLine();
            }
            sb.AppendLine("    )");
            sb.AppendLine("}");
            first = false;
        }

        sb.AppendLine("else {");
        sb.AppendLine("    Write-Output '========================================'");
        sb.AppendLine("    Write-Output \"Sunucu: $env:COMPUTERNAME\"");
        sb.AppendLine("    Write-Output \"Hata: Desteklenmeyen veya taninmayan isletim sistemi ($osInfo / $displayVersion)\"");
        sb.AppendLine("    Write-Output '========================================'");
        sb.AppendLine("    exit 1");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("$computerName = $env:COMPUTERNAME");
        sb.AppendLine("$globalRestartPending = $false");
        sb.AppendLine("$results = @()");
        sb.AppendLine();
        sb.AppendLine("# 2. Asama: Dosyalari sirayla kur");
        sb.AppendLine("foreach ($File in $UpdateFiles) {");
        sb.AppendLine("    $FullPath = Join-Path -Path $BasePath -ChildPath $File");
        sb.AppendLine("    $statusMessage = ''");
        sb.AppendLine("    $exitCode = $null");
        sb.AppendLine();
        sb.AppendLine("    if (-not (Test-Path -LiteralPath $FullPath)) {");
        sb.AppendLine("        $statusMessage = 'Hata: Dosyaya erisilemedi.'");
        sb.AppendLine("        $results += [PSCustomObject]@{ Dosya = $File; Kod = 'N/A'; Durum = $statusMessage }");
        sb.AppendLine("        continue");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    try {");
        sb.AppendLine("        $extension = [System.IO.Path]::GetExtension($File).ToLowerInvariant()");
        sb.AppendLine("        if ($extension -eq '.msu') {");
        sb.AppendLine("            $process = Start-Process -FilePath 'wusa.exe' -ArgumentList @(\"`\"$FullPath`\"\", '/quiet', '/norestart') -Wait -PassThru");
        sb.AppendLine("            $exitCode = $process.ExitCode");
        sb.AppendLine("        }");
        sb.AppendLine("        elseif ($extension -eq '.exe') {");
        sb.AppendLine("            $process = Start-Process -FilePath $FullPath -ArgumentList '/q' -Wait -PassThru");
        sb.AppendLine("            $exitCode = $process.ExitCode");
        sb.AppendLine("        }");
        sb.AppendLine("        else {");
        sb.AppendLine("            $statusMessage = 'Atlandi: Desteklenmeyen uzanti.'");
        sb.AppendLine("            $results += [PSCustomObject]@{ Dosya = $File; Kod = 'N/A'; Durum = $statusMessage }");
        sb.AppendLine("            continue");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        switch ($exitCode) {");
        sb.AppendLine("            0       { $statusMessage = 'Basarili: Kuruldu (Restart gerekmiyor).' }");
        sb.AppendLine("            1641    { $statusMessage = 'Basarili: Kuruldu (Restart baslatildi).'; $globalRestartPending = $true }");
        sb.AppendLine("            3010    { $statusMessage = 'Basarili: Kuruldu (RESTART BEKLIYOR).'; $globalRestartPending = $true }");
        sb.AppendLine("            2359302 { $statusMessage = 'Atlandi: Zaten yuklu.' }");
        sb.AppendLine("            default { $statusMessage = \"Hata: Basarisiz (Kod: $exitCode).\" }");
        sb.AppendLine("        }");
        sb.AppendLine("        $results += [PSCustomObject]@{ Dosya = $File; Kod = $exitCode; Durum = $statusMessage }");
        sb.AppendLine("    }");
        sb.AppendLine("    catch {");
        sb.AppendLine("        $results += [PSCustomObject]@{ Dosya = $File; Kod = 'Hata'; Durum = \"Kritik Hata: $($_.Exception.Message)\" }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# 3. Asama: Kurulum ozeti");
        sb.AppendLine("$finalStatus = if ($globalRestartPending) { 'RESTART BEKLIYOR (Kontrollu restart bekleniyor)' } else { 'RESTART GEREKMIYOR' }");
        sb.AppendLine("Write-Output '========================================'");
        sb.AppendLine("Write-Output \"Sunucu: $computerName\"");
        sb.AppendLine("Write-Output \"Isletim Sistemi: $osInfo\"");
        sb.AppendLine("Write-Output \"Genel Durum: $finalStatus\"");
        sb.AppendLine("Write-Output '========================================'");
        sb.AppendLine("Write-Output ($results | Format-Table -AutoSize | Out-String -Width 4096)");
        return sb.ToString();
    }

    private static string NormalizeUnc(string path)
    {
        if (path.StartsWith("\\", StringComparison.Ordinal) && !path.StartsWith("\\\\", StringComparison.Ordinal))
            return "\\" + path;
        return path;
    }

    private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
