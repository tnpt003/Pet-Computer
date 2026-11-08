using System.IO;
using DesktopPet;

// The field's rules (1.5.1 M3): the order of the steps, growth by the clock, the save file, the lift.
internal static class FarmTests
{
    public static void Run(Action<bool, string> check)
    {
        var start = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        Steps(check, start);
        Clock(check, start);
        Saving(check, start);
        Lift(check);
    }

    private static void Steps(Action<bool, string> check, DateTime start)
    {
        var farm = new PetFarm();
        check(farm.Tiles.Count == 6 && farm.Tiles.All(t => t.Stage == FarmStage.Wild), "A new field has six wild tiles");
        check(new[] { 5, 15, 40, 120 }.SequenceEqual(Enum.GetValues<FarmCrop>().Select(c => (int)(PetFarm.GrowSeconds(c) / 60))), "Four crops of 5, 15, 40 and 120 minutes");

        check(farm.Next(0) == FarmStep.Till, "Wild ground is tilled first");
        check(!farm.Plant(0, FarmCrop.Carrot) && !farm.Water(0, start) && farm.Harvest(0) == null, "Nothing else can be done to wild ground");
        check(farm.Till(0) && farm.Next(0) == FarmStep.Plant && !farm.Till(0), "Tilled ground waits for a seed");
        check(!farm.Water(0, start), "Bare ground cannot be watered");
        check(farm.Plant(0, FarmCrop.Carrot) && farm.Next(0) == FarmStep.Water && farm.Tiles[0].Crop == FarmCrop.Carrot, "A seed waits for water");
        check(farm.Tiles[1].Stage == FarmStage.Wild, "The other tiles are left alone");
        check(!farm.Plant(-1, FarmCrop.Carrot) && !farm.Till(6) && farm.Harvest(9) == null, "Tiles outside the field are refused");

        var now = start;
        foreach (var stage in new[] { FarmStage.Sprout, FarmStage.Young, FarmStage.Ripe })
        {
            check(farm.Water(0, now) && farm.Next(0) == FarmStep.Wait && !farm.Water(0, now), $"Watered once, it grows towards {stage}");
            now = now.AddSeconds(PetFarm.StageSeconds(FarmCrop.Carrot));
            check(farm.Advance(now).SequenceEqual(new[] { 0 }) && farm.Tiles[0].Stage == stage, $"...and reaches {stage}");
        }
        check(now == start.AddMinutes(15), "Watered on time, a carrot takes its 15 minutes");
        check(farm.Next(0) == FarmStep.Harvest && farm.Progress(0, now) == 1, "Ripe, it waits for the harvest");
        check(farm.Harvest(0) == FarmCrop.Carrot && farm.HarvestedCount(FarmCrop.Carrot) == 1, "The harvest is counted");
        check(farm.Tiles[0].Stage == FarmStage.Wild && farm.Tiles[0].Crop == null && farm.Harvest(0) == null, "The tile is wild again and gives nothing twice");
    }

    private static void Clock(Action<bool, string> check, DateTime start)
    {
        var farm = new PetFarm();
        farm.Till(0); farm.Plant(0, FarmCrop.Radish);
        check(farm.Advance(start.AddDays(30)).Count == 0 && farm.Tiles[0].Stage == FarmStage.Seed, "Without water a seed waits for a month, not wilting");

        double stage = PetFarm.StageSeconds(FarmCrop.Radish);
        farm.Water(0, start);
        check(Math.Abs(farm.Progress(0, start.AddSeconds(stage / 2)) - 1.0 / 6) < 1e-9, "Half of the first stage is a sixth of the way");
        check(Math.Abs(farm.Remaining(0, start.AddSeconds(30)).TotalSeconds - (stage - 30)) < 1e-6, "The time left counts down");
        check(farm.Advance(start.AddSeconds(stage - 1)).Count == 0, "A second early it has not grown");

        farm.Advance(start.AddDays(10));
        check(farm.Tiles[0].Stage == FarmStage.Sprout && farm.Next(0) == FarmStep.Water, "Ten days away pass only the one stage that was watered");

        farm.Water(0, start);
        check(farm.Advance(start.AddDays(-5)).Count == 0, "A clock set backwards does not ripen anything");
        check(farm.Remaining(0, start.AddDays(-5)).TotalSeconds <= stage + 1e-6, "...and never makes the wait longer than one stage");

        var ripe = new PetFarm();
        ripe.Till(0); ripe.Plant(0, FarmCrop.Radish);
        for (int i = 0; i < 3; i++) { ripe.Water(0, start); ripe.Advance(start.AddSeconds(stage)); }
        check(ripe.Advance(start.AddYears(5)).Count == 0 && ripe.Tiles[0].Stage == FarmStage.Ripe, "Ripe crops wait for years");
    }

    private static void Saving(Action<bool, string> check, DateTime start)
    {
        string folder = Path.Combine(Path.GetTempPath(), "pet-farm-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "farm.json");
        try
        {
            check(PetFarm.Load(path).Tiles.Count == 6, "No file: a new field");

            var farm = new PetFarm { Monitor = @"\\.\DISPLAY2", FractionX = 0.4 };
            farm.Till(1); farm.Plant(1, FarmCrop.Pumpkin); farm.Water(1, start);
            farm.Till(2);
            check(farm.Save(path), "The field is saved");
            var back = PetFarm.Load(path);
            check(back.Tiles[1].Stage == FarmStage.Seed && back.Tiles[1].Crop == FarmCrop.Pumpkin && back.Tiles[2].Stage == FarmStage.Tilled, "Its tiles come back");
            check(back.Tiles[1].GrowsUntil == start.AddSeconds(PetFarm.StageSeconds(FarmCrop.Pumpkin)) && back.Tiles[1].GrowsUntil!.Value.Kind == DateTimeKind.Utc, "The growing time comes back in UTC");
            check(back.Monitor == @"\\.\DISPLAY2" && back.FractionX == 0.4, "The plot's place comes back");
            check(File.ReadAllText(path).Contains("\"Seed\"") && File.ReadAllText(path).Contains("\"Pumpkin\""), "Stages and crops are written by name");

            farm.Till(3);
            farm.Save(path);
            File.WriteAllText(path, "{ not json");
            check(PetFarm.Load(path).Tiles[1].Crop == FarmCrop.Pumpkin, "A damaged file falls back to its backup");

            File.Delete(path + ".bak");
            File.WriteAllText(path, "{ not json");
            check(PetFarm.Load(path).Tiles.All(t => t.Stage == FarmStage.Wild), "With no backup either: a new field");

            File.WriteAllText(path, """
                { "Tiles": [ { "Stage": "Ripe" }, { "Stage": "Seed", "GrowsUntil": "2026-11-08T12:00:00Z" }, { "Stage": "Wild", "Crop": "Carrot", "GrowsUntil": "2026-11-08T12:00:00Z" } ],
                  "Harvested": { "Carrot": 3, "Gold": 99, "Tomato": -2 }, "FractionX": 7 }
                """);
            var odd = PetFarm.Load(path);
            check(odd.Tiles.Count == 6 && odd.Tiles[0].Stage == FarmStage.Wild && odd.Tiles[1].Stage == FarmStage.Wild, "A crop stage with no crop becomes wild ground");
            check(odd.Tiles[2].Crop == null && odd.Tiles[2].GrowsUntil == null, "Wild ground carries no crop or timer");
            check(odd.HarvestedCount(FarmCrop.Carrot) == 3 && odd.Harvested.Count == 1, "Unknown or negative harvest counts are dropped");
            check(odd.FractionX == 1, "A place off the screen is pulled back");

            File.WriteAllText(path, """{ "Version": 99, "FractionX": 0.2 }""");
            var newer = PetFarm.Load(path);
            check(newer.ReadOnly && !newer.Save(path) && File.ReadAllText(path).Contains("99"), "A file from a newer version is not overwritten");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private static void Lift(Action<bool, string> check)
    {
        var lift = new FarmLift();
        check(lift.Phase == FarmLiftPhase.Down && lift.Height == 0 && !lift.Busy, "The plot starts on the ground");
        lift.Drop();
        check(lift.Phase == FarmLiftPhase.Down, "What is on the ground cannot be dropped");

        lift.Begin();
        double last = 0;
        bool smooth = true;
        for (double t = 0; t < FarmLift.RiseSeconds; t += 1.0 / 60) { lift.Step(1.0 / 60); smooth &= lift.Height >= last - 1e-9 || lift.Floating; smooth &= Math.Abs(lift.Height - last) < 5; last = lift.Height; }
        lift.Step(1.0 / 60);
        check(smooth && lift.Floating, "It rises smoothly and floats");

        double low = 999, high = 0;
        for (double t = 0; t < 4; t += 1.0 / 60) { lift.Step(1.0 / 60); low = Math.Min(low, lift.Height); high = Math.Max(high, lift.Height); }
        check(lift.Floating && Math.Abs(high - low - FarmLift.BobHeight * 2) < 0.2, $"It bobs gently in the air ({low:F1} to {high:F1})");

        lift.Drop();
        double from = lift.Height, previous = from, biggest = 0, lastStep = 0, frames = 0;
        bool landed = false;
        while (!landed && frames < 300)
        {
            landed = lift.Step(1.0 / 60);
            frames++;
            lastStep = previous - lift.Height;
            biggest = Math.Max(biggest, lastStep);
            previous = lift.Height;
        }
        check(landed && lift.Height == 0 && Math.Abs(frames / 60 - FarmLift.FallSeconds) < 0.05, $"It settles in about {FarmLift.FallSeconds} s ({frames / 60:F2} s)");
        check(biggest < 1.5 && lastStep < biggest / 3 || landed && biggest < 1.5, $"...slowly, with no sudden drop ({biggest:F2} DIPs a frame at most)");
        check(lift.Dust == 1 && lift.Busy, "A puff of dust as it lands");
        for (int i = 0; i < 40; i++) lift.Step(1.0 / 60);
        check(!lift.Busy, "...and then all is still");

        lift.Begin(); lift.Step(0.1); lift.Drop();
        check(lift.Phase == FarmLiftPhase.Falling, "It can be put down while still rising");
        lift.Reset();
        check(lift.Phase == FarmLiftPhase.Down && lift.Height == 0 && !lift.Busy, "A reset puts it on the ground at once");
    }
}
