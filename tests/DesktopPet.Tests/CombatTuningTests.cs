using DesktopPet;

// Battle balance 2026-10-24: a combo's splash, the hammer's stun, a quick slime that loses its sting
// when stunned during its windup, and a boss rally that may pick a support.
internal static class CombatTuningTests
{
    const double dt = 1.0 / 60;

    private static EnemyType Kind(string id, double hp = 1000, double speed = 60, double damage = 0) => new()
    {
        Id = id, MaxHp = hp, Speed = speed, Damage = damage, Range = 64, Windup = 0.8, AttackCooldown = 1.8, Recover = 0.35
    };

    private static BattleSettings Settings(EnemyType[] kinds, WaveGroup[] groups, double catDamage = 10, WeaponSpec[]? weapons = null) => new()
    {
        FirstWaveDelay = 0, SpawnInterval = 0, CatMaxHp = 1_000_000, MaxAlive = 4, Enemies = kinds, CatDamage = catDamage, SpawnMinDistance = 0,
        Plan = new[] { new WaveSpec { Groups = groups } }, Weapons = weapons
    };

    // Steps with a cat that walks where the battle sends it (130 DIPs/s).
    private static double Walk(BattleSim sim, double cat, double seconds, Func<bool>? done = null)
    {
        for (double t = 0; t < seconds && !sim.Over && done?.Invoke() != true; t += dt)
        {
            var order = sim.Step(dt, cat, false);
            if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -130 * dt, 130 * dt);
        }
        return cat;
    }

    public static void Run(Action<bool, string> check)
    {
        // The combo's third hit also hits the slime next to its target for 60 % of its damage.
        var pair = new BattleSim(Settings(new[] { Kind("slime") }, new[] { new WaveGroup { Enemy = "slime", Count = 2, Side = "a" } }), 0, 1600, new Random(2));
        Walk(pair, 300, 30, () => pair.CombosLanded >= 1);
        var hurt = pair.Slimes.Where(s => s.Hp < s.MaxHp).OrderBy(s => s.Hp).ToList();
        check(pair.CombosLanded == 1 && pair.SplashHits == 1 && hurt.Count == 2 && Math.Abs(hurt[1].MaxHp - hurt[1].Hp - 12) < 1e-9,
            $"A combo (20 damage) splashes 12 on the slime beside its target ({pair.SplashHits} splash, {string.Join("/", hurt.Select(s => s.MaxHp - s.Hp))} lost)");
        var apart = new BattleSim(new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0, CatMaxHp = 1_000_000, MaxAlive = 4, CatDamage = 10, SpawnMinDistance = 0, ComboSplash = 0,
            Enemies = new[] { Kind("slime") }, Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 2, Side = "a" } } } }
        }, 0, 1600, new Random(2));
        Walk(apart, 300, 30, () => apart.CombosLanded >= 1);
        check(apart.CombosLanded == 1 && apart.SplashHits == 0 && apart.Slimes.Count(s => s.Hp < s.MaxHp) == 1, "comboSplash 0: the combo hits only its target");

        // The hammer's stun: a weapon that always stuns dazes a plain slime, never a boss; a sword never does.
        var maul = new[] { new WeaponSpec { Id = "maul", Damage = 5, Knock = 0, StunChance = 1, StunSeconds = 0.8 } };
        var stunned = new BattleSim(Settings(new[] { Kind("slime") }, new[] { new WaveGroup { Enemy = "slime" } }, weapons: maul), 0, 1600, new Random(1), weapon: "maul");
        bool dazed = false;
        double length = 0;
        Walk(stunned, 800, 20, () => stunned.StrikeStuns > 0);
        var target = stunned.Slimes[0];
        dazed = target.State == SlimeState.Stunned;
        length = target.StunRemaining;
        check(stunned.StrikeStuns == 1 && dazed && Math.Abs(length - 0.8) < 0.05, $"A weapon with stunChance 1 dazes the slime it hits for 0.8 s ({length:0.00} s)");
        var king = new EnemyType { Id = "king", MaxHp = 5000, Speed = 30, Damage = 0, Range = 110, Windup = 1.2, AttackCooldown = 2.6, Boss = true, Slam = true, Scale = 2 };
        var boss = new BattleSim(Settings(new[] { king }, new[] { new WaveGroup { Enemy = "king" } }, weapons: maul), 0, 1600, new Random(1), weapon: "maul");
        Walk(boss, 800, 20);
        check(boss.Slimes[0].Hp < boss.Slimes[0].MaxHp - 20 && boss.StrikeStuns == 0, "The hammer's stun never dazes a boss");
        var data = BattleData.Settings;
        check(data.Weapon(BattleWeapon.Hammer) is { StunChance: 0.15, StunSeconds: 0.8 } && data.Weapon(BattleWeapon.Sword).StunChance == 0
              && data.ComboSplash == 0.6 && data.ComboSplashRadius == 60,
            "battle.json: the hammer stuns 15 % of its hits for 0.8 s, the sword never; a combo splashes 60 % within 60 DIPs");

        // A quick slime stunned during its windup loses its sting: its later dashes hurt but never stun.
        var dasher = new EnemyType
        {
            Id = "runner", MaxHp = 1000, Speed = 70, Damage = 6, Range = 54, Windup = 0.7, AttackCooldown = 2.6, Recover = 0.4, Scale = 0.75,
            Dash = true, DashRange = 170, DashDistance = 230, DashSpeed = 420
        };
        var sting = new BattleSim(new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0, CatDamage = 0, PushCooldown = 0.1, Enemies = new[] { dasher },
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "runner" } } } }
        }, 0, 1600, new Random(3));
        for (int i = 0; i < 900 && !(sting.Slimes.Count > 0 && sting.Slimes[0].State == SlimeState.Windup); i++) sting.Step(dt, 800, false);
        var runner = sting.Slimes[0];
        check(runner.State == SlimeState.Windup && sting.UsePush(runner.Id, 800) && runner.Defanged, "(setup) a push during the windup stuns the quick slime and takes its sting");
        double hp = sting.CatHp;
        for (int i = 0; i < 900 && sting.CatHp == hp; i++) sting.Step(dt, 800, false);
        check(sting.CatHp < hp && !sting.CatStunned && sting.StunsTaken == 0, $"Its next dash still hurts ({hp - sting.CatHp:0} HP) but does not stun the cat");
        var fresh = new BattleSim(new BattleSettings
        {
            FirstWaveDelay = 0, SpawnInterval = 0, CatDamage = 0, Enemies = new[] { dasher },
            Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "runner" } } } }
        }, 0, 1600, new Random(3));
        for (int i = 0; i < 1200 && !fresh.CatStunned; i++) fresh.Step(dt, 800, false);
        check(fresh.CatStunned && !fresh.Slimes[0].Defanged, "A quick slime never stunned in its windup still stuns the cat");
    }
}
