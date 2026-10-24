using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using static PetPrivate;

// Battle Mode's first-battle guide and its danger feedback in the real pet window: the guide
// opens once, holds the battle while open, is mouse-only and never takes focus; a slime holding
// its attack while the cat is carried is shown on the field and in the HUD hint. In-memory
// preferences only (the real preferences file is never written).
internal static class TutorialChecks
{
    private static MouseButtonEventArgs MouseEvent(RoutedEvent routedEvent) => new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = routedEvent };
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    private static Window? GuideWindow() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet tutorial" && w.IsVisible);

    public static async Task Run(PetWindow window, PetModel pet, Action<bool, string> check)
    {
        BattleSim Sim() => window.Battle!;
        void TickBattle(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattle", 1.0 / 60); }
        void SetContext(bool fullscreen) { SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, 0))); Call(window, "TickContext", 1.1); }
        var prefs = Field<PetPreferences>(window, "preferences");
        string language = prefs.Language;
        prefs.Language = "en";
        SetContext(false);
        window.Reset();
        WaveSpec Slimes(int count) => new() { Groups = new[] { new WaveGroup { Enemy = "slime", Count = count } } };
        var learn = new StageSpec { Id = "t_learn", NameVi = "Học", NameEn = "Learn", Waves = new[] { Slimes(2) } };
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 2 }, MaxAlive = 2, SpawnInterval = 0, SlimeMaxHp = 1000, CatDamage = 0, SlimeDamage = 0,
            Campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "t", NameVi = "T", NameEn = "T", Stages = new[] { learn } } } } };

        // Adventure is where the game is learned: the first Adventure stage setup opens the guide (a
        // free battle's does not); no battle exists behind it yet.
        prefs.BattleTutorialSeen = false;
        check(window.OpenBattlePrep() == null && !window.BattleTutorialOpen, "A free battle's setup does not open the guide by itself");
        window.CancelBattlePrep();
        check(window.OpenBattlePrep(stage: learn) == null && window.BattleTutorialOpen && window.BattleTutorialStep == 0 && !window.BattleActive,
            "The first Adventure stage setup opens the guide (no battle runs yet)");
        await Task.Delay(20);
        var guide = GuideWindow();
        long style = guide == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(guide).Handle, -20).ToInt64();
        check(guide is { ShowActivated: false, ShowInTaskbar: false } && (style & 0x08000000) != 0, "The guide never takes focus and has no taskbar button");
        TickBattle(2);
        check(!window.BattleActive && window.BattlePreparing, "Nothing runs behind the guide (no battle until Ready)");
        var cards = new List<string> { window.BattleTutorialText };
        for (int i = 1; i < PetWindow.BattleTutorialSteps; i++) { window.BattleTutorialNext(); cards.Add(window.BattleTutorialText); }
        check(cards[0].StartsWith("Pick a target") && cards[1].Contains("Push") && cards[2].Contains("red ring") &&
              cards[3].Contains("1.5 s") && cards[3].Contains("every 5 s") && cards[4].StartsWith("Desktop apps") && cards[4].Contains("virus") && cards[4].Contains("Malware") && cards.Distinct().Count() == 5,
            $"Five cards: targets, skills, rescue (the held attack), protection and cooldowns with the battle's numbers, the desktop apps and infected ones ({string.Join(" | ", cards.Select(c => c.Split('\n')[0]))})");
        window.BattleTutorialNext();
        check(!window.BattleTutorialOpen && prefs.BattleTutorialSeen && window.BattlePreparing && GuideWindow() == null,
            $"Done closes it and remembers it; the setup panel stays ({window.BattleTutorialOpen}, {prefs.BattleTutorialSeen}, {GuideWindow() == null})");
        window.ShowBattleTutorial();
        check(window.BattleTutorialOpen && window.BattlePrepReady() == null && !window.BattleTutorialOpen && !Sim().Paused,
            "Opened again from the setup panel, Ready closes it and the battle runs at once");
        TickBattle(0.3);
        check(Sim().Time > 0.2, "The battle runs normally after it");
        window.EndBattle();
        check(window.OpenBattlePrep() == null && !window.BattleTutorialOpen, "The next setup opens without the guide");
        window.BattlePrepReady();
        check(!window.BattleTutorialOpen && !Sim().Paused, "and the next battle starts without it");

        // Should it be open mid-battle, it pauses; Skip removes only its own pause.
        window.ShowBattleTutorial();
        check(window.BattleTutorialOpen && Sim().Paused && (window.BattlePauseReasons & PetWindow.BattlePause.Tutorial) != 0, "Opened again mid-battle it pauses the battle");
        window.CloseBattleTutorial(finished: true);
        check(!Sim().Paused, "Skip lets the battle go on");
        window.SetBattlePaused(true);
        window.ShowBattleTutorial();
        window.CloseBattleTutorial(finished: true);
        check(Sim().Paused && window.BattlePauseReasons == PetWindow.BattlePause.User, "Closing the guide keeps a pause the user made");
        window.SetBattlePaused(false);
        // A fullscreen app takes it away; the battle then waits for the user as usual.
        window.ShowBattleTutorial();
        SetContext(true); TickBattle(1.0 / 60);
        check(!window.BattleTutorialOpen && Sim().Paused && (window.BattlePauseReasons & PetWindow.BattlePause.Tutorial) == 0, "A fullscreen app closes the guide (the battle stays paused for the user)");
        SetContext(false); TickBattle(1.0 / 60);
        window.SetBattlePaused(false);
        // Without a battle, and in Vietnamese.
        window.EndBattle();
        prefs.Language = "vi";
        window.ShowBattleTutorial();
        check(window.BattleTutorialOpen && !window.BattleActive && window.BattleTutorialText.StartsWith("Chọn mục tiêu"), "It opens without a battle too, in the pet's language");
        window.Reset();
        check(!window.BattleTutorialOpen && GuideWindow() == null, "Bringing the pet back closes it");
        prefs.Language = "en";

        // Danger feedback: a slime holding its attack while the cat is carried.
        window.StartBattle();
        TickBattle(0.7);
        var slime = Sim().Slimes.First(s => s.Targetable);
        foreach (var other in Sim().Slimes) if (other != slime) other.X = Sim().Right - 40;
        slime.X = pet.X + 56 + 40;
        for (int i = 0; i < 120 && slime.State != SlimeState.Windup; i++) TickBattle(1.0 / 60);
        var hud = Field<BattleHud>(window, "battleHud");
        var view = Field<BattleFieldView>(window, "battleView");
        byte RingAlpha()
        {
            view.InvalidateVisual(); view.UpdateLayout();
            int vw = Math.Max(1, (int)view.ActualWidth), vh = Math.Max(1, (int)view.ActualHeight);
            var bitmap = new RenderTargetBitmap(vw, vh, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
            var px = new byte[vw * vh * 4]; bitmap.CopyPixels(px, vw * 4, 0);
            // The far end of the floor ring, on the side away from the cat.
            int x = (int)Math.Clamp(slime.X - view.OriginX + (slime.Range + 12), 0, vw - 1), y = (int)Math.Clamp(vh - 4, 0, vh - 1);
            byte best = 0;
            for (int dx = -2; dx <= 2; dx++) for (int dy = -3; dy <= 3; dy++)
                best = Math.Max(best, px[(Math.Clamp(y + dy, 0, vh - 1) * vw + Math.Clamp(x + dx, 0, vw - 1)) * 4 + 3]);
            return best;
        }
        byte ringBefore = RingAlpha();
        window.Activate(); await Task.Delay(20);
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        SetField(window, "pressPosition", new Point(-100000, -100000));
        Call(window, "CheckDragThreshold");
        TickBattle(0.1);
        Call(window, "UpdateBattleHud");
        byte ringHeld = RingAlpha();
        check(slime.State == SlimeState.Windup && BattleHud.HeldAttacks(Sim()) == 1 && hud.HintText.Contains("about to hit") && hud.HintText.Contains("red ring"),
            $"Carrying the cat: the HUD says a slime is about to hit and where to put the cat ({hud.HintText})");
        check(ringBefore == 0 && ringHeld > 0, $"The field shows the coming attack's reach as a ring on the floor only while the cat is carried ({ringBefore} → {ringHeld})");
        TickBattle(0.3); // A real rescue (at least rescueGraceMinLiftSeconds) earns the landing protection.
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        pet.Grounded = true; pet.VX = pet.VY = 0; pet.Action = PetAction.Idle; pet.Y = Field<Area>(window, "bounds").Bottom - 112;
        TickBattle(1.0 / 60);
        Call(window, "UpdateBattleHud");
        check(Sim().Grace > 0 && hud.HintText.StartsWith("Protected after landing"), $"After a rescue the HUD says the cat is protected, and for how long ({hud.HintText})");

        window.EndBattle();

        // Lessons on the spot (2026-10-24), only in Adventure: the first slime winding up next to the
        // cat in the stage that teaches it rings the cat with "Lift the cat!" (the battle keeps its
        // speed); lifting it ends the lesson for good.
        Window? CoachWindow() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet coach" && w.IsVisible);
        prefs.BattleLessonsSeen = new List<string> { PetWindow.LessonTarget, PetWindow.LessonStun };
        window.BattleCoachEnabled = true;
        window.Reset();
        var firstSteps = new StageSpec { Id = PetWindow.StageFirstSteps, NameVi = "Bước đầu", NameEn = "First steps", Waves = new[] { Slimes(1) } };
        // The chapter's five stages, so each lesson waits for the stage that teaches it.
        var chapter = new[] { PetWindow.StageBreakThrough, PetWindow.StageHoldOn, PetWindow.StageCutSupport, PetWindow.StageSlimeKing }
            .Select(id => new StageSpec { Id = id, NameVi = id, NameEn = id, Waves = new[] { Slimes(1) } }).Prepend(firstSteps).ToArray();
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, MaxAlive = 1, SpawnInterval = 0, SlimeMaxHp = 1000, CatDamage = 0, SlimeDamage = 0,
            Campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "c1", NameVi = "C", NameEn = "C", Stages = chapter } } } };
        window.AdventureProgress.Seen.Add(PetWindow.StageFirstSteps); // Its tip was read.
        void ToGround() { pet.Grounded = true; pet.VX = pet.VY = 0; pet.Action = PetAction.Idle; pet.Y = Field<Area>(window, "bounds").Bottom - 112; }
        void NextToCat() { foreach (var s in Sim().Slimes) s.X = pet.X + Field<PetSettings>(window, "settings").Width / 2 + 60; }
        // Keeps every slime next to the cat until a lesson starts (at most `seconds` of battle).
        void RunToLesson(double seconds)
        {
            for (int i = 0; i < seconds * 60 && window.BattleCoachLesson == null; i++) { NextToCat(); Call(window, "TickBattle", 1.0 / 60); }
        }
        check(window.StartBattle() == null, "(setup) a free battle with lessons on");
        ToGround();
        RunToLesson(8);
        check(window.BattleCoachLesson == null && CoachWindow() == null, $"A free battle has no lessons ({window.BattleCoachLesson})");
        window.EndBattle();
        ToGround();
        check(window.StartBattle(stage: firstSteps) == null, "(setup) the Adventure stage that teaches lifting");
        ToGround();
        RunToLesson(PetWindow.CoachFirst - 0.5);
        check(window.BattleCoachLesson == null, $"No lesson in the first {PetWindow.CoachFirst:0} s of a stage ({window.BattleCoachLesson})");
        RunToLesson(8);
        var coach = CoachWindow();
        long coachStyle = coach == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(coach).Handle, -20).ToInt64();
        check(window.BattleCoachLesson == PetWindow.LessonLift && coach != null && (coachStyle & 0x20) != 0 && (coachStyle & 0x08000000) != 0,
            $"A slime winding up by the cat starts the lift lesson: a click-through mark shows ({window.BattleCoachLesson})");
        // PET_RENDER_REVIEW: a picture of the lesson's mark (ring, hand, words).
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review && coach?.Content is FrameworkElement mark)
        {
            mark.UpdateLayout();
            var shot = new RenderTargetBitmap((int)CoachMark.ViewWidth, (int)CoachMark.ViewHeight, 96, 96, PixelFormats.Pbgra32);
            var back = new DrawingVisual();
            using (var dc = back.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x6A, 0x8C, 0xAF)), null, new Rect(0, 0, CoachMark.ViewWidth, CoachMark.ViewHeight));
            shot.Render(back);
            shot.Render(mark);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(shot));
            System.IO.Directory.CreateDirectory(review);
            using var stream = System.IO.File.Create(System.IO.Path.Combine(review, "battle-coach-lift.png")); encoder.Save(stream);
        }
        double before = Sim().Time;
        TickBattle(0.2);
        check(Math.Abs(Sim().Time - before - 0.2) < 0.03, $"During the lesson the battle runs at its own speed ({Sim().Time - before:0.00} s in 0.2 s)");
        window.Activate(); await Task.Delay(20);
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        SetField(window, "pressPosition", new Point(-100000, -100000));
        Call(window, "CheckDragThreshold");
        TickBattle(0.05);
        check(window.BattleCoachLesson == null && CoachWindow() == null && prefs.BattleLessonsSeen.Contains(PetWindow.LessonLift),
            "Lifting the cat ends the lesson and it is remembered");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        window.EndBattle();
        ToGround();
        string? again0 = window.StartBattle(stage: firstSteps);
        check(again0 == null, $"(setup) another battle ({again0})");
        ToGround();
        bool again = false;
        for (int i = 0; i < 600; i++) { NextToCat(); Call(window, "TickBattle", 1.0 / 60); again |= window.BattleCoachLesson == PetWindow.LessonLift; }
        check(!again, "A lesson already learned does not come back");
        window.EndBattle();

        // Not done: the lesson comes again at a later moment (CoachGap s apart), CoachTries times, then rests.
        prefs.BattleLessonsSeen = new List<string> { PetWindow.LessonTarget, PetWindow.LessonStun };
        ToGround();
        check(window.StartBattle(stage: firstSteps) == null, "(setup) a stage where the cat is never lifted");
        ToGround();
        var shown = new List<double>();
        for (int i = 0; i < 60 * 60 && shown.Count <= PetWindow.CoachTries; i++)
        {
            string? was = window.BattleCoachLesson;
            NextToCat(); Call(window, "TickBattle", 1.0 / 60); ToGround();
            if (was == null && window.BattleCoachLesson == PetWindow.LessonLift) shown.Add(Sim().Time);
        }
        bool spaced = shown.Zip(shown.Skip(1), (a, b) => b - a).All(gap => gap >= PetWindow.CoachGap);
        check(shown.Count == PetWindow.CoachTries && spaced && prefs.BattleLessonsSeen.Contains(PetWindow.LessonLift),
            $"Not lifting: the lesson comes {PetWindow.CoachTries} times at least {PetWindow.CoachGap:0} s apart, then rests ({string.Join(", ", shown.Select(t => t.ToString("0.0")))})");
        window.EndBattle();
        // A chapter stage's tip says what it teaches (the boss stage's lessons are not in the first stage's).
        window.ShowStageTip(firstSteps);
        string tip = window.BattleTutorialText;
        check(tip.StartsWith("Tip · ") && tip.Contains("In this stage: ") && tip.Contains("lift the cat") && !tip.Contains("slam"),
            $"The first stage's tip lists its lessons ({tip.Replace('\n', ' ')})");
        window.CloseBattleTutorial();
        window.ShowBattleTutorial();
        check(prefs.BattleLessonsSeen.Count == 0, "How to play brings the lessons back");
        window.CloseBattleTutorial(finished: true);
        window.BattleCoachEnabled = false;
        prefs.BattleLessonsSeen = null;
        window.AdventureProgress.Seen.Remove(PetWindow.StageFirstSteps);

        prefs.Language = language;
        prefs.BattleTutorialSeen = true;
        window.Reset();
        window.BattleTuning = new BattleSettings();
    }
}
