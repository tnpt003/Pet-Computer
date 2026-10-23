using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopPet;

// BattleSim: the desktop apps of Adventure, Expedition and Infinity battles (battle.json "apps",
// AppsSpec). Helper icons drop on the field (a cleared wave, sometimes a defeated enemy) and wait
// there for a click; from a threat on, the enemies open an ad popup over one of them that lets a
// slime out unless the user closes it. Icons and the popup wait while the cat is in the user's
// hands (like the waves), and a helper cannot be used then (like the skills). Free battles have none.
public sealed partial class BattleSim
{
    private readonly List<BattleApp> apps = new();
    public IReadOnlyList<BattleApp> Apps => apps;
    private int nextAppId = 1;
    // The apps this battle uses (null: none).
    public AppsSpec? AppRules => Stage != null ? Settings.Apps : null;
    // Tab tornado: seconds left, and the time to its next pulse.
    public double TabsRemaining { get; private set; }
    private double tabClock;
    // When this wave's popup opens (NaN: none coming).
    private double popupTimer = double.NaN;
    // What the cat lost and when (battle time), for Undo.
    private readonly List<(double Time, double Hp)> hurts = new();
    public int AppsUsed { get; private set; }
    public int PopupsClosed { get; private set; }
    public int PopupsMissed { get; private set; }
    // Where the cat stood on the floor at the last Step (the tabs circle it).
    public double CatX => lastCatX;

    public BattleApp? FindApp(int id) { foreach (var a in apps) if (a.Id == id) return a; return null; }

    private void StepApps(double dt, double catX, bool catHeld)
    {
        if (AppRules is not AppsSpec rules) return;
        // The tornado wears off like Cheer; it only hits while the cat is down.
        TabsRemaining = Math.Max(0, TabsRemaining - dt);
        if (TabsRemaining > 0 && !CatLifted)
        {
            tabClock -= dt;
            if (tabClock <= 0) { tabClock += rules.TabEvery; TabPulse(catX, rules); }
        }
        if (catHeld) return;
        for (int i = apps.Count - 1; i >= 0; i--)
        {
            var app = apps[i];
            app.Age += dt;
            if (app.Age < app.Life) continue;
            apps.RemoveAt(i);
            if (app.Hostile) PopupRanOut(app, rules);
            else events.Add(new(BattleEventKind.AppExpired, app.X, app.Id));
        }
        if (!double.IsNaN(popupTimer) && Phase == BattlePhase.Fighting)
        {
            popupTimer -= dt;
            if (popupTimer <= 0) { popupTimer = double.NaN; OpenPopup(rules); }
        }
    }

    // A new wave: maybe a popup some seconds in (from the rules' threat on).
    private void RollPopup()
    {
        popupTimer = double.NaN;
        if (AppRules is not AppsSpec rules || Threat < rules.PopupMinThreat) return;
        if (appRandom.NextDouble() < rules.PopupChance) popupTimer = 2 + appRandom.NextDouble() * 4;
    }

    // A cleared wave: open popups close by themselves and the next wave's helper drops near the cat.
    private void AppsWaveCleared(double catX, bool last)
    {
        if (AppRules is not AppsSpec rules) return;
        popupTimer = double.NaN;
        for (int i = apps.Count - 1; i >= 0; i--)
            if (apps[i].Hostile) { AddEffect(BattleEffectKind.Poof, apps[i].X, 40); apps.RemoveAt(i); }
        if (!last && rules.WaveDrop)
        {
            int side = appRandom.Next(2) == 0 ? -1 : 1;
            double x = catX + side * (90 + appRandom.NextDouble() * 70);
            if (x < Left + 40 || x > Right - 40) x = catX - side * (90 + appRandom.NextDouble() * 70);
            DropApp(x, rules);
        }
    }

    // A defeated enemy: maybe a helper where it stood.
    private void AppsEnemyDefeated(Slime slime)
    {
        if (AppRules is not AppsSpec rules || Over) return;
        if (appRandom.NextDouble() < rules.DropChance) DropApp(slime.X, rules);
    }

    private void DropApp(double x, AppsSpec rules)
    {
        int helpers = 0;
        foreach (var a in apps) if (!a.Hostile) helpers++;
        if (helpers >= rules.MaxOnField) return;
        // One of the helpers, by weight (in BattleAppKind order, so the draw never depends on the file's order).
        var kinds = new[] { BattleAppKind.Tabs, BattleAppKind.Ping, BattleAppKind.EndTask, BattleAppKind.Undo };
        double total = kinds.Sum(k => Math.Max(0, rules.App(k).Weight));
        if (total <= 0) return;
        double roll = appRandom.NextDouble() * total;
        var kind = kinds[^1];
        foreach (var k in kinds)
        {
            roll -= Math.Max(0, rules.App(k).Weight);
            if (roll < 0) { kind = k; break; }
        }
        // Never on top of another icon: a step toward the middle until it has room.
        double min = Left + 30, max = Math.Max(min, Right - 30);
        x = Math.Clamp(x, min, max);
        for (int tries = 0; tries < 6 && apps.Any(a => !a.Hostile && Math.Abs(a.X - x) < AppSpacing); tries++)
            x = Math.Clamp(x + (x < (Left + Right) / 2 ? AppSpacing : -AppSpacing), min, max);
        var app = new BattleApp { Id = nextAppId++, Kind = kind, X = x, Life = rules.Life };
        apps.Add(app);
        events.Add(new(BattleEventKind.AppDropped, x, app.Id));
    }

    // Closest two icons stand (centre to centre).
    public const double AppSpacing = 56;

    // The ad opens over one of the enemies (so a click on it hits the ad, not the enemy).
    private void OpenPopup(AppsSpec rules)
    {
        var under = slimes.Where(s => s.Targetable).ToList();
        if (under.Count == 0) return;
        var slime = under[appRandom.Next(under.Count)];
        double x = Math.Clamp(slime.X, Left + PopupWidth / 2, Math.Max(Left + PopupWidth / 2, Right - PopupWidth / 2));
        var app = new BattleApp { Id = nextAppId++, Kind = BattleAppKind.Popup, X = x, Life = rules.PopupSeconds };
        apps.Add(app);
        events.Add(new(BattleEventKind.PopupOpened, x, app.Id));
    }

    // The popup's size on the field (the view draws it, the clicks use it).
    public const double PopupWidth = 104, PopupHeight = 66;

    // Left alone, the ad lets an enemy out where it was (when the floor has room).
    private void PopupRanOut(BattleApp popup, AppsSpec rules)
    {
        PopupsMissed++;
        AddEffect(BattleEffectKind.Poof, popup.X, 40);
        if (Alive >= maxAlive || Settings.EnemyTypes.All(e => e.Id != rules.PopupSpawn)) return;
        var slime = AddSlime(popup.X, Settings.Enemy(rules.PopupSpawn));
        slime.Facing = lastCatX < slime.X ? -1 : 1;
        events.Add(new(BattleEventKind.PopupSpawned, slime.X, slime.Id));
    }

    // A click on an app: a popup closes (whatever the cat is doing), a helper does its work if it
    // can (otherwise it stays, e.g. Undo with nothing to undo). A chosen skill is dropped.
    public bool UseApp(int id, double catX)
    {
        if (FindApp(id) is not BattleApp app || Over || Paused || ChoosingUpgrade || AppRules is not AppsSpec rules) return false;
        if (app.Hostile)
        {
            apps.Remove(app);
            PopupsClosed++;
            AddEffect(BattleEffectKind.Poof, app.X, 40);
            events.Add(new(BattleEventKind.PopupClosed, app.X, app.Id));
            return true;
        }
        if (SkillsLocked) return false;
        bool used = app.Kind switch
        {
            BattleAppKind.Tabs => StartTabs(rules),
            BattleAppKind.Ping => PingStorm(rules),
            BattleAppKind.EndTask => EndTask(rules),
            _ => Undo(rules, catX)
        };
        if (!used) return false;
        apps.Remove(app);
        AppsUsed++;
        Armed = null;
        events.Add(new(BattleEventKind.AppUsed, app.X, (int)app.Kind));
        return true;
    }

    private bool StartTabs(AppsSpec rules)
    {
        TabsRemaining = rules.TabSeconds;
        tabClock = 0; // The first pulse at once.
        return true;
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

    // Every enemy on the field: a bell and a little damage, and a stun for those not in the middle of
    // something they cannot stop (a dash, a heal, a leap) and not immune to Push.
    private bool PingStorm(AppsSpec rules)
    {
        var hit = slimes.Where(s => s.Targetable && !s.Shielded).ToList();
        if (hit.Count == 0) return false;
        blasting = true;
        foreach (var slime in hit)
        {
            AddEffect(BattleEffectKind.Bell, slime.X, slime.Height + 26);
            Damage(slime, rules.PingDamage * DamageFactor, DamageSource.App);
            if (!slime.Targetable || slime.LeapHeight > 0 || slime.Elite is { PushImmune: true }) continue;
            if (slime.State is not (SlimeState.Moving or SlimeState.Windup or SlimeState.Recover)) continue;
            double stun = rules.PingStun * slime.Type.StunFactor;
            if (stun <= 0) continue;
            slime.StunRemaining = stun;
            slime.Set(SlimeState.Stunned);
        }
        blasting = false;
        RefreshGuards();
        return true;
    }

    // The enemy with the most HP: gone (a boss or an elite loses a share of its max HP instead).
    private bool EndTask(AppsSpec rules)
    {
        Slime? target = null;
        foreach (var s in slimes)
            if (s.Targetable && !s.Shielded && !s.Mended && (target == null || s.Hp > target.Hp)) target = s;
        if (target == null) return false;
        AddEffect(BattleEffectKind.EndTask, target.X, target.Height + 30, target.Width);
        bool tough = target.IsBoss || target.Elite != null;
        Damage(target, tough ? target.MaxHp * rules.EndTaskShare : target.Hp, DamageSource.App);
        return true;
    }

    // The HP lost in the last UndoSeconds comes back (nothing lost: the icon stays).
    private bool Undo(AppsSpec rules, double catX)
    {
        double lost = 0;
        foreach (var (time, hp) in hurts) if (Time - time <= rules.UndoSeconds) lost += hp;
        double back = Math.Min(lost, CatMaxHp - CatHp);
        if (back <= 0) return false;
        CatHp += back;
        hurts.Clear();
        AddEffect(BattleEffectKind.Rewind, catX, 60);
        AddEffect(BattleEffectKind.Number, catX, 115, -Math.Round(back));
        return true;
    }

    // Every hit the cat really took (HurtCat), kept as long as Undo can reach back.
    private void NoteHurt(double hp)
    {
        if (AppRules is not AppsSpec rules) return;
        hurts.Add((Time, hp));
        hurts.RemoveAll(h => Time - h.Time > rules.UndoSeconds);
    }

    private void ClearApps()
    {
        apps.Clear();
        TabsRemaining = 0;
        popupTimer = double.NaN;
        hurts.Clear();
    }
}
