using DesktopPet;

// The battle hub's Bag reads the cat's numbers from a BattleSim made with the build and relics
// (SwingDamage, SwingsPerSecond, Knock, HurtFactor, CatMaxHp, MoveFactor): the battle's own rules,
// with the built-in data.
internal static class InventoryStatTests
{
    private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

    public static void Run(Action<bool, string> check)
    {
        var data = BattleData.Settings;
        var sword = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Sword, seed: 0);
        var w = sword.Weapon;
        check(Near(sword.SwingDamage, w.Damage) && Near(sword.SwingsPerSecond, 1 / Math.Max(w.AttackCooldown, w.SwingDuration)) && Near(sword.Knock, w.Knock)
            && Near(sword.HurtFactor, 1) && Near(sword.MoveFactor, 1) && Near(sword.CatMaxHp, data.CatMaxHp),
            "Without a build the Bag's numbers are the weapon's own");
        var hammer = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Hammer, seed: 0);
        check(Near(hammer.SwingDamage, hammer.Weapon.Damage) && Near(hammer.Knock, hammer.Weapon.Knock) && hammer.SwingsPerSecond < sword.SwingsPerSecond,
            "The hammer's numbers are its own (harder, slower)");

        var build = new Dictionary<string, int> { ["sharp"] = 2, ["fast_paws"] = 1, ["heavy_push"] = 1, ["thick_fur"] = 1, ["big_heart"] = 1 };
        var built = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Sword, build: build, seed: 0);
        check(Near(built.SwingDamage, w.Damage * 1.4) && Near(built.SwingsPerSecond, 1 / Math.Max(w.AttackCooldown * 0.88, w.SwingDuration))
            && Near(built.Knock, w.Knock + 80) && Near(built.HurtFactor, 0.85) && Near(built.CatMaxHp, data.CatMaxHp + 20),
            $"A build changes the numbers as its cards say (damage {built.SwingDamage}, swings {built.SwingsPerSecond:0.###}/s, knock {built.Knock}, taken {built.HurtFactor})");

        var relics = new BattleSim(data, 0, 1000, weapon: BattleWeapon.Sword, build: build, seed: 0, relics: new[] { "reckless_charm", "light_feather" });
        check(Near(relics.HurtFactor, 0.85 * 1.1) && Near(relics.MoveFactor, 1.15) && Near(relics.CatMaxHp, data.CatMaxHp + 20 - 15)
            && Near(relics.SwingsPerSecond, 1 / Math.Max(w.AttackCooldown * (1 - 0.12 - 0.1), w.SwingDuration)) && Near(relics.SwingDamage, built.SwingDamage),
            "Relics add their prices and gains (Reckless: more damage taken always, its bonus only in a fight; Light feather: faster, less max HP)");
    }
}
