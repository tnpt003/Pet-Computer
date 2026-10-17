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
    public BattleSettings BattleTuning { get; set; } = new();
    private BattleSim? battle;
    private Area battleArea;
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
        battle = new BattleSim(BattleTuning, battleArea.Left, battleArea.Right);
        battlePausedByMenu = false;
        pet.SwordOut = true;
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
        pet.SwordOut = false; pet.SwordSwing = -1; pet.BattleHurt = 0;
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
        // The work area changed, or the cat was carried to another monitor: the field follows.
        if (!held && bounds != battleArea) { battleArea = bounds; battle.SetArea(bounds.Left, bounds.Right); }
        var order = battle.Step(dt, pet.X + settings.Width / 2, held, lifted);
        PlayBattleSound();
        ShowBattleBanners();
        battle.ClearEvents();
        if (battle.Finished)
        {
            var result = new BattleSummary(battle.Phase == BattlePhase.Won, battle.Defeated, battle.FightSeconds, battle.CatHp, battle.Settings.CatMaxHp, battle.BossDefeated);
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

    // One cue per frame: the most important event wins (a defeat's pop over its hit's squish).
    private void PlayBattleSound()
    {
        if (battle == null) return;
        BattleEventKind? best = null;
        foreach (var e in battle.Events)
            if (best == null || SoundRank(e.Kind) > SoundRank(best.Value)) best = e.Kind;
        _ = best switch
        {
            BattleEventKind.Won or BattleEventKind.WaveCleared or BattleEventKind.Cheered => PlayPetSound("excited"),
            BattleEventKind.Lost => PlayPetSound("annoyed"),
            BattleEventKind.CatHurt or BattleEventKind.BossAppeared => PlayPetSound("surprised"),
            BattleEventKind.Healed => PlayPetSound("satisfied"),
            BattleEventKind.SlimeDefeated or BattleEventKind.BombExploded => PlayPetSound("pop"),
            BattleEventKind.SlimeHit => PlayPetSound("squish"),
            BattleEventKind.Blocked => PlayPetSound("ball_tap"),
            BattleEventKind.Pushed or BattleEventKind.Swing or BattleEventKind.BombThrown or BattleEventKind.Shielded => PlayPetSound("swoosh"),
            _ => false
        };
        static int SoundRank(BattleEventKind kind) => kind switch
        {
            BattleEventKind.Won or BattleEventKind.Lost => 9, BattleEventKind.BossAppeared => 8, BattleEventKind.CatHurt => 7,
            BattleEventKind.WaveCleared => 6, BattleEventKind.Healed or BattleEventKind.Cheered => 5, BattleEventKind.Blocked => 5,
            BattleEventKind.SlimeDefeated or BattleEventKind.BombExploded => 4, BattleEventKind.SlimeHit => 3,
            BattleEventKind.Pushed or BattleEventKind.BombThrown or BattleEventKind.Shielded => 2, BattleEventKind.Swing => 1, _ => 0
        };
    }

    // Big moments get a banner across the HUD's centre.
    private void ShowBattleBanners()
    {
        if (battle == null || battleHud == null) return;
        foreach (var e in battle.Events)
        {
            string? text = e.Kind switch
            {
                BattleEventKind.WaveStarted when battle.IsBossWave((int)e.Value) => L("BOSS!", "BOSS!"),
                BattleEventKind.WaveStarted => L($"ĐỢT {(int)e.Value}", $"WAVE {(int)e.Value}"),
                BattleEventKind.BossSummoned => L("Vua Slime gọi quân!", "The King calls for help!"),
                BattleEventKind.BossEnraged => L("Vua Slime nổi giận!", "The Slime King is angry!"),
                BattleEventKind.Won => L("CHIẾN THẮNG!", "VICTORY!"),
                BattleEventKind.Lost => L("THUA RỒI…", "DEFEATED…"),
                _ => null
            };
            if (text != null) battleHud.ShowBanner(text, battle.Time, e.Kind is BattleEventKind.Won or BattleEventKind.Lost ? 3 : 1.6);
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
        if (battlePlacedScale != scale || battlePlacedArea != area)
        {
            battlePlacedScale = scale; battlePlacedArea = area;
            RequestBattleRedraw();
            battleView.OriginX = area.Left;
            var handle = new WindowInteropHelper(battleFieldWindow).Handle;
            const uint NoZOrder = 0x0004, NoActivate = 0x0010;
            SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(area.Left * scale), (int)Math.Round((area.Bottom - BattleFieldView.FieldHeight) * scale),
                (int)Math.Round((area.Right - area.Left) * scale), (int)Math.Round(BattleFieldView.FieldHeight * scale), NoZOrder | NoActivate);
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
        MoveWindow(battleHudWindow, (area.Left + area.Right - width) / 2, area.Top + 12);
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

    private static readonly Brush HudButtonBack = PetUi.Frozen(Color.FromArgb(0x50, 255, 255, 255));

    private static (Border Button, TextBlock Text, Border Progress) HudButton(Action click)
    {
        var text = new TextBlock { Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        var progress = new Border { Height = 3, Background = new SolidColorBrush(Color.FromRgb(0x5C, 0xC8, 0xFF)), HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 3, 0, 0) };
        var button = new Border
        {
            Background = HudButtonBack, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 4, 10, 3),
            Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, MinWidth = 70, Child = new StackPanel { Children = { text, progress } }
        };
        button.MouseLeftButtonDown += (_, e) => { if (button.IsEnabled) click(); e.Handled = true; };
        button.MouseEnter += (_, _) => button.Opacity = button.IsEnabled ? 0.85 : button.Opacity;
        button.MouseLeave += (_, _) => button.Opacity = button.IsEnabled ? 1 : 0.5;
        return (button, text, progress);
    }

    private void UpdateBattleHud()
    {
        if (battle is not BattleSim b || battleHud == null) return;
        battleHud.Update(b, b.Paused && !battlePausedByMenu, context.Fullscreen, BattleHoldsOff);
        if (battleView != null) battleView.Cursor = b.Armed != null ? Cursors.Cross : Cursors.Hand;
    }


    // A short click-through note above the cat (why a battle cannot start); never takes focus.
    private Window? battleNoticeWindow;
    private System.Windows.Threading.DispatcherTimer? battleNoticeTimer;
    public const double BattleNoticeSeconds = 2.5;

    private void ShowBattleNotice(string text)
    {
        CloseBattleNotice();
        var label = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 240, FontFamily = PetUi.TextFont };
        var panel = new Border { Background = HudBack, BorderBrush = Brushes.White, BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5, 10, 6), Margin = new Thickness(4), Child = label };
        var notice = CreateBattleWindow(panel, "Pet notice", clickThrough: true);
        notice.SizeToContent = SizeToContent.WidthAndHeight;
        notice.IsHitTestVisible = false;
        notice.Closed += (_, _) => { if (battleNoticeWindow == notice) battleNoticeWindow = null; };
        battleNoticeWindow = notice;
        notice.Show();
        notice.UpdateLayout();
        double left = Math.Clamp(pet.X + settings.Width / 2 - notice.ActualWidth / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - notice.ActualWidth));
        MoveWindow(notice, left, Math.Max(bounds.Top, pet.Y + 6 - notice.ActualHeight));
        battleNoticeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(BattleNoticeSeconds) };
        battleNoticeTimer.Tick += (_, _) => CloseBattleNotice();
        battleNoticeTimer.Start();
    }

    private void CloseBattleNotice()
    {
        battleNoticeTimer?.Stop();
        battleNoticeWindow?.Close();
        battleNoticeWindow = null;
    }

    // The result after a won or lost battle: stats, Play again and Close. It closes by itself.
    public readonly record struct BattleSummary(bool Won, int Defeated, double Seconds, double Hp, double MaxHp, bool Boss = false);
    public BattleSummary? LastBattleSummary { get; private set; }
    private Window? battleSummaryWindow;
    private System.Windows.Threading.DispatcherTimer? battleSummaryTimer;
    public const double BattleSummarySeconds = 12;

    private void ShowBattleSummary(BattleSummary result)
    {
        CloseBattleSummary();
        LastBattleSummary = result;
        if (closed || context.Fullscreen) return;
        var title = new TextBlock { Text = result.Won ? (result.Boss ? L("Chiến thắng! Đã hạ Vua Slime", "Victory! The Slime King is down") : L("Chiến thắng!", "Victory!"))
                : L("Thua rồi… mèo choáng váng", "Defeated… the cat sees stars"),
            Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold };
        var time = TimeSpan.FromSeconds(Math.Round(result.Seconds));
        string clock = $"{(int)time.TotalMinutes}:{time.Seconds:00}";
        var stats = new TextBlock { Foreground = Brushes.White, FontSize = 12.5, Margin = new Thickness(0, 4, 0, 0),
            Text = L($"Hạ {result.Defeated} slime · {clock} · HP còn {Math.Ceiling(result.Hp)}/{result.MaxHp}",
                     $"{result.Defeated} slime{(result.Defeated == 1 ? "" : "s")} defeated · {clock} · HP {Math.Ceiling(result.Hp)}/{result.MaxHp} left") };
        var (again, againText, _) = HudButton(() =>
        {
            CloseBattleSummary();
            if (StartBattle() is string reason) ShowBattleNotice(reason);
        });
        againText.Text = L("Chơi lại", "Play again");
        var (close, closeText, _) = HudButton(CloseBattleSummary);
        closeText.Text = L("Đóng", "Close");
        var panel = new Border
        {
            Background = HudBack, BorderBrush = Brushes.White, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(6),
            Child = new StackPanel { Children = { title, stats, new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), Children = { again, close } } } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
        };
        panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        var window = CreateBattleWindow(panel, "Pet result");
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.Closed += (_, _) => { if (battleSummaryWindow == window) battleSummaryWindow = null; };
        battleSummaryWindow = window;
        window.Show();
        window.UpdateLayout();
        MoveWindow(window, (bounds.Left + bounds.Right - window.ActualWidth) / 2, bounds.Top + 12);
        battleSummaryTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(BattleSummarySeconds) };
        battleSummaryTimer.Tick += (_, _) => CloseBattleSummary();
        battleSummaryTimer.Start();
    }

    private void CloseBattleSummary()
    {
        battleSummaryTimer?.Stop();
        battleSummaryWindow?.Close();
        battleSummaryWindow = null;
    }

    private static readonly Brush HudBack = PetUi.Frozen(Color.FromArgb(0xE8, 0x26, 0x23, 0x38));
}
