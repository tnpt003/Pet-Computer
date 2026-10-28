using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// Adventure upgrades between waves: when BattleSim offers cards, a small panel shows them (picture,
// name, short effect, how often it was taken); the user clicks one, then Confirm. While it is open
// the battle is paused for this reason only (BattlePause.Upgrade): confirming removes just that, so
// a user, menu or fullscreen pause stays. Like the other battle panels it never takes focus and is
// used with the mouse only; a fullscreen app hides it, the battle's end closes it.
public sealed partial class PetWindow
{
    private Window? upgradeWindow;
    // Where the offer was put (DIPs on the battle's monitor; the card lesson points at it).
    private Point upgradeAt;
    private string? upgradePick;
    // The card under the mouse (the Bag previews it, or else the picked one).
    private string? upgradeHover;
    private readonly List<(string Id, Border Card, TextBlock Name, TextBlock Desc, TextBlock Count, TextBlock Build)> upgradeCards = new();
    private TextBlock? upgradeTitle, upgradeConfirmText;
    private Border? upgradeConfirm;

    public bool BattleUpgradeOpen => upgradeWindow != null;
    public bool BattleUpgradeVisible => upgradeWindow?.IsVisible == true;
    public string? BattleUpgradePick => upgradePick;
    // The card the open Bag previews: the one under the mouse, otherwise the picked one.
    public string? BattleUpgradePreview => upgradeWindow == null ? null : upgradeHover ?? upgradePick;
    public bool BattleUpgradeConfirmEnabled => upgradeConfirm?.IsEnabled == true;
    public string BattleUpgradeText => string.Join("\n", new[] { upgradeTitle?.Text, upgradeConfirmText?.Text }
        .Concat(upgradeCards.SelectMany(c => new[] { c.Name.Text, c.Desc.Text, c.Count.Text, c.Build.Text })));

    // Each battle frame: show the panel while the battle offers cards, hide it for a fullscreen app.
    private void TickBattleUpgrade()
    {
        if (battle == null) return;
        bool choosing = battle.ChoosingUpgrade;
        SetBattlePause(BattlePause.Upgrade, choosing);
        if (!choosing) { CloseBattleUpgrade(); return; }
        if (upgradeWindow == null) ShowBattleUpgrade();
        if (context.Fullscreen) { if (upgradeWindow!.IsVisible) upgradeWindow.Hide(); }
        else if (!upgradeWindow!.IsVisible) { upgradeWindow.Show(); PlaceBattleUpgrade(); }
    }

    public bool PickBattleUpgrade(string id)
    {
        if (battle?.UpgradeOffer is not { } offer || offer.All(u => u.Id != id)) return false;
        upgradePick = id;
        UpdateBattleUpgrade();
        FillBag(force: false);
        return true;
    }

    // Confirm: the picked card goes into this run; the panel closes and only its pause reason goes.
    // A second click finds no offer and does nothing.
    public bool ConfirmBattleUpgrade()
    {
        if (battle == null || upgradePick == null || !battle.ChooseUpgrade(upgradePick, pet.X + settings.Width / 2)) return false;
        System.Diagnostics.Trace.WriteLine($"Battle upgrade chosen: {upgradePick}.");
        CloseBattleUpgrade();
        SetBattlePause(BattlePause.Upgrade, false);
        PlayBattleSound();
        ShowBattleBanners(); // A synergy switched on by this card.
        battle.ClearEvents();
        RequestBattleRedraw();
        UpdateBattleHud();
        return true;
    }

    private void CloseBattleUpgrade()
    {
        upgradeWindow?.Close();
        upgradeWindow = null;
        upgradePick = upgradeHover = null;
        upgradeConfirm = null;
        upgradeCards.Clear();
    }

    private void ShowBattleUpgrade()
    {
        if (battle?.UpgradeOffer is not { } offer) return;
        upgradePick = null;
        // The cards set the panel's width; a long title wraps instead of widening it (an empty strip right of the cards).
        upgradeTitle = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.HeadingSize, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap, MaxWidth = Math.Max(1, offer.Count) * UpgradeCardStep - 8 };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        upgradeCards.Clear();
        foreach (var upgrade in offer)
        {
            string id = upgrade.Id;
            var name = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.TitleSize, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
            var desc = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.SmallSize, Opacity = 0.9, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
            var count = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.CaptionSize, Opacity = 0.65, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            // The card's group and its progress toward the group's synergy (gold when it completes it).
            var build = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.CaptionSize, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            var card = new Border
            {
                Width = UpgradeCardStep - 8, MinHeight = 150, CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 10, 8, 8), Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = new StackPanel { Children = { new Border { Width = 86, Height = 86, CornerRadius = new CornerRadius(43), BorderThickness = new Thickness(2),
                    BorderBrush = upgrade.Group == "defense" ? PetUi.Mint : upgrade.Group == "speed" ? PetUi.Lilac : PetUi.AccentBright,
                    Background = PetUi.Game.Stage, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false,
                    Child = new UpgradeIcon { Effect = upgrade.Effect, Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }, name, desc, build, count } }
            };
            card.MouseLeftButtonDown += (_, e) => { PickBattleUpgrade(id); e.Handled = true; };
            card.MouseEnter += (_, _) => { upgradeHover = id; FillBag(force: false); };
            card.MouseLeave += (_, _) => { if (upgradeHover == id) { upgradeHover = null; FillBag(force: false); } };
            row.Children.Add(card);
            upgradeCards.Add((id, card, name, desc, count, build));
        }
        var (confirm, confirmText, _) = HudButton(() => ConfirmBattleUpgrade(), plain: true);
        confirm.Background = PrepChosen; confirm.Padding = new Thickness(18, 6, 18, 5); confirm.MinWidth = 110; confirm.Margin = new Thickness(0, 12, 8, 0);
        confirm.HorizontalAlignment = HorizontalAlignment.Right;
        confirmText.FontSize = PetUi.TitleSize; confirmText.FontWeight = FontWeights.Bold;
        upgradeConfirm = confirm; upgradeConfirmText = confirmText;
        var panel = new Border
        {
            Background = HudBack, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 8, 12), Margin = new Thickness(6),
            Child = new StackPanel { Children = { upgradeTitle, row, confirm } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
        };
        panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        var window = CreateOverlayWindow(panel, "Pet upgrade");
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.Closed += (_, _) => { if (upgradeWindow == window) upgradeWindow = null; };
        upgradeWindow = window;
        UpdateBattleUpgrade();
        if (!context.Fullscreen) { window.Show(); PlaceBattleUpgrade(); }
    }

    // Above the battle's floor strip, centred (where the guide appears).
    private void PlaceBattleUpgrade()
    {
        if (upgradeWindow == null) return;
        upgradeWindow.UpdateLayout();
        double width = upgradeWindow.ActualWidth, height = upgradeWindow.ActualHeight;
        double left = Math.Clamp((battleArea.Left + battleArea.Right - width) / 2, battleArea.Left, Math.Max(battleArea.Left, battleArea.Right - width));
        double top = Math.Max(battleArea.Top, battleArea.Bottom - BattleFieldView.FieldHeight - height - 12);
        MoveWindow(upgradeWindow, left, top, battleScale);
        upgradeAt = new Point(left, top);
    }

    private void UpdateBattleUpgrade()
    {
        if (upgradeTitle == null || upgradeConfirm == null || battle == null) return;
        upgradeTitle.Text = battleExpedition != null
            ? L("Chọn một nâng cấp (giữ suốt chuyến viễn chinh khi thắng bậc này)", "Pick an upgrade (kept for the rest of the Expedition when you win this step)")
            : battle.Endless ? L("Chọn một nâng cấp (giữ tới hết lượt Vô tận)", "Pick an upgrade (kept for the rest of this Infinity run)")
            : battle.Stage != null
            ? L("Chọn một nâng cấp (giữ sang màn sau khi thắng)", "Pick an upgrade (kept for the next stage when you win)")
            : L("Chọn một nâng cấp cho lượt này", "Pick an upgrade for this run");
        foreach (var (id, card, name, desc, count, build) in upgradeCards)
        {
            var spec = battle.Settings.UpgradeList.FirstOrDefault(u => u.Id == id);
            if (spec == null) continue;
            bool chosen = id == upgradePick;
            name.Text = (chosen ? "✓ " : "") + spec.Name(preferences.Language); // Chosen: a tick, not only a colour.
            desc.Text = spec.Describe(preferences.Language);
            int have = battle.Stacks(id);
            count.Text = spec.Rare ? L("★ Thẻ hiếm · một lần mỗi build", "★ Rare · once per build")
                : battle.StackLimit(spec) is int most && most > 0 ? LF($"Đã có {have}/{most}", $"Have {have}/{most}") : have > 0 ? LF($"Đã lấy {have} lần", $"Taken {have}×") : "";
            count.Foreground = spec.Rare ? RareGold : Brushes.White;
            count.Opacity = spec.Rare ? 1 : 0.65;
            build.Text = CardBuildText(battle, spec);
            build.Foreground = battle.Unlocks(spec) != null ? PrepWarning : Brushes.White;
            card.Background = chosen ? PetUi.Game.ChosenCard : spec.Rare ? PetUi.Game.RareBack : PrepCardBack;
            // A rare card keeps a gold rim (orange once chosen, like any card).
            card.BorderBrush = chosen ? PetUi.Game.ChosenRim : spec.Rare ? RareGold : PrepCardRim;
            card.BorderThickness = new Thickness(chosen ? 2.5 : spec.Rare ? 2 : 1);
        }
        upgradeConfirmText!.Text = L("Xác nhận", "Confirm");
        bool can = upgradePick != null;
        if (upgradeConfirm.IsEnabled != can) { upgradeConfirm.IsEnabled = can; upgradeConfirm.Opacity = can ? 1 : 0.5; }
    }

    // "Defense 1 → 2/3", "Defense 2 → 3/3 · Thorns!" or, for a card of no group, that it counts for none.
    private string CardBuildText(BattleSim b, UpgradeSpec card)
    {
        if (card.Group is not string group) return L("Không tính vào nhóm", "Counts for no group");
        int points = b.GroupPoints(group), goal = 0;
        foreach (var y in b.Settings.SynergyList) if (y.Group == group && b.CanUnlock(y) && !b.Synergies.Contains(y) && (goal == 0 || y.At < goal)) goal = y.At;
        string text = BuildGroups.Name(group, preferences.Language) + $" {points} → {points + 1}" + (goal > 0 ? $"/{goal}" : "");
        if (b.Unlocks(card) is SynergySpec synergy) text += LF($" · Mở {synergy.Name(PetLanguage.Vietnamese)}!", $" · {synergy.Name(preferences.Language)}!");
        return text;
    }

    private static readonly Brush RareGold = PetUi.Game.RareGold;
    // An upgrade card's width with the gap after it.
    private const double UpgradeCardStep = 140;

    // "Sharp edge ×2 · Quick feet" for the HUD and the result panel (empty without upgrades).
    private string UpgradeSummary(BattleSim b) => string.Join(" · ", b.Settings.UpgradeList.Where(u => b.Stacks(u.Id) > 0)
        .Select(u => u.Name(preferences.Language) + (b.Stacks(u.Id) > 1 ? $" ×{b.Stacks(u.Id)}" : "")));
}

// An upgrade card's picture, one simple vector drawing per effect.
public sealed class UpgradeIcon : FrameworkElement
{
    public string Effect { get; init; } = "damage";
    private static readonly Brush Back = PetUi.Frozen(Color.FromArgb(0x40, 255, 255, 255));
    private static readonly Brush Gold = PetUi.Frozen(Color.FromRgb(0xFF, 0xD3, 0x5C));
    private static readonly Brush Pink = PetUi.Frozen(Color.FromRgb(0xF4, 0x9A, 0x7A));
    private static readonly Pen Ink = PetUi.FrozenPen(PetUi.ArtInk, 2);
    private static readonly Pen Line = PetUi.FrozenPen(Brushes.White, 3);
    private static readonly Brush Green = PetUi.Frozen(Color.FromRgb(0x9A, 0xD8, 0x84)), Blue = PetUi.Frozen(Color.FromArgb(0xC0, 0x7C, 0xC0, 0xEE));
    // Path data is always written with '.' decimals, whatever the user's culture.
    private static Geometry Shape(FormattableString path) => PetUi.FrozenGeometry(FormattableString.Invariant(path));

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight, cx = w / 2, cy = h / 2, r = Math.Min(w, h) / 2;
        dc.DrawEllipse(Back, null, new Point(cx, cy), r, r);
        switch (Effect)
        {
            case "damage": // A blade with a gleam.
                dc.PushTransform(new TranslateTransform(cx - 10, cy + 17));
                PetRenderer.DrawWeaponIcon(dc, "sword");
                dc.Pop();
                break;
            case "moveSpeed": // Speed lines behind an arrow.
                for (int i = 0; i < 3; i++) dc.DrawLine(Line, new Point(cx - 16, cy - 9 + i * 9), new Point(cx - 4, cy - 9 + i * 9));
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx},{cy - 12} L {cx + 16},{cy} L {cx},{cy + 12} Z"));
                break;
            case "attackSpeed": // A clock with a short hand.
                dc.DrawEllipse(Gold, Ink, new Point(cx, cy), r * 0.55, r * 0.55);
                dc.DrawLine(Ink, new Point(cx, cy), new Point(cx, cy - r * 0.38));
                dc.DrawLine(Ink, new Point(cx, cy), new Point(cx + r * 0.28, cy + r * 0.1));
                break;
            case "knock": // A burst pushing an arrow away.
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx - 14},{cy - 5} L {cx + 4},{cy - 5} L {cx + 4},{cy - 12} L {cx + 16},{cy} L {cx + 4},{cy + 12} L {cx + 4},{cy + 5} L {cx - 14},{cy + 5} Z"));
                dc.DrawLine(Line, new Point(cx - 18, cy - 12), new Point(cx - 14, cy - 8));
                dc.DrawLine(Line, new Point(cx - 18, cy + 12), new Point(cx - 14, cy + 8));
                break;
            case "maxHp": // A heart.
                dc.DrawGeometry(Pink, Ink, Shape($"M {cx},{cy + 13} C {cx - 18},{cy + 1} {cx - 15},{cy - 14} {cx},{cy - 6} C {cx + 15},{cy - 14} {cx + 18},{cy + 1} {cx},{cy + 13} Z"));
                dc.DrawLine(Line, new Point(cx - 4, cy - 1), new Point(cx + 4, cy - 1));
                dc.DrawLine(Line, new Point(cx, cy - 5), new Point(cx, cy + 3));
                break;
            case "heal": // A fish.
                dc.DrawGeometry(Pink, Ink, Shape($"M {cx + 6},{cy} L {cx + 17},{cy - 8} L {cx + 17},{cy + 8} Z"));
                dc.DrawEllipse(Pink, Ink, new Point(cx - 4, cy), 12, 7.5);
                dc.DrawEllipse(Ink.Brush, null, new Point(cx - 10, cy - 1.5), 1.6, 1.6);
                break;
            case "shell": // A turtle shell: a dome with plates.
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx - 16},{cy + 7} C {cx - 15},{cy - 13} {cx + 15},{cy - 13} {cx + 16},{cy + 7} Z"));
                dc.DrawGeometry(null, Ink, Shape($"M {cx - 6},{cy + 7} L {cx - 4},{cy - 3} L {cx + 4},{cy - 3} L {cx + 6},{cy + 7} M {cx - 4},{cy - 3} L {cx - 8},{cy - 8} M {cx + 4},{cy - 3} L {cx + 8},{cy - 8}"));
                break;
            case "opener": // A blade striking a star.
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx},{cy - 15} L {cx + 4},{cy - 4} L {cx + 15},{cy} L {cx + 4},{cy + 4} L {cx},{cy + 15} L {cx - 4},{cy + 4} L {cx - 15},{cy} L {cx - 4},{cy - 4} Z"));
                dc.DrawLine(Line, new Point(cx - 13, cy + 13), new Point(cx + 13, cy - 13));
                break;
            case "momentum": // A runner's dust behind a swoosh.
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx - 16},{cy + 5} C {cx - 4},{cy + 6} {cx + 6},{cy - 1} {cx + 16},{cy - 15} C {cx + 13},{cy + 6} {cx + 2},{cy + 16} {cx - 16},{cy + 15} Z"));
                dc.DrawEllipse(Back, Ink, new Point(cx - 13, cy - 5), 3.5, 3.5);
                dc.DrawEllipse(Back, Ink, new Point(cx - 5, cy - 11), 2.5, 2.5);
                break;
            case "crit": // Three claw marks.
                for (int i = -1; i <= 1; i++)
                    dc.DrawGeometry(Gold, Ink, Shape($"M {cx - 9 + i * 7},{cy - 13} Q {cx - 2 + i * 7},{cy} {cx - 7 + i * 7},{cy + 14} Q {cx + 2 + i * 7},{cy + 1} {cx - 9 + i * 7},{cy - 13} Z"));
                break;
            case "execute": // A blade over a nearly empty HP bar.
                dc.DrawRoundedRectangle(Back, Ink, new Rect(cx - 15, cy + 5, 30, 8), 3, 3);
                dc.DrawRoundedRectangle(Pink, null, new Rect(cx - 14, cy + 6, 7, 6), 2, 2);
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx - 12},{cy + 1} L {cx + 10},{cy - 15} L {cx + 13},{cy - 12} L {cx - 9},{cy + 4} Z"));
                break;
            case "comboWindow": // Three hits on a stretched line.
                dc.DrawLine(Line, new Point(cx - 16, cy + 8), new Point(cx + 16, cy + 8));
                for (int i = 0; i < 3; i++) dc.DrawEllipse(i == 2 ? Pink : Gold, Ink, new Point(cx - 11 + i * 11, cy - 2 - i * 3), 4.5 + i, 4.5 + i);
                break;
            case "landingRush": // An arrow down onto the floor with speed lines.
                dc.DrawLine(Line, new Point(cx - 14, cy + 13), new Point(cx + 14, cy + 13));
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx - 5},{cy - 15} L {cx + 5},{cy - 15} L {cx + 5},{cy - 2} L {cx + 11},{cy - 2} L {cx},{cy + 10} L {cx - 11},{cy - 2} L {cx - 5},{cy - 2} Z"));
                dc.DrawLine(Line, new Point(cx - 15, cy - 10), new Point(cx - 15, cy + 2));
                dc.DrawLine(Line, new Point(cx + 15, cy - 10), new Point(cx + 15, cy + 2));
                break;
            case "regen": // A green leaf with a plus.
                dc.DrawGeometry(Green, Ink, Shape($"M {cx - 13},{cy + 12} C {cx - 14},{cy - 6} {cx},{cy - 15} {cx + 14},{cy - 14} C {cx + 13},{cy + 2} {cx + 2},{cy + 13} {cx - 13},{cy + 12} Z"));
                dc.DrawLine(Line, new Point(cx - 3, cy + 1), new Point(cx + 5, cy + 1));
                dc.DrawLine(Line, new Point(cx + 1, cy - 3), new Point(cx + 1, cy + 5));
                break;
            case "stunCut": // A dizzy star struck through.
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx},{cy - 13} L {cx + 4},{cy - 4} L {cx + 13},{cy - 3} L {cx + 6},{cy + 4} L {cx + 8},{cy + 13} L {cx},{cy + 8} L {cx - 8},{cy + 13} L {cx - 6},{cy + 4} L {cx - 13},{cy - 3} L {cx - 4},{cy - 4} Z"));
                dc.DrawLine(Line, new Point(cx - 14, cy + 14), new Point(cx + 14, cy - 14));
                break;
            case "skillCooldown": // An hourglass.
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx - 10},{cy - 14} L {cx + 10},{cy - 14} L {cx + 1},{cy} L {cx + 10},{cy + 14} L {cx - 10},{cy + 14} L {cx - 1},{cy} Z"));
                dc.DrawLine(Ink, new Point(cx - 12, cy - 14), new Point(cx + 12, cy - 14));
                dc.DrawLine(Ink, new Point(cx - 12, cy + 14), new Point(cx + 12, cy + 14));
                break;
            case "appCharge": // A battery filling up.
                dc.DrawRoundedRectangle(Back, Ink, new Rect(cx - 15, cy - 8, 27, 16), 3, 3);
                dc.DrawRectangle(Ink.Brush, null, new Rect(cx + 12, cy - 4, 3, 8));
                dc.DrawRoundedRectangle(Green, null, new Rect(cx - 13, cy - 6, 17, 12), 2, 2);
                break;
            case "killCharge": // A bolt.
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx + 3},{cy - 15} L {cx - 9},{cy + 2} L {cx - 1},{cy + 2} L {cx - 4},{cy + 15} L {cx + 9},{cy - 3} L {cx + 1},{cy - 3} Z"));
                break;
            case "bomb": // A big ball of yarn.
                dc.DrawEllipse(Pink, Ink, new Point(cx, cy + 1), 13, 13);
                dc.DrawLine(Line, new Point(cx - 9, cy - 5), new Point(cx + 6, cy + 10));
                dc.DrawLine(Line, new Point(cx - 10, cy + 5), new Point(cx + 3, cy - 10));
                break;
            case "shieldTime": // A bubble with a plus.
                dc.DrawEllipse(Blue, Ink, new Point(cx - 2, cy + 1), 12, 12);
                dc.DrawLine(Line, new Point(cx + 8, cy - 12), new Point(cx + 16, cy - 12));
                dc.DrawLine(Line, new Point(cx + 12, cy - 16), new Point(cx + 12, cy - 8));
                break;
            default: // armor: a shield.
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx},{cy - 15} L {cx + 13},{cy - 9} C {cx + 13},{cy + 5} {cx + 7},{cy + 12} {cx},{cy + 16} C {cx - 7},{cy + 12} {cx - 13},{cy + 5} {cx - 13},{cy - 9} Z"));
                break;
        }
    }
}

// A synergy's medal in the Bag: its picture on a round back (drawn on a 44×44 design and scaled),
// and a ring around it: Progress of the way to it (1: full), in Ring (null: no ring).
// Picked by the synergy's id; a synergy battle.json adds later falls back on its effect.
public sealed class SynergyIcon : FrameworkElement
{
    public string Id { get; init; } = "";
    // The synergy's effect, for a picture when its id has none.
    public string Fallback { get; init; } = "";
    public Brush? Ring { get; init; }
    public double Progress { get; init; }
    private static readonly Brush Back = PetUi.Frozen(Color.FromArgb(0x40, 255, 255, 255)), Track = PetUi.Frozen(Color.FromArgb(0x30, 255, 255, 255));
    private static readonly Brush Gold = PetUi.Frozen(Color.FromRgb(0xFF, 0xD3, 0x5C)), Pink = PetUi.Frozen(Color.FromRgb(0xF4, 0x9A, 0x7A));
    private static readonly Brush Cream = PetUi.Frozen(Color.FromRgb(0xFF, 0xF3, 0xE2)), Stone = PetUi.Frozen(Color.FromRgb(0xB8, 0xB4, 0xC8));
    private static readonly Brush Blue = PetUi.Frozen(Color.FromArgb(0xB0, 0x7C, 0xC0, 0xEE)), Green = PetUi.Frozen(Color.FromRgb(0x9A, 0xD8, 0x84));
    private static readonly Pen Ink = PetUi.FrozenPen(PetUi.ArtInk, 2);
    private static readonly Pen Line = PetUi.FrozenPen(Brushes.White, 3), Thin = PetUi.FrozenPen(Brushes.White, 2);
    private static readonly Pen TrackPen = PetUi.FrozenPen(Track, 3);
    private static readonly Pen WindInk = PetUi.FrozenPen(PetUi.ArtInk, 6.5), Wind = PetUi.FrozenPen(Gold, 3.5);
    private static Geometry Shape(FormattableString path) => PetUi.FrozenGeometry(FormattableString.Invariant(path));

    private string Picture => Id switch
    {
        "thorns" or "armor_break" or "afterimage" or "afterimage_plus" or "last_stand" or "fortress" or "shockwave" or "finisher" or "gale" => Id,
        _ => Fallback switch
        {
            "thorns" => "thorns", "armorBreak" => "armor_break", "echo" => "afterimage", "lastStand" => "last_stand", "autoShield" => "fortress",
            "comboStun" => "shockwave", "finisher" => "finisher", "rush" => "gale", _ => "star"
        }
    };

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double k = Math.Min(w, h) / 44;
        dc.PushTransform(new TranslateTransform((w - 44 * k) / 2, (h - 44 * k) / 2));
        dc.PushTransform(new ScaleTransform(k, k));
        const double cx = 22, cy = 22;
        dc.DrawEllipse(Back, null, new Point(cx, cy), 20, 20);
        switch (Picture)
        {
            case "thorns": // A shield with spikes all round.
                foreach (var (x, y, dx, dy) in new[] { (cx, cy - 14, 0.0, -1.0), (cx - 12, cy - 7, -1.0, -0.4), (cx + 12, cy - 7, 1.0, -0.4), (cx - 11, cy + 5, -1.0, 0.5), (cx + 11, cy + 5, 1.0, 0.5) })
                    dc.DrawGeometry(Cream, Ink, Shape($"M {x - dy * 4},{y + dx * 4} L {x + dx * 7},{y + dy * 7} L {x + dy * 4},{y - dx * 4} Z"));
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx},{cy - 13} L {cx + 11},{cy - 8} C {cx + 11},{cy + 4} {cx + 6},{cy + 10} {cx},{cy + 14} C {cx - 6},{cy + 10} {cx - 11},{cy + 4} {cx - 11},{cy - 8} Z"));
                break;
            case "armor_break": // A stone shield cracked by a blade.
                dc.DrawGeometry(Stone, Ink, Shape($"M {cx - 2},{cy - 14} L {cx + 11},{cy - 8} C {cx + 11},{cy + 4} {cx + 6},{cy + 11} {cx - 2},{cy + 15} C {cx - 9},{cy + 11} {cx - 14},{cy + 4} {cx - 14},{cy - 8} Z"));
                dc.DrawGeometry(null, Ink, Shape($"M {cx - 4},{cy - 13} L {cx},{cy - 5} L {cx - 5},{cy + 1} L {cx},{cy + 6} L {cx - 3},{cy + 14}"));
                dc.DrawLine(Line, new Point(cx - 15, cy + 13), new Point(cx + 15, cy - 13));
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx + 11},{cy - 17} L {cx + 17},{cy - 11} L {cx + 13},{cy - 9} L {cx + 9},{cy - 13} Z"));
                break;
            case "afterimage": // A sword and its fading echo.
            case "afterimage_plus": // Two echoes and a plus.
            {
                bool plus = Picture == "afterimage_plus";
                var copies = plus ? new[] { (-16.0, 0.35), (-9.0, 0.6), (-2.0, 1.0) } : new[] { (-14.0, 0.5), (-4.0, 1.0) };
                foreach (var (shift, opacity) in copies)
                {
                    dc.PushOpacity(opacity);
                    dc.PushTransform(new TranslateTransform(cx + shift, cy + 15));
                    PetRenderer.DrawWeaponIcon(dc, "sword");
                    dc.Pop();
                    dc.Pop();
                }
                if (plus)
                {
                    dc.DrawEllipse(Gold, Ink, new Point(cx + 13, cy - 13), 6.5, 6.5);
                    dc.DrawLine(Ink, new Point(cx + 10, cy - 13), new Point(cx + 16, cy - 13));
                    dc.DrawLine(Ink, new Point(cx + 13, cy - 16), new Point(cx + 13, cy - 10));
                }
                break;
            }
            case "last_stand": // A heart that keeps its flag up.
                dc.DrawLine(Ink, new Point(cx + 9, cy - 1), new Point(cx + 9, cy - 17));
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx + 9},{cy - 17} L {cx + 18},{cy - 14} L {cx + 9},{cy - 11} Z"));
                dc.DrawGeometry(Pink, Ink, Shape($"M {cx - 2},{cy + 14} C {cx - 19},{cy + 3} {cx - 16},{cy - 12} {cx - 2},{cy - 5} C {cx + 12},{cy - 12} {cx + 15},{cy + 3} {cx - 2},{cy + 14} Z"));
                dc.DrawLine(Line, new Point(cx - 9, cy - 2), new Point(cx - 6, cy - 5));
                break;
            case "fortress": // A tower inside a shield bubble.
                dc.DrawEllipse(Blue, null, new Point(cx, cy), 17, 17);
                dc.DrawGeometry(Cream, Ink, Shape($"M {cx - 9},{cy + 12} L {cx - 9},{cy - 9} L {cx - 5},{cy - 9} L {cx - 5},{cy - 5} L {cx - 2},{cy - 5} L {cx - 2},{cy - 9} L {cx + 2},{cy - 9} L {cx + 2},{cy - 5} L {cx + 5},{cy - 5} L {cx + 5},{cy - 9} L {cx + 9},{cy - 9} L {cx + 9},{cy + 12} Z"));
                dc.DrawGeometry(Ink.Brush, null, Shape($"M {cx - 3},{cy + 12} L {cx - 3},{cy + 5} C {cx - 3},{cy + 1} {cx + 3},{cy + 1} {cx + 3},{cy + 5} L {cx + 3},{cy + 12} Z"));
                dc.DrawEllipse(null, Thin, new Point(cx, cy), 17, 17);
                break;
            case "shockwave": // A star of a hit with rings running out.
                dc.DrawEllipse(null, Thin, new Point(cx, cy + 2), 17, 7);
                dc.DrawEllipse(null, Line, new Point(cx, cy + 2), 11, 4.5);
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx},{cy - 15} L {cx + 4},{cy - 6} L {cx + 13},{cy - 5} L {cx + 6},{cy + 1} L {cx + 8},{cy + 10} L {cx},{cy + 5} L {cx - 8},{cy + 10} L {cx - 6},{cy + 1} L {cx - 13},{cy - 5} L {cx - 4},{cy - 6} Z"));
                break;
            case "finisher": // An axe over a nearly empty HP bar.
                dc.DrawRoundedRectangle(Back, Ink, new Rect(cx - 15, cy + 7, 30, 8), 3, 3);
                dc.DrawRoundedRectangle(Pink, null, new Rect(cx - 14, cy + 8, 5, 6), 2, 2);
                dc.DrawLine(Ink, new Point(cx - 11, cy + 4), new Point(cx + 7, cy - 14));
                dc.DrawGeometry(Gold, Ink, Shape($"M {cx + 1},{cy - 15} C {cx + 10},{cy - 20} {cx + 18},{cy - 10} {cx + 13},{cy - 2} L {cx + 4},{cy - 8} Z"));
                break;
            case "gale": // A gust curling round with speed lines.
                foreach (var pen in new[] { WindInk, Wind })
                    dc.DrawGeometry(null, pen, Shape($"M {cx + 13},{cy - 8} C {cx + 4},{cy - 17} {cx - 14},{cy - 11} {cx - 12},{cy + 2} C {cx - 10},{cy + 12} {cx + 5},{cy + 12} {cx + 6},{cy + 3} C {cx + 7},{cy - 3} {cx - 2},{cy - 5} {cx - 3},{cy + 1}"));
                dc.DrawLine(Line, new Point(cx + 8, cy + 9), new Point(cx + 16, cy + 9));
                dc.DrawLine(Line, new Point(cx + 11, cy + 14), new Point(cx + 17, cy + 14));
                break;
            default: // Any other synergy: a star.
                dc.DrawGeometry(Green, Ink, Shape($"M {cx},{cy - 14} L {cx + 4},{cy - 5} L {cx + 13},{cy - 4} L {cx + 6},{cy + 2} L {cx + 8},{cy + 12} L {cx},{cy + 7} L {cx - 8},{cy + 12} L {cx - 6},{cy + 2} L {cx - 13},{cy - 4} L {cx - 4},{cy - 5} Z"));
                break;
        }
        if (Ring != null)
        {
            dc.DrawEllipse(null, TrackPen, new Point(cx, cy), 20.5, 20.5);
            double share = Math.Clamp(Progress, 0, 1);
            var ring = new Pen(Ring, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            if (share >= 0.999) dc.DrawEllipse(null, ring, new Point(cx, cy), 20.5, 20.5);
            else if (share > 0)
            {
                // Clockwise from the top.
                double end = share * 2 * Math.PI;
                var arc = new StreamGeometry();
                using (var g = arc.Open())
                {
                    g.BeginFigure(new Point(cx, cy - 20.5), false, false);
                    g.ArcTo(new Point(cx + Math.Sin(end) * 20.5, cy - Math.Cos(end) * 20.5), new Size(20.5, 20.5), 0, share > 0.5, SweepDirection.Clockwise, true, false);
                }
                dc.DrawGeometry(null, ring, arc);
            }
        }
        dc.Pop();
        dc.Pop();
    }
}
