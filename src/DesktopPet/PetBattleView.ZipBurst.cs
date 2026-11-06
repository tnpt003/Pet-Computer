using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    // Bounded procedural particles travel from the archive to every part of the battle screen.
    private static void DrawArchiveBurst(DrawingContext dc, BattleSim sim, Slime boss, Func<double, double, Point> at, double time)
    {
        var origin = at(boss.X, boss.Height * 0.65);
        bool burst = boss.ZipBurstLeft > 0;
        double progress = burst ? Math.Clamp(1 - boss.ZipBurstLeft / 0.9, 0, 1) : Math.Clamp(1 - boss.BombClock / 2, 0, 1);
        double spread = burst ? 1 - Math.Pow(1 - progress, 3) : 1 - progress * 0.75;
        dc.PushOpacity(burst ? Math.Min(1, boss.ZipBurstLeft / 0.2) : 0.3 + progress * 0.5);
        for (int i = 0; i < 72; i++)
        {
            double x = sim.Left + (sim.Right - sim.Left) * (0.03 + 0.94 * ((i * 0.61803398875 + 0.07) % 1));
            double h = sim.SkyHeight * (0.03 + 0.94 * ((i * 0.41421356237 + 0.11) % 1));
            Point target = at(x, h);
            Point p = origin + (target - origin) * spread;
            dc.DrawLine(PageLine, p, p + (origin - p) * (burst ? 0.07 : -0.04));
            dc.PushTransform(new TranslateTransform(p.X, p.Y));
            dc.PushTransform(new RotateTransform((i % 2 == 0 ? 1 : -1) * progress * 140 + i * 17));
            double scale = 0.8 + 0.45 * Math.Abs(Math.Sin(i * 3.7));
            dc.PushTransform(new ScaleTransform(scale, scale));
            if (i % 3 == 0) DrawFolderNode(dc, 0, 0, 26);
            else ZipPage(dc, 0, 0, 0);
            dc.Pop(); dc.Pop(); dc.Pop();
        }
        if (burst)
        {
            double gap = 15 + progress * 65;
            dc.DrawLine(Halo, new Point(origin.X - gap, origin.Y - 35), new Point(origin.X - gap, origin.Y + 35));
            dc.DrawLine(Halo, new Point(origin.X + gap, origin.Y - 35), new Point(origin.X + gap, origin.Y + 35));
            DrawZipTeeth(dc, new Point(origin.X - gap, origin.Y - 35), new Point(origin.X - gap, origin.Y + 35));
            DrawZipTeeth(dc, new Point(origin.X + gap, origin.Y - 35), new Point(origin.X + gap, origin.Y + 35));
        }
        dc.Pop();
    }
}
