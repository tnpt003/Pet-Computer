using System.Windows.Media;
using System.Windows;
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
        OverflowScreenChecks(check, data);
        var burstSim = new BattleSim(data, 0, 600, seed: 1);
        burstSim.SetSky(600);
        var bursting = burstSim.AddSlime(300, data.Enemy("zip"));
        bursting.Shielded = true; bursting.BombClock = 1; bursting.ZipStage = 2;
        var burstPreview = new ZipCanopyPreview(burstSim);
        var charge = SkyVisualChecks.Render(burstPreview, 600, 640, "zip-final-burst-charge");
        bursting.BombClock = 0; bursting.ZipBurstLeft = 0.65;
        var expanding = SkyVisualChecks.Render(burstPreview, 600, 640, "zip-final-burst-expanding");
        bursting.ZipBurstLeft = 0.25;
        var expanded = SkyVisualChecks.Render(burstPreview, 600, 640, "zip-final-burst-screen");
        check(BattleFieldView.ZipAirBusy(burstSim) && !charge.SequenceEqual(expanding) && !expanding.SequenceEqual(expanded),
            "Final archive charge and expanding screen-wide file burst have distinct animation frames");
        var canopySim = new BattleSim(data, 0, 600, seed: 1);
        var canopy = new ZipBlow { Kind = ZipBlowKind.Canopy, X = 300, Radius = 50, Top = 600, Warn = 1, Left = 0.5, Life = 0.8 };
        ((ICollection<ZipBlow>)canopySim.ZipBlows).Add(canopy);
        var preview = new ZipCanopyPreview(canopySim);
        var canopyWarning = SkyVisualChecks.Render(preview, 600, 640, "zip-final-tree-warning");
        canopy.Done = true;
        var canopyActive = SkyVisualChecks.Render(preview, 600, 640, "zip-final-tree-active");
        check(BattleFieldView.ZipAirBusy(canopySim) && !canopyWarning.SequenceEqual(canopyActive), "Final tree renders full-height warning and active hierarchy in the sky overlay");
        check(SkyVisualChecks.Count(canopyActive, 600, 640, Color.FromRgb(0xE8, 0xB8, 0x4A), 250, 350, 30, 200) > 30,
            "Directory branches remain visible near the top of the screen");
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
        windup.ZipAct = ZipAct.Tumble; windup.Set(SlimeState.Windup); windup.Rolling = true; windup.DashDirection = 1; windup.Facing = 1; windup.StateTime = 0.9; windup.RollDistance = data.Enemy("zip").Roll!.Distance;
        windup.RollGoal = windup.X + windup.RollDistance;
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
        BeltAnimation(check, data);
        var layout = new BattleSim(data, 0, Width, seed: 9);
        var layoutBoss = Add(layout, "zip", 600, 1);
        layoutBoss.PhasesDone = 1; layoutBoss.Hp = layoutBoss.MaxHp * 0.5;
        layout.Damage(layoutBoss, 100000, DamageSource.App);
        for (int i = 0; i < 6; i++) layout.Step(1.0 / 60, 80, false);
        SkyVisualChecks.Render(new BattleFieldView(layout) { OriginX = 0 }, w, h, "zip-phase2-clear-boss");
        check(layout.Slimes.Where(s => s.ZipProp == ZipProp.File && s.Targetable).All(s => Math.Abs(s.X - layoutBoss.X) > layoutBoss.Width / 2 + 32 && Math.Abs(layout.ZipFileGoal(s) - layoutBoss.X) > layoutBoss.Width / 2 + 32), "Rendered phase-two files and folders stay clear of the boss");
        var styles = Enum.GetValues<ZipVisual>();
        var fingerprints = styles.Select(style => SkyVisualChecks.Render(new ZipImpactPreview(style), 200, 120)).ToArray();
        check(fingerprints.Select(Convert.ToBase64String).Distinct().Count() == styles.Length, "Each ZIP attack has a distinct impact silhouette");
        SkyVisualChecks.Render(new ZipImpactPreview(), 600, 480, "zip-unique-attacks");
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
        check(SkyVisualChecks.Count(px, w, h, warning, 840, 1100, floor - 24, floor - 16) < 10, "The tumble has only its current lane, with no duplicate return line");

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
        three.SetSky(h - 4);
        var px3 = SkyVisualChecks.Render(new ZipCanopyPreview(three, h - 2), w, h, "enemy-zip-moves-3");
        int incomingPaper = SkyVisualChecks.Count(px3, w, h, Color.FromRgb(0xFF, 0xFB, 0xF2), 0, 200, floor - 60, floor, 24, 100);
        check(incomingPaper > 80, $"A folded file appears in the incoming paper wall ({incomingPaper} pixels)");
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

    private static void OverflowScreenChecks(Action<bool, string> check, BattleSettings data)
    {
        var sim = new BattleSim(data, 0, 900, seed: 2);
        sim.SetSky(600);
        var boss = sim.AddSlime(150, data.Enemy("zip"));
        boss.Set(SlimeState.Moving); boss.ZipStage = 2;
        boss.Width = data.SlimeWidth * 2.9; boss.Height = data.SlimeHeight * 2.9; boss.ZipScale = 2.9;
        boss.ZipAct = ZipAct.Overflow; boss.ZipBeat = ZipBeat.Warn;
        boss.ZipSeconds = 1; boss.ZipLeft = 0.5; boss.ZipAimX = 600;
        var scene = new System.Windows.Controls.Canvas { Width = 900, Height = 640 };
        var field = new BattleFieldView(sim) { Width = 900, Height = BattleFieldView.FieldHeight };
        System.Windows.Controls.Canvas.SetTop(field, 620 - (BattleFieldView.FieldHeight - 2));
        scene.Children.Add(field);
        var overlay = new ZipCanopyPreview(sim) { Width = 900, Height = 640 };
        scene.Children.Add(overlay);
        var warn = SkyVisualChecks.Render(scene, 900, 640, "zip-overflow-full-warning");
        boss.ZipBeat = ZipBeat.Strike; boss.ZipSeconds = 2.2; boss.ZipLeft = 1.1; boss.ZipTear = 0.5;
        field.InvalidateVisual(); overlay.InvalidateVisual();
        var filling = SkyVisualChecks.Render(scene, 900, 640, "zip-overflow-full-filling");
        boss.ZipTear = 1;
        field.InvalidateVisual(); overlay.InvalidateVisual();
        var filled = SkyVisualChecks.Render(scene, 900, 640, "zip-overflow-full-peak");
        check(BattleFieldView.ZipAirBusy(sim) && !warn.SequenceEqual(filling) && !filling.SequenceEqual(filled),
            "Overflow renders in the full-screen overlay with warning, filling and peak frames");
        var gold = Color.FromRgb(0xFF, 0xC8, 0x3A);
        check(SkyVisualChecks.Count(filled, 900, 640, gold, 500, 510, 25, 160) > 50
            && SkyVisualChecks.Count(filled, 900, 640, gold, 690, 700, 300, 450) > 50,
            "Both gold corridor rails extend through the full screen height");
        var paper = Color.FromRgb(0xFF, 0xFB, 0xF2);
        check(SkyVisualChecks.Count(filled, 900, 640, paper, 50, 450, 30, 180, 24, 100) > 100,
            "Unpacking files are visible in the danger area near the screen top");
        check(SkyVisualChecks.Count(filled, 900, 640, paper, 520, 680, 30, 450, 24, 100) == 0,
            "The announced 190-DIP safe corridor stays clear of dangerous paper effects");
    }

    private static void BeltAnimation(Action<bool, string> check, BattleSettings data)
    {
        var sim = new BattleSim(data, 0, Width, seed: 1);
        var boss = sim.AddSlime(600, data.Enemy("zip"));
        boss.Set(SlimeState.Moving); boss.Shielded = true; boss.ShieldTag = 1;
        var belt = new ZipBlow { OwnerId = boss.Id, Kind = ZipBlowKind.Belt, FromX = 600, ToX = 65, X = 332.5, Radius = 267.5, Top = 65, Travel = 0.35, Warn = 0.8, Left = 0.4, Life = 0.55 };
        ((ICollection<ZipBlow>)sim.ZipBlows).Add(belt);
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        var view = new BattleFieldView(sim);
        SkyVisualChecks.Render(view, w, h, "zip-belt-charge");
        belt.Done = true; belt.Left = 0; belt.Elapsed = 0.1;
        var first = SkyVisualChecks.Render(view, w, h, "zip-belt-front-1");
        check(SkyVisualChecks.Count(first, w, h, Color.FromRgb(0xFF, 0xD3, 0x5C), 480, 550, h - 17, h - 10) > 100, "The moving belt has a visible gold ribbon, independent of partial-class static initialization order");
        belt.Elapsed = 0.2;
        var second = SkyVisualChecks.Render(view, w, h, "zip-belt-front-2");
        check(!first.SequenceEqual(second), "The belt front visibly travels from the boss instead of filling its entire lane instantly");
        check(Math.Abs(belt.Front - (600 - 535 * 0.2 / 0.35)) < 0.01, "Renderer and combat read the same moving belt front");
        belt.Elapsed = 0.45;
        SkyVisualChecks.Render(view, w, h, "zip-belt-return");
    }

    private static void Debuffs(Action<bool, string> check)
    {
        static byte[] Cat(int zip, double squeeze, string name, int clicks = 0, int swings = 0, double strike = -1, double flash = 0)
        {
            var pet = new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1, BattleZip = zip, BattleSqueeze = squeeze, BattleZipLook = 1, BattleZipClicks = clicks, BattleZipSwings = swings, BattleZipStrike = strike, BattleZipHitFlash = flash, BattleZipText = "Nén!" };
            return SkyVisualChecks.Render(new PetRenderer(pet), 192, 192, name);
        }
        var plain = Cat(0, 1, "cat-zip-plain");
        var squeezed = Cat(1, 0.75, "cat-zip-squeezed");
        var locked = Cat(2, 1, "cat-zip-locked", 1, 2);
        var struck = Cat(2, 1, "cat-zip-self-hit", 1, 3, 0.45, 0.15);
        check(!locked.SequenceEqual(struck), "Self-directed swing visibly strikes and cracks the padlock");
        var gold = Color.FromRgb(0xFF, 0xC8, 0x3A);
        check(!plain.SequenceEqual(squeezed), "Squeezed, the cat looks different");
        check(SkyVisualChecks.Count(locked, 192, 192, gold, 0, 192, 0, 192) > SkyVisualChecks.Count(plain, 192, 192, gold, 0, 192, 0, 192) + 100, "Locked, it wears the gold shell and padlock");
    }
}

internal sealed class ZipCanopyPreview(BattleSim sim, double floor = 620) : FrameworkElement
{
    protected override void OnRender(DrawingContext dc) => BattleFieldView.DrawZipAir(dc, sim, (x, h) => new Point(x, floor - h), new Point(100, 400), 0);
}

internal sealed class ZipImpactPreview : FrameworkElement
{
    private readonly ZipVisual? single;
    public ZipImpactPreview(ZipVisual? style = null) { single = style; }
    protected override void OnRender(DrawingContext dc)
    {
        var styles = single is ZipVisual style ? new[] { style } : Enum.GetValues<ZipVisual>();
        for (int i = 0; i < styles.Length; i++)
        {
            double x = single != null ? 100 : 100 + i % 3 * 200;
            double y = single != null ? 100 : 100 + i / 3 * 120;
            BattleFieldView.DrawZipImpact(dc, styles[i], x, y, 55, 0.45);
            if (single == null)
            {
                var label = new FormattedText(styles[i].ToString(), System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 13, PetUi.Ink, 1);
                dc.DrawRoundedRectangle(PetUi.Surface, new Pen(PetUi.ArtInk, 1), new Rect(x - 54, y - 103, label.Width + 8, label.Height + 4), 3, 3);
                dc.DrawText(label, new Point(x - 50, y - 101));
            }
        }
    }
}
