using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopPet;

// How things come and go (feedback 13/11, docs/TOY-PLAY.md → Hiện ra và biến mất): taken out, a toy pops in
// a little too big and settles, as the keepsake does, in a puff of smoke with sparkles; put away, it swells
// a moment and shrinks to nothing in a puff. Only the look: no physics, never the cat. Pause holds both.
public sealed partial class PetWindow
{
    public const double ToyPopSeconds = 0.55, ToyPopGrowSeconds = 0.32, ToyVanishSeconds = 0.3;

    // Things already gone from the desktop's list, shrinking away in their own windows.
    private readonly List<(Window Window, FrameworkElement Holder, double Age)> toyVanishing = new();
    private readonly List<(Window Window, PetIntroArt Art, double Age)> toyPuffs = new();

    public int ToyVanishingCount => toyVanishing.Count;
    public int ToyPuffCount => toyPuffs.Count;

    // The holder around a thing's own surface: scaling it leaves the thing's own turns and shakes alone.
    private static Border WrapToySurface(Window window, FrameworkElement surface, Point origin)
    {
        window.Content = null;
        var holder = new Border { Child = surface, RenderTransformOrigin = origin };
        window.Content = holder;
        return holder;
    }

    // Ease out with an overshoot (the keepsake's appear): 0, then a bit over 1, then 1.
    public static double ToyPopScale(double age)
    {
        double u = Math.Clamp(age / ToyPopGrowSeconds, 0, 1);
        return 1 + 2.7 * Math.Pow(u - 1, 3) + 1.7 * Math.Pow(u - 1, 2);
    }

    // Swells a little, then shrinks to nothing.
    public static double ToyVanishScale(double age)
    {
        double v = Math.Clamp(age / ToyVanishSeconds, 0, 1);
        return v < 0.25 ? 1 + v / 0.25 * 0.18 : 1.18 * (1 - (v - 0.25) / 0.75);
    }

    private void StartToyPop(DesktopThing thing)
    {
        thing.PopAge = 0;
        if (thing.Holder != null) thing.Holder.RenderTransform = new ScaleTransform(0, 0);
    }

    // Its window, once shown, gets the puff where it stands.
    private void StepToyPop(DesktopThing thing, double dt)
    {
        if (thing.PopAge < 0 || thing.Window is not { IsVisible: true } window || thing.Holder == null) return;
        if (!thing.PopPuffed) { thing.PopPuffed = true; AddToyPuff(window); }
        thing.PopAge += dt;
        double scale = thing.PopAge >= ToyPopSeconds ? 1 : ToyPopScale(thing.PopAge);
        thing.Holder.RenderTransform = scale == 1 ? Transform.Identity : new ScaleTransform(scale, scale);
        if (thing.PopAge >= ToyPopSeconds) thing.PopAge = -1;
    }

    // Instead of closing at once: the window stays for the shrink, then closes.
    private void VanishDesktopThing(DesktopThing thing)
    {
        var window = thing.Window;
        var holder = thing.Holder;
        thing.Window = null; // Taken from the thing first, so CloseDesktopThing leaves it open.
        CloseDesktopThing(thing);
        if (window == null) return;
        if (holder == null || !window.IsVisible) { window.Close(); return; }
        holder.IsHitTestVisible = false;
        AddToyPuff(window);
        toyVanishing.Add((window, holder, 0));
    }

    private void StepToyMagic(double dt)
    {
        for (int i = toyVanishing.Count - 1; i >= 0; i--)
        {
            var (window, holder, age) = toyVanishing[i];
            age += dt;
            if (age >= ToyVanishSeconds || !window.IsLoaded) { window.Close(); toyVanishing.RemoveAt(i); continue; }
            double scale = Math.Max(0, ToyVanishScale(age));
            holder.RenderTransform = new ScaleTransform(scale, scale);
            toyVanishing[i] = (window, holder, age);
        }
        for (int i = toyPuffs.Count - 1; i >= 0; i--)
        {
            var (window, art, age) = toyPuffs[i];
            age += dt;
            if (age >= PetIntro.FxSeconds || !window.IsLoaded) { window.Close(); toyPuffs.RemoveAt(i); continue; }
            art.T = age / PetIntro.FxSeconds;
            art.InvalidateVisual();
            toyPuffs[i] = (window, art, age);
        }
    }

    // The intro's puff of smoke with sparkles, centred on the thing, in pixels (it may be on another monitor).
    private void AddToyPuff(Window item)
    {
        var handle = new WindowInteropHelper(item).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect)) return;
        double itemScale = item.ActualWidth > 0 ? (rect.Right - rect.Left) / item.ActualWidth : scale;
        var art = new PetIntroArt(false);
        var window = CreateOverlayWindow(art, "Pet toy magic", clickThrough: true);
        window.Width = art.Width; window.Height = art.Height;
        window.Show();
        double width = art.Width * itemScale, height = art.Height * itemScale;
        double middle = (rect.Left + rect.Right) / 2.0, bottom = rect.Bottom;
        // Low on the thing: most stand on the floor at the bottom of their window.
        double centre = Math.Max(rect.Top + height / 2, bottom - Math.Min(rect.Bottom - rect.Top, 70 * itemScale) / 2 - 10 * itemScale);
        MoveWindow(window, middle - width / 2, centre - height / 2, atScale: 1);
        toyPuffs.Add((window, art, 0));
    }

    // App closing or the toys torn down: no half-finished magic is left behind.
    private void CloseToyMagic()
    {
        foreach (var (window, _, _) in toyVanishing) window.Close();
        foreach (var (window, _, _) in toyPuffs) window.Close();
        toyVanishing.Clear(); toyPuffs.Clear();
    }
}
