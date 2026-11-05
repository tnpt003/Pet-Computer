using System.Windows.Media;
using DesktopPet;

// 1.5 M17, redrawn 2026-11-05, with the shipped Zip Slime: its three stages side by side (no crown; belted and
// small, a fat folder with a page poking out, a bloated bag with its zipper wide open) and a roped helper. A second
// strip: a leap's landing ring, a roll's lane, the press riddle's padlock and gold spot, the bomb's bar and tab.
// PET_RENDER_REVIEW=<folder> saves the strips (enemy-zip.png, enemy-zip-roll.png) on dark and light backgrounds.
internal static class ZipVisualChecks
{
    const double Width = 1200;

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var teeth = Color.FromRgb(0x8C, 0x6A, 0x2E);
        var crown = Color.FromRgb(0xFF, 0xC8, 0x3A);
        var rope = Color.FromRgb(0x8E, 0xD8, 0x6E);
        var page = Color.FromRgb(0xFF, 0xFB, 0xF2);
        var inside = Color.FromRgb(0x4A, 0x36, 0x1C);
        var warning = Color.FromRgb(0xE5, 0x3E, 0x3E);
        var gold = Color.FromRgb(0xFF, 0xC8, 0x3A);
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight, floor = h - 2;

        var sim = new BattleSim(data, 0, Width, new Random(1));
        Slime Add(BattleSim on, string kind, double x, int stage = 0)
        {
            var s = on.AddSlime(x, data.Enemy(kind));
            s.Set(SlimeState.Moving); s.StateTime = 0.4; s.Facing = -1;
            if (s.Type.Zip is ZipSpec zip)
            {
                s.ZipStage = stage;
                double scale = zip.Stages[stage].Scale;
                s.ZipScale = scale;
                s.Width = data.SlimeWidth * scale; s.HitWidth = data.SlimeWidth * scale; s.Height = data.SlimeHeight * scale;
                s.Range = zip.Stages[stage].Range;
            }
            return s;
        }
        var small = Add(sim, "zip", 130);
        var middle = Add(sim, "zip", 520, 1);
        var helper = Add(sim, "slime", 340);
        helper.RopedTo = middle.Id;
        var big = Add(sim, "zip", 960, 2);
        var px = SkyVisualChecks.Render(new BattleFieldView(sim) { OriginX = 0 }, w, h, "enemy-zip");

        int belts = SkyVisualChecks.Count(px, w, h, teeth, 100, 160, floor - 50, floor);
        check(belts > 15, $"Small, it is belted shut round a zipper ({belts} pixels)");
        check(small.Width < middle.Width && middle.Width < big.Width, "Each stage is drawn bigger");
        int goo = SkyVisualChecks.Count(px, w, h, rope, 380, 450, floor - 60, floor);
        check(goo > 15, $"Its roped helper hangs on a goo rope to it ({goo} rope pixels)");
        int pagesMiddle = SkyVisualChecks.Count(px, w, h, page, 480, 560, floor - 40, floor);
        check(pagesMiddle > 12, $"The fat folder has a page poking out ({pagesMiddle} page pixels)");
        int gap = SkyVisualChecks.Count(px, w, h, inside, 900, 1020, floor - 50, floor);
        check(gap > 300, $"The bloated bag's zipper gapes open ({gap} pixels)");
        int crowns = SkyVisualChecks.Count(px, w, h, crown, 880, 1040, 0, floor - 125) + SkyVisualChecks.Count(px, w, h, crown, 90, 170, 0, floor - 42);
        check(crowns == 0, $"It wears no crown ({crowns} crown pixels)");

        var moves = new BattleSim(data, 0, Width, new Random(1));
        var hopping = Add(moves, "zip", 90);
        hopping.Set(SlimeState.Windup); hopping.Hopping = true; hopping.HopFrom = 90; hopping.HopTo = 240; hopping.StateTime = 0.1;
        var windup = Add(moves, "zip", 420, 1);
        windup.Set(SlimeState.Windup); windup.Rolling = true; windup.DashDirection = 1; windup.Facing = 1; windup.StateTime = 0.9; windup.RollDistance = data.Enemy("zip").Roll!.Distance;
        var pressed = Add(moves, "zip", 800);
        pressed.Shielded = true; pressed.ShieldTag = 1; pressed.PressWindow = 1; pressed.Pins = 1;
        var bomb = Add(moves, "zip", 1060, 2);
        bomb.Shielded = true; bomb.ShieldTag = 3; bomb.BombClock = data.Enemy("zip").Zip!.Bomb.Charge * 0.4; bomb.BombPulls = 1; bomb.TabX = 960; bomb.TabHeight = 60;
        var lane = SkyVisualChecks.Render(new BattleFieldView(moves) { OriginX = 0 }, w, h, "enemy-zip-roll");
        int landing = SkyVisualChecks.Count(lane, w, h, warning, 190, 300, floor - 16, floor);
        check(landing > 15, $"A leap shows its landing ring where it will land ({landing} pixels)");
        int ahead = SkyVisualChecks.Count(lane, w, h, warning, 560, 760, floor - 16, floor);
        check(ahead > 30, $"A roll winding up shows its lane ahead on the floor ({ahead} pixels)");
        int spot = SkyVisualChecks.Count(lane, w, h, gold, 720, 880, floor - 60, floor);
        check(spot > 30, $"The press riddle shows a gold spot to drop the cat on ({spot} pixels)");
        int bar = SkyVisualChecks.Count(lane, w, h, warning, 1000, 1120, 0, floor - 90);
        check(bar > 100, $"The bomb shows its filling bar ({bar} pixels)");
        int tab = SkyVisualChecks.Count(lane, w, h, gold, 920, 1000, floor - 110, floor - 20);
        check(tab > 20, $"And its zipper tab, ringed gold ({tab} pixels)");
    }
}
