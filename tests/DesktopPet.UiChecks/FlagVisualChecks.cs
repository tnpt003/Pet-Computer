using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// 1.5 M12, drawn for real with the shipped flag slime: a pole on its back with a gold pennant streaming
// away from the side it faces, its gold aura ring on the floor, two gold speed lines behind an ally the
// aura speeds up (none behind one outside it). PET_RENDER_REVIEW=<folder> saves the strip on dark and
// light floors (enemy-flag.png).
internal static class FlagVisualChecks
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

    // Pixels (mostly opaque) near this colour inside a box (x from–to, y from–to).
    private static int Count(byte[] px, Color c, int x0, int x1, int y0, int y1, int tolerance = 24, int alpha = 180)
    {
        int n = 0, w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(w, x1); x++)
            {
                int i = (y * w + x) * 4, a = px[i + 3];
                if (a <= alpha) continue;
                // Premultiplied: back to the colour drawn.
                int r = px[i + 2] * 255 / a, g = px[i + 1] * 255 / a, b = px[i] * 255 / a;
                if (Math.Abs(r - c.R) <= tolerance && Math.Abs(g - c.G) <= tolerance && Math.Abs(b - c.B) <= tolerance) n++;
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
        var gold = Color.FromRgb(0xFF, 0xC8, 0x2E);
        var pole = Color.FromRgb(0x6B, 0x4A, 0x2E);

        Add("flagbearer", 300, -1);
        var sped = Add("slime", 420, -1);
        sped.AuraSpeed = data.Enemy("flagbearer").Link!.Speed;
        Add("slime", 700, -1);
        Add("flagbearer", 950, 1);
        var px = Shot(view, "enemy-flag");

        // Facing left: the pole at its right (back), the pennant streaming right of it.
        check(Count(px, pole, 300, 316, floor - 58, floor - 30, 20) > 10, "The flag slime carries a pole on its back");
        check(Count(px, gold, 310, 340, floor - 66, floor - 40) > 40, "A gold pennant streams from the pole, away from the side it faces");
        check(Count(px, gold, 920, 945, floor - 66, floor - 40) > 40 && Count(px, gold, 960, 990, floor - 66, floor - 40) < 10, "Turned the other way, the pennant streams the other way");
        int ring = Count(px, gold, 100, 250, floor - 16, floor + 2, 60, 100);
        check(ring > 40, $"Its gold aura ring lies on the floor ({ring} pixels)");
        int lines = Count(px, gold, 445, 470, floor - 22, floor - 14, 30, 120), none = Count(px, gold, 725, 750, floor - 22, floor - 14, 30, 120);
        check(lines > 6 && none == 0, $"An ally the aura speeds up trails gold speed lines, one outside it none ({lines} / {none})");
    }
}
