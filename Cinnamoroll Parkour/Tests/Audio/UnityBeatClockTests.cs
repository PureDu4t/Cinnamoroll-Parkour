using System;

internal static class UnityBeatClockTests
{
    private static void Close(double actual, double expected, double tolerance = 1e-8)
    {
        if (Math.Abs(actual - expected) > tolerance)
            throw new Exception("Expected " + expected + ", got " + actual);
    }
    public static void Main()
    {
        var clock = new BroadcastBeatClock(100, 90);
        Close(clock.SecondsPerBeat, 2.0 / 3.0);
        foreach (double press in new[] { 100.01, 100.2, 100.65 })
            Close(clock.TimeAtStep(clock.NextStepIndexAfter(press)), 100 + 2.0 / 3.0);
        Close(clock.TimeAtStep(clock.NextStepIndexAfter(101.1)), 100 + 4.0 / 3.0);
        Console.WriteLine("90 BPM: keypresses share the next running-clock beat; no keypress reset.");

        for (long i = 1; i < 100000; i += 13)
        {
            double boundary = clock.TimeAtStep(i);
            long next = clock.NextStepIndexAfter(boundary);
            if (next != i + 1) throw new Exception("Boundary repeated instead of next beat.");
        }
        Close(clock.TimeAtStep(clock.NextStepIndexAfter(100.1, .5), .5), 100 + 1.0 / 3.0);
        Console.WriteLine("Exact boundaries and half-beat subdivisions passed.");

        foreach (double fps in new[] { 15.0, 30.0, 60.0, 144.0 })
        {
            var longClock = new BroadcastBeatClock(.137, 90);
            long next = 1;
            double maxLate = 0;
            int frameCount = (int)(1800 * fps);
            for (int frame = 0; frame <= frameCount; frame++)
            {
                double now = frame / fps;
                double target = longClock.TimeAtStep(next);
                if (now < target) continue;
                maxLate = Math.Max(maxLate, now - target);
                next++;
            }
            if (maxLate > 1.0 / fps + 1e-8) throw new Exception("Accumulated timing drift.");
            Console.WriteLine(fps + " FPS, 30 minutes: max frame sampling lateness " + (maxLate * 1000).ToString("F2") + " ms; no accumulated drift.");
        }
        var stalledClock = new BroadcastBeatClock(0, 90);
        long resumed = stalledClock.NextStepIndexAfter(4.9);
        Close(stalledClock.TimeAtStep(resumed), 16.0 / 3.0);
        if (stalledClock.TimeAtStep(resumed) <= 4.9) throw new Exception("Stall resumed in the past.");
        Console.WriteLine("Long stall: next future beat remains on the original grid.");
    }
}
