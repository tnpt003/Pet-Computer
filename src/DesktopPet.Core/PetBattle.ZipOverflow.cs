using System;
using System.Collections.Generic;

namespace DesktopPet;

public sealed partial class BattleSim
{
    // Leave enough room even while the stage-three body is still growing; includes the swing's contact tolerance.
    private double OverflowReach(Slime boss)
    {
        double scale = StageOf(boss)!.Scale;
        double hitWidth = Math.Max(boss.HitWidth, Settings.SlimeWidth * scale * boss.Type.Hitbox / boss.Type.Scale);
        return Math.Max(0, arms.Range + (hitWidth - Settings.SlimeWidth) / 2) + 14 + CatHalfWidth + 16;
    }

    private bool TryOverflowLayout(Slime boss, ZipMoveSpec move, double catX, out double gap, out double goal, bool choose = false)
    {
        gap = goal = 0;
        double width = Math.Max(boss.Width, Settings.SlimeWidth * StageOf(boss)!.Scale);
        double min = Left + width / 2, max = Right - width / 2;
        double margin = move.Radius + 40;
        if (max <= min || Right - Left < margin * 2) return false;
        double travel = move.Speed * move.Warn;
        double left = Math.Clamp(boss.X - travel, min, max), right = Math.Clamp(boss.X + travel, min, max);
        var layouts = new List<(double Gap, double Goal)>();
        foreach (int side in new[] { -1, 1 })
        {
            double candidate = Math.Clamp(catX + side * (move.Radius + 125), Left + margin, Right - margin);
            double retreat = Math.Abs(left - candidate) >= Math.Abs(right - candidate) ? left : right;
            if (Math.Abs(candidate - catX) <= move.Radius + CatHalfWidth) continue;
            if (Math.Abs(retreat - candidate) - move.Radius <= OverflowReach(boss)) continue;
            layouts.Add((candidate, retreat));
        }
        if (layouts.Count == 0) return false;
        var layout = layouts[choose ? (zipRandom ??= BattleRandom.Stream(Seed, BattleRandom.Zip)).Next(layouts.Count) : 0];
        gap = layout.Gap; goal = layout.Goal;
        return true;
    }

    private bool OverflowLayoutStillFits(Slime boss, ZipMoveSpec move)
    {
        double width = Math.Max(boss.Width, Settings.SlimeWidth * StageOf(boss)!.Scale);
        return boss.ZipOverflowGoal >= Left + width / 2 && boss.ZipOverflowGoal <= Right - width / 2
            && boss.ZipAimX - move.Radius >= Left && boss.ZipAimX + move.Radius <= Right
            && Math.Abs(boss.ZipOverflowGoal - boss.ZipAimX) - move.Radius > OverflowReach(boss);
    }
}
