namespace ServerUpdatePackager;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Microsoft Update Catalog TLS 1.2 ister. Tek dosyalık .NET Framework
        // uygulamalarında makine kayıt defteri varsayımlarına bağlı kalmayız.
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        System.Net.ServicePointManager.Expect100Continue = false;
        System.Net.ServicePointManager.DefaultConnectionLimit = 16;

        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = SelfTests.Run();
            return;
        }

        if (args.Contains("--catalog-test", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = RunCatalogTestAsync().GetAwaiter().GetResult();
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length >= 2 && args[0].Equals("--screenshot", StringComparison.OrdinalIgnoreCase))
        {
            using var form = new MainForm();
            form.Show();
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.GetFullPath(args[1]));
            form.Close();
            return;
        }

        Application.Run(new MainForm());
    }

    private static async Task<int> RunCatalogTestAsync()
    {
        try
        {
            using var client = new CatalogClient();
            foreach (var target in UpdateTargetDefinition.All)
            {
                var entry = await client.FindMonthlyUpdateAsync(target, 2026, 8, CancellationToken.None);
                if (entry is null) throw new InvalidOperationException(target.DisplayName + " sonucu yok");
                var files = await client.ResolveDownloadFilesAsync(entry.UpdateId, CancellationToken.None);
                Console.WriteLine($"{target.DisplayName}: {entry.Kb} -> {string.Join(", ", files.Select(x => x.FileName))}");
            }
            var msrt = await client.FindMsrtAsync(2026, 8, CancellationToken.None)
                       ?? throw new InvalidOperationException("MSRT sonucu yok");
            var msrtFiles = await client.ResolveDownloadFilesAsync(msrt.UpdateId, CancellationToken.None);
            Console.WriteLine($"MSRT: {string.Join(", ", msrtFiles.Select(x => x.FileName))}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
