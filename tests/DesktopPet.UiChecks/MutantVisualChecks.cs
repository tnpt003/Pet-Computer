using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet;

internal static class MutantVisualChecks
{
    private sealed class Air(BattleSim sim) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc) => BattleFieldView.DrawMutantAir(dc, sim,
            (x, h) => new Point(x, 698 - h), new Point(550, 340), sim.Time);
    }
    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var sim = new BattleSim(data, 0, 1200, seed: 26);
        sim.SetSky(650);
        var boss = sim.AddSlime(650, data.Enemy("mutant_king"));
        boss.Set(SlimeState.Moving);
        sim.Step(0.016, 500, false);
        var m = boss.Mutant!;
        Grid View()
        {
            var grid = new Grid();
            grid.Children.Add(new BattleFieldView(sim) { Height = BattleFieldView.FieldHeight, VerticalAlignment = VerticalAlignment.Bottom });
            grid.Children.Add(new Air(sim));
            return grid;
        }
        var baseImage = SkyVisualChecks.Render(View(), 1200, 700, "mutant-phase1");
        sim.Damage(boss, 10000, DamageSource.Weapon);
        for (int i = 0; i < 100; i++) sim.Step(0.016, 550, true, true, new CatPose(550, 600, 0, 0, true, false));
        var bumps = SkyVisualChecks.Render(View(), 1200, 700, "mutant-six-bumps");
        check(!baseImage.SequenceEqual(bumps), "Mutant bump puzzle renders numbered floor and rotated wall targets with active guidance");
        m.Bumps[0].SolvedAt = sim.Time; m.NextBump = 1;
        var solved = SkyVisualChecks.Render(View(), 1200, 700, "mutant-bump-solved");
        check(!bumps.SequenceEqual(solved), "Solved mutant bump and next active target have distinct visuals");
        m.Bumps.Clear(); m.Mode = MutantMode.Transition; m.Clock = 1.5; m.Phase = 2;
        var transition = SkyVisualChecks.Render(View(), 1200, 700, "mutant-terrain-transition");
        check(!baseImage.SequenceEqual(transition), "Mutant transition visibly pumps permanent goo onto the floor");
        m.Mode = MutantMode.Combat; m.Phase = 3; m.Age = 8;
        for (int i = 0; i < 4; i++) m.Bags.Add(new MutantBag { Lane = i, X = 150 + i * 280, H = 270 + i % 2 * 160, Clock = i == 0 ? 8 : i == 1 ? 6.5 : 1 });
        var terrain = SkyVisualChecks.Render(View(), 1200, 700, "mutant-phase3-terrain");
        check(!transition.SequenceEqual(terrain), "Four airborne goo bags show separate rest, warning and spray states");
        m.Final = true; m.TerrainCleared = true; m.FadeAt = sim.Time; m.Mode = MutantMode.Roar;
        var clearing = SkyVisualChecks.Render(View(), 1200, 700, "mutant-final-clearing");
        check(!terrain.SequenceEqual(clearing), "Final roar pulls the old battlefield hazards back into the boss");
        m.Bags.Clear(); m.Mode = MutantMode.AllIn; m.CoreHits = 2;
        for (int i = 0; i < 6; i++) m.Zones.Add(new MutantZone { Shape = MutantShape.Column, X = i % 2 == 0 ? 260 + i * 100 : 600, W = i % 2 == 0 ? 65 : 1200,
            H = i % 2 == 0 ? 0 : 120 + i * 55, Height = i % 2 == 0 ? 650 : 50, Warn = 1, Age = i == 5 ? 0.5 : 2, Life = 30 });
        var columns = SkyVisualChecks.Render(View(), 1200, 700, "mutant-final-six-columns");
        check(!clearing.SequenceEqual(columns), "Final attack displays six persistent horizontal and vertical goo strips");
        m.Zones.Clear(); m.Mode = MutantMode.Core;
        var exposed = SkyVisualChecks.Render(View(), 1200, 700, "mutant-final-core");
        check(!columns.SequenceEqual(exposed), "After the all-in attack all strips disappear and the cracked core is exposed");
    }
}
