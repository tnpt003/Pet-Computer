using DesktopPet;

// Battle data the user can change without rebuilding: battle.json in the data folder, merged over
// the built-in file, validated as a whole, with a safe fallback. Definitions (enemy kinds,
// weapons) are shared; each slime keeps its own state; a battle keeps the data it started with.
internal static class BattleCustomDataTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        var builtIn = BattleData.Settings;
        check(builtIn.SchemaVersion == BattleData.SchemaVersion && builtIn.EnemyTypes.All(e => e.Family == "slime" && e.Renderer == "slime" && e.HitScale == null),
            "The built-in data is schema 1; every enemy is a slime drawn by the slime renderer, hitbox = drawn size");

        string dir = Path.Combine(Path.GetTempPath(), "pet-battle-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "battle.json");
        BattleDataLoad With(string json) { File.WriteAllText(file, json); return BattleData.Load(file); }
        try
        {
            var none = BattleData.Load(file);
            check(none.Settings == builtIn && !none.Custom && none.Error == null, "No file: the built-in battle, no error");
            check(BattleData.Load(null).Settings == builtIn, "No path (tests, a window with given preferences): the built-in battle");

            // The documented example (docs/examples/battle.json): a new slime variant, data only.
            string example = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "stone-slime.battle.json"));
            var loaded = With(example);
            var data = loaded.Settings;
            var stone = data.EnemyTypes.FirstOrDefault(e => e.Id == "stone_slime");
            check(loaded.Custom && loaded.Error == null && stone is { Family: "slime", Renderer: "slime", MaxHp: 120, Speed: 22, Scale: 1.35, PushFactor: 0.35, StunFactor: 0.5, Boss: false },
                $"The example file loads: stone_slime is a slime variant with its own numbers ({loaded.Error})");
            check(data.EnemyTypes.Count == builtIn.EnemyTypes.Count + 1 && data.Enemy("rock").MaxHp == builtIn.Enemy("rock").MaxHp &&
                  data.WeaponList.SequenceEqual(builtIn.WeaponList) && data.CatMaxHp == builtIn.CatMaxHp,
                "Everything the file does not name stays built-in (other enemies, weapons, numbers)");
            check(!data.WavePlan.SelectMany(w => w.Spawns()).Contains("stone_slime") &&
                  string.Join("|", data.WavePlan.Select(w => string.Join(",", w.Spawns()))) == string.Join("|", builtIn.WavePlan.Select(w => string.Join(",", w.Spawns()))),
                "A new kind is not put into the default waves by itself");

            // The variant fights with the existing slime code; each instance has its own HP.
            var arena = new BattleSettings
            {
                FirstWaveDelay = 0, MaxAlive = 2, SpawnInterval = 0, Enemies = data.Enemies,
                Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "stone_slime", Count = 2 } } } }
            };
            var sim = new BattleSim(arena, 0, 1600, new Random(1));
            for (int i = 0; i < 3; i++) sim.Step(dt, 800, false);
            for (double t = 0; t < 2; t += dt) sim.Step(dt, 800, true);
            var pair = sim.Slimes.Where(s => s.Type.Id == "stone_slime").ToList();
            check(pair.Count == 2 && pair.All(s => s.MaxHp == 120 && Math.Abs(s.Width - arena.SlimeWidth * 1.35) < 0.01) && pair[0].Type == pair[1].Type,
                "Two stone slimes spawn from one shared definition, at its size and HP");
            pair[0].Hp -= 50;
            check(pair[0].Hp == 70 && pair[1].Hp == 120 && stone!.MaxHp == 120, "Hurting one leaves the other and the definition untouched");

            // Changing a built-in kind or weapon: only the keys given; a new weapon is added.
            var tweak = With("""{ "schemaVersion": 1, "Enemies": [ { "id": "slime", "maxHp": 60 } ], "easy": { "enemyHp": 0.5 },""" +
                """ "weapons": [ { "id": "spear", "nameVi": "Giáo", "nameEn": "Spear", "style": "sword", "damage": 18, "range": 110, "attackCooldown": 0.9, "swingDuration": 0.4, "hitTime": 0.2, "knock": 0 } ] }""");
            var t2 = tweak.Settings;
            check(tweak.Error == null && t2.Enemy("slime") is { MaxHp: 60, Speed: 34, Color: "#7CD36A" } && t2.Easy.EnemyHp == 0.5 && t2.Easy.EnemyDamage == builtIn.Easy.EnemyDamage,
                $"An entry with a known id changes just the keys it names (keys match without case) ({tweak.Error})");
            check(t2.WeaponList.Select(w => w.Id).SequenceEqual(new[] { "sword", "hammer", "spear" }) && t2.Weapon("sword") == builtIn.Weapon("sword"),
                "A new weapon joins the list after the built-in ones");

            // The chosen weapon is what the battle swings: damage and reach come from it.
            var duel = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0, Weapons = t2.Weapons is null ? null : t2.WeaponList.ToArray() };
            var spear = new BattleSim(duel, 0, 1600, new Random(3), weapon: "spear");
            spear.Step(dt, 800, false);
            for (double t = 0; t < 0.6; t += dt) spear.Step(dt, 800, true);
            spear.SetCatState(false, false);
            var target = spear.Slimes[0];
            target.X = 800 + 100; // In the spear's reach (110), outside the sword's (76).
            double hit = 0;
            for (double t = 0; t < 1.5 && hit == 0; t += dt)
            {
                target.X = 900;
                spear.Step(dt, 800, false);
                hit = target.MaxHp - target.Hp;
            }
            check(spear.Weapon.Id == "spear" && hit == 18, $"A battle started with the spear hits for its damage at its reach ({hit})");
            check(new BattleSim(duel, 0, 1600, weapon: "bow").Weapon.Id == "sword" && duel.Weapon("HAMMER").Id == "hammer",
                "An id the data does not have falls back to the first weapon; ids ignore case (old preferences)");

            // Snapshot: a running battle keeps its data when the file changes.
            var before = With("""{ "schemaVersion": 1, "enemies": [ { "id": "slime", "maxHp": 70 } ] }""").Settings;
            var running = new BattleSim(before, 0, 1600);
            var after = With("""{ "schemaVersion": 1, "enemies": [ { "id": "slime", "maxHp": 999 } ] }""").Settings;
            check(running.Settings == before && running.Settings.Enemy("slime").MaxHp == 70 && after.Enemy("slime").MaxHp == 999,
                "Editing the file changes the next battle's data, never a running battle's");

            // Mistakes: the whole file is refused, the built-in battle is used, the reason says what to fix.
            (bool Fallback, string Error) Broken(string json)
            {
                var result = With(json);
                return (result.Settings == builtIn && !result.Custom, result.Error ?? "");
            }
            var missing = Broken("""{ "enemies": [ { "id": "slime", "maxHp": 60 } ] }""");
            var future = Broken("""{ "schemaVersion": 2 }""");
            check(missing.Fallback && missing.Error.Contains("schemaVersion is missing") && future.Fallback && future.Error.Contains("not supported"),
                $"No schemaVersion, or another version: refused with a reason ({missing.Error} / {future.Error})");
            var ghost = Broken("""{ "schemaVersion": 1, "waves": [ { "groups": [ { "enemy": "ghost", "count": 1 } ] } ] }""");
            var summon = Broken("""{ "schemaVersion": 1, "enemies": [ { "id": "king", "summonType": "ghost" } ] }""");
            check(ghost.Fallback && ghost.Error.Contains("ghost") && summon.Fallback && summon.Error.Contains("ghost"),
                $"A wave or summon naming an unknown enemy is refused and named ({ghost.Error} / {summon.Error})");
            var renderer = Broken("""{ "schemaVersion": 1, "enemies": [ { "id": "bat", "renderer": "bat" } ] }""");
            var style = Broken("""{ "schemaVersion": 1, "weapons": [ { "id": "bow", "style": "bow" } ] }""");
            var ability = Broken("""{ "schemaVersion": 1, "enemies": [ { "id": "bat", "flying": true } ] }""");
            check(renderer.Fallback && renderer.Error.Contains("enemies[bat].renderer") && style.Fallback && style.Error.Contains("weapons[bow].style") && ability.Fallback,
                $"A drawing, weapon style or ability the code does not have is refused ({renderer.Error} / {style.Error} / {ability.Error})");
            var badId = Broken("""{ "schemaVersion": 1, "enemies": [ { "id": "Stone Slime" } ] }""");
            var noId = Broken("""{ "schemaVersion": 1, "weapons": [ { "damage": 10 } ] }""");
            var twice = Broken("""{ "schemaVersion": 1, "weapons": [ { "id": "spear" }, { "id": "spear" } ] }""");
            check(badId.Fallback && badId.Error.Contains(".id") && noId.Fallback && noId.Error.Contains("weapons[0]") && twice.Fallback,
                $"Ids must be stable lower-case names, present on every entry ({badId.Error} / {noId.Error})");
            var huge = Broken("""{ "schemaVersion": 1, "enemies": [ { "id": "slime", "maxHp": 1e9 } ] }""");
            var tiny = Broken("""{ "schemaVersion": 1, "enemies": [ { "id": "slime", "hitScale": 0.1 } ] }""");
            var nan = Broken("""{ "schemaVersion": 1, "enemies": [ { "id": "slime", "speed": "NaN" } ] }""");
            check(huge.Fallback && huge.Error.Contains("enemies[slime].maxHp") && tiny.Fallback && tiny.Error.Contains("hitScale") && nan.Fallback,
                $"Numbers out of their limits (or not numbers) are refused ({huge.Error} / {tiny.Error} / {nan.Error})");
            check(Broken("{ not json").Fallback && Broken("[1, 2]").Fallback && Broken("""{ "schemaVersion": 1, "catMaxHp": 120, "catMaxHpp": 1 }""").Fallback,
                "Broken JSON, a file that is not an object, or a misspelt key: refused as a whole");
            // NaN and similar cannot come from JSON numbers, but the checks hold for any data.
            bool Rejects(EnemyType enemy)
            {
                try { BattleData.Validate(new BattleSettings { Enemies = new[] { enemy }, Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = enemy.Id, Count = 1 } } } } }); return false; } catch (InvalidDataException) { return true; }
            }
            check(!Rejects(new EnemyType()) && Rejects(new EnemyType { PushFactor = double.NaN }) && Rejects(new EnemyType { SummonAt = double.NaN }) &&
                  Rejects(new EnemyType { HitScale = double.PositiveInfinity }) && Rejects(new EnemyType { StunFactor = -1 }),
                "NaN, infinite or negative factors are refused");

            // Drawn size and hitbox are separate; unset, the hitbox is the drawn size (no change to old fights).
            var sizes = new BattleSettings
            {
                FirstWaveDelay = 0, MaxAlive = 2, SpawnInterval = 0,
                Enemies = new[] { new EnemyType { Id = "slime" }, new EnemyType { Id = "puffy", Scale = 2, HitScale = 1 } },
                Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 1 }, new WaveGroup { Enemy = "puffy", Count = 1 } } } }
            };
            var sized = new BattleSim(sizes, 0, 1600, new Random(2));
            for (int i = 0; i < 3; i++) sized.Step(dt, 800, false);
            for (double t = 0; t < 2; t += dt) sized.Step(dt, 800, true);
            var plain = sized.Slimes.First(s => s.Type.Id == "slime");
            var puffy = sized.Slimes.First(s => s.Type.Id == "puffy");
            check(plain.HitWidth == plain.Width && puffy.Width == 2 * sizes.SlimeWidth && puffy.HitWidth == sizes.SlimeWidth,
                "A kind may look big and be hit like a small one (hitScale); by default both sizes are the same");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
