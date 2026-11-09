using System;

namespace DesktopPet;

public enum FocusPhase { Off, Focusing, Resting }
public enum FocusEvent { None, Done, RestOver }

// The focus timer (docs/WINDOWS.md): a session the user starts, then a short rest. It runs on real
// time and reads no input: leaving the desk to read still counts. Pure state, so the rules are
// testable; the window decides when the chime and the line can be delivered.
public sealed class PetFocus
{
    public const int ShortMinutes = 25, LongMinutes = 50;
    // A longer gap between two frames means the computer slept: that time is not work.
    public const double MaxGap = 30;
    // How long the chime keeps trying (another sound playing) before it is given up.
    public const double ChimeGiveUp = 6;

    public FocusPhase Phase { get; private set; }
    public int Minutes { get; private set; }
    public double Remaining { get; private set; }
    public bool Focusing => Phase == FocusPhase.Focusing;
    public bool Running => Phase != FocusPhase.Off;

    // Waiting to be delivered: the newest news replaces older news that was not said yet.
    public FocusEvent LineWaiting { get; private set; }
    public bool ChimeWaiting { get; private set; }
    private double chimeTried;

    public static int RestMinutes(int minutes) => minutes >= LongMinutes ? 10 : 5;

    // A session cannot replace one in progress; it can cut a rest short.
    public bool Start(int minutes)
    {
        if (Focusing || minutes is not (ShortMinutes or LongMinutes)) return false;
        Phase = FocusPhase.Focusing;
        Minutes = minutes;
        Remaining = minutes * 60.0;
        LineWaiting = FocusEvent.None;
        ChimeWaiting = false;
        return true;
    }

    // No reward and no penalty.
    public void Stop()
    {
        Phase = FocusPhase.Off;
        Remaining = 0;
        LineWaiting = FocusEvent.None;
        ChimeWaiting = false;
    }

    public FocusEvent Step(double elapsed)
    {
        if (!Running || !double.IsFinite(elapsed) || elapsed <= 0 || elapsed > MaxGap) return FocusEvent.None;
        Remaining -= elapsed;
        if (Remaining > 0) return FocusEvent.None;
        var happened = Focusing ? FocusEvent.Done : FocusEvent.RestOver;
        if (Focusing) { Phase = FocusPhase.Resting; Remaining = RestMinutes(Minutes) * 60.0; }
        else { Phase = FocusPhase.Off; Remaining = 0; }
        LineWaiting = happened;
        ChimeWaiting = true;
        chimeTried = 0;
        return happened;
    }

    public void ChimeStep(bool rang, double elapsed)
    {
        chimeTried += elapsed;
        if (rang || chimeTried >= ChimeGiveUp) ChimeWaiting = false;
    }

    public void LineSaid() => LineWaiting = FocusEvent.None;

    public static bool IsFocusLine(string? id) => id != null && id.StartsWith("focus:", StringComparison.Ordinal);
    public static bool IsDoneLine(string? id) => id != null && id.StartsWith("focus:done:", StringComparison.Ordinal);

    private static readonly string[] DoneVietnamese = { "Xong một phiên rồi, giỏi quá sen! Nghỉ chút nào~", "Hết giờ rồi! Vươn vai với {name} nha~" };
    private static readonly string[] DoneEnglish = { "Session done, well done human! Time for a rest~", "Time's up! Stretch with {name}~" };
    private static readonly string[] DoneJapanese = { "一区切りついたね、えらいよご主人！少し休もう〜", "時間だよ！{name}と伸びしよう〜" };
    private static readonly string[] RestVietnamese = { "Hết giờ nghỉ rồi, sẵn sàng thì mình làm tiếp nha!" };
    private static readonly string[] RestEnglish = { "Rest is over, start again when you're ready!" };
    private static readonly string[] RestJapanese = { "休憩おわり、準備ができたらまた始めよう！" };

    internal static string[] Lines(FocusEvent kind, string language) => (kind, language) switch
    {
        (FocusEvent.Done, PetLanguage.Vietnamese) => DoneVietnamese,
        (FocusEvent.Done, PetLanguage.Japanese) => DoneJapanese,
        (FocusEvent.Done, _) => DoneEnglish,
        (_, PetLanguage.Vietnamese) => RestVietnamese,
        (_, PetLanguage.Japanese) => RestJapanese,
        _ => RestEnglish
    };

    public static (string Id, string Text) LocalLine(FocusEvent kind, string language, string name, Random random)
    {
        var lines = Lines(kind, language);
        int index = random.Next(lines.Length);
        string group = kind == FocusEvent.Done ? "done" : "rest";
        return ($"focus:{group}:{language}:{index}", lines[index].Replace("{name}", string.IsNullOrWhiteSpace(name) ? "Mochi" : name));
    }
}
