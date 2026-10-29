using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// 1.5 M13, drawn for real with the shipped kinds: the ghost seen, faded out (barely there) and brought out
// by the cursor (a soft glow); the bubble slime floating in its pale blue bubble, with cracks for the
// clicks; the mirage's real one with its shadow once seen through, a copy blurred and shadowless; the sulky
// slime puffed up with its pink bar of how soothed it is, and the word a hit on it shows.
// PET_RENDER_REVIEW=<folder> saves the strip on dark and light floors (enemy-tricks.png).
internal static class TrickVisualChecks
{
    const double Width = 1200;

    private static byte[] Shot(BattleFieldView view, string? save = null)
    {
        view.InvalidateVisual(); view.UpdateLayout();
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var pixels = new byte[w * h * 4];
        bitmap.CopyPixels(pixels, w * 4, 0);
        if (save != null && Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string folder)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x24)), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)), null, new Rect(0, h, w, h));
                dc.DrawImage(bitmap, new Rect(0, 0, w, h));
                dc.DrawImage(bitmap, new Rect(0, h, w, h));
            }
            var image = new RenderTargetBitmap(w, h * 2, 96, 96, PixelFormats.Pbgra32);
            image.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = System.IO.File.Create(System.IO.Path.Combine(folder, save + ".png"));
            encoder.Save(stream);
        }
        return pixels;
    }

    // Pixels near this colour inside a box (x from–to, y from–to), at least this opaque. Premultiplied
    // colours are divided back by alpha first, so see-through ones are compared by their own colour.
    private static int Count(byte[] px, Color c, int x0, int x1, int y0, int y1, int tolerance = 24, int alpha = 200)
    {
        int n = 0, w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(w, x1); x++)
            {
                int i = (y * w + x) * 4, a = px[i + 3];
                if (a < alpha || a == 0) continue;
                int r = px[i + 2] * 255 / a, g = px[i + 1] * 255 / a, b = px[i] * 255 / a;
                if (Math.Abs(r - c.R) <= tolerance && Math.Abs(g - c.G) <= tolerance && Math.Abs(b - c.B) <= tolerance) n++;
            }
        return n;
    }

    // Pixels at least this opaque in a box (anything drawn there).
    private static int Inked(byte[] px, int x0, int x1, int y0, int y1, int alpha)
    {
        int n = 0, w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(w, x1); x++)
                if (px[(y * w + x) * 4 + 3] >= alpha) n++;
        return n;
    }

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var sim = new BattleSim(data, 0, Width, new Random(1));
        Slime Add(string kind, double x)
        {
            var s = sim.AddSlime(x, data.Enemy(kind));
            s.Set(SlimeState.Moving); s.StateTime = 0.4; s.Facing = -1;
            return s;
        }
        var view = new BattleFieldView(sim) { OriginX = 0 };
        view.Measure(new Size(Width, BattleFieldView.FieldHeight));
        view.Arrange(new Rect(0, 0, Width, BattleFieldView.FieldHeight));
        int floor = (int)BattleFieldView.FieldHeight - 2;
        var ghostBlue = Color.FromRgb(0xC9, 0xD6, 0xF2);
        var bubbleEdge = Color.FromRgb(0x6C, 0xC4, 0xEE);
        var bubbleBody = Color.FromRgb(0x7F, 0xC8, 0xEA);
        var crack = Color.FromRgb(0x2E, 0x6E, 0x96);
        var sulkPink = Color.FromRgb(0xF2, 0xA7, 0xA0);
        var calmPink = Color.FromRgb(0xFF, 0x8F, 0xB8);
        var glow = Color.FromRgb(0xFF, 0xF4, 0xB0);

        var seen = Add("phantom", 80);
        var faded = Add("phantom", 200);
        var lit = Add("phantom", 320);
        var bubble = Add("bubble", 470);
        var real = Add("mirage", 640);
        var copy = Add("mirage", 780);
        var sulky = Add("sulky", 1000);
        var calm = Add("sulky", 1130);
        copy.DecoyOf = real.Id;
        real.Cast = copy.Cast = true;
        var before = Shot(view);
        faded.Faded = true;
        lit.Revealed = 2;
        bubble.Pokes = 2;
        real.Revealed = copy.Revealed = 2;
        sulky.Puff = 1.6;
        calm.Calm = 0.5;
        var px = Shot(view, "enemy-tricks");

        int seenBody = Count(px, ghostBlue, 60, 100, floor - 30, floor - 4, 20), fadedBody = Count(px, ghostBlue, 180, 220, floor - 30, floor - 4, 20);
        check(seenBody > 300 && fadedBody < seenBody / 10, $"A faded ghost is barely there ({seenBody} → {fadedBody} body pixels)");
        check(Inked(px, 170, 230, floor - 40, floor, 60) > 40, "A faint wobbling outline still shows where it is");
        check(Count(px, glow, 290, 350, floor - 50, floor, 30, 60) > 40 && Count(before, glow, 290, 350, floor - 50, floor, 30, 60) < 10,
            "Brought out by the cursor it glows softly");
        check(Count(px, bubbleEdge, 440, 500, floor - 70, floor, 20) > 80, "The bubble slime floats in a pale blue bubble");
        check(Count(px, bubbleBody, 455, 485, floor - 8, floor, 16) < Count(px, bubbleBody, 455, 485, floor - 28, floor - 18, 16),
            "Lifted off the floor in it");
        check(Count(px, crack, 470, 520, floor - 60, floor - 10, 20) > 6 && Count(before, crack, 470, 520, floor - 60, floor - 10, 20) == 0,
            "Each click so far leaves a crack in the bubble");
        // Just past the bodies' sides at their feet (the real one's shadow reaches out there).
        int Sides(byte[] p, double x) => Inked(p, (int)x - 30, (int)x - 25, floor - 4, floor + 1, 30) + Inked(p, (int)x + 25, (int)x + 30, floor - 4, floor + 1, 30);
        int realShadow = Sides(px, real.X), copyShadow = Sides(px, copy.X), unseenShadow = Sides(before, real.X);
        check(realShadow > copyShadow + 10 && realShadow > unseenShadow + 10, $"Seen through, the mirage's real one has a shadow and its copy none ({realShadow} / {copyShadow}, unseen {unseenShadow})");
        int copyNow = Inked(px, 765, 795, floor - 28, floor - 6, 250), copyBefore = Inked(before, 765, 795, floor - 28, floor - 6, 250);
        check(copyBefore > 300 && copyNow < copyBefore / 3, $"And the copy blurs, see-through ({copyBefore} → {copyNow} solid pixels)");
        int puffed = Count(px, sulkPink, 940, 1060, floor - 60, floor), plain = Count(before, sulkPink, 940, 1060, floor - 60, floor);
        check(puffed > plain * 2, $"A sulky slime puffs up ({plain} → {puffed} pixels)");
        check(Count(px, calmPink, 1100, 1160, floor - 55, floor - 38, 20) > 20, "Its pink bar shows how soothed it is");

        // The word a hit shows: a miss on the ghost or the bubble, a sulk on the sulky slime.
        var hit = new BattleSim(data, 0, Width, new Random(1));
        var hitView = new BattleFieldView(hit) { OriginX = 0 };
        hitView.Measure(new Size(Width, BattleFieldView.FieldHeight));
        hitView.Arrange(new Rect(0, 0, Width, BattleFieldView.FieldHeight));
        var empty = Shot(hitView);
        hit.AddEffect(BattleEffectKind.Miss, 300, 30);
        hit.AddEffect(BattleEffectKind.Miss, 700, 30, 1);
        hit.AddEffect(BattleEffectKind.Heart, 1000, 40);
        var words = Shot(hitView);
        check(Inked(words, 260, 340, 0, floor - 20, 200) > 60 && Inked(empty, 260, 340, 0, floor - 20, 200) == 0, "A hit that does nothing shows MISS");
        check(Count(words, calmPink, 660, 740, 0, floor - 20, 20) > 30, "A hit on a sulky slime shows a pink SULK!");
        check(Count(words, Color.FromRgb(0xFF, 0x6B, 0x8E), 960, 1040, 0, floor, 20) > 30, "A soothed one leaves in hearts");
    }
}
