using System.IO;
using System.Windows;
using System.Windows.Media;
using DesktopPet;
using static PetPrivate;

// Mini-games M5 (docs/MINI-GAMES.md): the treasure hunt in the real pet window — the 60–120 minute timer, the
// find scene and the map over the cat's head (blocked asleep, in Quiet, over a fullscreen app; 3 a day), starting
// from the map and from the menu, a full hunt (prints, the cat walking under them on its ground, the chest beside
// it, the story, the diary's numbers), hints, no losing at 3 minutes, the chest opening by itself, Esc, a click or
// drag on the cat, Pause, a fullscreen app, the switch, a window edge; and drawings to review.
internal static class TreasureChecks
{
    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        var physics = Field<PetPhysics>(window, "physics");
        void Frame()
        {
            Call(window, "TickDesktopToys", 1 / 60.0); Call(window, "TickGuests", 1 / 60.0); Call(window, "TickTreasure", 1 / 60.0); Call(window, "TickFeatures", 1 / 60.0);
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
        Point cursor = new(bounds.Left + 5, bounds.Top + 5);
        var keys = new HashSet<int>();
        var today = new DateTime(2026, 11, 12, 10, 0, 0);
        SetField(window, "cursorProbe", new Func<Point>(() => new Point(cursor.X * scale, cursor.Y * scale)));
        SetField(window, "keyProbe", new Func<int, bool>(keys.Contains));
        SetField(window, "treasureClock", new Func<DateTime>(() => today));
        window.Reset(); pet.Emotions.FinishSleep(0); pet.Emotions.Reset();
        preferences.Quiet = false; behavior.Quiet = false;
        var saved = (preferences.Treasure, preferences.TreasureChests, preferences.TreasureStories, preferences.TreasureMapsToday, preferences.TreasureMapDay);
        preferences.Treasure = true; preferences.TreasureChests = 0; preferences.TreasureStories = null; preferences.TreasureMapsToday = 0; preferences.TreasureMapDay = null;
        void Home(double? x = null) { pet.X = (x ?? middle) - PetSettings.CenterX; pet.Y = bounds.Bottom - catHeight; pet.LedgeId = 0; Free(); }
        double CatCenter() => pet.X + settings.Width / 2;
        var map = Field<TreasureMap>(window, "treasureMap");
        void Clear() { Call(window, "StopTreasure"); Call(window, "EndTreasureFind"); map.Take(); Home(); }
        // A map in hand without the find scene (resets the day's count, so the 3 a day never gets in the way).
        void GiveMap() { preferences.TreasureMapsToday = 0; map.Give(); }
        Home();

        // The timer: 60–120 minutes from the start; a blocked moment tries again two minutes later.
        SetField(window, "treasureWait", double.NaN);
        Call(window, "TickTreasure", 0.0);
        check(window.TreasureWait is >= 60 * 60 and <= 120 * 60 && !window.TreasureMapHeld, "The first map is 60–120 minutes away");
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        SetField(window, "treasureWait", 0.005);
        Call(window, "TickTreasure", 1 / 60.0);
        check(!window.TreasureMapHeld && window.TreasureWait == TreasureRules.RetrySeconds && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked,
            "Asleep: no map, the cat sleeps on, it tries again in 2 minutes");
        pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();
        preferences.Quiet = true; behavior.Quiet = true;
        check(!window.TryTreasureFind(), "No map in Quiet mode");
        preferences.Quiet = false; behavior.Quiet = false;
        check(window.StartFocus(25) && !window.TryTreasureFind(), "No map in focus time");
        window.StopFocus(); Free();
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (true, 0)));
        Call(window, "TickContext", 1.1);
        check(!window.TryTreasureFind(), "No map over a fullscreen app");
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (false, 0)));
        Call(window, "TickContext", 1.1);
        SetField(window, "contextProbe", null);
        Free(); Home();
        check(window.TryGuestVisit(GuestKind.Slime, follows: false) && !window.TryTreasureFind(), "No map while a visitor is here");
        Call(window, "CloseGuest"); Free(); Home();

        // The find scene: the cat sits holding the rolled map, looking up about 2 seconds; then the map over its head.
        check(window.TreasureMenuEnabled && window.TreasureMenuLabel == "Treasure hunt" && window.TreasureMenuNote == "The pet finds a map, then you hunt together.",
            "No map yet: the row still works (the cat will find one first)");
        SetField(window, "treasureWait", 0.005);
        Call(window, "TickTreasure", 1 / 60.0);
        var renderer = Field<PetRenderer>(window, "renderer");
        check(window.TreasureMapHeld && window.TreasureFinding && renderer.CarryMap && window.TreasureWait >= 60 * 60 && TreasureRules.MapsToday(preferences, today) == 1,
            "Free: the cat finds a map, the next is 60–120 minutes away, counted for today");
        bool sat = true, noEmotion = true;
        double catY = pet.Y;
        int frames = 0;
        while (window.TreasureFinding && frames++ < 60 * 4)
        {
            Frame();
            if (!window.TreasureFinding) break;
            sat &= pet.Action == PetAction.Sitting && Math.Abs(pet.Y - catY) < 0.01;
            noEmotion &= !pet.Emotions.Suspended && pet.Emotions.Current is not (PetEmotion.Surprised or PetEmotion.Happy);
            if (frames == 60) { Snapshot(window, "treasure-find", light: false); Snapshot(window, "treasure-find-light", light: true); }
        }
        check(frames / 60.0 is >= 1.8 and <= 2.3 && sat && noEmotion && !renderer.CarryMap, $"...sitting with the map about 2 seconds ({frames / 60.0:F1} s), a scene, not an emotion");
        Frame();
        check(window.TreasureIconShown && window.TreasureMapLeft is > 590 and <= 600 && window.TreasureMenuEnabled && window.TreasureMenuLabel == "Treasure hunt",
            "Then a little map over its head, kept 10 minutes; the row is ready");
        Snapshot(window, "treasure-icon", light: false, icon: true);
        Snapshot(window, "treasure-icon-light", light: true, icon: true);
        var iconHost = Field<Window?>(window, "treasureIconWindow");
        check(iconHost != null && Math.Abs(iconHost.Left + TreasureMapIcon.Size / 2 - CatCenter()) < 1.5 && iconHost.Top + TreasureMapIcon.Size <= pet.Y + 10,
            "The map sits over the cat's head");
        check(!window.TryTreasureFind(), "One map at a time");
        window.SetPaused(true);
        double left = window.TreasureMapLeft;
        for (int i = 0; i < 60; i++) Call(window, "TickTreasure", 0.0);
        check(window.TreasureMapLeft == left && window.TreasureMapHeld, "Pause stops the map's 10 minutes");
        window.SetPaused(false);
        Clear();
        preferences.TreasureMapsToday = 3; preferences.TreasureMapDay = today.Date;
        check(!window.TryTreasureFind(), "Three maps today: no more");
        today = today.AddDays(1);
        check(window.TryTreasureFind() && TreasureRules.MapsToday(preferences, today) == 1, "The next day: a map again");
        Clear();

        // A map left alone fades out after its 10 minutes; nothing is lost.
        GiveMap();
        for (int i = 0; i < 6010; i++) Call(window, "TickTreasure", 0.1);
        check(!window.TreasureMapHeld && !window.TreasureIconShown, "Ten minutes untouched: the map fades away");
        Home();

        // A full hunt, started from the map over the head (not a click on the cat).
        GiveMap(); Frame();
        int chests = preferences.TreasureChests;
        check(window.ClickTreasureMap() && window.TreasurePhase == "Hunt" && window.TreasureArtShown && !window.TreasureIconShown, "A click on the map starts the hunt; the map icon goes");
        check(pet.Emotions.Suspended && (bool)Call(window, "FeatureOwnsMovement")! && window.MiniGameActive, "No automatic emotion or behaviour while hunting");
        check(!window.CupGameMenuEnabled && !window.FirefliesMenuEnabled && !window.TryGuestVisit(GuestKind.Butterfly), "One mini-game at a time, and no visitor during the hunt");
        check(window.MiniLabel == "Find the 3 paw prints · 0/3", "\"Find the 3 paw prints · 0/3\"");
        var hunt = window.TreasureHuntNow;
        double ground = bounds.Bottom;
        check(hunt.Spots.Count == 3 && hunt.Spots.All(s => Math.Abs(s.X - CatCenter()) <= 400 && s.Y < ground && s.Y >= ground - 300),
            "Three prints around the cat, up to 300 DIPs above its floor");
        check(Enumerable.Range(0, 3).All(i => Math.Abs(window.TreasureGlow(i) - TreasureRules.Faint) < 1e-9), "...faint");
        cursor = new Point(hunt.Spots[0].X + 20, hunt.Spots[0].Y);
        check(window.TreasureGlow(0) > TreasureRules.Faint, "...one brighter as the cursor comes near");
        Snapshot(window, "treasure-prints", light: false);
        Snapshot(window, "treasure-prints-light", light: true);
        check(!window.ClickTreasureAt(bounds.Left + 3, bounds.Top + 3) && window.TreasureActive, "A click on empty space finds nothing and does not end it");
        bool stayedOnFloor = true, chestClear = true;
        foreach (int print in new[] { 2, 0, 1 })
        {
            var spot = hunt.Spots[print];
            check(window.ClickTreasureAt(spot.X, spot.Y) && hunt.Found(print) && window.TreasureGlow(print) == 1, $"Print {hunt.FoundCount}/3 found, fully lit");
            check(window.MiniLabel == $"{hunt.FoundCount}/3", $"\"{hunt.FoundCount}/3\" over the cat");
            double under = TreasureRules.Under(spot.X, bounds.Left, bounds.Right);
            bool sniffed = false;
            // Long enough for the farthest pair of prints (2 × Spread at the walking speed, TreasureRules.WalkLimit) and the sniff.
            for (int i = 0; i < 60 * 18 && window.TreasurePhase is "Go" or "Sniff"; i++)
            {
                Frame();
                sniffed |= window.TreasurePhase == "Sniff";
                stayedOnFloor &= Math.Abs(pet.Y + catHeight - ground) < 0.5 && pet.LedgeId == 0;
            }
            check(sniffed && Math.Abs(CatCenter() - under) < 10, $"...the cat walks under it and sniffs ({CatCenter() - under:F0})");
        }
        check(stayedOnFloor, "The cat stays on its floor all along");
        check(window.TreasureChestX is double && window.TreasurePhase is "ToChest" or "Chest", "All three: a chest with a puff on the floor");
        Snapshot(window, "treasure-chest-puff", light: false);
        check(Until(() => window.TreasurePhase == "Chest", 60 * 8), "The cat goes to stand beside it");
        double chestX = window.TreasureChestX ?? 0;
        chestClear &= Math.Abs(CatCenter() - chestX) >= TreasureRules.CatHalfWidth + TreasureRules.ChestHalf;
        check(chestClear && Math.Abs(window.TreasureChestFloor - ground) < 0.5 && pet.Facing == (chestX < CatCenter() ? -1 : 1), "...beside it, not on it, facing it");
        for (int i = 0; i < 30; i++) Frame();
        Snapshot(window, "treasure-chest", light: false);
        Snapshot(window, "treasure-chest-light", light: true);
        check(window.ClickTreasureAt(chestX, ground - 10) && window.TreasurePhase == "Paw", "A click on the chest: the cat paws it");
        check(preferences.TreasureChests == chests + 1 && preferences.TreasureStories?.Count == 1 && !window.TreasureMapHeld, "...counted, its story kept, the map used up");
        bool shotOpen = false;
        Until(() => { if (!shotOpen && window.TreasureLid >= 1) { Snapshot(window, "treasure-open", light: false); shotOpen = true; } return window.TreasurePhase == "Story"; }, 60 * 3);
        check(window.TreasureLid == 1 && window.TreasureStoryShown && window.TreasureStoryText(window.TreasureStoryId).Length > 20, "The lid springs open and a story comes out");
        Frame();
        Snapshot(window, "treasure-story", light: false, story: true);
        check(window.ClickTreasureStory() && !window.TreasureActive && !window.TreasureStoryShown && !window.TreasureArtShown && !pet.Emotions.Suspended,
            "A click on the paper closes it; the hunt is over");
        check(pet.Action == PetAction.Idle && !(bool)Call(window, "FeatureOwnsMovement")!, "The cat is itself again");
        check(!window.TreasureMapHeld && window.TreasureMenuLabel == "Treasure hunt" && window.TreasureMenuEnabled, "No map left after the chest; the row still works");

        // From the menu, the story closing itself after 8 seconds, the chest opening itself after 30.
        Home(); GiveMap();
        check(window.ToggleTreasure() && window.TreasurePhase == "Hunt", "The \"Treasure hunt\" row starts it too");
        foreach (int print in new[] { 0, 1, 2 }) { window.ClickTreasureAt(hunt.Spots[print].X, hunt.Spots[print].Y); Until(() => window.TreasurePhase is not ("Go" or "Sniff"), 60 * 18); }
        check(Until(() => window.TreasurePhase == "Chest", 60 * 8), "...three prints, the chest");
        check(Until(() => window.TreasurePhase == "Paw", 60 * 31) && preferences.TreasureChests == chests + 2, "Not clicked for 30 seconds: the cat opens it itself");
        check(Until(() => window.TreasurePhase == "Story", 60 * 3) && Until(() => !window.TreasureActive, 60 * 9), "The story goes after about 8 seconds");
        check(preferences.TreasureStories?.Count == 2 && preferences.TreasureStories[0] != preferences.TreasureStories[1], "...another story, no repeat");

        // Hints: 20 seconds the cat turns its head, 40 seconds it walks near a print, which glows a little.
        Home(); GiveMap();
        window.ToggleTreasure();
        hunt = window.TreasureHuntNow;
        for (int i = 0; i < 60 * 21; i++) Frame();
        int near = hunt.Nearest(CatCenter());
        check(hunt.Hint == 1 && pet.Action == PetAction.Looking && pet.Facing == (hunt.Spots[near].X < CatCenter() ? -1 : 1), "20 seconds: the cat looks toward the nearest print");
        int nearNow = -1;
        double toPrint = double.MaxValue;
        cursor = new Point(bounds.Left + 5, bounds.Top + 5);
        Until(() =>
        {
            nearNow = hunt.Nearest(CatCenter());
            toPrint = Math.Abs(CatCenter() - TreasureRules.Under(hunt.Spots[nearNow].X, bounds.Left, bounds.Right));
            return hunt.Hint == 2 && toPrint <= 40 && pet.Action == PetAction.Looking;
        }, 60 * 34);
        check(hunt.Hint == 2 && toPrint <= 40 && window.TreasureGlow(nearNow) > TreasureRules.Faint && window.TreasureGlow(nearNow) < 1, $"40 seconds: it walks near it ({toPrint:F0}), that print glows a little");
        // No losing: at 3 minutes the rest light up and the cat leads to the chest.
        hunt.Step(TreasureRules.HuntSeconds - 2 - hunt.Time);
        check(Until(() => window.TreasureChestX is double, 60 * 4) && hunt.AllFound && hunt.TimedOut && window.MiniLabel == "The paw prints light up!", "3 minutes: the prints light up by themselves, the chest comes");
        Call(window, "StopTreasure");
        check(preferences.TreasureChests == chests + 2 && window.TreasureMapHeld, "Leaving before the chest opens: not counted, the map stays");
        Home();

        // Esc, the row again, a drag on the cat end it; the map stays for the rest of its time.
        window.ToggleTreasure();
        keys.Add(0x1B); Frame(); keys.Clear();
        check(!window.TreasureActive && window.TreasureMapHeld && !pet.Emotions.Suspended, "Esc ends it; the map stays");
        Home();
        window.ToggleTreasure();
        check(window.ToggleTreasure() && !window.TreasureActive, "The row again ends it");
        Home();
        window.ToggleTreasure();
        cursor = new Point(bounds.Left + 5, bounds.Top + 5);
        keys.Add(0x01); Frame(); keys.Clear();
        check(window.TreasureActive, "A click on empty space does not end it");
        pet.Action = PetAction.Dragging; pet.Grounded = false;
        Frame();
        check(!window.TreasureActive && !window.TreasureArtShown && pet.Action == PetAction.Dragging, "Picking up the cat ends it (it is lifted as usual)");
        Free(); Home();

        // Pause freezes the hunt; a fullscreen app ends it, not counted.
        window.ToggleTreasure();
        hunt = window.TreasureHuntNow;
        for (int i = 0; i < 30; i++) Frame();
        window.SetPaused(true);
        double t = hunt.Time;
        for (int i = 0; i < 60; i++) Call(window, "TickTreasure", 0.0);
        check(window.TreasureActive && hunt.Time == t && !window.ClickTreasureAt(hunt.Spots[0].X, hunt.Spots[0].Y), "Pause freezes the hunt (no clicks either)");
        window.SetPaused(false);
        Frame();
        check(window.TreasureActive, "...and it goes on after Pause");
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (true, 0)));
        Call(window, "TickContext", 1.1);
        Frame();
        check(!window.TreasureActive && !window.TreasureArtShown && preferences.TreasureChests == chests + 2, "A fullscreen app ends it at once, not counted");
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (false, 0)));
        Call(window, "TickContext", 1.1);
        SetField(window, "contextProbe", null);
        Free(); Home();

        // Asleep: the hunt waits (never wakes the cat) and gives up after 30 seconds.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        check(window.ToggleTreasure() && window.TreasurePhase == "Wait", "Asleep: the hunt waits");
        for (int i = 0; i < 60 * 31; i++) Call(window, "TickFeatures", 1 / 60.0);
        check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && !window.TreasureActive, "...the cat sleeps on; after 30 seconds it gives up");
        pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free();

        // Quiet: the user's hunt still runs.
        preferences.Quiet = true; behavior.Quiet = true;
        check(window.ToggleTreasure() && window.TreasurePhase == "Hunt", "Quiet mode: the user's hunt still runs");
        window.ToggleTreasure();
        preferences.Quiet = false; behavior.Quiet = false;
        Home();

        // The row without a map (approved 14/11): the find scene first, not counted in the 3 a day, then the hunt.
        Clear();
        preferences.TreasureMapsToday = 2; preferences.TreasureMapDay = today.Date;
        check(window.ToggleTreasure() && window.TreasurePhase == "Wait" && window.TreasureFinding && window.TreasureUserFind && window.TreasureMapHeld && renderer.CarryMap
              && TreasureRules.MapsToday(preferences, today) == 2, "No map: the row makes the cat find one now, not counted for today");
        int sceneFrames = 0;
        bool sceneSat = true;
        while (window.TreasurePhase == "Wait" && sceneFrames++ < 60 * 4) { Frame(); sceneSat &= !window.TreasureFinding || pet.Action == PetAction.Sitting; }
        check(window.TreasurePhase == "Hunt" && sceneFrames / 60.0 is >= 1.8 and <= 2.4 && sceneSat && !renderer.CarryMap && !window.TreasureUserFind,
            $"...it sits with the map about 2 seconds ({sceneFrames / 60.0:F1} s), then the hunt starts");
        window.ToggleTreasure(); Clear();
        preferences.TreasureMapsToday = 3; preferences.TreasureMapDay = today.Date;
        check(!window.TryTreasureFind() && window.ToggleTreasure() && window.TreasureFinding, "Three maps found today: the row still works");
        pet.Action = PetAction.Dragging; pet.Grounded = false;
        Frame();
        check(!window.TreasureActive && !window.TreasureFinding && window.TreasureMapHeld && pet.Action == PetAction.Dragging, "Picking up the cat in that scene ends it; the map stays");
        Free(); Clear();
        // Asleep: no find scene and no map; the cat sleeps on, and after 30 seconds it gives up.
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        check(window.ToggleTreasure() && window.TreasurePhase == "Wait" && !window.TreasureFinding && !window.TreasureMapHeld, "Asleep, no map: the hunt waits, no scene");
        for (int i = 0; i < 60 * 31; i++) Call(window, "TickFeatures", 1 / 60.0);
        check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && !window.TreasureActive && !window.TreasureMapHeld && !window.TreasureUserFind,
            "...the cat sleeps on, gives up after 30 seconds, no map");
        pet.Emotions.FinishSleep(0); pet.Emotions.Reset(); Free(); Home();

        // The switch only stops the automatic finding: a map held is gone at once, the row still works.
        GiveMap();
        preferences.Treasure = false;
        Frame();
        check(!window.TreasureMapHeld && !window.TreasureIconShown && !window.TryTreasureFind() && window.TreasureMenuEnabled, "Switched off: the map is gone, no new map by itself");
        check(window.ToggleTreasure() && window.TreasureFinding && Until(() => window.TreasurePhase == "Hunt", 60 * 4), "...the row still finds one and starts the hunt");
        Frame();
        check(window.TreasureActive, "...and that hunt goes on with the switch off");
        window.ToggleTreasure();
        Frame();
        check(!window.TreasureMapHeld, "...after it the map is gone again");
        preferences.Treasure = true;
        Home();

        // On a window edge: the cat walks under the prints along that edge, never off it; the chest stands on it.
        var ledge = new Ledge(4343, bounds.Bottom - 300, middle - 160, middle + 160, middle - 160, middle + 160, bounds.Bottom - 100);
        SetField(window, "ledges", new List<Ledge> { ledge });
        pet.X = middle - PetSettings.CenterX; pet.Y = ledge.Top - catHeight; pet.LedgeId = ledge.Id; Free();
        double ledgeY = pet.Y;
        GiveMap();
        window.ToggleTreasure();
        hunt = window.TreasureHuntNow;
        bool onEdge = true;
        foreach (int print in new[] { 0, 1, 2 })
        {
            window.ClickTreasureAt(hunt.Spots[print].X, hunt.Spots[print].Y);
            for (int i = 0; i < 60 * 18 && window.TreasurePhase is "Go" or "Sniff"; i++)
            {
                Frame();
                onEdge &= pet.LedgeId == ledge.Id && Math.Abs(pet.Y - ledgeY) < 0.01 && CatCenter() - TreasureRules.CatHalfWidth >= ledge.Left - 0.5 && CatCenter() + TreasureRules.CatHalfWidth <= ledge.Right + 0.5;
            }
        }
        Until(() => window.TreasurePhase == "Chest", 60 * 8);
        check(onEdge && hunt.Spots.All(s => s.Y < ledge.Top), "On a window edge the cat stays on it; the prints are above it");
        check(window.TreasureChestX is double cx && cx - TreasureRules.ChestHalf >= ledge.Left && cx + TreasureRules.ChestHalf <= ledge.Right && Math.Abs(window.TreasureChestFloor - ledge.Top) < 0.5,
            "...and the chest stands on that edge");
        Call(window, "StopTreasure"); map.Take();
        SetField(window, "ledges", new List<Ledge>());
        pet.LedgeId = 0; Home();

        // The diary keeps the numbers.
        check(preferences.TreasureChests == chests + 2 && preferences.TreasureStories?.Count == 2, "Chests and stories are kept for the diary");

        (preferences.Treasure, preferences.TreasureChests, preferences.TreasureStories, preferences.TreasureMapsToday, preferences.TreasureMapDay) = saved;
        SetField(window, "cursorProbe", null);
        SetField(window, "keyProbe", null);
        SetField(window, "treasureClock", null);
        SetField(window, "treasureWait", double.NaN);
        window.Reset();
    }

    // PET_RENDER_REVIEW=<folder>: the cat's window with the hunt's (and the map icon or the story card), on a light or dark desktop.
    private static void Snapshot(PetWindow window, string name, bool light, bool icon = false, bool story = false)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not { Length: > 0 } folder) return;
        Call(window, "Present", false);
        var renderer = Field<PetRenderer>(window, "renderer");
        renderer.InvalidateVisual(); renderer.UpdateLayout();
        var pet = Field<PetModel>(window, "pet");
        var parts = new List<(FrameworkElement Element, double Left, double Top, double Width, double Height)>();
        if (Field<Window?>(window, "treasureWindow") is { } hunt && hunt.Content is FrameworkElement art) parts.Add((art, hunt.Left, hunt.Top, art.Width, art.Height));
        double catLeft = pet.X - PickupAnimation.Padding, catTop = pet.Y - PickupAnimation.Padding;
        parts.Add((renderer, catLeft, catTop, renderer.ActualWidth, renderer.ActualHeight));
        if (icon && Field<Window?>(window, "treasureIconWindow") is { } iconHost && iconHost.Content is FrameworkElement mapIcon) parts.Add((mapIcon, iconHost.Left, iconHost.Top, mapIcon.Width, mapIcon.Height));
        if (story && Field<Window?>(window, "treasureStoryWindow") is { } storyHost && storyHost.Content is FrameworkElement card) parts.Add((card, storyHost.Left, storyHost.Top, storyHost.ActualWidth, storyHost.ActualHeight));
        double left = parts.Min(p => p.Left) - 10, right = parts.Max(p => p.Left + p.Width) + 10;
        double top = parts.Min(p => p.Top) - 10, bottom = parts.Max(p => p.Top + p.Height) + 10;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(light ? Color.FromRgb(0xE9, 0xEE, 0xF2) : Color.FromRgb(0x1F, 0x24, 0x2B)), null, new Rect(0, 0, right - left, bottom - top));
            foreach (var (element, x, y, w, h) in parts)
            {
                if (w <= 0 || h <= 0) continue;
                element.InvalidateVisual(); element.UpdateLayout();
                dc.DrawRectangle(new VisualBrush(element) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, w, h) }, null, new Rect(x - left, y - top, w, h));
            }
        }
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)((right - left) * 2), (int)((bottom - top) * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        encoder.Save(stream);
    }
}
