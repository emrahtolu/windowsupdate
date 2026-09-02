using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ServerUpdatePackager;

internal sealed class MainForm : Form
{
    private static readonly Color Navy = Color.FromArgb(16, 35, 63);
    private static readonly Color Blue = Color.FromArgb(39, 120, 240);
    private static readonly Color BlueHover = Color.FromArgb(28, 100, 214);
    private static readonly Color Surface = Color.White;
    private static readonly Color Canvas = Color.FromArgb(244, 247, 251);
    private static readonly Color Border = Color.FromArgb(220, 227, 237);
    private static readonly Color Muted = Color.FromArgb(91, 105, 124);
    private static readonly Color Success = Color.FromArgb(19, 142, 89);

    private readonly CatalogClient _catalog = new();
    private readonly BindingList<UpdateFileRow> _rows = [];
    private readonly Dictionary<string, CheckBox> _targetChecks = new();

    private NumericUpDown _year = null!;
    private ComboBox _month = null!;
    private CheckBox _includeMsrt = null!;
    private CheckBox _includeSsu = null!;
    private CheckBox _useSubfolders = null!;
    private TextBox _manualKb = null!;
    private TextBox _downloadRoot = null!;
    private TextBox _basePath = null!;
    private DataGridView _grid = null!;
    private RichTextBox _script = null!;
    private Button _searchButton = null!;
    private Button _manualSearchButton = null!;
    private Button _downloadButton = null!;
    private Button _cancelButton = null!;
    private Button _openFolderButton = null!;
    private Label _status = null!;
    private Label _summary = null!;
    private ProgressBar _progress = null!;
    private CancellationTokenSource? _operationCts;
    private string? _lastPackageFolder;

    public MainForm()
    {
        Text = "Server Update Packager — @emrahtolu";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 760);
        Size = new Size(1380, 880);
        BackColor = Canvas;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* Varsayılan ikon kullanılır. */ }
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        BuildUi();
        FormClosed += (_, _) => _catalog.Dispose();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Canvas,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Canvas,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(body, 0, 1);
        body.Controls.Add(BuildSidebar(), 0, 0);
        body.Controls.Add(BuildWorkspace(), 1, 0);
        root.Controls.Add(BuildStatusBar(), 0, 2);
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Navy, Margin = Padding.Empty };
        var badge = new Label
        {
            Text = "SU",
            ForeColor = Color.White,
            BackColor = Blue,
            Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(22, 17),
            Size = new Size(48, 44)
        };
        var title = new Label
        {
            Text = "Server Update Packager",
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(84, 12)
        };
        var subtitle = new Label
        {
            Text = "Microsoft Catalog paketlerini seçin, indirin ve kurulum scriptini tek adımda üretin  •  @emrahtolu",
            ForeColor = Color.FromArgb(190, 205, 226),
            Font = new Font("Segoe UI", 9.5F),
            AutoSize = true,
            Location = new Point(86, 47)
        };
        var official = new Label
        {
            Text = "MICROSOFT UPDATE CATALOG",
            ForeColor = Color.FromArgb(167, 191, 224),
            Font = new Font("Segoe UI Semibold", 8F),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(1160, 31)
        };
        panel.Resize += (_, _) => official.Left = panel.ClientSize.Width - official.Width - 24;
        panel.Controls.AddRange([badge, title, subtitle, official]);
        return panel;
    }

    private Control BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(22, 18, 22, 12),
            Margin = Padding.Empty
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Surface,
            Margin = Padding.Empty
        };
        sidebar.Controls.Add(flow);

        flow.Controls.Add(StepLabel("1", "DÖNEM"));
        flow.Controls.Add(FieldLabel("Yıl"));
        _year = new NumericUpDown
        {
            Minimum = 2020,
            Maximum = DateTime.Today.Year + 1,
            Value = DateTime.Today.Year,
            Width = 250,
            Height = 32,
            Font = new Font("Segoe UI", 10F),
            BorderStyle = BorderStyle.FixedSingle
        };
        flow.Controls.Add(_year);
        flow.Controls.Add(FieldLabel("Ay"));
        _month = new ComboBox
        {
            Width = 250,
            Height = 34,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10F),
            FlatStyle = FlatStyle.Flat
        };
        _month.Items.AddRange(CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.MonthNames.Take(12).Select(TitleCase).ToArray());
        _month.SelectedIndex = DateTime.Today.Month - 1;
        flow.Controls.Add(_month);
        flow.Controls.Add(Spacer(8));
        flow.Controls.Add(StepLabel("2", "HEDEF SİSTEMLER"));

        foreach (var target in UpdateTargetDefinition.All)
        {
            var check = new CheckBox
            {
                Text = target.DisplayName,
                Checked = target.DefaultSelected,
                Width = 250,
                Height = 30,
                ForeColor = Navy,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand
            };
            _targetChecks[target.Key] = check;
            flow.Controls.Add(check);
        }

        flow.Controls.Add(Spacer(7));
        flow.Controls.Add(StepLabel("3", "EK PAKETLER"));
        _includeMsrt = new CheckBox
        {
            Text = "Malicious Software Removal Tool\n(KB890830 x64)",
            Checked = true,
            Width = 250,
            Height = 48,
            ForeColor = Navy,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand
        };
        _includeSsu = new CheckBox
        {
            Text = "Ayrı SSU varsa ekle\n(Servicing Stack Update)",
            Checked = false,
            Width = 250,
            Height = 48,
            ForeColor = Navy,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand
        };
        _useSubfolders = new CheckBox
        {
            Text = "Hedef bazlı alt klasör oluştur",
            Checked = true,
            Width = 250,
            Height = 30,
            ForeColor = Navy,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand
        };
        _useSubfolders.CheckedChanged += (_, _) => RefreshScript();
        flow.Controls.Add(_includeMsrt);
        flow.Controls.Add(_includeSsu);
        flow.Controls.Add(_useSubfolders);
        flow.Controls.Add(Spacer(12));

        var note = new Label
        {
            Text = "Aylık aramada Preview, .NET ve Dynamic Update paketleri hariç tutulur. Manuel KB sonuçlarında yalnız x64 gösterilir.",
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.25F),
            Width = 250,
            Height = 64,
            Margin = new Padding(0, 12, 0, 0)
        };
        flow.Controls.Add(note);
        return sidebar;
    }

    private Control BuildWorkspace()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Canvas,
            Padding = new Padding(20, 15, 20, 15),
            Margin = Padding.Empty
        };
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 43));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 57));

        var resultHeader = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Canvas };
        resultHeader.Controls.Add(new Label
        {
            Text = "Bulunan paketler",
            ForeColor = Navy,
            Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 5)
        });
        resultHeader.Controls.Add(new Label
        {
            Text = "Manuel KB:",
            ForeColor = Muted,
            Font = new Font("Segoe UI Semibold", 8.5F),
            AutoSize = true,
            Location = new Point(178, 11)
        });
        _manualKb = new TextBox
        {
            Size = new Size(125, 28),
            Location = new Point(250, 5),
            Font = new Font("Segoe UI", 9.5F),
            BorderStyle = BorderStyle.FixedSingle
        };
        _manualKb.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            await SearchManualKbAsync();
        };
        _manualSearchButton = SecondaryButton("KB Ara ve Ekle", 132);
        _manualSearchButton.Height = 28;
        _manualSearchButton.Location = new Point(383, 5);
        _manualSearchButton.Margin = Padding.Empty;
        _manualSearchButton.Click += async (_, _) => await SearchManualKbAsync();
        resultHeader.Controls.Add(_manualKb);
        resultHeader.Controls.Add(_manualSearchButton);
        _searchButton = PrimaryButton("Aylık Paketleri Ara", 150);
        _searchButton.Height = 28;
        _searchButton.Location = new Point(523, 5);
        _searchButton.Click += async (_, _) => await SearchCatalogAsync();
        resultHeader.Controls.Add(_searchButton);
        _cancelButton = SecondaryButton("İşlemi İptal Et", 150);
        _cancelButton.Height = 28;
        _cancelButton.Location = new Point(523, 5);
        _cancelButton.Margin = Padding.Empty;
        _cancelButton.Visible = false;
        _cancelButton.ForeColor = Color.FromArgb(190, 55, 55);
        _cancelButton.Click += (_, _) => _operationCts?.Cancel();
        resultHeader.Controls.Add(_cancelButton);
        _summary = new Label
        {
            Text = "Henüz arama yapılmadı",
            ForeColor = Muted,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(760, 9)
        };
        resultHeader.Resize += (_, _) => _summary.Left = resultHeader.ClientSize.Width - _summary.Width;
        resultHeader.Controls.Add(_summary);
        host.Controls.Add(resultHeader, 0, 0);

        _grid = BuildGrid();
        host.Controls.Add(_grid, 0, 1);
        host.Controls.Add(BuildDownloadPanel(), 0, 2);
        host.Controls.Add(BuildScriptHeader(), 0, 3);

        _script = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 29, 43),
            ForeColor = Color.FromArgb(221, 230, 242),
            BorderStyle = BorderStyle.None,
            Font = new Font("Cascadia Mono", 9F),
            ReadOnly = false,
            DetectUrls = false,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            Margin = Padding.Empty,
            Text = "# Önce dönem ve hedef sistemleri seçip Catalog'da Ara düğmesine basın."
        };
        host.Controls.Add(_script, 0, 4);
        return host;
    }

    private DataGridView BuildGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Surface,
            BorderStyle = BorderStyle.None,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 38,
            RowTemplate = { Height = 34 },
            GridColor = Color.FromArgb(235, 239, 245),
            DataSource = _rows,
            Margin = Padding.Empty
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(233, 239, 248);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Navy;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.75F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(39, 51, 68);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(222, 235, 253);
        grid.DefaultCellStyle.SelectionForeColor = Navy;
        grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 254);

        grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(UpdateFileRow.Selected), HeaderText = "✓", Width = 42 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(UpdateFileRow.Target), HeaderText = "Hedef", Width = 126, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(UpdateFileRow.Kb), HeaderText = "KB", Width = 88, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(UpdateFileRow.PackageType), HeaderText = "Tür", Width = 110, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(UpdateFileRow.FileName), HeaderText = "Dosya adı", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 270, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(UpdateFileRow.DateText), HeaderText = "Tarih", Width = 88, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(UpdateFileRow.SizeText), HeaderText = "Boyut", Width = 78, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(UpdateFileRow.Status), HeaderText = "Durum", Width = 126, ReadOnly = true });
        grid.CellValueChanged += (_, e) => { if (e.ColumnIndex == 0) RefreshScript(); };
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellFormatting += (_, e) =>
        {
            if (grid.Columns[e.ColumnIndex].DataPropertyName == nameof(UpdateFileRow.Status) && e.Value is string value)
            {
                e.CellStyle!.ForeColor = value.Contains("Tamam", StringComparison.OrdinalIgnoreCase) ? Success
                    : value.Contains("Hata", StringComparison.OrdinalIgnoreCase) ? Color.Firebrick : Muted;
            }
        };
        return grid;
    }

    private Control BuildDownloadPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            BackColor = Surface,
            Margin = new Padding(0, 10, 0, 0),
            Padding = new Padding(12, 7, 12, 7)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        panel.Controls.Add(SmallFieldLabel("İndirme yolu"), 0, 0);
        _downloadRoot = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 1, 8, 2)
        };
        panel.Controls.Add(_downloadRoot, 1, 0);
        var browse = SecondaryButton("Gözat...", 104);
        browse.Dock = DockStyle.Fill;
        browse.Margin = new Padding(0, 0, 8, 2);
        browse.Click += (_, _) => SelectDownloadFolder();
        panel.Controls.Add(browse, 2, 0);

        _downloadButton = PrimaryButton("Seçilenleri İndir", 150);
        _downloadButton.Dock = DockStyle.Fill;
        _downloadButton.Margin = new Padding(0, 0, 0, 2);
        _downloadButton.Enabled = false;
        _downloadButton.Click += async (_, _) => await DownloadSelectedAsync();
        panel.SetRowSpan(_downloadButton, 2);
        panel.Controls.Add(_downloadButton, 3, 0);

        panel.Controls.Add(SmallFieldLabel("Kurulum yolu"), 0, 1);
        _basePath = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            Text = "DOSYA YOLUNU YAZIN",
            Margin = new Padding(0, 2, 8, 0)
        };
        _basePath.Leave += (_, _) => RefreshScript();
        panel.Controls.Add(_basePath, 1, 1);
        _openFolderButton = SecondaryButton("Klasörü Aç", 104);
        _openFolderButton.Dock = DockStyle.Fill;
        _openFolderButton.Margin = new Padding(0, 2, 8, 0);
        _openFolderButton.Enabled = false;
        _openFolderButton.Click += (_, _) => OpenLastFolder();
        panel.Controls.Add(_openFolderButton, 2, 1);
        return panel;
    }

    private Control BuildScriptHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Canvas, Margin = Padding.Empty };
        panel.Controls.Add(new Label
        {
            Text = "Güncel PowerShell kurulum scripti",
            ForeColor = Navy,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 14)
        });
        var refresh = SecondaryButton("Güncelle", 92);
        var save = SecondaryButton("PS1 Kaydet", 112);
        var copy = PrimaryButton("Scripti Kopyala", 136);
        refresh.Height = save.Height = copy.Height = 32;
        refresh.Top = save.Top = copy.Top = 8;
        copy.Anchor = save.Anchor = refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        refresh.Click += (_, _) => RefreshScript();
        save.Click += async (_, _) => await SaveScriptAsAsync();
        copy.Click += (_, _) => CopyScript();
        panel.Controls.AddRange([refresh, save, copy]);
        panel.Resize += (_, _) =>
        {
            copy.Left = panel.ClientSize.Width - copy.Width;
            save.Left = copy.Left - save.Width - 8;
            refresh.Left = save.Left - refresh.Width - 8;
        };
        return panel;
    }

    private Control BuildStatusBar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(230, 235, 242), Margin = Padding.Empty };
        _status = new Label
        {
            Text = "Hazır",
            ForeColor = Muted,
            AutoEllipsis = true,
            Location = new Point(14, 8),
            Size = new Size(800, 18)
        };
        _progress = new ProgressBar
        {
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Size = new Size(250, 12),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(1080, 10),
            Visible = false
        };
        panel.Resize += (_, _) => _progress.Left = panel.ClientSize.Width - _progress.Width - 16;
        panel.Controls.AddRange([_status, _progress]);
        return panel;
    }

    private async Task SearchCatalogAsync()
    {
        var selectedTargets = UpdateTargetDefinition.All.Where(x => _targetChecks[x.Key].Checked).ToList();
        if (selectedTargets.Count == 0)
        {
            MessageBox.Show(this, "En az bir Windows Server veya Windows 11 hedefi seçin.", "Seçim gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _operationCts = new CancellationTokenSource();
        SetBusy(true, "Microsoft Update Catalog sorgulanıyor...");
        _rows.Clear();
        _summary.Text = "Aranıyor...";
        var year = (int)_year.Value;
        var month = _month.SelectedIndex + 1;
        var warnings = new List<string>();

        try
        {
            var includeSsu = _includeSsu.Checked;
            var searchTasks = selectedTargets.Select(async target =>
            {
                var cumulativeTask = _catalog.FindMonthlyUpdateAsync(target, year, month, _operationCts.Token);
                var ssuTask = includeSsu
                    ? _catalog.FindMonthlySsuAsync(target, year, month, _operationCts.Token)
                    : Task.FromResult<CatalogEntry?>(null);
                await Task.WhenAll(cumulativeTask, ssuTask);
                return (Target: target, Cumulative: await cumulativeTask, Ssu: await ssuTask);
            }).ToList();
            var searchResults = await Task.WhenAll(searchTasks);

            foreach (var result in searchResults)
            {
                _operationCts.Token.ThrowIfCancellationRequested();
                if (includeSsu)
                {
                    if (result.Ssu is null)
                        warnings.Add($"{result.Target.DisplayName}: ayrı SSU yok; LCU ile birleşik olabilir.");
                    else
                        await AddResolvedEntryAsync(result.Target, result.Ssu, false, _operationCts.Token, isSsu: true);
                }

                if (result.Cumulative is null)
                {
                    warnings.Add($"{result.Target.DisplayName}: uygun {year:D4}-{month:D2} x64 CU bulunamadı.");
                }
                else
                {
                    await AddResolvedEntryAsync(result.Target, result.Cumulative, false, _operationCts.Token);
                }
            }

            if (_includeMsrt.Checked && _rows.Any(x => !x.IsCommon && !x.IsManual))
            {
                var msrt = await _catalog.FindMsrtAsync(year, month, _operationCts.Token);
                if (msrt is null) warnings.Add($"KB890830: {year:D4}-{month:D2} x64 sürümü bulunamadı.");
                else await AddResolvedEntryAsync(null, msrt, true, _operationCts.Token);
            }

            RefreshScript();
            _downloadButton.Enabled = _rows.Count > 0;
            _summary.Text = $"{_rows.Count} dosya • {_rows.Select(x => x.Kb).Distinct().Count()} KB";
            _status.Text = warnings.Count == 0
                ? "Paketler hazır. İndirme klasörünü seçip Seçilenleri İndir düğmesine basın."
                : string.Join("  |  ", warnings);

            if (_rows.Count == 0)
                MessageBox.Show(this, "Seçilen dönem için uygun paket bulunamadı. Dönemi ve internet erişimini kontrol edin.",
                    "Sonuç yok", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "İşlem iptal edildi.";
        }
        catch (Exception ex)
        {
            _status.Text = "Catalog sorgusu başarısız.";
            MessageBox.Show(this, FriendlyNetworkError(ex), "Catalog hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            _operationCts.Dispose();
            _operationCts = null;
        }
    }

    private async Task SearchManualKbAsync()
    {
        var match = Regex.Match(_manualKb.Text.Trim(), @"^(?:KB)?\s*(\d{6,8})$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            MessageBox.Show(this, "KB ve 6-8 haneli numarayı girin. Örnek: KB5039212",
                "Geçersiz KB", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var kb = "KB" + match.Groups[1].Value;
        _manualKb.Text = kb;
        _operationCts = new CancellationTokenSource();
        SetBusy(true, $"{kb} Catalog'da aranıyor...");

        try
        {
            for (var i = _rows.Count - 1; i >= 0; i--)
                if (_rows[i].IsManual) _rows.RemoveAt(i);

            var entries = await _catalog.FindByKbAsync(kb, _operationCts.Token);
            foreach (var entry in entries)
                await AddResolvedEntryAsync(null, entry, false, _operationCts.Token, isManual: true);

            RefreshScript();
            _downloadButton.Enabled = _rows.Count > 0;
            _summary.Text = $"{_rows.Count} dosya • {_rows.Select(x => x.Kb).Distinct().Count()} KB";
            _status.Text = entries.Count == 0
                ? $"{kb} için x64 paket bulunamadı."
                : $"{kb}: {entries.Count} Catalog kaydı listelendi. İndirmek istemediklerinizin işaretini kaldırın.";

            if (entries.Count == 0)
                MessageBox.Show(this, $"{kb} için x64 paket bulunamadı.", "Sonuç yok", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Manuel KB araması iptal edildi.";
        }
        catch (Exception ex)
        {
            _status.Text = "Manuel KB araması başarısız.";
            MessageBox.Show(this, FriendlyNetworkError(ex), "Catalog hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            _operationCts.Dispose();
            _operationCts = null;
        }
    }

    private async Task AddResolvedEntryAsync(
        UpdateTargetDefinition? target,
        CatalogEntry entry,
        bool common,
        CancellationToken cancellationToken,
        bool isSsu = false,
        bool isManual = false)
    {
        _status.Text = $"{(common ? "KB890830" : isManual ? entry.Kb : target!.DisplayName)} indirme bağlantıları hazırlanıyor...";
        var resolved = await _catalog.ResolveDownloadFilesAsync(entry.UpdateId, cancellationToken);
        var files = isManual
            ? resolved.ToList()
            : resolved.Where(x => Path.GetExtension(x.FileName).Equals(".msu", StringComparison.OrdinalIgnoreCase)
                               || Path.GetExtension(x.FileName).Equals(".exe", StringComparison.OrdinalIgnoreCase)).ToList();
        if (files.Count == 0) throw new InvalidOperationException(entry.Kb + " için desteklenen bir MSU/EXE paketi bulunamadı.");

        foreach (var file in files)
        {
            if (isManual && _rows.Any(x => x.IsManual && x.DownloadUrl.Equals(file.Url, StringComparison.OrdinalIgnoreCase)))
                continue;
            var kb = CatalogText.ExtractKb(file.FileName);
            if (kb == "KB?") kb = entry.Kb;
            var isCheckpoint = target?.SupportsCheckpoints == true && !isSsu
                && !kb.Equals(entry.Kb, StringComparison.OrdinalIgnoreCase);
            _rows.Add(new UpdateFileRow
            {
                Target = common ? "Tüm seçili sistemler" : isManual ? entry.Product : target!.DisplayName,
                TargetKey = common ? "common" : isManual ? "manual" : target!.Key,
                FolderName = common ? "Common" : isManual ? "Manual KB" : target!.FolderName,
                Kb = kb,
                Title = isCheckpoint ? $"{kb} - gerekli checkpoint" : entry.Title,
                FileName = file.FileName,
                DownloadUrl = file.Url,
                UpdateId = entry.UpdateId,
                LastUpdated = entry.LastUpdated,
                SizeText = files.Count == 1 ? entry.SizeText : "Catalog",
                InstallOrder = isSsu ? -100 + file.Order : common ? 10000 + file.Order : isManual ? 20000 + file.Order : file.Order,
                IsCommon = common,
                IsCheckpoint = isCheckpoint,
                IsSsu = isSsu,
                IsManual = isManual,
                Status = "Hazır"
            });
        }
    }

    private async Task DownloadSelectedAsync()
    {
        _grid.EndEdit();
        var selected = _rows.Where(x => x.Selected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "İndirilecek en az bir paket seçin.", "Seçim gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(_downloadRoot.Text))
        {
            SelectDownloadFolder();
            if (string.IsNullOrWhiteSpace(_downloadRoot.Text)) return;
        }

        var root = Path.GetFullPath(_downloadRoot.Text.Trim());
        var packageFolder = Path.Combine(root, $"SU_{(int)_year.Value:D4}_{_month.SelectedIndex + 1:D2}");
        _operationCts = new CancellationTokenSource();
        SetBusy(true, "İndirme hazırlanıyor...");
        _progress.Visible = true;

        try
        {
            Directory.CreateDirectory(packageFolder);
            for (var i = 0; i < selected.Count; i++)
            {
                var row = selected[i];
                var destination = Path.Combine(packageFolder, row.RelativePath(_useSubfolders.Checked));
                row.Status = "İndiriliyor";
                var fileProgress = new Progress<(long Received, long? Total)>(p =>
                {
                    var filePercent = p.Total is > 0 ? (double)p.Received / p.Total.Value : 0;
                    var totalPercent = ((i + filePercent) / selected.Count) * 100;
                    _progress.Value = Clamp((int)totalPercent, 0, 100);
                    _status.Text = $"{i + 1}/{selected.Count} • {row.FileName} • {FormatBytes(p.Received)}"
                        + (p.Total.HasValue ? $" / {FormatBytes(p.Total.Value)}" : "");
                });
                try
                {
                    row.Sha256 = await _catalog.DownloadFileAsync(row.DownloadUrl, destination, fileProgress, _operationCts.Token);
                    row.Status = "Tamam";
                }
                catch
                {
                    row.Status = "Hata";
                    throw;
                }
            }

            RefreshScript();
            var hasInstallScript = selected.Any(x => !x.IsManual);
            if (hasInstallScript)
                await WriteAllTextAsync(Path.Combine(packageFolder, "Install-Updates.ps1"), _script.Text,
                    new UTF8Encoding(true), _operationCts.Token);
            await WriteAllTextAsync(Path.Combine(packageFolder, "paket-manifest.csv"), BuildManifest(selected),
                new UTF8Encoding(true), _operationCts.Token);

            _lastPackageFolder = packageFolder;
            _openFolderButton.Enabled = true;
            _progress.Value = 100;
            _status.Text = hasInstallScript
                ? $"Tamamlandı: {selected.Count} dosya indirildi. Script ve manifest kaydedildi."
                : $"Tamamlandı: {selected.Count} manuel KB dosyası indirildi. Manifest kaydedildi.";
            MessageBox.Show(this,
                hasInstallScript
                    ? $"Paket hazır.\n\n{packageFolder}\n\nBu klasörün içeriğini Kurulum yolu alanındaki klasöre aynı yapıyla kopyalayın. Manuel KB sonuçları güvenlik amacıyla otomatik kurulum scriptine eklenmez."
                    : $"Manuel KB paketi hazır.\n\n{packageFolder}\n\nManuel KB sonuçları güvenlik amacıyla otomatik kurulum scriptine eklenmez.",
                "İndirme tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "İndirme iptal edildi. Yarım kalan .partial dosyası temizlendi.";
        }
        catch (Exception ex)
        {
            _status.Text = "İndirme başarısız.";
            MessageBox.Show(this, FriendlyNetworkError(ex), "İndirme hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            _progress.Visible = false;
            _operationCts.Dispose();
            _operationCts = null;
        }
    }

    private void RefreshScript()
    {
        if (_script is null || _basePath is null) return;
        if (_rows.Count == 0) return;
        _grid?.EndEdit();
        _script.Text = ScriptGenerator.Generate(_basePath.Text, _rows, _useSubfolders.Checked);
    }

    private void CopyScript()
    {
        if (string.IsNullOrWhiteSpace(_script.Text)) return;
        try
        {
            Clipboard.SetText(_script.Text);
            _status.Text = "PowerShell scripti panoya kopyalandı.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Pano erişilemedi: " + ex.Message, "Kopyalama hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task SaveScriptAsAsync()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "PowerShell scripti (*.ps1)|*.ps1|Tüm dosyalar (*.*)|*.*",
            FileName = $"Install-Updates-{(int)_year.Value:D4}-{_month.SelectedIndex + 1:D2}.ps1",
            Title = "Güncel scripti kaydet"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await WriteAllTextAsync(dialog.FileName, _script.Text, new UTF8Encoding(true), CancellationToken.None);
        _status.Text = "Script kaydedildi: " + dialog.FileName;
    }

    private string BuildManifest(IEnumerable<UpdateFileRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Target,KB,Type,FileName,RelativePath,LastUpdated,SHA256,DownloadUrl");
        foreach (var row in rows)
        {
            string[] values =
            [
                row.Target, row.Kb, row.PackageType, row.FileName, row.RelativePath(_useSubfolders.Checked),
                row.DateText, row.Sha256 ?? "", row.DownloadUrl
            ];
            sb.AppendLine(string.Join(",", values.Select(Csv)));
        }
        return sb.ToString();
    }

    private void SelectDownloadFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Güncelleme paketlerinin indirileceği ana klasörü seçin",
            ShowNewFolderButton = true,
            SelectedPath = Directory.Exists(_downloadRoot.Text) ? _downloadRoot.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _downloadRoot.Text = dialog.SelectedPath;
    }

    private void OpenLastFolder()
    {
        if (_lastPackageFolder is null || !Directory.Exists(_lastPackageFolder)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_lastPackageFolder}\"") { UseShellExecute = true });
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _searchButton.Enabled = !busy;
        _manualSearchButton.Enabled = !busy;
        _manualKb.Enabled = !busy;
        _downloadButton.Enabled = !busy && _rows.Count > 0;
        _cancelButton.Visible = busy;
        _year.Enabled = _month.Enabled = !busy;
        foreach (var check in _targetChecks.Values) check.Enabled = !busy;
        _includeMsrt.Enabled = _includeSsu.Enabled = _useSubfolders.Enabled = !busy;
        UseWaitCursor = busy;
        if (message is not null) _status.Text = message;
        if (!busy && _progress.Value != 100) _progress.Value = 0;
    }

    private static string FriendlyNetworkError(Exception ex)
    {
        var detail = ex.GetBaseException().Message;
        return "Microsoft Update Catalog ile işlem tamamlanamadı.\n\n"
             + "• İnternet/proxy erişimini kontrol edin.\n"
             + "• catalog.update.microsoft.com ve *.dl.delivery.mp.microsoft.com için TCP 443 çıkış iznini doğrulayın.\n"
             + "• Eski Catalog bağlantıları için download.windowsupdate.com adresine TCP 80/443 gerekebilir.\n"
             + "• Kurumsal proxy kimlik doğrulaması gerekiyorsa aracı oturum açmış kullanıcıyla çalıştırın.\n\n"
             + "Teknik ayrıntı: " + detail;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    private static string TitleCase(string value) => string.IsNullOrEmpty(value) ? value : char.ToUpper(value[0], CultureInfo.GetCultureInfo("tr-TR")) + value.Substring(1);

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    private static Task WriteAllTextAsync(string path, string text, Encoding encoding, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(path, text, encoding);
        }, cancellationToken);

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        ForeColor = Muted,
        Font = new Font("Segoe UI Semibold", 8.5F),
        Width = 250,
        Height = 24,
        TextAlign = ContentAlignment.BottomLeft,
        Margin = new Padding(0, 4, 0, 2)
    };

    private static Label SmallFieldLabel(string text) => new()
    {
        Text = text,
        ForeColor = Muted,
        Font = new Font("Segoe UI Semibold", 8.25F),
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = Padding.Empty
    };

    private static Control StepLabel(string number, string text)
    {
        var panel = new Panel { Width = 250, Height = 32, Margin = new Padding(0, 0, 0, 5) };
        panel.Controls.Add(new Label
        {
            Text = number,
            ForeColor = Color.White,
            BackColor = Blue,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(26, 26),
            Location = new Point(0, 2)
        });
        panel.Controls.Add(new Label
        {
            Text = text,
            ForeColor = Navy,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(36, 7)
        });
        return panel;
    }

    private static Panel Spacer(int height) => new() { Width = 250, Height = height, Margin = Padding.Empty };

    private static Button PrimaryButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 36,
            BackColor = Blue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = Padding.Empty,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = BlueHover;
        return button;
    }

    private static Button SecondaryButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 34,
            BackColor = Surface,
            ForeColor = Navy,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 8.75F),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 6, 0, 0),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 245, 253);
        return button;
    }
}
