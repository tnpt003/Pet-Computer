using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet;

internal static class MotherVisualChecks
{
    private sealed class Air(BattleSim sim) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc) => BattleFieldView.DrawMotherAir(dc, sim,
            (x, h) => new Point(x, 698 - h), new Point(550, 340), sim.Time);
    }
    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings; var sim = new BattleSim(data, 0, 1200, seed: 31); sim.SetSky(650);
        var boss = sim.AddSlime(780, data.Enemy("mother")); boss.Set(SlimeState.Moving);
        sim.Step(.016, 100, false);
        Grid View()
        {
            var grid = new Grid(); grid.Children.Add(new BattleFieldView(sim) { Height = BattleFieldView.FieldHeight, VerticalAlignment = VerticalAlignment.Bottom });
            grid.Children.Add(new Air(sim)); return grid;
        }
        var first = SkyVisualChecks.Render(View(), 1200, 700, "mother-phase1-eggs-bow");
        check(SkyVisualChecks.Count(first, 1200, 700, Color.FromRgb(0xFF, 0xED, 0xD4), 740, 820, 630, 698) > 25,
            "Mother translucent belly contains cream eggs within four-lobed skirt");
        sim.Damage(boss, 100000, DamageSource.Weapon); var m = boss.Mother!;
        boss.HitFlash = 0;
        foreach (var effect in sim.Effects) effect.Age = effect.Life;
        var hanging = SkyVisualChecks.Render(View(), 1200, 700, "mother-hanging-eggs");
        check(!first.SequenceEqual(hanging), "Hanging-egg puzzle draws high/low eggs and visible pink suspension cords");
        m.Tokens[0].Falling = true; m.Tokens[0].H = 40; m.Tokens[0].Hits = 1;
        var falling = SkyVisualChecks.Render(View(), 1200, 700, "mother-fallen-egg-cracked");
        check(!falling.SequenceEqual(hanging), "Fallen cracked egg loses its suspension cord");
        m.Mode = MotherMode.Combat; m.Phase = 2; boss.Shielded = false; m.Tokens.Clear();
        var phase2 = SkyVisualChecks.Render(View(), 1200, 700, "mother-phase2-protect");
        m.Phase = 3; m.Soothe = 80;
        var phase3 = SkyVisualChecks.Render(View(), 1200, 700, "mother-phase3-calming");
        check(!phase2.SequenceEqual(phase3), "Mother phases change eyes, bow and visible egg count");
        foreach (MotherArt art in Enum.GetValues<MotherArt>())
        {
            m.Zones.Clear(); var z = new MotherZone { Art = art, X = 450, H = 120, Width = 170, Height = 130, Warn = 1.2, Life = 1, Age = .6 };
            m.Zones.Add(z);
            var warning = SkyVisualChecks.Render(View(), 1200, 700, "mother-" + art.ToString().ToLowerInvariant() + "-warning");
            z.Age = 1.4;
            var live = SkyVisualChecks.Render(View(), 1200, 700, "mother-" + art.ToString().ToLowerInvariant() + "-live");
            check(!warning.SequenceEqual(live), $"Mother {art} windup and impact remain visibly distinct");
        }
        m.Zones.Clear(); m.Mode = MotherMode.Cradle; m.RingX = 500;
        m.Tokens.Add(new MotherToken { X = 400, H = 25 });
        SkyVisualChecks.Render(View(), 1200, 700, "mother-return-child-cradle");
        m.Mode = MotherMode.Lullaby; m.Tokens.Add(new MotherToken { X = 600, H = 260, Bad = true });
        SkyVisualChecks.Render(View(), 1200, 700, "mother-heart-notes");
        m.Mode = MotherMode.Stroke; m.Tokens.Clear();
        SkyVisualChecks.Render(View(), 1200, 700, "mother-stroke-ring-arrow");
        m.Mode = MotherMode.LastEgg; m.Zones.Add(new MotherZone { Art = MotherArt.Heartbeat, X = 600, Width = 1200, Height = 65, Warn = 1, Life = .35, Age = 1.6 });
        SkyVisualChecks.Render(View(), 1200, 700, "mother-last-egg-open");
        m.Mode = MotherMode.Sleeping; m.Deadline = sim.Time + 2;
        var asleep = SkyVisualChecks.Render(View(), 1200, 700, "mother-sleeping");
        check(!asleep.SequenceEqual(phase3), "Mother sleeps with closed eyes and dissolving bubbles without touching cat sleep");
        m.Tokens.Clear(); m.Mode = MotherMode.Combat; boss.Shielded = false; m.Thresholds = 3; m.Soothe = 0;
        for (int phase = 1; phase <= 3; phase++)
        {
            m.Phase = phase; m.Bag.Clear(); m.NextWrap = 9999;
            for (int turn = 0; turn < 3; turn++)
            {
                m.Zones.Clear(); m.Clock = 0; m.TravelTime = 0;
                sim.Step(.016, 350, true, true, new CatPose(350, 350, 0, 0, true, false));
                SkyVisualChecks.Render(View(), 1200, 700, $"mother-phase{phase}-move{m.Move}-windup");
            }
        }
    }
}
