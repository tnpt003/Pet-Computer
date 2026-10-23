using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// The desktop apps' drawings, shared by the icons (DesktopAppIcon), what flies (AppFlightView) and
// the strip (BattleFieldView): the game's own art in its warm colours, never a real app's logo.
// Everything is drawn around a centre point at a scale (1 = the 50 DIP desktop icon).
public static class AppArt
{
    private static Geometry Parse(string path) { var g = Geometry.Parse(path); g.Freeze(); return g; }

    public static readonly Brush Ink = Frozen(255, 0x30, 0x2B, 0x48);
    public static readonly Pen InkPen = FrozenPen(Ink, 2.2);
    public static readonly Pen ThinInk = FrozenPen(Ink, 1.5);
    public static readonly Pen HaloPen = FrozenPen(Brushes.White, 5);
    public static readonly Brush Paper = Frozen(255, 0xFF, 0xF6, 0xE6);
    public static readonly Pen PaperPen = FrozenPen(Paper, 3.2);
    public static readonly Brush BadgeRed = Frozen(255, 0xE5, 0x4B, 0x4B);
    public static readonly Brush Shine = Frozen(90, 255, 255, 255);
    public static readonly Brush Gold = Frozen(255, 0xFF, 0xD2, 0x3F);
    public static readonly Brush GooGreen = Frozen(255, 0x7A, 0xD9, 0x4E);
    public static readonly Brush GooDark = Frozen(255, 0x3E, 0x9A, 0x2E);
    public static readonly Brush VirusPurple = Frozen(255, 0x8E, 0x4F, 0xC9);
    public static readonly Brush HealGreen = Frozen(255, 0x7C, 0xF0, 0x7A);
    public static readonly Brush WindowGrey = Frozen(255, 0xEE, 0xEA, 0xF4);
    public static readonly Brush WindowBar = Frozen(255, 0x8B, 0x7F, 0xB8);
    public static readonly Brush AdYellow = Frozen(255, 0xFF, 0xE0, 0x6B);
    public static readonly Brush AdBar = Frozen(255, 0xE2, 0x5A, 0x7A);
    public static readonly Pen UndoPen = FrozenPen(Frozen(255, 0x3F, 0xB0, 0x9A), 4);
    private static readonly Typeface Bold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);

    // Each app's tile colour: tabs coral, bell amber, power slate purple, undo teal, bin sage.
    public static Brush Back(BattleAppKind kind) => kind switch
    {
        BattleAppKind.Tabs => TabsBack, BattleAppKind.Ping => PingBack, BattleAppKind.EndTask => EndTaskBack, BattleAppKind.Undo => UndoBack, _ => TrashBack
    };
    private static readonly Brush TabsBack = Frozen(255, 0xF2, 0x89, 0x6B);
    private static readonly Brush PingBack = Frozen(255, 0xF2, 0xB2, 0x3E);
    private static readonly Brush EndTaskBack = Frozen(255, 0x6E, 0x5E, 0xA8);
    private static readonly Brush UndoBack = Frozen(255, 0x3F, 0xB0, 0x9A);
    private static readonly Brush TrashBack = Frozen(255, 0x8F, 0xB5, 0x6A);

    // A word drawn as an outline (the same few repeat): cached. The strip keeps its own (TagShape,
    // NumberShape) for its words and numbers.
    private static readonly Dictionary<(string, double), (Geometry Shape, double Width, double Height)> Words = new();
    public static (Geometry Shape, double Width, double Height) Word(string text, double size)
    {
        if (Words.TryGetValue((text, size), out var cached)) return cached;
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Bold, size, Brushes.White, 1);
        var geometry = formatted.BuildGeometry(new Point(0, 0));
        geometry.Freeze();
        if (Words.Count > 128) Words.Clear();
        return Words[(text, size)] = (geometry, formatted.Width, formatted.Height);
    }

    // The app tile: a rounded square with its glyph (size: the tile's side in DIPs).
    // lid: how far the bin's lid is open (0–1).
    public static void DrawTile(DrawingContext dc, BattleAppKind kind, Point c, double size, double lid = 0)
    {
        double h = size / 2, corner = size * 0.24, k = size / 34;
        var box = new Rect(c.X - h, c.Y - h, size, size);
        dc.DrawRoundedRectangle(null, HaloPen, box, corner, corner);
        dc.DrawRoundedRectangle(Back(kind), InkPen, box, corner, corner);
        dc.DrawRoundedRectangle(Shine, null, new Rect(box.X + size * 0.12, box.Y + size * 0.09, size * 0.76, size * 0.24), corner * 0.5, corner * 0.5);
        DrawGlyph(dc, kind, c, k, lid);
    }

    // The glyph alone, around c (k = 1: as on a 34 DIP tile).
    public static void DrawGlyph(DrawingContext dc, BattleAppKind kind, Point c, double k, double lid = 0)
    {
        switch (kind)
        {
            case BattleAppKind.Tabs:
                for (int i = 2; i >= 0; i--) DrawTab(dc, new Point(c.X + (i * 4 - 4) * k, c.Y + (i * 4 - 4) * k), k);
                break;
            case BattleAppKind.Ping:
                DrawBell(dc, new Point(c.X - k, c.Y + 2 * k), k, false);
                break;
            case BattleAppKind.EndTask:
                dc.PushTransform(new MatrixTransform(k, 0, 0, k, c.X, c.Y));
                dc.DrawGeometry(null, PaperPen, PowerRing);
                dc.DrawLine(PaperPen, new Point(0, -11), new Point(0, -1));
                dc.Pop();
                break;
            case BattleAppKind.Undo:
                DrawUndo(dc, c, 9 * k, PaperPen);
                break;
            default:
                DrawBin(dc, c, k, lid);
                break;
        }
    }
    private static readonly Geometry PowerRing = Parse("M -6,-6 A 9,9 0 1 0 6,-6");

    // A page tab centred on c: a body with a raised tab on its top-left.
    public static void DrawTab(DrawingContext dc, Point c, double k)
    {
        dc.PushTransform(new MatrixTransform(k, 0, 0, k, c.X, c.Y));
        dc.DrawGeometry(Paper, ThinInk, TabShape);
        dc.DrawLine(ThinInk, new Point(-4, 1), new Point(4, 1));
        dc.Pop();
    }
    private static readonly Geometry TabShape = Parse("M -7,-3 L -5,-7 L 1,-7 L 3,-3 L 7,-3 L 7,7 L -7,7 Z");

    // A flying browser tab of the tornado, centred on c (about 30 × 20 at k 1): a white halo for
    // any desktop, the rounded tab with a coloured dot, a title line and its ×. color: the dot.
    public static void DrawFlyingTab(DrawingContext dc, Point c, double k, Brush color)
    {
        dc.PushTransform(new MatrixTransform(k, 0, 0, k, c.X, c.Y));
        dc.DrawGeometry(null, HaloPen, FlyingTabShape);
        dc.DrawGeometry(Paper, ThinInk, FlyingTabShape);
        dc.DrawRectangle(TabsStripe, null, new Rect(-14.2, 4, 28.4, 4.2));
        dc.DrawEllipse(color, ThinInk, new Point(-8, -2), 3, 3);
        dc.DrawLine(ThinInk, new Point(-3, -2), new Point(5, -2));
        dc.DrawLine(ThinInk, new Point(8.5, -4.5), new Point(12.5, 0.5));
        dc.DrawLine(ThinInk, new Point(12.5, -4.5), new Point(8.5, 0.5));
        dc.Pop();
    }
    private static readonly Geometry FlyingTabShape = Parse("M -15,9 L -15,-4 Q -15,-9 -10,-9 L 10,-9 Q 15,-9 15,-4 L 15,9 Z");
    private static readonly Brush TabsStripe = Frozen(255, 0xF7, 0xC2, 0xB0);
    // The tornado's three tab dots: the Tab app's coral, a gold and a teal.
    public static readonly Brush[] TabDots = { TabsBack, Gold, UndoBack };

    // The tornado's ring: three tabs circling (cx, middle) on a flat ring, each trailing a curved
    // swish along it. It is drawn in two halves so the cat comes between them: the far half (faded,
    // smaller) on the strip under the pet's window, the near half by the cat's renderer over the cat.
    public static void DrawTabRing(DrawingContext dc, double cx, double middle, double radius, double time, bool front)
    {
        Point On(double a) => new(cx + Math.Cos(a) * radius, middle + Math.Sin(a) * 11);
        for (int i = 0; i < 3; i++)
        {
            double a = time * 5 + i * Math.PI * 2 / 3;
            if (Math.Sin(a) >= 0 != front) continue;
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
            DrawFlyingTab(dc, at, size, TabDots[i]);
            dc.Pop();
            if (!front) dc.Pop();
        }
    }
    // The ring's drawn radius for the tornado's hit radius: capped so the near half stays whole
    // inside the pet's window (96 DIPs either side of the cat's middle).
    public static double TabRingRadius(double hitRadius) => Math.Min(hitRadius * 0.85, 74);
    private static readonly Pen SwishPen = RoundPen(Frozen(170, 255, 246, 230), 3.5);
    private static readonly Pen SwishBackPen = RoundPen(Frozen(90, 255, 246, 230), 2.5);
    private static Pen RoundPen(Brush brush, double width)
    {
        var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();
        return pen;
    }

    // A bell with a red "99+" badge (angry: a red glow and a frown, the infected app's).
    public static void DrawBell(DrawingContext dc, Point c, double k, bool angry)
    {
        dc.PushTransform(new MatrixTransform(k, 0, 0, k, c.X, c.Y));
        if (angry) dc.DrawEllipse(AngryGlow, null, new Point(0, -1), 15, 14);
        dc.DrawGeometry(Paper, ThinInk, BellShape);
        dc.DrawEllipse(Paper, ThinInk, new Point(0, 8), 2.5, 2.5);
        if (angry)
        {
            dc.DrawLine(ThinInk, new Point(-4, -4), new Point(-1, -2.5));
            dc.DrawLine(ThinInk, new Point(4, -4), new Point(1, -2.5));
            dc.DrawLine(ThinInk, new Point(-2.5, 2), new Point(2.5, 2));
        }
        dc.DrawRoundedRectangle(BadgeRed, ThinInk, new Rect(2, -14, 16, 10), 5, 5);
        var (badge, width, height) = Word("99+", 10);
        dc.PushTransform(new MatrixTransform(0.7, 0, 0, 0.7, 10 - width * 0.35, -9 - height * 0.35));
        dc.DrawGeometry(Brushes.White, null, badge);
        dc.Pop();
        dc.Pop();
    }
    private static readonly Geometry BellShape = Parse("M -9,6 C -7,4 -7,1 -7,-3 C -7,-8 -4,-10 0,-10 C 4,-10 7,-8 7,-3 C 7,1 7,4 9,6 Z");
    private static readonly Brush AngryGlow = Frozen(110, 0xE5, 0x4B, 0x4B);

    // A curved arrow turning back (counter-clockwise), radius r around c.
    public static void DrawUndo(DrawingContext dc, Point c, double r, Pen pen)
    {
        var arc = new StreamGeometry();
        using (var g = arc.Open())
        {
            g.BeginFigure(new Point(c.X + r, c.Y + 2), false, false);
            g.ArcTo(new Point(c.X - r * 0.7, c.Y - r * 0.6), new Size(r, r), 0, true, SweepDirection.Counterclockwise, true, false);
        }
        arc.Freeze();
        dc.DrawGeometry(null, pen, arc);
        var tip = new Point(c.X - r * 0.7, c.Y - r * 0.6);
        dc.DrawLine(pen, tip, new Point(tip.X + r * 0.1, tip.Y + r * 0.66));
        dc.DrawLine(pen, tip, new Point(tip.X + r * 0.66, tip.Y - r * 0.2));
    }

    // A small bin with a lid and three ribs.
    public static void DrawBin(DrawingContext dc, Point c, double k, double lidOpen = 0)
    {
        dc.PushTransform(new MatrixTransform(k, 0, 0, k, c.X, c.Y));
        dc.DrawGeometry(Paper, ThinInk, BinBody);
        foreach (double x in new[] { -3.0, 0, 3 }) dc.DrawLine(ThinInk, new Point(x, -2), new Point(x * 0.85, 7));
        dc.PushTransform(new RotateTransform(-lidOpen * 40, -8, -5));
        dc.DrawRoundedRectangle(Paper, ThinInk, new Rect(-9, -8, 18, 3.5), 1.5, 1.5);
        dc.DrawRoundedRectangle(Paper, ThinInk, new Rect(-2.5, -10.5, 5, 3), 1, 1);
        dc.Pop();
        dc.Pop();
    }
    private static readonly Geometry BinBody = Parse("M -7,-4 L 7,-4 L 5.5,9 L -5.5,9 Z");

    // A small window titled nothing, saying "Not responding" (scale: 1 = 32 DIPs tall, this wide).
    public static double NotRespondingWidth(string language) => Math.Max(Word(NotRespondingText(language), 10).Width + 14, 64);
    private static string NotRespondingText(string language) => PetText.Pick(language, "Không phản hồi", "Not responding");
    public static void DrawNotResponding(DrawingContext dc, Point c, double scale, string language)
    {
        var (label, lw, lh) = Word(NotRespondingText(language), 10);
        double w = NotRespondingWidth(language), h = 32;
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, c.X, c.Y));
        var r = new Rect(-w / 2, -h / 2, w, h);
        dc.DrawRoundedRectangle(null, HaloPen, r, 4, 4);
        dc.DrawRoundedRectangle(WindowGrey, InkPen, r, 4, 4);
        dc.DrawRoundedRectangle(WindowBar, InkPen, new Rect(r.X, r.Y, r.Width, 10), 4, 4);
        dc.PushTransform(new TranslateTransform(-lw / 2, r.Y + 21 - lh / 2));
        dc.DrawGeometry(Ink, null, label);
        dc.Pop();
        dc.Pop();
    }

    // The virus: a spiky green ball with angry eyes (r: its radius), wobbling with time.
    public static void DrawVirus(DrawingContext dc, Point c, double r, double time)
    {
        var spikes = new StreamGeometry();
        using (var g = spikes.Open())
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4 + time * 1.5;
                var from = new Point(c.X + Math.Cos(a) * r * 0.8, c.Y + Math.Sin(a) * r * 0.8);
                g.BeginFigure(from, false, false);
                g.LineTo(new Point(c.X + Math.Cos(a) * r * 1.35, c.Y + Math.Sin(a) * r * 1.35), true, true);
            }
        spikes.Freeze();
        dc.DrawGeometry(null, VirusSpike, spikes);
        double s = 1 + 0.06 * Math.Sin(time * 9);
        dc.DrawEllipse(GooGreen, InkPen, c, r * s, r / s);
        dc.DrawEllipse(Shine, null, new Point(c.X - r * 0.35, c.Y - r * 0.4), r * 0.3, r * 0.2);
        // Angry eyes: slanted brows over two dots.
        double e = r * 0.36;
        dc.DrawEllipse(Ink, null, new Point(c.X - e, c.Y + r * 0.08), r * 0.13, r * 0.16);
        dc.DrawEllipse(Ink, null, new Point(c.X + e, c.Y + r * 0.08), r * 0.13, r * 0.16);
        dc.DrawLine(ThinInk, new Point(c.X - e - r * 0.22, c.Y - r * 0.28), new Point(c.X - e + r * 0.2, c.Y - r * 0.12));
        dc.DrawLine(ThinInk, new Point(c.X + e + r * 0.22, c.Y - r * 0.28), new Point(c.X + e - r * 0.2, c.Y - r * 0.12));
    }
    private static readonly Pen VirusSpike = FrozenPen(Frozen(255, 0x3E, 0x9A, 0x2E), 2.4);

    // A blob of goo (r: its radius), stretched along its flight (stretch ≥ 1 along `angle`).
    public static void DrawGoo(DrawingContext dc, Point c, double r, double angle, double stretch)
    {
        dc.PushTransform(new RotateTransform(angle * 180 / Math.PI, c.X, c.Y));
        dc.DrawEllipse(GooGreen, InkPen, c, r * stretch, r / Math.Sqrt(stretch));
        dc.DrawEllipse(Shine, null, new Point(c.X + r * 0.3, c.Y - r * 0.35), r * 0.35, r * 0.2);
        dc.Pop();
    }

    // A slime-coloured blob (the bin's catch, a spat slime), squashing as it flies.
    public static void DrawBlob(DrawingContext dc, Point c, double r, Brush fill, double squash)
    {
        dc.DrawEllipse(fill, InkPen, c, r * squash, r / squash);
        dc.DrawEllipse(Shine, null, new Point(c.X - r * 0.3, c.Y - r * 0.35), r * 0.3, r * 0.2);
        dc.DrawEllipse(Ink, null, new Point(c.X - r * 0.28, c.Y + r * 0.05), r * 0.1, r * 0.13);
        dc.DrawEllipse(Ink, null, new Point(c.X + r * 0.28, c.Y + r * 0.05), r * 0.1, r * 0.13);
    }

    // The ad window (w × h around c): pink title bar with a ×, "-90%!!" sticker, a bar filling up (k).
    public static void DrawAd(DrawingContext dc, Rect r, string language, double k, double time)
    {
        dc.DrawRoundedRectangle(null, HaloPen, r, 6, 6);
        dc.DrawRoundedRectangle(AdYellow, InkPen, r, 6, 6);
        dc.DrawRoundedRectangle(AdBar, InkPen, new Rect(r.X, r.Y, r.Width, 18), 6, 6);
        var (title, _, th) = Word(PetText.Pick(language, "QUẢNG CÁO", "AD"), 10);
        dc.PushTransform(new TranslateTransform(r.X + 7, r.Y + 9 - th / 2));
        dc.DrawGeometry(Brushes.White, null, title);
        dc.Pop();
        var close = new Point(r.Right - 10, r.Y + 9);
        dc.DrawEllipse(Brushes.White, InkPen, close, 6.5, 6.5);
        dc.DrawLine(InkPen, new Point(close.X - 2.5, close.Y - 2.5), new Point(close.X + 2.5, close.Y + 2.5));
        dc.DrawLine(InkPen, new Point(close.X + 2.5, close.Y - 2.5), new Point(close.X - 2.5, close.Y + 2.5));
        var (sale, sw, sh) = Word("-90%!!", 16);
        double mid = r.Y + r.Height * 0.52;
        dc.PushTransform(new RotateTransform(-8 + Math.Sin(time * 6) * 2.4, r.X + r.Width / 2, mid));
        dc.PushTransform(new TranslateTransform(r.X + r.Width / 2 - sw / 2, mid - sh / 2));
        dc.DrawGeometry(null, HaloPen, sale);
        dc.DrawGeometry(BadgeRed, null, sale);
        dc.Pop();
        dc.Pop();
        var track = new Rect(r.X + 8, r.Bottom - 12, Math.Max(0, r.Width - 16), 6);
        dc.DrawRoundedRectangle(AdTrack, null, track, 3, 3);
        if (k > 0) dc.DrawRoundedRectangle(BadgeRed, null, new Rect(track.X, track.Y, track.Width * Math.Clamp(k, 0, 1), track.Height), 3, 3);
    }
    private static readonly Brush AdTrack = Frozen(255, 0xF3, 0xC3, 0x4E);

    // Malware's loading ring: a pale patch on the floor (radius either side of x) and, up where the
    // cat does not cover it, a spinner of eight dots on a white disc with a thin bar of the time left.
    // grow: 0..1 as it opens; left: share of its time left.
    public static void DrawLoading(DrawingContext dc, double x, double floor, double radius, Point spinner, double grow, double left, double time)
    {
        dc.PushTransform(new ScaleTransform(grow, grow, x, floor));
        dc.DrawEllipse(LoadingFloor, LoadingRim, new Point(x, floor - 3), radius, 8);
        dc.Pop();
        dc.PushTransform(new ScaleTransform(grow, grow, spinner.X, spinner.Y));
        dc.DrawEllipse(Brushes.White, InkPen, spinner, 15, 15);
        int lit = (int)(time * 10) % 8;
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4, shade = ((lit - i + 8) % 8) / 8.0;
            dc.DrawEllipse(LoadingDot, null, new Point(spinner.X + Math.Cos(a) * 9, spinner.Y + Math.Sin(a) * 9), 2.6 - shade * 1.4, 2.6 - shade * 1.4);
        }
        var track = new Rect(spinner.X - 16, spinner.Y + 19, 32, 4);
        dc.DrawRoundedRectangle(LoadingTrack, null, track, 2, 2);
        dc.DrawRoundedRectangle(LoadingDot, null, new Rect(track.X, track.Y, track.Width * Math.Clamp(left, 0, 1), track.Height), 2, 2);
        dc.Pop();
    }
    // Holding a loading ring's spinner: a gold arc fills around it (k: 0–1). Not held, a dashed ring
    // breathes around it, so it reads as something to press and hold.
    public static void DrawHoldRing(DrawingContext dc, Point spinner, double k, bool held, double time)
    {
        if (!held || k <= 0)
        {
            double r = 19 + Math.Sin(time * 4) * 1.5;
            dc.DrawEllipse(null, HoldHint, spinner, r, r);
            return;
        }
        k = Math.Clamp(k, 0, 1);
        dc.DrawEllipse(null, HoldTrack, spinner, 19, 19);
        if (k >= 0.999) { dc.DrawEllipse(null, HoldFill, spinner, 19, 19); return; }
        var arc = new StreamGeometry();
        using (var g = arc.Open())
        {
            double a = k * 2 * Math.PI;
            g.BeginFigure(new Point(spinner.X, spinner.Y - 19), false, false);
            g.ArcTo(new Point(spinner.X + Math.Sin(a) * 19, spinner.Y - Math.Cos(a) * 19), new Size(19, 19), 0, k > 0.5, SweepDirection.Clockwise, true, false);
        }
        arc.Freeze();
        dc.DrawGeometry(null, HoldFill, arc);
    }
    private static readonly Pen HoldTrack = FrozenPen(Frozen(150, 255, 255, 255), 4.5);
    private static readonly Pen HoldFill = FrozenPen(Frozen(255, 0xFF, 0xD2, 0x3F), 4.5);
    private static readonly Pen HoldHint = FrozenPen(Frozen(200, 0xFF, 0xD2, 0x3F), 2, new DashStyle(new[] { 2.0, 2.0 }, 0));

    // A padlock over an ad's × (c: the ×'s centre): the infected Tabs keeps the ads open.
    public static void DrawAdLock(DrawingContext dc, Point c)
    {
        dc.DrawEllipse(WindowGrey, InkPen, c, 7.5, 7.5);
        dc.PushTransform(new TranslateTransform(c.X, c.Y));
        dc.DrawGeometry(null, ThinInk, LockShackle);
        dc.DrawRoundedRectangle(Gold, ThinInk, new Rect(-4, -1, 8, 6), 1.2, 1.2);
        dc.Pop();
    }
    private static readonly Geometry LockShackle = Parse("M -2.6,-1 L -2.6,-3 A 2.6,2.6 0 0 1 2.6,-3 L 2.6,-1");

    // Malware's forced update: a grey window standing on the floor (r), titled "UPDATE", with its
    // percentage, a progress bar and a Later button (clicks left after it).
    public static Rect LaterButton(Rect r) => new(r.X + 7, r.Bottom - 24, r.Width - 14, 17);
    public static void DrawUpdateWall(DrawingContext dc, Rect r, string language, double progress, int clicksLeft)
    {
        dc.DrawRoundedRectangle(null, HaloPen, r, 5, 5);
        dc.DrawRoundedRectangle(WindowGrey, InkPen, r, 5, 5);
        dc.DrawRoundedRectangle(UpdateBar, InkPen, new Rect(r.X, r.Y, r.Width, 16), 5, 5);
        var (title, tw, th) = Word(PetText.Pick(language, "CẬP NHẬT", "UPDATE"), 8.5);
        dc.PushTransform(new TranslateTransform(r.X + r.Width / 2 - tw / 2, r.Y + 8 - th / 2));
        dc.DrawGeometry(Brushes.White, null, title);
        dc.Pop();
        // Two arrows chasing round: "updating".
        var c = new Point(r.X + r.Width / 2, r.Y + 32);
        dc.DrawEllipse(null, UpdateArrow, c, 9, 9);
        var (percent, pw, ph) = Word($"{Math.Clamp((int)(progress * 100), 0, 99)}%", 13);
        dc.PushTransform(new TranslateTransform(c.X - pw / 2, r.Y + 46));
        dc.DrawGeometry(Ink, null, percent);
        dc.Pop();
        var track = new Rect(r.X + 7, r.Y + 50 + ph, r.Width - 14, 5);
        dc.DrawRoundedRectangle(AdTrack, null, track, 2.5, 2.5);
        dc.DrawRoundedRectangle(UpdateFill, null, new Rect(track.X, track.Y, track.Width * Math.Clamp(progress, 0, 1), track.Height), 2.5, 2.5);
        var button = LaterButton(r);
        dc.DrawRoundedRectangle(Brushes.White, InkPen, button, 4, 4);
        var (later, lw, lh) = Word(PetText.Pick(language, "Nhắc sau", "Later") + (clicksLeft > 1 ? $" ×{clicksLeft}" : ""), 8.5);
        dc.PushTransform(new TranslateTransform(button.X + button.Width / 2 - lw / 2, button.Y + button.Height / 2 - lh / 2));
        dc.DrawGeometry(Ink, null, later);
        dc.Pop();
    }
    private static readonly Brush UpdateBar = Frozen(255, 0x4A, 0x8F, 0xD8), UpdateFill = Frozen(255, 0x4A, 0x8F, 0xD8);
    private static readonly Pen UpdateArrow = FrozenPen(Frozen(255, 0x4A, 0x8F, 0xD8), 2.6, new DashStyle(new[] { 2.2, 1.4 }, 0));
    private static readonly Brush LoadingFloor = Frozen(70, 0x5C, 0x9C, 0xF0), LoadingDot = Frozen(255, 0x3D, 0x7B, 0xD9), LoadingTrack = Frozen(160, 255, 255, 255);
    private static readonly Pen LoadingRim = FrozenPen(Frozen(200, 0x3D, 0x7B, 0xD9), 1.6, new DashStyle(new[] { 3.0, 2.5 }, 0));

    // A four-point twinkle (r: its size).
    public static void DrawTwinkle(DrawingContext dc, Point c, double r, Brush fill)
    {
        dc.PushTransform(new MatrixTransform(r / 10, 0, 0, r / 10, c.X, c.Y));
        dc.DrawGeometry(fill, ThinInk, Twinkle);
        dc.Pop();
    }
    private static readonly Geometry Twinkle = Parse("M 0,-10 Q 1.5,-1.5 10,0 Q 1.5,1.5 0,10 Q -1.5,1.5 -10,0 Q -1.5,-1.5 0,-10 Z");

    public static Brush Tint(string hex)
    {
        if (Colors.TryGetValue(hex, out var brush)) return brush;
        Brush made;
        try { made = Hex(hex); }
        catch (FormatException) { made = GooGreen; }
        return Colors[hex] = made;
    }
    private static readonly Dictionary<string, Brush> Colors = new();
}
