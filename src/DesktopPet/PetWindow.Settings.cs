using System;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace DesktopPet;

// The controls of an open settings window (kept so checks can drive the form).
public sealed class PetSettingsForm
{
    public TabControl Tabs = null!;
    public TextBox Name = null!;
    public ComboBox Language = null!, Personality = null!, Coat = null!, Accessory = null!, Chattiness = null!, BreakInterval = null!, PreviewCue = null!, BirthDay = null!, BirthMonth = null!;
    public string[] AccessoryIds = Array.Empty<string>();
    public CheckBox HolidayAccessories = null!, ClimbWindows = null!, ShowTrophies = null!, Talks = null!, Autostart = null!, BreakReminders = null!, Sounds = null!, InteractionSounds = null!, EmotionSounds = null!, SleepSounds = null!;
    public Slider Volume = null!;
    public Button Save = null!, Cancel = null!, Tuning = null!, Forget = null!, Preview = null!;
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

// Settings in four tabs (Pet, Speech, Sounds, General). Nothing changes until Save;
// Cancel or closing the window keeps the old settings.
public sealed partial class PetWindow
{
    private PetSettingsForm? settingsForm;

    private static readonly (string Cue, string Vi, string En)[] PreviewCues =
    {
        ("meow", "Meo", "Meow"), ("purr", "Gừ gừ", "Purr"), ("excited", "Phấn khích", "Excited"),
        ("surprised", "Giật mình", "Surprised"), ("annoyed", "Khó chịu", "Annoyed"), ("sleep", "Ngủ", "Sleep"),
        ("ball_tap", "Chạm bóng", "Ball tap"), ("satisfied", "No nê", "Satisfied"), ("wake", "Thức dậy", "Wake"),
    };

    public void ShowPetSettings()
    {
        if (settingsWindow != null) { settingsWindow.Activate(); return; }
        var form = new PetSettingsForm();

        // Pet: who the cat is and how it looks. Stored values stay English ("Balanced", "Orange"...).
        string[] personalities = { "Balanced", "Playful", "Curious", "Calm" };
        form.Name = new TextBox { Text = preferences.Name, MaxLength = 24, Padding = new Thickness(2, 3, 2, 3) };
        form.Personality = new ComboBox { ItemsSource = new[] { L("Cân bằng", "Balanced"), L("Ham chơi", "Playful"), L("Tò mò", "Curious"), L("Điềm tĩnh", "Calm") },
            SelectedIndex = Math.Max(0, Array.IndexOf(personalities, preferences.Personality)) };
        // Coats and their names come from Assets/coats.json, in PetPreferences.Coats order.
        form.Coat = new ComboBox { ItemTemplate = CoatTemplate,
            ItemsSource = PetCoats.All.Select(coat => new CoatChoice(L(coat.NameVi, coat.NameEn), CoatPalette.For(coat.Id))).ToArray(),
            SelectedIndex = Math.Max(0, Array.IndexOf(PetPreferences.Coats, preferences.Coat)) };
        // Accessory names come from Assets/accessories.json, in PetPreferences.Accessories order;
        // Adventure rewards only once received (or already worn). The list maps back by id.
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
                L("Tháng " + m, System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m)))).ToArray() };
        var birthday = new StackPanel { Orientation = Orientation.Horizontal,
            ToolTip = L("Mèo sẽ chúc mừng sinh nhật bạn vào ngày này (không cần năm sinh).", "The pet wishes you a happy birthday on this day (no year needed).") };
        birthday.Children.Add(form.BirthDay); birthday.Children.Add(form.BirthMonth);
        form.Tuning = PetUi.LinkButton(L("Tinh chỉnh hành vi nâng cao…", "Advanced behavior tuning…"));
        form.Tuning.Click += (_, _) => ShowBehaviorSettings();
        // Live preview: its own model and renderer; the real pet changes only on Save.
        var previewPet = new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1 };
        form.PreviewPet = previewPet;
        form.PetPreview = new PetRenderer(previewPet) { Coat = preferences.Coat, Accessory = preferences.Accessory, Width = 192, Height = 192, Margin = new Thickness(-30) };
        form.Coat.SelectionChanged += (_, _) => form.PetPreview.Coat = PetPreferences.Coats[Math.Max(0, form.Coat.SelectedIndex)];
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
        form.ShowTrophies = new CheckBox { Content = L("Góc trưng bày kỷ vật", "Keepsake display corner"), IsChecked = preferences.ShowTrophies,
            ToolTip = L("Cúp và đồ Viễn chinh đứng ở góc phải dưới màn hình; mèo thỉnh thoảng ra ngắm.", "Expedition cups and keepsakes stand at the bottom right of the screen; the cat looks at them now and then.") };
        var petTab = Page(
            Row(L("Tên", "Name"), form.Name),
            Row(L("Tính cách", "Personality"), form.Personality),
            Row(L("Sinh nhật của bạn", "Your birthday"), birthday),
            appearance,
            form.ClimbWindows,
            form.ShowTrophies,
            form.Tuning);
        // Gentle idle motion (breathing, tail, blinks) while the Pet tab is showing.
        var previewClock = System.Diagnostics.Stopwatch.StartNew();
        double previewLast = 0;
        var previewTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        previewTimer.Tick += (_, _) =>
        {
            double now = previewClock.Elapsed.TotalSeconds, dt = Math.Min(0.1, now - previewLast);
            previewLast = now;
            if (form.Tabs.SelectedIndex != 0) return;
            previewPet.AnimationTime += dt; previewPet.ActionTime += dt;
            form.PetPreview.InvalidateVisual();
        };
        previewTimer.Start();
        form.PreviewTimer = previewTimer;

        // Speech bubbles (lines come from the AI): how often, and forgetting learned habits.
        form.Talks = new CheckBox { Content = L("Mèo biết nói (bong bóng chữ)", "Pet talks (speech bubbles)"), IsChecked = preferences.Speech };
        form.Chattiness = new ComboBox { ItemsSource = new[] { L("Ít — chỉ khi có chuyện", "Rarely — events only"), L("Vừa", "Sometimes"), L("Nhiều", "Often") },
            SelectedIndex = preferences.Chattiness };
        EnableWith(form.Chattiness, form.Talks);
        form.Forget = PetUi.LinkButton(L("Quên những gì mèo đã học…", "Forget what the pet learned…"));
        form.Forget.Click += (_, _) =>
        {
            if (MessageBox.Show(L("Mèo sẽ quên những hành động và câu nói bạn thích. Nhu cầu và độ thân thiết được giữ.",
                                  "The pet will forget which actions and lines you liked. Needs and bond are kept."),
                    L("Quên thói quen đã học", "Forget learned habits"), MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            preferences.LearningReset = Guid.NewGuid().ToString("N"); // Python applies it on its next decision.
            SavePreferences();
        };
        var speechTab = Page(
            form.Talks,
            Row(L("Mức độ", "How often"), form.Chattiness),
            PetUi.Hint(L("Mèo tự chọn câu nói và học điều bạn thích khi \"Mèo thông minh (AI)\" ở biểu tượng khay đang bật.",
                         "The pet picks its lines and learns what you like while \"Smart pet (AI)\" is on in the tray icon.")),
            form.Forget);

        // Sounds: one master switch, three kinds, volume and a preview that plays even while muted.
        form.Sounds = new CheckBox { Content = L("Bật âm thanh", "Enable sounds"), IsChecked = preferences.Sounds };
        form.InteractionSounds = new CheckBox { Content = L("Khi tương tác (vuốt, chơi, ăn)", "Interaction (petting, play, treats)"), IsChecked = preferences.InteractionSounds };
        form.EmotionSounds = new CheckBox { Content = L("Khi có cảm xúc (giật mình, khó chịu)", "Emotions (surprised, annoyed)"), IsChecked = preferences.EmotionSounds };
        form.SleepSounds = new CheckBox { Content = L("Khi ngủ và thức dậy", "Sleeping and waking"), IsChecked = preferences.SleepSounds };
        var kinds = new StackPanel { Margin = new Thickness(22, 2, 0, 0) };
        foreach (var box in new[] { form.InteractionSounds, form.EmotionSounds, form.SleepSounds })
        {
            box.Margin = new Thickness(0, 4, 0, 0);
            EnableWith(box, form.Sounds);
            kinds.Children.Add(box);
        }
        form.Volume = new Slider { Minimum = 0, Maximum = 1, Value = preferences.Volume, SmallChange = 0.05, LargeChange = 0.1, VerticalAlignment = VerticalAlignment.Center };
        var percent = new TextBlock { Width = 40, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        void ShowPercent() => percent.Text = Math.Round(form.Volume.Value * 100) + "%";
        form.Volume.ValueChanged += (_, _) => ShowPercent(); ShowPercent();
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
            form.Sounds, kinds,
            Row(L("Âm lượng", "Volume"), volumeRow),
            Row(L("Nghe thử", "Preview"), previewRow),
            PetUi.Hint(L("Nghe thử phát cả khi đang tắt âm thanh.", "Preview plays even while sounds are off.")));

        // General: language (labelled in both languages so it is always findable), startup, breaks.
        form.Language = new ComboBox { ItemsSource = new[] { "English", "Tiếng Việt" }, SelectedIndex = preferences.Language == "en" ? 0 : 1 };
        // Read fresh from the registry; written only on Save and only if it changed.
        bool autostartOn = Environment.ProcessPath is string exe && startup.IsEnabled(exe);
        form.Autostart = new CheckBox { Content = L("Khởi động cùng Windows", "Start with Windows"), IsChecked = autostartOn };
        // Counts only time spent at the computer; being away 5 minutes counts as a break.
        form.BreakReminders = new CheckBox { Content = L("Nhắc tôi nghỉ giải lao", "Remind me to take a break"), IsChecked = preferences.BreakReminders, Margin = new Thickness(0, 14, 0, 0) };
        form.BreakInterval = new ComboBox { ItemsSource = new[] { L("Mỗi 30 phút", "Every 30 minutes"), L("Mỗi 45 phút", "Every 45 minutes"), L("Mỗi 60 phút", "Every 60 minutes") },
            SelectedIndex = Math.Max(0, Array.IndexOf(PetPreferences.BreakChoices, preferences.BreakMinutes)) };
        EnableWith(form.BreakInterval, form.BreakReminders);
        var generalTab = Page(
            Row("Language / Ngôn ngữ", form.Language),
            form.Autostart,
            form.BreakReminders,
            Row(L("Nhắc", "Remind"), form.BreakInterval),
            PetUi.Hint(L("Chỉ tính thời gian bạn dùng máy. Không nhắc khi đang mở app toàn màn hình.",
                         "Counts only time you use the computer. Not shown during fullscreen apps.")));

        form.Tabs = new TabControl { Margin = new Thickness(12, 12, 12, 0), Padding = new Thickness(0) };
        form.Tabs.Items.Add(new TabItem { Header = L("Mèo", "Pet"), Content = petTab });
        form.Tabs.Items.Add(new TabItem { Header = L("Lời nói", "Speech"), Content = speechTab });
        form.Tabs.Items.Add(new TabItem { Header = L("Âm thanh", "Sounds"), Content = soundsTab });
        form.Tabs.Items.Add(new TabItem { Header = L("Chung", "General"), Content = generalTab });
        foreach (TabItem tab in form.Tabs.Items) tab.Padding = new Thickness(12, 4, 12, 4);

        // Save and Cancel stay visible at the bottom; each tab scrolls on its own if the screen is small.
        form.Save = PetUi.PrimaryButton(L("Lưu", "Save"));
        form.Cancel = PetUi.SecondaryButton(L("Hủy", "Cancel"));
        form.Cancel.IsCancel = true;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
        buttons.Children.Add(form.Cancel); buttons.Children.Add(form.Save);
        var root = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons); root.Children.Add(form.Tabs);

        double areaHeight = Math.Max(300, bounds.Bottom - bounds.Top);
        double height = Math.Min(490, areaHeight - 24);
        settingsWindow = new Window { Title = L("Cài đặt", "Settings") + " — " + preferences.Name, Icon = AppIcon.WindowImage(), Width = 460, Height = height,
            Content = root, Background = PetUi.Surface, ResizeMode = ResizeMode.NoResize, Topmost = true, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = Math.Clamp(pet.X + settings.Width / 2 - 230, bounds.Left, Math.Max(bounds.Left, bounds.Right - 460)),
            Top = bounds.Top + Math.Max(8, (areaHeight - height) / 2) };
        PetUi.ApplyWindowFont(settingsWindow);
        form.Cancel.Click += (_, _) => settingsWindow?.Close();
        form.Save.Click += (_, _) =>
        {
            preferences.Name = string.IsNullOrWhiteSpace(form.Name.Text) ? "Mochi" : form.Name.Text.Trim();
            preferences.Personality = personalities[Math.Max(0, form.Personality.SelectedIndex)]; Title = preferences.Name + " — Desktop Pet";
            behavior.Personality = preferences.Personality;
            preferences.Volume = form.Volume.Value; preferences.Sounds = form.Sounds.IsChecked == true;
            preferences.InteractionSounds = form.InteractionSounds.IsChecked == true; preferences.EmotionSounds = form.EmotionSounds.IsChecked == true;
            preferences.SleepSounds = form.SleepSounds.IsChecked == true;
            bool languageChanged = preferences.Language != (form.Language.SelectedIndex == 0 ? "en" : "vi");
            preferences.Speech = form.Talks.IsChecked == true; preferences.Language = form.Language.SelectedIndex == 0 ? "en" : "vi";
            if (languageChanged) { pet.Speech.Hide(); pet.Speech.DropPending(); RaiseLanguageChanged(); } // No line in the old language.
            preferences.Chattiness = Math.Clamp(form.Chattiness.SelectedIndex, 0, 2);
            if (!preferences.Speech) pet.Speech.Hide();
            preferences.Coat = PetPreferences.Coats[Math.Max(0, form.Coat.SelectedIndex)];
            preferences.Accessory = form.AccessoryIds[Math.Max(0, form.Accessory.SelectedIndex)];
            // A day past the month's end (31 February) becomes its last day; both parts are needed.
            int birthMonth = Math.Max(0, form.BirthMonth.SelectedIndex), birthDay = Math.Max(0, form.BirthDay.SelectedIndex);
            preferences.Birthday = birthMonth > 0 && birthDay > 0
                ? $"{birthMonth:00}-{Math.Min(birthDay, DateTime.DaysInMonth(2000, birthMonth)):00}" : "";
            preferences.HolidayAccessories = form.HolidayAccessories.IsChecked == true;
            renderer.Coat = preferences.Coat; ApplyAccessory();
            preferences.BreakReminders = form.BreakReminders.IsChecked == true;
            preferences.BreakMinutes = PetPreferences.BreakChoices[Math.Max(0, form.BreakInterval.SelectedIndex)];
            breaks.Configure(preferences.BreakReminders, preferences.BreakMinutes);
            preferences.ClimbWindows = form.ClimbWindows.IsChecked == true;
            if (!preferences.ClimbWindows) DropLedges(); // A pet on a window falls; a climbing one lets go.
            preferences.ShowTrophies = form.ShowTrophies.IsChecked == true;
            if (!preferences.ShowTrophies) StopAdmiring();
            // Test windows never touch the real Windows startup entry.
            if (persistPreferences && form.Autostart.IsChecked == true != autostartOn &&
                (Environment.ProcessPath is not string path || !startup.SetEnabled(form.Autostart.IsChecked == true, path)))
                MessageBox.Show(L("Không đổi được cài đặt khởi động cùng Windows.", "Windows startup could not be changed."), "Desktop Pet");
            sound?.Stop(); Interact("preferences_changed"); SavePreferences(); settingsWindow?.Close();
        };
        settingsWindow.Closed += (_, _) => { form.PreviewTimer.Stop(); settingsWindow = null; settingsForm = null; };
        settingsForm = form;
        settingsWindow.Show();
    }

    private static ScrollViewer Page(params UIElement[] children)
    {
        var panel = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };
        foreach (var child in children)
        {
            if (child is FrameworkElement element && element.Margin == default && child is not StackPanel) element.Margin = new Thickness(0, 6, 0, 6);
            panel.Children.Add(child);
        }
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    // Label on the left, control on the right, so a tab reads like a short form.
    private static Grid Row(string label, UIElement control)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
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
