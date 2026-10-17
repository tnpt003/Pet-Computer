using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet;

// Battle Mode (optional, off by default, never resumed at startup): the cat fights slimes
// with a wooden sword; the user picks targets, pushes slimes away and can lift the cat to
// safety. The fight is BattleSim (C# only, no Python); this file owns its lifecycle, turns
// its orders into the pet's normal walking, and shows two small windows: the floor strip
// (BattleFieldView) and the HUD. While a battle runs it owns the pet: no autonomous
// behavior, AI commands, games, sleep or automatic emotions.
public sealed partial class PetWindow
{
    // From Assets/battle.json (built-in numbers if the file is broken); tests may replace it.
    public BattleSettings BattleTuning { get; set; } = BattleData.Settings;
    private BattleSim? battle;
    private Area battleArea;
    // The battle stays on the monitor it started on (its work area and DPI scale). If the cat is
    // carried or thrown to another monitor the battle waits, paused, until the cat is back.
    private string battleMonitor = "";
    private double battleScale = 1;
    private bool battlePausedByAway;
    public bool BattleCatAway => battlePausedByAway;
    // Replaceable for UI checks; the default asks Windows which monitor the cat is on.
    private Func<string>? battleMonitorProbe;
    private string CatMonitor() => (battleMonitorProbe ??= () => CurrentMonitor().DeviceName)();
    private double battlePlacedScale;
    private Area battlePlacedArea;
    private bool battlePausedByMenu;
    private Window? battleFieldWindow, battleHudWindow;
    private BattleFieldView? battleView;
    private BattleHud? battleHud;
    // The field and HUD redraw at most 30 times a second, only while something moves; a
    // click, pause or placement change asks for one redraw at once.
    public const double BattleFrameSeconds = 1.0 / 30;
    private double battleFrameClock;
    private bool battleRedraw, battleFieldIdle;
    public int BattleFieldDraws { get; private set; }
    private void RequestBattleRedraw() => battleRedraw = true;

    public BattleSim? Battle => battle;
    public bool BattleActive => battle != null;

    // Measurement only (scripts/perf.ps1 -Battle): PET_BATTLE_AUTOSTART=run keeps a battle going
    // (a new one after each end), =paused keeps a paused one open. Unset in normal use, where a
    // battle never starts by itself.
    private readonly string? battleAutostart = Environment.GetEnvironmentVariable("PET_BATTLE_AUTOSTART") is "run" or "paused"
        ? Environment.GetEnvironmentVariable("PET_BATTLE_AUTOSTART") : null;
    private double battleAutostartTimer;

    private void TickBattleAutostart(double dt)
    {
        if (battleAutostart == null || battle != null) return;
        battleAutostartTimer += dt;
        if (battleAutostartTimer < 1) return;
        battleAutostartTimer = 0;
        if (StartBattle() == null && battleAutostart == "paused") SetBattlePaused(true);
    }

    private void InitializeBattle(ContextMenu menu)
    {
        var root = PetUi.MenuItem(PetUi.Battle, Brushes.SeaGreen);
        var start = new MenuItem();
        start.Click += (_, _) => { if (StartBattle() is string reason) ShowBattleNotice(reason); };
        var pause = new MenuItem();
        pause.Click += (_, _) => { if (battle != null) SetBattlePaused(!(battle.Paused && !battlePausedByMenu)); };
        var end = new MenuItem();
        end.Click += (_, _) => EndBattle();
        root.Items.Add(start); root.Items.Add(pause); root.Items.Add(end);
        menu.Items.Add(root);
        menu.Opened += (_, _) =>
        {
            // Opening the menu pauses a running battle; closing resumes only that pause.
            if (battle is { Paused: false }) { battle.Paused = true; battlePausedByMenu = true; RequestBattleRedraw(); }
            bool userPaused = battle is { Paused: true } && !battlePausedByMenu;
            root.Header = L("Chế độ chiến đấu", "Battle Mode");
            string? refusal = battle == null ? BattleRefusal() : null;
            start.Header = L("Bắt đầu trận", "Start battle") + (refusal != null ? $" ({refusal.TrimEnd('.')})" : "");
            start.IsEnabled = refusal == null;
            start.Visibility = battle == null ? Visibility.Visible : Visibility.Collapsed;
            pause.Header = userPaused ? L("Tiếp tục trận", "Resume battle") : L("Tạm dừng trận", "Pause battle");
            pause.IsEnabled = !(userPaused && context.Fullscreen);
            pause.Visibility = end.Visibility = battle != null ? Visibility.Visible : Visibility.Collapsed;
            end.Header = L("Kết thúc trận", "End battle");
        };
        menu.Closed += (_, _) =>
        {
            if (battle != null && battlePausedByMenu) { battle.Paused = false; RequestBattleRedraw(); }
            battlePausedByMenu = false;
        };
    }

    // Null when a battle may start now; otherwise the reason, for the user.
    public string? BattleRefusal()
    {
        if (battle != null) return L("Trận đang diễn ra.", "A battle is already running.");
        if (closed) return L("Mèo đã đóng.", "The pet is closed.");
        if (paused) return L("Mèo đang tạm dừng.", "The pet is paused.");
        if (context.Fullscreen) return L("Đang có ứng dụng toàn màn hình.", "A fullscreen app is open.");
        if (pet.Emotions.SleepLocked || pet.Action == PetAction.Sleeping || behavior.HoldSleep) return L("Mèo đang ngủ.", "The pet is asleep.");
        if (pet.Action == PetAction.Waking) return L("Mèo đang thức dậy.", "The pet is waking up.");
        if (dragging || pressPending || pet.Action == PetAction.Dragging) return L("Mèo đang bị kéo.", "The pet is being dragged.");
        if (!pet.Grounded || PetActionRules.IsInFlight(pet.Action) || pet.Action == PetAction.Climbing) return L("Mèo đang bay hoặc đang leo.", "The pet is in the air or climbing.");
        if (pet.LedgeId != 0) return L("Mèo đang đứng trên cửa sổ, hãy đưa mèo xuống sàn.", "The pet is on a window; put it on the floor first.");
        if (toyWindow != null || laserWindow != null || pet.TreatRemaining > 0 || treatSatisfiedRemaining > 0)
            return L("Mèo đang chơi hoặc đang ăn.", "The pet is playing or eating.");
        if (pet.Emotions.BlocksAutonomy) return L("Mèo đang phản ứng, thử lại sau giây lát.", "The pet is reacting; try again in a moment.");
        return null;
    }

    // Null on success, otherwise why the battle cannot start (nothing changes then).
    public string? StartBattle()
    {
        if (BattleRefusal() is string reason) return reason;
        CloseBattleSummary();
        CancelFeatureActivity();
        queuedCommand = null;
        revision++; // Any AI reply chosen before the battle is now stale.
        pet.Speech.Hide(); pet.Thought.Clear();
        battleArea = bounds;
        battleMonitor = CatMonitor();
        battleScale = scale;
        battlePausedByAway = false;
        battle = new BattleSim(BattleTuning, battleArea.Left, battleArea.Right);
        battlePausedByMenu = false;
        pet.SwordOut = true;
        pet.MoveScale = battle.Settings.CatRunScale;
        pet.Emotions.SetSuspended(true);
        pet.Action = PetAction.Idle;
        pet.VX = pet.SmoothedWalkVelocity = 0;
        behavior.Reset(pet);
        ShowBattleWindows();
        System.Diagnostics.Trace.WriteLine("Battle started.");
        PlayPetSound("excited");
        return null;
    }

    // Removes every battle object and gives the pet back to its normal life. A pet that is
    // being dragged or is in the air keeps that state; input and physics finish it.
    public void EndBattle()
    {
        if (battle == null) return;
        battle = null;
        battlePausedByMenu = false;
        battleFieldWindow?.Close(); battleHudWindow?.Close();
        battleFieldWindow = battleHudWindow = null;
        battleView = null;
        battleHud = null;
        pet.SwordOut = false; pet.SwordSwing = -1; pet.BattleHurt = 0; pet.MoveScale = 1;
        pet.BattleDazed = pet.BattleCheer = pet.BattleShield = pet.BattleBuff = false;
        pet.Emotions.SetSuspended(false);
        revision++; // Nothing requested during the battle may run late.
        behavior.Reset(pet);
        if (!dragging && !pressPending && pet.Grounded && pet.Action is PetAction.Idle or PetAction.Walking or PetAction.Sitting)
        {
            pet.Action = PetAction.Idle;
            pet.VX = pet.SmoothedWalkVelocity = 0;
        }
        System.Diagnostics.Trace.WriteLine("Battle ended.");
        if (!closed) Present();
    }

    // The user's pause (menu or HUD). Resuming waits until no fullscreen app is open.
    public bool SetBattlePaused(bool value)
    {
        if (battle == null || (!value && context.Fullscreen)) return false;
        battle.Paused = value;
        battlePausedByMenu = false;
        battlePausedByAway = false; // The user's own pause or resume wins.
        RequestBattleRedraw();
        UpdateBattleHud();
        return true;
    }

    public bool ArmBattlePush(bool armed)
    {
        if (battle == null || (armed && battle.Paused)) return false;
        bool done = battle.ArmPush(armed);
        RequestBattleRedraw();
        UpdateBattleHud();
        return done;
    }

    // A skill slot on the HUD: an aimed skill (Push, Bomb) is chosen or un-chosen and waits for
    // a slime click; the others (Heal, Cheer, Shield) act at once.
    public bool UseBattleSkill(BattleSkill skill)
    {
        if (battle == null || battle.Paused) return false;
        bool done = BattleSim.IsAimed(skill) ? battle.Arm(battle.Armed == skill ? null : skill) : battle.Use(skill, pet.X + settings.Width / 2);
        RequestBattleRedraw();
        UpdateBattleHud();
        battleView?.InvalidateVisual();
        return done;
    }

    // A click on a slime: with a skill chosen it uses it there (the target stays as it was),
    // otherwise it marks the slime as the cat's target. Marking deals no damage.
    // offset: where on the slime the click landed (DIPs from its centre); it sets Push's direction.
    public bool BattleClickSlime(int id, double offset = 0)
    {
        if (battle == null || battle.Paused) return false;
        bool done = battle.Armed is BattleSkill skill
            ? battle.Use(skill, id, pet.X + settings.Width / 2, skill == BattleSkill.Push ? BattleSim.PushDirection(offset) : null)
            : battle.Select(id);
        RequestBattleRedraw();
        UpdateBattleHud();
        battleView?.InvalidateVisual();
        return done;
    }

    // The battle may not move or hurt the cat: pressed, dragged, in the air or climbing.
    private bool BattleHoldsOff => dragging || pressPending || !pet.Grounded ||
        pet.Action is PetAction.Dragging or PetAction.Climbing || PetActionRules.IsInFlight(pet.Action);

    // Every unpaused frame, before physics.
    private void TickBattle(double dt)
    {
        if (battle == null) return;
        if (context.Fullscreen && !battle.Paused) { battle.Paused = true; battlePausedByMenu = false; RequestBattleRedraw(); }
        bool held = BattleHoldsOff;
        bool lifted = dragging || !pet.Grounded || pet.Action is PetAction.Dragging or PetAction.Thrown;
        if (!held) KeepBattleOnItsMonitor();
        var order = battle.Step(dt, pet.X + settings.Width / 2, held, lifted);
        PlayBattleSound();
        ShowBattleBanners();
        battle.ClearEvents();
        if (battle.Finished)
        {
            var result = new BattleSummary(battle.Phase == BattlePhase.Won, battle.Defeated, battle.FightSeconds, battle.CatHp, battle.Settings.CatMaxHp, battle.BossDefeated, battle.DefeatedBoss?.Name(preferences.Language == "en"));
            EndBattle();
            ShowBattleSummary(result);
            return;
        }
        pet.SwordSwing = battle.SwingProgress;
        pet.BattleHurt = battle.HurtFlash;
        pet.BattleCheer = battle.Phase == BattlePhase.Won;
        pet.BattleDazed = battle.Phase == BattlePhase.Lost;
        pet.BattleShield = battle.ShieldRemaining > 0;
        pet.BattleBuff = battle.CheerRemaining > 0;
        if (!held)
        {
            if (order.Facing != 0) pet.Facing = order.Facing;
            double target = Math.Clamp(order.X - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
            if (order.Command == CatCommand.Move && !battle.Paused && Math.Abs(target - pet.X) >= 6) WalkToward(target);
            else
            {
                pet.Action = battle.Over ? PetAction.Sitting : PetAction.Idle;
                pet.VX = pet.SmoothedWalkVelocity = 0;
            }
        }
        UpdateBattleWindows(dt);
    }

    // The cat landed on another monitor: pause and say where the battle is. Back on the battle's
    // monitor: resume that pause. On its own monitor a changed work area (taskbar, resolution,
    // DPI) moves the field with it.
    private void KeepBattleOnItsMonitor()
    {
        if (battle == null) return;
        if (CatMonitor() != battleMonitor)
        {
            if (battle.Paused) return;
            battle.Paused = true;
            battlePausedByAway = true;
            RequestBattleRedraw();
            ShowBattleNotice(L("Chiến trường ở màn hình khác — đưa mèo về đó để đánh tiếp.", "The battlefield is on another screen — bring the cat back to go on."));
            return;
        }
        if (battlePausedByAway)
        {
            battlePausedByAway = false;
            if (!context.Fullscreen) battle.Paused = false;
            CloseBattleNotice();
            RequestBattleRedraw();
        }
        if (bounds != battleArea)
        {
            battleArea = bounds;
            battleScale = scale;
            battle.SetArea(bounds.Left, bounds.Right);
        }
    }

    private Window CreateBattleWindow(UIElement content, string title, bool clickThrough = false)
    {
        var window = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true,
            Background = Brushes.Transparent, Topmost = true, ShowInTaskbar = false, ShowActivated = false,
            Focusable = false, Content = content, Title = title, Left = -10000, Top = -10000, Width = 10, Height = 10
        };
        window.SourceInitialized += (_, _) =>
        {
            // Clickable (slimes, HUD buttons) but never activated: typing elsewhere is untouched.
            ((HwndSource)PresentationSource.FromVisual(window)).AddHook(WindowHook);
            var handle = new WindowInteropHelper(window).Handle;
            const int ExStyle = -20;
            const long ToolWindow = 0x80, NoActivate = 0x08000000, Transparent = 0x20;
            SetWindowLongPtr(handle, ExStyle, new IntPtr(GetWindowLongPtr(handle, ExStyle).ToInt64() | ToolWindow | NoActivate | (clickThrough ? Transparent : 0)));
        };
        return window;
    }

    private void ShowBattleWindows()
    {
        if (battle == null) return;
        battleView = new BattleFieldView(battle);
        battleView.SlimeClicked += (id, offset) => BattleClickSlime(id, offset);
        battleView.RightClicked += () => { battle?.Arm(null); RequestBattleRedraw(); UpdateBattleHud(); }; // Right-click on the field cancels the skill.
        battleFieldWindow = CreateBattleWindow(battleView, "Pet battle");
        battleFieldWindow.DpiChanged += (_, _) => battlePlacedScale = 0; // Re-place in pixels next frame.
        battleHudWindow = CreateBattleWindow(BuildBattleHud(), "Pet battle HUD");
        battleHudWindow.SizeToContent = SizeToContent.WidthAndHeight;
        battlePlacedScale = 0;
        battleFrameClock = 0; battleFieldIdle = false; BattleFieldDraws = 0;
        RequestBattleRedraw();
        battleFieldWindow.Show();
        battleHudWindow.Show();
        UpdateBattleWindows();
    }

    private void UpdateBattleWindows(double dt = 0)
    {
        if (battle == null || battleFieldWindow == null || battleHudWindow == null || battleView == null) return;
        // A fullscreen app (game, video) gets a clean screen; the paused battle comes back after.
        if (context.Fullscreen)
        {
            if (battleFieldWindow.IsVisible) { battleFieldWindow.Hide(); battleHudWindow.Hide(); }
            return;
        }
        if (!battleFieldWindow.IsVisible) { battleFieldWindow.Show(); battleHudWindow.Show(); battlePlacedScale = 0; RequestBattleRedraw(); }
        var area = battleArea;
        double fieldScale = battleScale;
        if (battlePlacedScale != fieldScale || battlePlacedArea != area)
        {
            battlePlacedScale = fieldScale; battlePlacedArea = area;
            RequestBattleRedraw();
            battleView.OriginX = area.Left;
            var handle = new WindowInteropHelper(battleFieldWindow).Handle;
            const uint NoZOrder = 0x0004, NoActivate = 0x0010;
            SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(area.Left * fieldScale), (int)Math.Round((area.Bottom - BattleFieldView.FieldHeight) * fieldScale),
                (int)Math.Round((area.Right - area.Left) * fieldScale), (int)Math.Round(BattleFieldView.FieldHeight * fieldScale), NoZOrder | NoActivate);
            // The pet stays above its battlefield, so clicks on the cat still reach the cat.
            const uint NoMove = 0x0002, NoSize = 0x0001;
            SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), 0, 0, 0, 0, NoMove | NoSize | NoActivate);
        }
        // Paused, or nothing on the floor (wave breaks): the strip is drawn once and left alone.
        var b = battle;
        bool moving = !b.Paused && (b.Slimes.Count > 0 || b.Effects.Count > 0 || b.Bombs.Count > 0);
        battleFrameClock += dt;
        bool due = battleFrameClock >= BattleFrameSeconds;
        if (due) battleFrameClock = Math.Min(battleFrameClock - BattleFrameSeconds, BattleFrameSeconds);
        if (battleRedraw || (moving && due) || (!moving && !battleFieldIdle))
        {
            battleView.InvalidateVisual();
            BattleFieldDraws++;
            battleFieldIdle = !moving;
        }
        if (battleRedraw || (due && !b.Paused)) UpdateBattleHud();
        battleRedraw = false;
        double width = battleHudWindow.ActualWidth;
        MoveWindow(battleHudWindow, (area.Left + area.Right - width) / 2, area.Top + 12, fieldScale);
    }

    // The game-style HUD (PetBattleHud.cs) with a still portrait of this cat.
    private UIElement BuildBattleHud()
    {
        var portrait = new PetRenderer(new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1 })
            { Coat = renderer.Coat, Accessory = renderer.Accessory };
        battleHud = new BattleHud(preferences.Name, portrait, L);
        battleHud.SkillClicked += skill => UseBattleSkill(skill);
        battleHud.PauseClicked += () => { if (battle != null) SetBattlePaused(!battle.Paused); };
        battleHud.EndClicked += EndBattle;
        UpdateBattleHud();
        return battleHud;
    }

    private void UpdateBattleHud()
    {
        if (battle is not BattleSim b || battleHud == null) return;
        battleHud.Update(b, b.Paused && !battlePausedByMenu && !battlePausedByAway, context.Fullscreen, BattleHoldsOff, battlePausedByAway);
        if (battleView != null) battleView.Cursor = b.Armed != null ? Cursors.Cross : Cursors.Hand;
    }
}
