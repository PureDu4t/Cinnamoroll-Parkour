using System;

/// <summary>Absolute beat deadlines; callers supply Unity DSP time, never frame deltas.</summary>
public sealed class BroadcastBeatClock
{
    public double Origin { get; }
    public double BeatsPerMinute { get; }
    public double SecondsPerBeat { get; }

    public BroadcastBeatClock(double origin, double beatsPerMinute)
    {
        if (double.IsNaN(origin) || double.IsInfinity(origin))
            throw new ArgumentOutOfRangeException(nameof(origin));
        if (beatsPerMinute <= 0 || double.IsNaN(beatsPerMinute) || double.IsInfinity(beatsPerMinute))
            throw new ArgumentOutOfRangeException(nameof(beatsPerMinute));
        Origin = origin;
        BeatsPerMinute = beatsPerMinute;
        SecondsPerBeat = 60.0 / beatsPerMinute;
    }

    public long NextStepIndexAfter(double now, double beatsPerStep = 1)
    {
        double interval = Interval(beatsPerStep);
        long index = Math.Max(1L, (long)Math.Floor((now - Origin) / interval) + 1L);
        // Guard a rounded division at a boundary: the target must be strictly future.
        if (TimeAtStep(index, beatsPerStep) <= now) index++;
        return index;
    }

    public double TimeAtStep(long index, double beatsPerStep = 1)
        => Origin + index * Interval(beatsPerStep);

    private double Interval(double beatsPerStep)
    {
        if (beatsPerStep <= 0 || double.IsNaN(beatsPerStep) || double.IsInfinity(beatsPerStep))
            throw new ArgumentOutOfRangeException(nameof(beatsPerStep));
        return SecondsPerBeat * beatsPerStep;
    }
}
