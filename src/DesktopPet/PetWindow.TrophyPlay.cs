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
    // The cat went to look because the user double-clicked (allowed in Quiet: the user asked).
    private bool admireAsked;

    // Napping on the slime cushion: walking over, then asleep on it (drawn lifted onto the cushion).
    private enum CushionNap { None, Walk, Asleep }
    private CushionNap cushionNap;
    private double napVisit = 360, napTime, napLift;
    public const double NapEvery = 600;

    public long ShelfLedgeId => shelfLedgeId;
    public bool NappingOnCushion => cushionNap == CushionNap.Asleep;
    public bool GoingToNap => cushionNap == CushionNap.Walk;
    public double NapLift => napLift;
    private bool ShelfOnFloor => shelfLedgeId == 0 && shelf.Ground == 0;
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
        bool standing = shelf.Height <= shelf.Ground + 0.5 && shelf.VY <= 0;
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
        if (!CanPlay || BattleActive || OnLedge || pet.LedgeId != 0 || TrophySpot() == null) return false;
        CancelFeatureActivity();
        admiring = admireAsked = true;
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
            napVisit = NapEvery + featureRandom.NextDouble() * 300;
            if (pet.Action is PetAction.Idle or PetAction.Sitting) GoNapOnCushion();
        }
        if (cushionNap != CushionNap.Walk) return false;
        napTime += dt;
        if (!CushionReady || napTime > 15) { StopCushionNap(); return false; }
        double target = Math.Clamp(ShelfCenter - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
        if (Math.Abs(target - pet.X) < 4)
        {
            pet.X = target;
            pet.VX = pet.SmoothedWalkVelocity = 0;
            pet.Action = PetAction.Idle;
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

    // The cushion is on display, on the floor of the cat's monitor, still, and free.
    private bool CushionReady => DisplayedKeepsake?.Kind == KeepsakeSpec.Cushion && ShelfBesideCat && ShelfOnFloor && shelf.Resting
        && !shelfHeld && shelfHop == ShelfHop.None && pet.LedgeId == 0;

    // For checks (and the timer): go and nap on the cushion now. False when it may not.
    public bool GoNapOnCushion()
    {
        if (!CanPlay || EffectiveQuiet || BattleActive || FeatureOwnsMovement() || OnLedge || !CushionReady || pet.Emotions.WakeCooldownRemaining > 0) return false;
        cushionNap = CushionNap.Walk;
        napTime = 0;
        return true;
    }

    private void StopCushionNap()
    {
        if (cushionNap == CushionNap.Walk && pet.Action == PetAction.Walking) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
        cushionNap = CushionNap.None;
    }

    // Every frame (TickShelf): while asleep on the cushion it is drawn lifted onto it; it ends with
    // the sleep (waking up there is fine) or when the cushion is gone from under it.
    private void TickNapLift(double dt)
    {
        if (cushionNap == CushionNap.Asleep)
        {
            bool still = pet.Action is PetAction.Sleeping or PetAction.Waking && pet.Grounded && CushionReady && Math.Abs(ShelfCenter - CatCenter) < 24;
            if (!still) cushionNap = CushionNap.None;
        }
        double target = cushionNap == CushionNap.Asleep ? CushionSurface : 0;
        napLift += (target - napLift) * (1 - Math.Exp(-dt * 12));
        if (Math.Abs(napLift - target) < 0.05) napLift = target;
    }
}
