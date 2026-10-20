using DesktopPet;

// Adventure milestone 4: the herb slime's heals (target, limits, budget, cancel), the chapter
// boss's phases (once each, summon cap, enrage) and the cosmetic rewards (given once, never with
// custom data, also for a stage won before the reward existed). BattleSim and data only.
internal static class AdventureContentTests
{
    public static void Run(Action<bool, string> check)
    {
        const double dt = 1.0 / 60;
        var data = BattleData.Settings;
        var stages = data.Campaign!.Chapters[0].Stages;
        var mender = data.Enemy("mender");
        var king = data.Enemy("king");
        var chapterKing = data.Enemy("king_c1");
        check(mender is { Id: "mender", HealAmount: 15, HealBudget: 90, Marks: "leaf", TeleportBelow: 0.35 } && !mender.Boss, "battle.json has the herb slime (heals 15, at most 90 in all)");
        check(chapterKing is { Boss: true, Phases.Length: 3, SummonCap: 4 } && chapterKing.Phases.Count(p => p.Shield) == 2 && king.Phases == null && king.SummonAt == 0.5 && king.EnrageAt == 0.3,
            "The chapter boss has 3 phases (two shield phases); the free battle's Slime King is unchanged");
        check(data.WavePlan.All(w => w.Groups.All(g => g.Enemy is "slime" or "runner" or "rock" or "king")), "The free battle's waves have no herb slime or chapter boss");
        check(stages[3].Waves.Skip(1).All(w => w.Groups.Any(g => g.Squad?.Any(m => m.Enemy == "mender") == true)) && stages[3].RulesVersion == 8 &&
              stages[4].Waves[^1].Groups.Single().Enemy == "king_c1" && stages[4].RulesVersion == 8,
            "Stage 4 sends herb slimes inside squads (waves 2–3); stage 5 ends with the chapter boss (rules version 8: formations, then roles)");
        check(stages[2].Reward == "SlimeHat" && stages[4].Reward == "SlimeKingCrown" && stages.Count(s => s.Reward != null) == 2 &&
              PetAccessories.Find("SlimeHat")?.Reward == "slime_invasion" && PetAccessories.Find("SlimeKingCrown")?.Reward == "slime_invasion" &&
              PetAccessories.Find("Bell")?.Reward == null, "Stages 3 and 5 give the Slime hat and the Slime King's crown (reward accessories)");

        // Heals: a herb slime and a hurt slime on the floor; the cat stands far off and never fights.
        // The chapter boss as it was before shield phases (Combat Depth milestone 3): plain phases
        // still work the same for custom data.
        var plainKing = new EnemyType
        {
            Id = "king_plain", MaxHp = 450, Speed = 22, Damage = 25, Range = 110, Windup = 1.2, AttackCooldown = 2.6, Recover = 0.7, Scale = 2,
            Boss = true, Slam = true, PushFactor = 0.35, StunFactor = 0.4, EnragedSpeed = 32, EnragedWindup = 0.8, SummonCap = 4,
            Phases = new[]
            {
                new BossPhase { At = 0.75, Summon = "slime", Count = 2 }, new BossPhase { At = 0.5, Summon = "mender", Count = 1 },
                new BossPhase { At = 0.4, Summon = "runner", Count = 2 }, new BossPhase { At = 0.3, Enrage = true }
            }
        };
        BattleSettings Sturdy(double? bombCooldown = null) => new()
        {
            // A tiny blast: the bomb hits only the enemy it is thrown at, never the helpers beside it.
            FirstWaveDelay = 0, SpawnInterval = 0, CatMaxHp = 1_000_000, CatDamage = 0, MaxAlive = 6, BombCooldown = bombCooldown ?? 12, BombRadius = 1,
            Enemies = data.EnemyTypes.Append(plainKing).ToArray()
        };
        StageSpec Stage(params (string Enemy, int Count)[] groups) => new()
        { Id = "t", Waves = new[] { new WaveSpec { Groups = groups.Select(g => new WaveGroup { Enemy = g.Enemy, Count = g.Count }).ToArray() } } };
        var events = new List<BattleEvent>();
        void Run(BattleSim sim, double seconds, double catX = 1500, Func<bool>? until = null)
        {
            for (double t = 0; t < seconds && until?.Invoke() != true; t += dt)
            {
                sim.Step(dt, catX, false);
                events.AddRange(sim.Events);
                sim.ClearEvents();
            }
        }
        var sim = new BattleSim(Sturdy(), 0, 600, new Random(2), stage: Stage(("slime", 1), ("mender", 1)));
        Run(sim, 2, until: () => sim.Slimes.Count == 2 && sim.Slimes.All(s => s.State != SlimeState.Spawning));
        var healer = sim.Slimes.Single(s => s.Type.Id == "mender");
        var hurt = sim.Slimes.Single(s => s.Type.Id == "slime");
        hurt.X = healer.X + 40;
        hurt.Hp = hurt.MaxHp - 30;
        events.Clear();
        Run(sim, 6, until: () => healer.State == SlimeState.Casting);
        check(healer.State == SlimeState.Casting && sim.Healing(healer) == hurt && events.Any(e => e.Kind == BattleEventKind.EnemyHealing),
            "The herb slime winds up a heal on its hurt friend (a warning first)");
        double before = hurt.Hp;
        Run(sim, 2, until: () => events.Any(e => e.Kind == BattleEventKind.EnemyHealed));
        check(Math.Abs(hurt.Hp - (before + 15)) < 1e-9 && healer.HealLeft == 75 && sim.Effects.Any(e => e.Kind == BattleEffectKind.Mend),
            "The heal gives 15 HP and is taken from its budget");
        hurt.X = healer.X + 30;
        events.Clear();
        Run(sim, 12, until: () => hurt.Hp >= hurt.MaxHp);
        check(hurt.Hp == hurt.MaxHp, "Healing never goes past max HP");
        // Itself, a full ally or one out of reach: no heal.
        healer.Hp = 5; hurt.Hp = hurt.MaxHp;
        events.Clear();
        Run(sim, 6);
        check(!events.Any(e => e.Kind == BattleEventKind.EnemyHealing), "It never heals itself, nor a friend at full HP");
        // Two enemies that never move, far apart.
        var still = new BattleSim(new BattleSettings { FirstWaveDelay = 0, SpawnInterval = 0, CatMaxHp = 1_000_000, CatDamage = 0,
            Enemies = new[] { new EnemyType { Id = "slime", Speed = 0 }, new EnemyType { Id = "mender", Speed = 0, HealAmount = 15, HealRange = 170, HealCooldown = 1, HealWindup = 0.5, HealBudget = 90 } } },
            0, 1600, new Random(2), stage: Stage(("slime", 1), ("mender", 1)));
        Run(still, 2, catX: 5000, until: () => still.Slimes.Count == 2 && still.Slimes.All(s => s.State != SlimeState.Spawning));
        var farHealer = still.Slimes.Single(s => s.Type.Id == "mender");
        var farFriend = still.Slimes.Single(s => s.Type.Id == "slime");
        farHealer.X = 200; farFriend.X = 200 + 170 + 60; farFriend.Hp = 10;
        events.Clear();
        Run(still, 5, catX: 5000);
        bool outOfReach = !events.Any(e => e.Kind == BattleEventKind.EnemyHealing) && farFriend.Hp == 10;
        farFriend.X = 200 + 100;
        Run(still, 5, catX: 5000);
        check(outOfReach && farFriend.Hp > 10, "A friend out of reach is not healed; in reach it is");
        // The budget ends healing for good.
        hurt.X = healer.X + 20; hurt.Hp = 10;
        healer.HealLeft = 4;
        Run(sim, 12, until: () => healer.HealLeft == 0);
        double afterBudget = hurt.Hp;
        events.Clear();
        Run(sim, 12);
        check(healer.HealLeft == 0 && Math.Abs(afterBudget - 14) < 1e-9 && hurt.Hp == afterBudget && !events.Any(e => e.Kind == BattleEventKind.EnemyHealing),
            "Its heal budget used up, it never heals again (no endless healing)");
        // A push during the windup cancels the heal and costs its cooldown; a defeated friend is never healed.
        var push = new BattleSim(Sturdy(), 0, 600, new Random(2), stage: Stage(("slime", 1), ("mender", 1)));
        Run(push, 2, until: () => push.Slimes.Count == 2 && push.Slimes.All(s => s.State != SlimeState.Spawning));
        var h2 = push.Slimes.Single(s => s.Type.Id == "mender");
        var f2 = push.Slimes.Single(s => s.Type.Id == "slime");
        f2.X = h2.X + 30; f2.Hp = 10;
        Run(push, 6, until: () => h2.State == SlimeState.Casting);
        push.SetCatState(false, false);
        bool pushed = push.Use(BattleSkill.Push, h2.Id, 1500);
        events.Clear();
        Run(push, mender.HealWindup + 0.2);
        check(pushed && f2.Hp == 10 && h2.HealCooldown > mender.HealCooldown - mender.HealWindup - 0.3 && !events.Any(e => e.Kind == BattleEventKind.EnemyHealed),
            "Pushing the herb slime while it winds up cancels the heal and costs its cooldown");
        var gone = new BattleSim(Sturdy(), 0, 600, new Random(2), stage: Stage(("slime", 1), ("mender", 1)));
        Run(gone, 2, until: () => gone.Slimes.Count == 2 && gone.Slimes.All(s => s.State != SlimeState.Spawning));
        var h3 = gone.Slimes.Single(s => s.Type.Id == "mender");
        var f3 = gone.Slimes.Single(s => s.Type.Id == "slime");
        f3.X = h3.X + 30; f3.Hp = 10;
        Run(gone, 6, until: () => h3.State == SlimeState.Casting);
        f3.Hp = 0; f3.Set(SlimeState.Defeated);
        events.Clear();
        Run(gone, 2);
        check(!events.Any(e => e.Kind == BattleEventKind.EnemyHealed) && f3.Hp == 0, "A friend defeated during the windup is not healed");

        // Boss phases: each once, helpers capped, enrage at the end. Yarn bombs do the damage.
        var bossFight = new BattleSim(Sturdy(bombCooldown: 0.1), 0, 1600, new Random(2), stage: Stage(("king_plain", 1)));
        Run(bossFight, 3, catX: 1500, until: () => bossFight.Boss is { State: not SlimeState.Spawning });
        var boss = bossFight.Boss!;
        int Helpers() => bossFight.Slimes.Count(s => s.SummonedBy == boss.Id && s.State != SlimeState.Defeated);
        void Bomb() { bossFight.SetCatState(false, false); bossFight.Use(BattleSkill.Bomb, boss.Id, 1500); Run(bossFight, 0.7, catX: 1500); }
        boss.Hp = boss.MaxHp * 0.76;
        Bomb();
        check(Helpers() == 2 && bossFight.Slimes.Where(s => s.SummonedBy == boss.Id).All(s => s.Type.Id == "slime") && !boss.Enraged, "At 75 % HP the boss calls two slimes");
        boss.Hp = boss.MaxHp * 0.74;
        Bomb();
        check(Helpers() == 2, "The same phase never fires twice");
        boss.Hp = boss.MaxHp * 0.51;
        Bomb();
        check(Helpers() == 3 && bossFight.Slimes.Any(s => s.SummonedBy == boss.Id && s.Type.Id == "mender"), "At 50 % it calls a herb slime");
        boss.Hp = boss.MaxHp * 0.41;
        Bomb();
        check(Helpers() == 4 && bossFight.Slimes.Count(s => s.SummonedBy == boss.Id && s.Type.Id == "runner") == 1,
            "At 40 % it calls quick slimes, but never past 4 helpers alive at once");
        boss.Hp = boss.MaxHp * 0.31;
        Bomb();
        check(boss.Enraged, "At 30 % it gets angry");
        // One big hit crossing every threshold fires each phase once.
        var bigHit = new BattleSim(Sturdy(), 0, 1600, new Random(2), stage: Stage(("king_plain", 1)));
        Run(bigHit, 3, catX: 1500, until: () => bigHit.Boss is { State: not SlimeState.Spawning });
        var boss2 = bigHit.Boss!;
        boss2.Hp = boss2.MaxHp * 0.2; // The bomb leaves it alive below every threshold (a killing blow fires no phase).
        bigHit.SetCatState(false, false);
        bigHit.Use(BattleSkill.Bomb, boss2.Id, 1500);
        Run(bigHit, 0.7, catX: 1500);
        check(boss2.Enraged && bigHit.Slimes.Count(s => s.SummonedBy == boss2.Id) == 4 && boss2.PhasesDone == 0b1111,
            "A hit past every threshold fires every phase once (helpers still capped at 4)");
        // Paused or the cat held: phases are about HP only; the free battle's king still calls 2 at 50 %.
        var free = new BattleSim(Sturdy(bombCooldown: 0.1), 0, 1600, new Random(2), stage: Stage(("king", 1)));
        Run(free, 3, catX: 1500, until: () => free.Boss is { State: not SlimeState.Spawning });
        var oldKing = free.Boss!;
        oldKing.Hp = oldKing.MaxHp * 0.51;
        free.SetCatState(false, false); free.Use(BattleSkill.Bomb, oldKing.Id, 1500); Run(free, 0.7, catX: 1500);
        check(free.Slimes.Count(s => s.SummonedBy == oldKing.Id) == 2 && !oldKing.Enraged, "The free battle's Slime King still calls two slimes at 50 % as before");
        free.Paused = true;
        double hp = oldKing.Hp;
        Run(free, 2, catX: 1500);
        check(oldKing.Hp == hp && free.Slimes.Count(s => s.SummonedBy == oldKing.Id) == 2, "Pause freezes the boss and its helpers");

        // Rewards: once, on a ranked win; also for a stage won before the reward existed.
        var progress = new AdventureProgress();
        var adventure = new Adventure(data.Campaign, progress);
        var lost = adventure.Apply("l", stages[2], true, false, "sword", BattleDifficulty.Normal, 3, 30);
        check((lost.Rewards ?? Array.Empty<string>()).Count == 0 && progress.Rewards.Count == 0, "A loss gives no reward");
        var custom = adventure.Apply("c", stages[2], false, true, "sword", BattleDifficulty.Normal, 3, 75);
        check(!custom.Counted && progress.Rewards.Count == 0, "A win with custom data gives no reward");
        var win = adventure.Apply("w", stages[2], true, true, "sword", BattleDifficulty.Normal, 9, 75);
        check(win.Rewards is ["SlimeHat"] && progress.Rewards.SequenceEqual(new[] { "SlimeHat" }), "Winning stage 3 gives the Slime hat");
        var again = adventure.Apply("w2", stages[2], true, true, "sword", BattleDifficulty.Normal, 9, 75);
        var twice = adventure.Apply("w", stages[2], true, true, "sword", BattleDifficulty.Normal, 9, 75);
        check((again.Rewards ?? Array.Empty<string>()).Count == 0 && !twice.Counted && progress.Rewards.Count == 1, "Winning it again (or the same run twice) gives nothing more");
        var older = new AdventureProgress { Completed = { stages[2].Id, stages[4].Id } };
        var late = new Adventure(data.Campaign, older);
        check(late.GrantRewards().Count == 2 && late.GrantRewards().Count == 0 && older.Rewards.Count == 2, "Stages won before their rewards existed give them once");

        // Validation of the new data.
        string Error(string json)
        {
            try { BattleData.Parse("{ \"schemaVersion\": 1, " + json + " }"); return ""; }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return ex.Message; }
        }
        string Enemies(string extra) => "\"waves\": [ { \"groups\": [ { \"enemy\": \"slime\", \"count\": 1 } ] } ], " +
            "\"enemies\": [ { \"id\": \"slime\" }, { \"id\": \"helper\", \"healAmount\": 5 }, { \"id\": \"big\", \"boss\": true, " + extra + " } ]";
        check(Error(Enemies("\"phases\": [ { \"at\": 0.5, \"summon\": \"helper\", \"count\": 2 }, { \"at\": 0.2, \"enrage\": true } ], \"enragedSpeed\": 30, \"enragedWindup\": 0.8")) == "",
            "Valid phases and a healer are accepted");
        check(Error(Enemies("\"phases\": [ { \"at\": 0.5, \"summon\": \"ghost\", \"count\": 1 } ]")).Contains("unknown enemy"), "A phase calling an unknown enemy is rejected");
        check(Error(Enemies("\"phases\": [ { \"at\": 0.5, \"summon\": \"big\", \"count\": 1 } ]")).Contains("summons too"), "A phase calling a summoner is rejected");
        check(Error(Enemies("\"phases\": [ { \"at\": 1.5, \"enrage\": true } ], \"enragedSpeed\": 30, \"enragedWindup\": 0.8")).Contains("between 0 and 1"), "A phase threshold outside 0–1 is rejected");
        check(Error(Enemies("\"phases\": [ { \"at\": 0.5 } ]")).Contains("summon or enrage"), "A phase doing nothing is rejected");
        check(Error("\"waves\": [ { \"groups\": [ { \"enemy\": \"slime\", \"count\": 1 } ] } ], \"enemies\": [ { \"id\": \"slime\", \"healAmount\": 5, \"healRange\": 2 } ]").Contains("healRange"), "A heal range out of bounds is rejected");
        string Stage1(string reward) => "\"campaign\": { \"chapters\": [ { \"id\": \"c\", \"nameVi\": \"C\", \"nameEn\": \"C\", \"stages\": [ { \"id\": \"s\", \"nameVi\": \"S\", \"nameEn\": \"S\", \"reward\": \"" +
            reward + "\", \"waves\": [ { \"groups\": [ { \"enemy\": \"slime\", \"count\": 1 } ] } ] } ] } ] }";
        check(Error(Stage1("SlimeHat")) == "" && Error(Stage1("Bell")).Contains("reward") && Error(Stage1("Nothing")).Contains("reward"),
            "A stage reward must be a reward accessory");
        AccessorySpec[]? Accessory(string reward)
        {
            try { return PetAccessories.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"accessories\":[{\"id\":\"Star\",\"layer\":\"head\",\"reward\":\"" + reward + "\",\"shapes\":[{\"ellipse\":[55,10,3,3],\"fill\":\"#FFD45C\"}]}]}"))); }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return null; }
        }
        check(Accessory("slime_invasion") is [{ Reward: "slime_invasion" }] && Accessory("Bad Reward") == null, "accessories.json reads \"reward\" and rejects a bad one");
    }
}
