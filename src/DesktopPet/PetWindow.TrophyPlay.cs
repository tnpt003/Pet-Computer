using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DesktopPet;

// More play with the keepsake on display (2026-10-22): its right-click menu (swap the keepsake with a
// puff, back to the middle, hide), standing on other windows' top edges (the same ledges the cat
// walks on; carried along, falls when the window goes), the cat watching it while it is held and
// startled when it lands close by, a hover wiggle, a double click calling the cat over, the toy ball
// bouncing off it, and the slime cushion as a bed the cat now and then naps on. All C#.
public sealed partial class PetWindow
{
    // The window edge it stands on (0: the floor) and that window's last known left/top (DIPs).
    private long shelfLedgeId;
    private double shelfLedgeLeft, shelfLedgeTop;
    private double shelfHoverBlend, shelfStartleAt;
    // A keepsake swap under way (from the menu): the one that appears after the puff.
    private string? shelfSwapTo;
    // Keepsake activities the user asked for (double click to look, "Do now" cushion nap): they
    // keep going in Quiet, which only stops the cat's own ideas.
    [Flags] private enum UserAsked { None = 0, Admire = 1, Nap = 2 }
    private UserAsked userAsked;
    private bool Asked(UserAsked what) => (userAsked & what) != 0;
    // "Do now → Sleep on the cushion" only when the cat is this close to it (centre to centre).
    public const double CushionNearDistance = 200;

    // Napping on the slime cushion: walking over, then asleep on it (drawn lifted onto the cushion).
    private enum CushionNap { None, Walk, Asleep }
    private CushionNap cushionNap;
    private double napVisit = 240, napTime, napLift;
    // The cushion draws naps: every 4–7 minutes, a sleep the cat picks on its own, and a little before
    // it would get sleepy from being left alone.
    public const double NapEvery = 240, NapBeforeSleepy = 25, NapWalkSeconds = 20, CushionReach = 1200;

    public long ShelfLedgeId => shelfLedgeId;
    public bool NappingOnCushion => cushionNap == CushionNap.Asleep;
    public bool GoingToNap => cushionNap == CushionNap.Walk;
    public double NapLift => napLift;
    private bool ShelfOnFloor => shelfLedgeId == 0 && shelf.Ground == 0;
    // On the same ground as the cat: both on the floor, or both on the same window's top edge.
    private bool ShelfWithCat => ShelfBesideCat && pet.Grounded && (ShelfOnFloor ? pet.LedgeId == 0 : pet.LedgeId == shelfLedgeId && OnLedge);
    private int FacingShelf => ShelfCenter < CatCenter ? -1 : 1;

    // Where the cat can stand (its left edge) on the ground it shares with the keepsake; null: no room.
    private double? CatCanStand(double x)
    {
        if (x < bounds.Left || x > bounds.Right - settings.Width) return null;
        return pet.LedgeId == 0 || PetLedges.Support(ledges, pet.LedgeId, x + settings.Width / 2) != null ? x : null;
    }

    // The cat stands still in this pose, facing this way.
    private void HoldStill(PetAction action, int facing)
    {
        pet.Action = action;
        pet.VX = pet.SmoothedWalkVelocity = 0;
        pet.Facing = facing;
    }
    private double ShelfCenter => shelf.X + ShelfArtWidth / 2;
    private double CatCenter => pet.X + settings.Width / 2;
    // The cushion's soft top above the floor, as the sleeping cat sinks into it a little.
    private static double CushionSurface => (TrophyShelf.ShelfHeight + TrophyShelf.CushionHeight * 0.6) * DisplayScale;

    // The keepsakes that can be on display, cups best first, then the cushion (menu and Settings).
    public IReadOnlyList<KeepsakeSpec> DisplayableKeepsakes =>
        ShownKeepsakes.OrderBy(k => k.Kind == KeepsakeSpec.Cushion ? 1 : -Array.IndexOf(KeepsakeSpec.Tiers, k.Tier)).ToList();

    // Right click: the owned keepsakes (✓ the one shown), back to the middle, hide.
    private void InitializeShelfMenu()
    {
        if (trophyShelf == null) return;
        var menu = new ContextMenu { FontFamily = PetUi.TextFont, FontSize = 13 };
        PetMenuStyle.Apply(menu);
        trophyShelf.ContextMenu = menu;
        trophyShelf.ContextMenuOpening += (_, e) =>
        {
            if (shelfHeld || paused) { e.Handled = true; return; }
            menu.Items.Clear();
            bool english = preferences.Language == "en";
            string? shown = DisplayedKeepsake?.Id;
            foreach (var keepsake in DisplayableKeepsakes)
            {
                string id = keepsake.Id;
                var item = new MenuItem { Header = keepsake.Name(english), IsCheckable = true, IsChecked = id == shown, IsEnabled = shelfHop == ShelfHop.None };
                item.Click += (_, _) => SwapDisplayed(id);
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
            var middle = new MenuItem { Header = L("Đưa về giữa màn hình", "Back to the middle"), IsEnabled = shelfHop == ShelfHop.None };
            middle.Click += (_, _) => { if (shelfHop == ShelfHop.None) StartShelfHop(alarm: false); };
            var hide = new MenuItem { Header = L("Ẩn món trưng bày", "Hide the keepsake") };
            hide.Click += (_, _) => { preferences.ShowTrophies = false; StopAdmiring(); SavePreferences(); };
            menu.Items.Add(middle); menu.Items.Add(hide);
        };
    }

    // Swaps the keepsake on display: the old one puffs away, the new one pops up in its place.
    public bool SwapDisplayed(string id)
    {
        if (shelfHop != ShelfHop.None || shelfHeld || id == DisplayedKeepsake?.Id || ShownKeepsakes.All(k => k.Id != id)) return false;
        shelfSwapTo = id;
        shelfHop = ShelfHop.Vanish;
        shelfHopTime = 0;
        shelfHopTo = shelf.X;
        PlayPetSound("swoosh");
        return true;
    }

    // At the end of the puff: the new keepsake, centred where the old one stood.
    private void ApplyShelfSwap()
    {
        if (shelfSwapTo == null) return;
        double center = ShelfCenter;
        preferences.TrophyShown = shelfSwapTo;
        shelfSwapTo = null;
        SavePreferences();
        TickTrophies();
        shelf.X = Math.Clamp(center - ShelfArtWidth / 2, shelfArea.Left, Math.Max(shelfArea.Left, shelfArea.Right - ShelfArtWidth));
    }

    // Window edges: the cat's ledge list (its monitor, "Climb on windows" on); the shelf's monitor must be the cat's.
    private bool ShelfLedgesUsable => LedgesEnabled && ShelfBesideCat && ledges.Count > 0;

    private void LeaveShelfLedge() { shelfLedgeId = 0; shelf.Ground = 0; }

    // Before the step: ride along with the window it stands on, or fall when it is gone.
    private void FollowShelfLedge()
    {
        if (shelfLedgeId == 0) return;
        if (!ShelfLedgesUsable || shelfHeld || windowRectProbe(shelfLedgeId) is not WindowSnapshot now) { LeaveShelfLedge(); return; }
        // Resting on it (not still falling onto it: the landing's squash and bounce come from the step).
        bool standing = !shelf.Held && shelf.VY == 0 && shelf.Height <= shelf.Ground + 0.5;
        if (standing) shelf.X += now.Left - shelfLedgeLeft;
        shelfLedgeLeft = now.Left; shelfLedgeTop = now.Top;
        // The scanned list can lag a moved window: compare in its coordinates.
        int index = ledges.FindIndex(l => l.Id == shelfLedgeId);
        double offset = index < 0 ? 0 : now.Left - ledges[index].WindowLeft;
        if (index < 0 || PetLedges.Support(ledges, shelfLedgeId, ShelfCenter - offset) == null) { LeaveShelfLedge(); return; }
        shelf.Ground = Math.Max(0, ShelfFloor - now.Top);
        if (standing) { shelf.Height = shelf.Ground; shelf.VY = 0; }
    }

    // After the step: falling past a window's top edge lands it there (the next step squashes it).
    private void LandShelfOnLedge(double bottomBefore)
    {
        if (shelfLedgeId != 0 || shelfHeld || shelf.VY >= 0 || shelfHop == ShelfHop.Appear || !ShelfLedgesUsable) return;
        if (PetLedges.Landing(ledges, bottomBefore, ShelfFloor - shelf.Height, ShelfCenter) is not Ledge ledge
            || windowRectProbe(ledge.Id) is not WindowSnapshot now) return;
        shelfLedgeId = ledge.Id;
        shelfLedgeLeft = now.Left; shelfLedgeTop = now.Top;
        shelf.Ground = Math.Max(0, ShelfFloor - ledge.Top);
        shelf.Height = shelf.Ground + 0.01;
    }

    // While the user holds it, a free cat (same monitor) turns to watch it.
    private void WatchHeldShelf()
    {
        if (!shelfHeld || !ShelfBesideCat || BattleActive || !CanPlay || (shelfWatch <= 0 && FeatureOwnsMovement())) return;
        shelfWatch = Math.Max(shelfWatch, 0.4);
    }

    // It landed hard close to the cat (not on it): the cat jumps a little (Surprised) and looks.
    private void NoticeShelfLanding()
    {
        double now = clock.Elapsed.TotalSeconds;
        if (shelfHop != ShelfHop.None || pet.Bonk > 0 || !ShelfBesideCat || BattleActive || now < shelfStartleAt || !PetActionRules.IsSettled(pet)
            || Math.Abs(ShelfCenter - CatCenter) > 170 || (FeatureOwnsMovement() && shelfWatch <= 0)) return;
        shelfStartleAt = now + 5;
        pet.Emotions.Startle();
        shelfWatch = Math.Max(shelfWatch, 1.2);
    }

    // Hover: a little wiggle as the cursor arrives, and it stays a touch bigger while under it.
    private void HoverShelf()
    {
        if (shelfHeld || shelfHop != ShelfHop.None || paused) return;
        shelf.SquashV -= 2.2; shelf.TiltV += (shelf.TiltV >= 0 ? 1 : -1) * 25;
    }

    // Double click: the cat comes to look at it now (even in Quiet: the user asked).
    public bool CallCatToLook()
    {
        if (!CanPlay || BattleActive || TrophySpot() == null) return false;
        CancelFeatureActivity();
        admiring = true; userAsked |= UserAsked.Admire;
        admireRemaining = 0;
        return true;
    }

    // The toy ball rolling into it bounces back; the keepsake wobbles and gives a little.
    private void ShelfMeetsBall()
    {
        if (!TrophiesVisible || !ShelfBesideCat || !ShelfOnFloor || shelfHeld || shelfHop != ShelfHop.None || ball.Height > ShelfArtHeight + shelf.Height) return;
        const double radius = 12;
        double left = shelf.X, right = shelf.X + ShelfArtWidth;
        if (ball.X + radius < left || ball.X - radius > right) return;
        int side = ball.X < ShelfCenter ? -1 : 1;
        if (ball.VX * side > 0) return; // Already moving away.
        double incoming = ball.VX;
        ball.X = side < 0 ? left - radius : right + radius;
        ball.VX = side * (Math.Abs(incoming) * 0.7 + 20);
        ball.Feedback = 0.2;
        shelf.VX += incoming * 0.12;
        shelf.TiltV += -side * Math.Min(120, Math.Abs(incoming) * 0.4);
        shelf.SquashV += 1.2;
        PlayPetSound("ball_tap");
    }

    // From TickFeatures (free pet, not Quiet): now and then, with the cushion on display, walk to it
    // and fall asleep on it. True while walking there.
    private bool TickCushionNap(double dt)
    {
        napVisit -= dt;
        if (cushionNap == CushionNap.None && napVisit <= 0)
        {
            napVisit = NapEvery + featureRandom.NextDouble() * 180;
            if (pet.Action is PetAction.Idle or PetAction.Sitting) GoNapOnCushion();
        }
        // Left alone and nearly sleepy: to the cushion first, rather than falling asleep where it stands.
        if (cushionNap == CushionNap.None && pet.Emotions.SecondsWithoutAttention >= PetEmotions.SleepyAfter - NapBeforeSleepy
            && pet.Emotions.SecondsWithoutAttention < PetEmotions.SleepyAfter && pet.Action is PetAction.Idle or PetAction.Sitting) GoNapOnCushion();
        if (cushionNap != CushionNap.Walk) return false;
        napTime += dt;
        if (!CushionReady || napTime > NapWalkSeconds) { StopCushionNap(); return false; }
        double target = Math.Clamp(ShelfCenter - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
        if (Math.Abs(target - pet.X) < 4)
        {
            pet.X = target;
            HoldStill(PetAction.Idle, pet.Facing);
            if (behavior.StartManual(pet, PetAction.Sleeping))
            {
                cushionNap = CushionNap.Asleep;
                System.Diagnostics.Trace.WriteLine("The cat naps on the slime cushion.");
            }
            else StopCushionNap();
            return cushionNap == CushionNap.Asleep;
        }
        WalkToward(target);
        return true;
    }

    // The cushion is on display, on the ground the cat stands on (floor or the same window), still, and free.
    private bool CushionReady => DisplayedKeepsake?.Kind == KeepsakeSpec.Cushion && ShelfWithCat && shelf.Resting
        && !shelfHeld && shelfHop == ShelfHop.None && CatCanStand(ShelfCenter - settings.Width / 2) != null;
    // Close enough for "Do now → Sleep on the cushion".
    public bool CushionNear => CushionReady && Math.Abs(ShelfCenter - CatCenter) <= CushionNearDistance;

    // For checks (and the timers): go and nap on the cushion now. False when it may not.
    public bool GoNapOnCushion()
    {
        if (!CanPlay || EffectiveQuiet || BattleActive || FeatureOwnsMovement() || !CushionReady || pet.Emotions.WakeCooldownRemaining > 0
            || Math.Abs(ShelfCenter - CatCenter) > CushionReach) return false;
        cushionNap = CushionNap.Walk;
        napTime = 0;
        return true;
    }

    // "Do now → Sleep on the cushion": only near it; like every "Do now" also in Quiet and during the
    // wake cooldown (the sleep rules themselves stay). Not while the pet cannot act (asleep, held, flying).
    public bool SleepOnCushionNow()
    {
        if (paused || closed || dragging || BattleOwnsPet || !CushionNear || !PetBehavior.CanStartManual(pet, PetAction.Sleeping)) return false;
        revision++; // Like DoNow: AI replies chosen before this request are dropped.
        CancelFeatureActivity();
        cushionNap = CushionNap.Walk;
        userAsked |= UserAsked.Nap;
        napTime = 0;
        return true;
    }

    // PetBehavior picked a sleep of its own: with the cushion on display it goes there to sleep instead.
    private bool SleepOnCushionInstead(PetModel _) => GoNapOnCushion();

    private void StopCushionNap()
    {
        if (cushionNap == CushionNap.Walk && pet.Action == PetAction.Walking) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
        cushionNap = CushionNap.None;
        userAsked &= ~UserAsked.Nap;
    }

    // Dropped or thrown onto the slime cushion (only the cushion): a trampoline. The cat bounces up
    // higher than off the floor and a little to the side, so it comes down beside it after a hop or two.
    // From the physics loop, after each step (PetPhysics is unchanged).
    private void BounceOnCushion(double previousFeet)
    {
        if (pet.Action != PetAction.Thrown || pet.VY < 250 || DisplayedKeepsake?.Kind != KeepsakeSpec.Cushion || !ShelfBesideCat
            || shelfHeld || shelfHop != ShelfHop.None || !shelf.Grounded) return;
        double surface = ShelfFloor - shelf.Height - (TrophyShelf.ShelfHeight + TrophyShelf.CushionHeight - 3) * DisplayScale;
        double feet = pet.Y + settings.Height;
        if (previousFeet > surface + 0.5 || feet < surface || CatCenter < shelf.X + 4 || CatCenter > shelf.X + ShelfArtWidth - 4) return;
        double impact = pet.VY;
        pet.Y = surface - settings.Height;
        pet.VY = -Math.Min(1100, impact * 0.9 + 140);
        int away = Math.Abs(CatCenter - ShelfCenter) < 1 ? (featureRandom.NextDouble() < 0.5 ? -1 : 1) : CatCenter < ShelfCenter ? -1 : 1;
        pet.VX += away * 150;
        shelf.SquashV += Math.Clamp(impact / 1000 * 7, 2.5, 9);
        shelfDust = 0; shelfDustStrength = Math.Clamp(impact / 1200, 0.3, 1);
        PlayPetSound("squish");
    }

    // Every frame (TickShelf): while asleep on the cushion it is drawn lifted onto it; it ends with
    // the sleep (waking up there is fine) or when the cushion is gone from under it.
    private void TickNapLift(double dt)
    {
        if (cushionNap == CushionNap.Asleep)
        {
            bool still = pet.Action is PetAction.Sleeping or PetAction.Waking && pet.Grounded && CushionReady && Math.Abs(ShelfCenter - CatCenter) < 24;
            if (!still) { cushionNap = CushionNap.None; userAsked &= ~UserAsked.Nap; }
        }
        double target = cushionNap == CushionNap.Asleep ? CushionSurface : 0;
        napLift += (target - napLift) * (1 - Math.Exp(-dt * 12));
        if (Math.Abs(napLift - target) < 0.05) napLift = target;
    }
}
