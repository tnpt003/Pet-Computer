using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet;

// Battle Mode's hub: menu → Battle Mode opens it (when no battle runs). It shows this cat with its
// sword and one card per game mode, each with its picture and where the user is in it: Adventure
// (the chapter's stages won), Expedition (locked until the Slime King is won; a run under way;
// keepsakes) and Infinity (coming later, not playable). A card opens that mode's map; the maps'
// Close comes back here, the hub's Close goes back to the desktop. Its Rewards button shows what
// was won and the records in place of the cards (PetWindow.Rewards.cs). Like the maps it never takes
// focus, is used with the mouse only, holds the pet's free time (Quiet, no AI, games or sleep from
// boredom), hides while a fullscreen app runs and starts nothing by itself.
public sealed partial class PetWindow
{
    public const string HubAdventure = "adventure", HubExpedition = "expedition", HubInfinity = "infinity";
    private static readonly string[] HubModes = { HubAdventure, HubExpedition, HubInfinity };

    private bool battleHubOpen;
    private Window? battleHubWindow;
    private double hubRefresh;
    private TextBlock? hubTitle, hubSubtitle, hubStatus, hubCloseText;
    private readonly List<(string Mode, Border Card, ModeArt Art, Border Ribbon, TextBlock RibbonText, TextBlock Name, TextBlock Desc, TextBlock State, Border Bar, Border Fill)> hubCards = new();
    private static readonly Brush HubRibbon = PetUi.Hex("#E0302520");

    public bool BattleHubOpen => battleHubOpen;
    public bool BattleHubVisible => battleHubWindow?.IsVisible == true;
    public bool BattleHubModeEnabled(string mode) => hubCards.FirstOrDefault(c => c.Mode == mode).Card is { IsEnabled: true };
    // For checks: the texts shown now.
    // 1.5 M22, for checks: the scene on the hub's Adventure card (the latest open chapter's).
    public PetSceneKind? BattleHubAdventureScene => hubCards.FirstOrDefault(c => c.Mode == HubAdventure).Art?.Scene;
    public string BattleHubText => string.Join("\n", new[] { hubTitle, hubSubtitle, hubStatus, hubCloseText }
        .Where(t => t is { Visibility: Visibility.Visible } && t.Text.Length > 0).Select(t => t!.Text)
        .Concat(hubCards.SelectMany(c => new[] { c.Name.Text, c.Desc.Text, c.State.Text }.Concat(c.Ribbon.Visibility == Visibility.Visible ? new[] { c.RibbonText.Text } : Array.Empty<string>()))));

    // Null when the hub is open (opening it twice keeps the one hub), otherwise why not. It
    // replaces an open map, setup or result panel; a battle must end first.
    public string? OpenBattleHub()
    {
        if (BattlePrepRefusal() is string reason) return reason;
        ReloadBattleData();
        if (battlePreparing) { CloseBattlePrep(); CloseBattleTutorial(); }
        CloseAdventureMap(); CloseExpeditionMap(); CloseBattleSummary(); CloseBattleNotice();
        if (!battleHubOpen)
        {
            battleHubOpen = true;
            queuedCommand = null;
            revision++; // An AI reply chosen before the hub must not move the cat now.
            pet.Speech.Hide(); pet.Thought.Clear();
            ApplyQuiet();
            ShowBattleHubWindow();
            System.Diagnostics.Trace.WriteLine("Battle hub opened.");
        }
        UpdateBattleHub();
        return null;
    }

    // A mode's card: its map opens (the hub closes). Null on success, otherwise why not.
    public string? BattleHubChoose(string mode)
    {
        if (!battleHubOpen) return L("Sảnh chiến đấu chưa mở.", "The battle hub is not open.");
        return mode switch
        {
            HubAdventure => OpenAdventureMap(),
            HubExpedition => OpenExpeditionMap(),
            HubInfinity => EndlessRefusal() ?? OpenBattlePrep(forEndless: true),
            _ => L("Không có chế độ này.", "There is no such mode.")
        };
    }

    // The hub's Close: back to the pet's normal life.
    public void CancelBattleHub()
    {
        if (!battleHubOpen) return;
        CloseBattleHub();
        revision++;
        behavior.Reset(pet);
        System.Diagnostics.Trace.WriteLine("Battle hub closed.");
    }

    // A map's Close: back to the hub, or to the desktop when the hub cannot open now.
    private void BackToBattleHub()
    {
        if (OpenBattleHub() == null) return;
        revision++;
        behavior.Reset(pet);
    }

    // Removes the hub (choosing a mode, a battle starting, reset, closing the pet). Leaves the pet's state alone.
    private void CloseBattleHub()
    {
        bool was = battleHubOpen;
        battleHubOpen = false;
        battleHubWindow?.Close();
        battleHubWindow = null;
        hubCards.Clear();
        hubRewardsShown = false; hubRewards = null; hubCardsRow = null; hubRewardsText = null;
        hubBookShown = false; hubBook = null; hubBookText = null; hubBookButton = null; bookDetail = null;
        if (was) ApplyQuiet();
    }

    // Every frame, paused or not: a fullscreen app hides the hub (it comes back after).
    private void TickBattleHub(double dt)
    {
        if (!battleHubOpen || battleHubWindow == null) return;
        if (context.Fullscreen) { if (battleHubWindow.IsVisible) battleHubWindow.Hide(); return; }
        if (!battleHubWindow.IsVisible) { battleHubWindow.Show(); PlaceGamePanel(battleHubWindow); }
        hubRefresh += dt;
        if (hubRefresh < BattlePrepRefreshSeconds) return;
        hubRefresh = 0;
        UpdateBattleHub();
    }

    private void ShowBattleHubWindow()
    {
        // The header: this cat with its sword on a soft round stage, the title and a line under it.
        var cat = new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1, SwordOut = true,
            WeaponStyle = BattleTuning.Weapon(preferences.BattleWeapon).Style };
        var catView = new PetRenderer(cat) { Coat = renderer.Coat, Accessory = renderer.Accessory, Width = 192, Height = 192 };
        var stage = new Border { Background = PrepStage, CornerRadius = new CornerRadius(36), Width = 72, Height = 72, Margin = new Thickness(0, 0, 12, 0),
            BorderBrush = PrepCardRim, BorderThickness = new Thickness(1), Child = CatPicture(new Grid { Width = 192, Height = 192, Children = { catView } }) };
        hubTitle = PanelLine(PetUi.DisplaySize, true, 440);
        hubSubtitle = PanelLine(PetUi.SmallSize, maxWidth: 440); hubSubtitle.Foreground = PetUi.Game.Soft; hubSubtitle.Margin = new Thickness(0, 2, 0, 0);
        var heading = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { hubTitle, hubSubtitle } };
        var header = new DockPanel { Children = { stage, heading } };
        DockPanel.SetDock(stage, Dock.Left);

        // One card per mode: the picture on top (a ribbon over it when locked or coming soon), then
        // the name, what the mode is, where the user is in it and a thin progress bar.
        var cards = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        hubCards.Clear();
        foreach (var mode in HubModes)
        {
            var art = new ModeArt { Mode = mode, Height = 116 };
            var ribbonText = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.CaptionSize, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            var ribbon = new Border
            {
                Background = HubRibbon, CornerRadius = new CornerRadius(9), Padding = new Thickness(7, 2, 9, 3), Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
                {
                    new Path { Data = LockMark, Stroke = Brushes.White, StrokeThickness = 1.6, Width = 11, Height = 12, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center },
                    ribbonText
                } }
            };
            var name = PanelLine(PetUi.TitleSize, true, 148);
            var desc = PanelLine(PetUi.SmallSize, maxWidth: 148); desc.Foreground = PetUi.Game.Soft; desc.Margin = new Thickness(0, 3, 0, 0);
            var state = PanelLine(PetUi.CaptionSize, true, 148); state.Margin = new Thickness(0, 8, 0, 0);
            var fill = new Border { Background = mode == HubAdventure ? PetUi.Game.Gold : mode == HubExpedition ? PetUi.AccentBright : PetUi.Lilac, CornerRadius = new CornerRadius(2.5), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            var bar = new Border { Height = 5, Background = PrepBarBack, CornerRadius = new CornerRadius(2.5), Margin = new Thickness(0, 4, 0, 0), Child = fill };
            var card = new Border
            {
                Width = 172, Margin = new Thickness(0, 0, 10, 0), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1.5),
                Background = PrepCardBack, BorderBrush = PrepCardRim,
                Child = new StackPanel { Children =
                {
                    new Grid { Children = { art, ribbon } },
                    new StackPanel { Margin = new Thickness(11, 8, 11, 11), Children = { name, desc, state, bar } }
                } }
            };
            string chosen = mode;
            card.MouseLeftButtonDown += (_, e) => { if (card.IsEnabled && BattleHubChoose(chosen) is string reason) ShowBattleNotice(reason); e.Handled = true; };
            card.MouseEnter += (_, _) => { if (card.IsEnabled) card.BorderBrush = Brushes.White; };
            card.MouseLeave += (_, _) => card.BorderBrush = PrepCardRim;
            cards.Children.Add(card);
            hubCards.Add((mode, card, art, ribbon, ribbonText, name, desc, state, bar, fill));
        }
        if (cards.Children.Count > 0) ((Border)cards.Children[^1]).Margin = new Thickness(0);
        hubStatus = PanelLine(PetUi.SmallSize, true, 520); hubStatus.Foreground = PrepWarning; hubStatus.Margin = new Thickness(0, 10, 0, 0);
        // Rewards (PetWindow.Rewards.cs) take the cards' place; the button turns into ‹ Hub.
        hubCardsRow = cards;
        hubRewardsShown = false; hubRewardsLanguage = null;
        hubRewards = new StackPanel { Width = RewardsWidth, Margin = new Thickness(0, 14, 0, 0), Visibility = Visibility.Collapsed };
        var (rewards, rewardsText, _) = HudButton(() => ShowBattleRewards(!hubRewardsShown), plain: true);
        hubRewardsText = rewardsText;
        // 1.5 M27: the Monster Book (PetWindow.MonsterBook.cs) takes the cards' place the same way.
        hubBookShown = false; hubBookLanguage = null;
        hubBook = new StackPanel { Width = RewardsWidth, Margin = new Thickness(0, 14, 0, 0), Visibility = Visibility.Collapsed };
        var (book, bookText, _) = HudButton(() => ShowMonsterBook(!hubBookShown), plain: true);
        book.Margin = new Thickness(8, 0, 0, 0);
        hubBookButton = book; hubBookText = bookText; hubRewardsButton = rewards;

        var (close, closeText, _) = HudButton(CancelBattleHub, plain: true);
        close.Margin = new Thickness(0);
        hubCloseText = closeText;
        var window = CreateGamePanel(new StackPanel { Children = { header, cards, hubRewards, hubBook, hubStatus } }, PanelActions(new UIElement[] { rewards, book }, new UIElement[] { close }), "Pet battle hub");
        window.Closed += (_, _) => { if (battleHubWindow == window) battleHubWindow = null; };
        battleHubWindow = window;
        UpdateBattleHub();
        window.Show();
        PlaceGamePanel(window);
    }

    private void UpdateBattleHub()
    {
        if (!battleHubOpen || hubTitle == null || hubSubtitle == null || hubStatus == null) return;
        static void Show(TextBlock text, string? value)
        {
            if (text.Text != (value ?? "")) text.Text = value ?? "";
            text.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
        hubTitle.Text = hubRewardsShown ? L("Phần thưởng", "Rewards") : hubBookShown ? L("Sổ tay quái", "Monster Book") : L("Chế độ chiến đấu", "Battle Mode");
        hubSubtitle.Text = hubRewardsShown ? LF($"Những gì {preferences.Name} đã giành được", $"What {preferences.Name} has won")
            : hubBookShown ? LF($"Những quái {preferences.Name} đã gặp và cách hạ chúng", $"The enemies {preferences.Name} has met and how to beat them")
            : LF($"Hôm nay {preferences.Name} ra trận theo cách nào?", $"How will {preferences.Name} fight today?");
        string? refusal = BattlePrepRefusal();
        Show(hubStatus, refusal != null && !hubRewardsShown && !hubBookShown ? L("Chưa thể vào: ", "Not yet: ") + refusal : null);
        hubCloseText!.Text = L("Đóng", "Close");
        if (hubRewardsText != null) hubRewardsText.Text = hubRewardsShown ? L("‹ Sảnh", "‹ Hub") : RewardsButtonText();
        if (hubBookText != null) hubBookText.Text = hubBookShown ? L("‹ Sảnh", "‹ Hub") : BookButtonText();
        // One page at a time: the other page's button steps aside.
        if (hubRewardsButton != null) hubRewardsButton.Visibility = hubBookShown ? Visibility.Collapsed : Visibility.Visible;
        if (hubBookButton != null) hubBookButton.Visibility = hubRewardsShown ? Visibility.Collapsed : Visibility.Visible;
        RefreshRewards();
        RefreshMonsterBook();
        foreach (var (mode, card, art, ribbon, ribbonText, name, desc, state, bar, fill) in hubCards)
        {
            var (title, about, line, progress, open, ribbonLabel) = HubCard(mode);
            name.Text = title; desc.Text = about;
            Show(state, line);
            state.Foreground = open ? PrepBarFill : PetUi.Game.Soft;
            bar.Visibility = progress is double ? Visibility.Visible : Visibility.Collapsed;
            fill.Width = 150 * Math.Clamp(progress ?? 0, 0, 1);
            ribbon.Visibility = ribbonLabel != null ? Visibility.Visible : Visibility.Collapsed;
            ribbonText.Text = ribbonLabel ?? "";
            bool can = open && refusal == null;
            card.IsEnabled = can;
            card.Cursor = can ? System.Windows.Input.Cursors.Hand : null;
            card.Opacity = can ? 1 : open ? 0.6 : 0.85;
            art.Opacity = open ? 1 : 0.45;
            if (mode == HubAdventure) art.Scene = PetScene.ForChapter(Adventure?.CurrentChapter?.Scene);
            if (!can) card.BorderBrush = PrepCardRim;
        }
    }

    // What a mode's card says: its name, what it is, where the user is in it (and how far, 0–1),
    // whether it can be opened, and the ribbon over its picture (locked, coming soon).
    private (string Name, string About, string? State, double? Progress, bool Open, string? Ribbon) HubCard(string mode)
    {
        switch (mode)
        {
            case HubAdventure:
            {
                string title = L("Hướng dẫn phiêu lưu", "Adventure Guide");
                string about = L("Học chơi qua từng màn của chương, mang build theo và hạ trùm.", "Learn to play stage by stage, carry your build and beat the boss.");
                if (Adventure is not Adventure adventure || adventure.CurrentChapter is not ChapterSpec chapter)
                    return (title, about, L("Dữ liệu chưa có chương", "No chapter in the data"), null, false, L("Chưa có", "Not available"));
                int won = chapter.Stages.Count(s => adventure.IsCompleted(s.Id));
                // 1.5 M21: the latest open chapter, with its number when there is more than one.
                string number = adventure.Campaign.Chapters.Length > 1 ? LF($"Chương {adventure.ChapterIndex(chapter) + 1}: ", $"Chapter {adventure.ChapterIndex(chapter) + 1}: ") : "";
                string line = (won == chapter.Stages.Length ? "✓ " : "") + number + chapter.Name(preferences.Language) + LF($" · {won}/{chapter.Stages.Length} màn", $" · {won}/{chapter.Stages.Length} stages");
                return (title, about, line, chapter.Stages.Length == 0 ? 0 : (double)won / chapter.Stages.Length, true, null);
            }
            case HubExpedition:
            {
                string title = L("Viễn chinh", "Expedition");
                string about = L("Một chuyến nhiều bậc chọn đường: HP, thẻ và di vật giữ suốt chuyến.", "A run of branching steps: HP, cards and relics carry through it.");
                if (Expedition is not Expedition x) return (title, about, L("Dữ liệu chưa có viễn chinh", "No Expedition in the data"), null, false, L("Chưa có", "Not available"));
                if (!x.IsUnlocked(AdventureProgress)) return (title, about, L("Thắng Vua Slime ở Phiêu lưu để mở", "Win the Slime King in the Adventure to open"), null, false, L("Đang khóa", "Locked"));
                if (x.Run is ExpeditionRun run)
                    return (title, about, L("Đang có chuyến · ", "Run under way · ") + (BattleTuning.Expedition!.All.Count > 1 ? x.Spec.Name(preferences.Language) + " · " : "") + StepTitle(Math.Min(run.Step, run.Map.Count - 1), run.Map.Count) + $" · HP {Math.Ceiling(run.Hp)}/{x.MaxHp(run):0}",
                        run.Map.Count == 0 ? 0 : (double)run.Step / run.Map.Count, true, null);
                // The keepsakes are the first route's (1.5 M23: the others give none).
                var keepsakes = BattleTuning.Expedition!.Keepsakes;
                int had = keepsakes.Count(k => x.Progress.Keepsakes.Contains(k.Id));
                return (title, about, keepsakes.Length == 0 ? L("Sẵn sàng lên đường", "Ready to set out") : LF($"Kỷ vật {had}/{keepsakes.Length}", $"Keepsakes {had}/{keepsakes.Length}"),
                    keepsakes.Length == 0 ? null : (double)had / keepsakes.Length, true, null);
            }
            default:
                return EndlessCard(); // PetWindow.Endless.cs
        }
    }
}
