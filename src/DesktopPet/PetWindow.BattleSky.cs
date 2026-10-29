using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// 1.5 M14: the flying slimes over the battle's screen (BattleSim, PetBattle.Fly.cs), docs/APPROVED-EXCEPTIONS.md
// → Battle Mode. Each flyer up in the air is drawn in its own small topmost window like an app icon (only
// its drawn body takes the mouse: a click with Push or the Grabber chosen uses it on the flyer; nothing
// else happens on a click), on the battle's monitor only, placed in that monitor's pixels (DPI). One
// click-through canvas over the battle's work area shows the drops of goo in flight and the aim lines,
// only while there are any. The battle is told how high its screen goes and which battle windows (the
// HUD, the card offer, the bag, the app icons' rows) the flyers keep clear of. A fullscreen app, Pause
// or a menu hides all of it. Drawing and input only: what the flyers do is BattleSim.
public sealed partial class PetWindow
{
    public const double FlyerViewSize = 120, FlyerFoot = 22;
    private sealed class FlyerWindow
    {
        public required Window Window;
        public required FlyerView View;
        public Rect Placed;
        public double PlacedScale;
    }
    private readonly Dictionary<int, FlyerWindow> flyerWindows = new();
    private Window? skyShotWindow;
    private SkyShotView? skyShotView;
    private bool skyShotShown, skyHidden;
    private Rect skyShotPlaced;
    private double skyShotScale;
    public IReadOnlyCollection<int> BattleFlyerWindows => flyerWindows.Keys;
    public bool BattleSkyShotsShown => skyShotShown && skyShotWindow is { IsVisible: true };

    // A spot of the battle (battle x, height over the floor) on the screen (DIPs).
    public Point BattleSkyPoint(double x, double height) => new(x, battleArea.Bottom - 2 - height);

    // Where a flyer's window goes on the screen (DIPs): its body's bottom FlyerFoot over the window's.
    public Rect BattleFlyerSpot(Slime s)
    {
        var foot = BattleSkyPoint(s.X, s.Altitude);
        return new Rect(foot.X - FlyerViewSize / 2, foot.Y - (FlyerViewSize - FlyerFoot), FlyerViewSize, FlyerViewSize);
    }

    // The battle windows the flyers keep clear of, in battle x and heights over the floor.
    private List<(double, double, double, double)> SkyBlockers(BattleSim b)
    {
        double floor = battleArea.Bottom - 2;
        var list = new List<(double, double, double, double)>();
        foreach (var r in AppBlockers()) list.Add((r.Left, floor - r.Bottom, r.Right, floor - r.Top));
        if (b.AppRules != null && b.Desktop.Count > 0)
            list.Add((battleArea.Left, BattleFieldView.FieldHeight, battleArea.Right, BattleFieldView.FieldHeight + b.GridRows * AppCellHeight));
        return list;
    }

    private bool skySeen;

    // Every frame of the battle windows (after the apps). draw: a frame is due (or something changed).
    private void UpdateBattleSky(bool draw)
    {
        if (battle is not BattleSim b) return;
        bool any = false;
        foreach (var s in b.Slimes) if (s.Type.Fly != null) { any = true; break; }
        // The sky's size goes to the battle from the start (a flyer picks its first height from it).
        if (any || !skySeen) { b.SetSky(battleArea.Bottom - 2 - battleArea.Top, any ? SkyBlockers(b) : null); skySeen = true; }
        bool hide = b.Paused;
        foreach (var id in flyerWindows.Keys.Where(id => b.Find(id) is not Slime s || !BattleSim.InSky(s)).ToList())
        {
            flyerWindows[id].Window.Close();
            flyerWindows.Remove(id);
        }
        foreach (var s in b.Slimes)
        {
            if (!BattleSim.InSky(s)) continue;
            if (!flyerWindows.TryGetValue(s.Id, out var flyer)) flyer = CreateFlyerWindow(b, s.Id);
            if (hide) { if (flyer.Window.IsVisible) flyer.Window.Hide(); continue; }
            if (!flyer.Window.IsVisible) { flyer.Window.Show(); flyer.Placed = Rect.Empty; }
            var spot = BattleFlyerSpot(s);
            if (spot != flyer.Placed || battleScale != flyer.PlacedScale) { flyer.Placed = spot; flyer.PlacedScale = battleScale; PlaceAppWindow(flyer.Window, spot); }
            if (draw) flyer.View.InvalidateVisual();
        }
        UpdateSkyShots(b, draw, hide);
    }

    private FlyerWindow CreateFlyerWindow(BattleSim b, int id)
    {
        var view = new FlyerView(b, id, () => battleView);
        view.Clicked += offset => BattleClickFlyer(id, offset);
        var window = CreateOverlayWindow(view, "Pet flying slime");
        window.Width = window.Height = FlyerViewSize;
        var flyer = new FlyerWindow { Window = window, View = view, Placed = Rect.Empty };
        window.DpiChanged += (_, _) => flyer.PlacedScale = 0; // Placed again in pixels next frame.
        flyerWindows[id] = flyer;
        window.Show();
        return flyer;
    }

    // A click on a flyer's window: Push knocks it down, the Grabber yanks it down; nothing else.
    public bool BattleClickFlyer(int id, double offset = 0)
    {
        if (battle == null || battle.Paused || battle.Find(id) is not Slime s || !BattleSim.InSky(s)) return false;
        return battle.Armed switch
        {
            BattleSkill.Grabber => BattleGrab(id, s.X, s.Altitude),
            BattleSkill.Push => BattleClickSlime(id, offset),
            _ => false
        };
    }

    // The canvas for drops and aims: over the battle's work area, shown while there is any.
    private void UpdateSkyShots(BattleSim b, bool draw, bool hide)
    {
        bool busy = b.Shots.Count > 0;
        if (!busy) foreach (var s in b.Slimes) if (s.Aiming && BattleSim.InSky(s)) { busy = true; break; }
        if (hide) busy = false;
        if (!busy && !skyShotShown) return;
        if (skyShotWindow == null)
        {
            skyShotView = new SkyShotView(b) { ToScreen = (x, h) => BattleSkyPoint(x, h) - (Vector)skyShotPlaced.TopLeft };
            skyShotWindow = CreateOverlayWindow(skyShotView, "Pet flying slime shots", clickThrough: true);
            skyShotWindow.DpiChanged += (_, _) => skyShotScale = 0;
            skyShotWindow.Show();
        }
        var area = battleArea;
        var spot = new Rect(area.Left, area.Top, area.Right - area.Left, area.Bottom - area.Top);
        if (busy && !skyShotWindow.IsVisible) { skyShotWindow.Show(); skyShotScale = 0; }
        if (spot != skyShotPlaced || skyShotScale != battleScale) { skyShotPlaced = spot; skyShotScale = battleScale; PlaceAppWindow(skyShotWindow, spot); }
        // The cat's centre: where the window really has it (held anywhere on the screen, or standing).
        skyShotView!.CatAt = b.CatCentre();
        skyShotShown = busy;
        if (draw || !busy) skyShotView.InvalidateVisual(); // One more frame once empty, to clear it.
    }

    private void HideBattleSky()
    {
        foreach (var f in flyerWindows.Values) if (f.Window.IsVisible) f.Window.Hide();
        skyShotWindow?.Hide();
        skyShotShown = false;
        skyHidden = true;
    }

    private void ShowBattleSky()
    {
        if (!skyHidden) return;
        skyHidden = false;
        foreach (var f in flyerWindows.Values) f.Placed = Rect.Empty; // Shown and placed again next frame.
    }

    private void CloseBattleSky()
    {
        foreach (var f in flyerWindows.Values) f.Window.Close();
        flyerWindows.Clear();
        skyShotWindow?.Close();
        skyShotWindow = null; skyShotView = null; skyShotShown = false; skyShotScale = 0; skyShotPlaced = Rect.Empty;
        skySeen = false; skyHidden = false;
    }
}

// One flyer up in the air, in its own window (PetWindow.BattleSky.cs): drawn by the battle's field view
// (the same slime look), its body (and a small halo round it) clickable.
public sealed class FlyerView : FrameworkElement
{
    private readonly BattleSim sim;
    private readonly int id;
    private readonly Func<BattleFieldView?> field;
    // A click on it and where (DIPs from its centre: negative = its left half).
    public event Action<double>? Clicked;

    public FlyerView(BattleSim sim, int id, Func<BattleFieldView?> field)
    {
        this.sim = sim; this.id = id; this.field = field;
        Cursor = Cursors.Hand;
        MouseLeftButtonDown += (_, e) => { Clicked?.Invoke(e.GetPosition(this).X - ActualWidth / 2); e.Handled = true; };
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (sim.Find(id) is not Slime slime || field() is not BattleFieldView view) return;
        view.DrawFlyer(dc, slime, ActualWidth / 2, ActualHeight - PetWindow.FlyerFoot);
    }
}

// The drops of goo in flight and the flyers' aims, over the battle's work area (click-through).
public sealed class SkyShotView : FrameworkElement
{
    private readonly BattleSim sim;
    // A spot of the battle (battle x, height over the floor) in this view.
    public Func<double, double, Point> ToScreen { get; set; } = (x, h) => new Point(x, -h);
    public (double X, double Height) CatAt { get; set; }

    public SkyShotView(BattleSim sim) { this.sim = sim; IsHitTestVisible = false; }

    protected override void OnRender(DrawingContext dc)
    {
        var cat = ToScreen(CatAt.X, CatAt.Height);
        foreach (var s in sim.Slimes)
        {
            if (!s.Aiming || !BattleSim.InSky(s) || s.Type.Fly is not FlySpec fly) continue;
            var (mx, mh) = BattleSim.Muzzle(s);
            BattleFieldView.DrawAim(dc, ToScreen(mx, mh), cat, fly.Radius, Math.Clamp(1 - s.AimLeft / fly.Aim, 0, 1), sim.Time);
        }
        foreach (var shot in sim.Shots) BattleFieldView.DrawShot(dc, shot, ToScreen(shot.X, shot.Height));
    }
}
