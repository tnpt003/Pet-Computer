using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

public sealed partial class PetFarmArt
{
    // The spell that brings a folded land out and puts it away (docs/FARM-GAME.md, Biển nông trại):
    // a ring of runes on the floor, a column of light, and stars. Amount runs 0 to 1 coming out and
    // back again going away, so the land sinks the way it came. Drawn only while it is moving.
    public sealed class LandMagic : FrameworkElement
    {
        private const int RuneCount = 18;
        private static readonly Brush
            Glow = PetUi.Frozen(0x66, 0xC5, 0xAD, 0xE5),
            Column = MakeColumn(),
            StarLilac = PetUi.Lilac;
        private static readonly Pen RingPen = PetUi.FrozenPen(PetUi.Lilac, 2.2), RingGold = PetUi.FrozenPen(PetUi.Game.Gold, 1.2), RunePen = PetUi.FrozenPen(PetUi.Game.Gold, 2), StarInk = PetUi.FrozenPen(PetUi.ArtInk, 0.8);

        private readonly double landWidth;

        public double Amount { get; set; }

        public LandMagic(double artWidth, double landWidth)
        {
            this.landWidth = landWidth;
            Width = artWidth;
            Height = ArtHeight;
            IsHitTestVisible = false;
            Visibility = Visibility.Collapsed;
        }

        private static Brush MakeColumn()
        {
            var brush = new RadialGradientBrush(Color.FromArgb(0x7A, 0xC5, 0xAD, 0xE5), Color.FromArgb(0, 0xC5, 0xAD, 0xE5));
            brush.Freeze();
            return brush;
        }

        // Up from `from` to 1 by `to`, stays, and back down to 0 from `off` to `end`.
        private static double Envelope(double value, double from, double to, double off, double end) =>
            value < to ? Math.Clamp((value - from) / (to - from), 0, 1) : Math.Clamp((end - value) / (end - off), 0, 1);

        private static double Fraction(double value) => value - Math.Floor(value);

        protected override void OnRender(DrawingContext dc)
        {
            if (Amount <= 0 || Amount >= 1) return;
            double mid = Side + landWidth / 2, floor = Floor - 6;
            double ring = Envelope(Amount, 0, 0.14, 0.62, 0.92);
            if (ring > 0) DrawRing(dc, mid, floor, ring);
            DrawStars(dc, mid);
        }

        private void DrawRing(DrawingContext dc, double mid, double floor, double strength)
        {
            double opened = 1 - Math.Pow(1 - Math.Clamp(Amount / 0.3, 0, 1), 3);
            double radiusX = (landWidth / 2 + 16) * (0.45 + 0.55 * opened), radiusY = 13 * (0.45 + 0.55 * opened);
            dc.PushOpacity(strength);
            dc.DrawEllipse(Column, null, new Point(mid, floor - 40), radiusX * 0.95, 52);
            dc.DrawEllipse(Glow, RingPen, new Point(mid, floor), radiusX, radiusY);
            dc.DrawEllipse(null, RingGold, new Point(mid, floor), radiusX * 0.86, radiusY * 0.8);
            for (int i = 0; i < RuneCount; i++)
            {
                double angle = i * Math.Tau / RuneCount + Amount * 1.6;
                double x = mid + Math.Cos(angle) * radiusX * 0.93, y = floor + Math.Sin(angle) * radiusY * 0.9;
                double tall = i % 3 == 0 ? 6 : 3.5;
                dc.DrawLine(RunePen, new Point(x, y - tall / 2), new Point(x, y + tall / 2));
            }
            dc.Pop();
        }

        // Each star has its own short life, the same on every run.
        private void DrawStars(DrawingContext dc, double mid)
        {
            int count = (int)Math.Clamp(landWidth / 14, 8, 18);
            for (int i = 0; i < count; i++)
            {
                double start = 0.3 + 0.32 * Fraction(i * 0.618), life = (Amount - start) / 0.38;
                if (life <= 0 || life >= 1) continue;
                double x = mid + (Fraction(i * 0.37 + 0.11) - 0.5) * (landWidth + 12) + Math.Sin(life * 5 + i) * 3;
                double y = SoilTop + 4 - life * (42 + 52 * Fraction(i * 0.77 + 0.3));
                double size = (0.65 + 0.5 * Fraction(i * 0.53)) * (1 - life * 0.35);
                dc.PushOpacity(Math.Sin(life * Math.PI));
                dc.PushTransform(new MatrixTransform(size, 0, 0, size, x, y));
                dc.DrawGeometry(i % 3 == 0 ? StarLilac : PetUi.Game.Gold, StarInk, Star);
                dc.Pop();
                dc.Pop();
            }
        }
    }
}
