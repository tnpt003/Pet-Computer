using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using static PetPrivate;

// The magic portal in the real pet window (1.5.1 M1): when one may appear by itself, what ends the
// spell and what closes a portal. Sleep, drag, Quiet, fullscreen, Pause and AI rules stay as approved.
internal static class PortalChecks
{
    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));
    private static int Count() => Application.Current.Windows.OfType<Window>().Count(w => w.Title == "Pet portal" && w.IsLoaded);

    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        void Tick(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickPortal", 1.0 / 60); }
        void Close() { Call(window, "ClosePortal"); Tick(0.02); }
        bool quiet = prefs.Quiet;
        prefs.Quiet = false;
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        window.Reset();
        try
        {
            pet.Grounded = true; pet.Action = PetAction.Idle; pet.LedgeId = 0;
            check(window.TryPortalEvent() && window.PortalOpen && Count() == 1, "A portal appears by itself when the cat is free");
            pet.Emotions.Step(pet, 1.0 / 60);
            check(pet.Emotions.Current == PetEmotion.Surprised || pet.Emotions.Target == PetEmotion.Surprised, $"The cat is startled by it ({pet.Emotions.Current})");
            var at = Application.Current.Windows.OfType<Window>().First(w => w.Title == "Pet portal");
            double catMid = pet.X + 56, portalMid = at.Left + PetPortalArt.ArtWidth / 2;
            check(Math.Abs(portalMid - catMid) > 80 && Math.Abs(portalMid - catMid) < 140, $"It stands beside the cat ({portalMid - catMid:F0} DIPs)");
            check((portalMid < catMid) == (pet.Facing < 0), "The cat faces it");
            check(!window.TryPortalEvent() && Count() == 1, "Never two portals");
            check(!window.BattleActive && !window.BattleHubOpen, "Nothing opens until the user clicks");
            Tick(0.8);
            check(window.PortalPhaseNow == PortalPhase.Open, "It opens fully");

            pet.Emotions.Reset(); pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
            Tick(0.05);
            check(window.PortalPhaseNow == PortalPhase.Closing, "It closes when the cat falls asleep, and does not wake it");
            check(pet.Emotions.SleepLocked && pet.Action == PetAction.Sleeping, "The sleeping cat stays asleep");
            Tick(0.6);
            check(!window.PortalOpen && Count() == 0, "...and is gone");
            check(!window.TryPortalEvent() && !window.PortalOpen, "None appears while the cat sleeps");
            pet.Emotions.Reset(); pet.Action = PetAction.Idle;

            prefs.Quiet = true;
            check(!window.TryPortalEvent() && !window.PortalOpen, "None appears by itself in Quiet");
            check(window.CastPortal() == null && window.PortalCasting, "The spell the user asks for works in Quiet");
            pet.Action = PetAction.Idle; pet.CastRemaining = 0;
            prefs.Quiet = false;

            SetContext(window, false, 100000); Call(window, "TickContext", 1.1);
            check(!window.TryPortalEvent() && !window.PortalOpen, "None appears while the user is away");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            pet.Emotions.Reset(); behavior.Reset(); pet.Action = PetAction.Idle;

            // The spell: it holds the cat, then the portal comes; input or a change of state ends it.
            check(window.CastPortal() == null && pet.CastRemaining == PetModel.CastDuration, "The spell begins");
            Tick(PetModel.CastDuration / 2);
            check(window.PortalCasting && pet.Action == PetAction.Sitting && pet.VX == 0 && !window.PortalOpen, "The cat stands still and casts");
            Call(window, "CancelFeatureActivity");
            check(!window.PortalCasting, "A click on the cat (any cancel) ends the spell");
            Tick(PetModel.CastDuration);
            check(!window.PortalOpen, "A cancelled spell brings no portal");

            check(window.CastPortal() == null, "Casting again");
            pet.Action = PetAction.Thrown; pet.Grounded = false;
            Tick(0.05);
            check(!window.PortalCasting && !window.PortalOpen, "Being thrown ends the spell");
            pet.Grounded = true; pet.Action = PetAction.Idle;

            check(window.CastPortal() == null, "Casting once more");
            Tick(PetModel.CastDuration + 0.1);
            check(window.PortalOpen, "The spell ends with a portal");
            SetContext(window, true, 0); Call(window, "TickContext", 1.1);
            Tick(0.02);
            check(!window.PortalOpen && Count() == 0, "A fullscreen app closes it at once");
            check(window.CastPortal() is string busy && busy.Length > 0 && !window.PortalCasting, "No spell over a fullscreen app");
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            pet.Emotions.Reset(); pet.Action = PetAction.Idle;

            check(window.TryPortalEvent(), "One more portal");
            Tick(0.8);
            var saved = Field<Area>(window, "bounds");
            SetField(window, "bounds", saved with { Right = saved.Right - 20 });
            Tick(0.02);
            check(!window.PortalOpen, "A work area that changed closes it instead of leaving it out of place");
            SetField(window, "bounds", saved);

            check(window.TryPortalEvent() && window.OpenBattleHub() == null, "A portal, then the hub by another way");
            Tick(0.02);
            check(window.PortalPhaseNow == PortalPhase.Closing, "The battle screens close it");
            Tick(0.6);
            Call(window, "CloseBattleHub");
            check(!window.PortalOpen, "...gone");
        }
        finally
        {
            Close();
            prefs.Quiet = quiet;
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            pet.CastRemaining = 0;
            window.Reset();
        }
        Art(check);
    }

    // The art: pictures on a light and a dark wallpaper, and the cat's spell pose.
    private static void Art(Action<bool, string> check)
    {
        string Render(double amount, double time, Color back, string? name)
        {
            var art = new PetPortalArt { Amount = amount, Time = time };
            art.Measure(new Size(PetPortalArt.ArtWidth, PetPortalArt.ArtHeight));
            art.Arrange(new Rect(0, 0, PetPortalArt.ArtWidth, PetPortalArt.ArtHeight));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(0, 0, PetPortalArt.ArtWidth, PetPortalArt.ArtHeight));
                dc.DrawRectangle(new VisualBrush(art), null, new Rect(0, 0, PetPortalArt.ArtWidth, PetPortalArt.ArtHeight));
            }
            var bitmap = new RenderTargetBitmap((int)PetPortalArt.ArtWidth, (int)PetPortalArt.ArtHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            if (name != null && Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string review)
            {
                Directory.CreateDirectory(review);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(review, name + ".png")); encoder.Save(stream);
            }
            var pixels = new byte[(int)PetPortalArt.ArtWidth * (int)PetPortalArt.ArtHeight * 4];
            bitmap.CopyPixels(pixels, (int)PetPortalArt.ArtWidth * 4, 0);
            return Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(pixels));
        }
        var light = Color.FromRgb(0xE8, 0xF0, 0xF8); var dark = Color.FromRgb(0x18, 0x20, 0x30);
        string opened = Render(1, 1.2, light, "portal-light");
        Render(1, 1.2, dark, "portal-dark");
        Render(0.45, 0.2, light, "portal-opening");
        check(opened != Render(1, 2.2, light, null), "The swirl turns over time");
        check(opened != Render(0.45, 1.2, light, null), "The opening amount grows the portal");
        check(Render(0, 1.2, light, null) != opened, "A closed portal draws nothing but its click area");
    }
}
