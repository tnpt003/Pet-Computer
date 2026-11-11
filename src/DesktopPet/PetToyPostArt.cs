using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The scratching post (docs/LIVING-CORNER.md → M1): a wooden base, a column wound with rope, a small
// disc on top with a pompom hanging from it, in the cat's vector style. Swing makes the pompom sway
// (a click); Scratching shakes the rope a little and lets a few fibres drift down by the cat's paws.
// Only what is drawn takes clicks.
public sealed class ToyPostArt : FrameworkElement
{
    public const double ArtWidth = 84, ArtHeight = 120;
    private const double Cx = ArtWidth / 2, Bottom = ArtHeight;
    private const double BaseHeight = 10, ColumnHalf = 9, DiscHalf = 22, DiscHeight = 7;

    private static Brush B(byte r, byte g, byte b) => PetUi.Frozen(Color.FromRgb(r, g, b));
    private static Pen P(Brush brush, double width) { var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
    private static readonly Brush Wood = B(0xB9, 0x84, 0x52), WoodTop = B(0xD2, 0xA0, 0x6A), Rope = B(0xE6, 0xCF, 0xA0),
        Pompom = B(0xE8, 0x83, 0x4F), Shade = PetUi.Frozen(Color.FromArgb(0x30, 0x5A, 0x34, 0x14));
    private static readonly Pen Outline = P(PetUi.ArtInk, 1.4), Twist = P(B(0xBF, 0x9E, 0x6A), 1.2), Thread = P(PetUi.ArtInk, 1), Fibre = P(B(0xC9, 0xAE, 0x7C), 1.1);

    // The pompom's sway in degrees, and the scratch under way (seconds into it; negative: none).
    public double Swing;
    public double ScratchTime = -1;
    // The side the cat scratches from (-1 left, 1 right): where the fibres fall.
    public int ScratchSide = -1;

    public ToyPostArt() { Width = ArtWidth; Height = ArtHeight; }

    protected override void OnRender(DrawingContext dc)
    {
        double top = Bottom - DesktopToyRules.PostHeight;
        bool scratching = ScratchTime >= 0;
        // The rope shivers while the cat rakes it.
        double shake = scratching ? Math.Sin(ScratchTime * 38) * 0.9 : 0;
        // Base: a low wooden plank with its lit top edge.
        var plank = new Rect(Cx - DesktopToyRules.PostHalfWidth, Bottom - BaseHeight, 2 * DesktopToyRules.PostHalfWidth, BaseHeight - 0.7);
        dc.DrawRoundedRectangle(Wood, null, plank, 3, 3);
        dc.DrawRectangle(WoodTop, null, new Rect(plank.Left + 2, plank.Top + 1, plank.Width - 4, 2.5));
        dc.DrawRoundedRectangle(null, Outline, plank, 3, 3);
        // Column, wound with rope: diagonal twists down its length.
        double columnTop = top + DiscHeight, columnBottom = plank.Top;
        var column = new Rect(Cx - ColumnHalf + shake, columnTop, 2 * ColumnHalf, columnBottom - columnTop);
        dc.DrawRectangle(Rope, null, column);
        dc.DrawRectangle(Shade, null, new Rect(column.Right - 5, column.Top, 5, column.Height));
        dc.PushClip(new RectangleGeometry(column));
        for (double y = columnTop + 3; y < columnBottom + 6; y += 5)
            dc.DrawLine(Twist, new Point(column.Left, y), new Point(column.Right, y - 4));
        dc.Pop();
        dc.DrawLine(Outline, column.TopLeft, column.BottomLeft);
        dc.DrawLine(Outline, column.TopRight, column.BottomRight);
        // The disc on top, and the pompom on its thread from the right-hand edge.
        var disc = new Rect(Cx - DiscHalf, top, 2 * DiscHalf, DiscHeight);
        dc.DrawRoundedRectangle(WoodTop, Outline, disc, 3, 3);
        var hook = new Point(Cx + DiscHalf - 5, disc.Bottom);
        double angle = Swing * Math.PI / 180;
        var ball = new Point(hook.X + Math.Sin(angle) * 20, hook.Y + Math.Cos(angle) * 20);
        dc.DrawLine(Thread, hook, ball);
        dc.DrawEllipse(Pompom, Outline, ball, 5.5, 5.5);
        if (!scratching) return;
        // A few fibres drifting down from where the paws rake, on the cat's side.
        double edge = Cx + ScratchSide * (ColumnHalf + 1);
        for (int i = 0; i < 3; i++)
        {
            double life = (ScratchTime * 1.4 + i / 3.0) % 1;
            double x = edge + ScratchSide * (2 + 9 * life + 3 * i), y = Bottom - 34 + 26 * life + 4 * Math.Sin(life * 9 + i);
            dc.PushOpacity(1 - life);
            dc.DrawLine(Fibre, new Point(x, y), new Point(x + 2.5 * ScratchSide, y + 1.5));
            dc.Pop();
        }
    }
}
