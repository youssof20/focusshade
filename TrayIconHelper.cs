using System.Drawing;
using System.Drawing.Drawing2D;

namespace FocusShade;

/// <summary>
/// Creates 16x16 monochrome tray icons: filled circle (active), ring (inactive).
/// Caller must keep the returned Icon and the bitmap alive (do not dispose bitmap while icon is in use).
/// </summary>
internal static class TrayIconHelper
{
    public static (Bitmap Bitmap, Icon Icon) CreateFilledCircleIcon()
    {
        var bm = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bm))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            g.FillEllipse(Brushes.White, 1, 1, 14, 14);
        }
        var icon = Icon.FromHandle(bm.GetHicon());
        return (bm, icon);
    }

    public static (Bitmap Bitmap, Icon Icon) CreateRingIcon()
    {
        var bm = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bm))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var pen = new Pen(Color.White, 2f);
            g.DrawEllipse(pen, 2, 2, 12, 12);
        }
        var icon = Icon.FromHandle(bm.GetHicon());
        return (bm, icon);
    }
}
