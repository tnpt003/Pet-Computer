using DesktopPet;

// A test player that uses the same floor/held weapon clocks as a person, never direct prop damage.
internal static class ZipPuzzlePlayer
{
    public static bool Step(BattleSim sim, ref double cat, double dt, bool dodge = false)
    {
        var prop = sim.Slimes.FirstOrDefault(s => s.Targetable && s.ZipProp != ZipProp.None && sim.ZipPropActive(s));
        if (prop == null)
        {
            if (dodge && (sim.Slimes.Any(s => s.Type.Zip != null && s.ZipRepulseWarn > 0)
                || sim.Slimes.Any(s => s.PacketFuse && s.PacketRevealed && s.PacketLeft is > 0 and < 0.6 && Math.Abs(s.X - sim.CatXForZip) < 180)))
            {
                sim.Step(dt, cat, true, true, new CatPose(cat, 260, 0, 0, true, false));
                return true;
            }
            return false;
        }
        double catX = cat;
        if (sim.Boss is { BombClock: > 0 and < 0.6 })
        {
            sim.Step(dt, cat, true, true, new CatPose(cat, 240, 0, 0, true, false));
            return true;
        }
        if (dodge && sim.ZipBlows.Any(b => !b.Done && b.Left < 0.45 && Math.Abs(b.X - catX) <= b.Radius + 30))
        {
            sim.Step(dt, cat, true, true, new CatPose(cat, 240, 0, 0, true, false));
            return true;
        }
        if (prop.Airborne)
        {
            cat = prop.X;
            sim.Step(dt, cat, true, true, new CatPose(cat, prop.Altitude + prop.Height / 2, 0, 0, true, false));
            return true;
        }
        if (prop.ZipProp == ZipProp.File)
        {
            // Position the cat on the side that pushes toward the matching folder.
            double desired = prop.X + (sim.ZipFileGoal(prop) >= prop.X ? -1 : 1) * 45;
            if (Math.Abs(desired - cat) > 3)
            {
                cat += Math.Clamp(desired - cat, -700 * dt, 700 * dt);
                sim.Step(dt, cat, true, true, new CatPose(cat, 100, 0, 0, true, false));
                return true;
            }
        }
        sim.Select(prop.Id);
        var order = sim.Step(dt, cat, false);
        if (order.Command == CatCommand.Shove) cat = order.X;
        else if (order.Command == CatCommand.Move && prop.ZipProp != ZipProp.File)
        {
            double run = 72 * sim.Settings.CatRunScale * sim.MoveFactor * sim.LagFactor;
            cat += Math.Clamp(order.X - cat, -run * dt, run * dt);
        }
        return true;
    }
}
