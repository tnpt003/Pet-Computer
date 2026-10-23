using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DesktopPet;

// First run after the installer (Velopack's OnFirstRun; a copied folder never sees it): a short
// welcome with a few tips, the language (switching it rewrites the welcome at once) and "Start with
// Windows", ticked. The choice is written to the Run key for this installed copy, which also moves an
// entry left by an older copied folder over to it.
public static class PetWelcome
{
    // A new user (no preferences file yet) starts in Windows' language; a returning one keeps theirs.
    public static string DefaultLanguage(bool newUser, string current) =>
        !newUser ? current : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            PetLanguage.Vietnamese => PetLanguage.Vietnamese,
            PetLanguage.Japanese => PetLanguage.Japanese,
            _ => PetLanguage.English
        };

    // The language id picked in the box (its Tag holds the ids, in order).
    public static string Chosen(ComboBox box) => ((string[])box.Tag)[Math.Max(0, box.SelectedIndex)];

    public static Window Create(string language, out CheckBox autostart, out ComboBox languageBox)
    {
        var languages = PetLanguage.Choices(language);
        var choose = new ComboBox { ItemsSource = languages.Select(PetLanguage.DisplayName).ToArray(), SelectedIndex = Array.IndexOf(languages, language), Tag = languages, MinWidth = 150 };
        var row = new DockPanel();
        var label = new TextBlock { Text = "Language / Ngôn ngữ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(label, Dock.Left);
        row.Children.Add(label); row.Children.Add(choose);
        var check = new CheckBox { IsChecked = true, Margin = new Thickness(0, 10, 0, 0) };
        var extra = new StackPanel();
        extra.Children.Add(row); extra.Children.Add(check);
        var window = PetDialog.Create(null, "Desktop Pet", "", "", extra: extra);
        var message = (TextBlock)((Panel)extra.Parent).Children[0];
        var start = Buttons(window).Single();
        void Apply()
        {
            Func<string, string, string> L = (vi, en) => PetText.Pick(Chosen(choose), vi, en);
            message.Text = L("Chào mừng! Mèo đã ở trên màn hình của bạn.\n" +
                             "• Kéo mèo để bế lên, thả hoặc vung để ném.\n" +
                             "• Chuột phải vào mèo: chơi, cho ăn, cảm xúc, Làm ngay.\n" +
                             "• Biểu tượng mèo ở khay (góc phải dưới): Cài đặt, Tạm dừng, Kiểm tra cập nhật.",
                             "Welcome! The pet is on your screen.\n" +
                             "• Drag the pet to pick it up; let go or swing to throw.\n" +
                             "• Right-click the pet: play, feed, emotions, Do now.\n" +
                             "• The pet's tray icon (bottom right): Settings, Pause, Check for updates.");
            check.Content = L("Khởi động cùng Windows", "Start with Windows");
            start.Content = L("Bắt đầu", "Let's go");
        }
        Apply();
        choose.SelectionChanged += (_, _) => Apply();
        autostart = check; languageBox = choose;
        return window;
    }

    private static System.Collections.Generic.IEnumerable<Button> Buttons(DependencyObject node) =>
        node is Button button ? new[] { button } : LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().SelectMany(Buttons);

    // Closing the window keeps the shown choices too: they were in plain sight.
    public static void Show(PetWindow pet, bool newUser, StartupRegistration startup)
    {
        var window = Create(DefaultLanguage(newUser, pet.UiLanguage), out var autostart, out var language);
        window.ShowDialog();
        pet.SetLanguage(Chosen(language));
        if (Environment.ProcessPath is not string exe) return;
        bool on = autostart.IsChecked == true;
        if (!startup.SetEnabled(on, exe)) System.Diagnostics.Trace.WriteLine("Welcome: Windows startup could not be changed.");
        else System.Diagnostics.Trace.WriteLine($"Welcome: start with Windows {(on ? "on" : "off")}.");
    }
}
