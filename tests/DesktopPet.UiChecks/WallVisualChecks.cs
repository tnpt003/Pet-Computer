using System.Windows;
using System.Windows.Media;
using DesktopPet;

// 1.5 M15, drawn for real with the shipped kinds: an ice slime in its shell (a pale blue rim round its
// body), one whose shell broke (only shards on its head); a wall climber up the left edge of the strip,
// off the floor, a red ring filling on the floor where its spike will land, and a spike on its way down;
// one knocked down lies on the floor. PET_RENDER_REVIEW=<folder> saves the strip (enemy-wall.png) on dark
// and light backgrounds.
internal static class WallVisualChecks
{
    const double Width = 1200;

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
        var rim = Color.FromRgb(0x6F, 0xB8, 0xD8);
        var olive = Color.FromRgb(0xB5, 0xB8, 0x4A);
        var spike = Color.FromRgb(0x7A, 0x6A, 0x3A);
        var red = Color.FromRgb(0xE5, 0x3E, 0x3E);

        var iced = Add("icy", 260);
        iced.Iced = true;
        var broken = Add("icy", 420);
        broken.Iced = false; broken.IceClock = 5;
        var c = data.Enemy("clinger").Cling!;
        var up = Add("clinger", 0);
        up.Cling = ClingPhase.Clinging; up.WallSide = -1; up.X = sim.Left + up.Width / 2; up.Altitude = c.Height; up.Facing = 1;
        up.AimLeft = c.Aim * 0.4; up.SpitAt = 700;
        var down = Add("clinger", 1000);
        down.Set(SlimeState.Stunned); down.StunRemaining = 2;
        var goos = (List<BattleGoo>)typeof(BattleSim).GetField("goos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(sim)!;
        goos.Add(new BattleGoo { FromX = up.X, FromHeight = c.Height + 20, X = 900, Flight = 1, Remaining = 0.5, Radius = c.Radius, Spike = true, Tint = "#B5B84A" });
        var px = SkyVisualChecks.Render(view, w, h, "enemy-wall");

        int shell = SkyVisualChecks.Count(px, w, h, rim, 220, 300, floor - 60, floor + 1, 30, 150), shards = SkyVisualChecks.Count(px, w, h, rim, 380, 460, floor - 60, floor + 1, 30, 150);
        check(shell > 60 && shards > 0 && shards < shell, $"An ice slime shows its shell; broken, only shards on its head ({shell} vs {shards} rim pixels)");
        int onWall = SkyVisualChecks.Count(px, w, h, olive, 0, 70, floor - (int)c.Height - 50, floor - (int)c.Height + 4);
        check(onWall > 150 && SkyVisualChecks.Count(px, w, h, olive, 0, 70, floor - 12, floor + 1) == 0, $"A wall climber clings up the left edge, off the floor ({onWall} body pixels)");
        check(SkyVisualChecks.Count(px, w, h, red, 640, 760, floor - 14, floor + 1, 30, 120) > 40, "A red ring fills on the floor where its spike will land");
        check(SkyVisualChecks.Count(px, w, h, spike, 440, 490, 0, floor) > 20, "A spike shows on its way down");
        check(SkyVisualChecks.Count(px, w, h, olive, 970, 1030, floor - 40, floor) > 150, "Knocked down it lies on the floor");
    }
}
