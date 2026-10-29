using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// BattleSim: bombs, waves and spawning (squads, edges, threat budget, elite rolls).
public sealed partial class BattleSim
{
    // True while one bomb's blast is dealt (see StepBombs).
    private bool blasting;

    private void StepBombs(double dt)
    {
        for (int i = bombs.Count - 1; i >= 0; i--)
        {
            var bomb = bombs[i];
            bomb.Remaining -= dt;
            if (bomb.Remaining > 0) continue;
            bombs.RemoveAt(i);
            double radius = Settings.BombRadius * (1 + bombBonus);
            AddEffect(BattleEffectKind.Explosion, bomb.X, 0, radius);
            events.Add(new(BattleEventKind.BombExploded, bomb.X));
            // One blast: a healer and the ally it guards caught together keep the guard for it,
            // whichever comes first in the list.
            blasting = true;
            foreach (var slime in slimes.ToArray())
                if (slime.Targetable && Math.Abs(slime.X - bomb.X) <= radius + slime.HitWidth / 2)
                    Damage(slime, Settings.BombDamage * (1 + bombBonus), DamageSource.Bomb);
            blasting = false;
            RefreshGuards();
        }
    }

    private void StepWaves(double dt, double catX)
    {
        if (Phase == BattlePhase.WaveBreak)
        {
            PhaseRemaining = Math.Max(0, PhaseRemaining - dt);
            if (PhaseRemaining > 0) return;
            Wave++;
            pending.Clear(); pendingNext = 0;
            farEdge = Far(catX); streamEdge = 0;
            var wave = WavePlan[Math.Min(Wave, WaveCount) - 1]; // A survive stage repeats its last wave.
            stream = wave.Stream;
            if (stream == null) pending.AddRange(SpawnDirector.Units(wave, spawnRandom, Cost, Settings.Formation, Threat));
            eliteBudget = SpawnDirector.BudgetLeft(wave, pending, Cost);
            Phase = BattlePhase.Fighting;
            spawnTimer = 0;
            AppsWaveStarted();
            lastStandSpent = false;
            events.Add(new(BattleEventKind.WaveStarted, catX, Wave));
            // Infinity's safety net: from AllSynergiesAt every synergy is on, whatever the build.
            if (Endless && Stage?.AllSynergiesAt is int all && Wave >= all)
                for (int i = 0; i < Settings.SynergyList.Count; i++)
                {
                    var y = Settings.SynergyList[i];
                    if (synergies.Contains(y)) continue;
                    synergies.Add(y);
                    armorCut += y.Armor;
                    events.Add(new(BattleEventKind.SynergyUnlocked, catX, i));
                }
        }
        spawnTimer = Math.Max(0, spawnTimer - dt);
        // An announced spawn comes once its arrow has run out and the floor has room; nothing else
        // spawns before it.
        if (incoming != null)
        {
            IncomingRemaining = Math.Max(0, IncomingRemaining - dt);
            if (IncomingRemaining > 0 || Alive + incoming.Length > maxAlive) return;
            SpawnUnit(catX, incoming, IncomingEdge, incomingElite);
            incoming = null; incomingElite = null;
            spawnTimer = NextSpawnIn();
            return;
        }
        if (stream != null)
        {
            // Never more than maxAlive at once, however long the stream runs.
            if (Alive < maxAlive && spawnTimer <= 0)
            {
                var kind = new[] { SpawnDirector.Pick(stream.Weights, spawnRandom) };
                // Its budget: a kind that does not fit gives way to the cheapest; nothing fits, it waits.
                if (stream.Budget > 0 && AliveCost() + Cost(kind[0]) > stream.Budget)
                {
                    kind[0] = SpawnDirector.Cheapest(stream.Weights, Cost);
                    if (AliveCost() + Cost(kind[0]) > stream.Budget) return;
                }
                // Late in the stage the stream comes from both sides in turn, each spawn announced.
                if (stream.PincerAfter > 0 && Stage?.SurviveSeconds is double hold && hold - SurviveRemaining >= stream.PincerAfter)
                {
                    streamEdge = streamEdge == 0 ? Far(catX) : -streamEdge;
                    Announce(catX, kind, streamEdge);
                }
                else { SpawnUnit(catX, kind, 0); spawnTimer = NextSpawnIn(); }
            }
            return; // Only the survive clock ends it.
        }
        // A unit waits until the floor has room for all of it (later ones wait behind it). Side "a"
        // comes from the edge far from the cat, "b" from the other one after a warning arrow.
        if (pendingNext < pending.Count && Alive + pending[pendingNext].Kinds.Length <= maxAlive && spawnTimer <= 0)
        {
            var unit = pending[pendingNext++];
            // A formation's first part: its banner (Value: its index in the data).
            if (unit.Formation != null)
            {
                int index = Settings.FormationList.ToList().FindIndex(f => f.Id == unit.Formation);
                FormationsMet++;
                events.Add(new(BattleEventKind.FormationArrived, catX, index));
            }
            if (unit.Side == "b") Announce(catX, unit.Kinds, -farEdge, unit.Elite);
            else { SpawnUnit(catX, unit.Kinds, unit.Side == "a" ? farEdge : 0, unit.Elite); spawnTimer = NextSpawnIn(); }
        }
        if (ToSpawn > 0 || Alive > 0 || bombs.Count > 0) return;
        WavesCleared++;
        bool last = Wave >= WaveCount && !Survival && !Endless;
        AppsWaveCleared();
        if (last) { Finish(BattlePhase.Won); return; }
        Phase = BattlePhase.WaveBreak;
        PhaseRemaining = Settings.WaveBreakSeconds;
        LastHeal = Math.Max(0, Math.Min(Settings.WaveHeal, CatMaxHp - CatHp));
        CatHp += LastHeal;
        if (LastHeal > 0) AddEffect(BattleEffectKind.Number, catX, 115, -Math.Round(LastHeal));
        events.Add(new(BattleEventKind.WaveCleared, catX));
        if (Stage is { Upgrades: true, UpgradeAt: null } && (Stage.UpgradeEvery is not int every || every <= 1 || Wave % every == 0)) OfferUpgrades();
        // A cleared boss wave in Infinity: a second card after the first.
        if (ChoosingUpgrade && Stage is { BossBonusPick: true } && IsBossWave(Wave)) bonusPicks = 1;
    }

    // Formations sent so far this battle (1.5 M12).
    public int FormationsMet { get; private set; }

    // The edge further from the cat: -1 left, +1 right.
    private int Far(double catX) => catX - Left >= Right - catX ? -1 : 1;

    // The wait before the next spawn: a stream's (shorter as the clock runs), otherwise SpawnInterval.
    private double NextSpawnIn()
    {
        if (stream == null) return Settings.SpawnInterval;
        double progress = Stage?.SurviveSeconds is double hold && hold > 0 ? Math.Clamp(1 - SurviveRemaining / hold, 0, 1) : 0;
        return stream.Interval + (stream.IntervalEnd - stream.Interval) * progress;
    }

    // A spawn from behind: an arrow at that edge first (IncomingWarning s). A cat standing at that
    // edge gets it from the other edge instead, unannounced (never on top of the cat).
    private void Announce(double catX, string[] kinds, int edge, bool[]? elite = null)
    {
        double x = EdgePoint(edge, Settings.SlimeWidth * kinds.Max(k => Settings.Enemy(k).Scale), 0);
        if (Math.Abs(x - catX) < Settings.SpawnMinDistance)
        {
            SpawnUnit(catX, kinds, -edge, elite);
            spawnTimer = NextSpawnIn();
            return;
        }
        incoming = kinds;
        incomingElite = elite;
        IncomingEdge = edge;
        IncomingX = x;
        IncomingRemaining = Settings.IncomingWarning;
        events.Add(new(BattleEventKind.Incoming, x));
    }

    private void SpawnUnit(double catX, string[] kinds, int edge, bool[]? elite = null)
    {
        if (kinds.Length == 1) Spawn(catX, Settings.Enemy(kinds[0]), edge, elite?[0] == true);
        else SpawnSquad(catX, kinds.Select(Settings.Enemy).ToArray(), edge, elite);
    }

    // edge: -1 / +1 that edge, 0 either. A lone support (no edge asked) joins the fighting ally
    // furthest from the cat, on its far side, when there is one.
    private void Spawn(double catX, EnemyType type, int edge = 0, bool elite = false)
    {
        double w = Settings.SlimeWidth * type.Scale;
        double x = SpawnPoint(catX, w, edge);
        if (type.Support && edge == 0)
        {
            Slime? back = null;
            foreach (var s in slimes)
                if (s.State != SlimeState.Defeated && !s.Type.Support && !s.Shielded && (back == null || Math.Abs(s.X - catX) > Math.Abs(back.X - catX))) back = s; // Even one still appearing.
            if (back != null)
            {
                int side = back.X >= catX ? 1 : -1;
                double nearAlly = Math.Clamp(back.X + side * (back.Width / 2 + w / 2 + 10), Left + w / 2, Math.Max(Left + w / 2, Right - w / 2));
                if (Math.Abs(nearAlly - catX) >= SquadCatGap) x = nearAlly;
            }
        }
        var slime = AddSlime(x, type);
        slime.Facing = catX < x ? -1 : 1;
        events.Add(new(type.Boss ? BattleEventKind.BossAppeared : BattleEventKind.SlimeSpawned, x));
        if (elite) ForceElite(slime); else RollElite(slime);
    }

    // What an enemy weighs against a budget (EnemyType.Cost).
    private double Cost(string kind) => Settings.Enemy(kind).Cost;

    // What the enemies the waves sent cost now (a boss's helpers aside), elites with their surcharge.
    private double AliveCost()
    {
        double cost = 0;
        foreach (var s in slimes)
            if (s.State != SlimeState.Defeated && s.SummonedBy == 0 && !s.Decoy) cost += s.Type.Cost + (s.Elite?.Cost ?? 0);
        return cost;
    }

    // An enemy a wave or stream just sent may turn elite: a stage with an elite chance, not on Easy,
    // a kind with an affix, fewer than MaxElites alive, budget for it; then one roll of its own stream.
    private void RollElite(Slime slime)
    {
        if (Stage is not { EliteChance: > 0 } stage || Difficulty == BattleDifficulty.Easy || slime.Type.Elite is not EliteSpec elite) return;
        int elites = 0;
        foreach (var s in slimes) if (s.Elite != null && !s.Rallied && !s.Spat && !s.Decoy && s.State != SlimeState.Defeated) elites++;
        // Infinity: more elites as the waves go on (EliteChancePerWave, MoreElitesEvery).
        int most = Settings.MaxElites + (stage.MoreElitesEvery > 0 ? Math.Min(stage.MoreElitesMax, Wave / stage.MoreElitesEvery) : 0);
        if (elites >= most) return;
        double chance = stage.EliteChance + stage.EliteChancePerWave * Math.Max(0, Wave - 1);
        if (stage.EliteChanceMax > 0) chance = Math.Min(chance, stage.EliteChanceMax);
        if (stream is { Budget: > 0 } flow ? AliveCost() + elite.Cost > flow.Budget : elite.Cost > eliteBudget) return;
        if (affixRandom.NextDouble() >= chance) return;
        if (stream == null) eliteBudget -= elite.Cost;
        ApplyElite(slime, elite);
        ElitesSpawned++;
        events.Add(new(BattleEventKind.EliteAppeared, slime.X, slime.Id));
    }

    // A group the data marks elite (WaveGroup.Elite): elite whatever the chance, the difficulty or the cap.
    private void ForceElite(Slime slime)
    {
        if (slime.Type.Elite is not EliteSpec elite || slime.Elite != null) return;
        ApplyElite(slime, elite);
        ElitesSpawned++;
        events.Add(new(BattleEventKind.EliteAppeared, slime.X, slime.Id));
    }

    // A point near an edge (-1 left, +1 right), up to `spread` DIPs in.
    private double EdgePoint(int edge, double w, double spread)
    {
        double minX = Left + w / 2, maxX = Math.Max(minX, Right - w / 2);
        return edge < 0 ? Math.Min(maxX, minX + spawnRandom.NextDouble() * spread) : Math.Max(minX, maxX - spawnRandom.NextDouble() * spread);
    }

    // Near one edge: the asked one, or a random one at least SpawnMinDistance from the cat when room allows.
    private double SpawnPoint(double catX, double w, int edge = 0)
    {
        if (edge != 0) return EdgePoint(edge, w, 30);
        double minX = Left + w / 2, maxX = Math.Max(minX, Right - w / 2);
        double left = Math.Min(maxX, minX + spawnRandom.NextDouble() * 60), right = Math.Max(minX, maxX - spawnRandom.NextDouble() * 60);
        double first = spawnRandom.Next(2) == 0 ? left : right, second = first == left ? right : left;
        return Math.Abs(first - catX) >= Settings.SpawnMinDistance ? first
            : Math.Abs(second - catX) >= Settings.SpawnMinDistance ? second
            : Math.Abs(first - catX) >= Math.Abs(second - catX) ? first : second;
    }

    // Closest a squad member appears to the cat when the floor allows (centre to centre).
    public const double SquadCatGap = 90;

    // A squad appears together near one edge, side by side: its supports at the back (the edge),
    // its fighters in front of them toward the cat, never on top of the cat when there is room.
    private void SpawnSquad(double catX, EnemyType[] members, int edge = 0, bool[]? elite = null)
    {
        // Supports first (each keeps its own elite flag).
        var ranked = members.Select((m, i) => (Type: m, Elite: elite?[i] == true)).ToArray();
        var flags = ranked.Where(m => m.Type.Support).Concat(ranked.Where(m => !m.Type.Support)).Select(m => m.Elite).ToArray();
        var order = members.Where(m => m.Support).Concat(members.Where(m => !m.Support)).ToArray();
        double back = SpawnPoint(catX, Settings.SlimeWidth * order.Max(m => m.Scale), edge);
        int toward = catX >= back ? 1 : -1;
        // Spacing a little wider than Separate keeps, so they do not shove each other on arrival;
        // squeezed (down to a third of it) when the cat stands too close to fit them all.
        var gaps = new double[order.Length];
        for (int i = 1; i < order.Length; i++) gaps[i] = Settings.SlimeWidth * (order[i].Scale + order[i - 1].Scale) / 2 * 0.8;
        double span = gaps.Sum(), room = Math.Abs(catX - back) - SquadCatGap;
        double squeeze = span > 0 && room < span ? Math.Clamp(room / span, 1.0 / 3, 1) : 1;
        double x = back;
        for (int i = 0; i < order.Length; i++)
        {
            x += toward * gaps[i] * squeeze;
            var slime = AddSlime(x, order[i]);
            slime.Facing = catX < slime.X ? -1 : 1;
            if (flags[i]) ForceElite(slime); else RollElite(slime);
        }
        events.Add(new(BattleEventKind.SlimeSpawned, back));
    }

    internal Slime AddSlime(double x, EnemyType type)
    {
        // Difficulty × the threat of now, once. Scaled numbers round to whole ones so the HUD and
        // damage numbers stay tidy; Normal at threat 1 is untouched.
        var (threatHp, threatDamage) = SpawnDirector.Scale(Threat, Settings.ThreatDamageShare);
        double hpFactor = level.EnemyHp * threatHp, damageFactor = level.EnemyDamage * threatDamage;
        double hp = hpFactor != 1 ? Math.Max(1, Math.Round(type.MaxHp * hpFactor)) : type.MaxHp;
        var slime = new Slime
        {
            Id = nextId++, Type = type, Width = Settings.SlimeWidth * type.Scale, HitWidth = Settings.SlimeWidth * type.Hitbox, Height = Settings.SlimeHeight * type.Scale,
            Hp = hp, MaxHp = hp, Damage = damageFactor != 1 ? Math.Round(type.Damage * damageFactor) : type.Damage, Range = type.Range
        };
        // A support enemy starts with half its heal cooldown and its whole budget; its heals (and the
        // budget) grow with the same factor as enemy HP, so healing keeps up on later stages.
        slime.HealCooldown = type.HealCooldown / 2;
        slime.HealPower = hpFactor;
        slime.HealLeft = hpFactor != 1 ? Math.Round(type.HealBudget * hpFactor) : type.HealBudget;
        slime.Altitude = type.Hover;
        slime.SpitCooldown = (type.Spit?.Cooldown ?? 0) / 2;
        // 1.5 M13: a ghost starts seen, a bubble slime in its bubble.
        slime.GhostClock = type.Ghost?.Visible ?? 0;
        slime.Bubbled = type.Bubble != null;
        var (minX, maxX) = Bounds(slime);
        slime.X = Math.Clamp(x, minX, maxX);
        slime.Set(SlimeState.Spawning);
        JoinLinks(slime);
        slimes.Add(slime);
        return slime;
    }
}
