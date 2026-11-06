using DesktopPet;

// A player that reads the public warning/target positions, carries at bounded speed and uses normal clocks.
internal sealed class MotherPuzzlePlayer
{
    double height = BattleSim.CatStandHeight;
    double stroke, ring = double.NaN;
    public bool Step(BattleSim sim, ref double cat, double dt, bool careful)
    {
        var boss = sim.Slimes.FirstOrDefault(s => s.Targetable && s.Mother != null);
        if (boss?.Mother is not { } m) return false;
        double catX = cat, x = cat, h = BattleSim.CatStandHeight;
        bool hold = true;
        sim.SetMotherPointer(null);
        if (sim.MotherWrapLeft > 0) sim.ClickMotherWrap();
        switch (m.Mode)
        {
            case MotherMode.HangingEggs:
                var egg = m.Tokens.Where(t => !t.Done).OrderBy(t => Math.Abs(t.X - catX)).FirstOrDefault();
                if (egg == null) return false;
                x = egg.X; h = egg.H; break;
            case MotherMode.Cradle:
                var child = m.Tokens.FirstOrDefault(t => t.Carried) ?? m.Tokens.Where(t => !t.Done).OrderBy(t => Math.Abs(t.X - catX)).FirstOrDefault();
                if (child == null) return false;
                x = child.Carried ? m.RingX : child.X; h = child.Carried ? Math.Min(sim.SkyHeight - 90, 300) : 56; break;
            case MotherMode.Lullaby:
                var note = m.Tokens.Where(t => !t.Done && !t.Bad).OrderBy(t => Math.Abs(t.X - catX) + Math.Abs(t.H - height)).FirstOrDefault();
                if (note == null) return false;
                x = note.X; h = note.H; break;
            case MotherMode.Stroke:
                x = m.RingX; hold = false;
                if (ring != m.RingX) { ring = m.RingX; stroke = 0; }
                stroke = Math.Min(boss.Width * .7, stroke + 180 * dt);
                if (Math.Abs(cat - m.RingX) < 65) sim.SetMotherPointer(boss.X + m.StrokeDirection * (-boss.Width * .35 + stroke), boss.Height * .8);
                break;
            case MotherMode.LastEgg:
                x = boss.X; h = m.BellyOpen ? boss.Height * .4 : 300; break;
            case MotherMode.Sleeping: h = 300; break;
            default:
                if (!careful) return false;
                var danger = m.Zones.FirstOrDefault(z => z.Age < z.Warn + z.Life && z.Age > z.Warn * .5 && Math.Abs(catX - z.X) < z.Width / 2 + 60);
                if (danger == null) return false;
                if (danger.Height < 200) h = Math.Max(300, danger.H + danger.Height + 100);
                else
                {
                    double best = cat, distance = double.PositiveInfinity;
                    for (double p = sim.Left + 45; p < sim.Right - 45; p += 30)
                        if (m.Zones.All(z => z.Height < 200 || z.Age >= z.Warn + z.Life || Math.Abs(p - z.X) > z.Width / 2 + 40) && Math.Abs(p - cat) < distance)
                        { distance = Math.Abs(p - cat); best = p; }
                    x = best; h = 300;
                }
                break;
        }
        cat += Math.Clamp(x - cat, -700 * dt, 700 * dt);
        height += Math.Clamp(h - height, -600 * dt, 600 * dt);
        sim.Step(dt, cat, hold, hold, hold ? new CatPose(cat, height, 0, 0, true, false) : null);
        return true;
    }
}
