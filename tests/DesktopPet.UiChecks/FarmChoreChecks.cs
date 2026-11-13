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
        SetField(window, "farmGoldRandom", null);
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
        void Hearth()
        {
            var field = window.Farm!;
            var art = window.FarmHearth.Art!;
            var area = Field<Area>(window, "bounds");
            double hearth = (double)Field2("FarmHearthMid");
            void Ready()
            {
                Free(); Clear(field);
                field.PotDish = null;
                field.Stock.Clear(); field.Stock["Radish"] = 3;
                pet.TreatRemaining = 0; pet.TreatDish = null;
                SetField(window, "treatCooldown", 0.0); SetField(window, "treatSatisfiedRemaining", 0.0);
                pet.X = area.Left + 10;
            }
            void Arrive() { pet.X = window.FarmChoreSpot; Features(Frame * 2); }

            // Cooking asked for by the user, from start to end.
            Ready();
            double treats = pet.Activity.Treats;
            check(window.FarmCook(FarmDish.RadishSoup) && window.FarmChoreNow == FarmChoreKind.Pour && chores.ByUser, "\"Cook\" sends the cat to the hearth");
            check(field.StockOf(FarmCrop.Radish) == 3 && field.PotDish == null, "Nothing is used up on the way");
            check(pet.TreatRemaining == 0, "It does not eat yet");
            check(!window.FarmCook(FarmDish.RadishSoup), "One dish at a time");
            double halfCat = Field<PetSettings>(window, "settings").Width / 2;
            check(Math.Abs(window.FarmChoreSpot + halfCat - hearth) > 50, "It stands beside the hearth, not on it");
            Features(Frame);
            check(pet.Action == PetAction.Walking && pet.Facing > 0, "It walks there");
            Arrive();
            check(window.FarmChorePhaseNow == FarmChorePhase.Work && pet.BatRemaining > 0 && pet.VX == 0, "There it reaches over the pot");
            Features(FarmChores.PourHit);
            check(field.PotDish == FarmDish.RadishSoup && field.StockOf(FarmCrop.Radish) == 1 && PetFarm.Load(path).PotDish == FarmDish.RadishSoup, "It pours: the store pays and the pot is saved");
            Features(FarmChores.PourSeconds - FarmChores.PourHit + Frame);
            check(window.FarmChoreNow == FarmChoreKind.Stir && chores.ByUser && chores.Phase == FarmChorePhase.Work, "It goes on to stir");
            Features(1);
            Farm(Frame * 2);
            check(pet.StirringRemaining > 0 && pet.Action == PetAction.Sitting && art.Stirring && art.HearthFire > 0 && art.PotDish == FarmDish.RadishSoup, "It stirs over a fire");
            check(pet.TreatRemaining == 0, "...and has not eaten");
            Features(FarmChores.StirSeconds - 1 + Frame * 2);
            check(window.FarmChoreNow == FarmChoreKind.None && pet.StirringRemaining == 0 && pet.TreatRemaining > 0 && pet.TreatDish == FarmDish.RadishSoup, "Done, it eats beside the pot");
            check(field.PotDish == null && PetFarm.Load(path).PotDish == null, "The pot is empty again");
            Features(PetModel.TreatDuration + 0.1);
            check(pet.Activity.Treats == treats + 1, "It counts as the treat the user gave");
            Farm(1);
            check(art.HearthFire == 0 && !art.Stirring && art.PotDish == null, "The fire dies down");
            double fireFree = Field<double>(window, "farmHearthFire");
            check(fireFree == 0, "...and the hearth is not redrawn any more");

            // Stopped before pouring: nothing lost. In Quiet what the user asked for goes on.
            Ready();
            check(window.FarmCook(FarmDish.RadishSoup), "Cooking again");
            Features(Frame);
            Call(window, "CancelFeatureActivity");
            check(window.FarmChoreNow == FarmChoreKind.None && field.StockOf(FarmCrop.Radish) == 3 && field.PotDish == null, "A cancel before pouring costs nothing");
            Ready();
            prefs.Quiet = true;
            check(window.FarmCook(FarmDish.RadishSoup), "The user may cook in Quiet");
            Call(window, "WatchFarmChores", Frame);
            Arrive();
            check(window.FarmChoreNow == FarmChoreKind.Pour && window.FarmChorePhaseNow == FarmChorePhase.Work, "...and the cat goes on in Quiet");
            prefs.Quiet = false;

            // What stops it, without waking anything or using anything up.
            void Stops(string what, Action set, Action unset)
            {
                Ready();
                check(window.FarmCook(FarmDish.RadishSoup), $"(cooking, before: {what})");
                set();
                Call(window, "WatchFarmChores", Frame);
                check(window.FarmChoreNow == FarmChoreKind.None && field.StockOf(FarmCrop.Radish) == 3, $"It ends the cooking: {what}");
                unset();
            }
            Stops("asleep", () => { pet.Emotions.Reset(); pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep(); }, () => { check(pet.Emotions.SleepLocked && pet.Action == PetAction.Sleeping, "A sleeping cat stays asleep"); pet.Emotions.Reset(); pet.Action = PetAction.Idle; });
            Stops("dragged", () => SetField(window, "dragging", true), () => SetField(window, "dragging", false));
            Stops("thrown", () => { pet.Action = PetAction.Thrown; pet.Grounded = false; }, () => { pet.Action = PetAction.Idle; pet.Grounded = true; });
            Stops("Pause", () => SetField(window, "paused", true), () => SetField(window, "paused", false));
            Stops("on a window", () => pet.LedgeId = 7, () => pet.LedgeId = 0);
            Stops("hearth on another monitor", () => window.FarmHearth.WithCat = false, () => window.FarmHearth.WithCat = true);

            // Stopped after pouring: the pot keeps it and the cat comes back, as its own meal.
            Ready();
            check(window.FarmCook(FarmDish.RadishSoup), "Cooking, to be stopped");
            Arrive();
            Features(FarmChores.PourHit + Frame);
            check(field.PotDish == FarmDish.RadishSoup, "Poured");
            SetField(window, "dragging", true);
            Call(window, "WatchFarmChores", Frame);
            SetField(window, "dragging", false);
            check(window.FarmChoreNow == FarmChoreKind.None && field.PotDish == FarmDish.RadishSoup && pet.StirringRemaining == 0, "Picked up: the dish stays in the pot");
            check(PetFarm.Load(path).PotDish == FarmDish.RadishSoup, "...also after closing the app");
            check(!window.FarmCook(FarmDish.RadishSoup), "No second dish while the pot is full");
            prefs.Quiet = true;
            check(!window.TryFarmVisit(), "The cat does not go back in Quiet");
            prefs.Quiet = false;
            SetField(window, "treatCooldown", 5.0);
            check(!window.TryFarmVisit(), "...nor while it cannot eat");
            SetField(window, "treatCooldown", 0.0);
            prefs.FarmHelper = false;
            pet.X = area.Left + 10;
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Stir && !chores.ByUser, "It goes back to the pot, even with the helper off");
            prefs.FarmHelper = true;
            Arrive();
            double attention = pet.Emotions.SecondsWithoutAttention;
            double treated = pet.Activity.Treats;
            Features(FarmChores.StirSeconds + Frame);
            check(pet.TreatRemaining > 0 && field.PotDish == null, "It finishes and eats");
            Features(PetModel.TreatDuration + 0.1);
            check(pet.Activity.Treats == treated && pet.Emotions.SecondsWithoutAttention == attention, "Its own meal is not attention and not a treat given");
            pet.Emotions.Step(pet, 4, -200, 50);
            check(pet.Emotions.Target == PetEmotion.Happy, "...but the cat is pleased");

            // A second helping of the pot while eating cannot go wrong.
            Ready();
            field.PotDish = FarmDish.RadishSoup;
            SetField(window, "treatCooldown", 0.0);
            check(window.TryFarmVisit(), "Back to a pot");
            Arrive();
            Features(FarmChores.StirSeconds - 0.2);
            SetField(window, "treatCooldown", 5.0);
            Features(0.4);
            check(field.PotDish == FarmDish.RadishSoup && pet.TreatRemaining == 0 && window.FarmChoreNow == FarmChoreKind.None, "It cannot eat yet: the dish stays in the pot");
            field.PotDish = null;

            // By itself.
            Ready();
            chores.ScheduleCook(100);
            check(!window.TryFarmVisit(), "Not before its time");
            field.Stock["Radish"] = 6;
            chores.ScheduleCook(0);
            prefs.FarmAutoCook = false;
            check(!window.TryFarmVisit(), "Not with the setting off");
            prefs.FarmAutoCook = true;
            prefs.FarmHelper = false;
            check(!window.TryFarmVisit(), "Not with the helper off");
            prefs.FarmHelper = true;
            prefs.Quiet = true;
            check(!window.TryFarmVisit(), "Not in Quiet");
            prefs.Quiet = false;
            field.Stock["Radish"] = 5;
            check(!window.TryFarmVisit(), "Not when it would break into the four kept");
            field.Stock["Radish"] = 6;
            Thirsty(field, 2);
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Water, "The field comes first");
            Free(); Clear(field);
            chores.ScheduleCook(0);
            pet.X = area.Left + 10;
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Pour && !chores.ByUser && chores.Dish == FarmDish.RadishSoup, "With nothing else to do it goes to cook");
            check(chores.CookDue >= FarmChores.CookMinSeconds, "...and not again for a while");
            prefs.Quiet = true;
            Call(window, "WatchFarmChores", Frame);
            check(window.FarmChoreNow == FarmChoreKind.None && field.StockOf(FarmCrop.Radish) == 6, "Quiet stops what the cat started itself");
            prefs.Quiet = false;
            Ready();
        }
        void Pond()
        {
            var field = window.Farm!;
            var art = window.FarmPond.Art!;
            var area = Field<Area>(window, "bounds");
            double pond = (double)Field2("FarmPondMid");
            void Ready()
            {
                Free(); Clear(field);
                field.PotDish = null;
                field.Stock.Clear(); field.Fish.Clear();
                pet.TreatRemaining = 0; pet.TreatDish = null;
                SetField(window, "treatCooldown", 0.0); SetField(window, "treatSatisfiedRemaining", 0.0);
                chores.ScheduleFish(100000); chores.ScheduleFishAgain(0); chores.ScheduleCook(100000);
                pet.X = area.Left + 10;
            }
            void Arrive() { pet.X = window.FarmChoreSpot; Features(Frame * 2); }
            MenuItemCheck();

            // Sent by the user, from start to end.
            Ready();
            check(window.FarmCanFish && window.FarmGoFish() && window.FarmChoreNow == FarmChoreKind.Fishing && chores.ByUser, "\"Go fishing\" sends the cat to the pond");
            check(!window.FarmCanFish && !window.FarmGoFish(), "One cast at a time");
            double halfCat = Field<PetSettings>(window, "settings").Width / 2;
            check(Math.Abs(Math.Abs(window.FarmChoreSpot + halfCat - pond) - 100) < 0.01 && chores.FishSide == 1 && window.FarmChoreSpot + halfCat > pond, "It stands on the far bank, clear of the hearth");
            Features(Frame);
            check(pet.Action == PetAction.Walking && pet.Facing > 0, "It walks there");
            Arrive();
            check(window.FarmChorePhaseNow == FarmChorePhase.Work && pet.Action == PetAction.Sitting && pet.VX == 0 && pet.FishingTime >= 0 && pet.Facing < 0, "On the bank it sits, faces the water and casts");
            Farm(Frame * 2);
            check(art.Fishing && art.FishSide == 1 && art.CaughtFish == null, "The water ripples round the float; nothing has bitten");
            // The rare one, to see it reported for Python's line.
            typeof(FarmChores).GetProperty(nameof(FarmChores.Catch))!.SetValue(chores, FarmFish.Goldfish);
            long goldfish = window.FarmReport.Goldfish;
            var fish = chores.Catch;
            int caught = field.FishOf(fish);
            double attention = pet.Emotions.SecondsWithoutAttention;
            Features(chores.BiteAt + 0.1 - chores.Time);
            check(pet.FishingTime >= chores.BiteAt && field.FishOf(fish) == caught, "It bites, and is not yet landed");
            Farm(Frame * 2);
            check(art.CaughtFish == fish && art.CaughtAge > 0, "The fish jumps out of the water");
            Features(FarmChores.ReelSeconds);
            check(field.FishOf(fish) == caught + 1 && PetFarm.Load(path).FishOf(fish) == caught + 1, "It lands in the store and is saved");
            check(window.FarmReport.Goldfish == goldfish + 1, "A golden fish is reported for Python's line");
            var pondGain = window.FarmPond.Gain!;
            check(pondGain.Opacity == 1 && ((System.Windows.Controls.TextBlock)pondGain.Child).Text.StartsWith("+1"), "\"+1\" rises over the pond");
            Features(FarmChores.FishShowSeconds + 0.05);
            check(window.FarmChoreNow == FarmChoreKind.None && pet.FishingTime == -1 && pet.Action == PetAction.Idle, "The trip ends and the cat is free");
            check(pet.Emotions.SecondsWithoutAttention == attention, "Fishing is not attention");
            check(!window.FarmCanFish && chores.FishAgain > 50, "Then a wait before the next cast");
            chores.ScheduleFishAgain(0.01);
            Call(window, "WatchFarmChores", 0.02);
            check(window.FarmCanFish, "...which runs out");
            Farm(0.5);
            check(!art.Fishing && art.CaughtFish == null, "The pond is quiet again");
            Farm(Frame);
            check(!Field<bool>(window, "farmPondBusy"), "...and is not redrawn any more");

            // What stops it: nothing is lost, nothing is woken.
            void Stops(string what, Action set, Action unset)
            {
                Ready();
                check(window.FarmGoFish(), $"(fishing, before: {what})");
                Arrive();
                set();
                Call(window, "WatchFarmChores", Frame);
                check(window.FarmChoreNow == FarmChoreKind.None && pet.FishingTime == -1 && field.Fish.Count == 0 && chores.FishAgain == 0, $"It ends the fishing: {what}");
                unset();
            }
            Stops("asleep", () => { pet.Emotions.Reset(); pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep(); }, () => { check(pet.Emotions.SleepLocked && pet.Action == PetAction.Sleeping, "A sleeping fisher stays asleep"); pet.Emotions.Reset(); pet.Action = PetAction.Idle; });
            Stops("dragged", () => SetField(window, "dragging", true), () => SetField(window, "dragging", false));
            Stops("thrown", () => { pet.Action = PetAction.Thrown; pet.Grounded = false; }, () => { pet.Action = PetAction.Idle; pet.Grounded = true; });
            Stops("Pause", () => SetField(window, "paused", true), () => SetField(window, "paused", false));
            Stops("on a window", () => pet.LedgeId = 7, () => pet.LedgeId = 0);
            Stops("pond on another monitor", () => window.FarmPond.WithCat = false, () => window.FarmPond.WithCat = true);

            void Refused(string what, Action set, Action unset)
            {
                Ready();
                set();
                check(!window.FarmCanFish && !window.FarmGoFish() && window.FarmChoreNow == FarmChoreKind.None, $"No fishing: {what}");
                unset();
            }
            Refused("asleep", () => { pet.Emotions.Reset(); pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep(); }, () => { check(pet.Emotions.SleepLocked, "...and it is not woken"); pet.Emotions.Reset(); pet.Action = PetAction.Idle; });
            Refused("Pause", () => SetField(window, "paused", true), () => SetField(window, "paused", false));
            Refused("on a window", () => pet.LedgeId = 7, () => pet.LedgeId = 0);
            Refused("eating", () => pet.TreatRemaining = 2, () => pet.TreatRemaining = 0);
            Ready();
            chores.BeginCook(FarmChoreKind.Stir, FarmDish.RadishSoup, 0, true);
            check(!window.FarmCanFish && !window.FarmGoFish() && window.FarmChoreNow == FarmChoreKind.Stir, "No fishing while the cat is cooking");
            Call(window, "StopFarmChore");

            // In Quiet what the user asked for goes on; what the cat started stops.
            Ready();
            prefs.Quiet = true;
            check(window.FarmGoFish(), "The user may send it in Quiet");
            Call(window, "WatchFarmChores", Frame);
            Arrive();
            check(window.FarmChoreNow == FarmChoreKind.Fishing && window.FarmChorePhaseNow == FarmChorePhase.Work, "...and it goes on");
            prefs.Quiet = false;

            // By itself: after the field, before the kitchen.
            Ready();
            field.Stock["Radish"] = 8;
            chores.ScheduleCook(0);
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Pour, "Not due: it cooks");
            Ready();
            chores.ScheduleFish(0);
            prefs.FarmAutoFish = false;
            check(!window.TryFarmVisit(), "Not with the setting off");
            prefs.FarmAutoFish = true;
            prefs.FarmHelper = false;
            check(!window.TryFarmVisit(), "Not with the helper off");
            prefs.FarmHelper = true;
            prefs.Quiet = true;
            check(!window.TryFarmVisit(), "Not in Quiet");
            prefs.Quiet = false;
            Thirsty(field, 2);
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Water, "The field comes first");
            Free(); Clear(field);
            field.Stock["Radish"] = 8;
            chores.ScheduleFish(0); chores.ScheduleCook(0);
            pet.X = area.Left + 10;
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Fishing && !chores.ByUser, "With both due it fishes before it cooks");
            check(chores.FishDue >= FarmChores.FishMinSeconds, "...and not again for a while");
            prefs.Quiet = true;
            Call(window, "WatchFarmChores", Frame);
            check(window.FarmChoreNow == FarmChoreKind.None, "Quiet stops what the cat started itself");
            prefs.Quiet = false;
            Ready();
            chores.ScheduleFish(FarmChores.FishMinSeconds);

            // Land in the air ends only the trips to that land.
            var plotLift = Field<FarmLift>(window, "farmLift");
            void Trip(string what, Func<bool> go, FarmLift lifted, bool ends)
            {
                Ready();
                field.Stock["Radish"] = 3;
                check(go(), $"(a trip, before: {what})");
                lifted.Begin();
                Call(window, "WatchFarmChores", Frame);
                check((window.FarmChoreNow == FarmChoreKind.None) == ends, $"{what}: {(ends ? "the trip ends" : "the trip goes on")}");
                lifted.Reset();
                Free();
            }
            Func<bool> cook = () => window.FarmCook(FarmDish.RadishSoup), fishing = () => window.FarmGoFish();
            Trip("the hearth lifted while cooking", cook, window.FarmHearth.Lift, true);
            Trip("the pond lifted while cooking", cook, window.FarmPond.Lift, false);
            Trip("the field lifted while cooking", cook, plotLift, false);
            Trip("the pond lifted while fishing", fishing, window.FarmPond.Lift, true);
            Trip("the hearth lifted while fishing", fishing, window.FarmHearth.Lift, false);
            Trip("the field lifted while fishing", fishing, plotLift, false);
            Ready(); Thirsty(field, 2);
            check(window.TryFarmVisit(), "(a watering trip)");
            window.FarmPond.Lift.Begin();
            Call(window, "WatchFarmChores", Frame);
            check(window.FarmChoreNow == FarmChoreKind.Water, "The pond in the air does not stop the watering");
            window.FarmPond.Lift.Reset();
            Ready();
            field.PotDish = FarmDish.RadishSoup;
            window.FarmHearth.Lift.Begin();
            check(!window.TryFarmVisit(), "The cat does not go back to a pot in the air");
            check(field.PotDish == FarmDish.RadishSoup && !window.FarmCook(FarmDish.RadishSoup), "...the dish stays, and nothing can be cooked");
            window.FarmHearth.Lift.Reset();
            field.PotDish = null;
            Ready();
            window.FarmPond.Lift.Begin();
            check(!window.FarmCanFish, "Nobody fishes from a pond in the air");
            chores.ScheduleFish(0);
            check(!window.TryFarmVisit(), "...not by itself either");
            window.FarmPond.Lift.Reset();
            Ready();
            plotLift.Begin();
            chores.ScheduleFish(0);
            pet.X = area.Left + 10;
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Fishing, "With the field in the air the cat can still fish");
            plotLift.Reset();
            Ready();
            chores.ScheduleFish(FarmChores.FishMinSeconds);

            void MenuItemCheck()
            {
                Ready();
                var item = window.FarmMenuItems().First(m => (string)m.Header != null && ((string)m.Header).Contains("fish", StringComparison.OrdinalIgnoreCase) || ((string)m.Header).Contains("Câu cá"));
                check(item.IsEnabled, "The menu offers fishing");
                item.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
                check(window.FarmChoreNow == FarmChoreKind.Fishing && chores.ByUser, "The menu sends the cat");
                pet.Emotions.Reset(); Free();
                pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
                var off = window.FarmMenuItems().First(m => ((string)m.Header).Contains("fish", StringComparison.OrdinalIgnoreCase) || ((string)m.Header).Contains("Câu cá"));
                check(!off.IsEnabled, "Asleep, the item is greyed");
                pet.Emotions.Reset(); pet.Action = PetAction.Idle;
            }
        }
        object Field2(string name) => typeof(PetWindow).GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
        // Land put away on the sign (docs/FARM-GAME.md, Biển nông trại): it comes out for the cat and folds when the cat is done.
        void Folded()
        {
            var field = window.Farm!;
            var area = Field<Area>(window, "bounds");
            var land = window.FarmFoldOf(FarmLandSlot.Field);
            var hearth = window.FarmFoldOf(FarmLandSlot.Hearth);
            var pond = window.FarmFoldOf(FarmLandSlot.Pond);
            void Put() => Farm(FarmLandFold.OpenSeconds + 0.05);
            void GiveBack() => Farm(FarmLandFold.BorrowGraceSeconds + FarmLandFold.FoldSeconds + 0.1);
            void Ready()
            {
                Free(); Clear(field);
                field.PotDish = null;
                field.Stock.Clear(); field.Stock["Radish"] = 6; field.Fish.Clear();
                pet.TreatRemaining = 0; pet.TreatDish = null;
                SetField(window, "treatCooldown", 0.0); SetField(window, "treatSatisfiedRemaining", 0.0);
                chores.ScheduleFish(100000); chores.ScheduleFishAgain(0); chores.ScheduleCook(100000);
                pet.X = area.Left + 10;
            }

            // The field.
            Ready();
            Thirsty(field, 2);
            prefs.FarmHelper = true;
            check(window.FarmToggleLand(FarmLandSlot.Field), "(field folded)");
            Put();
            check(field.LandWork(FarmLandSlot.Field) == 1, "The sign knows there is work");
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Water && land.Borrowed && land.Showing && field.FieldFolded, "The cat sets off for a folded field and it comes out, still folded in the save");
            GiveBack();
            check(land.Borrowed && land.Amount == 1 && window.FarmChoreNow == FarmChoreKind.Water, "It stays out the whole way there and while the cat is at it");
            pet.X = window.FarmChoreSpot; Features(Frame * 2);
            Features(FarmChores.WaterSeconds + Frame * 3);
            check(window.FarmChoreNow == FarmChoreKind.None && field.Next(2) != FarmStep.Water, "The cat waters it");
            GiveBack();
            check(!land.Borrowed && !land.Showing && land.Amount == 0, "...and only then does it fold again");
            Thirsty(field, 2);
            pet.X = area.Left + 10;
            check(window.TryFarmVisit() && land.Borrowed, "(on its way again)");
            window.FarmToggleLand(FarmLandSlot.Field);
            Call(window, "WatchFarmChores", Frame);
            check(window.FarmChoreNow == FarmChoreKind.None && !land.Showing, "Folding it by hand ends the trip");
            Put();
            window.FarmToggleLand(FarmLandSlot.Field);
            Put();

            // Cooking: the hearth comes out for the job and goes away after.
            Ready();
            window.FarmToggleLand(FarmLandSlot.Hearth);
            Put();
            prefs.FarmAutoCook = true;
            chores.ScheduleCook(0);
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Pour && !chores.ByUser && hearth.Borrowed, "The cat cooks by itself on a folded hearth too, and it comes out");
            Free();
            GiveBack();
            check(!hearth.Showing, "(folded again when the trip is off)");
            chores.ScheduleCook(100000);
            check(window.FarmCook(FarmDish.RadishSoup) && window.FarmChoreNow == FarmChoreKind.Pour && hearth.Borrowed && hearth.Showing && field.HearthFolded, "\"Cook\" opens it for this dish, and it stays folded in the save");
            Put();
            check(hearth.Amount == 1 && window.FarmHearth.Surface!.Visibility == Visibility.Visible, "...so it can be seen");
            pet.X = window.FarmChoreSpot; Features(Frame * 2);
            Features(FarmChores.PourSeconds + Frame * 2);
            check(window.FarmChoreNow == FarmChoreKind.Stir, "It pours and goes on to stir");
            GiveBack();
            check(hearth.Borrowed && hearth.Amount == 1, "The land stays while the cat is at it");
            Features(FarmChores.StirSeconds + Frame * 2);
            check(window.FarmChoreNow == FarmChoreKind.None && field.PotDish == null && pet.TreatRemaining > 0, "It is cooked and the cat eats");
            GiveBack();
            check(hearth.Borrowed && hearth.Amount == 1, "The hearth waits for the bowl to be empty");
            pet.TreatRemaining = 0; pet.TreatDish = null;
            GiveBack();
            check(!hearth.Borrowed && !hearth.Showing && hearth.Amount == 0, "...and is folded again");

            // A dish left in a folded pot.
            Ready();
            field.PotDish = FarmDish.RadishSoup;
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Stir && hearth.Borrowed && !chores.ByUser, "A dish left in a folded pot brings the cat back");
            Free();
            field.PotDish = null;
            GiveBack();
            check(!hearth.Showing, "(folded again)");

            // When the cat may not go, nothing comes out; a bird brings the cat, and the field, out.
            Ready();
            Thirsty(field, 2);
            window.FarmToggleLand(FarmLandSlot.Field);
            Put();
            prefs.Quiet = true;
            check(!window.TryFarmVisit() && !land.Showing, "In Quiet a folded field stays hidden");
            prefs.Quiet = false;
            check(window.TryFarmBird() && window.FarmChoreNow == FarmChoreKind.Bird && land.Borrowed, "A bird on a folded field brings it out for the chase");
            Free();
            GiveBack();
            check(!land.Showing, "(folded again)");
            window.FarmToggleLand(FarmLandSlot.Field);
            Put();

            // Fishing.
            Ready();
            window.FarmToggleLand(FarmLandSlot.Pond);
            Put();
            check(window.FarmCanFish && window.FarmGoFish() && window.FarmChoreNow == FarmChoreKind.Fishing && pond.Borrowed, "\"Go fishing\" opens a folded pond");
            Free();
            GiveBack();
            check(!pond.Borrowed && !pond.Showing, "...and it goes away after");

            // A click during a borrowed job folds it for good.
            Ready();
            check(window.FarmGoFish() && pond.Borrowed, "(fishing again)");
            window.FarmToggleLand(FarmLandSlot.Pond);
            Call(window, "WatchFarmChores", Frame);
            check(!pond.Borrowed && !pond.Showing && window.FarmChoreNow == FarmChoreKind.None, "A click on its board ends the job");
            Put();
            chores.ScheduleFishAgain(0);

            window.FarmSetAllLands(true);
            Put();
            Free();
        }
        void Clear(PetFarm field) { for (int i = 0; i < PetFarm.TileCount; i++) { field.Tiles[i].Stage = FarmStage.Wild; field.Tiles[i].Crop = null; field.Tiles[i].GrowsUntil = null; field.Tiles[i].LastCrop = null; } }

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
            field.OpenTiles = 6;
            chores.ScheduleVisit(100000); chores.ScheduleBird(100000); chores.ScheduleFish(100000); // Only the trips asked for here.
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
            check(pet.FarmTool == FarmToolPose.Harvest && pet.FarmToolCrop == FarmCrop.Carrot && pet.FarmToolLength == FarmChores.HarvestSeconds, "The cat is shown pulling up that crop");
            int before = field.HarvestedCount(FarmCrop.Carrot);
            Features(FarmChores.HarvestSeconds + 0.1);
            check(window.FarmChorePhaseNow == FarmChorePhase.Carry && window.FarmChoreNow == FarmChoreKind.Harvest && field.StockOf(FarmCrop.Carrot) > 0, "It goes off to the basket with it, already in the store");
            Features(Frame);
            check(pet.FarmTool == FarmToolPose.Carry && pet.FarmToolCrop == FarmCrop.Carrot && pet.Action == PetAction.Walking, "It walks there holding the crop");
            pet.X = window.FarmChoreSpot;
            Features(Frame * 2);
            check(window.FarmChorePhaseNow == FarmChorePhase.Drop && pet.FarmTool == FarmToolPose.Drop && pet.VX == 0, "At the basket it lets go");
            Features(FarmChores.DropHit + Frame);
            check(Field<double>(window, "farmBasketAge") >= 0, "The basket bounces");
            Features(FarmChores.DropSeconds);
            check(pet.FarmTool == FarmToolPose.None && pet.FarmToolCrop == null && pet.PlayCrouch == 0 && !Field<bool>(window, "farmCropInPaws"), "When the job ends nothing stays in its paws");
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
            check(window.FarmChoreNow == FarmChoreKind.Water && chores.VisitDue >= FarmChores.RestMinSeconds, "When it is due and free, the cat goes");
            double wait = chores.VisitDue;
            Call(window, "WatchFarmChores", 1.0);
            check(chores.VisitDue == wait, "The rest does not count down during the trip");
            Free();

            // Tilling and sowing again, by the real trip.
            Clear(field);
            chores.ScheduleVisit(100000);
            field.Coins = 50;
            pet.X = (double)Call(window, "FarmTileMid", 4)! - 200;
            field.Tiles[4] = new FarmTile { LastCrop = FarmCrop.Carrot };
            void Arrive() { pet.X = window.FarmChoreSpot; Features(Frame * 2); }
            prefs.FarmResow = false;
            check(!window.TryFarmVisit(), "With sowing again off a harvested tile gets no trip");
            prefs.FarmResow = true;
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Till && window.FarmChoreTile == 4, "The cat goes to till it");
            Arrive();
            check(window.FarmChorePhaseNow == FarmChorePhase.Work && pet.BatRemaining > 0 && field.Next(4) == FarmStep.Till, "It paws at the soil");
            Features(FarmChores.TillHit);
            check(field.Next(4) == FarmStep.Plant && field.Tiles[4].LastCrop == FarmCrop.Carrot, "The soil is tilled and the tile still remembers");
            Features(FarmChores.TillSeconds);
            check(window.FarmChoreNow == FarmChoreKind.None && pet.Action == PetAction.Idle, "That trip ends");
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Sow, "The cat goes to sow");
            Arrive();
            int coins = field.Coins;
            Features(FarmChores.SowHit);
            check(field.Tiles[4].Crop == FarmCrop.Carrot && field.Coins == coins - 4 && field.Next(4) == FarmStep.Water, "It sows the same crop and pays");
            check(PetFarm.Load(path).Tiles[4].Crop == FarmCrop.Carrot, "...and it is saved");
            Features(FarmChores.SowSeconds);
            Free();

            // A tile tilled by hand while the cat is on its way is the user's.
            Clear(field);
            field.Tiles[1] = new FarmTile { LastCrop = FarmCrop.Radish };
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Till, "A trip to till");
            window.FarmClick(1);
            Features(Frame);
            check(window.FarmChoreNow == FarmChoreKind.None && field.Tiles[1].LastCrop == null, "Tilled by hand, the trip is off and the tile is the user's");
            check(!window.TryFarmVisit(), "...and the cat does not sow it");
            Free();

            // The cat's own sounds are spaced out; a click always sounds.
            Clear(field);
            SetField(window, "farmChoreSoundWait", 0.0);
            SetField(window, "farmCatHands", true);
            Call(window, "PlayFarmSound", "pop");
            double gap = Field<double>(window, "farmChoreSoundWait");
            check(gap == FarmChores.SoundGapSeconds, "The cat's sound starts the wait");
            Call(window, "PlayFarmSound", "pop");
            check(Field<double>(window, "farmChoreSoundWait") == gap, "...and the next is held back");
            SetField(window, "farmCatHands", false);
            Call(window, "PlayFarmSound", "pop");
            check(Field<double>(window, "farmChoreSoundWait") == gap, "A user's click does not touch the wait");
            Call(window, "WatchFarmChores", 2.0);
            check(Field<double>(window, "farmChoreSoundWait") == gap - 2, "The wait runs down");
            SetField(window, "farmChoreSoundWait", 0.0);
            Free();

            Hearth();
            Pond();
            Folded();

            // A better can waters the tiles beside this one, on the same trip.
            Free(); Clear(field);
            field.CanTier = 2;
            Thirsty(field, 0); Thirsty(field, 1); Thirsty(field, 2); Thirsty(field, 3);
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Water, "(off to water)");
            int watering = window.FarmChoreTile;
            pet.X = window.FarmChoreSpot;
            Features(Frame * 2);
            Features(FarmChores.WaterHit + Frame);
            check(pet.FarmToolTier == 2 && field.TilesAt(FarmStep.Water).Count == 1 && field.Next(watering) == FarmStep.Wait, "A gold can waters two more tiles");
            Features(FarmChores.WaterSeconds);
            check(pet.FarmToolTier == 0, "When it is over the tool is put away");
            field.CanTier = 0;

            // Now and then it goes to play with the hens.
            Free(); Clear(field);
            field.Hens = 2;
            Call(window, "CreateFarmPatchWindows");
            Call(window, "SettleFarmPatches");
            Call(window, "ShowFarmPatches");
            chores.ScheduleHen(0); chores.ScheduleFish(9999); chores.ScheduleCook(9999);
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Hen, "With a coop and nothing to do on the field it goes to the hens");
            check(chores.HenDue >= FarmChores.HenMinSeconds, "...and not again for a while");
            pet.X = window.FarmChoreSpot;
            Features(Frame * 2);
            check(window.FarmChorePhaseNow == FarmChorePhase.Work && pet.Action == PetAction.Sitting, "There it crouches");
            Features(FarmChores.HenHit + Frame);
            check(Field<double>(window, "farmHenScatter") >= 0 && pet.BatRemaining > 0, "It pounces and they scatter");
            Features(FarmChores.HenSeconds);
            check(window.FarmChoreNow == FarmChoreKind.None && pet.PlayCrouch == 0, "Then it is over");

            // Eggs in the nest: the visit picks them up instead (feedback 13/11).
            Free(); Clear(field);
            int stock = field.EggStock, xp = field.Xp;
            field.EggsInCoop = 2;
            SetField(window, "farmHenScatter", -1.0); // The last pounce's flutter is over.
            chores.ScheduleHen(0);
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Eggs, "With eggs in the nest the cat goes to collect them");
            pet.X = window.FarmChoreSpot;
            Features(Frame * 2);
            check(window.FarmChorePhaseNow == FarmChorePhase.Work && pet.Action == PetAction.Idle, "There it bends down to the nest");
            Features(FarmChores.EggsHit + Frame);
            check(field.EggsInCoop == 0 && field.EggStock == stock + 2 && field.Xp == xp + 2 * PetFarm.EggXp, "The eggs go to the store, with their XP");
            check(Field<double>(window, "farmHenScatter") < 0, "...without scaring the hens");
            Features(FarmChores.EggsSeconds);
            check(window.FarmChoreNow == FarmChoreKind.None && pet.PlayCrouch == 0, "Then it is over");
            Free(); Clear(field);
            field.EggsInCoop = 1;
            chores.ScheduleHen(0);
            check(window.TryFarmVisit() && window.FarmChoreNow == FarmChoreKind.Eggs, "Again with one egg");
            field.CollectEggs();
            Features(Frame * 2);
            check(window.FarmChoreNow == FarmChoreKind.None, "Picked up by hand on the way: the cat stops");
            field.EggStock = stock;
            Free(); Clear(field);
            chores.ScheduleHen(0);
            prefs.Quiet = true;
            Features(Frame);
            check(!window.TryFarmVisit() || window.FarmChoreNow != FarmChoreKind.Hen, "In Quiet it leaves them alone");
            prefs.Quiet = false;
            Free();
            Application.Current.Windows.OfType<Window>().First(w => w.Title == "Pet farm coop").Close();
            window.FarmCoop.Monitor = null;
            field.Hens = 0;
            chores.ScheduleHen(FarmChores.HenMinSeconds);
        }
        finally
        {
            Free();
            Call(window, "CloseFarm");
            prefs.Farm = false;
            prefs.FarmHelper = true;
            prefs.FarmAutoCook = true;
            prefs.FarmAutoFish = true;
            prefs.Quiet = quiet;
            SetField(window, "dragging", false);
            SetField(window, "pressPending", false);
            SetField(window, "paused", false);
            SetField(window, "farmClock", null);
            SetField(window, "farmPath", PetFarm.DefaultPath);
            SetField(window, "farm", null);
            chores.ScheduleVisit(FarmChores.FirstVisitSeconds); chores.ScheduleBird(FarmChores.BirdMinSeconds); chores.ScheduleCook(FarmChores.CookMinSeconds); chores.ScheduleFish(FarmChores.FishMinSeconds); chores.ScheduleFishAgain(0);
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
            void Tool(FarmToolPose tool, double time, double hit, double length, string name, int facing = 1) => Cat(m =>
            {
                m.FarmTool = tool; m.FarmToolTime = time; m.FarmToolHit = hit; m.FarmToolLength = length; m.FarmToolCrop = FarmCrop.Carrot; m.Facing = facing;
            }, back, $"chore-tool-{name}-{tone}");
            Tool(FarmToolPose.Till, 0.4, FarmChores.TillHit, FarmChores.TillSeconds, "till-up");
            Tool(FarmToolPose.Till, 0.75, FarmChores.TillHit, FarmChores.TillSeconds, "till-down");
            Tool(FarmToolPose.Sow, 0.5, FarmChores.SowHit, FarmChores.SowSeconds, "sow");
            Tool(FarmToolPose.Harvest, 0.2, FarmChores.HarvestHit, FarmChores.HarvestSeconds, "harvest-reach");
            Tool(FarmToolPose.Harvest, 0.95, FarmChores.HarvestHit, FarmChores.HarvestSeconds, "harvest-up");
            Tool(FarmToolPose.Harvest, 0.95, FarmChores.HarvestHit, FarmChores.HarvestSeconds, "harvest-up-left", -1);
            Tool(FarmToolPose.Pour, 0.6, FarmChores.PourHit, FarmChores.PourSeconds, "pour");
            Tool(FarmToolPose.Pounce, 0.7, FarmChores.PounceHit, FarmChores.PounceSeconds, "pounce");
            Tool(FarmToolPose.Carry, 0.5, 0, 1000, "carry");
            Cat(m => { m.FarmTool = FarmToolPose.Carry; m.FarmToolTime = 0.5; m.FarmToolLength = 1000; m.FarmToolCrop = FarmCrop.Carrot; m.FarmToolGold = true; m.FarmToolHold = true; }, back, $"chore-tool-carry-gold-{tone}");
            Tool(FarmToolPose.Drop, 0.15, FarmChores.DropHit, FarmChores.DropSeconds, "drop");
            Plot(a => { a.BasketFill = 3; a.BasketBounce = 0.3; }, back, $"chore-basket-full-{tone}");
            Plot(a => { a.BasketFill = 0; }, back, $"chore-basket-empty-{tone}");
            Cat(m => { m.WateringRemaining = 1; m.FarmToolTier = 2; }, back, $"chore-water-gold-{tone}");
            Cat(m => { m.FarmTool = FarmToolPose.Till; m.FarmToolTime = 0.4; m.FarmToolHit = FarmChores.TillHit; m.FarmToolLength = FarmChores.TillSeconds; m.FarmToolTier = 1; }, back, $"chore-till-silver-{tone}");
            Cat(m => { m.StirringRemaining = 3.2; }, back, $"chore-tool-stir-sweat-{tone}");
            Cat(m => { m.FishingTime = 5.2; m.FishingBiteAt = 5; }, back, $"chore-tool-fish-bite-{tone}");
            Cat(m => { m.FishingTime = 6.2; m.FishingBiteAt = 5; }, back, $"chore-tool-fish-cheer-{tone}");
        }
        string Idle = Cat(_ => { }, dark, null);
        foreach (var tool in new[] { FarmToolPose.Till, FarmToolPose.Sow, FarmToolPose.Harvest, FarmToolPose.Pour })
            check(Cat(m => { m.FarmTool = tool; m.FarmToolTime = 0.28; m.FarmToolHit = 0.3; m.FarmToolLength = 1; }, dark, null) != Idle, $"{tool} shows in the cat's paws");
        check(Cat(m => { m.FarmTool = FarmToolPose.Till; m.FarmToolTime = 0.28; m.FarmToolHit = 0.3; m.FarmToolLength = 1; m.Action = PetAction.Dragging; }, dark, null) == Cat(m => m.Action = PetAction.Dragging, dark, null), "Picked up, it holds no tool");
        string Land(FarmPatchKind kind, Action<PetFarmArt.Land> set, Color back, string? name)
        {
            var art = new PetFarmArt.Land(kind);
            set(art);
            int width = (int)art.Width, height = (int)PetFarmArt.ArtHeight;
            art.Measure(new Size(width, height)); art.Arrange(new Rect(0, 0, width, height));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual); bitmap.Render(art);
            if (name != null) Save(bitmap, name);
            return Hash(bitmap);
        }
        foreach (var (back, tone) in new[] { (light, "light"), (dark, "dark") })
        {
            var host = new System.Windows.Controls.Grid { Width = 180, Height = 130, Background = new SolidColorBrush(back) };
            host.Children.Add(new PetFarmVisitorArt { Time = 0.4, HorizontalAlignment = HorizontalAlignment.Center });
            host.Measure(new Size(180, 130)); host.Arrange(new Rect(0, 0, 180, 130));
            var visitorShot = new RenderTargetBitmap(180, 130, 96, 96, PixelFormats.Pbgra32);
            visitorShot.Render(host);
            Save(visitorShot, $"visitor-{tone}");
            foreach (var decor in PetFarm.GiftDecors)
            {
                var iconHost = new System.Windows.Controls.Grid { Width = 60, Height = 60, Background = new SolidColorBrush(back) };
                iconHost.Children.Add(new FarmDecorIcon(decor, 60));
                iconHost.Measure(new Size(60, 60)); iconHost.Arrange(new Rect(0, 0, 60, 60));
                var iconShot = new RenderTargetBitmap(60, 60, 96, 96, PixelFormats.Pbgra32);
                iconShot.Render(iconHost);
                Save(iconShot, $"gift-{decor}-{tone}");
            }
        }
        foreach (var season in Enum.GetValues<FarmSeason>())
        {
            var was = PetFarmArt.CurrentSeason;
            PetFarmArt.CurrentSeason = season;
            var crop = new[] { FarmCrop.Strawberry, FarmCrop.Corn, FarmCrop.SweetPotato, FarmCrop.NapaCabbage }[(int)season];
            Plot(a => { for (int i = 0; i < 3; i++) { a.Tiles[i] = new(FarmStage.Ripe, crop, false, false, 1); a.Tiles[i + 3] = new(FarmStage.Young, crop, true, false, 0.5); } }, light, $"season-{season}");
            PetFarmArt.CurrentSeason = was;
        }
        foreach (var (back, tone) in new[] { (light, "light"), (dark, "dark") })
            Land(FarmPatchKind.Coop, a => { a.Hens = 3; a.NestEggs = 2; a.HenTime = 1.3; }, back, $"coop-{tone}");
        check(Land(FarmPatchKind.Coop, a => { a.Hens = 3; a.HenTime = 1.3; a.HenScatter = 0.5; }, dark, "coop-scatter") != Land(FarmPatchKind.Coop, a => { a.Hens = 3; a.HenTime = 1.3; }, dark, null), "The hens scatter visibly");
        // The land, and the cat beside it as the two windows place them.
        void Scene(FarmPatchKind kind, Action<PetFarmArt.Land> set, int side, double gap, Action<PetModel> cat, Color back, string name)
        {
            const int margin = 170;
            var art = new PetFarmArt.Land(kind);
            set(art);
            int width = (int)art.Width + margin * 2, height = (int)PetFarmArt.ArtHeight;
            art.Measure(new Size(art.Width, height)); art.Arrange(new Rect(margin, 0, art.Width, height));
            var model = new PetModel { Action = PetAction.Sitting, Grounded = true, Facing = -side, AnimationTime = 1, ActionTime = 1 };
            cat(model);
            var pet = new PetRenderer(model);
            pet.Measure(new Size(180, 180));
            pet.Arrange(new Rect(margin + art.Width / 2 + side * gap - 56 - PickupAnimation.Padding, PetFarmArt.Floor - 112 - PickupAnimation.Padding, 180, 180));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(back), null, new Rect(0, 0, width, height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual); bitmap.Render(art); bitmap.Render(pet);
            Save(bitmap, name);
        }
        foreach (var (back, tone) in new[] { (light, "light"), (dark, "dark") })
        {
            Land(FarmPatchKind.Hearth, _ => { }, back, $"hearth-idle-{tone}");
            Land(FarmPatchKind.Hearth, a => { a.PourDish = FarmDish.StuffedTomato; a.PourAge = 0.3; a.HearthFire = 0.6; a.HearthTime = 1; }, back, $"hearth-pour-{tone}");
            Land(FarmPatchKind.Hearth, a => { a.PotDish = FarmDish.CarrotStew; a.PourDish = FarmDish.CarrotStew; a.PourAge = 0.7; a.HearthFire = 0.9; a.HearthTime = 1; }, back, $"hearth-splash-{tone}");
            Land(FarmPatchKind.Hearth, a => a.PotDish = FarmDish.RadishSoup, back, $"hearth-waiting-{tone}");
            Land(FarmPatchKind.Pond, _ => { }, back, $"pond-idle-{tone}");
            Land(FarmPatchKind.Hearth, a => { a.Lift = FarmLift.FloatHeight; }, back, $"hearth-floating-{tone}");
            Land(FarmPatchKind.Pond, a => { a.Lift = FarmLift.FloatHeight; a.Shed = 1.5; }, back, $"pond-floating-{tone}");
            Land(FarmPatchKind.Pond, a => { a.Dustiness = 0.8; a.Squash = 0.7; a.ShakeX = 2; }, back, $"pond-landing-{tone}");
            foreach (int side in new[] { -1, 1 })
            {
                Scene(FarmPatchKind.Hearth, a => { a.PotDish = FarmDish.CarrotStew; a.Stirring = true; a.HearthFire = 1; a.HearthTime = 1.4; }, side, 58, m => m.StirringRemaining = 4, back, $"hearth-stir-{(side < 0 ? "left" : "right")}-{tone}");
                Scene(FarmPatchKind.Hearth, a => { a.PourDish = FarmDish.CarrotStew; a.PourAge = 0.5; }, side, 58, m => { m.FarmTool = FarmToolPose.Pour; m.FarmToolTime = 0.6; m.FarmToolHit = FarmChores.PourHit; m.FarmToolLength = FarmChores.PourSeconds; }, back, $"hearth-pour-{(side < 0 ? "left" : "right")}-{tone}");
                foreach (var (time, stage) in new[] { (3.0, "wait"), (6.3, "bite"), (7.2, "reel") })
                    Scene(FarmPatchKind.Pond, a => { a.Fishing = true; a.FishSide = side; a.HearthTime = time; a.CaughtFish = time > 6 ? FarmFish.Carp : null; a.CaughtAge = time > 6 ? (time - 6) / 1.4 : 0; }, side, 100, m => { m.FishingTime = time; m.FishingBiteAt = 6; }, back, $"pond-fish-{stage}-{(side < 0 ? "left" : "right")}-{tone}");
            }
        }
        foreach (var kind in Enum.GetValues<FarmFish>())
            Land(FarmPatchKind.Pond, a => { a.Fishing = true; a.HearthTime = 1; a.CaughtFish = kind; a.CaughtAge = 0.45; }, dark, $"pond-catch-{kind}-dark");
        string idle = Land(FarmPatchKind.Hearth, _ => { }, dark, null);
        check(idle != Land(FarmPatchKind.Hearth, a => a.HearthFire = 1, dark, null), "The fire is drawn");
        string ablaze = Land(FarmPatchKind.Hearth, a => { a.HearthFire = 1; a.HearthTime = 0.2; }, dark, null);
        check(ablaze != Land(FarmPatchKind.Hearth, a => { a.HearthFire = 1; a.HearthTime = 0.5; }, dark, null), "It flickers");
        check(idle != Land(FarmPatchKind.Hearth, a => a.PotDish = FarmDish.RadishSoup, dark, null), "The pot shows what it holds");
        check(idle != Land(FarmPatchKind.Hearth, a => a.Stirring = true, dark, null), "The stick leaves the pot while the cat holds it");
        check(idle != Land(FarmPatchKind.Hearth, a => { a.PourDish = FarmDish.RadishSoup; a.PourAge = 0.2; }, dark, null), "Ingredients fall in");
        check(idle != Land(FarmPatchKind.Hearth, a => a.Lift = FarmLift.FloatHeight, dark, null), "Land in the air is drawn in the air");
        check(idle != Land(FarmPatchKind.Hearth, a => a.Shed = 1, dark, null), "It sheds crumbs");
        check(idle != Land(FarmPatchKind.Hearth, a => a.Dustiness = 0.6, dark, null), "It lands with dirt");
        string still = Land(FarmPatchKind.Pond, _ => { }, dark, null);
        check(still != Land(FarmPatchKind.Pond, a => { a.Fishing = true; a.HearthTime = 1; }, dark, null), "The float makes ripples");
        check(still != Land(FarmPatchKind.Pond, a => { a.CaughtFish = FarmFish.Carp; a.CaughtAge = 0.5; }, dark, null), "A fish jumps");
        check(Land(FarmPatchKind.Pond, a => { a.CaughtFish = FarmFish.Minnow; a.CaughtAge = 0.5; }, dark, null) != Land(FarmPatchKind.Pond, a => { a.CaughtFish = FarmFish.Goldfish; a.CaughtAge = 0.5; }, dark, null), "Each fish looks its own");
        string plain = Plot(_ => { }, dark, null);
        check(plain != Plot(a => { a.BirdTile = 1; a.BirdTime = 1; }, dark, null), "A bird is drawn");
        string resting = Plot(a => { a.BirdTile = 1; a.BirdTime = 1.5; }, dark, null);
        check(resting != Plot(a => { a.BirdTile = 1; a.BirdTime = 0.3; }, dark, null), "It pecks");
        check(resting != Plot(a => { a.BirdTile = 1; a.BirdTime = 1.5; a.BirdFlying = true; a.BirdAir = 0.5; }, dark, null), "It flies");
        check(Plot(a => { a.BirdTile = 1; a.BirdAir = 1; a.BirdFlying = true; }, dark, null) == plain, "Up and away it has faded out");
        string sitting = Cat(_ => { }, dark, null);
        check(sitting != Cat(m => m.WateringRemaining = 1, dark, null), "The cat holds a watering can");
        check(sitting != Cat(m => m.StirringRemaining = 3, dark, null), "The cat holds a stick");
        check(sitting != Cat(m => { m.FishingTime = 3; m.FishingBiteAt = 6; }, dark, null), "The cat holds a rod");
        string asleep = Cat(m => { m.Action = PetAction.Sleeping; m.Emotions.EnterSleep(); }, dark, null);
        check(asleep == Cat(m => { m.FishingTime = 3; m.FishingBiteAt = 6; m.Action = PetAction.Sleeping; m.Emotions.EnterSleep(); }, dark, null), "A sleeping cat never holds the rod");
        check(asleep == Cat(m => { m.StirringRemaining = 3; m.Action = PetAction.Sleeping; m.Emotions.EnterSleep(); }, dark, null), "A sleeping cat never holds the stick");
        check(asleep == Cat(m => { m.WateringRemaining = 1; m.Action = PetAction.Sleeping; m.Emotions.EnterSleep(); }, dark, null), "A sleeping cat never holds the can");
    }
}
