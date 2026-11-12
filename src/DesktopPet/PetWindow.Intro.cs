using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopPet;

// The startup intro (M2a, docs/APPROVED-EXCEPTIONS.md → Cổng phép): only the app starts it, right
// before the cat window is first shown, so a second launch (which exits) and the UI checks never see
// it. A decorative portal (not the one into Battle Mode: no click into the hub, no effect on its
// 20–40 minute timer) opens where the cat was saved, the cat is tossed out with an ordinary throw,
// the farm sign rises out of the ground and the keepsake, basket and toys pop in. No emotion, no
// interaction count, nothing sent to Python. A click on the cat or the portal, a fullscreen app or a
// battle shows everything at once.
public sealed partial class PetWindow
{
    private readonly PetIntro intro = new();
    private readonly PetPortal introPortal = new();
    private bool introPending;
    private Window? introPortalWindow;
    private PetPortalArt? introPortalArt;
    private Area introBounds;
    private readonly List<(Window Window, int Index, bool Revealed)> introItems = new();
    private readonly List<(Window Window, PetIntroArt Art, double Start)> introEffects = new();
    private bool introSignDust;
    private Action? afterIntro;

    // True from BeginIntro until everything is shown.
    public bool IntroRunning => introPending || intro.Active;

    // While it runs nothing automatic moves the cat (FeatureOwnsMovement).
    private bool IntroOwnsMovement => IntroRunning;

    // Called by the app before the first Show: the cat starts hidden in the portal.
    public void BeginIntro()
    {
        introPending = true;
        renderer.Opacity = 0;
    }

    // Runs once the intro is over (at once when there is none): the first-run welcome waits for it.
    public void WhenIntroDone(Action action)
    {
        if (!IntroRunning) { Dispatcher.BeginInvoke(action); return; }
        afterIntro += action;
    }

    // Each frame, also in Pause (it is only three seconds; the cat's own physics still holds in Pause).
    private void TickIntro(double dt)
    {
        if (introPending)
        {
            introPending = false;
            if (contextProbe().Fullscreen || context.Fullscreen || BattleActive) { FinishIntro(); return; }
            StartIntro();
            return;
        }
        if (!intro.Active) return;
        if (context.Fullscreen || BattleActive || bounds != introBounds) { FinishIntro(); return; }
        if (intro.Step(dt)) TossCatOut();
        if (introPortalWindow != null && introPortalArt != null)
        {
            if (introPortal.Step(dt)) CloseIntroPortal();
            else { introPortalArt.Amount = introPortal.Amount; introPortalArt.Time = introPortal.Time; introPortalArt.InvalidateVisual(); }
        }
        StepIntroItems();
        StepIntroSign();
        StepIntroEffects();
        if (!intro.Active) FinishIntro();
    }

    private void StartIntro()
    {
        intro.Start();
        introBounds = bounds;
        // The saved column, on the floor (a cat saved on a window's edge comes out below it).
        double half = PetPortalArt.ArtWidth / 2;
        double mid = Math.Clamp(pet.X + settings.Width / 2, bounds.Left + half, Math.Max(bounds.Left + half, bounds.Right - half));
        pet.X = mid - settings.Width / 2;
        pet.Y = bounds.Bottom - settings.Height;
        pet.VX = pet.VY = pet.SmoothedWalkVelocity = 0;
        pet.Grounded = true;
        pet.LedgeId = 0;
        pet.Action = PetAction.Idle;
        SnapPresentation();

        introPortalArt = new PetPortalArt { Cursor = System.Windows.Input.Cursors.Hand };
        introPortalWindow = CreateOverlayWindow(introPortalArt, "Pet intro portal");
        introPortalWindow.Width = PetPortalArt.ArtWidth; introPortalWindow.Height = PetPortalArt.ArtHeight;
        introPortalWindow.MouseLeftButtonDown += (_, e) => { e.Handled = true; FinishIntro(); };
        introPortalWindow.Closed += (_, _) => { introPortalWindow = null; introPortalArt = null; };
        introPortal.Open(PetIntro.PortalLifetime);
        introPortalArt.Amount = introPortal.Amount;
        introPortalWindow.Show();
        MoveWindow(introPortalWindow, mid - half, bounds.Bottom - PetPortalArt.ArtHeight);
        PlayPetSound("sparkle");

        // Keepsake, basket, toys: hidden now, each shown in turn.
        introItems.Clear();
        var windows = new List<Window?> { keepsakeWindow, toyBasket?.Window };
        windows.AddRange(desktopToys.Select(thing => thing.Window));
        int index = 0;
        foreach (var window in windows)
        {
            if (window is not { IsVisible: true }) continue;
            window.Opacity = 0;
            introItems.Add((window, index++, false));
        }
        introSignDust = false;
        StepIntroSign();
    }

    // Out of the portal's middle, toward the side with more room: an ordinary throw (no anger, no
    // Dizzy, no interaction). It lands and bounces as any throw does.
    private void TossCatOut()
    {
        double mid = pet.X + settings.Width / 2;
        var (vx, vy) = PetIntro.ThrowVelocity(mid - bounds.Left, bounds.Right - mid);
        pet.Y = bounds.Bottom - settings.Height - 30;
        pet.VX = vx; pet.VY = vy;
        pet.Facing = vx < 0 ? -1 : 1;
        pet.Grounded = false;
        pet.Action = PetAction.Thrown;
        pet.Pickup.Reset();
        behavior.Reset();
        SnapPresentation();
        renderer.Opacity = 1;
    }

    private void StepIntroItems()
    {
        for (int i = 0; i < introItems.Count; i++)
        {
            var (window, index, revealed) = introItems[i];
            double shown = intro.ItemShown(index);
            window.Opacity = shown;
            if (revealed || intro.Time < PetIntro.ItemAt(index) || !window.IsVisible) continue;
            introItems[i] = (window, index, true);
            AddIntroEffect(window, dirt: false);
            if (index == 0 || index == introItems.Count - 1) PlayPetSound("sparkle"); // The cue keeps its own no-overlap rule.
        }
    }

    // The sign comes up out of the ground: pushed down inside its own window (which hides what is
    // below the floor), with a small shake and dirt flying up at its foot.
    private void StepIntroSign()
    {
        if (FarmDockPatch.Surface is not { } surface) return;
        double rise = intro.SignRise, drop = (1 - rise) * PetFarmArt.Floor;
        if (!intro.Active || rise >= 1 || !FarmVisible || FarmDockPatch.Window is not { IsVisible: true } window)
        {
            if (surface.Clip != null) { surface.RenderTransform = Transform.Identity; surface.Clip = null; }
            return;
        }
        surface.RenderTransform = new TranslateTransform(intro.SignOffset, drop);
        // The clip moves with the transform: keep what is above the floor line of the window.
        surface.Clip = new RectangleGeometry(new Rect(-40, -drop - 400, window.Width + 80, PetFarmArt.Floor + 400));
        if (!introSignDust && intro.Time >= PetIntro.SignAt)
        {
            introSignDust = true;
            AddIntroEffect(window, dirt: true);
        }
    }

    private void AddIntroEffect(Window item, bool dirt)
    {
        var handle = new WindowInteropHelper(item).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect)) return;
        double itemScale = item.ActualWidth > 0 ? (rect.Right - rect.Left) / item.ActualWidth : scale;
        var art = new PetIntroArt(dirt);
        var window = CreateOverlayWindow(art, "Pet intro magic", clickThrough: true);
        window.Width = art.Width; window.Height = art.Height;
        window.Show();
        double width = art.Width * itemScale, height = art.Height * itemScale, middle = (rect.Left + rect.Right) / 2.0;
        // Dirt sits on the sign's floor line; a puff is centred on the thing.
        double top = dirt ? rect.Top + PetFarmArt.Floor * itemScale - height + 8 * itemScale : (rect.Top + rect.Bottom) / 2.0 - height / 2;
        MoveWindow(window, middle - width / 2, top, atScale: 1); // Pixels: the item may be on another monitor.
        introEffects.Add((window, art, intro.Time));
    }

    private void StepIntroEffects()
    {
        for (int i = introEffects.Count - 1; i >= 0; i--)
        {
            var (window, art, start) = introEffects[i];
            double t = (intro.Time - start) / (art.Dirt ? PetIntro.SignSeconds : PetIntro.FxSeconds);
            if (!intro.Active || t >= 1) { window.Close(); introEffects.RemoveAt(i); continue; }
            art.T = t;
            art.InvalidateVisual();
        }
    }

    private void CloseIntroPortal()
    {
        introPortal.Reset();
        introPortalWindow?.Close();
    }

    // Everything at once: the portal and the effects go, every thing is fully shown, the sign stands.
    // A cat still inside the portal simply appears where it stands.
    private void FinishIntro()
    {
        bool wasRunning = IntroRunning || afterIntro != null;
        introPending = false;
        intro.Finish();
        CloseIntroPortal();
        foreach (var (window, _, _) in introItems) window.Opacity = 1;
        introItems.Clear();
        StepIntroEffects();
        StepIntroSign();
        renderer.Opacity = 1;
        if (!wasRunning) return;
        var then = afterIntro;
        afterIntro = null;
        if (then != null) Dispatcher.BeginInvoke(then);
    }

    // A click on the cat (left or right) or opening the battle hub ends the show.
    private void SkipIntro()
    {
        if (IntroRunning) FinishIntro();
    }
}
