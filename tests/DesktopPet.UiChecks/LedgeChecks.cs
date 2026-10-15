using System.Reflection;
using DesktopPet;

// Walking on windows inside the real PetWindow, with a fake list of app windows.
internal static class LedgeChecks
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private static T Field<T>(PetWindow window, string name) => (T)typeof(PetWindow).GetField(name, Private)!.GetValue(window)!;
    private static void SetField(PetWindow window, string name, object? value) => typeof(PetWindow).GetField(name, Private)!.SetValue(window, value);

    // Replaces the Windows probes; the list is read fresh on every scan.
    public static void SetWindows(PetWindow window, Func<IReadOnlyList<WindowSnapshot>> windows)
    {
        SetField(window, "windowProbe", windows);
        SetField(window, "windowRectProbe", new Func<long, WindowSnapshot?>(id =>
        {
            foreach (var w in windows()) if (w.Id == id) return w;
            return null;
        }));
        SetField(window, "ledges", new List<Ledge>());
    }

    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences preferences, Action<bool, string> check)
    {
        var physics = Field<PetPhysics>(window, "physics");
        var tickLedges = typeof(PetWindow).GetMethod("TickLedges", Private)!;
        var tickFeatures = typeof(PetWindow).GetMethod("TickFeatures", Private)!;
        var ownsMovement = typeof(PetWindow).GetMethod("FeatureOwnsMovement", Private)!;
        const double step = 1.0 / 120;
        void Simulate(double seconds, Func<bool>? until = null)
        {
            for (double t = 0; t < seconds; t += step)
            {
                tickFeatures.Invoke(window, new object[] { step });
                tickLedges.Invoke(window, new object[] { step });
                bool owns = (bool)ownsMovement.Invoke(window, null)!;
                if (!owns) behavior.Step(pet, step);
                physics.Step(pet, Field<Area>(window, "bounds"), step, Field<List<Ledge>>(window, "ledges"), owns);
                pet.AnimationTime += step; pet.ActionTime += step;
                if (until?.Invoke() == true) return;
            }
        }

        window.Reset();
        var bounds = Field<Area>(window, "bounds");
        var app = new WindowSnapshot(77, bounds.Left + 300, bounds.Bottom - 450, bounds.Left + 900, bounds.Bottom, true);
        SetWindows(window, () => new[] { app });
        bool quiet = preferences.Quiet;
        preferences.Quiet = false; behavior.Quiet = false;

        // Thrown from above onto the window: lands on its edge.
        pet.X = bounds.Left + 600 - 56; pet.Y = app.Top - 350; pet.VX = pet.VY = 0;
        pet.Grounded = false; pet.Action = PetAction.Thrown;
        SetField(window, "ledgeIdea", 1000.0); // No autonomous trips during this part.
        Simulate(4, () => pet.Grounded && pet.VY == 0 && pet.Action != PetAction.Thrown);
        check(pet.Grounded && pet.LedgeId == 77 && Math.Abs(pet.Y - (app.Top - 112)) < 0.01, "A pet thrown over an app window lands on its top edge");

        // The user drags the window: the pet rides along.
        double x = pet.X, y = pet.Y;
        app = app with { Left = app.Left + 120, Right = app.Right + 120, Top = app.Top - 50 };
        Simulate(0.2);
        // It may also be strolling meanwhile (at most ~15 DIPs in 0.2 s).
        check(pet.LedgeId == 77 && Math.Abs(pet.X - (x + 120)) < 20 && Math.Abs(pet.Y - (y - 50)) < 0.01,
            "Moving the window carries the pet with it");

        // The once-a-second floor correction leaves a pet on a window alone.
        SetField(window, "boundsTimer", 1.0);
        typeof(PetWindow).GetField("previous", Private)!.SetValue(window, Field<System.Diagnostics.Stopwatch>(window, "clock").Elapsed.TotalSeconds - 0.05);
        typeof(PetWindow).GetMethod("Tick", Private)!.Invoke(window, new object?[] { null, EventArgs.Empty });
        check(pet.LedgeId == 77 && Math.Abs(pet.Y - (app.Top - 112)) < 1, "The monitor/work-area check does not pull a pet off its window");

        // Minimized or closed: it falls to the floor.
        var saved = app;
        SetWindows(window, () => Array.Empty<WindowSnapshot>());
        Simulate(4, () => pet.Grounded && pet.LedgeId == 0 && pet.VY == 0);
        check(pet.Grounded && pet.LedgeId == 0 && Math.Abs(pet.Y - (bounds.Bottom - 112)) < 0.01, "When the window goes away the pet falls to the floor");

        // Climb: from the floor, walk to the window's side, climb it and hop onto the top.
        app = saved;
        SetWindows(window, () => new[] { app });
        window.Reset();
        pet.X = app.Right + 200;
        SetField(window, "ledgeIdea", 0.0);
        SetField(window, "ledgeScan", 0.0);
        bool climbed = false;
        Simulate(25, () => { climbed |= pet.Action == PetAction.Climbing; return climbed && pet.LedgeId == 77 && pet.Grounded; });
        check(climbed && pet.LedgeId == 77 && pet.Grounded, "On its own the pet walks to a tall window, climbs its side and gets onto the top");

        // Clicking a climbing pet makes it let go.
        window.Reset();
        pet.X = app.Right + PetSettings.ClimbOffset - 56;
        pet.Action = PetAction.Climbing; pet.Grounded = false; pet.ClimbId = 77; pet.ClimbSide = 1;
        Simulate(1);
        typeof(PetWindow).GetMethod("CancelFeatureActivity", Private)!.Invoke(window, null);
        check(pet.Action == PetAction.Jumping && !pet.Grounded && pet.ClimbId == 0, "A click or right-click makes a climbing pet let go");
        Simulate(4, () => pet.Grounded);

        // Quiet mode: no trips of its own.
        window.Reset();
        preferences.Quiet = true; behavior.Quiet = true;
        SetField(window, "ledgeIdea", 0.0);
        Simulate(3);
        check(pet.LedgeId == 0 && pet.Action != PetAction.Climbing && Field<object?>(window, "climbPlan") == null,
            "Quiet mode keeps the pet off windows unless the user puts it there");
        preferences.Quiet = false; behavior.Quiet = false;

        // Turned off in Settings: no ledges at all, and a pet on a window drops.
        pet.X = bounds.Left + 600 - 56; pet.Y = app.Top - 350; pet.Grounded = false; pet.Action = PetAction.Thrown;
        SetField(window, "ledgeIdea", 1000.0);
        Simulate(4, () => pet.LedgeId == 77 && pet.Grounded);
        preferences.ClimbWindows = false;
        Simulate(4, () => pet.Grounded && pet.LedgeId == 0 && pet.VY == 0);
        check(pet.LedgeId == 0 && Field<List<Ledge>>(window, "ledges").Count == 0 && Math.Abs(pet.Y - (bounds.Bottom - 112)) < 0.01,
            "Turning the setting off drops the pet and ignores windows");
        pet.X = bounds.Left + 600 - 56; pet.Y = app.Top - 350; pet.Grounded = false; pet.Action = PetAction.Thrown;
        Simulate(4, () => pet.Grounded && pet.VY == 0);
        check(pet.LedgeId == 0, "With the setting off a thrown pet passes in front of windows to the floor");
        preferences.ClimbWindows = true;

        preferences.Quiet = quiet; behavior.Quiet = quiet;
        SetWindows(window, () => Array.Empty<WindowSnapshot>());
        window.Reset();
    }
}
