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
    private Rect? _lastExcludeRect;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        _fadeTimer.Interval = TimeSpan.FromMilliseconds(FadeMs / (double)FadeSteps);
        _fadeTimer.Tick += FadeTick;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        ApplyRegion();
    }

    public void SetBounds(Rect rect)
    {
        Left = rect.X;
        Top = rect.Y;
        Width = rect.Width;
        Height = rect.Height;
    }

    public void SetBlurredImage(BitmapSource? source)
    {
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
        DimLayer.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0, 0, 0));
    }

    public void SetExcludeRect(Rect? exclude)
    {
        _lastExcludeRect = exclude;
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
        var exclude = _lastExcludeRect;
        try
        {
            if (exclude.HasValue)
            {
                var r = exclude.Value;
                int x1 = (int)(r.Left - Left);
                int y1 = (int)(r.Top - Top);
                int x2 = (int)(r.Right - Left);
                int y2 = (int)(r.Bottom - Top);
                IntPtr holeRgn = NativeMethods.CreateRectRgn(x1, y1, x2, y2);
                if (holeRgn != IntPtr.Zero)
                {
                    try
                    {
                        int result = NativeMethods.CombineRgn(fullRgn, fullRgn, holeRgn, NativeMethods.RGN_DIFF);
                        if (result == 0)
                        {
                            NativeMethods.DeleteObject(holeRgn);
                            return;
                        }
                    }
                    finally
                    {
                        NativeMethods.DeleteObject(holeRgn);
                    }
                }
            }

            NativeMethods.SetWindowRgn(hwnd, fullRgn, true);
            owned = true; // System owns the region now; do not delete
        }
        finally
        {
            if (!owned)
                NativeMethods.DeleteObject(fullRgn);
        }
    }

    public void ShowWithFade()
    {
        _fadeTargetOpacity = 1.0;
        Opacity = 0;
        Show();
        _fadeStep = 0;
        _fadeTimer.Start();
    }

    public void HideWithFade()
    {
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
