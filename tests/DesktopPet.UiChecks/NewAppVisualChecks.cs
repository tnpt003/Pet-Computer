using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// 1.5 M26, drawn for real: the Screenshot icon (a camera on a sky-blue tile), frozen slimes in viewfinder
// corners and a dazzled cat's stars on the strip, the captcha's nine tiles (cats ticked once picked), the
// HUD's low battery strip and frozen cursor cover. PET_RENDER_REVIEW=<folder> saves them on dark and
// light backgrounds (app-screenshot.png, captcha.png, hud-malware.png).
internal static class NewAppVisualChecks
{
    private static byte[] Render(Visual visual, int w, int h, string? save)
    {
        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[w * h * 4];
        bitmap.CopyPixels(pixels, w * 4, 0);
        if (save != null && Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string folder)
        {
            var both = new DrawingVisual();
            using (var dc = both.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x24)), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)), null, new Rect(0, h, w, h));
                dc.DrawImage(bitmap, new Rect(0, 0, w, h));
                dc.DrawImage(bitmap, new Rect(0, h, w, h));
            }
            var image = new RenderTargetBitmap(w, h * 2, 96, 96, PixelFormats.Pbgra32);
            image.Render(both);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = System.IO.File.Create(System.IO.Path.Combine(folder, save + ".png"));
            encoder.Save(stream);
        }
        return pixels;
    }

    // Opaque pixels near this colour inside a box.
    private static int Count(byte[] px, int w, int h, Color c, int x0, int x1, int y0, int y1, int tolerance = 26, int alpha = 160)
    {
        int n = 0;
        for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(w, x1); x++)
            {
                int i = (y * w + x) * 4, a = px[i + 3];
                if (a <= alpha) continue;
                int r = px[i + 2] * 255 / a, g = px[i + 1] * 255 / a, b = px[i] * 255 / a;
                if (Math.Abs(r - c.R) <= tolerance && Math.Abs(g - c.G) <= tolerance && Math.Abs(b - c.B) <= tolerance) n++;
            }
        return n;
    }

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        // The strip: two frozen slimes, the camera tile and flash drawn beside them, a dazzled cat.
        const int W = 900, H = (int)BattleFieldView.FieldHeight;
        var sim = new BattleSim(data, 0, W, new Random(1));
        foreach (double x in new[] { 250.0, 520.0 })
        {
            var s = sim.AddSlime(x, data.Enemy("slime"));
            s.Set(SlimeState.Moving); s.StateTime = 0.4;
        }
        sim.FrozenRemaining = 2;
        sim.DazzleRemaining = 1;
        sim.Step(1.0 / 60, 760, false);
        var view = new BattleFieldView(sim) { OriginX = 0 };
        view.Measure(new Size(W, H)); view.Arrange(new Rect(0, 0, W, H));
        view.UpdateLayout();
        var field = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        field.Render(view);
        var strip = new DrawingVisual();
        using (var dc = strip.RenderOpen())
        {
            dc.DrawImage(field, new Rect(0, 0, W, H));
            AppArt.DrawTile(dc, BattleAppKind.Screenshot, new Point(70, 60), 50);
            AppArt.DrawFlash(dc, new Point(150, 60), 24, 0);
        }
        var px = Render(strip, W, H, "app-screenshot");
        int floor = H - 2;
        var finder = Color.FromRgb(0x3E, 0x7F, 0xB0);
        check(Count(px, W, H, Color.FromRgb(0x5F, 0xA8, 0xD8), 45, 95, 35, 85) > 400 && Count(px, W, H, Color.FromRgb(0xFF, 0xF6, 0xE6), 55, 85, 45, 75) > 60,
            "The Screenshot icon: a sky-blue tile with a pale camera");
        int corners = Count(px, W, H, finder, 200, 300, floor - 70, floor + 2, 40, 120);
        check(corners > 20, $"A frozen slime stands inside viewfinder corners ({corners} pixels)");
        int stars = Count(px, W, H, Color.FromRgb(0xFF, 0xE0, 0x6B), 720, 800, floor - 110, floor - 80, 30);
        check(stars > 15, $"A dazzled cat has gold stars before its eyes ({stars} pixels)");

        // The captcha: its window body, the title bar, nine tiles; cats ticked once picked.
        var stage = new StageSpec { Id = "captcha_look", NewAppsFrom = 1, MalwarePlan = new[] { new MalwareCue { At = 0, Kind = "captcha" } } };
        var settings = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 1 }, SlimeMaxHp = 1000, SlimeDamage = 0, SlimeSpeed = 0,
            Apps = new AppsSpec { InfectChance = 0, MalwareMinThreat = 0 } };
        var csim = new BattleSim(settings, 0, 1200, stage: stage, seed: 3);
        for (int i = 0; i < 300 && csim.Captcha == null; i++) csim.Step(1.0 / 60, 600, false);
        check(csim.Captcha != null, "(setup) a captcha to draw");
        if (csim.Captcha is BattleCaptcha c)
        {
            int cat = Array.IndexOf(c.Tiles, BattleCaptcha.Cat);
            csim.ClickCaptcha(cat);
            int cw = (int)c.Width, ch = (int)BattleSim.CaptchaHeight;
            var cview = new CaptchaView(csim) { Language = "en" };
            cview.Measure(new Size(cw, ch)); cview.Arrange(new Rect(0, 0, cw, ch));
            var cpx = Render(cview, cw, ch, "captcha");
            var tile = cview.TileRect(cat);
            check(Count(cpx, cw, ch, Color.FromRgb(0x8B, 0x7F, 0xB8), 10, cw - 10, 3, 18) > 300, "The captcha has its title bar");
            int coats = 0;
            for (int i = 0; i < 9; i++)
            {
                var r = cview.TileRect(i);
                if (c.Tiles[i] == BattleCaptcha.Cat) coats += Count(cpx, cw, ch, Color.FromRgb(0xF2, 0xA9, 0x5C), (int)r.Left, (int)r.Right, (int)r.Top, (int)r.Bottom) > 30 ? 1 : 0;
            }
            check(coats == c.Tiles.Count(t => t == BattleCaptcha.Cat), $"Each cat tile shows a cat's orange face ({coats})");
            check(Count(cpx, cw, ch, Color.FromRgb(0x5F, 0xA8, 0xD8), (int)tile.Left, (int)tile.Right, (int)tile.Top, (int)tile.Bottom) > 300,
                "A picked cat tile turns blue with a tick");
            check(cview.TileAt(new Point(tile.Left + tile.Width / 2, tile.Top + tile.Height / 2)) == cat && cview.TileAt(new Point(3, 3)) == null, "A click is mapped to its tile (the frame is no tile)");
        }

        // The HUD: the low battery strip and the frozen cursor cover over the slots.
        var hsim = new BattleSim(settings, 0, 1200, stage: new StageSpec { Id = "hud_malware", NewAppsFrom = 1, MalwarePlan = new[]
            { new MalwareCue { At = 0, Kind = "battery" }, new MalwareCue { At = 0.1, Kind = "cursor" } } }, seed: 3);
        for (int i = 0; i < 300 && !(hsim.BatteryLow && hsim.CursorFrozen); i++) hsim.Step(1.0 / 60, 600, false);
        var hud = new BattleHud("Test", new PetRenderer(new PetModel()), () => "vi");
        hud.Update(hsim, false, false, false);
        hud.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        hud.Arrange(new Rect(hud.DesiredSize));
        hud.UpdateLayout();
        int hw = (int)Math.Ceiling(hud.ActualWidth), hh = (int)Math.Ceiling(hud.ActualHeight);
        Render(hud, hw, hh, "hud-malware");
        check(hsim.BatteryLow && hsim.CursorFrozen && hud.BatteryStrip.Visibility == Visibility.Visible && hud.CursorCover.Visibility == Visibility.Visible,
            "Low battery and a frozen cursor show over the skill slots");
        var slot = hud.Slot(BattleSkill.Heal)!;
        var slotRect = slot.TransformToAncestor(hud).TransformBounds(new Rect(slot.RenderSize));
        var coverRect = hud.CursorCover.TransformToAncestor(hud).TransformBounds(new Rect(hud.CursorCover.RenderSize));
        check(coverRect.Contains(new Point(slotRect.Left + slotRect.Width / 2, slotRect.Top + slotRect.Height / 2)), "The frozen cursor's cover lies over the slots");
        hsim.PlugIn(); hsim.ResumeCursor();
        hud.Update(hsim, false, false, false);
        hud.UpdateLayout();
        check(hud.BatteryStrip.Visibility != Visibility.Visible && hud.CursorCover.Visibility != Visibility.Visible, "Plugged in and going again, both are gone");
    }

    // Win32: whether window `a` is above window `b` in the z-order.
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    public static bool IsAbove(IntPtr a, IntPtr b)
    {
        const uint Next = 2;
        for (var w = GetWindow(a, Next); w != IntPtr.Zero; w = GetWindow(w, Next)) if (w == b) return true;
        return false;
    }
}
