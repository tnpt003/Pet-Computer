using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// 2026-11-04 (docs/GOPY-C-2026-11-04.md D4, docs/APPROVED-EXCEPTIONS.md → Battle Mode D1e): the Scissors
// and the Fan are aimed anywhere on the battle's screen. While one of them is chosen, a window over the
// battle's work area takes the click (a nearly clear fill; the HUD, the cat and the battle strip are left
// out as holes so they keep working as before: the strip takes its own click). It shows where the cut or
// the column would go under the cursor. A right click puts the skill away. Pause, a menu, a fullscreen
// app or the cat in the user's hands hide it. Input and drawing only: the cut and the wind are BattleSim.
public sealed partial class PetWindow
{
    private Window? aimWindow;
    private AimOverlay? aimView;
    private Rect aimPlaced;
    private double aimScale;
    public bool BattleAimShown => aimWindow is { IsVisible: true };

    // A click on the battle's screen (battle x, height over the floor) with the Scissors or the Fan chosen.
    public bool BattleUseOnScreen(double x, double height)
    {
        if (battle == null || battle.Paused || battle.Armed is not BattleSkill skill) return false;
        SyncBattleCatState();
        bool done = battle.UseOnScreen(skill, x, height);
        RequestBattleRedraw();
        UpdateBattleHud();
        return done;
    }

    // Every frame of the battle windows.
    private void UpdateBattleAim(bool hide)
    {
        bool want = !hide && battle is BattleSim b && !b.Paused && !b.Over && b.Armed is BattleSkill armed && BattleSkills.Get(armed).Aim == SkillAim.Screen && !BattleHoldsOff;
        if (!want)
        {
            if (aimWindow is { IsVisible: true }) aimWindow.Hide();
            return;
        }
        if (aimWindow == null)
        {
            aimView = new AimOverlay();
            aimView.Clicked += p => BattleUseOnScreen(p.X + aimPlaced.Left, battleArea.Bottom - 2 - (p.Y + aimPlaced.Top));
            aimView.Cancelled += () => { if (battle != null) { battle.Arm(null); RequestBattleRedraw(); UpdateBattleHud(); } };
            aimWindow = CreateOverlayWindow(aimView, "Pet battle aim");
            aimWindow.DpiChanged += (_, _) => aimScale = 0;
        }
        var area = battleArea;
        var spot = new Rect(area.Left, area.Top, area.Right - area.Left, area.Bottom - area.Top);
        if (!aimWindow.IsVisible) { aimWindow.Show(); aimScale = 0; }
        if (spot != aimPlaced || aimScale != battleScale) { aimPlaced = spot; aimScale = battleScale; PlaceAppWindow(aimWindow, spot); }
        // The holes: the HUD, the cat's window and the strip (in this window's DIPs).
        var holes = new List<Rect>();
        if (battleHudWindow is { IsVisible: true } hud) holes.Add(new Rect(hud.Left - spot.Left, hud.Top - spot.Top, hud.ActualWidth, hud.ActualHeight));
        holes.Add(new Rect(Left - spot.Left, Top - spot.Top, ActualWidth, ActualHeight));
        holes.Add(new Rect(0, spot.Height - BattleFieldView.FieldHeight, spot.Width, BattleFieldView.FieldHeight));
        aimView!.Holes = holes;
        aimView.Skill = battle!.Armed;
        aimView.CutLength = battle.Settings.ScissorsLength;
        aimView.ColumnWidth = battle.ColumnWidth;
        aimView.InvalidateVisual();
    }

    private void CloseBattleAim()
    {
        aimWindow?.Close();
        aimWindow = null; aimView = null; aimPlaced = Rect.Empty; aimScale = 0;
    }
}

// The aiming layer: clear except a faint fill that takes the mouse, holes where other windows work, and
// a guide under the cursor (a dashed line for the Scissors, a dashed column for the Fan).
public sealed class AimOverlay : FrameworkElement
{
    public event Action<Point>? Clicked;
    public event Action? Cancelled;
    public IReadOnlyList<Rect> Holes { get; set; } = Array.Empty<Rect>();
    public BattleSkill? Skill { get; set; }
    public double CutLength { get; set; } = 360;
    public double ColumnWidth { get; set; } = 150;
    private Point? cursor;

    private static readonly Brush Catch = PetUi.Frozen(Color.FromArgb(2, 0, 0, 0));
    private static readonly Pen Guide = Frozen(new Pen(PetUi.Frozen(Color.FromArgb(220, 0xFF, 0xD2, 0x3F)), 3) { DashStyle = DashStyles.Dash });
    private static readonly Pen GuideHalo = Frozen(new Pen(PetUi.Frozen(Color.FromArgb(150, 0x2A, 0x1F, 0x16)), 6));
    private static Pen Frozen(Pen p) { p.Freeze(); return p; }

    public AimOverlay()
    {
        Cursor = Cursors.Cross;
        MouseMove += (_, e) => { cursor = e.GetPosition(this); InvalidateVisual(); };
        MouseLeave += (_, _) => { cursor = null; InvalidateVisual(); };
        MouseLeftButtonDown += (_, e) => { Clicked?.Invoke(e.GetPosition(this)); e.Handled = true; };
        MouseRightButtonDown += (_, e) => { Cancelled?.Invoke(); e.Handled = true; };
    }

    protected override void OnRender(DrawingContext dc)
    {
        Geometry fill = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        foreach (var hole in Holes) if (!hole.IsEmpty) fill = Geometry.Combine(fill, new RectangleGeometry(hole), GeometryCombineMode.Exclude, null);
        dc.DrawGeometry(Catch, null, fill);
        if (cursor is not Point at) return;
        if (Skill == BattleSkill.Scissors)
        {
            var a = new Point(at.X - CutLength / 2, at.Y); var b = new Point(at.X + CutLength / 2, at.Y);
            dc.DrawLine(GuideHalo, a, b);
            dc.DrawLine(Guide, a, b);
        }
        else if (Skill == BattleSkill.Fan)
        {
            var r = new Rect(at.X - ColumnWidth / 2, 0, ColumnWidth, ActualHeight);
            dc.DrawRectangle(null, GuideHalo, r);
            dc.DrawRectangle(null, Guide, r);
        }
    }
}
