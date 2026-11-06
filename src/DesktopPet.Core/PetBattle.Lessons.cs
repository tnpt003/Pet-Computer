using System;
using System.Linq;

namespace DesktopPet;

public sealed partial class BattleSim
{
    private bool learningGuide, lessonSent, lessonFinished;
    public bool LearningBusy => learningGuide && !lessonFinished && Stage?.Lesson != null;
    public bool LessonCompleted => lessonFinished;
    public void SetLearningGuide(bool enabled) => learningGuide = enabled && !Over;

    private void StepLearningApp(AppsSpec rules)
    {
        if (!LearningBusy || Stage?.Lesson is not string kind) return;
        bool active = kind switch { "screenshot" => desktop.Any(a => a.Kind == BattleAppKind.Screenshot && a.Charge >= 1),
            "captcha" => Captcha != null, "battery" => BatteryLow, "cursor" => CursorFrozen, _ => false };
        if (lessonSent) { if (!active) lessonFinished = true; return; }
        if (Time < 4 || !slimes.Any(s => s.Targetable && s.State != SlimeState.Spawning) || slimes.Any(s => s.Targetable && s.State == SlimeState.Windup)) return;
        switch (kind)
        {
            case "screenshot":
                var icon = desktop.FirstOrDefault(a => a.Kind == BattleAppKind.Screenshot);
                // Respect an explicitly chosen app loadout; never add a sixth icon for a lesson.
                if (icon == null) { lessonFinished = true; return; }
                if (icon.Infected) return;
                icon.Charge = 1; lessonSent = true; break;
            case "captcha": OpenCaptcha(rules); lessonSent = Captcha != null; break;
            case "battery": DrainBattery(); lessonSent = BatteryLow; break;
            case "cursor": FreezeCursor(rules); lessonSent = CursorFrozen; break;
        }
        if (lessonSent && kind != "screenshot") NoteApp(kind);
    }
}
