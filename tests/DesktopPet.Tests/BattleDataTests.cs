using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using DesktopPet;

// Battle Mode data: Assets/battle.json, enemy kinds, wave plans and the skill list.
internal static class BattleDataTests
{
    public static void Run(Action<bool, string> check)
    {
        // The shipped file says exactly what the built-in battle says (no balance change by moving it).
        var shipped = BattleData.Settings;
        var builtIn = new BattleSettings();
        bool Same(object a, object b, Type type, List<string> diffs, string prefix)
        {
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetCustomAttribute<JsonIgnoreAttribute>() != null || p.GetIndexParameters().Length > 0) continue;
                if (p.PropertyType != typeof(double) && p.PropertyType != typeof(int) && p.PropertyType != typeof(bool) && p.PropertyType != typeof(string)) continue;
                if (!Equals(p.GetValue(a), p.GetValue(b))) diffs.Add($"{prefix}{p.Name}: {p.GetValue(a)} vs {p.GetValue(b)}");
            }
            return diffs.Count == 0;
        }
        var differences = new List<string>();
        Same(shipped, builtIn, typeof(BattleSettings), differences, "");
        check(differences.Count == 0, "battle.json holds the same battle numbers as the built-in defaults" + (differences.Count > 0 ? ": " + string.Join(", ", differences) : ""));
        differences.Clear();
        foreach (var builtInKind in builtIn.EnemyTypes)
            Same(shipped.Enemy(builtInKind.Id), builtInKind, typeof(EnemyType), differences, builtInKind.Id + ".");
        check(differences.Count == 0, "battle.json's slime and Slime King match the built-in ones" + (differences.Count > 0 ? ": " + string.Join(", ", differences) : ""));
        string Plan(BattleSettings s) => string.Join(" | ", s.WavePlan.Select(w => string.Join(",", w.Spawns())));
        check(Plan(builtIn) == "slime,slime | slime,slime,slime | slime,slime,slime,slime | king", "Without the file: 2, 3, 4 slimes, then the King");
        check(Plan(shipped) == "slime,slime | slime,slime,runner,runner | rock,runner,runner,slime,slime | king",
            $"battle.json mixes in quick and rock slimes before the King ({Plan(shipped)})");
        var quick = shipped.Enemy("runner");
        var rock = shipped.Enemy("rock");
        check(quick.Id == "runner" && quick.Speed > 2 * shipped.Enemy("slime").Speed && quick.MaxHp < 50 && quick.Scale < 1 && quick.Marks == "speed",
            "The quick slime is small, fast and frail, with wind streaks");
        check(rock.Id == "rock" && rock.MaxHp > 2 * 50 && rock.Speed < 34 && rock.Scale > 1 && rock.PushFactor < 1 && rock.Marks == "cracks" && !rock.Boss,
            "The rock slime is big, slow, tough and hard to push, with cracks");

        // A broken file is rejected as a whole (the app then logs it and uses the built-in battle).
        bool Rejected(string json)
        {
            try { BattleData.Parse(new MemoryStream(Encoding.UTF8.GetBytes(json))); return false; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return true; }
        }
        const string Slime = "{\"id\":\"slime\",\"maxHp\":50}";
        check(!Rejected("{\"catMaxHp\":120}") && BattleData.Parse(new MemoryStream(Encoding.UTF8.GetBytes("{\"catMaxHp\":120}"))).CatMaxHp == 120,
            "A file may change just one number; everything else keeps its default");
        check(Rejected("{\"catMaxHP\":120,\"catMaxHpp\":1}"), "A misspelt key is an error, not a silently ignored setting");
        check(Rejected("{\"catMaxHp\":-5}") && Rejected("{\"catHitTime\":2}") && Rejected("{\"pushCooldown\":0}"), "Impossible numbers are rejected");
        check(Rejected("{\"enemies\":[{\"id\":\"slime\",\"color\":\"green\"}]}"), "Colours must be #RRGGBB");
        check(Rejected("{\"enemies\":[{\"id\":\"giant\",\"scale\":3}],\"waves\":[{\"groups\":[{\"enemy\":\"giant\",\"count\":1}]}]}"), "Enemies too big for the battle strip are rejected");
        check(Rejected("{\"enemies\":[" + Slime + "],\"waves\":[{\"groups\":[{\"enemy\":\"ghost\",\"count\":1}]}]}"), "A wave naming an unknown enemy is rejected");
        check(Rejected("{\"enemies\":[" + Slime + "],\"waves\":[]}") && Rejected("{\"enemies\":[" + Slime + "," + Slime + "]}"), "No waves, or two enemies with one id, are rejected");
        check(Rejected("{\"enemies\":[{\"id\":\"a\",\"summonAt\":0.5,\"summonCount\":1,\"summonType\":\"a\"}],\"waves\":[{\"groups\":[{\"enemy\":\"a\",\"count\":1}]}]}"),
            "An enemy that summons a summoner (endless helpers) is rejected");
        check(Rejected("{ not json"), "Not JSON at all is rejected");
        check(Rejected("{\"enemies\":[{\"id\":\"slime\",\"marks\":\"spots\"}],\"waves\":[{\"groups\":[{\"enemy\":\"slime\",\"count\":1}]}]}"), "Unknown marks are rejected");

        // New content is data only: a fast little runner, and a boss in the middle of the battle.
        var custom = BattleData.Parse(new MemoryStream(Encoding.UTF8.GetBytes("""
            {
              "firstWaveDelay": 0, "waveBreakSeconds": 0.1,
              "enemies": [
                { "id": "slime", "maxHp": 50 },
                { "id": "runner", "nameVi": "Slime nhanh", "nameEn": "Runner", "maxHp": 20, "speed": 90, "damage": 5, "scale": 0.7, "color": "#5CC8FF" },
                { "id": "chief", "maxHp": 100, "scale": 1.5, "boss": true, "summonAt": 0.6, "summonCount": 1, "summonType": "runner" }
              ],
              "waves": [
                { "groups": [ { "enemy": "runner", "count": 1 }, { "enemy": "slime", "count": 1 } ] },
                { "groups": [ { "enemy": "chief", "count": 1 } ] },
                { "groups": [ { "enemy": "slime", "count": 1 } ] }
              ]
            }
            """)));
        var sim = new BattleSim(custom, 0, 1600, new Random(5));
        const double dt = 1.0 / 60;
        sim.Step(dt, 800, false);
        var runner = sim.Slimes[0];
        check(sim.WaveCount == 3 && runner.Type.Id == "runner" && runner.MaxHp == 20 && Math.Abs(runner.Width - custom.SlimeWidth * 0.7) < 0.01 && sim.ToSpawn == 1,
            "A new enemy kind from the data spawns with its own HP and size, groups in order");
        check(!sim.IsBossWave(1) && sim.IsBossWave(2) && !sim.IsBossWave(3), "Any wave can hold a boss");
        double startX = runner.X;
        for (double t = 0; t < 0.6; t += dt) sim.Step(dt, 800, true);
        for (double t = 0; t < 0.6; t += dt) sim.Step(dt, 800, false);
        check(Math.Abs(runner.X - startX) > 45, "It moves at its own speed (90 DIPs/s)");
        // Clear wave 1 (the cat finishes weakened slimes next to it), then the chief arrives.
        for (int i = 0; i < 30 && sim.Wave < 2; i++)
        {
            foreach (var s in sim.Slimes.Where(s => s.Targetable)) { s.Hp = Math.Min(s.Hp, 1); s.X = 840; }
            for (double t = 0; t < 1; t += dt) sim.Step(dt, 800, false);
        }
        for (double t = 0; t < 3 && sim.Boss == null; t += dt) sim.Step(dt, 800, false);
        var chief = sim.Boss;
        check(sim.Wave == 2 && chief != null && chief.Type.Id == "chief" && Math.Abs(chief.Width - custom.SlimeWidth * 1.5) < 0.01, "The boss wave brings the data's boss");
    }
}
