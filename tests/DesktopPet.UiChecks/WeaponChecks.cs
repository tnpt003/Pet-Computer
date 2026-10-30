using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using static PetPrivate;

// Battle Mode's weapon choice in the real pet window: the choice (by id, from the battle data) is
// remembered and used by the next battle, never by a running one; the menu has no weapon item; the HUD and the result panel name it; the hammer is drawn in
// every pose without leaving the window. In-memory preferences only.
internal static class WeaponChecks
{

    public static async Task Run(PetWindow window, PetModel pet, Action<bool, string> check)
    {
        void TickBattle(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattle", 1.0 / 60); }
        var prefs = Field<PetPreferences>(window, "preferences");
        string language = prefs.Language, savedWeapon = prefs.BattleWeapon;
        prefs.Language = "en";
        window.Reset();

        prefs.BattleWeapon = "Axe";
        check(window.BattleWeaponChoice == BattleWeapon.Sword, "An unknown weapon in preferences reads as the sword");
        prefs.BattleWeapon = "Hammer";
        check(window.BattleWeaponChoice == BattleWeapon.Hammer, "Older preferences (\"Hammer\") still choose the hammer");
        check(!window.SetBattleWeapon("axe") && window.SetBattleWeapon(BattleWeapon.Hammer) && window.BattleWeaponChoice == BattleWeapon.Hammer && prefs.BattleWeapon == "hammer",
            "Choosing the hammer is remembered by its id; an id the data does not have is refused");

        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, Boss = false, SlimeMaxHp = 1, OutroSeconds = 0.5 };
        check(window.StartBattle() == null && window.Battle?.Weapon.Id == BattleWeapon.Hammer && pet.Hammer && pet.SwordOut,
            "The next battle uses the hammer, and the cat holds it");
        var hud = Field<BattleHud>(window, "battleHud");
        string HudText(DependencyObject node) => node is TextBlock t ? t.Text + "|" : string.Concat(LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().Select(HudText));
        check(HudText(hud).Contains("· Hammer"), "The HUD names the weapon next to the cat");

        // During a battle the menu holds only Pause / End: weapon, difficulty and the guide are on the setup panel.
        window.ContextMenu!.IsOpen = true;
        await Task.Delay(30);
        var root = window.ContextMenu.Items.OfType<MenuItem>().First(i => i.Header as string == "Battle Mode");
        string[] items = root.Items.OfType<MenuItem>().Select(i => i.Header as string ?? "").ToArray();
        window.ContextMenu.IsOpen = false;
        await Task.Delay(30);
        check(items.Length == 2 && items[0] == "Pause battle" && items[1] == "End battle" && !items.Any(h => h.StartsWith("Weapon") || h.StartsWith("Difficulty") || h.StartsWith("How to play")),
            $"The right-click menu has no weapon, difficulty or guide item ({string.Join(", ", items)})");
        window.SetBattleWeapon(BattleWeapon.Sword);
        check(window.Battle?.Weapon.Id == BattleWeapon.Hammer && pet.Hammer, "Changing it mid-battle does not change the running battle");
        window.SetBattleWeapon(BattleWeapon.Hammer);

        // A won battle with the hammer: the result panel names it; records stay per difficulty.
        int winsBefore = window.BattleRecords.For(BattleWeapon.Hammer, BattleDifficulty.Normal).Wins;
        int swordWins = window.BattleRecords.For(BattleWeapon.Sword, BattleDifficulty.Normal).Wins;
        TickBattle(0.6);
        if (window.Battle is { } b && b.Slimes.Count > 0) b.Slimes[0].X = pet.X + 56 + 50;
        TickBattle(2);
        string panel = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet result" && w.IsVisible)?.Content is DependencyObject content ? HudText(content) : "";
        check(!window.BattleActive && window.LastBattleSummary is { Won: true, Weapon: BattleWeapon.Hammer } && panel.Contains("Hammer · Normal") &&
              panel.Contains("Records (Hammer · Normal)") && window.BattleRecords.For(BattleWeapon.Hammer, BattleDifficulty.Normal).Wins == winsBefore + 1 &&
              window.BattleRecords.For(BattleWeapon.Sword, BattleDifficulty.Normal).Wins == swordWins,
            $"The result names the hammer; the win counts in the hammer + Normal records only ({panel})");
        check(!pet.Hammer && !pet.SwordOut, "After the battle the cat puts the hammer away");

        // The hammer drawn in every pose, both ways, inside the window; it looks unlike the sword.
        bool edgesClear = true;
        string Render(PetModel model)
        {
            const int size = 192;
            var visual = new PetRenderer(model);
            visual.Measure(new Size(size, size)); visual.Arrange(new Rect(0, 0, size, size));
            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32); bmp.Render(visual);
            var px = new byte[size * size * 4]; bmp.CopyPixels(px, size * 4, 0);
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string heldReview && model.Action == PetAction.Dragging
                && (Math.Abs(model.SwordSwing - 0.3) < 0.001 || Math.Abs(model.SwordSwing - 0.6) < 0.001))
            {
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
                using var stream = File.Create(Path.Combine(heldReview, $"held-{model.WeaponStyle}-{model.Facing}-{model.Pickup.Angle:0}-{model.SwordSwing:F1}.png"));
                encoder.Save(stream);
            }
            for (int i = 0; i < size; i++)
                edgesClear &= px[i * size * 4 + 3] == 0 && px[(i * size + size - 1) * 4 + 3] == 0 && px[i * 4 + 3] == 0 && px[((size - 1) * size + i) * 4 + 3] == 0;
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review && (Math.Abs(model.SwordSwing - 0.5) < 0.01 || Math.Abs(model.SwordSwing - 0.4) < 0.01 || Math.Abs(model.SwordSwing - 0.7) < 0.01 || model.SwordSwing < 0) && model.Action == PetAction.Idle)
            {
                System.IO.Directory.CreateDirectory(review);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
                using var stream = System.IO.File.Create(System.IO.Path.Combine(review, $"battle-{(model.Hammer ? "hammer" : "sword")}-{model.Facing}-{model.SwordSwing:F1}.png"));
                encoder.Save(stream);
            }
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(px));
        }
        PetModel Model(int facing, bool hammer) => new() { Action = PetAction.Idle, Grounded = true, Facing = facing, AnimationTime = 1, ActionTime = 1, SwordOut = true, Hammer = hammer,
            SwordHitAt = BattleData.Settings.Weapon(hammer ? BattleWeapon.Hammer : BattleWeapon.Sword).HitFraction };
        check(Render(Model(1, true)) != Render(Model(1, false)), "The hammer is drawn differently from the sword");
        foreach (int facing in new[] { 1, -1 })
        {
            for (double u = -1; u <= 1.0001; u += 0.1)
            {
                var m = Model(facing, true); m.SwordSwing = u < 0 ? -1 : u; m.SwordCombo = u > 0.4 && u < 0.6;
                Render(m);
            }
            var dragged = Model(facing, true); dragged.Action = PetAction.Dragging; dragged.Grounded = false; dragged.Pickup.Begin(0, 0);
            for (int frame = 0; frame < 90; frame++) dragged.Pickup.Step(frame < 45 ? frame * 40 : (90 - frame) * 40, 0, 1.0 / 120);
            Render(dragged);
            var flying = Model(facing, true); flying.Action = PetAction.Thrown; flying.Grounded = false; flying.VY = -700; Render(flying);
            var dazed = Model(facing, true); dazed.BattleDazed = true; dazed.Action = PetAction.Sitting; Render(dazed);
            var cheer = Model(facing, true); cheer.BattleCheer = true; cheer.Action = PetAction.Sitting; Render(cheer);
            var walking = Model(facing, true); walking.Action = PetAction.Walking; walking.VX = 130 * facing; Render(walking);
        }
        bool heldFaces = true, heldMoves = true;
        var poseMethod = typeof(PetRenderer).GetMethod("ComputePose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        foreach (string style in new[] { "sword", "hammer", "yarn", "glove" })
            foreach (int facing in new[] { -1, 1 })
                foreach (double tilt in new[] { -PickupAnimation.MaxAngle, 0, PickupAnimation.MaxAngle })
                {
                    var m = Model(-facing, false); m.Action = PetAction.Dragging; m.Grounded = false; m.WeaponStyle = style;
                    m.SwordHitAt = BattleData.Settings.Weapon(style).HitFraction;
                    m.Motion.Reset(m); m.Facing = facing; m.Pickup.Begin(0, 0);
                    for (int i = 0; i < 120; i++) m.Pickup.Step(0, 0, 1.0 / 120);
                    typeof(PickupAnimation).GetProperty(nameof(PickupAnimation.Angle))!.SetValue(m.Pickup, tilt);
                    var angles = new List<double>();
                    for (int i = 0; i <= 10; i++)
                    {
                        m.SwordSwing = i / 10.0;
                        var pose = poseMethod.Invoke(new PetRenderer(m), null)!;
                        angles.Add((double)pose.GetType().GetField("SwordAngle")!.GetValue(pose)!);
                        heldFaces &= Math.Sign((double)pose.GetType().GetField("FacingScale")!.GetValue(pose)!) == facing;
                        Render(m);
                    }
                    heldMoves &= angles.Max() - angles.Min() > 20;
                }
        check(heldFaces, "Held attacks face the target even when the normal motion pose faces the other way");
        check(heldMoves, "All four held weapons actually change angle after the pickup pose has fully settled");
        check(edgesClear, "Every held weapon and swing stays inside the window, facing both ways at maximum pendulum angles");

        prefs.BattleWeapon = savedWeapon;
        prefs.Language = language;
        window.Reset();
        window.BattleTuning = new BattleSettings();
    }
}
