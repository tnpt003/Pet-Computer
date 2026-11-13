using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

public enum FarmDecorPlace { FieldBack, FieldFront, Hearth, Pond }

public readonly record struct FarmLandPose(double Lift, double ShakeX, double Tilt, double Squash);

// What the stall sells (docs/FARM-GAME.md, finishing milestone B): still pictures that stay inside
// their land and move with it. Each is drawn on a layer of its own that takes no clicks, because
// anything drawn on the land's own art would become part of what a click lands on.
public sealed partial class PetFarmArt
{
    private const double HearthBase = Floor - 16, PondBase = Floor - 15;

    private static readonly Brush Bamboo = PetUi.Hex("#BCC873"), BambooDeep = PetUi.Hex("#93A552");
    private static readonly Brush LanternRed = PetUi.Hex("#E2663F"), LanternGlow = PetUi.Hex("#F9C46A"), Thatch = PetUi.Hex("#DDBB6E"), ThatchDeep = PetUi.Hex("#B98F49");
    private static readonly Brush PetalPink = PetUi.Hex("#F29DB4"), PetalWhite = PetUi.Hex("#FFF4E4"), PetalGold = PetUi.Hex("#F6C85B"), PadGreen = PetUi.Hex("#7DBB6F");
    private static readonly Pen LanternRib = PetUi.FrozenPen(PetUi.Hex("#B8482B"), 1.2);
    private static readonly Pen TasselPen = PetUi.FrozenPen(PetalGold, 1.6);
    private static readonly Pen ThatchLine = PetUi.FrozenPen(ThatchDeep, 1.3);
    private static readonly Pen RailPen = PetUi.FrozenPen(PetUi.Hex("#B4815E"), 2.4);
    private static readonly Brush LeafDark = PetUi.Hex("#4E9256");

    private static readonly Geometry ThatchRoof = Parse("M-48,-60 L-34,-80 L34,-80 L48,-60 Q40,-53 32,-60 Q24,-53 16,-60 Q8,-53 0,-60 Q-8,-53 -16,-60 Q-24,-53 -32,-60 Q-40,-53 -48,-60 Z");
    private static readonly Geometry ThatchStraw = Parse("M-28,-78 L-36,-62 M-14,-78 L-18,-61 M0,-78 L0,-61 M14,-78 L18,-61 M28,-78 L36,-62");
    private static readonly Geometry BridgeDeck = Parse("M-42,-2 Q0,-38 42,-2 L36,3 Q0,-31 -36,3 Z");
    private static readonly Geometry BridgeRail = Parse("M-40,-12 Q0,-46 40,-12");
    private static readonly Geometry LilyPetals = Parse("M-6,0 C-6,-4 -3,-5 -3,-5 C-3,-7 -1,-8 0,-9 C1,-8 3,-7 3,-5 C3,-5 6,-4 6,0 C3,1.5 -3,1.5 -6,0 Z");

    // One of the land's layers: behind its art or in front of it, in the land's own place and pose.
    public sealed class DecorLayer : FrameworkElement
    {
        private readonly FarmDecorPlace place;
        private readonly double mid;
        private FarmLandPose pose;

        // Which decorations are out, one bit each (PetFarm.DecorMask).
        public int Shown { get; private set; }
        // The pinwheel's turn in degrees, and a puff of wind going past it from 0 to 1 (-1: none).
        public double PinwheelTurn { get; private set; }
        public double Gust { get; private set; } = -1;

        public DecorLayer(FarmDecorPlace place, double artWidth, double mid)
        {
            this.place = place;
            this.mid = mid;
            Width = artWidth;
            Height = ArtHeight;
            IsHitTestVisible = false;
        }

        // Draws again only when something it depends on has changed.
        public void Show(FarmLandPose newPose, int shown)
        {
            if (newPose == pose && shown == Shown) return;
            pose = newPose;
            Shown = shown;
            InvalidateVisual();
        }

        // The wind on the pinwheel: drawn again only when it moved.
        public void Blow(double turn, double gust)
        {
            if (turn == PinwheelTurn && gust == Gust) return;
            PinwheelTurn = turn;
            Gust = gust;
            if (Has(FarmDecor.Pinwheel)) InvalidateVisual();
        }

        private bool Has(FarmDecor decor) => (Shown & (1 << (int)decor)) != 0;

        protected override void OnRender(DrawingContext dc)
        {
            if (Shown == 0) return;
            var land = Matrix.Identity;
            land.ScaleAt(1 + 0.04 * pose.Squash, 1 - 0.12 * pose.Squash, mid, Floor);
            land.RotateAt(pose.Tilt, mid, Floor);
            land.Translate(pose.ShakeX, -Math.Max(0, pose.Lift));
            dc.PushTransform(new MatrixTransform(land));
            switch (place)
            {
                case FarmDecorPlace.FieldBack:
                    if (Has(FarmDecor.BambooFence)) At(dc, mid, SoilTop + 4, () => DrawBambooFence(dc, PlotWidth - 12));
                    if (Has(FarmDecor.Lantern)) At(dc, Side + 6, SoilTop + 1, () => DrawLantern(dc));
                    if (Has(FarmDecor.Pinwheel)) At(dc, Side + PlotWidth - 34, SoilTop + 1, () => { DrawPinwheel(dc, PinwheelTurn); if (Gust >= 0) DrawGust(dc, Gust); });
                    break;
                case FarmDecorPlace.FieldFront:
                    if (Has(FarmDecor.FlowerBed)) At(dc, mid, Floor - 1, () => { for (int i = -5; i <= 5; i++) { dc.PushTransform(new TranslateTransform(i * 30, 0)); DrawFlowers(dc, i); dc.Pop(); } });
                    break;
                case FarmDecorPlace.Hearth:
                    if (Has(FarmDecor.Awning)) At(dc, mid, HearthBase, () => DrawAwning(dc));
                    if (Has(FarmDecor.TeaPot)) At(dc, mid + 38, HearthBase + 2, () => DrawTeaPot(dc));
                    break;
                default:
                    if (Has(FarmDecor.WoodBridge)) At(dc, mid, PondBase, () => DrawBridge(dc));
                    if (Has(FarmDecor.WaterLily)) At(dc, mid, PondBase, () => DrawLilies(dc));
                    if (Has(FarmDecor.StoneFrog)) At(dc, mid - 44, PondBase + 1, () => DrawStoneFrog(dc));
                    break;
            }
            dc.Pop();
        }

        private static void At(DrawingContext dc, double x, double y, Action draw)
        {
            dc.PushTransform(new TranslateTransform(x, y));
            draw();
            dc.Pop();
        }
    }

    private static readonly Brush PinRed = PetUi.Hex("#E2663F"), PinBlue = PetUi.Hex("#6FB3D9"), FrogStone = PetUi.Hex("#8FB58A"), PotCream = PetUi.Hex("#F3E6D2"), PotBlue = PetUi.Hex("#6C93B8");
    private static readonly Geometry PinwheelBlade = Parse("M0,0 L3,-14 L12,-11 Z");

    // The neighbour's gifts, still pictures like the rest.
    private static void DrawPinwheel(DrawingContext dc, double turn = 0)
    {
        dc.DrawRoundedRectangle(Stake, InkThin, new Rect(-1.8, -46, 3.6, 46), 1.5, 1.5);
        dc.PushTransform(new TranslateTransform(0, -48));
        for (int i = 0; i < 4; i++)
        {
            dc.PushTransform(new RotateTransform(i * 90 + 15 + turn));
            dc.DrawGeometry(i % 2 == 0 ? PinRed : PinBlue, InkThin, PinwheelBlade);
            dc.Pop();
        }
        dc.DrawEllipse(PetalGold, InkThin, new Point(0, 0), 2.6, 2.6);
        dc.Pop();
    }

    private static readonly Pen[] GustPens = { PetUi.FrozenPen(PetUi.Hex("#E8F2F4"), 1.6), PetUi.FrozenPen(PetUi.Hex("#7FA6B3"), 0.8) };

    // Three wavy streaks of wind going past the pinwheel, left to right, fading in and out.
    private static void DrawGust(DrawingContext dc, double age)
    {
        double fade = Math.Sin(Math.Clamp(age, 0, 1) * Math.PI);
        dc.PushOpacity(0.85 * fade);
        foreach (var (y, lag, length) in new[] { (-56.0, 0.0, 34.0), (-44.0, 0.12, 26.0), (-31.0, 0.05, 30.0) })
        {
            double x = -64 + (age - lag) * 110;
            var figure = new StreamGeometry();
            using (var g = figure.Open())
            {
                g.BeginFigure(new Point(x, y), false, false);
                g.BezierTo(new Point(x + length * 0.3, y - 4), new Point(x + length * 0.6, y + 4), new Point(x + length, y), true, true);
            }
            figure.Freeze();
            foreach (var pen in GustPens) dc.DrawGeometry(null, pen, figure);
        }
        dc.Pop();
    }

    private static void DrawStoneFrog(DrawingContext dc)
    {
        dc.DrawEllipse(FrogStone, InkThin, new Point(0, -7), 12, 8);
        foreach (int side in new[] { -1, 1 })
        {
            dc.DrawEllipse(FrogStone, InkThin, new Point(side * 6, -15), 4, 4);
            dc.DrawEllipse(PetUi.ArtInk, null, new Point(side * 6, -15.5), 1.3, 1.7);
        }
        dc.DrawLine(InkThin, new Point(-6, -6), new Point(6, -6));
    }

    private static void DrawTeaPot(DrawingContext dc)
    {
        dc.DrawLine(InkThin, new Point(10, -12), new Point(18, -18));
        dc.DrawEllipse(null, InkThin, new Point(-13, -9), 4.5, 5.5);
        dc.DrawEllipse(PotCream, InkThin, new Point(0, -9), 12, 9.5);
        dc.DrawLine(RailPen, new Point(-8, -8), new Point(8, -8));
        dc.DrawEllipse(PotBlue, InkThin, new Point(0, -18.5), 6, 2.2);
        dc.DrawEllipse(PotBlue, InkThin, new Point(0, -21), 1.8, 1.8);
    }

    // Each drawing stands on (0,0).
    private static void DrawBambooFence(DrawingContext dc, double width)
    {
        foreach (double y in new[] { -28.0, -15.0 }) dc.DrawRoundedRectangle(BambooDeep, InkThin, new Rect(-width / 2, y, width, 4), 2, 2);
        int posts = Math.Max(2, (int)(width / 26) + 1);
        for (int i = 0; i < posts; i++)
        {
            double x = -width / 2 + 3 + i * (width - 6) / (posts - 1), height = 32 + i % 3 * 3;
            dc.DrawRoundedRectangle(Bamboo, InkThin, new Rect(x - 3, -height, 6, height), 2, 2);
            foreach (double node in new[] { 0.36, 0.7 }) dc.DrawLine(InkThin, new Point(x - 3, -height * node), new Point(x + 3, -height * node));
        }
    }

    private static void DrawFlowers(DrawingContext dc, int which)
    {
        var (first, second) = Math.Abs(which) % 2 == 0 ? (PetalPink, PetalGold) : (PetalWhite, PetalPink);
        dc.DrawEllipse(Leaf, InkThin, new Point(-5, -2), 6, 2.6);
        dc.DrawEllipse(LeafDark, InkThin, new Point(6, -2), 5, 2.3);
        DrawFlower(dc, -6, -7, first);
        DrawFlower(dc, 6, -6, second);
        DrawFlower(dc, 0, -11, second == PetalGold ? PetalWhite : PetalGold);
    }

    private static void DrawFlower(DrawingContext dc, double x, double y, Brush petal)
    {
        for (int i = 0; i < 5; i++)
        {
            double angle = i * Math.PI * 2 / 5 - Math.PI / 2;
            dc.DrawEllipse(petal, InkThin, new Point(x + Math.Cos(angle) * 2.8, y + Math.Sin(angle) * 2.8), 2.2, 2.2);
        }
        dc.DrawEllipse(Orange, null, new Point(x, y), 1.6, 1.6);
    }

    private static void DrawLantern(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Stake, InkThin, new Rect(-2, -66, 4, 66), 1.5, 1.5);
        dc.DrawRoundedRectangle(Stake, InkThin, new Rect(-2, -66, 15, 3.5), 1.5, 1.5);
        DrawPaperLantern(dc);
    }

    private static void DrawPaperLantern(DrawingContext dc)
    {
        dc.DrawLine(InkThin, new Point(9, -62.5), new Point(9, -59));
        dc.DrawEllipse(LanternRed, InkThin, new Point(9, -48), 8, 10.5);
        dc.DrawEllipse(null, LanternRib, new Point(9, -48), 3.6, 10.5);
        dc.DrawEllipse(LanternGlow, null, new Point(6.5, -50), 2, 3.6);
        foreach (double y in new[] { -60.0, -39.0 }) dc.DrawRoundedRectangle(PetUi.ArtInk, null, new Rect(4.5, y, 9, 2.6), 1, 1);
        dc.DrawLine(TasselPen, new Point(9, -36.5), new Point(9, -30));
    }

    private static void DrawAwning(DrawingContext dc)
    {
        foreach (double x in new[] { -41.0, 41.0 }) dc.DrawRoundedRectangle(Stake, InkThin, new Rect(x - 3, -66, 6, 66), 2, 2);
        dc.DrawGeometry(Thatch, Ink, ThatchRoof);
        dc.DrawGeometry(null, ThatchLine, ThatchStraw);
        dc.DrawRoundedRectangle(ThatchDeep, InkThin, new Rect(-36, -83, 72, 5), 2.5, 2.5);
    }

    private static void DrawLilies(DrawingContext dc)
    {
        DrawLily(dc, -31, 1, true);
        DrawLily(dc, 1, 0, false);
        DrawLily(dc, 31, 1, true);
    }

    private static void DrawLily(DrawingContext dc, double x, double y, bool bloom)
    {
        dc.DrawEllipse(PadGreen, InkThin, new Point(x, y), 8.5, 2.8);
        dc.DrawLine(InkThin, new Point(x, y), new Point(x + 6, y + 1.8));
        if (!bloom) return;
        dc.PushTransform(new TranslateTransform(x, y - 1));
        dc.DrawGeometry(PetalPink, InkThin, LilyPetals);
        dc.DrawEllipse(PetalGold, null, new Point(0, -3), 1.7, 1.2);
        dc.Pop();
    }

    // A high arch: the float on the water stays in sight under it.
    private static void DrawBridge(DrawingContext dc)
    {
        foreach (int side in new[] { -1, 1 }) dc.DrawRoundedRectangle(StoneDeep, InkThin, new Rect(side * 38 - 4, -2, 8, 7), 2, 2);
        foreach (var (x, deck) in new[] { (-28.0, -12.0), (0.0, -20.0), (28.0, -12.0) }) dc.DrawLine(RailPen, new Point(x, deck), new Point(x, deck - 10));
        dc.DrawGeometry(null, RailPen, BridgeRail);
        dc.DrawGeometry(Stake, Ink, BridgeDeck);
        foreach (var (x, y) in new[] { (-21.0, -15.5), (-7.0, -19.5), (7.0, -19.5), (21.0, -15.5) }) dc.DrawLine(InkThin, new Point(x, y), new Point(x, y + 4));
    }

    // In a shop's row: the same drawings, or a short piece of the long ones.
    // Centred on what is really drawn (measured once per decoration), so each sits in the middle of its square.
    internal static void DrawDecorIcon(DrawingContext dc, FarmDecor decor, double size)
    {
        var bounds = DecorIconBounds(decor);
        double scale = Math.Min(size / bounds.Width, size / bounds.Height);
        dc.PushTransform(new TranslateTransform(size / 2, size / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.PushTransform(new TranslateTransform(-(bounds.Left + bounds.Width / 2), -(bounds.Top + bounds.Height / 2)));
        DrawDecorShape(dc, decor);
        dc.Pop(); dc.Pop(); dc.Pop();
    }

    private static readonly System.Collections.Generic.Dictionary<FarmDecor, Rect> DecorBounds = new();

    internal static Rect DecorIconBounds(FarmDecor decor)
    {
        if (DecorBounds.TryGetValue(decor, out var known)) return known;
        var drawing = new DrawingGroup();
        using (var context = drawing.Open()) DrawDecorShape(context, decor);
        var bounds = drawing.Bounds;
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) bounds = new Rect(-15, -15, 30, 30);
        return DecorBounds[decor] = bounds;
    }

    private static void DrawDecorShape(DrawingContext dc, FarmDecor decor)
    {
        switch (decor)
        {
            case FarmDecor.BambooFence: DrawBambooFence(dc, 48); break;
            case FarmDecor.FlowerBed: DrawFlowers(dc, 0); break;
            case FarmDecor.WaterLily: DrawLily(dc, 0, 0, true); break;
            case FarmDecor.Lantern: DrawPaperLantern(dc); break;
            case FarmDecor.WoodBridge: DrawBridge(dc); break;
            case FarmDecor.Pinwheel: DrawPinwheel(dc); break;
            case FarmDecor.StoneFrog: DrawStoneFrog(dc); break;
            case FarmDecor.TeaPot: DrawTeaPot(dc); break;
            default: DrawAwning(dc); break;
        }
    }
}

public sealed class FarmDecorIcon : FrameworkElement
{
    private readonly FarmDecor decor;
    private readonly double size;

    public FarmDecorIcon(FarmDecor decor, double size = 30)
    {
        this.decor = decor;
        this.size = size;
        Width = Height = size;
    }

    protected override void OnRender(DrawingContext dc) => PetFarmArt.DrawDecorIcon(dc, decor, size);
}
