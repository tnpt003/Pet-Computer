using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// 1.5 M11, drawn for real with the shipped kinds: the fusing slime's feeler and the giant's three; the bomb
// slime's wick with a spark running down it once lit, and the red ring of its blast on the floor (filling
// as the fuse burns), riding on a quick slime's back; a bomb that blew up is not drawn; the splitting
// slime's seam and its half's plaster. PET_RENDER_REVIEW=<folder> saves the strip on dark and light floors
// (enemy-swarm.png).
internal static class SwarmVisualChecks
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
        var feeler = Color.FromRgb(0xFF, 0xD3, 0xE6);
        var giantPink = Color.FromRgb(0xE0, 0x7A, 0xAE);
        var spark = Color.FromRgb(0xFF, 0xF2, 0x9E);
        var red = Color.FromRgb(0xE5, 0x3E, 0x3E);
        var slate = Color.FromRgb(0x6E, 0x7B, 0x9C);
        var seam = Color.FromRgb(0x5A, 0x3E, 0x28);
        var plaster = Color.FromRgb(0xF7, 0xE8, 0xD2);

        Add("fuser", 80, -1);
        Add("giant", 200, -1);
        var bomb = Add("bomber", 420, -1);
        var mount = Add("runner", 620, -1);
        var rider = Add("bomber", 620, -1);
        var gone = Add("bomber", 820, -1);
        Add("splitter", 980, -1);
        Add("splitling", 1120, -1);
        var unlit = Shot(view);
        bomb.FuseLeft = 0.75; bomb.FuseTotal = 1.5;
        rider.LinkedTo = mount.Id; rider.RideHeight = mount.Height * 0.8;
        gone.Set(SlimeState.Defeated); gone.Exploded = true;
        var px = Shot(view, "enemy-swarm");

        check(Count(px, feeler, 65, 95, floor - 40, floor - 18, 16) > 6, "The fusing slime has a pink feeler on its head");
        check(Count(px, feeler, 160, 240, floor - 70, floor - 50, 16) > 12, "The giant has three feelers");
        check(Count(px, giantPink, 150, 250, floor - 50, floor) > 1200, "The giant is drawn big");
        int sparkNow = Count(px, spark, 405, 440, floor - 50, floor - 25, 20), sparkBefore = Count(unlit, spark, 405, 440, floor - 50, floor - 25, 20);
        check(sparkNow > 4 && sparkBefore == 0, $"A lit bomb has a spark on its wick ({sparkBefore} -> {sparkNow} pixels)");
        int ringNow = Count(px, red, 320, 520, floor - 14, floor + 2, 30, 100), ringBefore = Count(unlit, red, 320, 520, floor - 14, floor + 2, 30, 100);
        check(ringNow > 80 && ringNow > ringBefore * 2, $"Its blast ring on the floor turns red as the fuse burns ({ringBefore} -> {ringNow} pixels)");
        check(Count(px, slate, 600, 640, floor - 60, floor - 25) > 150, "A riding bomb sits on its mount's back");
        check(Count(px, slate, 790, 850, floor - 40, floor) < 5 && Count(unlit, slate, 790, 850, floor - 40, floor) > 150, "A bomb that blew up is not drawn");
        check(Count(px, seam, 970, 990, floor - 36, floor - 2, 20) > 20, "The splitting slime has a seam down its middle");
        check(Count(px, plaster, 1100, 1140, floor - 30, floor - 12, 16) > 15, "Its half wears a plaster on its head");
    }
}
