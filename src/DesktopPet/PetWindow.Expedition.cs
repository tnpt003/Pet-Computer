using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet;

// Expedition (docs/EXPEDITION-DIRECTION.md, milestone 2): menu → Battle Mode → Expedition opens its
// map once the Adventure's Slime King is won. Without a run the map chooses the weapon and difficulty
// for the whole run; with one it shows the steps (won, the one to fight now, the ones ahead) and the
// cat's HP and build. Fight starts the chosen node with the usual battle engine, carrying the run's HP
// and build; the result panel goes back to the map. The run is saved in expedition.json (apart from
// the Adventure progress) at every step, and when its battle starts: a battle left by End, by closing
// the app or a crash costs ExpeditionSpec.RetreatCost when the map next opens, and the step is fought
// again with the same battle. Like the Adventure map it never takes focus, holds the pet's free time
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
    public Expedition? Expedition => BattleTuning.Expedition != null ? new Expedition(BattleTuning, ExpeditionProgress) : null;
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
        .Concat(new[] { xRecord, xMainText, xCloseText })
        .Concat(xAbandon is { Visibility: Visibility.Visible } ? new[] { xAbandonText } : Array.Empty<TextBlock?>())
        .Where(t => t is { Visibility: Visibility.Visible } && t.Text.Length > 0).Select(t => t!.Text)
        .Concat(xSetup is { Visibility: Visibility.Visible } ? expeditionWeaponChips.Select(c => c.Text.Text).Concat(expeditionLevelChips.Select(c => c.Text.Text)) : Enumerable.Empty<string>()));

    private static readonly Brush MapAhead = PetUi.Frozen(Color.FromArgb(0x50, 255, 255, 255));
    private static readonly Brush EliteInk = PetUi.Frozen(Color.FromRgb(0xFF, 0xD3, 0x5C));

    private string KindName(string kind) => kind switch
    {
        ExpeditionSpec.Elite => L("Tinh anh", "Elite"), DesktopPet.Expedition.BossKind => L("Trùm", "Boss"), _ => L("Giao tranh", "Fight")
    };
    private static string KindGlyph(string kind) => kind switch { ExpeditionSpec.Elite => "★", DesktopPet.Expedition.BossKind => "♛", _ => "⚔" };
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
        if (!expeditionMapOpen || Expedition?.Node(lane) == null) return false;
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
        xSetup = xMap = null;
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
        xTitle = Line(16, true);
        xData = Line(11.5); xData.Foreground = PrepWarning; xData.Margin = new Thickness(0, 6, 0, 0);
        xStatus = Line(12.5, true); xStatus.Margin = new Thickness(0, 6, 0, 0);
        xBuild = Line(11.5); xBuild.Opacity = 0.85; xBuild.Margin = new Thickness(0, 2, 0, 0);
        xNote = Line(12, true); xNote.Foreground = PrepWarning; xNote.Margin = new Thickness(0, 6, 0, 0);

        // Before a run: the weapon and difficulty for the whole run.
        (Border, TextBlock) Chip(Action click)
        {
            var text = new TextBlock { Foreground = Brushes.White, FontSize = 12.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            var chip = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 6, 0), MinWidth = 64,
                BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand, Child = text };
            chip.MouseLeftButtonDown += (_, e) => { click(); e.Handled = true; };
            return (chip, text);
        }
        xSetupLabel = Line(12); xSetupLabel.Margin = new Thickness(0, 10, 0, 4);
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
                var glyph = new TextBlock { FontSize = 16, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var node = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(19), Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Center,
                    Cursor = System.Windows.Input.Cursors.Hand, Child = glyph };
                node.MouseLeftButtonDown += (_, e) => { ExpeditionSelect(at); e.Handled = true; };
                column.Children.Add(node);
                expeditionNodes.Add((step, lane, node, glyph));
            }
            var number = new TextBlock { Foreground = Brushes.White, Opacity = 0.7, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center,
                Text = (step + 1).ToString() };
            if (step >= spec.Steps.Length) xBossLabel = number; // Named in the pet's language by UpdateExpeditionMap.
            column.Children.Add(number);
            row.Children.Add(column);
        }
        xMap = new StackPanel { Children = { row } };
        xNodeName = Line(14, true);
        xNodeGoal = Line(12.5); xNodeGoal.Margin = new Thickness(0, 3, 0, 0);
        xDetails = new Border { Background = PrepCardBack, CornerRadius = new CornerRadius(9), Padding = new Thickness(11, 8, 11, 9), Margin = new Thickness(0, 10, 0, 0),
            Child = new StackPanel { Children = { xNodeName, xNodeGoal } } };
        xMap.Children.Add(xDetails);
        xRecord = Line(11.5); xRecord.Opacity = 0.8; xRecord.Margin = new Thickness(0, 8, 0, 0);

        var (close, closeText, _) = HudButton(CancelExpeditionMap, plain: true);
        var (abandon, abandonText, _) = HudButton(() =>
        {
            if (!expeditionConfirmAbandon) { expeditionConfirmAbandon = true; UpdateExpeditionMap(); }
            else ExpeditionAbandon();
        }, plain: true);
        var (main, mainText, _) = HudButton(() =>
        {
            string? reason = Expedition?.Run == null ? ExpeditionStart() : ExpeditionFight();
            if (reason != null) ShowBattleNotice(reason);
        }, plain: true);
        main.Background = PrepChosen; main.Padding = new Thickness(18, 6, 18, 5); main.MinWidth = 110; main.Margin = new Thickness(0);
        mainText.FontSize = 14; mainText.FontWeight = FontWeights.Bold;
        xMain = main; xMainText = mainText; xAbandon = abandon; xAbandonText = abandonText; xCloseText = closeText;
        var actions = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
        DockPanel.SetDock(close, Dock.Left); DockPanel.SetDock(abandon, Dock.Left); DockPanel.SetDock(main, Dock.Right);
        actions.Children.Add(close); actions.Children.Add(abandon); actions.Children.Add(main);
        var panel = new Border
        {
            Background = HudBack, BorderBrush = Brushes.White, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12),
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
            double max = expedition.MaxHp(run.Build);
            Show(xStatus, $"{StepTitle(run.Step, run.Map.Count)} · HP {Math.Ceiling(run.Hp)}/{max:0} · {weaponName} · {levelName}");
            var parts = BuildParts(run.Build);
            Show(xBuild, L("Build: ", "Build: ") + (parts.Count == 0 ? L("chưa có thẻ", "no cards yet") : string.Join(" · ", parts)));
        }
        xSetup.Visibility = run == null ? Visibility.Visible : Visibility.Collapsed;

        // The map: the run's (or, before one, the spec's kinds with no encounters drawn yet).
        int now = run?.Step ?? -1;
        foreach (var (step, lane, node, glyph) in expeditionNodes)
        {
            string kind = expedition.Kind(step, lane);
            bool won = run != null && step < now && run.Path[step] == lane;
            bool passed = run != null && step < now && !won;
            bool current = step == now;
            bool selected = current && lane == expeditionLane;
            node.Background = won ? MapDone : current ? PrepChosen : passed ? MapLocked : MapAhead;
            node.BorderBrush = selected ? Brushes.White : PrepCardRim;
            node.BorderThickness = new Thickness(selected ? 3 : 1);
            node.Opacity = passed ? 0.5 : 1;
            node.Cursor = current ? System.Windows.Input.Cursors.Hand : null;
            glyph.Text = KindGlyph(kind);
            glyph.Foreground = won ? MapDoneInk : kind == ExpeditionSpec.Elite && !current ? EliteInk : Brushes.White;
            node.ToolTip = run != null && spec.Encounter(run.Map[step][lane]) is StageSpec encounter ? $"{KindName(kind)} — {encounter.Name(English)}" : KindName(kind);
        }
        for (int i = 0; i < expeditionLinks.Count; i++) expeditionLinks[i].Fill = run != null && i + 1 < now ? MapDone : MapLocked;

        if (run != null && expedition.Node(expeditionLane) is ExpeditionNode chosen)
        {
            xDetails!.Visibility = Visibility.Visible;
            xNodeName!.Text = $"{StepTitle(chosen.Step, run.Map.Count)} · {KindName(chosen.Kind)} — {chosen.Stage.Name(English)}";
            string threat = chosen.Threat > 1.001 ? L($" · quái mạnh ×{chosen.Threat:0.00}", $" · enemies ×{chosen.Threat:0.00}") : "";
            Show(xNodeGoal, chosen.Stage.Goal(English) + "\n" + ObjectiveText(chosen.Stage) + StageRules(chosen.Stage) + threat);
        }
        else xDetails!.Visibility = Visibility.Collapsed;

        var record = expedition.Record(run?.Weapon ?? expeditionWeapon, run?.Level ?? expeditionLevel);
        Show(xRecord, record.Runs == 0 ? null : L($"Kỷ lục ({weaponName} · {levelName}): thắng {record.Wins}/{record.Runs} chuyến · xa nhất {record.BestSteps}/{spec.StepCount} bậc", $"Records ({weaponName} · {levelName}): {record.Wins}/{record.Runs} runs won · furthest {record.BestSteps}/{spec.StepCount} steps")
            + (record.BestSeconds is double best ? L($" · nhanh nhất {Clock(best)}", $" · fastest {Clock(best)}") : ""));

        xMainText!.Text = run == null ? L("Bắt đầu chuyến", "Start the run") : L("Vào trận", "Fight");
        xCloseText!.Text = L("Đóng", "Close");
        xAbandonText!.Text = expeditionConfirmAbandon ? L("Bấm lần nữa để bỏ", "Click again to give up") : L("Bỏ chuyến", "Give up");
        xAbandon!.Visibility = run != null ? Visibility.Visible : Visibility.Collapsed;
        bool can = run == null ? BattlePrepRefusal() == null : BattleRefusal() == null && expedition.Node(expeditionLane) != null;
        if (xMain.IsEnabled != can) { xMain.IsEnabled = can; xMain.Opacity = can ? 1 : 0.5; }
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
        void Add(string text, double size = 12.5, Brush? brush = null, bool bold = false, double opacity = 1) { var line = Line(size, brush, bold, opacity); line.Text = text; lines.Children.Add(line); }
        if (!result.Ranked) Add(L("Dữ liệu tùy chỉnh: chuyến này không được lưu.", "Custom data: this run is not saved."), 11.5, PrepWarning);
        else if (ExpeditionWarning() is string warning) Add(warning, 11.5, PrepWarning);
        int steps = Expedition?.Spec.StepCount ?? step.Step + 1;
        if (step.RunWon) Add(L("Hoàn thành viễn chinh!", "Expedition complete!"), 13, RecordGold, true);
        else if (step.RunOver) Add(L($"Chuyến đi kết thúc ở bậc {step.Step + 1}/{steps}.", $"The run ended at step {step.Step + 1}/{steps}."), 12.5, PrepWarning, true);
        else
        {
            Add(L($"Qua bậc {step.Step + 1}/{steps} · HP mang tiếp {Math.Ceiling(step.Hp)}/{step.MaxHp:0}", $"Step {step.Step + 1}/{steps} done · HP carried on {Math.Ceiling(step.Hp)}/{step.MaxHp:0}"), 12.5, bold: true);
            if (step.Healed >= 0.5) Add(L($"Nghỉ chân: +{Math.Round(step.Healed)} HP", $"A short rest: +{Math.Round(step.Healed)} HP"), 11.5, opacity: 0.85);
        }
        if (result.Upgrades.Length > 0) Add(L("Build hiện tại: ", "Build now: ") + result.Upgrades, 11.5, opacity: 0.85);
        if (step.Fastest) Add(L("Kỷ lục mới: chuyến nhanh nhất!", "New record: fastest run!"), 12.5, RecordGold, true);
        else if (step.BestSteps && step.RunOver && !step.RunWon) Add(L("Kỷ lục mới: đi xa nhất!", "New record: furthest run!"), 12.5, RecordGold, true);

        var (main, mainText, _) = HudButton(() => choose(() => { if (OpenExpeditionMap() is string reason) ShowBattleNotice(reason); }), plain: true);
        main.Background = ResultMain; main.Padding = new Thickness(16, 6, 16, 5); main.Margin = new Thickness(0); main.HorizontalAlignment = HorizontalAlignment.Right;
        mainText.FontSize = 13.5; mainText.FontWeight = FontWeights.Bold;
        mainText.Text = step.RunOver ? L("Bản đồ viễn chinh", "Expedition map") : L("Tiếp tục", "Continue");
        var (home, homeText, _) = HudButton(() => choose(() => { }), plain: true);
        homeText.Text = step.RunOver ? L("Về desktop", "Back to desktop") : L("Về desktop (chuyến được lưu)", "Back to desktop (the run is saved)");
        home.Margin = new Thickness(0);
        var others = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), Children = { home } };
        return (lines, new StackPanel { Margin = new Thickness(0, 12, 0, 0), Children = { main, others } });
    }
}
