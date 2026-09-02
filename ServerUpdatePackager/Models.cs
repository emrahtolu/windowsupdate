using System.ComponentModel;

namespace ServerUpdatePackager;

internal sealed class UpdateTargetDefinition
{
    public UpdateTargetDefinition(
        string key,
        string version,
        string displayName,
        string folderName,
        string queryTemplate,
        string ssuQueryTemplate,
        string scriptCondition,
        bool defaultSelected,
        bool supportsCheckpoints,
        Func<CatalogEntry, bool> entryMatcher)
    {
        Key = key;
        Version = version;
        DisplayName = displayName;
        FolderName = folderName;
        QueryTemplate = queryTemplate;
        SsuQueryTemplate = ssuQueryTemplate;
        ScriptCondition = scriptCondition;
        DefaultSelected = defaultSelected;
        SupportsCheckpoints = supportsCheckpoints;
        EntryMatcher = entryMatcher;
    }

    public string Key { get; }
    public string Version { get; }
    public string DisplayName { get; }
    public string FolderName { get; }
    public string QueryTemplate { get; }
    public string SsuQueryTemplate { get; }
    public string ScriptCondition { get; }
    public bool DefaultSelected { get; }
    public bool SupportsCheckpoints { get; }
    public Func<CatalogEntry, bool> EntryMatcher { get; }

    public string Query(int year, int month) => string.Format(QueryTemplate, $"{year:D4}-{month:D2}");
    public string SsuQuery(int year, int month) => string.Format(SsuQueryTemplate, $"{year:D4}-{month:D2}");

    public static readonly IReadOnlyList<UpdateTargetDefinition> All =
    [
        new(
            "server-2016", "2016", "Windows Server 2016", "Server 2016",
            "{0} Cumulative Update for Windows Server 2016 for x64-based Systems",
            "{0} Servicing Stack Update for Windows Server 2016 for x64-based Systems",
            "$osInfo -match 'Server 2016'", true, false,
            e => e.Product.Contains("Windows Server 2016", StringComparison.OrdinalIgnoreCase)),
        new(
            "server-2019", "2019", "Windows Server 2019", "Server 2019",
            "{0} Cumulative Update for Windows Server 2019 for x64-based Systems",
            "{0} Servicing Stack Update for Windows Server 2019 for x64-based Systems",
            "$osInfo -match 'Server 2019'", true, false,
            e => e.Product.Contains("Windows Server 2019", StringComparison.OrdinalIgnoreCase)),
        new(
            "server-2022", "2022", "Windows Server 2022", "Server 2022",
            "{0} Cumulative Update for Microsoft server operating system version 21H2 for x64-based Systems",
            "{0} Servicing Stack Update for Microsoft server operating system version 21H2 for x64-based Systems",
            "$osInfo -match 'Server 2022'", true, false,
            e => (e.Product.Contains("21H2", StringComparison.OrdinalIgnoreCase)
                  || e.Title.Contains("21H2", StringComparison.OrdinalIgnoreCase))
                 && !e.Title.Contains("Windows 11", StringComparison.OrdinalIgnoreCase)),
        new(
            "server-2025", "2025", "Windows Server 2025", "Server 2025",
            "{0} Cumulative Update for Microsoft server operating system version 24H2 for x64-based Systems",
            "{0} Servicing Stack Update for Microsoft server operating system version 24H2 for x64-based Systems",
            "$osInfo -match 'Server 2025'", true, true,
            e => (e.Product.Contains("24H2", StringComparison.OrdinalIgnoreCase)
                  || e.Title.Contains("server operating system version 24H2", StringComparison.OrdinalIgnoreCase))
                 && !e.Title.Contains("Windows 11", StringComparison.OrdinalIgnoreCase)),
        new(
            "windows-11-23h2", "23H2", "Windows 11 23H2", "Windows 11 23H2",
            "{0} Cumulative Update for Windows 11 Version 23H2 for x64-based Systems",
            "{0} Servicing Stack Update for Windows 11 Version 23H2 for x64-based Systems",
            "$osInfo -match 'Windows 11' -and $displayVersion -eq '23H2'", false, false,
            e => e.Title.Contains("Windows 11", StringComparison.OrdinalIgnoreCase)
                 && e.Title.Contains("23H2", StringComparison.OrdinalIgnoreCase)),
        new(
            "windows-11-24h2", "24H2", "Windows 11 24H2", "Windows 11 24H2",
            "{0} Cumulative Update for Windows 11 Version 24H2 for x64-based Systems",
            "{0} Servicing Stack Update for Windows 11 Version 24H2 for x64-based Systems",
            "$osInfo -match 'Windows 11' -and $displayVersion -eq '24H2'", false, true,
            e => e.Title.Contains("Windows 11", StringComparison.OrdinalIgnoreCase)
                 && e.Title.Contains("24H2", StringComparison.OrdinalIgnoreCase)),
        new(
            "windows-11-25h2", "25H2", "Windows 11 25H2", "Windows 11 25H2",
            "{0} Cumulative Update for Windows 11 Version 25H2 for x64-based Systems",
            "{0} Servicing Stack Update for Windows 11 Version 25H2 for x64-based Systems",
            "$osInfo -match 'Windows 11' -and $displayVersion -eq '25H2'", false, true,
            e => e.Title.Contains("Windows 11", StringComparison.OrdinalIgnoreCase)
                 && e.Title.Contains("25H2", StringComparison.OrdinalIgnoreCase)),
        new(
            "windows-11-26h1", "26H1", "Windows 11 26H1", "Windows 11 26H1",
            "{0} Cumulative Update for Windows 11 Version 26H1 for x64-based Systems",
            "{0} Servicing Stack Update for Windows 11 Version 26H1 for x64-based Systems",
            "$osInfo -match 'Windows 11' -and $displayVersion -eq '26H1'", false, true,
            e => e.Title.Contains("Windows 11", StringComparison.OrdinalIgnoreCase)
                 && e.Title.Contains("26H1", StringComparison.OrdinalIgnoreCase))
    ];
}

internal sealed class CatalogEntry
{
    public string UpdateId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Product { get; set; } = "";
    public string Classification { get; set; } = "";
    public DateTime? LastUpdated { get; set; }
    public string SizeText { get; set; } = "";
    public long? SizeBytes { get; set; }
    public string Kb => CatalogText.ExtractKb(Title);
}

internal sealed class CatalogFile
{
    public CatalogFile(string url, string fileName, int order) { Url = url; FileName = fileName; Order = order; }
    public string Url { get; }
    public string FileName { get; }
    public int Order { get; }
}

internal sealed class UpdateFileRow : INotifyPropertyChanged
{
    private bool _selected = true;
    private string _status = "Hazır";

    public bool Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(nameof(Selected)); }
    }

    public string Target { get; set; } = "";
    public string TargetKey { get; set; } = "";
    public string FolderName { get; set; } = "";
    public string Kb { get; set; } = "";
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string UpdateId { get; set; } = "";
    public DateTime? LastUpdated { get; set; }
    public string SizeText { get; set; } = "—";
    public int InstallOrder { get; set; }
    public bool IsCommon { get; set; }
    public bool IsCheckpoint { get; set; }
    public bool IsSsu { get; set; }
    public bool IsManual { get; set; }
    public string? Sha256 { get; set; }

    public string DateText => LastUpdated?.ToString("dd.MM.yyyy") ?? "—";
    public string PackageType => IsManual ? "Manuel KB" : IsCommon ? "MSRT" : IsSsu ? "SSU" : IsCheckpoint ? "Checkpoint" : "Toplu güncelleme";

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(nameof(Status)); }
    }

    public string RelativePath(bool useSubfolders)
    {
        if (!useSubfolders) return FileName;
        var folder = IsManual ? "Manual KB" : IsCommon ? "Common" : FolderName;
        return Path.Combine(folder, FileName);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new(name));
}

internal static class CatalogText
{
    public static string ExtractKb(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text ?? "", @"KB\s*(\d{6,8})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? "KB" + match.Groups[1].Value : "KB?";
    }
}
