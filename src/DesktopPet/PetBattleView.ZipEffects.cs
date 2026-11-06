using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    private static void DrawZipTeeth(DrawingContext dc, Point start, Point end)
    {
        Vector d = end - start;
        double distance = d.Length;
        if (distance < 1) return;
        d.Normalize();
        var normal = new Vector(-d.Y, d.X);
        for (double step = 8; step < distance; step += 13)
        {
            var at = start + d * step;
            dc.DrawLine(Halo, at - normal * 3, at + normal * 3);
            dc.DrawLine(ZipTeeth, at - normal * 3, at + normal * 3);
        }
    }

    private static void ZipPage(DrawingContext dc, double x, double y, double angle, bool crumpled = false)
    {
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new RotateTransform(angle));
        if (crumpled)
        {
            dc.DrawGeometry(PageFill, Outline, CrumpledPaper);
            dc.DrawLine(PageLine, new Point(-6, -3), new Point(4, 5));
            dc.DrawLine(PageLine, new Point(4, 5), new Point(6, -5));
        }
        else
        {
            dc.DrawGeometry(PageFill, Outline, PaperShot);
            dc.DrawLine(PageLine, new Point(-4, -3), new Point(4, -3));
            dc.DrawLine(PageLine, new Point(-4, 2), new Point(4, 2));
        }
        dc.Pop(); dc.Pop();
    }

    private static readonly Geometry CrumpledPaper = Parse("M -10,-3 L -5,-10 L 2,-6 L 9,-8 L 11,2 L 5,9 L -3,6 L -9,9 Z");

    // Shape and extent communicate the hit, while folded paper, straps and zipper teeth distinguish moves.
    private static void DrawZipTarget(DrawingContext dc, ZipVisual style, double x, double y, double radius, double height, double fill)
    {
        var box = new Rect(x - radius, y - height, Math.Max(1, radius * 2), Math.Max(1, height));
        dc.DrawRoundedRectangle(MarkFill, Halo, box, 3, 3);
        dc.DrawRoundedRectangle(null, TacticEdge, box, 3, 3);
        if (style == ZipVisual.Tree)
        {
            dc.PushOpacity(0.25 + fill * 0.4);
            DrawFolderTree(dc, 2, radius, x, y, height);
            dc.Pop();
        }
        else if (style == ZipVisual.Belt || style == ZipVisual.Clamp)
        {
            double yy = y - height / 2;
            dc.DrawLine(StrapPen, new Point(box.Left, yy), new Point(box.Right, yy));
            for (double px = box.Left + 6; px < box.Right; px += 20)
                dc.DrawLine(WarningPen, new Point(px, yy - 5), new Point(px + 6, yy + 5));
            if (style == ZipVisual.Clamp)
                foreach (int side in new[] { -1, 1 })
                    dc.DrawRoundedRectangle(FlapFill, Outline, new Rect(x + side * radius - 4, y - height, 8, height), 2, 2);
        }
        else
        {
            // Corrupt archive uses folded corners; overwrite uses a rectangular stamp plunger.
            double top = y - height;
            dc.DrawLine(Outline, new Point(box.Right - 14, top), new Point(box.Right - 14, top + 14));
            dc.DrawLine(Outline, new Point(box.Right - 14, top + 14), new Point(box.Right, top + 14));
            if (style == ZipVisual.Overwrite)
            {
                dc.DrawRoundedRectangle(ZipTab, Outline, new Rect(x - 6, top - 17 + fill * 10, 12, 16), 3, 3);
                dc.DrawRectangle(FlapFill, Outline, new Rect(x - 18, top - 4 + fill * 10, 36, 6));
            }
            else
                for (int i = 0; i < 3; i++)
                    dc.DrawLine(WarningPen, new Point(x - 13 + i * 9, y - height * 0.7), new Point(x - 9 + i * 9, y - height * 0.25));
        }
    }

    private void DrawZipTape(DrawingContext dc, Slime slime, double floor)
    {
        if (!slime.Rolling || slime.State is not (SlimeState.Windup or SlimeState.Dashing)) return;
        double left = Math.Min(slime.X, slime.RollGoal) - OriginX;
        double width = Math.Max(1, Math.Abs(slime.X - slime.RollGoal));
        dc.DrawRoundedRectangle(FlapFill, Halo, new Rect(left, floor - 12, width, 9), 2, 2);
        dc.DrawRoundedRectangle(null, WarningPen, new Rect(left, floor - 12, width, 9), 2, 2);
        for (double x = left + 8; x < left + width - 6; x += 24)
            dc.DrawLine(ZipTrack, new Point(x, floor - 11), new Point(x + 8, floor - 4));
        if (slime.State == SlimeState.Dashing) ZipPage(dc, slime.X - OriginX - slime.DashDirection * 30, floor - 20, slime.StateTime * 180);
    }

    private static void DrawRepulseWarning(DrawingContext dc, double x, double floor, int side, double fill)
    {
        dc.DrawRoundedRectangle(FlapFill, Halo, new Rect(x - 14, floor - 48, 28, 30), 4, 4);
        for (int i = 0; i < 3; i++)
        {
            double xx = x + side * (30 + i * 22 + fill * 10);
            ZipPage(dc, xx, floor - 30 - i * 5, side * 15);
            dc.DrawLine(WarningPen, new Point(xx - side * 12, floor - 12), new Point(xx, floor - 12));
        }
    }

    internal static void DrawZipImpact(DrawingContext dc, ZipVisual style, double x, double y, double radius, double progress)
    {
        double k = Math.Clamp(progress, 0, 1), reach = Math.Max(18, Math.Abs(radius));
        if (style == ZipVisual.Belt)
        {
            dc.DrawLine(BeltEdge, new Point(x - reach, y - 24), new Point(x, y - 24));
            dc.DrawLine(BeltRibbon, new Point(x - reach, y - 24), new Point(x, y - 24));
            dc.DrawRoundedRectangle(FlapFill, Outline, new Rect(x - 10, y - 65, 20, 65), 4, 4);
        }
        else if (style == ZipVisual.Clamp)
        {
            double gap = style == ZipVisual.Clamp ? 18 * (1 - k) : reach;
            foreach (int side in new[] { -1, 1 })
            {
                dc.DrawRoundedRectangle(FlapFill, Outline, new Rect(x + side * gap - 10, y - 64, 20, 60), 3, 3);
                dc.DrawLine(StrapPen, new Point(x, y - 25), new Point(x + side * gap, y - 25));
            }
        }
        else if (style == ZipVisual.Tumble)
        {
            dc.DrawRoundedRectangle(FlapFill, Outline, new Rect(x - 65, y - 12, 65, 9), 2, 2);
            dc.DrawEllipse(FlapFill, Outline, new Point(x, y - 18), 17, 17);
            dc.DrawEllipse(PageFill, Outline, new Point(x, y - 18), 8, 8);
            ZipPage(dc, x - 40 - k * 10, y - 32, k * 160);
        }
        else if (style == ZipVisual.Overwrite)
        {
            dc.DrawRectangle(PageFill, Outline, new Rect(x - reach, y - 55, reach * 2, 50));
            dc.DrawRoundedRectangle(null, WarningPen, new Rect(x - reach + 4, y - 46, Math.Max(1, reach * 2 - 8), 28), 2, 2);
            dc.DrawLine(WarningPen, new Point(x - 9, y - 35), new Point(x + 9, y - 23));
        }
        else if (style == ZipVisual.Tree)
            DrawFolderTree(dc, 1, reach, x, y, 100);
        else if (style is ZipVisual.Bolt or ZipVisual.Tear)
        {
            for (int i = -3; i <= 3; i++)
            {
                double xx = x + i * (7 + 8 * k), yy = y - 14 - (3 - Math.Abs(i)) * 8 * k;
                dc.DrawRoundedRectangle(ZipTab, Outline, new Rect(xx, yy, 7, 5), 1, 1);
                if (style == ZipVisual.Tear) dc.DrawLine(PageLine, new Point(xx, yy), new Point(xx + 9, yy - 13));
            }
        }
        else
        {
            int count = style == ZipVisual.Burst ? 14 : style == ZipVisual.Overflow ? 9 : 5;
            for (int i = 0; i < count; i++)
            {
                double fraction = count > 1 ? i / (count - 1.0) : 0.5;
                double xx = x + (fraction * 2 - 1) * Math.Min(reach, 680) * (0.35 + 0.65 * k);
                double yy = y - 10 - (18 + 30 * Math.Sin(i * 2.3)) * Math.Sin(Math.PI * k);
                if (style == ZipVisual.Repulse) { xx = x + Math.Sign(radius) * (25 + i * 22) * (1 + k); yy = y - i * 5; }
                if (style == ZipVisual.Overflow) yy -= (i % 3) * 12;
                ZipPage(dc, xx, yy, style == ZipVisual.Overflow ? -8 : i * 39 + k * 100, style == ZipVisual.Chase);
                if (style == ZipVisual.Faulty)
                    dc.DrawLine(WarningPen, new Point(xx - 8, yy - 8), new Point(xx + 8, yy + 8));
            }
            if (style == ZipVisual.Burst)
                foreach (int side in new[] { -1, 1 })
                    dc.DrawLine(StrapPen, new Point(x, y - 24), new Point(x + side * 75 * k, y - 55));
        }
    }
}
