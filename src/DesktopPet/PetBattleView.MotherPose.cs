using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    internal readonly record struct MotherPose(double SX, double SY, double Lean, Point Left, Point Right, double BowAngle, double Open, double Charge, double Strike);
    private static double MotherEase(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
    internal static Point MotherBowCentre(Slime s) => new(-s.Facing * 7, -34 * 1.2);
    internal static double MotherBowScale(Slime s) => s.Mother?.Phase == 3 ? .95 : 1.03;
    internal static double MotherBowAngle(Slime s) => Math.Clamp(SampleMother(s).BowAngle + (s.Mother?.Phase == 2 ? -8 * s.Facing : 0), -12, 12);
    // One pure sample for both the body and its emissions. No render-frame accumulation or wall clock.
    internal static MotherPose SampleMother(Slime s)
    {
        var m = s.Mother;
        double time = m?.Age ?? s.StateTime, age = m?.ActionAge ?? 100, warn = Math.Max(.1, m?.ActionWarn ?? 1.1);
        double end = m?.ActionEnd ?? 0;
        double charge = MotherEase(age / warn), release = MotherEase((age - warn) / .18);
        double recovery = Math.Exp(-5 * Math.Max(0, age - end));
        double envelope = age < end ? 1 : recovery;
        if (m?.Protected == true) envelope *= 1 - MotherEase(m.ModeAge / .4);
        charge *= envelope; release *= envelope;
        double effort = charge * (1 - release), impact = release * Math.Exp(-3 * Math.Max(0, age - warn));
        double speed = Math.Clamp((m?.Velocity ?? 0) / 130, -1, 1);
        if (s.State == SlimeState.Stunned || s.Handled) speed = 0;
        if (m?.Protected == true) speed *= 1 - MotherEase(m.ModeAge / .3);
        double gait = Math.Sin(time * 7) * Math.Abs(speed), breathe = Math.Sin(time * 3.2) * .018;
        double sx = 1 + breathe + gait * .025, sy = 1 - breathe - gait * .03;
        double lean = speed * 2.2;
        double lx = -12, rx = 12, ly = -13, ry = -13, bow = Math.Sin(time * 2.5) * 3 - speed * 8;
        int move = m?.PoseMove ?? -1;
        switch (move)
        {
            case 0: // Alternate palms, not two identical wings.
                lx -= 8 * charge; ly -= 11 * effort; rx += 5 * charge;
                ry -= 10 * MotherEase((age - .4) / warn) * envelope; lean -= s.Facing * effort * 1.8; break;
            case 1: sx += .09 * effort - .04 * impact; sy -= .09 * effort - .07 * impact;
                lx += 5 * effort; rx -= 5 * effort; ly += 2 * effort - 12 * release; ry = ly; break;
            case 2: case 8:
                lx -= 12 * charge * (1 - release); rx += 12 * charge * (1 - release);
                lx += 6 * release; rx -= 6 * release; ly -= 7 * charge; ry = ly;
                sx -= .07 * effort; sy += .07 * effort; lean += s.Facing * impact * 2; break;
            case 3: lx -= 7 * effort; rx += 7 * effort; ly -= 12 * effort; ry -= 8 * effort;
                sy += .06 * effort; bow += 9 * effort; break;
            case 4: case 6: lx -= 8 * charge; rx += 8 * charge; ly -= 10 * charge; ry = ly;
                bow += Math.Sin(time * 5) * 9 * charge; sy += .04 * effort; break;
            case 5: sx += .12 * effort; sy -= .12 * effort; ly += 6 * charge; ry = ly; break;
            case 7: lx += 3 * effort; rx -= 3 * effort; ly -= 12 * charge; ry = ly;
                sy += .07 * effort; bow -= 13 * effort; break;
        }
        double modeIn = MotherEase((m?.ModeAge ?? 1) / .55), open = 0;
        if (m != null)
        {
            if (m.Mode is MotherMode.HangingEggs or MotherMode.Cradle) { ly -= 7 * modeIn; ry -= 7 * modeIn; sx += .035 * modeIn; }
            if (m.Mode == MotherMode.Lullaby) { lean += Math.Sin(time * 2) * 1.4 * modeIn; bow += Math.Sin(time * 2) * 6; }
            if (m.Mode == MotherMode.Stroke) { sx += .07 * modeIn; sy -= .07 * modeIn; }
            if (m.Mode == MotherMode.LastEgg)
                foreach (var z in m.Zones)
                    if (z.Art == MotherArt.Heartbeat)
                    {
                        double t = z.Age - z.Warn - z.Life;
                        open = Math.Max(open, MotherEase((t + .15) / .2) * (1 - MotherEase((t - 1.6) / .2)));
                        double pulse = MotherEase(z.Age / Math.Max(.1,z.Warn)) * Math.Exp(-6 * Math.Max(0,z.Age-z.Warn));
                        sx += .05 * pulse; sy -= .045 * pulse;
                    }
            if (m.Mode == MotherMode.Sleeping) { sx += .12 * modeIn; sy -= .25 * modeIn; ly += 3 * modeIn; ry = ly; bow -= 15 * modeIn; }
            lx -= open * 5; rx += open * 5; ly -= open * 2; ry -= open * 2;
        }
        if (s.State == SlimeState.Stunned) { lean += Math.Sin(time * 9) * Math.Exp(-s.StateTime * 3) * 3; bow += Math.Sin(time * 8) * 15; }
        return new(sx, sy, lean, new(lx, ly), new(rx, ry), bow, open, effort, impact);
    }
    internal static Point MotherEmission(Slime s, MotherArt art, Func<double, double, Point> at, int side = 1)
    {
        var p = SampleMother(s);
        Point local = art switch { MotherArt.Hand => side < 0 ? p.Left : p.Right,
            MotherArt.Ribbon => MotherBowCentre(s), MotherArt.Flood or MotherArt.Shell => new(side * 15, -4), _ => new(0, -34 * .36) };
        double size = s.Width / 46;
        return at(s.X + (local.X * p.SX + p.Lean) * size, s.LeapHeight - local.Y * p.SY * size);
    }
    private static readonly Geometry MotherBowCloth = Parse("M -1,0 C -5,-6 -11,-7 -10,-1 C -12,6 -5,6 -1,1 Z M 1,0 C 5,-6 11,-7 10,-1 C 12,6 5,6 1,1 Z");
    private static readonly Geometry MotherShell = Parse("M -12,-4 L -7,0 L -3,-5 L 2,0 L 7,-4 L 12,-1 C 11,16 -10,17 -12,-4 Z");
    private static readonly Geometry MotherBowTail = Parse("M -1,0 C -3,4 -2,7 -1,10 L 1,8 L 3,9 C 1,5 3,2 2,0 Z");
    private static readonly Pen MotherCordHalo = FrozenPen(PetUi.ArtInk, 12);
    private static readonly Pen MotherCordRim = FrozenPen(MotherEgg, 9);
    private static readonly Pen MotherCordFill = FrozenPen(BowPink, 7);
    private static readonly Pen MotherCordShine = FrozenPen(BellyHeart, 1.4);
    private static readonly Pen MotherGelArm = FrozenPen(MotherGel, 5.5);
    private static readonly Pen MotherGelRim = FrozenPen(PetUi.ArtInk, 7.5);
    // A bounded cubic mesh, shared by its rim/fill/highlight instead of allocating brushes per frame.
    private static void MotherCord(DrawingContext dc, Point a, Point b, double bend, bool gel = false)
    {
        Vector d = b - a; double length = Math.Max(1, d.Length); var n = new Vector(-d.Y / length, d.X / length) * bend;
        var path = new StreamGeometry();
        using (var c = path.Open()) { c.BeginFigure(a, false, false); c.BezierTo(a + d * .32 + n, a + d * .72 + n, b, true, false); }
        path.Freeze();
        dc.DrawGeometry(null, gel ? MotherGelRim : MotherCordHalo, path);
        dc.DrawGeometry(null, gel ? MotherGelArm : MotherCordRim, path);
        if (!gel) dc.DrawGeometry(null, MotherCordFill, path);
        dc.DrawGeometry(null, MotherCordShine, path);
    }
}
