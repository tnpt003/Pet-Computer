using System.IO;
using System.Windows;
using System.Windows.Media;
using DesktopPet;
using static PetPrivate;

// Mini-games M4 (docs/MINI-GAMES.md): visitors in the real pet window — the 40–90 minute timer and the
// 2 minute retry, one visit of each guest (the cat watches, follows on its own ground, pounces in place at the
// butterfly), clicks on the guests, a click or drag on the cat mid-visit, asleep, Pause, Quiet, focus time, a
// fullscreen app, a mini-game, an emotion, the switch, a window edge; and drawings to review.
internal static class GuestChecks
{
    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        var physics = Field<PetPhysics>(window, "physics");
        void Frame()
        {
            Call(window, "TickDesktopToys", 1 / 60.0); Call(window, "TickGuests", 1 / 60.0); Call(window, "TickFeatures", 1 / 60.0);
            bool owns = (bool)Call(window, "FeatureOwnsMovement")!;
            if (!owns) behavior.Step(pet, 1 / 60.0);
            physics.Step(pet, Field<Area>(window, "bounds"), 1 / 60.0, Field<List<Ledge>>(window, "ledges"), owns);
            pet.ActionTime += 1 / 60.0;
        }
        bool Until(Func<bool> done, int frames = 900) { for (int i = 0; i < frames && !done(); i++) Frame(); return done(); }
        void Free() { pet.Action = PetAction.Idle; pet.VX = 0; pet.Grounded = true; }
        var bounds = Field<Area>(window, "bounds");
        double scale = Field<double>(window, "scale");
        double middle = (bounds.Left + bounds.Right) / 2;
        var settings = Field<PetSettings>(window, "settings");
        double catHeight = settings.Height;
        SetField(window, "cursorProbe", new Func<Point>(() => new Point(middle * scale, (bounds.Top + 40) * scale)));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        preferences.Quiet = false; behavior.Quiet = false;
        preferences.Guests = true; int visitsBefore = preferences.GuestVisits; preferences.GuestVisits = 0;
        void Home(double? x = null) { pet.X = (x ?? middle) - PetSettings.CenterX; pet.Y = bounds.Bottom - catHeight; pet.LedgeId = 0; Free(); }
        double CatCenter() => pet.X + settings.Width / 2;
        GuestCat Cat() => new(CatCenter(), pet.Y + catHeight, pet.LedgeId == 0);
        void Clear() { Call(window, "CloseGuest"); Home(); }
        Home();

        // The timer: 40–90 minutes from the start; a blocked moment tries again two minutes later.
        SetField(window, "guestWait", double.NaN);
        Call(window, "TickGuests", 0.0);
        check(window.GuestWait is >= 40 * 60 and <= 90 * 60 && !window.GuestActive, "The first visitor is 40–90 minutes away");
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        SetField(window, "guestWait", 0.005);
        Call(window, "TickGuests", 1 / 60.0);
        check(!window.GuestActive && window.GuestWait == GuestRules.RetrySeconds && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked,
            "Asleep: no visitor, the cat sleeps on, and it tries again in 2 minutes");
        pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();
        SetField(window, "guestWait", 0.005);
        Call(window, "TickGuests", 1 / 60.0);
        check(window.GuestActive && window.GuestWindowShown && window.GuestWait >= 40 * 60 && preferences.GuestVisits == 1, "Free: a visitor comes, the next is 40–90 minutes away");
        Clear();

        // A butterfly, the cat following: it watches, walks at most ~150 DIPs on its floor, pounces once in place.
        double catY = pet.Y, startX = CatCenter();
        check(window.TryGuestVisit(GuestKind.Butterfly, follows: true) && window.GuestWatching && (bool)Call(window, "FeatureOwnsMovement")!,
            "A paper butterfly comes; the cat watches (nothing else moves the cat)");
        check(preferences.GuestVisits == 2 && !pet.Emotions.Suspended, "The visit is counted; emotions are not suspended");
        bool touched = false, chased = false, pounced = false, looked = false, stayed = true, noEmotion = true;
        double walked = 0, highest = 0;
        int frames = 0;
        bool shotNear = false, shotPounce = false;
        while (window.GuestActive && frames++ < 60 * 20)
        {
            Frame();
            if (!window.GuestActive) break;
            var g = window.GuestNow;
            touched |= g.Touches(g.X, g.Y, Cat());
            chased |= window.GuestChasing;
            pounced |= window.GuestPouncing;
            looked |= pet.Action == PetAction.Looking && pet.Facing == (g.X < CatCenter() ? -1 : 1);
            walked = Math.Max(walked, Math.Abs(CatCenter() - startX));
            highest = Math.Max(highest, window.GuestHop);
            stayed &= Math.Abs(pet.Y - catY) < 0.01 && pet.Grounded && pet.LedgeId == 0;
            noEmotion &= pet.Emotions.Current is not (PetEmotion.Surprised or PetEmotion.Dizzy);
            if (!shotNear && Math.Abs(g.X - CatCenter()) < 120) { Snapshot(window, "guest-butterfly", light: false); Snapshot(window, "guest-butterfly-light", light: true); shotNear = true; }
            if (!shotPounce && window.GuestPouncing && window.GuestHop > FireflyRules.HopHeight - 2) { Snapshot(window, pet.Facing > 0 ? "guest-pounce-right" : "guest-pounce-left", light: true); shotPounce = true; }
        }
        check(!window.GuestActive && !window.GuestWindowShown && frames / 60.0 is >= 6 and <= 17, $"The butterfly crosses and is gone in about 8–15 seconds ({frames / 60.0:F1} s)");
        check(!touched, "...never into the cat's body");
        check(looked && chased && walked <= GuestRules.ChaseDistance + 8, $"The cat turns to watch and follows a little ({walked:F0} DIPs)");
        check(pounced && highest > 20 && highest <= FireflyRules.HopHeight + 0.01 && stayed && window.GuestHop == 0, "...pounces once, a hop drawn in place: it lands where it stood");
        check(noEmotion && !window.GuestWatching && pet.Action is PetAction.Idle or PetAction.Walking or PetAction.Sitting, "No Surprised or Dizzy; then the cat is itself again");

        // A slime, the cat not following: it stops about 80 DIPs away, bounces hello, goes back; the cat stays.
        Home(middle);
        startX = CatCenter();
        check(window.TryGuestVisit(GuestKind.Slime, follows: false), "A gentle slime comes");
        bool greeted = false, slimeClear = true, catStill = true;
        double gap = double.NaN;
        bool shotGreet = false;
        frames = 0;
        while (window.GuestActive && frames++ < 60 * 25)
        {
            Frame();
            if (!window.GuestActive) break;
            var g = window.GuestNow;
            slimeClear &= !g.Touches(g.X, g.Y, Cat()) && Math.Abs(g.Y - bounds.Bottom) < 0.01;
            catStill &= Math.Abs(CatCenter() - startX) < 0.5;
            if (g.Phase == GuestPhase.Greeting)
            {
                greeted = true; gap = Math.Abs(g.X - CatCenter()) - GuestRules.CatHalfWidth - GuestRules.SlimeHalf;
                if (!shotGreet && g.PhaseTime > 0.3) { Snapshot(window, "guest-slime", light: false); Snapshot(window, "guest-slime-light", light: true); shotGreet = true; }
            }
        }
        check(greeted && Math.Abs(gap - GuestRules.SlimeStop) < 3 && slimeClear, $"The slime crawls on the floor, stops about 80 DIPs from the cat and bounces hello ({gap:F0})");
        check(catStill && !window.GuestActive, "Not following this time: the cat only watches; then the slime is gone");

        // Clicks on the guests: the butterfly zooms off, the slime bounces once.
        Home();
        window.TryGuestVisit(GuestKind.Butterfly, follows: false);
        for (int i = 0; i < 30; i++) Frame();
        check(window.ClickGuest() && window.GuestNow.Zooming, "A click sends the butterfly off with sparks");
        Frame(); Frame();
        Snapshot(window, "guest-butterfly-zoom", light: false);
        check(Until(() => !window.GuestActive, 60 * 12), "...and it is gone sooner");
        Home();
        window.TryGuestVisit(GuestKind.Slime, follows: false);
        for (int i = 0; i < 30; i++) Frame();
        check(window.ClickGuest() && window.GuestNow.Hop >= 0 && window.GuestActive, "A click makes the slime bounce, and it goes on");
        Clear();

        // A drag on the cat mid-visit: it stops watching, lifted as usual; the guest goes on its way.
        window.TryGuestVisit(GuestKind.Butterfly, follows: true);
        for (int i = 0; i < 20; i++) Frame();
        pet.Action = PetAction.Dragging; pet.Grounded = false;
        Frame();
        check(!window.GuestWatching && window.GuestActive && pet.Action == PetAction.Dragging && window.GuestHop == 0, "Picking up the cat: it stops watching, the guest goes on");
        Free(); Home(); Clear();

        // An emotion that owns the cat ends the watching (the visit itself never causes one).
        window.TryGuestVisit(GuestKind.Slime, follows: false);
        for (int i = 0; i < 10; i++) Frame();
        bool startled = pet.Emotions.Startle(); pet.Emotions.Step(pet, 0.1);
        Frame();
        check(startled && !window.GuestWatching && window.GuestActive, "A real emotion goes first: the cat stops watching, the guest goes on");
        pet.Emotions.Reset(); Free(); Clear();

        // Asleep mid-visit: the guest fades out at once; the cat is not woken.
        window.TryGuestVisit(GuestKind.Butterfly, follows: false);
        for (int i = 0; i < 20; i++) Frame();
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        for (int i = 0; i < 25; i++) Call(window, "TickGuests", 1 / 60.0);
        check(!window.GuestActive && !window.GuestWindowShown && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "The cat falls asleep: the guest fades out, the cat sleeps on");
        check(!window.TryGuestVisit(GuestKind.Slime), "No visitor while the cat sleeps");
        pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();

        // Pause: the guest holds still; nothing new comes.
        window.TryGuestVisit(GuestKind.Butterfly, follows: false);
        for (int i = 0; i < 20; i++) Frame();
        window.SetPaused(true);
        double frozenX = window.GuestNow.X;
        for (int i = 0; i < 30; i++) Call(window, "TickGuests", 0.0);
        check(window.GuestActive && window.GuestNow.X == frozenX, "Pause freezes the guest");
        window.SetPaused(false);
        Clear();
        window.SetPaused(true);
        check(!window.TryGuestVisit(GuestKind.Slime), "No visitor in Pause");
        window.SetPaused(false);

        // Quiet, focus time, a fullscreen app, a mini-game, the switch: no visitor.
        preferences.Quiet = true; behavior.Quiet = true;
        check(!window.TryGuestVisit(GuestKind.Slime), "No visitor in Quiet mode");
        preferences.Quiet = false; behavior.Quiet = false;
        check(window.StartFocus(25) && !window.TryGuestVisit(GuestKind.Slime), "No visitor in focus time");
        window.StopFocus(); Free();
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (true, 0)));
        Call(window, "TickContext", 1.1);
        check(!window.TryGuestVisit(GuestKind.Slime), "No visitor over a fullscreen app");
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (false, 0)));
        Call(window, "TickContext", 1.1);
        Free(); Home();
        window.TryGuestVisit(GuestKind.Butterfly, follows: false);
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (true, 0)));
        Call(window, "TickContext", 1.1);
        for (int i = 0; i < 25; i++) Call(window, "TickGuests", 1 / 60.0);
        check(!window.GuestActive, "A fullscreen app opening: the guest fades out at once");
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (false, 0)));
        Call(window, "TickContext", 1.1);
        SetField(window, "contextProbe", null);
        Free(); Home();
        check(window.ToggleCupGame() && !window.TryGuestVisit(GuestKind.Slime), "No visitor during a mini-game");
        window.ToggleCupGame(); Free(); Home();
        preferences.Guests = false;
        check(!window.TryGuestVisit(GuestKind.Slime), "Switched off: no visitor");
        preferences.Guests = true;
        window.TryGuestVisit(GuestKind.Slime, follows: false);
        preferences.Guests = false;
        for (int i = 0; i < 25; i++) Call(window, "TickGuests", 1 / 60.0);
        check(!window.GuestActive, "Switching it off sends a guest away at once");
        preferences.Guests = true;
        Free(); Home();

        // On a window edge: the cat follows only along that edge, never off it.
        var ledge = new Ledge(4242, bounds.Bottom - 300, middle - 110, middle + 110, middle - 110, middle + 110, bounds.Bottom - 100);
        SetField(window, "ledges", new List<Ledge> { ledge });
        pet.X = middle - PetSettings.CenterX; pet.Y = ledge.Top - catHeight; pet.LedgeId = ledge.Id; Free();
        double ledgeY = pet.Y;
        window.TryGuestVisit(GuestKind.Butterfly, follows: true);
        bool onEdge = true, edgeChase = false;
        for (int i = 0; i < 60 * 20 && window.GuestActive; i++)
        {
            Frame();
            edgeChase |= window.GuestChasing;
            onEdge &= pet.LedgeId == ledge.Id && Math.Abs(pet.Y - ledgeY) < 0.01 && CatCenter() - GuestRules.CatHalfWidth >= ledge.Left - 0.5 && CatCenter() + GuestRules.CatHalfWidth <= ledge.Right + 0.5;
            if (!window.GuestWatching) break;
        }
        check(edgeChase && onEdge, "On a window edge the cat follows along it and never leaves it");
        Clear();
        SetField(window, "ledges", new List<Ledge>());
        pet.LedgeId = 0; Home();

        // The diary line.
        check(preferences.GuestVisits > 0, "Visits are kept for the diary");

        preferences.GuestVisits = visitsBefore;
        SetField(window, "cursorProbe", null);
        SetField(window, "guestWait", double.NaN);
        window.Reset();
    }

    // PET_RENDER_REVIEW=<folder>: the cat's window and the guest's, on a light or dark desktop.
    private static void Snapshot(PetWindow window, string name, bool light)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not { Length: > 0 } folder) return;
        Call(window, "Present", false);
        var renderer = Field<PetRenderer>(window, "renderer");
        renderer.InvalidateVisual(); renderer.UpdateLayout();
        var art = Field<GuestArt?>(window, "guestArt");
        var host = Field<Window?>(window, "guestWindow");
        var pet = Field<PetModel>(window, "pet");
        double catLeft = pet.X - PickupAnimation.Padding, catTop = pet.Y - PickupAnimation.Padding;
        double artLeft = host?.Left ?? catLeft, artTop = host?.Top ?? catTop, artW = art?.Width ?? 0, artH = art?.Height ?? 0;
        double left = Math.Min(catLeft, artLeft) - 10, right = Math.Max(catLeft + renderer.ActualWidth, artLeft + artW) + 10;
        double top = Math.Min(catTop, artTop) - 10, bottom = Math.Max(catTop + renderer.ActualHeight, artTop + artH) + 10;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(light ? Color.FromRgb(0xE9, 0xEE, 0xF2) : Color.FromRgb(0x1F, 0x24, 0x2B)), null, new Rect(0, 0, right - left, bottom - top));
            if (art != null)
            {
                art.InvalidateVisual(); art.UpdateLayout();
                dc.DrawRectangle(new VisualBrush(art) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, artW, artH) }, null,
                    new Rect(artLeft - left, artTop - top, artW, artH));
            }
            dc.DrawRectangle(new VisualBrush(renderer) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, renderer.ActualWidth, renderer.ActualHeight) }, null,
                new Rect(catLeft - left, catTop - top, renderer.ActualWidth, renderer.ActualHeight));
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)((right - left) * 2), (int)((bottom - top) * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }
}
