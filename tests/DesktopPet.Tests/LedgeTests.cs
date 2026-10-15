using DesktopPet;

// Walking on windows: geometry (visible edges, climbable sides) and physics
// (land from above only, turn at the ends, drop off, ride, climb, leap).
internal static class LedgeTests
{
    public static void Run(Action<bool, string> check)
    {
        var settings = new PetSettings();
        var physics = new PetPhysics(settings);
        var bounds = new Area(0, 0, 1920, 1040);
        const double step = 1.0 / 120;
        // A normal window whose top edge is at 600 and which reaches down to the floor.
        var window = new WindowSnapshot(1, 400, 600, 1000, 1040, true);
        var ledges = PetLedges.Build(new[] { window }, bounds, 112, 112);
        check(ledges.Count == 1 && ledges[0] is { Id: 1, Top: 600, Left: 400, Right: 1000, ClimbLeft: true, ClimbRight: true },
            "An uncovered window edge is one ledge, climbable on both sides");

        // A window above it covering the middle of the edge splits it into two ledges.
        var cover = new WindowSnapshot(2, 600, 500, 800, 700, false);
        var split = PetLedges.Build(new[] { cover, window }, bounds, 112, 112);
        check(split.Count == 2 && split[0].Right == 600 && split[1].Left == 800,
            "A window above covering part of the edge leaves only the visible pieces");
        var sideCover = new WindowSnapshot(3, 950, 700, 1300, 900, false);
        var sided = PetLedges.Build(new[] { sideCover, window }, bounds, 112, 112);
        check(sided.Count == 1 && sided[0].ClimbLeft && !sided[0].ClimbRight, "A covered side cannot be climbed");
        check(PetLedges.Build(new[] { window with { Candidate = false } }, bounds, 112, 112).Count == 0 &&
              PetLedges.Build(new[] { window with { Top = 60 } }, bounds, 112, 112).Count == 0 &&
              PetLedges.Build(new[] { window with { Right = 550 } }, bounds, 112, 112).Count == 0 &&
              PetLedges.Build(new[] { window with { Top = 1020 } }, bounds, 112, 112).Count == 0,
            "Tool/maximized windows, edges too close to the screen top or floor and narrow windows are ignored");
        var floating = PetLedges.Build(new[] { window with { Bottom = 800 } }, bounds, 112, 112);
        check(floating.Count == 1 && !floating[0].ClimbLeft && !floating[0].ClimbRight, "A window far above the floor can be stood on but not climbed");

        PetModel Falling(double centerX, double y, double vy = 0) =>
            new() { X = centerX - 56, Y = y, VY = vy, Grounded = false, Action = PetAction.Thrown };
        void Run(PetModel pet, double seconds, IReadOnlyList<Ledge>? list, bool leave = false)
        {
            for (double t = 0; t < seconds; t += step) physics.Step(pet, bounds, step, list, leave);
        }

        var pet = Falling(700, 300);
        Run(pet, 1.5, ledges);
        check(pet.Grounded && pet.LedgeId == 1 && pet.Y == 600 - 112, "A falling pet lands on the window edge below it");
        var rising = Falling(700, 900, -1500);
        Run(rising, 0.6, ledges);
        check(!rising.Grounded && rising.LedgeId == 0 && rising.Y + 112 < 600 && rising.VY < 0, "Rising through a window edge never stops the pet");
        var beside = Falling(1200, 300);
        Run(beside, 4, ledges);
        check(beside.Grounded && beside.LedgeId == 0 && beside.Y == 1040 - 112, "Beside the window the pet falls to the floor as before");
        var hard = Falling(700, 0, 900);
        Run(hard, 0.12, ledges);
        bool bounced = false;
        for (double t = 0; t < 4; t += step) { physics.Step(hard, bounds, step, ledges); bounced |= hard.VY < 0; }
        check(bounced && hard.Grounded && hard.LedgeId == 1, "A hard landing on a ledge bounces, then settles on it");
        check(new PetModel { X = 100, Y = 928, Grounded = true }.LedgeId == 0, "The pet starts on the floor");

        // Walking turns at the ledge's ends unless a game or the resting spot leads it off.
        pet.Action = PetAction.Walking; pet.Facing = 1; pet.VX = settings.WalkSpeed;
        Run(pet, 8, ledges);
        double center = pet.X + 56;
        check(pet.Grounded && pet.LedgeId == 1 && center <= 1000 - PetSettings.LedgeMargin && pet.Facing == -1,
            "Wandering turns around at the end of the ledge");
        pet.Facing = 1; pet.Action = PetAction.Walking; pet.VX = settings.WalkSpeed;
        Run(pet, 6, ledges, leave: true);
        check(pet.LedgeId == 0 && pet.X + 56 > 1000, "A game or the resting spot walks the pet off the end; it drops");

        // The window disappears (closed, minimized, covered): the pet falls.
        pet = Falling(700, 300);
        Run(pet, 1.5, ledges);
        pet.Action = PetAction.Sitting;
        Run(pet, 4, new List<Ledge>());
        check(pet.Grounded && pet.LedgeId == 0 && pet.Y == 1040 - 112, "When its window goes away the pet falls to the floor");
        pet = Falling(700, 300);
        Run(pet, 1.5, ledges);
        pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
        Run(pet, 1.5, new List<Ledge>());
        check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked && pet.Grounded && pet.VY == 0,
            "A sleeping pet whose window vanished lands softly and keeps sleeping");

        // A moved window: the ledge list moves and the window code shifts the pet; physics keeps it standing.
        pet = Falling(700, 300);
        Run(pet, 1.5, ledges);
        var moved = new List<Ledge> { ledges[0].Shifted(150, -100) };
        pet.X += 150; pet.Y -= 100;
        Run(pet, 0.5, moved);
        check(pet.Grounded && pet.LedgeId == 1 && pet.Y == 500 - 112, "A pet carried along with its window keeps standing on it");

        // Climbing the right side, then the hop onto the top edge.
        var climber = new PetModel { X = 1000 + PetSettings.ClimbOffset - 56, Y = 1040 - 112, Action = PetAction.Climbing, ClimbId = 1, ClimbSide = 1 };
        Run(climber, 1, ledges);
        check(climber.Action == PetAction.Climbing && climber.Y < 1040 - 112 - 100 && climber.Facing == -1 && !climber.Grounded,
            "Climbing goes up the window's side, facing it");
        Run(climber, 6, ledges);
        check(climber.Grounded && climber.LedgeId == 1 && climber.X + 56 < 1000 && climber.Action is PetAction.Idle or PetAction.Jumping,
            "At the top the pet hops onto the edge");
        var letGo = new PetModel { X = 1000 + PetSettings.ClimbOffset - 56, Y = 800, Action = PetAction.Climbing, ClimbId = 1, ClimbSide = 1 };
        Run(letGo, 2, new List<Ledge>());
        check(letGo.Action != PetAction.Climbing && letGo.Grounded && letGo.Y == 1040 - 112, "If the window goes away mid-climb the pet drops");
        check(PetActionRules.RejectsDecisions(PetAction.Climbing), "AI commands cannot interrupt a climb");

        // Planning: a nearby low window is a leap; a tall one is a climb.
        var low = PetLedges.Build(new[] { new WindowSnapshot(5, 500, 800, 900, 1040, true) }, bounds, 112, 112);
        var floorPet = new PetModel { X = 450, Y = 1040 - 112, Grounded = true };
        var jump = PetLedges.PlanJump(low, floorPet, bounds, settings);
        check(jump is { VY: < 0 } && Math.Abs(jump.Value.VX) > 0, "A low window within reach is a leap");
        if (jump is var (_, vx, vy))
        {
            floorPet.Action = PetAction.Jumping; floorPet.Grounded = false; floorPet.VX = vx; floorPet.VY = vy;
            Run(floorPet, 2, low);
            check(floorPet.Grounded && floorPet.LedgeId == 5, "The planned leap lands on the window's edge");
        }
        check(PetLedges.PlanJump(ledges, new PetModel { X = 450, Y = 1040 - 112, Grounded = true }, bounds, settings) == null,
            "A window edge 440 DIPs up is too high to leap");
        var climb = PetLedges.PlanClimb(ledges, new PetModel { X = 1200, Y = 1040 - 112, Grounded = true }, settings);
        check(climb is { Side: 1 } && Math.Abs(climb.Value.StartX - (1000 + PetSettings.ClimbOffset - 56)) < 0.01,
            "A tall window is climbed from the nearer side");

        // Leaps are autonomous: Quiet refuses them, and the crouch uses the planned velocity.
        var behavior = new PetBehavior(settings, new Random(1)) { Quiet = true };
        var leaper = new PetModel { X = 450, Y = 1040 - 112, Grounded = true };
        check(!behavior.StartLeap(leaper, 100, -900), "Quiet mode refuses an autonomous leap");
        behavior.Quiet = false;
        check(behavior.StartLeap(leaper, 100, -900) && leaper.Action == PetAction.PreparingJump, "A leap starts with the usual crouch");
        for (int i = 0; i < 30 && leaper.Action == PetAction.PreparingJump; i++) behavior.Step(leaper, step);
        check(leaper.Action == PetAction.Jumping && leaper.VX == 100 && leaper.VY == -900, "Then it launches with the planned velocity");
        var plain = new PetModel { X = 450, Y = 1040 - 112, Grounded = true };
        behavior.StartLeap(plain, 100, -900);
        behavior.Reset(); plain.Action = PetAction.Idle; // As a click or Quiet would cancel it.
        check(behavior.StartManual(plain, PetAction.PreparingJump), "Do now Jump after a cancelled leap");
        for (int i = 0; i < 30 && plain.Action == PetAction.PreparingJump; i++) behavior.Step(plain, step);
        check(plain.VY == -settings.JumpSpeed, "A cancelled leap never leaks into a later ordinary jump");

        // Waking keeps its own 1.2 s even if the pet is falling meanwhile.
        var waking = new PetModel { Action = PetAction.Waking, Grounded = false };
        var wakeBehavior = new PetBehavior(settings);
        wakeBehavior.Reset();
        double elapsed = 0;
        typeof(PetBehavior).GetField("remaining", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(wakeBehavior, PetSettings.WakingDuration);
        while (waking.Action == PetAction.Waking && elapsed < 3) { wakeBehavior.Step(waking, step); elapsed += step; }
        check(Math.Abs(elapsed - PetSettings.WakingDuration) < 0.02, "Waking lasts 1.2 s even while falling");
    }
}
