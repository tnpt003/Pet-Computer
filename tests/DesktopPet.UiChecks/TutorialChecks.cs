using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using static PetPrivate;

// Battle Mode's danger feedback and the Adventure Guide in the real pet window: a slime holding its
// attack while the cat is carried is shown on the field and in the HUD hint; the guide runs only in
// a chapter 1 Adventure stage, holds the battle still (the cat can still be lifted) with a dark,
// click-through shade and a line that never takes focus; on by default (won stages too), and an
// unticked setup chip is remembered. In-memory preferences only (the real preferences file is never written).
internal static class TutorialChecks
{
    private static MouseButtonEventArgs MouseEvent(RoutedEvent routedEvent) => new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = routedEvent };
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    private static Window? Visible(string title) => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == title && w.IsVisible);

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
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 2 }, MaxAlive = 2, SpawnInterval = 0, SlimeMaxHp = 1000, CatDamage = 0, SlimeDamage = 0 };

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

        // The Adventure Guide (2026-10-24). Chapter 1's five stages with one slime each that never dies.
        WaveSpec Slimes(int count) => new() { Groups = new[] { new WaveGroup { Enemy = "slime", Count = count } } };
        var ids = new[] { PetWindow.StageFirstSteps, PetWindow.StageBreakThrough, PetWindow.StageHoldOn, PetWindow.StageCutSupport, PetWindow.StageSlimeKing };
        // Stage 3 sends virus.exe 1 s in (its plan), no random malware.
        var chapter = ids.Select(id => new StageSpec { Id = id, NameVi = id, NameEn = id, Waves = new[] { Slimes(1) },
            MalwarePlan = id == PetWindow.StageHoldOn ? new[] { new MalwareCue { At = 1, Kind = "loading" }, new MalwareCue { At = 3, Kind = "virus" } } : null }).ToArray();
        var outside = new StageSpec { Id = "t_other", NameVi = "Khác", NameEn = "Other", Waves = new[] { Slimes(1) } };
        // 1.5 M05: stages that teach a skill (their unlocksSkill), in a chapter with the skill choice.
        var teachLaser = new StageSpec { Id = "t_laser", NameVi = "Laser", NameEn = "Laser", UnlocksSkill = "laser", Waves = new[] { Slimes(2) } };
        var teachFan = new StageSpec { Id = "t_fan", NameVi = "Quạt", NameEn = "Fan", UnlocksSkill = "fan", Waves = new[] { Slimes(2) } };
        var teachGrabber = new StageSpec { Id = "t_grabber", NameVi = "Kẹp", NameEn = "Grabber", UnlocksSkill = "grabber", Waves = new[] { Slimes(2) } };
        var teachScissors = new StageSpec { Id = "t_scissors", NameVi = "Kéo", NameEn = "Scissors", UnlocksSkill = "scissors", Waves = new[] { Slimes(2) } };
        var teachSpray = new StageSpec { Id = "t_spray", NameVi = "Xịt", NameEn = "Spray", UnlocksSkill = "spray", Waves = new[] { Slimes(2) } };
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, MaxAlive = 1, SpawnInterval = 0, SlimeMaxHp = 1000, CatDamage = 0, SlimeDamage = 0,
            Apps = new AppsSpec { InfectChance = 0, MalwareMinThreat = 99, VirusExeWeight = 1, VirusExeEvery = 30 },
            Campaign = new CampaignSpec { Chapters = new[] { new ChapterSpec { Id = "c1", NameVi = "C", NameEn = "C", Stages = chapter.Append(outside).ToArray() },
                new ChapterSpec { Id = "c2", NameVi = "C2", NameEn = "C2", SkillChoice = true, Stages = new[] { teachLaser, teachFan, teachGrabber, teachScissors, teachSpray } } } } };
        window.BattleGuideEnabled = true;
        void ToGround() { pet.Grounded = true; pet.VX = pet.VY = 0; pet.Action = PetAction.Idle; pet.Y = Field<Area>(window, "bounds").Bottom - 112; }
        void NextToCat() { foreach (var s in Sim().Slimes) s.X = pet.X + Field<PetSettings>(window, "settings").Width / 2 + 60; }
        // Keeps every slime next to the cat until a guide step shows (at most `seconds` of frames).
        void RunToStep(double seconds)
        {
            for (int i = 0; i < seconds * 60 && window.BattleGuideStep == null; i++) { NextToCat(); ToGround(); Call(window, "TickBattle", 1.0 / 60); }
        }
        async Task Lift()
        {
            window.Activate(); await Task.Delay(10);
            window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
            SetField(window, "pressPosition", new Point(-100000, -100000));
            Call(window, "CheckDragThreshold");
            TickBattle(0.05);
        }
        int GuideTimesShown(string id) => window.BattleGuideShown(id);
        void Drop() { window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent)); ToGround(); TickBattle(1.0 / 60); }

        // Not in a free battle, nor in a stage it does not teach.
        ToGround();
        check(window.StartBattle() == null, "(setup) a free battle with the guide on");
        RunToStep(4);
        check(window.BattleGuideStep == null && !window.BattleGuideOn && Visible("Pet guide shade") == null, $"A free battle has no guide ({window.BattleGuideStep})");
        window.EndBattle();
        ToGround();
        window.StartBattle(stage: outside);
        RunToStep(4);
        check(!window.BattleGuideOn && window.BattleGuideStep == null, "A stage outside chapter 1's lessons has no guide");
        window.EndBattle();

        // The setup panel: the Guide chip for a chapter 1 stage, on until the stage is won.
        ToGround();
        check(window.OpenBattlePrep(stage: chapter[0]) == null && window.BattlePrepHasGuide && window.BattlePrepGuide && window.BattlePrepText.Contains("Guide in the battle"),
            $"The setup panel's Guide chip starts ticked ({window.BattlePrepGuide})");
        check(window.BattlePrepChooseGuide(false) && !window.BattlePrepGuide && window.BattlePrepChooseGuide(true) && window.BattlePrepGuide, "The chip turns the guide off and on");
        check(!window.BattlePrepText.Contains("How to play"), "The old How to play guide is gone from the setup panel");
        window.CancelBattlePrep(); window.CancelAdventureMap(); window.CancelBattleHub();

        // 1 · First steps: a slime winding up next to the cat holds the battle still; lifting the cat ends it.
        ToGround();
        check(window.StartBattle(stage: chapter[0]) == null, "(setup) stage 1 with the guide");
        RunToStep(8);
        var shade = Visible("Pet guide shade");
        var bubble = Visible("Pet guide");
        long shadeStyle = shade == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(shade).Handle, -20).ToInt64();
        long bubbleStyle = bubble == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(bubble).Handle, -20).ToInt64();
        check(window.BattleGuideStep == PetWindow.GuideLift && window.BattleGuideOn && shade != null && bubble is { ShowActivated: false, ShowInTaskbar: false }
              && (shadeStyle & 0x20) != 0 && (shadeStyle & 0x08000000) != 0 && (bubbleStyle & 0x20) == 0 && (bubbleStyle & 0x08000000) != 0,
            $"A slime winding up by the cat: the lift step shows a click-through shade and a line that never takes focus ({window.BattleGuideStep})");
        check(window.BattleGuideText.Contains("red !") && window.BattleGuideText.Contains("Drag the cat up"), $"The line says what to do ({window.BattleGuideText})");
        // Centred on what it points at: the drawn cat (its body's centre, not its window's) and the slime.
        double scale = Field<double>(window, "battleScale");
        Point OnScreen(UIElement element, Point at) { var p = element.PointToScreen(at); return new Point(p.X / scale, p.Y / scale); }
        var spots = window.BattleGuideSpots;
        // The cat's frame sits PickupAnimation.Padding in from its window's corner; pet.X/Y is the frame's corner.
        double pad = PickupAnimation.Padding;
        var catCentre = OnScreen(window, new Point(pad + PetSettings.CenterX, 0));
        var frameCorner = OnScreen(window, new Point(pad, pad));
        var fieldView = Field<BattleFieldView>(window, "battleView");
        var hitter = Sim().Slimes.First(s => s.State == SlimeState.Windup);
        var slimeCentre = OnScreen(fieldView, new Point(hitter.X - fieldView.OriginX, 0));
        bool catLit = spots.Count == 2 && Math.Abs(spots[0].X + spots[0].Width / 2 - catCentre.X) < 1 && spots[0].Top <= OnScreen(window, new Point(0, pad + PetSettings.HeadTop)).Y
            && spots[0].Bottom >= OnScreen(window, new Point(0, pad + 104)).Y
            // And where the pet's own position says the cat is (no window padding in between).
            && Math.Abs(spots[0].X + spots[0].Width / 2 - (pet.X + PetSettings.CenterX)) < 1.5 && Math.Abs(frameCorner.X - pet.X) < 1.5;
        bool slimeLit = spots.Count == 2 && Math.Abs(spots[1].X + spots[1].Width / 2 - slimeCentre.X) < 1 && spots[1].Height >= hitter.Height * 1.28;
        check(catLit && slimeLit, $"The lit spots are centred on the cat's body and on the slime, the slime's at its tallest ({string.Join(" ", spots.Select(r => $"{r.X:0.#},{r.Y:0.#} {r.Width:0.#}×{r.Height:0.#}"))}; cat x {catCentre.X:0.#}, slime x {slimeCentre.X:0.#})");
        double frozenAt = Sim().Time;
        var windup = Sim().Slimes.First(s => s.State == SlimeState.Windup);
        double windupLeft = windup.StateTime;
        TickBattle(0.5);
        check(Sim().Time == frozenAt && windup.State == SlimeState.Windup && windup.StateTime == windupLeft && pet.MoveScale == 0 && !Sim().Paused,
            $"The battle stands still while the step shows (time {Sim().Time - frozenAt:0.00} s in 0.5 s), it is not a pause");
        // PET_RENDER_REVIEW: a picture of the shade with a lit spot and the line.
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review && bubble?.Content is FrameworkElement line)
        {
            var sample = new GuideShade { Holes = new List<Rect> { new(160, 70, 120, 110) }, Width = 520, Height = 300 };
            sample.Measure(new Size(520, 300)); sample.Arrange(new Rect(0, 0, 520, 300));
            line.UpdateLayout();
            var shot = new RenderTargetBitmap(520, 300 + (int)Math.Ceiling(line.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            var back = new DrawingVisual();
            using (var dc = back.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x6A, 0x8C, 0xAF)), null, new Rect(0, 0, 520, 300 + line.ActualHeight));
            shot.Render(back); shot.Render(sample);
            var moved = new DrawingVisual();
            using (var dc = moved.RenderOpen()) dc.DrawRectangle(new VisualBrush(line), null, new Rect(0, 300, line.ActualWidth, line.ActualHeight));
            shot.Render(moved);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(shot));
            System.IO.Directory.CreateDirectory(review);
            using var stream = System.IO.File.Create(System.IO.Path.Combine(review, "adventure-guide-lift.png")); encoder.Save(stream);
        }
        await Lift();
        check(window.BattleGuideStep == null && Visible("Pet guide shade") == null && Sim().Time > frozenAt, "Lifting the cat ends the step and the battle runs again");
        Drop();
        // Five lifts in all, then no more.
        for (int round = 0; round < 12 && (window.BattleGuideShown(PetWindow.GuideLift) < PetWindow.GuideLifts || window.BattleGuideStep != null); round++)
        {
            RunToStep(10);
            if (window.BattleGuideStep == PetWindow.GuideLift) { await Lift(); Drop(); }
        }
        RunToStep(10);
        check(window.BattleGuideShown(PetWindow.GuideLift) == PetWindow.GuideLifts && window.BattleGuideStep == null,
            $"The lift step comes {PetWindow.GuideLifts} times in one run, then the cat is on its own ({window.BattleGuideShown(PetWindow.GuideLift)}, {window.BattleGuideStep})");
        window.EndBattle();

        // Skip the guide: the rest of this run has none; Pause hides a step and Resume brings it back.
        ToGround();
        window.StartBattle(stage: chapter[0]);
        RunToStep(8);
        window.SetBattlePaused(true);
        TickBattle(1.0 / 60);
        check(window.BattleGuideStep == PetWindow.GuideLift && !window.BattleGuideVisible, "Pause hides the step (it waits)");
        window.SetBattlePaused(false);
        TickBattle(1.0 / 60);
        check(window.BattleGuideVisible, "Resume shows it again");
        window.SkipBattleGuide();
        double before = Sim().Time;
        RunToStep(6);
        check(!window.BattleGuideOn && window.BattleGuideStep == null && Sim().Time > before + 1, "Skip the guide: the battle goes on without it");
        window.EndBattle();

        // 2 · Break through: the HUD tour, one point at a time, each with Got it.
        ToGround();
        window.StartBattle(stage: chapter[1]);
        var tour = new List<string>();
        string pushLine = "";
        var litByStep = new Dictionary<string, List<Rect>>();
        for (int i = 0; i < 40; i++)
        {
            RunToStep(2);
            if (window.BattleGuideStep is not string step) break;
            if (tour.LastOrDefault() == step) break;
            tour.Add(step);
            if (step == PetWindow.GuidePush) pushLine = window.BattleGuideText;
            if (step is PetWindow.GuideHp or PetWindow.GuideSkills or PetWindow.GuidePush or PetWindow.GuideBag) litByStep[step] = window.BattleGuideSpots.ToList();
            double held = Sim().Time;
            TickBattle(0.2);
            if (Sim().Time != held) { tour.Add("(ran)"); break; }
            window.BattleGuideOk();
            TickBattle(1.0 / 60);
        }
        var expected = new List<string> { PetWindow.GuideHp, PetWindow.GuideWaves, PetWindow.GuideSkills, PetWindow.GuidePush, PetWindow.GuideHeal, PetWindow.GuideCheer,
            PetWindow.GuideBomb, PetWindow.GuideShield, PetWindow.GuideBag };
        if (Sim().Desktop.Count > 0) expected.Add(PetWindow.GuideApps);
        check(tour.SequenceEqual(expected), $"Stage 2 starts with the HUD tour, each point held until Got it ({string.Join(", ", tour)})");
        check(pushLine.StartsWith("Push: ") && pushLine.Contains("5 s cooldown"), $"A skill's line is its name and the data's description ({pushLine})");
        // Each HUD point is lit exactly: its own bounds (wherever the HUD stands) with the same padding all round.
        var tourHud = Field<BattleHud>(window, "battleHud");
        double tourScale = Field<double>(window, "battleScale");
        Rect Bounds(FrameworkElement e)
        {
            var a = e.PointToScreen(new Point(0, 0)); var b = e.PointToScreen(new Point(e.ActualWidth, e.ActualHeight));
            return new Rect(a.X / tourScale, a.Y / tourScale, (b.X - a.X) / tourScale, (b.Y - a.Y) / tourScale);
        }
        bool Snug(string step, Rect inner) => litByStep.TryGetValue(step, out var lit) && lit.Count == 1
            && new[] { inner.Left - lit[0].Left, lit[0].Right - inner.Right, inner.Top - lit[0].Top, lit[0].Bottom - inner.Bottom }.All(gap => Math.Abs(gap - PetWindow.GuidePad) < 0.6);
        var slotsUnion = tourHud.SkillOrder.Select(k => Bounds(tourHud.Slot(k)!)).Aggregate(Rect.Union);
        check(Snug(PetWindow.GuideHp, Bounds(tourHud.HpBlock)) && Snug(PetWindow.GuideSkills, slotsUnion) && Snug(PetWindow.GuidePush, Bounds(tourHud.Slot(BattleSkill.Push)!))
              && Snug(PetWindow.GuideBag, Bounds(tourHud.BagButton)),
            $"HP, the skill bar (its five slots), a skill and the bag are each lit with {PetWindow.GuidePad} DIPs all round ({string.Join("; ", litByStep.Select(p => $"{p.Key} {string.Join(" ", p.Value.Select(r => $"{r.X:0.#},{r.Y:0.#} {r.Width:0.#}×{r.Height:0.#}"))}"))} | bar {slotsUnion.X:0.#},{slotsUnion.Y:0.#} {slotsUnion.Width:0.#}×{slotsUnion.Height:0.#})");
        window.EndBattle();

        // 3 · Malware is shown once it has finished appearing (the battle stands still during the step).
        ToGround();
        window.StartBattle(stage: chapter[2]);
        for (int i = 0; i < 60 * 6 && window.BattleGuideStep != PetWindow.GuideLoading; i++) { NextToCat(); ToGround(); Call(window, "TickBattle", 1.0 / 60); }
        var ring = Sim().Hazards.FirstOrDefault(h => h.Kind == HazardKind.Loading);
        var ringSpots = window.BattleGuideSpots;
        double ringRadius = Sim().AppRules?.LoadingRadius ?? 60;
        check(window.BattleGuideStep == PetWindow.GuideLoading && ring != null && ring.Age >= 0.3 && ringSpots.Count == 3 && ringSpots[1].Width >= ringRadius * 2,
            $"The loading ring step waits for the ring to grow in, and lights its spinner, its zone on the floor and the cat ({ring?.Age:0.00} s, {ringSpots.Count} spots, zone {(ringSpots.Count > 1 ? ringSpots[1].Width : 0):0} DIPs)");
        window.BattleGuideOk();
        TickBattle(1.0 / 60);
        for (int i = 0; i < 60 * 8 && window.BattleGuideStep != PetWindow.GuideVirus; i++) { NextToCat(); ToGround(); Call(window, "TickBattle", 1.0 / 60); }
        // The battle stands still from the step's start, so the file's age now is its age then.
        var file = Sim().VirusFiles.FirstOrDefault();
        check(window.BattleGuideStep == PetWindow.GuideVirus && file != null && Sim().Time - file.BornAt >= 0.35,
            $"The virus.exe step waits until the file has popped in fully ({(file == null ? "no file" : $"{Sim().Time - file.BornAt:0.00} s after it appeared")})");
        window.EndBattle();

        // On by default, a won stage too; unticked on Ready, the choice is kept; ticked again, it teaches.
        window.AdventureProgress.Completed.Add(PetWindow.StageFirstSteps);
        ToGround();
        window.StartBattle(stage: chapter[0]);
        RunToStep(8);
        check(window.BattleGuideOn && window.BattleGuideStep == PetWindow.GuideLift, "A won stage still has the guide by default");
        await Lift(); Drop();
        window.EndBattle();
        check(window.OpenBattlePrep(stage: chapter[0]) == null && window.BattlePrepGuide, "Its setup chip starts ticked");
        window.BattlePrepChooseGuide(false);
        ToGround();
        window.BattlePrepReady();
        ToGround();
        RunToStep(6);
        check(!window.BattleGuideOn && window.BattleGuideStep == null && !prefs.AdventureGuide, "Unticked: no guide, and the choice is kept");
        window.EndBattle();
        check(window.OpenBattlePrep(stage: chapter[1]) == null && !window.BattlePrepGuide, "The next setup remembers it unticked");
        window.BattlePrepChooseGuide(true);
        ToGround();
        window.BattlePrepReady();
        check(prefs.AdventureGuide, "Ticked again on Ready: kept on");
        window.EndBattle();
        window.AdventureProgress.Completed.Remove(PetWindow.StageFirstSteps);

        // 1.5 M05: the stage that teaches a skill shows a do-step for it while the battle carries it
        // (here both stages are won, so the skills are open and chosen; lending them comes with chapter 2).
        window.AdventureProgress.Completed.Add(teachLaser.Id);
        window.AdventureProgress.Completed.Add(teachFan.Id);
        prefs.BattleLoadout = new() { "push", "heal", "cheer", "laser", "fan" };
        prefs.AdventureGuide = true;
        ToGround();
        check(window.StartBattle(stage: teachLaser) == null && Sim().Carries(BattleSkill.Laser), "(setup) the Laser's stage, the Laser carried");
        RunToStep(4);
        check(window.BattleGuideStep == PetWindow.GuideLaser && window.BattleGuideFreezes && window.BattleGuideSpots.Count == 1,
            $"The Laser's stage teaches it: the slot is lit ({window.BattleGuideStep})");
        window.UseBattleSkill(BattleSkill.Laser);
        TickBattle(1.0 / 60);
        check(window.BattleGuideStep == PetWindow.GuideLaser && Sim().Armed == BattleSkill.Laser && window.BattleGuideText.Contains("click a spot") && window.BattleGuideSpots.Count == 1,
            $"Chosen, the step points at the floor ({window.BattleGuideText})");
        check(window.BattleClickSpot(Sim().CatX + 150) && Sim().LaserX != null, "A click on the floor puts the dot down (the battle frozen, not paused)");
        TickBattle(1.0 / 60);
        check(window.BattleGuideStep == null && GuideTimesShown(PetWindow.GuideLaser) == 1, "Using it ends the step");
        window.EndBattle();
        ToGround();
        window.StartBattle(stage: teachFan);
        RunToStep(4);
        check(window.BattleGuideStep == PetWindow.GuideFan, $"The Fan's stage teaches the Fan ({window.BattleGuideStep})");
        window.UseBattleSkill(BattleSkill.Fan);
        TickBattle(1.0 / 60);
        check(window.BattlePressFan(Sim().CatX + 120) && Sim().FanBlowing, "Held on the floor, the Fan blows");
        TickBattle(1.0 / 60);
        check(window.BattleGuideStep == null && Sim().FanBlowing, "Blowing ends the step; the fan goes on while held");
        window.BattleReleaseFan();
        check(!Sim().FanBlowing && Sim().Armed == BattleSkill.Fan, "Let go, it stops and stays chosen");
        window.EndBattle();
        window.AdventureProgress.Completed.Remove(teachLaser.Id);
        window.AdventureProgress.Completed.Remove(teachFan.Id);

        // 1.5 M06: the same for the Grabber, the Scissors and the Spray.
        foreach (var stage in new[] { teachGrabber, teachScissors, teachSpray }) window.AdventureProgress.Completed.Add(stage.Id);
        prefs.BattleLoadout = new() { "push", "heal", "grabber", "scissors", "spray" };
        ToGround();
        check(window.StartBattle(stage: teachGrabber) == null && Sim().Carries(BattleSkill.Grabber), "(setup) the Grabber's stage, the Grabber carried");
        RunToStep(4);
        check(window.BattleGuideStep == PetWindow.GuideGrabber && window.BattleGuideSpots.Count == 1, $"The Grabber's stage teaches it ({window.BattleGuideStep})");
        window.UseBattleSkill(BattleSkill.Grabber);
        TickBattle(1.0 / 60);
        check(Sim().Armed == BattleSkill.Grabber && window.BattleGuideText.Contains("press a slime"), $"Chosen, the step says to press a slime ({window.BattleGuideText})");
        var grabbed = Sim().Slimes.First(s => s.Targetable);
        check(window.BattleGrab(grabbed.Id, grabbed.X, 10) && Sim().Grabbing, "A press on a slime lifts it (the battle frozen, not paused)");
        TickBattle(1.0 / 60);
        check(window.BattleGuideStep == null && GuideTimesShown(PetWindow.GuideGrabber) == 1, "Lifting one ends the step");
        window.BattleReleaseGrab();
        check(!Sim().Grabbing && Sim().Cooldown(BattleSkill.Grabber) > 0, "Let go, the Grabber cools down");
        window.EndBattle();
        ToGround();
        window.StartBattle(stage: teachScissors);
        RunToStep(4);
        check(window.BattleGuideStep == PetWindow.GuideScissors, $"The Scissors' stage teaches them ({window.BattleGuideStep})");
        window.UseBattleSkill(BattleSkill.Scissors);
        TickBattle(1.0 / 60);
        double cx = Sim().CatX;
        check(!window.BattleSnip(cx + 100, 20, cx + 110, 20) && Sim().Armed == BattleSkill.Scissors, "A stroke too short does nothing");
        check(window.BattleSnip(cx + 60, 20, cx + 400, 20) && Sim().Snips == 1, "A long stroke is a cut");
        TickBattle(1.0 / 60);
        check(window.BattleGuideStep == null && GuideTimesShown(PetWindow.GuideScissors) == 1, "Cutting ends the step");
        window.EndBattle();
        ToGround();
        window.StartBattle(stage: teachSpray);
        RunToStep(4);
        check(window.BattleGuideStep == PetWindow.GuideSpray, $"The Spray's stage teaches it ({window.BattleGuideStep})");
        window.UseBattleSkill(BattleSkill.Spray);
        TickBattle(1.0 / 60);
        check(window.BattlePressHold(Sim().CatX + 120, 20) && Sim().Spraying && !Sim().FanBlowing, "Held on the floor, the Spray sprays (not the Fan)");
        TickBattle(1.0 / 60);
        check(window.BattleGuideStep == null && Sim().Spraying, "Spraying ends the step; it goes on while held");
        window.BattleReleaseFan();
        check(!Sim().Spraying && Sim().Armed == BattleSkill.Spray, "Let go, it stops and stays chosen");
        window.EndBattle();
        foreach (var stage in new[] { teachGrabber, teachScissors, teachSpray }) window.AdventureProgress.Completed.Remove(stage.Id);
        prefs.BattleLoadout = null;

        // 1.5 M21: a stage that opens a skill lends it until it is won: carried in the last slot (the
        // Shield of the first five) and taught, though the user never chose it.
        ToGround();
        check(window.StartBattle(stage: teachLaser) == null && Sim().Carries(BattleSkill.Laser) && !Sim().Carries(BattleSkill.Shield) && !window.BattleSkillOpen(BattleSkill.Laser),
            "A stage not won yet lends its skill in the last slot (not opened for other battles)");
        RunToStep(4);
        check(window.BattleGuideStep == PetWindow.GuideLaser, $"and teaches it ({window.BattleGuideStep})");
        window.EndBattle();

        // The built-in chapter 2: the map shows the latest open chapter, ‹ › turns chapters, a locked one
        // says what opens it; the setup panel lends the stage's skill; a new slime's first showing is a step.
        window.BattleTuning = BattleData.Settings;
        var progress = window.AdventureProgress;
        progress.Completed.Add(PetWindow.StageSlimeKing);
        check(window.OpenAdventureMap() == null && window.AdventureMapChapter == "download_dump" && window.AdventureSelectedStage == PetWindow.StageDownloadGate,
            $"With the Slime King beaten the map opens on chapter 2, its first stage chosen ({window.AdventureMapChapter}, {window.AdventureSelectedStage})");
        Call(window, "UpdateAdventureMap");
        string mapText = window.AdventureMapText;
        check(mapText.Contains("Chapter 2: The Download Dump") && mapText.Contains("Suggested: Fan") && mapText.Contains("This stage lends Fan") && window.AdventureEnterEnabled,
            $"The map names chapter 2 and what its stage lends and suggests ({mapText.Replace('\n', '|')})");
        check(window.AdventureShowChapter(-1) && window.AdventureMapChapter == "slime_invasion" && !window.AdventureShowChapter(-1) && window.AdventureShowChapter(1),
            "‹ › turn between the chapters (and stop at the ends)");
        check(window.AdventureEnterStage() == null && window.BattlePreparing && window.BattlePrepLent == BattleSkill.Fan && window.BattlePrepSkills.Contains(BattleSkill.Fan)
            && !window.BattlePrepSkills.Contains(BattleSkill.Shield) && !window.BattlePrepToggleSkill(BattleSkill.Fan) && window.BattlePrepSkills.Contains(BattleSkill.Fan),
            "The stage's setup carries the lent Fan in the Shield's slot, and it cannot be put back");
        check(window.BattlePrepText.Contains("(Lent for this stage.)"), $"The setup panel says the Fan is lent ({window.BattlePrepText.Replace('\n', '|')})");
        check(window.BattlePrepToggleSkill(BattleSkill.Heal) && window.BattlePrepToggleSkill(BattleSkill.Shield) && window.BattlePrepReady() == null
            && Sim().Carries(BattleSkill.Fan) && Sim().Carries(BattleSkill.Shield) && !Sim().Carries(BattleSkill.Heal) && Sim().Loot == BattleLoot.Items && prefs.BattleLoadout == null,
            "The other four may be changed: the battle carries that choice with the Fan, drops items, and the lent loadout is not kept as the choice");
        window.EndBattle();
        window.Reset();
        progress.Completed.Remove(PetWindow.StageSlimeKing);
        window.Reset();
        check(window.OpenAdventureMap(PetWindow.StageGhostFolder) == null && window.AdventureMapChapter == "download_dump" && !window.AdventureEnterEnabled
            && window.AdventureMapText.Contains("beat The Slime King (chapter 1) to open this chapter") == false && window.AdventureMapText.Contains("Locked"),
            $"Before the Slime King, chapter 2's stages are locked ({window.AdventureMapText.Replace('\n', '|')})");
        window.AdventureSelectStage(PetWindow.StageDownloadGate);
        Call(window, "UpdateAdventureMap");
        check(window.AdventureMapText.Contains("Locked — beat The Slime King (chapter 1) to open this chapter"), $"Its first stage says what opens the chapter ({window.AdventureMapText.Replace('\n', '|')})");
        window.Reset();
        progress.Completed.Add(PetWindow.StageSlimeKing);
        progress.Completed.Add(PetWindow.StageDownloadGate);
        ToGround();
        check(window.StartBattle(stage: window.Adventure!.Stage(PetWindow.StageGhostFolder)) == null && Sim().Carries(BattleSkill.Laser) && Sim().Carries(BattleSkill.Fan) == false,
            "(setup) chapter 2 stage 2 with the lent Laser");
        for (int i = 0; i < 900 && window.BattleGuideStep != PetWindow.GuidePhantom && window.BattleActive; i++)
        {
            if (window.BattleGuideStep != null) window.BattleGuideOk();
            ToGround();
            Call(window, "TickBattle", 1.0 / 60);
        }
        check(window.BattleGuideStep == PetWindow.GuidePhantom && window.BattleGuideFreezes && window.BattleGuideSpots.Count == 1 && window.BattleGuideText.Contains("ghost slime"),
            $"A ghost slime's first showing is a guide step lighting it ({window.BattleGuideStep}: {window.BattleGuideText})");
        window.BattleGuideOk();
        TickBattle(1.0 / 60);
        check(window.BattleGuideStep != PetWindow.GuidePhantom && GuideTimesShown(PetWindow.GuidePhantom) == 1, "Got it ends it, once a run");
        window.EndBattle();
        progress.Completed.Remove(PetWindow.StageSlimeKing);
        progress.Completed.Remove(PetWindow.StageDownloadGate);
        window.BattleGuideEnabled = false;

        prefs.Language = language;
        window.Reset();
        window.BattleTuning = new BattleSettings();
    }
}
