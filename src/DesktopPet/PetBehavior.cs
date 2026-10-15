using System;
using DesktopPet.AI;

namespace DesktopPet;

public sealed class PetBehavior(PetSettings settings, Random? randomSource = null)
{
    private readonly Random random = randomSource ?? new();
    private double remaining = 2;
    private bool followingMouse;
    private double attentionCooldown, selfCareCooldown = 25;
    public string Personality { get; set; } = "Balanced";
    private PetAction? lastLocalAction;
    // The action the user asked for from the menu; it runs its full length even in Quiet mode.
    private PetAction? manualAction;
    public static readonly PetAction[] ManualActions = { PetAction.Sitting, PetAction.Looking, PetAction.PreparingJump,
        PetAction.Sleeping, PetAction.SeekingAttention, PetAction.Grooming, PetAction.Stretching, PetAction.Kneading,
        PetAction.RollingOver, PetAction.ChasingTail, PetAction.Scratching, PetAction.Sneezing };
    public const double ManualPoseDuration = 4;
    private readonly System.Collections.Generic.Queue<PetAction> recentLocal = new();

    private PetAction ChooseLocal(PetModel pet)
    {
        PetAction[] choices = { PetAction.Idle, PetAction.Walking, PetAction.Jumping,
            PetAction.Sitting, PetAction.Sleeping, PetAction.Looking, PetAction.Grooming, PetAction.Stretching,
            PetAction.ChasingTail, PetAction.Scratching, PetAction.Sneezing };
        double total = 0;
        var weights = new double[choices.Length];
        for (int i = 0; i < choices.Length; i++)
        {
            // Self-care and small gestures share one cooldown so they stay occasional.
            if ((choices[i] is PetAction.Grooming or PetAction.Stretching || PetActionRules.IsGesture(choices[i])) && selfCareCooldown > 0) continue;
            if (choices[i] == lastLocalAction || (choices[i] == PetAction.Sleeping && pet.Emotions.WakeCooldownRemaining > 0)) continue;
            double weight = choices[i] switch
            {
                PetAction.Walking => 3,
                PetAction.ChasingTail => Personality == "Playful" ? 1.1 : Personality == "Calm" ? 0.2 : 0.6,
                PetAction.Scratching => 0.5,
                PetAction.Sneezing => 0.2,
                _ => 1
            };
            if (Personality == "Calm" && choices[i] is PetAction.Walking or PetAction.Jumping) weight *= 0.4;
            if (Personality == "Curious" && choices[i] == PetAction.Looking) weight *= 2;
            if (Personality == "Playful" && choices[i] == PetAction.Jumping) weight *= 1.8;
            foreach (var previous in recentLocal) if (previous == choices[i]) weight *= 0.4;
            total += weights[i] = weight;
        }
        double pick = random.NextDouble() * total;
        var selected = PetAction.Idle;
        for (int i = 0; i < choices.Length; i++) { pick -= weights[i]; if (pick < 0) { selected = choices[i]; break; } }
        lastLocalAction = selected;
        recentLocal.Enqueue(selected);
        if (recentLocal.Count > 4) recentLocal.Dequeue();
        return selected;
    }
    public event Action? DecisionRequested;
    public string LastAppliedCommand { get; private set; } = "LOCAL";
    public bool Quiet { get; set; }
    // Away nap: the user left the computer. The sleep clock is held (like Pause) until
    // the user returns or clicks; ordinary sleep is unaffected.
    public bool HoldSleep { get; private set; }

    // With the pet given, an ongoing waking keeps its own 1.2 s clock (AI toggles, clicks
    // and Quiet changes must not stretch it).
    public void Reset(PetModel? pet = null)
    {
        if (pet?.Action != PetAction.Waking) remaining = 2;
        followingMouse = false;
        manualAction = null;
        leap = null;
        LastAppliedCommand = "LOCAL";
    }

    // "Do now" menu: skips cooldowns, bond, chance and Quiet, but never input, flight,
    // sleep or waking (the window queues the request until the pet is free).
    public static bool CanStartManual(PetModel pet, PetAction action) =>
        Array.IndexOf(ManualActions, action) >= 0 && pet.Grounded && !pet.Emotions.SleepLocked && !pet.Emotions.BlocksAutonomy &&
        pet.Action is not (PetAction.Sleeping or PetAction.Waking or PetAction.Dragging or PetAction.Thrown or PetAction.Jumping or PetAction.PreparingJump);

    public bool StartManual(PetModel pet, PetAction action)
    {
        if (!CanStartManual(pet, action)) return false;
        followingMouse = false;
        leap = null;
        pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
        LastAppliedCommand = "LOCAL";
        pet.Action = action;
        manualAction = action == PetAction.Sleeping ? null : action;
        if (action == PetAction.Sleeping) { pet.Emotions.EnterSleep(); return true; } // Wake cooldown skipped; the sleep rules stay.
        remaining = action switch
        {
            PetAction.PreparingJump => PetSettings.JumpPreparationDuration,
            PetAction.SeekingAttention => PetSettings.AttentionGestureDuration,
            PetAction.Grooming => PetSettings.GroomingDuration,
            PetAction.Stretching => PetSettings.StretchingDuration,
            _ when PetActionRules.IsGesture(action) => PetSettings.GestureDuration(action),
            _ => ManualPoseDuration
        };
        // Keeps the automatic ones occasional; the menu itself ignores this cooldown.
        if (action is PetAction.Grooming or PetAction.Stretching || PetActionRules.IsGesture(action)) selfCareCooldown = PetSettings.SelfCareCooldown;
        return true;
    }

    public void Step(PetModel pet, double dt, double? cursorX = null)
    {
        attentionCooldown = Math.Max(0, attentionCooldown - dt);
        selfCareCooldown = Math.Max(0, selfCareCooldown - dt);
        if (!pet.Grounded && pet.Action == PetAction.Waking)
        {
            // Waking keeps its 1.2 s even if the window under the pet vanished meanwhile.
            remaining -= dt;
            if (remaining <= 0) { pet.Action = PetAction.Idle; Reset(); }
            return;
        }
        if (!pet.Grounded || pet.Action is PetAction.Dragging or PetAction.Thrown) return;
        if (pet.Action == PetAction.Sleeping || pet.Emotions.SleepLocked)
        {
            // Only this sleep clock or a direct click can release the lock.
            pet.Emotions.EnterSleep();
            followingMouse = false;
            pet.Action = PetAction.Sleeping;
            pet.VX = pet.VY = 0;
            if (!HoldSleep) pet.Emotions.AdvanceSleep(dt);
            double duration = double.IsFinite(settings.SleepDurationSeconds) && settings.SleepDurationSeconds > 0
                ? settings.SleepDurationSeconds : 10;
            if (pet.Emotions.SleepElapsed >= duration) Wake(pet);
            return;
        }
        if (pet.Action is not (PetAction.Sleeping or PetAction.Waking or PetAction.Jumping) && pet.Emotions.BlocksAutonomy)
        {
            followingMouse = false;
            pet.Action = PetAction.Idle;
            pet.VX = pet.VY = 0;
            return;
        }
        if (pet.Action == PetAction.Walking && pet.VX != 0)
            pet.VX = pet.Facing * settings.WalkSpeed * pet.Emotions.WalkScale;
        if (pet.Action == PetAction.Waking)
        {
            // These states own their full duration: no random actions or AI requests.
            pet.VX = pet.VY = 0;
            remaining -= dt;
            if (remaining <= 0)
            {
                pet.Action = PetAction.Idle; Reset();
            }
            return;
        }
        if (manualAction == pet.Action && pet.Action != PetAction.PreparingJump)
        {
            pet.VX = pet.VY = 0; remaining -= dt;
            if (remaining <= 0) { pet.Action = PetAction.Idle; Reset(); }
            return;
        }
        // Kneading answers the user's petting, so Quiet mode keeps it; other poses are autonomous.
        if ((pet.Action is PetAction.Grooming or PetAction.Stretching || PetActionRules.IsGesture(pet.Action)) &&
            (!Quiet || pet.Action == PetAction.Kneading))
        {
            pet.VX = pet.VY = 0; remaining -= dt;
            if (remaining <= 0) { pet.Action = PetAction.Idle; Reset(); }
            return;
        }
        if (Quiet && !(manualAction == PetAction.PreparingJump && pet.Action == PetAction.PreparingJump))
        {
            followingMouse = false;
            leap = null;
            pet.Action = PetAction.Idle;
            pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
            // Python still hears about the pet on the usual schedule (needs, diary, reaction
            // lines); TryApply refuses its commands while Quiet, so the cat stays still.
            remaining -= dt;
            if (remaining <= 0) { remaining = 2 + random.NextDouble() * 4; LastAppliedCommand = "LOCAL"; DecisionRequested?.Invoke(); }
            return;
        }
        if (pet.Action == PetAction.PreparingJump)
        {
            pet.VX = pet.VY = 0; remaining -= dt;
            if (remaining <= 0)
            {
                pet.Action = PetAction.Jumping; pet.Grounded = false; manualAction = null;
                pet.VY = -settings.JumpSpeed; pet.VX = pet.Facing * settings.WalkSpeed; remaining = 2;
                if (leap is var (vx, vy)) { pet.VX = vx; pet.VY = vy; leap = null; } // Up onto a window.
            }
            return;
        }
        if (followingMouse && cursorX.HasValue)
        {
            double difference = cursorX.Value - (pet.X + settings.Width / 2);
            if (Math.Abs(difference) < 18)
            {
                pet.VX = 0;
                pet.Action = PetAction.Idle;
            }
            else
            {
                pet.Facing = Math.Sign(difference);
                pet.VX = pet.Facing * settings.WalkSpeed * pet.Emotions.WalkScale;
                pet.Action = PetAction.Walking;
            }
        }
        remaining -= dt;
        if (remaining > 0) return;
        // Always choose a local action immediately. An async AI reply may replace it.
        DecisionRequested?.Invoke();
        followingMouse = false;
        manualAction = null;
        leap = null;
        LastAppliedCommand = "LOCAL";
        remaining = 2 + random.NextDouble() * 4;
        pet.VX = 0;
        pet.Facing = random.Next(2) == 0 ? -1 : 1;
        pet.Action = ChooseLocal(pet);
        if (pet.Action is PetAction.Grooming or PetAction.Stretching)
        { remaining = pet.Action == PetAction.Grooming ? PetSettings.GroomingDuration : PetSettings.StretchingDuration; selfCareCooldown = PetSettings.SelfCareCooldown; }
        if (PetActionRules.IsGesture(pet.Action)) { remaining = PetSettings.GestureDuration(pet.Action); selfCareCooldown = PetSettings.SelfCareCooldown; }
        if (pet.Action == PetAction.Looking && Personality == "Curious") remaining = 5 + random.NextDouble() * 2;
        if (pet.Action == PetAction.Walking) pet.VX = settings.WalkSpeed * pet.Facing * pet.Emotions.WalkScale;
        if (pet.Action == PetAction.Jumping)
        {
            pet.Action = PetAction.PreparingJump; pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
            remaining = PetSettings.JumpPreparationDuration;
        }
        if (pet.Action == PetAction.Sleeping) pet.Emotions.EnterSleep();
    }

    public bool TryApply(PetModel pet, AiDecision decision)
    {
        if (decision.Command == "SEEK_ATTENTION" && attentionCooldown > 0) return false;
        if (Quiet) return false; // Quiet (or a fullscreen app): no AI-driven action of any kind.
        if (decision.Command == "SLEEP" && pet.Emotions.WakeCooldownRemaining > 0) return false;
        if (!pet.Grounded || pet.Emotions.BlocksAutonomy || PetActionRules.RejectsDecisions(pet.Action) ||
            !AiProtocol.IsCommand(decision.Command) || !double.IsFinite(decision.DurationSeconds) ||
            decision.DurationSeconds < 0.5 || decision.DurationSeconds > 12) return false;
        followingMouse = decision.Command == "FOLLOW_MOUSE";
        manualAction = null;
        leap = null;
        remaining = decision.Command == "SEEK_ATTENTION" ? PetSettings.AttentionGestureDuration : decision.DurationSeconds;
        if (decision.Command == "SEEK_ATTENTION") attentionCooldown = PetSettings.AttentionCooldown;
        LastAppliedCommand = decision.Command;
        pet.VX = pet.VY = 0;
        pet.Action = decision.Command switch
        {
            "WALK_LEFT" or "WALK_RIGHT" or "FOLLOW_MOUSE" => PetAction.Walking,
            "LOOK" => PetAction.Looking,
            "SIT" => PetAction.Sitting,
            "SLEEP" => PetAction.Sleeping,
            "JUMP" => PetAction.Jumping,
            "SEEK_ATTENTION" => PetAction.SeekingAttention,
            "ROLL_OVER" => PetAction.RollingOver,
            "CHASE_TAIL" => PetAction.ChasingTail,
            "SCRATCH" => PetAction.Scratching,
            _ => PetAction.Idle
        };
        // Gestures own a fixed C# duration; Python's duration only paces ordinary actions.
        if (PetActionRules.IsGesture(pet.Action)) remaining = PetSettings.GestureDuration(pet.Action);
        if (decision.Command is "WALK_LEFT" or "WALK_RIGHT")
        {
            pet.Facing = decision.Command == "WALK_LEFT" ? -1 : 1;
            pet.VX = pet.Facing * settings.WalkSpeed * pet.Emotions.WalkScale;
        }
        if (pet.Action == PetAction.Jumping)
        {
            pet.Action = PetAction.PreparingJump; pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
            remaining = PetSettings.JumpPreparationDuration;
        }
        if (pet.Action == PetAction.Sleeping) pet.Emotions.EnterSleep();
        return true;
    }

    public void WakeByClick(PetModel pet)
        => Wake(pet);

    // Only for a grounded pet that input does not own; ignores the wake cooldown
    // because nobody is there to be disturbed.
    public bool StartAwayNap(PetModel pet)
    {
        if (!pet.Grounded || pet.Action is PetAction.Dragging or PetAction.Thrown or PetAction.Jumping or PetAction.PreparingJump) return false;
        followingMouse = false;
        pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
        pet.Action = PetAction.Sleeping;
        pet.Emotions.EnterSleep();
        HoldSleep = true;
        return true;
    }

    // The user is back: a held nap ends with the normal waking animation.
    public void EndAwayNap(PetModel pet)
    {
        if (!HoldSleep) return;
        HoldSleep = false;
        Wake(pet);
    }

    // Sustained petting: the pet sits and kneads. Works with Quiet mode and without Python.
    public bool StartKneading(PetModel pet)
    {
        if (!pet.Grounded || pet.Emotions.BlocksAutonomy || PetActionRules.RejectsDecisions(pet.Action)) return false;
        followingMouse = false;
        pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
        pet.Action = PetAction.Kneading;
        remaining = PetSettings.KneadingDuration;
        LastAppliedCommand = "LOCAL";
        return true;
    }

    // Leap onto a window's edge: the usual short crouch, then the planned velocity.
    // Autonomous, so Quiet mode and real reactions refuse it.
    private (double VX, double VY)? leap;
    public bool StartLeap(PetModel pet, double vx, double vy)
    {
        if (Quiet || !pet.Grounded || pet.Emotions.BlocksAutonomy || PetActionRules.RejectsDecisions(pet.Action) ||
            !double.IsFinite(vx) || !double.IsFinite(vy)) return false;
        followingMouse = false;
        manualAction = null;
        pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
        if (Math.Abs(vx) > 1) pet.Facing = vx < 0 ? -1 : 1;
        pet.Action = PetAction.PreparingJump;
        remaining = PetSettings.JumpPreparationDuration;
        leap = (vx, vy);
        LastAppliedCommand = "LOCAL";
        return true;
    }

    // A short stretch (break reminder); only from a free, awake pose.
    public bool StartStretch(PetModel pet)
    {
        if (Quiet || !pet.Grounded || pet.Emotions.BlocksAutonomy || PetActionRules.RejectsDecisions(pet.Action)) return false;
        followingMouse = false;
        pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
        pet.Action = PetAction.Stretching;
        remaining = PetSettings.StretchingDuration;
        LastAppliedCommand = "LOCAL";
        return true;
    }

    private void Wake(PetModel pet)
    {
        HoldSleep = false;
        if (pet.Action != PetAction.Sleeping && !pet.Emotions.SleepLocked) return;
        pet.Emotions.FinishSleep(settings.WakeCooldownSeconds);
        followingMouse = false;
        pet.VX = pet.VY = 0;
        pet.Action = PetAction.Waking;
        remaining = PetSettings.WakingDuration;
    }
}
