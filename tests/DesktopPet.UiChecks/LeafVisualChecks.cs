using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// 2026-11-04 (batch C4), drawn for real with the shipped kinds: the planted flag instead of a slime body, the
// green leaf bubble on a bomb slime, a bomb's soot, the quick slime's "!" and the big drum's rings.
// PET_RENDER_REVIEW=<folder> saves the strip (enemy-flag-leaf-drum.png).
internal static class LeafVisualChecks
{
    const double Width = 1000;

    private static (byte[] Pixels, BitmapSource Image) Shot(BattleFieldView view)
    {
        view.InvalidateVisual(); view.UpdateLayout();
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var pixels = new byte[w * h * 4];
        bitmap.CopyPixels(pixels, w * 4, 0);
        return (pixels, bitmap);
    }

    private static int Count(byte[] px, Color c, int x0, int x1, int tolerance = 28, int alpha = 180)
    {
        int n = 0, w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        for (int y = 0; y < h; y++)
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
        Slime Add(string kind, double x)
        {
            var s = sim.AddSlime(x, data.Enemy(kind));
            s.Set(SlimeState.Moving); s.StateTime = 0.4; s.Facing = -1;
            return s;
        }
        var view = new BattleFieldView(sim) { OriginX = 0 };
        view.Measure(new Size(Width, BattleFieldView.FieldHeight));
        view.Arrange(new Rect(0, 0, Width, BattleFieldView.FieldHeight));

        // The big drum goes off first: a few frames later its rings are on their way out.
        var drummer = Add("drummer", 860);
        drummer.BeatCount = data.Enemy("drummer").Link!.Beats - 1;
        drummer.BeatClock = data.Enemy("drummer").Link!.Beat - 0.01;
        for (int i = 0; i < 16; i++) { sim.Step(1.0 / 60, 40, false); sim.ClearEvents(); }

        var post = Add("flagpost", 120);
        var herb = Add("mender", 270);
        var bomb = Add("bomber", 400);
        bomb.LeafShield = bomb.LeafUsed = true;
        bomb.LeafBy = herb.Id;
        bomb.LeafCastAt = sim.Time;
        var mount = Add("runner", 560);
        var burnt = Add("bomber", 660);
        burnt.Charred = true; burnt.ThrownBy = mount.Id; burnt.RecoilLeft = burnt.RecoilTotal = 0.6; burnt.RecoilFrom = burnt.RecoilTo = 660;

        var (px, image) = Shot(view);
        var green = Color.FromRgb(0x4F, 0xC9, 0x6A);
        check(Count(px, Color.FromRgb(0x5B, 0x8D, 0xEF), 100, 170) > 120, "The planted flag shows its blue cloth");
        check(Count(px, green, 340, 460) > 25, "A shielded bomb slime has leaves going round it");
        check(Count(px, Color.FromRgb(0xE5, 0x3E, 0x3E), 530, 590, 30) > 12, "The quick slime waiting for its bomb shows a !");
        check(Count(px, Color.FromRgb(0xF0, 0x6A, 0x3A), 700, 1000, 40, 120) > 40, "The big drum runs out in rings");

        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not string folder) return;
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x24)), null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)), null, new Rect(0, h, w, h));
            dc.DrawImage(image, new Rect(0, 0, w, h));
            dc.DrawImage(image, new Rect(0, h, w, h));
        }
        var strip = new RenderTargetBitmap(w, h * 2, 96, 96, PixelFormats.Pbgra32);
        strip.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(strip));
        using var stream = File.Create(Path.Combine(folder, "enemy-flag-leaf-drum.png"));
        encoder.Save(stream);
    }
}
