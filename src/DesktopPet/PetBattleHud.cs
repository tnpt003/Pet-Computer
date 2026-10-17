using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// The game-style battle HUD: the cat's portrait and HP bar (with a trail of lost HP), wave
// dots or the boss's HP bar, a banner for big moments, five skill slots with clock-wipe
// cooldowns, and small Pause / End buttons. Mouse only (no keyboard: the window never takes
// focus). Presentation only: PetWindow.Battle.cs feeds it from BattleSim each frame.
public sealed class BattleHud : Border
{
    public event Action<BattleSkill>? SkillClicked;
    public event Action? PauseClicked, EndClicked;
    // Slots follow the skill list (PetBattleData.cs): a new skill gets its slot automatically.
    public static readonly BattleSkill[] SkillOrder = Array.ConvertAll(BattleSkills.All, info => info.Skill);
    private const double HpWidth = 170, BossWidth = 200;
    private readonly Func<string, string, string> L;
    private readonly TextBlock hpText, status, hint, banner, bossName, comboText;
    private readonly Border hpFill, hpTrail, bossFill, bossBar, pauseButton, endButton;
    private readonly HudGlyph pauseGlyph;
    private readonly StackPanel waveDots;
    private readonly SkillSlot[] slots = new SkillSlot[SkillOrder.Length];
    private double trail = 1, lastTime = -1, bannerUntil = -1;
    private int dotsFor = -1, dotsWave = -1;
    private (double Hp, double Max) shownHp = (-1, -1);
    private (BattlePhase, int, int, bool, bool, double, int) shownStatus;
    private (double, bool) shownBoss = (-1, false);
    private (bool, BattleSkill?, bool) shownHint;
    private (int, bool) shownCombo = (-1, false);
    private bool hintShown;
    private BattleSim? last;
    private bool lastHeld;

    private static Brush Frozen(byte a, byte r, byte g, byte b) => PetUi.Frozen(Color.FromArgb(a, r, g, b));
    private static readonly Brush Panel = MakePanel();
    private static Brush MakePanel()
    {
        var brush = new LinearGradientBrush(Color.FromArgb(0xF0, 0x34, 0x2E, 0x4E), Color.FromArgb(0xF0, 0x1E, 0x1B, 0x2E), 90);
        brush.Freeze(); return brush;
    }
    private static readonly Brush Rim = Frozen(0xC8, 255, 255, 255);
    private static readonly Brush BarBack = Frozen(0x90, 0x0E, 0x0C, 0x18);
    private static readonly Brush Green = Frozen(255, 0x6F, 0xE0, 0x6A), Yellow = Frozen(255, 0xF5, 0xC8, 0x42), Red = Frozen(255, 0xF2, 0x5C, 0x54);
    private static readonly Brush Trail = Frozen(255, 0xF0, 0xA0, 0x4B);
    private static readonly Brush BossColor = Frozen(255, 0xB4, 0x8C, 0xF5), BossAngryColor = Frozen(255, 0xE8, 0x6A, 0x6A);
    private static readonly Brush Soft = Frozen(255, 0xD8, 0xD6, 0xE8), Gold = Frozen(255, 0xFF, 0xD2, 0x3F), Dim = Frozen(0x70, 255, 255, 255);

    public BattleHud(string petName, PetRenderer portrait, Func<string, string, string> localize)
    {
        L = localize;
        Background = Panel; BorderBrush = Rim; BorderThickness = new Thickness(1.5);
        CornerRadius = new CornerRadius(12); Padding = new Thickness(10, 8, 10, 6); Margin = new Thickness(8);
        // No drop shadows here: on a transparent (layered) window every bitmap effect is redrawn in
        // software whenever anything under it changes, and the HUD changes every frame.
        SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        // Left: portrait, name and HP.
        portrait.Width = portrait.Height = 192;
        portrait.RenderTransform = new TransformGroup { Children = { new ScaleTransform(0.5, 0.5), new TranslateTransform(-21.5, -15) } };
        var face = new Border
        {
            Width = 54, Height = 54, CornerRadius = new CornerRadius(27), BorderBrush = Gold, BorderThickness = new Thickness(2.5),
            Background = Frozen(255, 0xF6, 0xEE, 0xE2), ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center,
            Child = new Canvas { Children = { portrait }, ClipToBounds = true }
        };
        face.Clip = new EllipseGeometry(new Point(27, 27), 25, 25);
        var name = new TextBlock { Text = petName, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13.5 };
        // The current hit chain (×1, ×2…), gold when the next hit is the combo. Its space is always
        // kept (Hidden, fixed width) so the HUD never changes width and re-centres mid-fight.
        comboText = new TextBlock { Foreground = Soft, FontWeight = FontWeights.Black, FontSize = 13.5, Width = 30, Margin = new Thickness(6, 0, 0, 0),
            Visibility = Visibility.Hidden };
        hpTrail = new Border { Background = Trail, CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left, Width = HpWidth };
        hpFill = new Border { Background = Green, CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left, Width = HpWidth };
        hpText = new TextBlock { Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center };
        var hpBar = new Border
        {
            Width = HpWidth + 4, Height = 16, CornerRadius = new CornerRadius(7), Background = BarBack, BorderBrush = Dim, BorderThickness = new Thickness(1),
            Padding = new Thickness(1), Margin = new Thickness(0, 4, 0, 0), Child = new Grid { Children = { hpTrail, hpFill, hpText } }
        };
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
            Children = { face, new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { new StackPanel { Orientation = Orientation.Horizontal, Children = { name, comboText } }, hpBar } } } };

        // Centre: the banner, or the status with wave dots / the boss's HP.
        banner = new TextBlock { Foreground = Gold, FontWeight = FontWeights.Black, FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed };
        status = new TextBlock { Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
        waveDots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };
        bossName = new TextBlock { Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center };
        bossFill = new Border { Background = BossColor, CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left, Width = BossWidth };
        bossBar = new Border
        {
            Width = BossWidth + 4, Height = 12, CornerRadius = new CornerRadius(5), Background = BarBack, BorderBrush = Gold, BorderThickness = new Thickness(1),
            Padding = new Thickness(1), Margin = new Thickness(0, 2, 0, 0), Child = bossFill
        };
        var bossPanel = new StackPanel { Children = { bossName, bossBar }, Margin = new Thickness(0, 3, 0, 0) };
        bossPanel.SetBinding(VisibilityProperty, new System.Windows.Data.Binding(nameof(Visibility)) { Source = bossBar });
        var centre = new Grid { Width = 230, Margin = new Thickness(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center,
            Children = { new StackPanel { Children = { status, waveDots, bossPanel } }, banner } };

        // Right: skills, then Pause / End.
        var bar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        for (int i = 0; i < slots.Length; i++)
        {
            var skill = SkillOrder[i];
            slots[i] = new SkillSlot(skill);
            slots[i].MouseLeftButtonDown += (_, e) => { SkillClicked?.Invoke(skill); e.Handled = true; };
            slots[i].MouseEnter += (_, _) => Hover(skill);
            slots[i].MouseLeave += (_, _) => { if (hovered == skill) Hover(null); };
            bar.Children.Add(slots[i]);
        }
        pauseGlyph = new HudGlyph(HudGlyphKind.Pause);
        pauseButton = SmallButton(pauseGlyph, () => PauseClicked?.Invoke());
        var end = endButton = SmallButton(new HudGlyph(HudGlyphKind.End), () => EndClicked?.Invoke());
        pauseButton.MouseEnter += (_, _) => HoverControl(L("Tạm dừng / tiếp tục trận", "Pause / resume the battle"));
        end.MouseEnter += (_, _) => HoverControl(L("Kết thúc trận (không có bảng kết quả)", "End the battle (no result panel)"));
        pauseButton.MouseLeave += (_, _) => HoverControl(null);
        end.MouseLeave += (_, _) => HoverControl(null);
        end.Margin = new Thickness(0, 4, 0, 0);
        var controls = new StackPanel { Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { pauseButton, end } };

        var top = new StackPanel { Orientation = Orientation.Horizontal, Children = { left, centre, bar, controls } };
        // As wide as the row above (never wider): a long description wraps instead of widening the
        // HUD, which would re-centre it and slide the slots out from under the mouse.
        hint = new TextBlock { Foreground = Soft, FontSize = 11.5, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0), MinHeight = 15 };
        hint.SetBinding(WidthProperty, new System.Windows.Data.Binding(nameof(ActualWidth)) { Source = top });
        Child = new StackPanel { Children = { top, hint } };
    }

    public SkillSlot Slot(BattleSkill skill) => slots[Array.IndexOf(SkillOrder, skill)];
    public TextBlock ComboText => comboText;
    public Border PauseButton => pauseButton;
    public Border EndButton => endButton;

    // For the HUD stepping aside (PetWindow.Battle.cs): points in this panel's coordinates.
    public bool IsInsidePanel(Point point) => point.X >= 0 && point.Y >= 0 && point.X < ActualWidth && point.Y < ActualHeight;

    public bool IsInteractiveAt(Point point)
    {
        bool Over(FrameworkElement element)
        {
            if (!element.IsVisible) return false;
            var at = TranslatePoint(point, element);
            return at.X >= 0 && at.Y >= 0 && at.X < element.ActualWidth && at.Y < element.ActualHeight;
        }
        foreach (var slot in slots) if (Over(slot)) return true;
        return Over(pauseButton) || Over(endButton);
    }
    public string HintText => hint.Text;

    // Hovering a slot (or a control) describes it in the hint line, one fixed place, instead of
    // a tooltip: WPF tooltips on a window that never activates keep showing the first one.
    private BattleSkill? hovered;
    private string? hoveredControl;
    public void Hover(BattleSkill? skill) { hovered = skill; hoveredControl = null; ShowHint(); }
    private void HoverControl(string? text) { hoveredControl = text; hovered = null; ShowHint(); }

    // The UI language, read through the shared localizer.
    private bool English => L("vi", "en") == "en";
    // Descriptions use the battle's own numbers (battle.json), so they always match.
    public string Describe(BattleSkill skill) => BattleSkills.Get(skill).Describe(last?.Settings ?? BattleData.Settings, English);
    private string SkillTip(BattleSkill skill) => Describe(skill);

    public void ShowBanner(string text, double now, double seconds = 1.6)
    {
        banner.Text = text;
        bannerUntil = now + seconds;
    }

    public void Update(BattleSim b, bool userPaused, bool fullscreen, bool held, bool away = false)
    {
        double dt = lastTime < 0 ? 0 : Math.Max(0, b.Time - lastTime);
        lastTime = b.Time;
        // HP: colour by amount; the orange trail of lost HP drains after it.
        double fraction = Math.Clamp(b.CatHp / Math.Max(1, b.Settings.CatMaxHp), 0, 1);
        trail = fraction >= trail ? fraction : trail + (fraction - trail) * Math.Min(1, dt * 2.5);
        Set(hpFill, HpWidth * fraction);
        Set(hpTrail, HpWidth * trail);
        var hpBrush = fraction > 0.5 ? Green : fraction > 0.25 ? Yellow : Red;
        if (hpFill.Background != hpBrush) hpFill.Background = hpBrush;
        var hp = (Math.Ceiling(b.CatHp), b.Settings.CatMaxHp);
        if (hp != shownHp) { shownHp = hp; hpText.Text = $"{hp.Item1} / {hp.Item2}"; }

        // Hit chain: shown from the first hit until it lapses or the combo lands.
        int chain = b.Over ? 0 : b.LiveCombo;
        var comboKey = (chain, chain > 0 && chain == b.Settings.ComboHits - 1);
        if (comboKey != shownCombo)
        {
            shownCombo = comboKey;
            comboText.Text = chain > 0 ? $"×{chain}" : "";
            comboText.Foreground = comboKey.Item2 ? Gold : Soft;
            comboText.Visibility = chain > 0 ? Visibility.Visible : Visibility.Hidden;
        }

        // Banner for a moment, otherwise the status line.
        bool showBanner = b.Time < bannerUntil;
        banner.Visibility = showBanner ? Visibility.Visible : Visibility.Collapsed;
        if (showBanner) banner.Opacity = Math.Clamp((bannerUntil - b.Time) / 0.4, 0, 1);
        int left = b.Alive + b.ToSpawn;
        var statusKey = (b.Phase, b.Wave, left, b.Paused, fullscreen, Math.Round(b.LastHeal), (b.IsBossWave(b.Wave) ? 1 : 0) + (away ? 2 : 0));
        if (statusKey != shownStatus)
        {
        shownStatus = statusKey;
        string text = b.Phase switch
        {
            BattlePhase.Won => L("Chiến thắng!", "Victory!"),
            BattlePhase.Lost => L("Thua rồi… mèo choáng váng", "Defeated… the cat sees stars"),
            BattlePhase.WaveBreak when b.Wave == 0 => L("Chuẩn bị!", "Get ready!"),
            BattlePhase.WaveBreak => L($"Xong đợt {b.Wave}!{HealNote(b)}", $"Wave {b.Wave} cleared!{HealNote(b)}"),
            _ when b.IsBossWave(b.Wave) => L($"Đánh bại {BossName(b)}!", $"Defeat the {BossName(b)}!"),
            _ => L($"Đợt {b.Wave}/{b.WaveCount} · còn {left} slime", $"Wave {b.Wave}/{b.WaveCount} · {left} slime{(left == 1 ? "" : "s")} left")
        };
        if (b.Paused && !b.Over) text += away ? L(" · Chờ mèo về màn hình này", " · Waiting for the cat to come back")
            : fullscreen ? L(" · Tạm dừng (toàn màn hình)", " · Paused (fullscreen)") : L(" · Tạm dừng", " · Paused");
        SetText(status, text);
        }
        status.Visibility = showBanner ? Visibility.Hidden : Visibility.Visible;
        UpdateDots(b);
        var boss = b.Boss;
        bossBar.Visibility = boss != null && boss.Targetable ? Visibility.Visible : Visibility.Collapsed;
        waveDots.Visibility = bossBar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if (boss != null)
        {
            Set(bossFill, BossWidth * Math.Clamp(boss.Hp / boss.MaxHp, 0, 1));
            var bossBrush = boss.Enraged ? BossAngryColor : BossColor;
            if (bossFill.Background != bossBrush) bossFill.Background = bossBrush;
            var bossKey = (Math.Ceiling(boss.Hp), boss.Enraged);
            if (bossKey != shownBoss)
            {
                shownBoss = bossKey;
                bossName.Text = boss.Type.Name(English) + (boss.Enraged ? L(" (nổi giận!)", " (enraged!)") : "") + $"  {bossKey.Item1}/{boss.MaxHp}";
            }
        }

        // Skills.
        foreach (var slot in slots)
        {
            var skill = slot.Skill;
            double cooldown = b.Cooldown(skill), total = b.Settings.SkillCooldown(skill);
            bool active = skill == BattleSkill.Cheer ? b.CheerRemaining > 0 : skill == BattleSkill.Shield && b.ShieldRemaining > 0;
            slot.Show(cooldown / Math.Max(0.01, total), cooldown, b.Armed == skill, !b.Over && !b.Paused, active);
        }
        pauseGlyph.Kind = userPaused ? HudGlyphKind.Play : HudGlyphKind.Pause;
        pauseButton.IsEnabled = !b.Over && !(b.Paused && fullscreen);
        pauseButton.Opacity = pauseButton.IsEnabled ? 1 : 0.45;
        last = b; lastHeld = held;
        var hintKey = (held && !b.Over, b.Armed, b.Over);
        if (hovered != null || hoveredControl != null || !hintShown || hintKey != shownHint) ShowHint();
    }

    private void ShowHint()
    {
        if (last is not BattleSim b) return;
        bool held = lastHeld;
        hintShown = true; shownHint = (held && !b.Over, b.Armed, b.Over);
        if (hoveredControl != null) { SetText(hint, hoveredControl); return; }
        if (hovered is BattleSkill skill)
        {
            double cooldown = b.Cooldown(skill);
            string state = cooldown > 0 ? L($" · còn {Math.Ceiling(cooldown)} giây", $" · ready in {Math.Ceiling(cooldown)} s") : L(" · sẵn sàng", " · ready");
            SetText(hint, SkillTip(skill) + state);
            return;
        }
        SetText(hint, held && !b.Over ? L("Mèo đang được bế: không ai đánh được mèo, quái bò theo chờ mèo xuống.", "The cat is lifted: nobody can hit it; slimes follow and wait for it to land.")
            : b.Armed == BattleSkill.Push ? L("Click nửa trái slime để đẩy sang phải, nửa phải để đẩy sang trái · bấm lại ô hoặc chuột phải để hủy",
                                             "Click a slime's left half to push it right, its right half to push it left · click the slot again or right-click to cancel")
            : b.Armed is BattleSkill armed ? L($"Click một slime để dùng {SkillName(armed)} · bấm lại ô hoặc chuột phải chiến trường để hủy",
                                               $"Click a slime to use {SkillName(armed)} · click the slot again or right-click the field to cancel")
            : L("Click slime: chọn mục tiêu · Rê chuột lên ô kỹ năng để xem · Nhấc mèo lên để cứu nguy",
                "Click a slime: set target · Hover a skill for details · Lift the cat to rescue it"));
    }

    public string SkillName(BattleSkill skill) => BattleSkills.Get(skill).Name(English);

    // The boss of the current wave (alive or still to come).
    private string BossName(BattleSim b)
    {
        if (b.Boss is { } boss) return boss.Type.Name(English);
        foreach (var group in b.Settings.WavePlan[Math.Clamp(b.Wave, 1, b.WaveCount) - 1].Groups)
            if (b.Settings.Enemy(group.Enemy) is { Boss: true } type) return type.Name(English);
        return "Boss";
    }

    private string HealNote(BattleSim b) => b.LastHeal >= 1 ? $" +{Math.Round(b.LastHeal)} HP" : "";

    // One dot per wave (filled: done, ringed: now), a small crown for the boss wave.
    private void UpdateDots(BattleSim b)
    {
        if (dotsFor == b.WaveCount && dotsWave == b.Wave) return;
        dotsFor = b.WaveCount; dotsWave = b.Wave;
        waveDots.Children.Clear();
        for (int wave = 1; wave <= b.WaveCount; wave++)
        {
            bool done = wave < b.Wave || (wave == b.Wave && b.Phase == BattlePhase.WaveBreak), now = wave == b.Wave && !done;
            FrameworkElement mark = b.IsBossWave(wave)
                ? new HudGlyph(HudGlyphKind.Crown) { Width = 18, Height = 14, Opacity = done || now ? 1 : 0.5 }
                : new Border { Width = 11, Height = 11, CornerRadius = new CornerRadius(6), Background = done ? Gold : BarBack,
                    BorderBrush = now ? Gold : Dim, BorderThickness = new Thickness(now ? 2.5 : 1.5), VerticalAlignment = VerticalAlignment.Center };
            mark.Margin = new Thickness(3, 0, 3, 0);
            waveDots.Children.Add(mark);
        }
    }

    private static Border SmallButton(UIElement content, Action click)
    {
        var button = new Border { Width = 28, Height = 24, CornerRadius = new CornerRadius(6), Background = Frozen(0x50, 255, 255, 255), Cursor = Cursors.Hand, Child = content };
        button.MouseLeftButtonDown += (_, e) => { if (button.IsEnabled) click(); e.Handled = true; };
        return button;
    }

    private static void Set(FrameworkElement element, double width) { width = Math.Max(0, Math.Round(width, 1)); if (element.Width != width) element.Width = width; }
    private static void SetText(TextBlock block, string text) { if (block.Text != text) block.Text = text; }
}

// One skill slot: icon, clock-wipe cooldown, seconds left; gold rim when ready, blue when chosen.
public sealed class SkillSlot : Border
{
    public BattleSkill Skill { get; }
    private readonly CooldownWipe wipe = new();
    private readonly TextBlock seconds = new() { Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 14,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private int shownSeconds = -1;
    private static readonly Brush Ready = PetUi.Frozen(Color.FromRgb(0xFF, 0xD2, 0x3F)), Waiting = PetUi.Frozen(Color.FromArgb(0x60, 255, 255, 255));
    private static readonly Brush Back = PetUi.Frozen(Color.FromArgb(0x55, 0x0E, 0x0C, 0x18)), ArmedBack = PetUi.Frozen(Color.FromArgb(0xC0, 0x2A, 0x78, 0xD6));
    private static readonly Brush ActiveBack = PetUi.Frozen(Color.FromArgb(0x90, 0xFF, 0xD2, 0x3F));
    public bool IsReady { get; private set; }
    public bool IsArmed { get; private set; }
    public double Remaining { get; private set; }

    public SkillSlot(BattleSkill skill)
    {
        Skill = skill;
        Width = Height = 46; CornerRadius = new CornerRadius(9); BorderThickness = new Thickness(2); Margin = new Thickness(3, 0, 3, 0);
        Cursor = Cursors.Hand; ClipToBounds = true; Background = Back; BorderBrush = Ready;
        Child = new Grid { Children = { new HudGlyph((HudGlyphKind)(int)skill) { Margin = new Thickness(6) }, wipe, seconds } };
    }

    public void Show(double fraction, double remaining, bool armed, bool usable, bool active)
    {
        Remaining = remaining; IsArmed = armed; IsReady = usable && remaining <= 0;
        wipe.Fraction = remaining > 0 ? fraction : 0;
        int whole = remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
        if (whole != shownSeconds) { shownSeconds = whole; seconds.Text = whole > 0 ? whole.ToString(CultureInfo.InvariantCulture) : ""; }
        var rim = armed ? Brushes.White : IsReady ? Ready : Waiting;
        if (BorderBrush != rim) BorderBrush = rim;
        var back = armed ? ArmedBack : active ? ActiveBack : Back;
        if (Background != back) Background = back;
        double opacity = usable || armed ? 1 : 0.55;
        if (Opacity != opacity) Opacity = opacity;
    }
}

// The dark clock-wipe over a cooling skill (Fraction 1 = just used, 0 = ready).
public sealed class CooldownWipe : FrameworkElement
{
    private static readonly Brush Shade = PetUi.Frozen(Color.FromArgb(0xB0, 0x0E, 0x0C, 0x18));
    private double fraction;
    public double Fraction
    {
        get => fraction;
        set { value = Math.Clamp(value, 0, 1); if (Math.Abs(value - fraction) < 0.004 && (value > 0) == (fraction > 0)) return; fraction = value; InvalidateVisual(); }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (fraction <= 0) return;
        double w = ActualWidth, h = ActualHeight, r = Math.Sqrt(w * w + h * h) / 2;
        var c = new Point(w / 2, h / 2);
        if (fraction >= 0.999) { dc.DrawRectangle(Shade, null, new Rect(0, 0, w, h)); return; }
        // The shaded part shrinks clockwise from 12 o'clock as the skill comes back.
        double start = -Math.PI / 2 + (1 - fraction) * 2 * Math.PI, end = -Math.PI / 2 + 2 * Math.PI;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(c, true, true);
            ctx.LineTo(new Point(c.X + r * Math.Cos(start), c.Y + r * Math.Sin(start)), false, false);
            ctx.ArcTo(new Point(c.X + r * Math.Cos(end), c.Y + r * Math.Sin(end)), new Size(r, r), 0, fraction > 0.5, SweepDirection.Clockwise, false, false);
        }
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        dc.DrawGeometry(Shade, null, g);
        dc.Pop();
    }
}

// Small vector icons for the HUD, in the cat's flat cartoon style (no font glyphs, no files).
public enum HudGlyphKind { Push, Heal, Cheer, Bomb, Shield, Pause, Play, End, Crown }

public sealed class HudGlyph : FrameworkElement
{
    private HudGlyphKind kind;
    public HudGlyphKind Kind { get => kind; set { if (kind != value) { kind = value; InvalidateVisual(); } } }
    public HudGlyph(HudGlyphKind kind) { this.kind = kind; IsHitTestVisible = false; }

    private static Pen P(Brush b, double t) { var p = new Pen(b, t) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; p.Freeze(); return p; }
    private static Geometry G(string path) { var g = Geometry.Parse(path); g.Freeze(); return g; }
    private static readonly Pen InkPen = P(PetUi.Frozen(Color.FromRgb(0x30, 0x2B, 0x48)), 1.6);
    private static readonly Pen WhiteBold = P(Brushes.White, 3.2);
    private static readonly Pen WhiteThin = P(Brushes.White, 1.6);
    private static readonly Pen YarnLine = P(PetUi.Frozen(Color.FromRgb(0xB0, 0x4A, 0x88)), 1.3);
    private static readonly Brush Salmon = PetUi.Frozen(Color.FromRgb(0xFA, 0x80, 0x72)), GoldFill = PetUi.Frozen(Color.FromRgb(0xFF, 0xD2, 0x3F));
    private static readonly Brush Pink = PetUi.Frozen(Color.FromRgb(0xE8, 0x7A, 0xB8)), Bubble = PetUi.Frozen(Color.FromArgb(0x90, 0x62, 0xD6, 0xFF));
    private static readonly Geometry Chevrons = G("M 4,5 L 11,12 L 4,19 M 12,5 L 19,12 L 12,19");
    private static readonly Geometry Fish = G("M 2,12 C 6,5 13,5 16,9 L 22,5 L 22,19 L 16,15 C 13,19 6,19 2,12 Z");
    private static readonly Geometry Bolt = G("M 14,1.5 L 5,14 L 11,14 L 9,22.5 L 19,9 L 13,9 Z");
    private static readonly Geometry Thread = G("M 17,7 C 20,3 22,6 23,3");
    private static readonly Geometry Shine = G("M 7,10 A 6,6 0 0 1 11,6");
    private static readonly Geometry CrownShape = G("M 3,19 L 3,8 L 8,13 L 12,5 L 16,13 L 21,8 L 21,19 Z");
    private static readonly Geometry Cross = G("M 7,7 L 17,17 M 17,7 L 7,17");
    private static readonly Geometry PlayShape = G("M 8,6 L 18,12 L 8,18 Z");

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        dc.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
        dc.PushTransform(new ScaleTransform(size / 24, size / 24));
        switch (kind)
        {
            case HudGlyphKind.Push: dc.DrawGeometry(null, WhiteBold, Chevrons); break;
            case HudGlyphKind.Heal:
                dc.DrawGeometry(Salmon, InkPen, Fish);
                dc.DrawEllipse(Brushes.Black, null, new Point(7, 11), 1.3, 1.3);
                break;
            case HudGlyphKind.Cheer: dc.DrawGeometry(GoldFill, InkPen, Bolt); break;
            case HudGlyphKind.Bomb:
                dc.DrawEllipse(Pink, InkPen, new Point(11, 13), 8.5, 8.5);
                dc.DrawLine(YarnLine, new Point(5, 10), new Point(16, 17));
                dc.DrawLine(YarnLine, new Point(5, 15), new Point(13, 6));
                dc.DrawLine(YarnLine, new Point(9, 21), new Point(18, 9));
                dc.DrawGeometry(null, YarnLine, Thread);
                break;
            case HudGlyphKind.Shield:
                dc.DrawEllipse(Bubble, WhiteThin, new Point(12, 12), 9.5, 9.5);
                dc.DrawGeometry(null, WhiteThin, Shine);
                break;
            case HudGlyphKind.Pause:
                dc.DrawLine(WhiteBold, new Point(9, 7), new Point(9, 17));
                dc.DrawLine(WhiteBold, new Point(15, 7), new Point(15, 17));
                break;
            case HudGlyphKind.Play: dc.DrawGeometry(Brushes.White, null, PlayShape); break;
            case HudGlyphKind.End: dc.DrawGeometry(null, WhiteBold, Cross); break;
            case HudGlyphKind.Crown: dc.DrawGeometry(GoldFill, InkPen, CrownShape); break;
        }
        dc.Pop(); dc.Pop();
    }
}
