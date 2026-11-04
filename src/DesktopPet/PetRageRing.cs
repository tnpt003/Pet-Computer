using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// 2026-11-04 (docs/GOPY-C-2026-11-04.md D3): the rage gauge drawn round the HUD's cat portrait instead of a
// thin bar under the HP. Filling: an orange-red arc going round clockwise. Full: the whole ring glows and
// pulses in gold and red with flickering flames and an anger mark, a "RAGE!" badge under it saying it can be
// clicked. Raging: flames all round, the ring burning down with the time left. Drawing only.
public sealed class RageRing : FrameworkElement
{
    public double Fraction { get; set; }
    public bool Full { get; set; }
    public bool Raging { get; set; }
    public double Time { get; set; }
    public string Label { get; set; } = "RAGE!";

    private static readonly Pen Track = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0x1E, 0x14, 0x10)), 5));
    private static readonly Brush Fill = FrozenBrush(Color.FromRgb(0xF0, 0x6A, 0x3A)), Hot = FrozenBrush(Color.FromRgb(0xFF, 0x3E, 0x2E));
    private static readonly Brush Gold = FrozenBrush(Color.FromRgb(0xFF, 0xD2, 0x3F)), Flame = FrozenBrush(Color.FromRgb(0xFF, 0x8A, 0x2A));
    private static readonly Brush BadgeBack = FrozenBrush(Color.FromRgb(0xD8, 0x3A, 0x2A));
    private static readonly Pen Ink = Frozen(new Pen(FrozenBrush(Color.FromRgb(0x3A, 0x26, 0x1C)), 1.6));
    private static readonly Typeface Face = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);

    private static Brush FrozenBrush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    private static Pen Frozen(Pen p) { p.StartLineCap = p.EndLineCap = PenLineCap.Round; p.Freeze(); return p; }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var c = new Point(w / 2, w / 2);
        double r = w / 2 - 4;
        dc.DrawEllipse(null, Track, c, r, r);
        double k = Math.Clamp(Fraction, 0, 1);
        bool hot = Full || Raging;
        double pulse = hot ? 0.5 + 0.5 * Math.Sin(Time * 8) : 0;
        if (hot)
        {
            // Flames round the ring (more and taller while raging).
            int flames = Raging ? 14 : 10;
            for (int i = 0; i < flames; i++)
            {
                double a = i * 2 * Math.PI / flames + Time * (Raging ? 1.6 : 0.6);
                double tall = (Raging ? 7 : 5) + 3 * Math.Sin(Time * 11 + i * 1.7);
                var foot1 = At(c, r + 1, a - 0.16); var foot2 = At(c, r + 1, a + 0.16); var tip = At(c, r + 2 + tall, a);
                var g = new StreamGeometry();
                using (var s = g.Open()) { s.BeginFigure(foot1, true, true); s.LineTo(tip, true, false); s.LineTo(foot2, true, false); }
                g.Freeze();
                dc.DrawGeometry(i % 2 == 0 ? Flame : Hot, null, g);
            }
            var glow = new Pen(new SolidColorBrush(Color.FromArgb((byte)(120 + 100 * pulse), 0xFF, 0xD2, 0x3F)), 6 + 2 * pulse);
            glow.Freeze();
            dc.DrawEllipse(null, glow, c, r, r);
        }
        if (k > 0)
        {
            var arcPen = new Pen(Raging ? Hot : Full ? Gold : Fill, 5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            arcPen.Freeze();
            if (k >= 0.999) dc.DrawEllipse(null, arcPen, c, r, r);
            else
            {
                double end = k * 2 * Math.PI;
                var arc = new StreamGeometry();
                using (var s = arc.Open())
                {
                    s.BeginFigure(At(c, r, 0), false, false);
                    s.ArcTo(At(c, r, end), new Size(r, r), 0, end > Math.PI, SweepDirection.Clockwise, true, false);
                }
                arc.Freeze();
                dc.DrawGeometry(null, arcPen, arc);
            }
        }
        if (!hot) return;
        // An anger mark at the top right.
        var mark = At(c, r - 2, Math.PI / 4);
        for (int i = 0; i < 4; i++)
        {
            double a = i * Math.PI / 2 + Math.PI / 4;
            var from = new Point(mark.X + Math.Cos(a) * 2, mark.Y + Math.Sin(a) * 2);
            var to = new Point(mark.X + Math.Cos(a) * (6 + pulse), mark.Y + Math.Sin(a) * (6 + pulse));
            var vein = new Pen(Hot, 2.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            vein.Freeze();
            dc.DrawLine(vein, from, to);
        }
        if (!Full || Raging) return;
        // The badge under the portrait: the word and a little pointing hand.
        var text = new FormattedText(Label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, 10, Brushes.White, 1);
        double bw = text.Width + 12, bh = 14, bx = w / 2 - bw / 2, by = w - 9 + pulse;
        dc.DrawRoundedRectangle(BadgeBack, Ink, new Rect(bx, by, bw, bh), 6, 6);
        dc.DrawText(text, new Point(bx + 6, by + (bh - text.Height) / 2));
        var hand = new Point(bx + bw + 3, by + 4 - 2 * pulse);
        dc.DrawEllipse(Gold, Ink, hand, 3.2, 3.2);
        dc.DrawLine(Ink, new Point(hand.X - 2, hand.Y - 2), new Point(hand.X - 6, hand.Y - 6));
    }

    // A point on the circle, angle 0 at the top, clockwise.
    private static Point At(Point c, double r, double angle) => new(c.X + r * Math.Sin(angle), c.Y - r * Math.Cos(angle));
}
