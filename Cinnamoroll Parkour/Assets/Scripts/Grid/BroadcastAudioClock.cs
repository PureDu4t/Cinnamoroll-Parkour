using System;

/// <summary>Unwraps the actual Wwise source position; frame timing cannot accumulate beat drift.</summary>
public sealed class BroadcastAudioClock
{
    private readonly double loopLength;
    private double previous = -1, loopOffset, elapsed;
    public BroadcastAudioClock(double loopLengthSeconds) { loopLength = Math.Max(.001, loopLengthSeconds); }
    public double Observe(double sourceSeconds)
    {
        sourceSeconds = Math.Max(0, sourceSeconds);
        // Small backward query jitter is not a source loop.
        if (previous >= 0 && sourceSeconds < previous - loopLength * .5) loopOffset += loopLength;
        previous = sourceSeconds;
        elapsed = Math.Max(elapsed, loopOffset + sourceSeconds);
        return elapsed;
    }
}
