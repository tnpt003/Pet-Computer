using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// BattleSim: the battle's desktop (battle.json "apps", AppsSpec) in Adventure, Expedition and Infinity
// battles; free battles have none. Five made-up app icons stand on a grid of desktop cells above the
// strip and charge up (faster as enemies fall); a charged one is clicked and what it sends flies
// down to the field and acts when it lands. From a threat on, a slime may shoot a virus at an icon:
// the infected icon works against the cat until it is scrubbed (clicks) or that slime is defeated.
// Everything here waits while the cat is in the user's hands (like the waves), a helper cannot be
// used then (like the skills); scrubbing and closing an ad can. Where the cells are on the screen is
// the window's business: the sim only knows the grid (SetAppGrid).
public sealed partial class BattleSim
{
    private readonly List<DesktopApp> desktop = new();
    internal readonly List<BattlePopup> popups = new();
    internal readonly List<AppLaunch> launches = new();
    public IReadOnlyList<DesktopApp> Desktop => desktop;
    public IReadOnlyList<BattlePopup> Popups => popups;
    public IReadOnlyList<AppLaunch> Launches => launches;
    private int nextAppId = 1;
    // The apps this battle uses (null: none).
    public AppsSpec? AppRules => Stage != null ? Settings.Apps : null;
    // The desktop grid: columns across the floor's width, rows up from the strip; cells the HUD covers.
    public int GridCols { get; private set; } = 12;
    public int GridRows { get; private set; } = 3;
    private readonly HashSet<(int Col, int Row)> blockedCells = new();
    // Tab tornado: seconds left, and the time to its next pulse.
    public double TabsRemaining { get; internal set; }
    private double tabClock;
    // When this wave's virus is shot (NaN: none coming).
    private double infectTimer = double.NaN;
    // What the cat lost and when (battle time), for Undo.
    private readonly List<(double Time, double Hp)> hurts = new();
    public int PopupsClosed { get; private set; }
    // Where the cat stood on the floor at the last Step (the tabs circle it, the bad bells fall on it).
    public double CatX => lastCatX;
    // Flight times (s): what an app sends, a virus, an infected app's attack.
    public const double LaunchSeconds = 0.45, GooSeconds = 0.7, AttackSeconds = 0.6;
    // The ad's size on the strip (the view draws it, the clicks use it).
    public const double PopupWidth = 104, PopupHeight = 66;
    private static readonly BattleAppKind[] IconKinds = { BattleAppKind.Tabs, BattleAppKind.Ping, BattleAppKind.EndTask, BattleAppKind.Undo, BattleAppKind.Trash };

    public DesktopApp? FindApp(int id) { foreach (var a in desktop) if (a.Id == id) return a; return null; }
    public BattlePopup? FindPopup(int id) { foreach (var p in popups) if (p.Id == id) return p; return null; }
    // The floor X under an icon's column (the grid spans the floor's width).
    public double IconX(DesktopApp app) => Left + (app.Col + 0.5) * (Right - Left) / GridCols;
    // The slime holding an icon with its virus (or about to): it wears the virus mark.
    public bool IsInfector(int slimeId)
    {
        foreach (var a in desktop) if (a.InfectedBy == slimeId) return true;
        foreach (var l in launches) if (l.Kind == LaunchKind.Goo && l.SlimeId == slimeId) return true;
        return false;
    }

    // The window's grid for the battle's monitor. Icons outside it (or under the HUD) move to a free
    // cell at once (no hop); the first call places the icons.
    public void SetAppGrid(int cols, int rows, IEnumerable<(int Col, int Row)>? blocked = null)
    {
        GridCols = Math.Max(1, cols);
        GridRows = Math.Max(1, rows);
        blockedCells.Clear();
        if (blocked != null) foreach (var cell in blocked) blockedCells.Add(cell);
        EnsureDesktop();
        foreach (var app in desktop)
            if (!CellOpen(app.Col, app.Row, app) && FreeCell(app) is (int c, int r)) { app.Col = app.FromCol = c; app.Row = app.FromRow = r; }
    }

    private bool CellOpen(int col, int row, DesktopApp? self)
    {
        if (col < 0 || col >= GridCols || row < 0 || row >= GridRows || blockedCells.Contains((col, row))) return false;
        foreach (var a in desktop) if (a != self && a.Col == col && a.Row == row) return false;
        return true;
    }

    // A random open cell (null: the grid is full).
    private (int Col, int Row)? FreeCell(DesktopApp? self)
    {
        var open = new List<(int, int)>();
        for (int r = 0; r < GridRows; r++)
            for (int c = 0; c < GridCols; c++)
                if (CellOpen(c, r, self)) open.Add((c, r));
        return open.Count == 0 ? null : open[appRandom.Next(open.Count)];
    }

    // The five icons pop up on random cells, partly charged (each its own start, or the rules').
    private void EnsureDesktop()
    {
        if (desktop.Count > 0 || AppRules is not AppsSpec rules) return;
        foreach (var kind in IconKinds)
        {
            var (col, row) = FreeCell(null) ?? (0, 0);
            desktop.Add(new DesktopApp { Id = nextAppId++, Kind = kind, Col = col, Row = row, FromCol = col, FromRow = row, Charge = Math.Clamp(rules.App(kind).Start ?? rules.StartCharge, 0, 1), BornAt = Time });
        }
    }

    private void StepApps(double dt, double catX, bool catHeld)
    {
        if (AppRules is not AppsSpec rules) return;
        EnsureDesktop();
        // The tornado wears off like Cheer; it only hits while the cat is down.
        TabsRemaining = Math.Max(0, TabsRemaining - dt);
        if (TabsRemaining > 0 && !CatLifted)
        {
            tabClock -= dt;
            if (tabClock <= 0) { tabClock += rules.TabEvery; TabPulse(catX, rules); }
        }
        // What is in the air lands as usual (a rescue does not stop a flight; what lands on the cat
        // misses a lifted cat).
        foreach (var launch in launches.ToArray())
        {
            launch.Age += dt;
            if (launch.Age < launch.Duration) continue;
            launches.Remove(launch);
            Land(launch, rules);
            if (Over) return;
        }
        if (catHeld) return;
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var popup = popups[i];
            popup.Age += dt;
            if (popup.Age < popup.Life) continue;
            popups.RemoveAt(i);
            PopupRanOut(popup, rules);
        }
        foreach (var app in desktop)
        {
            if (!app.Infected)
            {
                Charge(app, rules.App(app.Kind).Charge > 0 ? dt / rules.App(app.Kind).Charge : 1);
                continue;
            }
            app.AttackClock -= dt;
            if (app.AttackClock <= 0) { app.AttackClock = rules.AttackEvery(app.Kind); InfectedAct(app, rules); }
            app.CrawlClock -= dt;
            if (app.CrawlClock <= 0) { app.CrawlClock = rules.CrawlEvery; Crawl(app); }
        }
        if (!double.IsNaN(infectTimer) && Phase == BattlePhase.Fighting)
        {
            infectTimer -= dt;
            if (infectTimer <= 0) { infectTimer = double.NaN; ShootGoo(rules); }
        }
    }

    private void Charge(DesktopApp app, double amount)
    {
        if (app.Infected || app.Charge >= 1) return;
        app.Charge = Math.Min(1, app.Charge + amount);
        if (app.Charge < 1) return;
        app.ReadyAt = Time;
        events.Add(new(BattleEventKind.AppReady, IconX(app), app.Id));
    }

    // A new wave: one clean icon moves house (from the second wave on), and maybe a virus some seconds in.
    private void AppsWaveStarted()
    {
        infectTimer = double.NaN;
        if (AppRules is not AppsSpec rules) return;
        EnsureDesktop();
        if (Wave > 1)
        {
            var clean = desktop.Where(a => !a.Infected).ToList();
            if (clean.Count > 0)
            {
                var app = clean[appRandom.Next(clean.Count)];
                if (FreeCell(app) is (int c, int r)) Move(app, c, r, false);
            }
        }
        if (Threat >= rules.InfectMinThreat && appRandom.NextDouble() < rules.InfectChance) infectTimer = 3 + appRandom.NextDouble() * 5;
    }

    private void Move(DesktopApp app, int col, int row, bool crawl)
    {
        app.FromCol = app.Col; app.FromRow = app.Row;
        app.Col = col; app.Row = row;
        app.MovedAt = Time;
        app.Crawled = crawl;
        events.Add(new(BattleEventKind.AppMoved, IconX(app), app.Id));
    }

    // An infected icon creeps to a free cell next to it (none free: it stays).
    private void Crawl(DesktopApp app)
    {
        var next = new[] { (app.Col - 1, app.Row), (app.Col + 1, app.Row), (app.Col, app.Row - 1), (app.Col, app.Row + 1) }
            .Where(cell => CellOpen(cell.Item1, cell.Item2, app)).ToList();
        if (next.Count == 0) return;
        var (c, r) = next[appRandom.Next(next.Count)];
        Move(app, c, r, true);
    }

    // A cleared wave: open ads close by themselves (their slimes are gone with the wave).
    private void AppsWaveCleared()
    {
        if (AppRules == null) return;
        infectTimer = double.NaN;
        foreach (var popup in popups) AddEffect(BattleEffectKind.Poof, popup.X, 40);
        popups.Clear();
    }

    // A defeated enemy: every clean icon charges a little; an icon it infected is clean again and a
    // virus it shot is lost on the way.
    private void AppsEnemyDefeated(Slime slime)
    {
        if (AppRules is not AppsSpec rules || Over) return;
        foreach (var app in desktop)
        {
            if (app.InfectedBy == slime.Id) Cure(app);
            else Charge(app, rules.KillCharge);
        }
        launches.RemoveAll(l => l.Kind == LaunchKind.Goo && l.SlimeId == slime.Id);
    }

    private void Cure(DesktopApp app)
    {
        app.InfectedBy = 0;
        app.ScrubsLeft = 0;
        app.CleanedAt = Time;
        events.Add(new(BattleEventKind.AppCleaned, IconX(app), app.Id));
    }

    // A slime spits its virus at a clean icon (while fewer than MaxInfected are taken).
    private void ShootGoo(AppsSpec rules)
    {
        int busy = desktop.Count(a => a.Infected) + launches.Count(l => l.Kind == LaunchKind.Goo);
        if (busy >= rules.MaxInfected) return;
        var shooters = slimes.Where(s => s.Targetable && !s.Shielded && s.LeapHeight == 0).ToList();
        var clean = desktop.Where(a => !a.Infected && !launches.Any(l => l.Kind == LaunchKind.Goo && l.AppId == a.Id)).ToList();
        if (shooters.Count == 0 || clean.Count == 0) return;
        var slime = shooters[appRandom.Next(shooters.Count)];
        var app = clean[appRandom.Next(clean.Count)];
        Launch(LaunchKind.Goo, app, slime.X, slime.Height * 0.8, GooSeconds, toIcon: true).SlimeId = slime.Id;
        events.Add(new(BattleEventKind.GooShot, slime.X, slime.Id));
    }

    private AppLaunch Launch(LaunchKind kind, DesktopApp app, double floorX, double height, double seconds, bool toIcon = false)
    {
        var launch = new AppLaunch { Id = nextAppId++, Kind = kind, AppId = app.Id, FloorX = floorX, Height = height, Duration = seconds, ToIcon = toIcon };
        launches.Add(launch);
        return launch;
    }

    // An infected icon's turn against the cat.
    private void InfectedAct(DesktopApp app, AppsSpec rules)
    {
        switch (app.Kind)
        {
            case BattleAppKind.Tabs:
                // An ad over one of the slimes (one ad at a time).
                if (popups.Count > 0 || launches.Any(l => l.Kind == LaunchKind.Popup)) return;
                var covered = slimes.Where(s => s.Targetable).ToList();
                double x = covered.Count > 0 ? covered[appRandom.Next(covered.Count)].X : IconX(app);
                Launch(LaunchKind.Popup, app, Math.Clamp(x, Left + PopupWidth / 2, Math.Max(Left + PopupWidth / 2, Right - PopupWidth / 2)), PopupHeight / 2, AttackSeconds);
                break;
            case BattleAppKind.Ping:
                Launch(LaunchKind.BadBell, app, lastCatX, 100, AttackSeconds);
                break;
            case BattleAppKind.EndTask:
                Launch(LaunchKind.Freeze, app, lastCatX, 120, AttackSeconds);
                break;
            case BattleAppKind.Undo:
                // The enemy with the smallest share of its HP left gets some back.
                Slime? hurt = null;
                foreach (var s in slimes)
                    if (s.Targetable && s.Hp < s.MaxHp && (hurt == null || s.Hp / s.MaxHp < hurt.Hp / hurt.MaxHp)) hurt = s;
                if (hurt == null) return;
                var mend = Launch(LaunchKind.Mend, app, hurt.X, hurt.Height, AttackSeconds);
                mend.Targets = new[] { hurt.Id };
                break;
            default:
                if (Alive >= maxAlive || Settings.EnemyTypes.All(e => e.Id != rules.PopupSpawn)) return;
                Launch(LaunchKind.Spit, app, Math.Clamp(IconX(app), Left + 30, Math.Max(Left + 30, Right - 30)), 0, AttackSeconds);
                break;
        }
        events.Add(new(BattleEventKind.AppAttack, IconX(app), app.Id));
    }

    // A flight arrives.
    private void Land(AppLaunch launch, AppsSpec rules)
    {
        switch (launch.Kind)
        {
            case LaunchKind.Tabs:
                TabsRemaining = rules.TabSeconds;
                tabClock = 0; // The first pulse at once.
                break;
            case LaunchKind.Bell:
                blasting = true;
                foreach (int id in launch.Targets)
                    if (Find(id) is { Targetable: true, Shielded: false } slime) Ping(slime, rules);
                blasting = false;
                RefreshGuards();
                break;
            case LaunchKind.EndTask:
                // Its target, or (gone meanwhile) the strongest one left.
                var target = launch.Targets.Length > 0 && Find(launch.Targets[0]) is { Targetable: true, Shielded: false, Mended: false } chosen ? chosen : Strongest();
                if (target == null) break;
                AddEffect(BattleEffectKind.EndTask, target.X, target.Height + 30, target.Width);
                Damage(target, target.IsBoss || target.Elite != null ? target.MaxHp * rules.EndTaskShare : target.Hp, DamageSource.App);
                break;
            case LaunchKind.Undo:
                // What the cat lost since just before the click (the flight time included).
                double lost = 0;
                foreach (var (time, hp) in hurts) if (Time - time <= rules.UndoSeconds + launch.Duration) lost += hp;
                double back = Math.Min(lost, CatMaxHp - CatHp);
                if (back <= 0) break;
                CatHp += back;
                hurts.Clear();
                AddEffect(BattleEffectKind.Rewind, lastCatX, 60);
                AddEffect(BattleEffectKind.Number, lastCatX, 115, -Math.Round(back));
                break;
            case LaunchKind.Goo:
                if (FindApp(launch.AppId) is not DesktopApp app || app.Infected || Find(launch.SlimeId) is not { Targetable: true } shooter) break;
                app.InfectedBy = shooter.Id;
                app.ScrubsLeft = Math.Max(1, rules.ScrubClicks);
                app.AttackClock = rules.AttackEvery(app.Kind) * 0.5;
                app.CrawlClock = rules.CrawlEvery;
                app.InfectedAt = Time;
                events.Add(new(BattleEventKind.AppInfected, IconX(app), app.Id));
                break;
            case LaunchKind.BadBell:
                // Falls on the cat's head (a lifted or protected cat is missed).
                if (CatLifted || Grace > 0) break;
                AddEffect(BattleEffectKind.Bell, lastCatX, 100);
                LandHit(rules.BellDamage, lastCatX, out _);
                break;
            case LaunchKind.Freeze:
                // "Not responding" over the cat: a short stun (the Shield stops it; no stun again too soon).
                if (CatLifted || Grace > 0) break;
                if (ShieldRemaining > 0) { events.Add(new(BattleEventKind.Blocked, lastCatX)); break; }
                AddEffect(BattleEffectKind.EndTask, lastCatX, 125);
                if (CatHeld || CatStunned || StunImmunity > 0) break;
                CatStunRemaining = rules.FreezeSeconds;
                StunsTaken++;
                CancelSwing();
                events.Add(new(BattleEventKind.CatStunned, lastCatX));
                break;
            case LaunchKind.Mend:
                if (launch.Targets.Length == 0 || Find(launch.Targets[0]) is not { Targetable: true } mended) break;
                double heal = Math.Min(mended.MaxHp - mended.Hp, mended.MaxHp * rules.MendShare);
                if (heal <= 0) break;
                mended.Hp += heal;
                AddEffect(BattleEffectKind.Mend, mended.X, mended.Height);
                AddEffect(BattleEffectKind.Number, mended.X, mended.Height + 10, -Math.Round(heal));
                break;
            case LaunchKind.Spit:
                if (Alive >= maxAlive || Settings.EnemyTypes.All(e => e.Id != rules.PopupSpawn)) break;
                var spat = AddSlime(launch.FloorX, Settings.Enemy(rules.PopupSpawn));
                spat.Facing = lastCatX < spat.X ? -1 : 1;
                events.Add(new(BattleEventKind.SlimeSpawned, spat.X));
                break;
            case LaunchKind.Popup:
                var popup = new BattlePopup { Id = nextAppId++, X = launch.FloorX, Life = rules.PopupSeconds };
                popups.Add(popup);
                events.Add(new(BattleEventKind.PopupOpened, popup.X, popup.Id));
                break;
        }
    }

    // Left alone, the ad lets an enemy out where it was (when the floor has room).
    private void PopupRanOut(BattlePopup popup, AppsSpec rules)
    {
        AddEffect(BattleEffectKind.Poof, popup.X, 40);
        if (Alive >= maxAlive || Settings.EnemyTypes.All(e => e.Id != rules.PopupSpawn)) return;
        var slime = AddSlime(popup.X, Settings.Enemy(rules.PopupSpawn));
        slime.Facing = lastCatX < slime.X ? -1 : 1;
        events.Add(new(BattleEventKind.PopupSpawned, slime.X, slime.Id));
    }

    // The ad's ×: it closes (whatever the cat is doing).
    public bool ClosePopup(int id)
    {
        if (FindPopup(id) is not BattlePopup popup || Over || Paused || ChoosingUpgrade) return false;
        popups.Remove(popup);
        PopupsClosed++;
        AddEffect(BattleEffectKind.Poof, popup.X, 40);
        events.Add(new(BattleEventKind.PopupClosed, popup.X, popup.Id));
        return true;
    }

    // A click on an icon: an infected one is scrubbed (whatever the cat is doing); a charged one sends
    // its help (not while the cat is in the user's hands, and only when it has something to do). A
    // chosen skill is dropped.
    public bool UseApp(int id, double catX)
    {
        if (FindApp(id) is not DesktopApp app || Over || Paused || ChoosingUpgrade || AppRules is not AppsSpec rules) return false;
        if (app.Infected)
        {
            app.ScrubsLeft--;
            app.ScrubbedAt = Time;
            events.Add(new(BattleEventKind.AppScrubbed, IconX(app), app.Id));
            if (app.ScrubsLeft <= 0) Cure(app);
            return true;
        }
        if (!app.Ready || SkillsLocked) return false;
        switch (app.Kind)
        {
            case BattleAppKind.Tabs:
                Launch(LaunchKind.Tabs, app, catX, 45, LaunchSeconds);
                break;
            case BattleAppKind.Ping:
                var rung = slimes.Where(s => s.Targetable && !s.Shielded).ToList();
                if (rung.Count == 0) return false;
                var bells = Launch(LaunchKind.Bell, app, catX, 0, LaunchSeconds);
                bells.Targets = rung.Select(s => s.Id).ToArray();
                bells.TargetX = rung.Select(s => s.X).ToArray();
                break;
            case BattleAppKind.EndTask:
                if (Strongest() is not Slime strongest) return false;
                Launch(LaunchKind.EndTask, app, strongest.X, strongest.Height + 30, LaunchSeconds).Targets = new[] { strongest.Id };
                break;
            case BattleAppKind.Undo:
                double lost = 0;
                foreach (var (time, hp) in hurts) if (Time - time <= rules.UndoSeconds) lost += hp;
                if (lost <= 0 || CatHp >= CatMaxHp) return false;
                Launch(LaunchKind.Undo, app, catX, 60, LaunchSeconds);
                break;
            default:
                // The plain enemies nearest the cat are taken at once; the flight shows them going in.
                var taken = slimes.Where(s => s.Targetable && !s.IsBoss && s.Elite == null && !s.Shielded && !s.Mended)
                    .OrderBy(s => Math.Abs(s.X - catX)).Take(Math.Max(1, rules.TrashCount)).ToList();
                if (taken.Count == 0) return false;
                var bin = Launch(LaunchKind.Trash, app, taken[0].X, 0, LaunchSeconds, toIcon: true);
                bin.Targets = taken.Select(s => s.Id).ToArray();
                bin.TargetX = taken.Select(s => s.X).ToArray();
                bin.Colors = taken.Select(s => s.Type.Color).ToArray();
                blasting = true;
                foreach (var s in taken) Damage(s, s.Hp + 1, DamageSource.App);
                blasting = false;
                RefreshGuards();
                break;
        }
        app.Charge = 0;
        app.UsedAt = Time;
        Armed = null;
        events.Add(new(BattleEventKind.AppUsed, IconX(app), app.Id));
        return true;
    }

    // The enemy End task goes for: the most HP (never a shielded boss or one guarded by a heal).
    private Slime? Strongest()
    {
        Slime? best = null;
        foreach (var s in slimes)
            if (s.Targetable && !s.Shielded && !s.Mended && (best == null || s.Hp > best.Hp)) best = s;
        return best;
    }

    private void TabPulse(double catX, AppsSpec rules)
    {
        blasting = true;
        foreach (var slime in slimes.ToArray())
            if (slime.Targetable && !slime.Shielded && Math.Abs(slime.X - catX) <= rules.TabRadius + slime.HitWidth / 2)
            {
                AddEffect(BattleEffectKind.Spark, slime.X, slime.Height * 0.6);
                events.Add(new(BattleEventKind.SlimeHit, slime.X));
                Damage(slime, rules.TabDamage * DamageFactor, DamageSource.App);
            }
        blasting = false;
        RefreshGuards();
    }

    // A bell on one enemy: a little damage, and a stun unless it is in the middle of something it
    // cannot stop (a dash, a heal, a leap) or ignores Push.
    private void Ping(Slime slime, AppsSpec rules)
    {
        AddEffect(BattleEffectKind.Bell, slime.X, slime.Height + 26);
        Damage(slime, rules.PingDamage * DamageFactor, DamageSource.App);
        if (!slime.Targetable || slime.LeapHeight > 0 || slime.Elite is { PushImmune: true }) return;
        if (slime.State is not (SlimeState.Moving or SlimeState.Windup or SlimeState.Recover)) return;
        double stun = rules.PingStun * slime.Type.StunFactor;
        if (stun <= 0) return;
        slime.StunRemaining = stun;
        slime.Set(SlimeState.Stunned);
    }

    // Every hit the cat really took (LandHit), kept as long as Undo can reach back.
    private void NoteHurt(double hp)
    {
        if (AppRules is not AppsSpec rules) return;
        hurts.Add((Time, hp));
        hurts.RemoveAll(h => Time - h.Time > rules.UndoSeconds + LaunchSeconds);
    }

    // The battle ended: nothing more flies, viruses and ads are gone; the icons stay for the outro.
    private void ClearApps()
    {
        launches.Clear();
        popups.Clear();
        TabsRemaining = 0;
        infectTimer = double.NaN;
        hurts.Clear();
        foreach (var app in desktop) { app.InfectedBy = 0; app.ScrubsLeft = 0; }
    }
}
