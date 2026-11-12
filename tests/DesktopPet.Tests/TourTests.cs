using DesktopPet;

// The cat-led guide's order (M2b): six steps, the sign and the thing only when there is one,
// the drag and menu steps marked done by the user, the end after the last.
internal static class TourTests
{
    public static void Run(Action<bool, string> check)
    {
        var tour = new PetTour();
        check(!tour.Active && tour.Step == TourStep.None, "No guide until it is started");

        tour.Start(_ => true);
        check(tour.Active && tour.Step == TourStep.Drag && tour.Number == 1 && tour.Total == 6, "It starts with dragging the cat, 1/6");
        var seen = new List<TourStep> { tour.Step };
        while (tour.Active) { tour.Next(_ => true); if (tour.Active) seen.Add(tour.Step); }
        check(string.Join(",", seen) == "Drag,Menu,Sign,Thing,Tray,Done", $"The steps in order ({string.Join(",", seen)})");
        check(!tour.Active, "After the last step the guide is over");

        tour.Start(step => step is not (TourStep.Sign or TourStep.Thing));
        seen = new List<TourStep> { tour.Step };
        while (tour.Active) { tour.Next(step => step is not (TourStep.Sign or TourStep.Thing)); if (tour.Active) seen.Add(tour.Step); }
        check(tour.Total == 4 && string.Join(",", seen) == "Drag,Menu,Tray,Done", "No farm sign and no thing: those steps are left out (4 steps)");

        tour.Start(_ => true);
        tour.Did(TourStep.Menu);
        check(!tour.Done, "Only the step on screen can be done by the user");
        tour.Did(TourStep.Drag);
        check(tour.Done && tour.Step == TourStep.Drag, "Dragging the cat marks the drag step done (the window moves on)");
        tour.Next(_ => true);
        check(tour.Step == TourStep.Menu && !tour.Done && tour.Number == 2 && tour.Time == 0, "The next step starts fresh, 2/6");
        tour.Tick(1.5);
        check(Math.Abs(tour.Time - 1.5) < 1e-9, "Its clock runs");
        tour.Next(_ => true);
        tour.Did(TourStep.Sign);
        check(!tour.Done, "The sign step only passes with Next");

        tour.Stop();
        check(!tour.Active && tour.Time == 0, "Skip ends it at once");
        tour.Next(_ => true);
        check(!tour.Active, "Nothing moves a stopped guide");
    }
}
