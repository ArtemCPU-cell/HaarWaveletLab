using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace HaarWaveletLab.UI;

/// <summary>Панель просмотра изображения с вписыванием по размеру и собственным владением Bitmap.</summary>
public sealed class ImageBox : Control
{
    private Bitmap? _image;

    public ImageBox()
    {
        DoubleBuffered = true;
        BackColor = Theme.ImageBackground;
        ResizeRedraw = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Padding = new Padding(6);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public InterpolationMode Interpolation { get; set; } = InterpolationMode.HighQualityBicubic;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EmptyText { get; set; } = "Нет изображения";

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Bitmap? Image => _image;

    public void SetImage(Bitmap? bitmap)
    {
        if (ReferenceEquals(_image, bitmap)) return;
        _image?.Dispose();
        _image = bitmap;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        if (_image is null)
        {
            TextRenderer.DrawText(g, EmptyText, Theme.SmallFont, ClientRectangle, Color.FromArgb(150, 158, 172),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        int availableWidth = Math.Max(1, ClientSize.Width - Padding.Horizontal);
        int availableHeight = Math.Max(1, ClientSize.Height - Padding.Vertical);

        double scale = Math.Min(availableWidth / (double)_image.Width, availableHeight / (double)_image.Height);
        int width = Math.Max(1, (int)Math.Round(_image.Width * scale));
        int height = Math.Max(1, (int)Math.Round(_image.Height * scale));
        int x = (ClientSize.Width - width) / 2;
        int y = (ClientSize.Height - height) / 2;

        g.InterpolationMode = Interpolation;
        g.PixelOffsetMode = scale < 1.0 ? PixelOffsetMode.HighQuality : PixelOffsetMode.Half;
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.DrawImage(_image, new Rectangle(x, y, width, height));

        using var border = new Pen(Color.FromArgb(35, 255, 255, 255));
        g.DrawRectangle(border, x - 1, y - 1, width + 1, height + 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _image?.Dispose();
        base.Dispose(disposing);
    }
}
