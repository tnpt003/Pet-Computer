using System;
using System.Collections.Generic;

namespace DesktopPet;

public sealed partial class BattleSim
{
    private void AddZipEffect(ZipVisual style, double x, double height, double radius)
    {
        AddEffect(BattleEffectKind.Zip, x, height, radius);
        effects[^1].ZipVisual = style;
    }
    private (double X, double Height)? paperPreviousCat;
    private (double X, double Height) paperSweepFrom;

    // A swept projectile against the cat's full body, in relative coordinates. Padding is the
    // projectile radius; fast drag and bounce cannot tunnel through a stationary paper either.
    private bool PaperTouchesCat(double x0, double h0, double x1, double h1, double radius)
    {
        var cat = CatCentre();
        return PaperCrossesBody(x0 - paperSweepFrom.X, h0 - paperSweepFrom.Height,
            x1 - cat.X, h1 - cat.Height, radius);
    }

    internal static bool PaperCrossesBody(double x0, double h0, double x1, double h1, double radius)
    {
        double enter = 0, leave = 1;
        bool Clip(double start, double delta, double min, double max)
        {
            if (Math.Abs(delta) < 1e-9) return start >= min && start <= max;
            double a = (min - start) / delta, b = (max - start) / delta;
            enter = Math.Max(enter, Math.Min(a, b));
            leave = Math.Min(leave, Math.Max(a, b));
            return enter <= leave;
        }
        return Clip(x0, x1 - x0, -30 - radius, 30 + radius)
            && Clip(h0, h1 - h0, -CatFeetBelow - radius, CatHeadAbove + radius);
    }

    private bool PlaceZipFiles(Slime boss, bool compact = false)
    {
        zipRandom ??= BattleRandom.Stream(Seed, BattleRandom.Zip);
        double margin = Math.Min(120, (Right - Left) * 0.1);
        double l = Left + margin, r = Right - margin;
        double half = Math.Max(boss.Width, (StageOf(boss)?.Scale ?? 1) * Settings.SlimeWidth) / 2;
        double a = Math.Clamp(boss.X - half - 36, l, r);
        double b = Math.Clamp(boss.X + half + 36, l, r);
        var choices = new List<int>();
        double minimum = compact ? Math.Min(80, Math.Max(48, (r - b) / 3)) : 80;
        for (int n = 0; n <= 3; n++)
            if ((n == 0 || (a - l) / n >= minimum) && (n == 3 || (r - b) / (3 - n) >= minimum)) choices.Add(n);
        if (choices.Count == 0) return false;
        int leftCount = choices[zipRandom.Next(choices.Count)], symbol = zipRandom.Next(3);
        for (int side = 0; side < 2; side++)
        {
            int count = side == 0 ? leftCount : 3 - leftCount;
            double from = side == 0 ? l : b, to = side == 0 ? a : r;
            for (int i = 0; i < count; i++)
            {
                double width = (to - from) / count;
                double low = from + i * width + 18, high = from + (i + 1) * width - 18;
                double gap = Math.Min(108, high - low), spare = high - low - gap;
                double start = low + spare * zipRandom.NextDouble();
                bool rightward = zipRandom.Next(2) == 0;
                var file = AddZipProp(boss, ZipProp.File, rightward ? start : start + gap, symbol++ % 3);
                file.ZipGoalX = rightward ? start + gap : start;
                file.ZipLaneLeft = low; file.ZipLaneRight = high;
            }
        }
        boss.ZipLayoutGoal = boss.X;
        return true;
    }
}
