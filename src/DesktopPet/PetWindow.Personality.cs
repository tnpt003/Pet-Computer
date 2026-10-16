using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace DesktopPet;

public sealed partial class PetWindow
{
    private Window? behaviorSettingsWindow;
    private void ShowBehaviorSettings()
    {
        if (behaviorSettingsWindow != null) { behaviorSettingsWindow.Activate(); return; }
        var personalityDefaults = PersonalityTuning.LoadDefaults();
        var draft = (preferences.Tuning ?? personalityDefaults).Normalized();
        var panel = new StackPanel { Margin = new Thickness(16, 14, 16, 4) };
        panel.Children.Add(new TextBlock { Text = L("Các thông số này điều khiển quyết định và năng lượng của Python AI. Có hiệu lực sau khi Lưu; không ảnh hưởng khóa ngủ hay vật lý khi ném.",
            "These values control the Python AI's decisions and energy. They apply after Save; sleep locks and throw physics are unaffected."),
            TextWrapping = TextWrapping.Wrap, Foreground = PetUi.Muted, Margin = new Thickness(0, 0, 0, 12) });
        var save = PetUi.PrimaryButton(L("Lưu", "Save"));
        var validInputs = new Dictionary<TextBox, bool>();
        var fields = new List<(Slider Slider, TextBox Input, Func<PersonalityTuning, double> Get, Action<PersonalityTuning, double> Set)>();
        StackPanel? group = null;
        void Group(string title)
        {
            group = new StackPanel();
            group.Children.Add(PetUi.SectionTitle(title));
            panel.Children.Add(PetUi.Card(group));
        }
        // One setting: name and exact value on a line, the slider below, then a short hint.
        void Add(string title, string help, double min, double max, Func<PersonalityTuning, double> get, Action<PersonalityTuning, double> set)
        {
            var slider = new Slider { Minimum = min, Maximum = max, Value = get(draft), ToolTip = help, SmallChange = (max - min) / 100, Margin = new Thickness(0, 4, 0, 0) };
            var number = new TextBox { Text = slider.Value.ToString("0.##", CultureInfo.InvariantCulture), Width = 76, TextAlignment = TextAlignment.Right, Padding = new Thickness(2, 1, 2, 1) };
            validInputs[number] = true;
            bool updating = false;
            slider.ValueChanged += (_, _) => { if (!updating) number.Text = slider.Value.ToString("0.##", CultureInfo.InvariantCulture); };
            number.TextChanged += (_, _) =>
            {
                bool valid = double.TryParse(number.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) && value >= min && value <= max;
                validInputs[number] = valid;
                number.ToolTip = valid ? help : L($"Nhập số từ {min} đến {max}.", $"Enter a number from {min} to {max}.");
                if (valid) { updating = true; slider.Value = value; updating = false; }
                save.IsEnabled = !validInputs.ContainsValue(false);
            };
            var header = new DockPanel { Margin = new Thickness(0, group!.Children.Count > 1 ? 12 : 2, 0, 0) };
            DockPanel.SetDock(number, Dock.Right);
            header.Children.Add(number);
            header.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            group.Children.Add(header); group.Children.Add(slider);
            var hint = PetUi.Hint(help); hint.Margin = new Thickness(0, 0, 0, 0);
            group.Children.Add(hint);
            fields.Add((slider, number, get, set));
        }
        Group(L("Tính cách", "Personality"));
        Add(L("Ham chơi", "Playfulness"), L("Cao hơn thì hay chọn hành động vui chơi.", "Higher values favor playful decisions."), 0, 1, p => p.Playfulness, (p,v) => p.Playfulness=v);
        Add(L("Tò mò", "Curiosity"), L("Cao hơn thì hay đi theo và nhìn quanh.", "Higher values favor following and looking around."), 0, 1, p => p.Curiosity, (p,v) => p.Curiosity=v);
        Add(L("Buồn ngủ", "Sleepiness"), L("Đổi mức thích ngủ ngẫu nhiên, không đổi thời lượng ngủ cố định.", "Changes random sleep preference, not the fixed sleep duration."), 0, 1, p => p.Sleepiness, (p,v) => p.Sleepiness=v);
        Group(L("Đòi chú ý", "Attention"));
        Add(L("Thời gian chờ (giây)", "Delay (seconds)"), L("Thời gian trước khi AI có thể đòi chú ý; tính cách điều chỉnh thêm.", "Time before AI may seek attention; personality adjusts this delay."), 10, 86400, p => p.AttentionAfterSeconds, (p,v) => p.AttentionAfterSeconds=v);
        Group(L("Năng lượng mỗi giây", "Energy per second"));
        Add(L("Khi đi", "Walking"), L("Năng lượng tiêu hao khi đi.", "Energy spent while walking."), 0, 10, p => p.WalkingEnergyPerSecond, (p,v) => p.WalkingEnergyPerSecond=v);
        Add(L("Khi chơi", "Playing"), L("Năng lượng tiêu hao khi nhảy.", "Energy spent jumping."), 0, 10, p => p.PlayingEnergyPerSecond, (p,v) => p.PlayingEnergyPerSecond=v);
        Add(L("Khi ngủ", "Sleeping"), L("Năng lượng hồi lại khi ngủ.", "Energy restored during sleep."), 0, 20, p => p.SleepEnergyPerSecond, (p,v) => p.SleepEnergyPerSecond=v);
        Add(L("Khi nghỉ", "Resting"), L("Năng lượng hồi lại khi nghỉ.", "Energy restored while resting."), 0, 5, p => p.RestEnergyPerSecond, (p,v) => p.RestEnergyPerSecond=v);
        var defaults = PetUi.LinkButton(L("Khôi phục mặc định", "Restore defaults"));
        defaults.ToolTip = L("Lấy lại giá trị trong personality.json", "Reload the values from personality.json");
        bool useDefaults = false;
        foreach (var field in fields) field.Slider.ValueChanged += (_, _) => useDefaults = false;
        defaults.Click += (_, _) => { personalityDefaults = PersonalityTuning.LoadDefaults(); foreach (var field in fields) { field.Slider.Value = field.Get(personalityDefaults); field.Input.Text = field.Slider.Value.ToString("0.##", CultureInfo.InvariantCulture); } useDefaults = true; };
        save.Click += (_, _) =>
        {
            foreach (var field in fields) field.Set(draft, field.Slider.Value);
            preferences.Tuning = useDefaults ? null : draft.Normalized();
            // Reload packaged policy defaults without resetting a sleeping/waking action.
            if (useDefaults && AiEnabled) { StopAi(); ai = new DesktopPet.AI.PythonDecisionSource(); }
            Interact("preferences_changed"); SavePreferences(); behaviorSettingsWindow?.Close();
        };
        var cancel = PetUi.SecondaryButton(L("Hủy", "Cancel"));
        cancel.IsCancel = true;
        cancel.Click += (_, _) => behaviorSettingsWindow?.Close();
        // Save and Cancel stay visible at the bottom while the groups scroll.
        var root = new DockPanel();
        var bar = PetUi.ButtonBar(defaults, cancel, save);
        DockPanel.SetDock(bar, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        behaviorSettingsWindow = new Window { Title = L("Tinh chỉnh hành vi", "Behavior tuning") + " — " + preferences.Name, Icon = AppIcon.WindowImage(), Width = 440,
            Height = Math.Min(680, SystemParameters.WorkArea.Height - 24), Owner = settingsWindow, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = settingsWindow != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Content = root, Background = PetUi.Surface, Topmost = true, ShowInTaskbar = false };
        PetUi.ApplyWindowFont(behaviorSettingsWindow);
        behaviorSettingsWindow.Closed += (_, _) => behaviorSettingsWindow = null;
        behaviorSettingsWindow.Show();
    }
}
