using System.Windows;
using System.Windows.Interop;

namespace FocusShade;

public class HotkeyManager : IDisposable
{
    private const int HotkeyId = 0x0001;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private bool _registered;
    private bool _disposed;
    private uint _modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
    private uint _key = 0x46; // VK_F
    private Window? _hiddenWindow;

    public event Action? HotkeyPressed;

    public void Register(Window? owner, uint modifiers, uint key)
    {
        Log.Info($"[HotkeyManager] Register owner={owner != null} mod={modifiers} key={key}");
        Unregister();
        _modifiers = modifiers;
        _key = key;

        Window? target = owner;
        if (target == null)
        {
            _hiddenWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.ToolWindow,
                ShowInTaskbar = false,
                Visibility = Visibility.Hidden
            };
            _hiddenWindow.Show();
            target = _hiddenWindow;
        }

        var helper = new WindowInteropHelper(target);
        _hwnd = helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(_hwnd);

        if (_hwnd == IntPtr.Zero || _hwndSource == null)
            return;

        _hwndSource.AddHook(WndProc);
        _registered = NativeMethods.RegisterHotKey(_hwnd, HotkeyId, _modifiers, _key);
        Log.Info($"[HotkeyManager] RegisterHotKey result={_registered}");
    }

    public void Unregister()
    {
        Log.Info("[HotkeyManager] Unregister()");
        if (_registered && _hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
        _hwnd = IntPtr.Zero;
        _hiddenWindow?.Close();
        _hiddenWindow = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam == (IntPtr)HotkeyId)
        {
            Log.Info("[HotkeyManager] WM_HOTKEY received");
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public (uint modifiers, uint key) GetCurrent() => (_modifiers, _key);

    public void Dispose()
    {
        if (_disposed) return;
        Unregister();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
