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
        hopping.Set(SlimeState.Windup); hopping.StateTime = 0.1;
        var windup = Add(moves, "zip", 420, 1);
        windup.Set(SlimeState.Windup); windup.Rolling = true; windup.DashDirection = 1; windup.Facing = 1; windup.StateTime = 0.9; windup.RollDistance = data.Enemy("zip").Roll!.Distance;
        var pressed = Add(moves, "zip", 800);
        pressed.Shielded = true; pressed.ShieldTag = 1; pressed.PressWindow = 1; pressed.Pins = 1;
        var bomb = Add(moves, "zip", 1060, 2);
        bomb.Shielded = true; bomb.ShieldTag = 3; bomb.BombClock = data.Enemy("zip").Zip!.Bomb.Charge * 0.4; bomb.BombPulls = 1; bomb.TabX = 960; bomb.TabHeight = 60;
        var lockProp = Add(moves, "zip_packet", 760);
        lockProp.ZipProp = ZipProp.Belt; lockProp.SummonedBy = pressed.Id;
        var pullProp = Add(moves, "zip_packet", 960);
        pullProp.ZipProp = ZipProp.Tab; pullProp.SummonedBy = bomb.Id;
        var lane = SkyVisualChecks.Render(new BattleFieldView(moves) { OriginX = 0 }, w, h, "enemy-zip-roll");
        int ahead = SkyVisualChecks.Count(lane, w, h, warning, 560, 760, floor - 16, floor);
        check(ahead > 30, $"A roll winding up shows its lane ahead on the floor ({ahead} pixels)");
        int spot = SkyVisualChecks.Count(lane, w, h, gold, 720, 880, floor - 60, floor);
        check(spot > 30, $"The belt puzzle shows a gold padlock ({spot} pixels)");
        int bar = SkyVisualChecks.Count(lane, w, h, warning, 1000, 1120, 0, floor - 90);
        check(bar > 100, $"The bomb shows its filling bar ({bar} pixels)");
        int tab = SkyVisualChecks.Count(lane, w, h, gold, 920, 1000, floor - 110, floor - 20);
        check(tab > 20, $"Its zipper pull has a distinct slider and handle ({tab} pixels)");

        var puzzle = new BattleSim(data, 0, Width, new Random(9));
        for (int i = 0; i < 3; i++)
        {
            var file = Add(puzzle, "zip_packet", 230 + i * 280);
            file.ZipProp = ZipProp.File; file.ZipSymbol = i;
        }
        var puzzlePixels = SkyVisualChecks.Render(new BattleFieldView(puzzle) { OriginX = 0 }, w, h, "zip-folder-puzzle");
        check(SkyVisualChecks.Count(puzzlePixels, w, h, page, 200, 250, floor - 34, floor) > 100, "Puzzle files are visible with matching folder symbols");
        Moves(check, data, warning, gold, floor, w, h, Add);
        Debuffs(check);
    }

    // 2026-11-06: each blow's warning, one strip a stage, and the cat's two debuffs (PET_RENDER_REVIEW: enemy-zip-moves-*.png, cat-zip-*.png).
    private static void Moves(Action<bool, string> check, BattleSettings data, Color warning, Color gold, int floor, int w, int h, Func<BattleSim, string, double, int, Slime> add)
    {
        var zip = data.Enemy("zip").Zip!;
        var one = new BattleSim(data, 0, Width, new Random(1));
        var clamp = add(one, "zip", 300, 0);
        clamp.ZipAct = ZipAct.Clamp; clamp.ZipBeat = ZipBeat.Warn; clamp.ZipSeconds = 1; clamp.ZipLeft = 0.4; clamp.ZipAimX = 300;
        var tumble = add(one, "zip", 800, 0);
        tumble.Set(SlimeState.Windup); tumble.Rolling = tumble.RollBack = true; tumble.ZipAct = ZipAct.Tumble; tumble.ZipBeat = ZipBeat.Strike; tumble.DashDirection = 1; tumble.Facing = 1;
        tumble.StateTime = 0.9; tumble.RollDistance = 300; tumble.RollGoal = 1100;
        var px = SkyVisualChecks.Render(new BattleFieldView(one) { OriginX = 0 }, w, h, "enemy-zip-moves-1");
        var unmarked = new BattleSim(data, 0, Width, new Random(1));
        add(unmarked, "zip", 300, 0);
        check(!SkyVisualChecks.Render(new BattleFieldView(unmarked) { OriginX = 0 }, w, h).SequenceEqual(px), "The clamp marks its gap on the floor");
        check(SkyVisualChecks.Count(px, w, h, Color.FromRgb(0xE8, 0xB8, 0x4A), 200, 260, floor - 90, floor) > 100, "And opens its two flaps round it");
        check(SkyVisualChecks.Count(px, w, h, warning, 840, 1100, floor - 22, floor - 10) < 10, "The tumble has only its current lane, with no duplicate return line");

        var two = new BattleSim(data, 0, Width, new Random(1), seed: 3);
        var tree = add(two, "zip", 600, 1);
        tree.Set(SlimeState.Moving);
        var packet = two.AddSlime(240, data.Enemy("zip_packet"));
        packet.Set(SlimeState.Moving); packet.PacketFuse = true; packet.PacketRevealed = true; packet.PacketLeft = 1.3; packet.SummonedBy = tree.Id;
        var plain = two.AddSlime(120, data.Enemy("zip_packet"));
        plain.Set(SlimeState.Moving);
        // The tree's numbered branches, through a real step of the sim.
        tree.ZipBag.Add(ZipAct.Tree);
        tree.Cooldown = 0;
        for (int i = 0; i < 4 && tree.ZipAct == ZipAct.None; i++) { two.Step(1.0 / 60, 600, false); }
        var px2 = SkyVisualChecks.Render(new BattleFieldView(two) { OriginX = 0 }, w, h, "enemy-zip-moves-2");
        check(tree.ZipAct != ZipAct.Tree || two.ZipBlows.Count >= 3, $"The tree marks its branches ({two.ZipBlows.Count})");
        check(SkyVisualChecks.Count(px2, w, h, Color.FromRgb(0xE5, 0x3E, 0x3E), 215, 265, floor - 40, floor) > 15, "The live packet is red and tied up");
        check(SkyVisualChecks.Count(px2, w, h, Color.FromRgb(0xFF, 0xFB, 0xF2), 95, 145, floor - 40, floor) > 60, "A plain one is a bundle of pages");

        int variant = 0;
        foreach (var branch in two.ZipBlows.Take(3))
        {
            branch.Variant = variant; branch.X = 430 + variant++ * 240;
            branch.Radius = 60; branch.Top = 120; branch.Done = true;
        }
        var trees = SkyVisualChecks.Render(new BattleFieldView(two) { OriginX = 0 }, w, h, "zip-folder-tree-variants");
        check(variant == 3 && !trees.SequenceEqual(px2), "Three directory tree silhouettes render inside their announced volumes");
        var three = new BattleSim(data, 0, Width, new Random(1));
        var flow = add(three, "zip", 600, 2);
        flow.ZipAct = ZipAct.Overflow; flow.ZipBeat = ZipBeat.Strike; flow.ZipSeconds = 2; flow.ZipLeft = 1; flow.ZipAimX = 600; flow.ZipTear = 0.5;
        var px3 = SkyVisualChecks.Render(new BattleFieldView(three) { OriginX = 0 }, w, h, "enemy-zip-moves-3");
        check(SkyVisualChecks.Count(px3, w, h, Color.FromRgb(0xFF, 0xFB, 0xF2), 0, 200, floor - 60, floor, 24, 100) > 200, "The overflow spills in from the edges");
        check(SkyVisualChecks.Count(px3, w, h, gold, 460, 740, floor - 60, floor) > 30, "Its safe gap shows in gold");

        var four = new BattleSim(data, 0, Width, new Random(1));
        var tear = add(four, "zip", 600, 2);
        tear.ZipAct = ZipAct.Tear; tear.ZipBeat = ZipBeat.Strike; tear.ZipSeconds = 1.4; tear.ZipLeft = 0.7; tear.ZipTear = 0.7;
        var px4 = SkyVisualChecks.Render(new BattleFieldView(four) { OriginX = 0 }, w, h, "enemy-zip-moves-4");
        double reach = zip.Move(ZipAct.Tear)!.Spacing * 0.7;
        int left = SkyVisualChecks.Count(px4, w, h, Color.FromRgb(0xE8, 0xB8, 0x4A), (int)(600 - reach - 70), (int)(600 - reach + 10), floor - 110, floor);
        check(left > 300, $"The torn body flies out in two halves ({left} pixels)");
        check(SkyVisualChecks.Count(px4, w, h, Color.FromRgb(0xE8, 0xB8, 0x4A), 570, 630, floor - 110, floor) < 30, "Leaving only its zipper in the middle");
    }

    private static void Debuffs(Action<bool, string> check)
    {
        static byte[] Cat(int zip, double squeeze, string name, int clicks = 0, int swings = 0)
        {
            var pet = new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1, BattleZip = zip, BattleSqueeze = squeeze, BattleZipLook = 1, BattleZipClicks = clicks, BattleZipSwings = swings, BattleZipText = "Nén!" };
            return SkyVisualChecks.Render(new PetRenderer(pet), 192, 192, name);
        }
        var plain = Cat(0, 1, "cat-zip-plain");
        var squeezed = Cat(1, 0.75, "cat-zip-squeezed");
        var locked = Cat(2, 1, "cat-zip-locked", 1, 2);
        var gold = Color.FromRgb(0xFF, 0xC8, 0x3A);
        check(!plain.SequenceEqual(squeezed), "Squeezed, the cat looks different");
        check(SkyVisualChecks.Count(locked, 192, 192, gold, 0, 192, 0, 192) > SkyVisualChecks.Count(plain, 192, 192, gold, 0, 192, 0, 192) + 100, "Locked, it wears the gold shell and padlock");
    }
}
