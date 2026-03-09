using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Keys = System.Windows.Forms.Keys;

namespace FocusShade;

public partial class SettingsWindow : Window
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "FocusShade";
    private const string Version = "v0.1.0";

    private readonly SettingsModel _settings;
    private readonly OverlayManager _overlayManager;
    private readonly HotkeyManager _hotkeyManager;
    private readonly TrayManager? _trayManager;
    private readonly ShakeDetector? _shakeDetector;
    private bool _capturingHotkey;
    private Storyboard? _powerGlowStoryboard;

    public SettingsWindow(SettingsModel settings, OverlayManager overlayManager, HotkeyManager hotkeyManager, TrayManager? trayManager = null, ShakeDetector? shakeDetector = null)
    {
        _settings = settings;
        _overlayManager = overlayManager;
        _hotkeyManager = hotkeyManager;
        _trayManager = trayManager;
        _shakeDetector = shakeDetector;
        InitializeComponent();
        VersionText.Text = $"FOCUSSHADE {Version}";
        LoadFromSettings();
        UpdateHotkeyPills();
        SyncShakeSegments();
    }

    private void LoadFromSettings()
    {
        PowerButton.IsChecked = _settings.IsEnabled;
        BlurSlider.Value = _settings.BlurIntensity;
        DimSlider.Value = _settings.DimIntensity;
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        BlurPercent.Text = $"{_settings.BlurIntensity}%";
        DimPercent.Text = $"{_settings.DimIntensity}%";
        SyncShakeSegments();
        StartOrStopPowerGlow(_settings.IsEnabled);
    }

    private void SyncShakeSegments()
    {
        ShakeOff.IsChecked = _settings.ShakeSensitivity == ShakeSensitivity.Off;
        ShakeLow.IsChecked = _settings.ShakeSensitivity == ShakeSensitivity.Low;
        ShakeMed.IsChecked = _settings.ShakeSensitivity == ShakeSensitivity.Medium;
        ShakeHigh.IsChecked = _settings.ShakeSensitivity == ShakeSensitivity.High;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void PowerButton_Click(object sender, RoutedEventArgs e)
    {
        bool on = PowerButton.IsChecked == true;
        _settings.IsEnabled = on;
        _overlayManager.IsEnabled = on;
        _trayManager?.SetActiveState(on);
        StartOrStopPowerGlow(on);
    }

    private void PowerButton_Changed(object sender, RoutedEventArgs e)
    {
        bool on = PowerButton.IsChecked == true;
        _settings.IsEnabled = on;
        _overlayManager.IsEnabled = on;
        _trayManager?.SetActiveState(on);
        StartOrStopPowerGlow(on);
    }

    private void StartOrStopPowerGlow(bool active)
    {
        _powerGlowStoryboard?.Stop();
        _powerGlowStoryboard = null;
        if (!active) return;
        var anim = new DoubleAnimation(1, 0.65, TimeSpan.FromSeconds(1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        _powerGlowStoryboard = new Storyboard();
        Storyboard.SetTarget(anim, PowerButton);
        Storyboard.SetTargetProperty(anim, new PropertyPath(OpacityProperty));
        _powerGlowStoryboard.Children.Add(anim);
        _powerGlowStoryboard.Begin();
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

    private void ShakeSegment_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Primitives.ToggleButton btn || btn != ShakeOff && btn != ShakeLow && btn != ShakeMed && btn != ShakeHigh) return;
        ShakeOff.IsChecked = btn == ShakeOff;
        ShakeLow.IsChecked = btn == ShakeLow;
        ShakeMed.IsChecked = btn == ShakeMed;
        ShakeHigh.IsChecked = btn == ShakeHigh;
        _settings.ShakeSensitivity = btn == ShakeOff ? ShakeSensitivity.Off
            : btn == ShakeLow ? ShakeSensitivity.Low
            : btn == ShakeMed ? ShakeSensitivity.Medium
            : ShakeSensitivity.High;
        if (_shakeDetector != null)
            _shakeDetector.DistanceThresholdPx = _settings.ShakeDistanceThresholdPx;
        if (_settings.ShakeSensitivity == ShakeSensitivity.Off)
            _shakeDetector?.Stop();
        else
            _shakeDetector?.Start();
    }

    private void UpdateHotkeyPills()
    {
        var (mod, key) = _hotkeyManager.GetCurrent();
        var pills = new List<string>();
        if ((mod & NativeMethods.MOD_CONTROL) != 0) pills.Add("CTRL");
        if ((mod & NativeMethods.MOD_SHIFT) != 0) pills.Add("SHIFT");
        if ((mod & NativeMethods.MOD_ALT) != 0) pills.Add("ALT");
        if ((mod & NativeMethods.MOD_WIN) != 0) pills.Add("WIN");
        try { pills.Add((Enum.GetName(typeof(Keys), key) ?? "F").ToUpperInvariant()); }
        catch { pills.Add("F"); }
        HotkeyPills.ItemsSource = pills;
    }

    private void HotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = true;
        HotkeyPills.ItemsSource = new[] { "…" };
        HotkeyPillsButton.Focus();
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
        UpdateHotkeyPills();
    }

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;
            if (_settings.StartWithWindows)
                key.SetValue(AppName, Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "FocusShade.exe"));
            else
                key.DeleteValue(AppName, false);
        }
        catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        _powerGlowStoryboard?.Stop();
        base.OnClosed(e);
    }
}
