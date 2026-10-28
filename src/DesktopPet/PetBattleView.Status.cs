using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// Statuses on an enemy (PetBattle.Status.cs), each plain at a glance: poison tints the body violet (not green: most slimes are)
// with bubbles rising off it, chill frosts it pale blue with ice crystals, stick leaves an amber glue puddle
// at its feet with strands up its sides. Over its head a small badge per status, its ring running
// down with the time left.
public sealed partial class BattleFieldView
{
    private static readonly Brush PoisonTint = Frozen(150, 0xA0, 0x50, 0xE0);
    private static readonly Brush PoisonBubble = Frozen(255, 0xD2, 0xA8, 0xF5);
    private static readonly Pen PoisonInk = FrozenPen(Frozen(255, 0x5A, 0x2A, 0x8A), 1.4);
    private static readonly Brush PoisonNumber = Frozen(255, 0xD2, 0xA8, 0xF5);
    private static readonly Brush FrostTint = Frozen(120, 0xC8, 0xEC, 0xFF);
    private static readonly Pen FrostRim = FrozenPen(Frozen(230, 255, 255, 255), 1.6);
    private static readonly Pen IcePen = FrozenPen(Frozen(255, 0x4E, 0x9E, 0xD8), 1.6);
    private static readonly Brush IceBadge = Frozen(255, 0xA8, 0xDC, 0xFF);
    private static readonly Brush GooFill = Frozen(225, 0xF0, 0xB8, 0x4A);
    private static readonly Pen GooPen = FrozenPen(Frozen(240, 0xF0, 0xB8, 0x4A), 3);
    private static readonly Pen GooInk = FrozenPen(Frozen(220, 0x8A, 0x5A, 0x1A), 1.4);
    private static readonly Pen BadgeRing = FrozenPen(Frozen(255, 0xFF, 0xF4, 0xD6), 2);
    private const double BadgeRadius = 7, BadgeGap = 17;

    private static bool HasStatus(Slime slime) => slime.Targetable && (slime.Poisoned || slime.Chilled || slime.Stuck);

    // Under the body (world coordinates): the stick's puddle.
    private static void DrawStatusUnder(DrawingContext dc, Slime slime, double x, double floor, double w)
    {
        if (!slime.Stuck || !slime.Targetable) return;
        dc.DrawEllipse(GooFill, GooInk, new Point(x, floor - 1), w / 2 + 8, 5);
    }

    // On the body (body coordinates, bottom centre at 0, 0): the poison's and the chill's tints.
    private static void DrawStatusTint(DrawingContext dc, Slime slime, Geometry body, double bw, double bh, double time)
    {
        if (!slime.Targetable) return;
        if (slime.Chilled)
        {
            dc.DrawGeometry(FrostTint, FrostRim, body);
            DrawCrystal(dc, new Point(-bw * 0.24, -bh * 0.3), 4.5);
            DrawCrystal(dc, new Point(bw * 0.26, -bh * 0.7), 3.5);
        }
        if (slime.Poisoned)
        {
            dc.DrawGeometry(PoisonTint, null, body);
            for (int i = 0; i < Math.Min(3, slime.PoisonLayers + 1); i++)
            {
                double f = (time * 0.9 + i * 0.37 + slime.Id * 0.13) % 1;
                var at = new Point(-9 + 9 * i + 2 * Math.Sin(time * 3 + i), -bh - 2 - 16 * f);
                dc.PushOpacity(1 - f * 0.8);
                dc.DrawEllipse(PoisonBubble, PoisonInk, at, 2.2 + i % 2, 2.2 + i % 2);
                dc.Pop();
            }
        }
    }

    // Over the body (world coordinates): the stick's strands, then one badge per status.
    // top: the top of the body; returns how much higher what goes over the head must sit.
    private static double DrawStatusOver(DrawingContext dc, Slime slime, double x, double floor, double top, double w, double h, double time)
    {
        if (!HasStatus(slime)) return 0;
        if (slime.Stuck)
            foreach (int side in new[] { -1, 1 })
            {
                var from = new Point(x + side * (w / 2 + 2), floor - 2);
                var to = new Point(x + side * w * 0.32, floor - h * 0.3);
                dc.DrawLine(GooPen, from, to);
                dc.DrawEllipse(GooFill, null, to, 2.4, 2.4);
            }
        int count = (slime.Poisoned ? 1 : 0) + (slime.Chilled ? 1 : 0) + (slime.Stuck ? 1 : 0);
        double y = top - 19 - (slime.State == SlimeState.Windup ? 17 : 0);
        double bx = x - (count - 1) * BadgeGap / 2;
        if (slime.Poisoned) { DrawBadge(dc, new Point(bx, y), SlimeStatus.Poison, slime.PoisonRemaining / Math.Max(slime.PoisonTotal, slime.PoisonRemaining)); bx += BadgeGap; }
        if (slime.Chilled) { DrawBadge(dc, new Point(bx, y), SlimeStatus.Chill, slime.ChillRemaining / Math.Max(slime.ChillTotal, slime.ChillRemaining)); bx += BadgeGap; }
        if (slime.Stuck) DrawBadge(dc, new Point(bx, y), SlimeStatus.Stick, slime.StuckRemaining / Math.Max(slime.StuckTotal, slime.StuckRemaining));
        return 13;
    }

    // A round badge with the status's mark and a ring for the time left (full: just put on).
    private static void DrawBadge(DrawingContext dc, Point at, SlimeStatus status, double left)
    {
        Brush fill = status switch { SlimeStatus.Poison => PoisonBubble, SlimeStatus.Chill => IceBadge, _ => GooFill };
        dc.DrawEllipse(null, Halo, at, BadgeRadius, BadgeRadius);
        dc.DrawEllipse(fill, Outline, at, BadgeRadius, BadgeRadius);
        switch (status)
        {
            case SlimeStatus.Poison:
                dc.DrawEllipse(null, PoisonInk, new Point(at.X - 1.6, at.Y + 1), 1.6, 1.6);
                dc.DrawEllipse(null, PoisonInk, new Point(at.X + 1.8, at.Y - 1.4), 1.2, 1.2);
                break;
            case SlimeStatus.Chill:
                DrawCrystal(dc, at, 3.4);
                break;
            default:
                dc.DrawLine(GooInk, new Point(at.X - 3, at.Y - 1), new Point(at.X + 3, at.Y - 1));
                dc.DrawLine(GooInk, new Point(at.X + 1, at.Y - 1), new Point(at.X + 1, at.Y + 2.5));
                break;
        }
        left = double.IsFinite(left) ? Math.Clamp(left, 0, 1) : 0;
        if (left <= 0) return;
        double r = BadgeRadius + 2.2;
        if (left >= 0.999) { dc.DrawEllipse(null, BadgeRing, at, r, r); return; }
        // From the top, clockwise, the part of the time still left.
        double a = -Math.PI / 2 + left * Math.PI * 2;
        var arc = new StreamGeometry();
        using (var c = arc.Open())
        {
            c.BeginFigure(new Point(at.X, at.Y - r), false, false);
            c.ArcTo(new Point(at.X + Math.Cos(a) * r, at.Y + Math.Sin(a) * r), new Size(r, r), 0, left > 0.5, SweepDirection.Clockwise, true, false);
        }
        arc.Freeze();
        dc.DrawGeometry(null, BadgeRing, arc);
    }

    // A small six-pointed ice crystal.
    private static void DrawCrystal(DrawingContext dc, Point at, double r)
    {
        for (int i = 0; i < 3; i++)
        {
            double a = i * Math.PI / 3;
            double dx = Math.Cos(a) * r, dy = Math.Sin(a) * r;
            dc.DrawLine(IcePen, new Point(at.X - dx, at.Y - dy), new Point(at.X + dx, at.Y + dy));
        }
    }
}
