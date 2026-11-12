using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// Treasure hunt (docs/MINI-GAMES.md → M5), drawn in the cat's vector style: the rolled map (in the cat's mouth
// and as the little icon over its head), the faint paw prints, the chest and the paper that flies out of it.
public static class TreasureDrawing
{
    private static Brush B(byte a, byte r, byte g, byte b) => PetUi.Frozen(Color.FromArgb(a, r, g, b));
    public static readonly Brush Paper = PetUi.Hex("#FFF3E2"), PaperShade = PetUi.Hex("#EAD3B2"), Mark = PetUi.Hex("#D9534F"),
        Wood = PetUi.Hex("#B4815E"), WoodDark = PetUi.Hex("#8A5A3C"), Inside = PetUi.Hex("#5A3826"), Gold = PetUi.Game.Gold,
        Paw = PetUi.Hex("#7A5A48"), Shine = B(0xC0, 0xFF, 0xE6, 0x8A), Puff = B(0xB0, 0xFF, 0xFF, 0xFF);
    public static readonly Pen Outline = PetUi.FrozenPen(PetUi.ArtInk, 1.6), Thin = PetUi.FrozenPen(PetUi.ArtInk, 1.1),
        MarkPen = PetUi.FrozenPen(Mark, 2.2), GoldPen = PetUi.FrozenPen(B(0xFF, 0xF2, 0xB8, 0x3C), 1.8), PuffPen = PetUi.FrozenPen(B(0x60, 0x80, 0x80, 0x80), 1),
        PawEdge = PetUi.FrozenPen(B(0xD0, 0xFF, 0xF3, 0xE2), 1.4);

    // A rolled map, lying across, centred on (x, y), about 22×8 DIPs at scale 1 (a red X on its open flap).
    public static void Scroll(DrawingContext dc, double x, double y, double scale = 1)
    {
        dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, x, y));
        dc.DrawRoundedRectangle(Paper, Thin, new Rect(-11, -4, 22, 8), 3, 3);
        dc.DrawEllipse(PaperShade, Thin, new Point(-11, 0), 2.4, 4);
        dc.DrawEllipse(PaperShade, Thin, new Point(11, 0), 2.4, 4);
        dc.DrawLine(MarkPen, new Point(-2.6, -2.4), new Point(2.6, 2.4));
        dc.DrawLine(MarkPen, new Point(2.6, -2.4), new Point(-2.6, 2.4));
        dc.Pop();
    }

    // One paw print centred on (x, y): the pad and four toes.
    // A pale edge keeps it readable on a dark desktop too.
    public static void PawPrint(DrawingContext dc, double x, double y, double size, Brush fill, Pen? edge = null)
    {
        dc.DrawEllipse(fill, edge, new Point(x, y + size * 0.28), size * 0.42, size * 0.34);
        dc.DrawEllipse(fill, edge, new Point(x - size * 0.5, y - size * 0.1), size * 0.17, size * 0.21);
        dc.DrawEllipse(fill, edge, new Point(x - size * 0.19, y - size * 0.42), size * 0.17, size * 0.22);
        dc.DrawEllipse(fill, edge, new Point(x + size * 0.19, y - size * 0.42), size * 0.17, size * 0.22);
        dc.DrawEllipse(fill, edge, new Point(x + size * 0.5, y - size * 0.1), size * 0.17, size * 0.21);
    }

    // The chest standing on (x, floor); lid 0 shut, 1 wide open.
    public static void Chest(DrawingContext dc, double x, double floor, double lid)
    {
        double half = TreasureRules.ChestHalf, height = TreasureRules.ChestHeight, body = height * 0.62, top = floor - body;
        // Open, the lid stands up behind the box (seen from the front: its underside, taller as it opens).
        if (lid > 0.5)
        {
            double up = (height - body) * 1.6 * (lid - 0.5) * 2;
            dc.DrawRoundedRectangle(WoodDark, Outline, new Rect(x - half + 1, top - up - 2, 2 * half - 2, up + 4), 4, 4);
            dc.DrawRectangle(Inside, null, new Rect(x - half + 5, top - up + 3, 2 * half - 10, Math.Max(0, up - 4)));
        }
        if (lid > 0) dc.DrawRectangle(Inside, null, new Rect(x - half + 2, top - 2, 2 * half - 4, 5));
        dc.DrawRoundedRectangle(Wood, Outline, new Rect(x - half, top, 2 * half, body), 3, 3);
        dc.DrawLine(PetUi.FrozenPen(WoodDark, 3), new Point(x - half * 0.55, top + 1.5), new Point(x - half * 0.55, floor - 1.5));
        dc.DrawLine(PetUi.FrozenPen(WoodDark, 3), new Point(x + half * 0.55, top + 1.5), new Point(x + half * 0.55, floor - 1.5));
        dc.DrawRoundedRectangle(Gold, Thin, new Rect(x - 3.5, top + 2, 7, 8), 1.5, 1.5);
        if (lid <= 0.5) DrawLid(dc, x, top, half, height - body, lid);
        else dc.DrawEllipse(Shine, null, new Point(x, top + 1), half * 0.6, 2.5); // A warm glow from inside.
    }

    private static void DrawLid(DrawingContext dc, double x, double top, double half, double rise, double lid)
    {
        // Seen from the front: shut, a rounded top; opening, it tips back and shows less of itself.
        double show = Math.Cos(Math.PI * 0.55 * Math.Clamp(lid, 0, 1));
        double h = Math.Max(2, rise * Math.Abs(show)), up = show >= 0 ? -h : 0;
        var lidShape = new StreamGeometry();
        using (var g = lidShape.Open())
        {
            double baseY = show >= 0 ? top : top - rise * 0.15;
            g.BeginFigure(new Point(x - half, baseY), true, true);
            g.BezierTo(new Point(x - half, baseY + up - h * 0.3), new Point(x + half, baseY + up - h * 0.3), new Point(x + half, baseY), true, true);
        }
        lidShape.Freeze();
        dc.DrawGeometry(show >= 0 ? Wood : WoodDark, Outline, lidShape);
    }

    // A small puff of smoke (t 0–1) around (x, y).
    public static void SmokePuff(DrawingContext dc, double x, double y, double t)
    {
        if (t < 0 || t >= 1) return;
        dc.PushOpacity(1 - t);
        for (int i = 0; i < 5; i++)
        {
            double a = Math.PI * (0.1 + 0.2 * i), r = 10 + 18 * t;
            dc.DrawEllipse(Puff, PuffPen, new Point(x + Math.Cos(a) * r * 1.3, y - Math.Sin(a) * r * 0.7), 7 + 4 * t, 6 + 3 * t);
        }
        dc.Pop();
    }

    // Sparkles bursting from (x, y), t 0–1.
    public static void Sparkles(DrawingContext dc, double x, double y, double t)
    {
        if (t < 0 || t >= 1) return;
        dc.PushOpacity(1 - t);
        double r = 6 + 22 * Math.Sqrt(t);
        for (int i = 0; i < 7; i++)
        {
            double a = -Math.PI * (0.08 + i * 0.14);
            var from = new Point(x + Math.Cos(a) * r, y + Math.Sin(a) * r);
            dc.DrawLine(GoldPen, from, new Point(from.X + Math.Cos(a) * 4, from.Y + Math.Sin(a) * 4));
            if (i % 2 == 0) dc.DrawEllipse(Shine, null, from, 1.8, 1.8);
        }
        dc.Pop();
    }
}

// The little map over the cat's head while it holds one: click it to start the hunt. Its own small window.
public sealed class TreasureMapIcon : FrameworkElement
{
    public const double Size = 34;
    private static readonly Brush Hit = PetUi.Frozen(Color.FromArgb(0x02, 0xFF, 0xFF, 0xFF));
    private static readonly Brush Halo = PetUi.CardBack;
    private static readonly Pen HaloEdge = PetUi.FrozenPen(PetUi.ArtInk, 1.4);
    public double Fade = 1, Time;

    public TreasureMapIcon() { Width = Height = Size; }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawEllipse(Hit, null, new Point(Size / 2, Size / 2), Size / 2, Size / 2);
        dc.PushOpacity(Math.Clamp(Fade, 0, 1));
        double bob = Math.Sin(Time * 2.4) * 1.6;
        dc.DrawEllipse(Halo, HaloEdge, new Point(Size / 2, Size / 2 + bob), 15, 12);
        dc.PushTransform(new RotateTransform(-12, Size / 2, Size / 2 + bob));
        TreasureDrawing.Scroll(dc, Size / 2, Size / 2 + bob, 1.15);
        dc.Pop();
        dc.Pop();
    }
}

// The hunt's own window (under the cat's): the paw prints, then the chest, its puff, the sparkles and the
// paper. Only the prints not found yet and the chest take clicks (a faint disc behind each); the rest is fully
// transparent and clicks pass through. Coordinates: the screen's, less Origin.
public sealed class TreasureArt : FrameworkElement
{
    private static readonly Brush Hit = PetUi.Frozen(Color.FromArgb(0x02, 0xFF, 0xFF, 0xFF));
    private static readonly Brush FoundGlow = PetUi.Frozen(Color.FromArgb(0xE8, 0xFF, 0xE9, 0xB0));
    private static readonly Pen FoundEdge = PetUi.FrozenPen(PetUi.Game.Gold, 1.6);
    private readonly TreasureHunt hunt;
    public Point Origin;
    public Func<int, double> PrintGlow = _ => TreasureRules.Faint;
    public double? ChestX, ChestFloor;
    public double Lid, Puff = -1, Spark = -1, PaperRise = -1;
    public bool ChestClickable;

    public TreasureArt(TreasureHunt hunt) { this.hunt = hunt; }

    public void Size(double width, double height) { Width = width; Height = height; }

    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new TranslateTransform(-Origin.X, -Origin.Y));
        for (int i = 0; i < hunt.Spots.Count; i++)
        {
            var spot = hunt.Spots[i];
            bool found = hunt.Found(i);
            if (!found) dc.DrawEllipse(Hit, null, new Point(spot.X, spot.Y), TreasureRules.PrintRadius + 6, TreasureRules.PrintRadius + 6);
            if (found) dc.DrawEllipse(FoundGlow, FoundEdge, new Point(spot.X, spot.Y), TreasureRules.PrintRadius + 4, TreasureRules.PrintRadius + 4);
            dc.PushOpacity(Math.Clamp(PrintGlow(i), 0, 1));
            // Alternate prints lean a little, like steps.
            dc.PushTransform(new RotateTransform(i % 2 == 0 ? -14 : 12, spot.X, spot.Y));
            TreasureDrawing.PawPrint(dc, spot.X, spot.Y, 22, TreasureDrawing.Paw, found ? null : TreasureDrawing.PawEdge);
            dc.Pop();
            dc.Pop();
        }
        if (ChestX is double x && ChestFloor is double floor)
        {
            if (ChestClickable) dc.DrawRectangle(Hit, null, new Rect(x - TreasureRules.ChestHalf - 6, floor - TreasureRules.ChestHeight - 6, 2 * TreasureRules.ChestHalf + 12, TreasureRules.ChestHeight + 6));
            double appear = Puff < 0 ? 1 : Math.Clamp(Puff / 0.4, 0, 1);
            dc.PushOpacity(appear);
            TreasureDrawing.Chest(dc, x, floor, Lid);
            dc.Pop();
            TreasureDrawing.SmokePuff(dc, x, floor - 10, Puff < 0 ? -1 : Puff);
            double top = floor - TreasureRules.ChestHeight * 0.62;
            TreasureDrawing.Sparkles(dc, x, top - 4, Spark);
            if (PaperRise is >= 0 and < 1)
            {
                double rise = 60 * Math.Sqrt(PaperRise);
                dc.PushOpacity(1 - PaperRise * PaperRise);
                dc.PushTransform(new RotateTransform(-10 + 20 * PaperRise, x, top - rise));
                dc.DrawRoundedRectangle(TreasureDrawing.Paper, TreasureDrawing.Thin, new Rect(x - 10, top - rise - 7, 20, 14), 2, 2);
                dc.DrawLine(TreasureDrawing.Thin, new Point(x - 6, top - rise - 2), new Point(x + 6, top - rise - 2));
                dc.DrawLine(TreasureDrawing.Thin, new Point(x - 6, top - rise + 2), new Point(x + 4, top - rise + 2));
                dc.Pop(); dc.Pop();
            }
        }
        dc.Pop();
    }
}
