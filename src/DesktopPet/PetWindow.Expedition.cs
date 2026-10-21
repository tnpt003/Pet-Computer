using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet;

// Expedition (docs/EXPEDITION-DIRECTION.md, milestones 2–3): menu → Battle Mode → Expedition opens its
// map once the Adventure's Slime King is won. Without a run the map chooses the weapon and difficulty
// for the whole run; with one it shows the steps (won, the one to fight now, the ones ahead) and the
// cat's HP and build. Fight starts the chosen node with the usual battle engine, carrying the run's HP
// and build; the result panel goes back to the map. The run is saved in expedition.json (apart from
// the Adventure progress) at every step, and when its battle starts: a battle left by End, by closing
// the app or a crash costs ExpeditionSpec.RetreatCost when the map next opens, and the step is fought
// again with the same battle. A rest or event node (milestone 3) opens its choice on the map (no
// battle), and a won elite leaves relics to choose from there; nothing moves on until it is made.
// Like the Adventure map it never takes focus, holds the pet's free time
// and starts nothing by itself. With the user's battle.json a run is kept in memory only.
public sealed partial class PetWindow
{
    // The saved file (null: kept in memory only, as for the UI checks' own pets).
    public string? ExpeditionProgressFile { get; set; }
    private ExpeditionProgress? expeditionProgress, customExpeditionProgress;
    public ExpeditionProgress ExpeditionProgress => BattleDataCustom
        ? customExpeditionProgress ??= new()
        : expeditionProgress ??= ExpeditionProgressFile != null ? ExpeditionProgress.Load(ExpeditionProgressFile) : new();
    // For checks: read the file again (like a restart).
    public void ReloadExpeditionProgress() => expeditionProgress = null;
    public Expedition? Expedition => BattleTuning.Expedition != null ? new Expedition(BattleTuning, ExpeditionProgress) { GrantKeepsakes = !BattleDataCustom } : null;
    private bool expeditionSaveFailed;

    // The running battle is this Expedition's step (null: a free battle or an Adventure stage).
    private sealed record ExpeditionBattle(Expedition Expedition, ExpeditionNode Node);
    private ExpeditionBattle? battleExpedition;
    public bool BattleIsExpedition => battleExpedition != null;

    private bool expeditionMapOpen;
    private Window? expeditionMapWindow;
    private int expeditionLane;
    private string expeditionWeapon = BattleWeapon.Sword;
    private BattleDifficulty expeditionLevel = BattleDifficulty.Normal;
    // A one-time line on the map: what happened to the run since it was last shown.
    private string? expeditionNote;
    private bool expeditionConfirmAbandon;
    private readonly List<(int Step, int Lane, Border Node, TextBlock Glyph)> expeditionNodes = new();
    private readonly List<Rectangle> expeditionLinks = new();
    private readonly List<(string Id, Border Chip, TextBlock Text)> expeditionWeaponChips = new();
    private readonly List<(BattleDifficulty Level, Border Chip, TextBlock Text)> expeditionLevelChips = new();
    // The open choice's panel (rest, event, a won elite's relics) and what the user picked in it.
    private StackPanel? xChoice;
    private string? xChoiceKey, expeditionPickCard, expeditionDropCard, expeditionPickRelic;
    private readonly List<TextBlock> xChoiceTexts = new();
    private readonly Dictionary<string, Border> xChoiceButtons = new();
    private TextBlock? xBossLabel, xTitle, xData, xStatus, xBuild, xNodeName, xNodeGoal, xRecord, xNote, xSetupLabel, xMainText, xAbandonText, xCloseText;
    private Border? xMain, xAbandon, xDetails;
    private StackPanel? xSetup, xMap;

    public bool ExpeditionMapOpen => expeditionMapOpen;
    public bool ExpeditionMapVisible => expeditionMapWindow?.IsVisible == true;
    public int ExpeditionSelectedLane => expeditionLane;
    public bool ExpeditionMainEnabled => xMain?.IsEnabled == true;
    // For checks: the texts shown now (in a part of the panel that is shown).
    public string ExpeditionMapText => string.Join("\n", new[] { xTitle, xData, xStatus, xBuild, xNote }
        .Concat(xSetup is { Visibility: Visibility.Visible } ? new[] { xSetupLabel } : Array.Empty<TextBlock?>())
        .Concat(xDetails is { Visibility: Visibility.Visible } ? new[] { xNodeName, xNodeGoal } : Array.Empty<TextBlock?>())
        .Concat(xChoice is { Visibility: Visibility.Visible } ? xChoiceTexts : Enumerable.Empty<TextBlock?>())
        .Concat(new[] { xRecord })
        .Concat(xMain is { Visibility: Visibility.Visible } ? new[] { xMainText } : Array.Empty<TextBlock?>())
        .Concat(new[] { xCloseText })
        .Concat(xAbandon is { Visibility: Visibility.Visible } ? new[] { xAbandonText } : Array.Empty<TextBlock?>())
        .Where(t => t is { Visibility: Visibility.Visible } && t.Text.Length > 0).Select(t => t!.Text)
        .Concat(xSetup is { Visibility: Visibility.Visible } ? expeditionWeaponChips.Select(c => c.Text.Text).Concat(expeditionLevelChips.Select(c => c.Text.Text)) : Enumerable.Empty<string>()));

    private static readonly Brush MapAhead = PetUi.Game.Ahead;
    private static readonly Brush EliteInk = PetUi.Game.Gold;

    private string KindName(string kind) => kind switch
    {
        ExpeditionSpec.Elite => L("Tinh anh", "Elite"), DesktopPet.Expedition.BossKind => L("Trùm", "Boss"),
        ExpeditionSpec.Rest => L("Nghỉ chân", "Rest"), ExpeditionSpec.Event => L("Sự kiện", "Event"), _ => L("Giao tranh", "Fight")
    };
    private static string KindGlyph(string kind) => kind switch
    {
        ExpeditionSpec.Elite => "★", DesktopPet.Expedition.BossKind => "♛", ExpeditionSpec.Rest => "♨", ExpeditionSpec.Event => "?", _ => "⚔"
    };
    private static bool IsFightKind(string kind) => ExpeditionSpec.IsBattle(kind) || kind == DesktopPet.Expedition.BossKind;
    // A node of the step to go now that can be chosen (no choice open, no battle under way).
    private static bool LaneOpen(Expedition x, int lane) => x.Run is ExpeditionRun r && r.Pending == null && r.Fighting == null && r.Step < r.Map.Count && lane >= 0 && lane < r.Map[r.Step].Length;
    private string CardName(string id) => BattleTuning.UpgradeList.FirstOrDefault(u => u.Id == id)?.Name(English) ?? id;
    private string RelicName(Expedition x, string id) => x.Spec.Relic(id)?.Name(English) ?? id;
    private string StepTitle(int step, int steps) => L($"Bậc {step + 1}/{steps}", $"Step {step + 1}/{steps}");
    private string ExpeditionStageTitle(int step, int steps, StageSpec stage) => L("Viễn chinh · ", "Expedition · ") + StepTitle(step, steps) + " · " + stage.Name(English);

    // Saves the run's own progress (the one kept for the user's battle.json stays in memory).
    private void SaveExpedition(ExpeditionProgress progress)
    {
        if (ExpeditionProgressFile == null || ReferenceEquals(progress, customExpeditionProgress) || !ReferenceEquals(progress, expeditionProgress)) return;
        expeditionSaveFailed = !progress.Save(ExpeditionProgressFile);
    }

    private string? ExpeditionWarning() =>
        ExpeditionProgress.ReadOnly ? L("Viễn chinh là của bản mới hơn: chỉ đọc, chuyến ở bản này không được lưu.", "Your Expedition comes from a newer version: read only, runs in this version are not saved.")
        : expeditionSaveFailed ? L("Không lưu được viễn chinh (xem log); chuyến đi có thể mất khi đóng app.", "The Expedition could not be saved (see the log); the run may be lost when the app closes.")
        : null;

    // Null when the map is open, otherwise why not. It replaces an open setup panel, Adventure map
    // or result panel; a battle must end first. A saved run that no longer fits the data ends here,
    // and a battle that was left (the app closed, a crash) costs its RetreatCost now.
    public string? OpenExpeditionMap()
    {
        if (BattlePrepRefusal() is string reason) return reason;
        ReloadBattleData();
        if (Expedition is not Expedition expedition) return L("Dữ liệu trận không có viễn chinh.", "The battle data has no Expedition.");
        if (!expedition.IsUnlocked(AdventureProgress))
            return L("Thắng màn Vua Slime ở Phiêu lưu để mở Viễn chinh.", "Win the Slime King in the Adventure to open the Expedition.");
        if (battlePreparing) { CloseBattlePrep(); CloseBattleTutorial(); }
        CloseAdventureMap(); CloseBattleSummary(); CloseBattleNotice();
        if (expedition.Run is ExpeditionRun saved)
        {
            if (expedition.Mismatch(saved) is string why)
            {
                System.Diagnostics.Trace.WriteLine($"Expedition run {saved.RunId} ended: {why}.");
                expedition.Progress.Active = null;
                SaveExpedition(expedition.Progress);
                expeditionNote = L("Viễn chinh đã đổi luật hoặc bản đồ: chuyến cũ đã kết thúc.", "The Expedition's rules or map changed: the old run has ended.");
            }
            else if (saved.Fighting != null)
            {
                var left = expedition.Retreat();
                SaveExpedition(expedition.Progress);
                expeditionNote = RetreatNote(left);
                System.Diagnostics.Trace.WriteLine($"Expedition run {saved.RunId}: the battle of step {saved.Step + 1} was left earlier.");
            }
        }
        expeditionWeapon = BattleTuning.Weapon(expedition.Run?.Weapon ?? preferences.BattleWeapon).Id;
        expeditionLevel = expedition.Run?.Level ?? BattleDifficultyChoice;
        expeditionConfirmAbandon = false;
        expeditionLane = 0;
        if (!expeditionMapOpen)
        {
            expeditionMapOpen = true;
            queuedCommand = null;
            revision++; // An AI reply chosen before the map must not move the cat now.
            pet.Speech.Hide(); pet.Thought.Clear();
            ApplyQuiet();
            ShowExpeditionMapWindow(expedition.Spec);
            System.Diagnostics.Trace.WriteLine("Expedition map opened.");
        }
        UpdateExpeditionMap();
        return null;
    }

    private string RetreatNote(ExpeditionStepOutcome left) => left.RunOver
        ? L($"Rời trận khi mèo đã quá yếu: chuyến đi kết thúc ở bậc {left.Step + 1}.", $"The battle was left with the cat too weak: the run ended at step {left.Step + 1}.")
        : L($"Đã rời trận bậc {left.Step + 1}: mất {Math.Round(-left.Healed)} HP, đánh lại được.", $"Left the step {left.Step + 1} battle: {Math.Round(-left.Healed)} HP lost, it can be fought again.");

    public bool ExpeditionSelect(int lane)
    {
        if (!expeditionMapOpen || Expedition is not Expedition x || !LaneOpen(x, lane)) return false;
        expeditionLane = lane;
        expeditionConfirmAbandon = false;
        UpdateExpeditionMap();
        return true;
    }

    public bool ExpeditionChooseWeapon(string id)
    {
        if (!expeditionMapOpen || Expedition?.Run != null || BattleTuning.FindWeapon(id) is not WeaponSpec weapon) return false;
        expeditionWeapon = weapon.Id;
        UpdateExpeditionMap();
        return true;
    }

    public bool ExpeditionChooseDifficulty(BattleDifficulty level)
    {
        if (!expeditionMapOpen || Expedition?.Run != null || !Enum.IsDefined(level)) return false;
        expeditionLevel = level;
        UpdateExpeditionMap();
        return true;
    }

    // Begin a run with the chosen weapon and difficulty (kept for the whole run). Null on success.
    public string? ExpeditionStart(int? seed = null)
    {
        if (!expeditionMapOpen || Expedition is not Expedition expedition) return L("Bản đồ viễn chinh chưa mở.", "The Expedition map is not open.");
        if (expedition.Run != null) return L("Đang có một chuyến đi.", "A run is already under way.");
        SetBattleWeapon(expeditionWeapon);
        SetBattleDifficulty(expeditionLevel);
        var run = expedition.Start(expeditionWeapon, expeditionLevel, seed ?? Random.Shared.Next());
        SaveExpedition(expedition.Progress);
        expeditionLane = 0;
        expeditionNote = null;
        System.Diagnostics.Trace.WriteLine($"Expedition run {run.RunId} started: {run.Weapon}, {run.Difficulty}, seed {run.Seed}.");
        UpdateExpeditionMap();
        return null;
    }

    // Fight: the chosen node of the step to fight now, with the run's HP and build. Null on success.
    public string? ExpeditionFight(int? lane = null)
    {
        if (!expeditionMapOpen || Expedition is not Expedition expedition) return L("Bản đồ viễn chinh chưa mở.", "The Expedition map is not open.");
        if (expedition.Run is not ExpeditionRun run) return L("Chưa bắt đầu chuyến đi.", "No run has started.");
        if (expedition.Node(lane ?? expeditionLane) is not ExpeditionNode node) return L("Không có điểm này.", "There is no such node.");
        if (StartBattle(run.Weapon, run.Level, node.Stage, new ExpeditionBattle(expedition, node)) is string reason) return reason;
        expedition.Enter(node.Lane);
        SaveExpedition(expedition.Progress);
        return null;
    }

    // Go to the chosen rest or event node: its choice opens on the map. Null on success.
    public string? ExpeditionVisit(int? lane = null)
    {
        if (!expeditionMapOpen || Expedition is not Expedition expedition) return L("Bản đồ viễn chinh chưa mở.", "The Expedition map is not open.");
        if (expedition.Run is not ExpeditionRun run) return L("Chưa bắt đầu chuyến đi.", "No run has started.");
        if (!expedition.Visit(lane ?? expeditionLane)) return L("Không có điểm này.", "There is no such node.");
        SaveExpedition(expedition.Progress);
        expeditionPickCard = expeditionDropCard = expeditionPickRelic = null;
        expeditionNote = null;
        System.Diagnostics.Trace.WriteLine($"Expedition run {run.RunId}: step {run.Step + 1} {run.Pending!.Kind} {run.Pending.Event}.");
        UpdateExpeditionMap();
        return null;
    }

    // The open choice: what the user picked in it (a card to take, a card to give up, a relic).
    public bool ExpeditionPick(string? card = null, string? drop = null, string? relic = null)
    {
        if (!expeditionMapOpen || Expedition?.Run is not ExpeditionRun run || run.Pending is not ExpeditionChoice open) return false;
        if (card != null && !open.Cards.Contains(card) || relic != null && !open.Relics.Contains(relic) || drop != null && !run.Build.ContainsKey(drop)) return false;
        if (card != null) expeditionPickCard = card;
        if (drop != null) expeditionDropCard = drop;
        if (relic != null) expeditionPickRelic = relic;
        UpdateExpeditionMap();
        return true;
    }

    public bool ExpeditionHasChoice => Expedition?.Run?.Pending != null;
    public bool ExpeditionOptionEnabled(string option) => xChoiceButtons.TryGetValue(option, out var b) && b.IsEnabled && b.Visibility == Visibility.Visible;

    // Resolve the open choice with the picks made. Null on success.
    public string? ExpeditionChoose(string option)
    {
        if (!expeditionMapOpen || Expedition is not Expedition expedition || expedition.Run is not ExpeditionRun run || run.Pending == null)
            return L("Không có lựa chọn nào đang mở.", "No choice is open.");
        if (expedition.Refusal(option, expeditionPickCard, expeditionDropCard, expeditionPickRelic) is string why)
            return why == "too weak" ? L("Mèo quá yếu để trả giá này.", "The cat is too weak to pay this.") : L("Hãy chọn trước.", "Choose first.");
        int step = run.Step;
        var done = expedition.Choose(option, expeditionPickCard, expeditionDropCard, expeditionPickRelic);
        if (!done.Done) return L("Không chọn được.", "That cannot be chosen.");
        SaveExpedition(expedition.Progress);
        string hp = done.Hp >= 0.5 ? $" +{Math.Round(done.Hp)} HP" : done.Hp <= -0.5 ? $" −{Math.Round(-done.Hp)} HP" : "";
        expeditionNote = (done.Option switch
        {
            DesktopPet.Expedition.Card => L($"Đã lấy thẻ {CardName(done.Card!)}.", $"Took the {CardName(done.Card!)} card."),
            DesktopPet.Expedition.SwapOption => L($"Đã đổi {CardName(done.Dropped!)} lấy {CardName(done.Card!)}.", $"Traded {CardName(done.Dropped!)} for {CardName(done.Card!)}."),
            DesktopPet.Expedition.Risk or DesktopPet.Expedition.Take => L($"Nhận di vật {RelicName(expedition, done.Relic!)}.", $"Got the {RelicName(expedition, done.Relic!)} relic."),
            DesktopPet.Expedition.Leave => L("Đi tiếp.", "Moved on."),
            _ => L("Nghỉ một lát.", "A short rest.")
        }) + hp;
        expeditionPickCard = expeditionDropCard = expeditionPickRelic = null;
        expeditionLane = 0;
        System.Diagnostics.Trace.WriteLine($"Expedition run {run.RunId}: step {step + 1} choice {done.Option} {done.Card ?? done.Relic}.");
        UpdateExpeditionMap();
        return null;
    }

    // Give the run up (the map's button asks twice). Counted as ended where it is.
    public bool ExpeditionAbandon()
    {
        if (!expeditionMapOpen || Expedition is not Expedition expedition || expedition.Run is not ExpeditionRun run) return false;
        expedition.Abandon();
        SaveExpedition(expedition.Progress);
        expeditionNote = L($"Đã bỏ chuyến đi ở bậc {run.Step + 1}.", $"The run was given up at step {run.Step + 1}.");
        expeditionConfirmAbandon = false;
        System.Diagnostics.Trace.WriteLine($"Expedition run {run.RunId} given up at step {run.Step + 1}.");
        UpdateExpeditionMap();
        return true;
    }

    public void CancelExpeditionMap()
    {
        if (!expeditionMapOpen) return;
        CloseExpeditionMap();
        revision++;
        behavior.Reset(pet);
        System.Diagnostics.Trace.WriteLine("Expedition map closed.");
    }

    private void CloseExpeditionMap()
    {
        bool was = expeditionMapOpen;
        expeditionMapOpen = false;
        expeditionMapWindow?.Close();
        expeditionMapWindow = null;
        xMain = xAbandon = xDetails = null;
        xSetup = xMap = xChoice = null;
        xChoiceKey = null; xChoiceTexts.Clear(); xChoiceButtons.Clear();
        expeditionNodes.Clear(); expeditionLinks.Clear(); expeditionWeaponChips.Clear(); expeditionLevelChips.Clear();
        if (was) ApplyQuiet();
    }

    // Every frame, paused or not: a fullscreen app hides the map (it comes back after).
    private double expeditionRefresh;
    private void TickExpeditionMap(double dt)
    {
        if (!expeditionMapOpen || expeditionMapWindow == null) return;
        if (context.Fullscreen) { if (expeditionMapWindow.IsVisible) expeditionMapWindow.Hide(); return; }
        if (!expeditionMapWindow.IsVisible) { expeditionMapWindow.Show(); PlaceExpeditionMap(); }
        expeditionRefresh += dt;
        if (expeditionRefresh < BattlePrepRefreshSeconds) return;
        expeditionRefresh = 0;
        UpdateExpeditionMap();
    }

    private void ShowExpeditionMapWindow(ExpeditionSpec spec)
    {
        TextBlock Line(double size, bool bold = false, double maxWidth = 460) => new()
        {
            Foreground = Brushes.White, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap, MaxWidth = maxWidth
        };
        xTitle = Line(PetUi.HeadingSize, true);
        xData = Line(PetUi.SmallSize); xData.Foreground = PrepWarning; xData.Margin = new Thickness(0, 6, 0, 0);
        xStatus = Line(PetUi.BodySize, true); xStatus.Margin = new Thickness(0, 6, 0, 0);
        xBuild = Line(PetUi.SmallSize); xBuild.Opacity = 0.85; xBuild.Margin = new Thickness(0, 2, 0, 0);
        xNote = Line(PetUi.SmallSize, true); xNote.Foreground = PrepWarning; xNote.Margin = new Thickness(0, 6, 0, 0);

        // Before a run: the weapon and difficulty for the whole run.
        (Border, TextBlock) Chip(Action click)
        {
            var text = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            var chip = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 6, 0), MinWidth = 64,
                BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand, Child = text };
            chip.MouseLeftButtonDown += (_, e) => { click(); e.Handled = true; };
            return (chip, text);
        }
        xSetupLabel = Line(PetUi.SmallSize); xSetupLabel.Margin = new Thickness(0, 10, 0, 4);
        var weapons = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var weapon in BattleTuning.WeaponList)
        {
            string id = weapon.Id;
            var (chip, text) = Chip(() => ExpeditionChooseWeapon(id));
            weapons.Children.Add(chip);
            expeditionWeaponChips.Add((id, chip, text));
        }
        var levels = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        foreach (var level in new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard })
        {
            var (chip, text) = Chip(() => ExpeditionChooseDifficulty(level));
            levels.Children.Add(chip);
            expeditionLevelChips.Add((level, chip, text));
        }
        xSetup = new StackPanel { Children = { xSetupLabel, weapons, levels } };

        // The steps left to right, each with its nodes stacked; the boss last. Gold: the path won;
        // blue: the step to fight now (white rim: chosen); faint: steps ahead; dim: nodes passed by.
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        expeditionNodes.Clear(); expeditionLinks.Clear();
        for (int step = 0; step < spec.StepCount; step++)
        {
            if (step > 0)
            {
                var link = new Rectangle { Width = 18, Height = 4, RadiusX = 2, RadiusY = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 14) };
                row.Children.Add(link);
                expeditionLinks.Add(link);
            }
            var column = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            int lanes = step < spec.Steps.Length ? spec.Steps[step].Length : 1;
            for (int lane = 0; lane < lanes; lane++)
            {
                int at = lane;
                var glyph = new TextBlock { FontSize = PetUi.HeadingSize, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var node = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(19), Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Center,
                    Cursor = System.Windows.Input.Cursors.Hand, Child = glyph };
                node.MouseLeftButtonDown += (_, e) => { ExpeditionSelect(at); e.Handled = true; };
                column.Children.Add(node);
                expeditionNodes.Add((step, lane, node, glyph));
            }
            var number = new TextBlock { Foreground = Brushes.White, Opacity = 0.7, FontSize = PetUi.CaptionSize, HorizontalAlignment = HorizontalAlignment.Center,
                Text = (step + 1).ToString() };
            if (step >= spec.Steps.Length) xBossLabel = number; // Named in the pet's language by UpdateExpeditionMap.
            column.Children.Add(number);
            row.Children.Add(column);
        }
        xMap = new StackPanel { Children = { row } };
        xNodeName = Line(PetUi.TitleSize, true);
        xNodeGoal = Line(PetUi.BodySize); xNodeGoal.Margin = new Thickness(0, 3, 0, 0);
        xDetails = new Border { Background = PrepCardBack, CornerRadius = new CornerRadius(9), Padding = new Thickness(11, 8, 11, 9), Margin = new Thickness(0, 10, 0, 0),
            Child = new StackPanel { Children = { xNodeName, xNodeGoal } } };
        xMap.Children.Add(xDetails);
        xChoice = new StackPanel { Margin = new Thickness(0, 10, 0, 0), MaxWidth = 470 };
        xMap.Children.Add(xChoice);
        xRecord = Line(PetUi.SmallSize); xRecord.Opacity = 0.8; xRecord.Margin = new Thickness(0, 8, 0, 0);

        var (close, closeText, _) = HudButton(CancelExpeditionMap, plain: true);
        var (abandon, abandonText, _) = HudButton(() =>
        {
            if (!expeditionConfirmAbandon) { expeditionConfirmAbandon = true; UpdateExpeditionMap(); }
            else ExpeditionAbandon();
        }, plain: true);
        var (main, mainText, _) = HudButton(() =>
        {
            string? reason = Expedition is not Expedition x || x.Run is not ExpeditionRun r ? ExpeditionStart()
                : r.Step < r.Map.Count && IsFightKind(x.Kind(r.Step, expeditionLane)) ? ExpeditionFight() : ExpeditionVisit();
            if (reason != null) ShowBattleNotice(reason);
        }, plain: true);
        main.Background = PrepChosen; main.Padding = new Thickness(18, 6, 18, 5); main.MinWidth = 110; main.Margin = new Thickness(0);
        mainText.FontSize = PetUi.TitleSize; mainText.FontWeight = FontWeights.Bold;
        xMain = main; xMainText = mainText; xAbandon = abandon; xAbandonText = abandonText; xCloseText = closeText;
        var actions = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
        DockPanel.SetDock(close, Dock.Left); DockPanel.SetDock(abandon, Dock.Left); DockPanel.SetDock(main, Dock.Right);
        actions.Children.Add(close); actions.Children.Add(abandon); actions.Children.Add(main);
        var panel = new Border
        {
            Background = HudBack, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(6), MinWidth = 460,
            Child = new StackPanel { Children = { xTitle, xData, xStatus, xBuild, xNote, xSetup, xMap, xRecord, actions } },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
        };
        panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        var window = CreateBattleWindow(panel, "Pet expedition");
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.Closed += (_, _) => { if (expeditionMapWindow == window) expeditionMapWindow = null; };
        expeditionMapWindow = window;
        UpdateExpeditionMap();
        window.Show();
        PlaceExpeditionMap();
    }

    private void PlaceExpeditionMap()
    {
        if (expeditionMapWindow == null) return;
        expeditionMapWindow.UpdateLayout();
        double width = expeditionMapWindow.ActualWidth;
        MoveWindow(expeditionMapWindow, Math.Clamp((bounds.Left + bounds.Right - width) / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width)), bounds.Top + 12);
    }

    private void UpdateExpeditionMap()
    {
        if (!expeditionMapOpen || xTitle == null || xMain == null || xSetup == null || xMap == null) return;
        if (Expedition is not Expedition expedition) return;
        static void Show(TextBlock? text, string? value)
        {
            if (text == null) return;
            if (text.Text != (value ?? "")) text.Text = value ?? "";
            text.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
        var spec = expedition.Spec;
        var run = expedition.Run;
        xTitle.Text = L("Viễn chinh — ", "Expedition — ") + spec.Name(English);
        if (xBossLabel != null) xBossLabel.Text = L("Trùm", "Boss");
        string? dataNote = BattleDataError != null
            ? L("File battle.json của bạn có lỗi nên dùng dữ liệu gốc: ", "Your battle.json has a mistake, so the built-in battle is used: ") + BattleDataError
            : BattleDataCustom ? L("Đang dùng battle.json tùy chỉnh: chơi được nhưng chuyến đi và kỷ lục không được lưu.", "Using your custom battle.json: you can play, but the run and records are not saved.") : null;
        Show(xData, string.Join("\n", new[] { dataNote, ExpeditionWarning() }.Where(t => t != null)));
        Show(xNote, expeditionNote);
        string weaponName = BattleTuning.Weapon(run?.Weapon ?? expeditionWeapon).Name(English);
        string levelName = DifficultyName(run?.Level ?? expeditionLevel);
        if (run == null)
        {
            Show(xStatus, L($"Chưa có chuyến đi · {spec.StepCount} bậc, bậc cuối là trùm. HP và build giữ suốt chuyến.",
                $"No run yet · {spec.StepCount} steps, the last one a boss. HP and build carry through the run."));
            Show(xBuild, null);
            xSetupLabel!.Text = L("Cách đánh và độ khó cho cả chuyến:", "Weapon and difficulty for the whole run:");
            foreach (var (id, chip, text) in expeditionWeaponChips)
            {
                bool selected = id == expeditionWeapon;
                text.Text = BattleTuning.Weapon(id).Name(English);
                chip.Background = selected ? PrepChosen : PrepCardBack;
                chip.BorderBrush = selected ? Brushes.White : PrepCardRim;
            }
            foreach (var (level, chip, text) in expeditionLevelChips)
            {
                bool selected = level == expeditionLevel;
                text.Text = DifficultyName(level);
                chip.Background = selected ? PrepChosen : PrepCardBack;
                chip.BorderBrush = selected ? Brushes.White : PrepCardRim;
            }
        }
        else
        {
            double max = expedition.MaxHp(run);
            Show(xStatus, $"{StepTitle(run.Step, run.Map.Count)} · HP {Math.Ceiling(run.Hp)}/{max:0} · {weaponName} · {levelName}");
            var parts = BuildParts(run.Build);
            Show(xBuild, L("Build: ", "Build: ") + (parts.Count == 0 ? L("chưa có thẻ", "no cards yet") : string.Join(" · ", parts))
                + "\n" + L("Di vật: ", "Relics: ") + (run.Relics.Count == 0 ? L("chưa có", "none yet") : string.Join(" · ", run.Relics.Select(id => "◈ " + RelicName(expedition, id))))
                + $" ({run.Relics.Count}/{spec.MaxRelics})");
        }
        xSetup.Visibility = run == null ? Visibility.Visible : Visibility.Collapsed;

        // The map: the run's (or, before one, the spec's kinds with no encounters drawn yet).
        int now = run?.Step ?? -1;
        foreach (var (step, lane, node, glyph) in expeditionNodes)
        {
            string kind = expedition.Kind(step, lane);
            bool won = run != null && step < now && run.Path[step] == lane;
            bool passed = run != null && step < now && !won;
            // While a node's choice is open only that node stays lit; after an elite, none.
            bool current = step == now && (run!.Pending == null || run.Pending.Kind != ExpeditionChoice.RewardKind && run.Pending.Lane == lane);
            bool selected = current && lane == expeditionLane;
            node.Background = won ? MapDone : current ? PrepChosen : passed ? MapLocked : MapAhead;
            node.BorderBrush = selected ? Brushes.White : PrepCardRim;
            node.BorderThickness = new Thickness(selected ? 3 : 1);
            node.Opacity = passed ? 0.5 : 1;
            node.Cursor = current ? System.Windows.Input.Cursors.Hand : null;
            glyph.Text = KindGlyph(kind);
            glyph.Foreground = won ? MapDoneInk : kind == ExpeditionSpec.Elite && !current ? EliteInk : Brushes.White;
            node.ToolTip = run == null ? KindName(kind)
                : spec.Encounter(run.Map[step][lane]) is StageSpec encounter ? $"{KindName(kind)} — {encounter.Name(English)}"
                : spec.FindEvent(run.Map[step][lane]) is ExpeditionEventSpec e ? $"{KindName(kind)} — {e.Name(English)}" : KindName(kind);
        }
        for (int i = 0; i < expeditionLinks.Count; i++) expeditionLinks[i].Fill = run != null && i + 1 < now ? MapDone : MapLocked;

        if (run != null && expedition.Node(expeditionLane) is ExpeditionNode chosen)
        {
            xDetails!.Visibility = Visibility.Visible;
            xNodeName!.Text = $"{StepTitle(chosen.Step, run.Map.Count)} · {KindName(chosen.Kind)} — {chosen.Stage.Name(English)}";
            string threat = chosen.Threat > 1.001 ? L($" · quái mạnh ×{chosen.Threat:0.00}", $" · enemies ×{chosen.Threat:0.00}") : "";
            string reward = chosen.Kind == ExpeditionSpec.Elite && spec.EliteRelics > 0 && run.Relics.Count < spec.MaxRelics
                ? "\n" + L($"Thắng: chọn 1 trong {spec.EliteRelics} di vật.", $"Win: pick one of {spec.EliteRelics} relics.") : "";
            Show(xNodeGoal, chosen.Stage.Goal(English) + "\n" + ObjectiveText(chosen.Stage) + StageRules(chosen.Stage) + threat + reward);
        }
        else if (run != null && LaneOpen(expedition, expeditionLane) && expedition.Kind(run.Step, expeditionLane) is string other && !IsFightKind(other))
        {
            xDetails!.Visibility = Visibility.Visible;
            var e = spec.FindEvent(run.Map[run.Step][expeditionLane]);
            xNodeName!.Text = $"{StepTitle(run.Step, run.Map.Count)} · {KindName(other)}" + (e != null ? " — " + e.Name(English) : "");
            Show(xNodeGoal, e?.Describe(English) ?? L($"Hồi {spec.RestHeal:P0} HP tối đa, hoặc lấy một thẻ nâng cấp thay vì hồi máu.", $"Get {spec.RestHeal:P0} of the max HP back, or take an upgrade card instead."));
        }
        else xDetails!.Visibility = Visibility.Collapsed;
        UpdateExpeditionChoice(expedition);

        var record = expedition.Record(run?.Weapon ?? expeditionWeapon, run?.Level ?? expeditionLevel);
        int keepsakesHad = spec.Keepsakes.Count(k => expedition.Progress.Keepsakes.Contains(k.Id));
        string keepsakeLine = spec.Keepsakes.Length == 0 ? "" : (record.Runs == 0 ? "" : "\n") + L($"Kỷ vật: {keepsakesHad}/{spec.Keepsakes.Length}", $"Keepsakes: {keepsakesHad}/{spec.Keepsakes.Length}")
            + (keepsakesHad < spec.Keepsakes.Length ? L(" · thắng cả chuyến ở mỗi độ khó để nhận", " · win a run at each difficulty to earn them") : "");
        Show(xRecord, (record.Runs == 0 ? "" : L($"Kỷ lục ({weaponName} · {levelName}): thắng {record.Wins}/{record.Runs} chuyến · xa nhất {record.BestSteps}/{spec.StepCount} bậc", $"Records ({weaponName} · {levelName}): {record.Wins}/{record.Runs} runs won · furthest {record.BestSteps}/{spec.StepCount} steps")
            + (record.BestSeconds is double best ? L($" · nhanh nhất {Clock(best)}", $" · fastest {Clock(best)}") : "")) + keepsakeLine);

        bool battleNode = run != null && run.Step < run.Map.Count && IsFightKind(expedition.Kind(run.Step, expeditionLane));
        xMainText!.Text = run == null ? L("Bắt đầu chuyến", "Start the run") : battleNode ? L("Vào trận", "Fight") : L("Đi tới đây", "Go here");
        xMain.Visibility = run?.Pending != null ? Visibility.Collapsed : Visibility.Visible;
        xCloseText!.Text = L("Đóng", "Close");
        xAbandonText!.Text = expeditionConfirmAbandon ? L("Bấm lần nữa để bỏ", "Click again to give up") : L("Bỏ chuyến", "Give up");
        xAbandon!.Visibility = run != null ? Visibility.Visible : Visibility.Collapsed;
        bool can = run == null ? BattlePrepRefusal() == null : battleNode ? BattleRefusal() == null && expedition.Node(expeditionLane) != null : LaneOpen(expedition, expeditionLane);
        if (xMain.IsEnabled != can) { xMain.IsEnabled = can; xMain.Opacity = can ? 1 : 0.5; }
    }

    // The open choice (rest, event, a won elite's relics): what it offers, with each option's price
    // said before choosing. Built again only when what it shows changes (not every refresh).
    private void UpdateExpeditionChoice(Expedition expedition)
    {
        if (xChoice == null) return;
        var run = expedition.Run;
        if (run?.Pending is not ExpeditionChoice open)
        {
            xChoice.Visibility = Visibility.Collapsed;
            xChoiceKey = null; xChoiceTexts.Clear(); xChoiceButtons.Clear(); xChoice.Children.Clear();
            return;
        }
        string key = string.Join("|", open.Kind, open.Lane, open.Event, string.Join(",", open.Cards), string.Join(",", open.Relics), expeditionPickCard, expeditionDropCard, expeditionPickRelic,
            English, Math.Round(run.Hp), string.Join(",", run.Build.Select(c => c.Key + c.Value)), run.Relics.Count);
        xChoice.Visibility = Visibility.Visible;
        if (key == xChoiceKey) return;
        xChoiceKey = key;
        xChoice.Children.Clear(); xChoiceTexts.Clear(); xChoiceButtons.Clear();
        TextBlock Text(string value, double size, bool bold = false, double opacity = 1, Brush? brush = null)
        {
            var t = new TextBlock { Text = value, Foreground = brush ?? Brushes.White, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Opacity = opacity, TextWrapping = TextWrapping.Wrap };
            xChoiceTexts.Add(t);
            return t;
        }
        // A pickable card or relic: its name and what it does; the picked one is blue with a white rim.
        Border Option(string name, string desc, bool picked, Action click)
        {
            var box = new Border
            {
                Background = picked ? PrepChosen : PrepCardBack, BorderBrush = picked ? Brushes.White : PrepCardRim, BorderThickness = new Thickness(picked ? 2 : 1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 5, 9, 6), Margin = new Thickness(0, 4, 0, 0), Cursor = System.Windows.Input.Cursors.Hand,
                Child = new StackPanel { Children = { Text(name, PetUi.BodySize, true), Text(desc, PetUi.SmallSize, opacity: 0.85) } }
            };
            box.MouseLeftButtonDown += (_, e) => { click(); e.Handled = true; };
            return box;
        }
        void Label(string value) { var t = Text(value, 12, true); t.Margin = new Thickness(0, 8, 0, 0); xChoice.Children.Add(t); }

        var spec = expedition.Spec;
        var e = spec.FindEvent(open.Event);
        string title = open.Kind switch
        {
            ExpeditionChoice.RestKind => L("Nghỉ chân — chọn một", "Rest — choose one"),
            ExpeditionChoice.RewardKind => L("Phần thưởng tinh anh", "Elite reward"),
            _ => e?.Name(English) ?? KindName(ExpeditionSpec.Event)
        };
        var head = Text(title, PetUi.TitleSize, true, brush: open.Kind == ExpeditionChoice.RewardKind ? RecordGold : null);
        xChoice.Children.Add(head);
        if (open.Kind == ExpeditionChoice.RewardKind) xChoice.Children.Add(Text(L("Chọn một di vật mang theo đến hết chuyến, hoặc bỏ qua.", "Pick a relic to keep for the rest of the run, or leave them."), PetUi.SmallSize, opacity: 0.85));
        else if (e != null) xChoice.Children.Add(Text(e.Describe(English), PetUi.SmallSize, opacity: 0.85));
        if (run.Relics.Count >= spec.MaxRelics && open.Kind != ExpeditionChoice.RestKind && e?.Kind != ExpeditionEventSpec.Swap)
            xChoice.Children.Add(Text(L($"Đã đủ {spec.MaxRelics} di vật.", $"The run already holds {spec.MaxRelics} relics."), PetUi.SmallSize, brush: PrepWarning));

        if (e?.Kind == ExpeditionEventSpec.Swap && run.Build.Count > 0)
        {
            Label(L("Bỏ thẻ:", "Give up:"));
            foreach (var (id, n) in run.Build.OrderBy(c => BattleTuning.UpgradeList.ToList().FindIndex(u => u.Id == c.Key)))
            {
                string card = id;
                var u = BattleTuning.UpgradeList.FirstOrDefault(x => x.Id == id);
                xChoice.Children.Add(Option(CardName(id) + (n > 1 ? $" ×{n}" : ""), u?.Describe(English) ?? "", expeditionDropCard == id, () => ExpeditionPick(drop: card)));
            }
        }
        if (open.Cards.Length > 0 && (e == null || e.Kind == ExpeditionEventSpec.Swap && run.Build.Count > 0))
        {
            Label(L("Lấy thẻ:", "Take:"));
            foreach (var id in open.Cards)
            {
                string card = id;
                var u = BattleTuning.UpgradeList.FirstOrDefault(x => x.Id == id);
                xChoice.Children.Add(Option(CardName(id) + (u?.Rare == true ? " ◆" : ""), u?.Describe(English) ?? "", expeditionPickCard == id, () => ExpeditionPick(card: card)));
            }
        }
        // A chest's relic stays hidden until it is opened; a shrine's and an elite's are shown.
        if (open.Relics.Length > 0 && e?.Kind != ExpeditionEventSpec.Chest && run.Relics.Count < spec.MaxRelics)
        {
            Label(L("Di vật:", "Relics:"));
            foreach (var id in open.Relics)
            {
                string relic = id;
                var r = spec.Relic(id);
                xChoice.Children.Add(Option("◈ " + (r?.Name(English) ?? id), r?.Describe(English) ?? "", expeditionPickRelic == id, () => ExpeditionPick(relic: relic)));
            }
        }

        var buttons = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        foreach (var option in expedition.Options())
        {
            double cost = Math.Round(expedition.Cost(option)), gain = Math.Round(expedition.Gain(option));
            string label = option switch
            {
                DesktopPet.Expedition.Heal => L($"Nghỉ: +{gain} HP", $"Rest: +{gain} HP"),
                DesktopPet.Expedition.Card => L("Lấy thẻ đã chọn", "Take the chosen card"),
                DesktopPet.Expedition.SwapOption => L("Đổi thẻ", "Trade"),
                DesktopPet.Expedition.Safe => L($"An toàn: +{gain} HP", $"Safe: +{gain} HP"),
                DesktopPet.Expedition.Risk => L($"Liều: −{cost} HP, 1 di vật ngẫu nhiên", $"Risk it: −{cost} HP, a random relic"),
                DesktopPet.Expedition.Take => cost > 0 ? L($"Lấy di vật đã chọn (−{cost} HP)", $"Take the chosen relic (−{cost} HP)") : L("Lấy di vật đã chọn", "Take the chosen relic"),
                _ => open.Kind == ExpeditionChoice.RewardKind ? L("Bỏ qua", "Leave them") : L("Đi tiếp", "Move on")
            };
            string chosen = option;
            var (button, text, _) = HudButton(() => { if (ExpeditionChoose(chosen) is string reason) ShowBattleNotice(reason); }, plain: true);
            text.Text = label;
            xChoiceTexts.Add(text);
            button.Margin = new Thickness(0, 0, 6, 6);
            bool main = option is not DesktopPet.Expedition.Leave;
            if (main) button.Background = PrepChosen;
            bool can = expedition.Refusal(option, expeditionPickCard, expeditionDropCard, expeditionPickRelic) == null;
            button.IsEnabled = can; button.Opacity = can ? 1 : 0.5;
            string? why = expedition.Refusal(option, expeditionPickCard, expeditionDropCard, expeditionPickRelic);
            if (why == "too weak") button.ToolTip = L("Mèo quá yếu để trả giá này.", "The cat is too weak to pay this.");
            buttons.Children.Add(button);
            xChoiceButtons[option] = button;
        }
        xChoice.Children.Add(buttons);
    }

    // A finished step's battle: counted once in the run (saved at once).
    private ExpeditionStepOutcome CountExpeditionStep(BattleSim finished, bool won)
    {
        if (battleExpedition is not ExpeditionBattle x) return default;
        var outcome = x.Expedition.Finish(won, finished.CatHp, finished.Build(), finished.FightSeconds, finished.Defeated);
        if (outcome.Counted)
        {
            SaveExpedition(x.Expedition.Progress);
            System.Diagnostics.Trace.WriteLine($"Expedition step {outcome.Step + 1} {(won ? "won" : "lost")}{(outcome.RunOver ? $", the run is over ({(outcome.RunWon ? "won" : "lost")})" : "")}.");
        }
        return outcome;
    }

    // End during a step's battle: the battle is left (RetreatCost), back to the map.
    private void LeaveExpeditionBattle(ExpeditionBattle x)
    {
        var left = x.Expedition.Retreat();
        SaveExpedition(x.Expedition.Progress);
        if (left.Counted) expeditionNote = RetreatNote(left);
        if (OpenExpeditionMap() is string reason) ShowBattleNotice(reason);
    }

    // The result panel of a step: what it changed, then back to the map (or the desktop).
    private (UIElement Lines, UIElement Actions) ExpeditionResult(BattleSummary result, ExpeditionStepOutcome step, Action<Action> choose)
    {
        TextBlock Line(double size, Brush? brush = null, bool bold = false, double opacity = 1) => new()
        {
            Foreground = brush ?? Brushes.White, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Opacity = opacity,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 300, Margin = new Thickness(0, 2, 0, 0)
        };
        var lines = new StackPanel();
        void Add(string text, double size = PetUi.BodySize, Brush? brush = null, bool bold = false, double opacity = 1) { var line = Line(size, brush, bold, opacity); line.Text = text; lines.Children.Add(line); }
        if (!result.Ranked) Add(L("Dữ liệu tùy chỉnh: chuyến này không được lưu.", "Custom data: this run is not saved."), PetUi.SmallSize, PrepWarning);
        else if (ExpeditionWarning() is string warning) Add(warning, PetUi.SmallSize, PrepWarning);
        int steps = Expedition?.Spec.StepCount ?? step.Step + 1;
        if (step.RunWon) Add(L("Hoàn thành viễn chinh!", "Expedition complete!"), PetUi.BodySize, RecordGold, true);
        else if (step.RunOver) Add(L($"Chuyến đi kết thúc ở bậc {step.Step + 1}/{steps}.", $"The run ended at step {step.Step + 1}/{steps}."), PetUi.BodySize, PrepWarning, true);
        else
        {
            Add(L($"Qua bậc {step.Step + 1}/{steps} · HP mang tiếp {Math.Ceiling(step.Hp)}/{step.MaxHp:0}", $"Step {step.Step + 1}/{steps} done · HP carried on {Math.Ceiling(step.Hp)}/{step.MaxHp:0}"), PetUi.BodySize, bold: true);
            if (step.Healed >= 0.5) Add(L($"Nghỉ chân: +{Math.Round(step.Healed)} HP", $"A short rest: +{Math.Round(step.Healed)} HP"), PetUi.SmallSize, opacity: 0.85);
            if (step.Reward) Add(L("Tinh anh để lại di vật: chọn trên bản đồ viễn chinh.", "The elite left relics: pick one on the Expedition map."), PetUi.BodySize, RecordGold, true);
        }
        if (result.Upgrades.Length > 0) Add(L("Build hiện tại: ", "Build now: ") + result.Upgrades, PetUi.SmallSize, opacity: 0.85);
        // Keepsakes this run gave (each only once): where to find them.
        foreach (var id in step.Keepsakes ?? Array.Empty<string>())
            if (Expedition?.Spec.Keepsake(id) is KeepsakeSpec k)
                Add(L("Kỷ vật mới: ", "New keepsake: ") + k.Name(English) + (k.Kind == KeepsakeSpec.AccessoryKind ? L(" (đội trong Cài đặt)", " (wear it from Settings)")
                    : preferences.ShowTrophies ? L(" (ở góc trưng bày)", " (in the display corner)") : L(" (bật góc trưng bày trong Cài đặt)", " (turn the display corner on in Settings)")), PetUi.BodySize, RecordGold, true);
        if (step.Fastest) Add(L("Kỷ lục mới: chuyến nhanh nhất!", "New record: fastest run!"), PetUi.BodySize, RecordGold, true);
        else if (step.BestSteps && step.RunOver && !step.RunWon) Add(L("Kỷ lục mới: đi xa nhất!", "New record: furthest run!"), PetUi.BodySize, RecordGold, true);

        var (main, mainText, _) = HudButton(() => choose(() => { if (OpenExpeditionMap() is string reason) ShowBattleNotice(reason); }), plain: true);
        main.Background = ResultMain; main.Padding = new Thickness(16, 6, 16, 5); main.Margin = new Thickness(0); main.HorizontalAlignment = HorizontalAlignment.Right;
        mainText.FontSize = PetUi.TitleSize; mainText.FontWeight = FontWeights.Bold;
        mainText.Text = step.RunOver ? L("Bản đồ viễn chinh", "Expedition map") : L("Tiếp tục", "Continue");
        var (home, homeText, _) = HudButton(() => choose(() => { }), plain: true);
        homeText.Text = step.RunOver ? L("Về desktop", "Back to desktop") : L("Về desktop (chuyến được lưu)", "Back to desktop (the run is saved)");
        home.Margin = new Thickness(0);
        var others = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), Children = { home } };
        return (lines, new StackPanel { Margin = new Thickness(0, 12, 0, 0), Children = { main, others } });
    }
}
