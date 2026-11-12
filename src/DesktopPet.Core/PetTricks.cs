using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// Calling the cat and teaching it tricks (docs/LIVING-CORNER.md → M5). All C#: the lessons are kept in the
// preferences, Python takes no part. The user asks for both, like "Do now".
public static class TrickRules
{
    public const string Spin = "spin", Beg = "beg", HighFive = "highfive";
    public static readonly string[] All = { Spin, Beg, HighFive };
    public const int LessonsToLearn = 5, LessonsPerDay = 3;
    // A lesson: a clumsy try of about three seconds, then six seconds to pet the cat; 30 seconds between lessons.
    public const double TrySeconds = 3, PetSeconds = 6, LessonGapSeconds = 30, LabelSeconds = 1.5;
    // Calling: a glance, then along its floor toward the cursor for up to ten seconds, then sitting looking up.
    // Asleep it waits for the cat to wake by itself (30 seconds at most); on another monitor it only looks.
    public const double CallTurnSeconds = 0.5, CallWalkSeconds = 10, CallNear = 40, CallSitSeconds = 3, CallLookSeconds = 1.5, CallSleepWaitSeconds = 30;
    // A high five: the cursor this close to the raised paw gets a little spark (drawing only).
    public const double HighFiveReach = 60;

    public static PetAction ActionOf(string id) => id switch
    {
        Spin => PetAction.Spinning,
        Beg => PetAction.Begging,
        HighFive => PetAction.HighFive,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };

    public static string? IdOf(PetAction action) => action switch
    {
        PetAction.Spinning => Spin,
        PetAction.Begging => Beg,
        PetAction.HighFive => HighFive,
        _ => null
    };

    public static string Name(string id, string language) => (id, language) switch
    {
        (Spin, "vi") => "Xoay vòng", (Beg, "vi") => "Ngồi xin", (HighFive, "vi") => "Đập tay",
        (Spin, _) => "Spin around", (Beg, _) => "Sit up and beg", _ => "High five"
    };

    public static string Day(DateTime local) => local.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public static int Lessons(PetPreferences p, string id) => p.TrickLessons?.GetValueOrDefault(id) ?? 0;
    public static bool Learned(PetPreferences p, string id) => Lessons(p, id) >= LessonsToLearn;
    public static int Today(PetPreferences p, string id, DateTime local) => p.TrickDay == Day(local) ? p.TrickToday?.GetValueOrDefault(id) ?? 0 : 0;
    public static bool MayLearnToday(PetPreferences p, string id, DateTime local) => !Learned(p, id) && Today(p, id, local) < LessonsPerDay;

    // One lesson counted. True when this one taught the trick.
    public static bool Record(PetPreferences p, string id, DateTime local)
    {
        if (!MayLearnToday(p, id, local)) return false;
        string day = Day(local);
        if (p.TrickDay != day) { p.TrickDay = day; p.TrickToday = null; }
        (p.TrickToday ??= new())[id] = Today(p, id, local) + 1;
        int lessons = Lessons(p, id) + 1;
        (p.TrickLessons ??= new())[id] = lessons;
        return lessons >= LessonsToLearn;
    }

    // Unknown tricks dropped, counts clamped; nothing left is null.
    public static void Normalize(PetPreferences p)
    {
        static Dictionary<string, int>? Clean(Dictionary<string, int>? counts, int max)
        {
            var clean = counts?.Where(c => All.Contains(c.Key) && c.Value > 0).ToDictionary(c => c.Key, c => Math.Min(c.Value, max));
            return clean is { Count: > 0 } ? clean : null;
        }
        p.TrickLessons = Clean(p.TrickLessons, LessonsToLearn);
        p.TrickToday = Clean(p.TrickToday, LessonsPerDay);
        if (p.TrickDay is not { Length: 10 }) { p.TrickDay = null; p.TrickToday = null; }
    }
}
