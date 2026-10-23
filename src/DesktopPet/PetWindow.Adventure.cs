using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet;

// Adventure (docs/ADVENTURE-UPDATE.md): the Battle Mode hub's Adventure opens the chapter map; a
// stage opens the usual setup panel with that stage; Ready plays it with the battle engine; the
// result panel counts the run in the saved progress (PetAdventure.cs) and offers the next stage,
// the map or the desktop. One step owns the pet at a time: the map, the setup panel, the battle,
// or nothing (the result panel only waits for a choice). Like the setup panel the map never takes
// focus, holds the pet's free time (Quiet, no AI, games or sleep from boredom) and starts nothing.
public sealed partial class PetWindow
{
    // The saved progress file (null: kept in memory only, as for the UI checks' own pets).
    public string? AdventureProgressFile { get; set; }
    private AdventureProgress? adventureProgress;
    public AdventureProgress AdventureProgress => adventureProgress ??= AdventureProgressFile != null ? AdventureProgress.Load(AdventureProgressFile) : new();
    // For checks: forget the progress in memory so it is read from the file again (like a restart).
    public void ReloadAdventureProgress() => adventureProgress = null;
    // The Adventure in the data for the next battle (null when that data has no campaign).
    public Adventure? Adventure => BattleTuning.Campaign is CampaignSpec campaign ? new Adventure(campaign, AdventureProgress) : null;
    private Adventure? AdventureOf(BattleSettings settings) => settings.Campaign is CampaignSpec campaign ? new Adventure(campaign, AdventureProgress) : null;

    // The stage the setup panel prepares (null: a free battle) and the running battle's run id.
    private StageSpec? prepStage;
    private string battleRunId = "";
    // The last save of the progress failed (disk full, no access): the map and the result say so.
    private bool adventureSaveFailed;
    public bool AdventureSaveFailed => adventureSaveFailed;
    // Why progress is not being kept right now (null: it is).
    private string? ProgressWarning() =>
        AdventureProgress.ReadOnly ? L("Tiến trình là của bản mới hơn: chỉ đọc, lượt chơi ở bản này không được lưu.", "Your progress comes from a newer version: read only, runs in this version are not saved.")
        : adventureSaveFailed ? L("Không lưu được tiến trình (xem log); lượt vừa chơi có thể mất khi đóng app.", "Progress could not be saved (see the log); the last run may be lost when the app closes.")
        : null;
    public StageSpec? BattlePrepStage => prepStage;

    private bool adventureMapOpen;
    private Window? adventureMapWindow;
    private string? mapStageId;
    private double mapRefresh;
    private readonly List<(string Id, Border Node, TextBlock Number, Path Mark, TextBlock Label, Rectangle? Link)> mapNodes = new();
    private TextBlock? mapTitle, mapData, mapStageName, mapGoal, mapStatus, mapRecord, mapEnterText, mapCloseText, mapBagText;
    private Border? mapEnter;
    private static readonly Brush MapDone = PetUi.Game.Gold;
    private static readonly Brush MapDoneInk = PetUi.Game.GoldInk;
    private static readonly Brush MapLocked = PetUi.Game.Locked;
    private static readonly Geometry LockMark = PetUi.FrozenGeometry("M 2,7 L 12,7 L 12,14 L 2,14 Z M 4,7 L 4,5 A 3,3 0 0 1 10,5 L 10,7");

    public bool AdventureMapOpen => adventureMapOpen;
    public bool AdventureMapVisible => adventureMapWindow?.IsVisible == true;
    public string? AdventureSelectedStage => mapStageId;
    public bool AdventureEnterEnabled => mapEnter?.IsEnabled == true;
    public string AdventureMapText => string.Join("\n", new[] { mapTitle, mapData, mapStageName, mapGoal, mapStatus, mapRecord, mapEnterText, mapCloseText }
        .Where(t => t is { Visibility: Visibility.Visible }).Select(t => t!.Text).Concat(mapNodes.Select(n => n.Label.Text)));

    private string StageNumber(StageSpec stage) => Adventure?.Locate(stage.Id) is { } at ? (at.Index + 1).ToString() : "";
    private string StageTitle(StageSpec stage) => LF($"Màn {StageNumber(stage)} · ", $"Stage {StageNumber(stage)} · ") + stage.Name(preferences.Language);

    // Null when the map is open (opening it twice keeps the one map), otherwise why not.
    // It replaces an open setup panel or result panel; a battle must end first.
    public string? OpenAdventureMap(string? select = null)
    {
        if (BattlePrepRefusal() is string reason) return reason;
        ReloadBattleData();
        if (Adventure is not Adventure adventure || adventure.Chapter() is not ChapterSpec chapter)
            return L("Dữ liệu trận không có chương phiêu lưu.", "The battle data has no Adventure chapter.");
        if (battlePreparing) { CloseBattlePrep(); CloseBattleTutorial(); }
        CloseBattleHub(); CloseExpeditionMap(); CloseBattleSummary(); CloseBattleNotice();
        // A reward added to a stage won before it existed is given now (only with official data).
        if (!BattleDataCustom && adventure.GrantRewards().Count > 0 && AdventureProgressFile != null) adventureSaveFailed = !AdventureProgress.Save(AdventureProgressFile);
        mapStageId = adventure.Stage(select) is StageSpec chosen ? chosen.Id
            : (chapter.Stages.FirstOrDefault(s => adventure.IsUnlocked(s.Id) && !adventure.IsCompleted(s.Id)) ?? chapter.Stages.Last(s => adventure.IsUnlocked(s.Id))).Id;
        if (!adventureMapOpen)
        {
            adventureMapOpen = true;
            queuedCommand = null;
            revision++; // An AI reply chosen before the map must not move the cat now.
            pet.Speech.Hide(); pet.Thought.Clear();
            ApplyQuiet();
            ShowAdventureMapWindow(chapter);
            System.Diagnostics.Trace.WriteLine("Adventure map opened.");
        }
        UpdateAdventureMap();
        return null;
    }

    public bool AdventureSelectStage(string id)
    {
        if (!adventureMapOpen || Adventure?.Stage(id) == null) return false;
        mapStageId = id;
        UpdateAdventureMap();
        return true;
    }

    // Enter: the setup panel opens for the chosen stage (nothing fights yet). Null on success.
    public string? AdventureEnterStage(string? id = null)
    {
        if (!adventureMapOpen) return L("Bản đồ chưa mở.", "The map is not open.");
        var adventure = Adventure;
        if (adventure?.Stage(id ?? mapStageId) is not StageSpec stage) return L("Không có màn này.", "There is no such stage.");
        if (!adventure.IsUnlocked(stage.Id)) return L("Màn này chưa mở.", "This stage is still locked.");
        return OpenBattlePrep(stage: stage);
    }

    // The map's Close: back to the battle hub (or the pet's normal life when the hub cannot open).
    public void CancelAdventureMap()
    {
        if (!adventureMapOpen) return;
        CloseAdventureMap();
        System.Diagnostics.Trace.WriteLine("Adventure map closed.");
        BackToBattleHub();
    }

    // Removes the map (entering a stage, reset, closing the pet). Leaves the pet's state alone.
    private void CloseAdventureMap()
    {
        bool was = adventureMapOpen;
        adventureMapOpen = false;
        adventureMapWindow?.Close();
        adventureMapWindow = null;
        mapEnter = null;
        mapNodes.Clear();
        if (was) ApplyQuiet();
    }

    // Every frame, paused or not: a fullscreen app hides the map (it comes back after).
    private void TickAdventureMap(double dt)
    {
        if (!adventureMapOpen || adventureMapWindow == null) return;
        if (context.Fullscreen) { if (adventureMapWindow.IsVisible) adventureMapWindow.Hide(); return; }
        if (!adventureMapWindow.IsVisible) { adventureMapWindow.Show(); PlaceAdventureMap(); }
        mapRefresh += dt;
        if (mapRefresh < BattlePrepRefreshSeconds) return;
        mapRefresh = 0;
        UpdateAdventureMap();
    }

    private void ShowAdventureMapWindow(ChapterSpec chapter)
    {
        static TextBlock Line(double size, bool bold = false) => PanelLine(size, bold, 420);
        mapTitle = Line(PetUi.HeadingSize, true);
        mapData = Line(PetUi.SmallSize); mapData.Foreground = PrepWarning; mapData.Margin = new Thickness(0, 6, 0, 0);
        // The stages in order, joined by a path: gold when won, orange when open, faint with a lock when not yet.
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        mapNodes.Clear();
        for (int i = 0; i < chapter.Stages.Length; i++)
        {
            string id = chapter.Stages[i].Id;
            Rectangle? link = null;
            if (i > 0)
            {
                link = new Rectangle { Width = 24, Height = 4, RadiusX = 2, RadiusY = 2, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 19, 0, 0) };
                row.Children.Add(link);
            }
            var number = new TextBlock { FontSize = PetUi.HeadingSize, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var mark = new Path { StrokeThickness = 2.2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
                Width = 15, Height = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var node = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(21), HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand, Child = new Grid { Children = { number, mark } } };
            var label = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.CaptionSize, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                Width = 76, Margin = new Thickness(0, 4, 0, 0) };
            var column = new StackPanel { Width = 76, Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, Children = { node, label } };
            column.MouseLeftButtonDown += (_, e) => { AdventureSelectStage(id); e.Handled = true; };
            row.Children.Add(column);
            mapNodes.Add((id, node, number, mark, label, link));
        }
        mapStageName = Line(PetUi.TitleSize, true); mapStageName.Margin = new Thickness(0, 12, 0, 0);
        mapGoal = Line(PetUi.BodySize); mapGoal.Margin = new Thickness(0, 3, 0, 0);
        mapStatus = Line(PetUi.SmallSize, true); mapStatus.Margin = new Thickness(0, 5, 0, 0);
        mapRecord = Line(PetUi.SmallSize); mapRecord.Opacity = 0.8; mapRecord.Margin = new Thickness(0, 2, 0, 0);
        var details = DetailCard(new Thickness(11, 0, 11, 9), mapStageName, mapGoal, mapStatus, mapRecord);
        var (close, closeText, _) = HudButton(CancelAdventureMap, plain: true);
        // The Bag (PetWindow.Inventory.cs): the build carried into the chosen stage.
        var (bag, bagText, _) = HudButton(() => { if (BattleBagOpen) CloseBattleBag(); else if (OpenBattleBag() is string reason) ShowBattleNotice(reason); }, plain: true);
        mapBagText = bagText;
        var (enter, enterText) = PrimaryButton(() => { if (AdventureEnterStage() is string reason) ShowBattleNotice(reason); });
        mapEnter = enter; mapEnterText = enterText; mapCloseText = closeText;
        var window = CreateGamePanel(new StackPanel { Children = { PanelTitle(HubAdventure, mapTitle), mapData, row, details } },
            PanelActions(new UIElement[] { close, bag }, new UIElement[] { enter }), "Pet adventure", minWidth: 440);
        window.Closed += (_, _) => { if (adventureMapWindow == window) adventureMapWindow = null; };
        adventureMapWindow = window;
        UpdateAdventureMap();
        window.Show();
        PlaceAdventureMap();
    }

    private void PlaceAdventureMap() => PlaceGamePanel(adventureMapWindow);

    private void UpdateAdventureMap()
    {
        if (!adventureMapOpen || mapTitle == null || mapEnter == null) return;
        var adventure = Adventure;
        if (adventure?.Chapter() is not ChapterSpec chapter) return;
        static void Show(TextBlock? text, string? value)
        {
            if (text == null) return;
            if (text.Text != (value ?? "")) text.Text = value ?? "";
            text.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
        mapTitle.Text = L("Phiêu lưu — ", "Adventure — ") + chapter.Name(preferences.Language);
        string? dataNote = BattleDataError != null
            ? L("File battle.json của bạn có lỗi nên dùng dữ liệu gốc: ", "Your battle.json has a mistake, so the built-in battle is used: ") + BattleDataError
            : BattleDataCustom ? L("Đang dùng battle.json tùy chỉnh: chơi được nhưng không lưu tiến trình và kỷ lục.", "Using your custom battle.json: you can play, but progress and records are not saved.") : null;
        Show(mapData, string.Join("\n", new[] { dataNote, ProgressWarning() }.Where(t => t != null)));
        foreach (var (id, node, number, mark, label, link) in mapNodes)
        {
            var stage = adventure.Stage(id);
            if (stage == null) continue;
            bool done = adventure.IsCompleted(id), open = adventure.IsUnlocked(id), selected = id == mapStageId;
            node.Background = done ? MapDone : open ? PrepChosen : MapLocked;
            node.BorderBrush = selected ? Brushes.White : PrepCardRim;
            node.BorderThickness = new Thickness(selected ? 3 : 1);
            number.Text = StageNumber(stage);
            number.Foreground = done ? MapDoneInk : Brushes.White;
            number.Visibility = open && !done ? Visibility.Visible : Visibility.Collapsed;
            // Won: a tick; locked: a padlock (not only a colour).
            mark.Data = done ? CheckMark : LockMark;
            mark.Stroke = done ? MapDoneInk : Brushes.White;
            mark.Visibility = done || !open ? Visibility.Visible : Visibility.Collapsed;
            label.Text = stage.Name(preferences.Language);
            label.Opacity = open ? 1 : 0.6;
            label.FontWeight = selected ? FontWeights.Bold : FontWeights.Normal;
            if (link != null)
            {
                var before = adventure.Locate(id) is { Index: > 0 } at ? at.Chapter.Stages[at.Index - 1] : null;
                link.Fill = before != null && adventure.IsCompleted(before.Id) ? MapDone : MapLocked;
            }
        }
        if (adventure.Stage(mapStageId) is not StageSpec chosen) return;
        bool unlocked = adventure.IsUnlocked(chosen.Id);
        mapStageName!.Text = StageTitle(chosen);
        Show(mapGoal, chosen.Goal(preferences.Language));
        var previous = adventure.Locate(chosen.Id) is { Index: > 0 } where ? where.Chapter.Stages[where.Index - 1] : null;
        Show(mapStatus, adventure.IsCompleted(chosen.Id) ? L("Đã hoàn thành", "Completed") + StageRules(chosen)
            : unlocked ? L("Đã mở · ", "Open · ") + ObjectiveText(chosen) + StageRules(chosen)
            : LF($"Chưa mở — thắng màn {StageNumber(previous!)} ({previous!.Name(PetLanguage.Vietnamese)}) trước", $"Locked — win stage {StageNumber(previous!)} ({previous!.Name(preferences.Language)}) first"));
        mapStatus!.Foreground = unlocked ? Brushes.White : PrepWarning;
        string? record = StageRecordLine(adventure, chosen);
        string? prize = chosen.Reward is string reward && PetAccessories.Find(reward) is { } item
            ? L("Phần thưởng: ", "Reward: ") + item.Name(preferences.Language) + (adventure.HasReward(reward) ? L(" ✓ đã nhận", " ✓ received") : "") : null;
        Show(mapRecord, string.Join("\n", new[] { record, prize }.Where(t => t != null)));
        mapEnterText!.Text = L("Vào màn", "Play stage");
        mapCloseText!.Text = L("‹ Sảnh", "‹ Hub");
        if (mapBagText != null) mapBagText.Text = L("Túi đồ", "Bag");
        bool can = unlocked && BattlePrepRefusal() == null;
        if (mapEnter.IsEnabled != can) { mapEnter.IsEnabled = can; mapEnter.Opacity = can ? 1 : 0.5; }
    }

    // "survive 75 s" or "3 waves"; " · upgrades between waves" when the stage offers them.
    private string ObjectiveText(StageSpec stage) => stage.Survival
        ? LF($"sống sót {stage.SurviveSeconds:0} giây", $"survive {stage.SurviveSeconds:0} s")
        : LF($"{stage.Waves.Length} đợt", $"{stage.Waves.Length} wave{(stage.Waves.Length == 1 ? "" : "s")}");
    private string StageRules(StageSpec stage) => stage.Upgrades ? L(" · có nâng cấp giữa các đợt", " · upgrades between waves") : "";

    // Wins and the fastest win of a stage over every loadout (the result panel shows per loadout).
    private string? StageRecordLine(Adventure adventure, StageSpec stage)
    {
        var records = adventure.Progress.Records.Where(pair => pair.Key.StartsWith(stage.Id + "|r" + stage.RulesVersion + "|", StringComparison.Ordinal)).Select(pair => pair.Value).ToList();
        int wins = records.Sum(r => r.Wins);
        if (wins == 0) return null;
        double? best = records.Where(r => r.BestSeconds != null).Select(r => r.BestSeconds).Min();
        string fastest = best is double s ? LF($" · nhanh nhất {Clock(s)}", $" · fastest {Clock(s)}") : "";
        return LF($"Đã thắng {wins} lần{fastest}", $"Won {wins} time{(wins == 1 ? "" : "s")}{fastest}");
    }

    // A stage battle just started: its name as a banner, and its tip the first time.
    private void StageBattleStarted(StageSpec stage)
    {
        // An Expedition encounter: its step and name as the banner (the HUD's name line already says
        // Expedition; encounters have no tips).
        if (battleExpedition is ExpeditionBattle x) { battleHud?.ShowBanner(StepTitle(x.Node.Step, x.Expedition.Spec.StepCount) + " · " + stage.Name(preferences.Language), 0, 1.4); return; }
        if (stage.Endless) { battleHud?.ShowBanner(stage.Name(preferences.Language), 0, 1.4); return; }
        battleHud?.ShowBanner(StageTitle(stage), 0, 1.4);
        if (!AdventureProgress.Seen.Contains(stage.Id) && stage.Tip(preferences.Language) is { Length: > 0 }) ShowStageTip(stage);
    }

    // End (HUD or menu): the battle stops with nothing counted. A stage run goes back to the map
    // (the free battle back to the desktop, as before); an Expedition step is left (RetreatCost)
    // and goes back to the Expedition map.
    public void EndBattleByUser()
    {
        // Infinity: End finishes the run at the wave reached (its result and record follow).
        if (battle?.Surrender() == true) return;
        var stage = battle?.Stage;
        var expedition = battleExpedition;
        EndBattle();
        if (expedition != null) { LeaveExpeditionBattle(expedition); return; }
        if (stage != null && OpenAdventureMap(stage.Id) is string reason) ShowBattleNotice(reason);
    }

    // Reward accessories offered in Settings: the received ones (and one already worn).
    public bool AccessoryAvailable(string id) =>
        PetAccessories.Find(id) is not { Reward: not null } || AdventureProgress.Rewards.Contains(id) || preferences.Accessory == id || KeepsakeAccessory(id);

    // A finished stage run: counted once in the progress (saved at once), never with custom data.
    private AdventureOutcome CountStageRun(BattleSim finished, bool won)
    {
        if (finished.Stage is not StageSpec stage || AdventureOf(finished.Settings) is not Adventure adventure) return default;
        var outcome = adventure.Apply(battleRunId, stage, !battleCustomData, won, finished.Weapon.Id, finished.Difficulty, finished.Defeated, finished.FightSeconds, finished.Build());
        if (outcome.Counted && AdventureProgressFile != null) adventureSaveFailed = !AdventureProgress.Save(AdventureProgressFile);
        if (outcome.Counted) System.Diagnostics.Trace.WriteLine($"Adventure stage {stage.Id} {(won ? "won" : "lost")}{(outcome.Unlocked != null ? $", {outcome.Unlocked.Id} opened" : "")}.");
        return outcome;
    }

    // The result panel of a stage: what the run changed, and its choices. The main button is
    // Next stage after a win (when there is one), otherwise Play again; the rest sit below it.
    private (UIElement Lines, UIElement Actions) StageResult(BattleSummary result, StageSpec stage, Action<Action> choose)
    {
        var adventure = AdventureOf(BattleTuning);
        TextBlock Line(double size, Brush? brush = null, bool bold = false, double opacity = 1) => new()
        {
            Foreground = brush ?? Brushes.White, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Opacity = opacity,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 300, Margin = new Thickness(0, 2, 0, 0)
        };
        var lines = new StackPanel();
        var next = adventure?.Next(stage.Id);
        if (result.Ranked && ProgressWarning() is string warning)
        {
            var unsaved = Line(PetUi.SmallSize, PrepWarning); unsaved.Text = warning;
            lines.Children.Add(unsaved);
        }
        if (!result.Ranked)
        {
            var note = Line(PetUi.SmallSize, PrepWarning); note.Text = L("Dữ liệu tùy chỉnh: lượt này không lưu tiến trình và kỷ lục.", "Custom data: this run saves no progress or records.");
            lines.Children.Add(note);
        }
        else if (result.Outcome.Unlocked is StageSpec opened)
        {
            var open = Line(PetUi.BodySize, RecordGold, true); open.Text = LF($"Đã mở: {StageTitle(opened)}", $"Unlocked: {StageTitle(opened)}");
            lines.Children.Add(open);
        }
        else if (result.Won && result.Outcome.FirstClear && next == null)
        {
            var done = Line(PetUi.BodySize, RecordGold, true); done.Text = L("Hoàn thành chương!", "Chapter complete!");
            lines.Children.Add(done);
        }
        foreach (var reward in result.Outcome.Rewards ?? Array.Empty<string>())
            if (PetAccessories.Find(reward) is { } item)
            {
                var prize = Line(PetUi.BodySize, RecordGold, true);
                prize.Text = LF($"Phần thưởng mới: {item.Name(PetLanguage.Vietnamese)} — đội trong Cài đặt → Mèo", $"New reward: {item.Name(preferences.Language)} — wear it in Settings → Cat");
                lines.Children.Add(prize);
            }
        if (result.Upgrades.Length > 0)
        {
            var taken = Line(PetUi.SmallSize, opacity: 0.85); taken.Text = L("Nâng cấp lượt này: ", "This run's upgrades: ") + result.Upgrades;
            lines.Children.Add(taken);
        }
        if (result.News.Any)
        {
            var news = Line(PetUi.BodySize, RecordGold, true);
            news.Text = result.News.Fastest ? L("Kỷ lục mới của màn: thắng nhanh nhất!", "New stage record: fastest win!") : L("Kỷ lục mới của màn: hạ nhiều slime nhất!", "New stage record: most slimes!");
            lines.Children.Add(news);
        }
        if (result.Ranked && adventure?.Record(stage, result.Weapon, result.Difficulty) is { Empty: false } best)
        {
            var records = Line(PetUi.SmallSize, opacity: 0.85); records.Text = L("Kỷ lục màn (trang bị này): ", "Stage records (this gear): ") + RecordLine(best);
            lines.Children.Add(records);
        }

        bool canNext = result.Won && next != null && adventure!.IsUnlocked(next.Id);
        var (main, mainText, _) = HudButton(() => choose(() =>
        {
            string? reason = canNext ? OpenBattlePrep(result.Weapon, result.Difficulty, next) : BattleReplay(result);
            if (reason != null) ShowBattleNotice(reason);
        }), plain: true);
        main.Background = ResultMain; main.Padding = new Thickness(16, 6, 16, 5); main.Margin = new Thickness(0); main.HorizontalAlignment = HorizontalAlignment.Right;
        mainText.FontSize = PetUi.TitleSize; mainText.FontWeight = FontWeights.Bold; mainText.Text = canNext ? L("Màn tiếp", "Next stage") : L("Chơi lại", "Play again");
        var others = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        if (canNext)
        {
            var (again, againText, _) = HudButton(() => choose(() => { if (BattleReplay(result) is string reason) ShowBattleNotice(reason); }), plain: true);
            againText.Text = L("Chơi lại", "Play again");
            others.Children.Add(again);
        }
        var (map, mapText, _) = HudButton(() => choose(() => { if (OpenAdventureMap(stage.Id) is string reason) ShowBattleNotice(reason); }), plain: true);
        mapText.Text = L("Bản đồ", "Map");
        var (home, homeText, _) = HudButton(() => choose(() => { }), plain: true);
        homeText.Text = L("Về desktop", "Back to desktop");
        home.Margin = new Thickness(0);
        others.Children.Add(map); others.Children.Add(home);
        return (lines, new StackPanel { Margin = new Thickness(0, 12, 0, 0), Children = { main, others } });
    }
}
