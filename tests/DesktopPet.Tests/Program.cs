using DesktopPet;

var settings = new PetSettings();
var physics = new PetPhysics(settings);
var bounds = new Area(-1920, 0, 0, 1040);
int checks = 0;
var failures = new List<string>();
// Record failures and keep going so one regression does not hide the others.
void Check(bool condition, string message)
{
    if (!condition)
    {
        failures.Add(message);
        Console.WriteLine($"FAIL: {message}");
        return;
    }
    checks++;
    Console.WriteLine($"PASS: {message}");
}

var pet = new PetModel { X = -500, Y = 400, VX = 1800, VY = -900, Action = PetAction.Thrown };
bool stayedInside = true;
for (int i = 0; i < 3600; i++)
{
    physics.Step(pet, bounds, 1.0 / 120);
    stayedInside &= pet.X >= bounds.Left && pet.X <= bounds.Right - settings.Width
        && pet.Y >= bounds.Top && pet.Y <= bounds.Bottom - settings.Height;
}
Check(stayedInside, "Fast throw stays within negative-coordinate monitor bounds");
Check(pet.Grounded && pet.VX == 0 && pet.VY == 0 && pet.Action == PetAction.Idle,
    "Throw bounces and eventually settles into idle");
pet = new PetModel { X = -113, Y = 928, VX = 72, Grounded = true, Action = PetAction.Walking };
physics.Step(pet, bounds, 0.1);
Check(pet.VX < 0 && pet.Facing == -1, "Walking reverses at the screen edge");
pet = new PetModel { X = -500, Y = 1, VY = -500, Action = PetAction.Thrown };
physics.Step(pet, bounds, 1.0 / 120);
Check(pet.Y == 0 && pet.VY > 0, "Ceiling collision rebounds downwards");
pet = new PetModel { X = -500, Y = 927, VY = 500, Action = PetAction.Thrown };
physics.Step(pet, bounds, 1.0 / 120);
Check(pet.VY < 0 && !pet.Grounded, "Floor collision bounces upwards");
pet = new PetModel { X = -500, Y = 200, VX = 100, Action = PetAction.Dragging };
physics.Step(pet, bounds, 0.1);
Check(pet.X == -500 && pet.Y == 200, "Physics does not fight mouse dragging");
var tracker = new ThrowTracker();
tracker.Add(0, 0, 0);
tracker.Add(0.1, 100, -50);
var velocity = tracker.Velocity(2200);
Check(Math.Abs(velocity.X - 1000) < 0.01 && Math.Abs(velocity.Y + 500) < 0.01,
    "Release velocity uses recent movement and time");
tracker.Add(0.3, 100, -50);
Check(tracker.Velocity(2200) == (0, 0), "Holding still before release removes stale throw velocity");
tracker.Clear();
tracker.Add(0, 0, 0);
tracker.Add(0.01, 10000, 10000);
velocity = tracker.Velocity(2200);
Check(Math.Abs(Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y) - 2200) < 0.01,
    "Extreme throws are capped by total speed");
Console.WriteLine($"Physics checks done ({checks} passed so far).");
await AiTests.Run(Check);
SleepTests.Run(Check);
PickupTests.Run(Check);
EmotionTests.Run(Check);
EmotionPriorityTests.Run(Check);
PolishTests.Run(Check);
InteractionPolishTests.Run(Check);
FeatureExpansionTests.Run(Check);
MotionTests.Run(Check);
ToolingTests.Run(Check);
NeedsTests.Run(Check);
SpeechTests.Run(Check);
ContextTests.Run(Check);
GestureTests.Run(Check);
ManualControlTests.Run(Check);
LaserTests.Run(Check);
LedgeTests.Run(Check);
CombinationTests.Run(Check);
BattleTests.Run(Check);
BattleSkillTests.Run(Check);
BattleRulesTests.Run(Check);
BattleStunTests.Run(Check);
BuildSynergyTests.Run(Check);
BattleRoleTests.Run(Check);
EnemyDirectorTests.Run(Check);
EnemyTacticsTests.Run(Check);
EnemyGachaTests.Run(Check);
EliteBuffTests.Run(Check);
BossLeapTests.Run(Check);
TacticsTests.Run(Check);
RescueTests.Run(Check);
BattleWeaponTests.Run(Check);
BattleDataTests.Run(Check);
BattleCustomDataTests.Run(Check);
AdventureTests.Run(Check);
AdventureUpgradeTests.Run(Check);
AdventureContentTests.Run(Check);
AdventureBalanceTests.Run(Check);
ExpeditionTests.Run(Check);
ExpeditionChoiceTests.Run(Check);
ExpeditionKeepsakeTests.Run(Check);
ExpeditionReleaseTests.Run(Check);
var activity = new PetActivity();
activity.Step(PetAction.Walking, 2);
activity.Step(PetAction.Jumping, 1);
activity.Step(PetAction.Sleeping, 10);
activity.Step(PetAction.Idle, 3);
activity.Step(PetAction.Dragging, 20);
activity.Step(PetAction.Thrown, 20);
activity.Step(PetAction.Walking, double.NaN);
Check(activity.Walking == 2 && activity.Playing == 1 && activity.Sleeping == 10 && activity.Resting == 3,
    "Activity telemetry counts execution without treating dragging or throwing as exertion");
var snapshot = activity.Snapshot();
activity.Step(PetAction.Walking, 1);
Check(snapshot.Walking == 2, "AI activity snapshot stays stable while real-time activity continues");
if (failures.Count > 0)
{
    Console.WriteLine($"{failures.Count} C# check(s) FAILED ({checks} passed):");
    foreach (string failure in failures) Console.WriteLine($"  - {failure}");
    return 1;
}
Console.WriteLine($"Total C# checks passed: {checks}");
return 0;
