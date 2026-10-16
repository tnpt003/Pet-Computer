using System;
using System.Collections.Generic;

namespace DesktopPet;

public enum ThoughtKind { None, Hungry, Bored, Sleepy, Love }

// Short thought bubble suggested by Python. C# decides whether it may appear and
// for how long; it is presentation only and never changes action or physics.
public sealed class PetThought
{
    public const double Duration = 2.5;
    public const double KindCooldown = 20;
    public const double GlobalCooldown = 8;
    public ThoughtKind Kind { get; private set; }
    public double Time { get; private set; }
    public bool Visible => Kind != ThoughtKind.None;
    private double now, globalReady;
    private readonly Dictionary<ThoughtKind, double> kindReady = new();

    public static ThoughtKind Parse(string? value) => value switch
    {
        "hungry" => ThoughtKind.Hungry,
        "bored" => ThoughtKind.Bored,
        "sleepy" => ThoughtKind.Sleepy,
        "love" => ThoughtKind.Love,
        _ => ThoughtKind.None
    };

    // Bubbles stay out of sleep, input, flight, eating and strong reactions.
    public static bool CanShow(PetModel pet) =>
        PetActionRules.IsSettled(pet) && pet.TreatRemaining <= 0 &&
        pet.Emotions.Current is not (PetEmotion.Angry or PetEmotion.Surprised or PetEmotion.Dizzy or PetEmotion.Sleepy) &&
        pet.Emotions.Target is not (PetEmotion.Angry or PetEmotion.Surprised or PetEmotion.Sleepy);

    public bool TryShow(ThoughtKind kind, PetModel pet)
    {
        if (kind == ThoughtKind.None || Visible || !CanShow(pet) || now < globalReady ||
            (kindReady.TryGetValue(kind, out double ready) && now < ready)) return false;
        Kind = kind;
        Time = 0;
        globalReady = now + Duration + GlobalCooldown;
        kindReady[kind] = now + KindCooldown;
        return true;
    }

    public void Step(PetModel pet, double dt)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        now += dt;
        if (!Visible) return;
        Time += dt;
        if (Time >= Duration || !CanShow(pet)) Clear();
    }

    public void Clear() { Kind = ThoughtKind.None; Time = 0; }

    // 0..1 fade in over 0.25 s and out over the last 0.4 s.
    public double Opacity => !Visible ? 0 : Math.Clamp(Math.Min(Time / 0.25, (Duration - Time) / 0.4), 0, 1);
    public double Rise => !Visible ? 0 : 4 * (1 - Math.Clamp(Time / 0.35, 0, 1));
}
