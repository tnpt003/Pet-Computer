using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// 1.5 M14, drawn for real with the shipped kinds: on the strip a flyer up in the air leaves only a faint
// shadow, one knocked down lies there with its wings folded; a burrower under the floor is a mound of
// earth, its cracks fill up red where it will come up, and coming up it rises half out of the floor. In
// its own window a flyer shows its mint body and white wings, and a red ring while it aims; the sky's
// canvas shows the aim line and a drop of goo. PET_RENDER_REVIEW=<folder> saves the strip
// (enemy-sky.png) and a flyer's window (enemy-flyer.png) on dark and light backgrounds.
internal static class SkyVisualChecks
{
    const double Width = 1200;

    internal static byte[] Render(FrameworkElement view, int w, int h, string? save = null)
    {
        view.Measure(new Size(w, h));
        view.Arrange(new Rect(0, 0, w, h));
        view.InvalidateVisual(); view.UpdateLayout();
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

    // Pixels near this colour inside a box, at least this opaque (premultiplied colours divided back).
    internal static int Count(byte[] px, int w, int h, Color c, int x0, int x1, int y0, int y1, int tolerance = 24, int alpha = 200)
    {
        int n = 0;
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

    private static int Inked(byte[] px, int w, int h, int x0, int x1, int y0, int y1, int alpha)
    {
        int n = 0;
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
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight, floor = h - 2;
        var view = new BattleFieldView(sim) { OriginX = 0 };
        var mint = Color.FromRgb(0x8F, 0xD3, 0xC8);
        var tan = Color.FromRgb(0xC4, 0x9A, 0x6C);
        var earth = Color.FromRgb(0x9A, 0x6B, 0x3F);
        var red = Color.FromRgb(0xE5, 0x3E, 0x3E);
        var wing = Color.FromRgb(0xF4, 0xFB, 0xFF);

        var aloft = Add("flyer", 120);
        aloft.Altitude = 400;
        var down = Add("flyer", 320);
        down.Fly = FlyPhase.Downed; down.Altitude = 0; down.Set(SlimeState.Stunned); down.StunRemaining = 2;
        var under = Add("burrower", 560);
        under.Burrow = BurrowPhase.Under;
        var cracking = Add("burrower", 800);
        cracking.Burrow = BurrowPhase.Cracking; cracking.CrackX = 800; cracking.BurrowClock = BattleSim.CrackTime(cracking) * 0.3;
        var rising = Add("burrower", 1050);
        rising.Burrow = BurrowPhase.Emerging; rising.BurrowClock = data.Enemy("burrower").Burrow!.Emerge * 0.5;
        var px = Render(view, w, h, "enemy-sky");

        check(Count(px, w, h, mint, 90, 150, 0, floor) == 0 && Inked(px, w, h, 90, 150, floor - 8, floor + 1, 20) > 30,
            "Up in the air a flyer leaves only a faint shadow on the strip");
        check(Count(px, w, h, mint, 290, 350, floor - 40, floor) > 200 && Count(px, w, h, wing, 270, 370, floor - 50, floor, 12) > 20,
            "Knocked down it lies on the strip, wings folded");
        check(Count(px, w, h, earth, 530, 590, floor - 20, floor) > 150 && Count(px, w, h, tan, 530, 590, floor - 40, floor - 20) < 10,
            "Under the floor a burrower is only a mound of earth");
        check(Count(px, w, h, red, 740, 860, floor - 14, floor + 1, 30, 120) > 40, "Its cracks fill up red where it will come up");
        int risingBody = Count(px, w, h, tan, 1020, 1080, floor - 40, floor);
        check(risingBody > 40 && risingBody < 600, $"Coming up it rises half out of the floor ({risingBody} body pixels)");

        // A flyer's own window.
        var flyerView = new FlyerView(sim, aloft.Id, () => view);
        int size = (int)PetWindow.FlyerViewSize;
        var still = Render(flyerView, size, size);
        aloft.AimLeft = data.Enemy("flyer").Fly!.Aim * 0.4;
        var aiming = Render(flyerView, size, size, "enemy-flyer");
        check(Count(still, size, size, mint, 20, 100, 40, size) > 300 && Count(still, size, size, wing, 10, 110, 30, size, 12) > 30,
            "In its own window a flyer shows its mint body and white wings");
        check(Count(aiming, size, size, red, 0, size, 0, size, 30, 120) > Count(still, size, size, red, 0, size, 0, size, 30, 120) + 40, "Aiming, a red ring pulses round it");

        // The sky's canvas: the aim line to the cat and a drop of goo.
        var sky = new SkyShotView(sim) { ToScreen = (x, hh) => new Point(x, 500 - hh), CatAt = (300, 34) };
        aloft.AimLeft = 0;
        var empty = Render(sky, 600, 500);
        aloft.AimLeft = data.Enemy("flyer").Fly!.Aim * 0.4;
        aloft.X = 120; aloft.Altitude = 300;
        var shots = (List<BattleShot>)typeof(BattleSim).GetField("shots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(sim)!;
        shots.Add(new BattleShot { X = 450, Height = 200, VX = -300, VY = -200, Tint = "#8FD3C8" });
        var drawn = Render(sky, 600, 500, "enemy-sky-shots");
        check(Count(drawn, 600, 500, red, 0, 600, 0, 500, 30, 120) > 60 && Count(empty, 600, 500, red, 0, 600, 0, 500, 30, 120) == 0, "The aim line and ring show on the sky's canvas");
        check(Count(drawn, 600, 500, mint, 430, 470, 280, 320) > 40, "A drop of goo shows where it flies");
    }
}
