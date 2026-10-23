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
    internal readonly List<BattleHazard> hazards = new();
    internal readonly List<VirusFile> virusFiles = new();
    public IReadOnlyList<VirusFile> VirusFiles => virusFiles;
    // When virus.exe next copies itself.
    private double virusExeClock;
    public IReadOnlyList<DesktopApp> Desktop => desktop;
    public IReadOnlyList<BattlePopup> Popups => popups;
    public IReadOnlyList<BattleHazard> Hazards => hazards;
    public IReadOnlyList<AppLaunch> Launches => launches;
    private int nextAppId = 1;
    // The apps this battle uses (null: none, or turned off in the setup panel).
    public AppsSpec? AppRules => Stage != null && AppsOn ? Settings.Apps : null;
    // The desktop grid: columns across the floor's width, rows up from the strip; cells the HUD covers.
    public int GridCols { get; private set; } = 12;
    public int GridRows { get; private set; } = 3;
    private readonly HashSet<(int Col, int Row)> blockedCells = new();
    // Tab tornado: seconds left, and the time to its next pulse.
    public double TabsRemaining { get; internal set; }
    private double tabClock;
    // When this wave's virus is shot (NaN: none coming).
    private double infectTimer = double.NaN;
    // When the next malware comes (NaN: not started yet), and the viruses elites shot this wave.
    private double malwareClock = double.NaN;
    private int eliteShots;
    // The loading ring whose spinner the user is holding down (0: none).
    public int HoldingHazardId { get; private set; }
    // While an infected Tabs holds an icon, its ads cannot be closed (and do not run out).
    public bool PopupsLocked => desktop.Any(a => a.Infected && a.Kind == BattleAppKind.Tabs);
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
    public BattleHazard? FindHazard(int id) { foreach (var h in hazards) if (h.Id == id) return h; return null; }
    // The cat's speed (running and swinging) against its usual: slower inside a loading ring (on the floor).
    public double LagFactor
    {
        get
        {
            if (AppRules is not AppsSpec rules || CatLifted) return 1;
            foreach (var h in hazards)
                if (h.Kind == HazardKind.Loading && Math.Abs(lastCatX - h.X) <= rules.LoadingRadius) return rules.LoadingSlow;
            return 1;
        }
    }
    public BattlePopup? FindPopup(int id) { foreach (var p in popups) if (p.Id == id) return p; return null; }
    // The floor X under an icon's column (the grid spans the floor's width).
    public double IconX(DesktopApp app) => Left + (app.Col + 0.5) * (Right - Left) / GridCols;
    // The slime holding an icon with its virus (or about to): it wears the virus mark.
    public bool IsInfector(int slimeId)
    {
        foreach (var a in desktop) if (a.InfectedBy == slimeId) return true;
        if (virusFiles.Count > 0 && virusFiles[0].SlimeId == slimeId) return true;
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
        foreach (var f in virusFiles) if (f.Col == col && f.Row == row) return false;
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
        StepHold(dt, rules);
        if (catHeld) return;
        StepVirusFiles(dt, rules);
        bool locked = PopupsLocked;
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var popup = popups[i];
            if (locked) continue;
            popup.Age += dt;
            if (popup.Age < popup.Life) continue;
            popups.RemoveAt(i);
            PopupRanOut(popup, rules);
        }
        for (int i = hazards.Count - 1; i >= 0; i--)
        {
            hazards[i].Age += dt;
            if (hazards[i].Age < hazards[i].Life) continue;
            AddEffect(BattleEffectKind.Poof, hazards[i].X, hazards[i].Kind == HazardKind.Update ? WallHeight / 2 : 20);
            hazards.RemoveAt(i);
        }
        foreach (var app in desktop)
        {
            if (!app.Infected)
            {
                // Power bank: every icon charges faster.
                Charge(app, rules.App(app.Kind).Charge > 0 ? dt / (rules.App(app.Kind).Charge * Math.Max(0.3, 1 - appChargeCut)) : 1);
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
        if (Phase != BattlePhase.Fighting) return;
        StepMalware(dt, rules);
        StepEliteViruses(dt, rules);
    }

    // Malware at random times while the fight goes on (from MalwareMinThreat): the first after half a
    // usual wait, then every MalwareEveryMin–MalwareEveryMax s.
    private void StepMalware(double dt, AppsSpec rules)
    {
        if (Threat < rules.MalwareMinThreat) return;
        double Wait() => rules.MalwareEveryMin + appRandom.NextDouble() * (rules.MalwareEveryMax - rules.MalwareEveryMin);
        if (double.IsNaN(malwareClock)) malwareClock = Wait() * 0.5;
        malwareClock -= dt;
        if (malwareClock > 0) return;
        malwareClock = Wait();
        ThrowMalware(rules);
    }

    // The kinds of malware out now: a loading ring, a forced update, virus.exe, the ads.
    private bool MalwareOut(int kind) => kind switch
    {
        0 => hazards.Any(h => h.Kind == HazardKind.Loading),
        1 => hazards.Any(h => h.Kind == HazardKind.Update),
        2 => virusFiles.Count > 0,
        _ => popups.Count > 0 || launches.Any(l => l.Kind == LaunchKind.Popup)
    };

    // One kind drawn by weight among those not out yet (none while MaxMalware kinds are out).
    private void ThrowMalware(AppsSpec rules)
    {
        if (!slimes.Any(s => s.Targetable)) return;
        double[] weights = { rules.LoadingWeight, rules.UpdateWeight, rules.VirusExeWeight, rules.AdWeight };
        int outNow = Enumerable.Range(0, weights.Length).Count(MalwareOut);
        if (outNow >= rules.MaxMalware) return;
        for (int i = 0; i < weights.Length; i++) if (MalwareOut(i) || weights[i] < 0) weights[i] = 0;
        if (!desktop.Any(a => !a.Infected)) weights[2] = 0; // virus.exe needs a clean icon to go for.
        double total = weights.Sum();
        if (total <= 0) return;
        double roll = appRandom.NextDouble() * total;
        int pick = 0;
        while (pick < weights.Length - 1 && (weights[pick] <= 0 || roll >= weights[pick])) { roll -= weights[pick]; pick++; }
        switch (pick)
        {
            case 0: ThrowLoading(rules); break;
            case 1: ThrowUpdate(rules); break;
            case 2: DropVirusExe(rules); break;
            default: OpenAds(rules, null); break;
        }
    }

    // An elite with a virus affix shoots at a clean icon every VirusEvery s (at most EliteVirusPerWave a
    // wave; with no icon to take it tries again a second later).
    private void StepEliteViruses(double dt, AppsSpec rules)
    {
        foreach (var slime in slimes)
        {
            if (!slime.Targetable || slime.Elite is not { VirusEvery: > 0 } elite) continue;
            slime.VirusClock -= dt;
            if (slime.VirusClock > 0) continue;
            if (eliteShots >= rules.EliteVirusPerWave) { slime.VirusClock = elite.VirusEvery; continue; }
            if (ShootGoo(rules, slime)) { eliteShots++; slime.VirusClock = elite.VirusEvery; }
            else slime.VirusClock = 1;
        }
    }

    // The user holds a loading ring's spinner: it closes after LoadingHold s of holding.
    private void StepHold(double dt, AppsSpec rules)
    {
        if (HoldingHazardId == 0) return;
        if (FindHazard(HoldingHazardId) is not { Kind: HazardKind.Loading } hazard) { HoldingHazardId = 0; return; }
        hazard.Held += dt;
        if (hazard.Held < rules.LoadingHold) return;
        HoldingHazardId = 0;
        RemoveHazard(hazard);
    }

    // The ads (AdCount in all, spread over the floor's width in random spots): from an infected Tabs
    // they fly from its icon, as malware they pop up where they stand.
    private void OpenAds(AppsSpec rules, DesktopApp? from)
    {
        int count = rules.AdCount - popups.Count - launches.Count(l => l.Kind == LaunchKind.Popup);
        if (count <= 0) return;
        double minX = Left + PopupWidth / 2, maxX = Math.Max(minX, Right - PopupWidth / 2), slot = (maxX - minX) / rules.AdCount;
        var taken = popups.Select(p => p.X).Concat(launches.Where(l => l.Kind == LaunchKind.Popup).Select(l => l.FloorX)).ToList();
        var slots = Enumerable.Range(0, rules.AdCount).Where(i => !taken.Any(x => Math.Abs(x - (minX + (i + 0.5) * slot)) < slot / 2)).ToList();
        for (int n = 0; n < count && slots.Count > 0; n++)
        {
            int k = appRandom.Next(slots.Count);
            double x = minX + (slots[k] + 0.2 + 0.6 * appRandom.NextDouble()) * slot;
            slots.RemoveAt(k);
            if (from != null) Launch(LaunchKind.Popup, from, x, PopupHeight / 2, AttackSeconds);
            else OpenPopup(x, rules);
        }
    }

    private void OpenPopup(double x, AppsSpec rules)
    {
        var popup = new BattlePopup { Id = nextAppId++, X = x, Life = rules.PopupSeconds };
        popups.Add(popup);
        events.Add(new(BattleEventKind.PopupOpened, popup.X, popup.Id));
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
        eliteShots = 0;
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

    // A cleared wave: open ads and malware close by themselves (their slimes are gone with the wave).
    private void AppsWaveCleared()
    {
        if (AppRules == null) return;
        infectTimer = double.NaN;
        foreach (var popup in popups) AddEffect(BattleEffectKind.Poof, popup.X, 40);
        popups.Clear();
        foreach (var hazard in hazards) AddEffect(BattleEffectKind.Poof, hazard.X, 20);
        hazards.Clear();
        RemoveVirusFiles();
    }

    // A defeated enemy: every clean icon charges a little; an icon it infected is clean again and a
    // virus it shot is lost on the way.
    private void AppsEnemyDefeated(Slime slime)
    {
        if (AppRules is not AppsSpec rules || Over) return;
        foreach (var app in desktop)
        {
            if (app.InfectedBy == slime.Id) Cure(app);
            else Charge(app, rules.KillCharge + killChargeBonus);
        }
        launches.RemoveAll(l => l.Kind == LaunchKind.Goo && l.SlimeId == slime.Id);
        if (virusFiles.Count > 0 && virusFiles[0].SlimeId == slime.Id) RemoveVirusFiles();
    }

    private void Cure(DesktopApp app)
    {
        app.InfectedBy = 0;
        app.ScrubsLeft = 0;
        app.CleanedAt = Time;
        events.Add(new(BattleEventKind.AppCleaned, IconX(app), app.Id));
    }

    // A slime drops virus.exe on a free desktop cell (one outbreak at a time, and only with a clean icon to go for).
    private void DropVirusExe(AppsSpec rules)
    {
        if (virusFiles.Count > 0 || !desktop.Any(a => !a.Infected)) return;
        var shooters = slimes.Where(s => s.Targetable).ToList();
        if (shooters.Count == 0 || FreeCell(null) is not (int col, int row)) return;
        var slime = shooters[appRandom.Next(shooters.Count)];
        AddVirusFile(slime.Id, col, row);
        virusExeClock = rules.VirusExeEvery;
        events.Add(new(BattleEventKind.GooShot, slime.X, slime.Id));
    }

    private void AddVirusFile(int slimeId, int col, int row)
    {
        var file = new VirusFile { Id = nextAppId++, SlimeId = slimeId, Col = col, Row = row, BornAt = Time };
        virusFiles.Add(file);
        events.Add(new(BattleEventKind.VirusFileAdded, Left + (col + 0.5) * (Right - Left) / GridCols, file.Id));
    }

    // Every VirusExeEvery s one copy appears next to a file (anywhere free when none has room); with
    // VirusExeCopies of them they infect a clean icon and are gone (they wait while none can be taken).
    private void StepVirusFiles(double dt, AppsSpec rules)
    {
        if (virusFiles.Count == 0) return;
        if (virusFiles.Count >= rules.VirusExeCopies) { InfectFromFiles(rules); return; }
        virusExeClock -= dt;
        if (virusExeClock > 0) return;
        virusExeClock = rules.VirusExeEvery;
        var spots = new List<(int, int)>();
        foreach (var f in virusFiles)
            foreach (var (c, r) in new[] { (f.Col - 1, f.Row), (f.Col + 1, f.Row), (f.Col, f.Row - 1), (f.Col, f.Row + 1) })
                if (CellOpen(c, r, null) && !spots.Contains((c, r))) spots.Add((c, r));
        (int, int)? spot = spots.Count > 0 ? spots[appRandom.Next(spots.Count)] : FreeCell(null);
        if (spot is (int col, int row)) AddVirusFile(virusFiles[0].SlimeId, col, row);
        if (virusFiles.Count >= rules.VirusExeCopies) InfectFromFiles(rules);
    }

    private void InfectFromFiles(AppsSpec rules)
    {
        int busy = desktop.Count(a => a.Infected) + launches.Count(l => l.Kind == LaunchKind.Goo);
        var clean = desktop.Where(a => !a.Infected).ToList();
        if (busy >= rules.MaxInfected || clean.Count == 0 || Find(virusFiles[0].SlimeId) is not { Targetable: true } slime) return;
        var app = clean[appRandom.Next(clean.Count)];
        app.InfectedBy = slime.Id;
        app.ScrubsLeft = Math.Max(1, rules.ScrubClicks);
        app.AttackClock = rules.AttackEvery(app.Kind) * 0.5;
        app.CrawlClock = rules.CrawlEvery;
        app.InfectedAt = Time;
        events.Add(new(BattleEventKind.AppInfected, IconX(app), app.Id));
        RemoveVirusFiles();
    }

    private void RemoveVirusFiles()
    {
        if (virusFiles.Count == 0) return;
        virusFiles.Clear();
        events.Add(new(BattleEventKind.VirusFilesGone, 0));
    }

    // A click on a virus.exe copy deletes it (whatever the cat is doing).
    public bool DeleteVirusFile(int id)
    {
        int index = virusFiles.FindIndex(f => f.Id == id);
        if (index < 0 || Over || Paused || ChoosingUpgrade) return false;
        var file = virusFiles[index];
        virusFiles.RemoveAt(index);
        events.Add(new(BattleEventKind.VirusFileDeleted, Left + (file.Col + 0.5) * (Right - Left) / GridCols, file.Id));
        return true;
    }

    // The forced update's half width and height on the strip (the view draws it, the walls use it).
    public const double WallHalfWidth = 38, WallHeight = 96;

    // A forced update standing strictly between two floor points (the nearest to `from`), or null.
    public BattleHazard? WallBetween(double from, double to)
    {
        BattleHazard? wall = null;
        foreach (var h in hazards)
            if (h.Kind == HazardKind.Update && (h.X - from) * (to - h.X) > 0 && (wall == null || Math.Abs(h.X - from) < Math.Abs(wall.X - from))) wall = h;
        return wall;
    }

    // The forced update the cat stands in (put down there), or null.
    private BattleHazard? WallAt(double catX)
    {
        foreach (var h in hazards)
            if (h.Kind == HazardKind.Update && Math.Abs(h.X - catX) < WallHalfWidth + CatHalfWidth - 4) return h;
        return null;
    }

    // A slime throws a forced update between the cat and the nearest slime far enough away (one at a
    // time, never on a slime); with no room for it, a loading ring instead.
    private void ThrowUpdate(AppsSpec rules)
    {
        if (hazards.Any(h => h.Kind == HazardKind.Update)) return;
        double gap = WallHalfWidth + CatHalfWidth + 30;
        double? spot = null;
        foreach (var s in slimes.Where(s => s.Targetable && !s.IsBoss).OrderBy(s => Math.Abs(s.X - lastCatX)))
        {
            double x = lastCatX + Math.Sign(s.X - lastCatX) * gap;
            if (Math.Abs(s.X - lastCatX) < gap + WallHalfWidth + s.Width / 2 || x < Left + WallHalfWidth || x > Right - WallHalfWidth) continue;
            if (slimes.Any(o => o.Targetable && Math.Abs(o.X - x) < WallHalfWidth + o.Width / 2)) continue;
            spot = x;
            break;
        }
        if (spot is not double at) { ThrowLoading(rules); return; }
        var wall = new BattleHazard { Id = nextAppId++, Kind = HazardKind.Update, X = at, Life = rules.UpdateSeconds, ClicksLeft = Math.Max(1, rules.UpdateClicks) };
        hazards.Add(wall);
        events.Add(new(BattleEventKind.HazardOpened, at, wall.Id));
    }

    // The cat's swing on a forced update: its progress jumps.
    private void HitWall(BattleHazard wall)
    {
        if (AppRules is not AppsSpec rules) return;
        wall.Age += rules.UpdateHitSeconds;
        wall.HitAt = Time;
        AddEffect(BattleEffectKind.Spark, wall.X, WallHeight * 0.5);
        events.Add(new(BattleEventKind.WallHit, wall.X, wall.Id));
    }

    // A slime throws a loading ring where the cat stands (one at a time).
    private void ThrowLoading(AppsSpec rules)
    {
        if (hazards.Any(h => h.Kind == HazardKind.Loading) || !slimes.Any(s => s.Targetable)) return;
        double x = Math.Clamp(lastCatX, Left + rules.LoadingRadius, Math.Max(Left + rules.LoadingRadius, Right - rules.LoadingRadius));
        var hazard = new BattleHazard { Id = nextAppId++, Kind = HazardKind.Loading, X = x, Life = rules.LoadingSeconds };
        hazards.Add(hazard);
        events.Add(new(BattleEventKind.HazardOpened, x, hazard.Id));
    }

    // A click on malware's spinner or Later button: it closes (a forced update after its clicks; a
    // loading ring with LoadingHold once its spinner is held that long), whatever the cat is doing.
    public bool CloseHazard(int id)
    {
        if (FindHazard(id) is not BattleHazard hazard || Over || Paused || ChoosingUpgrade) return false;
        if (hazard.Kind == HazardKind.Update && --hazard.ClicksLeft > 0)
        {
            hazard.HitAt = Time;
            events.Add(new(BattleEventKind.HazardTapped, hazard.X, hazard.Id));
            return true;
        }
        if (hazard.Kind == HazardKind.Loading && AppRules is { LoadingHold: > 0 })
        {
            HoldingHazardId = hazard.Id;
            hazard.HitAt = Time;
            return true;
        }
        RemoveHazard(hazard);
        return true;
    }

    // The user let go of a loading ring's spinner before it closed: its hold starts over.
    public void ReleaseHazard()
    {
        if (FindHazard(HoldingHazardId) is BattleHazard hazard) hazard.Held = 0;
        HoldingHazardId = 0;
    }

    private void RemoveHazard(BattleHazard hazard)
    {
        hazards.Remove(hazard);
        AddEffect(BattleEffectKind.Poof, hazard.X, 20);
        events.Add(new(BattleEventKind.HazardClosed, hazard.X, hazard.Id));
    }

    // A slime (any, or the one given) spits its virus at a clean icon (while fewer than MaxInfected
    // are taken). False: no shot.
    private bool ShootGoo(AppsSpec rules, Slime? from = null)
    {
        int busy = desktop.Count(a => a.Infected) + launches.Count(l => l.Kind == LaunchKind.Goo);
        if (busy >= rules.MaxInfected) return false;
        var shooters = from != null ? new List<Slime> { from } : slimes.Where(s => s.Targetable && !s.Shielded && s.LeapHeight == 0).ToList();
        var clean = desktop.Where(a => !a.Infected && !launches.Any(l => l.Kind == LaunchKind.Goo && l.AppId == a.Id)).ToList();
        if (shooters.Count == 0 || clean.Count == 0 || from is { LeapHeight: > 0 }) return false;
        var slime = shooters[appRandom.Next(shooters.Count)];
        var app = clean[appRandom.Next(clean.Count)];
        Launch(LaunchKind.Goo, app, slime.X, slime.Height * 0.8, GooSeconds, toIcon: true).SlimeId = slime.Id;
        events.Add(new(BattleEventKind.GooShot, slime.X, slime.Id));
        return true;
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
                // The ads (AdCount of them, a new set once none is open).
                if (popups.Count > 0 || launches.Any(l => l.Kind == LaunchKind.Popup)) return;
                OpenAds(rules, app);
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
                // One spat enemy alive at a time.
                if (Alive >= maxAlive || SpitKinds(rules).Count == 0 || slimes.Any(s => s.Spat && s.State != SlimeState.Defeated)) return;
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
                if (!LandHit(rules.BellDamage, lastCatX, out _) || rules.BellStun <= 0) break;
                // ...and stuns it (no stun again too soon, as after a dash).
                if (CatHeld || CatStunned || StunImmunity > 0) break;
                CatStunRemaining = StunFor(rules.BellStun);
                StunsTaken++;
                CancelSwing();
                events.Add(new(BattleEventKind.CatStunned, lastCatX));
                break;
            case LaunchKind.Freeze:
                // "Not responding" over the cat: a short stun (the Shield stops it; no stun again too soon).
                if (CatLifted || Grace > 0) break;
                if (ShieldRemaining > 0) { events.Add(new(BattleEventKind.Blocked, lastCatX)); break; }
                AddEffect(BattleEffectKind.EndTask, lastCatX, 125);
                if (CatHeld || CatStunned || StunImmunity > 0) break;
                CatStunRemaining = StunFor(rules.FreezeSeconds);
                StunsTaken++;
                CancelSwing();
                events.Add(new(BattleEventKind.CatStunned, lastCatX));
                break;
            case LaunchKind.Mend:
                if (launch.Targets.Length == 0 || Find(launch.Targets[0]) is not { Targetable: true } mended) break;
                double heal = Math.Min(mended.MaxHp - mended.Hp, mended.MaxHp * rules.MendShare);
                if (heal <= 0) break;
                mended.Hp += heal;
                // ...and guards it a moment (never a boss).
                if (!mended.IsBoss && rules.MendGuard > 0) mended.WardUntil = Time + rules.MendGuard;
                AddEffect(BattleEffectKind.Mend, mended.X, mended.Height);
                AddEffect(BattleEffectKind.Number, mended.X, mended.Height + 10, -Math.Round(heal));
                break;
            case LaunchKind.Spit:
                var kinds = SpitKinds(rules);
                if (Alive >= maxAlive || kinds.Count == 0) break;
                var kind = kinds[appRandom.Next(kinds.Count)];
                var spat = AddSlime(launch.FloorX, kind);
                spat.Facing = lastCatX < spat.X ? -1 : 1;
                spat.Spat = true;
                events.Add(new(BattleEventKind.SlimeSpawned, spat.X));
                // An elite of its kind (not counted against maxElites: one spat enemy at a time anyway).
                if (rules.SpitElite && kind.Elite is EliteSpec affix)
                {
                    ApplyElite(spat, affix);
                    events.Add(new(BattleEventKind.EliteAppeared, spat.X, spat.Id));
                }
                break;
            case LaunchKind.Popup:
                var popup = new BattlePopup { Id = nextAppId++, X = launch.FloorX, Life = rules.PopupSeconds };
                popups.Add(popup);
                events.Add(new(BattleEventKind.PopupOpened, popup.X, popup.Id));
                break;
        }
    }

    // What an infected bin spits: any kind with an elite affix (no boss), or the popup's enemy.
    private List<EnemyType> SpitKinds(AppsSpec rules) => rules.SpitElite
        ? Settings.EnemyTypes.Where(e => !e.Boss && e.Elite != null).ToList()
        : Settings.EnemyTypes.Where(e => e.Id == rules.PopupSpawn).ToList();

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
        if (PopupsLocked) { events.Add(new(BattleEventKind.PopupLocked, popup.X, popup.Id)); return false; }
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
                // The bells go to the enemies nearest the cat (PingTargets of them; 0: every one).
                var rung = slimes.Where(s => s.Targetable && !s.Shielded).OrderBy(s => Math.Abs(s.X - catX))
                    .Take(rules.PingTargets > 0 ? rules.PingTargets : int.MaxValue).ToList();
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
        hazards.Clear();
        virusFiles.Clear();
        TabsRemaining = 0;
        infectTimer = double.NaN;
        HoldingHazardId = 0;
        hurts.Clear();
        foreach (var app in desktop) { app.InfectedBy = 0; app.ScrubsLeft = 0; }
    }
}
