using System.Windows;
using System.Windows.Controls;
using DesktopPet;
using static PetPrivate;

// The cat-led guide in the real pet window (M2b): never on a sleeping cat, holds the cat and blocks
// automatic behaviour, the drag and menu steps pass when the user does them, the tray card sits by
// the tray, Skip / a fullscreen app end it.
internal static class TourChecks
{
    private static void SetContext(PetWindow window, bool fullscreen) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, 0.0)));
    private static Window? Card() => Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.Title == "Pet guide" && w.IsLoaded);

    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        void Tick(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) { Call(window, "TickTourClock", 1.0 / 60); Call(window, "TickTour"); } }
        bool quiet = prefs.Quiet;
        prefs.Quiet = false;
        SetContext(window, false); Call(window, "TickContext", 1.1);
        window.Reset();
        try
        {
            pet.Emotions.Reset(); behavior.Reset();
            pet.Grounded = true; pet.LedgeId = 0;
            pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep();
            check(window.StartTour() is string asleep && asleep.Length > 0 && !window.TourActive, "A sleeping cat does not start the guide");
            check(pet.Action == PetAction.Sleeping && pet.Emotions.SleepLocked, "...and is not woken");
            pet.Emotions.Reset(); behavior.Reset(); pet.Action = PetAction.Idle;
            long interactions = Field<long>(window, "interactionCount");

            check(window.TourMenuEnabled, "The tray Guide row is on while the cat is awake and free");
            check(window.StartTour() == null && window.TourActive && window.TourStepNow == TourStep.Drag && Card() != null, "The guide starts with a card by the cat: drag me");
            check(!window.TourMenuEnabled, "...and greyed while the guide runs");
            check((bool)Call(window, "FeatureOwnsMovement")! && !(bool)Call(window, "MayAct", AutoKind.ToyInvite)! && !(bool)Call(window, "MayAct", AutoKind.PortalEvent)!,
                "Nothing automatic runs during the guide");
            Tick(0.2);
            check(pet.Action == PetAction.Sitting && pet.VX == 0, "The cat sits and waits");
            var card = Card()!;
            card.UpdateLayout();
            double catMiddle = (double)typeof(PetWindow).GetProperty("CatCenter", Flags)!.GetValue(window)!, cardMiddle = card.Left + card.ActualWidth / 2;
            var area = Field<Area>(window, "bounds");
            check(Math.Abs(cardMiddle - catMiddle) < 2 || card.Left <= area.Left + 1 || card.Left + card.ActualWidth >= area.Right - 1, $"The card is over the cat ({cardMiddle - catMiddle:F0} DIPs)");

            Call(window, "TourSawDrag");
            check(window.TourStepNow == TourStep.Drag, "A release is not the end of the step while the cat is still moving");
            pet.Grounded = true; pet.Action = PetAction.Idle;
            Tick(1.0 / 60);
            check(window.TourStepNow == TourStep.Menu, "Dragging the cat passes the drag step once it is down");

            window.ContextMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, window.ContextMenu));
            check(window.TourStepNow == TourStep.Menu, "While the menu is open the card stays");
            window.ContextMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent, window.ContextMenu));
            check(window.TourStepNow != TourStep.Menu && window.TourActive, $"Opening the menu passes the menu step ({window.TourStepNow})");

            int guard = 0;
            while (window.TourStepNow is TourStep.Sign or TourStep.Thing && guard++ < 4)
            {
                Tick(0.05);
                check(pet.Action is PetAction.Walking or PetAction.Sitting, $"At {window.TourStepNow} the cat walks to it or sits by it ({pet.Action})");
                Call(window, "NextTourStep");
            }
            check(window.TourStepNow == TourStep.Tray, "Then the tray");
            Tick(1.0 / 60);
            card = Card()!;
            card.UpdateLayout();
            check(Math.Abs(card.Left + card.ActualWidth - (area.Right - 8)) < 2 && Math.Abs(card.Top + card.ActualHeight - (area.Bottom - 8)) < 2, "The tray card sits by the tray, bottom right");
            Call(window, "NextTourStep");
            check(window.TourStepNow == TourStep.Done, "The last card");
            Call(window, "NextTourStep");
            check(!window.TourActive && Card() == null, "Done ends the guide and closes its card");
            check(pet.Action == PetAction.Idle, "The cat is free again");
            check(Field<long>(window, "interactionCount") == interactions, "The guide itself counts no interaction");

            check(window.StartTour() == null, "Again");
            Call(window, "StopTour");
            check(!window.TourActive && Card() == null, "Skip ends it at once");

            check(window.StartTour() == null, "Once more");
            SetContext(window, true); Call(window, "TickContext", 1.1);
            Tick(1.0 / 60);
            check(!window.TourActive && Card() == null, "A fullscreen app ends it");
            check(window.StartTour() is string busy && busy.Length > 0, "No guide over a fullscreen app");
        }
        finally
        {
            Call(window, "StopTour");
            prefs.Quiet = quiet;
            SetContext(window, false); Call(window, "TickContext", 1.1);
            pet.Emotions.Reset();
            window.Reset();
        }
    }
}
