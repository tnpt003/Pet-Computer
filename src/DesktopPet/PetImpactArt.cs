using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// The landing's dust (Landing, PetImpact.cs), drawn the same for the cat and the keepsake.
public static class LandingDust
{
    private static readonly Brush DustBrush = PetUi.Frozen(Color.FromArgb(0xA0, 0xE8, 0xDC, 0xCC));

    // Two little clouds kicked out sideways along the ground at groundY, fading; halfWidth: from the
    // center to where the clouds start; scale grows the puffs and their spread.
    public static void Draw(DrawingContext dc, Landing landing, double centerX, double groundY, double halfWidth, double scale = 1)
    {
        if (landing.Dust is < 0 or >= 1) return;
        double t = landing.Dust, size = (3 + 4 * Math.Clamp(landing.Strength, 0, 1)) * scale;
        dc.PushOpacity(1 - t);
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
            {
                double x = centerX + side * (halfWidth - 4 + t * (14 + i * 9) * scale), y = groundY - 2 - (t * (3 + i * 3) + i) * scale;
                dc.DrawEllipse(DustBrush, null, new Point(x, y), size * (1 - t * 0.4) - i * 0.8 * scale, (size - i * scale) * 0.7);
            }
        dc.Pop();
    }
}
