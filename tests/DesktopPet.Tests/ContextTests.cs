using DesktopPet;
using DesktopPet.AI;

internal static class ContextTests
{
    public static void Run(Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "DesktopPetContextTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string file = Path.Combine(folder, "preferences.json");
            File.WriteAllText(file, "{\"Coat\":\"Purple\",\"Accessory\":\"Cape\",\"BreakMinutes\":7,\"BreakReminders\":true}");
            var loaded = PetPreferences.Load(file);
            check(loaded.Coat == "Orange" && loaded.Accessory == "None" && loaded.BreakMinutes == 45 && loaded.BreakReminders,
                "Unknown coat, accessory and break interval fall back to defaults");
            File.WriteAllText(file, "{\"Coat\":\"Calico\",\"Accessory\":\"Hat\",\"BreakMinutes\":30}");
            loaded = PetPreferences.Load(file);
            check(loaded.Coat == "Calico" && loaded.Accessory == "Hat" && loaded.BreakMinutes == 30 && !loaded.BreakReminders,
                "Saved appearance and break interval load back; reminders stay off unless enabled");
            File.WriteAllText(file, "{\"BattleTutorialSeen\":true,\"BattleLessonsSeen\":[\"lift\"],\"Coat\":\"Calico\"}");
            check(PetPreferences.Load(file).Coat == "Calico", "Preferences from before the Adventure Guide (the old guide's keys) still load");
            File.WriteAllText(file, "{\"Birthday\":\"02-30\"}");
            check(PetPreferences.Load(file).Birthday == "", "An impossible birthday is dropped when preferences load");
            File.WriteAllText(file, "{\"Birthday\":\"02-29\"}");
            check(PetPreferences.Load(file).Birthday == "02-29" && PetPreferences.NormalizeBirthday("1-5") == "" &&
                  PetPreferences.NormalizeBirthday("12-31") == "12-31" && PetPreferences.NormalizeBirthday(null) == "",
                "Birthdays are kept only as valid MM-dd (29 February allowed)");
            check(new PetPreferences() is { Coat: "Orange", Accessory: "None", BreakReminders: false, BreakMinutes: 45, Birthday: "" },
                "Defaults keep the original orange cat and no reminders");
        }
        finally { Directory.Delete(folder, true); }

        // Coats come from the embedded coats.json: same ids, order and colors as the old built-in list.
        check(PetPreferences.Coats.SequenceEqual(new[] { "Orange", "Gray", "Black", "White", "Calico", "Tabby", "GingerTabby", "Tuxedo", "RussianBlue", "Siamese" }),
            "coats.json lists the 10 original coats in the original settings order");
        var siamese = PetCoats.All.Single(coat => coat.Id == "Siamese");
        check(PetCoats.All[0] == PetCoats.Fallback && siamese is { Points: "#8A6A55", Iris: "#6FB3F2", Ink: "#3B2B24", NameVi: "Xiêm" } &&
              PetCoats.All.Single(coat => coat.Id == "Calico") is { Markings: CoatMarkings.Calico, PatchB: "#45403F" } &&
              PetCoats.All.Single(coat => coat.Id == "Tuxedo").Paw == "#FFFFFF",
            "Coat colors, markings, points and paws load from JSON; Orange keeps the original artwork values");
        CoatSpec[]? Parse(string json) { try { return PetCoats.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))); } catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return null; } }
        check(Parse("{\"coats\":[{\"id\":\"Mint\",\"fur\":\"#AAFFCC\",\"cream\":\"#FFFFFF\",\"pink\":\"#EE9999\"}]}") is [{ Id: "Mint", NameEn: "Mint", Ink: "#49372E" }] &&
              Parse("{\"coats\":[{\"id\":\"Bad\",\"fur\":\"orange\",\"cream\":\"#FFFFFF\",\"pink\":\"#EE9999\"}]}") == null &&
              Parse("{\"coats\":[{\"id\":\"Stripe\",\"fur\":\"#AAAAAA\",\"cream\":\"#FFFFFF\",\"pink\":\"#EE9999\",\"markings\":\"Tabby\"}]}") == null &&
              Parse("{\"coats\":[]}") == null && Parse("{ broken") == null,
            "A coat file with a bad color, missing patch colors, no coats or broken JSON is rejected as a whole");
        check(PetCoats.All.All(coat => coat.NameEn.Length > 0 && coat.NameVi.Length > 0 && coat.NameVi != coat.Id),
            "Every coat has Vietnamese and English names for settings");

        // Accessories come from the embedded accessories.json; the original seven keep their ids and order.
        check(PetPreferences.Accessories.SequenceEqual(new[] { "None", "Bell", "Bow", "Hat", "Scarf", "Glasses", "Flower", "Crown",
                "SantaHat", "WitchHat", "Apricot", "Lantern", "SlimeHat", "SlimeKingCrown", "ExpeditionScarf", "InfinityCollar", "StarHalo", "GalaxyCape" }),
            "accessories.json lists None, the seven original accessories, four holiday ones, two Adventure rewards, the Expedition scarf and Infinity's three in order");
        check(PetAccessories.ForEvent("christmas")?.Id == "SantaHat" && PetAccessories.ForEvent("halloween")?.Id == "WitchHat" &&
              PetAccessories.ForEvent("tet")?.Id == "Apricot" && PetAccessories.ForEvent("mid_autumn")?.Id == "Lantern" &&
              PetAccessories.ForEvent("birthday")?.Id == "Hat" && PetAccessories.ForEvent("anniversary") == null && PetAccessories.ForEvent(null) == null,
            "Each holiday (and the user's birthday) has its accessory");
        check(PetAccessories.Find("Bell") is { Layer: AccessoryLayer.Neck, Shapes.Length: 4 } &&
              PetAccessories.Find("Glasses")!.Shapes[0] is { Kind: ShapeKind.Ellipse, Fill: "#40FFFFFF", Stroke: { Kind: StrokeKind.Custom, Width: 2, Round: true, RoundJoin: true } } &&
              PetAccessories.All.All(item => item.NameVi.Length > 0 && item.NameEn.Length > 0 && item.NameVi != item.Id),
            "Accessory layers, shapes, pens and names load from JSON");
        AccessorySpec[]? Accessories(string json)
        {
            try { return PetAccessories.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))); }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { return null; }
        }
        string One(string shape, string layer = "head") => "{\"accessories\":[{\"id\":\"Star\",\"layer\":\"" + layer + "\",\"shapes\":[" + shape + "]}]}";
        check(Accessories(One("{\"ellipse\":[55,10,3,3],\"fill\":\"#FFD45C\",\"stroke\":\"outline\"}")) is [{ Id: "Star", NameEn: "Star", Event: null }] &&
              Accessories(One("{\"ellipse\":[55,10,3,3],\"fill\":\"gold\"}")) == null &&
              Accessories(One("{\"ellipse\":[55,10,3]}")) == null &&
              Accessories(One("{\"line\":[1,2,3,4]}")) == null &&
              Accessories(One("{\"path\":\"M 0,0 L 5,5 <script>\",\"stroke\":\"outline\"}")) == null &&
              Accessories(One("{\"ellipse\":[55,10,3,3],\"fill\":\"#FFFFFF\"}", "tail")) == null &&
              Accessories(One("{\"ellipse\":[999,10,3,3],\"fill\":\"#FFFFFF\"}")) == null &&
              Accessories("{\"accessories\":[{\"id\":\"None\",\"layer\":\"head\",\"shapes\":[{\"ellipse\":[1,1,1,1],\"fill\":\"#FFFFFF\"}]}]}") == null,
            "An accessory file with bad colors, shapes, layers, ranges or ids is rejected as a whole");
        check(new PetPreferences().HolidayAccessories, "Dressing up for holidays is on by default");
        const string reply = "{\"version\":1,\"id\":3,\"type\":\"decision\",\"command\":\"IDLE\",\"duration_seconds\":3,\"mood\":\"content\"";
        check(AiProtocol.Parse(reply + ",\"special_day\":\"christmas\"}", 3).SpecialDay == "christmas" &&
              AiProtocol.Parse(reply + ",\"special_day\":\"Bad Day!\"}", 3).SpecialDay == null && AiProtocol.Parse(reply + "}", 3).SpecialDay == null,
            "A reply's special_day is read when well formed and otherwise ignored");

        var breaks = new PetBreaks();
        breaks.Step(4000, 0, true);
        check(!breaks.Due && breaks.ActiveSeconds == 0, "Disabled break reminders never count or fire");
        breaks.Configure(true, 30);
        for (int i = 0; i < 29 * 60; i++) breaks.Step(1, 10, true);
        check(!breaks.Due, "Not due before the interval");
        breaks.Step(200, PetBreaks.AwayResets, true);
        check(breaks.ActiveSeconds == 0, "Being away five minutes counts as the break and restarts the clock");
        for (int i = 0; i < 30 * 60; i++) breaks.Step(1, 60, false);
        check(breaks.Due && breaks.DueFor == 0 && !breaks.NeedsLocalLine, "Due after 30 active minutes but waits while the pet cannot speak");
        for (int i = 0; i < 14; i++) breaks.Step(1, 1, true);
        check(!breaks.NeedsLocalLine, "Python gets a chance to word the reminder first");
        breaks.Step(1, 1, true);
        check(breaks.NeedsLocalLine, "A built-in line is used when Python stays silent (AI off)");
        breaks.Delivered();
        check(!breaks.Due && breaks.ActiveSeconds == 0, "Delivering the reminder restarts the clock");
        breaks.Step(100, 1, true);
        breaks.Configure(true, 60);
        check(breaks.ActiveSeconds == 0 && breaks.IntervalSeconds == 3600, "Changing the interval restarts the clock");
        var (id, text) = PetBreaks.LocalLine("en", "Bơ", new Random(1));
        var (viId, viText) = PetBreaks.LocalLine("vi", "", new Random(2));
        check(PetBreaks.IsBreakLine(id) && PetBreaks.IsBreakLine(viId) && !text.Contains("{name}") && !viText.Contains("{name}") &&
              id.Contains(":en:") && viId.Contains(":vi:") && !PetBreaks.IsBreakLine("hello:vi:1"),
            "Built-in reminder lines follow the language and never show placeholders");
        var (jaId, jaText) = PetBreaks.LocalLine("ja", "Mochi", new Random(3));
        check(jaId.Contains(":ja:") && jaText.Any(c => c >= '぀' && c <= 'ヿ') && !jaText.Contains("{name}"),
            "Japanese has its own built-in reminder lines");
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        using (var lines = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "ai", "lines.json"))))
            foreach (string language in new[] { PetLanguage.Vietnamese, PetLanguage.English, PetLanguage.Japanese })
            {
                var python = lines.RootElement.GetProperty("break").GetProperty(language).EnumerateArray().Select(line => line.GetString()).ToArray();
                var local = PetBreaks.Lines(language);
                check(python.Length >= local.Length && local.SequenceEqual(python.Take(local.Length)),
                    $"Built-in reminder lines ({language}) are the first break lines of ai/lines.json");
            }

        var context = new PetContext();
        var change = context.Update(false, PetContext.AwayAfter - 1);
        check(!context.Away && !change.WentAway, "Under five minutes without input is not away");
        change = context.Update(false, PetContext.AwayAfter);
        var again = context.Update(false, PetContext.AwayAfter + 60);
        check(context.Away && change.WentAway && !again.WentAway, "Going away is reported once");
        change = context.Update(false, 0.5);
        check(!context.Away && change.CameBack && context.Returned, "Fresh input after being away means the user is back");
        check(context.TakeReturned() && !context.TakeReturned(), "The return is reported to the AI only once");
        change = context.Update(true, 1);
        var still = context.Update(true, 2);
        check(context.Fullscreen && change.FullscreenChanged && !still.FullscreenChanged && !change.CameBack, "Entering fullscreen is reported once");
        check(context.Update(false, double.NaN).FullscreenChanged && context.IdleSeconds == 0, "Leaving fullscreen is reported; invalid idle time is ignored");

        var settings = new PetSettings();
        var behavior = new PetBehavior(settings, new Random(3));
        var pet = new PetModel { Action = PetAction.Walking, Grounded = true, VX = 50 };
        check(behavior.StartAwayNap(pet) && pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && pet.VX == 0 && behavior.HoldSleep,
            "Away nap puts the pet to sleep");
        for (int i = 0; i < 240 * 60; i++) { pet.Emotions.Step(pet, 1.0 / 60); behavior.Step(pet, 1.0 / 60); } // Longer than the sad-episode clock.
        check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepElapsed == 0, "The away nap's sleep clock is held while the user is gone");
        behavior.EndAwayNap(pet);
        check(pet.Action == PetAction.Waking && !behavior.HoldSleep && pet.Emotions.WakeCooldownRemaining == settings.WakeCooldownSeconds,
            "The returning user wakes the pet with the normal waking animation and cooldown");
        pet.Emotions.UserReturned();
        bool sad = false;
        for (int i = 0; i < 90; i++) { pet.Emotions.Step(pet, 1.0 / 60); behavior.Step(pet, 1.0 / 60); sad |= pet.Emotions.Target == PetEmotion.Sad; }
        check(pet.Action == PetAction.Idle, "Waking from the away nap ends in Idle");
        for (int i = 0; i < 100; i++) { pet.Emotions.Step(pet, 0.1); pet.Action = PetAction.Idle; sad |= pet.Emotions.Target == PetEmotion.Sad; }
        check(!sad, "Coming back is not greeted with an instant sad episode");
        pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        behavior.StartAwayNap(pet);
        behavior.WakeByClick(pet);
        check(pet.Action == PetAction.Waking && !behavior.HoldSleep, "A click still wakes the pet from the away nap at once");
        pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        for (int i = 0; i < 11 * 60; i++) { pet.Emotions.Step(pet, 1.0 / 60); behavior.Step(pet, 1.0 / 60); }
        check(pet.Action is PetAction.Waking or PetAction.Idle, "Ordinary sleep still wakes by itself after its duration");
        var flying = new PetModel { Action = PetAction.Thrown, Grounded = false };
        check(!behavior.StartAwayNap(flying) && flying.Action == PetAction.Thrown, "No away nap while the pet is in the air");

        pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        check(behavior.StartStretch(pet) && pet.Action == PetAction.Stretching, "Break reminder starts a stretch from a free pose");
        for (int i = 0; i < 60; i++) behavior.Step(pet, 1.0 / 60);
        check(pet.Action == PetAction.Stretching, "The stretch lasts its full duration");
        for (int i = 0; i < 120; i++) behavior.Step(pet, 1.0 / 60);
        check(pet.Action == PetAction.Idle, "The stretch ends back in Idle");
        behavior.Quiet = true;
        check(!behavior.StartStretch(pet), "Quiet mode (or a fullscreen app) skips the stretch");
        behavior.Quiet = false;
        var asleep = new PetModel { Action = PetAction.Sleeping, Grounded = true };
        asleep.Emotions.EnterSleep();
        check(!behavior.StartStretch(asleep) && asleep.Action == PetAction.Sleeping, "A reminder never wakes or moves a sleeping pet");

        string json = System.Text.Json.JsonSerializer.Serialize(new PetObservation("Idle", true, 1, 1, "x", "LOCAL",
            BreakDue: true, Context: new ContextReport(true, false, true), Birthday: "12-25"), AiProtocol.Json);
        check(json.Contains("\"break_due\":true") && json.Contains("\"context\":{\"fullscreen\":true,\"away\":false,\"returned\":true}") &&
              json.Contains("\"birthday\":\"12-25\""),
            "Observation reports break, computer context and the user's birthday as additive fields");
    }
}
