using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Keys = System.Windows.Forms.Keys;

namespace FocusShade;

public partial class SettingsWindow : Window
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "FocusShade";

    private readonly SettingsModel _settings;
    private readonly OverlayManager _overlayManager;
    private readonly HotkeyManager _hotkeyManager;
    private readonly TrayManager? _trayManager;
    private readonly ShakeDetector? _shakeDetector;
    private bool _capturingHotkey;

    public SettingsWindow(SettingsModel settings, OverlayManager overlayManager, HotkeyManager hotkeyManager, TrayManager? trayManager = null, ShakeDetector? shakeDetector = null)
    {
        _settings = settings;
        _overlayManager = overlayManager;
        _hotkeyManager = hotkeyManager;
        _trayManager = trayManager;
        _shakeDetector = shakeDetector;
        InitializeComponent();
        LoadFromSettings();
        UpdateHotkeyButton();
    }

    private void LoadFromSettings()
    {
        ToggleSwitch.IsChecked = _settings.IsEnabled;
        BlurSlider.Value = _settings.BlurIntensity;
        DimSlider.Value = _settings.DimIntensity;
        ShakeCombo.SelectedIndex = (int)_settings.ShakeSensitivity;
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        BlurPercent.Text = $"{_settings.BlurIntensity}%";
        DimPercent.Text = $"{_settings.DimIntensity}%";
    }

    private void UpdateHotkeyButton()
    {
        var (mod, key) = _hotkeyManager.GetCurrent();
        HotkeyButton.Content = ModifierKeyToString(mod) + KeyToString(key);
    }

    private static string ModifierKeyToString(uint mod)
    {
        var parts = new List<string>();
        if ((mod & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mod & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((mod & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((mod & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
        return parts.Count > 0 ? string.Join("+", parts) + "+" : "";
    }

    private static string KeyToString(uint vk)
    {
        try { return Enum.GetName(typeof(Keys), vk) ?? "F"; }
        catch { return "F"; }
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        bool on = ToggleSwitch.IsChecked == true;
        _settings.IsEnabled = on;
        _overlayManager.IsEnabled = on;
        _trayManager?.SetActiveState(on);
    }

    private void BlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _settings.BlurIntensity = (int)BlurSlider.Value;
        BlurPercent.Text = $"{_settings.BlurIntensity}%";
        _overlayManager.RefreshSettings();
    }

    private void DimSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _settings.DimIntensity = (int)DimSlider.Value;
        DimPercent.Text = $"{_settings.DimIntensity}%";
        _overlayManager.RefreshSettings();
    }

    private void ShakeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ShakeCombo.SelectedIndex >= 0)
        {
            _settings.ShakeSensitivity = (ShakeSensitivity)ShakeCombo.SelectedIndex;
            if (_shakeDetector != null)
                _shakeDetector.DistanceThresholdPx = _settings.ShakeDistanceThresholdPx;
            if (_settings.ShakeSensitivity == ShakeSensitivity.Off)
                _shakeDetector?.Stop();
            else
                _shakeDetector?.Start();
        }
    }

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = true;
        HotkeyButton.Content = "Press key...";
        HotkeyButton.Focus();
        KeyDown += CaptureHotkey_KeyDown;
    }

    private void CaptureHotkey_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_capturingHotkey) return;
        e.Handled = true;
        KeyDown -= CaptureHotkey_KeyDown;
        _capturingHotkey = false;

        uint mod = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mod |= NativeMethods.MOD_CONTROL;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mod |= NativeMethods.MOD_SHIFT;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mod |= NativeMethods.MOD_ALT;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mod |= NativeMethods.MOD_WIN;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(e.Key);
        if (vk == 0) vk = 0x46;
        _settings.HotkeyModifiers = mod;
        _settings.HotkeyKey = vk;
        _hotkeyManager.Register(null, mod, vk);
        UpdateHotkeyButton();
    }

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;
            if (_settings.StartWithWindows)
                key.SetValue(AppName, Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location);
            else
                key.DeleteValue(AppName, false);
        }
        catch { }
    }
}
