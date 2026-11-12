using System;
using System.Linq;
using System.Windows;
using System.Windows.Interop;

namespace DesktopPet;

// Visitors (docs/MINI-GAMES.md → M4): every 40–90 minutes a paper butterfly or a gentle slime comes by on its
// own, across the cat's screen, in its own small window under the cat's (clickable on the guest only, never
// taking focus). An automatic behaviour, blocked like the cat bringing its ball (asleep, Quiet, focus time, a
// fullscreen app, a battle, the user away, busy...), with its switch in Customize → Habits. The cat turns to
// watch it; about half the time it follows a little (at most ~150 DIPs or 3 seconds on its own ground) and
// pounces once in place at a butterfly (drawn, like the fireflies'). Nothing touches the cat, nothing wakes it,
// no emotion: a click or drag on the cat only stops it watching, the guest goes on its way. Python knows nothing
// of guests; while the cat watches, its commands wait like for any feature that owns the cat.
public sealed partial class PetWindow
{
    private readonly Guest guest = new();
    private readonly GuestWatcher guestWatcher = new();
    private Window? guestWindow;
    private GuestArt? guestArt;
    private Area guestBounds;
    private double guestWait = double.NaN, guestScale, guestPounce = -1, guestOrderCheck;
    private bool guestWatching, guestPounceDone;

    public bool GuestActive => guest.Active;
    public GuestKind GuestKindNow => guest.Kind;
    public GuestPhase GuestPhaseNow => guest.Phase;
    public Guest GuestNow => guest;
    public bool GuestWatching => guestWatching;
    public bool GuestChasing => guestWatcher.Chasing;
    public bool GuestPouncing => guestPounce >= 0;
    public double GuestHop => renderer.TeleportDrop;
    public double GuestWait => guestWait;
    public bool GuestWindowShown => guestWindow != null;

    private bool GuestOwnsMovement => guestWatching;

    private GuestCat GuestCatNow() => new(CatCenter, pet.Y + settings.Height,
        pet.LedgeId == 0 && Math.Abs(pet.Y + settings.Height - bounds.Bottom) < 4);

    // One visitor now, if this is a moment one may come by itself (also the UiChecks' hook: there is no menu
    // row). kind/follows: chosen at random unless given. True when it came.
    public bool TryGuestVisit(GuestKind? kind = null, bool? follows = null)
    {
        if (closed || !preferences.Guests || guest.Active || !MayAct(AutoKind.GuestVisit) || MiniGameActive || portalWindow != null || pet.CastRemaining > 0
            || !pet.Grounded || pet.Action is not (PetAction.Idle or PetAction.Sitting or PetAction.Looking or PetAction.Walking)) return false;
        if (!guest.Start(kind ?? GuestRules.Pick(featureRandom), featureRandom, bounds.Left, bounds.Right, bounds.Top, bounds.Bottom, GuestCatNow()))
        {
            System.Diagnostics.Trace.WriteLine("Visitors: no room on this screen.");
            return false;
        }
        guestBounds = bounds; guestScale = scale;
        ShowGuest();
        GuestRules.CountVisit(preferences);
        SavePreferences();
        guestWatcher.Reset(featureRandom);
        if (follows is bool given) guestWatcher.ForceFollow(given);
        guestWatching = true;
        guestPounce = -1;
        System.Diagnostics.Trace.WriteLine($"Visitors: a {(guest.Kind == GuestKind.Butterfly ? "paper butterfly" : "gentle slime")} comes by{(guestWatcher.Follows ? ", the cat follows" : "")}.");
        return true;
    }

    // Every frame from Tick (dt 0 in Pause: the guest holds still): the timer, the guest's way, what ends it.
    private void TickGuests(double dt)
    {
        if (double.IsNaN(guestWait)) guestWait = GuestRules.NextDelay(featureRandom); // From the app's start, in RAM only.
        if (guest.Active)
        {
            bool gone = bounds != guestBounds || Math.Abs(scale - guestScale) > 0.001; // Another screen, DPI, a monitor unplugged.
            bool fade = !preferences.Guests || context.Fullscreen || Blocking(AutoBlock.AnyBattle) != AutoBlock.None
                || pet.Emotions.SleepLocked || pet.Action is PetAction.Sleeping;
            if (gone || (fade && paused)) { CloseGuest(); return; }
            if (fade) { guest.Leave(); StopGuestWatch(); }
            guest.Step(dt, GuestCatNow());
            if (!guest.Active) { CloseGuest(); return; }
            PlaceGuest();
            if ((guestOrderCheck -= dt) <= 0) { guestOrderCheck = 0.5; KeepGuestBelowCat(); }
            return;
        }
        if (paused || dt <= 0) return;
        if ((guestWait -= dt) > 0) return;
        guestWait = TryGuestVisit() ? GuestRules.NextDelay(featureRandom) : GuestRules.RetrySeconds;
    }

    // From TickFeatures with the cat free (CanPlay), after the games. True while the cat watches (it owns the cat).
    private bool TickGuestWatch(double dt)
    {
        if (!guestWatching) return false;
        if (!guest.Active || guest.Leaving || MiniGameActive || Blocking(AutoBlock.AnyQuiet | AutoBlock.Away) != AutoBlock.None) { StopGuestWatch(); return false; }
        double cat = CatCenter;
        int face = guest.X < cat ? -1 : 1;
        if (guestPounce >= 0) { TickGuestPounce(dt, face); return true; }
        if (guest.Kind == GuestKind.Butterfly && !guest.Zooming && guestWatcher.MayPounce(cat, guest.X))
        {
            guestWatcher.Pounce();
            guestPounce = 0; guestPounceDone = false;
            HoldStill(PetAction.Looking, face);
            return true;
        }
        if (guestWatcher.Chasing || guestWatcher.BeginChase(cat, guest.X))
        {
            if (guestWatcher.StepChase(dt, cat))
            {
                double groundLeft = bounds.Left, groundRight = bounds.Right;
                if (pet.LedgeId != 0 && PetLedges.Support(ledges, pet.LedgeId, cat) is Ledge ledge) { groundLeft = ledge.Left; groundRight = ledge.Right; }
                double keep = guest.Kind == GuestKind.Slime ? GuestRules.CatHalfWidth + GuestRules.SlimeHalf + GuestRules.SlimeGap : 0;
                double target = GuestRules.ChaseTarget(guestWatcher.Start, guest.X, groundLeft, groundRight, keep);
                if (Math.Abs(target - cat) > 6) { pet.ReactTilt = 0; WalkToward(target - settings.Width / 2); return true; }
            }
        }
        HoldStill(PetAction.Looking, face);
        pet.ReactTilt = Math.Clamp((pet.Y + settings.Height - guest.Y - 80) / 12, -8, 8);
        return true;
    }

    // The pounce at a butterfly: a crouch and a little hop drawn in place (pet.Y never changes: it lands where it stood).
    private void TickGuestPounce(double dt, int face)
    {
        guestPounce += dt;
        HoldStill(PetAction.Looking, face);
        pet.ReactTilt = 0;
        if (guestPounce < FireflyRules.CrouchSeconds) { pet.ReactDip = 6 * Math.Sin(Math.PI / 2 * guestPounce / FireflyRules.CrouchSeconds); return; }
        double hop = (guestPounce - FireflyRules.CrouchSeconds) / FireflyRules.HopSeconds;
        pet.ReactDip = 0;
        renderer.TeleportDrop = FireflyRules.HopHeight * Math.Sin(Math.PI * Math.Min(1, hop));
        if (!guestPounceDone && hop >= 0.15) { pet.BatRemaining = 0.3; PlayPetSound("ball_tap"); guestPounceDone = true; }
        if (hop >= 1) EndGuestPounce();
    }

    private void EndGuestPounce()
    {
        if (guestPounce < 0) return;
        guestPounce = -1;
        renderer.TeleportDrop = 0;
        pet.ReactDip = pet.ReactTilt = 0;
    }

    // The cat stops watching (a click or drag on it, an emotion, Quiet...); the guest goes on its way.
    private void StopGuestWatch()
    {
        if (!guestWatching) return;
        guestWatching = false;
        EndGuestPounce();
        guestWatcher.EndChase();
        pet.ReactTilt = 0;
        if (!dragging && pet.Action is PetAction.Looking or PetAction.Walking) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
    }

    // A click on the guest: the butterfly zooms off with sparks, the slime bounces. True when it reacted.
    public bool ClickGuest()
    {
        if (paused || !guest.Click()) return false;
        if (guest.Kind == GuestKind.Butterfly) { guestArt?.Zoomed(); PlayPetSound("sparkle"); }
        return true;
    }

    private void ShowGuest()
    {
        guestWindow?.Close();
        guestArt = new GuestArt(guest) { Cursor = System.Windows.Input.Cursors.Hand };
        guestWindow = CreateOverlayWindow(guestArt, "Pet visitor");
        guestWindow.Width = guestArt.Width; guestWindow.Height = guestArt.Height;
        guestArt.MouseLeftButtonDown += (_, e) => { e.Handled = true; ClickGuest(); };
        var shown = guestWindow;
        guestWindow.Closed += (_, _) => { if (guestWindow == shown) { guestWindow = null; guestArt = null; } };
        PlaceGuest();
        guestWindow.Show();
        KeepGuestBelowCat();
        guestOrderCheck = 0.5;
    }

    private void PlaceGuest()
    {
        if (guestWindow == null || guestArt == null) return;
        guestArt.InvalidateVisual();
        double left = guest.X - guestArt.Width / 2;
        double top = guest.Kind == GuestKind.Butterfly ? guest.Y - guestArt.Height / 2 : guest.Floor - guestArt.Height + GuestArt.SlimeBase;
        MoveWindow(guestWindow, left, top);
    }

    // The guest's window stays under the cat's, so the cat (and a click on it) is always in front.
    private void KeepGuestBelowCat()
    {
        var cat = new WindowInteropHelper(this).Handle;
        if (cat == IntPtr.Zero || guestWindow == null) return;
        var handle = new WindowInteropHelper(guestWindow).Handle;
        if (handle == IntPtr.Zero || !WindowsAbove(cat).Contains(handle)) return;
        SetWindowPos(handle, cat, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
    }

    private void CloseGuest()
    {
        StopGuestWatch();
        guest.Reset();
        guestWindow?.Close();
        guestWindow = null; guestArt = null;
    }
}
