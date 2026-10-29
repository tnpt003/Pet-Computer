using System.Windows;
using System.Windows.Media;
using DesktopPet;

// 1.5 M19, drawn for real with the shipped drops: a fish (salmon), a coin (gold) and a potion (pink)
// lying on the floor of the strip, each easy to click (DropAt finds it through its halo, not far off);
// one near the end of its life drawn faded. PET_RENDER_REVIEW=<folder> saves the strip (drops.png) on
// dark and light backgrounds.
internal static class DropVisualChecks
{
    const double Width = 600;

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var sim = new BattleSim(data, 0, Width, new Random(1), loot: BattleLoot.Coins);
        sim.AddDrop(BattleDropKind.Fish, 100, 0);
        sim.AddDrop(BattleDropKind.Coin, 220, 1);
        sim.AddDrop(BattleDropKind.Potion, 340, 0);
        sim.AddDrop(BattleDropKind.Coin, 460, 1);
        foreach (var d in sim.Drops) d.Age = 1; // Past the pop, lying on the floor.
        sim.Drops[3].Age = sim.Drops[3].Life - 0.9; // Fading.
        int w = (int)Width, h = (int)BattleFieldView.FieldHeight, floor = h - 2;
        var view = new BattleFieldView(sim) { OriginX = 0 };
        var px = SkyVisualChecks.Render(view, w, h, "drops");

        var fish = Color.FromRgb(0xFF, 0x9E, 0x7A);
        var coin = Color.FromRgb(0xFF, 0xC8, 0x2E);
        var potion = Color.FromRgb(0xF0, 0x6E, 0xB4);
        check(SkyVisualChecks.Count(px, w, h, fish, 80, 120, floor - 30, floor) > 30, "A fish drop lies on the floor (salmon)");
        check(SkyVisualChecks.Count(px, w, h, coin, 200, 240, floor - 30, floor) > 20, "A coin drop lies on the floor (gold)");
        check(SkyVisualChecks.Count(px, w, h, potion, 320, 360, floor - 30, floor) > 30, "A potion drop lies on the floor (pink)");
        check(SkyVisualChecks.Count(px, w, h, coin, 440, 480, floor - 30, floor) == 0 && SkyVisualChecks.Count(px, w, h, coin, 440, 480, floor - 30, floor, alpha: 20) > 0,
            "A drop near the end of its life is drawn faded");

        view.Measure(new Size(w, h)); view.Arrange(new Rect(0, 0, w, h));
        check(view.DropAt(new Point(100, floor - 10)) == sim.Drops[0].Id && view.DropAt(new Point(100 + BattleSim.DropHalfWidth + 2, floor - 4)) == sim.Drops[0].Id,
            "A click on a drop or just beside it finds it");
        check(view.DropAt(new Point(160, floor - 10)) == null && view.DropAt(new Point(100, floor - 60)) == null, "A click away from every drop finds none");
        check(VisualTreeHelper.HitTest(view, new Point(100 + BattleSim.DropHalfWidth + 2, floor - 12)) != null && VisualTreeHelper.HitTest(view, new Point(160, floor - 12)) == null,
            "A drop's halo takes the mouse; the strip beside it stays click-through");
    }
}
