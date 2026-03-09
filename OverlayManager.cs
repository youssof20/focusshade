using System.Windows;
using System.Windows.Media.Imaging;

namespace FocusShade;

public class OverlayManager : IDisposable
{
    private readonly SettingsModel _settings;
    private readonly List<OverlayWindow> _overlays = new();
    private IntPtr _winEventHookForeground;
    private IntPtr _winEventHookLocation;
    private IntPtr _lastForegroundHwnd = IntPtr.Zero;
    private NativeMethods.RECT _lastForegroundRect;
    private readonly System.Timers.Timer _locationDebounce = new(150);
    private bool _enabled;
    private bool _disposed;
    private static readonly string[] ExcludedClasses = { "Progman", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow" };

    public OverlayManager(SettingsModel settings)
    {
        _settings = settings;
        _enabled = settings.IsEnabled;
        _locationDebounce.Elapsed += (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(UpdateOverlays);
        _locationDebounce.AutoReset = false;
    }

    public bool IsEnabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            _settings.IsEnabled = value;
            if (_enabled)
                EnsureOverlays();
            else
                HideAll();
        }
    }

    public void Toggle() => IsEnabled = !IsEnabled;

    public void Start()
    {
        EnsureOverlays();
        _winEventHookForeground = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            WinEventCallback,
            0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        _winEventHookLocation = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero,
            WinEventCallback,
            0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND)
        {
            _lastForegroundHwnd = hwnd;
            if (NativeMethods.GetWindowRect(hwnd, out _lastForegroundRect))
                System.Windows.Application.Current.Dispatcher.BeginInvoke(UpdateOverlays);
        }
        else if (eventType == NativeMethods.EVENT_OBJECT_LOCATIONCHANGE)
        {
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == hwnd)
            {
                _locationDebounce.Stop();
                _locationDebounce.Start();
            }
        }
    }

    private void EnsureOverlays()
    {
        var monitors = new List<(IntPtr hMonitor, NativeMethods.RECT rect)>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT r, IntPtr dwData) =>
        {
            monitors.Add((hMonitor, r));
            return true;
        }, IntPtr.Zero);

        while (_overlays.Count < monitors.Count)
        {
            var w = new OverlayWindow();
            w.Closed += (_, _) => _overlays.Remove(w);
            _overlays.Add(w);
        }

        for (int i = 0; i < monitors.Count; i++)
        {
            var r = monitors[i].rect;
            var rect = new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            _overlays[i].SetBounds(rect);
        }

        if (_enabled)
            UpdateOverlays();
    }

    private void UpdateOverlays()
    {
        if (!_enabled || _disposed)
        {
            HideAll();
            return;
        }

        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero || ShouldExclude(fg))
        {
            HideAll();
            return;
        }

        if (!NativeMethods.GetWindowRect(fg, out var fgRect))
        {
            HideAll();
            return;
        }

        _lastForegroundHwnd = fg;
        _lastForegroundRect = fgRect;

        EnsureOverlays();

        byte dimAlpha = _settings.DimAlpha;
        int blurRadius = _settings.BlurRadiusPx;
        var fgRectWpf = new Rect(fgRect.Left, fgRect.Top, fgRect.Right - fgRect.Left, fgRect.Bottom - fgRect.Top);

        for (int i = 0; i < _overlays.Count; i++)
        {
            var overlay = _overlays[i];
            var monitorRect = new Rect(overlay.Left, overlay.Top, overlay.Width, overlay.Height);
            Rect? exclude = Rect.Intersect(fgRectWpf, monitorRect);
            if (exclude.HasValue && exclude.Value.IsEmpty)
                exclude = null;

            BitmapSource? blurred = null;
            if (blurRadius > 0 || dimAlpha > 0)
            {
                blurred = BlurHelper.CaptureAndBlur(monitorRect, blurRadius);
            }

            overlay.SetBlurredImage(blurred);
            overlay.SetDimAlpha(dimAlpha);
            overlay.SetExcludeRect(exclude);
            overlay.ShowWithFade();
        }
    }

    private bool ShouldExclude(IntPtr hwnd)
    {
        uint pid;
        NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
        if (pid == Environment.ProcessId)
            return true;

        var sb = new System.Text.StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        string className = sb.ToString();
        foreach (var ex in ExcludedClasses)
            if (string.Equals(className, ex, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void HideAll()
    {
        foreach (var w in _overlays)
            w.HideWithFade();
    }

    public void RefreshSettings()
    {
        if (_enabled && _lastForegroundHwnd != IntPtr.Zero && !ShouldExclude(_lastForegroundHwnd))
            UpdateOverlays();
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_winEventHookForeground != IntPtr.Zero)
            NativeMethods.UnhookWinEvent(_winEventHookForeground);
        if (_winEventHookLocation != IntPtr.Zero)
            NativeMethods.UnhookWinEvent(_winEventHookLocation);
        _winEventHookForeground = IntPtr.Zero;
        _winEventHookLocation = IntPtr.Zero;
        _locationDebounce.Stop();
        _locationDebounce.Dispose();
        foreach (var w in _overlays.ToList())
            w.Close();
        _overlays.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
