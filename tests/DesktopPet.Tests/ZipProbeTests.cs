using DesktopPet;

// 2026-11-06: how long the Zip Slime's phases last, for choosing its HP (docs/ZIP-SLIME.md). A simulation, not real
// play: a player who lifts the cat over floor blows, pins, cuts and pulls on time, clicks the shell open and starts
// Rage when full, in the boss fight alone at the stage's threat (no earlier waves, a given build). It reports time
// when the boss can be hurt apart from time behind a shield, real DPS and rounds of its three moves. Printed only
// with PET_ZIP_PROBE=1; the one check kept in every run: the plain Sword's fight stays within the agreed rounds.
internal static class ZipProbeTests
{
    private const double dt = 1.0 / 30;
    private sealed class Phase { public double Open, Shut, Damage; public int Moves; }

    private static readonly (string Name, Dictionary<string, int> Cards)[] Builds =
    {
        ("none", new()),
        ("mid", new() { ["sharp"] = 2, ["fast_paws"] = 2 }),
        ("strong", new() { ["sharp"] = 3, ["fast_paws"] = 3, ["thick_fur"] = 1 })
    };

    private static StageSpec BossOnly(BattleSettings data, double threat) => new()
    {
        Id = "zip_probe", NameVi = "Do", NameEn = "Probe", Threat = threat,
        Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "zip", Count = 1 } } } }
    };

    private static (bool Won, double Seconds, double HpLeft, double BossHp, Phase[] Phases) Play(BattleSettings data, string weapon, BattleDifficulty level, double threat, Dictionary<string, int> build, int seed, double weaponBonus = 0)
    {
        var sim = new BattleSim(data, 0, 1600, new Random(seed), level, weapon, BossOnly(data, threat), build, seed, apps: false, skills: null, weaponBonus: weaponBonus);
        var phases = new[] { new Phase(), new Phase(), new Phase() };
        double cat = 700, hpBefore = 0, nextHand = 0, bossHp = 0;
        int moves = 0;
        for (double t = 0; t < 600 && !sim.Over; t += dt)
        {
            sim.Use(BattleSkill.Heal, cat);
            var packets = sim.Slimes.Where(s => s.PacketLeft > 0 && s.Targetable).ToArray();
            if (packets.Length > 0) sim.SetPointer((packets[(int)(t / 0.25) % packets.Length].X, 10));
            if (ZipPuzzlePlayer.Step(sim, ref cat, dt, dodge: true))
            {
                if (seed == 1 && Environment.GetEnvironmentVariable("PET_ZIP_DIAG") != null && sim.Boss is { BombClock: > 0 } && (int)(t * 30) % 90 == 0)
                    Console.WriteLine($"ZIP final t={t:0.0} cat={cat:0} hp={sim.CatHp:0} bomb={sim.Boss.BombClock:0.0} target={sim.TargetId} cd={sim.CatCooldown:0.0} stun={sim.CatStunRemaining:0.0} swing={sim.SwingTime:0.0}");
                sim.ClearEvents(); continue;
            }
            var boss = sim.Boss;
            CatPose? pose = null;
            bool held = false;
            if (boss != null)
            {
                bossHp = boss.MaxHp;
                var shield = sim.ShieldPhase(boss);
                if (sim.CatLocked && t >= nextHand) { sim.ClickZipLock(); nextHand = t + 0.3; }
                if (sim.RageReady) sim.StartRage();
                sim.Use(BattleSkill.Heal, cat);
                sim.Use(BattleSkill.Cheer, cat);
                if (!boss.Shielded) sim.Use(BattleSkill.Bomb, boss.Id, cat);
                foreach (var drop in sim.Drops.ToList()) sim.PickUp(drop.Id);
                if (sim.CatLocked) { }
                else if (boss.ZipRepulseWarn > 0 || sim.Slimes.Any(s => s.PacketFuse && s.PacketRevealed && s.PacketLeft > 0 && Math.Abs(s.X - cat) < 180))
                {
                    held = true; pose = new CatPose(cat, 260, 0, 0, true, false);
                }
                else if (sim.ZipThreatens(boss, cat) && boss.ZipSeconds - boss.ZipLeft >= 0.35)
                {
                    held = true; pose = new CatPose(cat, 240, 0, 0, true, false);
                }
                else if (boss.ZipAct is ZipAct.Bolt or ZipAct.Overwrite && boss.ZipBeat is ZipBeat.Lock or ZipBeat.Strike || sim.Shots.Any(s => s.Paper && s.LockIn <= 0))
                {
                    // Out of the locked line: a step aside in the hand.
                    cat = Math.Clamp(cat + (cat < 800 ? 60 : -60) * dt * 4, 60, 1540);
                    held = true; pose = new CatPose(cat, 110, 0, 0, true, false);
                }
                hpBefore = boss.Hp;
            }
            var order = held ? sim.Step(dt, cat, true, true, pose) : sim.Step(dt, cat, false);
            if (!held)
            {
                double run = 72 * data.CatRunScale * sim.MoveFactor * sim.LagFactor;
                if (order.Command == CatCommand.Shove) cat = order.X; else if (order.Command == CatCommand.Move) cat += Math.Clamp(order.X - cat, -run * dt, run * dt);
            }
            sim.ClearEvents();
            if (boss == null) continue;
            var phase = phases[Math.Clamp(boss.ZipStage, 0, 2)];
            if (boss.Shielded) phase.Shut += dt; else phase.Open += dt;
            phase.Damage += Math.Max(0, hpBefore - boss.Hp);
            if (Environment.GetEnvironmentVariable("PET_ZIP_TRACE") != null && hpBefore - boss.Hp > 25) Console.WriteLine($"t={t:0.00} -{hpBefore - boss.Hp:0} hp={boss.Hp:0} stage={boss.ZipStage} state={boss.State} bonus={boss.MechBonusClock:0.0} raging={sim.Raging} bombCd={sim.Cooldown(BattleSkill.Bomb):0.0} combo={sim.Combo} slimes={sim.Slimes.Count(s => s.Targetable)}");
            phase.Moves += sim.ZipMovesUsed - moves;
            moves = sim.ZipMovesUsed;
        }
        if (sim.Phase != BattlePhase.Won && Environment.GetEnvironmentVariable("PET_ZIP_DIAG") != null)
            Console.WriteLine($"ZIP diagnostic seed={seed} phase={sim.Phase} time={sim.Time:0} cat={sim.CatHp:0} boss={sim.Boss?.Hp:0} stage={sim.Boss?.ZipStage} props={string.Join(',', sim.Slimes.Where(s => s.ZipProp != ZipProp.None && s.Targetable).Select(s => $"{s.ZipProp}@{s.X:0}/{s.Altitude:0}"))}");
        return (sim.Phase == BattlePhase.Won, sim.FightSeconds, sim.CatHp, bossHp, phases);
    }

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var campaign = data.Campaign!.Chapters[1].Stages[^1];
        const int seeds = 4;
        var fight = Enumerable.Range(1, seeds).Select(seed => Play(data, "sword", BattleDifficulty.Normal, campaign.Threat, new(), seed)).ToArray();
        check(fight.All(f => f.Won), $"The careful player beats it with the plain Sword ({fight.Count(f => f.Won)}/{seeds})");
        if (Environment.GetEnvironmentVariable("PET_ZIP_TRACE") != null) { var r = Play(data, "sword", BattleDifficulty.Normal, campaign.Threat, Builds[2].Cards, 1); Console.WriteLine($"t=END {r.Seconds:0} " + string.Join(" | ", r.Phases.Select(p => $"{p.Open:0.0}/{p.Shut:0.0}/{p.Damage:0}/{p.Moves}"))); return; }
        if (Environment.GetEnvironmentVariable("PET_ZIP_PROBE") == null) return;

        double endlessThreat = data.Endless!.Threat(20);
        var scenes = new (string Name, BattleDifficulty Level, double Threat, double Forge)[]
        {
            ("adventure normal", BattleDifficulty.Normal, campaign.Threat, 0),
            ("adventure hard", BattleDifficulty.Hard, campaign.Threat, 0),
            ("expedition normal", BattleDifficulty.Normal, 1.2, 0.3),
            ("endless w20 normal", BattleDifficulty.Normal, endlessThreat, 0)
        };
        foreach (var (scene, level, threat, forge) in scenes)
        {
            Console.WriteLine($"-- {scene} (threat {threat:0.##})");
            Console.WriteLine("weapon build | win | secs | cat HP | boss HP | P1 open/shut dps rounds | P2 | P3");
            foreach (var weapon in data.Weapons.Select(w => w.Id))
                foreach (var (name, cards) in Builds)
                {
                    var runs = Enumerable.Range(1, 8).Select(seed => Play(data, weapon, level, threat, cards, seed, forge)).ToArray();
                    string Of(int i)
                    {
                        double open = runs.Average(r => r.Phases[i].Open), shut = runs.Average(r => r.Phases[i].Shut), dps = runs.Average(r => r.Phases[i].Damage) / Math.Max(0.1, open), rounds = runs.Average(r => r.Phases[i].Moves) / 3.0;
                        return $"{open,5:0.0}/{shut,4:0.0} {dps,4:0} {rounds,3:0.0}";
                    }
                    Console.WriteLine($"{weapon,-7}{name,-7}| {runs.Count(r => r.Won),2}/8 | {runs.Average(r => r.Seconds),4:0} | {runs.Average(r => r.HpLeft),4:0} | {runs[0].BossHp,4:0} | {Of(0)} | {Of(1)} | {Of(2)}");
                }
        }
    }
}
