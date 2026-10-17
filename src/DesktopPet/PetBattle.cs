using System;
using System.Collections.Generic;

namespace DesktopPet;

// Battle Mode tuning: every number the fight uses (DIPs and seconds). Change the defaults
// here to try a different balance.
public sealed class BattleSettings
{
    public double CatMaxHp { get; init; } = 100;
    public double CatDamage { get; init; } = 25;
    // Centre-to-centre distance at which the sword reaches a slime.
    public double CatAttackRange { get; init; } = 76;
    // From the start of one swing to the next (at least the swing itself).
    public double CatAttackCooldown { get; init; } = 0.75;
    public double CatSwingDuration { get; init; } = 0.36;
    // The one moment in a swing that can deal damage.
    public double CatHitTime { get; init; } = 0.16;
    public double SlimeMaxHp { get; init; } = 50;
    public double SlimeSpeed { get; init; } = 34;
    public double SlimeDamage { get; init; } = 10;
    public double SlimeAttackRange { get; init; } = 64;
    // The slime swells and shows "!" this long before its hit: time for the user to help.
    public double SlimeWindup { get; init; } = 0.8;
    public double SlimeAttackCooldown { get; init; } = 1.8;
    public double SlimeRecover { get; init; } = 0.35;
    public double SlimeWidth { get; init; } = 46;
    public double SlimeHeight { get; init; } = 34;
    public double SpawnDuration { get; init; } = 0.5;
    public double DefeatDuration { get; init; } = 0.6;
    // Slimes per wave. With Boss on, one more wave follows with the Slime King alone.
    public int[] Waves { get; init; } = { 2, 3, 4 };
    public bool Boss { get; init; } = true;
    public int MaxAlive { get; init; } = 3;
    public double SpawnInterval { get; init; } = 1.6;
    public double FirstWaveDelay { get; init; } = 1.5;
    public double WaveBreakSeconds { get; init; } = 3.5;
    // HP the cat gets back after each wave except the last.
    public double WaveHeal { get; init; } = 15;
    // New slimes appear at least this far from the cat (centre to centre) when room allows.
    public double SpawnMinDistance { get; init; } = 200;
    // After the user puts the cat down: no damage and no new slime attacks on it.
    public double LandingGraceSeconds { get; init; } = 1.5;
    public double OutroSeconds { get; init; } = 3;
    public double HurtFlashSeconds { get; init; } = 0.35;
    public int MaxEffects { get; init; } = 32;

    // The user's skills.
    public double PushCooldown { get; init; } = 5;
    public double PushSpeed { get; init; } = 520;
    public double PushStunSeconds { get; init; } = 1.4;
    public double HealCooldown { get; init; } = 20;
    public double HealAmount { get; init; } = 25;
    public double CheerCooldown { get; init; } = 18;
    public double CheerSeconds { get; init; } = 5;
    public double CheerSpeed { get; init; } = 1.6;
    public double CheerDamage { get; init; } = 1.4;
    public double BombCooldown { get; init; } = 12;
    public double BombDelay { get; init; } = 0.5;
    public double BombDamage { get; init; } = 30;
    public double BombRadius { get; init; } = 70;
    public double ShieldCooldown { get; init; } = 15;
    public double ShieldSeconds { get; init; } = 3;

    // The Slime King (last wave).
    public double BossMaxHp { get; init; } = 400;
    public double BossScale { get; init; } = 2;
    public double BossSpeed { get; init; } = 22;
    public double BossDamage { get; init; } = 25;
    // The ground slam reaches this far from the boss's centre.
    public double BossRange { get; init; } = 110;
    public double BossWindup { get; init; } = 1.2;
    public double BossAttackCooldown { get; init; } = 2.6;
    // Below these HP fractions: calls two helpers (once), then gets angry (faster, shorter windup).
    public double BossSummonAt { get; init; } = 0.5;
    public int BossSummonCount { get; init; } = 2;
    public double BossEnrageAt { get; init; } = 0.3;
    public double BossEnragedSpeed { get; init; } = 32;
    public double BossEnragedWindup { get; init; } = 0.8;
    // Heavy: pushes move it and stun it less.
    public double BossPushFactor { get; init; } = 0.35;
    public double BossStunFactor { get; init; } = 0.4;

    public double SkillCooldown(BattleSkill skill) => skill switch
    {
        BattleSkill.Push => PushCooldown, BattleSkill.Heal => HealCooldown, BattleSkill.Cheer => CheerCooldown,
        BattleSkill.Bomb => BombCooldown, _ => ShieldCooldown
    };
}

// Push and Bomb are aimed (choose, then click a slime); the others act at once.
public enum BattleSkill { Push, Heal, Cheer, Bomb, Shield }

public enum SlimeState { Spawning, Moving, Windup, Recover, Stunned, Defeated }

public sealed class Slime
{
    public int Id { get; init; }
    public bool IsBoss { get; init; }
    // Centre of the slime on the battle floor (DIPs, screen coordinates).
    public double X;
    public double Hp, MaxHp;
    public double Width { get; init; }
    public double Height { get; init; }
    public double Damage { get; init; }
    public double Range { get; init; }
    public SlimeState State { get; private set; }
    public double StateTime;
    public double Cooldown, StunRemaining, KnockVelocity, HitFlash;
    public int Facing = -1;
    public bool Enraged, Summoned;
    public bool Targetable => State is not (SlimeState.Spawning or SlimeState.Defeated);
    public void Set(SlimeState state) { State = state; StateTime = 0; }
}

public enum BattlePhase { WaveBreak, Fighting, Won, Lost }
public enum BattleEventKind
{
    WaveStarted, SlimeSpawned, Swing, SlimeHit, SlimeDefeated, CatHurt, Pushed, WaveCleared, Won, Lost,
    Healed, Cheered, Shielded, Blocked, BombThrown, BombExploded, BossAppeared, BossSummoned, BossEnraged
}
public readonly record struct BattleEvent(BattleEventKind Kind, double X, double Value = 0);
// Number: a floating damage (Value > 0) or heal (Value < 0, shown as +HP above the cat).
public enum BattleEffectKind { Spark, Poof, Push, Explosion, Slam, Number }
public sealed class BattleEffect
{
    public BattleEffectKind Kind;
    // Height is how far above the floor the effect starts.
    public double X, Height, Age, Life, Value;
}
public sealed class BattleBomb
{
    public double X, Remaining;
}

// What the battle wants the cat to do this frame. Hold: the user has the cat (or it is in
// the air), so the battle leaves it to input and physics.
public enum CatCommand { Hold, Stand, Move }
public readonly record struct CatOrder(CatCommand Command, double X = 0, int Facing = 0);

// The whole fight, without WPF: slimes, the boss, waves, targeting, the sword and the
// user's skills. Time only advances through Step (simulation time), so Pause freezes
// everything. The window turns CatOrder into the pet's normal walking; physics stays in PetPhysics.
public sealed class BattleSim
{
    public BattleSettings Settings { get; }
    private readonly Random random;
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
    public int WaveCount => Settings.Waves.Length + (Settings.Boss ? 1 : 0);
    public bool IsBossWave(int wave) => Settings.Boss && wave == WaveCount;
    public int ToSpawn { get; private set; }
    public int Defeated { get; private set; }
    public bool BossDefeated { get; private set; }
    public double PhaseRemaining { get; private set; }
    public double CatCooldown { get; private set; }
    // Seconds into the current swing; negative when not swinging.
    public double SwingTime { get; private set; } = -1;
    public double SwingProgress => SwingTime < 0 ? -1 : Math.Clamp(SwingTime / Settings.CatSwingDuration, 0, 1);
    public int? SelectedId { get; private set; }
    // The slime the cat is going for (selected or chosen automatically).
    public int? TargetId { get; private set; }
    public BattleSkill? Armed { get; private set; }
    public bool PushArmed => Armed == BattleSkill.Push;
    public double PushCooldown => cooldowns[(int)BattleSkill.Push];
    public double CheerRemaining { get; private set; }
    public double ShieldRemaining { get; private set; }
    public double Grace { get; private set; }
    public double HurtFlash { get; private set; }
    // Simulation clock (stops while paused); animations read it instead of wall time.
    public double Time { get; private set; }
    // HP given back after the last cleared wave, and the battle's length (for the result panel).
    public double LastHeal { get; private set; }
    public double FightSeconds { get; private set; }
    public bool Finished => Phase is BattlePhase.Won or BattlePhase.Lost && PhaseRemaining <= 0;
    public bool Over => Phase is BattlePhase.Won or BattlePhase.Lost;
    public Slime? Boss { get { foreach (var s in slimes) if (s.IsBoss && s.State != SlimeState.Defeated) return s; return null; } }
    private int nextId = 1, autoTargetId, swingTargetId, swingFacing = 1;
    private bool swingHit, lifted;
    private double spawnTimer;

    public BattleSim(BattleSettings settings, double left, double right, Random? random = null)
    {
        Settings = settings;
        this.random = random ?? new Random();
        Left = left; Right = Math.Max(left + settings.SlimeWidth * settings.BossScale, right);
        CatHp = settings.CatMaxHp;
        PhaseRemaining = settings.FirstWaveDelay;
    }

    public int Alive { get { int n = 0; foreach (var s in slimes) if (s.State != SlimeState.Defeated) n++; return n; } }
    public Slime? Find(int id) { foreach (var s in slimes) if (s.Id == id) return s; return null; }
    public void ClearEvents() => events.Clear();
    public double Cooldown(BattleSkill skill) => cooldowns[(int)skill];
    public static bool IsAimed(BattleSkill skill) => skill is BattleSkill.Push or BattleSkill.Bomb;
    public bool Ready(BattleSkill skill) => !Over && cooldowns[(int)skill] <= 0;

    // catHeld: the battle may not move or hurt the cat (pressed, dragged, flying, climbing).
    // lifted: it is really off the ground (not a plain click); putting it down grants grace.
    public CatOrder Step(double dt, double catX, bool catHeld, bool lifted = false)
    {
        if (Paused || !double.IsFinite(dt) || dt <= 0 || !double.IsFinite(catX)) return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        dt = Math.Min(dt, 0.1);
        Time += dt;
        StepEffects(dt);
        HurtFlash = Math.Max(0, HurtFlash - dt);
        for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = Math.Max(0, cooldowns[i] - dt);
        CheerRemaining = Math.Max(0, CheerRemaining - dt);
        ShieldRemaining = Math.Max(0, ShieldRemaining - dt);
        if (Over)
        {
            PhaseRemaining = Math.Max(0, PhaseRemaining - dt);
            StepSlimes(dt, catX, true);
            return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        }
        if (catHeld) { lifted |= this.lifted; this.lifted = lifted; CancelSwing(); }
        else
        {
            if (this.lifted) Grace = Settings.LandingGraceSeconds;
            this.lifted = false;
            Grace = Math.Max(0, Grace - dt);
        }
        CatCooldown = Math.Max(0, CatCooldown - dt);
        StepBombs(dt);
        StepSlimes(dt, catX, catHeld);
        if (Over) return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        // Holding the cat freezes the waves: no spawning, no progress.
        if (!catHeld) StepWaves(dt, catX);
        if (catHeld) { TargetId = PickTarget(catX)?.Id; return new(CatCommand.Hold); }
        return CatTurn(dt, catX);
    }

    private void CancelSwing() { SwingTime = -1; swingHit = false; }

    private CatOrder CatTurn(double dt, double catX)
    {
        var target = Over ? null : PickTarget(catX);
        TargetId = target?.Id;
        if (SwingTime >= 0)
        {
            SwingTime += dt;
            if (!swingHit && SwingTime >= Settings.CatHitTime) { swingHit = true; ResolveHit(catX); }
            if (SwingTime >= Settings.CatSwingDuration) CancelSwing();
            return new(CatCommand.Stand, catX, swingFacing);
        }
        if (target == null) return new(CatCommand.Stand, catX);
        double dx = target.X - catX;
        int facing = dx < 0 ? -1 : 1;
        double reach = Reach(target);
        if (Math.Abs(dx) <= reach)
        {
            if (CatCooldown <= 0)
            {
                SwingTime = 0; swingHit = false; swingTargetId = target.Id; swingFacing = facing;
                double cooldown = Settings.CatAttackCooldown / (CheerRemaining > 0 ? Settings.CheerSpeed : 1);
                CatCooldown = Math.Max(cooldown, Settings.CatSwingDuration);
                events.Add(new(BattleEventKind.Swing, catX));
            }
            return new(CatCommand.Stand, catX, facing);
        }
        return new(CatCommand.Move, target.X - facing * reach * 0.7, facing);
    }

    // A wider slime is reached from further away (the sword hits its near edge).
    private double Reach(Slime slime) => Settings.CatAttackRange + (slime.Width - Settings.SlimeWidth) / 2;

    // The swing's single hit: only its own target, only if still in reach at this moment.
    private void ResolveHit(double catX)
    {
        var slime = Find(swingTargetId);
        if (slime == null || !slime.Targetable || Math.Abs(slime.X - catX) > Reach(slime) + 14) return;
        events.Add(new(BattleEventKind.SlimeHit, slime.X));
        AddEffect(BattleEffectKind.Spark, slime.X, slime.Height / 2);
        Damage(slime, Settings.CatDamage * (CheerRemaining > 0 ? Settings.CheerDamage : 1));
    }

    private void Damage(Slime slime, double amount)
    {
        if (!slime.Targetable || amount <= 0) return;
        slime.Hp = Math.Max(0, slime.Hp - amount);
        slime.HitFlash = 0.18;
        AddEffect(BattleEffectKind.Number, slime.X + (slime.IsBoss ? slime.Width * 0.38 : 0), slime.Height + 10, Math.Round(amount));
        if (slime.Hp <= 0)
        {
            slime.Set(SlimeState.Defeated);
            slime.KnockVelocity = 0;
            Defeated++;
            if (slime.IsBoss) BossDefeated = true;
            if (SelectedId == slime.Id) SelectedId = null;
            AddEffect(BattleEffectKind.Poof, slime.X, slime.Height / 2, slime.IsBoss ? 2 : 1);
            events.Add(new(BattleEventKind.SlimeDefeated, slime.X, slime.IsBoss ? 1 : 0));
            return;
        }
        if (!slime.IsBoss) return;
        double fraction = slime.Hp / slime.MaxHp;
        if (!slime.Summoned && fraction <= Settings.BossSummonAt)
        {
            slime.Summoned = true;
            for (int i = 0; i < Settings.BossSummonCount; i++)
                AddSlime(slime.X + (i % 2 == 0 ? -1 : 1) * (slime.Width / 2 + 30 + 20 * (i / 2)), false);
            events.Add(new(BattleEventKind.BossSummoned, slime.X));
        }
        if (!slime.Enraged && fraction <= Settings.BossEnrageAt)
        {
            slime.Enraged = true;
            events.Add(new(BattleEventKind.BossEnraged, slime.X));
        }
    }

    private Slime? PickTarget(double catX)
    {
        if (SelectedId is int selected)
        {
            var chosen = Find(selected);
            if (chosen != null && chosen.Targetable) return chosen;
            SelectedId = null;
        }
        Slime? nearest = null;
        foreach (var s in slimes)
            if (s.Targetable && (nearest == null || Math.Abs(s.X - catX) < Math.Abs(nearest.X - catX))) nearest = s;
        // Keep the current choice unless another slime is clearly closer (no dithering).
        var current = Find(autoTargetId);
        if (current != null && current.Targetable && nearest != null &&
            Math.Abs(current.X - catX) <= Math.Abs(nearest.X - catX) + 40) nearest = current;
        autoTargetId = nearest?.Id ?? 0;
        return nearest;
    }

    private (double Min, double Max) Bounds(Slime slime)
    {
        double min = Left + slime.Width / 2;
        return (min, Math.Max(min, Right - slime.Width / 2));
    }

    private void StepSlimes(double dt, double catX, bool catHeld)
    {
        var s = Settings;
        foreach (var slime in slimes)
        {
            var (minX, maxX) = Bounds(slime);
            slime.StateTime += dt;
            slime.HitFlash = Math.Max(0, slime.HitFlash - dt);
            slime.Cooldown = Math.Max(0, slime.Cooldown - dt);
            if (slime.KnockVelocity != 0)
            {
                slime.X += slime.KnockVelocity * dt;
                slime.KnockVelocity *= Math.Exp(-6 * dt);
                if (Math.Abs(slime.KnockVelocity) < 5) slime.KnockVelocity = 0;
            }
            slime.X = Math.Clamp(slime.X, minX, maxX);
            if (Over) continue; // The outro: slimes just bounce in place.
            double dx = catX - slime.X;
            switch (slime.State)
            {
                case SlimeState.Spawning:
                    if (slime.StateTime >= s.SpawnDuration * (slime.IsBoss ? 2 : 1)) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Stunned:
                    slime.StunRemaining -= dt;
                    if (slime.StunRemaining <= 0) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Recover:
                    if (slime.StateTime >= s.SlimeRecover * (slime.IsBoss ? 2 : 1)) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Windup:
                    // The cat was picked up: the attack aimed at where it stood is called off.
                    if (catHeld) { slime.Set(SlimeState.Moving); slime.Cooldown = Math.Max(slime.Cooldown, 0.5); break; }
                    if (slime.StateTime >= Windup(slime)) Strike(slime, catX);
                    break;
                case SlimeState.Moving:
                    // While the user carries the cat the slimes follow it along the floor, but never
                    // wind up: a lifted cat cannot be hit.
                    if (Math.Abs(dx) > 1) slime.Facing = dx < 0 ? -1 : 1;
                    if (!catHeld && Math.Abs(dx) <= slime.Range && slime.Cooldown <= 0 && Grace <= 0) slime.Set(SlimeState.Windup);
                    else if (Math.Abs(dx) > slime.Range * 0.75)
                        slime.X = Math.Clamp(slime.X + Math.Sign(dx) * Math.Min(Math.Abs(dx), Speed(slime) * dt), minX, maxX);
                    break;
            }
            if (Over) break;
        }
        // Plain loops here and below: this runs every frame, so no per-call lambdas or lists.
        for (int i = slimes.Count - 1; i >= 0; i--)
            if (slimes[i].State == SlimeState.Defeated && slimes[i].StateTime >= s.DefeatDuration * (slimes[i].IsBoss ? 1.5 : 1)) slimes.RemoveAt(i);
        Separate();
    }

    public double Windup(Slime slime) => !slime.IsBoss ? Settings.SlimeWindup : slime.Enraged ? Settings.BossEnragedWindup : Settings.BossWindup;
    private double Speed(Slime slime) => !slime.IsBoss ? Settings.SlimeSpeed : slime.Enraged ? Settings.BossEnragedSpeed : Settings.BossSpeed;

    private readonly List<Slime> scratch = new();
    private static readonly Comparison<Slime> ByX = (a, b) => a.X.CompareTo(b.X);

    // Slimes do not stack on one spot.
    private void Separate()
    {
        var live = scratch;
        live.Clear();
        foreach (var slime in slimes) if (slime.State != SlimeState.Defeated) live.Add(slime);
        live.Sort(ByX);
        for (int i = 1; i < live.Count; i++)
        {
            double gap = (live[i].Width + live[i - 1].Width) / 2 * 0.7;
            double overlap = gap - (live[i].X - live[i - 1].X);
            if (overlap <= 0) continue;
            var (minA, maxA) = Bounds(live[i - 1]);
            var (minB, maxB) = Bounds(live[i]);
            live[i - 1].X = Math.Clamp(live[i - 1].X - overlap / 2, minA, maxA);
            live[i].X = Math.Clamp(live[i].X + overlap / 2, minB, maxB);
        }
    }

    private void Strike(Slime slime, double catX)
    {
        slime.Set(SlimeState.Recover);
        slime.Cooldown = slime.IsBoss ? Settings.BossAttackCooldown : Settings.SlimeAttackCooldown;
        if (slime.IsBoss) AddEffect(BattleEffectKind.Slam, slime.X, 0, slime.Range);
        if (Math.Abs(catX - slime.X) > slime.Range + 12 || Grace > 0) return; // Missed: the cat moved away.
        if (ShieldRemaining > 0)
        {
            events.Add(new(BattleEventKind.Blocked, catX));
            return;
        }
        CatHp = Math.Max(0, CatHp - slime.Damage);
        HurtFlash = Settings.HurtFlashSeconds;
        events.Add(new(BattleEventKind.CatHurt, catX, slime.Damage));
        if (CatHp <= 0) Finish(BattlePhase.Lost);
    }

    private void StepBombs(double dt)
    {
        for (int i = bombs.Count - 1; i >= 0; i--)
        {
            var bomb = bombs[i];
            bomb.Remaining -= dt;
            if (bomb.Remaining > 0) continue;
            bombs.RemoveAt(i);
            AddEffect(BattleEffectKind.Explosion, bomb.X, 0, Settings.BombRadius);
            events.Add(new(BattleEventKind.BombExploded, bomb.X));
            foreach (var slime in slimes.ToArray())
                if (slime.Targetable && Math.Abs(slime.X - bomb.X) <= Settings.BombRadius + slime.Width / 2)
                    Damage(slime, Settings.BombDamage);
        }
    }

    private void StepWaves(double dt, double catX)
    {
        if (Phase == BattlePhase.WaveBreak)
        {
            PhaseRemaining = Math.Max(0, PhaseRemaining - dt);
            if (PhaseRemaining > 0) return;
            Wave++;
            ToSpawn = IsBossWave(Wave) ? 1 : Settings.Waves[Wave - 1];
            Phase = BattlePhase.Fighting;
            spawnTimer = 0;
            events.Add(new(BattleEventKind.WaveStarted, catX, Wave));
        }
        spawnTimer = Math.Max(0, spawnTimer - dt);
        if (ToSpawn > 0 && Alive < Settings.MaxAlive && spawnTimer <= 0)
        {
            Spawn(catX, IsBossWave(Wave));
            ToSpawn--;
            spawnTimer = Settings.SpawnInterval;
        }
        if (ToSpawn > 0 || Alive > 0 || bombs.Count > 0) return;
        if (Wave >= WaveCount) { Finish(BattlePhase.Won); return; }
        Phase = BattlePhase.WaveBreak;
        PhaseRemaining = Settings.WaveBreakSeconds;
        LastHeal = Math.Max(0, Math.Min(Settings.WaveHeal, Settings.CatMaxHp - CatHp));
        CatHp += LastHeal;
        if (LastHeal > 0) AddEffect(BattleEffectKind.Number, catX, 115, -Math.Round(LastHeal));
        events.Add(new(BattleEventKind.WaveCleared, catX));
    }

    private void Spawn(double catX, bool boss)
    {
        double w = Settings.SlimeWidth * (boss ? Settings.BossScale : 1);
        double minX = Left + w / 2, maxX = Math.Max(minX, Right - w / 2);
        double left = Math.Min(maxX, minX + random.NextDouble() * 60), right = Math.Max(minX, maxX - random.NextDouble() * 60);
        double first = random.Next(2) == 0 ? left : right, second = first == left ? right : left;
        double x = Math.Abs(first - catX) >= Settings.SpawnMinDistance ? first
            : Math.Abs(second - catX) >= Settings.SpawnMinDistance ? second
            : Math.Abs(first - catX) >= Math.Abs(second - catX) ? first : second;
        var slime = AddSlime(x, boss);
        slime.Facing = catX < x ? -1 : 1;
        events.Add(new(boss ? BattleEventKind.BossAppeared : BattleEventKind.SlimeSpawned, x));
    }

    private Slime AddSlime(double x, bool boss)
    {
        var s = Settings;
        double scale = boss ? s.BossScale : 1;
        var slime = new Slime
        {
            Id = nextId++, IsBoss = boss, Width = s.SlimeWidth * scale, Height = s.SlimeHeight * scale,
            Hp = boss ? s.BossMaxHp : s.SlimeMaxHp, MaxHp = boss ? s.BossMaxHp : s.SlimeMaxHp,
            Damage = boss ? s.BossDamage : s.SlimeDamage, Range = boss ? s.BossRange : s.SlimeAttackRange
        };
        var (minX, maxX) = Bounds(slime);
        slime.X = Math.Clamp(x, minX, maxX);
        slime.Set(SlimeState.Spawning);
        slimes.Add(slime);
        return slime;
    }

    private void Finish(BattlePhase phase)
    {
        Phase = phase;
        PhaseRemaining = Settings.OutroSeconds;
        FightSeconds = Time;
        Armed = null;
        SelectedId = null;
        TargetId = null;
        bombs.Clear();
        CancelSwing();
        foreach (var s in slimes) if (s.State == SlimeState.Windup) s.Set(SlimeState.Moving);
        events.Add(new(phase == BattlePhase.Won ? BattleEventKind.Won : BattleEventKind.Lost, 0));
    }

    // Click on a slime: the cat goes for it first. Never deals damage by itself.
    public bool Select(int id)
    {
        if (Over || Find(id) is not { Targetable: true }) return false;
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
                double healed = Math.Min(Settings.HealAmount, Settings.CatMaxHp - CatHp);
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
        cooldowns[(int)skill] = Settings.SkillCooldown(skill);
        return true;
    }

    // An aimed skill on a slime. Neither changes the cat's target.
    // direction (Push only): +1 to the right, -1 to the left; null pushes it away from the cat.
    public bool Use(BattleSkill skill, int id, double catX, int? direction = null)
    {
        var slime = Find(id);
        if (!IsAimed(skill) || !Ready(skill) || slime is not { Targetable: true }) return false;
        if (skill == BattleSkill.Push)
        {
            // Knock the slime away from the cat and stun it briefly; the boss is heavy.
            int push = direction is int chosen && chosen != 0 ? Math.Sign(chosen) : slime.X < catX ? -1 : 1;
            slime.KnockVelocity = push * Settings.PushSpeed * (slime.IsBoss ? Settings.BossPushFactor : 1);
            slime.StunRemaining = Settings.PushStunSeconds * (slime.IsBoss ? Settings.BossStunFactor : 1);
            slime.Set(SlimeState.Stunned);
            AddEffect(BattleEffectKind.Push, slime.X, slime.Height / 2);
            events.Add(new(BattleEventKind.Pushed, slime.X));
        }
        else
        {
            // A ball of yarn lands on the slime's spot and bursts after a short delay.
            bombs.Add(new BattleBomb { X = slime.X, Remaining = Settings.BombDelay });
            events.Add(new(BattleEventKind.BombThrown, slime.X));
        }
        cooldowns[(int)skill] = Settings.SkillCooldown(skill);
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
        right = Math.Max(left + Settings.SlimeWidth * Settings.BossScale, right);
        double oldWidth = Math.Max(1, Right - Left), newWidth = right - left;
        foreach (var s in slimes) s.X = left + (s.X - Left) / oldWidth * newWidth;
        foreach (var e in effects) e.X = left + (e.X - Left) / oldWidth * newWidth;
        foreach (var b in bombs) b.X = left + (b.X - Left) / oldWidth * newWidth;
        Left = left; Right = right;
        foreach (var s in slimes) { var (minX, maxX) = Bounds(s); s.X = Math.Clamp(s.X, minX, maxX); }
    }

    private void AddEffect(BattleEffectKind kind, double x, double height = 0, double value = 0)
    {
        if (effects.Count >= Settings.MaxEffects) effects.RemoveAt(0);
        double life = kind switch
        {
            BattleEffectKind.Poof => 0.6 * Math.Max(1, value), BattleEffectKind.Number => 0.9,
            BattleEffectKind.Explosion or BattleEffectKind.Slam => 0.5, _ => 0.35
        };
        effects.Add(new BattleEffect { Kind = kind, X = x, Height = height, Life = life, Value = value });
    }

    private void StepEffects(double dt)
    {
        foreach (var e in effects) e.Age += dt;
        for (int i = effects.Count - 1; i >= 0; i--) if (effects[i].Age >= effects[i].Life) effects.RemoveAt(i);
    }
}
