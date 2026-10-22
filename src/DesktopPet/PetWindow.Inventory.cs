using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// The Bag: what the cat carries in the mode it is playing, to look at only (nothing in it can be
// used or changed). It opens from the battle HUD (the battle pauses while it is open, BattlePause.Bag),
// from the Adventure map (the build carried into the chosen stage) and from the Expedition map (the
// run: also while a capsule machine, rest or event waits; not while a capsule opens). A floating
// window the user can drag by its header (where it was dragged is kept for the session, on the
// cat's monitor), at most BagMaxHeight tall with its content scrolling, three tabs:
//  Stats: max HP, damage, attack speed, reach, knock-back, running speed, damage taken, each with
//   an icon and a bar (the weapon's own part, then what cards and relics add in gold or take away
//   in red); warnings (a relic's price, low HP, Reckless switched on); the skills' cooldowns and the
//   fish's heal with the relics' changes; in a battle "This battle" (enemies defeated and what the
//   build's effects have done) and, over a card offer, the card under the mouse (or picked) as a
//   preview: what it does, the synergy it would switch on, and → the numbers after it in blue.
//  Cards: the build as picture tiles (×n, rare ◆), each group's progress to its synergies.
//  Relics: the Expedition run's relics (gain ▲, price ▼) and its log; outside the Expedition the
//   chapter's progress instead.
// The numbers come from a BattleSim (the battle's own rules; in a battle the battle itself). Like the
// other game panels it never takes focus, has no taskbar button and hides while a fullscreen app
// runs. It closes when its battle or map goes away, or with ×.
public sealed partial class PetWindow
{
    public const string BagStatsTab = "stats", BagCardsTab = "cards", BagRelicsTab = "relics";
    public const string BagBattle = "battle", BagAdventure = "adventure", BagExpedition = "expedition";
    public const double BagWidth = 440, BagMaxHeight = 540;
    private static readonly string[] BagTabs = { BagStatsTab, BagCardsTab, BagRelicsTab };
    private static readonly Brush BagBad = PetUi.Hex("#FF9A8A"), BagCream = PetUi.Hex("#E8DCCB");

    private Window? bagWindow;
    private string? bagContext;
    private string bagTab = BagStatsTab;
    private string bagSignature = "";
    private double bagRefresh;
    private bool bagPlacing;
    // Where the user dragged the Bag (DIPs; null: not dragged in this session).
    private Point? bagPlace;
    private StackPanel? bagBody;
    private TextBlock? bagTitle, bagSubtitle, bagHpText;
    private Border? bagHpBar, bagHpFill;
    private readonly List<(string Tab, Border Button, TextBlock Text)> bagTabButtons = new();

    public bool BattleBagOpen => bagWindow != null;
    public bool BattleBagVisible => bagWindow?.IsVisible == true;
    public string BattleBagTab => bagTab;
    public string? BattleBagMode => bagWindow == null ? null : BagData()?.Mode;
    // For checks: every text the Bag shows now.
    public string BattleBagText => bagWindow?.Content is DependencyObject content ? string.Join("\n", BagTexts(content)) : "";

    private static IEnumerable<string> BagTexts(DependencyObject node)
    {
        if (node is not TextBlock t) return LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().SelectMany(BagTexts);
        // A line built from coloured runs keeps its words there.
        string text = t.Inlines.Count > 0 ? string.Concat(t.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text)) : t.Text;
        return t.Visibility == Visibility.Visible && text.Length > 0 ? new[] { text } : Array.Empty<string>();
    }

    // What the Bag belongs to now: the battle, or the map that is open (null: nothing).
    private string? BagContext() => battle != null ? BagBattle : adventureMapOpen ? BagAdventure : expeditionMapOpen ? BagExpedition : null;

    // Null when the Bag is open (on tab, when given), otherwise why not.
    public string? OpenBattleBag(string? tab = null)
    {
        string? where = BagContext();
        if (closed) return L("Mèo đã đóng.", "The pet is closed.");
        if (where == null) return L("Túi đồ mở từ HUD trận hoặc bản đồ Phiêu lưu/Viễn chinh.", "The Bag opens from the battle HUD or the Adventure/Expedition map.");
        if (context.Fullscreen) return L("Đang có ứng dụng toàn màn hình.", "A fullscreen app is open.");
        if (where == BagExpedition && xRevealTimer != null) return L("Đợi viên nang mở xong.", "Wait for the capsule to open.");
        if (tab != null && Array.IndexOf(BagTabs, tab) >= 0) bagTab = tab;
        if (bagWindow != null && bagContext != where) CloseBattleBag();
        if (bagWindow == null)
        {
            bagContext = where;
            ShowBagWindow();
            System.Diagnostics.Trace.WriteLine($"Bag opened ({where}).");
        }
        if (where == BagBattle) SetBattlePause(BattlePause.Bag, true);
        FillBag(force: true);
        return null;
    }

    public string? ShowBattleBagTab(string tab)
    {
        if (bagWindow == null) return L("Túi đồ chưa mở.", "The Bag is not open.");
        if (Array.IndexOf(BagTabs, tab) < 0) return L("Không có mục này.", "There is no such tab.");
        bagTab = tab;
        FillBag(force: true);
        return null;
    }

    // ×, the HUD's button again, its battle or map gone, reset, closing the pet. A battle goes on.
    public void CloseBattleBag()
    {
        if (bagWindow == null) return;
        var window = bagWindow;
        bagWindow = null;
        window.Close();
        bagTabButtons.Clear();
        bagBody = null; bagTitle = bagSubtitle = bagHpText = null; bagHpBar = bagHpFill = null;
        bagSignature = "";
        if (bagContext == BagBattle) SetBattlePause(BattlePause.Bag, false);
        bagContext = null;
        System.Diagnostics.Trace.WriteLine("Bag closed.");
    }

    // Every frame, paused or not.
    private void TickBattleBag(double dt)
    {
        if (bagWindow == null) return;
        if (BagContext() != bagContext || closed) { CloseBattleBag(); return; }
        if (context.Fullscreen) { if (bagWindow.IsVisible) bagWindow.Hide(); return; }
        if (!bagWindow.IsVisible) { bagWindow.Show(); PlaceBag(); }
        // A capsule opening keeps its prize a secret until it pops: the Bag waits for it.
        if (bagContext == BagExpedition && xRevealTimer != null) return;
        bagRefresh += dt;
        if (bagRefresh < 0.25) return;
        bagRefresh = 0;
        FillBag(force: false);
    }

    private void ShowBagWindow()
    {
        var view = BagData();
        var cat = new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1, SwordOut = true,
            Hammer = view?.Sim.Weapon.Style == "hammer" };
        var catView = new PetRenderer(cat) { Coat = renderer.Coat, Accessory = renderer.Accessory, Width = 192, Height = 192 };
        var stage = new Border { Background = PetUi.Game.Stage, CornerRadius = new CornerRadius(25), Width = 50, Height = 50, Margin = new Thickness(0, 0, 10, 0),
            BorderBrush = PetUi.Game.CardRim, BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Top,
            Child = CatPicture(new Grid { Width = 192, Height = 192, Children = { catView } }) };
        bagTitle = PanelLine(PetUi.TitleSize, true, 300);
        bagSubtitle = PanelLine(PetUi.CaptionSize, maxWidth: 300); bagSubtitle.Foreground = PetUi.Game.Soft; bagSubtitle.Margin = new Thickness(0, 1, 0, 0);
        bagHpFill = new Border { Background = PetUi.Hex("#7FD06A"), CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
        bagHpBar = new Border { Width = 150, Height = 6, Background = PetUi.Game.BarBack, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center, Child = bagHpFill };
        bagHpText = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.CaptionSize, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var hpRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0), Children = { bagHpBar, bagHpText } };
        var close = new Border
        {
            Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = PetUi.Game.ButtonBack, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = "×", Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -2, 0, 0) }
        };
        close.MouseLeftButtonDown += (_, e) => { CloseBattleBag(); e.Handled = true; };
        close.MouseEnter += (_, _) => close.Background = PetUi.Game.Chosen;
        close.MouseLeave += (_, _) => close.Background = PetUi.Game.ButtonBack;
        var header = new DockPanel { Background = Brushes.Transparent, Cursor = Cursors.SizeAll, Children = { stage, close, new StackPanel { Children = { bagTitle, bagSubtitle, hpRow } } } };
        DockPanel.SetDock(stage, Dock.Left); DockPanel.SetDock(close, Dock.Right);
        // Dragging the header moves the window (it stays where it is put for the session).
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState != MouseButtonState.Pressed || bagWindow == null) return;
            try { bagWindow.DragMove(); } catch (InvalidOperationException) { }
        };

        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 8) };
        bagTabButtons.Clear();
        foreach (var tab in BagTabs)
        {
            var text = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.SmallSize, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            var icon = new BagIcon { Kind = tab == BagStatsTab ? "stats" : tab == BagCardsTab ? "cards" : "relic", Width = 18, Height = 18, Margin = new Thickness(0, 0, 6, 0) };
            var button = new Border
            {
                CornerRadius = new CornerRadius(PetUi.Game.ButtonRadius), Padding = new Thickness(10, 4, 12, 4), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand,
                BorderThickness = new Thickness(1.5), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, text } }
            };
            string chosen = tab;
            button.MouseLeftButtonDown += (_, e) => { ShowBattleBagTab(chosen); e.Handled = true; };
            tabs.Children.Add(button);
            bagTabButtons.Add((tab, button, text));
        }

        bagBody = new StackPanel();
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Content = bagBody };
        scroll.Resources.MergedDictionaries.Add(PetControlStyle.Resources);
        var body = new DockPanel();
        DockPanel.SetDock(header, Dock.Top); DockPanel.SetDock(tabs, Dock.Top);
        body.Children.Add(header); body.Children.Add(tabs); body.Children.Add(scroll);
        var panel = new Border
        {
            Background = HudBack, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 12, 14, 12), Margin = new Thickness(6), Width = BagWidth, Child = body,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
        };
        panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
        var window = CreateBattleWindow(panel, "Pet bag");
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.LocationChanged += (_, _) => { if (!bagPlacing && window.IsVisible && window == bagWindow) bagPlace = new Point(window.Left, window.Top); };
        window.Closed += (_, _) => { if (bagWindow == window) bagWindow = null; };
        bagWindow = window;
        bagSignature = "";
        FillBag(force: true);
        window.Show();
        PlaceBag();
    }

    // On the cat's monitor: where the user put it, otherwise near its top right, under the HUD.
    private void PlaceBag()
    {
        if (bagWindow == null) return;
        bagWindow.MaxHeight = Math.Max(240, Math.Min(BagMaxHeight, bounds.Bottom - bounds.Top - 24));
        bagWindow.UpdateLayout();
        double w = bagWindow.ActualWidth, h = bagWindow.ActualHeight;
        var at = bagPlace ?? new Point(bounds.Right - w - 16, bounds.Top + 110);
        double left = Math.Clamp(at.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - w));
        double top = Math.Clamp(at.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - h));
        bagPlacing = true;
        try { MoveWindow(bagWindow, left, top); }
        finally { bagPlacing = false; }
    }

    // What the Bag shows: the mode, the cat's numbers (Sim: with the build and relics; Plain: the
    // weapon alone), its HP now (null: not in a run or battle), a line saying where, the run.
    private sealed record BagView(string Mode, BattleSim Sim, BattleSim Plain, double? Hp, string Where, Expedition? X, ExpeditionRun? Run, bool Live);

    private BagView? BagData()
    {
        var settings = BattleTuning;
        BattleSim Plain(BattleSettings s, string weapon) => new(s, 0, 1000, weapon: weapon, seed: 0);
        if (battle is BattleSim b)
        {
            var plain = Plain(b.Settings, b.Weapon.Id);
            if (battleExpedition is ExpeditionBattle x)
                return new(BagExpedition, b, plain, b.CatHp, StepTitle(x.Node.Step, x.Expedition.Spec.StepCount) + " · " + DifficultyName(b.Difficulty), x.Expedition, x.Expedition.Run, true);
            string where = (b.Stage is StageSpec stage ? StageTitle(stage) : L("Trận", "Battle")) + " · " + DifficultyName(b.Difficulty);
            return new(b.Stage != null ? BagAdventure : BagBattle, b, plain, b.CatHp, where, null, null, true);
        }
        string weaponId = settings.Weapon(preferences.BattleWeapon).Id;
        if (bagContext == BagExpedition && Expedition is Expedition expedition)
        {
            if (expedition.Run is ExpeditionRun run)
            {
                var sim = new BattleSim(settings, 0, 1000, weapon: run.Weapon, build: run.Build, seed: 0, relics: run.Relics);
                return new(BagExpedition, sim, Plain(settings, sim.Weapon.Id), run.Hp,
                    StepTitle(Math.Min(run.Step, Math.Max(0, run.Map.Count - 1)), run.Map.Count) + " · " + DifficultyName(run.Level), expedition, run, false);
            }
            var none = Plain(settings, weaponId);
            return new(BagExpedition, none, none, null, L("Chưa có chuyến nào", "No run under way"), expedition, null, false);
        }
        IReadOnlyDictionary<string, int> build = new Dictionary<string, int>();
        string line = "";
        if (Adventure is Adventure adventure && adventure.Chapter() is ChapterSpec chapter)
        {
            var stage = adventure.Stage(mapStageId) ?? chapter.Stages.FirstOrDefault(s => !adventure.IsCompleted(s.Id)) ?? chapter.Stages.LastOrDefault();
            if (stage != null)
            {
                build = adventure.StartBuild(stage);
                line = L("Build mang vào ", "Build carried into ") + StageTitle(stage);
            }
        }
        return new(BagAdventure, new BattleSim(settings, 0, 1000, weapon: weaponId, build: build, seed: 0), Plain(settings, weaponId), null, line, null, null, false);
    }

    private void FillBag(bool force)
    {
        if (bagWindow == null || bagBody == null || bagTitle == null || bagSubtitle == null) return;
        var view = BagData();
        if (view == null) return;
        var sim = view.Sim;
        // Rebuilt only when something it shows changed.
        string signature = string.Join("|", view.Mode, English, bagTab, view.Where, view.Hp is double h ? Math.Round(h).ToString(CultureInfo.InvariantCulture) : "-",
            Math.Round(sim.CatMaxHp).ToString(CultureInfo.InvariantCulture), sim.Reckless,
            string.Join(",", sim.UpgradesTaken.Where(t => t.Value > 0).Select(t => t.Key + t.Value)), string.Join(",", sim.Relics.Select(r => r.Id)),
            view.Run?.Log.Count ?? 0, preferences.Name, BattleUpgradePreview ?? "");
        if (!force && signature == bagSignature) return;
        bagSignature = signature;

        bagTitle.Text = L("Túi đồ · ", "Bag · ") + (view.Mode switch
        {
            BagExpedition => L("Viễn chinh", "Expedition"), BagAdventure => L("Phiêu lưu", "Adventure"), _ => L("Trận", "Battle")
        });
        bagSubtitle.Text = $"{preferences.Name} · {sim.Weapon.Name(English)}" + (view.Where.Length > 0 ? " · " + view.Where : "");
        bool hasHp = view.Hp is double;
        bagHpBar!.Visibility = bagHpText!.Visibility = hasHp ? Visibility.Visible : Visibility.Collapsed;
        if (view.Hp is double hp)
        {
            double share = Math.Clamp(hp / Math.Max(1, sim.CatMaxHp), 0, 1);
            bagHpFill!.Width = 150 * share;
            bagHpFill.Background = share < 0.35 ? BagBad : PetUi.Hex("#7FD06A");
            bagHpText.Text = $"HP {Math.Ceiling(hp)}/{sim.CatMaxHp:0}";
        }
        foreach (var (tab, button, text) in bagTabButtons)
        {
            text.Text = tab switch { BagStatsTab => L("Chỉ số", "Stats"), BagCardsTab => L("Thẻ", "Cards"), _ => L("Di vật", "Relics") };
            ShowChosen(button, tab == bagTab);
        }
        bagBody.Children.Clear();
        switch (bagTab)
        {
            case BagCardsTab: FillBagCards(view); break;
            case BagRelicsTab: FillBagRelics(view); break;
            default: FillBagStats(view); break;
        }
        if (bagPlace == null || !bagWindow.IsVisible) PlaceBag();
    }

    // ── Stats ────────────────────────────────────────────────────────────────────────────────

    private static readonly Brush BagPreviewInk = PetUi.Hex("#8FD3FF");

    // A card on offer under the mouse (or picked): the build as it is and as it would be with it.
    private (UpgradeSpec Card, BattleSim Before, BattleSim After)? BagPreview(BagView view)
    {
        if (!view.Live || BattleUpgradePreview is not string id || view.Sim.UpgradeOffer?.FirstOrDefault(u => u.Id == id) is not UpgradeSpec card) return null;
        var build = view.Sim.UpgradesTaken.Where(t => t.Value > 0).ToDictionary(t => t.Key, t => t.Value);
        var relics = view.Sim.Relics.Select(r => r.Id).ToList();
        var before = new BattleSim(view.Sim.Settings, 0, 1000, weapon: view.Sim.Weapon.Id, build: build, seed: 0, relics: relics);
        build[card.Id] = build.GetValueOrDefault(card.Id) + 1;
        return (card, before, new BattleSim(view.Sim.Settings, 0, 1000, weapon: view.Sim.Weapon.Id, build: build, seed: 0, relics: relics));
    }

    // "If you take Sharp edge": what it does, the synergy it would switch on, how to read the arrows
    // (numbersChange null: the Cards tab, where there are no numbers to read).
    private void AddPreviewCard(BagView view, UpgradeSpec card, bool? numbersChange)
    {
        var first = BagLine(L("Nếu lấy: ", "If you take: ") + card.Name(English), PetUi.SmallSize, BagPreviewInk, bold: true);
        first.Margin = new Thickness(0);
        var lines = new StackPanel { Children = { first, BagLine(card.Describe(English), PetUi.CaptionSize, Brushes.White) } };
        if (view.Sim.Unlocks(card) is SynergySpec synergy)
            lines.Children.Add(BagLine(L($"Sẽ mở ★ {synergy.Name(false)}: {synergy.Describe(false)}", $"Switches on ★ {synergy.Name(true)}: {synergy.Describe(true)}"), PetUi.CaptionSize, PetUi.Game.Gold, bold: true));
        string heal = Number(Math.Max(0, Math.Min(card.Amount, view.Sim.CatMaxHp - view.Sim.CatHp)), "0");
        string? hint = card.Effect == "heal" ? L($"Hồi ngay {heal} HP (thẻ hồi máu không giữ sang màn sau).", $"Heals {heal} HP now (a heal is not carried on).")
            : numbersChange == true ? L("Số xanh sau dấu → là chỉ số khi đã lấy thẻ này.", "Blue numbers after → are the stats with this card.")
            : numbersChange == false ? L("Không đổi các chỉ số ở đây: tác dụng của nó hiện ra trong trận.", "Changes none of the numbers here: it works in the fight itself.")
            : null;
        if (hint != null) lines.Children.Add(BagLine(hint, PetUi.CaptionSize, PetUi.Game.Soft));
        var box = BagCard(lines, bottom: 8);
        box.BorderBrush = BagPreviewInk; box.BorderThickness = new Thickness(1.5);
        bagBody!.Children.Add(box);
    }

    // Each number: its icon, its name, how it is shown, its bar's full length, and whether more is better.
    private (string Icon, string Name, Func<BattleSim, double> Value, double Max, bool Higher, Func<double, string> Show)[] StatRows() => new (string, string, Func<BattleSim, double>, double, bool, Func<double, string>)[]
    {
        ("maxHp", L("HP tối đa", "Max HP"), s => s.CatMaxHp, 200, true, v => Number(v, "0")),
        ("damage", L("Sát thương mỗi đòn", "Damage per hit"), s => s.SwingDamage, 80, true, v => Number(v, "0.#")),
        ("attackSpeed", L("Tốc độ đánh", "Attack speed"), s => s.SwingsPerSecond, 3, true, v => Number(v, "0.00") + L("/giây", "/s")),
        ("reach", L("Tầm đánh", "Reach"), s => s.Weapon.Range, 140, true, v => Number(v, "0")),
        ("knock", L("Đẩy lùi", "Knock-back"), s => s.Knock, 400, true, v => Number(v, "0")),
        ("moveSpeed", L("Tốc độ chạy", "Running speed"), s => s.MoveFactor * 100, 200, true, v => Number(v, "0") + "%"),
        ("armor", L("Sát thương phải nhận", "Damage taken"), s => s.HurtFactor * 100, 150, false, v => Number(v, "0") + "%"),
    };

    private void FillBagStats(BagView view)
    {
        var sim = view.Sim;
        var rows = StatRows();
        var preview = BagPreview(view);
        if (preview is var (card, before, after))
            AddPreviewCard(view, card, rows.Any(r => r.Show(r.Value(before)) != r.Show(r.Value(after))));

        var warnings = new List<(string Text, bool Bad)>();
        if (view.Hp is double hp && hp < 0.35 * sim.CatMaxHp)
            warnings.Add((view.Live ? L("HP thấp: cân nhắc Cá hồi máu hoặc Khiên.", "Low HP: think of the Healing fish or the Shield.")
                : L("HP thấp: cân nhắc điểm nghỉ trước trận tiếp.", "Low HP: think of a rest before the next battle."), true));
        if (view.Live && sim.Reckless && sim.Relics.FirstOrDefault(r => r.Effect == RelicSpec.Reckless) is RelicSpec reckless)
            warnings.Add((L($"{reckless.Name(English)} đang kích hoạt: +{Percent(reckless.Amount)} sát thương.", $"{reckless.Name(English)} is on: +{Percent(reckless.Amount)} damage."), false));
        foreach (var relic in sim.Relics) warnings.Add((relic.Name(English) + ": " + RelicPrice(relic), true));
        if (warnings.Count > 0)
        {
            var list = new StackPanel();
            foreach (var (text, bad) in warnings)
            {
                var line = BagLine(text, PetUi.SmallSize, bad ? BagBad : PetUi.Game.Gold, bold: true);
                line.Margin = new Thickness(0);
                var row = new DockPanel { Margin = new Thickness(0, list.Children.Count == 0 ? 0 : 4, 0, 0), Children = { new BagIcon { Kind = bad ? "warning" : "spark", Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Top }, line } };
                DockPanel.SetDock(row.Children[0], Dock.Left);
                list.Children.Add(row);
            }
            bagBody!.Children.Add(BagCard(list, bottom: 8));
        }

        var grid = StatGrid();
        for (int r = 0; r < rows.Length; r++)
        {
            var (icon, name, value, max, higher, show) = rows[r];
            // The card pictures are drawn for 44 DIPs: drawn at that size and scaled down.
            FrameworkElement picture = icon == "reach" ? new BagIcon { Kind = "reach" } : new Viewbox { Child = new UpgradeIcon { Effect = icon, Width = 44, Height = 44 } };
            double? step = preview is var (_, b, a) ? value(a) - value(b) : null;
            AddStatRow(grid, r, picture, name, value(sim), value(view.Plain), max, higher, show, step);
        }
        var note = BagLine(L($"Thanh kem: {view.Plain.Weapon.Name(English)} không thẻ. Vàng: thẻ/di vật cộng thêm. Đỏ: bị trừ. Cổ vũ, combo và hiệu ứng chỉ có trong trận không tính ở đây.",
            $"Cream: the {view.Plain.Weapon.Name(English)} with no cards. Gold: added by cards or relics. Red: taken away. Cheer, combos and other in-battle effects are not counted here."),
            PetUi.CaptionSize, PetUi.Game.Soft);
        note.Margin = new Thickness(0, 8, 0, 0);
        bagBody!.Children.Add(BagCard(new StackPanel { Children = { grid, note } }, bottom: 8));

        // The skills with the relics' changes (and, in a battle, how long until each is ready).
        var skills = StatGrid();
        int at = 0;
        foreach (var info in BattleSkills.All)
        {
            var skill = info.Skill;
            var glyph = new Border { CornerRadius = new CornerRadius(6), Background = PetUi.Game.Shadow(0x80),
                Child = new HudGlyph(Enum.Parse<HudGlyphKind>(skill.ToString())) { Width = 18, Height = 18 } };
            string name = info.Name(English) + (view.Live && sim.Cooldown(skill) > 0.05 ? L($" · còn {Number(sim.Cooldown(skill), "0")} giây", $" · ready in {Number(sim.Cooldown(skill), "0")} s") : "");
            AddStatRow(skills, at++, glyph, name, sim.SkillCooldown(skill), view.Plain.SkillCooldown(skill), 30, false, v => L("hồi ", "every ") + Number(v, "0.#") + L(" giây", " s"), null);
            if (skill == BattleSkill.Heal)
                AddStatRow(skills, at++, new Border(), L("lượng hồi", "heals"), sim.HealAmount, view.Plain.HealAmount, Math.Max(1, view.Plain.HealAmount * 2.5), true, v => Number(v, "0") + " HP", null);
        }
        var heading = BagLine(L("Kỹ năng", "Skills"), PetUi.SmallSize, Brushes.White, bold: true);
        heading.Margin = new Thickness(0, 0, 0, 2);
        bagBody!.Children.Add(BagCard(new StackPanel { Children = { heading, skills } }, bottom: view.Live ? 8 : 0));

        // This battle: what the build and relics have done so far.
        if (view.Live)
        {
            bool Has(string effect) => sim.Settings.UpgradeList.Any(u => u.Effect == effect && sim.Stacks(u.Id) > 0);
            bool On(string effect) => sim.Synergies.Any(y => y.Effect == effect);
            var lines = new List<(string Name, int Count)> { (L("Quái đã hạ", "Enemies defeated"), sim.Defeated) };
            if (On("thorns") || sim.Reflects > 0) lines.Add((L("Đòn phản lại (gai)", "Hits reflected (thorns)"), sim.Reflects));
            if (On("echo") || sim.Echoes > 0) lines.Add((L("Đòn dội lại", "Echo hits"), sim.Echoes));
            if (sim.HasShell || sim.ShellBlocks > 0) lines.Add((L("Đòn Mai rùa đỡ", "Hits the Turtle shell stopped"), sim.ShellBlocks));
            if (Has("momentum") || sim.ChargedHits > 0) lines.Add((L("Đòn tích lực (chạy đà)", "Charged hits (momentum)"), sim.ChargedHits));
            if (Has("opener") || sim.OpenerHits > 0) lines.Add((L("Đòn mở màn", "Opener hits"), sim.OpenerHits));
            if (sim.Relics.Any(r => r.Effect == RelicSpec.Counter) || sim.CounterHits > 0) lines.Add((L("Đòn phản công (chuông)", "Counter hits (bell)"), sim.CounterHits));
            var title = BagLine(L("Trận này", "This battle") + " · " + Clock(sim.Time), PetUi.SmallSize, Brushes.White, bold: true);
            title.Margin = new Thickness(0, 0, 0, 2);
            var stack = new StackPanel { Children = { title } };
            foreach (var (what, count) in lines)
            {
                var line = new DockPanel { Margin = new Thickness(0, 3, 0, 0) };
                var value = BagLine(count.ToString(CultureInfo.InvariantCulture), PetUi.SmallSize, count > 0 ? PetUi.Game.Gold : PetUi.Game.Soft, bold: true);
                value.Margin = new Thickness(0);
                DockPanel.SetDock(value, Dock.Right);
                var label = BagLine(what, PetUi.SmallSize, Brushes.White); label.Margin = new Thickness(0);
                line.Children.Add(value); line.Children.Add(label);
                stack.Children.Add(line);
            }
            bagBody!.Children.Add(BagCard(stack));
        }
    }

    private static Grid StatGrid()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        return grid;
    }

    // One row: picture, name, the value (gold when cards/relics help, red when they hurt, with the
    // change from the weapon's own; with a preview, → the value after the card in blue), and a bar.
    private void AddStatRow(Grid grid, int r, FrameworkElement picture, string name, double now, double plain, double max, bool higher, Func<double, string> show, double? preview)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bool changed = show(now) != show(plain);
        bool better = changed && (now > plain) == higher;
        picture.Width = 24; picture.Height = 24; picture.Margin = new Thickness(0, 3, 6, 3);
        var label = BagLine(name, PetUi.SmallSize, Brushes.White); label.VerticalAlignment = VerticalAlignment.Center; label.Margin = new Thickness(0);
        var number = new TextBlock { FontSize = PetUi.SmallSize, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 10, 0), TextAlignment = TextAlignment.Right };
        number.Inlines.Add(new System.Windows.Documents.Run(show(now)) { Foreground = changed ? (better ? PetUi.Game.Gold : BagBad) : Brushes.White, FontWeight = FontWeights.Bold });
        if (changed)
        {
            double delta = now - plain;
            number.Inlines.Add(new System.Windows.Documents.Run($" {(delta > 0 ? "+" : "−")}{show(Math.Abs(delta))}") { Foreground = better ? PetUi.Game.Gold : BagBad, FontSize = PetUi.CaptionSize });
        }
        if (preview is double step && show(now + step) != show(now))
            number.Inlines.Add(new System.Windows.Documents.Run(" → " + show(now + step)) { Foreground = BagPreviewInk, FontWeight = FontWeights.Bold });
        var bar = StatBar(Math.Min(now, plain) / max, Math.Abs(now - plain) / max, better);
        Grid.SetRow(picture, r); Grid.SetRow(label, r); Grid.SetRow(number, r); Grid.SetRow(bar, r);
        Grid.SetColumn(label, 1); Grid.SetColumn(number, 2); Grid.SetColumn(bar, 3);
        grid.Children.Add(picture); grid.Children.Add(label); grid.Children.Add(number); grid.Children.Add(bar);
    }

    // The weapon's own part in cream, then the change: gold when it helps, red when it hurts.
    private static FrameworkElement StatBar(double plainShare, double changeShare, bool better)
    {
        const double width = 108;
        double a = Math.Clamp(plainShare, 0, 1) * width, b = Math.Clamp(changeShare, 0, 1 - Math.Clamp(plainShare, 0, 1)) * width;
        var fill = new StackPanel { Orientation = Orientation.Horizontal, Children =
        {
            new Border { Width = a, Background = BagCream, CornerRadius = new CornerRadius(3, 0, 0, 3) },
            new Border { Width = b, Background = better ? PetUi.Game.Gold : BagBad }
        } };
        return new Border { Width = width, Height = 7, Background = PetUi.Game.BarBack, CornerRadius = new CornerRadius(3.5), ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center, Child = fill };
    }

    // ── Cards ────────────────────────────────────────────────────────────────────────────────

    private static Brush GroupColor(string? group) => group switch
    {
        "defense" => PetUi.Hex("#7CC0EE"), "power" => PetUi.Hex("#F49272"), "speed" => PetUi.Hex("#9AD884"), _ => PetUi.Game.CardRim
    };

    private void FillBagCards(BagView view)
    {
        var sim = view.Sim;
        var settings = sim.Settings;
        if (BagPreview(view) is var (offered, _, _)) AddPreviewCard(view, offered, null);
        // Each group: its points as pips, what synergies it has switched on, and how far the next one is.
        var progress = new StackPanel();
        foreach (var group in BuildGroups.All)
        {
            var synergies = settings.SynergyList.Where(y => y.Group == group).OrderBy(y => y.At).ToList();
            int points = sim.GroupPoints(group);
            if (synergies.Count == 0 && points == 0) continue;
            int pips = Math.Max(points, synergies.Count == 0 ? 0 : synergies.Max(y => y.At));
            var dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
            for (int i = 0; i < pips; i++)
                dots.Children.Add(new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), Margin = new Thickness(0, 0, 3, 0),
                    Background = i < points ? GroupColor(group) : Brushes.Transparent, BorderBrush = GroupColor(group), BorderThickness = new Thickness(1.3) });
            var parts = synergies.Where(y => points >= y.At).Select(y => "★ " + y.Name(English)).ToList();
            if (synergies.FirstOrDefault(y => points < y.At) is SynergySpec next)
                parts.Add(L($"còn {next.At - points} thẻ → ★ {next.Name(English)}", $"{next.At - points} more → ★ {next.Name(English)}"));
            var name = BagLine($"{BuildGroups.Name(group, English)} {points}", PetUi.SmallSize, GroupColor(group), bold: true); name.Margin = new Thickness(0); name.Width = 96;
            var state = BagLine(string.Join(" · ", parts), PetUi.CaptionSize, parts.Any(p => p.StartsWith("★")) ? PetUi.Game.Gold : PetUi.Game.Soft); state.Margin = new Thickness(0); state.VerticalAlignment = VerticalAlignment.Center;
            progress.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, progress.Children.Count == 0 ? 0 : 5, 0, 0), Children = { name, dots, state } });
        }
        if (progress.Children.Count > 0) bagBody!.Children.Add(BagCard(progress, bottom: 8));

        var cards = settings.UpgradeList.Where(u => sim.Stacks(u.Id) > 0).OrderBy(u => Array.IndexOf(BuildGroups.All, u.Group ?? "")).ToList();
        var detail = BagLine(L("Rê chuột lên một thẻ để xem nó làm gì.", "Hover a card to see what it does."), PetUi.CaptionSize, PetUi.Game.Soft);
        detail.Margin = new Thickness(2, 6, 0, 0); detail.MinHeight = 30;
        if (cards.Count == 0)
        {
            var empty = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 6), Children =
            {
                new BagIcon { Kind = "cards", Width = 44, Height = 44, Opacity = 0.5 },
                BagLine(view.Mode == BagExpedition ? L("Chuyến này chưa có thẻ. Thẻ có từ trận thắng, điểm nghỉ, sự kiện và viên nang.", "No cards in this run yet: battles, rests, events and capsules give them.")
                    : L("Chưa có thẻ. Chọn thẻ trong trận; thắng màn thì giữ thẻ sang màn sau.", "No cards yet. Cards are chosen in battle; winning a stage keeps them for the next."),
                    PetUi.SmallSize, PetUi.Game.Soft)
            } };
            ((TextBlock)empty.Children[1]).TextAlignment = TextAlignment.Center;
            bagBody!.Children.Add(BagCard(empty));
            return;
        }
        var tiles = new WrapPanel();
        foreach (var u in cards)
        {
            int n = sim.Stacks(u.Id);
            var name = new TextBlock { Text = u.Name(English), Foreground = Brushes.White, FontSize = PetUi.CaptionSize, FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0), MaxWidth = 80 };
            var picture = new Grid { Width = 44, Height = 44, HorizontalAlignment = HorizontalAlignment.Center, Children = { new UpgradeIcon { Effect = u.Effect } } };
            var stack = new StackPanel { Children = { picture, name } };
            var face = new Grid { Children = { stack } };
            if (n > 1)
                face.Children.Add(new Border { Background = PetUi.Game.Gold, CornerRadius = new CornerRadius(8), Padding = new Thickness(5, 0, 5, 1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Child = new TextBlock { Text = "×" + n, FontSize = PetUi.CaptionSize, FontWeight = FontWeights.Bold, Foreground = PetUi.Game.GoldInk } });
            if (u.Rare)
                face.Children.Add(new TextBlock { Text = "◆", Foreground = PetUi.Game.RareGold, FontSize = PetUi.SmallSize, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
            var tile = new Border
            {
                Width = 92, MinHeight = 92, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(6, 6, 6, 6), CornerRadius = new CornerRadius(9),
                Background = u.Rare ? PetUi.Game.RareBack : PetUi.Game.CardBack, BorderBrush = u.Rare ? PetUi.Game.RareGold : GroupColor(u.Group), BorderThickness = new Thickness(u.Rare ? 2 : 1.5),
                Child = face
            };
            string about = u.Name(English) + (n > 1 ? $" ×{n}" : "") + " — " + u.Describe(English) + (u.Rare ? L(" (thẻ hiếm)", " (rare card)") : "");
            tile.MouseEnter += (_, _) => { detail.Text = about; detail.Foreground = Brushes.White; };
            tiles.Children.Add(tile);
        }
        bagBody!.Children.Add(BagCard(new StackPanel { Children = { tiles, detail } }));
    }

    // ── Relics and the run's log ─────────────────────────────────────────────────────────────

    private void FillBagRelics(BagView view)
    {
        var sim = view.Sim;
        if (view.Mode != BagExpedition || view.X is not Expedition x)
        {
            var info = new StackPanel { Children =
            {
                new BagIcon { Kind = "relic", Width = 44, Height = 44, Opacity = 0.5, HorizontalAlignment = HorizontalAlignment.Center },
                BagLine(L("Di vật chỉ có trong Viễn chinh: mỗi chuyến giữ tối đa vài món, mỗi món có lợi và có giá, hết chuyến thì mất.",
                    "Relics belong to the Expedition: a run holds a few, each with a gain and a price, and loses them when it ends."), PetUi.SmallSize, PetUi.Game.Soft)
            } };
            ((TextBlock)info.Children[1]).TextAlignment = TextAlignment.Center;
            bagBody!.Children.Add(BagCard(info, bottom: 8));
            // The chapter's progress instead.
            if (Adventure is Adventure adventure && adventure.Chapter() is ChapterSpec chapter)
            {
                var list = new StackPanel { Children = { BagLine(chapter.Name(English), PetUi.SmallSize, Brushes.White, bold: true) } };
                foreach (var stage in chapter.Stages)
                {
                    bool done = adventure.IsCompleted(stage.Id);
                    list.Children.Add(BagLine((done ? "✓ " : "○ ") + StageTitle(stage), PetUi.SmallSize, done ? PetUi.Game.Gold : PetUi.Game.Soft));
                }
                bagBody!.Children.Add(BagCard(list));
            }
            return;
        }

        // The slots: the held relics, then empty ones up to the cap.
        var slots = new WrapPanel();
        for (int i = 0; i < Math.Max(x.Spec.MaxRelics, sim.Relics.Count); i++)
        {
            var relic = i < sim.Relics.Count ? sim.Relics[i] : null;
            var name = new TextBlock { Text = relic?.Name(English) ?? L("Ô trống", "Empty"), Foreground = relic != null ? Brushes.White : PetUi.Game.Soft, FontSize = PetUi.CaptionSize,
                FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0), MaxWidth = 100 };
            UIElement picture = relic != null ? new BagIcon { Kind = relic.Effect, Width = 44, Height = 44 }
                : (UIElement)new TextBlock { Text = "?", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = PetUi.Game.Soft, HorizontalAlignment = HorizontalAlignment.Center, Height = 44 };
            slots.Children.Add(new Border
            {
                Width = 116, MinHeight = 86, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(6), CornerRadius = new CornerRadius(9),
                Background = relic != null ? PetUi.Game.CardBack : Brushes.Transparent, BorderBrush = relic != null ? PetUi.Game.Gold : PetUi.Game.CardRim,
                BorderThickness = new Thickness(1.5), Child = new StackPanel { Children = { picture, name } }
            });
        }
        var top = new StackPanel { Children = { BagLine(L($"Di vật {sim.Relics.Count}/{x.Spec.MaxRelics}", $"Relics {sim.Relics.Count}/{x.Spec.MaxRelics}"), PetUi.SmallSize, Brushes.White, bold: true), slots } };
        ((TextBlock)top.Children[0]).Margin = new Thickness(0, 0, 0, 6);
        foreach (var relic in sim.Relics)
        {
            var title = BagLine("◈ " + relic.Name(English) + (relic.Effect == RelicSpec.Reckless && view.Live && sim.Reckless ? L(" · đang kích hoạt", " · on now") : ""), PetUi.SmallSize, PetUi.Game.Gold, bold: true);
            title.Margin = new Thickness(0, 6, 0, 0);
            top.Children.Add(title);
            top.Children.Add(BagLine("▲ " + RelicGain(relic), PetUi.CaptionSize, PetUi.Hex("#C9F0B8")));
            top.Children.Add(BagLine("▼ " + RelicPrice(relic), PetUi.CaptionSize, BagBad));
        }
        if (sim.Relics.Count == 0)
            top.Children.Add(BagLine(L("Chưa có di vật. Thắng điểm tinh anh, mở rương hay đền, hoặc may mắn với viên nang vàng.", "No relics yet: win an elite node, open a chest or shrine, or get lucky with a gold capsule."),
                PetUi.CaptionSize, PetUi.Game.Soft));
        bagBody!.Children.Add(BagCard(top, bottom: 8));

        // The run's log, the latest first.
        var log = new StackPanel { Children = { BagLine(L("Nhật ký chuyến", "Run log"), PetUi.SmallSize, Brushes.White, bold: true) } };
        var entries = view.Run?.Log ?? new List<ExpeditionLogEntry>();
        if (entries.Count == 0) log.Children.Add(BagLine(L("Chưa có gì: chuyến vừa bắt đầu.", "Nothing yet: the run has just begun."), PetUi.CaptionSize, PetUi.Game.Soft));
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            var line = new TextBlock { FontSize = PetUi.CaptionSize, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
            line.Inlines.Add(new System.Windows.Documents.Run(L($"Bậc {e.Step + 1} · ", $"Step {e.Step + 1} · ")) { Foreground = PetUi.Game.Soft });
            line.Inlines.Add(new System.Windows.Documents.Run(LogText(x, e)));
            if (Math.Abs(e.Hp) >= 0.5)
                line.Inlines.Add(new System.Windows.Documents.Run($"  {(e.Hp > 0 ? "+" : "−")}{Math.Abs(Math.Round(e.Hp))} HP") { Foreground = e.Hp > 0 ? PetUi.Hex("#C9F0B8") : BagBad, FontWeight = FontWeights.SemiBold });
            log.Children.Add(line);
        }
        bagBody!.Children.Add(BagCard(log));
    }

    // One log line in words: "★ Elite: won", "Gold capsule: the Big heart card", "Rest: took Sharp edge".
    private string LogText(Expedition x, ExpeditionLogEntry e)
    {
        string Relic(string? id) => id == null ? "" : L("di vật ◈ ", "relic ◈ ") + RelicName(x, id);
        switch (e.Kind)
        {
            case ExpeditionLogEntry.Retreat: return "↩ " + L("Rời trận", "Left the battle");
            case ExpeditionChoice.RestKind:
                return "♨ " + KindName(ExpeditionSpec.Rest) + ": " + (e.Card != null ? L("lấy thẻ ", "took ") + CardName(e.Card) : L("hồi máu", "rested"));
            case ExpeditionChoice.EventKind:
            {
                string name = x.Spec.FindEvent(e.Event)?.Name(English) ?? KindName(ExpeditionSpec.Event);
                string what = e.Option switch
                {
                    DesktopPet.Expedition.SwapOption when e.Dropped != null && e.Card != null => L($"đổi {CardName(e.Dropped)} lấy {CardName(e.Card)}", $"traded {CardName(e.Dropped)} for {CardName(e.Card)}"),
                    DesktopPet.Expedition.Safe => L("chọn an toàn", "played safe"),
                    DesktopPet.Expedition.Risk or DesktopPet.Expedition.Take => Relic(e.Relic),
                    _ => L("đi tiếp", "moved on")
                };
                return "! " + name + ": " + what;
            }
            case ExpeditionChoice.RewardKind: return "★ " + L("Thưởng tinh anh: ", "Elite reward: ") + (e.Relic != null ? Relic(e.Relic) : L("bỏ qua", "left it"));
            case ExpeditionChoice.CapsuleKind:
                return "◉ " + (e.Option switch
                {
                    DesktopPet.Expedition.Free => L("Viên thường: ", "Plain capsule: ") + PrizeText(x, e.Prize),
                    DesktopPet.Expedition.Gold => L("Viên vàng: ", "Gold capsule: ") + PrizeText(x, e.Prize),
                    _ => L("Bỏ qua máy viên nang", "Skipped the capsule machine")
                });
            default: return KindGlyph(e.Kind) + " " + KindName(e.Kind) + ": " + L("thắng", "won");
        }
    }

    // What a relic gives and what it costs, from its numbers.
    private string RelicGain(RelicSpec r) => r.Effect switch
    {
        RelicSpec.Counter => L($"Sau khi Khiên hoặc Mai rùa đỡ đòn, nhát trúng kế tiếp +{Percent(r.Amount)} sát thương", $"After the Shield or Turtle shell stops a hit, the next landed swing deals +{Percent(r.Amount)}"),
        RelicSpec.Relief => L($"{BattleSkills.Get(BattleSkill.Heal).Name(false)} hồi thêm {Percent(r.Amount)}", $"The {BattleSkills.Get(BattleSkill.Heal).Name(true)} heals {Percent(r.Amount)} more"),
        RelicSpec.Reckless => L($"+{Percent(r.Amount)} sát thương khi HP dưới {Percent(r.Below)}", $"+{Percent(r.Amount)} damage while HP is under {Percent(r.Below)}"),
        _ => L($"+{Percent(r.Amount)} tốc độ chạy, −{Percent(r.Extra)} thời gian hồi đòn", $"+{Percent(r.Amount)} running speed, −{Percent(r.Extra)} time between swings")
    };

    private string RelicPrice(RelicSpec r) => r.Effect switch
    {
        RelicSpec.Counter => L($"{BattleSkills.Get(BattleSkill.Shield).Name(false)} hồi chiêu lâu thêm {Number(r.Penalty, "0.#")} giây", $"The {BattleSkills.Get(BattleSkill.Shield).Name(true)} takes {Number(r.Penalty, "0.#")} s longer to recharge"),
        RelicSpec.Relief => L($"{BattleSkills.Get(BattleSkill.Heal).Name(false)} hồi chiêu lâu thêm {Percent(r.Penalty)}", $"The {BattleSkills.Get(BattleSkill.Heal).Name(true)} takes {Percent(r.Penalty)} longer to recharge"),
        RelicSpec.Reckless => L($"luôn nhận thêm {Percent(r.Penalty)} sát thương", $"always {Percent(r.Penalty)} more damage taken"),
        _ => L($"−{Number(r.Penalty, "0")} HP tối đa", $"−{Number(r.Penalty, "0")} max HP")
    };

    // ── Small pieces ─────────────────────────────────────────────────────────────────────────

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

    private static TextBlock BagLine(string text, double size, Brush color, bool bold = false) => new()
    {
        Text = text, FontSize = size, Foreground = color, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0)
    };

    private static Border BagCard(UIElement content, double bottom = 0) => new()
    {
        Background = PetUi.Game.CardBack, CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 8, 10, 9), Margin = new Thickness(0, 0, 0, bottom), Child = content
    };
}

// The Bag's own little pictures (vector, no image files): its tabs (stats, cards, relic), reach,
// each relic effect (counter: a bell; relief: a pouch with a fish; reckless: a charm with a flame;
// light: a feather), a warning, a spark, a lock and a cup for records.
public sealed class BagIcon : FrameworkElement
{
    public string Kind { get; init; } = "relic";
    private static readonly Brush Back = PetUi.Frozen(Color.FromArgb(0x40, 255, 255, 255));
    private static readonly Brush Gold = PetUi.Game.Gold, Pink = PetUi.Hex("#F49A7A"), Red = PetUi.Hex("#FF8A70"), Blue = PetUi.Hex("#7CC0EE"), Green = PetUi.Hex("#9AD884"), Cream = PetUi.Hex("#FFF3E2");
    private static readonly Pen Ink = PetUi.FrozenPen(PetUi.Frozen(Color.FromRgb(0x30, 0x2B, 0x48)), 1.6);
    private static readonly Pen White = PetUi.FrozenPen(Brushes.White, 2);
    private static Geometry Shape(FormattableString path) => PetUi.FrozenGeometry(FormattableString.Invariant(path));

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        // Every picture is drawn on a 24×24 design and scaled.
        double k = Math.Min(w, h) / 24;
        dc.PushTransform(new TranslateTransform((w - 24 * k) / 2, (h - 24 * k) / 2));
        dc.PushTransform(new ScaleTransform(k, k));
        switch (Kind)
        {
            case "stats": // Three bars.
                dc.DrawRoundedRectangle(Cream, Ink, new Rect(3, 13, 5, 8), 1, 1);
                dc.DrawRoundedRectangle(Gold, Ink, new Rect(9.5, 8, 5, 13), 1, 1);
                dc.DrawRoundedRectangle(Pink, Ink, new Rect(16, 3, 5, 18), 1, 1);
                break;
            case "cards": // Two cards fanned.
                dc.PushTransform(new RotateTransform(-12, 12, 12));
                dc.DrawRoundedRectangle(Blue, Ink, new Rect(4, 4, 11, 16), 2, 2);
                dc.Pop();
                dc.PushTransform(new RotateTransform(10, 12, 12));
                dc.DrawRoundedRectangle(Gold, Ink, new Rect(9, 4, 11, 16), 2, 2);
                dc.DrawEllipse(Cream, null, new Point(14.5, 12), 2.4, 2.4);
                dc.Pop();
                break;
            case "reach": // An arrow to a target.
                dc.DrawEllipse(Back, null, new Point(12, 12), 12, 12);
                dc.DrawEllipse(null, Ink, new Point(17, 12), 4.5, 4.5);
                dc.DrawEllipse(Red, null, new Point(17, 12), 1.8, 1.8);
                dc.DrawLine(White, new Point(3, 12), new Point(12, 12));
                dc.DrawGeometry(Cream, Ink, Shape($"M 10,8.5 L 13.5,12 L 10,15.5 Z"));
                break;
            case RelicSpec.Counter: // A bell.
                dc.DrawGeometry(Gold, Ink, Shape($"M 12,3 C 6,3 6,10 5.5,15 L 4,18 L 20,18 L 18.5,15 C 18,10 18,3 12,3 Z"));
                dc.DrawEllipse(Gold, Ink, new Point(12, 19.5), 2.2, 2.2);
                dc.DrawLine(PetUi.FrozenPen(Cream, 1.4), new Point(9, 7), new Point(8.3, 12));
                break;
            case RelicSpec.Relief: // A pouch with a fish peeking out.
                dc.DrawGeometry(Pink, Ink, Shape($"M 13,6 L 20,2.5 L 20,9.5 Z"));
                dc.DrawGeometry(PetUi.Hex("#C88B5A"), Ink, Shape($"M 5,9 C 3,14 4,21 12,21 C 20,21 21,14 19,9 Z"));
                dc.DrawLine(Ink, new Point(5, 9), new Point(19, 9));
                dc.DrawEllipse(Green, Ink, new Point(12, 15), 2.6, 2.6);
                break;
            case RelicSpec.Reckless: // A red charm with a flame.
                dc.DrawLine(Ink, new Point(12, 1.5), new Point(12, 5));
                dc.DrawGeometry(Red, Ink, Shape($"M 12,5 L 19,12 L 12,22 L 5,12 Z"));
                dc.DrawGeometry(Gold, null, Shape($"M 12,9 C 15,12 14.5,17 12,18 C 9.5,17 9,13 12,9 Z"));
                break;
            case RelicSpec.Light: // A feather.
                dc.DrawGeometry(Cream, Ink, Shape($"M 19,3 C 9,4 4,12 5,19 C 12,19 19,13 19,3 Z"));
                dc.DrawLine(Ink, new Point(3, 21), new Point(16, 7));
                break;
            case "warning": // A triangle with "!".
                dc.DrawGeometry(Red, Ink, Shape($"M 12,2.5 L 22,20.5 L 2,20.5 Z"));
                dc.DrawLine(PetUi.FrozenPen(Brushes.White, 2.4), new Point(12, 9), new Point(12, 14));
                dc.DrawEllipse(Brushes.White, null, new Point(12, 17.3), 1.3, 1.3);
                break;
            case "spark": // A four-point star.
                dc.DrawGeometry(Gold, Ink, Shape($"M 12,2 Q 13,11 22,12 Q 13,13 12,22 Q 11,13 2,12 Q 11,11 12,2 Z"));
                break;
            case "lock":
                dc.DrawGeometry(null, PetUi.FrozenPen(Cream, 2.2), Shape($"M 7.5,11 L 7.5,8 A 4.5,4.5 0 0 1 16.5,8 L 16.5,11"));
                dc.DrawRoundedRectangle(PetUi.Hex("#8A7A6C"), Ink, new Rect(5, 11, 14, 10), 2, 2);
                dc.DrawEllipse(Cream, null, new Point(12, 15.5), 1.6, 1.6);
                break;
            case "cup": // A trophy.
                dc.DrawGeometry(Gold, Ink, Shape($"M 6,3 L 18,3 L 17,10 C 16,14 8,14 7,10 Z"));
                dc.DrawGeometry(null, Ink, Shape($"M 6,5 C 2,5 2,10 7,10 M 18,5 C 22,5 22,10 17,10"));
                dc.DrawRectangle(Gold, Ink, new Rect(10.5, 13.5, 3, 4));
                dc.DrawRoundedRectangle(PetUi.Hex("#4A3C5C"), Ink, new Rect(7, 17.5, 10, 3.5), 1, 1);
                break;
            default: // "relic": a gem.
                dc.DrawGeometry(PetUi.Hex("#C49BFF"), Ink, Shape($"M 7,4 L 17,4 L 21,9 L 12,21 L 3,9 Z"));
                dc.DrawGeometry(null, Ink, Shape($"M 3,9 L 21,9 M 9,4 L 12,9 L 15,4 M 12,9 L 12,21"));
                break;
        }
        dc.Pop(); dc.Pop();
    }
}
