using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// 1.5 M09, drawn for real with the shipped kinds: the spitter's spout, the orange rings where its goo will
// land (an elite's two), a drop in the air over its ring, the puddle's purple rim; the spring slime's gold
// ring, red while it squeezes, grey once flattened. PET_RENDER_REVIEW=<folder> saves the strip on dark and
// light floors (enemy-spit-spring.png).
internal static class TrapVisualChecks
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
    private static int Count(byte[] px, Color c, int x0, int x1, int y0, int y1, int tolerance = 24)
    {
        int n = 0, w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(w, x1); x++)
            {
                int i = (y * w + x) * 4;
                if (px[i + 3] > 200 && Math.Abs(px[i + 2] - c.R) <= tolerance && Math.Abs(px[i + 1] - c.G) <= tolerance && Math.Abs(px[i] - c.B) <= tolerance) n++;
            }
        return n;
    }

    private static List<T> Private<T>(BattleSim sim, string field) =>
        (List<T>)typeof(BattleSim).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(sim)!;

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var sim = new BattleSim(data, 0, Width, new Random(1));
        Slime Add(string kind, double x, bool elite = false)
        {
            var s = sim.AddSlime(x, data.Enemy(kind));
            s.Set(SlimeState.Moving); s.StateTime = 0.4; s.Facing = -1;
            if (elite) s.Elite = s.Type.Elite;
            return s;
        }
        var view = new BattleFieldView(sim) { OriginX = 0 };
        view.Measure(new Size(Width, BattleFieldView.FieldHeight));
        view.Arrange(new Rect(0, 0, Width, BattleFieldView.FieldHeight));
        int floor = (int)BattleFieldView.FieldHeight - 2;
        var orange = Color.FromRgb(0xFF, 0x9A, 0x3C);
        var purple = Color.FromRgb(0x9A, 0x5C, 0xD0);
        var gold = Color.FromRgb(0xF2, 0xB8, 0x1E);
        var red = Color.FromRgb(0xE5, 0x3E, 0x3E);
        var lilac = Color.FromRgb(0xC3, 0x8B, 0xE8);

        var spitter = Add("spitter", 60);
        var elite = Add("spitter", 330, elite: true);
        var ready = Add("springer", 690);
        var squeezing = Add("springer", 810);
        var flat = Add("springer", 930);
        var before = Shot(view);
        // An elite spitter winding up (two rings), a drop in the air, a puddle; the springs set, squeezing, flattened.
        foreach (var (s, at) in new[] { (spitter, 180.0), (elite, 230.0) }) { s.Set(SlimeState.Windup); s.Spitting = true; s.SpitAt = at; s.StateTime = 0.6; }
        spitter.Set(SlimeState.Moving);
        Private<BattleGoo>(sim, "goos").Add(new BattleGoo { FromX = 60, X = 420, Flight = 0.8, Remaining = 0.4, Radius = 42, Tint = "#C38BE8" });
        Private<BattlePuddle>(sim, "puddles").Add(new BattlePuddle { X = 540, Radius = 48, Remaining = 5, Life = 6 });
        squeezing.Spring = SpringState.Squeezing; squeezing.SpringClock = 0.4;
        flat.Spring = SpringState.Flat; flat.SpringClock = 2;
        var px = Shot(view, "enemy-spit-spring");

        check(Count(px, orange, 220, 250, floor - 14, floor + 2) > 10 && Count(px, orange, 150, 210, floor - 14, floor + 2) > 10,
            "An elite spitter winding up shows two orange rings where its drops will land");
        check(Count(px, lilac, 40, 80, floor - 45, floor - 28) > 15, "The spitter's spout shows on its head");
        // Halfway from 60 to 420: over 240, high up.
        check(Count(px, lilac, 220, 260, 20, floor - 40) > 30 && Count(before, lilac, 220, 260, 20, floor - 40) == 0, "A drop of goo flies over the field");
        check(Count(px, purple, 480, 600, floor - 14, floor + 2) > 30 && Count(before, purple, 480, 600, floor - 14, floor + 2) == 0, "The puddle's purple rim lies on the floor");
        check(Count(px, gold, 620, 760, floor - 14, floor + 2) > 30, "A spring that is set shows its gold ring");
        check(Count(px, red, 740, 880, floor - 14, floor + 2) > 30 && Count(before, red, 740, 880, floor - 14, floor + 2) < 5, "Squeezing, its ring turns red");
        // The ring's left end, clear of the yellow body and of the squeezing ring next to it.
        int flatGold = Count(px, gold, 874, 892, floor - 14, floor + 2), flatRed = Count(px, red, 874, 892, floor - 14, floor + 2), setGold = Count(px, gold, 624, 650, floor - 14, floor + 2);
        check(flatGold < 5 && flatRed < 5 && setGold > 10, $"Flattened, its ring is neither gold nor red (gold {flatGold}, red {flatRed}; a set one's is gold there: {setGold})");
        int coilSet = Count(px, Color.FromRgb(0xE8, 0x5D, 0x5D), 670, 710, 0, floor - 30), coilFlat = Count(px, Color.FromRgb(0xE8, 0x5D, 0x5D), 910, 950, 0, floor - 30);
        check(coilSet > 10 && coilFlat < coilSet / 2, $"The spring's red pad stands on its coil, sunk once flattened ({coilSet} -> {coilFlat} pixels high up)");
    }
}
