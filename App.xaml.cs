using System.Windows;
using System.Windows.Threading;

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
        Log.Info("FocusShade starting");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            Log.Error("Unhandled exception", ex);
            ShowError("Unhandled exception", ex);
        };
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Dispatcher unhandled exception", args.Exception);
            ShowError("FocusShade error", args.Exception);
            args.Handled = true;
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unobserved task exception", args.Exception);
            ShowError("Background task error", args.Exception?.GetBaseException() ?? args.Exception);
            args.SetObserved();
        };

        if (!SingleInstanceMutex.WaitOne(0, false))
        {
            Log.Info("Another instance already running; exiting.");
            System.Windows.MessageBox.Show("FocusShade is already running.", "FocusShade", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        try
        {
            _settings = SettingsModel.Load();
            Log.Info("Settings loaded");
            _overlayManager = new OverlayManager(_settings);
            _overlayManager.Start();
            _overlayManager.IsEnabled = _settings.IsEnabled;
            Log.Info("OverlayManager started");

            _shakeDetector = new ShakeDetector();
            _shakeDetector.DistanceThresholdPx = _settings.ShakeDistanceThresholdPx;
            _shakeDetector.ShakeDetected += OnShakeOrHotkeyToggle;
            if (_settings.ShakeSensitivity != ShakeSensitivity.Off)
                _shakeDetector.Start();
            Log.Info("ShakeDetector started");

            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.HotkeyPressed += OnShakeOrHotkeyToggle;
            _hotkeyManager.Register(null, _settings.HotkeyModifiers, _settings.HotkeyKey);
            Log.Info("HotkeyManager registered");

            void OpenSettings()
            {
                Log.Info("[App] OpenSettings()");
                if (_settingsWindow == null || !_settingsWindow.IsLoaded)
                {
                    Log.Info("[App] OpenSettings: creating SettingsWindow");
                    _settingsWindow = new SettingsWindow(_settings!, _overlayManager!, _hotkeyManager!, _trayManager, _shakeDetector);
                    _settingsWindow.Closed += (_, _) => { Log.Info("[App] SettingsWindow Closed"); _settingsWindow = null; };
                }
                Log.Info("[App] OpenSettings: Show+Activate");
                _settingsWindow.Show();
                _settingsWindow.Activate();
            }

            _trayManager = new TrayManager(this, _overlayManager, OpenSettings);
            _trayManager.Show();
            _trayManager.SetActiveState(_overlayManager.IsEnabled);
            Log.Info("Tray shown; startup complete.");
        }
        catch (Exception ex)
        {
            Log.Error("Startup failed", ex);
            System.Windows.MessageBox.Show(
                $"FocusShade failed to start. Check the log:\n{Log.LogFilePath}",
                "FocusShade", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void OnShakeOrHotkeyToggle()
    {
        Log.Info("[App] OnShakeOrHotkeyToggle()");
        try
        {
            if (_overlayManager == null) return;
            _overlayManager.Toggle();
            NativeMethods.MessageBeep(0);
            _trayManager?.SetActiveState(_overlayManager.IsEnabled);
        }
        catch (Exception ex)
        {
            Log.Error("OnShakeOrHotkeyToggle failed", ex);
            ShowError("Toggle failed", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info($"[App] OnExit called, exit code {e.ApplicationExitCode}");
        try
        {
            _settings?.Save();
            _hotkeyManager?.Dispose();
            _overlayManager?.Dispose();
            _shakeDetector?.Dispose();
            _trayManager?.Dispose();
            _settingsWindow?.Close();
            SingleInstanceMutex.ReleaseMutex();
            SingleInstanceMutex.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error("Error during exit", ex);
        }
        base.OnExit(e);
    }

    public void RequestQuit()
    {
        Log.Info("[App] RequestQuit() -> Shutdown");
        Shutdown();
    }

    private static void ShowError(string title, Exception ex)
    {
        try
        {
            var msg = ex?.ToString() ?? "Unknown error";
            var logPath = Log.LogFilePath;
            System.Windows.MessageBox.Show(
                $"{title}\n\n{ex?.Message}\n\nLog: {logPath}\n\nDetails:\n{msg}",
                "FocusShade",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch { }
    }
}
