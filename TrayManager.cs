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
        (_bmFilled, _iconFilled) = TrayIconHelper.CreateFilledCircleIcon();
        (_bmRing, _iconRing) = TrayIconHelper.CreateRingIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _overlayManager.IsEnabled ? _iconFilled : _iconRing,
            Visible = true,
            Text = "FocusShade"
        };

        _toggleItem = new ToolStripMenuItem(GetToggleText(_overlayManager.IsEnabled), null, (_, _) => Toggle());
        var menu = new ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add("Settings", null, (_, _) => _openSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => _app.RequestQuit());
        _notifyIcon.ContextMenuStrip = menu;

        _clickTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            _openSettings();
        };
        _notifyIcon.Click += (_, _) => _clickTimer?.Start();
        _notifyIcon.DoubleClick += (_, _) =>
        {
            _clickTimer?.Stop();
            Toggle();
        };
        SetActiveState(_overlayManager.IsEnabled);
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
        if (_toggleItem != null)
            _toggleItem.Text = GetToggleText(active);
        _notifyIcon!.Text = "FocusShade" + (active ? " (active)" : "");
        if (_notifyIcon != null && _iconFilled != null && _iconRing != null)
            _notifyIcon.Icon = active ? _iconFilled : _iconRing;
    }

    public void Dispose()
    {
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
