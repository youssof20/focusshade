using System.Windows.Threading;

namespace FocusShade;

public class ShakeDetector : IDisposable
{
    private readonly DispatcherTimer _timer = new();
    private readonly Queue<(long timeMs, NativeMethods.POINT point)> _history = new();
    private const int WindowMs = 300;
    private const int TickMs = 50;
    private int _distanceThresholdPx = 500;
    private bool _disposed;

    public event Action? ShakeDetected;

    public ShakeDetector()
    {
        _timer.Interval = TimeSpan.FromMilliseconds(TickMs);
        _timer.Tick += Tick;
    }

    public int DistanceThresholdPx
    {
        get => _distanceThresholdPx;
        set => _distanceThresholdPx = value;
    }

    public bool IsRunning => _timer.IsEnabled;

    public void Start()
    {
        _history.Clear();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _history.Clear();
    }

    private void Tick(object? sender, EventArgs e)
    {
        if (!NativeMethods.GetCursorPos(out var pt))
            return;

        long now = Environment.TickCount64;
        _history.Enqueue((now, pt));

        while (_history.Count > 0 && now - _history.Peek().timeMs > WindowMs)
            _history.Dequeue();

        if (_history.Count < 2) return;

        double totalDistance = 0;
        var arr = _history.ToArray();
        for (int i = 1; i < arr.Length; i++)
        {
            var a = arr[i - 1].point;
            var b = arr[i].point;
            totalDistance += Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
        }

        if (totalDistance >= _distanceThresholdPx)
        {
            _history.Clear();
            ShakeDetected?.Invoke();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _timer.Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
