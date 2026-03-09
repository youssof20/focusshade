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
    private int _tickCount;

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
        Log.Info("[ShakeDetector] Start()");
        _history.Clear();
        _tickCount = 0;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _history.Clear();
    }

    private void Tick(object? sender, EventArgs e)
    {
        try
        {
            _tickCount++;
            if (_tickCount % 20 == 1)
                Log.Info($"[ShakeDetector] Tick #{_tickCount}");
            if (_disposed) return;
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
                Log.Info($"[ShakeDetector] ShakeDetected! distance={totalDistance:F0}");
                _history.Clear();
                try { ShakeDetected?.Invoke(); }
                catch (Exception ex) { Log.Error("ShakeDetected handler failed", ex); }
            }
        }
        catch (Exception ex)
        {
            Log.Error("ShakeDetector.Tick failed", ex);
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
