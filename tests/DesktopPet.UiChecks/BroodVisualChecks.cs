using System.Windows.Media;
using DesktopPet;

// 1.5 M18, drawn for real with the shipped bosses: the Slime Mother with a pink bow by her crown; a second
// mother with a giant from her clutch feeding her (a dashed pink line between them, a heart over its head);
// the Mutant King with goo dripping down its sides and a paler, crooked crown. A second strip: its goo rain
// falling onto the rings around the cat, far from the king.
// PET_RENDER_REVIEW=<folder> saves the strips (enemy-brood.png, enemy-rain.png) on dark and light backgrounds.
internal static class BroodVisualChecks
{
    const double Width = 1200;

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var bow = Color.FromRgb(0xE8, 0x5A, 0x9A);
        var drip = Color.FromRgb(0x5E, 0x9E, 0x3C);
        var paleGold = Color.FromRgb(0xC9, 0xC2, 0x4A);
        var green = Color.FromRgb(0x7F, 0xBF, 0x5A);
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight, floor = h - 2;

        var sim = new BattleSim(data, 0, Width, new Random(1));
        Slime Add(BattleSim on, string kind, double x)
        {
            var s = on.AddSlime(x, data.Enemy(kind));
            s.Set(SlimeState.Moving); s.StateTime = 0.4; s.Facing = -1;
            return s;
        }
        Add(sim, "mother", 130);
        var fed = Add(sim, "mother", 640);
        var giant = Add(sim, "giant", 640 - (fed.Width + data.SlimeWidth * data.Enemy("giant").Scale) / 2 * 0.72);
        giant.SummonedBy = fed.Id;
        giant.Nursing = giant.Feeding = true;
        giant.Facing = 1;
        Add(sim, "mutant_king", 1020);
        var px = SkyVisualChecks.Render(new BattleFieldView(sim) { OriginX = 0 }, w, h, "enemy-brood");

        int bows = SkyVisualChecks.Count(px, w, h, bow, 80, 180, 0, floor - 40);
        check(bows > 20, $"The Slime Mother wears a pink bow by her crown ({bows} bow pixels)");
        int feeding = SkyVisualChecks.Count(px, w, h, bow, (int)giant.X - 30, (int)fed.X - 20, 0, floor);
        check(feeding > 30, $"Her giant feeding her: a pink line and a heart over its head ({feeding} pixels)");
        int drips = SkyVisualChecks.Count(px, w, h, drip, 940, 1100, 0, floor);
        int crown = SkyVisualChecks.Count(px, w, h, paleGold, 960, 1080, 0, floor - 40);
        check(drips > 20 && crown > 20, $"The Mutant King drips goo and wears a pale crooked crown ({drips} drip, {crown} crown pixels)");

        var rains = new BattleSim(data, 0, Width, new Random(1));
        var king = Add(rains, "mutant_king", 1050);
        king.RainClock = 0.01;
        for (int i = 0; i < 30; i++) rains.Step(1.0 / 60, 300, false);
        var lane = SkyVisualChecks.Render(new BattleFieldView(rains) { OriginX = 0 }, w, h, "enemy-rain");
        int drops = SkyVisualChecks.Count(lane, w, h, green, 150, 450, 0, floor - 12);
        check(rains.Goos.Count(g => g.Rain) == data.Enemy("mutant_king").Rain!.Drops && drops > 30, $"Its goo rain falls around the cat ({drops} drop pixels)");
    }
}
