using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DesktopPet;

// The magic portal (1.5.1 M1), the way into Battle Mode: one appears on the floor beside the cat
// now and then (the cat is startled), or the cat summons one ("Cast portal spell" on the menu).
// Clicking it opens the battle hub; left alone it closes by itself, with nothing lost.
// The portal never starts a battle and never takes focus. See docs/APPROVED-EXCEPTIONS.md.
public sealed partial class PetWindow
{
    private readonly PetPortal portal = new();
    private Window? portalWindow;
    private PetPortalArt? portalArt;
    private FrameworkElement? portalLabel;
    private Area portalBounds;

    public bool PortalOpen => portalWindow != null;
    public bool PortalCasting => pet.CastRemaining > 0;
    public PortalPhase PortalPhaseNow => portal.Phase;

    // Null when the cat may start the spell, otherwise why not.
    public string? PortalCastRefusal()
    {
        if (BattleRefusal() is string reason) return reason;
        if (portalWindow != null) return L("Cổng đang mở.", "A portal is already open.");
        if (pet.CastRemaining > 0) return L("Mèo đang niệm phép.", "The pet is casting.");
        return MayAct(AutoKind.UserGame) ? null : L("Mèo đang bận, thử lại sau giây lát.", "The pet is busy; try again in a moment.");
    }

    // The menu's spell: null when the cat began casting, otherwise why not.
    public string? CastPortal()
    {
        if (PortalCastRefusal() is string reason) return reason;
        CancelFeatureActivity();
        pet.CastRemaining = PetModel.CastDuration;
        pet.Action = PetAction.Sitting;
        pet.VX = pet.SmoothedWalkVelocity = 0;
        Interact("portal_cast");
        PlayPetSound("excited");
        return null;
    }

    // A click on the portal: the battle hub opens and the portal closes. Null on success.
    public string? EnterPortal()
    {
        if (portalWindow == null || portal.Phase == PortalPhase.Closing) return L("Cổng đã đóng.", "The portal has closed.");
        if (OpenBattleHub() is string reason) return reason;
        portal.BeginClose();
        return null;
    }

    // One portal now, if this is a moment one may come by itself. True when it appeared.
    public bool TryPortalEvent()
    {
        if (portalWindow != null || pet.CastRemaining > 0 || !MayAct(AutoKind.PortalEvent) || BattleRefusal() != null) return false;
        ShowPortal(PetPortal.EventLifetime);
        pet.VX = pet.SmoothedWalkVelocity = 0;
        if (pet.Action == PetAction.Walking) pet.Action = PetAction.Idle;
        pet.Emotions.Startle(); // Its look and cue; not in manual emotion control.
        Interact("portal_appeared");
        return true;
    }

    // Each unpaused frame. True while the spell owns the cat this frame.
    private bool TickPortal(double dt)
    {
        if (portalWindow != null)
        {
            // Never over a fullscreen app; a work area that changed leaves the art in the wrong place.
            if (context.Fullscreen || bounds != portalBounds) ClosePortal();
            else
            {
                if (BattleOwnsPet || pet.Emotions.SleepLocked || pet.Action == PetAction.Sleeping) portal.BeginClose();
                if (portal.Step(dt)) ClosePortal();
                else UpdatePortalArt();
            }
        }
        else if (portal.CountDown(dt))
            portal.Schedule(TryPortalEvent() ? PetPortal.NextDelay(featureRandom) : PetPortal.RetrySeconds);
        return TickPortalCast(dt);
    }

    private bool TickPortalCast(double dt)
    {
        if (pet.CastRemaining <= 0) return false;
        if (dragging || pressPending || context.Fullscreen || BattleOwnsPet || !pet.Grounded || pet.Emotions.SleepLocked ||
            pet.Action is PetAction.Sleeping or PetAction.Waking or PetAction.Dragging or PetAction.Thrown)
        {
            pet.CastRemaining = 0;
            return false;
        }
        if (pet.Emotions.BlocksAutonomy) return true; // A real reaction runs first; the spell waits.
        pet.Action = PetAction.Sitting;
        pet.VX = pet.SmoothedWalkVelocity = 0;
        pet.CastRemaining = Math.Max(0, pet.CastRemaining - dt);
        if (pet.CastRemaining == 0) ShowPortal(PetPortal.CastLifetime);
        return true;
    }

    private void ShowPortal(double lifetime)
    {
        double catMid = pet.X + settings.Width / 2, gap = settings.Width / 2 + PetPortalArt.ArtWidth / 2 - 12;
        bool Fits(double mid) => mid - PetPortalArt.ArtWidth / 2 >= bounds.Left && mid + PetPortalArt.ArtWidth / 2 <= bounds.Right;
        double mid = catMid + (pet.Facing < 0 ? -gap : gap);
        if (!Fits(mid)) mid = catMid + (pet.Facing < 0 ? gap : -gap);
        mid = Math.Clamp(mid, bounds.Left + PetPortalArt.ArtWidth / 2, Math.Max(bounds.Left + PetPortalArt.ArtWidth / 2, bounds.Right - PetPortalArt.ArtWidth / 2));
        pet.Facing = mid < catMid ? -1 : 1; // The cat looks at it.

        var art = new PetPortalArt();
        var label = new Border
        {
            Background = PetUi.Game.Panel, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(PetUi.SmallRadius), Padding = new Thickness(8, 2, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Opacity = 0,
            Child = new TextBlock { Text = L("Bấm để vào trận", "Click to enter"), Foreground = System.Windows.Media.Brushes.White, FontFamily = PetUi.TextFont, FontSize = PetUi.SmallSize }
        };
        var surface = new Grid { Width = PetPortalArt.ArtWidth, Height = PetPortalArt.ArtHeight, Cursor = Cursors.Hand };
        surface.Children.Add(art);
        surface.Children.Add(label);
        portalArt = art; portalLabel = label;
        portalWindow = CreateOverlayWindow(surface, "Pet portal");
        portalWindow.Width = PetPortalArt.ArtWidth; portalWindow.Height = PetPortalArt.ArtHeight;
        portalWindow.MouseLeftButtonDown += (_, e) => { e.Handled = true; if (EnterPortal() is string reason) ShowBattleNotice(reason); };
        portalWindow.Closed += (_, _) => { portalWindow = null; portalArt = null; portalLabel = null; };
        portalBounds = bounds;
        portal.Open(lifetime);
        UpdatePortalArt();
        portalWindow.Show();
        MoveWindow(portalWindow, mid - PetPortalArt.ArtWidth / 2, bounds.Bottom - PetPortalArt.ArtHeight);
        PlayPetSound("sparkle");
    }

    private void UpdatePortalArt()
    {
        if (portalArt == null || portalLabel == null) return;
        portalArt.Amount = portal.Amount;
        portalArt.Time = portal.Time;
        portalArt.InvalidateVisual();
        portalLabel.Opacity = portal.Phase == PortalPhase.Open ? 1 : Math.Clamp(portal.Amount, 0, 1);
    }

    private void ClosePortal()
    {
        portal.Reset();
        portalWindow?.Close();
    }
}
