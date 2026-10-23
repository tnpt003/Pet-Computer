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
        [BattleEventKind.EliteAppeared] = ("sparkle", 5), [BattleEventKind.ShellBlocked] = ("ball_tap", 5),
        [BattleEventKind.HitWarded] = ("ball_tap", 3), [BattleEventKind.PushResisted] = ("clank", 3), [BattleEventKind.BossRallied] = ("sparkle", 5),
        [BattleEventKind.BossLeaping] = ("swoosh", 3), [BattleEventKind.BossLanded] = ("pop", 5), [BattleEventKind.BossSummoning] = ("surprised", 4),
        [BattleEventKind.Pushed] = ("swoosh", 2), [BattleEventKind.BombThrown] = ("swoosh", 2), [BattleEventKind.Shielded] = ("swoosh", 2),
        [BattleEventKind.Swing] = ("swoosh", 1),
        [BattleEventKind.AppUsed] = ("sparkle", 6), [BattleEventKind.AppInfected] = ("surprised", 6), [BattleEventKind.AppCleaned] = ("excited", 6),
        [BattleEventKind.PopupOpened] = ("surprised", 5), [BattleEventKind.PopupSpawned] = ("surprised", 5),
        [BattleEventKind.GooShot] = ("squish", 4), [BattleEventKind.PopupClosed] = ("pop", 4), [BattleEventKind.AppScrubbed] = ("ball_tap", 3),
        [BattleEventKind.HazardOpened] = ("surprised", 5), [BattleEventKind.HazardClosed] = ("pop", 4),
        [BattleEventKind.HazardTapped] = ("ball_tap", 3), [BattleEventKind.WallHit] = ("clank", 3),
        [BattleEventKind.AppAttack] = ("swoosh", 3), [BattleEventKind.AppReady] = ("ball_tap", 2), [BattleEventKind.AppMoved] = ("swoosh", 1),
    };

    private void PlayBattleSound()
    {
        if (battle == null) return;
        (string Sound, int Rank) best = ("", 0);
        for (int i = 0; i < battle.Events.Count; i++)
            if (BattleCues.TryGetValue(battle.Events[i].Kind, out var cue) && cue.Rank > best.Rank) best = cue;
        if (best.Rank > 0) PlayPetSound(best.Sound);
    }

    // A desktop app's name from an app event (Value: the icon's id).
    private string? AppName(BattleEvent e) =>
        battle?.AppRules is AppsSpec rules && battle.FindApp((int)e.Value) is DesktopApp app ? rules.App(app.Kind).Name(preferences.Language) : null;

    // A malware event about a forced update (Value: its id).
    private bool IsUpdate(BattleEvent e) => battle?.FindHazard((int)e.Value) is { Kind: HazardKind.Update };

    private string BossName() => battle?.Boss?.Type.Name(preferences.Language) ?? "Boss";

    // Big moments get a banner across the HUD's centre. The first heal, far heal, rally call and attack
    // from behind of a battle say what is going on (once each per battle), and so does the first elite
    // of each kind; every helper a rally makes elite is named.
    private bool healerExplained, farHealExplained, rallyExplained, behindExplained, wardExplained, pushResistExplained, appExplained, popupExplained, virusExplained, loadingExplained, updateExplained;
    private readonly HashSet<string> elitesExplained = new();
    private void ShowBattleBanners()
    {
        if (battle == null || battleHud == null) return;
        if (battle.Time < 0.05) { healerExplained = farHealExplained = rallyExplained = behindExplained = wardExplained = pushResistExplained = appExplained = popupExplained = virusExplained = loadingExplained = updateExplained = false; elitesExplained.Clear(); }
        for (int i = 0; i < battle.Events.Count; i++)
        {
            var e = battle.Events[i];
            string? text = e.Kind switch
            {
                BattleEventKind.WaveStarted when battle.IsBossWave((int)e.Value) => L("BOSS!", "BOSS!"),
                BattleEventKind.WaveStarted => LF($"ĐỢT {(int)e.Value}", $"WAVE {(int)e.Value}"),
                BattleEventKind.BossSummoned => LF($"{BossName()} gọi quân!", $"The {BossName()} calls for help!"),
                BattleEventKind.BossShielded => LF($"{BossName()} bật khiên! Hạ hết quân", $"The {BossName()} shields up! Defeat its helpers"),
                BattleEventKind.BossShieldDown => L("KHIÊN VỠ!", "SHIELD DOWN!"),
                BattleEventKind.BossEnraged =>LF($"{BossName()} nổi giận!", $"The {BossName()} is angry!"),
                BattleEventKind.SynergyUnlocked when (int)e.Value >= 0 && (int)e.Value < battle.Settings.SynergyList.Count =>
                    "★ " + battle.Settings.SynergyList[(int)e.Value].Name(preferences.Language).ToUpper(System.Globalization.CultureInfo.CurrentCulture) + "!",
                BattleEventKind.EnemyHealing when e.Value > 0 && !farHealExplained => L("Slime thảo dược hồi máu từ xa! Đẩy nó để cắt", "A herb slime heals from afar! Push it to cut it"),
                BattleEventKind.EnemyHealing when !healerExplained => L("Quái hồi máu! Hạ nó trước", "A healer! Take it down first"),
                BattleEventKind.BossRallying when !rallyExplained => LF($"{BossName()} sắp phong tinh anh! Đẩy để cắt", $"The {BossName()} is making an elite! Push it to cut it"),
                BattleEventKind.BossRallied when battle.Find((int)e.Value) is Slime rallied => LF($"★ {rallied.Type.Name(PetLanguage.Vietnamese)} thành tinh anh!", $"★ The {rallied.Type.Name(preferences.Language)} turns elite!"),
                BattleEventKind.Incoming when !behindExplained => L("Quái tới từ phía sau!", "Enemies from behind!"),
                BattleEventKind.EnemyWarded when !wardExplained => L("Đang được hồi thì miễn sát thương! Đẩy slime thảo dược", "Healing makes it immune! Push the herb slime"),
                BattleEventKind.EliteAppeared when battle.Find((int)e.Value) is { Elite: EliteSpec elite } enemy && elitesExplained.Add(enemy.Type.Id) =>
                    LF($"★ {enemy.Type.Name(PetLanguage.Vietnamese)} tinh anh: {elite.Describe(PetLanguage.Vietnamese)}", $"★ Elite {enemy.Type.Name(preferences.Language)}: {elite.Describe(preferences.Language)}"),
                BattleEventKind.PushResisted when !pushResistExplained && battle.Find((int)e.Value) is Slime firm =>
                    LF($"★ {firm.Type.Name(PetLanguage.Vietnamese)} tinh anh miễn Đẩy!", $"★ The elite {firm.Type.Name(preferences.Language)} ignores Push!"),
                // Infinity: a milestone reward not won before (it is saved when the run ends).
                BattleEventKind.WaveCleared when battle.Endless && !battleCustomData && EndlessRewards is EndlessSpec rewards &&
                    rewards.MilestoneAt(battle.WavesCleared) is KeepsakeSpec prize && prize.Wave > BattleRecords.EndlessCleared(rewards) =>
                    LF($"★ Mở khóa: {prize.Name(PetLanguage.Vietnamese)}!", $"★ Unlocked: {prize.Name(preferences.Language)}!"),
                // Desktop apps: the first drop and the first popup say what to do; every app used shouts its name.
                BattleEventKind.AppReady when !appExplained => L("App trên màn hình đã sạc đầy! Bấm vào icon để dùng", "An app on the screen is charged! Click its icon"),
                BattleEventKind.AppUsed when AppName(e) is string used => "★ " + used.ToUpper(System.Globalization.CultureInfo.CurrentCulture) + "!",
                BattleEventKind.AppInfected when AppName(e) is string hit => virusExplained
                    ? LF($"☣ {hit} nhiễm virus!", $"☣ {hit} is infected!")
                    : LF($"☣ {hit} nhiễm virus! Bấm icon 3 lần hoặc hạ slime đeo ☣", $"☣ {hit} is infected! Click it 3 times or defeat the slime wearing ☣"),
                BattleEventKind.AppCleaned when AppName(e) is string clean => LF($"✓ {clean} sạch virus!", $"✓ {clean} is clean!"),
                BattleEventKind.PopupOpened when !popupExplained => L("Quảng cáo bật lên! Bấm × để tắt", "An ad popped up! Click × to close it"),
                BattleEventKind.PopupSpawned => L("Quảng cáo thả slime ra!", "The ad let a slime out!"),
                BattleEventKind.HazardOpened when IsUpdate(e) && !updateExplained => L("Cập nhật bắt buộc! Tường chắn mèo và slime. Bấm Nhắc sau 2 lần", "Forced update! It walls off the cat and the slimes. Click Later twice"),
                BattleEventKind.HazardOpened when IsUpdate(e) => L("Đang cập nhật…", "Updating…"),
                BattleEventKind.HazardOpened when !loadingExplained => L("⟳ Vòng loading! Mèo trong vòng bị chậm. Bấm vòng xoay để tắt", "⟳ A loading ring! The cat is slow inside it. Click the spinner to close it"),
                BattleEventKind.HazardOpened => L("⟳ Đang tải…", "⟳ Loading…"),
                BattleEventKind.Won => L("CHIẾN THẮNG!", "VICTORY!"),
                BattleEventKind.Lost => L("THUA RỒI…", "DEFEATED…"),
                _ => null
            };
            if (e.Kind == BattleEventKind.EnemyHealing && e.Value > 0) farHealExplained = true;
            else if (e.Kind == BattleEventKind.EnemyHealing) healerExplained = true;
            if (e.Kind == BattleEventKind.BossRallying) rallyExplained = true;
            if (e.Kind == BattleEventKind.Incoming) behindExplained = true;
            if (e.Kind == BattleEventKind.EnemyWarded) wardExplained = true;
            if (e.Kind == BattleEventKind.PushResisted) pushResistExplained = true;
            if (e.Kind == BattleEventKind.AppReady) appExplained = true;
            if (e.Kind == BattleEventKind.AppInfected) virusExplained = true;
            if (e.Kind == BattleEventKind.PopupOpened) popupExplained = true;
            if (e.Kind == BattleEventKind.HazardOpened && IsUpdate(e)) updateExplained = true;
            else if (e.Kind == BattleEventKind.HazardOpened) loadingExplained = true;
            if (text != null) battleHud.ShowBanner(text, battle.Time, e.Kind is BattleEventKind.Won or BattleEventKind.Lost ? 3 : 1.6);
        }
    }
}
