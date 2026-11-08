using DesktopPet;

// The weather's decisions (1.5.1 M2): which kind a wallpaper's colors mean, and how it changes.
internal static class WeatherTests
{
    private static byte[] Picture(params (int Share, byte R, byte G, byte B)[] parts)
    {
        var bytes = new List<byte>();
        foreach (var (share, r, g, b) in parts)
            for (int i = 0; i < share; i++) bytes.AddRange(new[] { b, g, r, (byte)255 });
        return bytes.ToArray();
    }

    public static void Run(Action<bool, string> check)
    {
        void Color(byte r, byte g, byte b, WeatherKind kind, string name) =>
            check(PetWeather.ClassifyColor(r, g, b) == kind, $"{name} means {kind} ({PetWeather.ClassifyColor(r, g, b)})");
        Color(0x4F, 0x9D, 0xE8, WeatherKind.Sunny, "Bright blue");
        Color(0x1F, 0x5F, 0xC0, WeatherKind.Sunny, "Strong blue");
        Color(0x80, 0x80, 0x84, WeatherKind.Rain, "Gray");
        Color(0x70, 0x7C, 0x88, WeatherKind.Rain, "Blue-gray");
        Color(0xF8, 0xF8, 0xF8, WeatherKind.Snow, "White");
        Color(0xD0, 0xE4, 0xF4, WeatherKind.Snow, "Cold pale blue");
        Color(0xF5, 0xEB, 0xDD, WeatherKind.Snow, "Cream (white, not gray)");
        Color(0xF0, 0x8A, 0x3C, WeatherKind.Sunset, "Orange");
        Color(0xEC, 0x7F, 0xA8, WeatherKind.Sunset, "Pink");
        Color(0x4C, 0xA8, 0x50, WeatherKind.Pollen, "Green");
        Color(0x08, 0x0A, 0x14, WeatherKind.Night, "Near black");
        Color(0x12, 0x20, 0x58, WeatherKind.Night, "Deep navy");

        check(PetWeather.Classify(Picture((50, 0xF0, 0x8A, 0x3C), (50, 0x4F, 0x9D, 0xE8))) is WeatherKind.Sunset or WeatherKind.Sunny,
            "Half orange, half blue is one of those two, not the gray of its average");
        check(PetWeather.Classify(Picture((70, 0x4C, 0xA8, 0x50), (30, 0xF8, 0xF8, 0xF8))) == WeatherKind.Pollen, "The kind covering the most wins");
        check(PetWeather.Classify(Picture((50, 0x4C, 0xA8, 0x50), (55, 0xF8, 0xF8, 0xF8))) == WeatherKind.Pollen, "Green hills under a white sky are green hills");
        var seeThrough = Picture((10, 0x08, 0x0A, 0x14), (5, 0x4C, 0xA8, 0x50));
        for (int i = 3; i < 40; i += 4) seeThrough[i] = 0;
        check(PetWeather.Classify(seeThrough) == WeatherKind.Pollen, "Transparent pixels do not count");
        check(PetWeather.Classify(ReadOnlySpan<byte>.Empty) == WeatherKind.Sunny, "An empty picture is clear sky");

        var random = new Random(11);
        var seen = new HashSet<string>();
        for (int i = 0; i < 400; i++)
        {
            double value = PetWeather.NextIntensity(random);
            if (value < 0 || value > 1) check(false, $"A strength is between 0 and 1 ({value})");
            seen.Add(value == 0 ? "lull" : value < 0.35 ? "light" : value < 0.75 ? "moderate" : "heavy");
        }
        check(seen.SetEquals(new[] { "lull", "light", "moderate", "heavy" }), "It rains hard, softly, moderately, and stops for a while");

        var weather = new PetWeather();
        const double Frame = 1.0 / 60;
        void Run(PetWeather w, double seconds, bool show) { for (double t = 0; t < seconds; t += Frame) w.Step(Frame, show, random); }

        check(!weather.Visible, "It starts hidden");
        weather.SetKind(WeatherKind.Rain);
        check(weather.Kind == WeatherKind.Rain, "Hidden weather changes kind at once");
        Run(weather, PetWeather.FadeSeconds + 0.1, true);
        check(weather.Visibility == 1, "It fades in");

        double time = weather.Time, visibility = weather.Visibility, strength = weather.Intensity, spell = weather.SpellRemaining;
        weather.Step(0, true, random);
        weather.Step(0, false, random);
        check(weather.Time == time && weather.Visibility == visibility && weather.Intensity == strength && weather.SpellRemaining == spell, "Pause holds it completely still");

        weather.SetKind(WeatherKind.Snow);
        check(weather.Kind == WeatherKind.Rain, "A showing weather keeps its kind until it has faded");
        double low = 1;
        for (double t = 0; t < PetWeather.FadeSeconds + 0.1 && weather.Kind == WeatherKind.Rain; t += Frame) { weather.Step(Frame, true, random); low = Math.Min(low, weather.Visibility); }
        check(weather.Kind == WeatherKind.Snow && low == 0, "It fades out and changes");
        Run(weather, PetWeather.FadeSeconds + 0.1, true);
        check(weather.Visibility == 1, "...and comes back as the new kind");

        weather.SetKind(WeatherKind.Pollen);
        weather.SetKind(WeatherKind.Snow);
        Run(weather, 0.5, true);
        check(weather.Kind == WeatherKind.Snow && weather.Visibility == 1, "Going back to the same wallpaper cancels the change");

        Run(weather, PetWeather.FadeSeconds + 0.1, false);
        check(!weather.Visible, "It fades out when it may not show");
        Run(weather, 0.2, true);
        weather.HideNow();
        check(!weather.Visible, "It can vanish at once");

        var lively = new PetWeather();
        double least = 1, most = 0;
        for (double t = 0; t < 3600; t += 0.1)
        {
            lively.Step(0.1, true, random);
            least = Math.Min(least, lively.Intensity); most = Math.Max(most, lively.Intensity);
        }
        check(least < 0.2 && most > 0.75, $"Over an hour the strength wanders ({least:F2} to {most:F2})");
        check(lively.Kind == WeatherKind.Sunny, "The strength never changes the kind");

        double before = new PetWeather().Intensity;
        var gentle = new PetWeather();
        gentle.Step(1.0 / 60, true, random);
        check(Math.Abs(gentle.Intensity - before) < 0.01, "It never jumps from one frame to the next");

        check(new PetPreferences().Weather, "Weather is on by default");
        foreach (var block in new[] { AutoBlock.Quiet, AutoBlock.Fullscreen, AutoBlock.Battle, AutoBlock.BattleScreens })
            check(!PetAutonomy.MayAct(AutoKind.Weather, block), $"No weather with {block}");
        foreach (var block in new[] { AutoBlock.Unsettled, AutoBlock.Held, AutoBlock.Paused, AutoBlock.Away })
            check(PetAutonomy.MayAct(AutoKind.Weather, block), $"{block} does not stop the weather");
    }
}
