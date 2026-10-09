using System;
using System.Collections.Generic;

namespace DesktopPet;

// Global cursor samples avoid feedback from the moving window's local coordinates.
public sealed class ThrowTracker
{
    private readonly Queue<(double Time, double X, double Y)> samples = new();
    public void Clear() => samples.Clear();
    public void Add(double time, double x, double y)
    {
        samples.Enqueue((time, x, y));
        while (samples.Count > 1 && time - samples.Peek().Time > 0.12) samples.Dequeue();
    }
    public (double X, double Y) Velocity(double maxSpeed)
    {
        if (samples.Count < 2) return (0, 0);
        var first = samples.Peek();
        var last = first;
        foreach (var sample in samples) last = sample;
        double elapsed = last.Time - first.Time;
        if (elapsed < 0.008) return (0, 0);
        double x = (last.X - first.X) / elapsed;
        double y = (last.Y - first.Y) / elapsed;
        double length = Math.Sqrt(x * x + y * y);
        double scale = length > maxSpeed ? maxSpeed / length : 1;
        return (x * scale, y * scale);
    }
}
