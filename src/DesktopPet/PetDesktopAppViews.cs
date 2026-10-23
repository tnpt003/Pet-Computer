using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// One desktop app of the battle (BattleSim.Desktop) in its own small window, drawn like a desktop
// shortcut: the tile, the shortcut arrow, the name under it. Charging, a grey veil goes down as it
// fills; charged, it glows and bobs; infected, goo drips over it, a virus sits on its corner and
// it shakes, with one pip per click still needed. It pops in, squashes when it lands from a hop,
// inches along when it crawls, and bursts when it is used, scrubbed or cleaned. Drawing only.
public sealed class DesktopAppIcon : FrameworkElement
{
    public const double ViewWidth = 100, ViewHeight = 124, TileSize = 50;
    // Where the tile's centre is in this view.
    public static readonly Point Centre = new(ViewWidth / 2, 40);
    private readonly BattleSim sim;
    public int AppId { get; }
    public string Language { get; set; } = PetLanguage.English;
    private bool English => PetLanguage.UsesEnglish(Language);
    public event Action? Clicked;
    public event Action<bool>? HoverChanged;
    public bool Hovered { get; private set; }

    public DesktopAppIcon(BattleSim sim, int appId)
    {
        this.sim = sim;
        AppId = appId;
        Width = ViewWidth; Height = ViewHeight;
        Cursor = Cursors.Hand;
        MouseLeftButtonDown += (_, e) => { Clicked?.Invoke(); e.Handled = true; };
        MouseEnter += (_, _) => { Hovered = true; HoverChanged?.Invoke(true); InvalidateVisual(); };
        MouseLeave += (_, _) => { Hovered = false; HoverChanged?.Invoke(false); InvalidateVisual(); };
    }

    private static readonly Brush Veil = AppArt.Frozen(150, 0x26, 0x23, 0x38);
    private static readonly Brush Selection = AppArt.Frozen(70, 0xFF, 0xF6, 0xE6);
    private static readonly Pen SelectionRim = AppArt.FrozenPen(AppArt.Frozen(150, 0xFF, 0xF6, 0xE6), 1);
    private static readonly Brush GoldGlow = AppArt.Frozen(110, 0xFF, 0xD2, 0x3F);
    private static readonly Pen GoldRing = AppArt.FrozenPen(AppArt.Frozen(255, 0xFF, 0xD2, 0x3F), 3);
    private static readonly Brush GooTint = AppArt.Frozen(80, 0x7A, 0xD9, 0x4E);
    private static readonly Pen PipPen = AppArt.FrozenPen(AppArt.Frozen(255, 0xE5, 0x4B, 0x4B), 1.6);
    private static readonly Brush Bubble = AppArt.Frozen(200, 255, 255, 255);
    private static readonly Pen BubbleRim = AppArt.FrozenPen(AppArt.Frozen(200, 0x9A, 0xD8, 0xF0), 1.2);
    private static readonly Brush ArrowBlue = AppArt.Frozen(255, 0x2F, 0x7B, 0xD6);
    private static readonly Pen ArrowPen = AppArt.FrozenPen(ArrowBlue, 1.8);
    private static readonly Pen TextHalo = AppArt.FrozenPen(AppArt.Frozen(230, 0x1E, 0x1B, 0x2C), 3);
    private static readonly Brush LabelGreen = AppArt.Frozen(255, 0xA8, 0xF0, 0x7A);
    private static readonly Typeface LabelFont = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    // An elastic pop: 0 → past 1 → 1.
    private static double Pop(double k) => k >= 1 ? 1 : 1 - Math.Pow(1 - k, 3) * Math.Cos(k * Math.PI * 2.5);

    protected override void OnRender(DrawingContext dc)
    {
        if (sim.FindApp(AppId) is not DesktopApp app) return;
        double t = sim.Time;
        double born = (t - app.BornAt - (int)app.Kind * 0.12) / 0.45;
        if (born <= 0) return;
        // The battle's outro: the icons fade and sink away.
        double fade = sim.Over ? Math.Clamp(sim.PhaseRemaining / Math.Max(0.01, sim.Settings.OutroSeconds), 0, 1) : 1;
        if (fade <= 0) return;
        dc.PushOpacity(fade);
        var c = Centre;
        double sx = 1, sy = 1, dy = 0, tilt = 0;
        double grow = Pop(Math.Min(1, born)) * (sim.Over ? 0.6 + 0.4 * fade : 1);
        // A hop: stretched in the air, squashed on landing; a crawl: inching (the window moves it).
        double moved = app.MovedAt >= 0 ? t - app.MovedAt : double.MaxValue;
        if (!app.Crawled && moved < HopSeconds) { sx = 0.9; sy = 1.12; }
        else if (!app.Crawled && moved < HopSeconds + 0.25) { double s = Math.Sin((moved - HopSeconds) / 0.25 * Math.PI); sx = 1 + 0.2 * s; sy = 1 - 0.18 * s; }
        else if (app.Crawled && moved < CrawlSeconds) { double s = Math.Sin(moved * 14); sx = 1 + 0.12 * s; sy = 1 - 0.1 * s; }
        double used = app.UsedAt >= 0 ? t - app.UsedAt : double.MaxValue;
        if (used < 0.35) { double s = 1 - 0.22 * Math.Sin(used / 0.35 * Math.PI); sx *= s; sy *= s; }
        double ready = app.ReadyAt >= 0 ? t - app.ReadyAt : double.MaxValue;
        if (app.Ready)
        {
            dy = Math.Sin(t * 3 + app.Id) * 2;
            if (ready < 0.5) { double s = 1 + 0.2 * Math.Sin(ready / 0.5 * Math.PI); sx *= s; sy *= s; }
        }
        double scrubbed = app.ScrubbedAt >= 0 ? t - app.ScrubbedAt : double.MaxValue;
        if (scrubbed < 0.25) { double s = Math.Sin(scrubbed / 0.25 * Math.PI); sx *= 1 + 0.14 * s; sy *= 1 - 0.12 * s; }
        if (app.Infected)
        {
            // Fits of shaking, as if something inside wants out.
            double fit = (t + app.Id * 0.7) % 1.6;
            if (fit < 0.45) tilt = Math.Sin(t * 40) * 6 * (1 - fit / 0.45);
            double hit = t - app.InfectedAt;
            if (hit < 0.4) { double s = Math.Sin(hit / 0.4 * Math.PI); sx *= 1 + 0.25 * s; sy *= 1 - 0.2 * s; }
        }
        var tile = new Point(c.X, c.Y + dy);
        // Hovered: a soft desktop selection box behind the icon and its name.
        if (Hovered) dc.DrawRoundedRectangle(Selection, SelectionRim, new Rect(4, 6, ViewWidth - 8, ViewHeight - 10), 6, 6);
        if (app.Ready)
        {
            double pulse = 1 + 0.08 * Math.Sin(t * 5);
            dc.DrawEllipse(GoldGlow, null, tile, 36 * pulse * grow, 36 * pulse * grow);
        }
        // Squash and stretch about the tile's bottom (it stands on the desktop), tilt about its centre.
        dc.PushTransform(new TranslateTransform(tile.X, tile.Y + TileSize / 2));
        dc.PushTransform(new ScaleTransform(sx * grow, sy * grow));
        dc.PushTransform(new RotateTransform(tilt, 0, -TileSize / 2));
        var local = new Point(0, -TileSize / 2);
        double lid = app.Kind == BattleAppKind.Trash ? BinLid(app, t) : 0;
        AppArt.DrawTile(dc, app.Kind, local, TileSize, lid);
        var box = new Rect(local.X - TileSize / 2, local.Y - TileSize / 2, TileSize, TileSize);
        double corner = TileSize * 0.24;
        if (!app.Ready && !app.Infected)
        {
            // The veil over what is still to charge (top part), a bright edge at the fill line.
            dc.PushClip(new RectangleGeometry(box, corner, corner));
            double left = box.Height * (1 - app.Charge);
            dc.DrawRectangle(Veil, null, new Rect(box.X, box.Y, box.Width, left));
            dc.DrawLine(FillEdge, new Point(box.X, box.Y + left), new Point(box.Right, box.Y + left));
            dc.Pop();
        }
        if (app.Infected) DrawInfection(dc, box, corner, t, app);
        DrawShortcutArrow(dc, new Point(box.X + 7, box.Bottom - 7));
        dc.Pop(); dc.Pop(); dc.Pop();
        // Twinkles circling a charged icon.
        if (app.Ready)
            for (int i = 0; i < 3; i++)
            {
                double a = t * 1.8 + i * Math.PI * 2 / 3;
                AppArt.DrawTwinkle(dc, new Point(tile.X + Math.Cos(a) * 33, tile.Y + Math.Sin(a) * 30), 4.5 + 1.5 * Math.Sin(t * 6 + i), AppArt.Gold);
            }
        if (app.Infected) DrawPips(dc, new Point(c.X, c.Y + TileSize / 2 + 8), app);
        DrawLabel(dc, app, c, t);
        DrawBursts(dc, app, tile, t, ready, used, scrubbed);
        dc.Pop();
    }
    private static readonly Pen FillEdge = AppArt.FrozenPen(AppArt.Frozen(200, 0xFF, 0xF6, 0xE6), 1.5);

    public const double HopSeconds = 0.5, CrawlSeconds = 1.4;

    // The bin opens its lid as it swallows its catch.
    private double BinLid(DesktopApp app, double t)
    {
        foreach (var l in sim.Launches)
            if (l.AppId == app.Id && l.Kind == LaunchKind.Trash) return Math.Clamp((l.Progress - 0.4) / 0.3, 0, 1);
        double since = app.UsedAt >= 0 ? t - app.UsedAt - BattleSim.LaunchSeconds : double.MaxValue;
        return since >= 0 && since < 0.3 ? 1 - since / 0.3 : 0;
    }

    // Goo over the tile: a green tint, a cap of goo on top with drips that stretch and fall, and the
    // virus clinging to the top-right corner.
    private static void DrawInfection(DrawingContext dc, Rect box, double corner, double t, DesktopApp app)
    {
        dc.PushClip(new RectangleGeometry(box, corner, corner));
        dc.DrawRectangle(GooTint, null, box);
        dc.Pop();
        var cap = new StreamGeometry();
        using (var g = cap.Open())
        {
            g.BeginFigure(new Point(box.X - 1, box.Y + 6), true, true);
            g.QuadraticBezierTo(new Point(box.X - 2, box.Y - 4), new Point(box.X + 10, box.Y - 3), true, true);
            g.QuadraticBezierTo(new Point(box.X + box.Width / 2, box.Y - 7), new Point(box.Right - 8, box.Y - 2), true, true);
            g.QuadraticBezierTo(new Point(box.Right + 2, box.Y - 2), new Point(box.Right + 1, box.Y + 7), true, true);
            // Wavy lower edge with three drips.
            for (int i = 3; i >= 0; i--)
            {
                double x = box.X + box.Width * i / 3;
                double drip = i is 1 or 2 ? 6 + 5 * ((t * 0.8 + i * 0.37) % 1) : 0;
                g.QuadraticBezierTo(new Point(x + 4, box.Y + 9 + drip), new Point(x, box.Y + 7 + (i % 2) * 2), true, true);
            }
        }
        cap.Freeze();
        dc.DrawGeometry(AppArt.GooGreen, AppArt.ThinInk, cap);
        // A drop that falls off now and then.
        double fall = (t * 0.8 + app.Id * 0.3) % 1;
        dc.DrawEllipse(AppArt.GooGreen, AppArt.ThinInk, new Point(box.X + box.Width * 0.68, box.Y + 10 + fall * 40), 2.4 * (1 - fall * 0.5), 3 * (1 - fall * 0.5));
        AppArt.DrawVirus(dc, new Point(box.Right - 2, box.Y + 2), 8, t);
    }

    // The shortcut overlay: a white square with a curved arrow (the generic shortcut sign).
    private static void DrawShortcutArrow(DrawingContext dc, Point c)
    {
        var r = new Rect(c.X - 7, c.Y - 7, 14, 14);
        dc.DrawRoundedRectangle(Brushes.White, AppArt.ThinInk, r, 2.5, 2.5);
        var arc = new StreamGeometry();
        using (var g = arc.Open())
        {
            g.BeginFigure(new Point(c.X - 3.5, c.Y + 4), false, false);
            g.QuadraticBezierTo(new Point(c.X - 3.5, c.Y - 3), new Point(c.X + 3, c.Y - 3), true, true);
        }
        arc.Freeze();
        dc.DrawGeometry(null, ArrowPen, arc);
        dc.DrawLine(ArrowPen, new Point(c.X + 3, c.Y - 3), new Point(c.X + 0.5, c.Y - 5.5));
        dc.DrawLine(ArrowPen, new Point(c.X + 3, c.Y - 3), new Point(c.X + 0.5, c.Y - 0.5));
    }

    // One pip per click still needed to scrub the virus (red), the scrubbed ones hollow.
    private void DrawPips(DrawingContext dc, Point c, DesktopApp app)
    {
        int total = Math.Max(app.ScrubsLeft, sim.AppRules?.ScrubClicks ?? app.ScrubsLeft);
        for (int i = 0; i < total; i++)
        {
            var at = new Point(c.X + (i - (total - 1) / 2.0) * 9, c.Y);
            dc.DrawEllipse(i < app.ScrubsLeft ? AppArt.BadgeRed : Brushes.White, PipPen, at, 3, 3);
        }
    }

    // The name under the icon, white with a dark rim like a desktop label (gold when charged, green
    // when infected).
    private static readonly Dictionary<string, (Geometry Shape, double Width, double Height)> Labels = new();
    private void DrawLabel(DrawingContext dc, DesktopApp app, Point c, double t)
    {
        string name = sim.AppRules?.App(app.Kind).Name(Language) ?? app.Kind.ToString();
        if (!Labels.TryGetValue(name, out var label))
        {
            var text = new FormattedText(name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, LabelFont, 11.5, Brushes.White, 1)
                { MaxTextWidth = ViewWidth - 6, TextAlignment = TextAlignment.Center, MaxLineCount = 2, Trimming = TextTrimming.CharacterEllipsis };
            var shape = text.BuildGeometry(new Point(0, 0));
            shape.Freeze();
            if (Labels.Count > 32) Labels.Clear();
            Labels[name] = label = (shape, text.MaxTextWidth, text.Height);
        }
        double top = c.Y + TileSize / 2 + (app.Infected ? 14 : 6);
        dc.PushTransform(new TranslateTransform(c.X - label.Width / 2, top));
        dc.DrawGeometry(null, TextHalo, label.Shape);
        dc.DrawGeometry(app.Infected ? LabelGreen : app.Ready ? AppArt.Gold : Brushes.White, null, label.Shape);
        dc.Pop();
    }

    private static readonly Dictionary<BattleAppKind, Pen> SparkPens = new();
    private static Pen SparkPen(BattleAppKind kind)
    {
        if (SparkPens.TryGetValue(kind, out var pen)) return pen;
        pen = AppArt.FrozenPen(AppArt.Back(kind), 3);
        return SparkPens[kind] = pen;
    }

    // Short bursts over the icon: charged (a gold ring), used (sparks in its colour), scrubbed
    // (soap bubbles), cleaned (twinkles), infected (goo splashes).
    private static void DrawBursts(DrawingContext dc, DesktopApp app, Point c, double t, double ready, double used, double scrubbed)
    {
        if (ready < 0.6)
        {
            double k = ready / 0.6;
            dc.PushOpacity(1 - k);
            dc.DrawEllipse(null, GoldRing, c, 26 + 26 * k, 26 + 26 * k);
            dc.Pop();
        }
        if (used < 0.5)
        {
            double k = used / 0.5;
            dc.PushOpacity(1 - k);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4 + 0.3;
                var from = new Point(c.X + Math.Cos(a) * (20 + 10 * k), c.Y + Math.Sin(a) * (20 + 10 * k));
                var to = new Point(c.X + Math.Cos(a) * (28 + 22 * k), c.Y + Math.Sin(a) * (28 + 22 * k));
                dc.DrawLine(AppArt.HaloPen, from, to);
                dc.DrawLine(SparkPen(app.Kind), from, to);
            }
            dc.Pop();
        }
        if (scrubbed < 0.6)
        {
            double k = scrubbed / 0.6;
            dc.PushOpacity(1 - k);
            for (int i = 0; i < 6; i++)
            {
                double x = c.X - 22 + i * 9 + Math.Sin(i * 2.1 + k * 6) * 4, y = c.Y + 10 - k * (30 + i % 3 * 10);
                dc.DrawEllipse(Bubble, BubbleRim, new Point(x, y), 3 + i % 3, 3 + i % 3);
            }
            dc.Pop();
        }
        double cleaned = app.CleanedAt >= 0 ? t - app.CleanedAt : double.MaxValue;
        if (cleaned < 0.8)
        {
            double k = cleaned / 0.8;
            dc.PushOpacity(1 - k);
            for (int i = 0; i < 6; i++)
            {
                double a = i * Math.PI / 3 - Math.PI / 2;
                AppArt.DrawTwinkle(dc, new Point(c.X + Math.Cos(a) * (18 + 26 * k), c.Y + Math.Sin(a) * (18 + 26 * k)), 6 * (1 - k * 0.5), Brushes.White);
            }
            dc.Pop();
        }
        double infected = app.InfectedAt >= 0 ? t - app.InfectedAt : double.MaxValue;
        if (app.Infected && infected < 0.6)
        {
            double k = infected / 0.6;
            for (int i = 0; i < 6; i++)
            {
                double a = -Math.PI * (0.1 + 0.8 * i / 5.0);
                var at = new Point(c.X + Math.Cos(a) * (14 + 26 * k), c.Y - 20 + Math.Sin(a) * (10 + 18 * k) + 40 * k * k);
                dc.DrawEllipse(AppArt.GooGreen, AppArt.ThinInk, at, 3.5 * (1 - k * 0.5), 3.5 * (1 - k * 0.5));
            }
        }
    }
}

// What flies between the icons and the field (BattleSim.Launches), on one click-through canvas over
// the desktop area and the strip. The window gives the icon centres and floor points in this
// canvas's DIPs; a flight follows an arc, with a short trail. Drawing only.
public sealed class AppFlightView : FrameworkElement
{
    private readonly BattleSim sim;
    public string Language { get; set; } = PetLanguage.English;
    private bool English => PetLanguage.UsesEnglish(Language);
    // An icon's tile centre (null: not shown), and a point `height` above the floor at floor X.
    public Func<int, Point?> IconCentre { get; set; } = _ => null;
    public Func<double, double, Point> FloorPoint { get; set; } = (x, h) => new Point(x, -h);

    public AppFlightView(BattleSim sim)
    {
        this.sim = sim;
        IsHitTestVisible = false;
    }

    private static readonly Brush TealOrb = AppArt.Frozen(255, 0x3F, 0xB0, 0x9A);
    private static readonly Brush TrailGold = AppArt.Frozen(170, 0xFF, 0xD2, 0x3F);
    private static readonly Brush TrailGreen = AppArt.Frozen(170, 0x7A, 0xD9, 0x4E);
    private static readonly Brush TrailPaper = AppArt.Frozen(170, 0xFF, 0xF6, 0xE6);
    private static readonly Brush TrailTeal = AppArt.Frozen(170, 0x3F, 0xB0, 0x9A);
    private static readonly Brush TrailRed = AppArt.Frozen(170, 0xE5, 0x4B, 0x4B);
    private static readonly Pen PlusPen = AppArt.FrozenPen(Brushes.White, 3);

    // Along the arc from `from` to `to` at t (0–1): bulging upward by a share of the distance.
    private static Point Arc(Point from, Point to, double t)
    {
        double distance = (to - from).Length, lift = Math.Clamp(distance * 0.25, 30, 130);
        return new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t - Math.Sin(t * Math.PI) * lift);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double time = sim.Time;
        foreach (var launch in sim.Launches)
        {
            if (IconCentre(launch.AppId) is not Point icon) continue;
            double t = launch.Progress;
            // Fast out of the icon, easing in to the target (or the other way going in).
            double e = launch.ToIcon ? t * t : 1 - (1 - t) * (1 - t);
            switch (launch.Kind)
            {
                case LaunchKind.Bell:
                    for (int i = 0; i < launch.Targets.Length; i++)
                    {
                        double x = sim.Find(launch.Targets[i]) is { } s ? s.X : launch.TargetX[i];
                        double h = sim.Find(launch.Targets[i]) is { } ss ? ss.Height + 26 : 40;
                        // Each bell leaves a moment after the one before.
                        double delay = Math.Min(i * 0.06, 0.5), ti = Math.Clamp((t - delay) / (1 - delay), 0, 1);
                        if (ti <= 0) continue;
                        var to = FloorPoint(x, h);
                        Trail(dc, icon, to, ti, TrailGold);
                        var at = Arc(icon, to, 1 - (1 - ti) * (1 - ti));
                        dc.PushTransform(new RotateTransform(Math.Sin(time * 20 + i) * 20, at.X, at.Y - 10));
                        AppArt.DrawBell(dc, at, 1.1, false);
                        dc.Pop();
                    }
                    break;
                case LaunchKind.Trash:
                    for (int i = 0; i < launch.TargetX.Length; i++)
                    {
                        var from = FloorPoint(launch.TargetX[i], 18);
                        double delay = Math.Min(i * 0.08, 0.4), ti = Math.Clamp((t - delay) / (1 - delay), 0, 1);
                        var at = Arc(from, icon, ti * ti);
                        // Stretched toward the bin, shrinking as it goes in, spinning.
                        double r = 13 * (1 - 0.65 * ti);
                        Trail(dc, from, icon, ti * ti, TrailPaper);
                        dc.PushTransform(new RotateTransform(ti * 540, at.X, at.Y));
                        AppArt.DrawBlob(dc, at, r, AppArt.Tint(launch.Colors.Length > i ? launch.Colors[i] : "#7CD36A"), 1 + 0.4 * Math.Sin(ti * Math.PI));
                        dc.Pop();
                    }
                    break;
                default:
                    var floor = FloorPoint(TargetX(launch), launch.Height);
                    var start = launch.ToIcon ? floor : icon;
                    var end = launch.ToIcon ? icon : floor;
                    var p = Arc(start, end, e);
                    DrawOne(dc, launch, start, end, p, e, time);
                    break;
            }
        }
    }

    // Where the floor end is now: the cat for what lands on it, the slime for what goes to one.
    private double TargetX(AppLaunch launch) => launch.Kind switch
    {
        LaunchKind.Tabs or LaunchKind.Undo or LaunchKind.BadBell or LaunchKind.Freeze => sim.CatX,
        LaunchKind.Goo => sim.Find(launch.SlimeId)?.X ?? launch.FloorX,
        LaunchKind.EndTask or LaunchKind.Mend when launch.Targets.Length > 0 => sim.Find(launch.Targets[0])?.X ?? launch.FloorX,
        _ => launch.FloorX
    };

    private void DrawOne(DrawingContext dc, AppLaunch launch, Point start, Point end, Point p, double e, double time)
    {
        var ahead = Arc(start, end, Math.Min(1, e + 0.02));
        double angle = Math.Atan2(ahead.Y - p.Y, ahead.X - p.X);
        switch (launch.Kind)
        {
            case LaunchKind.Tabs:
                Trail(dc, start, end, e, TrailPaper);
                for (int i = 0; i < 3; i++)
                {
                    double a = time * 12 + i * Math.PI * 2 / 3;
                    var at = new Point(p.X + Math.Cos(a) * 11, p.Y + Math.Sin(a) * 8);
                    dc.PushTransform(new RotateTransform(a * 40, at.X, at.Y));
                    AppArt.DrawTab(dc, at, 1.25);
                    dc.Pop();
                }
                break;
            case LaunchKind.EndTask:
            case LaunchKind.Freeze:
                Trail(dc, start, end, e, launch.Kind == LaunchKind.Freeze ? TrailRed : TrailPaper);
                dc.PushTransform(new RotateTransform(Math.Sin(time * 14) * 8, p.X, p.Y));
                AppArt.DrawNotResponding(dc, p, 0.45 + 0.4 * e, Language);
                dc.Pop();
                break;
            case LaunchKind.Undo:
                Trail(dc, start, end, e, TrailTeal);
                dc.DrawEllipse(TealOrb, AppArt.InkPen, p, 10, 10);
                dc.PushTransform(new RotateTransform(-time * 720 % 360, p.X, p.Y));
                AppArt.DrawUndo(dc, p, 6, AppArt.PaperPen);
                dc.Pop();
                break;
            case LaunchKind.Goo:
                Trail(dc, start, end, e, TrailGreen);
                AppArt.DrawGoo(dc, p, 8, angle, 1.5);
                break;
            case LaunchKind.BadBell:
                Trail(dc, start, end, e, TrailRed);
                dc.PushTransform(new RotateTransform(Math.Sin(time * 22) * 25, p.X, p.Y - 10));
                AppArt.DrawBell(dc, p, 1.2, true);
                dc.Pop();
                break;
            case LaunchKind.Mend:
                Trail(dc, start, end, e, TrailGreen);
                dc.DrawEllipse(AppArt.HealGreen, AppArt.InkPen, p, 9, 9);
                dc.DrawLine(PlusPen, new Point(p.X - 4.5, p.Y), new Point(p.X + 4.5, p.Y));
                dc.DrawLine(PlusPen, new Point(p.X, p.Y - 4.5), new Point(p.X, p.Y + 4.5));
                break;
            case LaunchKind.Spit:
                Trail(dc, start, end, e, TrailGreen);
                string color = SpitColor();
                dc.PushTransform(new RotateTransform(e * 360, p.X, p.Y));
                AppArt.DrawBlob(dc, p, 8 + 6 * e, AppArt.Tint(color), 1 + 0.3 * Math.Sin(e * Math.PI * 3));
                dc.Pop();
                break;
            case LaunchKind.Popup:
                double w = BattleSim.PopupWidth * (0.3 + 0.7 * e), h = BattleSim.PopupHeight * (0.3 + 0.7 * e);
                dc.PushTransform(new RotateTransform((1 - e) * 20, p.X, p.Y));
                AppArt.DrawAd(dc, new Rect(p.X - w / 2, p.Y - h / 2, w, h), Language, 0, time);
                dc.Pop();
                break;
        }
    }

    // The colour of the slime an infected bin spits out (a slime green when it is not in the data).
    private string SpitColor()
    {
        if (sim.AppRules is AppsSpec rules)
            foreach (var kind in sim.Settings.EnemyTypes) if (kind.Id == rules.PopupSpawn) return kind.Color;
        return "#7CD36A";
    }

    // A few fading dots behind a flight.
    private static void Trail(DrawingContext dc, Point from, Point to, double e, Brush fill)
    {
        for (int i = 1; i <= 4; i++)
        {
            double back = e - i * 0.06;
            if (back <= 0) break;
            var at = Arc(from, to, back);
            dc.PushOpacity(1 - i * 0.22);
            dc.DrawEllipse(fill, null, at, 4.5 - i * 0.7, 4.5 - i * 0.7);
            dc.Pop();
        }
    }
}
