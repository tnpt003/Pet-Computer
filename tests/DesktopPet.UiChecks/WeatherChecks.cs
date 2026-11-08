using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using static PetPrivate;

// The weather in the real pet window (1.5.1 M2): when it shows, what it covers, that it leaves the
// cat and the mouse alone. Sleep, drag, Quiet, fullscreen, Pause rules stay as approved.
internal static class WeatherChecks
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));
    private static Window? Overlay() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet weather" && w.IsLoaded);

    public static void Run(PetWindow window, PetModel pet, PetPreferences prefs, Action<bool, string> check)
    {
        void Tick(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickWeather", 1.0 / 60); }
        var showing = WeatherKind.Rain;
        bool quiet = prefs.Quiet;
        prefs.Quiet = false;
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        window.Reset();
        SetField(window, "wallpaperProbe", new Func<WeatherKind>(() => showing));
        SetField(window, "wallpaperCheckDue", 0.0);
        try
        {
            pet.Grounded = true; pet.Action = PetAction.Idle; pet.LedgeId = 0;
            Tick(0.1);
            check(!window.WeatherShowing && Overlay() == null, "With weather turned off nothing appears");

            prefs.Weather = true;
            Tick(0.05);
            check(window.WeatherShowing && Overlay() != null, "Turned on, it appears");
            check(window.WeatherKindNow == WeatherKind.Rain, "It shows what the wallpaper means");
            Tick(PetWeather.FadeSeconds);

            var overlay = Overlay()!;
            var bounds = Field<Area>(window, "bounds");
            check(overlay.ActualWidth <= 520 && overlay.ActualHeight <= 520 && overlay.ActualWidth < bounds.Right - bounds.Left,
                $"It covers a region around the cat, not the screen ({overlay.ActualWidth:F0}x{overlay.ActualHeight:F0})");
            double catMid = pet.X + 56;
            check(overlay.Left <= catMid && overlay.Left + overlay.ActualWidth >= catMid, "The region surrounds the cat");
            check(overlay.Left >= bounds.Left - 1 && overlay.Left + overlay.ActualWidth <= bounds.Right + 1 && overlay.Top + overlay.ActualHeight <= bounds.Bottom + 1, "It stays inside the work area");
            long style = GetWindowLongPtr(new WindowInteropHelper(overlay).Handle, -20).ToInt64();
            check((style & 0x20) != 0 && (style & 0x08000000) != 0 && (style & 0x80) != 0, "Clicks pass through it; it takes no focus and has no taskbar button");
            check(overlay.Topmost && !overlay.ShowInTaskbar && !overlay.ShowActivated, "An overlay like the others");

            double walkedFrom = overlay.Left;
            pet.X += 60;
            Tick(0.1);
            check(Math.Abs(overlay.Left - walkedFrom - 60) < 1 || overlay.Left + overlay.ActualWidth >= bounds.Right - 1, "It follows the cat");
            pet.X -= 60;

            // Pause: the weather stands still and the cat, if held, still has it around.
            double time = window.WeatherTime;
            for (int i = 0; i < 10; i++) Call(window, "TickWeather", 0.0);
            check(window.WeatherTime == time && window.WeatherShowing, "Pause holds it still");
            double heldFrom = overlay.Left;
            pet.X += 40;
            Call(window, "TickWeather", 0.0);
            check(Math.Abs(overlay.Left - heldFrom - 40) < 1 || overlay.Left + overlay.ActualWidth >= bounds.Right - 1, "A cat dragged in Pause keeps its weather around it");
            pet.X -= 40;
            Tick(0.1);

            pet.Emotions.Reset(); pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
            Tick(1);
            check(pet.Emotions.SleepLocked && pet.Action == PetAction.Sleeping, "Weather does not wake the cat");
            check(window.WeatherShowing, "...and goes on over the sleeping cat");
            pet.Emotions.Reset(); pet.Action = PetAction.Idle;

            double vx = pet.VX; var action = pet.Action; var emotion = pet.Emotions.Current; double x = pet.X, y = pet.Y;
            Tick(2);
            check(pet.VX == vx && pet.Action == action && pet.Emotions.Current == emotion && pet.X == x && pet.Y == y, "It never touches the cat");

            showing = WeatherKind.Snow;
            SetField(window, "wallpaperCheckDue", 0.0);
            Tick(0.05);
            check(window.WeatherKindNow == WeatherKind.Rain, "A new wallpaper first fades the old weather");
            Tick(PetWeather.FadeSeconds + 0.2);
            check(window.WeatherKindNow == WeatherKind.Snow && window.WeatherShowing, "...then shows the new one");

            var saved = Field<Area>(window, "bounds");
            var first = Overlay();
            SetField(window, "bounds", saved with { Right = saved.Right - 20 });
            Tick(0.05);
            check(Overlay() != null && !ReferenceEquals(first, Overlay()), "A work area that changed rebuilds it in place");
            SetField(window, "bounds", saved);
            Tick(0.05);

            prefs.Quiet = true;
            Tick(PetWeather.FadeSeconds + 0.2);
            check(!window.WeatherShowing && Overlay() == null, "Quiet hides it");
            prefs.Quiet = false;
            Tick(0.1);
            check(window.WeatherShowing, "It comes back after Quiet");

            SetContext(window, true, 0); Call(window, "TickContext", 1.1);
            Tick(0.02);
            check(!window.WeatherShowing && Overlay() == null, "A fullscreen app hides it at once");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            Tick(0.1);
            check(window.WeatherShowing, "It comes back after the fullscreen app");

            check(window.OpenBattleHub() == null, "Opening the battle hub");
            Tick(0.02);
            check(!window.WeatherShowing && Overlay() == null, "The battle screens hide it at once");
            Call(window, "CloseBattleHub");
            Tick(0.1);
            check(window.WeatherShowing, "It comes back after the battle screens");

            prefs.Weather = false;
            Tick(PetWeather.FadeSeconds + 0.2);
            check(!window.WeatherShowing && Overlay() == null, "The setting turns it off");

            prefs.Weather = true;
            Tick(0.1);
            prefs.Quiet = true;
            Call(window, "TickWeather", 0.0);
            check(!window.WeatherShowing, "Quiet chosen during Pause still hides it");
            prefs.Quiet = false;
        }
        finally
        {
            Call(window, "CloseWeather");
            prefs.Weather = false;
            prefs.Quiet = quiet;
            SetField(window, "wallpaperProbe", null);
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            window.Reset();
        }
        Reading(check);
        Art(check);
    }

    // The real reading path, on this computer's own wallpaper and on small pictures made here.
    private static void Reading(Action<bool, string> check)
    {
        WeatherKind FromFile(Color color)
        {
            string file = Path.Combine(Path.GetTempPath(), $"pet-weather-{Guid.NewGuid():N}.png");
            try
            {
                var pixels = new byte[64 * 48 * 4];
                for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = color.B; pixels[i + 1] = color.G; pixels[i + 2] = color.R; pixels[i + 3] = 255; }
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(64, 48, 96, 96, PixelFormats.Bgra32, null, pixels, 64 * 4)));
                using (var stream = File.Create(file)) encoder.Save(stream);
                return PetWallpaper.ClassifyFile(file);
            }
            finally { File.Delete(file); }
        }
        check(FromFile(Color.FromRgb(0xF0, 0x8A, 0x3C)) == WeatherKind.Sunset, "An orange picture file reads as a sunset");
        check(FromFile(Color.FromRgb(0x80, 0x80, 0x84)) == WeatherKind.Rain, "A gray picture file reads as rain");

        var done = new ManualResetEventSlim();
        string? key = null; WeatherKind? kind = null;
        var center = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        PetWallpaper.Sample(new System.Drawing.Point(center.Left + center.Width / 2, center.Top + center.Height / 2), null, (k, w) => { key = k; kind = w; done.Set(); });
        check(done.Wait(8000) && kind != null, $"This computer's wallpaper answers without an error ({kind}, {(string.IsNullOrEmpty(key) ? "no picture found" : key[0] == '#' ? "solid color" : "picture")})");

        var again = new ManualResetEventSlim();
        WeatherKind? repeat = WeatherKind.Rain;
        PetWallpaper.Sample(new System.Drawing.Point(center.Left + center.Width / 2, center.Top + center.Height / 2), key, (_, w) => { repeat = w; again.Set(); });
        check(again.Wait(8000) && (string.IsNullOrEmpty(key) || repeat == null), "An unchanged wallpaper is not decoded again");
    }

    // Pictures of every kind on the wallpapers that bring it, and on the opposite one.
    private static void Art(Action<bool, string> check)
    {
        var region = PetWeatherArt.RegionFor(112);
        check(region.Width <= 520 && region.Width >= 320, $"The region is about three cats wide ({region.Width:F0})");
        int width = (int)region.Width, height = (int)region.Height;
        string Render(WeatherKind kind, double strength, double time, Color back, string? name, double originX = 0)
        {
            var art = new PetWeatherArt(region.Width, region.Height) { Kind = kind, Intensity = strength, Time = time, OriginX = originX };
            art.Measure(region);
            art.Arrange(new Rect(region));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(region));
                dc.DrawRectangle(new VisualBrush(art), null, new Rect(region));
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            if (name != null && Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review)
            {
                Directory.CreateDirectory(review);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(review, name + ".png")); encoder.Save(stream);
            }
            var pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            return Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(pixels));
        }
        var own = new Dictionary<WeatherKind, Color>
        {
            [WeatherKind.Sunny] = Color.FromRgb(0x4F, 0x9D, 0xE8), [WeatherKind.Rain] = Color.FromRgb(0x78, 0x7C, 0x84),
            [WeatherKind.Snow] = Color.FromRgb(0xE8, 0xF0, 0xF8), [WeatherKind.Sunset] = Color.FromRgb(0xE8, 0x90, 0x60),
            [WeatherKind.Pollen] = Color.FromRgb(0x5C, 0xA0, 0x58), [WeatherKind.Night] = Color.FromRgb(0x12, 0x18, 0x34),
        };
        var plain = Color.FromRgb(0x40, 0x40, 0x48);
        foreach (var (kind, back) in own)
        {
            string heavy = Render(kind, 1, 3.3, back, $"weather-{kind}-heavy");
            Render(kind, 0.3, 3.3, back, $"weather-{kind}-light");
            check(heavy != Render(kind, 1, 4.1, back, null), $"{kind} moves over time");
            check(heavy != Render(kind, 1, 3.3, back, null, 37), $"{kind} stays put on the screen when the region moves");
        }
        string Empty(Color back) => Render(WeatherKind.Rain, 0, 3.3, back, null);
        var nothing = new DrawingVisual();
        using (var dc = nothing.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(plain), null, new Rect(region));
        var blank = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        blank.Render(nothing);
        var bytes = new byte[width * height * 4];
        blank.CopyPixels(bytes, width * 4, 0);
        check(Empty(plain) == Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(bytes)), "In a lull no rain falls at all");
        check(Render(WeatherKind.Rain, 1, 3.3, plain, null) != Render(WeatherKind.Rain, 0.2, 3.3, plain, null), "Heavy rain is not light rain");
    }
}
