using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M12 (Q8): the flag slime carries a pole on its back with a gold pennant fluttering behind it (the
// gold dashed ring of its aura comes from PetBattleView.Links.cs); an ally sped up by an aura trails two
// short gold speed lines. Outlines and small shapes only, so clicks still go through to the desktop.
public sealed partial class BattleFieldView
{
    private static readonly Pen PolePen = FrozenPen(Frozen(255, 0x6B, 0x4A, 0x2E), 2.4);
    private static readonly Brush PennantGold = Frozen(255, 0xFF, 0xC8, 0x2E);
    private static readonly Pen PennantStripe = FrozenPen(Frozen(255, 0xE5, 0x4B, 0x4B), 1.6);
    private static readonly Pen SpeedLine = FrozenPen(Frozen(220, 0xFF, 0xC8, 0x2E), 2);
    // Pointing right from the top of the pole (0, 0); flipped and fluttered by a transform.
    private static readonly Geometry Pennant = Parse("M 0,0 Q 13,3 28,8 Q 13,12 0,16 Z");
    private static readonly Geometry PennantBand = Parse("M 5,8 L 17,8");

    // Body coordinates, not flipped: the pole stands at its back, the pennant streams away from its front.
    private static void DrawFlag(DrawingContext dc, Slime slime, double time)
    {
        double back = -slime.Facing * 9;
        var foot = new Point(back, -22);
        var top = new Point(back, -58);
        dc.DrawLine(Halo, foot, top);
        dc.DrawLine(PolePen, foot, top);
        double flutter = Math.Sin(time * 7 + slime.Id) * 0.12;
        dc.PushTransform(new MatrixTransform(-slime.Facing, flutter, 0, 1, back, -58));
        dc.DrawGeometry(null, Halo, Pennant);
        dc.DrawGeometry(PennantGold, Ink, Pennant);
        dc.DrawGeometry(null, PennantStripe, PennantBand);
        dc.Pop();
        dc.DrawEllipse(PennantGold, Ink, top, 2.2, 2.2);
    }

    // An ally an aura speeds up (screen coordinates): two short gold lines behind it.
    private static void DrawAuraTrail(DrawingContext dc, Slime slime, double x, double top)
    {
        if (slime.AuraSpeed <= 1 || slime.Link?.Kind == EnemyLinks.Aura || slime.State != SlimeState.Moving) return;
        double back = x - slime.Facing * (slime.Width / 2 + 4);
        for (int i = 0; i < 2; i++)
        {
            double y = top + slime.Height * (0.45 + i * 0.25);
            dc.DrawLine(SpeedLine, new Point(back, y), new Point(back - slime.Facing * (10 - i * 3), y));
        }
    }
}
