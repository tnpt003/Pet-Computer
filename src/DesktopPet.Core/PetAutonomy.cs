using System;

namespace DesktopPet;

// What can stop the cat from doing something right now. PetWindow works out only
// the conditions a kind asks for (PetWindow.Autonomy.cs).
[Flags]
public enum AutoBlock
{
    None = 0,
    Paused = 1,
    Held = 2,           // Being dragged, or a press that may still become a drag.
    Unsettled = 4,      // Asleep, waking, in flight, eating, or an emotion that owns the cat (PetActionRules.IsSettled, BlocksAutonomy).
    Quiet = 8,          // The saved Quiet setting.
    Fullscreen = 16,    // A fullscreen app on the cat's monitor (acts like Quiet without changing it).
    Battle = 32,        // A fight is running (PetBattle.cs owns the cat).
    BattleScreens = 64, // The battle hub, setup panel or a map is open.
    Feature = 128,      // Another feature already owns the cat's movement (treat, ball, keepsake, ledge plan...).
    Away = 256,         // The user has not touched the computer for a while.
    WakeCooldown = 512, // Just woke up: no new nap yet.

    Busy = Paused | Held | Unsettled,
    AnyQuiet = Quiet | Fullscreen,
    AnyBattle = Battle | BattleScreens,
}

public enum AutoKind
{
    Play,           // The cat is free for anything at all (CanPlay).
    LocalBehavior,  // Wandering, sitting, napping chosen by PetBehavior on its own.
    Treat,          // Feeding from the menu (the user asks; allowed in Quiet).
    UserGame,       // Ball or laser the user starts (allowed in Quiet, not with a fullscreen app).
    RestVisit,      // Walking to the favourite resting spot.
    Admire,         // Going to look at the keepsake on its own.
    CallToLook,     // Double click on the keepsake: the user asked, so also in Quiet.
    ShoveKeepsake,
    CushionNap,     // Walking to the slime cushion to nap on its own.
    WatchKeepsake,  // Turning to watch the keepsake the user holds.
    Knead,          // Sustained petting.
    Stretch,        // Stretching along with a break reminder.
    ClimbLedge,     // Planning a jump or climb onto a window.
    Ledges,         // Window edges count as ground at all.
    AwayNap,
    BreakReminder,
    Speech,
    Thought,
    PortalEvent,    // A portal appearing by itself; the spell the user asks for goes by UserGame.
    Weather,        // The weather shown around the cat; it does not move the cat, so Busy does not matter.
    FarmChore,      // The cat going to water, harvest or chase a bird off the field on its own.
    ToyFetch,       // Fetching the yarn ball the user threw (allowed in Quiet and the wake cooldown, like the ball game).
    ToyInvite,      // Bringing the yarn ball to the user on its own, to ask for a game.
    ToyChase,       // Chasing the feather wand the user holds or the mouse the user wound (allowed in Quiet and the wake cooldown).
    ToyPass,        // Batting back the yarn ball the user rolled to it (allowed in Quiet and the wake cooldown).
    ToySniff,       // Going to sniff a toy it has never seen, on its own (blocked like the fetch invitation).
    ToyReact,       // A short reaction to the user's doing (a new accessory); allowed in Quiet.
    PostScratch,    // Walking to the scratching post on its own (blocked like a nap in the cardboard box).
    PlantVisit,     // Walking to the plant pot on its own (blocked like the scratching post).
    BoxPeek,        // Hiding in the cardboard box awake, its head over the edge (blocked like a nap in the box).
    BondAct,        // What a bond milestone opens: greeting the user back, the after-play routine (docs/LIVING-CORNER.md → M3).
    FavoriteVisit,  // Walking to its favourite toy on its own (docs/LIVING-CORNER.md → M4; blocked like bringing the ball).
    GuestVisit,     // A visitor coming by on its own (docs/MINI-GAMES.md → M4; blocked like bringing the ball).
}

public static class PetAutonomy
{
    // The one table: which conditions stop each kind. Call sites add only their own
    // details (a cooldown, the ball already out, the keepsake in reach...).
    public static AutoBlock BlockedBy(AutoKind kind) => kind switch
    {
        AutoKind.Play => AutoBlock.Busy,
        AutoKind.LocalBehavior => AutoBlock.AnyQuiet | AutoBlock.BattleScreens,
        AutoKind.Treat => AutoBlock.Busy | AutoBlock.AnyBattle,
        AutoKind.UserGame => AutoBlock.Busy | AutoBlock.Fullscreen | AutoBlock.AnyBattle,
        AutoKind.RestVisit => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.AnyBattle,
        AutoKind.Admire => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.Battle | AutoBlock.Feature,
        AutoKind.CallToLook => AutoBlock.Busy | AutoBlock.Battle,
        AutoKind.ShoveKeepsake => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.Battle | AutoBlock.Feature,
        AutoKind.CushionNap => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.Battle | AutoBlock.Feature | AutoBlock.WakeCooldown,
        AutoKind.WatchKeepsake => AutoBlock.Busy | AutoBlock.Battle,
        AutoKind.Knead => AutoBlock.Busy | AutoBlock.Battle | AutoBlock.Feature,
        AutoKind.Stretch => AutoBlock.Busy | AutoBlock.Feature,
        AutoKind.ClimbLedge => AutoBlock.Busy | AutoBlock.AnyQuiet,
        AutoKind.Ledges => AutoBlock.Fullscreen | AutoBlock.Battle,
        AutoKind.AwayNap => AutoBlock.Held | AutoBlock.AnyBattle,
        AutoKind.BreakReminder => AutoBlock.Paused | AutoBlock.Held | AutoBlock.Fullscreen | AutoBlock.AnyBattle | AutoBlock.Away,
        AutoKind.Speech => AutoBlock.Fullscreen | AutoBlock.AnyBattle,
        AutoKind.Thought => AutoBlock.Fullscreen,
        AutoKind.PortalEvent => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Feature | AutoBlock.Away,
        AutoKind.Weather => AutoBlock.AnyQuiet | AutoBlock.AnyBattle,
        AutoKind.FarmChore => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Feature | AutoBlock.Away,
        AutoKind.ToyFetch => AutoBlock.Busy | AutoBlock.Fullscreen | AutoBlock.AnyBattle | AutoBlock.Feature,
        AutoKind.ToyChase => AutoBlock.Busy | AutoBlock.Fullscreen | AutoBlock.AnyBattle | AutoBlock.Feature,
        AutoKind.ToyPass => AutoBlock.Busy | AutoBlock.Fullscreen | AutoBlock.AnyBattle | AutoBlock.Feature,
        AutoKind.ToySniff => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Feature | AutoBlock.WakeCooldown | AutoBlock.Away,
        AutoKind.ToyReact => AutoBlock.Busy | AutoBlock.Fullscreen | AutoBlock.AnyBattle | AutoBlock.Feature,
        AutoKind.PostScratch => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Feature | AutoBlock.WakeCooldown,
        AutoKind.PlantVisit => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Feature | AutoBlock.WakeCooldown,
        AutoKind.BoxPeek => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Feature | AutoBlock.WakeCooldown,
        AutoKind.BondAct => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Feature,
        AutoKind.ToyInvite => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Feature | AutoBlock.WakeCooldown | AutoBlock.Away,
        AutoKind.FavoriteVisit => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Feature | AutoBlock.WakeCooldown | AutoBlock.Away,
        AutoKind.GuestVisit => AutoBlock.Busy | AutoBlock.AnyQuiet | AutoBlock.AnyBattle | AutoBlock.Feature | AutoBlock.WakeCooldown | AutoBlock.Away,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static bool MayAct(AutoKind kind, AutoBlock now) => (BlockedBy(kind) & now) == 0;
}
