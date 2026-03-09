using System.Windows;

namespace FocusShade;

public partial class App : System.Windows.Application
{
    private static readonly Mutex SingleInstanceMutex = new(false, "FocusShade_SingleInstance");
    private TrayManager? _trayManager;
    private SettingsModel? _settings;
    private OverlayManager? _overlayManager;
    private ShakeDetector? _shakeDetector;
    private HotkeyManager? _hotkeyManager;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstanceMutex.WaitOne(0, false))
        {
            System.Windows.MessageBox.Show("FocusShade is already running.", "FocusShade", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        _settings = SettingsModel.Load();
        _overlayManager = new OverlayManager(_settings);
        _overlayManager.Start();
        _overlayManager.IsEnabled = _settings.IsEnabled;

        _shakeDetector = new ShakeDetector();
        _shakeDetector.DistanceThresholdPx = _settings.ShakeDistanceThresholdPx;
        _shakeDetector.ShakeDetected += OnShakeOrHotkeyToggle;
        if (_settings.ShakeSensitivity != ShakeSensitivity.Off)
            _shakeDetector.Start();

        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.HotkeyPressed += OnShakeOrHotkeyToggle;
        _hotkeyManager.Register(null, _settings.HotkeyModifiers, _settings.HotkeyKey);

        void OpenSettings()
        {
            if (_settingsWindow == null || !_settingsWindow.IsLoaded)
            {
                _settingsWindow = new SettingsWindow(_settings!, _overlayManager!, _hotkeyManager!, _trayManager, _shakeDetector);
                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            }
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        _trayManager = new TrayManager(this, _overlayManager, OpenSettings);
        _trayManager.Show();
        _trayManager.SetActiveState(_overlayManager.IsEnabled);
    }

    private void OnShakeOrHotkeyToggle()
    {
        if (_overlayManager == null) return;
        _overlayManager.Toggle();
        NativeMethods.MessageBeep(0);
        _trayManager?.SetActiveState(_overlayManager.IsEnabled);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settings?.Save();
        _hotkeyManager?.Dispose();
        _overlayManager?.Dispose();
        _shakeDetector?.Dispose();
        _trayManager?.Dispose();
        _settingsWindow?.Close();
        SingleInstanceMutex.ReleaseMutex();
        SingleInstanceMutex.Dispose();
        base.OnExit(e);
    }

    public void RequestQuit()
    {
        Shutdown();
    }
}
