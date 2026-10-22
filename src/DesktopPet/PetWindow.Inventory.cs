using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// The battle hub's Bag: what the cat carries from the stages, to look at only (nothing in it can
// be used, equipped or changed). Its numbers (max HP, damage, swings, reach, knock-back, running
// speed, damage taken) as the chosen weapon gives them, with the Adventure build carried into the
// next stage and with the Expedition run under way (its weapon, cards and relics); the cards,
// synergies and relics themselves; and the rewards: the Adventure's accessories and the
// Expedition's keepsakes, won or still to win. The numbers come from a BattleSim made for them
// alone (the battle's own rules), never from a second formula. It replaces the hub's mode cards in
// the same window (the hub's header, Close and rules stay), and reads the saved progress only.
public sealed partial class PetWindow
{
    private bool hubInventoryShown;
    private bool? hubInventoryEnglish;
    private StackPanel? hubInventory;
    private FrameworkElement? hubCardsRow;
    private TextBlock? hubBagText;
    private const double InventoryWidth = 536;

    public bool BattleInventoryShown => battleHubOpen && hubInventoryShown;
    // For checks: the Bag's texts.
    public string BattleInventoryText => hubInventory is null || !hubInventoryShown ? "" : string.Join("\n", InventoryTexts(hubInventory));

    private static IEnumerable<string> InventoryTexts(DependencyObject node) => node is TextBlock t
        ? (t.Visibility == Visibility.Visible && t.Text.Length > 0 ? new[] { t.Text } : Array.Empty<string>())
        : LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().SelectMany(InventoryTexts);

    // The Bag's button on the hub (and its ‹ Hub): null on success, otherwise why not.
    public string? ShowBattleInventory(bool show)
    {
        if (!battleHubOpen || hubInventory == null || hubCardsRow == null) return L("Sảnh chiến đấu chưa mở.", "The battle hub is not open.");
        if (show == hubInventoryShown) return null;
        hubInventoryShown = show;
        if (show) FillInventory();
        hubInventory.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        hubCardsRow.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        UpdateBattleHub();
        PlaceGamePanel(battleHubWindow);
        System.Diagnostics.Trace.WriteLine(show ? "Battle inventory opened." : "Battle inventory closed.");
        return null;
    }

    // Every hub refresh: only a language change rebuilds it (nothing it shows changes while open).
    private void RefreshInventory()
    {
        if (hubInventoryShown && hubInventoryEnglish != English) FillInventory();
    }

    private void FillInventory()
    {
        if (hubInventory == null) return;
        hubInventoryEnglish = English;
        hubInventory.Children.Clear();
        var settings = BattleTuning;
        var adventure = Adventure;
        var expedition = Expedition;
        var run = expedition?.Run;

        // Where the Adventure build goes next: the first stage not won yet (its carried build), or
        // the last stage's build once the chapter is all won.
        var chapter = adventure?.Chapter();
        IReadOnlyDictionary<string, int> adventureBuild = new Dictionary<string, int>();
        string? adventureWhere = null;
        if (adventure != null && chapter is { Stages.Length: > 0 })
        {
            var next = chapter.Stages.FirstOrDefault(s => !adventure.IsCompleted(s.Id));
            if (next != null)
            {
                adventureBuild = adventure.StartBuild(next);
                adventureWhere = L("Mang vào màn ", "Carried into ") + next.Name(English);
            }
            else
            {
                var last = chapter.Stages[^1];
                if (adventure.Progress.Builds.TryGetValue(last.Id, out var build)) adventureBuild = build;
                adventureWhere = L("Build thắng màn cuối ", "The build that won ") + last.Name(English);
            }
        }

        string weaponId = settings.Weapon(preferences.BattleWeapon).Id;
        var baseSim = new BattleSim(settings, 0, 1000, weapon: weaponId, seed: 0);
        var adventureSim = new BattleSim(settings, 0, 1000, weapon: weaponId, build: adventureBuild, seed: 0);
        var runSim = run == null ? null : new BattleSim(settings, 0, 1000, weapon: run.Weapon, build: run.Build, seed: 0, relics: run.Relics);

        // The numbers: one column per loadout, in gold where cards or relics change its weapon's own.
        var columns = new List<(string Head, BattleSim Sim, BattleSim Plain)>
        {
            (L("Gốc · ", "Base · ") + baseSim.Weapon.Name(English), baseSim, baseSim),
            (L("Phiêu lưu", "Adventure"), adventureSim, baseSim)
        };
        if (runSim != null) columns.Add((L("Viễn chinh · ", "Expedition · ") + runSim.Weapon.Name(English), runSim,
            runSim.Weapon.Id == weaponId ? baseSim : new BattleSim(settings, 0, 1000, weapon: run!.Weapon, seed: 0)));
        var rows = new (string Name, Func<BattleSim, double> Value, Func<double, string> Show)[]
        {
            (L("HP tối đa", "Max HP"), s => s.CatMaxHp, v => Number(v, "0")),
            (L("Sát thương mỗi đòn", "Damage per hit"), s => s.SwingDamage, v => Number(v, "0.#")),
            (L("Tốc độ đánh", "Attack speed"), s => s.SwingsPerSecond, v => Number(v, "0.00") + L("/giây", "/s")),
            (L("Tầm đánh", "Reach"), s => s.Weapon.Range, v => Number(v, "0")),
            (L("Đẩy lùi", "Knock-back"), s => s.Knock, v => Number(v, "0")),
            (L("Tốc độ chạy", "Running speed"), s => s.MoveFactor * 100, v => Number(v, "0") + "%"),
            (L("Sát thương phải nhận", "Damage taken"), s => s.HurtFactor * 100, v => Number(v, "0") + "%"),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foreach (var _ in columns) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        for (int r = 0; r <= rows.Length; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        void Cell(TextBlock text, int row, int column) { Grid.SetRow(text, row); Grid.SetColumn(text, column); grid.Children.Add(text); }
        for (int c = 0; c < columns.Count; c++)
        {
            var head = Small(columns[c].Head, soft: true); head.TextAlignment = TextAlignment.Right; head.MaxWidth = 116; head.HorizontalAlignment = HorizontalAlignment.Right;
            Cell(head, 0, c + 1);
        }
        for (int r = 0; r < rows.Length; r++)
        {
            var (name, value, show) = rows[r];
            var label = Small(name); label.Margin = new Thickness(0, 4, 0, 0);
            Cell(label, r + 1, 0);
            for (int c = 0; c < columns.Count; c++)
            {
                double v = value(columns[c].Sim);
                bool changed = show(v) != show(value(columns[c].Plain));
                var cell = Small(show(v), bold: true); cell.TextAlignment = TextAlignment.Right; cell.HorizontalAlignment = HorizontalAlignment.Right; cell.Margin = new Thickness(0, 4, 0, 0);
                if (changed) cell.Foreground = PetUi.Game.Gold;
                Cell(cell, r + 1, c + 1);
            }
        }
        var note = Caption(L("Số vàng: thẻ hoặc di vật đã đổi so với số gốc của vũ khí đó. Cổ vũ, combo và hiệu ứng chỉ có trong trận không tính ở đây.",
            "Gold: cards or relics changed it from that weapon's own number. Cheer, combos and other in-battle effects are not counted here."));
        note.Margin = new Thickness(0, 8, 0, 0);
        AddSection(L("Chỉ số", "Stats"), grid, note);

        // The Adventure build.
        if (adventure != null)
        {
            var lines = new List<UIElement> { Caption(adventureWhere ?? L("Chưa có màn nào", "No stages yet")) };
            if (adventureBuild.Count == 0) lines.Add(Small(L("Chưa có thẻ (thắng màn để giữ thẻ đã chọn sang màn sau).", "No cards yet (win a stage to keep its cards for the next).")));
            else lines.AddRange(BuildLines(adventureSim));
            AddSection(L("Build Phiêu lưu", "Adventure build"), lines.ToArray());
        }

        // The Expedition run under way: its HP, cards and relics.
        if (expedition != null && expedition.IsUnlocked(AdventureProgress))
        {
            var lines = new List<UIElement>();
            if (run != null && runSim != null)
            {
                lines.Add(Caption(StepTitle(Math.Min(run.Step, run.Map.Count - 1), run.Map.Count) + $" · HP {Math.Ceiling(run.Hp)}/{runSim.CatMaxHp:0} · "
                    + DifficultyName(run.Level)));
                var cards = BuildLines(runSim);
                if (cards.Count == 0) lines.Add(Small(L("Chưa có thẻ.", "No cards yet.")));
                lines.AddRange(cards);
                var maxRelics = expedition.Spec.MaxRelics;
                lines.Add(Small(L($"Di vật ({runSim.Relics.Count}/{maxRelics})", $"Relics ({runSim.Relics.Count}/{maxRelics})"), bold: true, top: 8));
                if (runSim.Relics.Count == 0) lines.Add(Small(L("Chưa có di vật (thắng điểm tinh anh để chọn).", "No relics yet (win an elite node to choose one).")));
                foreach (var relic in runSim.Relics) lines.Add(Item("◈ " + relic.Name(English), relic.Describe(English)));
            }
            else lines.Add(Small(L("Không có chuyến nào đang đi. Thẻ và di vật của một chuyến chỉ giữ đến hết chuyến.", "No run under way. A run's cards and relics last until the run ends.")));
            AddSection(L("Chuyến Viễn chinh", "Expedition run"), lines.ToArray());
        }

        // Rewards: the Adventure's accessories (by the stage that gives each) and the keepsakes.
        var rewards = new List<UIElement>();
        if (adventure != null)
            foreach (var stage in adventure.Campaign.Chapters.SelectMany(c => c.Stages))
                if (stage.Reward is string id && PetAccessories.Find(id) is AccessorySpec accessory)
                    rewards.Add(adventure.HasReward(id)
                        ? Item("✓ " + accessory.Name(English), L("Phụ kiện · đeo trong Cài đặt → Mèo → Phụ kiện", "Accessory · wear it from Settings → Pet → Accessory"), true)
                        : Item("? " + accessory.Name(English), L("Thắng màn ", "Win the stage ") + stage.Name(English) + L(" để nhận", " to get it")));
        if (expedition != null)
            foreach (var k in expedition.Spec.Keepsakes)
            {
                bool had = expedition.Progress.Keepsakes.Contains(k.Id);
                string what = k.Kind switch
                {
                    KeepsakeSpec.Cup => L("Cúp · trên kệ desktop", "Cup · on the desktop shelf"),
                    KeepsakeSpec.Cushion => L("Đệm · trên kệ desktop", "Cushion · on the desktop shelf"),
                    _ => L("Phụ kiện · đeo trong Cài đặt → Mèo → Phụ kiện", "Accessory · wear it from Settings → Pet → Accessory")
                };
                string level = Enum.TryParse<BattleDifficulty>(k.Difficulty, out var at) ? DifficultyName(at) : k.Difficulty;
                rewards.Add(had ? Item("✓ " + k.Name(English), what, true)
                    : Item("? " + k.Name(English), L($"Thắng một chuyến Viễn chinh {level}" + (k.Exact ? "" : " trở lên"), $"Win an Expedition run on {level}" + (k.Exact ? "" : " or harder"))));
            }
        if (rewards.Count == 0) rewards.Add(Small(L("Chưa có phần thưởng nào trong dữ liệu.", "No rewards in the data.")));
        AddSection(L("Phần thưởng", "Rewards"), rewards.ToArray());
    }

    // The build's cards (×times taken, what each does), its synergies (★) and rare cards (◆).
    private List<UIElement> BuildLines(BattleSim sim)
    {
        var lines = new List<UIElement>();
        foreach (var group in BuildGroups.All)
        {
            var cards = sim.Settings.UpgradeList.Where(u => u.Group == group && sim.Stacks(u.Id) > 0).ToList();
            if (cards.Count == 0) continue;
            lines.Add(Small($"{BuildGroups.Name(group, English)} {sim.GroupPoints(group)}", bold: true, top: lines.Count == 0 ? 4 : 8));
            foreach (var u in cards)
                lines.Add(Item((u.Rare ? "◆ " : "• ") + u.Name(English) + (sim.Stacks(u.Id) > 1 ? $" ×{sim.Stacks(u.Id)}" : ""), u.Describe(English), u.Rare));
        }
        foreach (var u in sim.Settings.UpgradeList.Where(u => u.Group == null && sim.Stacks(u.Id) > 0))
            lines.Add(Item((u.Rare ? "◆ " : "• ") + u.Name(English) + (sim.Stacks(u.Id) > 1 ? $" ×{sim.Stacks(u.Id)}" : ""), u.Describe(English), u.Rare));
        foreach (var y in sim.Synergies) lines.Add(Item("★ " + y.Name(English), y.Describe(English), true));
        return lines;
    }

    private void AddSection(string title, params UIElement[] lines)
    {
        var heading = PanelLine(PetUi.TitleSize, true, InventoryWidth);
        heading.Text = title;
        heading.Margin = new Thickness(0, hubInventory!.Children.Count == 0 ? 0 : 12, 0, 0);
        hubInventory.Children.Add(heading);
        var card = DetailCard(new Thickness(11, 8, 11, 10), lines);
        card.Margin = new Thickness(0, 6, 0, 0);
        hubInventory.Children.Add(card);
    }

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static TextBlock Small(string text, bool bold = false, double top = 0, bool soft = false)
    {
        var line = PanelLine(PetUi.SmallSize, bold, InventoryWidth - 24);
        line.Text = text;
        line.Margin = new Thickness(0, top, 0, 0);
        if (soft) line.Foreground = PetUi.Game.Soft;
        return line;
    }

    private static TextBlock Caption(string text)
    {
        var line = PanelLine(PetUi.CaptionSize, maxWidth: InventoryWidth - 24);
        line.Text = text;
        line.Foreground = PetUi.Game.Soft;
        return line;
    }

    // A named thing and what it does, on two lines; had: its name in gold.
    private static StackPanel Item(string name, string about, bool had = false)
    {
        var title = Small(name, bold: true);
        if (had) title.Foreground = PetUi.Game.Gold;
        var desc = Caption(about);
        desc.Margin = new Thickness(12, 0, 0, 0);
        return new StackPanel { Margin = new Thickness(0, 4, 0, 0), Children = { title, desc } };
    }
}
