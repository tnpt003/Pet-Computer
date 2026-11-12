using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The startup intro's small effects (PetWindow.Intro.cs), each in its own click-through window:
// a puff of smoke with sparkles where a keepsake or toy appears, and dirt crumbs with sparkles
// at the foot of the farm sign as it rises out of the ground.
public sealed class PetIntroArt : FrameworkElement
{
    public const double PuffWidth = 140, PuffHeight = 130, DirtWidth = 220, DirtHeight = 110;
    private const int Crumbs = 9;
    private static readonly Brush Soil = PetUi.Frozen(0xFF, 0x8A, 0x62, 0x3E), SoilDark = PetUi.Frozen(0xFF, 0x6A, 0x47, 0x2A);
    private static readonly Pen CrumbInk = PetUi.FrozenPen(PetUi.ArtInk, 0.8);

    public bool Dirt { get; }
    // 0 to 1 over the effect's life.
    public double T { get; set; }

    public PetIntroArt(bool dirt)
    {
        Dirt = dirt;
        Width = dirt ? DirtWidth : PuffWidth;
        Height = dirt ? DirtHeight : PuffHeight;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (T <= 0 || T >= 1) return;
        if (Dirt) DrawDirt(dc);
        else
        {
            TreasureDrawing.SmokePuff(dc, Width / 2, Height * 0.62, T);
            TreasureDrawing.Sparkles(dc, Width / 2, Height * 0.5, Math.Clamp(T * 1.15, 0, 0.999));
        }
    }

    // Crumbs thrown up both ways from the ground line, falling back; sparkles over the middle.
    private void DrawDirt(DrawingContext dc)
    {
        double ground = Height - 8, mid = Width / 2;
        dc.PushOpacity(Math.Clamp((1 - T) * 2, 0, 1));
        for (int i = 0; i < Crumbs; i++)
        {
            double side = i % 2 == 0 ? -1 : 1, spread = 0.35 + 0.65 * ((i * 37) % 11) / 10.0;
            double x = mid + side * (14 + i * 6) + side * 70 * spread * T;
            double y = ground - (120 * (0.5 + 0.5 * spread) * T - 230 * T * T);
            if (y > ground) y = ground;
            double size = 2.2 + (i % 3) * 0.9;
            dc.DrawEllipse(i % 3 == 0 ? SoilDark : Soil, CrumbInk, new Point(x, y), size, size * 0.8);
        }
        dc.Pop();
        TreasureDrawing.Sparkles(dc, mid, ground - 30, Math.Clamp(T * 1.1, 0, 0.999));
    }
}
