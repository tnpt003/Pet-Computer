using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
using static PetPrivate;

// The cat working the field in the real pet window (1.5.1 M4): a trip from start to end, what ends
// one, the bird, and the art. Uses a temporary file and a false clock.
internal static class FarmChoreChecks
{
    private const double Frame = 1.0 / 60;

    private static void SetContext(PetWindow window, bool fullscreen, double idle) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, idle)));

    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "pet-chores-ui-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "farm.json");
        var now = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        var chores = Field<FarmChores>(window, "farmChores");
        var bird = Field<FarmBird>(window, "farmBird");
        void Features(double seconds) { for (double t = 0; t < seconds; t += Frame) Call(window, "TickFeatures", Frame); }
        void Farm(double seconds) { for (double t = 0; t < seconds; t += Frame) Call(window, "TickFarm", Frame); }
        void Free()
        {
            pet.Emotions.Reset(); behavior.Reset(); pet.Action = PetAction.Idle; pet.Grounded = true; pet.LedgeId = 0; pet.VX = pet.VY = 0;
            Call(window, "StopFarmChore");
            bird.Reset();
        }
        void Thirsty(PetFarm field, int tile)
        {
            field.Tiles[tile].Stage = FarmStage.Seed; field.Tiles[tile].Crop = FarmCrop.Radish; field.Tiles[tile].GrowsUntil = null;
        }
        void Clear(PetFarm field) { for (int i = 0; i < PetFarm.TileCount; i++) { field.Tiles[i].Stage = FarmStage.Wild; field.Tiles[i].Crop = null; field.Tiles[i].GrowsUntil = null; } }

        SetField(window, "farmPath", path);
        SetField(window, "farmClock", new Func<DateTime>(() => now));
        bool quiet = prefs.Quiet;
        prefs.Quiet = false;
        SetContext(window, false, 0); Call(window, "TickContext", 1.1);
        window.Reset();
        prefs.Farm = true;
        try
        {
            Farm(0.05);
            var field = window.Farm!;
            chores.ScheduleVisit(100000); chores.ScheduleBird(100000); // Only the trips asked for here.
            Free();
            var area = Field<Area>(window, "bounds");
            pet.X = area.Left + 10;

            check(!window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.None, "An empty field gives the cat no trip");
            field.Till(1);
            check(!window.TryFarmVisit(), "It does not sow on its own");
            Clear(field);

            // A watering trip, start to end.
            Thirsty(field, 2);
            double attention = pet.Emotions.SecondsWithoutAttention;
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Water && window.FarmChoreTile == 2, "The cat sets off to water");
            check(Call(window, "FeatureOwnsMovement") is true, "The trip owns the cat's movement");
            check(!window.TryFarmVisit() && window.FarmChoreTile == 2, "One trip at a time");
            Features(Frame);
            check(pet.Action == PetAction.Walking && pet.VX > 0 && pet.Facing > 0, "It walks towards the tile");
            double mid = (double)Call(window, "FarmTileMid", 2)!;
            double halfCat = Field<PetSettings>(window, "settings").Width / 2;
            check(Math.Abs(window.FarmChoreSpot + halfCat - mid) > 50, $"It stops beside the tile, not on it ({window.FarmChoreSpot + halfCat - mid:F0} DIPs)");
            pet.X = window.FarmChoreSpot;
            Features(Frame * 2);
            check(window.FarmChorePhaseNow == FarmChorePhase.Work && pet.Action == PetAction.Sitting && pet.VX == 0, "There, it stands still and works");
            check(pet.WateringRemaining > 0 && field.Next(2) == FarmStep.Water, "The can is out; no water yet");
            Features(FarmChores.WaterHit);
            check(field.Next(2) == FarmStep.Wait && File.Exists(path), "The water lands and is saved");
            check(PetFarm.Load(path).Tiles[2].GrowsUntil != null, "...in the file too");
            Features(FarmChores.WaterSeconds - FarmChores.WaterHit + 0.1);
            check(window.FarmChoreNow == FarmChoreKind.None && pet.WateringRemaining == 0 && pet.Action == PetAction.Idle && pet.VX == 0, "The trip ends and the cat is free");
            check(pet.Emotions.SecondsWithoutAttention == attention, "The trip is not attention");

            // On its own legs, through the real physics, from the screen's far end.
            var physics = Field<PetPhysics>(window, "physics");
            var ledges = Field<List<Ledge>>(window, "ledges");
            double Walk(FarmChoreKind kind)
            {
                double walked = 0;
                while (walked < 40 && window.FarmChoreNow == kind && window.FarmChorePhaseNow != FarmChorePhase.Work)
                {
                    Call(window, "TickFeatures", Frame);
                    physics.Step(pet, area, Frame, ledges, true);
                    walked += Frame;
                }
                return walked;
            }
            Free(); Clear(field); Thirsty(field, 5);
            pet.X = area.Right - Field<PetSettings>(window, "settings").Width;
            check(window.TryFarmVisit(), "A trip from the far end");
            double took = Walk(FarmChoreKind.Water);
            check(window.FarmChorePhaseNow == FarmChorePhase.Work && Math.Abs(pet.X - window.FarmChoreSpot) < 0.01, $"It walks there and stops on its spot ({took:F1} s)");
            Free(); Clear(field); Thirsty(field, 0);
            pet.X = area.Left;
            check(window.TryFarmBird(), "A bird, to creep up on");
            took = Walk(FarmChoreKind.Bird);
            check(window.FarmChorePhaseNow == FarmChorePhase.Work && Math.Abs(pet.X - window.FarmChoreSpot) < 0.01, $"It creeps there too ({took:F1} s)");

            // A harvest counts like a click.
            Free(); Clear(field);
            field.Tiles[0].Stage = FarmStage.Ripe; field.Tiles[0].Crop = FarmCrop.Carrot;
            Thirsty(field, 1);
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Harvest && window.FarmChoreTile == 0, "A ripe tile comes before a thirsty one");
            pet.X = window.FarmChoreSpot;
            Features(Frame * 2);
            check(pet.BatRemaining > 0, "The paw reaches out");
            int before = field.HarvestedCount(FarmCrop.Carrot);
            Features(FarmChores.HarvestSeconds + 0.1);
            check(field.HarvestedCount(FarmCrop.Carrot) == before + 1 && field.Tiles[0].Stage == FarmStage.Wild, "The harvest is counted");
            var gain = Field<System.Windows.Controls.Border?>(window, "farmGain")!;
            check(gain.Opacity == 1, "\"+1\" rises over the tile");
            check(window.FarmChoreNow == FarmChoreKind.None && field.Tiles[1].GrowsUntil == null, "One tile a trip");

            // Done by hand on the way: nothing happens.
            Free(); Clear(field); Thirsty(field, 3);
            check(window.TryFarmVisit(), "Setting off again");
            Features(Frame);
            window.FarmClick(3);
            var until = field.Tiles[3].GrowsUntil;
            Features(Frame * 2);
            check(window.FarmChoreNow == FarmChoreKind.None && field.Tiles[3].GrowsUntil == until, "The user watered it first: the trip ends");
            check(pet.Action == PetAction.Idle && pet.VX == 0, "...and the cat stops walking");

            // A walk that never gets there gives up.
            Free(); Clear(field); Thirsty(field, 4);
            pet.X = area.Left + 10;
            check(window.TryFarmVisit(), "Another trip");
            double spotFar = window.FarmChoreSpot;
            for (double t = 0; t <= FarmChores.WalkLimitSeconds + 0.1 && window.FarmChoreNow != FarmChoreKind.None; t += 0.05) { pet.X = area.Left + 10; Call(window, "TickFeatures", 0.05); }
            check(window.FarmChoreNow == FarmChoreKind.None && field.Next(4) == FarmStep.Water && spotFar > pet.X, "A walk that takes too long is given up");

            // What stops a trip, or keeps it from starting.
            void Blocked(string what, Action set, Action unset)
            {
                Free(); Clear(field); Thirsty(field, 2);
                set();
                check(!window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.None, $"No trip: {what}");
                unset();
                Free();
                check(window.TryFarmVisit(), $"(a trip, before: {what})");
                set();
                Call(window, "WatchFarmChores", Frame);
                check(window.FarmChoreNow == FarmChoreKind.None, $"It ends the trip: {what}");
                unset();
                Free();
            }
            Blocked("asleep",
                () => { pet.Emotions.Reset(); pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep(); },
                () => { pet.Emotions.Reset(); pet.Action = PetAction.Idle; });
            Blocked("dragged", () => SetField(window, "dragging", true), () => SetField(window, "dragging", false));
            Blocked("a press that may become a drag", () => SetField(window, "pressPending", true), () => SetField(window, "pressPending", false));
            Blocked("thrown", () => { pet.Action = PetAction.Thrown; pet.Grounded = false; }, () => { pet.Action = PetAction.Idle; pet.Grounded = true; });
            Blocked("Quiet", () => prefs.Quiet = true, () => prefs.Quiet = false);
            Blocked("Pause", () => SetField(window, "paused", true), () => SetField(window, "paused", false));
            Blocked("the user away", () => { SetContext(window, false, 100000); Call(window, "TickContext", 1.1); }, () => { SetContext(window, false, 0); Call(window, "TickContext", 1.1); });
            Blocked("on a window", () => pet.LedgeId = 7, () => pet.LedgeId = 0);
            Blocked("field on another monitor", () => SetField(window, "farmWithCat", false), () => SetField(window, "farmWithCat", true));
            Blocked("the setting off", () => prefs.FarmHelper = false, () => prefs.FarmHelper = true);

            // Asleep mid-trip: it stays asleep, untouched.
            Free(); Clear(field); Thirsty(field, 2);
            check(window.TryFarmVisit(), "A trip before the nap");
            pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
            Features(0.1);
            check(pet.Emotions.SleepLocked && pet.Action == PetAction.Sleeping && window.FarmChoreNow == FarmChoreKind.None, "A cat that falls asleep stays asleep");
            Free();

            // A click on the cat (any cancel) ends a trip.
            Thirsty(field, 2);
            check(window.TryFarmVisit(), "A trip before the click");
            Call(window, "CancelFeatureActivity");
            check(window.FarmChoreNow == FarmChoreKind.None && pet.WateringRemaining == 0, "A cancel ends it");

            // A plot being moved.
            Free(); Thirsty(field, 2);
            check(window.TryFarmVisit(), "A trip before the plot moves");
            window.BeginFarmMove();
            Call(window, "WatchFarmChores", Frame);
            check(window.FarmChoreNow == FarmChoreKind.None, "Lifting the plot ends the trip");
            check(!window.TryFarmVisit(), "No trip to a plot in the air");
            Farm(FarmLift.RiseSeconds + 0.1);
            window.DropFarm();
            Farm(FarmLift.FallSeconds + FarmLift.DustSeconds + 0.1);

            // The bird.
            Free(); Clear(field);
            check(!window.TryFarmBird() && window.FarmBirdPhaseNow == FarmBirdPhase.Away, "No bird on an empty field");
            Thirsty(field, 2);
            pet.X = area.Left + 10;
            check(window.TryFarmBird() && window.FarmBirdPhaseNow == FarmBirdPhase.Landing && window.FarmChoreNow == FarmChoreKind.Bird, "A bird comes and the cat notices");
            check(bird.Side > 0, "It stands beyond the crop from the cat");
            check(!window.TryFarmBird(), "One bird at a time");
            var art = Field<PetFarmArt>(window, "farmArt");
            Farm(FarmBird.LandSeconds + 0.1);
            check(window.FarmBirdPhaseNow == FarmBirdPhase.Perched && art.BirdTile == 2 && art.BirdAir == 0, "It lands and the art shows it");
            Features(Frame);
            check(pet.Action == PetAction.Looking && window.FarmChorePhaseNow == FarmChorePhase.Notice && pet.VX == 0, "The cat looks at it first");
            Features(FarmChores.NoticeSeconds + 0.02);
            Features(Frame);
            double full = Field<PetSettings>(window, "settings").WalkSpeed;
            check(window.FarmChorePhaseNow == FarmChorePhase.Walk && pet.Action == PetAction.Walking && pet.VX > 0 && pet.VX < full * 0.8, "It creeps up on it");
            check(pet.PlayCrouch > 0, "...low to the ground");
            pet.X = window.FarmChoreSpot;
            Features(Frame * 2);
            check(window.FarmChorePhaseNow == FarmChorePhase.Work && window.FarmBirdPhaseNow == FarmBirdPhase.Perched, "It crouches");
            Features(FarmChores.PounceHit + 0.02);
            check(window.FarmBirdPhaseNow == FarmBirdPhase.Leaving && pet.BatRemaining > 0, "It pounces and the bird flies off");
            int stage = (int)field.Tiles[2].Stage;
            Farm(FarmBird.LeaveSeconds + 0.1);
            Features(FarmChores.PounceSeconds);
            check(window.FarmBirdPhaseNow == FarmBirdPhase.Away && window.FarmChoreNow == FarmChoreKind.None && (int)field.Tiles[2].Stage == stage && field.Tiles[2].Crop != null, "The bird is gone and took nothing");
            check(pet.PlayCrouch == 0 && pet.Action == PetAction.Idle, "The cat stands up");

            // A bird alone: the cat was picked up, the bird leaves by itself and the plot's art is cleared.
            Free(); Thirsty(field, 2);
            check(window.TryFarmBird(), "Another bird");
            Farm(FarmBird.LandSeconds + 0.1);
            SetField(window, "dragging", true);
            Features(Frame);
            check(window.FarmChoreNow == FarmChoreKind.None && window.FarmBirdPhaseNow == FarmBirdPhase.Perched, "The cat is picked up; the bird stays");
            SetField(window, "dragging", false);
            Farm(FarmBird.StaySeconds + FarmBird.LeaveSeconds + 0.2);
            check(window.FarmBirdPhaseNow == FarmBirdPhase.Away && art.BirdTile == -1, "...and flies off by itself");

            // A bird goes when its crop is taken or the ground goes.
            Free(); Thirsty(field, 2);
            check(window.TryFarmBird(), "A bird on a crop");
            Farm(FarmBird.LandSeconds + 0.1);
            Clear(field);
            Farm(0.05);
            check(window.FarmBirdPhaseNow == FarmBirdPhase.Leaving, "An emptied tile sends it away");
            Free(); Thirsty(field, 2);
            check(window.TryFarmBird(), "A bird before the plot moves");
            window.BeginFarmMove();
            check(window.FarmBirdPhaseNow == FarmBirdPhase.Leaving, "Lifting the plot sends it away");
            Farm(FarmLift.RiseSeconds + 0.1);
            window.DropFarm();
            Farm(FarmLift.FallSeconds + FarmLift.DustSeconds + 0.1);

            // A timer that comes due while the cat may not go waits and looks again soon.
            Free(); Thirsty(field, 2);
            prefs.Quiet = true;
            chores.ScheduleVisit(0.01);
            Call(window, "WatchFarmChores", 0.02);
            check(window.FarmChoreNow == FarmChoreKind.None && Math.Abs(chores.VisitDue - FarmChores.RetrySeconds) < 0.01, "A wrong moment waits and looks again");
            prefs.Quiet = false;
            chores.ScheduleVisit(0.01);
            Call(window, "WatchFarmChores", 0.02);
            check(window.FarmChoreNow == FarmChoreKind.Water && chores.VisitDue >= FarmChores.VisitMinSeconds, "When it is due and free, the cat goes");
            Free();
        }
        finally
        {
            Free();
            Call(window, "CloseFarm");
            prefs.Farm = false;
            prefs.FarmHelper = true;
            prefs.Quiet = quiet;
            SetField(window, "dragging", false);
            SetField(window, "pressPending", false);
            SetField(window, "paused", false);
            SetField(window, "farmClock", null);
            SetField(window, "farmPath", PetFarm.DefaultPath);
            SetField(window, "farm", null);
            chores.ScheduleVisit(FarmChores.VisitMinSeconds); chores.ScheduleBird(FarmChores.BirdMinSeconds);
            SetContext(window, false, 0); Call(window, "TickContext", 1.1);
            window.Reset();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        Art(check);
    }

    // The can and the bird, on a light and a dark wallpaper.
    private static void Art(Action<bool, string> check)
    {
        void Save(RenderTargetBitmap bitmap, string name)
        {
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not string review) return;
            Directory.CreateDirectory(review);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(review, name + ".png")); encoder.Save(stream);
        }
        string Hash(RenderTargetBitmap bitmap)
        {
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(pixels));
        }
        string Plot(Action<PetFarmArt> set, Color back, string? name)
        {
            int width = (int)PetFarmArt.ArtWidth, height = (int)PetFarmArt.ArtHeight;
            var art = new PetFarmArt();
            for (int i = 0; i < 6; i++) art.Tiles[i] = new(FarmStage.Young, FarmCrop.Carrot, true, false, 0.5);
            set(art);
            art.Measure(new Size(width, height)); art.Arrange(new Rect(0, 0, width, height));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual); bitmap.Render(art);
            if (name != null) Save(bitmap, name);
            return Hash(bitmap);
        }
        string Cat(Action<PetModel> set, Color back, string? name)
        {
            const int size = 180;
            var model = new PetModel { Action = PetAction.Sitting, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1 };
            set(model);
            var cat = new PetRenderer(model);
            cat.Measure(new Size(size, size)); cat.Arrange(new Rect(0, 0, size, size));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(0, 0, size, size));
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual); bitmap.Render(cat);
            if (name != null) Save(bitmap, name);
            return Hash(bitmap);
        }

        var light = Color.FromRgb(0xF1, 0xEE, 0xE6);
        var dark = Color.FromRgb(0x1C, 0x22, 0x30);
        foreach (var (back, tone) in new[] { (light, "light"), (dark, "dark") })
        {
            Plot(a => { a.BirdTile = 1; a.BirdSide = 1; a.BirdTime = 0.3; }, back, $"chore-bird-peck-{tone}");
            Plot(a => { a.BirdTile = 4; a.BirdSide = -1; a.BirdTime = 1; }, back, $"chore-bird-rest-{tone}");
            Plot(a => { a.BirdTile = 3; a.BirdSide = 1; a.BirdAir = 0.45; a.BirdFlying = true; a.BirdTime = 0.1; }, back, $"chore-bird-fly-{tone}");
            Cat(m => m.WateringRemaining = 1, back, $"chore-water-{tone}");
            Cat(m => { m.WateringRemaining = 1; m.Facing = -1; }, back, $"chore-water-left-{tone}");
        }
        string plain = Plot(_ => { }, dark, null);
        check(plain != Plot(a => { a.BirdTile = 1; a.BirdTime = 1; }, dark, null), "A bird is drawn");
        string resting = Plot(a => { a.BirdTile = 1; a.BirdTime = 1.5; }, dark, null);
        check(resting != Plot(a => { a.BirdTile = 1; a.BirdTime = 0.3; }, dark, null), "It pecks");
        check(resting != Plot(a => { a.BirdTile = 1; a.BirdTime = 1.5; a.BirdFlying = true; a.BirdAir = 0.5; }, dark, null), "It flies");
        check(Plot(a => { a.BirdTile = 1; a.BirdAir = 1; a.BirdFlying = true; }, dark, null) == plain, "Up and away it has faded out");
        string sitting = Cat(_ => { }, dark, null);
        check(sitting != Cat(m => m.WateringRemaining = 1, dark, null), "The cat holds a watering can");
        string asleep = Cat(m => { m.Action = PetAction.Sleeping; m.Emotions.EnterSleep(); }, dark, null);
        check(asleep == Cat(m => { m.WateringRemaining = 1; m.Action = PetAction.Sleeping; m.Emotions.EnterSleep(); }, dark, null), "A sleeping cat never holds the can");
    }
}
