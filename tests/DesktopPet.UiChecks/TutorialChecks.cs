using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// Battle Mode's first-battle guide and its danger feedback in the real pet window: the guide
// opens once, holds the battle while open, is mouse-only and never takes focus; a slime holding
// its attack while the cat is carried is shown on the field and in the HUD hint. In-memory
// preferences only (the real preferences file is never written).
internal static class TutorialChecks
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Field<T>(PetWindow window, string name) => (T)typeof(PetWindow).GetField(name, Flags)!.GetValue(window)!;
    private static void SetField(PetWindow window, string name, object? value) => typeof(PetWindow).GetField(name, Flags)!.SetValue(window, value);
    private static object? Call(PetWindow window, string name, params object?[] args) => typeof(PetWindow).GetMethod(name, Flags)!.Invoke(window, args);
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
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 2 }, MaxAlive = 2, SpawnInterval = 0, SlimeMaxHp = 1000, CatDamage = 0, SlimeDamage = 0 };

        // The first battle setup opens the guide; no battle exists behind it yet.
        prefs.BattleTutorialSeen = false;
        check(window.OpenBattlePrep() == null && window.BattleTutorialOpen && window.BattleTutorialStep == 0 && !window.BattleActive,
            "The first battle setup opens the guide (no battle runs yet)");
        await Task.Delay(20);
        var guide = GuideWindow();
        long style = guide == null ? 0 : GetWindowLongPtr(new WindowInteropHelper(guide).Handle, -20).ToInt64();
        check(guide is { ShowActivated: false, ShowInTaskbar: false } && (style & 0x08000000) != 0, "The guide never takes focus and has no taskbar button");
        TickBattle(2);
        check(!window.BattleActive && window.BattlePreparing, "Nothing runs behind the guide (no battle until Ready)");
        var cards = new List<string> { window.BattleTutorialText };
        for (int i = 1; i < PetWindow.BattleTutorialSteps; i++) { window.BattleTutorialNext(); cards.Add(window.BattleTutorialText); }
        check(cards[0].StartsWith("Pick a target") && cards[1].Contains("Push") && cards[2].Contains("red ring") &&
              cards[3].Contains("1.5 s") && cards[3].Contains("every 5 s") && cards.Distinct().Count() == 4,
            $"Four cards: targets, skills, rescue (the held attack), protection and cooldowns with the battle's numbers ({string.Join(" | ", cards.Select(c => c.Split('\n')[0]))})");
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
        check(slime.State == SlimeState.Windup && BattleHud.HeldAttacks(Sim()) == 1 && hud.HintText.Contains("holds its attack") && hud.HintText.Contains("red ring"),
            $"Carrying the cat: the HUD says a slime holds its attack and where to put the cat ({hud.HintText})");
        check(ringBefore == 0 && ringHeld > 0, $"The field shows the held attack's reach as a ring on the floor only while the cat is carried ({ringBefore} → {ringHeld})");
        window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        pet.Grounded = true; pet.VX = pet.VY = 0; pet.Action = PetAction.Idle; pet.Y = Field<Area>(window, "bounds").Bottom - 112;
        TickBattle(1.0 / 60);
        Call(window, "UpdateBattleHud");
        check(Sim().Grace > 0 && hud.HintText.StartsWith("Protected after landing"), $"After a rescue the HUD says the cat is protected, and for how long ({hud.HintText})");

        window.EndBattle();
        prefs.Language = language;
        prefs.BattleTutorialSeen = true;
        window.Reset();
        window.BattleTuning = new BattleSettings();
    }
}
