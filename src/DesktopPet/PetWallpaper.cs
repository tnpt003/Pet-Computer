using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DesktopPet;

// Which weather the wallpaper of the cat's monitor means (1.5.1 M2). Only a 32-pixel thumbnail is
// decoded, in memory, for its colors, and dropped at once: nothing is saved or sent anywhere.
// See docs/APPROVED-EXCEPTIONS.md.
public static class PetWallpaper
{
    private const int ThumbnailWidth = 32;
    private static string? lastProblem;

    // On a short-lived thread of its own (the shell object wants an STA, and a big picture must not
    // stall the cat). done runs on that thread with a key naming the picture, and the kind, or null
    // when the key equals known and nothing had to be decoded. A failure reports clear sky.
    public static void Sample(System.Drawing.Point monitorCenter, string? known, Action<string, WeatherKind?> done)
    {
        var thread = new Thread(() =>
        {
            string key = "";
            WeatherKind? kind = WeatherKind.Sunny;
            // Anything thrown on this thread would end the app, and the spec's answer is clear sky.
            try { (key, kind) = Read(monitorCenter, known); }
            catch (Exception e) { Report(e.GetType().Name); }
            Dispatcher.FromThread(Thread.CurrentThread)?.InvokeShutdown(); // WPF's imaging made one here.
            done(key, kind);
        }) { IsBackground = true, Name = "Wallpaper colors" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static (string Key, WeatherKind? Kind) Read(System.Drawing.Point center, string? known)
    {
        var (path, color) = Current(center);
        if (color is uint bgr)
        {
            string solid = $"#{bgr:X6}";
            return (solid, solid == known ? null : PetWeather.ClassifyColor((byte)bgr, (byte)(bgr >> 8), (byte)(bgr >> 16)));
        }
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) // Spotlight and the like: Windows' own copy.
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
        if (!File.Exists(path)) return ("", WeatherKind.Sunny);
        var info = new FileInfo(path);
        string key = $"{path}|{info.LastWriteTimeUtc.Ticks}|{info.Length}";
        return (key, key == known ? null : ClassifyFile(path));
    }

    public static WeatherKind ClassifyFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.DecodePixelWidth = ThumbnailWidth;
        image.StreamSource = stream;
        image.EndInit();
        var pixels = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int stride = pixels.PixelWidth * 4;
        var bytes = new byte[stride * pixels.PixelHeight];
        pixels.CopyPixels(bytes, stride, 0);
        return PetWeather.Classify(bytes);
    }

    // The picture on the monitor holding this pixel; no picture means a solid color (0x00BBGGRR).
    private static (string? Path, uint? Color) Current(System.Drawing.Point center)
    {
        var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
        try
        {
            string? monitor = null, first = null;
            uint count = wallpaper.GetMonitorDevicePathCount();
            for (uint i = 0; i < count && monitor == null; i++)
            {
                try
                {
                    string id = wallpaper.GetMonitorDevicePathAt(i);
                    first ??= id;
                    var rect = wallpaper.GetMonitorRECT(id);
                    if (center.X >= rect.Left && center.X < rect.Right && center.Y >= rect.Top && center.Y < rect.Bottom) monitor = id;
                }
                catch (COMException) { } // A monitor that is known but not attached has no rectangle.
            }
            string? path = wallpaper.GetWallpaper(monitor ?? first ?? "");
            return string.IsNullOrEmpty(path) ? (null, wallpaper.GetBackgroundColor()) : (path, null);
        }
        finally { Marshal.ReleaseComObject(wallpaper); }
    }

    // Once per different problem: this runs every minute and a broken API would fill the log.
    private static void Report(string problem)
    {
        if (problem == lastProblem) return;
        lastProblem = problem;
        Trace.WriteLine($"Wallpaper colors unavailable: {problem}. Weather stays clear.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorRect { public int Left, Top, Right, Bottom; }

    [ComImport, Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    private class DesktopWallpaperClass { }

    // Every slot in vtable order, used or not.
    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        MonitorRect GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(int position);
        int GetPosition();
        void SetSlideshow(IntPtr items);
        IntPtr GetSlideshow();
        void SetSlideshowOptions(int options, uint slideshowTick);
        void GetSlideshowOptions(out int options, out uint slideshowTick);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorId, int direction);
        int GetStatus();
        void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
    }
}
