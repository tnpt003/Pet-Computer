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
    private readonly System.Collections.Generic.Queue<PetAction> recentLocal = new();

    private PetAction ChooseLocal(PetModel pet)
    {
        PetAction[] choices = { PetAction.Idle, PetAction.Walking, PetAction.Jumping,
            PetAction.Sitting, PetAction.Sleeping, PetAction.Looking, PetAction.Grooming, PetAction.Stretching };
        double total = 0;
        var weights = new double[choices.Length];
        for (int i = 0; i < choices.Length; i++)
        {
            if ((choices[i] is PetAction.Grooming or PetAction.Stretching) && selfCareCooldown > 0) continue;
            if (choices[i] == lastLocalAction || (choices[i] == PetAction.Sleeping && pet.Emotions.WakeCooldownRemaining > 0)) continue;
            double weight = choices[i] == PetAction.Walking ? 3 : 1;
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

    public void Reset()
    {
        remaining = 2;
        followingMouse = false;
        LastAppliedCommand = "LOCAL";
    }

    public void Step(PetModel pet, double dt, double? cursorX = null)
    {
        attentionCooldown = Math.Max(0, attentionCooldown - dt);
        selfCareCooldown = Math.Max(0, selfCareCooldown - dt);
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
        if (Quiet)
        {
            followingMouse = false;
            pet.Action = PetAction.Idle;
            pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
            return;
        }
        if (pet.Action == PetAction.PreparingJump)
        {
            pet.VX = pet.VY = 0; remaining -= dt;
            if (remaining <= 0)
            {
                pet.Action = PetAction.Jumping; pet.Grounded = false;
                pet.VY = -settings.JumpSpeed; pet.VX = pet.Facing * settings.WalkSpeed; remaining = 2;
            }
            return;
        }
        if (pet.Action is PetAction.Grooming or PetAction.Stretching)
        {
            pet.VX = pet.VY = 0; remaining -= dt;
            if (remaining <= 0) { pet.Action = PetAction.Idle; Reset(); }
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
        LastAppliedCommand = "LOCAL";
        remaining = 2 + random.NextDouble() * 4;
        pet.VX = 0;
        pet.Facing = random.Next(2) == 0 ? -1 : 1;
        pet.Action = ChooseLocal(pet);
        if (pet.Action is PetAction.Grooming or PetAction.Stretching)
        { remaining = pet.Action == PetAction.Grooming ? PetSettings.GroomingDuration : PetSettings.StretchingDuration; selfCareCooldown = PetSettings.SelfCareCooldown; }
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
        if (Quiet && decision.Command is "WALK_LEFT" or "WALK_RIGHT" or "FOLLOW_MOUSE" or "JUMP" or "SEEK_ATTENTION") return false;
        if (decision.Command == "SLEEP" && pet.Emotions.WakeCooldownRemaining > 0) return false;
        if (!pet.Grounded || pet.Emotions.BlocksAutonomy || PetActionRules.RejectsDecisions(pet.Action) ||
            !AiProtocol.IsCommand(decision.Command) || !double.IsFinite(decision.DurationSeconds) ||
            decision.DurationSeconds < 0.5 || decision.DurationSeconds > 12) return false;
        followingMouse = decision.Command == "FOLLOW_MOUSE";
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
            _ => PetAction.Idle
        };
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
