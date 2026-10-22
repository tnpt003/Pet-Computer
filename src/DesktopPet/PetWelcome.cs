using System;
using System.Windows;
using System.Windows.Controls;

namespace DesktopPet;

// First run after the installer (Velopack's OnFirstRun; a copied folder never sees it): a short
// welcome with a few tips and "Start with Windows", ticked. The choice is written to the Run key
// for this installed copy, which also moves an entry left by an older copied folder over to it.
public static class PetWelcome
{
    public static Window Create(Func<string, string, string> L, out CheckBox autostart)
    {
        autostart = new CheckBox { Content = L("Khởi động cùng Windows", "Start with Windows"), IsChecked = true };
        return PetDialog.Create(null, "Desktop Pet",
            L("Chào mừng! Mèo đã ở trên màn hình của bạn.\n" +
              "• Kéo mèo để bế lên, thả hoặc vung để ném.\n" +
              "• Chuột phải vào mèo: chơi, cho ăn, cảm xúc, Làm ngay.\n" +
              "• Biểu tượng mèo ở khay (góc phải dưới): Cài đặt, Tạm dừng, Kiểm tra cập nhật.",
              "Welcome! The pet is on your screen.\n" +
              "• Drag the pet to pick it up; let go or swing to throw.\n" +
              "• Right-click the pet: play, feed, emotions, Do now.\n" +
              "• The pet's tray icon (bottom right): Settings, Pause, Check for updates."),
            L("Bắt đầu", "Let's go"), extra: autostart);
    }

    // Closing the window keeps the shown choice too: the checkbox was in plain sight.
    public static void Show(Func<string, string, string> L, StartupRegistration startup)
    {
        var window = Create(L, out var autostart);
        window.ShowDialog();
        if (Environment.ProcessPath is not string exe) return;
        bool on = autostart.IsChecked == true;
        if (!startup.SetEnabled(on, exe)) System.Diagnostics.Trace.WriteLine("Welcome: Windows startup could not be changed.");
        else System.Diagnostics.Trace.WriteLine($"Welcome: start with Windows {(on ? "on" : "off")}.");
    }
}
