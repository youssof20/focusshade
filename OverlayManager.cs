using System.Windows;
using System.Windows.Media.Imaging;

namespace FocusShade;

public class OverlayManager : IDisposable
{
    private readonly SettingsModel _settings;
    private readonly List<OverlayWindow> _overlays = new();
    private IntPtr _winEventHookForeground;
    private IntPtr _lastForegroundHwnd = IntPtr.Zero;
    private NativeMethods.RECT _lastForegroundRect;
    private bool _enabled;
    private bool _disposed;
    private System.Windows.Threading.DispatcherTimer? _foregroundDebounce;
    private const int ForegroundDebounceMs = 150;
    private static readonly string[] ExcludedClasses = { "Progman", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow" };

    public OverlayManager(SettingsModel settings)
    {
        _settings = settings;
        _enabled = settings.IsEnabled;
    }

    public bool IsEnabled
    {
        get => _enabled;
        set
        {
            Log.Info($"[OverlayManager] IsEnabled set: {_enabled} -> {value}");
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
        Log.Info("[OverlayManager] Start()");
        EnsureOverlays();
        _winEventHookForeground = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            WinEventCallback,
            0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
        Log.Info($"[OverlayManager] WinEvent hook registered: {_winEventHookForeground}");
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        try
        {
            Log.Info($"[OverlayManager] WinEventCallback eventType={eventType} hwnd={hwnd}");
            if (_disposed) { Log.Info("[OverlayManager] WinEventCallback: disposed, return"); return; }
            var app = System.Windows.Application.Current;
            if (app == null) { Log.Info("[OverlayManager] WinEventCallback: app null, return"); return; }
            if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND)
            {
                if (ShouldExclude(hwnd))
                    return;
                _lastForegroundHwnd = hwnd;
                if (!NativeMethods.GetWindowRect(hwnd, out _lastForegroundRect))
                    return;
                DebouncedQueueSafeUpdateOverlays(app);
            }
        }
        catch (Exception ex)
        {
            Log.Error("WinEventCallback error", ex);
        }
    }

    private void DebouncedQueueSafeUpdateOverlays(System.Windows.Application app)
    {
        app.Dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            if (_foregroundDebounce == null)
            {
                _foregroundDebounce = new System.Windows.Threading.DispatcherTimer(
                    System.Windows.Threading.DispatcherPriority.Background,
                    app.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(ForegroundDebounceMs)
                };
                _foregroundDebounce.Tick += (_, _) =>
                {
                    _foregroundDebounce?.Stop();
                    SafeUpdateOverlays();
                };
            }
            _foregroundDebounce.Stop();
            _foregroundDebounce.Start();
        });
    }

    private void EnsureOverlays()
    {
        Log.Info("[OverlayManager] EnsureOverlays()");
        var monitors = new List<(IntPtr hMonitor, NativeMethods.RECT rect)>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT r, IntPtr dwData) =>
        {
            monitors.Add((hMonitor, r));
            return true;
        }, IntPtr.Zero);
        Log.Info($"[OverlayManager] EnsureOverlays: {monitors.Count} monitors, {_overlays.Count} overlays");

        while (_overlays.Count < monitors.Count)
        {
            Log.Info($"[OverlayManager] EnsureOverlays: creating overlay {_overlays.Count}");
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
        Log.Info("[OverlayManager] EnsureOverlays: bounds set");

        if (_enabled)
            UpdateOverlays();
    }

    private bool _updatingOverlays;

    private void SafeUpdateOverlays()
    {
        Log.Info("[OverlayManager] SafeUpdateOverlays() enter");
        if (_updatingOverlays) { Log.Info("[OverlayManager] SafeUpdateOverlays: re-entrant skip"); return; }
        try
        {
            _updatingOverlays = true;
            UpdateOverlays();
            Log.Info("[OverlayManager] SafeUpdateOverlays() UpdateOverlays done");
        }
        catch (Exception ex)
        {
            Log.Error("UpdateOverlays error", ex);
        }
        finally
        {
            _updatingOverlays = false;
            Log.Info("[OverlayManager] SafeUpdateOverlays() exit");
        }
    }

    private void UpdateOverlays()
    {
        Log.Info("[OverlayManager] UpdateOverlays() enter");
        if (!_enabled || _disposed)
        {
            Log.Info("[OverlayManager] UpdateOverlays: not enabled or disposed, HideAll");
            HideAll();
            return;
        }

        IntPtr fg = NativeMethods.GetForegroundWindow();
        Log.Info($"[OverlayManager] UpdateOverlays: GetForegroundWindow={fg}");
        if (fg == IntPtr.Zero || ShouldExclude(fg))
        {
            Log.Info($"[OverlayManager] UpdateOverlays: fg zero or excluded, HideAll");
            HideAll();
            return;
        }

        if (!NativeMethods.GetWindowRect(fg, out var fgRect))
        {
            Log.Info("[OverlayManager] UpdateOverlays: GetWindowRect failed, HideAll");
            HideAll();
            return;
        }

        _lastForegroundHwnd = fg;
        _lastForegroundRect = fgRect;
        Log.Info($"[OverlayManager] UpdateOverlays: fgRect=({fgRect.Left},{fgRect.Top},{fgRect.Right},{fgRect.Bottom})");

        // Do NOT call EnsureOverlays() here - it calls UpdateOverlays() when enabled, causing infinite recursion.
        // Overlays are already created by EnsureOverlays() from Start() or IsEnabled setter.

        byte dimAlpha = _settings.DimAlpha;
        int blurRadius = _settings.BlurRadiusPx;
        var fgRectWpf = new Rect(fgRect.Left, fgRect.Top, fgRect.Right - fgRect.Left, fgRect.Bottom - fgRect.Top);
        Log.Info($"[OverlayManager] UpdateOverlays: overlays={_overlays.Count} dimAlpha={dimAlpha} blurRadius={blurRadius}");

        for (int i = 0; i < _overlays.Count; i++)
        {
            var overlay = _overlays[i];
            var monitorRect = new Rect(overlay.Left, overlay.Top, overlay.Width, overlay.Height);
            var excludeRects = new List<Rect>();

            Rect? fgExclude = Rect.Intersect(fgRectWpf, monitorRect);
            if (fgExclude.HasValue && !fgExclude.Value.IsEmpty)
                excludeRects.Add(fgExclude.Value);

            foreach (var taskbarRect in GetTaskbarRectsIntersecting(monitorRect))
                excludeRects.Add(taskbarRect);

            BitmapSource? blurred = null;
            if (blurRadius > 0 || dimAlpha > 0)
                blurred = BlurHelper.CaptureAndBlur(monitorRect, blurRadius);

            overlay.SetBlurredImage(blurred);
            overlay.SetDimAlpha(dimAlpha);
            overlay.SetExcludeRects(excludeRects);
            overlay.ShowWithFade();
        }
        Log.Info("[OverlayManager] UpdateOverlays() exit");
    }

    private static IEnumerable<Rect> GetTaskbarRectsIntersecting(Rect monitorRect)
    {
        foreach (var className in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
        {
            var hwnd = NativeMethods.FindWindow(className, null);
            if (hwnd == IntPtr.Zero) continue;
            if (!NativeMethods.GetWindowRect(hwnd, out var r)) continue;
            var rect = new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            var intersection = Rect.Intersect(rect, monitorRect);
            if (!intersection.IsEmpty)
                yield return intersection;
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
        Log.Info($"[OverlayManager] HideAll() count={_overlays.Count}");
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
        Log.Info("[OverlayManager] Dispose()");
        if (_disposed) return;
        _foregroundDebounce?.Stop();
        _foregroundDebounce = null;
        if (_winEventHookForeground != IntPtr.Zero)
            NativeMethods.UnhookWinEvent(_winEventHookForeground);
        _winEventHookForeground = IntPtr.Zero;
        foreach (var w in _overlays.ToList())
            w.Close();
        _overlays.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
        Log.Info("[OverlayManager] Dispose() done");
    }
}
