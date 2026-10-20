using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// Enemy Director milestone 2, drawn for real: the herb slime's leaf cap (no "+" on its body), its
// dull leaf once its heals are spent and its teleport warning; the rock's armour on the side it
// faces; the "in the way" ring; the boss's glowing crown while it rallies; the arrow before a
// spawn from behind. Milestone 3: an elite's gold rim and star, the rare cards' icons.
// PET_RENDER_REVIEW=<folder> also saves the pictures on dark and light floors.
internal static class EnemyVisualChecks
{
    const double Width = 600;

    private static EnemyType Kind(string id, string marks, double scale, string color, bool support = false, bool boss = false) => new()
    {
        Id = id, MaxHp = 100, Speed = 0, Damage = 0, Range = 60, Windup = 1, AttackCooldown = 2, Recover = 0.4, Scale = scale, Color = color, Marks = marks,
        Support = support, HealAmount = support ? 10 : 0, HealBudget = 90, TeleportBelow = support ? 0.35 : 0, Boss = boss,
        Blocker = marks == "cracks", HitScale = marks == "cracks" ? scale * 1.12 : null,
        Phases = boss ? new[] { new BossPhase { At = 0.5, Shield = true, Summon = "slime", Count = 1 } } : null, RallyEvery = boss ? 5 : 0
    };

    private static readonly EnemyType[] Kinds =
    {
        Kind("slime", "none", 1, "#7CD36A"), Kind("mender", "leaf", 0.9, "#4FC9A8", support: true), Kind("cross", "cross", 0.9, "#4FC9A8", support: true),
        Kind("rock", "cracks", 1.35, "#A8A095"), Kind("king", "none", 2, "#9B7BE0", boss: true)
    };

    // A field with these enemies placed by hand (spawned, then moved), standing still.
    private static (BattleSim Sim, BattleFieldView View) Field(params (string Kind, double X)[] enemies)
    {
        var plan = new[] { new WaveSpec { Groups = enemies.Select(e => new WaveGroup { Enemy = e.Kind }).ToArray() } };
        var sim = new BattleSim(new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, MaxAlive = 8, SpawnMinDistance = 0, CatDamage = 0, Enemies = Kinds, Plan = plan },
            0, Width, seed: 1);
        for (int i = 0; i < 120 && sim.Slimes.Count(s => s.Targetable) < enemies.Length; i++) sim.Step(1.0 / 60, -1000, false); // The cat far off: nothing reaches it.
        for (int i = 0; i < enemies.Length; i++) sim.Slimes[i].X = enemies[i].X;
        sim.Paused = true;
        var view = new BattleFieldView(sim) { OriginX = 0 };
        view.Measure(new Size(Width, BattleFieldView.FieldHeight));
        view.Arrange(new Rect(0, 0, Width, BattleFieldView.FieldHeight));
        return (sim, view);
    }

    private static byte[] Shot(BattleFieldView view, string? save = null)
    {
        view.InvalidateVisual(); view.UpdateLayout();
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var pixels = new byte[w * h * 4];
        bitmap.CopyPixels(pixels, w * 4, 0);
        if (save != null && Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string folder)
        {
            // The same picture on a dark and a light desktop, one above the other.
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x24)), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)), null, new Rect(0, h, w, h));
                dc.DrawImage(bitmap, new Rect(0, 0, w, h));
                dc.DrawImage(bitmap, new Rect(0, h, w, h));
            }
            var image = new RenderTargetBitmap(w, h * 2, 96, 96, PixelFormats.Pbgra32);
            image.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = System.IO.File.Create(System.IO.Path.Combine(folder, save + ".png"));
            encoder.Save(stream);
        }
        return pixels;
    }

    // Opaque pixels near this colour inside a box (x from–to, y from–to).
    private static int Count(byte[] px, Color c, int x0, int x1, int y0, int y1, int tolerance = 18)
    {
        int n = 0, w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(w, x1); x++)
            {
                int i = (y * w + x) * 4;
                if (px[i + 3] > 240 && Math.Abs(px[i + 2] - c.R) <= tolerance && Math.Abs(px[i + 1] - c.G) <= tolerance && Math.Abs(px[i] - c.B) <= tolerance) n++;
            }
        return n;
    }

    public static void Run(Action<bool, string> check)
    {
        int floor = (int)BattleFieldView.FieldHeight - 2;
        var white = Color.FromRgb(255, 255, 255);
        var leaf = Color.FromRgb(0x3E, 0x9B, 0x4F);

        // The herb slime: a leaf cap over its head, no white "+" on its body (the older mark still draws one).
        var (sim, view) = Field(("mender", 150), ("cross", 400));
        var px = Shot(view, "enemy-herbal");
        int plusOnLeaf = Count(px, white, 145, 156, floor - 14, floor - 4), plusOnCross = Count(px, white, 395, 406, floor - 14, floor - 4);
        check(plusOnLeaf == 0 && plusOnCross > 10, $"The herb slime has no \"+\" on its body (white pixels {plusOnLeaf}; the older mark {plusOnCross})");
        check(Count(px, leaf, 140, 175, floor - 46, floor - 26) > 20, "It wears a green leaf cap");
        // Heals spent: the leaf droops, dull.
        sim.Slimes[0].HealLeft = 0;
        px = Shot(view, "enemy-herbal-spent");
        check(Count(px, Color.FromRgb(0x93, 0x9A, 0x74), 130, 175, floor - 46, floor - 20) > 15 && Count(px, leaf, 140, 175, floor - 46, floor - 26) < 5,
            "With its heals spent the leaf turns dull and droops");

        // A teleport on its way: the support shrinks inside a ring of leaves; the arrival spot is marked.
        var (tsim, tview) = Field(("mender", 150), ("slime", 450));
        var support = tsim.Slimes[0];
        support.TeleportX = 500;
        support.Set(SlimeState.Teleporting);
        support.StateTime = 0.3;
        px = Shot(tview, "enemy-teleport");
        check(Count(px, leaf, 480, 520, floor - 30, floor - 8) > 8, "A teleport shows a leaf and a dashed ring where the support will appear");

        // The rock: an armour slab on the side it faces.
        var (rsim, rview) = Field(("rock", 300));
        var rock = rsim.Slimes[0];
        var armor = Color.FromRgb(0x7E, 0x77, 0x6D);
        rock.Facing = 1;
        px = Shot(rview, "enemy-rock");
        int right = Count(px, armor, 300, 345, floor - 45, floor), left = Count(px, armor, 255, 300, floor - 45, floor);
        rock.Facing = -1;
        px = Shot(rview);
        int leftAfter = Count(px, armor, 255, 300, floor - 45, floor), rightAfter = Count(px, armor, 300, 345, floor - 45, floor);
        check(right > left + 20 && leftAfter > rightAfter + 20, $"The rock's armour is on the side it faces (right {right}/{left}, left {leftAfter}/{rightAfter})");

        // A herb slime behind three rocks (it came first, so it used to be drawn under them): it is drawn
        // on top, the rocks over it fade a little, and a click on it picks it.
        var (gsim, gview) = Field(("mender", 300), ("rock", 270), ("rock", 300), ("rock", 330));
        foreach (var s in gsim.Slimes) s.Facing = 1;
        px = Shot(gview, "enemy-guarded-healer");
        int body = Count(px, Color.FromRgb(0x4F, 0xC9, 0xA8), 285, 315, floor - 22, floor - 4), cap = Count(px, leaf, 290, 325, floor - 46, floor - 24);
        int? picked = gview.SlimeAt(new Point(300, floor - 12));
        check(body > 60 && cap > 15 && picked == gsim.Slimes[0].Id, $"A herb slime among three rocks stays in sight and is the one a click picks (body {body}, leaf {cap} pixels)");

        // In the way: a grey dashed ring at the rock's feet while the cat goes past it for its target.
        var (wsim, wview) = Field(("rock", 300), ("slime", 480));
        wsim.Paused = false;
        wsim.Select(wsim.Slimes[1].Id);
        wsim.Step(1.0 / 60, 120, false);
        wsim.Paused = true;
        px = Shot(wview, "enemy-in-the-way");
        check(wsim.BlockedById == wsim.Slimes[0].Id && Count(px, Color.FromRgb(0x8C, 0x85, 0x7A), 250, 350, floor - 8, floor + 2, 10) > 10, "A rock in the cat's way gets a grey ring at its feet");

        // The boss's crown glows while it rallies.
        var (bsim, bview) = Field(("king", 300));
        var calm = Shot(bview);
        bsim.Slimes[0].RallyCharge = 0.5;
        var glowing = Shot(bview, "enemy-rally");
        check(Count(glowing, Color.FromRgb(0xFF, 0xE6, 0x70), 270, 330, floor - 90, floor - 50, 40) > Count(calm, Color.FromRgb(0xFF, 0xE6, 0x70), 270, 330, floor - 90, floor - 50, 40),
            "The boss's crown glows while it rallies");

        // A spawn from behind: a red arrow at that edge.
        var incoming = new BattleSim(new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0, CatDamage = 0, Enemies = Kinds,
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Side = "a" }, new WaveGroup { Enemy = "slime", Side = "b" } } } }
        }, 0, Width, seed: 1);
        for (int i = 0; i < 120 && !incoming.Incoming; i++) incoming.Step(1.0 / 60, 300, false);
        incoming.Paused = true;
        var iview = new BattleFieldView(incoming) { OriginX = 0 };
        iview.Measure(new Size(Width, BattleFieldView.FieldHeight)); iview.Arrange(new Rect(0, 0, Width, BattleFieldView.FieldHeight));
        px = Shot(iview, "enemy-incoming");
        int edge = (int)incoming.IncomingX;
        check(incoming.Incoming && Count(px, Color.FromRgb(0xE5, 0x3E, 0x3E), edge - 40, edge + 40, floor - 75, floor - 15, 30) > 30, "An arrow warns of a spawn from behind at its edge");

        // An elite: a gold rim round its body and a gold star at its upper back corner, for every kind.
        var rim = Color.FromRgb(0xF2, 0xB8, 0x1E);
        var star = Color.FromRgb(0xFF, 0xC8, 0x2E);
        var (esim, eview) = Field(("slime", 100), ("mender", 250), ("rock", 420));
        var plain = Shot(eview);
        foreach (var s in esim.Slimes) { s.Elite = new EliteSpec { WindupAdd = 0.3 }; s.Facing = 1; }
        px = Shot(eview, "enemy-elite");
        bool all = true;
        string counts = "";
        foreach (var (x, half) in new[] { (100, 30), (250, 30), (420, 40) })
        {
            int gold = Count(px, rim, x - half, x + half, floor - 60, floor + 2, 30), before = Count(plain, rim, x - half, x + half, floor - 60, floor + 2, 30);
            int stars = Count(px, star, x - half - 10, x, floor - 62, floor - 15, 20); // Facing right: the star is on its left.
            all &= gold > before + 25 && stars > 6;
            counts += $" {gold}/{before}/{stars}";
        }
        check(all, $"Elites show a gold rim and a star (plain, herb and rock slime; rim/before/star:{counts})");

        // The rare cards' icons draw something of their own (gold over the round back).
        foreach (var effect in new[] { "shell", "opener", "momentum" })
        {
            var icon = new UpgradeIcon { Effect = effect, Width = 52, Height = 52 };
            icon.Measure(new Size(52, 52)); icon.Arrange(new Rect(0, 0, 52, 52));
            var bitmap = new RenderTargetBitmap(52, 52, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(icon);
            var pixels = new byte[52 * 52 * 4];
            bitmap.CopyPixels(pixels, 52 * 4, 0);
            int goldPixels = 0;
            for (int i = 0; i < pixels.Length; i += 4) if (pixels[i + 3] > 240 && pixels[i + 2] > 230 && pixels[i + 1] is > 190 and < 225 && pixels[i] < 120) goldPixels++;
            check(goldPixels > 60, $"The {effect} card has its own icon ({goldPixels} gold pixels)");
        }
    }
}
