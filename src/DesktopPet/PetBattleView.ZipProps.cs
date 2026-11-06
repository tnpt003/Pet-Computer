using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    private void DrawZipProp(DrawingContext dc, Slime prop, double x, double floor)
    {
        if (!prop.Targetable) return;
        if (prop.ZipProp == ZipProp.File)
        {
            double goal = sim.ZipFileGoal(prop) - OriginX;
            DrawFolderNode(dc, goal, floor - 20, 32);
            DrawZipSymbol(dc, prop.ZipSymbol, goal, floor - 21);
            dc.DrawRoundedRectangle(PageFill, Outline, new Rect(x - 17, floor - 34, 34, 34), 3, 3);
            dc.DrawLine(PageLine, new Point(x - 10, floor - 8), new Point(x + 10, floor - 8));
            DrawZipSymbol(dc, prop.ZipSymbol, x, floor - 23);
            return;
        }
        bool active = sim.ZipPropActive(prop);
        dc.PushOpacity(active ? 1 : 0.4);
        dc.PushTransform(new TranslateTransform(x, floor - 20));
        if (prop.ZipProp == ZipProp.Belt)
        {
            dc.DrawRoundedRectangle(null, Halo, new Rect(-11, -25, 22, 26), 11, 11);
            dc.DrawRoundedRectangle(null, Shackle, new Rect(-11, -25, 22, 26), 11, 11);
            dc.DrawRoundedRectangle(FlapFill, Outline, new Rect(-20, -8, 40, 29), 5, 5);
            dc.DrawEllipse(ZipInside, null, new Point(0, 4), 4, 4);
            dc.DrawRectangle(ZipInside, null, new Rect(-2, 4, 4, 9));
        }
        else
        {
            // Metal zipper pull: closed loop, neck and a wider slider base.
            dc.DrawRoundedRectangle(ZipTab, Outline, new Rect(-13, -27, 26, 40), 8, 8);
            dc.DrawRoundedRectangle(ZipInside, Outline, new Rect(-6, -20, 12, 17), 5, 5);
            dc.DrawRoundedRectangle(FlapFill, Outline, new Rect(-18, 12, 36, 12), 4, 4);
        }
        dc.Pop(); dc.Pop();
    }

    private static void DrawZipSymbol(DrawingContext dc, int symbol, double x, double y)
    {
        if (symbol % 3 == 0) dc.DrawEllipse(Game.Warning, Outline, new Point(x, y), 5, 5);
        else if (symbol % 3 == 1) dc.DrawRectangle(Game.Warning, Outline, new Rect(x - 5, y - 5, 10, 10));
        else
        {
            dc.DrawLine(Outline, new Point(x - 5, y - 5), new Point(x + 5, y + 5));
            dc.DrawLine(Outline, new Point(x + 5, y - 5), new Point(x - 5, y + 5));
        }
    }

    private static void DrawFolderNode(DrawingContext dc, double x, double y, double width)
    {
        dc.DrawRoundedRectangle(FlapFill, Outline, new Rect(x - width / 2, y - 10, width * 0.46, 8), 2, 2);
        dc.DrawRoundedRectangle(FlapFill, Outline, new Rect(x - width / 2, y - 6, width, 16), 3, 3);
    }

    private static void DrawFolderTree(DrawingContext dc, ZipBlow b, double x, double y, double high) =>
        DrawFolderTree(dc, b.Variant, b.Radius, x, y, high);

    private static void DrawFolderTree(DrawingContext dc, int variant, double radius, double x, double y, double high)
    {
        // Three recognizable directory hierarchies; all remain inside the announced damage box.
        double spread = Math.Max(8, radius - 13), rootY = y - high + 12;
        DrawFolderNode(dc, x, rootY, 24);
        for (int i = 0; i < 3; i++)
        {
            double nx = variant == 0 ? x + (i - 1) * spread : variant == 1 ? x + (i % 2 == 0 ? -1 : 1) * spread * 0.65 : x + (i - 1) * spread * 0.8;
            double ny = variant == 2 ? y - 20 : rootY + (high - 32) * (i + 1) / 3;
            double join = variant == 2 ? rootY + 22 : ny - 12;
            dc.DrawLine(Halo, new Point(x, rootY + 8), new Point(x, join));
            dc.DrawLine(ZipSeam, new Point(x, rootY + 8), new Point(x, join));
            dc.DrawLine(ZipSeam, new Point(x, join), new Point(nx, join));
            dc.DrawLine(ZipSeam, new Point(nx, join), new Point(nx, ny));
            DrawFolderNode(dc, nx, ny, 22);
        }
    }
}
