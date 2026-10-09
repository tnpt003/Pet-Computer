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
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Changes apply after Save. Sleep locks and throw physics are unaffected. The eight tuning values control Python AI decisions and energy.", TextWrapping = TextWrapping.Wrap });
        var save = new Button { Content = "Save behavior tuning", Margin = new Thickness(0,4,0,0) };
        var validInputs = new Dictionary<TextBox, bool>();
        var fields = new List<(Slider Slider, TextBox Input, Func<PersonalityTuning, double> Get, Action<PersonalityTuning, double> Set)>();
        void Add(string title, string help, double min, double max, Func<PersonalityTuning, double> get, Action<PersonalityTuning, double> set)
        {
            var label = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };
            var slider = new Slider { Minimum = min, Maximum = max, Value = get(draft), ToolTip = help, SmallChange = (max - min) / 100 };
            void Refresh() => label.Text = title + ": " + slider.Value.ToString("0.##", CultureInfo.InvariantCulture);
            slider.ValueChanged += (_, _) => Refresh(); Refresh();
            var number = new TextBox { Text = slider.Value.ToString("0.##", CultureInfo.InvariantCulture), Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
            validInputs[number] = true;
            bool updating = false;
            slider.ValueChanged += (_, _) => { if (!updating) number.Text = slider.Value.ToString("0.##", CultureInfo.InvariantCulture); };
            number.TextChanged += (_, _) =>
            {
                bool valid = double.TryParse(number.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) && value >= min && value <= max;
                validInputs[number] = valid;
                number.ToolTip = valid ? help : $"Enter a number from {min} to {max}.";
                if (valid) { updating = true; slider.Value = value; updating = false; }
                save.IsEnabled = !validInputs.ContainsValue(false);
            };
            panel.Children.Add(label); panel.Children.Add(slider); panel.Children.Add(number);
            panel.Children.Add(new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, FontSize = 11 });
            fields.Add((slider, number, get, set));
        }
        Add("Playfulness", "Higher values favor playful decisions.", 0, 1, p => p.Playfulness, (p,v) => p.Playfulness=v);
        Add("Curiosity", "Higher values favor following and looking around.", 0, 1, p => p.Curiosity, (p,v) => p.Curiosity=v);
        Add("Sleepiness", "Changes random sleep preference, not the fixed sleep duration.", 0, 1, p => p.Sleepiness, (p,v) => p.Sleepiness=v);
        Add("Attention delay (seconds)", "Time before AI may seek attention; personality adjusts this delay.", 10, 86400, p => p.AttentionAfterSeconds, (p,v) => p.AttentionAfterSeconds=v);
        Add("Walking energy / second", "Energy spent while walking.", 0, 10, p => p.WalkingEnergyPerSecond, (p,v) => p.WalkingEnergyPerSecond=v);
        Add("Playing energy / second", "Energy spent jumping.", 0, 10, p => p.PlayingEnergyPerSecond, (p,v) => p.PlayingEnergyPerSecond=v);
        Add("Sleep energy / second", "Energy restored during sleep.", 0, 20, p => p.SleepEnergyPerSecond, (p,v) => p.SleepEnergyPerSecond=v);
        Add("Rest energy / second", "Energy restored while resting.", 0, 5, p => p.RestEnergyPerSecond, (p,v) => p.RestEnergyPerSecond=v);
        var defaults = new Button { Content = "Restore defaults from personality.json", Margin = new Thickness(0,12,0,4) };
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
        panel.Children.Add(defaults); panel.Children.Add(save);
        behaviorSettingsWindow = new Window { Title = "Behavior tuning", Icon = AppIcon.WindowImage(), Width = 430,
            Height = Math.Min(720, SystemParameters.WorkArea.Height), Owner = settingsWindow,
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            Topmost = true, ShowInTaskbar = false };
        behaviorSettingsWindow.Closed += (_, _) => behaviorSettingsWindow = null;
        behaviorSettingsWindow.Show();
    }
}
