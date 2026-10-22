using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The toy ball, drawn as a ball of pink yarn in the cat's vector style (the same yarn as Battle
// Mode's yarn bomb, the shared dark outline): wound strands, a shine, a loose thread end. Static
// art; the window rotates it as it rolls.
public sealed class YarnBallArt : FrameworkElement
{
    private static Brush B(byte r, byte g, byte b) => PetUi.Frozen(Color.FromRgb(r, g, b));
    private static Pen P(Brush brush, double width) { var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; pen.Freeze(); return pen; }
    private static readonly Brush Yarn = B(0xE8, 0x7A, 0xB8), Shine = PetUi.Frozen(Color.FromArgb(0xB0, 255, 255, 255));
    private static readonly Pen Outline = P(B(0x3A, 0x2E, 0x4A), 1.4), Strand = P(B(0xB0, 0x4A, 0x88), 1.3);
    // The hover/hit ring around it (cream, like the game panels' highlight).
    public static readonly Brush Halo = B(0xFF, 0xF4, 0xE0);

    protected override void OnRender(DrawingContext dc)
    {
        // Leaves room for the loose end (it reaches 1.12 radii out).
        double r = Math.Min(ActualWidth, ActualHeight) * 0.42, cx = ActualWidth / 2, cy = ActualHeight / 2;
        if (r <= 2) return;
        var c = new Point(cx, cy);
        // The loose end first, so the ball covers where it leaves.
        var tail = new StreamGeometry();
        using (var g = tail.Open())
        {
            g.BeginFigure(new Point(cx + r * 0.55, cy + r * 0.7), false, false);
            g.BezierTo(new Point(cx + r * 1.0, cy + r * 1.05), new Point(cx + r * 1.15, cy + r * 0.6), new Point(cx + r * 1.12, cy + r * 1.05), true, true);
        }
        tail.Freeze();
        dc.DrawGeometry(null, Strand, tail);
        dc.DrawEllipse(Yarn, Outline, c, r, r);
        // Wound strands, clipped to the ball.
        dc.PushClip(new EllipseGeometry(c, r - 0.6, r - 0.6));
        for (int i = -1; i <= 1; i++)
            dc.DrawEllipse(null, Strand, new Point(cx + i * r * 0.5, cy), r * 0.42, r * 1.05);
        dc.DrawLine(Strand, new Point(cx - r, cy - r * 0.35), new Point(cx + r, cy + r * 0.15));
        dc.DrawLine(Strand, new Point(cx - r, cy + r * 0.25), new Point(cx + r, cy + r * 0.75));
        dc.Pop();
        dc.DrawEllipse(Shine, null, new Point(cx - r * 0.4, cy - r * 0.45), r * 0.22, r * 0.14);
    }
}
