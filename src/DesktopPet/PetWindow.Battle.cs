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
    private TextBlock? hudStatus, hudHp, hudPushText, hudPauseText, hudEndText, hudHint;
    private Border? hudHpFill, hudPushFill, hudPush, hudPause;

    public BattleSim? Battle => battle;
    public bool BattleActive => battle != null;

    private void InitializeBattle(ContextMenu menu)
    {
        var root = PetUi.MenuItem(PetUi.Battle, Brushes.SeaGreen);
        var start = new MenuItem();
        start.Click += (_, _) => { if (StartBattle() is string reason) MessageBox.Show(reason, "Desktop Pet"); };
        var pause = new MenuItem();
        pause.Click += (_, _) => { if (battle != null) SetBattlePaused(!(battle.Paused && !battlePausedByMenu)); };
        var end = new MenuItem();
        end.Click += (_, _) => EndBattle();
        root.Items.Add(start); root.Items.Add(pause); root.Items.Add(end);
        menu.Items.Add(root);
        menu.Opened += (_, _) =>
        {
            // Opening the menu pauses a running battle; closing resumes only that pause.
            if (battle is { Paused: false }) { battle.Paused = true; battlePausedByMenu = true; }
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
            if (battle != null && battlePausedByMenu) battle.Paused = false;
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
        hudStatus = hudHp = hudPushText = hudPauseText = hudEndText = hudHint = null;
        hudHpFill = hudPushFill = hudPush = hudPause = null;
        pet.SwordOut = false; pet.SwordSwing = -1; pet.BattleHurt = 0;
        pet.BattleDazed = pet.BattleCheer = false;
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
        UpdateBattleHud();
        return true;
    }

    public bool ArmBattlePush(bool armed)
    {
        if (battle == null || (armed && battle.Paused)) return false;
        bool done = battle.ArmPush(armed);
        UpdateBattleHud();
        return done;
    }

    // A click on a slime: with the push skill chosen it pushes (the target stays as it was),
    // otherwise it marks the slime as the cat's target. Neither deals damage.
    public bool BattleClickSlime(int id)
    {
        if (battle == null || battle.Paused) return false;
        bool done = battle.PushArmed ? battle.UsePush(id, pet.X + settings.Width / 2) : battle.Select(id);
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
        if (context.Fullscreen && !battle.Paused) { battle.Paused = true; battlePausedByMenu = false; }
        bool held = BattleHoldsOff;
        bool lifted = dragging || !pet.Grounded || pet.Action is PetAction.Dragging or PetAction.Thrown;
        // The work area changed, or the cat was carried to another monitor: the field follows.
        if (!held && bounds != battleArea) { battleArea = bounds; battle.SetArea(bounds.Left, bounds.Right); }
        var order = battle.Step(dt, pet.X + settings.Width / 2, held, lifted);
        foreach (var e in battle.Events)
        {
            _ = e.Kind switch
            {
                BattleEventKind.SlimeHit or BattleEventKind.Pushed => PlayPetSound("ball_tap"),
                BattleEventKind.CatHurt => PlayPetSound("surprised"),
                BattleEventKind.WaveCleared or BattleEventKind.Won => PlayPetSound("excited"),
                BattleEventKind.Lost => PlayPetSound("annoyed"),
                _ => false
            };
        }
        battle.ClearEvents();
        if (battle.Finished) { EndBattle(); return; }
        pet.SwordSwing = battle.SwingProgress;
        pet.BattleHurt = battle.HurtFlash;
        pet.BattleCheer = battle.Phase == BattlePhase.Won;
        pet.BattleDazed = battle.Phase == BattlePhase.Lost;
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
        UpdateBattleWindows();
    }

    private Window CreateBattleWindow(UIElement content, string title)
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
            const long ToolWindow = 0x80, NoActivate = 0x08000000;
            SetWindowLongPtr(handle, ExStyle, new IntPtr(GetWindowLongPtr(handle, ExStyle).ToInt64() | ToolWindow | NoActivate));
        };
        return window;
    }

    private void ShowBattleWindows()
    {
        if (battle == null) return;
        battleView = new BattleFieldView(battle);
        battleView.SlimeClicked += id => BattleClickSlime(id);
        battleView.RightClicked += () => ArmBattlePush(false); // Right-click on the field cancels the skill.
        battleFieldWindow = CreateBattleWindow(battleView, "Pet battle");
        battleFieldWindow.DpiChanged += (_, _) => battlePlacedScale = 0; // Re-place in pixels next frame.
        battleHudWindow = CreateBattleWindow(BuildBattleHud(), "Pet battle HUD");
        battleHudWindow.SizeToContent = SizeToContent.WidthAndHeight;
        battlePlacedScale = 0;
        battleFieldWindow.Show();
        battleHudWindow.Show();
        UpdateBattleWindows();
    }

    private void UpdateBattleWindows()
    {
        if (battle == null || battleFieldWindow == null || battleHudWindow == null || battleView == null) return;
        // A fullscreen app (game, video) gets a clean screen; the paused battle comes back after.
        if (context.Fullscreen)
        {
            if (battleFieldWindow.IsVisible) { battleFieldWindow.Hide(); battleHudWindow.Hide(); }
            return;
        }
        if (!battleFieldWindow.IsVisible) { battleFieldWindow.Show(); battleHudWindow.Show(); battlePlacedScale = 0; }
        var area = battleArea;
        if (battlePlacedScale != scale || battlePlacedArea != area)
        {
            battlePlacedScale = scale; battlePlacedArea = area;
            battleView.OriginX = area.Left;
            var handle = new WindowInteropHelper(battleFieldWindow).Handle;
            const uint NoZOrder = 0x0004, NoActivate = 0x0010;
            SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(area.Left * scale), (int)Math.Round((area.Bottom - BattleFieldView.FieldHeight) * scale),
                (int)Math.Round((area.Right - area.Left) * scale), (int)Math.Round(BattleFieldView.FieldHeight * scale), NoZOrder | NoActivate);
            // The pet stays above its battlefield, so clicks on the cat still reach the cat.
            const uint NoMove = 0x0002, NoSize = 0x0001;
            SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), 0, 0, 0, 0, NoMove | NoSize | NoActivate);
        }
        battleView.InvalidateVisual();
        UpdateBattleHud();
        double width = battleHudWindow.ActualWidth;
        MoveWindow(battleHudWindow, (area.Left + area.Right - width) / 2, area.Top + 12);
    }

    // HUD: dark rounded panel with a white edge, readable on light and dark desktops.
    private UIElement BuildBattleHud()
    {
        var white = Brushes.White;
        hudStatus = new TextBlock { Foreground = white, FontWeight = FontWeights.SemiBold, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center };
        hudHp = new TextBlock { Foreground = white, FontSize = 12, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        hudHpFill = new Border { Background = new SolidColorBrush(Color.FromRgb(0x6F, 0xE0, 0x6A)), CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Width = 90 };
        var hpBar = new Border { Width = 90, Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            Child = hudHpFill, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var top = new StackPanel { Orientation = Orientation.Horizontal, Children = { hudStatus, hpBar, hudHp } };
        (hudPush, hudPushText, hudPushFill) = HudButton(() => { if (battle != null) ArmBattlePush(!battle.PushArmed); });
        (hudPause, hudPauseText, _) = HudButton(() => { if (battle != null) SetBattlePaused(!battle.Paused); });
        (var end, hudEndText, _) = HudButton(EndBattle);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), Children = { hudPush, hudPause, end } };
        hudHint = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xD8, 0xD6, 0xE8)), FontSize = 11.5, Margin = new Thickness(0, 5, 0, 0) };
        var panel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE8, 0x26, 0x23, 0x38)), BorderBrush = white, BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(6),
            Child = new StackPanel { Children = { top, buttons, hudHint } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
        };
        TextOptions.SetTextFormattingMode(panel, TextFormattingMode.Display);
        panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        UpdateBattleHud();
        return panel;
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

    private static void SetText(TextBlock? block, string text) { if (block != null && block.Text != text) block.Text = text; }

    private void UpdateBattleHud()
    {
        if (battle is not BattleSim b || hudStatus == null) return;
        int left = b.Alive + b.ToSpawn;
        string status = b.Phase switch
        {
            BattlePhase.Won => L("Chiến thắng!", "Victory!"),
            BattlePhase.Lost => L("Thua rồi… mèo choáng váng", "Defeated… the cat sees stars"),
            BattlePhase.WaveBreak when b.Wave == 0 => L($"Chuẩn bị! Đợt 1/{b.WaveCount}", $"Get ready! Wave 1/{b.WaveCount}"),
            BattlePhase.WaveBreak => L($"Xong đợt {b.Wave}! Đợt {b.Wave + 1}/{b.WaveCount} sắp tới", $"Wave {b.Wave} cleared! Wave {b.Wave + 1}/{b.WaveCount} next"),
            _ => L($"Đợt {b.Wave}/{b.WaveCount} · còn {left} slime", $"Wave {b.Wave}/{b.WaveCount} · {left} slime{(left == 1 ? "" : "s")} left")
        };
        if (b.Paused && !b.Over) status += context.Fullscreen ? L(" · Tạm dừng (toàn màn hình)", " · Paused (fullscreen)") : L(" · Tạm dừng", " · Paused");
        SetText(hudStatus, status);
        SetText(hudHp, $"HP {Math.Ceiling(b.CatHp)}/{b.Settings.CatMaxHp}");
        if (hudHpFill != null) hudHpFill.Width = 90 * Math.Clamp(b.CatHp / Math.Max(1, b.Settings.CatMaxHp), 0, 1);
        bool pushReady = !b.Over && !b.Paused && b.PushCooldown <= 0;
        SetText(hudPushText, b.PushArmed ? L("Chọn slime để đẩy… (bấm để hủy)", "Click a slime to push… (click to cancel)")
            : b.PushCooldown > 0 ? L($"Đẩy quái {b.PushCooldown:F1}s", $"Push {b.PushCooldown:F1}s") : L("Đẩy quái", "Push"));
        if (hudPush != null)
        {
            hudPush.IsEnabled = pushReady || b.PushArmed;
            if (!hudPush.IsMouseOver) hudPush.Opacity = hudPush.IsEnabled ? 1 : 0.5;
            hudPush.Background = b.PushArmed ? PetUi.Accent : HudButtonBack;
        }
        if (hudPushFill != null) hudPushFill.Width = b.PushCooldown > 0 ? Math.Max(0, (hudPush?.ActualWidth ?? 70) - 20) * (1 - b.PushCooldown / b.Settings.PushCooldown) : 0;
        SetText(hudPauseText, b.Paused && !battlePausedByMenu ? L("Tiếp tục", "Resume") : L("Tạm dừng", "Pause"));
        if (hudPause != null)
        {
            hudPause.IsEnabled = !b.Over && !(b.Paused && context.Fullscreen);
            if (!hudPause.IsMouseOver) hudPause.Opacity = hudPause.IsEnabled ? 1 : 0.5;
        }
        SetText(hudEndText, L("Kết thúc", "End"));
        SetText(hudHint, BattleHoldsOff && !b.Over ? L("Mèo đang được bế: quái đứng chờ, không ai bị đánh.", "The cat is lifted: slimes wait, nobody gets hit.")
            : b.PushArmed ? L("Chuột phải lên chiến trường cũng hủy chọn kỹ năng.", "Right-click the field to cancel the skill too.")
            : L("Click slime: chọn mục tiêu · Nhấc mèo lên để cứu nguy", "Click a slime: set target · Lift the cat to rescue it"));
        if (battleView != null) battleView.Cursor = b.PushArmed ? Cursors.Cross : Cursors.Hand;
    }
}
