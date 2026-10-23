using System;

namespace DesktopPet;

// Break reminder clock. Counts only time the user is at the computer (recent
// system-wide input); being away long enough counts as the break itself.
// The pet window decides when a reminder can be shown; Python picks the words.
public sealed class PetBreaks
{
    public const double AwayResets = PetContext.AwayAfter;
    // If Python does not supply a line this long after the reminder could be shown,
    // a built-in line is used so reminders also work with the AI off.
    public const double LocalFallbackAfter = 15;
    public bool Enabled { get; private set; }
    public double IntervalSeconds { get; private set; } = 45 * 60;
    public double ActiveSeconds { get; private set; }
    public double DueFor { get; private set; }
    public bool Due => Enabled && ActiveSeconds >= IntervalSeconds;

    public void Configure(bool enabled, int minutes)
    {
        double interval = Math.Clamp(minutes, 1, 240) * 60.0;
        if (enabled != Enabled || interval != IntervalSeconds) { ActiveSeconds = 0; DueFor = 0; }
        Enabled = enabled; IntervalSeconds = interval;
    }

    // deliverable: the pet could show a line right now (awake, free, not fullscreen).
    public void Step(double dt, double systemIdleSeconds, bool deliverable)
    {
        if (!Enabled || !double.IsFinite(dt) || dt <= 0) return;
        if (systemIdleSeconds >= AwayResets) { ActiveSeconds = 0; DueFor = 0; return; }
        ActiveSeconds += dt;
        DueFor = Due && deliverable ? DueFor + dt : 0;
    }

    public bool NeedsLocalLine => Due && DueFor >= LocalFallbackAfter;

    public void Delivered() { ActiveSeconds = 0; DueFor = 0; }

    public static bool IsBreakLine(string? id) => id != null && id.StartsWith("break:", StringComparison.Ordinal);

    // The first three "break" lines of ai/lines.json in each language (ContextTests compares them).
    private static readonly string[] Vietnamese =
    {
        "Sen ơi, uống ngụm nước đi nào!", "Ngồi lâu rồi đó, đứng dậy vươn vai với {name} nha~", "Nghỉ mắt chút đi sen, nhìn ra xa nè!"
    };
    private static readonly string[] English =
    {
        "Human, time for a sip of water!", "You've been sitting a while, stretch with {name}~", "Rest your eyes, look far away for a bit!"
    };

    private static readonly string[] Japanese =
    {
        "ご主人、お水を一口飲もう！", "ずっと座ってるね、{name}と伸びしよう〜", "目を休めて、ちょっと遠くを見よう！"
    };

    internal static string[] Lines(string language) =>
        language switch { PetLanguage.Vietnamese => Vietnamese, PetLanguage.Japanese => Japanese, _ => English };

    public static (string Id, string Text) LocalLine(string language, string name, Random random)
    {
        var lines = Lines(language);
        int index = random.Next(lines.Length);
        return ($"break:local:{language}:{index}", lines[index].Replace("{name}", string.IsNullOrWhiteSpace(name) ? "Mochi" : name));
    }
}

// What the pet knows about the computer: only window geometry and input timing,
// never screen content. Pure state so the rules are testable without Win32.
public sealed class PetContext
{
    public const double AwayAfter = 300;
    // Input within this many seconds after being away means the user is back.
    public const double BackWithin = 2;
    public bool Fullscreen { get; private set; }
    public bool Away { get; private set; }
    public double IdleSeconds { get; private set; }
    // Set when the user comes back after being away; consumed by the next AI observation.
    public bool Returned { get; private set; }

    // Returns what changed so the window can react once per transition.
    public (bool FullscreenChanged, bool WentAway, bool CameBack) Update(bool fullscreen, double idleSeconds)
    {
        IdleSeconds = double.IsFinite(idleSeconds) ? Math.Max(0, idleSeconds) : 0;
        bool fullscreenChanged = fullscreen != Fullscreen;
        Fullscreen = fullscreen;
        bool wentAway = !Away && IdleSeconds >= AwayAfter;
        bool cameBack = Away && IdleSeconds < BackWithin;
        if (wentAway) Away = true;
        if (cameBack) { Away = false; Returned = true; }
        return (fullscreenChanged, wentAway, cameBack);
    }

    public bool TakeReturned() { bool value = Returned; Returned = false; return value; }
}
