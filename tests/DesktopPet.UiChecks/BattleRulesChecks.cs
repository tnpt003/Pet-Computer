using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using DesktopPet;
using static PetPrivate;

// Battle Mode rules inside the real pet window: a paused battle holds the cat's physics too,
// pause reasons do not undo each other, lifting the cat cannot be used to dodge or to wait out
// cooldowns, and a disconnected battle monitor does not strand the battle. Monitors are faked
// with probes (no real monitor is plugged or unplugged); the real cursor is never moved.
internal static class BattleRulesChecks
{
    private static MouseButtonEventArgs MouseEvent(RoutedEvent routedEvent) => new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = routedEvent };
    private static int Notices() => Application.Current.Windows.OfType<Window>().Count(w => w.Title == "Pet notice" && w.IsVisible);

    public static async Task Run(PetWindow window, PetModel pet, Action<bool, string> check)
    {
        const string Home = "\\\\.\\HOME", Other = "\\\\.\\OTHER", Next = "\\\\.\\NEXT";
        BattleSim Sim() => window.Battle!;
        var clock = Field<Stopwatch>(window, "clock");
        void TickBattle(double seconds) { for (double t = 0; t < seconds; t += 1.0 / 60) Call(window, "TickBattle", 1.0 / 60); }
        // One whole frame of the pet (input, battle, physics, drawing) of 1/60 s.
        void Frame() { SetField(window, "previous", clock.Elapsed.TotalSeconds - 1.0 / 60); Call(window, "Tick", null, EventArgs.Empty); }
        void Frames(int count) { for (int i = 0; i < count; i++) Frame(); }
        Area Floor() => Field<Area>(window, "bounds");
        void Ground() { pet.Grounded = true; pet.VX = pet.VY = 0; pet.Action = PetAction.Idle; pet.Y = Floor().Bottom - 112; }
        void Press() => window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        void DragPastThreshold() { SetField(window, "pressPosition", new Point(-100000, -100000)); Call(window, "CheckDragThreshold"); }
        void Release() => window.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonUpEvent));
        PetWindow.BattlePause Reasons() => window.BattlePauseReasons;

        // Fake monitors: the cat is on catOn; Home can be disconnected; Next is a mixed-DPI
        // monitor left of and above the primary one (negative coordinates, 150 %).
        string catOn = Home;
        bool homeConnected = true;
        var nextArea = new Area(-1280, -300, 0, 420);
        SetField(window, "battleMonitorProbe", new Func<string>(() => catOn));
        SetField(window, "battleScreenProbe", new Func<string?, (string, Area, double)?>(name => name switch
        {
            Home or null when homeConnected => (Home, Floor(), 1.0),
            Other => (Other, new Area(Floor().Right, Floor().Top, Floor().Right + 1600, Floor().Bottom), 1.0),
            Next or null => (Next, nextArea, 1.5),
            _ => null
        }));
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (false, 0)));
        Call(window, "TickContext", 1.1);
        window.Reset();
        window.BattleTuning = new BattleSettings { FirstWaveDelay = 0, Waves = new[] { 3 }, MaxAlive = 3, SpawnInterval = 0, SlimeMaxHp = 1000, SlimeDamage = 0, CatDamage = 0 };
        check(window.StartBattle() == null, "(setup) a battle starts");
        TickBattle(0.7);

        // 1. Pausing mid-throw: the cat hangs there with its velocity, then goes on.
        pet.Action = PetAction.Thrown; pet.Grounded = false; pet.Y = Floor().Bottom - 112 - 300; pet.VX = 400; pet.VY = -200;
        Frame();
        window.SetBattlePaused(true);
        var (x, y, vx, vy) = (pet.X, pet.Y, pet.VX, pet.VY);
        double time = Sim().Time, hp = Sim().CatHp;
        Frames(30);
        check(pet.X == x && pet.Y == y && pet.VX == vx && pet.VY == vy && pet.Action == PetAction.Thrown && Sim().Time == time,
            $"A paused battle holds a thrown cat in place with its velocity ({x:F1},{y:F1} → {pet.X:F1},{pet.Y:F1})");
        window.SetBattlePaused(false);
        Frame();
        check(Math.Abs(pet.X - x) <= Math.Abs(vx) * 0.05 && Math.Abs(pet.Y - y) <= 20 && pet.X != x && Sim().Time - time <= 0.1 + 1e-9 && Sim().CatHp == hp,
            $"After Resume the throw goes on from there: one normal frame, no jump or catch-up ({pet.X - x:F1}, {pet.Y - y:F1} DIPs; {Sim().Time - time:F3} s)");

        // 2. The pause holds the cat (user, 2026-10-23): it cannot be picked up, and its poses stop
        // (a stunned cat's stars and sway hold still).
        Ground(); TickBattle(1.0 / 60);
        pet.BattleStun = 1;
        window.SetBattlePaused(true);
        window.Activate();
        await Task.Delay(20);
        double pose = pet.AnimationTime, action = pet.ActionTime;
        Frames(20);
        check(pet.AnimationTime == pose && pet.ActionTime == action, "A paused battle freezes the cat's poses (a stunned cat does not sway)");
        Press(); DragPastThreshold();
        check(pet.Action != PetAction.Dragging && !window.IsMouseCaptured && !Field<bool>(window, "pressPending") && Sim().Paused,
            "The cat cannot be picked up while the battle is paused");
        Release();
        window.SetBattlePaused(false);
        Frames(2);
        check(pet.AnimationTime > pose, "Resume: the poses go on");
        Press(); DragPastThreshold();
        check(pet.Action == PetAction.Dragging, "After Resume it can be picked up again");
        Release(); Ground(); TickBattle(1.0 / 60);
        pet.BattleStun = 0;

        // 2b. (2026-10-24) A stunned cat cannot be picked up until the stun ends; shaking it hard in a
        // battle never makes it Dizzy (the stars there mean a stun).
        var stunField = typeof(BattleSim).GetField("<CatStunRemaining>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        stunField.SetValue(Sim(), 1.0);
        Press(); DragPastThreshold();
        check(Sim().CatStunned && pet.Action != PetAction.Dragging && !window.IsMouseCaptured && !Field<bool>(window, "pressPending"),
            "A stunned cat cannot be picked up");
        Release();
        stunField.SetValue(Sim(), 0.0);
        Press(); DragPastThreshold();
        check(pet.Action == PetAction.Dragging, "Once the stun is over it can be picked up");
        double shake = 0;
        for (int i = 0; i < 90; i++)
        {
            pet.Pickup.Step((i % 2 == 0 ? 0 : 120), 0, 1.0 / 60);
            Call(window, "UpdateDrag", 1.0 / 60);
            shake = Math.Max(shake, pet.Pickup.Dizziness);
        }
        check(pet.Pickup.NoDizzy && shake < 0.02 && pet.Emotions.Current != PetEmotion.Dizzy, $"Shaking the cat hard in a battle never makes it Dizzy ({shake:0.00})");
        Release(); Ground(); TickBattle(1.0 / 60);

        // 3. Pause reasons stack: nothing resumes while another reason holds the battle.
        window.SetBattlePaused(true);
        window.ContextMenu!.IsOpen = true;
        await Task.Delay(20);
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (true, 0)));
        Call(window, "TickContext", 1.1); TickBattle(1.0 / 60);
        window.ContextMenu.IsOpen = false;
        await Task.Delay(20);
        TickBattle(1.0 / 60);
        check(Sim().Paused && Reasons() == (PetWindow.BattlePause.User | PetWindow.BattlePause.Fullscreen), $"User pause + menu + fullscreen: closing the menu keeps the battle paused ({Reasons()})");
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (false, 0)));
        Call(window, "TickContext", 1.1); TickBattle(1.0 / 60);
        check(Sim().Paused && Reasons() == PetWindow.BattlePause.User, "When the fullscreen app closes the battle still waits for the user");
        check(window.SetBattlePaused(false) && !Sim().Paused, "Resume");
        window.ContextMenu.IsOpen = true;
        await Task.Delay(20);
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (true, 0)));
        Call(window, "TickContext", 1.1); TickBattle(1.0 / 60);
        window.ContextMenu.IsOpen = false;
        await Task.Delay(20);
        SetField(window, "contextProbe", new Func<(bool, double)>(() => (false, 0)));
        Call(window, "TickContext", 1.1); TickBattle(1.0 / 60);
        check(Sim().Paused, "A fullscreen app that starts while the menu is open: closing the menu, then the app, still needs Resume");
        window.SetBattlePaused(false);
        var hud = Field<BattleHud>(window, "battleHud");
        catOn = Other; TickBattle(1.0 / 60);
        hud.PauseButton.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        check(Reasons() == (PetWindow.BattlePause.Away | PetWindow.BattlePause.User), $"The HUD's Pause while the cat is away adds the user's pause (it used to resume) ({Reasons()})");
        catOn = Home; TickBattle(1.0 / 60);
        check(Sim().Paused && Reasons() == PetWindow.BattlePause.User, "Back on the battle's monitor the user's pause stays");
        hud.PauseButton.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        check(!Sim().Paused, "The HUD's button resumes it");
        catOn = Other; TickBattle(1.0 / 60);
        window.ContextMenu.IsOpen = true; await Task.Delay(20);
        window.ContextMenu.IsOpen = false; await Task.Delay(20);
        TickBattle(1.0 / 60);
        check(Sim().Paused && Reasons() == PetWindow.BattlePause.Away, "Closing the menu does not resume a battle waiting for the cat");
        catOn = Home; TickBattle(1.0 / 60);
        check(!Sim().Paused, "The cat back home: it runs again");
        // Pausing the whole pet stops the battle and the cat too; un-pausing leaves the battle's own state.
        pet.Action = PetAction.Thrown; pet.Grounded = false; pet.Y = Floor().Bottom - 412; pet.VX = 300; pet.VY = 0;
        window.SetPaused(true);
        double petX = pet.X, petY = pet.Y; time = Sim().Time;
        Frames(20);
        check(pet.X == petX && pet.Y == petY && Sim().Time == time, "Pausing the whole pet freezes the battle and the cat");
        window.SetPaused(false);
        Frame();
        check(!Sim().Paused && Sim().Time > time && pet.Y != petY, "Un-pausing the pet continues a battle that was running");
        Ground(); TickBattle(1.0 / 60);

        // 4. Rescue protection from the window: a click is no rescue, a second drop too soon gives nothing.
        window.EndBattle(); window.Reset();
        window.StartBattle();
        TickBattle(0.7);
        check(window.UseBattleSkill(BattleSkill.Bomb) && Sim().Armed == BattleSkill.Bomb, "(setup) the bomb is chosen");
        window.Activate(); await Task.Delay(20);
        Press();
        TickBattle(0.1);
        var hudNow = Field<BattleHud>(window, "battleHud");
        bool slotsDuringPress = hudNow.Slot(BattleSkill.Cheer)!.IsReady;
        Release();
        TickBattle(1.0 / 60);
        check(Sim().Armed == BattleSkill.Bomb && Sim().Grace == 0 && Sim().RescueGraceReady && slotsDuringPress,
            "A plain click on the cat keeps the chosen skill, leaves the HUD as it was and gives no protection");
        Press(); DragPastThreshold();
        check(Sim().Armed == null && !window.UseBattleSkill(BattleSkill.Cheer) && !window.ArmBattlePush(true) && !hudNow.Slot(BattleSkill.Cheer)!.IsReady,
            "A real drag drops the chosen skill at once; skills cannot be used and the HUD shows them waiting");
        TickBattle(1);
        Release();
        check(!window.UseBattleSkill(BattleSkill.Cheer), "Still none while the cat flies");
        TickBattle(0.2);
        Ground(); TickBattle(1.0 / 60);
        check(Sim().Grace > 1.4 && window.UseBattleSkill(BattleSkill.Cheer), "Landing: protection, and the skills work again");
        TickBattle(0.3);
        double graceLeft = Sim().Grace;
        Press(); DragPastThreshold(); TickBattle(0.2); Release(); Ground(); TickBattle(1.0 / 60);
        check(Sim().Grace <= graceLeft && !Sim().RescueGraceReady, $"A second rescue within the cooldown gives no fresh protection ({graceLeft:F2} → {Sim().Grace:F2})");

        // 5. The battle's monitor is disconnected while the cat is being carried.
        window.EndBattle(); window.Reset();
        catOn = Home; homeConnected = true;
        window.StartBattle();
        TickBattle(0.7);
        window.UseBattleSkill(BattleSkill.Shield);
        double shieldCooldown = Sim().Cooldown(BattleSkill.Shield);
        hp = Sim().CatHp;
        int wave = Sim().Wave, slimes = Sim().Slimes.Count;
        window.Activate(); await Task.Delay(20);
        Press(); DragPastThreshold();
        bool captured = window.IsMouseCaptured;
        double carriedX = pet.X, carriedY = pet.Y;
        catOn = Next; homeConnected = false;
        TickBattle(1.1);
        var battleArea = Field<Area>(window, "battleArea");
        check(Field<string>(window, "battleMonitor") == Next && battleArea == nextArea && Field<double>(window, "battleScale") == 1.5,
            "The disconnected monitor's battle moves to the cat's monitor (its own area and DPI)");
        check(Sim().Paused && Reasons() == PetWindow.BattlePause.User && Notices() == 1, $"It stays paused for the user to resume, with one note ({Reasons()})");
        check(pet.Action == PetAction.Dragging && window.IsMouseCaptured == captured && pet.X == carriedX && pet.Y == carriedY,
            "The carried cat is not moved and keeps the mouse");
        check(Sim().CatHp == hp && Sim().Wave == wave && Sim().Slimes.Count == slimes && Sim().Cooldown(BattleSkill.Shield) == shieldCooldown,
            "HP, wave, slimes and cooldowns are kept");
        check(Sim().Left == nextArea.Left && Sim().Right == nextArea.Right &&
              Sim().Slimes.All(s => s.X - s.Width / 2 >= nextArea.Left - 0.01 && s.X + s.Width / 2 <= nextArea.Right + 0.01) &&
              Sim().Effects.All(e => e.X >= nextArea.Left && e.X <= nextArea.Right) && Sim().Bombs.All(b => b.X >= nextArea.Left && b.X <= nextArea.Right),
            "Every slime, bomb and effect lands inside the new floor (negative coordinates, 150 %)");
        Call(window, "UpdateBattleWindows", 0.0);
        check(Field<Area>(window, "battlePlacedArea") == nextArea && Field<double>(window, "battlePlacedScale") == 1.5, "The field and HUD are placed on the new monitor");
        TickBattle(2);
        check(Field<string>(window, "battleMonitor") == Next && Notices() <= 1, "Nothing moves again or repeats the note afterwards");
        Release();
        Ground();
        homeConnected = true;

        // The battle's monitor still exists: the cat away is only waited for, as before.
        window.EndBattle(); window.Reset();
        catOn = Home;
        window.StartBattle();
        TickBattle(0.2);
        catOn = Other;
        TickBattle(1.5);
        check(Field<string>(window, "battleMonitor") == Home && Reasons() == PetWindow.BattlePause.Away, "With the battle's monitor still there, a cat on another one is waited for");
        catOn = Home;
        TickBattle(1.0 / 60);
        check(!Sim().Paused, "and the battle runs again when it is back");

        // 6. Pausing the whole pet (tray/menu Pause) also holds the battle against clicks on its
        // field and HUD, which stay on screen; un-pausing removes only that reason.
        window.EndBattle(); window.Reset();
        window.StartBattle();
        TickBattle(0.7);
        var target = Sim().Slimes.First(s => s.Targetable);
        window.SetPaused(true);
        var appHud = Field<BattleHud>(window, "battleHud");
        time = Sim().Time; hp = Sim().CatHp;
        appHud.Slot(BattleSkill.Shield)!.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        appHud.Slot(BattleSkill.Bomb)!.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        check(Sim().Paused && Reasons() == PetWindow.BattlePause.App && !appHud.Slot(BattleSkill.Shield)!.IsReady,
            $"Pausing the pet pauses the battle; the HUD shows the skills unavailable ({Reasons()})");
        check(Sim().ShieldRemaining == 0 && Sim().Cooldown(BattleSkill.Shield) == 0 && Sim().Armed == null &&
              !window.UseBattleSkill(BattleSkill.Cheer) && !window.ArmBattlePush(true) && !window.BattleClickSlime(target.Id) && Sim().SelectedId == null &&
              Sim().Bombs.Count == 0 && Sim().Time == time && Sim().CatHp == hp,
            "While the pet is paused no skill, bomb or target can be used (HUD clicks and calls alike); nothing in the battle changes");
        window.SetBattlePaused(true);
        window.SetPaused(false);
        check(Sim().Paused && Reasons() == PetWindow.BattlePause.User, "Un-pausing the pet keeps the battle's own pause");
        window.SetBattlePaused(false);
        TickBattle(0.1);
        check(!Sim().Paused && Sim().Time > time && Sim().Time - time <= 7.0 / 60 + 1e-9 && window.UseBattleSkill(BattleSkill.Shield),
            "Then it runs on without catching up, and skills work");
        window.SetPaused(true);
        appHud.EndButton.RaiseEvent(MouseEvent(UIElement.MouseLeftButtonDownEvent));
        check(!window.BattleActive, "The HUD's End still ends the battle while the pet is paused");
        window.SetPaused(false);

        window.EndBattle();
        SetField(window, "battleMonitorProbe", null);
        SetField(window, "battleScreenProbe", null);
        window.Reset();
        window.BattleTuning = new BattleSettings();

        // 7. "Bring the pet to the main screen" (tray and menu): a puff where the cat is, then it pops
        // up in the middle of the main screen, falls in and is surprised; it cannot be grabbed meanwhile.
        var home = Floor();
        double middle = (home.Left + home.Right - 112) / 2, start = home.Left + 30;
        pet.X = start; Ground(); pet.Emotions.Reset();
        window.BringHome();
        Frames(6);
        check(window.CatTeleporting && pet.X == start, "Bring home: first the cat vanishes in a puff where it is");
        Press();
        check(!window.IsMouseCaptured && pet.Action != PetAction.Dragging, "It cannot be picked up during the puff");
        Release();
        Frames(20);
        check(window.CatTeleporting && Math.Abs(pet.X - middle) < 1, $"Then it pops up in the middle of the main screen ({pet.X:F0} vs {middle:F0})");
        Frames(40);
        check(!window.CatTeleporting && Math.Abs(pet.X - middle) < 1 && pet.Grounded && pet.Emotions.Target == PetEmotion.Surprised,
            $"It lands there, surprised ({pet.Emotions.Target})");
        pet.Emotions.Reset();
        window.SetPaused(true);
        pet.X = start;
        window.BringHome();
        check(!window.CatTeleporting && Math.Abs(pet.X - middle) < 1, "Paused, it is brought straight there (the rescue always works)");
        window.SetPaused(false);
    }
}
