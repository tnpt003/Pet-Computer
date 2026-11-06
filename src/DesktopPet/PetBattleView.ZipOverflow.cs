using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    private static void DrawZipOverflow(DrawingContext dc, BattleSim sim, Slime boss, ZipMoveSpec move,
        Func<double, double, Point> at, double time)
    {
        if (boss.ZipBeat == ZipBeat.None) return;
        double fade = boss.ZipBeat == ZipBeat.Recover ? Math.Clamp(1 - (boss.ZipSeconds - boss.ZipLeft) / 0.35, 0, 1) : 1;
        if (fade <= 0) return;
        double amount = boss.ZipBeat == ZipBeat.Strike ? boss.ZipTear : boss.ZipBeat == ZipBeat.Recover ? 1 : 0.02;
        var edges = sim.OverflowEdges(boss, move, amount);
        double floor = at(sim.Left, 0).Y, top = at(sim.Left, sim.SkyHeight).Y;
        double x0 = at(sim.Left, 0).X, x1 = at(sim.Right, 0).X;
        double gapL = at(boss.ZipAimX - move.Radius, 0).X, gapR = at(boss.ZipAimX + move.Radius, 0).X;
        var origin = at(boss.X, boss.Height * 0.7);
        dc.PushOpacity(fade);
        dc.PushOpacity(0.12);
        dc.DrawRectangle(TideFill, null, new Rect(x0, top, Math.Max(0, gapL - x0), floor - top));
        dc.DrawRectangle(TideFill, null, new Rect(gapR, top, Math.Max(0, x1 - gapR), floor - top));
        dc.Pop();
        Side(new Rect(x0, top, Math.Max(0, at(edges.Left, 0).X - x0), floor - top), true);
        Side(new Rect(at(edges.Right, 0).X, top, Math.Max(0, x1 - at(edges.Right, 0).X), floor - top), false);
        foreach (double rail in new[] { gapL, gapR })
        {
            dc.DrawLine(Halo, new Point(rail, top), new Point(rail, floor));
            dc.DrawLine(SpotPen, new Point(rail, top), new Point(rail, floor));
        }
        dc.DrawRoundedRectangle(null, SpotPen, new Rect(gapL, floor - 20, move.Radius * 2, 16), 3, 3);
        DrawFolderNode(dc, (gapL + gapR) / 2, floor - 30, 24);
        dc.Pop();

        void Side(Rect box, bool left)
        {
            if (box.Width < 1 || box.Height < 1) return;
            dc.PushClip(new RectangleGeometry(box));
            dc.PushOpacity(0.3);
            dc.DrawRectangle(TideFill, null, box);
            dc.Pop();
            double front = left ? box.Right : box.Left;
            dc.DrawLine(Halo, new Point(front, top), new Point(front, floor));
            dc.DrawLine(TideEdge, new Point(front, top), new Point(front, floor));
            for (double y = top + 10; y < floor; y += 30)
                dc.DrawLine(ZipTeeth, new Point(front - 4, y), new Point(front + 4, y));
            int rows = Math.Clamp((int)(box.Height / 100), 2, 12), cols = Math.Clamp((int)(box.Width / 150), 1, 5);
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
            {
                double x = box.Left + box.Width * (col + 0.5) / cols + Math.Sin(time * 3 + row * 2 + col) * 8;
                double y = top + box.Height * (row + 0.5) / rows;
                if ((row + col) % 3 == 0) DrawFolderNode(dc, x, y, 25);
                else ZipPage(dc, x, y, Math.Sin(time * 4 + row + col) * 12);
                dc.DrawLine(PageLine, new Point(x - 17, y + 20), new Point(x + 17, y + 20));
            }
            for (int i = 0; i < 12; i++)
            {
                double travel = (time * 1.7 + i * 0.173) % 1;
                var target = new Point(front, top + box.Height * ((i * 0.618 + 0.07) % 1));
                Point p = origin + (target - origin) * travel;
                dc.DrawLine(PageLine, p, p + (origin - p) * 0.06);
                ZipPage(dc, p.X, p.Y, travel * (left ? -100 : 100));
            }
            dc.Pop();
        }
    }
}
