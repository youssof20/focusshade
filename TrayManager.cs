using System.Windows.Forms;

namespace FocusShade;

public sealed class TrayManager : IDisposable
{
    private readonly App _app;
    private readonly OverlayManager _overlayManager;
    private readonly Action _openSettings;
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _toggleItem;
    private bool _disposed;

    public TrayManager(App app, OverlayManager overlayManager, Action openSettings)
    {
        _app = app;
        _overlayManager = overlayManager;
        _openSettings = openSettings;
    }

    public void Show()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? SystemIcons.Application,
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

        _notifyIcon.DoubleClick += (_, _) => Toggle();
        SetActiveState(_overlayManager.IsEnabled);
    }

    private static string GetToggleText(bool isActive) => isActive ? "FocusShade - ON" : "FocusShade - OFF";

    private void Toggle()
    {
        _overlayManager.Toggle();
        SetActiveState(_overlayManager.IsEnabled);
        NativeMethods.MessageBeep(0);
    }

    private void OpenSettings()
    {
        _openSettings();
    }

    public void SetActiveState(bool active)
    {
        if (_toggleItem != null)
            _toggleItem.Text = GetToggleText(active);
        _notifyIcon!.Text = "FocusShade" + (active ? " (active)" : "");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
