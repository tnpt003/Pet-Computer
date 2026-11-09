using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// One tile as the art draws it (PetWindow.Farm.cs fills these from PetFarm).
public readonly record struct FarmTileView(FarmStage Stage, FarmCrop? Crop, bool Wet, bool NeedsWater, double Progress, bool Locked = false);

public enum FarmEffect { None, Till, Plant, Water, Harvest, Grow }

// The little field on the desktop (1.5.1 M3), drawn by code: a bank of earth with a grass lip and
// six beds, side on, standing on the floor. The window never moves up and down: the art lifts the
// plot itself, so its shadow can stay on the floor. Redrawn only when something changes.
// Only the plot and what grows on it take clicks; the rest of the window is click-through.
public sealed partial class PetFarmArt : FrameworkElement
{
    public const double TileWidth = 54, Pad = 10, Side = 40, SoilHeight = 28, PlantRoom = 72, LiftRoom = 54, BottomRoom = 6;
    public const double PlotWidth = TileWidth * PetFarm.TileCount + Pad * 2, ArtWidth = PlotWidth + Side * 2;
    // The hearth and the pond are land of their own (PetFarmArt.Land.cs).
    public const double HearthLandWidth = 96, PondLandWidth = 150;
    private const double PlotMid = Side + PlotWidth / 2;
    public const double ArtHeight = SoilHeight + PlantRoom + LiftRoom + BottomRoom, Floor = ArtHeight - BottomRoom, SoilTop = Floor - SoilHeight;
    public const double BasketX = Side + 9;
    private const double GroundY = SoilTop + 8, BadgeGap = 13, FenceHeight = 27, ScarecrowX = Side + PlotWidth - 4;
    private const int BurstCrumbs = 16, ShedCrumbs = 24, LandingCrumbs = 36;

    private static readonly Brush Hit = PetUi.Frozen(Color.FromArgb(1, 0, 0, 0));
    private static readonly Brush Earth = PetUi.Hex("#A9744F"), EarthDeep = PetUi.Hex("#8A5B3D"), Moss = PetUi.Hex("#A5BE78");
    private static readonly Brush Tilled = PetUi.Hex("#7C5136"), Wet = PetUi.Hex("#573825"), WetShine = PetUi.Frozen(Color.FromArgb(0x60, 0xBF, 0xE3, 0xFF));
    private static readonly Brush Leaf = PetUi.Hex("#6FB36A"), LeafDeep = PetUi.Hex("#4E9256"), Stake = PetUi.Hex("#B4815E");
    private static readonly Brush RadishRed = PetUi.Hex("#E2587A"), RadishTip = PetUi.Hex("#FFF3EC"), Orange = PetUi.Hex("#F0913A"), TomatoRed = PetUi.Hex("#E2503F");
    private static readonly Brush Pumpkin = PetUi.Hex("#EE9A3C"), PumpkinRib = PetUi.Hex("#D97E2A"), Water = PetUi.Hex("#6FC0EE"), Seed = PetUi.Hex("#E9D2A6");
    private static readonly Brush Shadow = PetUi.Frozen(Color.FromArgb(0x58, 0x2A, 0x1F, 0x16));
    private static readonly Brush BarBack = PetUi.Frozen(Color.FromArgb(0xC0, 0x2A, 0x1F, 0x16));
    private static readonly Brush Dust = PetUi.Frozen(Color.FromArgb(0xB0, 0xD9, 0xC2, 0xA2));

    private static readonly Pen Ink = PetUi.FrozenPen(PetUi.ArtInk, 2.4);
    private static readonly Pen InkThin = PetUi.FrozenPen(PetUi.ArtInk, 1.5);
    private static readonly Pen Furrow = PetUi.FrozenPen(PetUi.Frozen(Color.FromArgb(0xA0, 0x3A, 0x2C, 0x1F)), 1.6);
    private static readonly Pen Tuft = PetUi.FrozenPen(LeafDeepBrush(), 1.6);
    private static readonly Pen StemPen = PetUi.FrozenPen(LeafDeepBrush(), 2.4);
    private static readonly Pen StakePen = PetUi.FrozenPen(Stake, 3);
    private static readonly Pen Hover = PetUi.FrozenPen(PetUi.Game.Gold, 2.2);
    private static readonly Pen Rib = PetUi.FrozenPen(PumpkinRib, 1.6);

    private static readonly Geometry Lip = Parse("M0,9 C0,3 5,0 11,0 L333,0 C339,0 344,3 344,9 C338,13 333,8 327,11 C321,14 315,9 309,12 C302,14 297,9 290,12 C283,14 278,9 271,12 C264,14 258,9 252,12 C245,14 239,9 232,12 C225,14 220,9 213,12 C206,14 200,9 193,12 C186,14 181,9 174,12 C167,14 161,9 154,12 C147,14 142,9 135,12 C128,14 122,9 115,12 C108,14 103,9 96,12 C89,14 83,9 76,12 C69,14 64,9 57,12 C50,14 45,9 38,12 C31,14 25,9 18,12 C12,14 6,9 0,9 Z");
    private static readonly Geometry Underside = Parse("M0,0 L344,0 C330,14 300,10 286,20 C262,31 238,18 214,28 C190,38 166,22 142,30 C116,37 96,20 72,24 C46,27 16,16 0,0 Z");
    private static readonly Geometry Mound = Parse("M-10,2 C-7,-5 7,-5 10,2 Z");
    private static readonly Geometry SproutLeaves = Parse("M0,-11 C-3,-18 -10,-21 -15,-18 C-13,-12 -5,-9 0,-11 Z M0,-11 C3,-18 10,-21 15,-18 C13,-12 5,-9 0,-11 Z");
    private static readonly Geometry Blades = Parse("M0,-2 C-9,-11 -15,-22 -13,-31 C-5,-26 -1,-14 0,-2 Z M0,-2 C9,-11 15,-22 13,-31 C5,-26 1,-14 0,-2 Z M0,-2 C-5,-15 -5,-29 0,-38 C5,-29 5,-15 0,-2 Z");
    private static readonly Geometry TallLeaves = Parse("M0,-13 C-4,-20 -13,-23 -18,-19 C-15,-13 -7,-11 0,-13 Z M0,-13 C4,-20 13,-23 18,-19 C15,-13 7,-11 0,-13 Z M0,-28 C-3,-35 -10,-39 -15,-36 C-13,-29 -6,-26 0,-28 Z M0,-28 C3,-35 10,-39 15,-36 C13,-29 6,-26 0,-28 Z");
    private static readonly Geometry Vine = Parse("M-4,-1 C-22,-3 -23,-23 -8,-26 C-3,-34 13,-33 14,-23 C24,-20 22,-4 7,-2 C3,-1 -1,-1 -4,-1 Z");
    private static readonly Geometry VineVein = Parse("M-2,-3 C-1,-10 1,-17 3,-25 M0,-11 C-5,-13 -10,-16 -13,-19 M1,-13 C6,-14 11,-16 14,-18");
    private static readonly Geometry RadishBulb = Parse("M-11,-14 C-13,-5 -7,1 0,5 C7,1 13,-5 11,-14 C6,-20 -6,-20 -11,-14 Z");
    private static readonly Geometry RadishEnd = Parse("M-5,-1 C-3,1 -1,3 0,5 C1,3 3,1 5,-1 C2,1 -2,1 -5,-1 Z");
    private static readonly Geometry CarrotRoot = Parse("M-11,-16 C-7,-20 7,-20 11,-16 C10,-7 4,1 0,6 C-4,1 -10,-7 -11,-16 Z");
    private static readonly Geometry CarrotLines = Parse("M-7,-10 L-1,-9 M7,-6 L1,-5 M-4,-3 L0,-2");
    private static readonly Geometry WaterDrop = Parse("M0,-8 C3,-4 5.5,-1.5 5.5,1.8 C5.5,5 3,7 0,7 C-3,7 -5.5,5 -5.5,1.8 C-5.5,-1.5 -3,-4 0,-8 Z");
    private static readonly Geometry Star = Parse("M0,-7 L1.9,-1.9 L7,0 L1.9,1.9 L0,7 L-1.9,1.9 L-7,0 L-1.9,-1.9 Z");
    private static readonly Geometry Calyx = Parse("M-4,-1 L0,-4 L4,-1 L1.5,-1 L0,1 L-1.5,-1 Z");

    private static readonly Geometry BowlFront = Parse("M-16,-13 C-12,-9 12,-9 16,-13 C16,-4 10,0 5,0 L-5,0 C-10,0 -16,-4 -16,-13 Z");
    private static readonly Geometry PieTin = Parse("M-16,-9 L16,-9 L12,0 L-12,0 Z");
    private static readonly Brush PinkBroth = PetUi.Hex("#F2A6B7"), StewBroth = PetUi.Hex("#D98A3F"), Puff = PetUi.Frozen(Color.FromArgb(0xC8, 0xFF, 0xFF, 0xFF));
    private static readonly Pen PuffEdge = PetUi.FrozenPen(PetUi.Frozen(Color.FromArgb(0x70, 0x2A, 0x1F, 0x16)), 1);

    // Watermelon: green with dark stripes outside, red with dark seeds inside; the dish keeps cold with ice.
    private static readonly Brush MelonRind = PetUi.Hex("#63B168"), MelonStripe = PetUi.Hex("#2F7A4B"), MelonRed = PetUi.Hex("#EF5F73"), Ice = PetUi.Hex("#D3EDF8");
    private static readonly Geometry MelonStripes = Parse("M0,-32 C-5,-26 -2,-21 -4,-16 C-2,-11 -5,-5 0,0 C5,-5 2,-11 4,-16 C2,-21 5,-26 0,-32 Z M-3,-31.6 C-12,-28 -11,-21 -15,-16 C-11,-10 -12,-4 -3,-0.4 C-9,-6 -7,-11 -10,-16 C-7,-21 -9,-26 -3,-31.6 Z M3,-31.6 C12,-28 11,-21 15,-16 C11,-10 12,-4 3,-0.4 C9,-6 7,-11 10,-16 C7,-21 9,-26 3,-31.6 Z");
    private static readonly Geometry MelonFlesh = Parse("M-13,-7 L0,-26 L13,-7 C7,-4 -7,-4 -13,-7 Z");
    private static readonly Geometry MelonSliceRind = Parse("M-14,-7 C-7,-4 7,-4 14,-7 L13.5,-3.6 C7,-0.8 -7,-0.8 -13.5,-3.6 Z");

    private static readonly Brush LockedMoss = PetUi.Hex("#B3B49A");
    private static readonly Pen Straw = PetUi.FrozenPen(PetUi.Hex("#D9B566"), 2);
    private static readonly Pen GoldStakePen = PetUi.FrozenPen(PetUi.Hex("#C9962E"), 3), GoldStraw = PetUi.FrozenPen(PetUi.Hex("#F5D36B"), 2);
    private static readonly Geometry LockShackle = Parse("M-3.8,-15 L-3.8,-18 C-3.8,-23.5 3.8,-23.5 3.8,-18 L3.8,-15");
    private static readonly Geometry ScarecrowShirt = Parse("M-12,-45 C-5,-47 5,-47 12,-45 L10,-20 C5,-23 3,-19 0,-21 C-3,-19 -6,-23 -10,-20 Z");
    private static readonly Geometry ScarecrowSmile = Parse("M-3.5,-51.5 C-1.5,-49.5 1.5,-49.5 3.5,-51.5");
    private static readonly Geometry ScarecrowCrown = Parse("M-7,-62 C-7,-71 7,-71 7,-62 Z");
    private static readonly Geometry ScarecrowBrim = Parse("M-14,-61 C-8,-65 8,-65 14,-61 C8,-58.5 -8,-58.5 -14,-61 Z");

    private static Brush LeafDeepBrush() => PetUi.Hex("#4E9256");
    private static Geometry Parse(string text) { var geometry = Geometry.Parse(text); geometry.Freeze(); return geometry; }

    public FarmTileView[] Tiles { get; } = new FarmTileView[PetFarm.TileCount];
    public FarmEffect[] Effects { get; } = new FarmEffect[PetFarm.TileCount];
    // 0 when the effect begins, 1 when it ends.
    public double[] EffectAge { get; } = new double[PetFarm.TileCount];
    public int HoverTile { get; set; } = -1;
    public double Lift { get; set; }
    public double Dustiness { get; set; }
    public double ShakeX { get; set; }
    public double Tilt { get; set; }
    public double Squash { get; set; }
    // Seconds since it began to rise, or -1: the crumbs it throws up and sheds.
    public double Shed { get; set; } = -1;
    // The paper bird (1.5.1 M4): its tile or -1, the side of the crop it stands on, 0 on the ground
    // to 1 up and away, seconds since it came.
    public int BirdTile { get; set; } = -1;
    public int BirdSide { get; set; } = 1;
    public double BirdAir { get; set; }
    public double BirdTime { get; set; }
    public bool BirdFlying { get; set; }
    // Stands at the right end of the plot, once bought (1.5.1 M5).
    public bool Scarecrow { get; set; }
    public bool GoldenScarecrow { get; set; }
    // The basket at the left end of the plot: how full it looks (0 to 3), and 0 to 1 while it bounces from a drop, or -1.
    public int BasketFill { get; set; }
    public double BasketBounce { get; set; } = -1;

    public PetFarmArt() { Width = ArtWidth; Height = ArtHeight; }

    public static double TileLeft(int index) => Side + Pad + index * TileWidth;
    public static double TileCenter(int index) => TileLeft(index) + TileWidth / 2;
    // The tile under x, or -1 on the plot's rim.
    public static int TileAt(double x)
    {
        double from = x - (Side + Pad);
        return from < 0 || from >= TileWidth * PetFarm.TileCount ? -1 : (int)(from / TileWidth);
    }

    public static double PlantHeight(FarmStage stage, FarmCrop? crop) => stage switch
    {
        FarmStage.Seed => 6,
        FarmStage.Sprout => 20,
        FarmStage.Young => crop == FarmCrop.Tomato ? 44 : crop is FarmCrop.Pumpkin or FarmCrop.Watermelon ? 34 : 38,
        FarmStage.Ripe => crop switch { FarmCrop.Tomato => 56, FarmCrop.Pumpkin or FarmCrop.Watermelon => 38, FarmCrop.Radish => 54, _ => 56 },
        _ => 0
    };

    private static double TopOf(in FarmTileView tile) => tile.Locked ? FenceHeight : PlantHeight(tile.Stage, tile.Crop) + (tile.NeedsWater || tile.Stage == FarmStage.Ripe ? BadgeGap + 8 : 0);

    // Distance from the top of the art down to the top of what stands on a tile (the label sits above it).
    public double TopOfTile(int index) => GroundY - TopOf(Tiles[index]);

    protected override void OnRender(DrawingContext dc)
    {
        double air = Math.Max(0, Lift);
        if (air > 0.5)
            dc.DrawEllipse(Shadow, null, new Point(PlotMid, Floor + 1), PlotWidth / 2 * (1 - air / 400), 5 + air / 12);

        var plot = Matrix.Identity;
        plot.ScaleAt(1 + 0.04 * Squash, 1 - 0.12 * Squash, PlotMid, Floor);
        plot.RotateAt(Tilt, PlotMid, Floor);
        plot.Translate(ShakeX, -air);
        dc.PushTransform(new MatrixTransform(plot));
        dc.DrawRectangle(Hit, null, new Rect(Side, SoilTop, PlotWidth, SoilHeight)); // The bank takes clicks.
        for (int i = 0; i < Tiles.Length; i++)
        {
            double top = TopOf(Tiles[i]);
            if (top > 0) dc.DrawRectangle(Hit, null, new Rect(TileLeft(i) + 3, GroundY - top, TileWidth - 6, top));
        }
        if (air > 0.5) DrawUnderside(dc, Side, PlotWidth, air);
        DrawBank(dc, Side, PlotWidth);
        if (Scarecrow)
        {
            dc.PushTransform(new TranslateTransform(ScarecrowX, SoilTop + 2));
            DrawScarecrow(dc, GoldenScarecrow);
            dc.Pop();
        }
        DrawBasket(dc);
        for (int i = 0; i < Tiles.Length; i++) DrawBed(dc, i);
        for (int i = 0; i < Tiles.Length; i++) DrawCrop(dc, i);
        for (int i = 0; i < Tiles.Length; i++) DrawEffect(dc, i);
        if (BirdTile >= 0) DrawBird(dc);
        dc.Pop();
        if (Shed >= 0) DrawShedding(dc, Side, PlotWidth, Shed);
        if (Dustiness > 0) { DrawDust(dc, Side, PlotWidth, Dustiness); DrawLandingDirt(dc, Side, PlotWidth, Dustiness); }
    }

    // The soil's underside hanging below land in the air. Every piece of land, the field and the patches, uses these.
    internal static void DrawUnderside(DrawingContext dc, double left, double width, double air)
    {
        double grown = Math.Clamp(air / FarmLift.FloatHeight, 0, 1);
        dc.PushTransform(new TranslateTransform(left, Floor - 1));
        dc.PushTransform(new ScaleTransform(1, grown));
        dc.DrawGeometry(EarthDeep, Ink, width == PlotWidth ? Underside : ShapeFor(width, undersides, MakeUnderside));
        dc.Pop();
        dc.Pop();
    }

    // The same every time for a given crumb and moment: nothing is kept between frames.
    private static double Noise(int crumb, int salt)
    {
        double value = Math.Sin(crumb * 12.9898 + salt * 78.233) * 43758.5453;
        return value - Math.Floor(value);
    }

    private static void DrawCrumb(DrawingContext dc, int crumb, double x, double y)
    {
        if (y >= Floor) return; // It has reached the floor.
        double size = 1.6 + Noise(crumb, 9) * 2.2;
        dc.DrawEllipse(crumb % 3 == 0 ? EarthDeep : crumb % 3 == 1 ? Earth : Tilled, null, new Point(x, y), size, size * 0.85);
    }

    private static int Crumbs(int full, double width) => Math.Max(8, (int)Math.Round(full * width / PlotWidth));

    internal static void DrawShedding(DrawingContext dc, double left, double width, double shed)
    {
        double mid = left + width / 2;
        for (int i = 0, count = Crumbs(BurstCrumbs, width); i < count; i++) // Thrown up from the rim as it tears loose.
        {
            double born = 0.08 + Noise(i, 1) * 0.95, age = shed - born;
            if (age < 0) continue;
            int side = i % 2 == 0 ? -1 : 1;
            double x = mid + side * width / 2 * (0.35 + 0.65 * Noise(i, 2)) + side * (12 + Noise(i, 3) * 40) * age;
            double y = SoilTop - FarmLift.RiseHeightAt(born) - (70 + Noise(i, 4) * 100) * age + 215 * age * age;
            DrawCrumb(dc, i, x, y);
        }
        for (int i = 0, count = Crumbs(ShedCrumbs, width); i < count; i++) // Dropping away from underneath.
        {
            double born = 0.55 + Noise(i, 5) * 1.55, age = shed - born;
            if (age < 0) continue;
            double x = left + 12 + Noise(i, 6) * (width - 24) + (Noise(i, 7) - 0.5) * 14 * age;
            double y = Floor - FarmLift.RiseHeightAt(born) + 6 + 8 * age + 160 * age * age;
            DrawCrumb(dc, i, x, y);
        }
    }

    // The blow of landing: dirt flung out at both ends and up along the front.
    internal static void DrawLandingDirt(DrawingContext dc, double left, double width, double dustiness)
    {
        double age = (1 - Math.Clamp(dustiness, 0, 1)) * FarmLift.DustSeconds;
        for (int i = 0, count = Crumbs(LandingCrumbs, width); i < count; i++)
        {
            int group = i % 3;
            double start = group switch { 0 => left + Noise(i, 1) * 30, 1 => left + width - Noise(i, 1) * 30, _ => left + Noise(i, 1) * width };
            double sideways = group switch { 0 => -(15 + Noise(i, 2) * 30), 1 => 15 + Noise(i, 2) * 30, _ => (Noise(i, 2) - 0.5) * 50 };
            double up = group == 2 ? 60 + Noise(i, 3) * 130 : 90 + Noise(i, 3) * 170;
            DrawCrumb(dc, i, start + sideways * age, Floor - 4 - up * age + 320 * age * age);
        }
    }

    // A bank of earth with its grass lip.
    internal static void DrawBank(DrawingContext dc, double left, double width)
    {
        dc.DrawRoundedRectangle(Earth, Ink, new Rect(left, SoilTop, width, SoilHeight), 9, 9);
        dc.PushTransform(new TranslateTransform(left, SoilTop - 2));
        dc.DrawGeometry(PetUi.Mint, InkThin, width == PlotWidth ? Lip : ShapeFor(width, lips, MakeLip));
        dc.Pop();
    }

    private static readonly System.Collections.Generic.Dictionary<double, Geometry> lips = new(), undersides = new();

    private static Geometry ShapeFor(double width, System.Collections.Generic.Dictionary<double, Geometry> shapes, Func<double, string> make)
    {
        if (!shapes.TryGetValue(width, out var shape)) shapes[width] = shape = Parse(make(width));
        return shape;
    }

    // The same scalloped grass as the field's, at another width.
    private static string MakeLip(double w)
    {
        var path = new System.Text.StringBuilder(FormattableString.Invariant($"M0,9 C0,3 5,0 11,0 L{w - 11},0 C{w - 5},0 {w - 1},3 {w},9"));
        int waves = Math.Max(3, (int)Math.Round(w / 19));
        double step = w / waves;
        for (int i = 0; i < waves; i++)
        {
            double from = w - i * step, to = from - step, y = i == waves - 1 ? 9 : 12;
            path.Append(FormattableString.Invariant($" C{from - step * 0.35},14 {from - step * 0.65},9 {to},{y}"));
        }
        return path.Append(" Z").ToString();
    }

    private static string MakeUnderside(double w)
    {
        var path = new System.Text.StringBuilder(FormattableString.Invariant($"M0,0 L{w},0"));
        int bumps = Math.Max(2, (int)Math.Round(w / 58));
        double step = w / bumps;
        for (int i = 0; i < bumps; i++)
        {
            double from = w - i * step, to = from - step, y = i == bumps - 1 ? 0 : i % 2 == 0 ? 22 : 15;
            path.Append(FormattableString.Invariant($" C{from - step * 0.3},{y + 12} {from - step * 0.7},{y + 8} {to},{y}"));
        }
        return path.Append(" Z").ToString();
    }

    private void DrawBed(DrawingContext dc, int index)
    {
        var tile = Tiles[index];
        var bed = new Rect(TileLeft(index) + 4, SoilTop + 8, TileWidth - 8, SoilHeight - 13);
        var fill = tile.Locked ? LockedMoss : tile.Stage == FarmStage.Wild ? Moss : tile.Wet ? Wet : Tilled;
        dc.DrawRoundedRectangle(fill, InkThin, bed, 6, 6);
        if (tile.Stage == FarmStage.Wild)
        {
            for (int blade = 0; blade < 4; blade++)
            {
                double x = bed.Left + 8 + blade * 10, y = bed.Bottom - 3 - blade % 2 * 3;
                dc.DrawLine(Tuft, new Point(x, y), new Point(x - 2, y - 5));
                dc.DrawLine(Tuft, new Point(x, y), new Point(x + 2, y - 4));
            }
        }
        else
        {
            for (int row = 1; row <= 2; row++)
                dc.DrawLine(Furrow, new Point(bed.Left + 6, bed.Top + row * 5), new Point(bed.Right - 6, bed.Top + row * 5));
            if (tile.Wet) dc.DrawEllipse(WetShine, null, new Point(bed.Left + 13, bed.Top + 4.5), 8, 2.2);
        }
        if (index == HoverTile) dc.DrawRoundedRectangle(null, Hover, bed, 6, 6);
        if (index == HoverTile && tile.Stage is FarmStage.Seed or FarmStage.Sprout or FarmStage.Young)
        {
            var bar = new Rect(bed.Left + 5, bed.Bottom - 6, bed.Width - 10, 4);
            dc.DrawRoundedRectangle(BarBack, null, bar, 2, 2);
            dc.DrawRoundedRectangle(PetUi.Game.Gold, null, new Rect(bar.Left, bar.Top, Math.Max(2, bar.Width * Math.Clamp(tile.Progress, 0, 1)), bar.Height), 2, 2);
        }
    }

    private void DrawCrop(DrawingContext dc, int index)
    {
        var tile = Tiles[index];
        if (tile.Locked)
        {
            dc.PushTransform(new TranslateTransform(TileCenter(index), GroundY + 6));
            DrawFence(dc);
            dc.Pop();
            return;
        }
        if (tile.Stage is FarmStage.Wild or FarmStage.Tilled) return;
        double pop = 1;
        if (Effects[index] is FarmEffect.Plant or FarmEffect.Grow) pop = 1 + 0.28 * Math.Sin(Math.Clamp(EffectAge[index], 0, 1) * Math.PI);
        dc.PushTransform(new TranslateTransform(TileCenter(index), GroundY + 6));
        if (pop != 1) dc.PushTransform(new ScaleTransform(pop, pop));
        DrawPlant(dc, tile.Stage, tile.Crop ?? FarmCrop.Carrot);
        if (pop != 1) dc.Pop();
        dc.Pop();

        if (tile.NeedsWater || tile.Stage == FarmStage.Ripe)
        {
            double y = GroundY - PlantHeight(tile.Stage, tile.Crop) - BadgeGap;
            dc.PushTransform(new TranslateTransform(TileCenter(index), y));
            dc.DrawGeometry(tile.NeedsWater ? Water : PetUi.Game.Gold, InkThin, tile.NeedsWater ? WaterDrop : Star);
            dc.Pop();
        }
    }

    private static readonly Brush Wicker = PetUi.Hex("#C89A5B"), WickerRim = PetUi.Hex("#A87A42"), PileOrange = PetUi.Hex("#EE9440"), PileRed = PetUi.Hex("#E2503F"), PileGreen = PetUi.Hex("#7DB45C");
    private static readonly Geometry BasketBody = Parse("M-13,-16 L13,-16 L10,0 L-10,0 Z");

    private void DrawBasket(DrawingContext dc)
    {
        double squash = BasketBounce >= 0 ? Math.Sin(BasketBounce * Math.PI) * (1 - BasketBounce) : 0;
        dc.PushTransform(new TranslateTransform(BasketX, SoilTop + 4));
        dc.PushTransform(new ScaleTransform(1 + 0.14 * squash, 1 - 0.16 * squash));
        if (BasketFill >= 2) dc.DrawEllipse(PileGreen, InkThin, new Point(7, -19), 5, 4.5);
        if (BasketFill >= 3) dc.DrawEllipse(PileRed, InkThin, new Point(-7, -19), 5, 4.5);
        if (BasketFill >= 1) dc.DrawEllipse(PileOrange, InkThin, new Point(0, -20), 5.5, 5);
        dc.DrawGeometry(Wicker, Ink, BasketBody);
        dc.DrawLine(InkThin, new Point(-11.5, -9), new Point(11.5, -9));
        dc.DrawLine(InkThin, new Point(-10.5, -4), new Point(10.5, -4));
        dc.DrawRoundedRectangle(WickerRim, Ink, new Rect(-14.5, -18, 29, 4.5), 2, 2);
        dc.Pop(); dc.Pop();
    }

    private const double PotRim = -50;
    private static readonly Brush Stone = PetUi.Hex("#B9B2A6"), StoneDeep = PetUi.Hex("#968F84"), Iron = PetUi.Hex("#585A68"), IronHigh = PetUi.Hex("#7D8092"), PotDark = PetUi.Hex("#2E2A2C");
    private static readonly Brush Flame = PetUi.Hex("#F0913A"), FlameCore = PetUi.Hex("#FFD36B"), Ember = PetUi.Hex("#E2603A");
    private static readonly Pen SpoonInk = PetUi.FrozenPen(PetUi.ArtInk, 5.2), SpoonWood = PetUi.FrozenPen(Stake, 2.8);
    private static readonly Geometry PotBody = Parse("M-25,-50 L25,-50 C27,-34 21,-21 12,-21 L-12,-21 C-21,-21 -27,-34 -25,-50 Z");
    private static readonly Geometry FlameTongue = Parse("M0,0 C-9,-3 -10,-12 -5,-17 C-4,-13 -2,-12 -1,-13 C-3,-19 0,-25 4,-28 C4,-22 10,-17 9,-9 C9,-4 5,0 0,0 Z");

    private static Brush Broth(FarmDish dish) => dish switch
    {
        FarmDish.RadishSoup => PinkBroth,
        FarmDish.CarrotStew => StewBroth,
        FarmDish.StuffedTomato or FarmDish.CarpTomato => TomatoRed,
        FarmDish.GrilledFish => StewBroth,
        FarmDish.ChilledWatermelon => MelonRed,
        _ => Pumpkin
    };

    private const double FloatOffset = 16;
    private static readonly Brush PondShine = PetUi.Frozen(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF));
    private static readonly Brush Minnow = PetUi.Hex("#9CC7D8"), Carp = PetUi.Hex("#D9A65A"), GoldFish = PetUi.Hex("#F5BF3A"), Cattail = PetUi.Hex("#8A5B3D");
    private static readonly Pen RipplePen = PetUi.FrozenPen(PetUi.Frozen(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF)), 1.3);
    private static readonly Geometry FishBody = Parse("M-12,0 C-6,-8 8,-8 14,0 C8,8 -6,8 -12,0 Z");
    private static readonly Geometry FishTail = Parse("M-11,0 L-20,-7 L-18,0 L-20,7 Z");
    private static readonly Geometry FishFin = Parse("M-3,-5.5 C-1,-11 5,-11 7,-5.5 Z");

    private static Brush FishColor(FarmFish fish) => fish switch { FarmFish.Minnow => Minnow, FarmFish.Carp => Carp, _ => GoldFish };

    // A fish side on, facing right, its middle at (0,0).
    public static void DrawFish(DrawingContext dc, FarmFish fish)
    {
        var color = FishColor(fish);
        dc.DrawGeometry(color, InkThin, FishFin);
        dc.DrawGeometry(color, InkThin, FishTail);
        dc.DrawGeometry(color, Ink, FishBody);
        dc.DrawLine(InkThin, new Point(3, -5), new Point(3, 5));
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(8, -1.5), 1.5, 1.5);
        if (fish != FarmFish.Goldfish) return;
        dc.PushTransform(new TranslateTransform(-3, -11));
        dc.DrawGeometry(RadishTip, InkThin, Star);
        dc.Pop();
    }

    private static Brush CropColor(FarmCrop crop) => crop switch
    {
        FarmCrop.Radish => RadishRed,
        FarmCrop.Carrot => Orange,
        FarmCrop.Tomato => TomatoRed,
        FarmCrop.Watermelon => MelonRind,
        _ => Pumpkin
    };

    // A small closed fence with a padlock, standing on (0,0): this tile is still to be bought.
    private static void DrawFence(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Stake, InkThin, new Rect(-22, -23, 44, 4.5), 1.5, 1.5);
        dc.DrawRoundedRectangle(Stake, InkThin, new Rect(-22, -12, 44, 4.5), 1.5, 1.5);
        foreach (double x in new[] { -17.0, 17.0 }) dc.DrawRoundedRectangle(Stake, InkThin, new Rect(x - 2.5, -27, 5, 27), 1.5, 1.5);
        dc.DrawGeometry(null, Ink, LockShackle);
        dc.DrawRoundedRectangle(PetUi.Game.Gold, InkThin, new Rect(-6.5, -15, 13, 11), 2.5, 2.5);
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(0, -10), 1.6, 1.6);
    }

    // A cooked dish side on, standing on y = 0, about 34 wide and 28 high (the kitchen and the cat's meal).
    public static void DrawDish(DrawingContext dc, FarmDish dish)
    {
        switch (dish)
        {
            case FarmDish.RadishSoup:
                DrawBowl(dc, RadishTip, PinkBroth, () =>
                {
                    foreach (double x in new[] { -6.5, 6.5 })
                    {
                        dc.DrawEllipse(RadishRed, InkThin, new Point(x, -13.5), 5, 2.2);
                        dc.DrawEllipse(RadishTip, null, new Point(x, -13.7), 3, 1.1);
                    }
                });
                break;
            case FarmDish.CarrotStew:
                DrawBowl(dc, Stake, StewBroth, () =>
                {
                    foreach (var (x, y) in new[] { (-8.0, -13.0), (0.0, -14.3), (8.0, -13.0) })
                        dc.DrawEllipse(Orange, InkThin, new Point(x, y), 4, 2);
                    dc.DrawEllipse(Leaf, InkThin, new Point(3.5, -16.5), 3.4, 1.6);
                });
                foreach (int side in new[] { -1, 1 }) dc.DrawEllipse(Stake, InkThin, new Point(side * 16.5, -8.5), 2.2, 3);
                break;
            case FarmDish.StuffedTomato:
                dc.DrawEllipse(RadishTip, Ink, new Point(0, -3), 17, 3.4);
                dc.DrawEllipse(TomatoRed, Ink, new Point(0, -13.5), 11.5, 10.5);
                dc.DrawEllipse(Seed, InkThin, new Point(0, -21.5), 7, 2.8);
                dc.DrawEllipse(Leaf, InkThin, new Point(0, -24), 3.6, 1.8);
                break;
            case FarmDish.GrilledFish:
                dc.DrawEllipse(RadishTip, Ink, new Point(0, -3), 18, 3.6);
                dc.PushTransform(new TranslateTransform(0, -10));
                dc.PushTransform(new ScaleTransform(0.95, 0.95));
                dc.PushTransform(new RotateTransform(-6));
                DrawFish(dc, FarmFish.Minnow);
                dc.Pop(); dc.Pop(); dc.Pop();
                foreach (double x in new[] { -5.0, 1.0, 7.0 }) dc.DrawLine(InkThin, new Point(x - 1.5, -15), new Point(x + 1.5, -6));
                break;
            case FarmDish.CarpTomato:
                DrawBowl(dc, Stake, TomatoRed, () =>
                {
                    dc.PushTransform(new TranslateTransform(-2, -15));
                    dc.PushTransform(new ScaleTransform(0.65, 0.5));
                    DrawFish(dc, FarmFish.Carp);
                    dc.Pop(); dc.Pop();
                    dc.DrawEllipse(Leaf, InkThin, new Point(11, -15.5), 3, 1.5);
                });
                break;
            case FarmDish.GoldfishHotpot:
                DrawBowl(dc, Iron, Pumpkin, () =>
                {
                    dc.PushTransform(new TranslateTransform(-1, -15.5));
                    dc.PushTransform(new ScaleTransform(0.7, 0.55));
                    DrawFish(dc, FarmFish.Goldfish);
                    dc.Pop(); dc.Pop();
                });
                foreach (int side in new[] { -1, 1 }) dc.DrawEllipse(Iron, InkThin, new Point(side * 16.5, -8.5), 2.2, 3);
                break;
            case FarmDish.ChilledWatermelon:
                dc.DrawEllipse(RadishTip, Ink, new Point(0, -3), 19, 3.6);
                dc.DrawRoundedRectangle(Ice, InkThin, new Rect(-19, -12, 8, 8), 2, 2);
                dc.DrawRoundedRectangle(Ice, InkThin, new Rect(11.5, -11, 7, 7), 2, 2);
                dc.DrawGeometry(MelonRed, Ink, MelonFlesh);
                dc.DrawGeometry(MelonRind, Ink, MelonSliceRind);
                foreach (var (x, y) in new[] { (-4.0, -11.0), (3.5, -10.0), (0.0, -17.0) })
                    dc.DrawEllipse(PetUi.ArtInk, null, new Point(x, y), 1.1, 1.7);
                break;
            default:
                dc.DrawGeometry(Stake, Ink, PieTin);
                dc.DrawEllipse(Seed, Ink, new Point(0, -9.5), 17, 5);
                dc.DrawEllipse(Pumpkin, InkThin, new Point(0, -10), 13, 3.2);
                dc.DrawEllipse(RadishTip, InkThin, new Point(0, -12.5), 5.5, 3);
                break;
        }
    }

    private static void DrawBowl(DrawingContext dc, Brush bowl, Brush broth, Action topping)
    {
        dc.DrawEllipse(broth, Ink, new Point(0, -13), 16, 4.5);
        topping();
        dc.DrawGeometry(bowl, Ink, BowlFront);
    }

    // Steam rising off a hot dish. Outlined, so a pale wallpaper does not swallow it.
    public static void DrawSteam(DrawingContext dc, double time)
    {
        for (int i = 0; i < 3; i++)
        {
            double k = (time * 0.7 + i / 3.0) % 1;
            dc.PushOpacity(Math.Sin(k * Math.PI));
            dc.DrawEllipse(Puff, PuffEdge, new Point((i - 1) * 9 + Math.Sin(k * 6 + i) * 2.5, -26 - k * 18), 2.4 + k * 2, 2.4 + k * 2);
            dc.Pop();
        }
    }

    // A straw-stuffed scarecrow on its pole, the foot of the pole at (0,0). The golden one is the top level's.
    public static void DrawScarecrow(DrawingContext dc, bool golden = false)
    {
        var pole = golden ? GoldStakePen : StakePen;
        dc.DrawLine(pole, new Point(0, 0), new Point(0, -48));
        dc.DrawLine(pole, new Point(-18, -41), new Point(18, -41));
        foreach (int side in new[] { -1, 1 })
            foreach (double fan in new[] { -3.5, 0.0, 3.5 })
                dc.DrawLine(golden ? GoldStraw : Straw, new Point(side * 17, -41), new Point(side * 23, -41 + fan));
        dc.DrawGeometry(golden ? PetUi.Game.Gold : Water, InkThin, ScarecrowShirt);
        dc.DrawEllipse(Seed, InkThin, new Point(0, -55), 9, 9.5);
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(-3.2, -56), 1.2, 1.2);
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(3.2, -56), 1.2, 1.2);
        dc.DrawGeometry(null, InkThin, ScarecrowSmile);
        dc.DrawGeometry(PetUi.Game.Gold, InkThin, ScarecrowCrown);
        dc.DrawGeometry(PetUi.Game.Gold, InkThin, ScarecrowBrim);
        if (!golden) return;
        foreach (var (x, y) in new[] { (-17.0, -58.0), (17.0, -52.0) })
        {
            dc.PushTransform(new TranslateTransform(x, y));
            dc.DrawGeometry(RadishTip, InkThin, Star);
            dc.Pop();
        }
    }

    // A plant with its roots at (0,0), growing up.
    public static void DrawPlant(DrawingContext dc, FarmStage stage, FarmCrop crop)
    {
        switch (stage)
        {
            case FarmStage.Seed:
                dc.DrawGeometry(Tilled, InkThin, Mound);
                dc.DrawEllipse(Seed, InkThin, new Point(0, -4), 3.2, 2.4);
                break;
            case FarmStage.Sprout:
                dc.DrawLine(StemPen, new Point(0, 0), new Point(0, -12));
                dc.DrawGeometry(Leaf, InkThin, SproutLeaves);
                break;
            case FarmStage.Young:
                DrawYoung(dc, crop);
                break;
            default:
                DrawRipe(dc, crop);
                break;
        }
    }

    private static void DrawYoung(DrawingContext dc, FarmCrop crop)
    {
        switch (crop)
        {
            case FarmCrop.Tomato:
                dc.DrawLine(StakePen, new Point(7, 0), new Point(7, -46));
                dc.DrawLine(StemPen, new Point(0, 0), new Point(0, -30));
                dc.DrawGeometry(Leaf, InkThin, TallLeaves);
                break;
            case FarmCrop.Pumpkin:
                dc.DrawGeometry(Leaf, Ink, Vine);
                dc.DrawGeometry(null, InkThin, VineVein);
                break;
            case FarmCrop.Watermelon:
                dc.DrawGeometry(LeafDeep, Ink, Vine);
                dc.DrawGeometry(null, InkThin, VineVein);
                dc.DrawEllipse(MelonStripe, InkThin, new Point(11, -6), 7, 5.5);
                dc.DrawEllipse(MelonRind, null, new Point(9.5, -7), 3, 2);
                break;
            default:
                dc.DrawGeometry(crop == FarmCrop.Radish ? Leaf : LeafDeep, InkThin, Blades);
                break;
        }
    }

    private static void DrawRipe(DrawingContext dc, FarmCrop crop)
    {
        switch (crop)
        {
            case FarmCrop.Radish:
                dc.PushTransform(new TranslateTransform(0, -15));
                dc.DrawGeometry(Leaf, InkThin, Blades);
                dc.Pop();
                dc.DrawGeometry(RadishRed, Ink, RadishBulb);
                dc.DrawGeometry(RadishTip, null, RadishEnd);
                break;
            case FarmCrop.Carrot:
                dc.PushTransform(new TranslateTransform(0, -17));
                dc.DrawGeometry(LeafDeep, InkThin, Blades);
                dc.Pop();
                dc.DrawGeometry(Orange, Ink, CarrotRoot);
                dc.DrawGeometry(null, InkThin, CarrotLines);
                break;
            case FarmCrop.Tomato:
                dc.DrawLine(StakePen, new Point(0, 0), new Point(0, -58));
                dc.PushTransform(new TranslateTransform(0, -14));
                dc.DrawGeometry(Leaf, InkThin, TallLeaves);
                dc.Pop();
                Fruit(dc, -12, -24); Fruit(dc, 12, -19); Fruit(dc, 7, -40);
                break;
            case FarmCrop.Watermelon:
                dc.DrawGeometry(StemBrown, InkThin, PumpkinStem);
                dc.DrawEllipse(MelonRind, Ink, new Point(0, -16), 21, 16);
                dc.DrawGeometry(MelonStripe, null, MelonStripes);
                break;
            default:
                dc.DrawGeometry(StemBrown, InkThin, PumpkinStem);
                dc.DrawEllipse(PumpkinRib, Ink, new Point(-11, -15), 11, 15);
                dc.DrawEllipse(PumpkinRib, Ink, new Point(11, -15), 11, 15);
                dc.DrawEllipse(Pumpkin, Ink, new Point(0, -15), 13, 16);
                dc.DrawLine(Rib, new Point(-5, -28), new Point(-5, -3));
                dc.DrawLine(Rib, new Point(5, -28), new Point(5, -3));
                break;
        }
    }

    private static readonly Brush StemBrown = PetUi.Hex("#7E9A4C");
    private static readonly Geometry PumpkinStem = Parse("M-3,-29 C-3,-34 -1,-38 4,-39 L6,-35 C3,-34 3,-31 3,-29 Z");

    private static void Fruit(DrawingContext dc, double x, double y)
    {
        dc.DrawEllipse(TomatoRed, Ink, new Point(x, y), 8, 7.5);
        dc.PushTransform(new TranslateTransform(x, y - 6));
        dc.DrawGeometry(LeafDeep, null, Calyx);
        dc.Pop();
    }

    private void DrawEffect(DrawingContext dc, int index)
    {
        double k = Math.Clamp(EffectAge[index], 0, 1), center = TileCenter(index);
        switch (Effects[index])
        {
            case FarmEffect.Till:
                dc.PushOpacity(1 - k);
                for (int i = 0; i < 6; i++)
                {
                    double spread = (i - 2.5) * 8 * (0.4 + k), rise = Math.Sin(k * Math.PI) * (10 + i % 3 * 6);
                    dc.DrawEllipse(Tilled, null, new Point(center + spread, GroundY + 4 - rise), 2.6, 2.6);
                }
                dc.Pop();
                break;
            case FarmEffect.Water:
                dc.PushOpacity(Math.Min(1, 2 - 2 * k));
                for (int i = 0; i < 6; i++)
                {
                    double fall = (k * 1.5 + i * 0.13) % 1;
                    dc.PushTransform(new TranslateTransform(center - 16 + i * 6.5, GroundY - 34 + fall * 36));
                    dc.PushTransform(new ScaleTransform(0.55, 0.55));
                    dc.DrawGeometry(Water, InkThin, WaterDrop);
                    dc.Pop(); dc.Pop();
                }
                dc.Pop();
                break;
            case FarmEffect.Harvest:
                dc.PushOpacity(1 - k);
                for (int i = 0; i < 6; i++)
                {
                    double angle = i * Math.PI / 3 + 0.4, reach = 8 + k * 26;
                    dc.PushTransform(new TranslateTransform(center + Math.Cos(angle) * reach, GroundY - 14 + Math.Sin(angle) * reach));
                    dc.DrawGeometry(PetUi.Game.Gold, InkThin, Star);
                    dc.Pop();
                }
                dc.Pop();
                break;
        }
    }

    // A folded-paper bird beside the crop, its feet at the origin, turned towards the crop. It pecks on
    // the ground; in the air the wing beats and it fades as it gets away.
    private void DrawBird(DrawingContext dc)
    {
        double air = Math.Clamp(BirdAir, 0, 1);
        double x = TileCenter(BirdTile) + BirdSide * (BirdStand + air * BirdFlyOut), y = GroundY + 3 - air * BirdFlyUp;
        DrawPaperBird(dc, x, y, BirdSide, air, BirdFlying, BirdTime);
    }

    // Its feet at (x, y). It pecks on the ground; in the air the wing beats and it fades as it gets away.
    private static void DrawPaperBird(DrawingContext dc, double x, double y, int side, double air, bool flying, double time)
    {
        double peck = flying ? 0 : 18 * Math.Pow(Math.Max(0, Math.Sin(time * 5)), 4);
        double wing = flying ? Math.Cos(time * 34) : -0.38;
        dc.PushOpacity(air < 0.6 ? 1 : (1 - air) / 0.4);
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new ScaleTransform(-side * BirdScale, BirdScale));
        dc.PushTransform(new RotateTransform(peck));
        dc.DrawGeometry(null, InkThin, BirdLegs);
        dc.DrawGeometry(Orange, InkThin, BirdBeak);
        dc.DrawGeometry(BirdPaper, InkThin, BirdBody);
        dc.DrawGeometry(null, BirdFold, BirdCrease);
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(6, -13), 1.3, 1.3);
        dc.PushTransform(new ScaleTransform(1, wing, 0, -8));
        dc.DrawGeometry(PetUi.Lilac, InkThin, BirdWing);
        dc.Pop();
        dc.Pop(); dc.Pop(); dc.Pop();
        dc.Pop();
    }

    private const double BirdStand = 27, BirdScale = 1.25, BirdFlyOut = 70, BirdFlyUp = 90;
    private static readonly Brush BirdPaper = PetUi.Hex("#F7F1E3");
    private static readonly Pen BirdFold = PetUi.FrozenPen(PetUi.Frozen(Color.FromArgb(0x80, 0x3A, 0x2C, 0x1F)), 1);
    private static readonly Geometry BirdBody = Parse("M-15,-17 L-3,-9 L3,-17 L9,-15 L6,-6 L-2,-3 Z");
    private static readonly Geometry BirdBeak = Parse("M8,-15.5 L15,-12.5 L7,-10.5 Z");
    private static readonly Geometry BirdWing = Parse("M-6,-8 L4,-8 L-3,-21 Z");
    private static readonly Geometry BirdCrease = Parse("M-3,-9 L4,-6");
    private static readonly Geometry BirdLegs = Parse("M-2,-4 L-2,0 M2,-5 L2,0");

    internal static void DrawDust(DrawingContext dc, double left, double width, double dustiness)
    {
        double k = 1 - Math.Clamp(dustiness, 0, 1), mid = left + width / 2;
        dc.PushOpacity(1 - k);
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 5; i++)
                dc.DrawEllipse(Dust, null, new Point(mid + side * (width / 2 + (i - 2) * 8 + k * 16), Floor - 3 - k * (5 + i * 5)), 5 + k * 8, 4 + k * 5);
        dc.Pop();
    }
}

// The ripe crop as a small picture (the seed menu).
public sealed class FarmCropIcon : FrameworkElement
{
    private readonly FarmCrop crop;

    public FarmCropIcon(FarmCrop crop, double size = 22)
    {
        this.crop = crop;
        Width = Height = size;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double zoom = ActualHeight / 62;
        dc.PushTransform(new TranslateTransform(ActualWidth / 2, ActualHeight - 6 * zoom));
        dc.PushTransform(new ScaleTransform(zoom, zoom));
        PetFarmArt.DrawPlant(dc, FarmStage.Ripe, crop);
        dc.Pop(); dc.Pop();
    }
}

// A fish as a small picture (the shop).
public sealed class FarmFishIcon : FrameworkElement
{
    private readonly FarmFish fish;

    public FarmFishIcon(FarmFish fish, double size = 22)
    {
        this.fish = fish;
        Width = Height = size;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double zoom = ActualHeight / 34;
        dc.PushTransform(new TranslateTransform(ActualWidth / 2 + 2 * zoom, ActualHeight / 2));
        dc.PushTransform(new ScaleTransform(zoom, zoom));
        PetFarmArt.DrawFish(dc, fish);
        dc.Pop(); dc.Pop();
    }
}

// A cooked dish as a small picture (the kitchen).
public sealed class FarmDishIcon : FrameworkElement
{
    private readonly FarmDish dish;

    public FarmDishIcon(FarmDish dish, double size = 22)
    {
        this.dish = dish;
        Width = Height = size;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double zoom = ActualHeight / 36;
        dc.PushTransform(new TranslateTransform(ActualWidth / 2, ActualHeight - 2 * zoom));
        dc.PushTransform(new ScaleTransform(zoom, zoom));
        PetFarmArt.DrawDish(dc, dish);
        dc.Pop(); dc.Pop();
    }
}

// The scarecrow as a small picture (the shop).
public sealed class FarmScarecrowIcon : FrameworkElement
{
    private bool golden;
    public bool Golden
    {
        get => golden;
        set { if (golden != value) { golden = value; InvalidateVisual(); } }
    }

    public FarmScarecrowIcon(double size = 22)
    {
        Width = Height = size;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double zoom = ActualHeight / 72;
        dc.PushTransform(new TranslateTransform(ActualWidth / 2, ActualHeight));
        dc.PushTransform(new ScaleTransform(zoom, zoom));
        PetFarmArt.DrawScarecrow(dc, golden);
        dc.Pop(); dc.Pop();
    }
}
