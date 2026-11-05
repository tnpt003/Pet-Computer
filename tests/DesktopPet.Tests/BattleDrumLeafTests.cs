using DesktopPet;

// 2026-11-04 (docs/GOPY-C-2026-11-04.md, batch C4): the drummer's big drum (hurts and throws the cat off the
// beat, a lifted cat is missed, a hit starts the count again); the herb slime's leaf shield on a bomb slime
// (no damage, never healed, falls with the herb, takes one blast and the bomb flies back to the quick slime
// that threw it); slimes after a lifted cat at half speed; the cat keeping under a flyer when the floor is
// clear; a timer's call that is a formation.
internal static class BattleDrumLeafTests
{
    const double dt = 1.0 / 60;
    private static BattleSettings Shipped => BattleData.Settings;

    private static BattleSim Field(params string[] kinds) => Field(null, kinds);

    private static BattleSim Field(List<BattleEventKind>? seen, params string[] kinds)
    {
        var settings = new BattleSettings
        {
            Weapons = new[] { Shipped.Weapon("sword") with { Range = 5, StunChance = 0 } }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 100_000, MaxAlive = 8,
            Enemies = Shipped.EnemyTypes.ToArray(), CatDamage = 0, RageHitGain = 0, BombDamage = 40, BombCooldown = 0.1, BombDelay = 0.01, BombRadius = 40,
            Plan = new[] { new WaveSpec { Groups = kinds.Select(k => new WaveGroup { Enemy = k, Count = 1 }).ToArray() } }
        };
        var sim = new BattleSim(settings, 0, 1600, seed: 5);
        for (int i = 0; i < 600 && sim.Slimes.Count(s => s.Targetable) < kinds.Length; i++) { sim.Step(dt, 100, false); seen?.AddRange(sim.Events.Select(e => e.Kind)); sim.ClearEvents(); }
        return sim;
    }

    // Steps until the event comes (the frame it came in, its events still there) or the time is up.
    private static bool Until(BattleSim sim, BattleEventKind kind, double seconds, double catX = 200, bool lifted = false, Action? each = null)
    {
        for (int i = 0; i < seconds / dt; i++)
        {
            sim.ClearEvents();
            sim.Step(dt, catX, lifted, lifted);
            each?.Invoke();
            if (sim.Events.Any(e => e.Kind == kind)) return true;
        }
        return false;
    }

    public static void Run(Action<bool, string> check)
    {
        DrumChecks(check);
        LeafChecks(check);
        RecoilChecks(check);
        FollowChecks(check);
        CallChecks(check);
    }

    private static void DrumChecks(Action<bool, string> check)
    {
        var link = Shipped.Enemy("drummer").Link!;
        check(link.DrumSeconds == 2 && link.EliteDrumSeconds == 2.5, "The shipped drummer's big drum: 2 s off the beat, an elite's 2.5 s");

        var floor = Field("drummer");
        var drummer = floor.Slimes.Single();
        drummer.X = 300;
        double hp = floor.CatHp;
        bool beat = Until(floor, BattleEventKind.BigDrum, 6, 320);
        check(beat && floor.CatHp < hp && floor.CatOffBeat && floor.BigDrums == 1, $"On the last beat the cat in the ring is hurt and off the beat ({hp - floor.CatHp:0})");
        check(floor.Effects.Any(e => e.Kind == BattleEffectKind.Drum), "It shows as rings of sound");

        var up = Field("drummer");
        up.Slimes.Single().X = 300;
        hp = up.CatHp;
        beat = Until(up, BattleEventKind.BigDrum, 6, 320, lifted: true);
        check(beat && up.CatHp == hp && !up.CatOffBeat, "A lifted cat is missed");

        var far = Field("drummer");
        far.Slimes.Single().X = 1200;
        hp = far.CatHp;
        beat = Until(far, BattleEventKind.BigDrum, 6, 320);
        check(beat && far.CatHp == hp && !far.CatOffBeat, "Out of the ring the drum does nothing to the cat");

        var hit = Field("drummer");
        var struck = hit.Slimes.Single();
        struck.X = 1200;
        Until(hit, BattleEventKind.LinkBeat, 4);
        bool counted = struck.BeatCount > 0;
        hit.Damage(struck, 1, DamageSource.Weapon);
        check(counted && struck.BeatCount == 0, "A weapon's hit starts its count again");
    }

    private static void LeafChecks(Action<bool, string> check)
    {
        var sim = Field("mender", "bomber", "runner");
        var herb = sim.Slimes.Single(s => s.Type.Id == "mender");
        var bomb = sim.Slimes.Single(s => s.Type.Id == "bomber");
        for (int i = 0; i < 60 && !bomb.LeafShield; i++) { sim.Step(dt, 200, false); sim.ClearEvents(); }
        check(bomb.LeafShield && bomb.LeafUsed && bomb.LeafBy == herb.Id && sim.LeafShields == 1, "A herb slime anywhere on the field shields a bomb slime");
        double hp = bomb.Hp;
        sim.Damage(bomb, 500, DamageSource.Bomb);
        check(bomb.Hp == hp && bomb.LeafShield, "Under the shield it takes no damage at all");

        // Hurt it by hand and put the herb next to it: it must not be healed.
        bomb.Hp = Math.Max(1, bomb.Hp - 5);
        hp = bomb.Hp;
        herb.X = bomb.X;
        bool healing = false;
        for (int i = 0; i < 6 / dt; i++)
        {
            sim.Step(dt, 200, false); sim.ClearEvents();
            healing |= sim.Healing(herb)?.Id == bomb.Id;
            herb.X = bomb.X;
        }
        check(!healing && bomb.Hp <= hp, "A herb slime never heals a bomb slime");

        var fall = Field("mender", "bomber", "runner");
        var fallHerb = fall.Slimes.Single(s => s.Type.Id == "mender");
        var fallBomb = fall.Slimes.Single(s => s.Type.Id == "bomber");
        for (int i = 0; i < 60 && !fallBomb.LeafShield; i++) { fall.Step(dt, 200, false); fall.ClearEvents(); }
        fall.Damage(fallHerb, 100_000, DamageSource.Bomb);
        check(!fallBomb.LeafShield, "The herb falling takes the shield with it");
        for (int i = 0; i < 120; i++) { fall.Step(dt, 200, false); fall.ClearEvents(); }
        check(!fallBomb.LeafShield, "One herb, one bomb: it is not shielded twice");
        hp = fallBomb.Hp;
        fall.Damage(fallBomb, 5, DamageSource.Bomb);
        check(fallBomb.Hp < hp, "Then it can be hurt again");
    }

    private static void RecoilChecks(Action<bool, string> check)
    {
        // Thrown and blown up with its shield on: the bomb lives, charred, and flies back to its mount.
        var sim = Field("mender", "bomber", "runner");
        var bomb = sim.Slimes.Single(s => s.Type.Id == "bomber");
        var mount = sim.Slimes.Single(s => s.Type.Id == "runner");
        var herb = sim.Slimes.Single(s => s.Type.Id == "mender");
        bool thrownAtShield = false;
        bool burst = Until(sim, BattleEventKind.LeafBurst, 60, each: () => { thrownAtShield |= bomb.LeafShield && bomb.ThrownBy == mount.Id; herb.Hp = herb.MaxHp; });
        check(burst && thrownAtShield, "A quick slime throws the shielded bomb and it goes off");
        check(bomb.Targetable && bomb.Charred && !bomb.LeafShield && bomb.RecoilLeft > 0, "The shield takes the blast: the bomb lives, charred, flying back");
        double hp = bomb.Hp;
        sim.Damage(bomb, 500, DamageSource.Bomb);
        check(bomb.Hp == hp, "No damage in the air, so no gap after the first blast");
        check(sim.AwaitingBomb(mount), "Its quick slime waits for it (the view's !)");
        bool caught = Until(sim, BattleEventKind.BombCaught, 2);
        check(caught && bomb.LinkedTo == mount.Id && sim.BombsCaught == 1, "The quick slime catches it");
        check(sim.LeafShields == 1, "The shield does not come back");

        var alone = Field("mender", "bomber", "runner");
        var lost = alone.Slimes.Single(s => s.Type.Id == "bomber");
        var ride = alone.Slimes.Single(s => s.Type.Id == "runner");
        var keeper = alone.Slimes.Single(s => s.Type.Id == "mender");
        burst = Until(alone, BattleEventKind.LeafBurst, 60, each: () => keeper.Hp = keeper.MaxHp);
        alone.Damage(ride, 100_000, DamageSource.Bomb);
        bool dazed = false;
        for (int i = 0; i < 2 / dt && !dazed; i++) { alone.Step(dt, 200, false); alone.ClearEvents(); dazed = lost.State == SlimeState.Stunned; }
        check(burst && dazed && lost.RecoilLeft == 0 && alone.BombsCaught == 0, "Its quick slime gone, the bomb lands dazed");
    }

    private static void FollowChecks(Action<bool, string> check)
    {
        double Walked(bool lifted)
        {
            var sim = Field("slime");
            var slime = sim.Slimes.Single();
            slime.X = 1000;
            for (int i = 0; i < 30; i++) { sim.Step(dt, 200, false); sim.ClearEvents(); }
            double from = slime.X;
            for (int i = 0; i < 60; i++) { sim.Step(dt, 200, lifted, lifted); sim.ClearEvents(); }
            return from - slime.X;
        }
        double free = Walked(false), held = Walked(true);
        check(free > 0 && Math.Abs(held / free - BattleSim.HeldFollow) < 0.03, $"Slimes follow a lifted cat at half speed ({held:0} vs {free:0} DIPs in 1 s)");

        var heard = new List<BattleEventKind>();
        var sky = Field(heard, "flyer");
        var flyer = sky.Slimes.Single();
        void Say() { heard.AddRange(sky.Events.Select(e => e.Kind)); sky.ClearEvents(); }
        for (int i = 0; i < 900 && !(flyer.Fly == FlyPhase.Aloft && flyer.State != SlimeState.Spawning); i++) { sky.Step(dt, 100, false); Say(); }
        flyer.X = 1200;
        var order = sky.Step(dt, 200, false);
        Say();
        for (int i = 0; i < 60; i++) { sky.Step(dt, 200, false); Say(); }
        check(flyer.Fly == FlyPhase.Aloft && order.Command == CatCommand.Move && order.X > 200, $"With the floor clear the cat walks towards the flyer ({order.Command} to {order.X:0})");
        int told = heard.Count(k => k == BattleEventKind.FlyerWatched);
        check(told == 1, $"Once, it says why ({told})");
    }

    private static void CallChecks(Action<bool, string> check)
    {
        var shipped = Shipped.Enemy("timer").Clock!;
        check(shipped.CallFormations is { Count: > 0 } && shipped.CallFormations.Keys.All(k => Shipped.Formation(k) != null), "A timer's call may be a shipped formation");

        BattleSim Timer(double minThreat)
        {
            var squad = new FormationSpec { Id = "squad", NameVi = "S", NameEn = "S", MinThreat = minThreat, Parts = new[] { new WaveGroup { Squad = new[] { new WaveGroup { Enemy = "rock", Count = 1 }, new WaveGroup { Enemy = "mender", Count = 1 } } } } };
            EnemyType Plain(string id) => new() { Id = id, MaxHp = 1000, Speed = 0, Damage = 0, Range = 50, Windup = 0.8, AttackCooldown = 2, Recover = 0.3 };
            var timer = new EnemyType { Id = "timer", MaxHp = 1000, Speed = 0, Damage = 0, Range = 50, Windup = 0.8, AttackCooldown = 2, Recover = 0.3, Clock = new ClockSpec { Seconds = 0.5, EliteSeconds = 0.5, Calls = 1, Call = new[] { "slime" }, CallFormations = new() { ["squad"] = 1 } } };
            return new BattleSim(new BattleSettings
            {
                Weapons = new[] { Shipped.Weapon("sword") with { Range = 5, StunChance = 0 } }, FirstWaveDelay = 0, SpawnInterval = 0.05, CatMaxHp = 100_000, MaxAlive = 8,
                Enemies = new[] { timer, Plain("slime"), Plain("rock"), Plain("mender") }, Formations = new[] { squad }, CatDamage = 0, RageHitGain = 0,
                Plan = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "timer", Count = 1 } } } }
            }, 0, 1600, seed: 5);
        }
        string Called(BattleSim sim)
        {
            for (int i = 0; i < 8 / dt; i++) { sim.Step(dt, 200, false); sim.ClearEvents(); }
            return string.Join(",", sim.Slimes.Where(s => s.Targetable && s.Type.Id != "timer").Select(s => s.Type.Id).OrderBy(m => m));
        }
        string team = Called(Timer(0)), plain = Called(Timer(5));
        check(team == "mender,rock", $"The timer's call brings the whole formation ({team})");
        check(plain == "slime", $"Below the formation's threat it calls its plain list ({plain})");
    }
}
