using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Continuum.Services;

/// <summary>Draws each app's icon (white glyph on an iOS-style rounded square).</summary>
public static class AppIcons
{
    public static Bitmap Render(AppInfo app, int size = 256)
    {
        var glyph = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(app.Icon),
            Fill = Brushes.White,
            Stretch = Stretch.Uniform,
            Width = size * 0.56,
            Height = size * 0.56,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        var color = Color.Parse(app.Color);
        var tile = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.225),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Lighten(color, 0.12), 0), new GradientStop(color, 1) },
            },
            Child = glyph,
        };
        tile.Measure(new Size(size, size));
        tile.Arrange(new Rect(0, 0, size, size));

        var bitmap = new RenderTargetBitmap(new PixelSize(size, size));
        bitmap.Render(tile);
        return bitmap;
    }

    public static byte[] RenderPng(AppInfo app)
    {
        using var ms = new MemoryStream();
        Render(app).Save(ms);
        return ms.ToArray();
    }

    static Color Lighten(Color c, double amount) => Color.FromRgb(
        (byte)(c.R + (255 - c.R) * amount),
        (byte)(c.G + (255 - c.G) * amount),
        (byte)(c.B + (255 - c.B) * amount));
}
