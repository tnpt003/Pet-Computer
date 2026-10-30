using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// 1.5 M29a (docs/UPDATE-1.5.md F1): the Custom Fight. The free battle's setup panel shows the presets
// (Default: the old free battle, with its records; then the user's own, at most six) and opens the
// builder in its place: slimes (a click goes Off → Few → Some → Many), a last boss, waves, enemies,
// elites, strength, cards, app icons and malware. Only what the Monster Book has met can be chosen
// (dark "???" tiles otherwise); at most two kinds of link and two cursor tricks in one fight. Every
// change is saved at once (custom-fights.json, CustomFightBook). The builder is a game panel like the
// others: mouse only, no focus, hidden by a fullscreen app; the setup panel stays the battle's owner
// meanwhile (battlePreparing), so nothing about the pet's ownership changes.
public sealed partial class PetWindow
{
    private CustomFightBook? customFights;
    private Window? fightBuilderWindow;
    private StackPanel? fightBuilderBody;
    private string? fightNote;
    private readonly List<(string? Id, Border Chip)> prepFightChips = new();
    private WrapPanel? prepFightRow;
    private TextBlock? prepFightLabel, prepFightHint, prepFightButtonText;
    private Border? prepFightButton;
    private StackPanel? prepFightBlock;
    private const string CustomPrefix = "custom:";

    public CustomFightBook FightBook
    {
        get
        {
            if (customFights != null) return customFights;
            customFights = persistPreferences ? CustomFightBook.Load(CustomFightBook.DefaultPath) : new();
            return customFights;
        }
    }
    private void SaveCustomFights() { if (persistPreferences && customFights != null) customFights.Save(CustomFightBook.DefaultPath); }

    public CustomFight? ChosenFight => FightBook.Chosen;
    public bool FightBuilderOpen => fightBuilderWindow != null;
    public bool FightBuilderVisible => fightBuilderWindow?.IsVisible == true;
    public string? FightNote => fightNote;
    public string FightBuilderText => fightBuilderBody is null ? "" : string.Join("\n", BagTexts(fightBuilderBody));
    private string CustomFightTitle(StageSpec stage) => L("Trận tự chọn · ", "Custom Fight · ") + stage.Name(preferences.Language);
    private string PresetName(int n) => LF($"Bộ {n}", $"Set {n}");

    // The battle a preset makes now (null: Default, gone, or nothing to fight).
    public StageSpec? CustomFightStage(string stageId) =>
        stageId.StartsWith(CustomPrefix, StringComparison.Ordinal) && FightBook.Find(stageId[CustomPrefix.Length..]) is CustomFight fight
            ? CustomFights.Build(BattleTuning, fight, MonsterBook) : null;
    private StageSpec? ChosenFightStage() => ChosenFight is CustomFight fight ? global::DesktopPet.CustomFights.Build(BattleTuning, fight, MonsterBook) : null;
    // The setup panel's Ready waits while the chosen preset makes no battle.
    private string? PrepFightRefusal() => PrepFree && ChosenFight != null && ChosenFightStage() == null
        ? L("bộ trận này chưa có quái hay trùm nào (mở Dựng trận để chọn).", "this set has no enemy or boss yet (open Build a fight to choose).") : null;

    // A preset chip (the setup panel and the builder): null id is Default.
    public bool ChooseFight(string? id)
    {
        if (!battlePreparing || !PrepFree) return false;
        if (id == CustomFight.DefaultId) id = null;
        if (id != null && FightBook.Find(id) == null) return false;
        if (FightBook.Selected != id) { FightBook.Selected = id; SaveCustomFights(); }
        fightNote = null;
        UpdateBattlePrep();
        FillFightBuilder();
        return true;
    }

    // The builder in the setup panel's place (free battle only). Null on success, otherwise why not.
    public string? OpenFightBuilder()
    {
        if (!battlePreparing || !PrepFree) return L("Không có bảng chuẩn bị trận tự chọn.", "No Custom Fight setup is open.");
        if (fightBuilderWindow != null) return null;
        fightNote = null;
        fightBuilderBody = new StackPanel { Width = 540 };
        var (back, backText) = PrimaryButton(() => CloseFightBuilder());
        backText.Text = L("Xong", "Done");
        var window = CreateGamePanel(fightBuilderBody, PanelActions(Array.Empty<UIElement>(), new UIElement[] { back }), "Pet custom fight", scene: PetSceneKind.Workshop);
        window.Closed += (_, _) => { if (fightBuilderWindow == window) { fightBuilderWindow = null; fightBuilderBody = null; } };
        fightBuilderWindow = window;
        FillFightBuilder();
        battlePrepWindow?.Hide();
        if (!context.Fullscreen) { window.Show(); PlaceGamePanel(window); }
        System.Diagnostics.Trace.WriteLine("Custom Fight builder opened.");
        return null;
    }

    // Done: back to the setup panel (the choices are already saved).
    public void CloseFightBuilder()
    {
        var window = fightBuilderWindow;
        fightBuilderWindow = null; fightBuilderBody = null;
        window?.Close();
        if (window == null) return;
        if (battlePreparing && battlePrepWindow != null && !context.Fullscreen) { battlePrepWindow.Show(); PlaceBattlePrep(); }
        UpdateBattlePrep();
    }

    // The builder's choices: each on the chosen preset only (Default is not changed), saved at once.
    private CustomFight? EditedFight => fightBuilderWindow != null && battlePreparing ? ChosenFight : null;

    private bool Edited(CustomFight fight, string? note = null)
    {
        fight.Normalize();
        fightNote = note;
        SaveCustomFights();
        FillFightBuilder();
        UpdateBattlePrep();
        return true;
    }

    // + New set: a copy of the chosen one (from Default: the old free battle's slimes and King).
    public CustomFight? FightAdd()
    {
        if (fightBuilderWindow == null || !battlePreparing) return null;
        if (FightBook.Full) { fightNote = LF($"Tối đa {CustomFightBook.MaxPresets} bộ: xóa bớt một bộ trước.", $"At most {CustomFightBook.MaxPresets} sets: delete one first."); FillFightBuilder(); return null; }
        var fight = FightBook.Add(ChosenFight, PresetName);
        if (fight == null) return null;
        // Only what the book has met comes along.
        foreach (var page in fight.Enemies.Keys.ToList()) if (!MonsterBook.Knows(page)) fight.Enemies.Remove(page);
        if (fight.Boss != null && !MonsterBook.Knows(fight.Boss)) fight.Boss = null;
        Edited(fight);
        return fight;
    }

    public bool FightDelete()
    {
        if (EditedFight is not CustomFight fight) return false;
        FightBook.Delete(fight.Id);
        fightNote = null;
        SaveCustomFights();
        FillFightBuilder();
        UpdateBattlePrep();
        return true;
    }

    // A slime tile: Off → Few → Some → Many → Off. Adding one past a limit is refused (and why).
    public CustomFightBlock FightCycleEnemy(string page)
    {
        if (EditedFight is not CustomFight fight) return CustomFightBlock.Locked;
        int level = fight.Enemies.GetValueOrDefault(page);
        if (level == 0 && global::DesktopPet.CustomFights.CanAdd(BattleTuning, MonsterBook, fight, page) is var block and not CustomFightBlock.None)
        {
            fightNote = BlockText(block, fight);
            FillFightBuilder();
            return block;
        }
        if (level >= CustomFight.MaxLevel) fight.Enemies.Remove(page); else fight.Enemies[page] = level + 1;
        Edited(fight);
        return CustomFightBlock.None;
    }

    // The last boss (null: none).
    public CustomFightBlock FightChooseBoss(string? page)
    {
        if (EditedFight is not CustomFight fight) return CustomFightBlock.Locked;
        if (page != null && page != fight.Boss && global::DesktopPet.CustomFights.CanAdd(BattleTuning, MonsterBook, fight, page, boss: true) is var block and not CustomFightBlock.None)
        {
            fightNote = BlockText(block, fight);
            FillFightBuilder();
            return block;
        }
        fight.Boss = page;
        Edited(fight);
        return CustomFightBlock.None;
    }

    public const string FightWaves = "waves", FightPerWave = "perWave", FightAlive = "alive", FightElites = "elites", FightThreat = "threat";

    // A − / + of the numbers (dir −1 or +1).
    public bool FightStep(string field, int dir)
    {
        if (EditedFight is not CustomFight fight || dir == 0) return false;
        dir = Math.Sign(dir);
        switch (field)
        {
            case FightWaves: fight.Waves += dir; break;
            case FightPerWave: fight.PerWave += dir; break;
            case FightAlive: fight.MaxAlive += dir; break;
            case FightElites:
                int at = Math.Max(0, Array.IndexOf(CustomFight.EliteSteps, fight.Elites));
                fight.Elites = CustomFight.EliteSteps[Math.Clamp(at + dir, 0, CustomFight.EliteSteps.Length - 1)];
                break;
            case FightThreat: fight.Threat += dir * CustomFight.ThreatStep; break;
            default: return false;
        }
        return Edited(fight);
    }

    public bool FightToggleCards()
    {
        if (EditedFight is not CustomFight fight) return false;
        fight.Cards = !fight.Cards;
        return Edited(fight);
    }

    public bool FightToggleIcon(string id)
    {
        if (EditedFight is not CustomFight fight || !MonsterBook.KnowsApp(id) || !CustomFight.IconOrder.Any(k => BattleSim.AppId(k) == id)) return false;
        if (!fight.Icons.Remove(id))
        {
            if (fight.Icons.Count >= 5) return false;
            fight.Icons.Add(id);
        }
        return Edited(fight);
    }

    // A malware chip: Off → Few → Some → Many → Off.
    public bool FightCycleMalware(string kind)
    {
        if (EditedFight is not CustomFight fight || !MonsterBook.KnowsApp(kind) || !MalwareCue.Kinds.Contains(kind)) return false;
        int level = fight.Malware.GetValueOrDefault(kind);
        if (level >= CustomFight.MaxLevel) fight.Malware.Remove(kind); else fight.Malware[kind] = level + 1;
        return Edited(fight);
    }

    private string BlockText(CustomFightBlock block, CustomFight fight)
    {
        var (links, mouse) = global::DesktopPet.CustomFights.Limits(BattleTuning, fight);
        return block switch
        {
            CustomFightBlock.Links => LF($"Tối đa {EnemyLinks.MaxKinds} loại liên kết trong một trận (đang có: {string.Join(", ", links.Select(LinkName))}).",
                $"At most {EnemyLinks.MaxKinds} kinds of link in one fight (already: {string.Join(", ", links.Select(LinkName))})."),
            CustomFightBlock.Mouse => LF($"Tối đa {BattleData.MaxMouseKinds} loại quái dùng chuột trong một trận (đang có: {string.Join(", ", mouse.Select(BookPageName))}).",
                $"At most {BattleData.MaxMouseKinds} cursor-trick kinds in one fight (already: {string.Join(", ", mouse.Select(BookPageName))})."),
            _ => L("Chưa gặp quái này: gặp nó trong trận để mở.", "Not met yet: meet it in a battle to open it.")
        };
    }

    private string LinkName(string kind) => kind switch
    {
        "tether" => L("dây xích", "chains"), "rhythm" => L("nhịp trống", "drums"), "ride" => L("cưỡi", "riding"),
        "merge" => L("hợp thể", "merging"), "aura" => L("hào quang cờ", "flag aura"), _ => kind
    };

    private string LevelName(int level) => level switch { 1 => L("Ít", "Few"), 2 => L("Vừa", "Some"), 3 => L("Nhiều", "Many"), _ => L("Tắt", "Off") };

    private string MalwareName(string kind) => kind switch
    {
        "ads" => L("Quảng cáo", "Ads"), "loading" => L("Vòng loading", "Loading ring"), "update" => L("Cập nhật bắt buộc", "Forced update"),
        "virus" => "virus.exe", "infect" => L("Virus vào icon", "Icon virus"), "captcha" => "Captcha",
        "battery" => L("Pin yếu", "Low battery"), "cursor" => L("Con trỏ đơ", "Frozen cursor"), _ => kind
    };

    private string IconName(BattleAppKind kind) => (BattleTuning.Apps ?? new AppsSpec()).App(kind).Name(preferences.Language);

    // A preset's one-line summary (the setup panel, the builder).
    private string FightSummary(CustomFight? fight)
    {
        if (fight == null) return L("Trận tự do cũ: 3 đợt slime rồi Vua Slime, không app, không thẻ. Có kỷ lục như trước.",
            "The old free battle: 3 waves of slimes, then the Slime King; no apps, no cards. Records as before.");
        var stage = global::DesktopPet.CustomFights.Build(BattleTuning, fight, MonsterBook);
        if (stage == null) return L("Chưa có quái hay trùm nào.", "No enemy or boss yet.");
        int kinds = fight.Enemies.Count(p => p.Value > 0 && MonsterBook.Knows(p.Key));
        int waves = stage.Waves.Count(w => w.Groups.Any(g => g.Pick != null));
        int enemies = waves == 0 ? 0 : Enumerable.Range(0, waves).Sum(i => fight.PerWave + i);
        string boss = fight.Boss is string b && MonsterBook.Knows(b) ? " · " + L("trùm ", "boss ") + BookPageName(b) : "";
        string apps = stage.CustomApps is CustomAppRules a ? " · " + LF($"{a.Icons.Length} app", $"{a.Icons.Length} app{(a.Icons.Length == 1 ? "" : "s")}")
            + (a.AnyMalware ? L(", có app độc", ", malware") : "") : " · " + L("không app", "no apps");
        return LF($"{waves} đợt · {enemies} quái ({kinds} loại){boss}{apps}", $"{waves} waves · {enemies} enemies ({kinds} kind{(kinds == 1 ? "" : "s")}){boss}{apps}")
            + (fight.Cards ? L(" · có thẻ", " · cards") : "") + L(" · không kỷ lục", " · no records");
    }

    // The setup panel's part (free battle only): a label, the preset chips, the summary and Build a fight.
    private StackPanel BuildPrepFight()
    {
        prepFightLabel = PanelLine(PetUi.SmallSize); prepFightLabel.Opacity = 0.75; prepFightLabel.VerticalAlignment = VerticalAlignment.Center;
        prepFightLabel.HorizontalAlignment = HorizontalAlignment.Left;
        prepFightRow = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
        prepFightHint = PanelLine(PetUi.CaptionSize); prepFightHint.Opacity = 0.85; prepFightHint.HorizontalAlignment = HorizontalAlignment.Left;
        var (button, text, _) = HudButton(() => { if (OpenFightBuilder() is string reason) ShowBattleNotice(reason); }, plain: true);
        button.HorizontalAlignment = HorizontalAlignment.Left; button.Margin = new Thickness(0, 0, 5, 5);
        button.Height = 28;
        prepFightButton = button; prepFightButtonText = text;
        prepFightBlock = new StackPanel { Margin = new Thickness(0, 8, 0, 0), Children = {
            prepFightLabel, prepFightRow,
            new ScrollViewer { Content = prepFightHint, Height = 30, Focusable = false, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } } };
        return prepFightBlock;
    }

    private void UpdatePrepFight()
    {
        if (prepFightRow == null || prepFightLabel == null || prepFightHint == null || prepFightButton == null) return;
        var shown = PrepFree ? Visibility.Visible : Visibility.Collapsed;
        if (prepFightBlock != null) prepFightBlock.Visibility = shown;
        prepFightLabel.Visibility = prepFightRow.Visibility = prepFightHint.Visibility = prepFightButton.Visibility = shown;
        if (!PrepFree) return;
        prepFightLabel.Text = L("Trận tự chọn", "Custom Fight");
        FillPresetChips(prepFightRow, prepFightChips);
        if (!prepFightRow.Children.Contains(prepFightButton)) prepFightRow.Children.Add(prepFightButton);
        foreach (var (_, chip) in prepFightChips) { chip.MinWidth = 72; chip.Height = 28; }
        prepFightHint.Text = FightSummary(ChosenFight);
        prepFightButtonText!.Text = L("Dựng trận…", "Build a fight…");
    }

    // Default, then the presets; the chosen one ticked. Rebuilt only when the set of presets changes.
    private void FillPresetChips(WrapPanel row, List<(string? Id, Border Chip)> chips)
    {
        var ids = new string?[] { null }.Concat(FightBook.Presets.Select(p => (string?)p.Id)).ToList();
        if (!chips.Select(c => c.Id).SequenceEqual(ids) || chips.Count == 0)
        {
            row.Children.Clear(); chips.Clear();
            foreach (var id in ids)
            {
                var chip = Chip(new TextBlock { Foreground = Brushes.White, FontSize = PetUi.SmallSize, FontWeight = FontWeights.SemiBold });
                string? chosen = id;
                chip.MouseLeftButtonDown += (_, e) => { ChooseFight(chosen); e.Handled = true; };
                row.Children.Add(chip); chips.Add((id, chip));
            }
        }
        foreach (var (id, chip) in chips)
        {
            ((TextBlock)chip.Child).Text = id == null ? L("Mặc định", "Default") : FightBook.Find(id)?.Name ?? "?";
            ShowChosen(chip, id == FightBook.Selected);
        }
    }

    private static Border Chip(UIElement content, bool enabled = true) => new()
    {
        CornerRadius = new CornerRadius(7), Padding = new Thickness(8, 3, 9, 3), Margin = new Thickness(0, 0, 5, 5), BorderThickness = new Thickness(1.5),
        Background = PrepCardBack, BorderBrush = PrepCardRim, Cursor = enabled ? System.Windows.Input.Cursors.Hand : null, Child = content, Opacity = enabled ? 1 : 0.55
    };

    // Every frame with the setup panel: a fullscreen app hides the builder too.
    private bool TickFightBuilder()
    {
        if (fightBuilderWindow == null) return false;
        if (context.Fullscreen) { if (fightBuilderWindow.IsVisible) fightBuilderWindow.Hide(); }
        else if (!fightBuilderWindow.IsVisible) { fightBuilderWindow.Show(); PlaceGamePanel(fightBuilderWindow); }
        if (battlePrepWindow?.IsVisible == true) battlePrepWindow.Hide();
        return true;
    }

    // The builder's content, rebuilt after each choice (the enemy pictures are cached).
    private void FillFightBuilder()
    {
        if (fightBuilderBody == null) return;
        var body = fightBuilderBody;
        body.Children.Clear();
        var fight = ChosenFight;
        var title = PanelLine(PetUi.HeadingSize, true, 540); title.Text = L("Dựng trận", "Build a fight");
        body.Children.Add(PanelTitle(title));
        var about = PanelLine(PetUi.SmallSize, maxWidth: 540); about.Foreground = PetUi.Game.Soft;
        about.Text = L("Chọn quái, trùm và app cho trận tự chọn. Chỉ chọn được thứ đã gặp trong Sổ tay quái. Vũ khí, độ khó và kỹ năng chọn ở bảng chuẩn bị.",
            "Choose the slimes, the boss and the apps of your Custom Fight. Only what the Monster Book has met can be chosen. Weapon, difficulty and skills are on the setup panel.");
        body.Children.Add(about);

        // The presets, + New set and Delete.
        var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var chips = new List<(string? Id, Border Chip)>();
        FillPresetChips(row, chips);
        var add = Chip(new TextBlock { Text = L("+ Bộ mới", "+ New set"), Foreground = Brushes.White, FontSize = PetUi.SmallSize, FontWeight = FontWeights.SemiBold }, !FightBook.Full);
        add.MouseLeftButtonDown += (_, e) => { FightAdd(); e.Handled = true; };
        row.Children.Add(add);
        if (fight != null)
        {
            var delete = Chip(new TextBlock { Text = L("Xóa bộ này", "Delete this set"), Foreground = Brushes.White, FontSize = PetUi.SmallSize });
            delete.MouseLeftButtonDown += (_, e) => { FightDelete(); e.Handled = true; };
            row.Children.Add(delete);
        }
        body.Children.Add(row);

        if (fight == null)
        {
            body.Children.Add(DetailCard(new Thickness(10, 8, 10, 8), Caption(FightSummary(null), PetUi.SmallSize, Brushes.White),
                Caption(L("Bộ Mặc định không sửa được. Bấm \"+ Bộ mới\" để dựng bộ của bạn từ nó.", "The Default set cannot be changed. Click \"+ New set\" to build your own from it."), PetUi.CaptionSize, PetUi.Game.Soft)));
            AddFightNote(body);
            return;
        }

        // The slimes: a click goes Off → Few → Some → Many.
        body.Children.Add(Heading(L("Quái · bấm để đổi: Tắt → Ít → Vừa → Nhiều", "Slimes · click: Off → Few → Some → Many")));
        var tiles = new WrapPanel();
        foreach (var type in global::DesktopPet.CustomFights.Kinds(BattleTuning)) tiles.Children.Add(FightTile(type, fight.Enemies.GetValueOrDefault(type.Id), () => FightCycleEnemy(type.Id)));
        body.Children.Add(tiles);

        // The last boss.
        body.Children.Add(Heading(L("Trùm cuối", "Last boss")));
        var bosses = new WrapPanel();
        var none = Chip(new TextBlock { Text = L("Không có trùm", "No boss"), Foreground = Brushes.White, FontSize = PetUi.SmallSize, FontWeight = FontWeights.SemiBold });
        ShowChosen(none, fight.Boss == null);
        none.VerticalAlignment = VerticalAlignment.Center;
        none.MouseLeftButtonDown += (_, e) => { FightChooseBoss(null); e.Handled = true; };
        bosses.Children.Add(none);
        foreach (var type in global::DesktopPet.CustomFights.Bosses(BattleTuning)) bosses.Children.Add(FightTile(type, fight.Boss == type.Id ? -1 : 0, () => FightChooseBoss(type.Id)));
        body.Children.Add(bosses);

        // The numbers.
        body.Children.Add(Heading(L("Trận", "Fight")));
        var numbers = new WrapPanel();
        numbers.Children.Add(Stepper(L("Số đợt", "Waves"), fight.Waves.ToString(CultureInfo.InvariantCulture), FightWaves));
        numbers.Children.Add(Stepper(L("Quái đợt đầu (+1 mỗi đợt)", "Enemies in wave 1 (+1 a wave)"), fight.PerWave.ToString(CultureInfo.InvariantCulture), FightPerWave));
        numbers.Children.Add(Stepper(L("Tối đa trên sàn", "On the floor at once"), fight.MaxAlive.ToString(CultureInfo.InvariantCulture), FightAlive));
        numbers.Children.Add(Stepper(L("Tinh anh", "Elites"), fight.Elites + "%", FightElites));
        numbers.Children.Add(Stepper(L("Sức mạnh quái", "Enemy strength"), "×" + fight.Threat.ToString("0.0", CultureInfo.InvariantCulture), FightThreat));
        var cards = Chip(new TextBlock { Text = (fight.Cards ? "✓ " : "") + L("Thẻ giữa đợt", "Cards between waves"), Foreground = Brushes.White, FontSize = PetUi.SmallSize, FontWeight = FontWeights.SemiBold });
        ShowChosen(cards, fight.Cards);
        cards.VerticalAlignment = VerticalAlignment.Center;
        cards.MouseLeftButtonDown += (_, e) => { FightToggleCards(); e.Handled = true; };
        numbers.Children.Add(cards);
        body.Children.Add(numbers);
        if (fight.Elites > 0) body.Children.Add(Caption(L("Mức Dễ không có tinh anh.", "Easy has no elites."), PetUi.CaptionSize, PetUi.Game.Soft));

        // The apps: icons on or off, malware Off → Few → Some → Many (needs at least one icon).
        body.Children.Add(Heading(L("App trong trận", "Battle apps")));
        var icons = new WrapPanel();
        foreach (var kind in CustomFight.IconOrder)
        {
            string id = BattleSim.AppId(kind);
            bool open = MonsterBook.KnowsApp(id), on = open && fight.Icons.Contains(id);
            var chip = Chip(new TextBlock { Text = open ? (on ? "✓ " : "") + IconName(kind) : "???", Foreground = Brushes.White, FontSize = PetUi.SmallSize, FontWeight = FontWeights.SemiBold }, open);
            ShowChosen(chip, on);
            chip.MouseLeftButtonDown += (_, e) => { if (open) FightToggleIcon(id); else { fightNote = L("Chưa gặp app này trong trận.", "Not met in a battle yet."); FillFightBuilder(); } e.Handled = true; };
            icons.Children.Add(chip);
        }
        body.Children.Add(icons);
        var malware = new WrapPanel();
        foreach (var kind in MalwareCue.Kinds)
        {
            bool open = MonsterBook.KnowsApp(kind);
            int level = open ? fight.Malware.GetValueOrDefault(kind) : 0;
            var chip = Chip(new TextBlock { Text = open ? MalwareName(kind) + ": " + LevelName(level) : "???", Foreground = Brushes.White, FontSize = PetUi.SmallSize }, open);
            ShowChosen(chip, level > 0);
            chip.MouseLeftButtonDown += (_, e) => { if (open) FightCycleMalware(kind); else { fightNote = L("Chưa gặp app độc này trong trận.", "Not met in a battle yet."); FillFightBuilder(); } e.Handled = true; };
            malware.Children.Add(chip);
        }
        body.Children.Add(malware);
        if (fight.Icons.Count == 0) body.Children.Add(Caption(L("Không có icon nào thì trận không có app và app độc.", "With no icon the fight has no apps and no malware."), PetUi.CaptionSize, PetUi.Game.Soft));

        var summary = Caption(FightSummary(fight), PetUi.SmallSize, PetUi.Game.Gold, true);
        summary.Margin = new Thickness(0, 10, 0, 0);
        body.Children.Add(summary);
        AddFightNote(body);
        if (fightBuilderWindow?.IsVisible == true) PlaceGamePanel(fightBuilderWindow);
    }

    private void AddFightNote(StackPanel body)
    {
        if (fightNote == null) return;
        var note = Caption(fightNote, PetUi.SmallSize, PrepWarning, true);
        note.Margin = new Thickness(0, 6, 0, 0);
        body.Children.Add(note);
    }

    private static TextBlock Caption(string text, double size, Brush brush, bool bold = false)
    {
        var line = PanelLine(size, bold, 540);
        line.Text = text; line.Foreground = brush;
        return line;
    }

    private static TextBlock Heading(string text)
    {
        var line = PanelLine(PetUi.SmallSize, true, 540);
        line.Text = text; line.Opacity = 0.8; line.Margin = new Thickness(0, 10, 0, 4);
        return line;
    }

    // An enemy tile: its picture (dark "???" until met), name and level (−1: a chosen boss).
    private Border FightTile(EnemyType type, int level, Action click)
    {
        bool open = MonsterBook.Knows(type.Id);
        var picture = BookPicture(type, false, open, 46, 38);
        picture.HorizontalAlignment = HorizontalAlignment.Center;
        var name = new TextBlock { Text = open ? type.Name(preferences.Language) : "???", Foreground = open ? Brushes.White : PetUi.Game.Soft, FontSize = PetUi.CaptionSize,
            FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 66 };
        var stack = new StackPanel { Children = { new Grid { Height = 38, Children = { picture } }, name } };
        if (open && level >= 0)
            stack.Children.Add(new TextBlock { Text = LevelName(level), Foreground = level > 0 ? PetUi.Game.Gold : PetUi.Game.Soft, FontSize = PetUi.CaptionSize,
                FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
        bool chosen = level != 0;
        var tile = new Border
        {
            Width = 72, MinHeight = 74, Margin = new Thickness(0, 0, 5, 5), Padding = new Thickness(2), CornerRadius = new CornerRadius(8),
            Background = chosen ? PetUi.Game.ChosenCard : open ? PrepCardBack : Brushes.Transparent, BorderBrush = chosen ? PetUi.Game.ChosenRim : PrepCardRim,
            BorderThickness = new Thickness(chosen ? 2 : 1.2), Child = stack, Cursor = open ? System.Windows.Input.Cursors.Hand : null
        };
        tile.MouseLeftButtonDown += (_, e) =>
        {
            if (open) click();
            else { fightNote = BlockText(CustomFightBlock.Locked, ChosenFight!); FillFightBuilder(); }
            e.Handled = true;
        };
        return tile;
    }

    // "Waves  − 3 +".
    private Border Stepper(string label, string value, string field)
    {
        var (minus, minusText, _) = HudButton(() => FightStep(field, -1), plain: true);
        var (plus, plusText, _) = HudButton(() => FightStep(field, 1), plain: true);
        minusText.Text = "−"; plusText.Text = "+";
        minus.Margin = plus.Margin = new Thickness(0); minus.Padding = plus.Padding = new Thickness(8, 1, 8, 2);
        var shown = new TextBlock { Text = value, Foreground = PetUi.Game.Gold, FontSize = PetUi.BodySize, FontWeight = FontWeights.Bold, MinWidth = 38,
            TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = label, Foreground = Brushes.White, FontSize = PetUi.CaptionSize, Opacity = 0.85, Margin = new Thickness(0, 0, 0, 2) };
        return new Border
        {
            CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 4, 8, 5), Margin = new Thickness(0, 0, 5, 5), Background = PrepCardBack,
            BorderBrush = PrepCardRim, BorderThickness = new Thickness(1.2),
            Child = new StackPanel { Children = { name, new StackPanel { Orientation = Orientation.Horizontal, Children = { minus, shown, plus } } } }
        };
    }
}
