using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// The whole fight, without WPF: slimes, the boss, waves, targeting, the sword and the
// user's skills. Time only advances through Step (simulation time), so Pause freezes
// everything. The window turns CatOrder into the pet's normal walking; physics stays in PetPhysics.
public sealed partial class BattleSim
{
    public BattleSettings Settings { get; }
    // This battle's seed (BattleRandom) and its streams: what spawns where, the cards, the rock's
    // blocks, the elite rolls, the rare cards and the desktop apps.
    public int Seed { get; }
    private readonly Random spawnRandom, offerRandom, blockRandom, affixRandom, rarityRandom, appRandom;
    private readonly List<Slime> slimes = new();
    private readonly List<BattleEffect> effects = new();
    private readonly List<BattleEvent> events = new();
    private readonly List<BattleBomb> bombs = new();
    private readonly double[] cooldowns = new double[5];
    public IReadOnlyList<Slime> Slimes => slimes;
    public IReadOnlyList<BattleEffect> Effects => effects;
    public IReadOnlyList<BattleEvent> Events => events;
    public IReadOnlyList<BattleBomb> Bombs => bombs;
    public BattlePhase Phase { get; private set; } = BattlePhase.WaveBreak;
    public bool Paused { get; set; }
    public double Left { get; private set; }
    public double Right { get; private set; }
    public double CatHp { get; private set; }
    // 1-based number of the current (or last started) wave; 0 before the first.
    public int Wave { get; private set; }
    // The waves of this battle: an Adventure stage's own, otherwise the free battle's.
    public IReadOnlyList<WaveSpec> WavePlan { get; }
    public int WaveCount => WavePlan.Count;
    // The Adventure stage being played (null: a free battle).
    public StageSpec? Stage { get; }
    // The app icons are on (the setup panel's choice; a free battle never has them).
    public bool AppsOn { get; }
    private readonly int maxAlive;
    // Survive stages: seconds still to hold out (counts only while the battle runs and the cat is
    // on the ground, like the waves). NaN for any other battle.
    public bool Survival => Stage?.Survival == true;
    // Infinity: never won; the furthest wave reached is what counts.
    public bool Endless => Stage?.Endless == true;
    public double SurviveRemaining { get; private set; } = double.NaN;

    // This run's build (Adventure stages that allow upgrades): how often each card was taken (the
    // build carried in from the stage before included), and the cards on offer while the user
    // chooses. While choosing, the battle stands still (Step does nothing, the clock and the record
    // time stop). The definitions are never changed; Build() is what a won stage passes on.
    private readonly Dictionary<string, int> taken = new();
    public IReadOnlyDictionary<string, int> UpgradesTaken => taken;
    public IReadOnlyList<UpgradeSpec>? UpgradeOffer { get; private set; }
    public bool ChoosingUpgrade => UpgradeOffer != null;
    private double damageBonus, moveBonus, cooldownCut, knockBonus, armorCut, maxHpBonus;
    // Rare cards: Turtle shell (its regrowth time; 0 without it), Opener and Momentum bonuses.
    private double shellRegrow, openerBonus, momentumBonus;
    // Turtle shell: seconds until it is back (0: ready); hits it stopped.
    public double ShellCooldown { get; private set; }
    public bool HasShell => shellRegrow > 0;
    public bool ShellReady => HasShell && ShellCooldown <= 0;
    public int ShellBlocks { get; private set; }
    // Momentum: DIPs the cat has run on the floor since its last swing, and whether the swing under
    // way is a charged one.
    public const double MomentumRun = 140;
    private double runDistance, runFromX = double.NaN;
    private bool chargedSwing;
    public bool MomentumReady => momentumBonus > 0 && runDistance >= MomentumRun;
    public int ChargedHits { get; private set; }
    public int OpenerHits { get; private set; }
    // Elites spawned in this battle; what the current wave's budget still has for elites.
    public int ElitesSpawned { get; private set; }
    private double eliteBudget = double.PositiveInfinity;
    public int Stacks(string upgrade) => taken.TryGetValue(upgrade, out int n) ? n : 0;
    // Cards chosen in this run (not the carried ones): a stage's maxUpgrades counts these.
    public int Picks { get; private set; }
    public double CatMaxHp => Settings.CatMaxHp + maxHpBonus;
    // A group's points: one per card of that group in the build.
    public int GroupPoints(string group)
    {
        int points = 0;
        foreach (var (id, n) in taken) if (Upgrade(id)?.Group == group) points += n;
        return points;
    }
    private UpgradeSpec? Upgrade(string id) { foreach (var u in Settings.UpgradeList) if (u.Id == id) return u; return null; }
    // Synergies the build has switched on (they stay on for the rest of the build).
    private readonly List<SynergySpec> synergies = new();
    public IReadOnlyList<SynergySpec> Synergies => synergies;
    private SynergySpec? Active(string effect) { foreach (var y in synergies) if (y.Effect == effect) return y; return null; }
    // The synergy this card would switch on now (null: none).
    public SynergySpec? Unlocks(UpgradeSpec card)
    {
        if (card.Group == null) return null;
        int points = GroupPoints(card.Group) + 1;
        foreach (var y in Settings.SynergyList)
            if (y.Group == card.Group && !synergies.Contains(y) && points >= y.At) return y;
        return null;
    }
    // The build to carry on: every card but the instant heals.
    public Dictionary<string, int> Build()
    {
        var build = new Dictionary<string, int>();
        foreach (var (id, n) in taken) if (n > 0 && Upgrade(id) is { Effect: not "heal" }) build[id] = n;
        return build;
    }
    public int Reflects { get; private set; }
    public int Echoes { get; private set; }
    // Hits stony enemies blocked; hits a shielded boss shrugged off; seconds a boss shield was up.
    public int BlocksMade { get; private set; }
    public int ImmuneHits { get; private set; }
    // Hits on an enemy guarded by a heal (HealGuard).
    public int WardedHits { get; private set; }
    public double BossShieldSeconds { get; private set; }
    private int landedSwings;
    // Where the cat stood at the last Step (a boss shield phase runs away from it).
    internal double lastCatX;
    private double groundX = double.NaN, liftedFor;
    // Multipliers the upgrades give (1 without any). Cuts never go below 30 % of the original.
    public double DamageFactor => 1 + damageBonus;
    public double MoveFactor => 1 + moveBonus;
    public double CooldownFactor => Math.Max(0.3, 1 - cooldownCut);
    public double ArmorFactor => Math.Max(0.3, 1 - armorCut);
    public double KnockBonus => knockBonus;
    // The cat's numbers with this build and these relics, before anything that only happens in a
    // fight (Cheer, combo, Momentum, Reckless's bonus): what the inventory shows.
    public double SwingDamage => arms.Damage * DamageFactor;
    public double SwingsPerSecond => 1 / Math.Max(arms.AttackCooldown * CooldownFactor, arms.SwingDuration);
    public double Knock => arms.Knock + knockBonus;
    public double HurtFactor => ArmorFactor * (1 + recklessHurt);
    public bool IsBossWave(int wave)
    {
        if (wave < 1 || wave > WaveCount) return false;
        foreach (var group in WavePlan[wave - 1].Groups) if (group.Fixed && Settings.Enemy(group.Enemy).Boss) return true;
        return false;
    }
    // Enemies of the current wave still to come, in spawn order: each unit (one enemy or a squad)
    // comes as one. A stream wave has none queued: it draws as it goes.
    private readonly List<SpawnUnit> pending = new();
    private int pendingNext;
    private StreamSpec? stream;
    public bool Streaming => stream != null;
    public int ToSpawn { get { int n = incoming?.Length ?? 0; for (int i = pendingNext; i < pending.Count; i++) n += pending[i].Kinds.Length; return n; } }
    // Two-sided waves: the edge far from the cat when the wave began (-1 left, +1 right), the edge a
    // stream used last, and a spawn announced by an arrow at IncomingX (IncomingEdge) that comes in
    // IncomingRemaining s.
    private int farEdge, streamEdge;
    private string[]? incoming;
    public double IncomingRemaining { get; private set; }
    public double IncomingX { get; private set; }
    public int IncomingEdge { get; private set; }
    public bool Incoming => incoming != null;
    // Survive stages: the card offers of UpgradeAt already made.
    private int timedOffers;
    // An Expedition run's relics (PetExpedition.cs RelicSpec) and what they change; none outside one.
    private readonly List<RelicSpec> relics = new();
    public IReadOnlyList<RelicSpec> Relics => relics;
    private double counterBonus, shieldDelay, fishBonus, fishDelay, recklessBonus, recklessBelow, recklessHurt;
    // Counter bell: the Shield or the Turtle shell stopped a hit, the next landed swing hits harder.
    public bool CounterReady { get; private set; }
    public int CounterHits { get; private set; }
    public bool Reckless => recklessBonus > 0 && CatHp < recklessBelow * CatMaxHp;
    // An Expedition step makes its stage this much stronger (1 otherwise).
    private readonly double threatScale = 1;
    // The threat new enemies get now: the stage's (× an Expedition step's) × the current wave's,
    // growing with the survive clock.
    public double Threat
    {
        get
        {
            if (Stage == null) return 1;
            double threat = Stage.Threat * threatScale * (Wave >= 1 ? WavePlan[Math.Min(Wave, WaveCount) - 1].Threat : 1);
            if (Survival && Stage.SurviveSeconds is double hold) threat *= 1 + Stage.ThreatGrowth * (hold - SurviveRemaining) / 60;
            return threat;
        }
    }
    public int Defeated { get; private set; }
    // Waves cleared so far (Infinity's milestones count these).
    public int WavesCleared { get; private set; }
    public bool BossDefeated { get; private set; }
    public EnemyType? DefeatedBoss { get; private set; }
    public double PhaseRemaining { get; private set; }
    public double CatCooldown { get; private set; }
    // Seconds into the current swing; negative when not swinging.
    public double SwingTime { get; private set; } = -1;
    public double SwingProgress => SwingTime < 0 ? -1 : Math.Clamp(SwingTime / arms.SwingDuration, 0, 1);
    // Hits in the current chain, and whether the swing under way would finish it (a gold slash).
    public int Combo { get; internal set; }
    public bool ComboSwing { get; private set; }
    public int CombosLanded { get; private set; }
    internal double comboClock;
    public int? SelectedId { get; private set; }
    // The slime the cat is going for (selected or chosen automatically).
    public int? TargetId { get; private set; }
    public BattleSkill? Armed { get; private set; }
    public bool PushArmed => Armed == BattleSkill.Push;
    public double PushCooldown => cooldowns[(int)BattleSkill.Push];
    public double CheerRemaining { get; private set; }
    public double ShieldRemaining { get; private set; }
    public double Grace { get; private set; }
    // Until this reaches 0, putting the cat down gives no new protection.
    public double GraceCooldown { get; private set; }
    public bool RescueGraceReady => GraceCooldown <= 0;
    // How the user is handling the cat (SetCatState): held = pressed, carried, in the air or
    // climbing (the battle may not move it); lifted = really off the ground (not a click). A cat
    // that is only pressed is still on the floor: an attack already under way reaches it (no stun).
    public bool CatHeld { get; private set; }
    public bool CatLifted { get; private set; }
    // While the cat is safe in the user's hands the skills wait: no use, no cooldown recovery.
    public bool SkillsLocked => CatHeld;
    public double HurtFlash { get; private set; }
    // The cat is stunned (seconds left; 0: not) and, after a stun, cannot be stunned again yet.
    public double CatStunRemaining { get; private set; }
    public double StunImmunity { get; private set; }
    public bool CatStunned => CatStunRemaining > 0;
    public int StunsTaken { get; private set; }
    // Simulation clock (stops while paused); animations read it instead of wall time.
    public double Time { get; internal set; }
    // HP given back after the last cleared wave, and the battle's length (for the result panel).
    public double LastHeal { get; private set; }
    public double FightSeconds { get; private set; }
    public bool Finished => Phase is BattlePhase.Won or BattlePhase.Lost && PhaseRemaining <= 0;
    public bool Over => Phase is BattlePhase.Won or BattlePhase.Lost;
    public Slime? Boss { get { foreach (var s in slimes) if (s.IsBoss && s.State != SlimeState.Defeated) return s; return null; } }
    private int nextId = 1, autoTargetId, swingTargetId, swingFacing = 1;
    // The forced update the swing under way is aimed at (0: a slime).
    private int swingWallId;
    private bool swingHit, wasLifted;
    private double spawnTimer;

    public BattleDifficulty Difficulty { get; }
    private readonly DifficultySpec level;
    // This battle's weapon (its definition is shared; the swing and cooldown are below).
    public WeaponSpec Weapon { get; }
    private readonly WeaponSpec arms;

    public BattleSim(BattleSettings settings, double left, double right, Random? random = null, BattleDifficulty difficulty = BattleDifficulty.Normal,
        string weapon = BattleWeapon.Sword, StageSpec? stage = null, IReadOnlyDictionary<string, int>? build = null, int? seed = null,
        double? startHp = null, double threat = 1, IReadOnlyList<string>? relics = null, bool apps = true)
    {
        // A given seed wins; otherwise one drawn from the given random (tests), else a fresh one.
        Seed = seed ?? random?.Next() ?? Random.Shared.Next();
        spawnRandom = BattleRandom.Stream(Seed, BattleRandom.Spawn);
        offerRandom = BattleRandom.Stream(Seed, BattleRandom.Offer);
        blockRandom = BattleRandom.Stream(Seed, BattleRandom.Block);
        affixRandom = BattleRandom.Stream(Seed, BattleRandom.Affix);
        rarityRandom = BattleRandom.Stream(Seed, BattleRandom.Rarity);
        appRandom = BattleRandom.Stream(Seed, BattleRandom.Apps);
        Settings = settings;
        Stage = stage;
        AppsOn = apps;
        WavePlan = stage?.Waves is { Length: > 0 } waves ? waves : settings.WavePlan;
        maxAlive = stage?.MaxAlive ?? settings.MaxAlive;
        if (stage is { Survival: true, SurviveSeconds: double hold }) SurviveRemaining = hold;
        Difficulty = difficulty;
        Weapon = arms = settings.Weapon(weapon);
        level = settings.Difficulty(difficulty);
        Left = left; Right = Math.Max(left + settings.SlimeWidth * settings.LargestScale, right);
        // A carried build (the stage before's): its cards as if taken, within their limits; cards no
        // longer in the data are skipped.
        if (build != null)
            foreach (var (id, count) in build)
                if (Upgrade(id) is UpgradeSpec card && card.Effect != "heal")
                    for (int i = 0; i < count && (card.MaxStacks == 0 || Stacks(id) < card.MaxStacks); i++) Take(card, 0, false);
        // An Expedition run's relics (unknown ones skipped), each its own modifier.
        if (relics != null && settings.Expedition is ExpeditionSpec expedition)
            foreach (var id in relics)
                if (expedition.Relic(id) is RelicSpec relic && !this.relics.Contains(relic)) AddRelic(relic);
        // An Expedition step starts with the HP the last one ended with (within the build's max).
        CatHp = startHp is double hp && double.IsFinite(hp) ? Math.Clamp(hp, 1, CatMaxHp) : CatMaxHp;
        if (stage != null && double.IsFinite(threat) && threat > 0) threatScale = threat;
        PhaseRemaining = settings.FirstWaveDelay;
    }

    public int Alive { get { int n = 0; foreach (var s in slimes) if (s.State != SlimeState.Defeated) n++; return n; } }
    public Slime? Find(int id) { foreach (var s in slimes) if (s.Id == id) return s; return null; }
    public void ClearEvents() => events.Clear();
    public double Cooldown(BattleSkill skill) => cooldowns[(int)skill];
    public static bool IsAimed(BattleSkill skill) => BattleSkills.Get(skill).Aimed;
    public bool Ready(BattleSkill skill) => !Over && !Paused && !ChoosingUpgrade && !SkillsLocked && cooldowns[(int)skill] <= 0;

    // The one description of the user's hold on the cat, shared by Step, the skills and the HUD.
    // A real lift cancels a chosen skill at once, so a later click cannot use it by mistake;
    // a plain press does not (it changes nothing visible).
    public void SetCatState(bool held, bool lifted)
    {
        CatHeld = held;
        CatLifted = held && lifted;
        if (!CatLifted) return;
        wasLifted = true;
        Armed = null;
    }

    // catHeld / lifted: see SetCatState. Putting a lifted cat down grants grace (if ready).
    public CatOrder Step(double dt, double catX, bool catHeld, bool lifted = false)
    {
        SetCatState(catHeld, lifted);
        if (Paused || ChoosingUpgrade || !double.IsFinite(dt) || dt <= 0 || !double.IsFinite(catX)) return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        dt = Math.Min(dt, 0.1);
        Time += dt;
        // While the cat is lifted the slimes go by where it last stood on the floor, not by the
        // cursor carrying it.
        if (!CatLifted || !double.IsFinite(groundX)) groundX = catX;
        double floorX = groundX;
        lastCatX = floorX;
        StepEffects(dt);
        HurtFlash = Math.Max(0, HurtFlash - dt);
        // The stun runs on battle time, held or not (lifting neither ends nor stretches it).
        if (CatStunRemaining > 0)
        {
            CatStunRemaining = Math.Max(0, CatStunRemaining - dt);
            if (CatStunRemaining <= 0) StunImmunity = Settings.StunImmunitySeconds;
        }
        else StunImmunity = Math.Max(0, StunImmunity - dt);
        // Skills recover only while the cat is down and at risk; buffs already running wear off as usual.
        // So does the Turtle shell; Momentum counts only the cat's own run on the floor.
        if (!catHeld)
        {
            for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = Math.Max(0, cooldowns[i] - dt);
            ShellCooldown = Math.Max(0, ShellCooldown - dt);
            if (!double.IsNaN(runFromX)) runDistance += Math.Abs(catX - runFromX);
            runFromX = catX;
        }
        else runFromX = double.NaN;
        CheerRemaining = Math.Max(0, CheerRemaining - dt);
        ShieldRemaining = Math.Max(0, ShieldRemaining - dt);
        if (Over)
        {
            PhaseRemaining = Math.Max(0, PhaseRemaining - dt);
            StepSlimes(dt, floorX, true);
            return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        }
        comboClock += dt;
        if (CatLifted) liftedFor += dt; // How long this rescue has lasted (battle time).
        if (catHeld) { CancelSwing(); Combo = 0; }
        else
        {
            // Landing after a real lift: protection, unless it was given too recently (then the
            // time left, if any, is neither renewed nor extended).
            if (wasLifted && liftedFor >= Settings.RescueGraceMinLiftSeconds && GraceCooldown <= 0)
            {
                Grace = Settings.LandingGraceSeconds;
                GraceCooldown = Settings.RescueGraceCooldownSeconds;
            }
            wasLifted = false;
            liftedFor = 0;
            Grace = Math.Max(0, Grace - dt);
            GraceCooldown = Math.Max(0, GraceCooldown - dt);
        }
        CatCooldown = Math.Max(0, CatCooldown - dt);
        StepBombs(dt);
        StepSlimes(dt, floorX, catHeld);
        if (Over) return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        StepApps(dt, floorX, catHeld);
        if (Over) return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        // Holding the cat freezes the waves (no spawning, no progress) and a survive stage's clock.
        if (!catHeld) StepWaves(dt, catX);
        if (!catHeld && Survival && !Over && !ChoosingUpgrade)
        {
            SurviveRemaining = Math.Max(0, SurviveRemaining - dt);
            if (SurviveRemaining <= 0) Finish(BattlePhase.Won);
            // Timed card offers (the clock stands still while the user chooses).
            else if (Stage is { Upgrades: true, UpgradeAt: { } offers, SurviveSeconds: double hold } && timedOffers < offers.Length && hold - SurviveRemaining >= offers[timedOffers])
            {
                timedOffers++;
                OfferUpgrades();
            }
        }
        if (ChoosingUpgrade) return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        if (catHeld) { TargetId = PickTarget(floorX)?.Id; return new(CatCommand.Hold); }
        return CatTurn(dt, catX);
    }

    private void CancelSwing() { SwingTime = -1; swingHit = false; ComboSwing = false; chargedSwing = false; swingWallId = 0; }

    // The chain only lasts ComboWindow seconds after its last hit.
    // Hits in the current chain (0 once it lapsed); the HUD shows it as ×N.
    public int LiveCombo => Combo > 0 && comboClock <= Settings.ComboWindow ? Combo : 0;

    private CatOrder CatTurn(double dt, double catX)
    {
        var target = Over ? null : PickTarget(catX);
        TargetId = target?.Id;
        if (SwingTime >= 0)
        {
            SwingTime += dt;
            if (!swingHit && SwingTime >= arms.HitTime) { swingHit = true; ResolveHit(catX); }
            if (SwingTime >= arms.SwingDuration) CancelSwing();
            return new(CatCommand.Stand, catX, swingFacing);
        }
        BlockedById = 0;
        if (CatStunned) return new(CatCommand.Stand, catX);
        // Put down on top of a blocker: a few steps off it to the nearest free side.
        if (Unstick(catX) is double free) return new(CatCommand.Move, free, free < catX ? -1 : 1);
        if (target == null) return new(CatCommand.Stand, catX);
        // A forced update: put down in it, the cat steps out; between the cat and its target, the cat
        // walks up to it and hits it (each hit brings the update closer to done).
        if (WallAt(catX) is BattleHazard inWall)
        {
            double outside = Math.Clamp(inWall.X + (catX < inWall.X ? -1 : 1) * (WallHalfWidth + CatHalfWidth + 4), Left + CatHalfWidth, Right - CatHalfWidth);
            return new(CatCommand.Move, outside, outside < catX ? -1 : 1);
        }
        if (WallBetween(catX, target.X) is BattleHazard update)
        {
            int toWall = update.X < catX ? -1 : 1;
            if (Math.Abs(update.X - catX) - WallHalfWidth <= arms.Range * 0.8)
            {
                if (CatCooldown <= 0)
                {
                    SwingTime = 0; swingHit = false; swingTargetId = 0; swingWallId = update.Id; swingFacing = toWall;
                    ComboSwing = false; chargedSwing = false; runDistance = 0;
                    CatCooldown = Math.Max(arms.AttackCooldown * CooldownFactor / LagFactor, arms.SwingDuration);
                    events.Add(new(BattleEventKind.Swing, catX));
                }
                return new(CatCommand.Stand, catX, toWall);
            }
            return new(CatCommand.Move, update.X - toWall * (WallHalfWidth + arms.Range * 0.5), toWall);
        }
        // A blocker on the floor between the cat and its target is hit first (the target keeps its marker).
        if (BlockerBetween(catX, target) is Slime wall) { BlockedById = wall.Id; target = wall; }
        double dx = target.X - catX;
        int facing = dx < 0 ? -1 : 1;
        double reach = Reach(target);
        if (Math.Abs(dx) <= reach)
        {
            if (CatCooldown <= 0)
            {
                SwingTime = 0; swingHit = false; swingTargetId = target.Id; swingFacing = facing;
                ComboSwing = LiveCombo == Settings.ComboHits - 1;
                // Momentum: a swing after a run is a charged one (the run starts again either way).
                chargedSwing = MomentumReady;
                runDistance = 0;
                double cooldown = arms.AttackCooldown * CooldownFactor / (CheerRemaining > 0 ? Settings.CheerSpeed : 1) / LagFactor;
                CatCooldown = Math.Max(cooldown, arms.SwingDuration);
                events.Add(new(BattleEventKind.Swing, catX));
            }
            return new(CatCommand.Stand, catX, facing);
        }
        return new(CatCommand.Move, target.X - facing * reach * 0.7, facing);
    }

    // The blocker standing in the cat's way to its target (null: the way is clear).
    public int BlockedById { get; private set; }

    private Slime? BlockerBetween(double catX, Slime target)
    {
        double dx = target.X - catX;
        Slime? wall = null;
        foreach (var s in slimes)
            if (s != target && s.Type.Blocker && s.Targetable && (s.X - catX) * dx > 0 && Math.Abs(s.X - catX) < Math.Abs(dx) &&
                (wall == null || Math.Abs(s.X - catX) < Math.Abs(wall.X - catX))) wall = s;
        return wall;
    }

    // Where the cat goes when it stands inside a blocker (null: it does not): just past the blocker's
    // edge on the cat's side, or the other side when that spot is in another blocker or off the floor.
    private double? Unstick(double catX)
    {
        Slime? inside = null;
        foreach (var s in slimes)
            if (s.Type.Blocker && s.Targetable && Math.Abs(s.X - catX) < s.HitWidth / 2 + CatHalfWidth - 4) { inside = s; break; }
        if (inside == null) return null;
        double clear = inside.HitWidth / 2 + CatHalfWidth + 4; // The window stops walking within 6 DIPs of an order.
        double near = inside.X + (catX < inside.X ? -clear : clear), far = inside.X + (catX < inside.X ? clear : -clear);
        bool Free(double x)
        {
            if (x < Left + CatHalfWidth || x > Right - CatHalfWidth) return false;
            foreach (var s in slimes) if (s.Type.Blocker && s.Targetable && Math.Abs(s.X - x) < s.HitWidth / 2 + CatHalfWidth - 4) return false;
            return true;
        }
        // Neither side free (blockers side by side): the near side; the next step frees it from the next one.
        return Free(near) || !Free(far) ? Math.Clamp(near, Left + CatHalfWidth, Right - CatHalfWidth) : far;
    }

    // A wider slime is reached from further away (the sword hits its near edge).
    private double Reach(Slime slime) => arms.Range + (slime.HitWidth - Settings.SlimeWidth) / 2;

    // The swing's single hit: only its own target, only if still in reach at this moment.
    private void ResolveHit(double catX)
    {
        if (swingWallId != 0)
        {
            if (FindHazard(swingWallId) is BattleHazard wall && Math.Abs(wall.X - catX) - WallHalfWidth <= arms.Range + 14) HitWall(wall);
            swingWallId = 0;
            return;
        }
        var slime = Find(swingTargetId);
        if (slime == null || !slime.Targetable || Math.Abs(slime.X - catX) > Reach(slime) + 14) { Combo = 0; return; } // A miss ends the chain.
        Combo = LiveCombo + 1;
        comboClock = 0;
        bool combo = Combo >= Settings.ComboHits;
        double damage = arms.Damage * DamageFactor * (CheerRemaining > 0 ? Settings.CheerDamage : 1) * (combo ? Settings.ComboDamage : 1);
        // Armor break: combo hits hit harder.
        if (combo && Active("armorBreak") is SynergySpec breaker) damage *= 1 + breaker.Amount;
        // Rare cards: the first landed hit on each enemy (Opener) and a swing after a run (Momentum)
        // hit harder; an Afterimage echo repeats the swing without them.
        double plain = damage;
        bool opening = openerBonus > 0 && !slime.Opened, charged = chargedSwing;
        chargedSwing = false;
        if (opening) damage *= 1 + openerBonus;
        if (charged) damage *= 1 + momentumBonus;
        if (Reckless) damage *= 1 + recklessBonus;
        bool countered = CounterReady;
        if (countered) damage *= 1 + counterBonus;
        double knock = arms.Knock + knockBonus;
        events.Add(new(combo ? BattleEventKind.ComboHit : BattleEventKind.SlimeHit, slime.X));
        AddEffect(combo || charged || countered ? BattleEffectKind.Combo : arms.Effect == "thud" ? BattleEffectKind.Thud : BattleEffectKind.Spark, slime.X, slime.Height / 2);
        if (combo) { Combo = 0; CombosLanded++; }
        // A blocked or shrugged-off hit (or the last one) moves nothing.
        var result = Damage(slime, damage, combo ? DamageSource.Combo : DamageSource.Weapon);
        // Only a hit that lands opens an enemy (a blocked one leaves the Opener bonus for the next).
        if (result == HitResult.Landed)
        {
            slime.Opened = true;
            if (opening) OpenerHits++;
            if (charged) ChargedHits++;
            if (countered) { CounterReady = false; CounterHits++; }
        }
        bool landed = result == HitResult.Landed && slime.State != SlimeState.Defeated;
        // A combo knocks back away from the cat (heavy kinds less), but does not stun; a heavy
        // weapon knocks back on every hit. An elite that ignores Push stands its ground (it still takes the damage).
        if (landed && (combo || knock > 0) && slime.Elite is not { PushImmune: true })
            slime.KnockVelocity = (slime.X < catX ? -1 : 1) * (combo ? Math.Max(Settings.ComboKnock, knock) : knock) * slime.Type.PushFactor;
        // Afterimage: every Every-th landed swing hits a second time (an echo counts nothing itself).
        landedSwings++;
        if (Active("echo") is SynergySpec echo && landedSwings % echo.Every == 0 && slime.Targetable)
        {
            Echoes++;
            AddEffect(BattleEffectKind.Spark, slime.X + (slime.X < catX ? -8 : 8), slime.Height * 0.7);
            events.Add(new(BattleEventKind.Echoed, slime.X));
            Damage(slime, plain * echo.Amount, DamageSource.Echo);
        }
    }

    // Every hit on an enemy goes through here: Landed, or why it did nothing.
    private HitResult Damage(Slime slime, double amount, DamageSource source)
    {
        if (!slime.Targetable || amount <= 0) return HitResult.Landed;
        if (slime.Shielded)
        {
            // A boss in its shield phase: nothing gets through (not even the bomb).
            ImmuneHits++;
            AddEffect(BattleEffectKind.Immune, slime.X, slime.Height / 2, slime.Width);
            events.Add(new(BattleEventKind.BossImmune, slime.X));
            return HitResult.Immune;
        }
        if (slime.Mended)
        {
            // Guarded by a heal winding up on it (push the healer to end it).
            WardedHits++;
            AddEffect(BattleEffectKind.Warded, slime.X, slime.Height / 2, slime.Width);
            events.Add(new(BattleEventKind.HitWarded, slime.X));
            return HitResult.Immune;
        }
        if (Blocks(slime, source)) return HitResult.Blocked;
        // A hit never takes a boss past its next shield phase: it stops there, the rest is lost.
        slime.Hp = Math.Max(ShieldFloor(slime), slime.Hp - amount);
        slime.HitFlash = 0.18;
        AddEffect(BattleEffectKind.Number, slime.X + (slime.IsBoss ? slime.Width * 0.38 : 0), slime.Height + 10, Math.Round(amount));
        if (slime.Hp <= 0)
        {
            slime.Set(SlimeState.Defeated);
            slime.KnockVelocity = 0;
            Defeated++;
            if (slime.IsBoss) { BossDefeated = true; DefeatedBoss = slime.Type; }
            if (SelectedId == slime.Id) SelectedId = null;
            // A defeated healer's guard ends with it (after the whole blast for a bomb, which hits
            // everything in it at once).
            if (slime.MendingId != 0) { slime.MendingId = 0; if (!blasting) RefreshGuards(); }
            AddEffect(BattleEffectKind.Poof, slime.X, slime.Height / 2, slime.IsBoss ? 2 : 1);
            events.Add(new(BattleEventKind.SlimeDefeated, slime.X, slime.IsBoss ? 1 : 0));
            AppsEnemyDefeated(slime);
            return HitResult.Landed;
        }
        var type = slime.Type;
        double fraction = slime.Hp / slime.MaxHp;
        if (type.SummonAt > 0 && !slime.Summoned && fraction <= type.SummonAt)
        {
            slime.Summoned = true;
            var helper = Settings.Enemy(type.SummonType);
            for (int i = 0; i < type.SummonCount; i++)
                AddSlime(slime.X + (i % 2 == 0 ? -1 : 1) * (slime.Width / 2 + 30 + 20 * (i / 2)), helper).SummonedBy = slime.Id;
            events.Add(new(BattleEventKind.BossSummoned, slime.X));
        }
        if (type.EnrageAt > 0 && !slime.Enraged && fraction <= type.EnrageAt)
        {
            slime.Enraged = true;
            events.Add(new(BattleEventKind.BossEnraged, slime.X));
        }
        if (type.Phases is { } phases)
            for (int i = 0; i < phases.Length && i < 31; i++)
            {
                var phase = phases[i];
                // The same product as ShieldFloor, so a hit stopped on a shield phase's line starts it.
                if ((slime.PhasesDone & (1 << i)) != 0 || slime.Hp > phase.At * slime.MaxHp) continue;
                slime.PhasesDone |= 1 << i; // Once, even if the call is skipped below.
                if (phase.Shield)
                {
                    StartShield(slime, phase, i + 1);
                    if (phase.Enrage && !slime.Enraged) { slime.Enraged = true; events.Add(new(BattleEventKind.BossEnraged, slime.X)); }
                    break; // Later phases wait for the shield to drop.
                }
                if (CallHelpers(slime, phase.Calls().ToList(), 0, 0) > 0) events.Add(new(BattleEventKind.BossSummoned, slime.X));
                if (phase.Enrage && !slime.Enraged)
                {
                    slime.Enraged = true;
                    events.Add(new(BattleEventKind.BossEnraged, slime.X));
                }
            }
        return HitResult.Landed;
    }

    // A stony enemy's block roll for one hit (rolled once per hit, never per frame).
    private bool Blocks(Slime slime, DamageSource source)
    {
        var type = slime.Type;
        if (type.BlockChance <= 0 || source is DamageSource.Bomb or DamageSource.Reflect or DamageSource.App) return false;
        // A front-only blocker: a hit from behind always lands (and ends its streak).
        if (type.BlockFrontOnly && (lastCatX - slime.X) * slime.Facing <= 0) { slime.BlockStreak = 0; return false; }
        // Armor break: combo hits go straight through.
        if (source == DamageSource.Combo && Active("armorBreak") != null) { slime.BlockStreak = 0; return false; }
        // An elite's sure blocks come first (no roll); then the usual roll and streak limit.
        bool sure = slime.SureBlocks > 0;
        if (!sure && (slime.BlockStreak >= type.BlockMaxStreak || blockRandom.NextDouble() >= type.BlockChance)) { slime.BlockStreak = 0; return false; }
        if (sure) slime.SureBlocks--;
        slime.BlockStreak++;
        BlocksMade++;
        AddEffect(BattleEffectKind.Block, slime.X, slime.Height * 0.6);
        events.Add(new(BattleEventKind.HitBlocked, slime.X));
        return true;
    }

    // The lowest HP a hit may leave: the line of the first shield phase still to come (0: none).
    private static double ShieldFloor(Slime slime)
    {
        double floor = 0;
        if (slime.Type.Phases is { } phases)
            for (int i = 0; i < phases.Length && i < 31; i++)
            {
                double line = phases[i].At * slime.MaxHp;
                if (phases[i].Shield && (slime.PhasesDone & (1 << i)) == 0 && line < slime.Hp) floor = Math.Max(floor, line);
            }
        return floor;
    }

    // Infinity's End: the run stops as a loss at the wave reached, so it gets its result and record.
    public bool Surrender()
    {
        if (!Endless || Over) return false;
        Finish(BattlePhase.Lost);
        return true;
    }

    private void Finish(BattlePhase phase)
    {
        Phase = phase;
        UpgradeOffer = null;
        PhaseRemaining = Settings.OutroSeconds;
        FightSeconds = Time;
        Armed = null;
        SelectedId = null;
        TargetId = null;
        BlockedById = 0;
        bombs.Clear();
        ClearApps();
        incoming = null;
        IncomingRemaining = 0;
        CancelSwing();
        Combo = 0;
        CatStunRemaining = StunImmunity = 0;
        foreach (var s in slimes)
        {
            if (s.State is SlimeState.Windup or SlimeState.Casting or SlimeState.Dashing) s.Set(SlimeState.Moving);
            s.RallyCharge = 0;
            s.FarHeal = false;
            // A leap in the air comes down where it is; announced helpers never come.
            s.Crouch = s.LeapHeight = 0;
            s.CallSpots = null;
            s.Mended = false;
            s.MendingId = 0;
            s.RunAwayLeft = 0;
        }
        events.Add(new(phase == BattlePhase.Won ? BattleEventKind.Won : BattleEventKind.Lost, 0));
    }

    // Click on a slime: the cat goes for it first. Never deals damage by itself.
    public bool Select(int id)
    {
        if (Over || Paused || Find(id) is not { Targetable: true }) return false;
        SelectedId = id;
        return true;
    }

    // Choose an aimed skill (the next slime click uses it), or null to cancel the choice.
    public bool Arm(BattleSkill? skill)
    {
        if (skill is BattleSkill chosen && (!IsAimed(chosen) || !Ready(chosen))) return false;
        Armed = skill;
        return true;
    }

    public bool ArmPush(bool armed) => armed ? Arm(BattleSkill.Push) : Arm(null) || true;

    // An instant skill (Heal, Cheer, Shield).
    public bool Use(BattleSkill skill, double catX)
    {
        if (IsAimed(skill) || !Ready(skill)) return false;
        switch (skill)
        {
            case BattleSkill.Heal:
                double healed = Math.Min(HealAmount, CatMaxHp - CatHp);
                if (healed <= 0) return false; // Full HP: keep the fish for later.
                CatHp += healed;
                AddEffect(BattleEffectKind.Number, catX, 115, -Math.Round(healed));
                events.Add(new(BattleEventKind.Healed, catX, healed));
                break;
            case BattleSkill.Cheer:
                CheerRemaining = Settings.CheerSeconds;
                events.Add(new(BattleEventKind.Cheered, catX));
                break;
            default:
                ShieldRemaining = Settings.ShieldSeconds;
                events.Add(new(BattleEventKind.Shielded, catX));
                break;
        }
        cooldowns[(int)skill] = SkillCooldown(skill);
        return true;
    }

    // An aimed skill on a slime. Neither changes the cat's target.
    // direction (Push only): +1 to the right, -1 to the left; null pushes it away from the cat.
    public bool Use(BattleSkill skill, int id, double catX, int? direction = null)
    {
        var slime = Find(id);
        if (!IsAimed(skill) || !Ready(skill) || slime is not { Targetable: true }) return false;
        if (skill == BattleSkill.Push && slime.Elite is { PushImmune: true })
        {
            // It stands firm: no knock back, no stun, its windup goes on; the skill is still spent.
            AddEffect(BattleEffectKind.Block, slime.X, slime.Height / 2);
            events.Add(new(BattleEventKind.PushResisted, slime.X, slime.Id));
        }
        else if (skill == BattleSkill.Push)
        {
            // Knock the slime away from the cat and stun it briefly; the boss is heavy.
            int push = direction is int chosen && chosen != 0 ? Math.Sign(chosen) : slime.X < catX ? -1 : 1;
            // A heal being wound up is cancelled and costs its full cooldown (a far heal's too).
            if (slime.State == SlimeState.Casting)
            {
                slime.HealCooldown = HealCooldown(slime); slime.HealTargetId = 0;
                if (slime.FarHeal) slime.FarHealCooldown = slime.Type.FarHealCooldown;
                slime.FarHeal = false;
                slime.MendingId = 0; // Its guard ends with it (RefreshGuards below).
            }
            slime.KnockVelocity = push * Settings.PushSpeed * slime.Type.PushFactor;
            slime.StunRemaining = Settings.PushStunSeconds * slime.Type.StunFactor;
            slime.Set(SlimeState.Stunned);
            RefreshGuards();
            AddEffect(BattleEffectKind.Push, slime.X, slime.Height / 2);
            events.Add(new(BattleEventKind.Pushed, slime.X));
        }
        else
        {
            // A ball of yarn lands on the slime's spot and bursts after a short delay.
            bombs.Add(new BattleBomb { X = slime.X, Remaining = Settings.BombDelay });
            events.Add(new(BattleEventKind.BombThrown, slime.X));
        }
        cooldowns[(int)skill] = SkillCooldown(skill);
        if (Armed == skill) Armed = null;
        return true;
    }

    // Push direction from where the slime is clicked (DIPs from its centre): the left half sends it
    // right, the right half left (poked from that side); the very centre leaves the default.
    public static int? PushDirection(double offset) => Math.Abs(offset) < 3 ? null : offset < 0 ? 1 : -1;

    public bool UsePush(int id, double catX, int? direction = null) => Use(BattleSkill.Push, id, catX, direction);

    // The work area changed (taskbar, resolution) or the cat now fights on another monitor:
    // keep every slime at the same relative spot inside the new floor.
    public void SetArea(double left, double right)
    {
        right = Math.Max(left + Settings.SlimeWidth * Settings.LargestScale, right);
        double oldWidth = Math.Max(1, Right - Left), newWidth = right - left;
        foreach (var s in slimes)
        {
            s.X = left + (s.X - Left) / oldWidth * newWidth;
            s.LeapFromX = left + (s.LeapFromX - Left) / oldWidth * newWidth;
            if (s.CallSpots is { } spots) for (int i = 0; i < spots.Count; i++) spots[i] = (spots[i].Kind, left + (spots[i].X - Left) / oldWidth * newWidth);
        }
        IncomingX = left + (IncomingX - Left) / oldWidth * newWidth;
        foreach (var e in effects) e.X = left + (e.X - Left) / oldWidth * newWidth;
        foreach (var b in bombs) b.X = left + (b.X - Left) / oldWidth * newWidth;
        foreach (var p in popups) p.X = left + (p.X - Left) / oldWidth * newWidth;
        foreach (var l in launches) { l.FloorX = left + (l.FloorX - Left) / oldWidth * newWidth; for (int i = 0; i < l.TargetX.Length; i++) l.TargetX[i] = left + (l.TargetX[i] - Left) / oldWidth * newWidth; }
        Left = left; Right = right;
        foreach (var s in slimes) { var (minX, maxX) = Bounds(s); s.X = Math.Clamp(s.X, minX, maxX); }
    }

    internal void AddEffect(BattleEffectKind kind, double x, double height = 0, double value = 0)
    {
        if (effects.Count >= Settings.MaxEffects) effects.RemoveAt(0);
        double life = kind switch
        {
            BattleEffectKind.Poof => 0.6 * Math.Max(1, value), BattleEffectKind.Number => 0.9, BattleEffectKind.Combo => 0.7, BattleEffectKind.Mend => 0.8,
            BattleEffectKind.Explosion or BattleEffectKind.Slam => 0.5, BattleEffectKind.Warded => 0.6,
            BattleEffectKind.Bell => 0.9, BattleEffectKind.EndTask => 1.0, BattleEffectKind.Rewind => 0.8, _ => 0.35
        };
        effects.Add(new BattleEffect { Kind = kind, X = x, Height = height, Life = life, Value = value });
    }

    private void StepEffects(double dt)
    {
        foreach (var e in effects) e.Age += dt;
        for (int i = effects.Count - 1; i >= 0; i--) if (effects[i].Age >= effects[i].Life) effects.RemoveAt(i);
    }
}
