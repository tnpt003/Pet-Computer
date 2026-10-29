using System.Windows.Media;
using DesktopPet;

// 1.5 M17, drawn for real with the shipped Zip Slime: one with its zipper down its front; one with a roped
// helper hanging on a goo rope to it; one packed small behind its shield with straps round it and file
// shards (pages with a folded corner) on both sides. A second strip: a roll winding up (a red lane on the
// floor ahead of it, no slam ring) and one turned over halfway through its roll.
// PET_RENDER_REVIEW=<folder> saves the strips (enemy-zip.png, enemy-zip-roll.png) on dark and light backgrounds.
internal static class ZipVisualChecks
{
    const double Width = 1200;

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var teeth = Color.FromRgb(0x8C, 0x6A, 0x2E);
        var rope = Color.FromRgb(0x8E, 0xD8, 0x6E);
        var page = Color.FromRgb(0xFF, 0xFB, 0xF2);
        var warning = Color.FromRgb(0xE5, 0x3E, 0x3E);
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight, floor = h - 2;

        var sim = new BattleSim(data, 0, Width, new Random(1));
        Slime Add(BattleSim on, string kind, double x)
        {
            var s = on.AddSlime(x, data.Enemy(kind));
            s.Set(SlimeState.Moving); s.StateTime = 0.4; s.Facing = -1;
            return s;
        }
        Add(sim, "zip", 150);
        var roped = Add(sim, "zip", 520);
        var helper = Add(sim, "slime", 340);
        helper.RopedTo = roped.Id;
        var packed = Add(sim, "zip", 960);
        packed.Shielded = packed.Packed = true;
        var left = Add(sim, "zipshard", 960 - packed.Width * BattleSim.PackedScale / 2 - 40);
        var right = Add(sim, "zipshard", 960 + packed.Width * BattleSim.PackedScale / 2 + 40);
        var px = SkyVisualChecks.Render(new BattleFieldView(sim) { OriginX = 0 }, w, h, "enemy-zip");

        int zipper = SkyVisualChecks.Count(px, w, h, teeth, 130, 170, floor - 60, floor);
        check(zipper > 6, $"The Zip Slime wears a zipper down its front ({zipper} teeth pixels)");
        int goo = SkyVisualChecks.Count(px, w, h, rope, 380, 450, floor - 60, floor);
        check(goo > 15, $"Its roped helper hangs on a goo rope to it ({goo} rope pixels)");
        int straps = SkyVisualChecks.Count(px, w, h, teeth, 920, 1000, floor - 60, floor);
        check(straps > zipper, $"Packed, it has straps round it ({straps} strap and teeth pixels)");
        int pages = SkyVisualChecks.Count(px, w, h, page, (int)left.X - 12, (int)left.X + 12, floor - 30, floor)
                    + SkyVisualChecks.Count(px, w, h, page, (int)right.X - 12, (int)right.X + 12, floor - 30, floor);
        check(pages > 30, $"Its file shards hold pages on both sides ({pages} page pixels)");

        var rolls = new BattleSim(data, 0, Width, new Random(1));
        var windup = Add(rolls, "zip", 300);
        windup.Set(SlimeState.Windup); windup.Rolling = true; windup.DashDirection = 1; windup.Facing = 1; windup.StateTime = 0.9;
        var rolling = Add(rolls, "zip", 1000);
        rolling.Set(SlimeState.Dashing); rolling.Rolling = true; rolling.DashDirection = -1; rolling.DashLeft = data.Enemy("zip").Roll!.Distance / 2;
        var lane = SkyVisualChecks.Render(new BattleFieldView(rolls) { OriginX = 0 }, w, h, "enemy-zip-roll");
        int ahead = SkyVisualChecks.Count(lane, w, h, warning, 420, 700, floor - 16, floor), behind = SkyVisualChecks.Count(lane, w, h, warning, 100, 240, floor - 16, floor);
        check(ahead > 30 && behind == 0, $"A roll winding up shows its lane ahead on the floor, no slam ring ({ahead} ahead, {behind} behind)");
        check(Math.Abs(BattleSim.RollTurn(rolling)) > 90 && BattleSim.RollTurn(windup) == 0, "Halfway through its roll it has turned over; winding up, not yet");
    }
}
