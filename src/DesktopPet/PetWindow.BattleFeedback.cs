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
        [BattleEventKind.BossShielded] = ("surprised", 6), [BattleEventKind.BossShieldDown] = ("excited", 6),
        [BattleEventKind.SlimeHit] = ("squish", 3), [BattleEventKind.EnemyHealed] = ("ball_tap", 3), [BattleEventKind.HitBlocked] = ("ball_tap", 3), [BattleEventKind.BossImmune] = ("ball_tap", 3),
        [BattleEventKind.Incoming] = ("surprised", 5), [BattleEventKind.BossRallying] = ("surprised", 4),
        [BattleEventKind.EliteAppeared] = ("surprised", 5), [BattleEventKind.ShellBlocked] = ("ball_tap", 5),
        [BattleEventKind.HitWarded] = ("ball_tap", 3), [BattleEventKind.EnemyFlanking] = ("surprised", 4),
        [BattleEventKind.BossLeaping] = ("swoosh", 3), [BattleEventKind.BossLanded] = ("pop", 5), [BattleEventKind.BossSummoning] = ("surprised", 4),
        [BattleEventKind.Pushed] = ("swoosh", 2), [BattleEventKind.BombThrown] = ("swoosh", 2), [BattleEventKind.Shielded] = ("swoosh", 2),
        [BattleEventKind.EnemyTeleported] = ("swoosh", 2),
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

    // Big moments get a banner across the HUD's centre. The first heal, rally call and attack from
    // behind of a battle say what is going on (once each per battle), and so does the first elite of
    // each kind.
    private bool healerExplained, rallyExplained, behindExplained, wardExplained, flankExplained, fallBackExplained;
    private readonly HashSet<string> elitesExplained = new();
    private void ShowBattleBanners()
    {
        if (battle == null || battleHud == null) return;
        if (battle.Time < 0.05) { healerExplained = rallyExplained = behindExplained = wardExplained = flankExplained = fallBackExplained = false; elitesExplained.Clear(); }
        for (int i = 0; i < battle.Events.Count; i++)
        {
            var e = battle.Events[i];
            string? text = e.Kind switch
            {
                BattleEventKind.WaveStarted when battle.IsBossWave((int)e.Value) => L("BOSS!", "BOSS!"),
                BattleEventKind.WaveStarted => L($"ĐỢT {(int)e.Value}", $"WAVE {(int)e.Value}"),
                BattleEventKind.BossSummoned => L($"{BossName()} gọi quân!", $"The {BossName()} calls for help!"),
                BattleEventKind.BossShielded => L($"{BossName()} bật khiên! Hạ hết quân", $"The {BossName()} shields up! Defeat its helpers"),
                BattleEventKind.BossShieldDown => L("KHIÊN VỠ!", "SHIELD DOWN!"),
                BattleEventKind.BossEnraged =>L($"{BossName()} nổi giận!", $"The {BossName()} is angry!"),
                BattleEventKind.SynergyUnlocked when (int)e.Value >= 0 && (int)e.Value < battle.Settings.SynergyList.Count =>
                    "★ " + battle.Settings.SynergyList[(int)e.Value].Name(English).ToUpper(System.Globalization.CultureInfo.CurrentCulture) + "!",
                BattleEventKind.EnemyHealing when !healerExplained => L("Quái hồi máu! Hạ nó trước", "A healer! Take it down first"),
                BattleEventKind.BossRallying when !rallyExplained => L($"{BossName()} ra lệnh xung phong! Đẩy để cắt", $"The {BossName()} rallies! Push it to cut the call"),
                BattleEventKind.Incoming when !behindExplained => L("Quái tới từ phía sau!", "Enemies from behind!"),
                BattleEventKind.EnemyWarded when !wardExplained => L("Đang được hồi thì miễn sát thương! Đẩy slime thảo dược", "Healing makes it immune! Push the herb slime"),
                BattleEventKind.EnemyFlanking when !flankExplained => L("Slime vòng ra sau lưng mèo!", "A slime is going round behind the cat!"),
                BattleEventKind.EnemyFallingBack when !fallBackExplained => L("Quái yếu lùi về để được hồi!", "A hurt slime falls back to be healed!"),
                BattleEventKind.EliteAppeared when battle.Find((int)e.Value) is { Elite: EliteSpec elite } enemy && elitesExplained.Add(enemy.Type.Id) =>
                    L($"★ {enemy.Type.Name(false)} tinh anh: {elite.Describe(false)}", $"★ Elite {enemy.Type.Name(true)}: {elite.Describe(true)}"),
                BattleEventKind.Won => L("CHIẾN THẮNG!", "VICTORY!"),
                BattleEventKind.Lost => L("THUA RỒI…", "DEFEATED…"),
                _ => null
            };
            if (e.Kind == BattleEventKind.EnemyHealing) healerExplained = true;
            if (e.Kind == BattleEventKind.BossRallying) rallyExplained = true;
            if (e.Kind == BattleEventKind.Incoming) behindExplained = true;
            if (e.Kind == BattleEventKind.EnemyWarded) wardExplained = true;
            if (e.Kind == BattleEventKind.EnemyFlanking) flankExplained = true;
            if (e.Kind == BattleEventKind.EnemyFallingBack) fallBackExplained = true;
            if (text != null) battleHud.ShowBanner(text, battle.Time, e.Kind is BattleEventKind.Won or BattleEventKind.Lost ? 3 : 1.6);
        }
    }
}
