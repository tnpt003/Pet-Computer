using System;

namespace DesktopPet;

// Timing and safety rules for speech bubbles. Python chooses the words; C# decides
// whether and how long they appear. Presentation only: never changes action or physics.
public sealed class PetSpeech
{
    public const double MinGap = 12;
    public const double MinDuration = 2.5;
    public const double MaxDuration = 6;
    public string? Id { get; private set; }
    public string Text { get; private set; } = "";
    public double Time { get; private set; }
    public double Duration { get; private set; }
    public bool Visible { get; private set; }
    private double now, ready, shownAt = double.NegativeInfinity;
    private string? lastId;

    // Unlike thought icons, a short reaction may appear while the pet is upset.
    public static bool CanShow(PetModel pet) =>
        pet.Grounded && !pet.Emotions.SleepLocked && pet.TreatRemaining <= 0 &&
        pet.Action is not (PetAction.Sleeping or PetAction.Waking or PetAction.Dragging or PetAction.Thrown or PetAction.Jumping or PetAction.PreparingJump);

    public static double DurationFor(string text) => Math.Clamp(MinDuration + 0.06 * text.Length, MinDuration, MaxDuration);

    public bool TryShow(string id, string text, PetModel pet)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text) || Visible || now < ready || !CanShow(pet)) return false;
        Id = lastId = id;
        Text = text;
        Time = 0;
        Duration = DurationFor(text);
        Visible = true;
        shownAt = now;
        ready = now + Math.Max(MinGap, Duration + 2);
        return true;
    }

    public void Step(PetModel pet, double dt)
    {
        if (!double.IsFinite(dt) || dt <= 0) return;
        now += dt;
        if (!Visible) return;
        Time += dt;
        if (Time >= Duration || !CanShow(pet)) Hide();
    }

    public void Hide() { Visible = false; Id = null; Time = 0; }

    // Fade in over 0.2 s and out over the last 0.4 s.
    public double Opacity => !Visible ? 0 : Math.Clamp(Math.Min(Time / 0.2, (Duration - Time) / 0.4), 0, 1);

    // The last line shown in the past 30 s, for crediting the user's reaction.
    public (string Id, double Age)? LastShown => lastId != null && now - shownAt <= 30 ? (lastId, now - shownAt) : null;
}
