using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    private static readonly Geometry MutantLeftShell = Parse("M -25,0 C -33,-4 -27,-15 -24,-22 C -22,-34 -9,-33 -10,-23 C -13,-18 -14,-11 -7,-4 C -9,1 -18,-1 -25,0 Z");
    private static readonly Geometry MutantRightShell = Parse("M 8,-3 C 14,-9 13,-16 10,-24 C 7,-35 20,-36 23,-25 C 24,-17 31,-16 29,-8 C 33,2 17,1 8,-3 Z");
    private static readonly Geometry MutantCavity = Parse("M -8,-3 C -12,-10 -10,-21 -5,-26 C -2,-30 5,-27 8,-22 C 11,-16 11,-8 7,-3 C 3,0 -4,0 -8,-3 Z");
    private static readonly Geometry MutantBaseBridge = Parse("M -29,-2 C -15,-6 14,-6 32,-2 L 31,1 Q 0,5 -28,1 Z");
    private static double MutantOpen(MutantBoss? m)
    {
        if (m == null) return 0;
        double t = m.Mode == MutantMode.Core ? m.PoseAge / 0.45 : m.PreviousMode == MutantMode.Core ? 1 - m.PoseAge / 0.32 : 0;
        t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t);
    }
    private static Geometry MutantShellBody(Slime s)
    {
        double open = MutantOpen(s.Mutant);
        if (open <= 0.01) return (s.Mutant?.Phase ?? 1) switch { 2 => MutationBody2, 3 => MutationBody3, _ => MutationBody };
        var left = MutantLeftShell.Clone(); left.Transform = new TranslateTransform((1 - open) * 7 - open * 3, 0);
        var right = MutantRightShell.Clone(); right.Transform = new TranslateTransform(-(1 - open) * 8 + open * 3, 0);
        return new CombinedGeometry(GeometryCombineMode.Union, new CombinedGeometry(GeometryCombineMode.Union, left, right), MutantBaseBridge);
    }
    // The same asymmetric seed, membrane and internal veins appear in the exposed core, thrown core and sealed bumps.
    private static void DrawLivingMutantCore(DrawingContext dc, Point center, double size, double time, int cracks = 0)
    {
        double beat = 1 + 0.045 * Math.Sin(time * 5);
        dc.PushTransform(new TranslateTransform(center.X, center.Y));
        dc.PushTransform(new ScaleTransform(size * beat, size / beat));
        var seed = GelMembrane(new Point(0, 0), 5.1, 8, time * 0.25, 2);
        dc.DrawGeometry(MutationCore, GelVein, seed);
        dc.DrawLine(GelVein, new Point(-1, -6), new Point(1, 5));
        dc.DrawLine(GelVein, new Point(0, -2), new Point(-3, 1));
        dc.DrawLine(GelVein, new Point(0, 2), new Point(3, 0));
        dc.DrawEllipse(GelShine, null, new Point(-1.8, -3.5), 1.2, 2);
        for (int i = 0; i < cracks; i++)
        {
            double y = -5 + i * 1.8;
            dc.DrawLine(MutationEyeLine, new Point(i % 2 == 0 ? -3.5 : 3.5, y), new Point(0, y + 1));
        }
        dc.Pop(); dc.Pop();
    }
    private static void DrawExposedMutantCore(DrawingContext dc, Slime s, double time, double open)
    {
        var m = s.Mutant!;
        dc.PushOpacity(open);
        dc.PushTransform(new ScaleTransform(Math.Max(0.05, open), 1));
        dc.DrawGeometry(MutationInk, GelVein, MutantCavity);
        Point core = new(0, -14 + Math.Sin(time * 2) * 0.7);
        for (int side = -1; side <= 1; side += 2)
        {
            dc.DrawLine(GelVein, new Point(side * 11, -19), core + new Vector(side * 3, -3));
            dc.DrawLine(GelVein, new Point(side * 9, -4), core + new Vector(side * 2, 5));
        }
        DrawLivingMutantCore(dc, core, 1, time, m.CoreHits);
        dc.Pop(); dc.Pop();
    }
    private static (double Gather, double Strike, MutantShape Shape) MutantActionPose(Slime s)
    {
        if (s.Mutant is not { } m) return default;
        MutantZone? next = null;
        double best = double.PositiveInfinity;
        foreach (var z in m.Zones)
        {
            if (z.Age >= z.Warn + z.Life) continue;
            double score = z.Age < z.Warn ? z.Warn - z.Age : 100 + z.Age - z.Warn;
            if (score < best) { best = score; next = z; }
        }
        if (next == null) return default;
        double gather = next.Age < next.Warn ? Math.Clamp(next.Age / Math.Max(0.01, next.Warn), 0, 1) : 0;
        double strike = next.Live ? Math.Sin(Math.Clamp((next.Age - next.Warn) / Math.Max(0.01, next.Life), 0, 1) * Math.PI) : 0;
        return (gather, strike, next.Shape);
    }
    private static void MutantAnatomyPose(Slime s, ref double sx, ref double sy)
    {
        var action = MutantActionPose(s); var m = s.Mutant!;
        double open = MutantOpen(m);
        sx *= 1 + open * 0.12;
        sy *= 1 - open * 0.18;
        if (m.Mode == MutantMode.AllIn && m.PoseAge < 1.2)
        {
            double melt = Math.Sin(Math.Clamp((m.PoseAge - 0.25) / 0.8, 0, 1) * Math.PI);
            sx *= 1 + melt * 0.4; sy *= 1 - melt * 0.55;
        }
        if (action.Shape is MutantShape.Breath or MutantShape.Goo or MutantShape.Waterfall)
        { sx *= 1 + action.Gather * 0.15 - action.Strike * 0.07; sy *= 1 - action.Gather * 0.1 + action.Strike * 0.13; }
        else if (action.Shape is MutantShape.Hand or MutantShape.Fan)
        { sx *= 1 - action.Gather * 0.06 + action.Strike * 0.15; sy *= 1 + action.Gather * 0.1 - action.Strike * 0.12; }
        else { sx *= 1 + action.Gather * 0.08; sy *= 1 - action.Gather * 0.06; }
    }
    private static Point MutantEmission(Slime s, MutantShape shape, Func<double, double, Point> at)
    {
        double h = shape == MutantShape.Eye || shape == MutantShape.Column ? 0.62 : shape == MutantShape.Hand ? 0.48 : 0.36;
        double x = shape is MutantShape.Goo or MutantShape.Breath or MutantShape.Hand ? s.Facing * s.Width * 0.22 : 0;
        return at(s.X + x, s.Height * h);
    }
    // Guide geometry is independent of the cat icon. The icon sits beside the shaft, never at either endpoint.
    internal static (Point Start, Point End, Point Icon) MutantGuide(Point target, int side, double minX, double maxX, double top)
    {
        if (side == 0)
        {
            Point end = new(target.X, target.Y - 28);
            Point start = new(target.X, Math.Max(top + 22, target.Y - 112));
            double iconX = target.X + 38;
            if (iconX + 26 > maxX) iconX = target.X - 38;
            return (start, end, new Point(Math.Clamp(iconX, minX + 12, maxX - 26), (start.Y + end.Y) / 2));
        }
        Point e = new(target.X - side * 30, target.Y);
        Point a = new(target.X - side * 116, target.Y);
        double iconY = target.Y - 34 < top + 16 ? target.Y + 34 : target.Y - 34;
        return (a, e, new Point(Math.Clamp((a.X + e.X) / 2 - 7, minX + 12, maxX - 26), iconY));
    }
}
