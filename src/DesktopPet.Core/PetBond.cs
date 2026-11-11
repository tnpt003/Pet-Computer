using System;

namespace DesktopPet;

// Bond milestones (docs/LIVING-CORNER.md → M3). Python keeps the bond and the highest milestone reached
// (1–5, never down) and sends it; C# opens one behaviour per milestone and times it. Without Python: 0,
// nothing opened.
public static class PetBondRules
{
    public const int Familiar = 2, Close = 3, VeryClose = 4, Soulmate = 5;
    public static readonly int[] Thresholds = { 0, 20, 40, 60, 80 };
    // Greeting the user back: after at least ten minutes away, once per return and 30 minutes apart; it waits
    // up to 30 seconds for the cat to be free (an away nap wakes by itself when the user comes back).
    public const double GreetAwaySeconds = 600, GreetWaitSeconds = 30, GreetGapSeconds = 1800, GlanceSeconds = 1.5, GreetWalkSeconds = 8, GreetNear = 40;
    // After a game the user started ends by itself: groom, stretch, then to the resting spot (or sit a while).
    public const double AfterPlayGapSeconds = 300, AfterPlayWaitSeconds = 20, RestWalkSeconds = 10, RestSitSeconds = 5;
    // Soulmate: a sleep it picks itself is slept right by a toy, a living-corner thing or the keepsake within reach.
    public const double LeanReach = 400, LeanGap = 30;
    // A milestone reached: a few hearts over the head, waiting up to ten minutes for the cat to be free.
    public const double HeartsSeconds = 1.5, BondUpWaitSeconds = 600;

    public static bool Valid(int level) => level is >= 1 and <= 5;

    public static string Name(int level, string language) => (Math.Clamp(level, 1, 5), language) switch
    {
        (1, "vi") => "Mới quen", (2, "vi") => "Quen mặt", (3, "vi") => "Thân", (4, "vi") => "Rất thân", (5, "vi") => "Tri kỷ",
        (1, _) => "New friends", (2, _) => "Familiar", (3, _) => "Good friends", (4, _) => "Best friends", _ => "Soulmates"
    };

    // What a milestone opens (null for the first: the behaviour the cat already has).
    public static string? Opens(int level, string language) => (level, language) switch
    {
        (2, "vi") => "Ngó chào khi sen quay lại", (3, "vi") => "Liếm chân, vươn vai sau khi chơi",
        (4, "vi") => "Ra đón khi sen quay lại", (5, "vi") => "Ngủ tựa vào đồ vật",
        (2, _) => "A glance hello when you come back", (3, _) => "Grooming and a stretch after play",
        (4, _) => "Coming to meet you when you come back", (5, _) => "Napping snug against its things",
        _ => null
    };
}
