using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// 1.5 M10, drawn for real with the shipped kinds: the drummer's red drum at its front (on the side it
// faces), its beat dots and the orange ring of its band on the floor; the chain slime's steel links on its
// head and the green rope of goo to its ally. PET_RENDER_REVIEW=<folder> saves the strip on dark and light
// floors (enemy-drummer-chain.png).
internal static class BandVisualChecks
{
    const double Width = 1000;

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

    // Opaque pixels near this colour inside a box (x from–to, y from–to).
    private static int Count(byte[] px, Color c, int x0, int x1, int y0, int y1, int tolerance = 24, int alpha = 200)
    {
        int n = 0, w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(w, x1); x++)
            {
                int i = (y * w + x) * 4;
                if (px[i + 3] > alpha && Math.Abs(px[i + 2] - c.R) <= tolerance && Math.Abs(px[i + 1] - c.G) <= tolerance && Math.Abs(px[i] - c.B) <= tolerance) n++;
            }
        return n;
    }

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var sim = new BattleSim(data, 0, Width, new Random(1));
        Slime Add(string kind, double x, int facing)
        {
            var s = sim.AddSlime(x, data.Enemy(kind));
            s.Set(SlimeState.Moving); s.StateTime = 0.4; s.Facing = facing;
            return s;
        }
        var view = new BattleFieldView(sim) { OriginX = 0 };
        view.Measure(new Size(Width, BattleFieldView.FieldHeight));
        view.Arrange(new Rect(0, 0, Width, BattleFieldView.FieldHeight));
        int floor = (int)BattleFieldView.FieldHeight - 2;
        var drum = Color.FromRgb(0xD6, 0x45, 0x45);
        var rope = Color.FromRgb(0x8E, 0xD8, 0x6E);
        var steel = Color.FromRgb(0xB8, 0xB2, 0xA8);
        var beat = Color.FromRgb(0xFF, 0x9F, 0x4A);

        var drummer = Add("drummer", 150, -1);
        drummer.BeatCount = 2; drummer.BeatClock = 0.1;
        var ally = Add("slime", 600, -1);
        var chain = Add("chain", 820, -1);
        var before = Shot(view);
        chain.LinkedTo = ally.Id;
        var px = Shot(view, "enemy-drummer-chain");

        // Facing left, its drum is on its left side, not its right.
        int left = Count(px, drum, 110, 150, floor - 30, floor), right = Count(px, drum, 150, 190, floor - 30, floor);
        check(left > 30 && right < left / 4, $"The drummer holds its red drum at the side it faces ({left} left, {right} right)");
        int dots = Count(px, beat, 120, 180, 0, floor - 40, 40), ring = Count(px, Color.FromRgb(150, 93, 43), 0, 400, floor - 14, floor + 2, 30, alpha: 100); // The ring is drawn at alpha 150 (premultiplied).
        check(dots > 6, $"Its beat dots show over its head ({dots} orange pixels)");
        check(ring > 40, $"The orange ring of its band lies on the floor ({ring} pixels)");
        check(Count(px, steel, 800, 840, floor - 45, floor - 20) > 15, "The chain slime wears its steel links on its head");
        int ropeNow = Count(px, rope, 640, 790, floor - 40, floor + 2), ropeBefore = Count(before, rope, 640, 790, floor - 40, floor + 2);
        check(ropeNow > 60 && ropeBefore < 5, $"Tied, a green rope of goo runs from it to its ally ({ropeBefore} -> {ropeNow} pixels)");
    }
}
