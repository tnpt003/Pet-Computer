using System.Windows.Media;
using DesktopPet;

// 1.5 M16, drawn for real with the shipped kinds: a hedgehog with its quills up (long, out of its back),
// one with them flat and one soaked by the spray; a timer with its clock over its head (a green ring, pale
// blue while it is dazed); a thief carrying a skill in a sack on its back, and one with empty paws.
// PET_RENDER_REVIEW=<folder> saves the strip (enemy-quills.png) on dark and light backgrounds.
internal static class QuillsVisualChecks
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
        var quill = Color.FromRgb(0x6E, 0x4B, 0x34);
        var ring = Color.FromRgb(0x3E, 0x9C, 0x74);
        var slow = Color.FromRgb(0x7F, 0xB8, 0xE8);
        var sack = Color.FromRgb(0xC9, 0x9A, 0x5E);
        var star = Color.FromRgb(0x5B, 0x9B, 0xE8);

        var up = Add("hedgehog", 120);
        up.Quill = QuillPhase.Up;
        var flat = Add("hedgehog", 260);
        flat.Quill = QuillPhase.Flat;
        var wet = Add("hedgehog", 400);
        wet.Quill = QuillPhase.Flat; wet.WetRemaining = 3;
        var timer = Add("timer", 560);
        timer.ClockTotal = 20; timer.ClockLeft = 12;
        var dazed = Add("timer", 700);
        dazed.ClockTotal = 20; dazed.ClockLeft = 12;
        dazed.Set(SlimeState.Stunned); dazed.StunRemaining = 2;
        var thief = Add("thief", 860);
        thief.Loot = StolenKind.Skill; thief.LootSkill = BattleSkill.Push;
        Add("thief", 1020);
        var px = SkyVisualChecks.Render(view, w, h, "enemy-quills");

        int tall = SkyVisualChecks.Count(px, w, h, quill, 60, 180, floor - 80, floor - 20), low = SkyVisualChecks.Count(px, w, h, quill, 200, 320, floor - 80, floor - 20);
        check(tall > 40 && low > 0 && tall > low * 1.3, $"A hedgehog's quills stand out long when up, short when flat ({tall} vs {low} quill pixels)");
        check(SkyVisualChecks.Count(px, w, h, quill, 340, 460, floor - 60, floor) > 0, "A soaked hedgehog still shows its drooping quills");
        check(SkyVisualChecks.Count(px, w, h, ring, 530, 590, floor - 90, floor - 20) > 10, "A timer shows its clock over its head, a green ring");
        check(SkyVisualChecks.Count(px, w, h, slow, 670, 730, floor - 90, floor - 20) > 10 && SkyVisualChecks.Count(px, w, h, ring, 670, 730, floor - 90, floor - 20) == 0,
            "Dazed, its clock's ring turns pale blue (the clock crawls)");
        int loot = SkyVisualChecks.Count(px, w, h, sack, 830, 920, floor - 70, floor);
        check(loot > 40 && SkyVisualChecks.Count(px, w, h, star, 830, 920, floor - 70, floor) > 3, $"A thief carrying a skill has a sack with a blue star on its back ({loot} sack pixels)");
        check(SkyVisualChecks.Count(px, w, h, sack, 980, 1080, floor - 70, floor) == 0, "A thief with nothing carries no sack");
    }
}
