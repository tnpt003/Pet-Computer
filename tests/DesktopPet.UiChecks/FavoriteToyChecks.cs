using System.IO;
using System.Windows;
using DesktopPet;
using DesktopPet.AI;
using static PetPrivate;

// The favourite toy (docs/LIVING-CORNER.md → M4) in the real pet window: the rounds counted, nothing new
// without Python, the visit, the invitation with the wand, the choice between two toys close together,
// chasing the favourite of two, and what stops it all.
internal static class FavoriteToyChecks
{
    private const string Ball = DesktopToyRules.Ball, Wand = DesktopToyRules.Wand, Mouse = DesktopToyRules.Mouse;

    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        void Toys() => Call(window, "TickDesktopToys", 1 / 60.0);
        void FeatureStep(double dt = 1 / 60.0) => Call(window, "TickFeatures", dt);
        void Frame()
        {
            Toys(); FeatureStep();
            if (!(bool)Call(window, "FeatureOwnsMovement")!) behavior.Step(pet, 1 / 60.0);
            if (pet.Action == PetAction.Walking) pet.X += pet.VX / 60.0; // The walk the physics step would make.
            pet.ActionTime += 1 / 60.0;
        }
        bool Until(Func<bool> done, int frames = 900) { for (int i = 0; i < frames && !done(); i++) Frame(); return done(); }
        void Settle(ToyBall motion) { for (int i = 0; i < 900 && !motion.AtRest; i++) Toys(); Toys(); }
        object Thing(string id) => Call(window, "DesktopToy", id)!;
        void SetThing(object thing, string field, object? value) => thing.GetType().GetField(field)!.SetValue(thing, value);
        void Favorite(string? id) => Call(window, "ApplyFavoriteDecision", new AiDecision("IDLE", 3, "content", FavoriteToy: id));
        void Python() => Call(window, "ApplyBondDecision", new AiDecision("IDLE", 3, "content", BondLevel: 2));
        void Free() { pet.Action = PetAction.Idle; pet.VX = 0; pet.Grounded = true; }
        var bounds = Field<Area>(window, "bounds");
        double scale = Field<double>(window, "scale");
        double middle = (bounds.Left + bounds.Right) / 2;
        double CatCentre() => pet.X + PetSettings.CenterX;
        Point cursor = new(middle - 300, bounds.Bottom - 150);
        SetField(window, "cursorProbe", new Func<Point>(() => new Point(cursor.X * scale, cursor.Y * scale)));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        preferences.Quiet = false; behavior.Quiet = false;
        window.ToyChaseStreak.Step(1000, false);
        preferences.ToysSniffed = new() { Ball, DesktopToyRules.Box, Wand, Mouse };
        window.ToggleToyBasket();
        Toys();
        check(window.TakeToyOut(Ball) && window.TakeToyOut(Wand) && window.TakeToyOut(Mouse), "The yarn ball, the wand and the mouse come out");
        Toys();
        var ball = window.ToyMotionOf(Ball)!; var wand = window.ToyMotionOf(Wand)!; var mouse = window.ToyMotionOf(Mouse)!;
        void Place(double ballX, double wandX, double mouseX)
        {
            ball.X = ballX; wand.X = wandX; mouse.X = mouseX;
            Settle(ball); Settle(wand); Settle(mouse);
        }
        Place(bounds.Right - 60, middle + 220, middle - 260);
        pet.X = middle - PetSettings.CenterX; Free();
        Call(window, "ForgetBond");
        var plays = window.ToyPlays;

        // Without Python: no favourite, the invitation brings the ball as before, no visit.
        check(window.FavoriteToy == null && !window.GoVisitFavorite(), "Without Python there is no favourite and no visit");
        check(window.InviteToPlay() && window.FetchingInvite && window.FavoritePhase == "None", "...and the invitation brings the yarn ball as in M3");
        Call(window, "CancelFeatureActivity"); Free();
        check(window.ToyPlays == plays, "The cat's own invitation is not a round the user played");

        // Python answers and likes the wand: a visit, a sniff or a paw, then sitting by it. The wand stays put.
        Python(); Favorite(Wand);
        double wandX = wand.X;
        check(window.FavoriteToy == Wand && window.GoVisitFavorite() && window.FavoritePhase == "Walk" && window.FavoriteTargetId == Wand, "Favourite wand: the cat walks to it");
        check(Until(() => window.FavoritePhase == "Greet") && pet.Action == PetAction.Sitting && pet.Facing == (wand.X < CatCentre() ? -1 : 1),
            "By the wand it sniffs or paws at it, facing it");
        check(Until(() => window.FavoritePhase == "Sit"), "...then sits by it");
        check(Until(() => window.FavoritePhase == "None", 60 * 7) && pet.Action == PetAction.Idle && wand.X == wandX, "...a while, then free; the wand never moved");

        // Quiet, asleep, a click: what stops it.
        pet.X = middle - PetSettings.CenterX; Free();
        preferences.Quiet = true; behavior.Quiet = true;
        check(!window.GoVisitFavorite(), "In Quiet there is no visit (the cat's own doing)");
        preferences.Quiet = false; behavior.Quiet = false;
        check(window.GoVisitFavorite(), "Out of Quiet it may go");
        preferences.Quiet = true; behavior.Quiet = true;
        Frame();
        check(window.FavoritePhase == "None", "Quiet coming on stops the trip");
        preferences.Quiet = false; behavior.Quiet = false;
        Free();
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        check(!window.GoVisitFavorite() && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "Asleep: it sleeps on");
        behavior.WakeByClick(pet); pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();
        check(window.GoVisitFavorite(), "Awake again: a visit");
        Frame();
        Call(window, "CancelFeatureActivity");
        check(window.FavoritePhase == "None", "A click or a drag on the cat ends it");
        Free();

        // An invitation with the wand: by it, looking at the user and patting it, for 20 seconds.
        pet.X = middle - PetSettings.CenterX;
        check(window.InviteToPlay() && window.FavoritePhase == "Walk" && !window.Fetching, "The invitation goes to the favourite wand, not the ball");
        check(Until(() => window.FavoritePhase == "Invite"), "By the wand it waits for the user");
        bool patted = false, looked = false, shot = false;
        for (int i = 0; i < 60 * 7 && window.FavoritePhase == "Invite"; i++)
        {
            Frame();
            if (!shot && pet.BatRemaining is > 0.1 and < 0.2) { shot = true; Snapshot(window, "favorite-pat"); }
            if (shot && looked == false && pet.Action == PetAction.Looking) Snapshot(window, "favorite-invite-look");
            patted |= pet.BatRemaining > 0;
            looked |= pet.Action == PetAction.Looking && pet.Facing == (cursor.X < CatCentre() ? -1 : 1);
        }
        check(patted && looked && wand.X == wandX, "It pats the wand and looks at the user; the wand does not move");
        check(Until(() => window.FavoritePhase == "None", 60 * 20) && window.ToyPlays == plays, "Nobody comes in 20 seconds: it goes; no round counted");

        // The user takes the wand while it waits: the chase of M4 starts as usual and is a wand round.
        Free(); pet.X = middle - PetSettings.CenterX;
        check(window.InviteToPlay() && Until(() => window.FavoritePhase == "Invite"), "Invited once more");
        var wandThing = Thing(Wand);
        Call(window, "StartToyDrag", wandThing); SetThing(wandThing, "GrabX", 0.0); SetThing(wandThing, "GrabY", 0.0);
        cursor = new Point(wand.X + 200, bounds.Bottom - 150);
        check(Until(() => window.ChaseToyId == Wand, 120) && window.FavoritePhase == "None", "The user swings the wand: the cat chases it");
        check(window.ToyPlays.Wand == plays.Wand + 1 && window.ToyPlays.Ball == plays.Ball, "...counted as one wand round");
        Call(window, "CancelFeatureActivity");
        Call(window, "EndToyDrag", wandThing, false);
        Settle(wand); Free();

        // Two toys close together: it stops between them, looks at each, then goes to the favourite.
        Place(bounds.Right - 60, middle + 220, middle + 380);
        Favorite(Mouse);
        pet.X = middle - 300 - PetSettings.CenterX; Free();
        check(window.GoVisitFavorite() && window.FavoritePhase == "WeighWalk", "The mouse lies 160 DIPs from the wand: first to the middle");
        check(Until(() => window.FavoritePhase == "Weigh") && Math.Abs(CatCentre() - (wand.X + mouse.X) / 2) < 12, "It stops between them");
        int facing = pet.Facing; bool turned = false;
        for (int i = 0; i < 150 && window.FavoritePhase == "Weigh"; i++)
        {
            Frame();
            if (i == 30) Snapshot(window, "favorite-weigh-1");
            if (i == 90) Snapshot(window, "favorite-weigh-2");
            turned |= pet.Facing != facing && pet.Action == PetAction.Looking;
        }
        check(turned && window.FavoritePhase == "Walk" && window.FavoriteTargetId == Mouse, "It looks at one, then the other, and picks the favourite mouse");
        Call(window, "CancelFeatureActivity"); Free();
        Favorite(null);
        pet.X = middle - 300 - PetSettings.CenterX;
        check(!window.GoVisitFavorite(), "No favourite: no visit");
        Place(middle + 200, middle + 330, middle - 400);
        check(window.InviteToPlay() && window.FavoritePhase == "WeighWalk", "No favourite, the ball by the wand: the invitation weighs them");
        check(Until(() => window.FavoritePhase is not ("WeighWalk" or "Weigh")) && (window.FetchingInvite || window.FavoriteTargetId == Wand),
            $"...then picks either ({window.FavoritePhase}, {window.FetchPhase})");
        Call(window, "CancelFeatureActivity"); Free();

        // The wand and the mouse at once: the favourite is chased.
        Place(bounds.Right - 60, middle + 250, middle - 250);
        pet.X = middle - PetSettings.CenterX; Free();
        Favorite(Mouse);
        Call(window, "StartToyDrag", wandThing); SetThing(wandThing, "GrabX", 0.0); SetThing(wandThing, "GrabY", 0.0);
        for (int i = 0; i < 30; i++) Toys();
        var mouseThing = Thing(Mouse);
        SetThing(mouseThing, "Press", true); Call(window, "EndToyDrag", mouseThing, true);
        FeatureStep();
        check(window.ChaseToyId == Mouse && window.WindUpOf(Mouse)!.Running && window.FeatherOf(Wand)!.Ready, "Wand in hand and the mouse running: the favourite mouse is chased");
        check(window.ToyPlays.Mouse == plays.Mouse + 1, "...a mouse round");
        Call(window, "CancelFeatureActivity");
        Call(window, "EndToyDrag", wandThing, false);
        Settle(wand); Settle(mouse); Free();

        // Python gone: no favourite any more.
        Favorite(Wand);
        Call(window, "ForgetBond");
        check(window.FavoriteToy == null && !window.GoVisitFavorite(), "Python gone: the favourite is forgotten");
        window.ToggleToyBasket();
        SetField(window, "cursorProbe", null);
        window.Reset();
    }

    // PET_RENDER_REVIEW=<folder>: the cat's own window as drawn (the toys are separate windows).
    private static void Snapshot(PetWindow window, string name)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not { Length: > 0 } folder) return;
        Call(window, "Present", false);
        var renderer = Field<PetRenderer>(window, "renderer");
        renderer.InvalidateVisual(); renderer.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)renderer.ActualWidth * 2, (int)renderer.ActualHeight * 2, 192, 192, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(renderer);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }
}
