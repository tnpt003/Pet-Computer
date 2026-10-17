using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// The one drawing layer for every slime, marker and effect: a transparent strip along the
// floor of the battle monitor. Empty pixels stay fully transparent, so clicks there pass
// through to the windows below; only slimes (and a near-invisible halo around each, to make
// them easy to click) take the mouse. Drawing only: the fight itself is BattleSim.
public sealed class BattleFieldView : FrameworkElement
{
    public const double FieldHeight = 120;
    private readonly BattleSim sim;
    // Screen X of the strip's left edge (DIPs).
    public double OriginX { get; set; }
    public event Action<int>? SlimeClicked;
    public event Action? RightClicked;

    public BattleFieldView(BattleSim sim)
    {
        this.sim = sim;
        Cursor = Cursors.Hand;
        MouseLeftButtonDown += (_, e) =>
        {
            if (SlimeAt(e.GetPosition(this)) is int id) SlimeClicked?.Invoke(id);
            e.Handled = true;
        };
        MouseRightButtonDown += (_, e) => { RightClicked?.Invoke(); e.Handled = true; };
    }

    private double Floor => ActualHeight - 2;

    // The slime under a point in this view, if any (the closest one when they overlap).
    public int? SlimeAt(Point point)
    {
        var s = sim.Settings;
        int? best = null;
        double bestDistance = double.MaxValue;
        foreach (var slime in sim.Slimes)
        {
            if (!slime.Targetable) continue;
            double dx = Math.Abs(point.X - (slime.X - OriginX));
            if (dx > s.SlimeWidth / 2 + 10 || point.Y < Floor - s.SlimeHeight - 24 || point.Y > Floor + 4) continue;
            if (dx < bestDistance) { bestDistance = dx; best = slime.Id; }
        }
        return best;
    }

    private static Brush Frozen(byte a, byte r, byte g, byte b) { var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b)); brush.Freeze(); return brush; }
    private static Pen FrozenPen(Brush brush, double thickness, DashStyle? dash = null)
    {
        var pen = new Pen(brush, thickness) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (dash != null) pen.DashStyle = dash;
        pen.Freeze(); return pen;
    }
    private static readonly Brush HitHalo = Frozen(1, 0, 0, 0); // Invisible but clickable.
    private static readonly Brush SlimeGreen = Frozen(255, 0x7C, 0xD3, 0x6A);
    private static readonly Brush SlimeAngry = Frozen(255, 0xF5, 0xA6, 0x3C);
    private static readonly Brush Shine = Frozen(220, 255, 255, 255);
    private static readonly Brush Eye = Frozen(255, 0x2B, 0x2A, 0x33);
    private static readonly Brush Marker = Frozen(255, 0xFF, 0xD2, 0x3F);
    private static readonly Brush BarBack = Frozen(200, 0x26, 0x23, 0x38);
    private static readonly Brush BarFill = Frozen(255, 0x6F, 0xE0, 0x6A);
    private static readonly Brush Warning = Frozen(255, 0xE5, 0x3E, 0x3E);
    private static readonly Brush PushBlue = Frozen(255, 0x5C, 0xC8, 0xFF);
    private static readonly Pen Outline = FrozenPen(Frozen(255, 0x2E, 0x3A, 0x2B), 2.2);
    private static readonly Pen Halo = FrozenPen(Brushes.White, 5);
    private static readonly Pen Ink = FrozenPen(Frozen(255, 0x30, 0x2B, 0x48), 2.4);
    private static readonly Pen MarkerRing = FrozenPen(Marker, 3);
    private static readonly Pen PushRing = FrozenPen(PushBlue, 2.5, new DashStyle(new double[] { 2, 2 }, 0));
    private static readonly Pen PushArc = FrozenPen(PushBlue, 3.5);
    private static readonly Pen SparkPen = FrozenPen(Frozen(255, 0xFF, 0xE0, 0x6A), 3);
    private static readonly Pen WarningPen = FrozenPen(Warning, 4);

    protected override void OnRender(DrawingContext dc)
    {
        var s = sim.Settings;
        double floor = Floor, time = sim.Time;
        foreach (var slime in sim.Slimes) DrawSlime(dc, slime, slime.X - OriginX, floor, s, time);
        foreach (var effect in sim.Effects) DrawEffect(dc, effect, effect.X - OriginX, floor - s.SlimeHeight / 2, s);
    }

    private void DrawSlime(DrawingContext dc, Slime slime, double x, double floor, BattleSettings s, double time)
    {
        double w = s.SlimeWidth, h = s.SlimeHeight, t = slime.StateTime;
        double sx = 1, sy = 1, opacity = 1, jitter = 0;
        bool targetable = slime.Targetable;
        switch (slime.State)
        {
            case SlimeState.Spawning:
                double rise = Math.Clamp(t / s.SpawnDuration, 0, 1);
                sx = 0.6 + 0.4 * rise; sy = rise; opacity = rise;
                break;
            case SlimeState.Moving:
            case SlimeState.Recover:
                double bounce = Math.Sin(slime.StateTime * 9 + slime.Id);
                sx = 1 + 0.07 * bounce; sy = 1 - 0.09 * bounce;
                break;
            case SlimeState.Windup:
                double swell = Math.Clamp(t / s.SlimeWindup, 0, 1);
                sx = 1 - 0.12 * swell; sy = 1 + 0.28 * swell;
                jitter = Math.Sin(t * 60) * 1.5 * swell;
                break;
            case SlimeState.Stunned:
                sx = 1.12; sy = 0.86;
                break;
            case SlimeState.Defeated:
                double fade = Math.Clamp(t / s.DefeatDuration, 0, 1);
                sx = 1 + 0.5 * fade; sy = Math.Max(0.05, 1 - fade); opacity = 1 - fade;
                break;
        }
        if (targetable) dc.DrawEllipse(HitHalo, null, new Point(x, floor - h / 2), w / 2 + 10, h / 2 + 12);
        // Feet ring under the selected slime; dashed blue rings while choosing a push target.
        if (sim.SelectedId == slime.Id)
        {
            dc.DrawEllipse(null, Halo, new Point(x, floor - 1), w / 2 + 6, 6);
            dc.DrawEllipse(null, MarkerRing, new Point(x, floor - 1), w / 2 + 6, 6);
        }
        if (sim.PushArmed && targetable) dc.DrawEllipse(null, PushRing, new Point(x, floor - h / 2), w / 2 + 7, h / 2 + 8);
        dc.PushOpacity(opacity);
        dc.PushTransform(new TranslateTransform(x + jitter, floor));
        dc.PushTransform(new ScaleTransform(sx, sy));
        var body = Body(w, h);
        Brush fill = slime.HitFlash > 0 ? Brushes.White : slime.State == SlimeState.Windup ? Blend(t / s.SlimeWindup) : SlimeGreen;
        dc.DrawGeometry(null, Halo, body);
        dc.DrawGeometry(fill, Outline, body);
        dc.DrawEllipse(Shine, null, new Point(-w * 0.2, -h * 0.68), 5, 3.5);
        double look = slime.Facing * 3;
        if (slime.State == SlimeState.Stunned || slime.State == SlimeState.Defeated)
        {
            // Dazed "x x" eyes.
            foreach (double ex in new[] { -7.0, 7.0 })
            {
                dc.DrawLine(Ink, new Point(ex + look - 3, -h * 0.55 - 3), new Point(ex + look + 3, -h * 0.55 + 3));
                dc.DrawLine(Ink, new Point(ex + look - 3, -h * 0.55 + 3), new Point(ex + look + 3, -h * 0.55 - 3));
            }
        }
        else
        {
            dc.DrawEllipse(Eye, null, new Point(-7 + look, -h * 0.55), 2.6, slime.State == SlimeState.Windup ? 2 : 3.6);
            dc.DrawEllipse(Eye, null, new Point(7 + look, -h * 0.55), 2.6, slime.State == SlimeState.Windup ? 2 : 3.6);
        }
        dc.Pop(); dc.Pop();
        dc.Pop();
        double top = floor - h * sy;
        if (slime.State == SlimeState.Windup)
        {
            // The warning: a red "!" over a swelling slime.
            double y = top - 16;
            dc.DrawLine(Halo, new Point(x, y - 9), new Point(x, y + 1));
            dc.DrawLine(WarningPen, new Point(x, y - 9), new Point(x, y + 1));
            dc.DrawEllipse(Warning, Halo, new Point(x, y + 7), 2.4, 2.4);
            dc.DrawEllipse(Warning, null, new Point(x, y + 7), 2.4, 2.4);
        }
        if (slime.State == SlimeState.Stunned)
        {
            for (int i = 0; i < 3; i++)
            {
                double a = time * 5 + i * Math.PI * 2 / 3;
                var star = new Point(x + Math.Cos(a) * 16, top - 6 + Math.Sin(a) * 4);
                dc.DrawEllipse(Marker, Outline, star, 3, 3);
            }
        }
        if (targetable && (slime.Hp < slime.MaxHp || sim.SelectedId == slime.Id || sim.TargetId == slime.Id))
        {
            double barY = top - 9, barW = 36, hpWidth = barW * Math.Clamp(slime.Hp / Math.Max(1, slime.MaxHp), 0, 1);
            dc.DrawRoundedRectangle(BarBack, null, new Rect(x - barW / 2 - 1, barY - 1, barW + 2, 7), 3, 3);
            if (hpWidth > 0) dc.DrawRoundedRectangle(BarFill, null, new Rect(x - barW / 2, barY, hpWidth, 5), 2.5, 2.5);
        }
        if (sim.SelectedId == slime.Id)
        {
            // Bobbing yellow arrow: readable on light and dark desktops (white halo, dark outline).
            double y = top - 30 - (slime.State == SlimeState.Windup ? 14 : 0) + Math.Sin(time * 4) * 3;
            var arrow = new StreamGeometry();
            using (var c = arrow.Open())
            {
                c.BeginFigure(new Point(x - 8, y - 9), true, true);
                c.LineTo(new Point(x + 8, y - 9), true, false);
                c.LineTo(new Point(x, y + 2), true, false);
            }
            dc.DrawGeometry(null, Halo, arrow);
            dc.DrawGeometry(Marker, Ink, arrow);
        }
    }

    private static Brush Blend(double k)
    {
        k = Math.Clamp(k, 0, 1);
        Color a = ((SolidColorBrush)SlimeGreen).Color, b = ((SolidColorBrush)SlimeAngry).Color;
        return new SolidColorBrush(Color.FromRgb((byte)(a.R + (b.R - a.R) * k), (byte)(a.G + (b.G - a.G) * k), (byte)(a.B + (b.B - a.B) * k)));
    }

    private static Geometry? body;
    private static double bodyW, bodyH;
    private static Geometry Body(double w, double h)
    {
        if (body != null && bodyW == w && bodyH == h) return body;
        var g = Geometry.Parse(FormattableString.Invariant(
            $"M {-w / 2},0 C {-w / 2},{-h * 0.7} {-w * 0.28},{-h} 0,{-h} C {w * 0.28},{-h} {w / 2},{-h * 0.7} {w / 2},0 Z"));
        g.Freeze();
        bodyW = w; bodyH = h;
        return body = g;
    }

    private static void DrawEffect(DrawingContext dc, BattleEffect effect, double x, double y, BattleSettings s)
    {
        double k = Math.Clamp(effect.Age / effect.Life, 0, 1);
        dc.PushOpacity(1 - k);
        switch (effect.Kind)
        {
            case BattleEffectKind.Spark:
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3 + 0.3;
                    var from = new Point(x + Math.Cos(a) * (6 + 10 * k), y + Math.Sin(a) * (6 + 10 * k));
                    var to = new Point(x + Math.Cos(a) * (14 + 14 * k), y + Math.Sin(a) * (14 + 14 * k));
                    dc.DrawLine(Halo, from, to);
                    dc.DrawLine(SparkPen, from, to);
                }
                break;
            case BattleEffectKind.Poof:
                // Soft white puffs as the slime pops; nothing gory.
                for (int i = 0; i < 5; i++)
                {
                    double a = i * Math.PI * 2 / 5 - Math.PI / 2;
                    var c = new Point(x + Math.Cos(a) * 18 * k, y + 6 + Math.Sin(a) * 12 * k);
                    dc.DrawEllipse(Brushes.White, Outline, c, 5 + 4 * k, 5 + 4 * k);
                }
                break;
            case BattleEffectKind.Push:
                for (int i = 0; i < 2; i++)
                {
                    double r = 16 + i * 8 + 18 * k;
                    var arc = new StreamGeometry();
                    using (var c = arc.Open())
                    {
                        c.BeginFigure(new Point(x - r * 0.5, y - r * 0.85), false, false);
                        c.ArcTo(new Point(x - r * 0.5, y + r * 0.85), new Size(r, r), 0, false, SweepDirection.Counterclockwise, true, false);
                    }
                    dc.DrawGeometry(null, PushArc, arc);
                }
                break;
        }
        dc.Pop();
    }
}
