using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopPet;

// Battle Mode feedback: which sound and banner each battle event gets. One table, so a new
// event (or a new skill's event) is one line here.
public sealed partial class PetWindow
{
    // Sound per event and its rank: one cue per frame, the highest rank wins (a defeat's pop
    // over its hit's squish); ties keep the earlier event. Rank 0: silent.
    private static readonly Dictionary<BattleEventKind, (string Sound, int Rank)> BattleCues = new()
    {
        [BattleEventKind.Won] = ("excited", 9), [BattleEventKind.Lost] = ("annoyed", 9),
        [BattleEventKind.BossAppeared] = ("surprised", 8), [BattleEventKind.CatHurt] = ("surprised", 7),
        [BattleEventKind.WaveCleared] = ("excited", 6), [BattleEventKind.SynergyUnlocked] = ("excited", 6),
        [BattleEventKind.Healed] = ("satisfied", 5), [BattleEventKind.UpgradeChosen] = ("satisfied", 5), [BattleEventKind.Cheered] = ("excited", 5), [BattleEventKind.Blocked] = ("ball_tap", 5),
        [BattleEventKind.SlimeDefeated] = ("pop", 4), [BattleEventKind.BombExploded] = ("pop", 4), [BattleEventKind.ComboHit] = ("ball_tap", 4),
        [BattleEventKind.SlimeHit] = ("squish", 3), [BattleEventKind.EnemyHealed] = ("ball_tap", 3),
        [BattleEventKind.Pushed] = ("swoosh", 2), [BattleEventKind.BombThrown] = ("swoosh", 2), [BattleEventKind.Shielded] = ("swoosh", 2),
        [BattleEventKind.Swing] = ("swoosh", 1),
    };

    private void PlayBattleSound()
    {
        if (battle == null) return;
        (string Sound, int Rank) best = ("", 0);
        for (int i = 0; i < battle.Events.Count; i++)
            if (BattleCues.TryGetValue(battle.Events[i].Kind, out var cue) && cue.Rank > best.Rank) best = cue;
        if (best.Rank > 0) PlayPetSound(best.Sound);
    }

    private string BossName() => battle?.Boss?.Type.Name(preferences.Language == "en") ?? "Boss";

    // Big moments get a banner across the HUD's centre. The first heal of a battle says what the
    // support enemy does (once per battle).
    private bool healerExplained;
    private void ShowBattleBanners()
    {
        if (battle == null || battleHud == null) return;
        if (battle.Time < 0.05) healerExplained = false;
        for (int i = 0; i < battle.Events.Count; i++)
        {
            var e = battle.Events[i];
            string? text = e.Kind switch
            {
                BattleEventKind.WaveStarted when battle.IsBossWave((int)e.Value) => L("BOSS!", "BOSS!"),
                BattleEventKind.WaveStarted => L($"ĐỢT {(int)e.Value}", $"WAVE {(int)e.Value}"),
                BattleEventKind.BossSummoned => L($"{BossName()} gọi quân!", $"The {BossName()} calls for help!"),
                BattleEventKind.BossEnraged => L($"{BossName()} nổi giận!", $"The {BossName()} is angry!"),
                BattleEventKind.SynergyUnlocked when (int)e.Value >= 0 && (int)e.Value < battle.Settings.SynergyList.Count =>
                    "★ " + battle.Settings.SynergyList[(int)e.Value].Name(English).ToUpper(System.Globalization.CultureInfo.CurrentCulture) + "!",
                BattleEventKind.EnemyHealing when !healerExplained => L("Quái hồi máu! Hạ nó trước", "A healer! Take it down first"),
                BattleEventKind.Won => L("CHIẾN THẮNG!", "VICTORY!"),
                BattleEventKind.Lost => L("THUA RỒI…", "DEFEATED…"),
                _ => null
            };
            if (e.Kind == BattleEventKind.EnemyHealing) healerExplained = true;
            if (text != null) battleHud.ShowBanner(text, battle.Time, e.Kind is BattleEventKind.Won or BattleEventKind.Lost ? 3 : 1.6);
        }
    }
}
