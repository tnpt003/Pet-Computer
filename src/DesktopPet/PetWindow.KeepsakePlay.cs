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
    private long keepsakeLedgeId;
    private double keepsakeLedgeLeft, keepsakeLedgeTop;
    private double keepsakeHoverBlend, keepsakeStartleAt;
    // A keepsake swap under way (from the menu): the one that appears after the puff.
    private string? keepsakeSwapTo;
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

    public long KeepsakeLedgeId => keepsakeLedgeId;
    public bool NappingOnCushion => cushionNap == CushionNap.Asleep;
    public bool GoingToNap => cushionNap == CushionNap.Walk;
    public double NapLift => napLift;
    private bool KeepsakeOnFloor => keepsakeLedgeId == 0 && keepsake.Ground == 0;
    // On the same ground as the cat: both on the floor, or both on the same window's top edge.
    private bool KeepsakeWithCat => KeepsakeBesideCat && pet.Grounded && (KeepsakeOnFloor ? pet.LedgeId == 0 : pet.LedgeId == keepsakeLedgeId && OnLedge);
    private int FacingKeepsake => KeepsakeCenter < CatCenter ? -1 : 1;

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
    private double KeepsakeCenter => keepsake.X + KeepsakeArtWidth / 2;
    private double CatCenter => pet.X + settings.Width / 2;
    // The cushion's soft top above the floor, as the sleeping cat sinks into it a little.
    private static double CushionSurface => (KeepsakeArt.StandHeight + KeepsakeArt.CushionHeight * 0.6) * DisplayScale;

    // The keepsakes that can be on display, cups best first, then the cushion (menu and Settings).
    public IReadOnlyList<KeepsakeSpec> DisplayableKeepsakes =>
        ShownKeepsakes.OrderBy(k => k.Kind == KeepsakeSpec.Cushion ? 1 : -Array.IndexOf(KeepsakeSpec.Tiers, k.Tier)).ToList();

    // Right click: the owned keepsakes (✓ the one shown), back to the middle, hide.
    private void InitializeKeepsakeMenu()
    {
        if (keepsakeArt == null) return;
        var menu = new ContextMenu { FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize };
        PetMenuStyle.Apply(menu);
        keepsakeArt.ContextMenu = menu;
        keepsakeArt.ContextMenuOpening += (_, e) =>
        {
            if (keepsakeHeld || paused) { e.Handled = true; return; }
            menu.Items.Clear();
            bool english = English;
            string? shown = DisplayedKeepsake?.Id;
            foreach (var keepsake in DisplayableKeepsakes)
            {
                string id = keepsake.Id;
                var item = new MenuItem { Header = keepsake.Name(english), IsCheckable = true, IsChecked = id == shown, IsEnabled = keepsakeHop == KeepsakeHop.None };
                item.Click += (_, _) => SwapDisplayed(id);
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
            var middle = new MenuItem { Header = L("Đưa về giữa màn hình", "Back to the middle"), IsEnabled = keepsakeHop == KeepsakeHop.None };
            middle.Click += (_, _) => { if (keepsakeHop == KeepsakeHop.None) StartKeepsakeHop(alarm: false); };
            var hide = new MenuItem { Header = L("Ẩn món trưng bày", "Hide the keepsake") };
            hide.Click += (_, _) => { preferences.ShowTrophies = false; StopAdmiring(); SavePreferences(); };
            menu.Items.Add(middle); menu.Items.Add(hide);
        };
    }

    // Swaps the keepsake on display: the old one puffs away, the new one pops up in its place.
    public bool SwapDisplayed(string id)
    {
        if (keepsakeHop != KeepsakeHop.None || keepsakeHeld || id == DisplayedKeepsake?.Id || ShownKeepsakes.All(k => k.Id != id)) return false;
        keepsakeSwapTo = id;
        keepsakeHop = KeepsakeHop.Vanish;
        keepsakeHopTime = 0;
        keepsakeHopTo = keepsake.X;
        PlayPetSound("swoosh");
        return true;
    }

    // At the end of the puff: the new keepsake, centred where the old one stood.
    private void ApplyKeepsakeSwap()
    {
        if (keepsakeSwapTo == null) return;
        double center = KeepsakeCenter;
        preferences.TrophyShown = keepsakeSwapTo;
        keepsakeSwapTo = null;
        SavePreferences();
        TickTrophies();
        keepsake.X = Math.Clamp(center - KeepsakeArtWidth / 2, keepsakeArea.Left, Math.Max(keepsakeArea.Left, keepsakeArea.Right - KeepsakeArtWidth));
    }

    // Window edges: the cat's ledge list (its monitor, "Climb on windows" on); the keepsake's monitor must be the cat's.
    private bool KeepsakeLedgesUsable => LedgesEnabled && KeepsakeBesideCat && ledges.Count > 0;

    private void LeaveKeepsakeLedge() { keepsakeLedgeId = 0; keepsake.Ground = 0; }

    // Before the step: ride along with the window it stands on, or fall when it is gone.
    private void FollowKeepsakeLedge()
    {
        if (keepsakeLedgeId == 0) return;
        if (!KeepsakeLedgesUsable || keepsakeHeld || windowRectProbe(keepsakeLedgeId) is not WindowSnapshot now) { LeaveKeepsakeLedge(); return; }
        // Resting on it (not still falling onto it: the landing's squash and bounce come from the step).
        bool standing = !keepsake.Held && keepsake.VY == 0 && keepsake.Height <= keepsake.Ground + 0.5;
        if (standing) keepsake.X += now.Left - keepsakeLedgeLeft;
        keepsakeLedgeLeft = now.Left; keepsakeLedgeTop = now.Top;
        // The scanned list can lag a moved window: compare in its coordinates.
        int index = ledges.FindIndex(l => l.Id == keepsakeLedgeId);
        double offset = index < 0 ? 0 : now.Left - ledges[index].WindowLeft;
        if (index < 0 || PetLedges.Support(ledges, keepsakeLedgeId, KeepsakeCenter - offset) == null) { LeaveKeepsakeLedge(); return; }
        keepsake.Ground = Math.Max(0, KeepsakeFloor - now.Top);
        if (standing) { keepsake.Height = keepsake.Ground; keepsake.VY = 0; }
    }

    // After the step: falling past a window's top edge lands it there (the next step squashes it).
    private void LandKeepsakeOnLedge(double bottomBefore)
    {
        if (keepsakeLedgeId != 0 || keepsakeHeld || keepsake.VY >= 0 || keepsakeHop == KeepsakeHop.Appear || !KeepsakeLedgesUsable) return;
        if (PetLedges.Landing(ledges, bottomBefore, KeepsakeFloor - keepsake.Height, KeepsakeCenter) is not Ledge ledge
            || windowRectProbe(ledge.Id) is not WindowSnapshot now) return;
        keepsakeLedgeId = ledge.Id;
        keepsakeLedgeLeft = now.Left; keepsakeLedgeTop = now.Top;
        keepsake.Ground = Math.Max(0, KeepsakeFloor - ledge.Top);
        keepsake.Height = keepsake.Ground + 0.01;
    }

    // While the user holds it, a free cat (same monitor) turns to watch it.
    private void WatchHeldKeepsake()
    {
        if (!keepsakeHeld || !KeepsakeBesideCat || BattleActive || !CanPlay || (keepsakeWatch <= 0 && FeatureOwnsMovement())) return;
        keepsakeWatch = Math.Max(keepsakeWatch, 0.4);
    }

    // It landed hard close to the cat (not on it): the cat jumps a little (Surprised) and looks.
    private void NoticeKeepsakeLanding()
    {
        double now = clock.Elapsed.TotalSeconds;
        if (keepsakeHop != KeepsakeHop.None || pet.Bonk > 0 || !KeepsakeBesideCat || BattleActive || now < keepsakeStartleAt || !PetActionRules.IsSettled(pet)
            || Math.Abs(KeepsakeCenter - CatCenter) > 170 || (FeatureOwnsMovement() && keepsakeWatch <= 0)) return;
        keepsakeStartleAt = now + 5;
        pet.Emotions.Startle();
        keepsakeWatch = Math.Max(keepsakeWatch, 1.2);
    }

    // Hover: a little wiggle as the cursor arrives, and it stays a touch bigger while under it.
    private void HoverKeepsake()
    {
        if (keepsakeHeld || keepsakeHop != KeepsakeHop.None || paused) return;
        keepsake.SquashV -= 2.2; keepsake.TiltV += (keepsake.TiltV >= 0 ? 1 : -1) * 25;
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
    private void KeepsakeMeetsBall()
    {
        if (!TrophiesVisible || !KeepsakeBesideCat || !KeepsakeOnFloor || keepsakeHeld || keepsakeHop != KeepsakeHop.None || ball.Height > KeepsakeArtHeight + keepsake.Height) return;
        const double radius = 12;
        double left = keepsake.X, right = keepsake.X + KeepsakeArtWidth;
        if (ball.X + radius < left || ball.X - radius > right) return;
        int side = ball.X < KeepsakeCenter ? -1 : 1;
        if (ball.VX * side > 0) return; // Already moving away.
        double incoming = ball.VX;
        ball.X = side < 0 ? left - radius : right + radius;
        ball.VX = side * (Math.Abs(incoming) * 0.7 + 20);
        ball.Feedback = 0.2;
        keepsake.VX += incoming * 0.12;
        keepsake.TiltV += -side * Math.Min(120, Math.Abs(incoming) * 0.4);
        keepsake.SquashV += 1.2;
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
        double target = Math.Clamp(KeepsakeCenter - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
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
    private bool CushionReady => DisplayedKeepsake?.Kind == KeepsakeSpec.Cushion && KeepsakeWithCat && keepsake.Resting
        && !keepsakeHeld && keepsakeHop == KeepsakeHop.None && CatCanStand(KeepsakeCenter - settings.Width / 2) != null;
    // Close enough for "Do now → Sleep on the cushion".
    public bool CushionNear => CushionReady && Math.Abs(KeepsakeCenter - CatCenter) <= CushionNearDistance;

    // For checks (and the timers): go and nap on the cushion now. False when it may not.
    public bool GoNapOnCushion()
    {
        if (!CanPlay || EffectiveQuiet || BattleActive || FeatureOwnsMovement() || !CushionReady || pet.Emotions.WakeCooldownRemaining > 0
            || Math.Abs(KeepsakeCenter - CatCenter) > CushionReach) return false;
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
        if (pet.Action != PetAction.Thrown || pet.VY < 250 || DisplayedKeepsake?.Kind != KeepsakeSpec.Cushion || !KeepsakeBesideCat
            || keepsakeHeld || keepsakeHop != KeepsakeHop.None || !keepsake.Grounded) return;
        double surface = KeepsakeFloor - keepsake.Height - (KeepsakeArt.StandHeight + KeepsakeArt.CushionHeight - 3) * DisplayScale;
        double feet = pet.Y + settings.Height;
        if (previousFeet > surface + 0.5 || feet < surface || CatCenter < keepsake.X + 4 || CatCenter > keepsake.X + KeepsakeArtWidth - 4) return;
        double impact = pet.VY;
        pet.Y = surface - settings.Height;
        pet.Motion.Contact(impact, pet); // The cat's own squash, dust and sound, like any landing.
        pet.VY = -Math.Min(1100, impact * 0.9 + 140);
        int away = Math.Abs(CatCenter - KeepsakeCenter) < 1 ? (featureRandom.NextDouble() < 0.5 ? -1 : 1) : CatCenter < KeepsakeCenter ? -1 : 1;
        pet.VX += away * 150;
        keepsake.SquashV += Math.Clamp(impact / 1000 * 7, 2.5, 9);
        keepsakeLanding.Hit(impact, quiet: true); // One sound: the cat's.
    }

    // Every frame (TickKeepsake): while asleep on the cushion it is drawn lifted onto it; it ends with
    // the sleep (waking up there is fine) or when the cushion is gone from under it.
    private void TickNapLift(double dt)
    {
        if (cushionNap == CushionNap.Asleep)
        {
            bool still = pet.Action is PetAction.Sleeping or PetAction.Waking && pet.Grounded && CushionReady && Math.Abs(KeepsakeCenter - CatCenter) < 24;
            if (!still) { cushionNap = CushionNap.None; userAsked &= ~UserAsked.Nap; }
        }
        double target = cushionNap == CushionNap.Asleep ? CushionSurface : 0;
        napLift += (target - napLift) * (1 - Math.Exp(-dt * 12));
        if (Math.Abs(napLift - target) < 0.05) napLift = target;
    }
}
