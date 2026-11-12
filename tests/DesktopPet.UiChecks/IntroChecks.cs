using System.Windows;
using DesktopPet;
using static PetPrivate;

// The startup intro in the real pet window (M2a): the decorative portal, the gentle toss, nothing
// automatic, no emotion, no interaction; a click or a fullscreen app shows everything at once.
internal static class IntroChecks
{
    private static void SetContext(PetWindow window, bool fullscreen) =>
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (fullscreen, 0.0)));
    private static int Count(string title) => Application.Current.Windows.OfType<Window>().Count(w => w.Title == title && w.IsLoaded);
    private static double CatOpacity(PetWindow window) => Field<UIElement>(window, "renderer").Opacity;

    public static void Run(PetWindow window, PetModel pet, PetBehavior behavior, PetPreferences prefs, Action<bool, string> check)
    {
        void Tick(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickIntro", 1.0 / 60); }
        bool quiet = prefs.Quiet;
        prefs.Quiet = false;
        SetContext(window, false); Call(window, "TickContext", 1.1);
        window.Reset();
        try
        {
            pet.Emotions.Reset(); behavior.Reset();
            pet.Grounded = true; pet.Action = PetAction.Idle; pet.LedgeId = 0;
            long interactions = Field<long>(window, "interactionCount"), rough = pet.Interactions.RoughThrows;
            double column = pet.X;

            window.BeginIntro();
            check(window.IntroRunning && CatOpacity(window) == 0, "The cat starts hidden in the portal");
            check((bool)Call(window, "FeatureOwnsMovement")! && !(bool)Call(window, "MayAct", AutoKind.PortalEvent)! && !(bool)Call(window, "MayAct", AutoKind.ToyInvite)!,
                "Nothing automatic runs during the intro (no wandering, no portal event, no invite)");
            Tick(1.0 / 60);
            check(Count("Pet intro portal") == 1 && !window.PortalOpen, "A decorative portal opens (not the one into the battle hub)");
            check(Math.Abs(pet.X - column) < 1 && pet.Grounded && pet.Action == PetAction.Idle, "...at the cat's saved column, on the floor");
            Tick(PetIntro.ThrowAt);
            double speed = Math.Sqrt(pet.VX * pet.VX + pet.VY * pet.VY);
            check(pet.Action == PetAction.Thrown && !pet.Grounded && pet.VY < 0 && CatOpacity(window) == 1, "The cat is tossed out with an ordinary throw");
            check(speed < 1600, $"...a gentle one, under the rough-throw speed ({speed:F0} DIP/s)");
            check(pet.Interactions.RoughThrows == rough, "No rough throw is counted");
            check(!(bool)typeof(PetWindow).GetProperty("CatFreeToStartle", Flags)!.GetValue(window)!, "The cursor cannot startle it meanwhile");
            Tick(PetIntro.TotalSeconds);
            check(!window.IntroRunning && Count("Pet intro portal") == 0 && Count("Pet intro magic") == 0, "After about three seconds the portal and the effects are gone");
            check(Field<long>(window, "interactionCount") == interactions, "It counts no interaction (nothing sent to Python)");
            check(pet.Emotions.Current == PetEmotion.Neutral && pet.Emotions.Target == PetEmotion.Neutral, $"It brings no emotion ({pet.Emotions.Target})");
            check(Field<Action?>(window, "afterIntro") == null, "What waited for it (the welcome) is released");

            window.Reset(); pet.Grounded = true; pet.Action = PetAction.Idle;
            window.BeginIntro();
            Tick(0.2);
            Call(window, "SkipIntro");
            check(!window.IntroRunning && Count("Pet intro portal") == 0 && CatOpacity(window) == 1, "A click shows everything at once");
            check(pet.Grounded && pet.Action != PetAction.Thrown, "A cat still inside the portal just appears where it stands");

            SetContext(window, true);
            window.BeginIntro();
            Tick(1.0 / 60);
            check(!window.IntroRunning && Count("Pet intro portal") == 0 && CatOpacity(window) == 1, "With a fullscreen app there is no intro");
        }
        finally
        {
            Call(window, "FinishIntro");
            prefs.Quiet = quiet;
            SetContext(window, false); Call(window, "TickContext", 1.1);
            pet.Emotions.Reset();
            window.Reset();
        }
    }
}
