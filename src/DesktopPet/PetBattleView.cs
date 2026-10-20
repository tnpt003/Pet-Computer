using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// The one drawing layer for every slime, the boss, markers and effects: a transparent strip
// along the floor of the battle monitor. Empty pixels stay fully transparent, so clicks there
// pass through to the windows below; only slimes (and a near-invisible halo around each, to
// make them easy to click) take the mouse. Drawing only: the fight itself is BattleSim.
public sealed class BattleFieldView : FrameworkElement
{
    // Tall enough for the boss, its marker and the numbers floating over the cat.
    public const double FieldHeight = 170;
    private readonly BattleSim sim;
    // Screen X of the strip's left edge (DIPs).
    public double OriginX { get; set; }
    // The slime clicked and where on it (DIPs from its centre: negative = its left half).
    public event Action<int, double>? SlimeClicked;
    public event Action? RightClicked;
    // The slime under the mouse while Push is chosen, and the side the cursor is on: the
    // arrow shows which way it will fly.
    private int? hoverId;
    private double hoverOffset;

    public BattleFieldView(BattleSim sim)
    {
        this.sim = sim;
        Cursor = Cursors.Hand;
        MouseLeftButtonDown += (_, e) =>
        {
            var point = e.GetPosition(this);
            if (SlimeAt(point) is int id && sim.Find(id) is { } slime) SlimeClicked?.Invoke(id, point.X - (slime.X - OriginX));
            e.Handled = true;
        };
        MouseRightButtonDown += (_, e) => { RightClicked?.Invoke(); e.Handled = true; };
        MouseMove += (_, e) => Hover(e.GetPosition(this));
        MouseLeave += (_, _) => { if (hoverId != null) { hoverId = null; InvalidateVisual(); } };
    }

    private void Hover(Point point)
    {
        int? id = sim.PushArmed ? SlimeAt(point) : null;
        double offset = id is int found && sim.Find(found) is { } slime ? point.X - (slime.X - OriginX) : 0;
        if (id == hoverId && Math.Sign(offset) == Math.Sign(hoverOffset)) return;
        hoverId = id; hoverOffset = offset;
        InvalidateVisual();
    }

    private double Floor => ActualHeight - 2;

    // The slime under a point in this view, if any (the closest one when they overlap).
    public int? SlimeAt(Point point)
    {
        int? best = null;
        double bestDistance = double.MaxValue;
        for (int i = 0; i < sim.Slimes.Count; i++)
        {
            var slime = sim.Slimes[i];
            if (!slime.Targetable) continue;
            double dx = Math.Abs(point.X - (slime.X - OriginX));
            if (dx > slime.Width / 2 + 10 || point.Y < Floor - slime.Height - 24 || point.Y > Floor + 4) continue;
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
    // Each enemy kind's colours (battle.json), frozen once.
    private static readonly System.Collections.Generic.Dictionary<string, Brush> KindBrushes = new();
    private static Brush KindBrush(string hex)
    {
        if (KindBrushes.TryGetValue(hex, out var brush)) return brush;
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var made = new SolidColorBrush(color); made.Freeze();
        return KindBrushes[hex] = made;
    }
    private static readonly Brush SlimeAngry = Frozen(255, 0xF5, 0xA6, 0x3C);
    private static readonly Brush Shine = Frozen(220, 255, 255, 255);
    private static readonly Brush Eye = Frozen(255, 0x2B, 0x2A, 0x33);
    private static readonly Brush Marker = Frozen(255, 0xFF, 0xD2, 0x3F);
    // Support enemies: a green "+" while healing, a ring on the ally being healed, rising pluses.
    private static readonly Pen HealPen = FrozenPen(Frozen(255, 0x3F, 0xC8, 0x6B), 2.4);
    private static readonly Pen HealDash = FrozenPen(Frozen(220, 0x3F, 0xC8, 0x6B), 2, new DashStyle(new[] { 3.0, 2.0 }, 0));
    private static readonly Brush Gold = Frozen(255, 0xFF, 0xC8, 0x2E);
    private static readonly Brush BarBack = Frozen(200, 0x26, 0x23, 0x38);
    private static readonly Brush BarFill = Frozen(255, 0x6F, 0xE0, 0x6A);
    private static readonly Brush Warning = Frozen(255, 0xE5, 0x3E, 0x3E);
    private static readonly Brush PushBlue = Frozen(255, 0x5C, 0xC8, 0xFF);
    private static readonly Brush Yarn = Frozen(255, 0xE8, 0x7A, 0xB8);
    private static readonly Brush HealGreen = Frozen(255, 0x7C, 0xF0, 0x7A);
    private static readonly Brush DamageWhite = Frozen(255, 0xFF, 0xF4, 0xD6);
    private static readonly Brush ComboGold = Frozen(255, 0xFF, 0xD2, 0x3F);
    private static readonly Pen Outline = FrozenPen(Frozen(255, 0x2E, 0x3A, 0x2B), 2.2);
    private static readonly Pen Halo = FrozenPen(Brushes.White, 5);
    private static readonly Pen TextHalo = FrozenPen(Frozen(255, 0x26, 0x23, 0x38), 3.5);
    private static readonly Pen Ink = FrozenPen(Frozen(255, 0x30, 0x2B, 0x48), 2.4);
    private static readonly Pen MarkerRing = FrozenPen(Marker, 3);
    private static readonly Pen PushRing = FrozenPen(PushBlue, 2.5, new DashStyle(new double[] { 2, 2 }, 0));
    private static readonly Pen BombRing = FrozenPen(Yarn, 2.5, new DashStyle(new double[] { 2, 2 }, 0));
    private static readonly Pen PushArc = FrozenPen(PushBlue, 3.5);
    private static readonly Pen SparkPen = FrozenPen(Frozen(255, 0xFF, 0xE0, 0x6A), 3);
    private static readonly Pen WarningPen = FrozenPen(Warning, 4);
    private static readonly Pen WarningEdge = FrozenPen(Warning, 2.5, new DashStyle(new double[] { 3, 2 }, 0));
    private static readonly Pen WarningFill = FrozenPen(Frozen(200, 0xE5, 0x3E, 0x3E), 3.5);
    private static readonly Pen YarnPen = FrozenPen(Frozen(255, 0xB0, 0x4A, 0x88), 1.6);
    private static readonly Pen BlastPen = FrozenPen(Frozen(255, 0xFF, 0x9E, 0x3D), 4);
    private static readonly Pen SlamPen = FrozenPen(Frozen(255, 0xC8, 0xB8, 0xF0), 4);
    private static readonly Typeface NumberFont = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);

    protected override void OnRender(DrawingContext dc)
    {
        double floor = Floor, time = sim.Time;
        // Index loops: foreach over IReadOnlyList boxes an enumerator every frame.
        for (int i = 0; i < sim.Bombs.Count; i++) DrawBomb(dc, sim.Bombs[i], sim.Bombs[i].X - OriginX, floor);
        for (int i = 0; i < sim.Slimes.Count; i++) DrawSlime(dc, sim.Slimes[i], sim.Slimes[i].X - OriginX, floor, sim.Settings, time);
        for (int i = 0; i < sim.Effects.Count; i++) DrawEffect(dc, sim.Effects[i], sim.Effects[i].X - OriginX, floor - sim.Effects[i].Height, floor);
    }

    private void DrawSlime(DrawingContext dc, Slime slime, double x, double floor, BattleSettings s, double time)
    {
        double w = slime.Width, h = slime.Height, t = slime.StateTime, size = slime.Width / s.SlimeWidth;
        double sx = 1, sy = 1, opacity = 1, jitter = 0;
        bool targetable = slime.Targetable;
        double windup = sim.Windup(slime);
        switch (slime.State)
        {
            case SlimeState.Spawning:
                double rise = Math.Clamp(t / (s.SpawnDuration * (slime.IsBoss ? 2 : 1)), 0, 1);
                sx = 0.6 + 0.4 * rise; sy = rise; opacity = rise;
                break;
            case SlimeState.Moving:
            case SlimeState.Recover:
                double bounce = Math.Sin(slime.StateTime * (slime.IsBoss ? 5 : 9) + slime.Id);
                sx = 1 + 0.07 * bounce; sy = 1 - 0.09 * bounce;
                break;
            case SlimeState.Windup:
                double swell = Math.Clamp(t / windup, 0, 1);
                sx = 1 - 0.12 * swell; sy = 1 + 0.28 * swell;
                jitter = Math.Sin(t * 60) * 1.5 * swell * size;
                break;
            case SlimeState.Stunned:
                sx = 1.12; sy = 0.86;
                break;
            case SlimeState.Dashing:
                sx = 1.25; sy = 0.8;
                break;
            case SlimeState.Casting:
                double hum = Math.Sin(t * 14);
                sx = 1 + 0.05 * hum; sy = 1 + 0.06 * hum;
                break;
            case SlimeState.Defeated:
                double fade = Math.Clamp(t / (s.DefeatDuration * (slime.IsBoss ? 1.5 : 1)), 0, 1);
                sx = 1 + 0.5 * fade; sy = Math.Max(0.05, 1 - fade); opacity = 1 - fade;
                break;
        }
        if (slime.Type.Slam && slime.State == SlimeState.Windup)
        {
            // The slam's reach on the floor, filling up as it comes: get out (or push, shield, lift).
            // Outlines only: a filled zone would catch clicks meant for the apps below.
            double k = Math.Clamp(t / windup, 0, 1);
            dc.DrawEllipse(null, WarningFill, new Point(x, floor - 4), Math.Max(1, slime.Range * k), Math.Max(1, 9 * k));
            dc.DrawEllipse(null, WarningEdge, new Point(x, floor - 4), slime.Range, 9);
        }
        if (slime.Type.Dash && slime.State == SlimeState.Windup)
        {
            // The dash's lane on the floor, growing as it comes, with an arrowhead at its end.
            double k = Math.Clamp(t / windup, 0, 1), dir = slime.DashDirection == 0 ? slime.Facing : slime.DashDirection;
            double end = x + dir * (w / 2 + slime.Type.DashDistance * (0.35 + 0.65 * k));
            var lane = new Point(end, floor - 4);
            dc.DrawLine(Halo, new Point(x + dir * w / 2, floor - 4), lane);
            dc.DrawLine(WarningEdge, new Point(x + dir * w / 2, floor - 4), lane);
            dc.DrawLine(WarningPen, lane, new Point(end - dir * 8, floor - 10));
            dc.DrawLine(WarningPen, lane, new Point(end - dir * 8, floor + 2));
        }
        if (targetable) dc.DrawEllipse(HitHalo, null, new Point(x, floor - h / 2), w / 2 + 10, h / 2 + 12);
        // Feet ring under the selected slime; dashed rings while choosing a target for a skill.
        if (sim.SelectedId == slime.Id)
        {
            dc.DrawEllipse(null, Halo, new Point(x, floor - 1), w / 2 + 6, 6);
            dc.DrawEllipse(null, MarkerRing, new Point(x, floor - 1), w / 2 + 6, 6);
        }
        if (sim.Armed is BattleSkill armed && targetable)
            dc.DrawEllipse(null, armed == BattleSkill.Bomb ? BombRing : PushRing, new Point(x, floor - h / 2), w / 2 + 7, h / 2 + 8);
        if (sim.PushArmed && targetable && hoverId == slime.Id && BattleSim.PushDirection(hoverOffset) is int way)
        {
            // Which way the push will send it.
            dc.PushTransform(new MatrixTransform(way, 0, 0, 1, x, floor - h / 2));
            dc.DrawGeometry(null, Halo, PushArrow);
            dc.DrawGeometry(PushBlue, Ink, PushArrow);
            dc.Pop();
        }
        if (slime.Type.Marks == "speed" && slime.State is SlimeState.Moving or SlimeState.Dashing && opacity > 0.5)
        {
            // Wind streaks trailing a fast slime.
            for (int i = 0; i < 3; i++)
            {
                double y = floor - h * (0.3 + 0.2 * i), back = -slime.Facing * (w / 2 + 4 + 3 * i);
                dc.DrawLine(Streak, new Point(x + back, y), new Point(x + back - slime.Facing * (10 + 4 * (i % 2)), y));
            }
        }
        dc.PushOpacity(opacity);
        dc.PushTransform(new MatrixTransform(sx * size, 0, 0, sy * size, x + jitter, floor));
        double bw = s.SlimeWidth, bh = s.SlimeHeight;
        var body = Body(bw, bh);
        Brush baseFill = KindBrush(slime.Enraged ? slime.Type.AngryColor : slime.Type.Color);
        Brush fill = slime.HitFlash > 0 ? Brushes.White : slime.State == SlimeState.Windup ? Blend(baseFill, t / windup) : baseFill;
        dc.DrawGeometry(null, Halo, body);
        dc.DrawGeometry(fill, Outline, body);
        dc.DrawEllipse(Shine, null, new Point(-bw * 0.2, -bh * 0.68), 5, 3.5);
        double look = slime.Facing * 3;
        if (slime.State == SlimeState.Stunned || slime.State == SlimeState.Defeated)
        {
            // Dazed "x x" eyes.
            foreach (double ex in new[] { -7.0, 7.0 })
            {
                dc.DrawLine(Ink, new Point(ex + look - 3, -bh * 0.55 - 3), new Point(ex + look + 3, -bh * 0.55 + 3));
                dc.DrawLine(Ink, new Point(ex + look - 3, -bh * 0.55 + 3), new Point(ex + look + 3, -bh * 0.55 - 3));
            }
        }
        else
        {
            dc.DrawEllipse(Eye, null, new Point(-7 + look, -bh * 0.55), 2.6, slime.State == SlimeState.Windup ? 2 : 3.6);
            dc.DrawEllipse(Eye, null, new Point(7 + look, -bh * 0.55), 2.6, slime.State == SlimeState.Windup ? 2 : 3.6);
            if (slime.IsBoss)
            {
                // Grumpy royal brows.
                dc.DrawLine(Ink, new Point(-11 + look, -bh * 0.72 + (slime.Enraged ? -1 : 1)), new Point(-4 + look, -bh * 0.66));
                dc.DrawLine(Ink, new Point(11 + look, -bh * 0.72 + (slime.Enraged ? -1 : 1)), new Point(4 + look, -bh * 0.66));
            }
        }
        if (slime.Type.Marks == "cracks")
        {
            // A stony body: two dark cracks.
            dc.DrawGeometry(null, Crack, Cracks);
        }
        // A support enemy: a white "+" on its body.
        if (slime.Type.Marks == "cross") dc.DrawGeometry(Brushes.White, Outline, CrossMark);
        if (slime.IsBoss) dc.DrawGeometry(Gold, Outline, Crown);
        dc.Pop();
        dc.Pop();
        double top = floor - h * sy - (slime.IsBoss ? 9 * size : 0);
        if (slime.State == SlimeState.Windup)
        {
            // The warning: a red "!" over a swelling slime.
            double y = top - 16;
            dc.DrawLine(Halo, new Point(x, y - 9), new Point(x, y + 1));
            dc.DrawLine(WarningPen, new Point(x, y - 9), new Point(x, y + 1));
            dc.DrawEllipse(Warning, Halo, new Point(x, y + 7), 2.4, 2.4);
            dc.DrawEllipse(Warning, null, new Point(x, y + 7), 2.4, 2.4);
            if (sim.CatHeld)
            {
                // The cat is in the user's hands: the attack is held, not gone. A pulsing ring on the
                // "!" says so, and a dashed ring on the floor shows where it still reaches (the hit
                // misses beyond Range + 12, see BattleSim.Strike): put the cat down outside it.
                dc.DrawEllipse(null, WarningEdge, new Point(x, y + 1), 9 + 2 * Math.Sin(time * 8), 9 + 2 * Math.Sin(time * 8));
                if (!slime.Type.Dash) // A dash shows its lane instead.
                {
                    dc.DrawEllipse(null, Halo, new Point(x, floor - 4), slime.Range + 12, 10);
                    dc.DrawEllipse(null, WarningEdge, new Point(x, floor - 4), slime.Range + 12, 10);
                }
            }
        }
        if (slime.State == SlimeState.Casting)
        {
            // The heal is coming: a pulsing green "+" over the healer and a dashed green ring under
            // the ally it heals. Push or stun the healer now to cancel it.
            double pulse = 1 + 0.15 * Math.Sin(time * 10);
            dc.PushTransform(new MatrixTransform(pulse, 0, 0, pulse, x, top - 12));
            dc.DrawGeometry(null, Halo, PlusMark);
            dc.DrawGeometry(HealGreen, Ink, PlusMark);
            dc.Pop();
            if (sim.Healing(slime) is Slime ally)
            {
                double ax = ally.X - OriginX;
                dc.DrawEllipse(null, Halo, new Point(ax, floor - 2), ally.Width / 2 + 8, 7);
                dc.DrawEllipse(null, HealDash, new Point(ax, floor - 2), ally.Width / 2 + 8, 7);
            }
        }
        if (slime.State == SlimeState.Stunned)
        {
            for (int i = 0; i < 3; i++)
            {
                double a = time * 5 + i * Math.PI * 2 / 3;
                var star = new Point(x + Math.Cos(a) * 16 * size, top - 6 + Math.Sin(a) * 4);
                dc.DrawEllipse(Marker, Outline, star, 3, 3);
            }
        }
        // The boss's HP is on the HUD; small bars only over ordinary slimes.
        if (targetable && !slime.IsBoss && (slime.Hp < slime.MaxHp || sim.SelectedId == slime.Id || sim.TargetId == slime.Id))
        {
            double barY = top - 9, barW = 36 * size, hpWidth = barW * Math.Clamp(slime.Hp / Math.Max(1, slime.MaxHp), 0, 1);
            dc.DrawRoundedRectangle(BarBack, null, new Rect(x - barW / 2 - 1, barY - 1, barW + 2, 7), 3, 3);
            if (hpWidth > 0) dc.DrawRoundedRectangle(BarFill, null, new Rect(x - barW / 2, barY, hpWidth, 5), 2.5, 2.5);
        }
        if (sim.SelectedId == slime.Id)
        {
            // Bobbing yellow arrow: readable on light and dark desktops (white halo, dark outline).
            double y = top - 30 - (slime.State == SlimeState.Windup ? 14 : 0) + Math.Sin(time * 4) * 3;
            dc.PushTransform(new TranslateTransform(x, y));
            dc.DrawGeometry(null, Halo, Arrow);
            dc.DrawGeometry(Marker, Ink, Arrow);
            dc.Pop();
        }
    }

    private static readonly Geometry Arrow = CreateArrow();
    // Body coordinates of the basic slime: the support mark below the eyes; the floating "+".
    private static readonly Geometry CrossMark = Parse("M -2,-15 L 2,-15 L 2,-11 L 6,-11 L 6,-7 L 2,-7 L 2,-3 L -2,-3 L -2,-7 L -6,-7 L -6,-11 L -2,-11 Z");
    private static readonly Geometry PlusMark = Parse("M -3,-9 L 3,-9 L 3,-3 L 9,-3 L 9,3 L 3,3 L 3,9 L -3,9 L -3,3 L -9,3 L -9,-3 L -3,-3 Z");
    private static Geometry Parse(string data) { var g = Geometry.Parse(data); g.Freeze(); return g; }
    // Mid blue: visible on light and dark desktops alike.
    private static readonly Pen Streak = FrozenPen(Frozen(230, 0x5A, 0x9C, 0xD0), 2.4);
    private static readonly Pen Crack = FrozenPen(Frozen(255, 0x5A, 0x52, 0x48), 1.6);
    // Body coordinates of the basic slime (scaled with the enemy).
    private static readonly Geometry Cracks = CreateCracks();
    private static Geometry CreateCracks()
    {
        var g = Geometry.Parse("M 10,-28 L 6,-21 L 11,-15 L 7,-9 M -14,-12 L -9,-8 L -12,-3");
        g.Freeze(); return g;
    }
    // Points right; mirrored for a push to the left.
    private static readonly Geometry PushArrow = CreatePushArrow();
    private static Geometry CreatePushArrow()
    {
        var g = Geometry.Parse("M -16,-5 L 8,-5 L 8,-12 L 22,0 L 8,12 L 8,5 L -16,5 Z");
        g.Freeze(); return g;
    }
    private static Geometry CreateArrow()
    {
        var g = Geometry.Parse("M -8,-9 L 8,-9 L 0,2 Z");
        g.Freeze(); return g;
    }

    // A small crown on the boss's head (body coordinates, before the boss's scale).
    private static readonly Geometry Crown = CreateCrown();
    private static Geometry CreateCrown()
    {
        var g = Geometry.Parse("M -10,-31 L -10,-41 L -5,-36 L 0,-43 L 5,-36 L 10,-41 L 10,-31 Z");
        g.Freeze(); return g;
    }

    private void DrawBomb(DrawingContext dc, BattleBomb bomb, double x, double floor)
    {
        double k = 1 - Math.Clamp(bomb.Remaining / sim.Settings.BombDelay, 0, 1);
        dc.DrawEllipse(null, BombRing, new Point(x, floor - 4), sim.Settings.BombRadius, 8);
        // A ball of yarn dropping onto the spot, trailing its thread.
        var ball = new Point(x, floor - 10 - (1 - k * k) * 100);
        dc.DrawLine(YarnPen, new Point(x + 6, ball.Y - 30), new Point(ball.X + 3, ball.Y - 6));
        dc.DrawEllipse(Yarn, Outline, ball, 9, 9);
        dc.DrawLine(YarnPen, new Point(ball.X - 6, ball.Y - 4), new Point(ball.X + 5, ball.Y + 5));
        dc.DrawLine(YarnPen, new Point(ball.X - 7, ball.Y + 2), new Point(ball.X + 2, ball.Y - 8));
    }

    // The windup's colour shift toward orange, in 16 frozen steps per base colour.
    private static readonly System.Collections.Generic.Dictionary<(Brush, int), Brush> BlendSteps = new();
    private static Brush Blend(Brush from, double k)
    {
        int step = (int)Math.Round(Math.Clamp(k, 0, 1) * 15);
        if (BlendSteps.TryGetValue((from, step), out var cached)) return cached;
        double f = step / 15.0;
        Color a = ((SolidColorBrush)from).Color, b = ((SolidColorBrush)SlimeAngry).Color;
        var brush = new SolidColorBrush(Color.FromRgb((byte)(a.R + (b.R - a.R) * f), (byte)(a.G + (b.G - a.G) * f), (byte)(a.B + (b.B - a.B) * f)));
        brush.Freeze();
        return BlendSteps[(from, step)] = brush;
    }

    // Floating numbers are few and repeat ("25", "+25"...): their outlines are built once.
    private static readonly System.Collections.Generic.Dictionary<string, (Geometry Shape, double Width, double Height)> Numbers = new();
    private static (Geometry Shape, double Width, double Height) NumberShape(string text, bool heal)
    {
        if (Numbers.TryGetValue(text, out var cached)) return cached;
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, NumberFont, heal ? 18 : 16, Brushes.White, 1);
        var geometry = formatted.BuildGeometry(new Point(0, 0));
        geometry.Freeze();
        if (Numbers.Count > 64) Numbers.Clear();
        return Numbers[text] = (geometry, formatted.Width, formatted.Height);
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

    private static void DrawEffect(DrawingContext dc, BattleEffect effect, double x, double y, double floor)
    {
        double k = Math.Clamp(effect.Age / effect.Life, 0, 1);
        dc.PushOpacity(effect.Kind == BattleEffectKind.Number ? Math.Min(1, (1 - k) * 2.5) : 1 - k);
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
                // Soft white puffs as the slime pops; nothing gory. Bigger for the boss.
                double big = Math.Max(1, effect.Value);
                for (int i = 0; i < 5 + 2 * (int)(big - 1); i++)
                {
                    double a = i * Math.PI * 2 / (5 + 2 * (int)(big - 1)) - Math.PI / 2;
                    var c = new Point(x + Math.Cos(a) * 18 * k * big, y + 6 + Math.Sin(a) * 12 * k * big);
                    dc.DrawEllipse(Brushes.White, Outline, c, (5 + 4 * k) * big, (5 + 4 * k) * big);
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
            case BattleEffectKind.Explosion:
                // A burst of yarn fluff: an orange ring and pink puffs along the floor.
                double radius = effect.Value * (0.4 + 0.6 * k);
                dc.DrawEllipse(null, Halo, new Point(x, floor - 14), radius, radius * 0.45);
                dc.DrawEllipse(null, BlastPen, new Point(x, floor - 14), radius, radius * 0.45);
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3;
                    dc.DrawEllipse(Yarn, Outline, new Point(x + Math.Cos(a) * radius * 0.7, floor - 14 + Math.Sin(a) * radius * 0.3), 6 * (1 - k) + 2, 6 * (1 - k) + 2);
                }
                break;
            case BattleEffectKind.Mend:
                // Three small green pluses rising from a healed enemy.
                for (int i = 0; i < 3; i++)
                {
                    double px = x + (i - 1) * 12, py = y - 8 - 26 * k - (i % 2) * 6;
                    dc.PushTransform(new MatrixTransform(0.6, 0, 0, 0.6, px, py));
                    dc.DrawGeometry(HealGreen, Ink, PlusMark);
                    dc.Pop();
                }
                break;
            case BattleEffectKind.Thud:
                // A heavy weapon hit: a flat ring on the floor under the slime and dust kicked up both ways.
                double spread = 14 + 22 * k;
                dc.DrawEllipse(null, Halo, new Point(x, floor - 3), spread, spread * 0.28);
                dc.DrawEllipse(null, SparkPen, new Point(x, floor - 3), spread, spread * 0.28);
                for (int i = 0; i < 4; i++)
                {
                    int side = i % 2 == 0 ? -1 : 1;
                    double reach = (10 + 6 * (i / 2)) + 16 * k;
                    dc.DrawEllipse(Brushes.White, Outline, new Point(x + side * reach, floor - 6 - 10 * k * (1 + i / 2)), 4.5 * (1 - k) + 2, 4.5 * (1 - k) + 2);
                }
                break;
            case BattleEffectKind.Slam:
                // Shock waves running out along the floor from the boss.
                foreach (int side in new[] { -1, 1 })
                {
                    double at = x + side * effect.Value * k;
                    dc.DrawLine(Halo, new Point(at, floor - 2), new Point(at - side * 12, floor - 16));
                    dc.DrawLine(SlamPen, new Point(at, floor - 2), new Point(at - side * 12, floor - 16));
                }
                break;
            case BattleEffectKind.Combo:
                // A bigger gold burst than a plain hit, a ring, and "COMBO!" rising above it.
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4 + 0.2;
                    var from = new Point(x + Math.Cos(a) * (8 + 14 * k), y + Math.Sin(a) * (8 + 14 * k));
                    var to = new Point(x + Math.Cos(a) * (20 + 22 * k), y + Math.Sin(a) * (20 + 22 * k));
                    dc.DrawLine(Halo, from, to);
                    dc.DrawLine(SparkPen, from, to);
                }
                dc.DrawEllipse(null, SparkPen, new Point(x, y), 12 + 26 * k, 12 + 26 * k);
                var (word, wordWidth, wordHeight) = NumberShape("COMBO!", false);
                dc.PushTransform(new TranslateTransform(x - wordWidth / 2, y - 40 - 20 * k - wordHeight));
                dc.DrawGeometry(null, TextHalo, word);
                dc.DrawGeometry(ComboGold, null, word);
                dc.Pop();
                break;
            case BattleEffectKind.Number:
                // Damage numbers rise from the slime; heals (+HP, green) rise over the cat.
                bool heal = effect.Value < 0;
                string text = heal ? "+" + (-effect.Value).ToString("0", CultureInfo.InvariantCulture) : effect.Value.ToString("0", CultureInfo.InvariantCulture);
                var (shape, width, height) = NumberShape(text, heal);
                dc.PushTransform(new TranslateTransform(x - width / 2, y - 26 * k - height));
                dc.DrawGeometry(null, TextHalo, shape);
                dc.DrawGeometry(heal ? HealGreen : DamageWhite, null, shape);
                dc.Pop();
                break;
        }
        dc.Pop();
    }
}
