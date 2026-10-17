using DesktopPet;
using DesktopPet.AI;

internal static class NeedsTests
{
    public static void Run(Action<bool, string> check)
    {
        // Protocol: optional thought/needs are parsed, invalid extras are dropped, not fatal.
        var full = AiProtocol.Parse("{\"version\":1,\"id\":3,\"type\":\"decision\",\"command\":\"IDLE\",\"duration_seconds\":2,\"mood\":\"content\"," +
            "\"thought\":\"hungry\",\"needs\":{\"hunger\":71.5,\"boredom\":20,\"affection\":50,\"bond\":31}}", 3);
        check(full.Thought == "hungry" && full.Needs == new PetNeeds(71.5, 20, 50, 31), "Decision carries a thought and needs report");
        var odd = AiProtocol.Parse("{\"version\":1,\"id\":4,\"type\":\"decision\",\"command\":\"SIT\",\"duration_seconds\":2,\"mood\":\"content\"," +
            "\"thought\":\"conquer\",\"needs\":{\"hunger\":500,\"boredom\":20,\"affection\":50,\"bond\":31}}", 4);
        check(odd.Command == "SIT" && odd.Thought == null && odd.Needs == null, "Unknown thoughts and out-of-range needs are ignored safely");
        var old = AiProtocol.Parse("{\"version\":1,\"id\":5,\"type\":\"decision\",\"command\":\"LOOK\",\"duration_seconds\":2,\"mood\":\"content\"}", 5);
        check(old.Thought == null && old.Needs == null, "Replies from older services without needs remain valid");

        // Interaction counters cross IPC with snake_case names and are snapshots.
        var pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        foreach (string kind in new[] { "petted", "petted", "left_clicked", "treat_consumed", "toy_started", "ball_clicked", "reset", "right_clicked" })
            pet.Interactions.Record(kind);
        pet.Interactions.BallHit(); pet.Interactions.RoughThrow();
        var snapshot = pet.Interactions.Snapshot();
        pet.Interactions.Record("petted");
        string json = System.Text.Json.JsonSerializer.Serialize(new PetObservation("Idle", true, 1, 1, "x", "LOCAL", Interactions: snapshot), AiProtocol.Json);
        check(json.Contains("\"interactions\":{\"petted\":2,\"clicked\":1,\"treats\":1,\"toy\":1,\"ball_clicks\":1,\"ball_hits\":1,\"rough_throws\":1}"),
            "Observation sends cumulative interaction counts by type");
        string battleJson = System.Text.Json.JsonSerializer.Serialize(new PetObservation("Idle", true, 1, 1, "x", "LOCAL", Battles: new BattleReport(3, 1, 5, 2)), AiProtocol.Json);
        check(battleJson.Contains("\"battles\":{\"won\":3,\"lost\":1,\"combos\":5,\"records\":2}"), "Observation sends this session's battle results");

        // Bubble rules: timing, cooldowns and protected states.
        var thought = pet.Thought;
        check(thought.TryShow(ThoughtKind.Hungry, pet) && thought.Kind == ThoughtKind.Hungry, "A grounded idle pet shows a suggested thought");
        check(!thought.TryShow(ThoughtKind.Bored, pet), "Only one bubble is shown at a time");
        thought.Step(pet, PetThought.Duration + 0.01);
        check(!thought.Visible, "The bubble disappears after its short duration");
        thought.Step(pet, PetThought.GlobalCooldown - 1);
        check(!thought.TryShow(ThoughtKind.Bored, pet), "A global gap separates consecutive bubbles");
        thought.Step(pet, 1.1);
        check(!thought.TryShow(ThoughtKind.Hungry, pet) && thought.TryShow(ThoughtKind.Bored, pet),
            "The same thought has a longer per-kind cooldown than different thoughts");
        pet.Action = PetAction.Dragging; pet.Grounded = false;
        thought.Step(pet, 0.01);
        check(!thought.Visible, "Picking the pet up clears its bubble immediately");
        thought.Step(pet, 30);
        check(!thought.TryShow(ThoughtKind.Love, pet), "No bubble while dragged");
        foreach (var action in new[] { PetAction.Sleeping, PetAction.Waking, PetAction.Thrown, PetAction.Jumping, PetAction.PreparingJump })
        {
            pet.Action = action; pet.Grounded = action is not (PetAction.Thrown or PetAction.Jumping);
            check(!PetThought.CanShow(pet), $"No bubble while {action}");
        }
        pet = new PetModel { Action = PetAction.Idle, Grounded = true, TreatRemaining = 2 };
        check(!PetThought.CanShow(pet), "No bubble while eating");
        pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        pet.Emotions.AggressiveInteraction();
        pet.Emotions.Step(pet, 0.1);
        check(!pet.Thought.TryShow(ThoughtKind.Love, pet), "No bubble during an angry reaction");
        pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        pet.Emotions.EnterSleep();
        check(!pet.Thought.TryShow(ThoughtKind.Sleepy, pet), "A bubble never appears under the sleep lock");
        pet = new PetModel { Action = PetAction.Idle, Grounded = true };
        double x = pet.X, y = pet.Y;
        pet.Thought.TryShow(ThoughtKind.Love, pet);
        for (int i = 0; i < 60; i++) pet.Thought.Step(pet, 1.0 / 60);
        check(pet.Action == PetAction.Idle && pet.X == x && pet.Y == y && pet.VX == 0, "Bubbles never change action or position");
    }
}
