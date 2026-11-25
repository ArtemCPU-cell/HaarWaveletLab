namespace HaarWaveletLab.UI;

/// <summary>Карточка с заголовком, изображением и подписью.</summary>
public sealed class ImageCard : UserControl
{
    private readonly Label _title;
    private readonly Label _badge;
    private readonly ImageBox _canvas;
    private readonly Label _caption;

    public ImageCard(string title, bool showCaption = true)
    {
        BackColor = Theme.Surface;
        Padding = new Padding(1);
        Margin = new Padding(0);

        _title = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            Font = Theme.TitleFont,
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            AutoEllipsis = true
        };

        _badge = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            Font = Theme.SmallFont,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 8, 0)
        };

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.SurfaceAlt,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(_title, 0, 0);
        header.Controls.Add(_badge, 1, 0);

        _canvas = new ImageBox { Dock = DockStyle.Fill, Margin = new Padding(0) };

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(6, 6, 6, 0)
        };
        body.Controls.Add(_canvas);

        _caption = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            Visible = showCaption,
            Font = Theme.SmallFont,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(9, 5, 9, 4),
            AutoEllipsis = true
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            BackColor = Theme.Surface
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, showCaption ? 42 : 0));
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(body, 0, 1);
        root.Controls.Add(_caption, 0, 2);
        Controls.Add(root);

        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    public ImageBox Canvas => _canvas;

    public void SetTitle(string title) => _title.Text = title;

    public void SetBadge(string badge) => _badge.Text = badge;

    public void SetCaption(string caption) => _caption.Text = caption;

    public void SetImage(Bitmap? bitmap) => _canvas.SetImage(bitmap);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
