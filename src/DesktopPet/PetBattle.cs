using System;
using System.Collections.Generic;

namespace DesktopPet;

// Battle Mode tuning: every number the MVP fights with (DIPs and seconds). Change the
// defaults here to try a different balance.
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
    // Slimes per wave; the battle is won after the last one.
    public int[] Waves { get; init; } = { 2, 3, 4 };
    public int MaxAlive { get; init; } = 3;
    public double SpawnInterval { get; init; } = 1.6;
    public double FirstWaveDelay { get; init; } = 1.5;
    public double WaveBreakSeconds { get; init; } = 3.5;
    // HP the cat gets back after each wave except the last.
    public double WaveHeal { get; init; } = 15;
    // New slimes appear at least this far from the cat (centre to centre) when room allows.
    public double SpawnMinDistance { get; init; } = 200;
    public double PushCooldown { get; init; } = 5;
    public double PushSpeed { get; init; } = 520;
    public double PushStunSeconds { get; init; } = 1.4;
    // After the user puts the cat down: no damage and no new slime attacks on it.
    public double LandingGraceSeconds { get; init; } = 1.5;
    public double OutroSeconds { get; init; } = 3;
    public double HurtFlashSeconds { get; init; } = 0.35;
    public int MaxEffects { get; init; } = 24;
}

public enum SlimeState { Spawning, Moving, Windup, Recover, Stunned, Defeated }

public sealed class Slime
{
    public int Id { get; init; }
    // Centre of the slime on the battle floor (DIPs, screen coordinates).
    public double X;
    public double Hp, MaxHp;
    public SlimeState State { get; private set; }
    public double StateTime;
    public double Cooldown, StunRemaining, KnockVelocity, HitFlash;
    public int Facing = -1;
    public bool Targetable => State is not (SlimeState.Spawning or SlimeState.Defeated);
    public void Set(SlimeState state) { State = state; StateTime = 0; }
}

public enum BattlePhase { WaveBreak, Fighting, Won, Lost }
public enum BattleEventKind { WaveStarted, SlimeSpawned, Swing, SlimeHit, SlimeDefeated, CatHurt, Pushed, WaveCleared, Won, Lost }
public readonly record struct BattleEvent(BattleEventKind Kind, double X);
public enum BattleEffectKind { Spark, Poof, Push }
public sealed class BattleEffect
{
    public BattleEffectKind Kind;
    public double X, Age, Life;
}

// What the battle wants the cat to do this frame. Hold: the user has the cat (or it is in
// the air), so the battle leaves it to input and physics.
public enum CatCommand { Hold, Stand, Move }
public readonly record struct CatOrder(CatCommand Command, double X = 0, int Facing = 0);

// The whole fight, without WPF: slimes, waves, targeting, the sword and the push skill.
// Time only advances through Step (simulation time), so Pause freezes everything.
// The window turns CatOrder into the pet's normal walking; physics stays in PetPhysics.
public sealed class BattleSim
{
    public BattleSettings Settings { get; }
    private readonly Random random;
    private readonly List<Slime> slimes = new();
    private readonly List<BattleEffect> effects = new();
    private readonly List<BattleEvent> events = new();
    public IReadOnlyList<Slime> Slimes => slimes;
    public IReadOnlyList<BattleEffect> Effects => effects;
    public IReadOnlyList<BattleEvent> Events => events;
    public BattlePhase Phase { get; private set; } = BattlePhase.WaveBreak;
    public bool Paused { get; set; }
    public double Left { get; private set; }
    public double Right { get; private set; }
    public double CatHp { get; private set; }
    // 1-based number of the current (or last started) wave; 0 before the first.
    public int Wave { get; private set; }
    public int WaveCount => Settings.Waves.Length;
    public int ToSpawn { get; private set; }
    public int Defeated { get; private set; }
    public double PhaseRemaining { get; private set; }
    public double CatCooldown { get; private set; }
    // Seconds into the current swing; negative when not swinging.
    public double SwingTime { get; private set; } = -1;
    public double SwingProgress => SwingTime < 0 ? -1 : Math.Clamp(SwingTime / Settings.CatSwingDuration, 0, 1);
    public int? SelectedId { get; private set; }
    // The slime the cat is going for (selected or chosen automatically).
    public int? TargetId { get; private set; }
    public bool PushArmed { get; private set; }
    public double PushCooldown { get; private set; }
    public double Grace { get; private set; }
    public double HurtFlash { get; private set; }
    // Simulation clock (stops while paused); animations read it instead of wall time.
    public double Time { get; private set; }
    // HP given back after the last cleared wave, and the battle's length (for the result panel).
    public double LastHeal { get; private set; }
    public double FightSeconds { get; private set; }
    public bool Finished => Phase is BattlePhase.Won or BattlePhase.Lost && PhaseRemaining <= 0;
    public bool Over => Phase is BattlePhase.Won or BattlePhase.Lost;
    private int nextId = 1, autoTargetId, swingTargetId, swingFacing = 1;
    private bool swingHit, lifted;
    private double spawnTimer;

    public BattleSim(BattleSettings settings, double left, double right, Random? random = null)
    {
        Settings = settings;
        this.random = random ?? new Random();
        Left = left; Right = Math.Max(left + settings.SlimeWidth, right);
        CatHp = settings.CatMaxHp;
        PhaseRemaining = settings.FirstWaveDelay;
    }

    public int Alive { get { int n = 0; foreach (var s in slimes) if (s.State != SlimeState.Defeated) n++; return n; } }
    public Slime? Find(int id) { foreach (var s in slimes) if (s.Id == id) return s; return null; }
    public void ClearEvents() => events.Clear();

    // catHeld: the battle may not move or hurt the cat (pressed, dragged, flying, climbing).
    // lifted: it is really off the ground (not a plain click); putting it down grants grace.
    public CatOrder Step(double dt, double catX, bool catHeld, bool lifted = false)
    {
        if (Paused || !double.IsFinite(dt) || dt <= 0 || !double.IsFinite(catX)) return new(catHeld ? CatCommand.Hold : CatCommand.Stand);
        dt = Math.Min(dt, 0.1);
        Time += dt;
        StepEffects(dt);
        HurtFlash = Math.Max(0, HurtFlash - dt);
        PushCooldown = Math.Max(0, PushCooldown - dt);
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
        if (Math.Abs(dx) <= Settings.CatAttackRange)
        {
            if (CatCooldown <= 0)
            {
                SwingTime = 0; swingHit = false; swingTargetId = target.Id; swingFacing = facing;
                CatCooldown = Math.Max(Settings.CatAttackCooldown, Settings.CatSwingDuration);
                events.Add(new(BattleEventKind.Swing, catX));
            }
            return new(CatCommand.Stand, catX, facing);
        }
        return new(CatCommand.Move, target.X - facing * Settings.CatAttackRange * 0.7, facing);
    }

    // The swing's single hit: only its own target, only if still in reach at this moment.
    private void ResolveHit(double catX)
    {
        var slime = Find(swingTargetId);
        if (slime == null || !slime.Targetable || Math.Abs(slime.X - catX) > Settings.CatAttackRange + 14) return;
        slime.Hp = Math.Max(0, slime.Hp - Settings.CatDamage);
        slime.HitFlash = 0.18;
        AddEffect(BattleEffectKind.Spark, slime.X);
        events.Add(new(BattleEventKind.SlimeHit, slime.X));
        if (slime.Hp > 0) return;
        slime.Set(SlimeState.Defeated);
        slime.KnockVelocity = 0;
        Defeated++;
        if (SelectedId == slime.Id) SelectedId = null;
        AddEffect(BattleEffectKind.Poof, slime.X);
        events.Add(new(BattleEventKind.SlimeDefeated, slime.X));
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

    private void StepSlimes(double dt, double catX, bool catHeld)
    {
        var s = Settings;
        double minX = Left + s.SlimeWidth / 2, maxX = Math.Max(minX, Right - s.SlimeWidth / 2);
        foreach (var slime in slimes)
        {
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
                    if (slime.StateTime >= s.SpawnDuration) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Stunned:
                    slime.StunRemaining -= dt;
                    if (slime.StunRemaining <= 0) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Recover:
                    if (slime.StateTime >= s.SlimeRecover) slime.Set(SlimeState.Moving);
                    break;
                case SlimeState.Windup:
                    // The cat was picked up: the attack aimed at where it stood is called off.
                    if (catHeld) { slime.Set(SlimeState.Moving); slime.Cooldown = Math.Max(slime.Cooldown, 0.5); break; }
                    if (slime.StateTime >= s.SlimeWindup) Strike(slime, catX);
                    break;
                case SlimeState.Moving:
                    if (catHeld) break; // Wait: nothing to chase while the cat is in the user's hand.
                    if (Math.Abs(dx) > 1) slime.Facing = dx < 0 ? -1 : 1;
                    if (Math.Abs(dx) <= s.SlimeAttackRange && slime.Cooldown <= 0 && Grace <= 0) slime.Set(SlimeState.Windup);
                    else if (Math.Abs(dx) > s.SlimeAttackRange * 0.75)
                        slime.X = Math.Clamp(slime.X + Math.Sign(dx) * Math.Min(Math.Abs(dx), s.SlimeSpeed * dt), minX, maxX);
                    break;
            }
            if (Over) break;
        }
        slimes.RemoveAll(x => x.State == SlimeState.Defeated && x.StateTime >= s.DefeatDuration);
        Separate(minX, maxX);
    }

    // Slimes do not stack on one spot.
    private void Separate(double minX, double maxX)
    {
        double gap = Settings.SlimeWidth * 0.7;
        var live = slimes.FindAll(x => x.State != SlimeState.Defeated);
        live.Sort((a, b) => a.X.CompareTo(b.X));
        for (int i = 1; i < live.Count; i++)
        {
            double overlap = gap - (live[i].X - live[i - 1].X);
            if (overlap <= 0) continue;
            live[i - 1].X = Math.Clamp(live[i - 1].X - overlap / 2, minX, maxX);
            live[i].X = Math.Clamp(live[i].X + overlap / 2, minX, maxX);
        }
    }

    private void Strike(Slime slime, double catX)
    {
        slime.Set(SlimeState.Recover);
        slime.Cooldown = Settings.SlimeAttackCooldown;
        if (Math.Abs(catX - slime.X) > Settings.SlimeAttackRange + 12 || Grace > 0) return; // Missed: the cat moved away.
        CatHp = Math.Max(0, CatHp - Settings.SlimeDamage);
        HurtFlash = Settings.HurtFlashSeconds;
        events.Add(new(BattleEventKind.CatHurt, catX));
        if (CatHp <= 0) Finish(BattlePhase.Lost);
    }

    private void StepWaves(double dt, double catX)
    {
        if (Phase == BattlePhase.WaveBreak)
        {
            PhaseRemaining = Math.Max(0, PhaseRemaining - dt);
            if (PhaseRemaining > 0) return;
            Wave++;
            ToSpawn = Settings.Waves[Wave - 1];
            Phase = BattlePhase.Fighting;
            spawnTimer = 0;
            events.Add(new(BattleEventKind.WaveStarted, catX));
        }
        spawnTimer = Math.Max(0, spawnTimer - dt);
        if (ToSpawn > 0 && Alive < Settings.MaxAlive && spawnTimer <= 0)
        {
            Spawn(catX);
            ToSpawn--;
            spawnTimer = Settings.SpawnInterval;
        }
        if (ToSpawn > 0 || Alive > 0) return;
        if (Wave >= WaveCount) { Finish(BattlePhase.Won); return; }
        Phase = BattlePhase.WaveBreak;
        PhaseRemaining = Settings.WaveBreakSeconds;
        LastHeal = Math.Max(0, Math.Min(Settings.WaveHeal, Settings.CatMaxHp - CatHp));
        CatHp += LastHeal;
        events.Add(new(BattleEventKind.WaveCleared, catX));
    }

    private void Spawn(double catX)
    {
        double w = Settings.SlimeWidth;
        double minX = Left + w / 2, maxX = Math.Max(minX, Right - w / 2);
        double left = Math.Min(maxX, minX + random.NextDouble() * 60), right = Math.Max(minX, maxX - random.NextDouble() * 60);
        double first = random.Next(2) == 0 ? left : right, second = first == left ? right : left;
        double x = Math.Abs(first - catX) >= Settings.SpawnMinDistance ? first
            : Math.Abs(second - catX) >= Settings.SpawnMinDistance ? second
            : Math.Abs(first - catX) >= Math.Abs(second - catX) ? first : second;
        var slime = new Slime { Id = nextId++, X = x, Hp = Settings.SlimeMaxHp, MaxHp = Settings.SlimeMaxHp, Facing = catX < x ? -1 : 1 };
        slime.Set(SlimeState.Spawning);
        slimes.Add(slime);
        events.Add(new(BattleEventKind.SlimeSpawned, x));
    }

    private void Finish(BattlePhase phase)
    {
        Phase = phase;
        PhaseRemaining = Settings.OutroSeconds;
        FightSeconds = Time;
        PushArmed = false;
        SelectedId = null;
        TargetId = null;
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

    public bool ArmPush(bool armed)
    {
        if (armed && (Over || PushCooldown > 0)) return false;
        PushArmed = armed;
        return true;
    }

    // The support skill: knock a slime away from the cat and stun it briefly. Does not
    // change the cat's target.
    public bool UsePush(int id, double catX)
    {
        var slime = Find(id);
        if (Over || PushCooldown > 0 || slime is not { Targetable: true }) return false;
        int direction = slime.X < catX ? -1 : 1;
        slime.KnockVelocity = direction * Settings.PushSpeed;
        slime.StunRemaining = Settings.PushStunSeconds;
        slime.Set(SlimeState.Stunned);
        PushCooldown = Settings.PushCooldown;
        PushArmed = false;
        AddEffect(BattleEffectKind.Push, slime.X);
        events.Add(new(BattleEventKind.Pushed, slime.X));
        return true;
    }

    // The work area changed (taskbar, resolution) or the cat now fights on another monitor:
    // keep every slime at the same relative spot inside the new floor.
    public void SetArea(double left, double right)
    {
        right = Math.Max(left + Settings.SlimeWidth, right);
        double oldWidth = Math.Max(1, Right - Left), newWidth = right - left;
        foreach (var s in slimes) s.X = left + (s.X - Left) / oldWidth * newWidth;
        foreach (var e in effects) e.X = left + (e.X - Left) / oldWidth * newWidth;
        Left = left; Right = right;
        double minX = Left + Settings.SlimeWidth / 2, maxX = Math.Max(minX, Right - Settings.SlimeWidth / 2);
        foreach (var s in slimes) s.X = Math.Clamp(s.X, minX, maxX);
    }

    private void AddEffect(BattleEffectKind kind, double x)
    {
        if (effects.Count >= Settings.MaxEffects) effects.RemoveAt(0);
        effects.Add(new BattleEffect { Kind = kind, X = x, Life = kind == BattleEffectKind.Poof ? 0.6 : 0.35 });
    }

    private void StepEffects(double dt)
    {
        foreach (var e in effects) e.Age += dt;
        effects.RemoveAll(e => e.Age >= e.Life);
    }
}
