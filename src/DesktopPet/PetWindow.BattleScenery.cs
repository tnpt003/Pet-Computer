using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopPet;

// 1.5 M22: a chapter's own backdrop behind its battles (ChapterSpec.Scene; chapter 2 "dump": heaps of
// folders and loose pages at both ends of the strip, a download sign, a few pages along the floor).
// Its own click-through window just under the battle strip, the same size and place: every click on
// it reaches the windows below, the slimes are drawn over it and the cat stays above both. Drawn once
// (again only when the strip is placed again); hidden with the strip for a fullscreen app. Decoration
// only: the fight never reads it. No backdrop for chapter 1, the Expedition, Infinity, the free battle.
public sealed partial class PetWindow
{
    private Window? battleSceneryWindow;
    private BattleSceneryView? battleScenery;

    // The scene behind this battle's stage, if its chapter has one (null: no backdrop).
    private string? BattleSceneFor(BattleSim b) =>
        b.Stage is { Endless: false } stage && battleExpedition == null ? AdventureOf(BattleTuning)?.SceneOf(stage) : null;

    public bool BattleSceneryShown => battleSceneryWindow?.IsVisible == true;
    public string? BattleSceneryKind => battleScenery?.Scene;

    // Before the strip is shown, so the strip comes above it.
    private void ShowBattleScenery(BattleSim b)
    {
        if (BattleSceneFor(b) is not string scene) return;
        battleScenery = new BattleSceneryView { Scene = scene };
        battleSceneryWindow = CreateOverlayWindow(battleScenery, "Pet battle scenery", clickThrough: true);
        battleSceneryWindow.Show();
    }

    // With the strip (its pixels, under it in the topmost band).
    private void PlaceBattleScenery(IntPtr field, int x, int y, int width, int height)
    {
        if (battleSceneryWindow == null) return;
        var handle = new WindowInteropHelper(battleSceneryWindow).Handle;
        const uint NoActivate = 0x0010;
        SetWindowPos(handle, field, x, y, width, height, NoActivate);
        battleScenery?.InvalidateVisual();
    }

    private void SetBattleSceneryVisible(bool visible)
    {
        if (battleSceneryWindow == null || battleSceneryWindow.IsVisible == visible) return;
        if (visible) battleSceneryWindow.Show(); else battleSceneryWindow.Hide();
    }

    private void CloseBattleScenery()
    {
        battleSceneryWindow?.Close();
        battleSceneryWindow = null; battleScenery = null;
    }
}

// The backdrop itself (PetWindow.BattleScenery.cs): the strip's DIPs, the floor 2 DIPs over its bottom
// as the battle's. A little faded, so the slimes over it stay the clearest thing on the floor.
public sealed class BattleSceneryView : FrameworkElement
{
    public string Scene { get; init; } = ChapterSpec.Dump;
    public const double Fade = 0.85, HeapWidth = 170;

    public BattleSceneryView() { IsHitTestVisible = false; Focusable = false; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, floor = ActualHeight - 2;
        if (w <= 0 || Scene != ChapterSpec.Dump) return;
        dc.PushOpacity(Fade);
        Heap(dc, 0, floor, false);
        Heap(dc, w - HeapWidth, floor, true);
        // The download sign by the left heap.
        dc.DrawLine(SignPost, new Point(HeapWidth + 14, floor), new Point(HeapWidth + 14, floor - 52));
        dc.DrawRoundedRectangle(PetUi.Surface, SignRim, new Rect(HeapWidth - 4, floor - 74, 36, 24), 5, 5);
        dc.DrawGeometry(PetUi.Mint, SignArrowRim, SignArrow(HeapWidth + 14, floor - 71));
        // Loose pages along the floor between the heaps, spaced by a fixed pattern (the same every battle).
        int count = (int)Math.Floor((w - HeapWidth * 2 - 80) / 230);
        for (int i = 0; i < count; i++)
        {
            double x = HeapWidth + 90 + i * 230 + (i * 53 % 70), tilt = (i * 37 % 50) - 25;
            PetScene.LoosePage(dc, x, floor - 12, 12, 15, 70 + tilt);
        }
        dc.Pop();
    }

    private static readonly Pen SignPost = Make(new Pen(PetUi.Hex("#6D4E40"), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
    private static readonly Pen SignRim = PetUi.FrozenPen(PetUi.Ink, 2), SignArrowRim = PetUi.FrozenPen(PetUi.Ink, 1.3);
    private static readonly Brush HeapBack = PetUi.Frozen(Color.FromArgb(0xC0, 0xB9, 0xA6, 0xDC));
    private static Pen Make(Pen pen) { pen.Freeze(); return pen; }

    private static Geometry SignArrow(double cx, double top)
    {
        var g = new StreamGeometry();
        using (var s = g.Open())
        {
            s.BeginFigure(new Point(cx - 3.5, top), true, true);
            foreach (var (dx, dy) in new[] { (3.5, 0.0), (3.5, 9.0), (8.5, 9.0), (0.0, 18.0), (-8.5, 9.0), (-3.5, 9.0) })
                s.LineTo(new Point(cx + dx, top + dy), true, true);
        }
        g.Freeze();
        return g;
    }

    // A heap of folders and pages HeapWidth wide on the floor, its high end at the screen edge.
    private static void Heap(DrawingContext dc, double left, double floor, bool right)
    {
        double Edge(double x, double width) => right ? left + HeapWidth - x - width : left + x;
        var mound = new StreamGeometry();
        using (var s = mound.Open())
        {
            s.BeginFigure(new Point(left, floor), true, true);
            if (right)
            {
                s.QuadraticBezierTo(new Point(left + HeapWidth * 0.55, floor - 30), new Point(left + HeapWidth, floor - 56), true, true);
                s.LineTo(new Point(left + HeapWidth, floor), true, true);
            }
            else
            {
                s.LineTo(new Point(left, floor - 56), true, true);
                s.QuadraticBezierTo(new Point(left + HeapWidth * 0.45, floor - 30), new Point(left + HeapWidth, floor), true, true);
            }
        }
        mound.Freeze();
        dc.DrawGeometry(HeapBack, null, mound);
        PetScene.Folder(dc, Edge(6, 46), floor - 64, 46, 34, right ? 8 : -8);
        PetScene.Folder(dc, Edge(44, 40), floor - 40, 40, 30, right ? -10 : 10);
        PetScene.LoosePage(dc, Edge(92, 18), floor - 30, 18, 24, right ? 14 : -14);
        PetScene.Folder(dc, Edge(2, 36), floor - 28, 36, 26, right ? -4 : 4);
        PetScene.LoosePage(dc, Edge(118, 14), floor - 16, 14, 18, right ? -24 : 24);
    }
}
