namespace ClockNode;

public sealed class SimulatedClock
{
    private readonly object _sync = new();
    private double _offsetMs;

    public SimulatedClock(IConfiguration config)
    {
        _offsetMs = double.TryParse(
            config["INITIAL_OFFSET_MS"],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value) ? value : 0;
    }

    public DateTimeOffset Now()
    {
        lock (_sync)
            return DateTimeOffset.UtcNow.AddMilliseconds(_offsetMs);
    }

    public double OffsetMilliseconds
    {
        get { lock (_sync) return _offsetMs; }
    }

    public double Adjust(double deltaMs)
    {
        lock (_sync)
        {
            _offsetMs += deltaMs;
            return _offsetMs;
        }
    }
}