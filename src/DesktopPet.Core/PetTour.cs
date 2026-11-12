using System;
using System.Linq;

namespace DesktopPet;

public enum TourStep { None, Drag, Menu, Sign, Thing, Tray, Done }

// The cat-led guide (M2b, docs/APPROVED-EXCEPTIONS.md → Hành vi mèo): a user command like calling
// the cat. Its steps in order; one the screen has nothing for (no farm sign, no keepsake or basket
// beside the cat) is left out. The drag and right-click steps also pass when the user does it.
// This is only the order and the clock; the window owns the cat, the card and what counts.
public sealed class PetTour
{
    public static readonly TourStep[] Order = { TourStep.Drag, TourStep.Menu, TourStep.Sign, TourStep.Thing, TourStep.Tray, TourStep.Done };
    // The walk to the sign or a thing gives up after this; the cat sits where it got to.
    public const double WalkSeconds = 8;
    // How close it stands to where it walks, in DIPs.
    public const double Near = 8;

    public TourStep Step { get; private set; }
    public bool Active => Step != TourStep.None;
    // Seconds in this step.
    public double Time { get; private set; }
    // "2/6": this step's place among the steps there were when it began.
    public int Number { get; private set; }
    public int Total { get; private set; }
    // The user did this step's thing (dragged the cat, opened its menu); the window moves on when it is over.
    public bool Done { get; private set; }

    public void Start(Func<TourStep, bool> available)
    {
        Total = Order.Count(step => Shown(step, available));
        Number = 0;
        Step = TourStep.None;
        Move(0, available);
    }

    // To the next step there is something for; after the last, the guide is over.
    public void Next(Func<TourStep, bool> available)
    {
        if (!Active) return;
        Move(Array.IndexOf(Order, Step) + 1, available);
    }

    public void Stop()
    {
        Step = TourStep.None;
        Time = 0;
        Done = false;
    }

    public void Tick(double dt)
    {
        if (Active) Time += Math.Max(0, dt);
    }

    // The user did what this step asks (only the drag and the menu steps wait for that).
    public void Did(TourStep step)
    {
        if (Active && step == Step && step is TourStep.Drag or TourStep.Menu) Done = true;
    }

    // Steps the guide always has: the cat itself, the tray, the end.
    private static bool Shown(TourStep step, Func<TourStep, bool> available) => step is not (TourStep.Sign or TourStep.Thing) || available(step);

    private void Move(int from, Func<TourStep, bool> available)
    {
        Time = 0;
        Done = false;
        for (int i = Math.Max(0, from); i < Order.Length; i++)
        {
            if (!Shown(Order[i], available)) continue;
            Step = Order[i];
            Number = Math.Min(Total, Number + 1);
            return;
        }
        Step = TourStep.None;
    }
}
