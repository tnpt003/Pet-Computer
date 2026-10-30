using System.Collections.Generic;

namespace DesktopPet;

// 1.5 M27 (docs/UPDATE-1.5.md L1): what this battle shows the Monster Book. A kind is met once one of
// it stands on the floor (spawned, not a mirage's copy); the ones the cat defeated are counted by kind;
// a formation is met when it arrives. The window adds them to the book when the battle ends (MonsterBook).
public sealed partial class BattleSim
{
    private readonly HashSet<string> metKinds = new(), metFormations = new();
    private readonly Dictionary<string, int> defeatedKinds = new();
    public IReadOnlyCollection<string> MetKinds => metKinds;
    public IReadOnlyCollection<string> MetFormations => metFormations;
    public IReadOnlyDictionary<string, int> DefeatedKinds => defeatedKinds;

    // Each Step: the enemies standing in the battle now.
    private void NoteMet()
    {
        for (int i = 0; i < slimes.Count; i++)
        {
            var s = slimes[i];
            if (s.State is not (SlimeState.Spawning or SlimeState.Defeated) && !s.Decoy) metKinds.Add(s.Type.Id);
        }
    }

    private void NoteDefeated(Slime slime)
    {
        if (slime.Decoy) return;
        metKinds.Add(slime.Type.Id);
        defeatedKinds[slime.Type.Id] = defeatedKinds.GetValueOrDefault(slime.Type.Id) + 1;
    }

    private void NoteFormation(string id) => metFormations.Add(id);
}
