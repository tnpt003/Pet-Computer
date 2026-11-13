using System.Windows.Media;
using DesktopPet;

// 1.5 M20, defence since 2026-11-13, drawn for real with the shipped slime: the fish basket (wicker) in the
// middle of the strip with its fish peeking over the rim and its HP bar over them (green while full, warm
// red once low, cracks on the wicker and fewer fish); a slime the cat hit wears a red "!" over its head.
// PET_RENDER_REVIEW=<folder> saves the strip (basket.png, basket-low.png) on dark and light backgrounds.
internal static class BasketVisualChecks
{
    const double Width = 600;

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 2 } } } };
        StageSpec Stage(double hp) => new() { Id = "basket", NameVi = "Giỏ", NameEn = "Basket", Basket = new BasketSpec { Hp = hp }, Waves = waves };
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight, floor = h - 2;
        var wicker = Color.FromRgb(0xD9, 0xA4, 0x62);
        var fish = Color.FromRgb(0xFF, 0x9E, 0x7A);
        var green = Color.FromRgb(0x6F, 0xE0, 0x6A);
        var low = Color.FromRgb(0xFF, 0x8A, 0x5C);
        var angry = Color.FromRgb(0xFF, 0x6B, 0x4A);

        // Full: the slimes kept off it, one of them hit by the cat.
        var sim = new BattleSim(data, 0, Width, new Random(1), stage: Stage(1000));
        for (int i = 0; i < 60 * 20 && sim.Slimes.Count(s => s.Targetable) < 2; i++) { sim.Step(1.0 / 60, 40, false); sim.ClearEvents(); }
        var slimes = sim.Slimes.Where(s => s.Targetable).ToList();
        check(slimes.Count == 2, "Two slimes are on the field");
        if (slimes.Count < 2) return;
        slimes[0].X = 90; slimes[1].X = 510;
        sim.Damage(slimes[0], 1, DamageSource.Weapon);
        var px = SkyVisualChecks.Render(new BattleFieldView(sim) { OriginX = 0 }, w, h, "basket");
        check(SkyVisualChecks.Count(px, w, h, wicker, 270, 330, floor - 26, floor) > 300, "The basket stands in the middle of the strip (wicker)");
        check(SkyVisualChecks.Count(px, w, h, fish, 275, 325, floor - 45, floor - 22) > 30, "Its fish peek over the rim");
        check(SkyVisualChecks.Count(px, w, h, green, 274, 326, floor - 50, floor - 42) > 120, "A full green HP bar over the basket");
        int top = (int)(floor - slimes[0].Height);
        check(SkyVisualChecks.Count(px, w, h, angry, 80, 125, top - 18, top + 4) > 40 && SkyVisualChecks.Count(px, w, h, angry, 490, 535, top - 18, top + 4) == 0,
            "A red ! over the slime the cat hit, none over the other");

        // Low: hit until under 30 %.
        var hurt = new BattleSim(data, 0, Width, new Random(1), stage: Stage(60));
        for (int i = 0; i < 60 * 40 && hurt.BasketPercent >= 30 && !hurt.Over; i++) { hurt.Step(1.0 / 60, 40, false); hurt.ClearEvents(); }
        check(!hurt.Over && hurt.BasketPercent is > 0 and < 30, $"The basket is hit down to {hurt.BasketPercent}%");
        foreach (var s in hurt.Slimes) s.X = s.X < 300 ? 90 : 510;
        var lpx = SkyVisualChecks.Render(new BattleFieldView(hurt) { OriginX = 0 }, w, h, "basket-low");
        check(SkyVisualChecks.Count(lpx, w, h, low, 274, 326, floor - 50, floor - 42) > 10 && SkyVisualChecks.Count(lpx, w, h, green, 274, 326, floor - 50, floor - 42) == 0,
            "Low, its bar is short and warm red");
        check(SkyVisualChecks.Count(lpx, w, h, fish, 275, 325, floor - 45, floor - 22) < SkyVisualChecks.Count(px, w, h, fish, 275, 325, floor - 45, floor - 22),
            "Fewer fish peek over a weak basket's rim");
    }
}
