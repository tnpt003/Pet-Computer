using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M26: the Screenshot (PetBattle.Malware2.cs) on the strip. Its flash whitens the strip a moment;
// while the enemies stand frozen they hold the pose they had at the flash (BattleFieldView reads
// SlimeTime) inside a camera's viewfinder corners; a dazzled cat has little stars before its eyes.
public sealed partial class BattleFieldView
{
    public const double FlashSeconds = 0.35;
    private static readonly Pen FinderPen = FrozenPen(Frozen(240, 0xFF, 0xFF, 0xFF), 4);
    private static readonly Pen FinderInk = FrozenPen(Frozen(255, 0x3E, 0x7F, 0xB0), 2.2);
    private static readonly Brush FlashWhite = Frozen(255, 255, 255, 255);
    private static readonly Brush DazzleStar = Frozen(255, 0xFF, 0xE0, 0x6B);

    // The time a slime is drawn at: frozen ones keep the moment of the flash.
    private double SlimeTime(double time) => sim.EnemiesFrozen ? Math.Min(time, sim.FlashAt) : time;

    private void DrawSnap(DrawingContext dc, double floor, double time)
    {
        double since = time - sim.FlashAt;
        if (since >= 0 && since < FlashSeconds)
        {
            dc.PushOpacity(0.55 * (1 - since / FlashSeconds));
            dc.DrawRectangle(FlashWhite, null, new Rect(0, 0, ActualWidth, ActualHeight));
            dc.Pop();
        }
        if (sim.EnemiesFrozen)
            foreach (var slime in sim.Slimes)
            {
                if (slime.State == SlimeState.Defeated || slime.Exploded || slime.Escaped || BattleSim.InSky(slime) || slime.Burrowed) continue;
                double x = slime.X - OriginX, half = slime.Width / 2 + 7;
                double top = floor - slime.LeapHeight - slime.Lift - slime.Height - 8, bottom = floor + 1;
                DrawFinder(dc, new Rect(x - half, top, half * 2, bottom - top));
            }
        if (sim.CatDazzled)
        {
            double cx = sim.CatX - OriginX, cy = floor - 96;
            for (int i = 0; i < 3; i++)
            {
                double a = time * 5 + i * Math.PI * 2 / 3;
                var at = new Point(cx + Math.Cos(a) * 22, cy + Math.Sin(a) * 7);
                AppArt.DrawTwinkle(dc, at, 8, DazzleStar);
            }
        }
    }

    // Four viewfinder corners round r.
    private static void DrawFinder(DrawingContext dc, Rect r)
    {
        double arm = Math.Min(10, Math.Min(r.Width, r.Height) / 3);
        foreach (var pen in new[] { FinderPen, FinderInk })
        {
            dc.DrawLine(pen, r.TopLeft, new Point(r.Left + arm, r.Top)); dc.DrawLine(pen, r.TopLeft, new Point(r.Left, r.Top + arm));
            dc.DrawLine(pen, r.TopRight, new Point(r.Right - arm, r.Top)); dc.DrawLine(pen, r.TopRight, new Point(r.Right, r.Top + arm));
            dc.DrawLine(pen, r.BottomLeft, new Point(r.Left + arm, r.Bottom)); dc.DrawLine(pen, r.BottomLeft, new Point(r.Left, r.Bottom - arm));
            dc.DrawLine(pen, r.BottomRight, new Point(r.Right - arm, r.Bottom)); dc.DrawLine(pen, r.BottomRight, new Point(r.Right, r.Bottom - arm));
        }
    }
}
