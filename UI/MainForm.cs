using System.Diagnostics;
using System.Globalization;
using HaarWaveletLab.App;
using HaarWaveletLab.Core;
using HaarWaveletLab.Export;
using HaarWaveletLab.Imaging;
using HaarWaveletLab.Models;
using HaarWaveletLab.Reporting;

namespace HaarWaveletLab.UI;

/// <summary>Главное окно программы: изображение, матрицы, эксперименты и отчёт.</summary>
public sealed class MainForm : Form
{
    private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    private readonly CommandLineOptions _options;
    private Bitmap? _sourceBitmap;
    private string? _sourcePath;
    private TestImageKind? _testKind;
    private string _sourceName = "Тестовая таблица";
    private string _sourceDescription = "";
    private int _imageSize = 256;
    private int _order = 5;
    private LabImage? _image;
    private HaarDecomposition? _decomposition;
    private RemovalExperiment? _baseline;
    private IReadOnlyList<RemovalExperiment> _singleExperiments = Array.Empty<RemovalExperiment>();
    private readonly HashSet<SubbandKind> _removedKinds = new();
    private bool _suspendEvents;
    private string? _startupError;
    private bool _initialized;

    private readonly MenuStrip _menu = new();
    private readonly ToolStrip _toolbar = new();
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusLabel = new("Готово");
    private readonly ToolStripStatusLabel _infoLabel = new("") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly TableLayoutPanel _content = new();
    private readonly TabControl _centerTabs = new();
    private readonly TabControl _rightTabs = new();
    private readonly ToolStripComboBox _testCombo = new();
    private readonly ToolStripComboBox _sizeCombo = new();
    private readonly ToolStripComboBox _orderCombo = new();
    private readonly Dictionary<SubbandKind, ToolStripButton> _removeButtons = new();
    private readonly Dictionary<SubbandKind, ToolStripMenuItem> _removeMenuItems = new();
    private readonly Dictionary<SubbandKind, ImageCard> _bandCards = new();
    private readonly ToolStripMenuItem[] _orderMenuItems = new ToolStripMenuItem[5];
    private readonly ImageCard _cardOriginal;
    private readonly ImageCard _cardRestored;
    private readonly ImageCard _cardPyramid;
    private readonly ImageCard _cardDifference;
    private readonly RichTextBox _analysis = new();
    private readonly RichTextBox _theory = new();
    private readonly DataGridView _grid = new();
    private readonly Label _tablesInfo = new();
    private readonly Panel _busyPanel = new();
    private readonly Label _busyLabel = new();

    public MainForm(CommandLineOptions options)
    {
        _options = options;
        Text = "Вейвлет-преобразование Хаара — лабораторная работа №3";
        Font = Theme.UiFont;
        BackColor = Theme.Background;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 720);
        KeyPreview = true;

        _cardOriginal = new ImageCard("Исходное изображение");
        _cardRestored = new ImageCard("Восстановленное изображение");
        _cardPyramid = new ImageCard("Пирамида коэффициентов");
        _cardDifference = new ImageCard("Карта разности");

        BuildUi();
        ApplyStartupOptions();

        Shown += OnShown;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var area = Screen.FromControl(this).WorkingArea;
        int width = Math.Min(1620, area.Width - 40);
        int height = Math.Min(1000, area.Height - 40);
        Size = new Size(Math.Max(MinimumSize.Width, width), Math.Max(MinimumSize.Height, height));
        CenterToScreen();
    }

    private void OnShown(object? sender, EventArgs e)
    {
        if (_initialized) return;
        _initialized = true;

        if (_startupError is not null)
        {
            MessageBox.Show(this, _startupError, "Изображение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _startupError = null;
        }

        RebuildAll();
    }

    // ==================== ПОСТРОЕНИЕ ИНТЕРФЕЙСА ====================

    private void BuildUi()
    {
        BuildMenu();
        BuildToolbar();
        BuildStatus();
        BuildContent();
        BuildOverlay();

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Theme.Background
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.Controls.Add(_menu, 0, 0);
        shell.Controls.Add(_toolbar, 0, 1);
        shell.Controls.Add(_content, 0, 2);
        shell.Controls.Add(_status, 0, 3);

        Controls.Add(shell);
        Controls.Add(_busyPanel);
        _busyPanel.BringToFront();
    }

    private void BuildMenu()
    {
        _menu.Dock = DockStyle.Fill;
        _menu.AutoSize = true;
        _menu.BackColor = Theme.Surface;
        _menu.Font = Theme.UiFont;
        _menu.Padding = new Padding(6, 2, 0, 2);
        _menu.Renderer = new ToolStripProfessionalRenderer(new LabColorTable()) { RoundedEdges = false };

        var fileMenu = new ToolStripMenuItem("Файл");
        fileMenu.DropDownItems.Add(Item("Открыть изображение…", Keys.Control | Keys.O, (_, _) => OpenImage()));
        var tests = new ToolStripMenuItem("Тестовое изображение");
        foreach (var kind in TestImagesInfo.All)
        {
            var captured = kind;
            tests.DropDownItems.Add(new ToolStripMenuItem(kind.Title(), null, (_, _) => SetTestImage(captured)));
        }

        fileMenu.DropDownItems.Add(tests);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Экспорт CSV…", null, async (_, _) => await ExportCsvAsync()));
        fileMenu.DropDownItems.Add(Item("Сохранить HTML-отчёт…", Keys.Control | Keys.R,
            async (_, _) => await GenerateReportAsync()));
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Выход", null, (_, _) => Close()));

        var orderMenu = new ToolStripMenuItem("Разложение");
        var orderSub = new ToolStripMenuItem("Порядок разложения");
        for (int i = 1; i <= 5; i++)
        {
            int captured = i;
            var item = new ToolStripMenuItem($"Порядок {i}", null, (_, _) => SetOrder(captured)) { CheckOnClick = false };
            _orderMenuItems[i - 1] = item;
            orderSub.DropDownItems.Add(item);
        }

        orderMenu.DropDownItems.Add(orderSub);
        orderMenu.DropDownItems.Add(Item("Пересчитать", Keys.F5, (_, _) => RebuildAll()));

        var removeMenu = new ToolStripMenuItem("Убрать матрицу");
        for (int i = 0; i < Subbands.All.Length; i++)
        {
            var kind = Subbands.All[i];
            var item = new ToolStripMenuItem($"{kind.Code()} — {kind.Role()}", null,
                (_, _) => ToggleRemoval(kind, !_removedKinds.Contains(kind)))
            {
                ShortcutKeys = Keys.Control | (Keys.D1 + i),
                ShowShortcutKeys = true
            };
            _removeMenuItems[kind] = item;
            removeMenu.DropDownItems.Add(item);
        }

        orderMenu.DropDownItems.Add(removeMenu);
        orderMenu.DropDownItems.Add(Item("Сбросить удаление", Keys.Control | Keys.D0, (_, _) => ResetRemovals()));

        var helpMenu = new ToolStripMenuItem("Справка");
        helpMenu.DropDownItems.Add(Item("Теория и контрольные вопросы", Keys.F1, (_, _) => ShowTheory()));
        helpMenu.DropDownItems.Add(new ToolStripMenuItem("О программе", null, (_, _) => ShowAbout()));

        _menu.Items.Add(fileMenu);
        _menu.Items.Add(orderMenu);
        _menu.Items.Add(helpMenu);
    }

    private static ToolStripMenuItem Item(string text, Keys shortcut, EventHandler handler) => new(text, null, handler)
    {
        ShortcutKeys = shortcut,
        ShowShortcutKeys = true
    };

    private void BuildToolbar()
    {
        _toolbar.Dock = DockStyle.Fill;
        _toolbar.AutoSize = true;
        _toolbar.GripStyle = ToolStripGripStyle.Hidden;
        _toolbar.BackColor = Theme.Surface;
        _toolbar.Font = Theme.UiFont;
        _toolbar.Padding = new Padding(6, 3, 6, 3);
        _toolbar.Renderer = new ToolStripProfessionalRenderer(new LabColorTable()) { RoundedEdges = false };

        _toolbar.Items.Add(Button("Открыть…", (_, _) => OpenImage(), "Открыть файл изображения (Ctrl+O)"));

        _testCombo.AutoSize = false;
        _testCombo.Width = 168;
        _testCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _testCombo.FlatStyle = FlatStyle.Flat;
        foreach (var kind in TestImagesInfo.All) _testCombo.Items.Add(kind.Title());
        _testCombo.SelectedIndexChanged += (_, _) => OnTestComboChanged();

        _sizeCombo.AutoSize = false;
        _sizeCombo.Width = 70;
        _sizeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _sizeCombo.FlatStyle = FlatStyle.Flat;
        foreach (int size in ImageLoader.SupportedSizes) _sizeCombo.Items.Add(size.ToString(CI));
        _sizeCombo.SelectedIndexChanged += (_, _) => OnSizeComboChanged();

        _orderCombo.AutoSize = false;
        _orderCombo.Width = 52;
        _orderCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _orderCombo.FlatStyle = FlatStyle.Flat;
        for (int i = 1; i <= 5; i++) _orderCombo.Items.Add(i.ToString(CI));
        _orderCombo.SelectedIndexChanged += (_, _) => OnOrderComboChanged();

        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(Label("Тест:"));
        _toolbar.Items.Add(_testCombo);
        _toolbar.Items.Add(Label("N:"));
        _toolbar.Items.Add(_sizeCombo);
        _toolbar.Items.Add(Label("Порядок:"));
        _toolbar.Items.Add(_orderCombo);
        _toolbar.Items.Add(Button("Пересчитать", (_, _) => RebuildAll(), "Пересчитать разложение (F5)"));
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(Label("Убрать:"));

        foreach (var kind in Subbands.All)
        {
            var captured = kind;
            var button = new ToolStripButton(kind.Code())
            {
                CheckOnClick = true,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = kind.Title() + ": " + kind.Meaning(),
                Padding = new Padding(4, 0, 4, 0),
                Margin = new Padding(1, 0, 1, 0)
            };
            button.Click += (_, _) => ToggleRemoval(captured, button.Checked);
            _removeButtons[kind] = button;
            _toolbar.Items.Add(button);
        }

        _toolbar.Items.Add(Button("Сброс", (_, _) => ResetRemovals(), "Вернуть все матрицы (Ctrl+0)"));
        _toolbar.Items.Add(new ToolStripSeparator());
        _toolbar.Items.Add(Button("HTML-отчёт", async (_, _) => await GenerateReportAsync(), "Сформировать HTML-отчёт (Ctrl+R)"));
        _toolbar.Items.Add(Button("CSV", async (_, _) => await ExportCsvAsync(), "Экспортировать таблицы в CSV"));
        _toolbar.Items.Add(Button("Теория", (_, _) => ShowTheory(), "Обучение: теория и контрольные вопросы (F1)"));
    }

    private static ToolStripButton Button(string text, EventHandler handler, string tooltip) => new(text, null, handler)
    {
        DisplayStyle = ToolStripItemDisplayStyle.Text,
        ToolTipText = tooltip,
        Padding = new Padding(6, 0, 6, 0),
        Margin = new Padding(1, 0, 1, 0)
    };

    private static ToolStripLabel Label(string text) => new(text)
    {
        ForeColor = Theme.Muted,
        Margin = new Padding(6, 0, 3, 0)
    };

    private void BuildStatus()
    {
        _status.Dock = DockStyle.Fill;
        _status.AutoSize = true;
        _status.BackColor = Theme.Surface;
        _status.SizingGrip = false;
        _status.Font = Theme.SmallFont;
        _status.Padding = new Padding(8, 2, 8, 2);
        _statusLabel.ForeColor = Theme.Text;
        _infoLabel.ForeColor = Theme.Muted;
        _status.Items.Add(_statusLabel);
        _status.Items.Add(_infoLabel);
    }

    private void BuildContent()
    {
        _content.Dock = DockStyle.Fill;
        _content.Padding = new Padding(12, 10, 12, 10);
        _content.BackColor = Theme.Background;
        _content.ColumnCount = 3;
        _content.RowCount = 1;
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 392));
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 442));
        _content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 10, 0),
            BackColor = Theme.Background
        };
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _cardOriginal.Dock = DockStyle.Fill;
        _cardOriginal.Margin = new Padding(0, 0, 0, 6);
        _cardRestored.Dock = DockStyle.Fill;
        _cardRestored.Margin = new Padding(0, 6, 0, 0);
        _cardOriginal.Canvas.Interpolation = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        _cardRestored.Canvas.Interpolation = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        left.Controls.Add(_cardOriginal, 0, 0);
        left.Controls.Add(_cardRestored, 0, 1);
        _content.Controls.Add(left, 0, 0);

        _centerTabs.Dock = DockStyle.Fill;
        _centerTabs.Font = Theme.UiFont;
        _centerTabs.Margin = new Padding(0);
        _centerTabs.Padding = new Point(14, 6);

        var pyramidPage = new TabPage("Пирамида") { BackColor = Theme.Surface, Padding = new Padding(8) };
        _cardPyramid.Dock = DockStyle.Fill;
        _cardPyramid.Canvas.Interpolation = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        pyramidPage.Controls.Add(_cardPyramid);

        var bandsPage = new TabPage("Матрицы (4)") { BackColor = Theme.Surface, Padding = new Padding(6) };
        var bandsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Theme.Surface
        };
        bandsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bandsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bandsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        bandsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        for (int i = 0; i < Subbands.All.Length; i++)
        {
            var kind = Subbands.All[i];
            var card = new ImageCard(kind.Code()) { Dock = DockStyle.Fill, Margin = new Padding(4) };
            card.Canvas.Interpolation = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            _bandCards[kind] = card;
            bandsGrid.Controls.Add(card, i % 2, i / 2);
        }

        bandsPage.Controls.Add(bandsGrid);

        var differencePage = new TabPage("Разность") { BackColor = Theme.Surface, Padding = new Padding(8) };
        _cardDifference.Dock = DockStyle.Fill;
        _cardDifference.Canvas.Interpolation = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        differencePage.Controls.Add(_cardDifference);

        _centerTabs.TabPages.Add(pyramidPage);
        _centerTabs.TabPages.Add(bandsPage);
        _centerTabs.TabPages.Add(differencePage);
        _content.Controls.Add(_centerTabs, 1, 0);

        _rightTabs.Dock = DockStyle.Fill;
        _rightTabs.Font = Theme.UiFont;
        _rightTabs.Margin = new Padding(10, 0, 0, 0);
        _rightTabs.Padding = new Point(14, 6);

        _analysis.ReadOnly = true;
        _analysis.BorderStyle = BorderStyle.None;
        _analysis.BackColor = Theme.Surface;
        _analysis.ForeColor = Theme.Text;
        _analysis.Font = Theme.TheoryBody;
        _analysis.Dock = DockStyle.Fill;
        _analysis.DetectUrls = false;
        _analysis.ScrollBars = RichTextBoxScrollBars.Vertical;
        _analysis.WordWrap = true;
        _analysis.HideSelection = false;
        _analysis.ShortcutsEnabled = true;

        var analysisPage = new TabPage("Анализ") { BackColor = Theme.Surface, Padding = new Padding(12) };
        analysisPage.Controls.Add(_analysis);

        var tablesPage = new TabPage("Таблицы") { BackColor = Theme.Surface, Padding = new Padding(8) };
        var tablesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Surface
        };
        tablesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tablesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tablesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var tablesNote = _tablesInfo;
        tablesNote.Dock = DockStyle.Fill;
        tablesNote.AutoSize = true;
        tablesNote.Font = Theme.SmallFont;
        tablesNote.ForeColor = Theme.Muted;
        tablesNote.Padding = new Padding(2, 2, 2, 8);
        tablesNote.Text = "PSNR и SSIM после удаления одной матрицы. Зелёный — потери почти нет, красный — сильная деградация.";        ConfigureGrid();
        tablesLayout.Controls.Add(tablesNote, 0, 0);
        tablesLayout.Controls.Add(_grid, 0, 1);
        tablesPage.Controls.Add(tablesLayout);

        _theory.ReadOnly = true;
        _theory.BorderStyle = BorderStyle.None;
        _theory.BackColor = Theme.Surface;
        _theory.ForeColor = Theme.Text;
        _theory.Font = Theme.TheoryBody;
        _theory.Dock = DockStyle.Fill;
        _theory.DetectUrls = false;
        _theory.ScrollBars = RichTextBoxScrollBars.Vertical;
        _theory.WordWrap = true;
        var theoryPage = new TabPage("Теория") { BackColor = Theme.Surface, Padding = new Padding(12) };
        theoryPage.Controls.Add(_theory);
        RichText.RenderTheory(_theory);

        _rightTabs.TabPages.Add(analysisPage);
        _rightTabs.TabPages.Add(tablesPage);
        _rightTabs.TabPages.Add(theoryPage);
        _content.Controls.Add(_rightTabs, 2, 0);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Theme.Surface;
        _grid.BorderStyle = BorderStyle.None;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AllowUserToResizeColumns = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ColumnHeadersHeight = 40;
        _grid.RowTemplate.Height = 31;
        _grid.EnableHeadersVisualStyles = false;
        _grid.GridColor = Theme.Border;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(238, 243, 251),
            ForeColor = Theme.AccentDark,
            Font = Theme.UiFontBold,
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            SelectionBackColor = Color.FromArgb(238, 243, 251)
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            Font = Theme.UiFont,
            SelectionBackColor = Color.FromArgb(219, 234, 254),
            SelectionForeColor = Theme.Text,
            Padding = new Padding(3, 0, 4, 0),
            Alignment = DataGridViewContentAlignment.MiddleRight
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Variant",
            HeaderText = "Вариант",
            FillWeight = 36,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Psnr", HeaderText = "PSNR, дБ", FillWeight = 24 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ssim", HeaderText = "SSIM", FillWeight = 18 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ratio", HeaderText = "Сжатие", FillWeight = 22 });
    }

    private void BuildOverlay()
    {
        _busyPanel.Dock = DockStyle.Fill;
        _busyPanel.BackColor = Color.FromArgb(235, 244, 246, 251);
        _busyPanel.Visible = false;
        _busyLabel.AutoSize = true;
        _busyLabel.Font = Theme.HeroFont;
        _busyLabel.ForeColor = Theme.AccentDark;
        _busyLabel.BackColor = Color.Transparent;
        _busyLabel.Text = "Формирование отчёта…";
        _busyPanel.Controls.Add(_busyLabel);
        _busyPanel.Resize += (_, _) => CenterBusyLabel();
    }

    private void CenterBusyLabel()
    {
        _busyLabel.Location = new Point(
            Math.Max(0, (_busyPanel.Width - _busyLabel.Width) / 2),
            Math.Max(0, (_busyPanel.Height - _busyLabel.Height) / 2));
    }

    // ==================== ЗАГРУЗКА ДАННЫХ ====================

    private void ApplyStartupOptions()
    {
        _imageSize = ImageLoader.NormalizeSize(_options.Size);
        _order = Math.Clamp(_options.Order, 1, 5);

        if (!string.IsNullOrWhiteSpace(_options.ImagePath))
        {
            try
            {
                _sourceBitmap = ImageLoader.LoadBitmap(_options.ImagePath);
                _sourcePath = Path.GetFullPath(_options.ImagePath);
                _sourceName = Path.GetFileName(_sourcePath);
                _sourceDescription = "файл: " + _sourcePath;
                _testKind = null;
            }
            catch (Exception ex)
            {
                _startupError = "Не удалось открыть изображение:\n" + _options.ImagePath + "\n\n" + ex.Message;
            }
        }

        if (_sourceBitmap is null)
        {
            _testKind = _options.TestImage ?? TestImageKind.TestCard;
            _sourceName = _testKind.Value.Title();
            _sourceDescription = _testKind.Value.Description();
            _sourceBitmap = TestImages.Create(_testKind.Value, _imageSize);
        }

        foreach (var kind in _options.Removed) _removedKinds.Add(kind);

        _suspendEvents = true;
        _testCombo.SelectedIndex = _testKind.HasValue
            ? Array.IndexOf(TestImagesInfo.All, _testKind.Value)
            : -1;
        _sizeCombo.SelectedItem = _imageSize.ToString(CI);
        _orderCombo.SelectedItem = _order.ToString(CI);
        _suspendEvents = false;

        ApplyStartupTab();
        SyncOrderUi();
        SyncRemovalUi();
    }

    private void ApplyStartupTab()
    {
        switch (_options.StartupTab)
        {
            case "pyramid": _centerTabs.SelectedIndex = 0; break;
            case "matrices": _centerTabs.SelectedIndex = 1; break;
            case "difference": _centerTabs.SelectedIndex = 2; break;
            case "analysis": _rightTabs.SelectedIndex = 0; break;
            case "tables": _rightTabs.SelectedIndex = 1; break;
            case "theory": _rightTabs.SelectedIndex = 2; break;
        }
    }

    private void OpenImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Открыть изображение",
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Все файлы|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var bitmap = ImageLoader.LoadBitmap(dialog.FileName);
            _sourceBitmap?.Dispose();
            _sourceBitmap = bitmap;
            _sourcePath = Path.GetFullPath(dialog.FileName);
            _sourceName = Path.GetFileName(dialog.FileName);
            _sourceDescription = "файл: " + _sourcePath;
            _testKind = null;
            _removedKinds.Clear();
            _suspendEvents = true;
            _testCombo.SelectedIndex = -1;
            _suspendEvents = false;
            SyncRemovalUi();
            RebuildAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось открыть изображение:\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetTestImage(TestImageKind kind)
    {
        _testKind = kind;
        _sourcePath = null;
        _sourceName = kind.Title();
        _sourceDescription = kind.Description();
        _sourceBitmap?.Dispose();
        _sourceBitmap = TestImages.Create(kind, _imageSize);
        _removedKinds.Clear();

        _suspendEvents = true;
        _testCombo.SelectedIndex = Array.IndexOf(TestImagesInfo.All, kind);
        _suspendEvents = false;

        SyncRemovalUi();
        RebuildAll();
    }

    private void SetSize(int size)
    {
        size = ImageLoader.NormalizeSize(size);
        if (size == _imageSize) return;

        _imageSize = size;
        if (_testKind is TestImageKind kind)
        {
            _sourceBitmap?.Dispose();
            _sourceBitmap = TestImages.Create(kind, size);
        }

        _removedKinds.Clear();
        SyncRemovalUi();
        RebuildAll();
    }

    private void SetOrder(int order)
    {
        order = Math.Clamp(order, 1, 5);
        if (order == _order) return;

        _order = order;
        _removedKinds.Clear();
        SyncOrderUi();
        SyncRemovalUi();
        RebuildAll();
    }

    private void RebuildAll()
    {
        if (_sourceBitmap is null) return;

        try
        {
            _image = ImageLoader.CreateLabImage(_sourceBitmap, _imageSize, _sourceName, _sourceDescription);
            _order = Math.Min(_order, Math.Min(5, SubbandGeometry.MaxOrder(_image.Size)));
            _decomposition = HaarDecomposition.Build(_image, _order);
            _baseline = LabAnalyzer.RunRemoval(_decomposition, Array.Empty<SubbandKind>());
            _singleExperiments = LabAnalyzer.RunSingleRemovals(_decomposition);
            SyncOrderUi();
            RefreshAll();
            SetStatus("Разложение готово");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Ошибка обработки изображения:\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshAll()
    {
        if (_image is null || _decomposition is null) return;

        _cardOriginal.SetImage(ImageRenderer.ToGrayscaleBitmap(_image.Pixels));
        _cardOriginal.SetBadge($"{_image.Size}×{_image.Size}");
        _cardOriginal.SetCaption($"Яркость Y · диапазон {_image.Min:F0}…{_image.Max:F0} · средняя {_image.Mean:F1}\n" +
                                 (_testKind.HasValue ? "встроенное тестовое изображение" : "загруженный файл"));
        _cardOriginal.SetTitle(_sourceName);

        _cardPyramid.SetImage(ImageRenderer.RenderPyramid(_decomposition));
        _cardPyramid.SetBadge($"порядок {_order}");
        _cardPyramid.SetCaption("ALL — серые градации, детали — синий (отрицательные) и жёлтый (положительные).\n" +
                                "Логарифмическая нормировка; рамками показаны границы матриц.");

        foreach (var kind in Subbands.All)
        {
            var stats = _decomposition.GetBandStats(_order, kind);
            var card = _bandCards[kind];
            card.SetTitle($"{kind.Code()}{_order}");
            card.SetBadge($"{stats.Size}×{stats.Size}");
            card.SetImage(ImageRenderer.RenderBand(_decomposition, _order, kind));
            card.SetCaption($"{kind.Axes()}\n{FormatCount(stats.CoefficientCount)} коэфф. · энергия {stats.EnergyPercent:F1} %");
        }

        UpdateReconstruction();
    }

    private void UpdateReconstruction()
    {
        if (_image is null || _decomposition is null) return;

        var kinds = Subbands.All.Where(kind => _removedKinds.Contains(kind)).ToArray();
        var combined = LabAnalyzer.RunRemoval(_decomposition, kinds);

        _cardRestored.SetImage(ImageRenderer.ToGrayscaleBitmap(combined.Reconstructed));
        _cardRestored.SetBadge(kinds.Length == 0
            ? "без потерь"
            : kinds.Length == 1 ? combined.Label : kinds.Length + " матрицы");
        _cardRestored.SetCaption($"PSNR {PsnrText(combined.Metrics)} дБ · SSIM {combined.Metrics.Ssim:F3} · " +
                                 $"MAE {combined.Metrics.Mae:F2}\n" +
                                 (kinds.Length == 0
                                     ? "Все матрицы сохранены: восстановление без потерь"
                                     : "Удалено: " + combined.Label));

        _cardDifference.SetImage(ImageRenderer.RenderDifference(_image.Pixels, combined.Reconstructed, 4));
        _cardDifference.SetBadge("×4");
        _cardDifference.SetCaption(kinds.Length == 0
            ? "Различий нет: все коэффициенты сохранены."
            : "|исходное − восстановленное| ×4; максимальная ошибка " + combined.Metrics.MaxError.ToString("F0", CI));

        RichText.SetAnalysis(_analysis, _order, AnalysisText.LiveSummary(_decomposition, kinds, combined, _singleExperiments));
        FillMetrics(combined, kinds);
        UpdateStatus(combined);
    }

    private void FillMetrics(RemovalExperiment combined, IReadOnlyList<SubbandKind> activeKinds)
    {
        _grid.Rows.Clear();

        if (_decomposition is not null)
        {
            var band = _decomposition.GetBandStats(_order, SubbandKind.All);
            double share = band.CoefficientCount * 100.0 / (_decomposition.Size * _decomposition.Size);
            _tablesInfo.Text = $"Порядок {_order}: каждая матрица {band.Size}×{band.Size} — {share:F2} % коэффициентов. " +
                               "PSNR и SSIM — после удаления одной матрицы; зелёный — потери почти нет, красный — сильная деградация.";
        }

        if (activeKinds.Count > 1)
            AddRow("Текущий набор (" + activeKinds.Count + ")", combined, active: true);

        if (_baseline is not null)
            AddRow("Без удаления", _baseline, active: activeKinds.Count == 0);

        foreach (var experiment in _singleExperiments)
        {
            var kind = experiment.RemovedKinds[0];
            AddRow(kind.Code() + _order, experiment, active: activeKinds.Contains(kind));
        }

        _grid.ClearSelection();
    }

    private void AddRow(string variant, RemovalExperiment experiment, bool active)
    {
        int index = _grid.Rows.Add(
            variant,
            PsnrText(experiment.Metrics),
            experiment.Metrics.Ssim.ToString("F3", CI),
            experiment.CompressionRatio.ToString("F2", CI) + "×");

        var row = _grid.Rows[index];
        if (active) row.DefaultCellStyle.Font = Theme.UiFontBold;
        row.Cells["Psnr"].Style.BackColor = Theme.ForPsnr(experiment.Metrics.Psnr);
        row.Cells["Psnr"].Style.ForeColor = Theme.TextForPsnr(experiment.Metrics.Psnr);
        row.Cells["Psnr"].Style.Font = Theme.UiFontBold;
    }

    private void UpdateStatus(RemovalExperiment combined)
    {
        if (_image is null || _decomposition is null) return;

        _statusLabel.Text = _removedKinds.Count == 0
            ? $"Порядок {_order} · все матрицы сохранены"
            : "Порядок " + _order + " · удалено: " + string.Join(", ", _removedKinds.Select(k => k.Code()));

        double allEnergy = _decomposition.GetBandStats(_order, SubbandKind.All).EnergyPercent;
        _infoLabel.Text = $"{_image.Size}×{_image.Size} · PSNR {PsnrText(combined.Metrics)} дБ · " +
                          $"SSIM {combined.Metrics.Ssim:F3} · ALL {allEnergy:F1} %";
    }

    // ==================== ДЕЙСТВИЯ ====================

    private void ToggleRemoval(SubbandKind kind, bool active)
    {
        if (_image is null) return;

        if (active) _removedKinds.Add(kind);
        else _removedKinds.Remove(kind);

        SyncRemovalUi();
        UpdateReconstruction();
    }

    private void ResetRemovals()
    {
        if (_removedKinds.Count == 0) return;
        _removedKinds.Clear();
        SyncRemovalUi();
        UpdateReconstruction();
    }

    private void SyncRemovalUi()
    {
        _suspendEvents = true;
        foreach (var kind in Subbands.All)
        {
            if (_removeButtons.TryGetValue(kind, out var button)) button.Checked = _removedKinds.Contains(kind);
            if (_removeMenuItems.TryGetValue(kind, out var menuItem)) menuItem.Checked = _removedKinds.Contains(kind);
        }

        _suspendEvents = false;
    }

    private void SyncOrderUi()
    {
        for (int i = 0; i < _orderMenuItems.Length; i++)
            if (_orderMenuItems[i] is not null) _orderMenuItems[i].Checked = i + 1 == _order;

        _suspendEvents = true;
        _orderCombo.SelectedIndex = _order - 1;
        _suspendEvents = false;
    }

    private void OnTestComboChanged()
    {
        if (_suspendEvents || _testCombo.SelectedIndex < 0) return;
        var kind = TestImagesInfo.All[_testCombo.SelectedIndex];
        if (_testKind == kind) return;
        SetTestImage(kind);
    }

    private void OnSizeComboChanged()
    {
        if (_suspendEvents || _sizeCombo.SelectedItem is null) return;
        if (int.TryParse((string)_sizeCombo.SelectedItem, NumberStyles.Integer, CI, out int size))
            SetSize(size);
    }

    private void OnOrderComboChanged()
    {
        if (_suspendEvents || _orderCombo.SelectedItem is null) return;
        if (int.TryParse((string)_orderCombo.SelectedItem, NumberStyles.Integer, CI, out int order))
            SetOrder(order);
    }

    private void ShowTheory()
    {
        _rightTabs.SelectedIndex = 2;
        _theory.Focus();
    }

    private void ShowAbout()
    {
        MessageBox.Show(this,
            "Вейвлет-преобразование изображений\nЛабораторная работа №3\n\n" +
            "Дисциплина: Основы цифрового телевидения\n" +
            "Реализовано: прямое и обратное преобразование Хаара,\n" +
            "разложение до 5 порядка, удаление матриц ALL/ALH/AHL/AHH,\n" +
            "PSNR/SSIM, сжатие коэффициентов, HTML-отчёт.\n\n" +
            "C# / .NET 10, Windows Forms",
            "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task GenerateReportAsync()
    {
        if (_image is null)
        {
            MessageBox.Show(this, "Сначала загрузите изображение.", "Отчёт",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить HTML-отчёт",
            Filter = "HTML-файл (*.html)|*.html",
            FileName = "report.html",
            InitialDirectory = AppContext.BaseDirectory,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var image = _image;
        string sourceLabel = _sourcePath is null ? "встроенное тестовое изображение" : "загруженный файл";
        string? sourcePath = _sourcePath;
        string path = dialog.FileName;

        SetBusy(true, "Формирование HTML-отчёта…");
        bool ok = false;
        try
        {
            await Task.Run(() =>
            {
                var model = ReportModel.Build(image, sourceLabel, sourcePath, 5);
                new HtmlReportGenerator().Generate(model, path);
            });
            ok = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось сформировать отчёт:\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }

        if (!ok) return;
        SetStatus("Отчёт сохранён");
        var answer = MessageBox.Show(this, $"Отчёт сохранён:\n{path}\n\nОткрыть в браузере?", "Готово",
            MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (answer == DialogResult.Yes) OpenWithShell(path);
    }

    private async Task ExportCsvAsync()
    {
        if (_image is null)
        {
            MessageBox.Show(this, "Сначала загрузите изображение.", "CSV",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Directory.CreateDirectory(ReportService.DefaultCsvDirectory);
        using var dialog = new FolderBrowserDialog
        {
            Description = "Папка для CSV-файлов",
            SelectedPath = ReportService.DefaultCsvDirectory,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var image = _image;
        string directory = dialog.SelectedPath;
        string sourceLabel = _sourcePath is null ? "встроенное тестовое изображение" : "загруженный файл";
        string? sourcePath = _sourcePath;

        SetBusy(true, "Экспорт CSV…");
        IReadOnlyList<string>? files = null;
        try
        {
            files = await Task.Run(() =>
            {
                var model = ReportModel.Build(image, sourceLabel, sourcePath, 5);
                return CsvExporter.ExportAll(model, directory);
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось экспортировать CSV:\n" + ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }

        if (files is null) return;
        SetStatus("CSV сохранены");
        var answer = MessageBox.Show(this,
            $"Сохранено {files.Count} файлов в:\n{directory}\n\nОткрыть папку?",
            "Готово", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (answer == DialogResult.Yes) OpenWithShell(directory);
    }

    private void SetBusy(bool busy, string text = "")
    {
        _busyLabel.Text = text;
        _busyPanel.Visible = busy;
        if (busy)
        {
            CenterBusyLabel();
            _busyPanel.BringToFront();
        }

        _menu.Enabled = !busy;
        _toolbar.Enabled = !busy;
        UseWaitCursor = busy;
        if (busy)
        {
            _statusLabel.Text = text;
            _busyPanel.Refresh();
        }
    }

    private void SetStatus(string text) => _statusLabel.Text = text;

    private static void OpenWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // Открыть проводник не удалось — файл всё равно сохранён.
        }
    }

    private static string PsnrText(QualityMetrics metrics) =>
        double.IsPositiveInfinity(metrics.Psnr) ? "∞" : metrics.Psnr.ToString("F2", CI);

    private static string FormatCount(int value) => value.ToString("N0", CI).Replace(",", " ");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sourceBitmap?.Dispose();
            _sourceBitmap = null;
        }

        base.Dispose(disposing);
    }
}
