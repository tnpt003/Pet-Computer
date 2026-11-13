using System;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using DesktopPet.AI;

namespace DesktopPet;

// The controls of an open Customize or Settings window (kept so checks can drive the form).
// Customize fills the cat's controls (profile, look, habits, keepsake), Settings the app's (speech, sounds, general).
public sealed class PetSettingsForm
{
    public TabControl Tabs = null!;
    public TextBox Name = null!;
    public ComboBox Language = null!, Personality = null!, Coat = null!, Accessory = null!, Chattiness = null!, BreakInterval = null!, PreviewCue = null!, BirthDay = null!, BirthMonth = null!;
    public string[] AccessoryIds = Array.Empty<string>();
    public string[] CoatIds = Array.Empty<string>();
    public string[] LanguageIds = Array.Empty<string>();
    // The keepsakes that can be on display (owned cups best first, then the cushion); empty: none yet.
    public ComboBox Trophy = null!;
    public string[] TrophyIds = Array.Empty<string>();
    public Border KeepsakeSection = null!;
    public CheckBox HolidayAccessories = null!, ClimbWindows = null!, Weather = null!, Guests = null!, Treasure = null!, Farm = null!, FarmHelper = null!, FarmResow = null!, FarmAutoCook = null!, FarmAutoFish = null!, ShowTrophies = null!, Talks = null!, Autostart = null!, BreakReminders = null!, Sounds = null!, InteractionSounds = null!, EmotionSounds = null!, SleepSounds = null!, AutoUpdate = null!;
    public Slider Volume = null!;
    public Button Save = null!, Cancel = null!, Tuning = null!, Forget = null!, Preview = null!, CheckUpdates = null!, Uninstall = null!, DebugView = null!, ReplayGuide = null!, ReplayWelcome = null!;
    public PetRenderer PetPreview = null!;
    public PetModel PreviewPet = null!;
    public System.Windows.Threading.DispatcherTimer PreviewTimer = null!;
}

// One row of the coat list: a colour dot (with the marking/point colour) and the name.
public sealed record CoatChoice(string Label, CoatPalette Palette)
{
    public Brush Fur => Palette.Fur;
    public Brush Ink => Palette.Ink;
    public Brush Accent => Palette.Swatch ?? Brushes.Transparent;
    public override string ToString() => Label;
}

// Two windows of titled cards, each with Save and Cancel; nothing changes until Save, and each saves only
// its own fields. Customize ({name} › Customize…, the tray, a keepsake's menu): who the cat is, how it looks,
// its habits and keepsake. Settings: the app, in three sidebar pages (Speech, Sounds, General).
public sealed partial class PetWindow
{
    private PetSettingsForm? settingsForm, customizeForm;

    private static readonly (string Cue, string Vi, string En)[] PreviewCues =
    {
        ("meow", "Meo", "Meow"), ("purr", "Gừ gừ", "Purr"), ("excited", "Phấn khích", "Excited"),
        ("surprised", "Giật mình", "Surprised"), ("annoyed", "Khó chịu", "Annoyed"), ("sleep", "Ngủ", "Sleep"),
        ("ball_tap", "Chạm bóng", "Ball tap"), ("satisfied", "No nê", "Satisfied"), ("wake", "Thức dậy", "Wake"),
    };

    // "Tháng 3", "March", "3月": the birthday month in the UI language.
    private string MonthName(int month) => preferences.Language == PetLanguage.Vietnamese ? "Tháng " + month
        : System.Globalization.CultureInfo.GetCultureInfo(preferences.Language).DateTimeFormat.GetMonthName(month);

    // keepsakes: scroll to the keepsake card (opened from a keepsake's menu).
    public void ShowCustomize(bool keepsakes = false)
    {
        if (customizeWindow != null) { customizeWindow.Activate(); if (keepsakes) customizeForm?.KeepsakeSection.BringIntoView(); return; }
        var form = new PetSettingsForm();

        // Who the cat is and how it looks. Stored values stay English ("Balanced", "Orange"...).
        string[] personalities = PetPersonality.All;
        form.Name = new TextBox { Text = preferences.Name, MaxLength = 24 };
        form.Personality = new ComboBox { ItemsSource = new[] { L("Cân bằng", "Balanced"), L("Ham chơi", "Playful"), L("Tò mò", "Curious"), L("Điềm tĩnh", "Calm") },
            SelectedIndex = Math.Max(0, Array.IndexOf(personalities, preferences.Personality)) };
        // Coats and their names come from Assets/coats.json, in PetPreferences.Coats order; achievement
        // coats only once earned (or already worn, 1.5 M28). The list maps back by id.
        form.CoatIds = PetPreferences.Coats.Where(CoatAvailable).ToArray();
        form.Coat = new ComboBox { ItemTemplate = CoatTemplate,
            ItemsSource = form.CoatIds.Select(id => PetCoats.All.First(coat => coat.Id == id)).Select(coat => new CoatChoice(L(coat.NameVi, coat.NameEn), CoatPalette.For(coat.Id))).ToArray(),
            SelectedIndex = Math.Max(0, Array.IndexOf(form.CoatIds, preferences.Coat)) };
        // Accessory names come from Assets/accessories.json, in PetPreferences.Accessories order;
        // Adventure and achievement rewards only once received (or already worn). The list maps back by id.
        form.AccessoryIds = PetPreferences.Accessories.Where(id => id == PetAccessories.None || AccessoryAvailable(id)).ToArray();
        form.Accessory = new ComboBox { ItemsSource = form.AccessoryIds.Select(id => PetAccessories.Find(id) is { } item ? L(item.NameVi, item.NameEn) : L("Không", "None")).ToArray(),
            SelectedIndex = Math.Max(0, Array.IndexOf(form.AccessoryIds, preferences.Accessory)) };
        // The user's birthday (day and month only); index 0 means not set.
        int savedMonth = preferences.Birthday.Length == 5 ? int.Parse(preferences.Birthday[..2]) : 0;
        int savedDay = preferences.Birthday.Length == 5 ? int.Parse(preferences.Birthday[3..]) : 0;
        form.BirthDay = new ComboBox { Width = 70, ItemsSource = new[] { L("Ngày", "Day") }.Concat(Enumerable.Range(1, 31).Select(d => d.ToString())).ToArray(),
            SelectedIndex = savedDay };
        form.BirthMonth = new ComboBox { Width = 120, Margin = new Thickness(8, 0, 0, 0), SelectedIndex = savedMonth,
            ItemsSource = new[] { L("Tháng", "Month") }.Concat(Enumerable.Range(1, 12).Select(m =>
                MonthName(m))).ToArray() };
        var birthday = new StackPanel { Orientation = Orientation.Horizontal,
            ToolTip = L("Mèo sẽ chúc mừng sinh nhật bạn vào ngày này (không cần năm sinh).", "The pet wishes you a happy birthday on this day (no year needed).") };
        birthday.Children.Add(form.BirthDay); birthday.Children.Add(form.BirthMonth);
        form.Tuning = PetUi.LinkButton(L("Tinh chỉnh hành vi nâng cao…", "Advanced behavior tuning…"));
        form.Tuning.Click += (_, _) => ShowBehaviorSettings();
        // Live preview: its own model and renderer; the real pet changes only on Save.
        var previewPet = new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1 };
        form.PreviewPet = previewPet;
        form.PetPreview = new PetRenderer(previewPet) { Coat = preferences.Coat, Accessory = preferences.Accessory, Width = 192, Height = 192, Margin = new Thickness(-30) };
        form.Coat.SelectionChanged += (_, _) => form.PetPreview.Coat = form.CoatIds[Math.Max(0, form.Coat.SelectedIndex)];
        form.Accessory.SelectionChanged += (_, _) => form.PetPreview.Accessory = form.AccessoryIds[Math.Max(0, form.Accessory.SelectedIndex)];
        var stage = new Grid { Width = 132, Height = 132, ClipToBounds = true }; // Crops the window's transparent margin.
        stage.Children.Add(form.PetPreview);
        var previewCard = new Border { Child = stage, Background = PetUi.Stage, CornerRadius = new CornerRadius(PetUi.CardRadius), Padding = new Thickness(4),
            BorderBrush = PetUi.Line, BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Top };
        var looks = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        looks.Children.Add(new TextBlock { Text = L("Bộ lông", "Coat"), Margin = new Thickness(0, 4, 0, 4) }); looks.Children.Add(form.Coat);
        looks.Children.Add(new TextBlock { Text = L("Phụ kiện", "Accessory"), Margin = new Thickness(0, 14, 0, 4) }); looks.Children.Add(form.Accessory);
        form.HolidayAccessories = new CheckBox { Content = L("Tự đội đồ ngày lễ", "Dress up for holidays"), IsChecked = preferences.HolidayAccessories,
            Margin = new Thickness(0, 10, 0, 0),
            ToolTip = L("Noel, Halloween, Tết, Trung thu, sinh nhật bạn: mèo tự đội đồ hợp dịp (cần AI bật).",
                        "Christmas, Halloween, Lunar New Year, Mid-Autumn, your birthday: the pet dresses up for the day (needs the AI).") };
        looks.Children.Add(form.HolidayAccessories);
        var appearance = new DockPanel();
        DockPanel.SetDock(previewCard, Dock.Left);
        appearance.Children.Add(previewCard); appearance.Children.Add(looks);
        form.ClimbWindows = new CheckBox { Content = L("Mèo leo lên cửa sổ", "Climb on windows"), IsChecked = preferences.ClimbWindows,
            ToolTip = L("Mèo nhảy hoặc trèo lên mép trên các cửa sổ app và đi dạo trên đó.", "The pet jumps or climbs onto the top edge of app windows and walks along it.") };
        form.Weather = new CheckBox { Content = L("Thời tiết quanh mèo", "Weather around the pet"), IsChecked = preferences.Weather,
            ToolTip = L("Mưa, tuyết, lá bay... quanh mèo, chọn theo màu hình nền Windows. Chỉ đọc màu từ ảnh thu nhỏ, không lưu ảnh và không gửi đi đâu.", "Rain, snow, drifting leaves... around the pet, chosen from your Windows wallpaper's colors. Only the colors of a tiny thumbnail are read; nothing is saved or sent anywhere.") };
        form.Guests = new CheckBox { Content = L("Khách ghé thăm", "Visitors"), IsChecked = preferences.Guests,
            ToolTip = L("Thỉnh thoảng (khoảng 40–90 phút) một con bướm giấy hoặc slime hiền đi ngang, mèo nhìn theo hoặc đuổi một đoạn. Không đến khi mèo ngủ, ở chế độ Yên lặng hay Giờ tập trung.",
                        "Now and then (every 40–90 minutes or so) a paper butterfly or a gentle slime comes by; the pet watches or follows a little. Never while it sleeps, in Quiet mode or focus time.") };
        form.Treasure = new CheckBox { Content = L("Săn kho báu", "Treasure hunt"), IsChecked = preferences.Treasure,
            ToolTip = L("Thỉnh thoảng (khoảng 60–120 phút, tối đa 3 lần mỗi ngày) mèo tìm thấy một tấm bản đồ; bấm bản đồ trên đầu mèo để cùng tìm 3 dấu chân và mở rương. Tắt thì bản đồ đang có mất ngay.",
                        "Now and then (every 60–120 minutes or so, at most 3 a day) the pet finds a map; click the map over its head to find 3 paw prints together and open a chest. Turned off, a map it holds is gone at once.") };
        form.Farm = new CheckBox { Content = L("Nông trại trên desktop", "Farm on the desktop"), IsChecked = preferences.Farm,
            ToolTip = L("Ruộng, bếp, hồ và các vùng đất khác sát trên thanh taskbar: bấm vào ô ruộng để cày, gieo, tưới và thu hoạch. Tắt thì ẩn đi, cây vẫn được giữ nguyên.", "The field, hearth, pond and other lands just above the taskbar: click a field tile to till, sow, water and harvest. Turned off they are hidden; your crops are kept.") };
        form.FarmHelper = new CheckBox { Content = L("Mèo làm vườn cùng", "Pet helps in the field"), IsChecked = preferences.FarmHelper,
            ToolTip = L("Mèo tự ra ruộng tưới hoặc gặt từng ô, nghỉ giữa các chuyến và đuổi chim. Không làm khi đang ngủ, bị kéo hay ở chế độ Yên lặng.", "The pet goes to the field to water or harvest tile by tile, resting between trips, and chases birds away. It does nothing while asleep, held or in Quiet mode.") };
        form.FarmResow = new CheckBox { Content = L("Mèo tự gieo lại", "Pet sows again"), IsChecked = preferences.FarmResow,
            ToolTip = L("Ô vừa gặt được mèo cào đất và gieo lại đúng cây đó, trả xu nông trại như thường. Ô chưa từng trồng vẫn là việc của bạn.", "A tile just harvested is tilled and sown again with the same crop, paying farm coins as usual. Tiles that never grew anything stay yours.") };
        form.FarmAutoCook = new CheckBox { Content = L("Mèo tự nấu ăn", "Pet cooks by itself"), IsChecked = preferences.FarmAutoCook,
            ToolTip = L("Thỉnh thoảng mèo ra bếp nấu một món từ kho rồi ăn, luôn chừa lại ít nhất 4 mỗi nguyên liệu. Tắt \"Mèo làm vườn cùng\" thì mèo cũng thôi tự nấu.", "Now and then the pet cooks a dish from the store and eats it, always leaving at least 4 of each ingredient. Turning off \"Pet helps in the field\" stops this too.") };
        form.FarmAutoFish = new CheckBox { Content = L("Mèo tự câu cá", "Pet fishes by itself"), IsChecked = preferences.FarmAutoFish,
            ToolTip = L("Lúc hết việc ruộng, thỉnh thoảng mèo ra hồ câu một con cá vào kho. Tắt \"Mèo làm vườn cùng\" thì mèo cũng thôi tự câu.", "When the field needs nothing, the pet now and then goes to the pond and brings a fish to the store. Turning off \"Pet helps in the field\" stops this too.") };
        form.ShowTrophies = new CheckBox { Content = L("Góc trưng bày kỷ vật", "Keepsake display corner"), IsChecked = preferences.ShowTrophies,
            ToolTip = L("Một kỷ vật Viễn chinh đứng trên sàn màn hình: cầm kéo, ném được; mèo thỉnh thoảng ra ngắm hoặc đẩy nó.",
                        "One Expedition keepsake stands on the screen's floor: pick it up, throw it; the cat looks at it or shoves it now and then.") };
        var displayable = DisplayableKeepsakes.ToArray();
        form.TrophyIds = displayable.Select(k => k.Id).ToArray();
        form.Trophy = new ComboBox { IsEnabled = displayable.Length > 0,
            ItemsSource = displayable.Length > 0 ? displayable.Select(k => k.Name(preferences.Language)).ToArray() : new[] { L("Chưa có kỷ vật", "No keepsakes yet") },
            SelectedIndex = Math.Max(0, Array.IndexOf(form.TrophyIds, DisplayedKeepsake?.Id)),
            ToolTip = L("Kỷ vật đứng trên màn hình (thắng Viễn chinh để nhận thêm).", "The keepsake standing on the screen (win Expeditions for more).") };
        form.KeepsakeSection = Section(L("Kỷ vật", "Keepsake"), PetMotif.Gem,
            form.ShowTrophies,
            Row(L("Món trưng bày", "On display"), form.Trophy));
        var page = Page(
            Section(L("Hồ sơ", "Profile"), PetMotif.Cat,
                Row(L("Tên", "Name"), form.Name),
                Row(L("Tính cách", "Personality"), form.Personality),
                Row(L("Sinh nhật của bạn", "Your birthday"), birthday)),
            Section(L("Ngoại hình", "Look"), PetMotif.Palette, appearance),
            Section(L("Thói quen", "Habits"), PetMotif.Clock,
                form.ClimbWindows,
                form.Weather,
                form.Guests,
                form.Treasure,
                form.Farm,
                form.FarmHelper,
                form.FarmResow,
                form.FarmAutoCook,
                form.FarmAutoFish,
                form.Tuning),
            form.KeepsakeSection);
        // Gentle idle motion (breathing, tail, blinks) while the window is open.
        var previewClock = System.Diagnostics.Stopwatch.StartNew();
        double previewLast = 0;
        var previewTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        previewTimer.Tick += (_, _) =>
        {
            double now = previewClock.Elapsed.TotalSeconds, dt = Math.Min(0.1, now - previewLast);
            previewLast = now;
            previewPet.AnimationTime += dt; previewPet.ActionTime += dt;
            form.PetPreview.InvalidateVisual();
        };
        previewTimer.Start();
        form.PreviewTimer = previewTimer;

        customizeWindow = FormWindow(L("Tùy chỉnh", "Customize") + " — " + preferences.Name, CustomizeWidth, 620, page, form);
        form.Save.Click += (_, _) =>
        {
            preferences.Name = string.IsNullOrWhiteSpace(form.Name.Text) ? "Mochi" : form.Name.Text.Trim();
            preferences.Personality = personalities[Math.Max(0, form.Personality.SelectedIndex)]; Title = preferences.Name + " — Desktop Pet";
            behavior.Personality = preferences.Personality;
            preferences.Coat = form.CoatIds[Math.Max(0, form.Coat.SelectedIndex)];
            string oldAccessory = preferences.Accessory;
            preferences.Accessory = form.AccessoryIds[Math.Max(0, form.Accessory.SelectedIndex)];
            // A day past the month's end (31 February) becomes its last day; both parts are needed.
            int birthMonth = Math.Max(0, form.BirthMonth.SelectedIndex), birthDay = Math.Max(0, form.BirthDay.SelectedIndex);
            preferences.Birthday = birthMonth > 0 && birthDay > 0
                ? $"{birthMonth:00}-{Math.Min(birthDay, DateTime.DaysInMonth(2000, birthMonth)):00}" : "";
            preferences.HolidayAccessories = form.HolidayAccessories.IsChecked == true;
            renderer.Coat = PetScene.Coat = preferences.Coat; ApplyAccessory();
            if (preferences.Accessory is { Length: > 0 } and not "None" && preferences.Accessory != oldAccessory) ReactToAccessory(); // PetWindow.ToyReact.cs
            preferences.ClimbWindows = form.ClimbWindows.IsChecked == true;
            if (!preferences.ClimbWindows) DropLedges(); // A pet on a window falls; a climbing one lets go.
            preferences.Weather = form.Weather.IsChecked == true;
            preferences.Guests = form.Guests.IsChecked == true;
            preferences.Treasure = form.Treasure.IsChecked == true;
            preferences.Farm = form.Farm.IsChecked == true;
            preferences.FarmHelper = form.FarmHelper.IsChecked == true;
            preferences.FarmResow = form.FarmResow.IsChecked == true;
            preferences.FarmAutoCook = form.FarmAutoCook.IsChecked == true;
            preferences.FarmAutoFish = form.FarmAutoFish.IsChecked == true;
            preferences.ShowTrophies = form.ShowTrophies.IsChecked == true;
            if (!preferences.ShowTrophies) StopAdmiring();
            if (form.TrophyIds.Length > 0)
            {
                string chosen = form.TrophyIds[Math.Clamp(form.Trophy.SelectedIndex, 0, form.TrophyIds.Length - 1)];
                // A keepsake on show swaps with its puff, like the keepsake's own menu; otherwise straight away.
                if (chosen == DisplayedKeepsake?.Id || !preferences.ShowTrophies || !TrophiesVisible || paused || !SwapDisplayed(chosen))
                    preferences.TrophyShown = chosen;
            }
            Interact("preferences_changed"); SavePreferences(); customizeWindow?.Close();
        };
        customizeWindow.Closed += (_, _) => { form.PreviewTimer.Stop(); customizeWindow = null; customizeForm = null; };
        customizeForm = form;
        customizeWindow.Show();
        if (keepsakes) { customizeWindow.UpdateLayout(); form.KeepsakeSection.BringIntoView(); }
    }

    public void ShowPetSettings()
    {
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        var form = new PetSettingsForm();

        // Speech bubbles (lines come from the AI): how often, and forgetting learned habits.
        form.Talks = new CheckBox { Content = L("Mèo biết nói (bong bóng chữ)", "Pet talks (speech bubbles)"), IsChecked = preferences.Speech };
        form.Chattiness = new ComboBox { ItemsSource = new[] { L("Ít — chỉ khi có chuyện", "Rarely — events only"), L("Vừa", "Sometimes"), L("Nhiều", "Often") },
            SelectedIndex = preferences.Chattiness };
        EnableWith(form.Chattiness, form.Talks);
        form.Forget = PetUi.LinkButton(L("Quên những gì mèo đã học…", "Forget what the pet learned…"));
        form.Forget.Click += (_, _) =>
        {
            if (!PetDialog.Show(settingsWindow, L("Quên thói quen đã học", "Forget learned habits"),
                    L("Mèo sẽ quên những hành động và câu nói bạn thích. Nhu cầu và độ thân thiết được giữ.",
                      "The pet will forget which actions and lines you liked. Needs and bond are kept."),
                    L("Quên", "Forget"), L("Hủy", "Cancel"), PetDialog.QuestionGlyph)) return;
            preferences.LearningReset = Guid.NewGuid().ToString("N"); // Python applies it on its next decision.
            SavePreferences();
        };
        var speechTab = Page(
            Section(L("Bong bóng chữ", "Speech bubbles"), PetMotif.Bubble,
                form.Talks,
                Row(L("Mức độ", "How often"), form.Chattiness),
                PetUi.Hint(L("Mèo tự chọn câu nói và học điều bạn thích khi \"Mèo thông minh (AI)\" ở biểu tượng khay đang bật.",
                             "The pet picks its lines and learns what you like while \"Smart pet (AI)\" is on in the tray icon."))),
            Section(L("Trí nhớ", "Memory"), PetMotif.Book, form.Forget));

        // Sounds: one master switch, three kinds, volume and a preview that plays even while muted.
        form.Sounds = new CheckBox { Content = L("Bật âm thanh", "Enable sounds"), IsChecked = preferences.Sounds };
        form.InteractionSounds = new CheckBox { Content = L("Khi tương tác (vuốt, chơi, ăn)", "Interaction (petting, play, treats)"), IsChecked = preferences.InteractionSounds };
        form.EmotionSounds = new CheckBox { Content = L("Khi có cảm xúc (giật mình, khó chịu)", "Emotions (surprised, annoyed)"), IsChecked = preferences.EmotionSounds };
        form.SleepSounds = new CheckBox { Content = L("Khi ngủ và thức dậy", "Sleeping and waking"), IsChecked = preferences.SleepSounds };
        var kinds = new StackPanel { Margin = new Thickness(46, 2, 0, 0) }; // Under the master switch's label.
        foreach (var box in new[] { form.InteractionSounds, form.EmotionSounds, form.SleepSounds })
        {
            box.Margin = new Thickness(0, 4, 0, 0);
            EnableWith(box, form.Sounds);
            kinds.Children.Add(box);
        }
        form.Volume = new Slider { Minimum = 0, Maximum = 1, Value = preferences.Volume, SmallChange = 0.05, LargeChange = 0.1, VerticalAlignment = VerticalAlignment.Center };
        var percent = new TextBlock { Width = 40, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        void ShowPercent() => percent.Text = Math.Round(form.Volume.Value * 100) + "%";
        // The volume applies while it is dragged (0 silences the cue playing now); Cancel puts it back.
        double savedVolume = preferences.Volume;
        form.Volume.ValueChanged += (_, _) =>
        {
            ShowPercent();
            preferences.Volume = form.Volume.Value;
            if (preferences.Volume <= 0) sound?.Stop();
        };
        ShowPercent();
        var volumeRow = new DockPanel();
        DockPanel.SetDock(percent, Dock.Right);
        volumeRow.Children.Add(percent); volumeRow.Children.Add(form.Volume);
        form.PreviewCue = new ComboBox { SelectedIndex = 0 };
        foreach (var (_, vi, en) in PreviewCues) form.PreviewCue.Items.Add(L(vi, en));
        form.PreviewCue.SelectedIndex = 0;
        form.Preview = PetUi.SecondaryButton("▶ " + L("Phát", "Play"));
        form.Preview.MinWidth = 0;
        form.Preview.Click += (_, _) => PlayPetSound(PreviewCues[Math.Max(0, form.PreviewCue.SelectedIndex)].Cue, true, form.Volume.Value);
        var previewRow = new DockPanel();
        DockPanel.SetDock(form.Preview, Dock.Right);
        previewRow.Children.Add(form.Preview); previewRow.Children.Add(form.PreviewCue);
        var soundsTab = Page(
            Section(L("Khi nào phát", "When to play"), PetMotif.Note, form.Sounds, kinds),
            Section(L("Âm lượng", "Volume"), PetMotif.Speaker,
                Row(L("Âm lượng", "Volume"), volumeRow),
                Row(L("Nghe thử", "Preview"), previewRow),
                PetUi.Hint(L("Âm lượng đổi ngay khi kéo (Hủy để trả lại). Nghe thử phát cả khi đang tắt âm thanh.",
                             "The volume changes as you drag it (Cancel puts it back). Preview plays even while sounds are off."))));

        // General: language (labelled in both languages so it is always findable), startup, breaks.
        form.LanguageIds = PetLanguage.Choices(preferences.Language);
        form.Language = new ComboBox { ItemsSource = form.LanguageIds.Select(PetLanguage.DisplayName).ToArray(), SelectedIndex = Array.IndexOf(form.LanguageIds, preferences.Language) };
        // Read fresh from the registry; written only on Save and only if it changed.
        bool autostartOn = Environment.ProcessPath is string exe && startup.IsEnabled(exe);
        form.Autostart = new CheckBox { Content = L("Khởi động cùng Windows", "Start with Windows"), IsChecked = autostartOn };
        // Counts only time spent at the computer; being away 5 minutes counts as a break.
        form.BreakReminders = new CheckBox { Content = L("Nhắc tôi nghỉ giải lao", "Remind me to take a break"), IsChecked = preferences.BreakReminders, Margin = new Thickness(0, 14, 0, 0) };
        form.BreakInterval = new ComboBox { ItemsSource = new[] { L("Mỗi 30 phút", "Every 30 minutes"), L("Mỗi 45 phút", "Every 45 minutes"), L("Mỗi 60 phút", "Every 60 minutes") },
            SelectedIndex = Math.Max(0, Array.IndexOf(PetPreferences.BreakChoices, preferences.BreakMinutes)) };
        EnableWith(form.BreakInterval, form.BreakReminders);
        form.BreakReminders.Margin = default;
        // Updates: only the installed app (Setup.exe) updates itself; a copied folder shows its version and says so.
        bool installed = Updates?.Available == true;
        form.AutoUpdate = new CheckBox { Content = L("Tự động tải và cài bản mới", "Download and install new versions automatically"), IsChecked = preferences.AutoUpdate, IsEnabled = installed };
        string checkText = L("Kiểm tra cập nhật", "Check for updates");
        form.CheckUpdates = PetUi.SecondaryButton(checkText);
        form.CheckUpdates.HorizontalAlignment = HorizontalAlignment.Left;
        form.CheckUpdates.IsEnabled = installed && Updates?.Checking != true;
        form.CheckUpdates.Click += async (_, _) =>
        {
            if (Updates == null) return;
            form.CheckUpdates.IsEnabled = false; form.CheckUpdates.Content = L("Đang kiểm tra…", "Checking…");
            await Updates.Check(true); // The tray says what it found.
            form.CheckUpdates.IsEnabled = true; form.CheckUpdates.Content = checkText;
        };
        // Uninstall (installed app only; a copied folder is removed by deleting it): asks first, data kept unless ticked.
        form.Uninstall = PetUi.SecondaryButton(L("Gỡ cài đặt Desktop Pet…", "Uninstall Desktop Pet…"));
        form.Uninstall.HorizontalAlignment = HorizontalAlignment.Left;
        form.Uninstall.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;
        form.Uninstall.Click += (_, _) =>
        {
            var question = UninstallQuestion(L, out var removeData);
            question.Owner = settingsWindow;
            if (question.ShowDialog() != true || Updates == null) return;
            if (Updates.StartUninstall(removeData.IsChecked == true)) Application.Current.Shutdown();
            else PetDialog.Show(settingsWindow, "Desktop Pet", L("Không mở được trình gỡ cài đặt. Hãy gỡ trong Cài đặt Windows → Ứng dụng.",
                "The uninstaller could not start. Uninstall from Windows Settings → Apps instead."), "OK", glyph: PetDialog.WarningGlyph);
        };
        // About (it was "More" on the cat's menu): where the data and logs are, the AI's state, and the debug view.
        form.DebugView = PetUi.SecondaryButton(L("Xem debug hành vi", "Behavior debug view"));
        form.DebugView.HorizontalAlignment = HorizontalAlignment.Left;
        form.DebugView.Click += (_, _) => ToggleDebug();
        // The cat-led guide again (M2b, PetWindow.Tour.cs); the settings stay open.
        form.ReplayGuide = PetUi.SecondaryButton(L("Xem lại hướng dẫn", "Replay the guide"));
        form.ReplayGuide.Click += (_, _) =>
        {
            if (StartTour() is string reason) PetDialog.Show(settingsWindow, "Desktop Pet", reason, "OK");
        };
        // The first-run welcome card again: the settings close unsaved (the card writes the same settings).
        form.ReplayWelcome = PetUi.SecondaryButton(L("Mở lại phiếu Chào mừng", "Open the welcome again"));
        form.ReplayWelcome.Click += (_, _) => { settingsWindow?.Close(); ReplayWelcome(); };
        var guideButtons = new WrapPanel();
        foreach (var button in new[] { form.ReplayGuide, form.ReplayWelcome }) { button.Margin = new Thickness(0, 0, 8, 4); guideButtons.Children.Add(button); }
        var dataFolder = new TextBox { Text = PetPreferences.DataDirectory, IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, TextWrapping = TextWrapping.Wrap };
        var generalTab = Page(
            Section(L("Hướng dẫn", "Guide"), PetMotif.Book,
                guideButtons,
                PetUi.Hint(L("Mèo dẫn bạn đi xem một vòng: kéo mèo, menu, biển nông trại, đồ vật, khay. Cũng có ở menu khay.",
                             "The pet shows you around: dragging, its menu, the farm sign, things, the tray. Also on the tray menu."))),
            Section(L("Ứng dụng", "App"), PetMotif.Gear,
                Row("Language / Ngôn ngữ", form.Language),
                form.Autostart,
                form.Uninstall),
            Section(L("Cập nhật", "Updates"), PetMotif.Gift,
                Row(L("Phiên bản", "Version"), new TextBlock { Text = VersionText, VerticalAlignment = VerticalAlignment.Center }),
                form.AutoUpdate,
                form.CheckUpdates,
                PetUi.Hint(installed
                    ? L("Bản mới được tải ngầm, khay báo khi xong. Tắt tự động thì chỉ kiểm tra khi bạn bấm.",
                        "New versions download in the background; the tray tells you when one is ready. Turned off, it only checks when you ask.")
                    : L("Bản này không được cài bằng Setup nên không tự cập nhật. Bản cài đặt có ở trang Releases trên GitHub.",
                        "This copy was not installed with Setup, so it does not update itself. The installer is on the GitHub Releases page."))),
            Section(L("Nghỉ giải lao", "Breaks"), PetMotif.Cup,
                form.BreakReminders,
                Row(L("Nhắc", "Remind"), form.BreakInterval),
                PetUi.Hint(L("Chỉ tính thời gian bạn dùng máy. Không nhắc khi đang mở app toàn màn hình.",
                             "Counts only time you use the computer. Not shown during fullscreen apps."))),
            Section(L("Giới thiệu Desktop Pet", "About Desktop Pet"), PetMotif.Cat,
                Row(L("Phiên bản", "Version"), new TextBlock { Text = PetLog.VersionLabel(PetLog.BuildVersion, preferences.Language), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }),
                Row(L("Dữ liệu và log", "Data and logs"), dataFolder),
                PetUi.Hint(AiStatusText),
                form.DebugView));

        foreach (var pair in new[] { (speechTab, PetSceneKind.Speech), (soundsTab, PetSceneKind.Sound), (generalTab, PetSceneKind.Settings) })
            if (pair.Item1.Content is StackPanel page) page.Children.Insert(0, new PetScene(pair.Item2, 100));

        // A sidebar (PetControlStyle); each Tag is the page's Segoe icon: chat, speaker, gear.
        form.Tabs = new TabControl { Padding = new Thickness(0) };
        form.Tabs.Items.Add(new TabItem { Header = L("Lời nói", "Speech"), Tag = "", Content = speechTab });
        form.Tabs.Items.Add(new TabItem { Header = L("Âm thanh", "Sounds"), Tag = "", Content = soundsTab });
        form.Tabs.Items.Add(new TabItem { Header = L("Chung", "General"), Tag = "", Content = generalTab });

        settingsWindow = FormWindow(L("Cài đặt", "Settings") + " — " + preferences.Name, SettingsWidth, 520, form.Tabs, form);
        form.Save.Click += (_, _) =>
        {
            preferences.Volume = savedVolume = form.Volume.Value; preferences.Sounds = form.Sounds.IsChecked == true;
            preferences.InteractionSounds = form.InteractionSounds.IsChecked == true; preferences.EmotionSounds = form.EmotionSounds.IsChecked == true;
            preferences.SleepSounds = form.SleepSounds.IsChecked == true;
            string language = form.LanguageIds[Math.Max(0, form.Language.SelectedIndex)];
            bool languageChanged = preferences.Language != language;
            preferences.Speech = form.Talks.IsChecked == true; preferences.Language = language;
            if (languageChanged) { pet.Speech.Hide(); pet.Speech.DropPending(); RaiseLanguageChanged(); } // No line in the old language.
            preferences.Chattiness = Math.Clamp(form.Chattiness.SelectedIndex, 0, 2);
            if (!preferences.Speech) pet.Speech.Hide();
            preferences.BreakReminders = form.BreakReminders.IsChecked == true;
            preferences.BreakMinutes = PetPreferences.BreakChoices[Math.Max(0, form.BreakInterval.SelectedIndex)];
            breaks.Configure(preferences.BreakReminders, preferences.BreakMinutes);
            preferences.AutoUpdate = form.AutoUpdate.IsChecked == true;
            Updates?.SetAutomatic(preferences.AutoUpdate);
            // Test windows never touch the real Windows startup entry.
            if (persistPreferences && form.Autostart.IsChecked == true != autostartOn &&
                (Environment.ProcessPath is not string path || !startup.SetEnabled(form.Autostart.IsChecked == true, path)))
                PetDialog.Show(settingsWindow, "Desktop Pet", L("Không đổi được cài đặt khởi động cùng Windows.", "Windows startup could not be changed."), "OK", glyph: PetDialog.WarningGlyph);
            sound?.Stop(); Interact("preferences_changed"); SavePreferences(); settingsWindow?.Close();
        };
        settingsWindow.Closed += (_, _) => { preferences.Volume = savedVolume; settingsWindow = null; settingsForm = null; };
        settingsForm = form;
        settingsWindow.Show();
    }

    private const double SettingsWidth = 600, CustomizeWidth = 480;

    // A form window on the pet's monitor, near the cat: the body above, Save and Cancel always visible
    // below it (the body scrolls on its own if the screen is small). Cancel or closing keeps everything.
    private Window FormWindow(string title, double width, double maxHeight, UIElement body, PetSettingsForm form)
    {
        form.Save = PetUi.PrimaryButton(L("Lưu", "Save"));
        form.Cancel = PetUi.SecondaryButton(L("Hủy", "Cancel"));
        form.Cancel.IsCancel = true;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
        buttons.Children.Add(form.Cancel); buttons.Children.Add(form.Save);
        var buttonBar = new Border { Child = buttons, BorderBrush = PetUi.Line, BorderThickness = new Thickness(0, 1, 0, 0) };
        var root = new DockPanel();
        DockPanel.SetDock(buttonBar, Dock.Bottom);
        root.Children.Add(buttonBar); root.Children.Add(body);

        double areaHeight = Math.Max(300, bounds.Bottom - bounds.Top);
        double height = Math.Min(maxHeight, areaHeight - 24);
        var window = new Window { Title = title, Icon = AppIcon.WindowImage(), Width = width, Height = height,
            Content = root, Background = PetUi.Surface, ResizeMode = ResizeMode.NoResize, Topmost = true, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = Math.Clamp(pet.X + settings.Width / 2 - width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width)),
            Top = bounds.Top + Math.Max(8, (areaHeight - height) / 2) };
        PetUi.ApplyWindowFont(window);
        PetControlStyle.Apply(window);
        form.Cancel.Click += (_, _) => window.Close();
        return window;
    }

    // "Uninstall Desktop Pet?" with "Also delete the pet's data" (not ticked: reinstalling remembers everything).
    public static Window UninstallQuestion(Func<string, string, string> L, out CheckBox removeData)
    {
        removeData = new CheckBox { Content = L("Xóa cả dữ liệu của mèo (cài đặt, nhật ký, tiến trình trận, kỷ lục)",
            "Also delete the pet's data (settings, diary, battle progress, records)"), IsChecked = false };
        return PetDialog.Create(null, L("Gỡ cài đặt Desktop Pet", "Uninstall Desktop Pet"),
            L("Gỡ Desktop Pet khỏi máy? Mèo sẽ đóng, shortcut và mục khởi động cùng Windows bị xóa. Không tích ô dưới thì cài lại mèo vẫn nhớ mọi thứ.",
              "Remove Desktop Pet from this PC? The pet closes; its shortcuts and Windows startup entry are removed. Leave the box unticked and a reinstall remembers everything."),
            L("Gỡ cài đặt", "Uninstall"), L("Hủy", "Cancel"), PetDialog.WarningGlyph, removeData);
    }

    // One sidebar page: its cards stacked, scrolling on its own.
    private static ScrollViewer Page(params UIElement[] sections)
    {
        var panel = new StackPanel { Margin = new Thickness(16, 14, 16, 6) };
        foreach (var section in sections) panel.Children.Add(section);
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    // A titled card of related settings, spaced evenly; a small picture leads the title.
    private static Border Section(string title, PetMotif motif, params UIElement[] children)
    {
        var group = new StackPanel();
        var heading = PetUi.SectionTitle(title);
        heading.Margin = new Thickness(0); heading.VerticalAlignment = VerticalAlignment.Center;
        var titled = PetIllustration.Beside(heading, motif, 26);
        titled.Margin = new Thickness(0, 0, 0, 6);
        group.Children.Add(titled);
        foreach (var child in children)
        {
            if (child is FrameworkElement element && element.Margin == default && child is not StackPanel) element.Margin = new Thickness(0, 5, 0, 5);
            group.Children.Add(child);
        }
        return PetUi.Card(group);
    }

    // Label on the left, control on the right, so a card reads like a short form.
    private static Grid Row(string label, UIElement control)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(142) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(control, 1);
        grid.Children.Add(text); grid.Children.Add(control);
        return grid;
    }

    private static readonly DataTemplate CoatTemplate = CreateCoatTemplate();
    private static DataTemplate CreateCoatTemplate()
    {
        var row = new FrameworkElementFactory(typeof(StackPanel));
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var dot = new FrameworkElementFactory(typeof(Grid));
        dot.SetValue(FrameworkElement.WidthProperty, 16.0);
        dot.SetValue(FrameworkElement.HeightProperty, 16.0);
        dot.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var fur = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
        fur.SetBinding(System.Windows.Shapes.Shape.FillProperty, new Binding(nameof(CoatChoice.Fur)));
        fur.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new Binding(nameof(CoatChoice.Ink)));
        fur.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty, 1.0);
        var accent = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
        accent.SetBinding(System.Windows.Shapes.Shape.FillProperty, new Binding(nameof(CoatChoice.Accent)));
        accent.SetValue(FrameworkElement.WidthProperty, 7.0);
        accent.SetValue(FrameworkElement.HeightProperty, 7.0);
        accent.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        accent.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Bottom);
        accent.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 2, 2));
        dot.AppendChild(fur); dot.AppendChild(accent);
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(CoatChoice.Label)));
        text.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        row.AppendChild(dot); row.AppendChild(text);
        var template = new DataTemplate(typeof(CoatChoice)) { VisualTree = row };
        template.Seal();
        return template;
    }

    // A dependent setting is greyed out while its switch is off.
    private static void EnableWith(UIElement target, CheckBox source) =>
        BindingOperations.SetBinding(target, UIElement.IsEnabledProperty, new Binding(nameof(CheckBox.IsChecked)) { Source = source });
}
