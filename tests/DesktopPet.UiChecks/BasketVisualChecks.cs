using System.Windows;
using System.Windows.Media;
using DesktopPet;

// 1.5 M20, drawn for real with the shipped slime: the fish basket (wicker) in the middle of the strip with
// its fish peeking over the rim, a slime running off with a fish held over its head, and a fish it dropped
// lying on the floor, easy to click (FishAt finds it through its halo, not far off); an empty basket shows
// no fish. PET_RENDER_REVIEW=<folder> saves the strip (basket.png) on dark and light backgrounds.
internal static class BasketVisualChecks
{
    const double Width = 600;

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var stage = new StageSpec
        {
            Id = "basket", NameVi = "Giỏ", NameEn = "Basket", Basket = new BasketSpec { Fish = 4, Every = 1, Back = 30 },
            Waves = new[] { new WaveSpec { Groups = new[] { new WaveGroup { Enemy = "slime", Count = 2 } } } }
        };
        var sim = new BattleSim(data, 0, Width, new Random(1), stage: stage);
        for (int i = 0; i < 60 * 30 && sim.Slimes.Count(s => s.CarriesFish) < 2 && !sim.Over; i++) { sim.Step(1.0 / 60, 40, false); sim.ClearEvents(); }
        var carriers = sim.Slimes.Where(s => s.CarriesFish).ToList();
        check(carriers.Count == 2 && sim.FishInBasket == 2, "Two slimes took a fish each (2 left in the basket)");
        if (carriers.Count < 2) return;
        sim.Damage(carriers[0], carriers[0].Hp, DamageSource.Bomb);
        sim.Step(1.0 / 60, 40, false);
        carriers[1].X = 470;
        var stray = sim.StrayFish.Single();
        stray.X = 160;
        for (int i = sim.Slimes.Count - 1; i >= 0; i--) if (sim.Slimes[i].State == SlimeState.Defeated) sim.Slimes[i].StateTime = 99;
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight, floor = h - 2;
        var view = new BattleFieldView(sim) { OriginX = 0 };
        var px = SkyVisualChecks.Render(view, w, h, "basket");

        var wicker = Color.FromRgb(0xD9, 0xA4, 0x62);
        var fish = Color.FromRgb(0xFF, 0x9E, 0x7A);
        int top = (int)(floor - carriers[1].Height);
        check(SkyVisualChecks.Count(px, w, h, wicker, 270, 330, floor - 26, floor) > 300, "The basket stands in the middle of the strip (wicker)");
        check(SkyVisualChecks.Count(px, w, h, fish, 280, 320, floor - 45, floor - 22) > 20, "Its fish peek over the rim");
        check(SkyVisualChecks.Count(px, w, h, fish, 450, 490, top - 22, top + 2) > 30, "A carrier holds its fish over its head");
        check(SkyVisualChecks.Count(px, w, h, fish, 140, 180, floor - 20, floor) > 30, "A dropped fish lies on the floor");

        view.Measure(new Size(w, h)); view.Arrange(new Rect(0, 0, w, h));
        check(view.FishAt(new Point(160, floor - 10)) == stray.Id && view.FishAt(new Point(160 + BattleSim.FishHalfWidth + 2, floor - 4)) == stray.Id,
            "A click on a dropped fish or just beside it finds it");
        check(view.FishAt(new Point(220, floor - 10)) == null && view.FishAt(new Point(160, floor - 60)) == null, "A click away from it finds none");
        check(VisualTreeHelper.HitTest(view, new Point(160 + BattleSim.FishHalfWidth + 2, floor - 12)) != null && VisualTreeHelper.HitTest(view, new Point(220, floor - 12)) == null,
            "A dropped fish's halo takes the mouse; the strip beside it stays click-through");

        // Every fish gone from the basket: none peeks over its rim.
        var empty = new BattleSim(data, 0, Width, new Random(1), stage: new StageSpec { Id = "e", NameVi = "E", NameEn = "E", Basket = new BasketSpec { Fish = 1, Every = 1 }, Waves = stage.Waves });
        for (int i = 0; i < 60 * 30 && empty.FishInBasket > 0 && !empty.Over; i++) { empty.Step(1.0 / 60, 40, false); empty.ClearEvents(); }
        foreach (var s in empty.Slimes) s.X = 520;
        var epx = SkyVisualChecks.Render(new BattleFieldView(empty) { OriginX = 0 }, w, h);
        check(empty.FishInBasket == 0 && SkyVisualChecks.Count(epx, w, h, fish, 280, 320, floor - 45, floor - 22) == 0
            && SkyVisualChecks.Count(epx, w, h, wicker, 270, 330, floor - 26, floor) > 300, "An empty basket shows no fish");
    }
}
