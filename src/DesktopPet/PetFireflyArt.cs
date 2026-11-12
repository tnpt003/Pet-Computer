using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// Catch the fireflies (docs/MINI-GAMES.md → M3): the play area's own window, under the cat's. Fireflies in
// the cat's vector style: bigger and warmer than the weather's night motes (a small ink body, flapping wings,
// an amber glowing tail and halo), so they read on a light or dark desktop. Each live fly has a faint disc
// the size of its tap reach behind it, so that spot takes the click; the rest of the window is fully
// transparent and clicks pass through. A caught fly bursts into sparks (with "+1" when the user caught it).
public sealed class FireflyArt : FrameworkElement
{
    private static Brush B(byte a, byte r, byte g, byte b) => PetUi.Frozen(Color.FromArgb(a, r, g, b));
    // A soft glow fading out to nothing (no hard rim), and the brighter core of a burst.
    private static readonly Brush Halo = Glow(), HaloCore = B(0x90, 0xFF, 0xD8, 0x6A), Tail = B(0xFF, 0xFF, 0xE0, 0x7A),
        TailShine = B(0xFF, 0xFF, 0xFB, 0xE6), Wing = B(0xC8, 0xFF, 0xFF, 0xFF), Hit = B(0x02, 0xFF, 0xFF, 0xFF), Spark = B(0xFF, 0xFF, 0xD8, 0x6A);
    private static readonly Pen Outline = PetUi.FrozenPen(PetUi.ArtInk, 1.2), WingEdge = PetUi.FrozenPen(PetUi.ArtInk, 0.8),
        SparkPen = PetUi.FrozenPen(B(0xFF, 0xFF, 0xC8, 0x50), 1.8), TextHalo = PetUi.FrozenPen(PetUi.CardBack, 3);
    private static readonly Typeface Face = new(PetUi.TextFont, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    private static Brush Glow()
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0xC0, 0xFF, 0xD8, 0x6A), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x70, 0xFF, 0xC2, 0x4A), 0.4));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xB0, 0x30), 1));
        brush.Freeze();
        return brush;
    }

    public sealed class Burst { public double X, Y, Age; public bool ByCat; }

    private readonly FireflySwarm swarm;
    public readonly List<Burst> Bursts = new();
    public const double BurstSeconds = 0.6;

    public FireflyArt(FireflySwarm swarm) { this.swarm = swarm; }

    public void Size(double width, double height) { Width = width; Height = height; }

    public void Step(double dt)
    {
        foreach (var burst in Bursts) burst.Age += dt;
        Bursts.RemoveAll(b => b.Age >= BurstSeconds);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double t = swarm.Time;
        foreach (var fly in swarm.Flies)
        {
            if (fly.Caught) continue;
            if (!swarm.Leaving) dc.DrawEllipse(Hit, null, new Point(fly.X, fly.Y), FireflyRules.TapRadius, FireflyRules.TapRadius);
            // A slow glow, quick blinks as it is about to go out.
            bool ending = fly.Life - fly.Age < FireflyRules.FadeSeconds;
            double glow = 0.6 + 0.4 * Math.Sin(fly.Age * (ending ? 18 : 4.5) + fly.Phase);
            Draw(dc, fly.X, fly.Y, Math.Cos(fly.Heading) < 0 ? -1 : 1, fly.Age * 34 + fly.Phase, glow, fly.Fade);
        }
        foreach (var burst in Bursts) DrawBurst(dc, burst);
    }

    // One firefly at (x, y), facing left or right; flap: the wings' phase; glow 0–1; fade 0–1.
    public static void Draw(DrawingContext dc, double x, double y, int facing, double flap, double glow, double fade)
    {
        if (fade <= 0) return;
        dc.PushOpacity(fade);
        dc.PushOpacity(Math.Clamp(glow, 0, 1));
        dc.DrawEllipse(Halo, null, new Point(x - facing * 5, y + 1), 18, 18);
        dc.Pop();
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new ScaleTransform(facing * 1.25, 1.25));
        // Wings over the back, flapping.
        double open = 0.45 + 0.55 * Math.Abs(Math.Sin(flap));
        dc.PushTransform(new ScaleTransform(1, open, 0, -1));
        dc.DrawEllipse(Wing, WingEdge, new Point(-1, -5), 4.2, 3.2);
        dc.DrawEllipse(Wing, WingEdge, new Point(2, -5.5), 3.6, 3);
        dc.Pop();
        // Body, head, the glowing tail.
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(0, 0), 4.2, 2.8);
        dc.DrawEllipse(PetUi.ArtInk, null, new Point(4.4, -0.6), 2.2, 2.2);
        dc.PushOpacity(0.7 + 0.3 * Math.Clamp(glow, 0, 1));
        dc.DrawEllipse(Tail, Outline, new Point(-4.6, 0.8), 3.8, 3.3);
        dc.DrawEllipse(TailShine, null, new Point(-5.2, 0), 1.4, 1.1);
        dc.Pop();
        dc.Pop(); dc.Pop();
        dc.Pop();
    }

    private void DrawBurst(DrawingContext dc, Burst burst)
    {
        double f = burst.Age / BurstSeconds, out_ = 4 + 16 * Math.Sqrt(f);
        dc.PushOpacity(1 - f);
        dc.DrawEllipse(HaloCore, null, new Point(burst.X, burst.Y), 10 * (1 - f) + 2, 10 * (1 - f) + 2);
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4 + 0.3;
            var from = new Point(burst.X + Math.Cos(a) * out_, burst.Y + Math.Sin(a) * out_);
            var to = new Point(burst.X + Math.Cos(a) * (out_ + 4), burst.Y + Math.Sin(a) * (out_ + 4));
            dc.DrawLine(SparkPen, from, to);
            if (i % 2 == 0) dc.DrawEllipse(Spark, null, to, 1.4, 1.4);
        }
        if (!burst.ByCat)
        {
            var text = new FormattedText("+1", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, PetUi.BodySize, PetUi.Ink, 1.25);
            var outline = text.BuildGeometry(new Point(burst.X - text.Width / 2, Math.Max(2, burst.Y - 30 - 14 * f)));
            dc.DrawGeometry(null, TextHalo, outline);
            dc.DrawGeometry(PetUi.Ink, null, outline);
        }
        dc.Pop();
    }
}
