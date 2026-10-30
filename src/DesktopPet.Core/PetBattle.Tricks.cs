using System;

namespace DesktopPet;

// 1.5 M13, docs/UPDATE-1.5.md (S1–S4) and docs/BATTLE-DATA.md → Quái dùng chuột: four slimes the cursor
// beats. The window tells the battle where the cursor is over the battle strip every frame (SetPointer:
// only there, only its position); while the cat is in the user's hand it counts as nowhere. Pause and the
// frozen guide stop all of it (time runs only in Step).
// - Ghost (EnemyType.Ghost): seen a while, faded out a while. Faded, nothing hits it (the cat does not go
//   for it, blows, bombs and tools pass through), it attacks nothing and drifts through the cat to its
//   other side. The cursor over it (or the laser's dot near it) brings it out and keeps it out.
// - Bubble: in its bubble it takes no hit at all (it still attacks); clicks on it (the window calls Poke
//   before choosing it as the cat's target) or a Scissors stroke through it pop the bubble: it drops,
//   stunned, and the bubble grows back later.
// - Mirage: it comes with look-alike copies. A copy's hits hurt nothing and any hit on it makes it vanish
//   (never the cat's kill, never a cost or an elite of the wave); the real one defeated, its copies go.
//   The cursor over one (or the laser near it) shows it for what it is a moment: a copy blurs, the real
//   one gets its shadow.
// - Sulk: hits take no HP and puff it up (its own hits grow with it); stroking it with the cursor soothes
//   it, and soothed it waves goodbye and leaves, counted as the cat's.
// None of them touches the cat's mood (emotions stay locked in a battle). No random stream is used, so
// battles without them play exactly as before.
public sealed partial class BattleSim
{
    // The cursor over the battle strip (battle x, height over the floor; null: elsewhere).
    public double? PointerX { get; private set; }
    public double PointerHeight { get; private set; }
    // Where the cursor was at the last Step (NaN: not over the strip), for the strokes on a sulky slime.
    private double strokeX = double.NaN, strokeHeight;
    // A stroke counts at most this far a frame (a jump of the cursor is not a stroke).
    public const double MaxStroke = 80;
    public int GhostsRevealed { get; private set; }
    public int BubblesPopped { get; private set; }
    public int DecoysDispelled { get; private set; }
    public int SulksCalmed { get; private set; }

    public void SetPointer((double X, double Height)? at)
    {
        if (at is (double x, double h) && double.IsFinite(x) && double.IsFinite(h)) { PointerX = x; PointerHeight = h; }
        else PointerX = null;
    }

    // The cursor is on this enemy (its body as drawn, with the field's click margin round it).
    public bool PointerOn(Slime s) => !CatHeld && PointerX is double x && Math.Abs(x - s.X) <= s.Width * s.Puff / 2 + 10
        && PointerHeight >= -4 && PointerHeight <= s.Altitude + BubbleLift(s) + s.Height * s.Puff + 24;

    // How high a bubble floats its enemy (0: no bubble up).
    public static double BubbleLift(Slime s) => s.Bubbled && s.Type.Bubble is BubbleSpec bubble ? bubble.Lift : 0;
    public static int PopsNeeded(Slime s) => s.Type.Bubble is not BubbleSpec bubble ? 0 : s.Elite != null ? bubble.ElitePops : bubble.Pops;
    private static double CalmNeeded(Slime s) => s.Type.Sulk is not SulkSpec sulk ? 1 : s.Elite != null ? sulk.EliteCalm : sulk.Calm;
    private bool LaserNear(Slime s, double range) => range > 0 && LaserShows(s.X, range);

    // Every frame (not paused), after the tools: the laser's dot is where it is now.
    private void StepTricks(double dt)
    {
        bool over = !CatHeld && PointerX != null;
        double stroke = over && !double.IsNaN(strokeX) ? Math.Min(MaxStroke, Math.Abs(PointerX!.Value - strokeX) + Math.Abs(PointerHeight - strokeHeight)) : 0;
        if (over) { strokeX = PointerX!.Value; strokeHeight = PointerHeight; } else strokeX = double.NaN;
        // By index over the enemies there were at the start: a mirage's copies join the list.
        int count = slimes.Count;
        for (int i = 0; i < count; i++)
        {
            var s = slimes[i];
            if (!s.Type.UsesMouse) continue;
            s.Revealed = Math.Max(0, s.Revealed - dt);
            if (s.State == SlimeState.Defeated) continue;
            if (s.Type.Ghost is GhostSpec ghost) StepGhost(s, ghost, dt);
            else if (s.Type.Bubble is BubbleSpec) StepBubble(s, dt);
            else if (s.Type.Mirage is MirageSpec mirage) StepMirage(s, mirage);
            else if (s.Type.Sulk is SulkSpec sulk) StepSulk(s, sulk, dt, stroke);
            if (Over) return;
        }
    }

    private void StepGhost(Slime s, GhostSpec ghost, double dt)
    {
        if (!s.Targetable) return; // Still appearing: seen.
        if (PointerOn(s) || LaserNear(s, ghost.LaserRange))
        {
            s.Revealed = ghost.Reveal;
            if (s.Faded)
            {
                s.Faded = false;
                s.Cooldown = Math.Max(s.Cooldown, 0.7);
                s.GhostClock = 0;
                GhostsRevealed++;
                events.Add(new(BattleEventKind.GhostRevealed, s.X, s.Id));
            }
        }
        s.GhostClock -= dt;
        if (s.GhostClock > 0) return;
        if (s.Faded) { s.Faded = false; s.Cooldown = Math.Max(s.Cooldown, 0.7); s.GhostClock = s.Elite != null ? ghost.EliteVisible : ghost.Visible; return; }
        // Kept out by the cursor or the laser, or in the middle of an attack (or the Grabber): not yet.
        if (s.Revealed > 0 || s.Handled || s.State is SlimeState.Windup or SlimeState.Dashing) return;
        s.Faded = true;
        s.GhostClock = ghost.Hidden;
        s.FadeSide = s.X < lastCatX ? -1 : 1;
        events.Add(new(BattleEventKind.GhostFaded, s.X, s.Id));
    }

    // A faded ghost drifts through the cat to a spot just past it on its other side (its own speed × 1.3).
    private void Drift(Slime s, double dt, double catX, double minX, double maxX)
    {
        double want = Math.Clamp(catX - s.FadeSide * s.Range * 0.7, minX, maxX), gap = want - s.X;
        if (Math.Abs(gap) > 1) s.Facing = gap < 0 ? -1 : 1;
        s.X = Math.Clamp(s.X + Math.Sign(gap) * Math.Min(Math.Abs(gap), Speed(s) * 1.3 * dt), minX, maxX);
    }

    private void StepBubble(Slime s, double dt)
    {
        if (s.Bubbled || !s.Targetable) return;
        s.BubbleClock -= dt;
        if (s.BubbleClock > 0 || s.Handled) return;
        s.Bubbled = true;
        s.Pokes = 0;
        events.Add(new(BattleEventKind.BubbleRegrown, s.X, s.Id));
    }

    // A click on an enemy, before it is chosen as the cat's target (the window): a bubble takes the click
    // (enough of them pop it). False: not an enemy in a bubble, so the click chooses it as usual.
    public bool Poke(int id)
    {
        if (Over || Paused || ChoosingUpgrade || Find(id) is not { Targetable: true, Bubbled: true } s) return false;
        s.Pokes++;
        s.HitFlash = 0.12;
        events.Add(new(BattleEventKind.BubblePoked, s.X, s.Id));
        if (s.Pokes >= PopsNeeded(s)) PopBubble(s);
        return true;
    }

    // The bubble bursts (clicks, the Scissors): it drops out of the air dazed (a heal or a windup is lost).
    private void PopBubble(Slime s)
    {
        if (!s.Bubbled || s.Type.Bubble is not BubbleSpec bubble) return;
        s.Bubbled = false;
        s.Pokes = 0;
        s.BubbleClock = bubble.Regrow;
        BubblesPopped++;
        AddEffect(BattleEffectKind.Spark, s.X, s.Height / 2 + bubble.Lift);
        if (bubble.Stun > 0 && !s.Handled)
        {
            if (s.State == SlimeState.Casting) { s.HealCooldown = HealCooldown(s); s.HealTargetId = 0; s.FarHeal = false; s.MendingId = 0; }
            s.StunRemaining = bubble.Stun * s.Type.StunFactor;
            s.Set(SlimeState.Stunned);
            RefreshGuards();
        }
        events.Add(new(BattleEventKind.BubblePopped, s.X, s.Id));
    }

    private void StepMirage(Slime s, MirageSpec mirage)
    {
        if (PointerOn(s) || LaserNear(s, mirage.LaserRange)) s.Revealed = mirage.Reveal;
        if (s.Decoy)
        {
            // A copy shows the real one's HP; the real one gone, so is it.
            var real = Find(s.DecoyOf);
            if (real == null || real.State == SlimeState.Defeated) { Dispel(s); return; }
            s.Hp = real.Hp; s.MaxHp = real.MaxHp;
            return;
        }
        if (s.Cast) return;
        // Its copies come with it, one on each side in turn, looking just like it (an elite's too).
        s.Cast = true;
        int copies = s.Elite != null ? mirage.EliteCopies : mirage.Copies;
        double gap = Math.Max(44, s.Width * 1.15);
        for (int k = 0; k < copies; k++)
        {
            var copy = AddSlime(s.X + (k % 2 == 0 ? 1 : -1) * (k / 2 + 1) * gap, s.Type);
            copy.DecoyOf = s.Id;
            copy.Cast = true;
            copy.Elite = s.Elite;
            copy.Hp = s.Hp; copy.MaxHp = s.MaxHp;
            copy.Facing = s.Facing;
            if (s.State == SlimeState.Spawning) copy.StateTime = s.StateTime; else copy.Set(SlimeState.Moving);
        }
        events.Add(new(BattleEventKind.MirageCast, s.X, s.Id));
    }

    // A copy is gone (hit, grabbed, pushed, or its real one defeated): not a defeat of the cat's.
    private void Dispel(Slime copy)
    {
        if (!copy.Decoy || copy.State == SlimeState.Defeated) return;
        copy.Set(SlimeState.Defeated);
        copy.Dispelled = true;
        copy.KnockVelocity = 0;
        ClearStatus(copy);
        if (SelectedId == copy.Id) SelectedId = null;
        DecoysDispelled++;
        AddEffect(BattleEffectKind.Poof, copy.X, copy.Height / 2, 1);
        events.Add(new(BattleEventKind.MirageDispelled, copy.X, copy.Id));
    }

    private void StepSulk(Slime s, SulkSpec sulk, double dt, double stroke)
    {
        if (!s.Targetable) return;
        if (stroke > 0 && PointerOn(s))
        {
            if (s.Calm <= 0) events.Add(new(BattleEventKind.SulkPetted, s.X, s.Id));
            double part = stroke / CalmNeeded(s);
            s.Calm += part;
            s.Puff = Math.Max(1, s.Puff - part * (sulk.MaxGrow - 1));
            s.CalmIdle = 0;
            if (s.Calm >= 1) Soothe(s);
            return;
        }
        s.CalmIdle += dt;
        if (s.CalmIdle > sulk.Idle) s.Calm = Math.Max(0, s.Calm - sulk.Fade * dt);
    }

    // Soothed: it waves goodbye and leaves, a defeat of the cat's (no puff of smoke: hearts instead).
    private void Soothe(Slime s)
    {
        s.Calm = 1;
        s.Calmed = true;
        SulksCalmed++;
        AddEffect(BattleEffectKind.Heart, s.X, s.Height);
        events.Add(new(BattleEventKind.SulkCalmed, s.X, s.Id));
        Defeat(s, false);
    }

    // Before a hit lands (PetBattle.cs Damage): a faded ghost, a bubble, a copy or a sulky slime takes it
    // its own way. True: the hit is spent here and did no damage.
    private bool TrickHit(Slime slime, DamageSource source)
    {
        if (!slime.Type.UsesMouse) return false;
        if (slime.Decoy) { Dispel(slime); return true; }
        if (slime.Faded)
        {
            if (source == DamageSource.Poison) return false; // Poison already in it still works.
            AddEffect(BattleEffectKind.Miss, slime.X, slime.Height / 2);
            events.Add(new(BattleEventKind.GhostMissed, slime.X, slime.Id));
            return true;
        }
        if (slime.Bubbled)
        {
            if (source == DamageSource.Poison) return true;
            AddEffect(BattleEffectKind.Miss, slime.X, slime.Height / 2 + BubbleLift(slime));
            events.Add(new(BattleEventKind.BubbleBlocked, slime.X, slime.Id));
            return true;
        }
        if (slime.Type.Sulk is SulkSpec sulk)
        {
            if (source == DamageSource.Poison) return true;
            slime.Puff = Math.Min(sulk.MaxGrow, slime.Puff + sulk.Grow);
            slime.Calm = Math.Max(0, slime.Calm - 0.1);
            slime.HitFlash = 0.18;
            AddEffect(BattleEffectKind.Miss, slime.X, slime.Height * slime.Puff / 2, 1);
            events.Add(new(BattleEventKind.SulkPuffed, slime.X, slime.Id));
            return true;
        }
        return false;
    }
}
