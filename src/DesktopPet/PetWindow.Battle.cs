using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;

namespace DesktopPet;

// Battle Mode (optional, off by default, never resumed at startup): the cat fights slimes
// with a wooden sword; the user picks targets, pushes slimes away and can lift the cat to
// safety. The fight is BattleSim (C# only, no Python); this file owns its lifecycle, turns
// its orders into the pet's normal walking, and shows two small windows: the floor strip
// (BattleFieldView) and the HUD. While a battle runs it owns the pet: no autonomous
// behavior, AI commands, games, sleep or automatic emotions.
public sealed partial class PetWindow
{
    // The battle data for the next battle: Assets/battle.json with the user's battle.json over it
    // (BattleData), read again each time the setup panel opens; a running battle keeps the one it
    // started with. Tests may set it: then exactly that is used and no file is read.
    private BattleSettings battleTuning = BattleData.Settings;
    private bool battleTuningFixed;
    public BattleSettings BattleTuning { get => battleTuning; set { battleTuning = value; battleTuningFixed = true; BattleDataError = null; BattleDataCustom = false; } }
    // The running battle was started with the user's battle.json (its records are kept apart).
    private bool battleCustomData;
    // The user's battle.json (null: none is read). Only the real pet reads it; UI checks may set it.
    public string? BattleDataFile { get; set; }
    // Why the user's file was not used (shown on the setup panel), and whether it is in use.
    public string? BattleDataError { get; private set; }
    public bool BattleDataCustom { get; private set; }

    private void ReloadBattleData()
    {
        if (battleTuningFixed) return;
        var load = BattleData.Load(BattleDataFile);
        battleTuning = load.Settings;
        BattleDataError = load.Error;
        BattleDataCustom = load.Custom;
    }
    private BattleSim? battle;
    private Area battleArea;
    // The battle stays on the monitor it started on (its work area and DPI scale). If the cat is
    // carried or thrown to another monitor the battle waits, paused, until the cat is back.
    private string battleMonitor = "";
    private double battleScale = 1;
    // Why the battle is paused; it runs only when there is no reason left. Each reason is added
    // and removed by its own owner, so closing the menu never resumes a pause the user (or a
    // fullscreen app) made. A fullscreen app also leaves a User pause: the user resumes after it.
    [Flags]
    // App: the whole pet is paused (tray/menu Pause); only un-pausing the pet removes it.
    // Tutorial: the first-battle guide is open (PetWindow.BattleTutorial.cs).
    // Upgrade: an Adventure stage waits for the user's upgrade pick (PetWindow.BattleUpgrade.cs).
    public enum BattlePause { None = 0, User = 1, Menu = 2, Fullscreen = 4, Away = 8, App = 16, Tutorial = 32, Upgrade = 64 }
    private BattlePause battlePause;
    public BattlePause BattlePauseReasons => battlePause;
    public bool BattleCatAway => (battlePause & BattlePause.Away) != 0;
    private bool BattleUserPaused => (battlePause & BattlePause.User) != 0;
    // Any pause but Away also freezes the cat's own physics (a throw stops mid-air and goes on
    // after). Away does not: the cat has to be able to fall and be carried back.
    private bool BattleFreezesPet => battle != null && (battlePause & ~BattlePause.Away) != 0;
    // Replaceable for UI checks; the default asks Windows which monitor the cat is on.
    private Func<string>? battleMonitorProbe;
    private string CatMonitor() => (battleMonitorProbe ??= () => CurrentMonitor().DeviceName)();
    // Replaceable for UI checks: a monitor by name (null: the primary one) with its work area in
    // its own DIPs and its DPI scale, or null when no such monitor is connected.
    private Func<string?, (string Name, Area Area, double Scale)?>? battleScreenProbe;
    private (string Name, Area Area, double Scale)? BattleScreen(string? name) => (battleScreenProbe ??= FindScreen)(name);
    private double battleMonitorClock;
    private double battlePlacedScale;
    private Area battlePlacedArea;
    private Window? battleFieldWindow, battleHudWindow;
    private BattleFieldView? battleView;
    private BattleHud? battleHud;
    // The field and HUD redraw at most 30 times a second, only while something moves; a
    // click, pause or placement change asks for one redraw at once.
    public const double BattleFrameSeconds = 1.0 / 30;
    private double battleFrameClock;
    private bool battleRedraw, battleFieldIdle;
    public int BattleFieldDraws { get; private set; }
    private void RequestBattleRedraw() => battleRedraw = true;

    public BattleSim? Battle => battle;
    public bool BattleActive => battle != null;

    // Measurement only (scripts/perf.ps1 -Battle): PET_BATTLE_AUTOSTART=run keeps a battle going
    // (a new one after each end), =paused keeps a paused one open. Unset in normal use, where a
    // battle never starts by itself.
    private readonly string? battleAutostart = Environment.GetEnvironmentVariable("PET_BATTLE_AUTOSTART") is "run" or "paused"
        ? Environment.GetEnvironmentVariable("PET_BATTLE_AUTOSTART") : null;
    private double battleAutostartTimer;

    private void TickBattleAutostart(double dt)
    {
        if (battleAutostart == null || battle != null) return;
        battleAutostartTimer += dt;
        if (battleAutostartTimer < 1) return;
        battleAutostartTimer = 0;
        if (StartBattle() == null && battleAutostart == "paused") SetBattlePaused(true);
    }

    private void InitializeBattle(ContextMenu menu)
    {
        var root = PetUi.MenuItem(PetUi.Battle, Brushes.SeaGreen);
        // Starting opens the setup panel (weapon, difficulty, guide, Ready); nothing else is chosen here.
        // Adventure: the chapter map (PetWindow.Adventure.cs); Start battle is the free battle as before.
        var adventure = new MenuItem();
        adventure.Click += (_, _) => { if (OpenAdventureMap() is string reason) ShowBattleNotice(reason); };
        var start = new MenuItem();
        start.Click += (_, _) => { if (OpenBattlePrep() is string reason) ShowBattleNotice(reason); };
        var pause = new MenuItem();
        pause.Click += (_, _) => { if (battle != null) SetBattlePaused(!BattleUserPaused); };
        var end = new MenuItem();
        end.Click += (_, _) => EndBattle();
        root.Items.Add(adventure); root.Items.Add(start); root.Items.Add(pause); root.Items.Add(end);
        menu.Items.Add(root);
        menu.Opened += (_, _) =>
        {
            // Opening the menu pauses the battle; closing removes only that reason. (Opened can
            // arrive after a quick close, so only a menu that is still open counts.)
            if (battle != null && menu.IsOpen) SetBattlePause(BattlePause.Menu, true);
            bool userPaused = battle != null && BattleUserPaused;
            root.Header = L("Chế độ chiến đấu", "Battle Mode");
            string? refusal = battle == null ? BattlePrepRefusal() : null;
            start.Header = L("Bắt đầu trận", "Start battle") + (refusal != null ? $" ({refusal.TrimEnd('.')})" : "");
            start.IsEnabled = refusal == null;
            start.Visibility = adventure.Visibility = battle == null ? Visibility.Visible : Visibility.Collapsed;
            adventure.Header = L("Phiêu lưu", "Adventure") + (refusal != null ? $" ({refusal.TrimEnd('.')})" : "");
            adventure.IsEnabled = refusal == null;
            pause.Header = userPaused ? L("Tiếp tục trận", "Resume battle") : L("Tạm dừng trận", "Pause battle");
            pause.IsEnabled = !(userPaused && context.Fullscreen);
            pause.Visibility = end.Visibility = battle != null ? Visibility.Visible : Visibility.Collapsed;
            end.Header = L("Kết thúc trận", "End battle");
        };
        menu.Closed += (_, _) => SetBattlePause(BattlePause.Menu, false);
    }

    // Adds or removes one reason; the battle runs only when none is left.
    private void SetBattlePause(BattlePause reason, bool on)
    {
        var before = battlePause;
        battlePause = on ? battlePause | reason : battlePause & ~reason;
        // Closed can arrive a moment after the menu is gone: a closed menu holds nothing.
        if (ContextMenu?.IsOpen != true) battlePause &= ~BattlePause.Menu;
        if (battle == null) { battlePause = BattlePause.None; return; }
        battle.Paused = battlePause != BattlePause.None;
        if (battlePause == before) return;
        RequestBattleRedraw();
        UpdateBattleHud();
    }

    public BattleDifficulty BattleDifficultyChoice =>
        Enum.TryParse<BattleDifficulty>(preferences.BattleDifficulty, out var level) ? level : BattleDifficulty.Normal;

    public void SetBattleDifficulty(BattleDifficulty level)
    {
        if (level == BattleDifficultyChoice) return;
        preferences.BattleDifficulty = level.ToString();
        SavePreferences();
    }

    // The remembered weapon's id in the current data. An old or removed id (older preferences
    // said "Sword"/"Hammer"; ids ignore case) falls back to the data's first weapon.
    public string BattleWeaponChoice => BattleTuning.Weapon(preferences.BattleWeapon).Id;

    // Only a weapon the current data has is remembered.
    public bool SetBattleWeapon(string weapon)
    {
        if (BattleTuning.FindWeapon(weapon) is not WeaponSpec found) return false;
        if (preferences.BattleWeapon == found.Id) return true;
        preferences.BattleWeapon = found.Id;
        SavePreferences();
        return true;
    }

    private bool English => preferences.Language == "en";
    private string WeaponName(WeaponSpec weapon) => weapon.Name(English);

    private string DifficultyName(BattleDifficulty level) => level switch
    {
        BattleDifficulty.Easy => L("Dễ", "Easy"), BattleDifficulty.Hard => L("Khó", "Hard"), _ => L("Thường", "Normal")
    };

    // Best battles per difficulty (battle-records.json). Only the real pet reads and writes the
    // file; a window built with given preferences (tests) keeps them in memory.
    private BattleRecords? battleRecords;
    public BattleRecords BattleRecords => battleRecords ??= persistPreferences ? BattleRecords.Load() : new();

    // This session's finished battles, sent with each observation once the battle is over.
    public AI.BattleReport BattleReport { get; private set; } = new(0, 0, 0, 0);

    // Null when a battle may start now; otherwise the reason, for the user.
    public string? BattleRefusal()
    {
        if (battle != null) return L("Trận đang diễn ra.", "A battle is already running.");
        if (closed) return L("Mèo đã đóng.", "The pet is closed.");
        if (paused) return L("Mèo đang tạm dừng.", "The pet is paused.");
        if (context.Fullscreen) return L("Đang có ứng dụng toàn màn hình.", "A fullscreen app is open.");
        if (pet.Emotions.SleepLocked || pet.Action == PetAction.Sleeping || behavior.HoldSleep) return L("Mèo đang ngủ.", "The pet is asleep.");
        if (pet.Action == PetAction.Waking) return L("Mèo đang thức dậy.", "The pet is waking up.");
        if (dragging || pressPending || pet.Action == PetAction.Dragging) return L("Mèo đang bị kéo.", "The pet is being dragged.");
        if (!pet.Grounded || PetActionRules.IsInFlight(pet.Action) || pet.Action == PetAction.Climbing) return L("Mèo đang bay hoặc đang leo.", "The pet is in the air or climbing.");
        if (pet.LedgeId != 0) return L("Mèo đang đứng trên cửa sổ, hãy đưa mèo xuống sàn.", "The pet is on a window; put it on the floor first.");
        if (toyWindow != null || laserWindow != null || pet.TreatRemaining > 0 || treatSatisfiedRemaining > 0)
            return L("Mèo đang chơi hoặc đang ăn.", "The pet is playing or eating.");
        if (pet.Emotions.BlocksAutonomy) return L("Mèo đang phản ứng, thử lại sau giây lát.", "The pet is reacting; try again in a moment.");
        return null;
    }

    // Starts the fight at once with the given (or remembered) weapon and difficulty: the setup
    // panel's Ready remembers its choice first, Play again passes the last battle's. The battle
    // keeps this data and loadout to its end. Null on success, otherwise why it cannot start
    // (nothing changes then).
    // stage: an Adventure stage (its waves and goal; the run is counted in the progress at its end).
    public string? StartBattle(string? weapon = null, BattleDifficulty? level = null, StageSpec? stage = null)
    {
        if (BattleRefusal() is string reason) return reason;
        CloseBattlePrep();
        CloseAdventureMap();
        CloseBattleSummary();
        CancelFeatureActivity();
        queuedCommand = null;
        revision++; // Any AI reply chosen before the battle is now stale.
        pet.Speech.Hide(); pet.Thought.Clear();
        battleArea = bounds;
        battleMonitor = CatMonitor();
        battleScale = scale;
        battlePause = BattlePause.None;
        battleMonitorClock = 0;
        battle = new BattleSim(BattleTuning, battleArea.Left, battleArea.Right, difficulty: level is BattleDifficulty chosen && Enum.IsDefined(chosen) ? chosen : BattleDifficultyChoice,
            weapon: BattleTuning.Weapon(weapon ?? preferences.BattleWeapon).Id, stage: stage);
        battleRunId = Guid.NewGuid().ToString("N");
        battleCustomData = BattleDataCustom;
        pet.SwordOut = true;
        pet.Hammer = battle.Weapon.Style == "hammer";
        pet.SwordHitAt = battle.Weapon.HitFraction;
        pet.MoveScale = battle.Settings.CatRunScale;
        pet.Emotions.SetSuspended(true);
        pet.Action = PetAction.Idle;
        pet.VX = pet.SmoothedWalkVelocity = 0;
        behavior.Reset(pet);
        ShowBattleWindows();
        // The guide belongs to the setup panel; one still open would hold the battle.
        if (BattleTutorialOpen) SetBattlePause(BattlePause.Tutorial, true);
        System.Diagnostics.Trace.WriteLine(stage != null ? $"Battle started: Adventure stage {stage.Id}." : "Battle started.");
        PlayPetSound("excited");
        return null;
    }

    // Removes every battle object and gives the pet back to its normal life. A pet that is
    // being dragged or is in the air keeps that state; input and physics finish it.
    public void EndBattle()
    {
        if (battle == null) return;
        battle = null;
        CloseBattleUpgrade();
        battlePause = BattlePause.None;
        battleFieldWindow?.Close(); battleHudWindow?.Close();
        battleFieldWindow = battleHudWindow = null;
        battleView = null;
        battleHud = null;
        pet.SwordOut = false; pet.Hammer = false; pet.SwordSwing = -1; pet.SwordCombo = false; pet.BattleHurt = 0; pet.MoveScale = 1;
        battleHudDodging = false;
        pet.BattleDazed = pet.BattleCheer = pet.BattleShield = pet.BattleBuff = false;
        pet.Emotions.SetSuspended(false);
        revision++; // Nothing requested during the battle may run late.
        behavior.Reset(pet);
        if (!dragging && !pressPending && pet.Grounded && pet.Action is PetAction.Idle or PetAction.Walking or PetAction.Sitting)
        {
            pet.Action = PetAction.Idle;
            pet.VX = pet.SmoothedWalkVelocity = 0;
        }
        System.Diagnostics.Trace.WriteLine("Battle ended.");
        if (!closed) Present();
    }

    // The user's pause (menu or HUD). Resuming waits until no fullscreen app is open, and only
    // removes the user's reason: an open menu or a cat on another monitor still holds the battle.
    public bool SetBattlePaused(bool value)
    {
        if (battle == null || (!value && context.Fullscreen)) return false;
        SetBattlePause(BattlePause.User, value);
        return true;
    }

    // Brings the battle's view of the cat up to date before a skill is used, so the HUD and
    // BattleSim judge "the cat is in the user's hands" by the same rule.
    private void SyncBattleCatState() => battle?.SetCatState(BattleHoldsOff, BattleLifted);

    public bool ArmBattlePush(bool armed)
    {
        if (battle == null || (armed && battle.Paused)) return false;
        SyncBattleCatState();
        bool done = battle.ArmPush(armed);
        RequestBattleRedraw();
        UpdateBattleHud();
        return done;
    }

    // A skill slot on the HUD: an aimed skill (Push, Bomb) is chosen or un-chosen and waits for
    // a slime click; the others (Heal, Cheer, Shield) act at once.
    public bool UseBattleSkill(BattleSkill skill)
    {
        if (battle == null || battle.Paused) return false;
        SyncBattleCatState();
        bool done = BattleSim.IsAimed(skill) ? battle.Arm(battle.Armed == skill ? null : skill) : battle.Use(skill, pet.X + settings.Width / 2);
        RequestBattleRedraw();
        UpdateBattleHud();
        battleView?.InvalidateVisual();
        return done;
    }

    // A click on a slime: with a skill chosen it uses it there (the target stays as it was),
    // otherwise it marks the slime as the cat's target. Marking deals no damage.
    // offset: where on the slime the click landed (DIPs from its centre); it sets Push's direction.
    public bool BattleClickSlime(int id, double offset = 0)
    {
        if (battle == null || battle.Paused) return false;
        SyncBattleCatState();
        bool done = battle.Armed is BattleSkill skill
            ? battle.Use(skill, id, pet.X + settings.Width / 2, skill == BattleSkill.Push ? BattleSim.PushDirection(offset) : null)
            : battle.Select(id);
        RequestBattleRedraw();
        UpdateBattleHud();
        battleView?.InvalidateVisual();
        return done;
    }

    // The battle may not move or hurt the cat: pressed, dragged, in the air or climbing.
    private bool BattleHoldsOff => dragging || pressPending || !pet.Grounded ||
        pet.Action is PetAction.Dragging or PetAction.Climbing || PetActionRules.IsInFlight(pet.Action);
    // Really off the ground (a drag past the threshold, or a throw/fall): not a plain click.
    private bool BattleLifted => dragging || !pet.Grounded || pet.Action is PetAction.Dragging or PetAction.Thrown;

    // A real drag just began: a chosen skill is dropped and the HUD shows it at once.
    private void BattleCatLifted()
    {
        if (battle == null) return;
        SyncBattleCatState();
        RequestBattleRedraw();
        UpdateBattleHud();
    }

    // Every unpaused frame, before physics.
    private void TickBattle(double dt)
    {
        if (battle == null) return;
        // A fullscreen app pauses the battle; when it closes the user's pause stays until Resume.
        if (context.Fullscreen) SetBattlePause(BattlePause.Fullscreen | BattlePause.User, true);
        else SetBattlePause(BattlePause.Fullscreen, false);
        if (ContextMenu?.IsOpen != true) SetBattlePause(BattlePause.Menu, false); // A menu reason never outlives the menu.
        battleMonitorClock += dt;
        if (battleMonitorClock >= 1) { battleMonitorClock = 0; RecoverLostBattleMonitor(); }
        bool held = BattleHoldsOff;
        if (!held) KeepBattleOnItsMonitor();
        var order = battle.Step(dt, pet.X + settings.Width / 2, held, BattleLifted);
        PlayBattleSound();
        ShowBattleBanners();
        battle.ClearEvents();
        if (battle.Finished)
        {
            bool won = battle.Phase == BattlePhase.Won;
            // A free battle goes to the battle records; an Adventure stage to the Adventure progress only.
            var outcome = CountStageRun(battle, won);
            var news = battle.Stage != null ? outcome.News : BattleRecords.Add(battle.Weapon.Id, battle.Difficulty, battleCustomData, won, battle.Defeated, battle.FightSeconds);
            if (battle.Stage == null && persistPreferences) BattleRecords.Save();
            BattleReport = BattleReport with
            {
                Won = BattleReport.Won + (won ? 1 : 0), Lost = BattleReport.Lost + (won ? 0 : 1),
                Combos = BattleReport.Combos + battle.CombosLanded, Records = BattleReport.Records + (news.Any ? 1 : 0)
            };
            var result = new BattleSummary(won, battle.Defeated, battle.FightSeconds, battle.CatHp, battle.Settings.CatMaxHp, battle.BossDefeated,
                battle.DefeatedBoss?.Name(English), battle.Difficulty, news, battle.Weapon.Id, WeaponName(battle.Weapon), battleCustomData, battle.Weapon.Style,
                battle.Stage, outcome, !battleCustomData, UpgradeSummary(battle));
            EndBattle();
            ShowBattleSummary(result);
            return;
        }
        TickBattleUpgrade();
        // Quick feet: this run's running speed.
        pet.MoveScale = battle.Settings.CatRunScale * battle.MoveFactor;
        pet.SwordSwing = battle.SwingProgress;
        pet.SwordCombo = battle.ComboSwing;
        pet.BattleHurt = battle.HurtFlash;
        pet.BattleCheer = battle.Phase == BattlePhase.Won;
        pet.BattleDazed = battle.Phase == BattlePhase.Lost;
        pet.BattleShield = battle.ShieldRemaining > 0;
        pet.BattleBuff = battle.CheerRemaining > 0;
        pet.BattleTime = battle.Time;
        if (!held)
        {
            if (order.Facing != 0) pet.Facing = order.Facing;
            double target = Math.Clamp(order.X - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
            if (order.Command == CatCommand.Move && !battle.Paused && Math.Abs(target - pet.X) >= 6) WalkToward(target);
            else
            {
                pet.Action = battle.Over ? PetAction.Sitting : PetAction.Idle;
                pet.VX = pet.SmoothedWalkVelocity = 0;
            }
        }
        UpdateBattleWindows(dt);
    }

    // The cat landed on another monitor: pause and say where the battle is. Back on the battle's
    // monitor: resume that pause. On its own monitor a changed work area (taskbar, resolution,
    // DPI) moves the field with it.
    private void KeepBattleOnItsMonitor()
    {
        if (battle == null) return;
        if (CatMonitor() != battleMonitor)
        {
            if (BattleCatAway) return;
            SetBattlePause(BattlePause.Away, true);
            ShowBattleNotice(L("Chiến trường ở màn hình khác — đưa mèo về đó để đánh tiếp.", "The battlefield is on another screen — bring the cat back to go on."));
            return;
        }
        if (BattleCatAway)
        {
            SetBattlePause(BattlePause.Away, false); // Runs again only if nothing else holds it.
            CloseBattleNotice();
        }
        if (bounds != battleArea)
        {
            battleArea = bounds;
            battleScale = scale;
            battle.SetArea(bounds.Left, bounds.Right);
        }
    }

    // The battle's monitor was disconnected: the cat could never be brought back to it. The
    // field and HUD move to the cat's monitor (the primary one if that cannot be found), every
    // slime, bomb and effect keeps its relative spot inside the new floor, and HP, wave and
    // cooldowns stay. The battle stays paused until the user resumes. The cat itself is not moved
    // (a drag in progress keeps its capture). Checked once a second, from TickBattle.
    private void RecoverLostBattleMonitor()
    {
        if (battle == null || BattleScreen(battleMonitor) != null) return;
        if ((BattleScreen(CatMonitor()) ?? BattleScreen(null)) is not { } screen) return;
        System.Diagnostics.Trace.WriteLine($"Battle monitor {battleMonitor} is gone; the battle moves to {screen.Name}.");
        battleMonitor = screen.Name;
        battleArea = screen.Area;
        battleScale = screen.Scale;
        battle.SetArea(screen.Area.Left, screen.Area.Right);
        battlePlacedScale = 0; // Re-place the windows in the new monitor's pixels.
        SetBattlePause(BattlePause.User, true);
        SetBattlePause(BattlePause.Away, false); // The notice below replaces the "come back" one.
        ShowBattleNotice(L("Màn hình chiến trường đã ngắt — trận chuyển sang màn hình này. Bấm Tiếp tục để đánh tiếp.",
            "The battle's screen was disconnected — the battle moved here. Press Resume to go on."));
    }

    private (string Name, Area Area, double Scale)? FindScreen(string? name)
    {
        var screen = name == null ? Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens.FirstOrDefault()
            : Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == name);
        if (screen == null) return null;
        double at = MonitorScale(screen);
        var work = screen.WorkingArea;
        return (screen.DeviceName, new Area(work.Left / at, work.Top / at, work.Right / at, work.Bottom / at), at);
    }

    private Window CreateBattleWindow(UIElement content, string title, bool clickThrough = false)
    {
        var window = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true,
            Background = Brushes.Transparent, Topmost = true, ShowInTaskbar = false, ShowActivated = false,
            Focusable = false, Content = content, Title = title, Left = -10000, Top = -10000, Width = 10, Height = 10
        };
        window.SourceInitialized += (_, _) =>
        {
            // Clickable (slimes, HUD buttons) but never activated: typing elsewhere is untouched.
            ((HwndSource)PresentationSource.FromVisual(window)).AddHook(WindowHook);
            var handle = new WindowInteropHelper(window).Handle;
            const int ExStyle = -20;
            const long ToolWindow = 0x80, NoActivate = 0x08000000, Transparent = 0x20;
            SetWindowLongPtr(handle, ExStyle, new IntPtr(GetWindowLongPtr(handle, ExStyle).ToInt64() | ToolWindow | NoActivate | (clickThrough ? Transparent : 0)));
        };
        return window;
    }

    private void ShowBattleWindows()
    {
        if (battle == null) return;
        battleView = new BattleFieldView(battle);
        battleView.SlimeClicked += (id, offset) => BattleClickSlime(id, offset);
        battleView.RightClicked += () => { battle?.Arm(null); RequestBattleRedraw(); UpdateBattleHud(); }; // Right-click on the field cancels the skill.
        battleFieldWindow = CreateBattleWindow(battleView, "Pet battle");
        battleFieldWindow.DpiChanged += (_, _) => battlePlacedScale = 0; // Re-place in pixels next frame.
        battleHudWindow = CreateBattleWindow(BuildBattleHud(), "Pet battle HUD");
        battleHudWindow.SizeToContent = SizeToContent.WidthAndHeight;
        battlePlacedScale = 0;
        battleFrameClock = 0; battleFieldIdle = false; BattleFieldDraws = 0;
        RequestBattleRedraw();
        battleFieldWindow.Show();
        battleHudWindow.Show();
        UpdateBattleWindows();
    }

    private void UpdateBattleWindows(double dt = 0)
    {
        if (battle == null || battleFieldWindow == null || battleHudWindow == null || battleView == null) return;
        // A fullscreen app (game, video) gets a clean screen; the paused battle comes back after.
        if (context.Fullscreen)
        {
            if (battleFieldWindow.IsVisible) { battleFieldWindow.Hide(); battleHudWindow.Hide(); }
            return;
        }
        if (!battleFieldWindow.IsVisible) { battleFieldWindow.Show(); battleHudWindow.Show(); battlePlacedScale = 0; RequestBattleRedraw(); }
        var area = battleArea;
        double fieldScale = battleScale;
        if (battlePlacedScale != fieldScale || battlePlacedArea != area)
        {
            battlePlacedScale = fieldScale; battlePlacedArea = area;
            RequestBattleRedraw();
            battleView.OriginX = area.Left;
            var handle = new WindowInteropHelper(battleFieldWindow).Handle;
            const uint NoZOrder = 0x0004, NoActivate = 0x0010;
            SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(area.Left * fieldScale), (int)Math.Round((area.Bottom - BattleFieldView.FieldHeight) * fieldScale),
                (int)Math.Round((area.Right - area.Left) * fieldScale), (int)Math.Round(BattleFieldView.FieldHeight * fieldScale), NoZOrder | NoActivate);
            // The pet stays above its battlefield, so clicks on the cat still reach the cat.
            const uint NoMove = 0x0002, NoSize = 0x0001;
            SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), 0, 0, 0, 0, NoMove | NoSize | NoActivate);
        }
        // Paused, or nothing on the floor (wave breaks): the strip is drawn once and left alone.
        var b = battle;
        bool moving = !b.Paused && (b.Slimes.Count > 0 || b.Effects.Count > 0 || b.Bombs.Count > 0);
        battleFrameClock += dt;
        bool due = battleFrameClock >= BattleFrameSeconds;
        if (due) battleFrameClock = Math.Min(battleFrameClock - BattleFrameSeconds, BattleFrameSeconds);
        if (battleRedraw || (moving && due) || (!moving && !battleFieldIdle))
        {
            battleView.InvalidateVisual();
            BattleFieldDraws++;
            battleFieldIdle = !moving;
        }
        if (battleRedraw || (due && !b.Paused)) UpdateBattleHud();
        battleRedraw = false;
        double width = battleHudWindow.ActualWidth;
        MoveWindow(battleHudWindow, (area.Left + area.Right - width) / 2, area.Top + 12, fieldScale);
        UpdateBattleHudDodge();
    }

    // The HUD steps aside for the cursor: over its information part (portrait, HP, status, hint)
    // it fades and lets clicks through to the app below; over a skill slot or Pause/End it is
    // solid and clickable as usual. While click-through the HUD gets no mouse messages, so the
    // cursor is read every frame instead.
    public const double BattleHudDodgeOpacity = 0.25;
    private bool battleHudDodging;
    public bool BattleHudDodging => battleHudDodging;
    // Replaceable for UI checks: the cursor in screen pixels.
    private Func<Point>? battleCursorProbe;

    private void UpdateBattleHudDodge()
    {
        if (battleHudWindow == null || battleHud == null || !battleHudWindow.IsVisible) return;
        var handle = new WindowInteropHelper(battleHudWindow).Handle;
        if (handle == IntPtr.Zero) return;
        Point cursor;
        if (battleCursorProbe != null) cursor = battleCursorProbe();
        else if (GetCursorPos(out var raw)) cursor = new Point(raw.X, raw.Y);
        else return;
        bool dodge = false;
        if (GetWindowRect(handle, out var rect) && cursor.X >= rect.Left && cursor.X < rect.Right && cursor.Y >= rect.Top && cursor.Y < rect.Bottom)
        {
            var local = battleHud.PointFromScreen(cursor);
            dodge = battleHud.IsInsidePanel(local) && !battleHud.IsInteractiveAt(local);
        }
        if (dodge == battleHudDodging) return;
        battleHudDodging = dodge;
        battleHud.Opacity = dodge ? BattleHudDodgeOpacity : 1;
        const int ExStyle = -20;
        const long Transparent = 0x20;
        long style = GetWindowLongPtr(handle, ExStyle).ToInt64();
        SetWindowLongPtr(handle, ExStyle, new IntPtr(dodge ? style | Transparent : style & ~Transparent));
    }

    // The game-style HUD (PetBattleHud.cs) with a still portrait of this cat.
    private UIElement BuildBattleHud()
    {
        var portrait = new PetRenderer(new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1 })
            { Coat = renderer.Coat, Accessory = renderer.Accessory };
        // The weapon of this battle next to the cat's name.
        battleHud = new BattleHud($"{preferences.Name} · {(battle != null ? WeaponName(battle.Weapon) : "")}", portrait, L);
        battleHud.SkillClicked += skill => UseBattleSkill(skill);
        battleHud.PauseClicked += () => { if (battle != null) SetBattlePaused(!BattleUserPaused); };
        battleHud.EndClicked += EndBattle;
        UpdateBattleHud();
        return battleHud;
    }

    private void UpdateBattleHud()
    {
        if (battle is not BattleSim b || battleHud == null) return;
        battleHud.Update(b, BattleUserPaused, context.Fullscreen, BattleLifted, BattleCatAway);
        if (battleView != null) battleView.Cursor = b.Armed != null ? Cursors.Cross : Cursors.Hand;
    }
}
