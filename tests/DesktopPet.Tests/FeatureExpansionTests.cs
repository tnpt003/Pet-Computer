using DesktopPet;
using DesktopPet.AI;
using System.Text.Json;
internal static class FeatureExpansionTests
{
    public static void Run(Action<bool, string> check)
    {
        var play = new ToyPlaySequence(); play.Reset();
        bool premature = false;
        for (int i=0; i<7; i++) premature |= play.Step(0.1, true, "Balanced");
        check(!premature && play.Phase == ToyPlayPhase.Observe, "Ball within reach does not skip observation");
        for (int i=0; i<2; i++) play.Step(0.1, true, "Balanced");
        check(play.Phase == ToyPlayPhase.Crouch, "Ball play prepares before chasing");
        for (int i=0; i<6; i++) play.Step(0.1, false, "Balanced");
        check(play.Phase == ToyPlayPhase.Chase && !play.Step(0.1, false, "Balanced"), "Chase waits for real contact");
        check(play.Step(0.1, true, "Balanced") && play.Phase == ToyPlayPhase.Recover, "Bat creates one contact event and a recovery phase");
        check(!play.Step(0.1, true, "Balanced"), "Recovery cannot repeatedly bat a nearby ball");
        play.Reset(); check(play.Phase == ToyPlayPhase.Observe && play.CrouchBlend == 0, "Cancelled play clears preparation pose");
        var curious = new ToyPlaySequence(); var playful = new ToyPlaySequence();
        for (int i=0; i<6; i++) { curious.Step(0.1,false,"Curious"); playful.Step(0.1,false,"Playful"); }
        check(curious.Phase == ToyPlayPhase.Observe && playful.Phase == ToyPlayPhase.Crouch, "Curious observes longer than Playful");
        for (int i=0; i<90; i++) play.Step(0.1,false,"Balanced");
        check(play.Phase != ToyPlayPhase.Chase, "Unreachable ball produces a pause instead of endless chasing");

        var behavior = new PetBehavior(new PetSettings(), new Random(17));
        var pet = new PetModel { Grounded=true };
        bool found = false;
        for (int i=0; i<100 && !found; i++)
        {
            pet.Action=PetAction.Idle; pet.Grounded=true; pet.Emotions.Reset();
            behavior.Reset(); behavior.Step(pet,3);
            found = pet.Action is PetAction.Grooming or PetAction.Stretching;
        }
        check(found, "Offline behavior occasionally selects self-care");
        check(!behavior.TryApply(pet,new("WALK_LEFT",2,"content")), "Self-care rejects delayed locomotion");
        check(!DecisionGuard.CanApply(1,1,0.1,false,pet), "Self-care also rejects stale AI emotion hints");
        behavior.Step(pet,4);
        check(pet.Action == PetAction.Idle && pet.VX == 0, "Self-care ends planted in Idle");
        bool respectedCooldown=true;
        for (int i=0;i<5;i++) { behavior.Reset(); behavior.Step(pet,3); respectedCooldown &= pet.Action is not (PetAction.Grooming or PetAction.Stretching); pet.Action=PetAction.Idle; pet.Grounded=true; pet.Emotions.Reset(); }
        check(respectedCooldown, "Interaction reset does not remove self-care cooldown");
        pet.Action=PetAction.Grooming; pet.Emotions.EnterSleep(); behavior.Step(pet,0.1);
        check(pet.Action == PetAction.Sleeping, "Sleep lock overrides self-care");
        pet.Emotions.Reset(); pet.Action=PetAction.Thrown; pet.Grounded=false; pet.VX=310;
        behavior.Step(pet,0.1); check(pet.Action==PetAction.Thrown && pet.VX==310,"Self-care does not alter throw physics");

        string? oldData = Environment.GetEnvironmentVariable("PET_DATA_DIRECTORY");
        string isolated = Path.Combine(Path.GetTempPath(), "DesktopPet-isolated-check");
        try { Environment.SetEnvironmentVariable("PET_DATA_DIRECTORY", isolated);
            check(PetPreferences.DataDirectory == isolated, "Published smoke tests can isolate preferences and memory"); }
        finally { Environment.SetEnvironmentVariable("PET_DATA_DIRECTORY", oldData); }
        var tuning = new PersonalityTuning { Playfulness=double.NaN, Curiosity=7, AttentionAfterSeconds=-4 }.Normalized();
        check(tuning.Playfulness==0.7 && tuning.Curiosity==1 && tuning.AttentionAfterSeconds==10,"Tuning validates nonfinite and out-of-range values");
        var obs = new PetObservation("Idle",true,3,1,"test","LOCAL",Tuning:new PersonalityTuning { Playfulness=0.23 });
        using (var json=JsonDocument.Parse(JsonSerializer.Serialize(obs,AiProtocol.Json)))
            check(json.RootElement.GetProperty("tuning").GetProperty("playfulness").GetDouble()==0.23,"User tuning crosses IPC using Python snake-case names");
        foreach (string cue in new[] { "ball_tap", "satisfied", "wake" })
        {
            var wave=PetSounds.CreateWave(cue,12);
            check(BitConverter.ToInt32(wave,40)==wave.Length-44 && wave.Length>1000 && wave.Length<44100,
                "Context cue is bounded valid PCM: "+cue);
            check(!wave.SequenceEqual(PetSounds.CreateWave("excited",12)),"Context cue differs from generic excitement: "+cue);
        }
    }
}
