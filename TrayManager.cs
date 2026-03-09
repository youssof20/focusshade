using System.Drawing;
using System.Windows.Forms;

namespace FocusShade;

public sealed class TrayManager : IDisposable
{
    private readonly App _app;
    private readonly OverlayManager _overlayManager;
    private readonly Action _openSettings;
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _toggleItem;
    private Bitmap? _bmFilled;
    private Icon? _iconFilled;
    private Bitmap? _bmRing;
    private Icon? _iconRing;
    private System.Windows.Forms.Timer? _clickTimer;
    private bool _disposed;

    public TrayManager(App app, OverlayManager overlayManager, Action openSettings)
    {
        _app = app;
        _overlayManager = overlayManager;
        _openSettings = openSettings;
    }

    public void Show()
    {
        Log.Info("[TrayManager] Show() enter");
        try
        {
            Log.Info("[TrayManager] CreateFilledCircleIcon");
            (_bmFilled, _iconFilled) = TrayIconHelper.CreateFilledCircleIcon();
            Log.Info("[TrayManager] CreateRingIcon");
            (_bmRing, _iconRing) = TrayIconHelper.CreateRingIcon();

            Log.Info("[TrayManager] new NotifyIcon");
            _notifyIcon = new NotifyIcon
            {
                Icon = _overlayManager.IsEnabled ? _iconFilled : _iconRing,
                Visible = true,
                Text = "FocusShade"
            };

            _toggleItem = new ToolStripMenuItem(GetToggleText(_overlayManager.IsEnabled), null, (_, _) => { Log.Info("[TrayManager] menu Toggle"); SafeToggle(); });
            var menu = new ContextMenuStrip();
            menu.Items.Add(_toggleItem);
            menu.Items.Add("Settings", null, (_, _) => { Log.Info("[TrayManager] menu Settings"); SafeOpenSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit", null, (_, _) => { Log.Info("[TrayManager] menu Quit"); _app.RequestQuit(); });
            _notifyIcon.ContextMenuStrip = menu;

            _clickTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _clickTimer.Tick += (_, _) =>
            {
                Log.Info("[TrayManager] ClickTimer.Tick (single-click -> open settings)");
                _clickTimer?.Stop();
                SafeOpenSettings();
            };
            _notifyIcon.Click += (_, _) => { Log.Info("[TrayManager] Click"); _clickTimer?.Start(); };
            _notifyIcon.DoubleClick += (_, _) =>
            {
                Log.Info("[TrayManager] DoubleClick");
                _clickTimer?.Stop();
                SafeToggle();
            };
            Log.Info("[TrayManager] SetActiveState");
            SetActiveState(_overlayManager.IsEnabled);
            Log.Info("[TrayManager] Show() done");
        }
        catch (Exception ex)
        {
            Log.Error("TrayManager.Show failed", ex);
            throw;
        }
    }

    private void SafeOpenSettings()
    {
        Log.Info("[TrayManager] SafeOpenSettings()");
        try { _openSettings(); }
        catch (Exception ex) { Log.Error("OpenSettings failed", ex); }
    }

    private void SafeToggle()
    {
        Log.Info("[TrayManager] SafeToggle()");
        try { Toggle(); }
        catch (Exception ex) { Log.Error("Toggle failed", ex); }
    }

    private static string GetToggleText(bool isActive) => isActive ? "FocusShade - ON" : "FocusShade - OFF";

    private void Toggle()
    {
        _overlayManager.Toggle();
        SetActiveState(_overlayManager.IsEnabled);
        NativeMethods.MessageBeep(0);
    }

    public void SetActiveState(bool active)
    {
        Log.Info($"[TrayManager] SetActiveState(active={active})");
        try
        {
            if (_disposed || _notifyIcon == null) { Log.Info("[TrayManager] SetActiveState: disposed or null, return"); return; }
            if (_toggleItem != null)
                _toggleItem.Text = GetToggleText(active);
            _notifyIcon.Text = "FocusShade" + (active ? " (active)" : "");
            if (_iconFilled != null && _iconRing != null)
                _notifyIcon.Icon = active ? _iconFilled : _iconRing;
        }
        catch (Exception ex)
        {
            Log.Error("SetActiveState failed", ex);
        }
    }

    public void Dispose()
    {
        Log.Info("[TrayManager] Dispose()");
        if (_disposed) return;
        _clickTimer?.Stop();
        _clickTimer?.Dispose();
        _clickTimer = null;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        _iconFilled?.Dispose();
        _iconFilled = null;
        _iconRing?.Dispose();
        _iconRing = null;
        _bmFilled?.Dispose();
        _bmFilled = null;
        _bmRing?.Dispose();
        _bmRing = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
