using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// 2026-11-05: the Scissors' cut drawn for real: halfway along it the big scissors must read as scissors (pale
// blades and red rings), not a dot on the line. PET_RENDER_REVIEW=<folder> saves the strip (scissors-cut.png).
internal static class ScissorsVisualChecks
{
    const double Width = 1000;

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var sim = new BattleSim(data, 0, Width, seed: 1, skills: new[] { BattleSkill.Push, BattleSkill.Heal, BattleSkill.Grabber, BattleSkill.Scissors, BattleSkill.Spray });
        var view = new BattleFieldView(sim) { OriginX = 0 };
        view.Measure(new Size(Width, BattleFieldView.FieldHeight));
        view.Arrange(new Rect(0, 0, Width, BattleFieldView.FieldHeight));

        for (int i = 0; i < 900 && !sim.Ready(BattleSkill.Scissors); i++) { sim.Step(1.0 / 60, 40, false); sim.ClearEvents(); }
        check(sim.Arm(BattleSkill.Scissors) && sim.UseOnScreen(BattleSkill.Scissors, 500, 12), "A cut starts near the floor");
        double wait = data.ScissorsPreview + data.ScissorsSweep / 2;
        for (double t = 0; t < wait; t += 1.0 / 60) { sim.Step(1.0 / 60, 40, false); sim.ClearEvents(); }
        check(sim.Cut != null, "The cut is halfway along its line");
        if (sim.Cut == null) return;

        view.InvalidateVisual(); view.UpdateLayout();
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var px = new byte[w * h * 4];
        bitmap.CopyPixels(px, w * 4, 0);
        int Count(byte r, byte g, byte b)
        {
            int n = 0;
            for (int i = 0; i < px.Length; i += 4)
                if (px[i + 3] > 200 && Math.Abs(px[i + 2] - r) <= 20 && Math.Abs(px[i + 1] - g) <= 20 && Math.Abs(px[i] - b) <= 20) n++;
            return n;
        }
        int blades = Count(0xE6, 0xEC, 0xF2), rings = Count(0xE8, 0x5D, 0x5D);
        check(blades > 300 && rings > 500, $"The running scissors are big ({blades} px of blade, {rings} px of ring)");
        // Clicked close to the floor, the art stays whole above it instead of sinking out of the window.
        int low = 0, floorRow = h - 2;
        for (int y = floorRow - 4; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                if (px[i + 3] > 200 && Math.Abs(px[i + 2] - 0xE8) <= 20 && Math.Abs(px[i + 1] - 0x5D) <= 20 && Math.Abs(px[i] - 0x5D) <= 20) low++;
            }
        check(low == 0, $"Near the floor the scissors stay above it ({low} px on the floor)");

        // The scissors' middle (their gold hub) sits on the line itself.
        int lineY = (int)(h - 2 - sim.Cut.Y), hub = 0;
        for (int y = lineY - 2; y <= lineY + 2; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                if (px[i + 3] > 200 && Math.Abs(px[i + 2] - 0xFF) <= 20 && Math.Abs(px[i + 1] - 0xD2) <= 20 && Math.Abs(px[i] - 0x3F) <= 30) hub++;
            }
        check(hub > 8, $"The scissors are centred on the cut ({hub} px of hub on the line)");
        Save(bitmap, "scissors-cut.png");
        ClawAndDrum(check);
    }

    private static void Save(BitmapSource image, string name)
    {
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is not string folder) return;
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(Path.Combine(folder, name)); encoder.Save(stream);
    }

    // The claw round a grabbed slime, and the drummer before its last beat (next to one that is not).
    private static void ClawAndDrum(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var sim = new BattleSim(data, 0, Width, seed: 2);
        Slime Add(string kind, double x)
        {
            var s = sim.AddSlime(x, data.Enemy(kind));
            s.Set(SlimeState.Moving); s.StateTime = 0.4; s.Facing = -1;
            return s;
        }
        var view = new BattleFieldView(sim) { OriginX = 0 };
        view.Measure(new Size(Width, BattleFieldView.FieldHeight));
        view.Arrange(new Rect(0, 0, Width, BattleFieldView.FieldHeight));
        var held = Add("slime", 150);
        held.Grabbed = true; held.Lift = 40;
        var calm = Add("drummer", 420);
        var loud = Add("drummer", 760);
        loud.BeatCount = data.Enemy("drummer").Link!.Beats - 1;
        loud.BeatClock = data.Enemy("drummer").Link!.Beat * 0.6;

        view.InvalidateVisual(); view.UpdateLayout();
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight;
        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var px = new byte[w * h * 4];
        bitmap.CopyPixels(px, w * 4, 0);
        int Count(byte r, byte g, byte b, int x0, int x1)
        {
            int n = 0;
            for (int y = 0; y < h; y++)
                for (int x = x0; x < x1; x++)
                {
                    int i = (y * w + x) * 4;
                    if (px[i + 3] > 180 && Math.Abs(px[i + 2] - r) <= 24 && Math.Abs(px[i + 1] - g) <= 24 && Math.Abs(px[i] - b) <= 24) n++;
                }
            return n;
        }
        int claw = Count(0xC9, 0xD3, 0xDC, 90, 210);
        check(claw > 250, $"A grabbed slime is held by a real claw ({claw} px of metal)");
        int quiet = Count(0xE5, 0x3E, 0x3E, 400, 560), warned = Count(0xE5, 0x3E, 0x3E, 700, 860);
        check(warned > quiet * 3 && warned > 120, $"The drummer's last beat stands out ({warned} red px against {quiet})");
        Save(bitmap, "claw-and-drum.png");
    }
}
