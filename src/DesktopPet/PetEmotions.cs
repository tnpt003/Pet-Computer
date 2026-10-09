using System;
using System.Collections.Generic;

namespace DesktopPet;

public enum PetEmotion { Neutral, Happy, Angry, Sad, Surprised, Sleepy, Dizzy }

// Immediate input reactions and offline fallback. Never changes position, action,
// or physics. Python may supply corroborated long-term mood hints separately.
public sealed class PetEmotions
{
    public const double SleepyAfter = 120;
    public const double SadAfter = 300;
    public PetEmotion Current { get; private set; }
    public PetEmotion Target { get; private set; }
    public bool SleepLocked { get; private set; }
    public double SleepElapsed { get; private set; }
    public double WakeCooldownRemaining { get; private set; }
    public void AdvanceSleep(double dt)
    {
        if (SleepLocked && double.IsFinite(dt) && dt > 0) SleepElapsed += dt;
    }
    public void EnterSleep()
    {
        if (!SleepLocked) SleepElapsed = 0;
        SleepLocked = true;
        Current = Target = PetEmotion.Sleepy;
        transient = aiHint = PetEmotion.Neutral;
        transientUntil = now;
        pettingTime = 0;
        PettingLean = 0;
        cursorKnown = false;
    }

    public void FinishSleep(double cooldown = 30)
    {
        Reset();
        WakeCooldownRemaining = double.IsFinite(cooldown) ? Math.Max(0, cooldown) : 30;
    }
    public bool BlocksAutonomy => Current is PetEmotion.Sleepy or PetEmotion.Surprised or PetEmotion.Angry ||
        Target is PetEmotion.Sleepy or PetEmotion.Surprised or PetEmotion.Angry;
    public double Strength { get; private set; }
    public double Time { get; private set; }
    public double SecondsWithoutAttention => now - lastAttention;
    public double PettingLean { get; private set; }
    public double WalkScale => Current == PetEmotion.Sad ? 1 - 0.35 * Strength : 1;
    private readonly Queue<double> clicks = new();
    private double now, lastAttention, transientUntil, happyAfter, angryAfter, surpriseAfter, pettingAfter;
    private double lastX, lastY, pettingTime;
    private bool cursorKnown;
    private PetEmotion transient, aiHint;

    public void Attention(bool click = false)
    {
        if (SleepLocked) return;
        lastAttention = now;
        aiHint = PetEmotion.Neutral;
        if (click)
        {
            while (clicks.Count > 0 && now - clicks.Peek() > 2) clicks.Dequeue();
            clicks.Enqueue(now);
            if (clicks.Count >= 4) { AggressiveInteraction(); return; }
        }
        if (transient == PetEmotion.Angry && now < transientUntil) return;
        if (now >= happyAfter)
        {
            SetTransient(PetEmotion.Happy, 3);
            happyAfter = now + 2;
        }
    }

    public void AggressiveInteraction()
    {
        if (SleepLocked) return;
        lastAttention = now;
        aiHint = PetEmotion.Neutral;
        if (now < angryAfter) return;
        SetTransient(PetEmotion.Angry, 4);
        angryAfter = now + 6;
    }

    public void Reset()
    {
        SleepLocked = false;
        SleepElapsed = 0;
        Current = Target = transient = aiHint = PetEmotion.Neutral;
        Strength = Time = 0;
        lastAttention = now;
        transientUntil = happyAfter = angryAfter = surpriseAfter = pettingAfter = now;
        pettingTime = 0;
        PettingLean = 0;
        cursorKnown = false;
        clicks.Clear();
    }

    public void ApplyAiHint(string? emotion)
    {
        if (SleepLocked) return;
        // Delayed hints cannot override fresh attention, and random action moods
        // such as "playful" do not generate unearned positive reactions.
        aiHint = emotion switch
        {
            "sad" when SecondsWithoutAttention >= SadAfter => PetEmotion.Sad,
            "sleepy" when SecondsWithoutAttention >= SleepyAfter && WakeCooldownRemaining <= 0 => PetEmotion.Sleepy,
            _ => PetEmotion.Neutral
        };
    }

    private void SetTransient(PetEmotion emotion, double duration)
    {
        transient = emotion;
        transientUntil = now + duration;
    }

    // Returns true only when a deliberate gentle hover-stroke registers as petting.
    public bool Step(PetModel pet, double dt, double? cursorX = null, double? cursorY = null, bool overPet = false)
    {
        if (!double.IsFinite(dt) || dt <= 0) return false;
        now += dt;
        WakeCooldownRemaining = Math.Max(0, WakeCooldownRemaining - dt);
        Time += dt;
        if (pet.Action == PetAction.Sleeping && !SleepLocked) EnterSleep();
        if (SleepLocked)
        {
            Current = Target = PetEmotion.Sleepy;
            Strength = Math.Min(1, Strength + dt * 5);
            return false;
        }
        bool petted = false;
        PettingLean *= Math.Exp(-dt * 3);
        bool busy = pet.TreatRemaining > 0 || pet.Action is PetAction.Dragging or PetAction.Thrown or PetAction.Jumping or PetAction.PreparingJump or PetAction.Waking;
        if (busy) PettingLean = 0;
        if (cursorX.HasValue && cursorY.HasValue)
        {
            double x = cursorX.Value, y = cursorY.Value;
            double dx = x - lastX, dy = y - lastY;
            double speed = cursorKnown ? Math.Sqrt(dx * dx + dy * dy) / dt : 0;
            lastX = x; lastY = y; cursorKnown = true;
            if (!busy && overPet && speed >= 15 && speed <= 450)
            {
                if (!(transient == PetEmotion.Angry && now < transientUntil))
                    PettingLean += (Math.Clamp((x - pet.X - 56) / 32, -1, 1) - PettingLean) * (1 - Math.Exp(-dt * 7));
                pettingTime += dt;
                if (pettingTime >= 0.65 && now >= pettingAfter && !(transient == PetEmotion.Angry && now < transientUntil))
                {
                    Attention();
                    petted = true;
                    pettingTime = 0;
                    pettingAfter = now + 3;
                }
            }
            else pettingTime = 0;
            double nearX = x - (pet.X + 56), nearY = y - (pet.Y + 50);
            if (!busy && speed >= 1600 &&
                nearX * nearX + nearY * nearY <= 140 * 140 && now >= surpriseAfter &&
                !(now < transientUntil && transient is PetEmotion.Angry or PetEmotion.Happy))
            {
                SetTransient(PetEmotion.Surprised, 1.1);
                surpriseAfter = now + 6;
            }
        }

        PetEmotion desired;
        if (pet.Action == PetAction.Dragging)
            desired = pet.Pickup.Dizziness > 0.02 ? PetEmotion.Dizzy : PetEmotion.Neutral;
        else if (busy) desired = PetEmotion.Neutral;
        else if (now < transientUntil) desired = transient;
        else if (pet.Action == PetAction.Sleeping) desired = PetEmotion.Sleepy;
        else if (SecondsWithoutAttention >= SadAfter) desired = PetEmotion.Sad;
        else if (SecondsWithoutAttention >= SleepyAfter && WakeCooldownRemaining <= 0) desired = PetEmotion.Sleepy;
        else desired = aiHint;
        Target = desired;
        if (desired == PetEmotion.Sleepy) EnterSleep();

        // Fade out the old expression before fading in a different one.
        if (desired != Current)
        {
            Strength = Math.Max(0, Strength - dt * 8);
            if (Strength == 0) { Current = desired; Time = 0; }
        }
        else Strength = Math.Min(1, Strength + dt * 5);
        return petted;
    }
}
