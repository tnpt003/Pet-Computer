using System;

namespace DesktopPet;

// Timing and safety rules for speech bubbles. Python chooses the words; C# decides
// whether and how long they appear. Presentation only: never changes action or physics.
public sealed class PetSpeech
{
    public const double MinGap = 12;
    public const double MinDuration = 2.5;
    public const double MaxDuration = 6;
    // A line that arrives while the pet cannot talk waits this long (newest wins).
    public const double PendingFor = 10;
    // The once-per-session greeting may wait through a short nap (~10 s sleep + 1.2 s waking).
    public const double GreetingPendingFor = 30;
    public string? Id { get; private set; }
    public string Text { get; private set; } = "";
    public double Time { get; private set; }
    public double Duration { get; private set; }
    public bool Visible { get; private set; }
    private double now, ready, shownAt = double.NegativeInfinity;
    private string? lastId;
    private (string Id, string Text, double Until)? pending;
    public string? PendingId => pending?.Id;

    public static bool IsGreeting(string id) => id.StartsWith("hello:", StringComparison.Ordinal) || id.StartsWith("welcome_back:", StringComparison.Ordinal);

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

    // Shows the line now if possible; otherwise keeps it as the single pending line.
    public bool Offer(string id, string text, PetModel pet)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text)) return false;
        if (TryShow(id, text, pet)) { pending = null; return true; }
        // A wake-up yawn must not push out a greeting that waited through the nap.
        if (pending is { } held && IsGreeting(held.Id) && id.StartsWith("wake_up:", StringComparison.Ordinal) && now <= held.Until) return false;
        pending = (id, text, now + (IsGreeting(id) ? GreetingPendingFor : PendingFor));
        return false;
    }

    // Called every frame by the window after its own checks; true when the pending line appeared.
    public bool TryShowPending(PetModel pet)
    {
        if (pending is not { } line) return false;
        if (now > line.Until) { pending = null; return false; }
        if (!TryShow(line.Id, line.Text, pet)) return false;
        pending = null;
        return true;
    }

    public void DropPending() => pending = null;

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
