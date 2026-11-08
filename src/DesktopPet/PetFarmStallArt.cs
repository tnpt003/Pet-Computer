using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The traveling stall's cart, drawn by code: two wheels, a wooden counter with its goods, a striped
// awning, and a gold mark when something it wants can be delivered. Redrawn only as it rolls.
public sealed class PetFarmStallArt : FrameworkElement
{
    public const double ArtWidth = 130, ArtHeight = 118, Floor = ArtHeight - PetFarmArt.BottomRoom;
    private const double WheelRadius = 11;

    private static readonly Brush Wood = PetUi.Hex("#B4815E"), WoodDeep = PetUi.Hex("#8A5B3D"), Cream = PetUi.Hex("#FFF3EC"), Hub = PetUi.Hex("#585A68");
    private static readonly Brush Tomato = PetUi.Hex("#E2503F"), Pumpkin = PetUi.Hex("#EE9A3C"), Leaf = PetUi.Hex("#6FB36A"), Shade = PetUi.Frozen(Color.FromArgb(0x58, 0x2A, 0x1F, 0x16));
    private static readonly Pen Ink = PetUi.FrozenPen(PetUi.ArtInk, 2.4), InkThin = PetUi.FrozenPen(PetUi.ArtInk, 1.5), Post = PetUi.FrozenPen(WoodDeep, 4);

    public bool Ready { get; set; }
    // DIPs rolled so far: turns the wheels.
    public double Rolled { get; set; }

    public PetFarmStallArt() { Width = ArtWidth; Height = ArtHeight; }

    protected override void OnRender(DrawingContext dc)
    {
        double y = Floor;
        dc.DrawEllipse(Shade, null, new Point(ArtWidth / 2, y + 1), 52, 4);
        dc.DrawLine(Post, new Point(24, y - 42), new Point(24, y - 88));
        dc.DrawLine(Post, new Point(106, y - 42), new Point(106, y - 88));

        // The goods on the counter.
        dc.DrawEllipse(Pumpkin, InkThin, new Point(40, y - 48), 10, 8);
        dc.DrawLine(InkThin, new Point(40, y - 56), new Point(41, y - 60));
        dc.DrawEllipse(Tomato, InkThin, new Point(61, y - 46), 7, 6.5);
        dc.DrawEllipse(Leaf, InkThin, new Point(61, y - 53), 3.6, 1.7);
        dc.PushTransform(new TranslateTransform(89, y - 48));
        dc.PushTransform(new ScaleTransform(0.8, 0.8));
        PetFarmArt.DrawFish(dc, FarmFish.Carp);
        dc.Pop(); dc.Pop();

        dc.DrawRoundedRectangle(Wood, Ink, new Rect(14, y - 42, 102, 24), 4, 4);
        foreach (double x in new[] { 38.0, 65.0, 92.0 }) dc.DrawLine(InkThin, new Point(x, y - 42), new Point(x, y - 18));

        DrawAwning(dc, y);
        foreach (double x in new[] { 34.0, 96.0 }) DrawWheel(dc, x, y - WheelRadius);

        if (!Ready) return;
        dc.DrawEllipse(PetUi.Game.Gold, InkThin, new Point(65, y - 90), 8, 8);
        dc.DrawLine(InkThin, new Point(65, y - 94.5), new Point(65, y - 89));
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(65, y - 85.8), 1.2, 1.2);
    }

    private static void DrawAwning(DrawingContext dc, double y)
    {
        const int stripes = 6;
        const double left = 10, width = 110, top = 88, drop = 18, each = width / stripes;
        for (int i = 0; i < stripes; i++)
        {
            var fill = i % 2 == 0 ? PetUi.Rose : Cream;
            double x = left + i * each;
            dc.DrawEllipse(fill, InkThin, new Point(x + each / 2, y - top + drop), each / 2, 5);
            dc.DrawRectangle(fill, null, new Rect(x, y - top, each, drop));
            dc.DrawLine(InkThin, new Point(x, y - top), new Point(x, y - top + drop));
        }
        dc.DrawLine(InkThin, new Point(left + width, y - top), new Point(left + width, y - top + drop));
        dc.DrawRoundedRectangle(WoodDeep, InkThin, new Rect(left - 2, y - top - 4, width + 4, 6), 3, 3);
    }

    private void DrawWheel(DrawingContext dc, double x, double y)
    {
        dc.DrawEllipse(Wood, Ink, new Point(x, y), WheelRadius, WheelRadius);
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new RotateTransform(Rolled / (Math.PI * 2 * WheelRadius) * 360));
        for (int i = 0; i < 3; i++)
        {
            dc.PushTransform(new RotateTransform(i * 60));
            dc.DrawLine(InkThin, new Point(-WheelRadius + 2, 0), new Point(WheelRadius - 2, 0));
            dc.Pop();
        }
        dc.Pop(); dc.Pop();
        dc.DrawEllipse(Hub, InkThin, new Point(x, y), 3, 3);
    }
}
