using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The desktop apps on the battle strip (BattleSim.Apps): helper icons floating over the floor, the
// ad popup standing over the slimes, the tab tornado around the cat and each app's effect. Every
// icon is the game's own drawing in its warm colours (a tab, a bell, a power sign, an undo arrow):
// made-up apps, never a real app's logo or colours.
public sealed partial class BattleFieldView
{
    // An app icon clicked (a helper, or a popup's ×).
    public event Action<int>? AppClicked;
    // The helper icon under the mouse: its description shows beside it.
    private int? hoverAppId;

    private const double IconSize = 34, IconRest = 100, DropSeconds = 0.45;

    // The icon's centre in this view: it drops in from the top of the strip, then bobs gently.
    private Point AppCentre(BattleApp app, double floor)
    {
        double rest = floor - IconRest, k = Math.Min(1, app.Age / DropSeconds);
        double y = rest - (1 - k) * (1 - k) * (rest - 14);
        if (k >= 1) y += Math.Sin(app.Age * 3 + app.Id) * 3;
        return new Point(app.X - OriginX, y);
    }

    private Rect PopupRect(BattleApp popup, double floor) =>
        new(popup.X - OriginX - BattleSim.PopupWidth / 2, floor - 2 - BattleSim.PopupHeight, BattleSim.PopupWidth, BattleSim.PopupHeight);
    private static Point CloseButton(Rect popup) => new(popup.Right - 10, popup.Top + 9);

    // A helper icon under the point, or a popup whose × is under it.
    public int? AppAt(Point point)
    {
        double floor = Floor;
        for (int i = sim.Apps.Count - 1; i >= 0; i--)
        {
            var app = sim.Apps[i];
            if (app.Hostile)
            {
                var close = CloseButton(PopupRect(app, floor));
                if ((point - close).Length <= 12) return app.Id;
                continue;
            }
            var c = AppCentre(app, floor);
            if (Math.Abs(point.X - c.X) <= IconSize / 2 + 4 && Math.Abs(point.Y - c.Y) <= IconSize / 2 + 4) return app.Id;
        }
        return null;
    }

    // On a popup (anywhere): clicks there never reach a slime behind it.
    public bool OnPopup(Point point)
    {
        for (int i = 0; i < sim.Apps.Count; i++)
            if (sim.Apps[i].Hostile && PopupRect(sim.Apps[i], Floor).Contains(point)) return true;
        return false;
    }

    private void HoverApp(Point point)
    {
        int? id = AppAt(point) is int found && sim.FindApp(found) is { Hostile: false } ? found : null;
        if (id == hoverAppId) return;
        hoverAppId = id;
        InvalidateVisual();
    }

    private static readonly Brush TabsBack = Frozen(255, 0xF2, 0x89, 0x6B);
    private static readonly Brush PingBack = Frozen(255, 0xF2, 0xB2, 0x3E);
    private static readonly Brush EndTaskBack = Frozen(255, 0x6E, 0x5E, 0xA8);
    private static readonly Brush UndoBack = Frozen(255, 0x3F, 0xB0, 0x9A);
    private static readonly Brush Paper = Frozen(255, 0xFF, 0xF6, 0xE6);
    private static readonly Brush BadgeRed = Frozen(255, 0xE5, 0x4B, 0x4B);
    private static readonly Brush IconShine = Frozen(90, 255, 255, 255);
    private static readonly Pen PaperPen = FrozenPen(Frozen(255, 0xFF, 0xF6, 0xE6), 3);
    private static readonly Brush AdYellow = Frozen(255, 0xFF, 0xE0, 0x6B);
    private static readonly Brush AdBar = Frozen(255, 0xE2, 0x5A, 0x7A);
    private static readonly Brush AdTrack = Frozen(255, 0xF3, 0xC3, 0x4E);
    private static readonly Brush HintBack = Frozen(235, 0x2E, 0x28, 0x40);
    private static readonly Pen HintRim = FrozenPen(Frozen(255, 0xF2, 0xC9, 0x8B), 1.5);
    private static readonly Brush WindowGrey = Frozen(255, 0xEE, 0xEA, 0xF4);
    private static readonly Brush WindowBar = Frozen(255, 0x8B, 0x7F, 0xB8);
    private static readonly Typeface HintFont = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private void DrawApps(DrawingContext dc, double floor, double time)
    {
        var rules = sim.AppRules;
        for (int i = 0; i < sim.Apps.Count; i++)
        {
            var app = sim.Apps[i];
            if (app.Hostile) { DrawPopup(dc, app, floor, time); continue; }
            var c = AppCentre(app, floor);
            // The last 3 s it blinks: soon gone.
            double left = app.Life - app.Age;
            dc.PushOpacity(left < 3 ? 0.45 + 0.55 * Math.Abs(Math.Cos(time * 8)) : 1);
            DrawIcon(dc, app.Kind, c);
            if (rules != null) DrawLabel(dc, rules.App(app.Kind).Name(English), c.X, c.Y + IconSize / 2 + 13);
            dc.Pop();
        }
        // The hovered helper's description, beside it (the strip is too low for above).
        if (rules != null && hoverAppId is int hovered && sim.FindApp(hovered) is { Hostile: false } shown)
            DrawHint(dc, rules.App(shown.Kind).Desc(English), AppCentre(shown, floor));
    }

    private static void DrawIcon(DrawingContext dc, BattleAppKind kind, Point c)
    {
        double h = IconSize / 2;
        var box = new Rect(c.X - h, c.Y - h, IconSize, IconSize);
        dc.DrawRoundedRectangle(null, Halo, box, 9, 9);
        dc.DrawRoundedRectangle(kind switch { BattleAppKind.Tabs => TabsBack, BattleAppKind.Ping => PingBack, BattleAppKind.EndTask => EndTaskBack, _ => UndoBack }, Ink, box, 9, 9);
        dc.DrawRoundedRectangle(IconShine, null, new Rect(box.X + 4, box.Y + 3, IconSize - 8, 8), 4, 4);
        switch (kind)
        {
            case BattleAppKind.Tabs:
                // Three page tabs stacked one behind another.
                for (int i = 2; i >= 0; i--) DrawTabShape(dc, c.X - 8 + i * 4, c.Y - 7 + i * 4, 1);
                break;
            case BattleAppKind.Ping:
                DrawBell(dc, new Point(c.X - 1, c.Y + 2), 1);
                break;
            case BattleAppKind.EndTask:
                // A power sign: an open ring and a bar through its gap.
                var ring = new StreamGeometry();
                using (var g = ring.Open())
                {
                    g.BeginFigure(new Point(c.X - 6, c.Y - 6), false, false);
                    g.ArcTo(new Point(c.X + 6, c.Y - 6), new Size(9, 9), 0, true, SweepDirection.Counterclockwise, true, false);
                }
                ring.Freeze();
                dc.DrawGeometry(null, PaperPen, ring);
                dc.DrawLine(PaperPen, new Point(c.X, c.Y - 11), new Point(c.X, c.Y - 1));
                break;
            default:
                DrawUndoArrow(dc, c, 9, PaperPen);
                break;
        }
    }

    // A page tab (top-left at x, y): a body with a raised tab on its top-left.
    private static void DrawTabShape(DrawingContext dc, double x, double y, double scale)
    {
        var shape = new StreamGeometry();
        using (var g = shape.Open())
        {
            g.BeginFigure(new Point(x, y + 4 * scale), true, true);
            g.LineTo(new Point(x + 2 * scale, y), true, true);
            g.LineTo(new Point(x + 8 * scale, y), true, true);
            g.LineTo(new Point(x + 10 * scale, y + 4 * scale), true, true);
            g.LineTo(new Point(x + 14 * scale, y + 4 * scale), true, true);
            g.LineTo(new Point(x + 14 * scale, y + 14 * scale), true, true);
            g.LineTo(new Point(x, y + 14 * scale), true, true);
        }
        shape.Freeze();
        dc.DrawGeometry(Paper, Ink, shape);
    }

    // A bell with a red "99+" badge on its shoulder.
    private static void DrawBell(DrawingContext dc, Point c, double scale)
    {
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, c.X, c.Y));
        dc.DrawGeometry(Paper, Ink, BellShape);
        dc.DrawEllipse(Paper, Ink, new Point(0, 8), 2.5, 2.5);
        dc.DrawRoundedRectangle(BadgeRed, Ink, new Rect(2, -14, 16, 10), 5, 5);
        var (badge, width, height) = TagShape("99+");
        dc.PushTransform(new MatrixTransform(0.7, 0, 0, 0.7, 10 - width * 0.35, -9 - height * 0.35));
        dc.DrawGeometry(Brushes.White, null, badge);
        dc.Pop();
        dc.Pop();
    }
    private static readonly Geometry BellShape = Parse("M -9,6 C -7,4 -7,1 -7,-3 C -7,-8 -4,-10 0,-10 C 4,-10 7,-8 7,-3 C 7,1 7,4 9,6 Z");

    // A curved arrow turning back (counter-clockwise), radius r around c.
    private static void DrawUndoArrow(DrawingContext dc, Point c, double r, Pen pen)
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
        dc.DrawLine(pen, tip, new Point(tip.X + 1, tip.Y + 6));
        dc.DrawLine(pen, tip, new Point(tip.X + 6, tip.Y - 2));
    }

    private static void DrawLabel(DrawingContext dc, string text, double x, double y)
    {
        var (shape, width, height) = TagShape(text);
        dc.PushTransform(new TranslateTransform(x - width / 2, y - height));
        dc.DrawGeometry(null, TextHalo, shape);
        dc.DrawGeometry(Brushes.White, null, shape);
        dc.Pop();
    }

    private void DrawHint(DrawingContext dc, string text, Point icon)
    {
        if (string.IsNullOrEmpty(text)) return;
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, HintFont, 11, Brushes.White, 1) { MaxTextWidth = 170 };
        double w = formatted.Width + 16, h = formatted.Height + 10;
        // To the right of the icon, or to its left near the strip's right end.
        double x = icon.X + IconSize / 2 + 8;
        if (x + w > ActualWidth - 4) x = icon.X - IconSize / 2 - 8 - w;
        double y = Math.Clamp(icon.Y - h / 2, 2, Math.Max(2, Floor - h - 2));
        dc.DrawRoundedRectangle(HintBack, HintRim, new Rect(x, y, w, h), 7, 7);
        dc.DrawText(formatted, new Point(x + 8, y + 5));
    }

    // The ad: a loud yellow window over the slimes, a pink title bar with a ×, a sale sticker and a
    // loading bar that fills toward the moment it lets a slime out.
    private void DrawPopup(DrawingContext dc, BattleApp popup, double floor, double time)
    {
        var r = PopupRect(popup, floor);
        double pop = Math.Min(1, popup.Age / 0.2), scale = 0.6 + 0.4 * pop + 0.04 * Math.Sin(time * 10) * (popup.Life - popup.Age < 2 ? 1 : 0);
        dc.PushTransform(new ScaleTransform(scale, scale, r.X + r.Width / 2, r.Bottom));
        dc.DrawRoundedRectangle(null, Halo, r, 6, 6);
        dc.DrawRoundedRectangle(AdYellow, Ink, r, 6, 6);
        var bar = new Rect(r.X, r.Y, r.Width, 18);
        dc.DrawRoundedRectangle(AdBar, Ink, bar, 6, 6);
        var (title, tw, th) = TagShape(English ? "AD" : "QUẢNG CÁO");
        dc.PushTransform(new TranslateTransform(r.X + 7, r.Y + 9 - th / 2));
        dc.DrawGeometry(Brushes.White, null, title);
        dc.Pop();
        var close = CloseButton(r);
        dc.DrawEllipse(Brushes.White, Ink, close, 6.5, 6.5);
        dc.DrawLine(Ink, new Point(close.X - 2.5, close.Y - 2.5), new Point(close.X + 2.5, close.Y + 2.5));
        dc.DrawLine(Ink, new Point(close.X + 2.5, close.Y - 2.5), new Point(close.X - 2.5, close.Y + 2.5));
        // The sticker: "-90%!!" in big red letters, wobbling a little.
        double wobble = Math.Sin(time * 6) * 0.08;
        var (sale, sw, sh) = NumberShape("-90%!!", false);
        dc.PushTransform(new RotateTransform(-8 + wobble * 30, r.X + r.Width / 2, r.Y + 34));
        dc.PushTransform(new TranslateTransform(r.X + r.Width / 2 - sw / 2, r.Y + 34 - sh / 2));
        dc.DrawGeometry(null, Halo, sale);
        dc.DrawGeometry(BadgeRed, null, sale);
        dc.Pop();
        dc.Pop();
        var track = new Rect(r.X + 8, r.Bottom - 12, r.Width - 16, 6);
        dc.DrawRoundedRectangle(AdTrack, null, track, 3, 3);
        double k = Math.Clamp(popup.Age / popup.Life, 0, 1);
        if (k > 0) dc.DrawRoundedRectangle(BadgeRed, null, new Rect(track.X, track.Y, track.Width * k, track.Height), 3, 3);
        dc.Pop();
    }

    // The tab tornado: three tabs circling the cat on the floor, fading out in its last half second.
    private void DrawTabs(DrawingContext dc, double catX, double floor, double time)
    {
        double radius = (sim.AppRules?.TabRadius ?? 70) * 0.85;
        dc.PushOpacity(Math.Min(1, sim.TabsRemaining / 0.5));
        for (int i = 0; i < 3; i++)
        {
            double a = time * 5 + i * Math.PI * 2 / 3;
            double x = catX + Math.Cos(a) * radius, y = floor - 40 + Math.Sin(a) * 14;
            dc.PushTransform(new RotateTransform(Math.Cos(a) * 25, x, y));
            DrawTabShape(dc, x - 10, y - 10, 1.4);
            dc.Pop();
        }
        dc.Pop();
    }

    // k: 0 → 1 over the effect's life (its opacity already fades with it).
    private void DrawAppEffect(DrawingContext dc, BattleEffect effect, double x, double y, double k)
    {
        switch (effect.Kind)
        {
            case BattleEffectKind.Bell:
                // Drops onto the enemy, then rings (swings side to side).
                double fall = Math.Min(1, k / 0.35);
                double swing = k > 0.35 ? Math.Sin((k - 0.35) * 40) * 18 * (1 - k) : 0;
                var at = new Point(x, y - 30 * (1 - fall * fall));
                dc.PushTransform(new RotateTransform(swing, at.X, at.Y - 10));
                DrawBell(dc, at, 1.3);
                dc.Pop();
                break;
            case BattleEffectKind.EndTask:
                // A small "not responding" window over the enemy.
                var (label, lw, lh) = TagShape(English ? "Not responding" : "Không phản hồi");
                double w = Math.Max(lw + 14, 64), h = 32;
                x = Math.Clamp(x, w / 2 + 2, Math.Max(w / 2 + 2, ActualWidth - w / 2 - 2)); // Whole on the strip, even at its ends.
                var r = new Rect(x - w / 2, y - h, w, h);
                dc.DrawRoundedRectangle(WindowGrey, Ink, r, 4, 4);
                dc.DrawRoundedRectangle(WindowBar, Ink, new Rect(r.X, r.Y, r.Width, 10), 4, 4);
                dc.PushTransform(new TranslateTransform(x - lw / 2, r.Y + 21 - lh / 2));
                dc.DrawGeometry(Eye, null, label);
                dc.Pop();
                break;
            default:
                // The undo arrow circling the cat backwards.
                double size = 22 + 12 * k;
                dc.PushTransform(new RotateTransform(-k * 300, x, y));
                DrawUndoArrow(dc, new Point(x, y), size, FrozenUndoPen);
                dc.Pop();
                break;
        }
    }
    private static readonly Pen FrozenUndoPen = FrozenPen(Frozen(255, 0x3F, 0xB0, 0x9A), 4);
}
