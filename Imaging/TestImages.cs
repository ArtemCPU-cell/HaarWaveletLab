using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using HaarWaveletLab.Models;

namespace HaarWaveletLab.Imaging;

/// <summary>Встроенные тестовые изображения, которые рисуются программно (без внешних файлов).</summary>
public static class TestImages
{
    public static Bitmap Create(TestImageKind kind, int size)
    {
        size = Math.Clamp(size, 32, 2048);
        return kind switch
        {
            TestImageKind.TestCard => CreateTestCard(size),
            TestImageKind.ColorBars => CreateColorBars(size),
            TestImageKind.Geometry => CreateGeometry(size),
            TestImageKind.Portrait => CreatePortrait(size),
            TestImageKind.Texture => CreateTexture(size),
            _ => CreateTestCard(size)
        };
    }

    private static Bitmap NewCanvas(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        return bitmap;
    }

    private static void DrawCenteredText(Graphics g, string text, Font font, Brush brush, float centerX, float centerY, float width)
    {
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var rect = new RectangleF(centerX - width / 2f, centerY - font.Height / 2f, width, font.Height * 1.2f);
        g.DrawString(text, font, brush, rect, format);
    }

    private static Bitmap CreateTestCard(int size)
    {
        float s = size / 256f;
        var bitmap = new Bitmap(size, size, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using (var background = new LinearGradientBrush(
                   new Rectangle(0, 0, size, size),
                   Color.FromArgb(238, 241, 245), Color.FromArgb(198, 206, 216), 90f))
        {
            g.FillRectangle(background, 0, 0, size, size);
        }

        using var gridPen = new Pen(Color.FromArgb(70, 90, 110, 130), Math.Max(1f, 1f * s));
        for (int i = 1; i < 16; i++)
        {
            float p = i * size / 16f;
            g.DrawLine(gridPen, p, 0, p, size);
            g.DrawLine(gridPen, 0, p, size, p);
        }

        using var borderPen = new Pen(Color.FromArgb(30, 32, 36), Math.Max(2f, 2.5f * s));
        float margin = 10 * s;
        g.DrawRectangle(borderPen, margin, margin, size - 2 * margin, size - 2 * margin);
        g.DrawRectangle(new Pen(Color.White, Math.Max(1f, 1.2f * s)),
            margin + 4 * s, margin + 4 * s, size - 2 * margin - 8 * s, size - 2 * margin - 8 * s);

        // Центральные окружности и перекрестие.
        using var circlePen = new Pen(Color.FromArgb(45, 48, 55), Math.Max(1.2f, 1.6f * s));
        g.DrawEllipse(circlePen, size / 2f - 78 * s, size / 2f - 78 * s, 156 * s, 156 * s);
        g.DrawEllipse(new Pen(Color.FromArgb(120, 40, 45, 55), Math.Max(1f, 1f * s)),
            size / 2f - 54 * s, size / 2f - 54 * s, 108 * s, 108 * s);
        using var crossPen = new Pen(Color.FromArgb(110, 40, 45, 55), Math.Max(1f, 1.2f * s)) { DashStyle = DashStyle.Dash };
        g.DrawLine(crossPen, margin, size / 2f, size - margin, size / 2f);
        g.DrawLine(crossPen, size / 2f, margin, size / 2f, size - margin);

        if (size >= 128)
        {
            using var titleFont = new Font("Segoe UI", 38 * s, FontStyle.Bold, GraphicsUnit.Pixel);
            using var subFont = new Font("Segoe UI", 11 * s, FontStyle.Regular, GraphicsUnit.Pixel);
            using var dark = new SolidBrush(Color.FromArgb(35, 38, 44));
            DrawCenteredText(g, "ОЦТВ", titleFont, dark, size / 2f, size / 2f - 6 * s, size);
            DrawCenteredText(g, "вейвлет Хаара · лаб. №3", subFont, dark, size / 2f, size / 2f + 26 * s, size);

            using var smallFont = new Font("Segoe UI", 8 * s, FontStyle.Bold, GraphicsUnit.Pixel);
            g.DrawString("ТЕСТОВАЯ ТАБЛИЦА", smallFont, dark, 18 * s, 15 * s);
            g.DrawString("Y = 0.299R + 0.587G + 0.114B", smallFont, dark, 18 * s, 28 * s);
        }

        // Угловые «миры» с разной пространственной частотой.
        DrawLineGrating(g, 170 * s, 20 * s, 30 * s, 46 * s, vertical: true, period: 4 * s, s);
        DrawLineGrating(g, 200 * s, 20 * s, 34 * s, 46 * s, vertical: false, period: 5 * s, s);
        DrawChecker(g, 20 * s, 176 * s, 48 * s, 48 * s, 6 * s);
        DrawDiagonalHatch(g, 188 * s, 176 * s, 48 * s, 48 * s, 8 * s);

        // Миры уменьшающегося периода.
        float wedgeY = 150 * s;
        for (int i = 0; i < 5; i++)
        {
            float x = 24 * s + i * 18 * s;
            DrawLineGrating(g, x, wedgeY, 16 * s, 18 * s, vertical: true, period: (5 - i) * s, s);
        }

        // Градационный клин (10 ступеней).
        float wedgeX = 44 * s;
        float wedgeW = 168 * s;
        float stepW = wedgeW / 10f;
        float top = 208 * s;
        float height = 26 * s;
        for (int i = 0; i < 10; i++)
        {
            int level = (int)Math.Round(255.0 * i / 9.0);
            using var brush = new SolidBrush(Color.FromArgb(level, level, level));
            g.FillRectangle(brush, wedgeX + i * stepW, top, stepW + 0.5f, height);
        }

        using var wedgePen = new Pen(Color.FromArgb(30, 32, 36), Math.Max(1f, s));
        g.DrawRectangle(wedgePen, wedgeX, top, wedgeW, height);
        for (int i = 1; i < 10; i++)
            g.DrawLine(wedgePen, wedgeX + i * stepW, top, wedgeX + i * stepW, top + height);

        if (size >= 128)
        {
            using var tinyFont = new Font("Segoe UI", 7 * s, FontStyle.Regular, GraphicsUnit.Pixel);
            g.DrawString("ГРАДАЦИИ СЕРОГО", tinyFont, Brushes.Black, 44 * s, 236 * s);
        }

        return bitmap;
    }

    private static void DrawLineGrating(Graphics g, float x, float y, float w, float h, bool vertical, float period, float scale)
    {
        using var clip = new Region(new RectangleF(x, y, w, h));
        var saved = g.Save();
        g.SetClip(clip, CombineMode.Replace);
        using var white = new SolidBrush(Color.FromArgb(245, 245, 245));
        using var black = new SolidBrush(Color.FromArgb(20, 20, 20));
        g.FillRectangle(white, x, y, w, h);

        if (vertical)
        {
            for (float p = x; p < x + w; p += period)
                g.FillRectangle(black, p, y, Math.Max(1f, period / 2f), h);
        }
        else
        {
            for (float p = y; p < y + h; p += period)
                g.FillRectangle(black, x, p, w, Math.Max(1f, period / 2f));
        }

        g.Restore(saved);
        using var frame = new Pen(Color.FromArgb(40, 40, 40), Math.Max(1f, scale));
        g.DrawRectangle(frame, x, y, w, h);
    }

    private static void DrawChecker(Graphics g, float x, float y, float w, float h, float cell)
    {
        using var dark = new SolidBrush(Color.FromArgb(25, 25, 25));
        using var light = new SolidBrush(Color.FromArgb(240, 240, 240));
        int columns = (int)Math.Ceiling(w / cell);
        int rows = (int)Math.Ceiling(h / cell);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                var brush = (r + c) % 2 == 0 ? dark : light;
                g.FillRectangle(brush, x + c * cell, y + r * cell, cell, cell);
            }
        }
    }

    private static void DrawDiagonalHatch(Graphics g, float x, float y, float w, float h, float period)
    {
        var saved = g.Save();
        g.SetClip(new RectangleF(x, y, w, h), CombineMode.Replace);
        using var white = new SolidBrush(Color.FromArgb(242, 242, 242));
        g.FillRectangle(white, x, y, w, h);
        using var pen = new Pen(Color.FromArgb(28, 28, 28), Math.Max(1f, period / 5f));
        for (float p = -h; p < w + h; p += period)
            g.DrawLine(pen, x + p, y + h, x + p + h, y);
        g.Restore(saved);
        using var frame = new Pen(Color.FromArgb(40, 40, 40), 1f);
        g.DrawRectangle(frame, x, y, w, h);
    }

    private static Bitmap CreateColorBars(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.FromArgb(22, 22, 22));
        int[] levels = { 235, 210, 170, 145, 106, 81, 41, 16 };
        float barWidth = size / 8f;
        float mainHeight = size * 0.78f;

        for (int i = 0; i < 8; i++)
        {
            int level = levels[i];
            using var brush = new SolidBrush(Color.FromArgb(level, level, level));
            g.FillRectangle(brush, i * barWidth, 0, barWidth + 0.5f, mainHeight);
        }

        using var separator = new Pen(Color.FromArgb(18, 18, 18), Math.Max(1f, size / 256f));
        for (int i = 1; i < 8; i++)
            g.DrawLine(separator, i * barWidth, 0, i * barWidth, mainHeight);

        // Нижняя полоса: ступени серого и мелкая вертикальная решётка.
        float lowerTop = mainHeight;
        float lowerHeight = size - lowerTop;
        int steps = 16;
        float stepWidth = size / (float)steps;
        for (int i = 0; i < steps; i++)
        {
            int level = (int)Math.Round(255.0 * i / (steps - 1));
            using var brush = new SolidBrush(Color.FromArgb(level, level, level));
            g.FillRectangle(brush, i * stepWidth, lowerTop, stepWidth + 0.5f, lowerHeight * 0.55f);
        }

        int stripes = 48;
        float stripeWidth = size / (float)stripes;
        for (int i = 0; i < stripes; i++)
        {
            int level = i % 2 == 0 ? 245 : 20;
            using var brush = new SolidBrush(Color.FromArgb(level, level, level));
            g.FillRectangle(brush, i * stripeWidth, lowerTop + lowerHeight * 0.58f, stripeWidth, lowerHeight * 0.42f);
        }

        if (size >= 128)
        {
            using var font = new Font("Segoe UI", 9 * size / 256f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var whiteBrush = new SolidBrush(Color.FromArgb(235, 235, 235));
            using var darkBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
            g.DrawString("Y", font, whiteBrush, 6 * size / 256f, 6 * size / 256f);
            g.DrawString("ЧЁРНЫЙ", font, darkBrush, size - 58 * size / 256f, 6 * size / 256f);
        }

        return bitmap;
    }

    private static Bitmap CreateGeometry(int size)
    {
        float s = size / 256f;
        var bitmap = new Bitmap(size, size, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(36, 38, 43));

        using var white = new Pen(Color.FromArgb(235, 235, 235), Math.Max(1.5f, 3 * s));
        using var gray = new Pen(Color.FromArgb(150, 158, 168), Math.Max(1f, 1.5f * s));
        using var whiteBrush = new SolidBrush(Color.FromArgb(232, 234, 238));
        using var grayBrush = new SolidBrush(Color.FromArgb(120, 126, 136));

        g.DrawEllipse(white, size * 0.16f, size * 0.16f, size * 0.5f, size * 0.5f);
        g.FillRectangle(whiteBrush, size * 0.08f, size * 0.68f, size * 0.2f, size * 0.14f);
        g.FillRectangle(grayBrush, size * 0.11f, size * 0.71f, size * 0.14f, size * 0.08f);

        var triangle = new[]
        {
            new PointF(size * 0.72f, size * 0.2f),
            new PointF(size * 0.9f, size * 0.48f),
            new PointF(size * 0.62f, size * 0.48f)
        };
        g.FillPolygon(grayBrush, triangle);

        g.DrawLine(white, size * 0.55f, size * 0.55f, size * 0.94f, size * 0.92f);
        g.DrawLine(gray, size * 0.08f, size * 0.55f, size * 0.48f, size * 0.55f);
        g.DrawLine(gray, size * 0.5f, size * 0.08f, size * 0.5f, size * 0.42f);

        for (int i = 0; i < 14; i++)
        {
            double angle = Math.PI * i / 13.0;
            g.DrawLine(gray,
                size * 0.4f, size * 0.4f,
                size * 0.4f + (float)(Math.Cos(angle) * size * 0.3f),
                size * 0.4f + (float)(Math.Sin(angle) * size * 0.3f));
        }

        // Точечная сетка — очень мелкие детали.
        using var dotBrush = new SolidBrush(Color.FromArgb(210, 214, 220));
        for (int y = (int)(size * 0.58f); y < size; y += Math.Max(3, (int)(10 * s)))
        {
            for (int x = (int)(size * 0.62f); x < size; x += Math.Max(3, (int)(10 * s)))
                g.FillEllipse(dotBrush, x, y, Math.Max(1.4f, 2.2f * s), Math.Max(1.4f, 2.2f * s));
        }

        // Шахматная доска в правом нижнем углу.
        float cell = size * 0.025f;
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 5; c++)
            {
                var brush = (r + c) % 2 == 0 ? Brushes.WhiteSmoke : Brushes.DimGray;
                g.FillRectangle(brush, size * 0.82f + c * cell, size * 0.72f + r * cell, cell, cell);
            }
        }

        return bitmap;
    }

    private static Bitmap CreatePortrait(int size)
    {
        float s = size / 256f;
        var bitmap = new Bitmap(size, size, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using (var background = new LinearGradientBrush(
                   new Rectangle(0, 0, size, size),
                   Color.FromArgb(228, 235, 243), Color.FromArgb(150, 168, 190), 90f))
        {
            g.FillRectangle(background, 0, 0, size, size);
        }

        // Плечи и шея.
        using var shirt = new SolidBrush(Color.FromArgb(52, 64, 84));
        g.FillEllipse(shirt, size * 0.06f, size * 0.76f, size * 0.88f, size * 0.42f);
        using var neck = new SolidBrush(Color.FromArgb(196, 158, 128));
        g.FillRectangle(neck, size * 0.44f, size * 0.62f, size * 0.12f, size * 0.18f);

        // Голова и уши.
        using var skin = new SolidBrush(Color.FromArgb(216, 178, 148));
        g.FillEllipse(skin, size * 0.28f, size * 0.16f, size * 0.44f, size * 0.54f);
        g.FillEllipse(skin, size * 0.24f, size * 0.38f, size * 0.06f, size * 0.1f);
        g.FillEllipse(skin, size * 0.7f, size * 0.38f, size * 0.06f, size * 0.1f);

        // Тени на лице (мягкие градиенты).
        using (var shade = new SolidBrush(Color.FromArgb(28, 120, 80, 60)))
        {
            g.FillEllipse(shade, size * 0.3f, size * 0.3f, size * 0.4f, size * 0.4f);
        }

        // Волосы.
        using var hair = new SolidBrush(Color.FromArgb(48, 36, 32));
        g.FillEllipse(hair, size * 0.25f, size * 0.12f, size * 0.5f, size * 0.26f);
        g.FillEllipse(hair, size * 0.25f, size * 0.24f, size * 0.06f, size * 0.22f);
        g.FillEllipse(hair, size * 0.69f, size * 0.24f, size * 0.06f, size * 0.22f);

        // Брови.
        using var brow = new Pen(Color.FromArgb(55, 40, 34), Math.Max(1.5f, 3 * s));
        g.DrawArc(brow, size * 0.33f, size * 0.32f, size * 0.12f, size * 0.05f, 200f, 140f);
        g.DrawArc(brow, size * 0.55f, size * 0.32f, size * 0.12f, size * 0.05f, 200f, 140f);

        // Глаза.
        using var eyeWhite = new SolidBrush(Color.FromArgb(246, 244, 240));
        using var iris = new SolidBrush(Color.FromArgb(62, 92, 110));
        using var pupil = new SolidBrush(Color.FromArgb(20, 20, 24));
        DrawEye(size * 0.35f, size * 0.4f);
        DrawEye(size * 0.57f, size * 0.4f);

        // Нос и рот.
        using var nosePen = new Pen(Color.FromArgb(170, 120, 100), Math.Max(1f, 2 * s));
        g.DrawLine(nosePen, size * 0.5f, size * 0.43f, size * 0.48f, size * 0.55f);
        g.DrawArc(nosePen, size * 0.46f, size * 0.54f, size * 0.08f, size * 0.03f, 0f, 180f);
        using var mouth = new Pen(Color.FromArgb(150, 72, 72), Math.Max(1.5f, 3 * s));
        g.DrawArc(mouth, size * 0.42f, size * 0.62f, size * 0.16f, size * 0.05f, 10f, 160f);

        // Очки — тонкие линии, дающие высокочастотные детали.
        using var frame = new Pen(Color.FromArgb(60, 60, 66), Math.Max(1f, 2 * s));
        g.DrawRectangle(frame, size * 0.31f, size * 0.365f, size * 0.16f, size * 0.085f);
        g.DrawRectangle(frame, size * 0.53f, size * 0.365f, size * 0.16f, size * 0.085f);
        g.DrawLine(frame, size * 0.47f, size * 0.395f, size * 0.53f, size * 0.395f);
        g.DrawLine(frame, size * 0.31f, size * 0.395f, size * 0.27f, size * 0.385f);
        g.DrawLine(frame, size * 0.69f, size * 0.395f, size * 0.73f, size * 0.385f);

        return bitmap;

        void DrawEye(float cx, float cy)
        {
            float w = size * 0.1f;
            float h = size * 0.055f;
            g.FillEllipse(eyeWhite, cx - w / 2f, cy - h / 2f, w, h);
            g.FillEllipse(iris, cx - h * 0.42f, cy - h * 0.42f, h * 0.84f, h * 0.84f);
            g.FillEllipse(pupil, cx - h * 0.2f, cy - h * 0.2f, h * 0.4f, h * 0.4f);
            g.FillEllipse(Brushes.White, cx - h * 0.35f, cy - h * 0.3f, h * 0.18f, h * 0.18f);
        }
    }

    private static Bitmap CreateTexture(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format24bppRgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int stride = data.Stride;
            var buffer = new byte[stride * size];
            float half = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double nx = (x - half) / half;
                    double ny = (y - half) / half;
                    double r = Math.Sqrt(nx * nx + ny * ny);
                    double angle = Math.Atan2(ny, nx);

                    double baseValue = 168 - 82 * r;
                    double rings = 26 * Math.Sin(28 * r + 2.5 * Math.Sin(3 * angle));
                    double stripes = 14 * Math.Sin(46 * nx + 12 * ny);
                    double noise = ((x * 73856093) ^ (y * 19349663)) % 11 - 5;

                    int value = (int)Math.Round(baseValue + rings + stripes + noise);
                    value = Math.Clamp(value, 0, 255);
                    int offset = y * stride + x * 3;
                    buffer[offset] = (byte)value;
                    buffer[offset + 1] = (byte)value;
                    buffer[offset + 2] = (byte)value;
                }
            }

            Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
}
