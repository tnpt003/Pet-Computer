using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// The desktop apps' part of the battle strip: the ads an infected Tabs opens (they take the click
// on their ×, the rest of them hides the slimes behind), the tab tornado around the cat, each app's
// effect on the field and the virus mark over a slime holding an icon. The icons themselves stand
// in their own windows (DesktopAppIcon); what flies between them and the field is AppFlightView.
public sealed partial class BattleFieldView
{
    // An ad's × clicked.
    public event Action<int>? PopupClicked;

    private Rect PopupRect(BattlePopup popup, double floor) =>
        new(popup.X - OriginX - BattleSim.PopupWidth / 2, floor - 2 - BattleSim.PopupHeight, BattleSim.PopupWidth, BattleSim.PopupHeight);
    private static Point CloseButton(Rect popup) => new(popup.Right - 10, popup.Top + 9);

    // The ad whose × is under the point.
    public int? PopupCloseAt(Point point)
    {
        double floor = Floor;
        for (int i = sim.Popups.Count - 1; i >= 0; i--)
            if ((point - CloseButton(PopupRect(sim.Popups[i], floor))).Length <= 12) return sim.Popups[i].Id;
        return null;
    }

    // On an ad (anywhere): clicks there never reach a slime behind it.
    public bool OnPopup(Point point)
    {
        for (int i = 0; i < sim.Popups.Count; i++)
            if (PopupRect(sim.Popups[i], Floor).Contains(point)) return true;
        return false;
    }

    private void DrawPopups(DrawingContext dc, double floor, double time)
    {
        for (int i = 0; i < sim.Popups.Count; i++)
        {
            var popup = sim.Popups[i];
            var r = PopupRect(popup, floor);
            // Pops open with a little overshoot; shakes in its last 2 s.
            double k = Math.Min(1, popup.Age / 0.25), scale = k < 1 ? 0.5 + 0.6 * k - 0.1 * k * k : 1;
            if (popup.Life - popup.Age < 2) scale += 0.03 * Math.Sin(time * 30);
            dc.PushTransform(new ScaleTransform(scale, scale, r.X + r.Width / 2, r.Bottom));
            AppArt.DrawAd(dc, r, Language, popup.Age / popup.Life, time);
            dc.Pop();
        }
    }

    // The tab tornado: three browser tabs circling the cat's legs on a flat ring, each trailing a
    // curved swish along the ring. The far half of the ring is drawn first, faded and smaller, so
    // the tabs read as going round behind the cat; all of it fades out in its last half second.
    private void DrawTabs(DrawingContext dc, double catX, double floor, double time)
    {
        double radius = (sim.AppRules?.TabRadius ?? 70) * 0.85, middle = floor - 26;
        Point On(double a) => new(catX + Math.Cos(a) * radius, middle + Math.Sin(a) * 11);
        dc.PushOpacity(Math.Min(1, sim.TabsRemaining / 0.5));
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < 3; i++)
            {
                double a = time * 5 + i * Math.PI * 2 / 3;
                bool front = Math.Sin(a) >= 0;
                if (front != (pass == 1)) continue;
                var at = On(a);
                var swish = new StreamGeometry();
                using (var g = swish.Open())
                {
                    g.BeginFigure(On(a - 0.9), false, false);
                    for (int s = 1; s <= 6; s++) g.LineTo(On(a - 0.9 + 0.15 * s), true, true);
                }
                dc.DrawGeometry(null, front ? SwishPen : SwishBackPen, swish);
                if (!front) dc.PushOpacity(0.55);
                double size = 0.8 + 0.25 * Math.Sin(a); // 0.55 at the back, 1.05 in front.
                dc.PushTransform(new RotateTransform(-Math.Cos(a) * 12, at.X, at.Y));
                AppArt.DrawFlyingTab(dc, at, size, AppArt.TabDots[i]);
                dc.Pop();
                if (!front) dc.Pop();
            }
        dc.Pop();
    }
    private static readonly Pen SwishPen = RoundPen(Frozen(170, 255, 246, 230), 3.5);
    private static readonly Pen SwishBackPen = RoundPen(Frozen(90, 255, 246, 230), 2.5);
    private static Pen RoundPen(Brush brush, double width)
    {
        var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();
        return pen;
    }

    // A slime holding an icon with its virus wears a small virus over its head.
    private void DrawVirusMark(DrawingContext dc, Slime slime, double x, double floor, double time)
    {
        double y = floor - slime.LeapHeight - slime.Height - 16 + Math.Sin(time * 4 + slime.Id) * 2;
        AppArt.DrawVirus(dc, new Point(x + slime.Width * 0.3, y), 7, time);
    }

    // k: 0 → 1 over the effect's life (its opacity already fades with it).
    private void DrawAppEffect(DrawingContext dc, BattleEffect effect, double x, double y, double k)
    {
        switch (effect.Kind)
        {
            case BattleEffectKind.Bell:
                // Bounces on the head it fell on, then rings (swings side to side) with sound lines.
                double bounce = k < 0.25 ? Math.Sin(k / 0.25 * Math.PI) * 10 : 0;
                double swing = k > 0.2 ? Math.Sin((k - 0.2) * 40) * 22 * (1 - k) : 0;
                var at = new Point(x, y - bounce);
                dc.PushTransform(new RotateTransform(swing, at.X, at.Y - 10));
                AppArt.DrawBell(dc, at, 1.3, false);
                dc.Pop();
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 2; i++)
                    {
                        double r = 16 + i * 6 + 8 * k;
                        dc.DrawLine(RingPen, new Point(x + side * r, y - 12 - i * 3), new Point(x + side * (r + 4), y - 16 - i * 3));
                    }
                break;
            case BattleEffectKind.EndTask:
                // The window pops over its target and shakes, then goes.
                double pop = k < 0.15 ? k / 0.15 : 1;
                double shake = k > 0.15 && k < 0.6 ? Math.Sin(k * 90) * 2.5 : 0;
                double width = AppArt.NotRespondingWidth(Language);
                x = Math.Clamp(x, width / 2 + 2, Math.Max(width / 2 + 2, ActualWidth - width / 2 - 2)); // Whole on the strip, even at its ends.
                AppArt.DrawNotResponding(dc, new Point(x + shake, y - 16), 0.6 + 0.4 * pop, Language);
                break;
            default:
                // The undo arrow circling the cat backwards, a ring of light shrinking into it.
                double size = 22 + 12 * k;
                dc.DrawEllipse(null, RewindRing, new Point(x, y), 40 * (1 - k) + 10, 40 * (1 - k) + 10);
                dc.PushTransform(new RotateTransform(-k * 360, x, y));
                AppArt.DrawUndo(dc, new Point(x, y), size, AppArt.UndoPen);
                dc.Pop();
                break;
        }
    }
    private static readonly Pen RingPen = FrozenPen(Frozen(255, 0xFF, 0xD2, 0x3F), 2.5);
    private static readonly Pen RewindRing = FrozenPen(Frozen(200, 0x3F, 0xB0, 0x9A), 3);
}
