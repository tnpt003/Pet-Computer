using System;
using System.Windows;
using Microsoft.Win32;

namespace DesktopPet;

// The weather around the cat (1.5.1 M2): the wallpaper's colors say which kind, this file says when
// it shows and keeps its click-through overlay around the cat. It never touches the cat: a sleeping
// cat stays asleep under the rain. See docs/APPROVED-EXCEPTIONS.md.
public sealed partial class PetWindow
{
    private const double WallpaperCheckSeconds = 60, WallpaperChangedDelay = 1, WeatherFrameSeconds = 1.0 / 30;

    private readonly PetWeather weather = new();
    // PET_WEATHER_PROBE=Rain: that weather whatever the wallpaper, and frame numbers in the log (for measuring).
    private static readonly WeatherKind? forcedWeather = Enum.TryParse<WeatherKind>(Environment.GetEnvironmentVariable("PET_WEATHER_PROBE"), true, out var kind) ? kind : null;
    // Replaceable for UI checks, so they never depend on this computer's wallpaper.
    private Func<WeatherKind>? wallpaperProbe = forcedWeather is WeatherKind forced ? () => forced : null;
    private Window? weatherWindow;
    private PetWeatherArt? weatherArt;
    private Area weatherBounds, wallpaperBounds;
    private double weatherScale, wallpaperCheckDue, weatherFrameDue;
    private bool weatherKnown, wallpaperReading, wallpaperHooked;
    private volatile bool wallpaperChanged;
    private string? wallpaperKey;

    public bool WeatherShowing => weatherWindow != null;
    public WeatherKind WeatherKindNow => weather.Kind;
    public double WeatherTime => weather.Time;

    // Every frame; dt is 0 in Pause, which holds the weather still.
    private void TickWeather(double dt)
    {
        bool allowed = preferences.Weather && MayAct(AutoKind.Weather);
        if (allowed && dt > 0) CheckWallpaper(dt);
        // A fullscreen app or a battle must not see it fade; Pause has no time to fade in.
        if (context.Fullscreen || BattleOwnsPet || (!allowed && dt <= 0)) weather.HideNow();
        weather.Step(dt, allowed && weatherKnown, featureRandom);
        if (!weather.Visible) { CloseWeather(); return; }
        if (weatherWindow != null && (bounds != weatherBounds || scale != weatherScale)) CloseWeather(); // Measured for another monitor.
        if (weatherWindow == null) ShowWeather();
        UpdateWeather(dt);
    }

    private void CheckWallpaper(double dt)
    {
        if (!wallpaperHooked)
        {
            wallpaperHooked = true;
            SystemEvents.UserPreferenceChanged += WallpaperPreferenceChanged;
            Closed += (_, _) => SystemEvents.UserPreferenceChanged -= WallpaperPreferenceChanged;
        }
        if (wallpaperChanged) { wallpaperChanged = false; wallpaperCheckDue = WallpaperChangedDelay; } // Windows is still writing it.
        if (bounds != wallpaperBounds) { wallpaperBounds = bounds; wallpaperCheckDue = 0; } // The cat changed monitor.
        wallpaperCheckDue -= dt;
        if (wallpaperCheckDue > 0 || wallpaperReading) return;
        wallpaperCheckDue = WallpaperCheckSeconds;
        if (wallpaperProbe != null) { ApplyWallpaper(null, wallpaperProbe()); return; }
        var monitor = CurrentMonitor().Bounds;
        wallpaperReading = true;
        PetWallpaper.Sample(new System.Drawing.Point(monitor.Left + monitor.Width / 2, monitor.Top + monitor.Height / 2), wallpaperKey,
            (key, kind) => Dispatcher.InvokeAsync(() => { wallpaperReading = false; ApplyWallpaper(key, kind); }));
    }

    private void WallpaperPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.Desktop) wallpaperChanged = true;
    }

    private void ApplyWallpaper(string? key, WeatherKind? kind)
    {
        wallpaperKey = key;
        if (kind is WeatherKind known) weather.SetKind(known);
        weatherKnown = true;
    }

    private void ShowWeather()
    {
        var region = PetWeatherArt.RegionFor(settings.Width);
        var art = new PetWeatherArt(region.Width, region.Height);
        weatherArt = art;
        weatherWindow = CreateOverlayWindow(art, "Pet weather", clickThrough: true);
        weatherWindow.Width = region.Width; weatherWindow.Height = region.Height;
        weatherWindow.Closed += (_, _) => { weatherWindow = null; weatherArt = null; };
        weatherBounds = bounds; weatherScale = scale;
        weatherFrameDue = 0;
        weatherWindow.Show();
    }

    private void UpdateWeather(double dt)
    {
        if (weatherWindow == null || weatherArt == null) return;
        double width = weatherArt.Width, height = weatherArt.Height;
        double catX = pet.X + settings.Width / 2, catY = pet.Y + settings.Height / 2;
        double left = Math.Clamp(catX - width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width));
        double top = Math.Clamp(catY - height * 0.6, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - height));
        bool moved = left != weatherArt.OriginX || top != weatherArt.OriginY;
        weatherFrameDue -= dt;
        if (dt > 0 ? weatherFrameDue > 0 : !moved) return;
        weatherFrameDue = WeatherFrameSeconds * 0.85;
        MoveWindow(weatherWindow, left, top);
        weatherArt.Kind = weather.Kind;
        weatherArt.Intensity = weather.Intensity;
        weatherArt.Time = weather.Time;
        weatherArt.OriginX = left; weatherArt.OriginY = top;
        weatherArt.CatX = catX - left; weatherArt.CatY = catY - top;
        weatherArt.Opacity = weather.Visibility;
        weatherArt.InvalidateVisual();
    }

    private void CloseWeather() => weatherWindow?.Close();
}
