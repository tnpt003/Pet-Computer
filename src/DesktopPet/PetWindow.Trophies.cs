using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopPet;

// Expedition milestone 4: the display corner. The keepsakes a won run gave that are shown (cups, a
// slime cushion) stand on a small shelf at the bottom right of the cat's monitor. On once the first
// keepsake is earned; Settings → Pet turns it off. Click-through, never takes focus, no taskbar
// button; hidden while a fullscreen app runs or a battle is on. Drawn again only when the list
// changes. Now and then (not in Quiet, asleep, held, in a battle) the cat walks over and looks at
// it for a few seconds: a C# walk like the rest-spot visit, Python is not involved.
public sealed partial class PetWindow
{
    private Window? trophyWindow;
    private TrophyShelf? trophyShelf;
    private int trophyCount = -1;
    // Seconds until the cat next goes to look at the shelf, and how long it still looks.
    private double admireVisit = 300, admireRemaining;
    private bool admiring;
    public const double AdmireSeconds = 4, AdmireEvery = 600;

    // The keepsakes in the saved Expedition progress (never the in-memory one of custom data), read
    // with the built-in definitions.
    private ExpeditionProgress SavedExpeditionProgress =>
        expeditionProgress ??= ExpeditionProgressFile != null ? ExpeditionProgress.Load(ExpeditionProgressFile) : new();
    private static IReadOnlyList<KeepsakeSpec> KeepsakeList => BattleData.Settings.Expedition?.Keepsakes ?? Array.Empty<KeepsakeSpec>();
    public IReadOnlyList<string> Keepsakes => SavedExpeditionProgress.Keepsakes;
    // Shown on the shelf: the cushion first, then the cups bronze → gold.
    public IReadOnlyList<KeepsakeSpec> ShownKeepsakes => KeepsakeList.Where(k => k.Shown && Keepsakes.Contains(k.Id))
        .OrderBy(k => k.Kind == KeepsakeSpec.Cushion ? -1 : Array.IndexOf(KeepsakeSpec.Tiers, k.Tier)).ToList();
    private bool KeepsakeAccessory(string id) => KeepsakeList.Any(k => k.Accessory == id && Keepsakes.Contains(k.Id));

    public bool TrophiesVisible => trophyWindow?.IsVisible == true;
    public Rect TrophyBounds => trophyWindow == null ? Rect.Empty : new Rect(trophyWindow.Left, trophyWindow.Top, trophyWindow.Width, trophyWindow.Height);
    public bool Admiring => admiring;

    // Every frame, paused or not (cheap: only a list count and a position).
    private void TickTrophies()
    {
        var shown = preferences.ShowTrophies && !context.Fullscreen && !BattleActive ? ShownKeepsakes : Array.Empty<KeepsakeSpec>();
        if (shown.Count == 0)
        {
            if (trophyWindow?.IsVisible == true) trophyWindow.Hide();
            return;
        }
        if (trophyWindow == null)
        {
            trophyShelf = new TrophyShelf();
            trophyWindow = new Window
            {
                Title = "Pet trophies", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true, Background = Brushes.Transparent,
                ShowInTaskbar = false, ShowActivated = false, Topmost = true, Focusable = false, SizeToContent = SizeToContent.Manual, Content = trophyShelf
            };
            trophyWindow.SourceInitialized += (_, _) =>
            {
                // Click-through, a tool window that never activates.
                const int ExStyle = -20;
                const long Transparent = 0x20, ToolWindow = 0x80, NoActivate = 0x08000000;
                var handle = new WindowInteropHelper(trophyWindow).Handle;
                SetWindowLongPtr(handle, ExStyle, new IntPtr(GetWindowLongPtr(handle, ExStyle).ToInt64() | Transparent | ToolWindow | NoActivate));
            };
            trophyWindow.Closed += (_, _) => { trophyWindow = null; trophyShelf = null; trophyCount = -1; };
            trophyCount = -1;
        }
        if (trophyCount != shown.Count)
        {
            trophyCount = shown.Count;
            trophyShelf!.Items = shown;
            trophyWindow.Width = trophyShelf.Width;
            trophyWindow.Height = trophyShelf.Height;
        }
        if (!trophyWindow.IsVisible) trophyWindow.Show();
        // Bottom right of the cat's monitor, standing on the floor the cat walks on.
        MoveWindow(trophyWindow, Math.Max(bounds.Left, bounds.Right - trophyWindow.Width - 10), bounds.Bottom - trophyWindow.Height);
    }

    // Where the cat stands to look (just left of the shelf, facing it); null: nothing to look at here.
    private double? TrophySpot()
    {
        if (!TrophiesVisible || trophyWindow == null) return null;
        double target = trophyWindow.Left - settings.Width * 0.72;
        return target < bounds.Left ? null : target;
    }

    // For checks: send the cat to look now (as the timer would). False when it may not.
    public bool AdmireTrophies()
    {
        if (!CanPlay || EffectiveQuiet || BattleActive || FeatureOwnsMovement() || TrophySpot() == null || OnLedge) return false;
        admiring = true;
        admireRemaining = 0;
        return true;
    }

    // From TickFeatures (the pet free, not in Quiet). True while the visit owns the cat this frame.
    private bool TickAdmiring(double dt)
    {
        admireVisit -= dt;
        if (!admiring && admireVisit <= 0)
        {
            admireVisit = AdmireEvery + featureRandom.NextDouble() * 300;
            if (!returningHome && climbPlan == null && ledgeExit == 0 && !OnLedge && pet.Action is PetAction.Idle or PetAction.Sitting) AdmireTrophies();
        }
        if (!admiring) return false;
        if (TrophySpot() is not double target) { StopAdmiring(); return false; }
        if (admireRemaining > 0)
        {
            admireRemaining = Math.Max(0, admireRemaining - dt);
            pet.Action = PetAction.Looking; pet.VX = pet.SmoothedWalkVelocity = 0; pet.Facing = 1;
            if (admireRemaining <= 0) StopAdmiring();
            return true;
        }
        if (Math.Abs(target - pet.X) < 8)
        {
            admireRemaining = AdmireSeconds;
            pet.Action = PetAction.Looking; pet.VX = pet.SmoothedWalkVelocity = 0; pet.Facing = 1;
            System.Diagnostics.Trace.WriteLine("The cat looks at its keepsakes.");
        }
        else WalkToward(target);
        return true;
    }

    private void StopAdmiring()
    {
        if (!admiring) return;
        admiring = false;
        admireRemaining = 0;
        if (pet.Action is PetAction.Looking or PetAction.Walking) pet.Action = PetAction.Idle;
        pet.VX = 0;
        behavior.Reset(pet);
    }

    private void CloseTrophies() { trophyWindow?.Close(); trophyWindow = null; }
}
