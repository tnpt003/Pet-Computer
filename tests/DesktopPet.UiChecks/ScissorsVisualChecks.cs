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

        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string folder)
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(folder, "scissors-cut.png")); encoder.Save(stream);
        }
    }
}
