using System;

namespace DesktopPet.AI;

// Names shared with ai/service.py. AiTests (with PET_TEST_PYTHON) compares
// these lists with Python's COMMANDS, MOODS and PERSONALITIES.

// Personality profiles; stored in preferences and sent to Python as these English ids.
public static class PetPersonality
{
    public const string Balanced = "Balanced", Playful = "Playful", Curious = "Curious", Calm = "Calm";
    // Settings order.
    public static readonly string[] All = { Balanced, Playful, Curious, Calm };
    public static string Normalize(string? value) => Array.IndexOf(All, value) >= 0 ? value! : Balanced;
}

// High-level commands Python may send in a decision.
public static class AiCommand
{
    public const string Look = "LOOK", Idle = "IDLE", WalkLeft = "WALK_LEFT", WalkRight = "WALK_RIGHT", Sit = "SIT", Jump = "JUMP",
        Sleep = "SLEEP", FollowMouse = "FOLLOW_MOUSE", SeekAttention = "SEEK_ATTENTION", RollOver = "ROLL_OVER", ChaseTail = "CHASE_TAIL",
        Scratch = "SCRATCH";
    public static readonly string[] All = { Look, Idle, WalkLeft, WalkRight, Sit, Jump, Sleep, FollowMouse, SeekAttention, RollOver, ChaseTail, Scratch };
    public static bool IsValid(string? command) => Array.IndexOf(All, command) >= 0;
}

// The mood Python reports with each decision.
public static class AiMood
{
    public const string Content = "content", Playful = "playful", Bored = "bored", Sleepy = "sleepy";
    public static readonly string[] All = { Content, Playful, Bored, Sleepy };
    public static bool IsValid(string? mood) => Array.IndexOf(All, mood) >= 0;
}
