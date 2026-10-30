using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DesktopPet;

// 1.5 M27 (docs/UPDATE-1.5.md L1): the battle hub's Monster Book, in place of the mode cards like the
// Rewards page. A tile per enemy kind with a page (slimes, then bosses) and per formation: met ones
// drawn as in battle (the battle strip's own drawing), with how many the cat defeated; the others a
// dark shape with "???". A click on a met tile shows its page above the tiles: the plain and elite
// look, the elite's trait, how to beat it and the skills that suit it (a formation: its members and
// its tip). Pages new since the book was last open carry "New" and the hub's button counts them.
// What is met is counted when a battle ends (MonsterBook, monster-book.json); custom battle data adds
// nothing. View only otherwise; Python has no part in it.
public sealed partial class PetWindow
{
    private MonsterBook? monsterBook;
    private BattleSim? bookCounted;
    private bool hubBookShown;
    private string? hubBookLanguage, bookSelected;
    private StackPanel? hubBook;
    private Border? hubBookButton, hubRewardsButton;
    private TextBlock? hubBookText;
    private Border? bookDetail;
    private HashSet<string> bookFresh = new();
    private static readonly Dictionary<(string Kind, bool Elite), ImageSource> BookPortraits = new();
    private static readonly Brush BookShape = PetUi.Frozen(Color.FromArgb(0xD0, 0x10, 0x10, 0x1C));

    public MonsterBook MonsterBook
    {
        get
        {
            if (monsterBook != null) return monsterBook;
            monsterBook = persistPreferences ? MonsterBook.Load(MonsterBook.DefaultPath) : new();
            // A book started after chapters were already won: those stages' enemies count as met.
            bool seeded = !BattleDataCustom && monsterBook.Seed(Adventure?.Campaign, BattleTuning, AdventureProgress);
            // 1.5 M29a: and their apps (for the Custom Fight's locks).
            seeded |= !BattleDataCustom && monsterBook.SeedApps(Adventure?.Campaign, BattleTuning, AdventureProgress);
            if (seeded) SaveMonsterBook();
            return monsterBook;
        }
    }
    public bool BattleBookShown => battleHubOpen && hubBookShown;
    public string BattleBookText => hubBook is null || !hubBookShown ? "" : string.Join("\n", BagTexts(hubBook));
    public int BattleBookNew => NewBookPages().Count;
    public string? BattleBookSelected => bookSelected;

    private void SaveMonsterBook() { if (persistPreferences && monsterBook != null) monsterBook.Save(MonsterBook.DefaultPath); }

    // Once per battle (its end, or the battle left early): what it met goes in the book. Returns the pages it opened.
    private List<string> CountMonsterBook(BattleSim b)
    {
        if (bookCounted == b) return new();
        bookCounted = b;
        if (battleCustomData) return new();
        var opened = MonsterBook.Add(BattleTuning, b.MetKinds, b.DefeatedKinds, b.MetFormations, out bool changed);
        MonsterBook.AddApps(b.MetApps, out bool apps); // 1.5 M29a
        if (changed || apps) SaveMonsterBook();
        if (opened.Count > 0) System.Diagnostics.Trace.WriteLine($"Monster book: new pages {string.Join(", ", opened)}.");
        return opened;
    }

    // The pages in the data: slimes, then bosses, then formations.
    private List<EnemyType> BookKinds(bool bosses) => BattleTuning.EnemyTypes.Where(e => e.TipVi != null && e.Boss == bosses).ToList();
    private List<string> BookPages() => BookKinds(false).Concat(BookKinds(true)).Select(e => e.Id)
        .Concat(BattleTuning.FormationList.Select(f => MonsterBook.FormationPage(f.Id))).ToList();
    private bool BookOpen(string page) => page.StartsWith("f:", StringComparison.Ordinal) ? MonsterBook.KnowsFormation(page[2..]) : MonsterBook.Knows(page);
    private List<string> NewBookPages() => BookPages().Where(p => BookOpen(p) && !MonsterBook.Seen.Contains(p)).ToList();

    // A page's name (the result's "new pages" line).
    private string BookPageName(string page) => page.StartsWith("f:", StringComparison.Ordinal)
        ? BattleTuning.Formation(page[2..])?.Name(preferences.Language) ?? page
        : BattleTuning.EnemyTypes.FirstOrDefault(e => e.Id == page)?.Name(preferences.Language) ?? page;

    private string BookButtonText()
    {
        int fresh = BattleBookNew;
        return L("Sổ tay quái", "Monster Book") + (fresh > 0 ? LF($" · {fresh} mới", $" · {fresh} new") : "");
    }

    // The hub's Monster Book button (and its ‹ Hub): null on success, otherwise why not.
    public string? ShowMonsterBook(bool show)
    {
        if (!battleHubOpen || hubBook == null || hubCardsRow == null) return L("Sảnh chiến đấu chưa mở.", "The battle hub is not open.");
        if (show == hubBookShown) return null;
        if (show && hubRewardsShown) ShowBattleRewards(false);
        hubBookShown = show;
        if (show)
        {
            var fresh = NewBookPages();
            bookFresh = fresh.ToHashSet();
            bookSelected = fresh.FirstOrDefault() ?? BookPages().FirstOrDefault(BookOpen);
            FillMonsterBook();
            if (fresh.Count > 0) { MonsterBook.Seen.AddRange(fresh); SaveMonsterBook(); }
        }
        hubBook.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        hubCardsRow.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        UpdateBattleHub();
        PlaceGamePanel(battleHubWindow);
        System.Diagnostics.Trace.WriteLine(show ? "Monster book opened." : "Monster book closed.");
        return null;
    }

    // A click on a tile (met pages only).
    public bool SelectBookPage(string page)
    {
        if (!hubBookShown || !BookOpen(page)) return false;
        bookSelected = page;
        FillMonsterBook();
        return true;
    }

    private void RefreshMonsterBook()
    {
        if (hubBookShown && hubBookLanguage != preferences.Language) FillMonsterBook();
    }

    private void FillMonsterBook()
    {
        if (hubBook == null) return;
        hubBookLanguage = preferences.Language;
        hubBook.Children.Clear();
        bookDetail = BagCard(BookDetail());
        hubBook.Children.Add(bookDetail);

        var sections = new StackPanel();
        void Section(string title, PetMotif motif, List<string> pages)
        {
            if (pages.Count == 0) return;
            int open = pages.Count(BookOpen);
            var heading = PanelLine(PetUi.TitleSize, true, RewardsWidth - 60);
            heading.Text = $"{title} · {open}/{pages.Count}"; heading.VerticalAlignment = VerticalAlignment.Center;
            var titled = PetIllustration.Beside(heading, motif, 22);
            titled.Margin = new Thickness(0, sections.Children.Count == 0 ? 0 : 8, 0, 4);
            var tiles = new WrapPanel();
            foreach (var page in pages) tiles.Children.Add(BookTile(page));
            sections.Children.Add(titled);
            sections.Children.Add(tiles);
        }
        Section(L("Slime", "Slimes"), PetMotif.Mushroom, BookKinds(false).Select(e => e.Id).ToList());
        Section(L("Trùm", "Bosses"), PetMotif.Crown, BookKinds(true).Select(e => e.Id).ToList());
        Section(L("Đội hình phối hợp", "Formations"), PetMotif.Flag, BattleTuning.FormationList.Select(f => MonsterBook.FormationPage(f.Id)).ToList());
        var scroll = new ScrollViewer { Content = sections, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 10, 0, 0), Focusable = false };
        hubBook.Children.Add(scroll);
    }

    // The chosen page, over the tiles.
    private FrameworkElement BookDetail()
    {
        if (bookSelected is not string page || !BookOpen(page))
            return PetIllustration.Beside(BagLine(L("Chưa gặp quái nào. Vào trận: mỗi loại quái gặp lần đầu mở một trang.", "No enemy met yet. Battle on: each kind met for the first time opens a page."),
                PetUi.SmallSize, PetUi.Game.Soft), PetMotif.Book, 30);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var art = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Top };
        void Add(string line, double size, Brush brush, bool bold = false) { var t = BagLine(line, size, brush, bold); t.MaxWidth = 300; text.Children.Add(t); }
        if (page.StartsWith("f:", StringComparison.Ordinal) && BattleTuning.Formation(page[2..]) is FormationSpec formation)
        {
            foreach (var kind in formation.Members.Distinct())
                if (BattleTuning.EnemyTypes.FirstOrDefault(e => e.Id == kind) is EnemyType member) art.Children.Add(BookPicture(member, false, MonsterBook.Knows(kind), 64, 56));
            Add(formation.Name(preferences.Language), PetUi.TitleSize, Brushes.White, true);
            Add(L("Đội hình phối hợp", "Formation"), PetUi.CaptionSize, PetUi.Game.Soft);
            if (formation.TipVi != null) Add(formation.Tip(preferences.Language), PetUi.SmallSize, Brushes.White);
            Add(LF($"Thành viên: {string.Join(", ", formation.Members.Distinct().Select(BookPageName))}", $"Members: {string.Join(", ", formation.Members.Distinct().Select(BookPageName))}"), PetUi.CaptionSize, PetUi.Game.Soft);
        }
        else if (BattleTuning.EnemyTypes.FirstOrDefault(e => e.Id == page) is EnemyType type)
        {
            art.Children.Add(BookPicture(type, false, true, 88, 80));
            if (type.Elite != null) art.Children.Add(BookPicture(type, true, true, 88, 80));
            Add(type.Name(preferences.Language), PetUi.TitleSize, Brushes.White, true);
            if (type.Boss) Add(L("Trùm", "Boss"), PetUi.CaptionSize, PetUi.Game.Gold, true);
            Add(type.Tip(preferences.Language), PetUi.SmallSize, Brushes.White);
            if (type.Elite is EliteSpec elite && elite.DescVi.Length > 0) Add(L("Tinh anh: ", "Elite: ") + elite.Describe(preferences.Language), PetUi.CaptionSize, PetUi.Game.Soft);
            var skills = (type.Counters ?? Array.Empty<string>()).Select(BattleSkills.Parse).Where(k => k != null).Select(k => SkillName(k!.Value)).ToList();
            if (skills.Count > 0) Add(L("Kỹ năng hợp: ", "Good skills: ") + string.Join(", ", skills), PetUi.CaptionSize, PetUi.AccentBright, true);
            Add(LF($"Đã hạ: {MonsterBook.DefeatedOf(type.Id)}", $"Defeated: {MonsterBook.DefeatedOf(type.Id)}"), PetUi.CaptionSize, PetUi.Game.Gold, true);
        }
        var row = new DockPanel { Children = { art, text } };
        DockPanel.SetDock(art, Dock.Left);
        return row;
    }

    // One tile: the picture (a dark shape until met), the name (or ???), the count; "New" when fresh.
    private Border BookTile(string page)
    {
        bool open = BookOpen(page), formation = page.StartsWith("f:", StringComparison.Ordinal);
        FrameworkElement picture;
        string name;
        if (formation)
        {
            var spec = BattleTuning.Formation(page[2..]);
            var members = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var kind in spec?.Members.Distinct().Take(3) ?? Enumerable.Empty<string>())
                if (BattleTuning.EnemyTypes.FirstOrDefault(e => e.Id == kind) is EnemyType member) members.Children.Add(BookPicture(member, false, open, 30, 40));
            picture = members;
            name = open ? spec?.Name(preferences.Language) ?? page : "???";
        }
        else
        {
            var type = BattleTuning.EnemyTypes.First(e => e.Id == page);
            picture = BookPicture(type, false, open, 60, 46);
            name = open ? type.Name(preferences.Language) : "???";
        }
        picture.HorizontalAlignment = HorizontalAlignment.Center;
        var label = new TextBlock { Text = name, Foreground = open ? Brushes.White : PetUi.Game.Soft, FontSize = PetUi.CaptionSize, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 76, Margin = new Thickness(0, 2, 0, 0) };
        var stack = new StackPanel { Children = { new Grid { Height = 46, Children = { picture } }, label } };
        if (open && !formation)
            stack.Children.Add(new TextBlock { Text = "× " + MonsterBook.DefeatedOf(page), Foreground = PetUi.Game.Gold, FontSize = PetUi.CaptionSize, HorizontalAlignment = HorizontalAlignment.Center });
        var face = new Grid { Children = { stack } };
        if (bookFresh.Contains(page))
            face.Children.Add(new Border { Background = PetUi.Accent, CornerRadius = new CornerRadius(7), Padding = new Thickness(5, 0, 5, 1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = L("Mới", "New"), Foreground = Brushes.White, FontSize = PetUi.CaptionSize, FontWeight = FontWeights.Bold } });
        bool chosen = page == bookSelected;
        var tile = new Border
        {
            Width = 82, MinHeight = 86, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(3), CornerRadius = new CornerRadius(9),
            Background = open ? PetUi.Game.CardBack : Brushes.Transparent, BorderBrush = chosen ? PetUi.Game.Gold : PetUi.Game.CardRim,
            BorderThickness = new Thickness(chosen ? 2 : 1.2), Child = face, Cursor = open ? System.Windows.Input.Cursors.Hand : null
        };
        tile.MouseLeftButtonDown += (_, e) => { SelectBookPage(page); e.Handled = true; };
        return tile;
    }

    // An enemy as the battle draws it (cached); not met: its dark shape only.
    private FrameworkElement BookPicture(EnemyType type, bool elite, bool met, double width, double maxHeight)
    {
        var source = BookPortrait(type, elite);
        double height = width * source.Height / source.Width;
        if (height > maxHeight) { width *= maxHeight / height; height = maxHeight; }
        if (met) return new Image { Source = source, Width = width, Height = height, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Bottom };
        var shape = new Rectangle { Width = width, Height = height, Fill = BookShape, OpacityMask = new ImageBrush(source) { Stretch = Stretch.Uniform }, VerticalAlignment = VerticalAlignment.Bottom };
        return new Grid { Children = { shape, new TextBlock { Text = "?", Foreground = PetUi.Game.Soft, FontSize = PetUi.TitleSize, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } };
    }

    // A one-slime battle strip drawn once into a picture (the lower part, where it stands).
    private ImageSource BookPortrait(EnemyType type, bool elite)
    {
        if (BookPortraits.TryGetValue((type.Id, elite), out var cached)) return cached;
        const int W = 150, Top = 40;
        int h = (int)BattleFieldView.FieldHeight;
        var sim = new BattleSim(BattleTuning, 0, W, new Random(1));
        var slime = sim.AddSlime(W / 2.0, type);
        slime.Set(SlimeState.Moving);
        slime.StateTime = 0.4;
        slime.Facing = -1;
        slime.Altitude = 0; // A flyer on the ground, where the strip draws it.
        if (elite) slime.Elite = type.Elite;
        var view = new BattleFieldView(sim) { OriginX = 0, Language = preferences.Language };
        view.Measure(new Size(W, h));
        view.Arrange(new Rect(0, 0, W, h));
        var bitmap = new RenderTargetBitmap(W * 2, h * 2, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(view);
        // Cut close around it by its size (room over its head for an elite's word), plain and elite alike.
        double scale = type.Scale;
        int cw = (int)Math.Clamp(BattleTuning.SlimeWidth * scale * 1.6 + 24, 56, W), ch = (int)Math.Clamp(BattleTuning.SlimeHeight * scale * 1.8 + 34, 48, h - Top);
        var cropped = new CroppedBitmap(bitmap, new Int32Rect((W - cw), (h - ch) * 2, cw * 2, ch * 2));
        cropped.Freeze();
        BookPortraits[(type.Id, elite)] = cropped;
        return cropped;
    }
}
