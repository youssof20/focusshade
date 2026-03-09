using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FocusShade;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _fadeTimer = new();
    private double _fadeTargetOpacity;
    private const int FadeMs = 120;
    private const int FadeSteps = 8;
    private int _fadeStep;
    private List<Rect> _lastExcludeRects = new();

    public OverlayWindow()
    {
        Log.Info("[OverlayWindow] Constructor");
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        _fadeTimer.Interval = TimeSpan.FromMilliseconds(FadeMs / (double)FadeSteps);
        _fadeTimer.Tick += FadeTick;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Apply extended styles and region as soon as we have an HWND, before first paint.
        // This prevents a fullscreen white/black flash and ensures the hole is there from frame one.
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        ApplyRegion();
    }

    public void SetBounds(Rect rect)
    {
        Log.Info($"[OverlayWindow] SetBounds ({rect.X},{rect.Y},{rect.Width},{rect.Height})");
        Left = rect.X;
        Top = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
    }

    public void SetBlurredImage(BitmapSource? source)
    {
        Log.Info($"[OverlayWindow] SetBlurredImage source={source != null}");
        if (source != null)
        {
            BlurImage.Source = source;
            BlurImage.Visibility = Visibility.Visible;
        }
        else
        {
            BlurImage.Source = null;
            BlurImage.Visibility = Visibility.Collapsed;
        }
    }

    public void SetDimAlpha(byte alpha)
    {
        Log.Info($"[OverlayWindow] SetDimAlpha {alpha}");
        DimLayer.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0, 0, 0));
    }

    /// <summary>Set one or more rects to punch out of the overlay (foreground window + taskbar).</summary>
    public void SetExcludeRects(IEnumerable<Rect> excludeRects)
    {
        _lastExcludeRects = excludeRects?.ToList() ?? new List<Rect>();
        ApplyRegion();
    }

    private void ApplyRegion()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        int w = (int)Width;
        int h = (int)Height;
        if (w <= 0 || h <= 0) return;

        IntPtr fullRgn = NativeMethods.CreateRectRgn(0, 0, w, h);
        if (fullRgn == IntPtr.Zero) return;

        bool owned = false;
        try
        {
            foreach (var r in _lastExcludeRects)
            {
                int x1 = (int)(r.Left - Left);
                int y1 = (int)(r.Top - Top);
                int x2 = (int)(r.Right - Left);
                int y2 = (int)(r.Bottom - Top);
                if (x1 >= x2 || y1 >= y2) continue;
                IntPtr holeRgn = NativeMethods.CreateRectRgn(x1, y1, x2, y2);
                if (holeRgn == IntPtr.Zero) continue;
                try
                {
                    int result = NativeMethods.CombineRgn(fullRgn, fullRgn, holeRgn, NativeMethods.RGN_DIFF);
                    if (result == 0)
                    {
                        NativeMethods.DeleteObject(holeRgn);
                        NativeMethods.DeleteObject(fullRgn);
                        return;
                    }
                }
                finally
                {
                    NativeMethods.DeleteObject(holeRgn);
                }
            }

            NativeMethods.SetWindowRgn(hwnd, fullRgn, true);
            owned = true;
        }
        finally
        {
            if (!owned)
                NativeMethods.DeleteObject(fullRgn);
        }
    }

    public void ShowWithFade()
    {
        Log.Info("[OverlayWindow] ShowWithFade");
        _fadeTargetOpacity = 1.0;
        Opacity = 0;
        Show();
        _fadeStep = 0;
        _fadeTimer.Start();
    }

    public void HideWithFade()
    {
        Log.Info("[OverlayWindow] HideWithFade");
        _fadeTargetOpacity = 0;
        _fadeStep = 0;
        _fadeTimer.Start();
    }

    private void FadeTick(object? sender, EventArgs e)
    {
        _fadeStep++;
        double t = (double)_fadeStep / FadeSteps;
        if (t >= 1.0)
        {
            _fadeTimer.Stop();
            Opacity = _fadeTargetOpacity;
            if (_fadeTargetOpacity == 0)
                Hide();
            return;
        }
        Opacity = Opacity + (_fadeTargetOpacity - Opacity) * 0.35;
    }
}
