using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M14 (PetBattle.Fly.cs, PetBattle.Burrow.cs). The flying slime: mint, two little wings flapping
// (folded once it is down), drawn in its own small window while up in the air (DrawFlyer, from
// PetWindow.BattleSky.cs), with only a faint shadow on the strip under it; aiming, it swells and a red
// ring pulses round it. The burrowing slime: tan, a pink nose and two clawed paws; under the floor only
// a mound of earth shows, moving; its ring of cracks fills up red where it will come up; digging in and
// coming up it sinks into / rises out of the floor. Drawing only.
public sealed partial class BattleFieldView
{
    private static readonly Brush WingFill = Frozen(235, 0xF4, 0xFB, 0xFF);
    private static readonly Pen WingInk = FrozenPen(PetUi.ArtInk, 1.6);
    private static readonly Brush Earth = Frozen(255, 0x9A, 0x6B, 0x3F);
    private static readonly Brush EarthLight = Frozen(255, 0xC4, 0x93, 0x5E);
    private static readonly Pen EarthInk = FrozenPen(Frozen(255, 0x5A, 0x3A, 0x1E), 2);
    private static readonly Brush NosePink = Frozen(255, 0xFF, 0x8F, 0xA8);
    private static readonly Pen CrackInk = FrozenPen(Frozen(255, 0x5A, 0x3A, 0x1E), 2.2);
    private static readonly Pen AimPen = FrozenPen(Frozen(255, 0xE5, 0x3E, 0x3E), 3, new DashStyle(new double[] { 2, 1.5 }, 0));
    private static readonly Brush SkyShadow = Frozen(40, 0x2A, 0x1F, 0x16);
    private static readonly Geometry WingShape = Parse("M 0,0 C -6,-12 -18,-14 -20,-6 C -16,-4 -12,0 -14,4 C -8,4 -4,2 0,0 Z");
    private static readonly Geometry Mound = Parse("M -26,0 C -22,-14 -8,-18 0,-18 C 8,-18 22,-14 26,0 Z");

    // A flyer up in the air, drawn by its own window: x its centre, bottom where its body's bottom is.
    public void DrawFlyer(DrawingContext dc, Slime slime, double x, double bottom)
    {
        double time = sim.Time;
        if (slime.Aiming && slime.Type.Fly is FlySpec fly)
        {
            double k = Math.Clamp(1 - slime.AimLeft / fly.Aim, 0, 1), pulse = 1 + 0.08 * Math.Sin(time * 18);
            var centre = new Point(x, bottom - slime.Height / 2);
            dc.DrawEllipse(null, Halo, centre, (slime.Width / 2 + 10) * pulse, (slime.Height / 2 + 10) * pulse);
            dc.DrawEllipse(null, WarningEdge, centre, (slime.Width / 2 + 10) * pulse, (slime.Height / 2 + 10) * pulse);
            dc.DrawEllipse(null, WarningFill, centre, Math.Max(1, (slime.Width / 2 + 10) * k), Math.Max(1, (slime.Height / 2 + 10) * k));
        }
        DrawSlime(dc, slime, x, bottom, sim.Settings, time);
    }

    // Its marks (body coordinates): a flyer's wings, a burrower's nose and paws.
    private static void DrawSkyMarks(DrawingContext dc, Slime slime, double bw, double bh, double time)
    {
        if (slime.Type.Marks == "wings")
        {
            // Flapping while up (and while crouching to rise), folded down on the floor.
            bool up = slime.Fly is FlyPhase.Aloft or FlyPhase.Rising && slime.State != SlimeState.Defeated;
            double flap = up ? Math.Sin(time * (slime.Fly == FlyPhase.Rising ? 26 : 16) + slime.Id) * 22 : -35;
            foreach (int side in new[] { -1, 1 })
            {
                dc.PushTransform(new MatrixTransform(-side, 0, 0, 1, side * bw * 0.4, -bh * 0.6)); // Out from its side.
                dc.PushTransform(new RotateTransform(flap));
                dc.DrawGeometry(null, Halo, WingShape);
                dc.DrawGeometry(WingFill, WingInk, WingShape);
                dc.Pop();
                dc.Pop();
            }
        }
        else if (slime.Type.Marks == "mole")
        {
            double look = slime.Facing * 3;
            dc.DrawEllipse(NosePink, ThinInk, new Point(look * 1.6, -bh * 0.4), 3.4, 2.6);
            foreach (int side in new[] { -1, 1 })
            {
                var paw = new Point(side * bw * 0.3 + look, -3);
                dc.DrawEllipse(EarthLight, ThinInk, paw, 5.5, 4);
                for (int c = -1; c <= 1; c++) dc.DrawLine(ThinInk, new Point(paw.X + c * 2.5, paw.Y + 2), new Point(paw.X + c * 2.8, paw.Y + 5));
            }
        }
    }

    // A burrower under the floor or going in/out of it, drawn here instead of the usual body. True: drawn.
    private bool DrawBurrower(DrawingContext dc, Slime slime, double x, double floor, double time)
    {
        if (slime.Type.Burrow is not BurrowSpec burrow || slime.State == SlimeState.Defeated) return false;
        if (slime.Burrow == BurrowPhase.Cracking) DrawCracks(dc, slime, burrow, slime.CrackX - OriginX, floor, time);
        if (slime.Burrowed)
        {
            // Only its mound of earth shows (still clickable for the Grabber).
            double wobble = 1 + 0.06 * Math.Sin(time * 12 + slime.Id), size = slime.Width / sim.Settings.SlimeWidth;
            dc.DrawEllipse(HitHalo, null, new Point(x, floor - 8), 34 * size, 20);
            dc.PushTransform(new MatrixTransform(size * wobble, 0, 0, size / wobble, x, floor));
            dc.DrawGeometry(null, Halo, Mound);
            dc.DrawGeometry(Earth, EarthInk, Mound);
            dc.DrawEllipse(EarthLight, null, new Point(-8, -11), 3, 2);
            dc.DrawEllipse(EarthLight, null, new Point(7, -7), 2.4, 1.8);
            dc.Pop();
            if (sim.Armed == BattleSkill.Grabber && sim.Grabbable(slime)) dc.DrawEllipse(null, PushRing, new Point(x, floor - 8), 32 * size, 18);
            return true;
        }
        double sink = slime.Burrow switch
        {
            BurrowPhase.Digging => Math.Clamp(1 - slime.BurrowClock / burrow.Dig, 0, 1),
            BurrowPhase.Emerging => Math.Clamp(slime.BurrowClock / burrow.Emerge, 0, 1),
            _ => 0
        };
        if (sink <= 0) return false;
        // Half in the floor: drawn lower and cut off at the floor, a mound round it.
        dc.PushClip(new RectangleGeometry(new Rect(x - slime.Width * 2, 0, slime.Width * 4, floor - 2)));
        DrawSlime(dc, slime, x, floor + slime.Height * sink * 0.9, sim.Settings, time);
        dc.Pop();
        double s = slime.Width / sim.Settings.SlimeWidth;
        dc.PushTransform(new MatrixTransform(s * 1.2, 0, 0, s * 0.5, x, floor));
        dc.DrawGeometry(Earth, EarthInk, Mound);
        dc.Pop();
        return true;
    }

    // Where it will come up: a ring of cracks filling up red as its time runs out.
    private void DrawCracks(DrawingContext dc, Slime slime, BurrowSpec burrow, double x, double floor, double time)
    {
        double total = BattleSim.CrackTime(slime), k = total > 0 ? Math.Clamp(1 - slime.BurrowClock / total, 0, 1) : 1;
        double r = burrow.Radius, pulse = 1 + 0.04 * Math.Sin(time * 16);
        dc.DrawEllipse(null, Halo, new Point(x, floor - 3), r * pulse, 9 * pulse);
        dc.DrawEllipse(null, WarningEdge, new Point(x, floor - 3), r * pulse, 9 * pulse);
        dc.DrawEllipse(null, WarningFill, new Point(x, floor - 3), Math.Max(1, r * k), Math.Max(1, 9 * k));
        for (int i = 0; i < 6; i++)
        {
            double a = i * Math.PI / 3 + 0.4, len = r * (0.35 + 0.5 * k);
            var from = new Point(x + Math.Cos(a) * 6, floor - 3 + Math.Sin(a) * 2);
            var mid = new Point(x + Math.Cos(a + 0.2) * len * 0.55, floor - 3 + Math.Sin(a + 0.2) * len * 0.12);
            var to = new Point(x + Math.Cos(a) * len, floor - 3 + Math.Sin(a) * len * 0.16);
            dc.DrawLine(CrackInk, from, mid);
            dc.DrawLine(CrackInk, mid, to);
        }
    }

    // A flyer up in the air: a faint shadow on the floor under it, smaller the higher it is.
    private static void DrawSkyShadow(DrawingContext dc, Slime slime, double x, double floor)
    {
        double k = 1 - Math.Clamp(slime.Altitude / 900, 0, 0.7);
        dc.DrawEllipse(SkyShadow, null, new Point(x, floor - 2), slime.Width * 0.45 * k, 4 * k);
    }

    // Aiming at the cat (the sky's canvas): a dashed red line from the flyer to the cat and a ring
    // closing in on it. from/to in that canvas's coordinates.
    public static void DrawAim(DrawingContext dc, Point from, Point to, double radius, double progress, double time)
    {
        dc.DrawLine(Halo, from, to);
        dc.DrawLine(AimPen, from, to);
        double r = radius * (1.6 - 0.6 * progress), pulse = 1 + 0.06 * Math.Sin(time * 18);
        dc.DrawEllipse(null, Halo, to, r * pulse, r * pulse);
        dc.DrawEllipse(null, WarningEdge, to, r * pulse, r * pulse);
        dc.DrawLine(WarningPen, new Point(to.X - 6, to.Y), new Point(to.X + 6, to.Y));
        dc.DrawLine(WarningPen, new Point(to.X, to.Y - 6), new Point(to.X, to.Y + 6));
    }

    // A drop of sticky goo in flight (the sky's canvas): a blob and two drips trailing it.
    public static void DrawShot(DrawingContext dc, BattleShot shot, Point at)
    {
        var fill = KindBrush(shot.Tint ?? "#8FD3C8");
        double speed = Math.Max(1, Math.Sqrt(shot.VX * shot.VX + shot.VY * shot.VY));
        double bx = -shot.VX / speed, by = shot.VY / speed; // Backwards along its flight (screen y down).
        for (int i = 2; i >= 1; i--)
            dc.DrawEllipse(fill, Outline, new Point(at.X + bx * 9 * i, at.Y + by * 9 * i), 5 - i, 5 - i);
        dc.DrawEllipse(null, Halo, at, 8, 7);
        dc.DrawEllipse(fill, Outline, at, 8, 7);
        dc.DrawEllipse(Shine, null, new Point(at.X - 2.5, at.Y - 2.5), 2.2, 1.6);
    }
}
