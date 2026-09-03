using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Hawa.App.Tray;

public enum TrayIconState { Normal, Low, Disconnected, NoAdapter }

/// <summary>Draws a 32x32 earbud-shaped icon whose fill height shows battery.</summary>
public static class TrayIconRenderer
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Render(TrayIconState state, int? percent)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var outline = state switch
        {
            TrayIconState.NoAdapter => Color.FromArgb(120, 120, 120),
            TrayIconState.Disconnected => Color.FromArgb(160, 160, 160),
            _ => Color.White,
        };
        var fill = state == TrayIconState.Low ? Color.FromArgb(255, 69, 58) : Color.FromArgb(52, 199, 89);

        // stem
        using (var pen = new Pen(outline, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(pen, 16, 14, 16, 29);

        // bud body
        var body = new Rectangle(7, 3, 18, 14);
        using (var path = RoundedRect(body, 7))
        {
            if (state is TrayIconState.Normal or TrayIconState.Low && percent is { } p)
            {
                using var brush = new SolidBrush(fill);
                int h = (int)Math.Round(body.Height * Math.Clamp(p, 0, 100) / 100.0);
                g.SetClip(path);
                g.FillRectangle(brush, body.X, body.Bottom - h, body.Width, h);
                g.ResetClip();
            }
            using var pen = new Pen(outline, 2);
            g.DrawPath(pen, path);
        }

        if (state == TrayIconState.NoAdapter)
            using (var pen = new Pen(Color.FromArgb(255, 69, 58), 3)) g.DrawLine(pen, 5, 27, 27, 5);

        var handle = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
