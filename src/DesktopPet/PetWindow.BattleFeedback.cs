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
        [BattleEventKind.HazardTapped] = ("ball_tap", 3), [BattleEventKind.WallHit] = ("clank", 3), [BattleEventKind.PopupLocked] = ("clank", 3),
        [BattleEventKind.VirusFileAdded] = ("squish", 4), [BattleEventKind.VirusFileDeleted] = ("pop", 4),
        [BattleEventKind.AppAttack] = ("swoosh", 3), [BattleEventKind.AppReady] = ("ball_tap", 2), [BattleEventKind.AppMoved] = ("swoosh", 1),
        // 1.5 M05: the Laser and the Fan (no cue of their own yet: the shared cartoon cues).
        [BattleEventKind.LaserOn] = ("sparkle", 3), [BattleEventKind.FanOn] = ("swoosh", 2), [BattleEventKind.FanBump] = ("ball_tap", 3),
        [BattleEventKind.FanSpent] = ("clank", 2),
        // 1.5 M06: the Grabber, the Scissors and the Spray (the shared cartoon cues as well).
        [BattleEventKind.Grabbed] = ("squish", 3), [BattleEventKind.GrabReleased] = ("swoosh", 2), [BattleEventKind.GrabHit] = ("ball_tap", 4),
        [BattleEventKind.Snipped] = ("swoosh", 3), [BattleEventKind.SprayOn] = ("squish", 2), [BattleEventKind.SpraySpent] = ("clank", 2),
        // 1.5 M07: the rage bar full, the cat raging.
        [BattleEventKind.RageFull] = ("sparkle", 5), [BattleEventKind.Raged] = ("excited", 6),
        // 1.5 M08: the glove's combo tossed a slime up.
        [BattleEventKind.Launched] = ("swoosh", 3),
        // 1.5 M09: the spitter and the spring trap (the shared cartoon cues).
        [BattleEventKind.SpitAimed] = ("squish", 2), [BattleEventKind.GooLanded] = ("squish", 3), [BattleEventKind.PuddleBoost] = ("swoosh", 3),
        [BattleEventKind.PuddleDried] = ("pop", 2), [BattleEventKind.SpringSqueezed] = ("clank", 4), [BattleEventKind.SpringBounced] = ("ball_tap", 5),
        [BattleEventKind.SpringFlattened] = ("squish", 4),
        // 1.5 M10: the drummer's beats and strike, the chain slime's rope.
        [BattleEventKind.LinkBeat] = ("ball_tap", 1), [BattleEventKind.LinkStrike] = ("clank", 4), [BattleEventKind.LinkFormed] = ("squish", 2),
        [BattleEventKind.LinkCut] = ("pop", 4), [BattleEventKind.LinkSnapped] = ("pop", 2),
        // 1.5 M11: the fusing slimes, the bomb slime, the splitting slime.
        [BattleEventKind.Merged] = ("squish", 4), [BattleEventKind.Dismounted] = ("swoosh", 2), [BattleEventKind.FuseLit] = ("clank", 4),
        [BattleEventKind.Blasted] = ("pop", 6), [BattleEventKind.SlimeSplit] = ("squish", 4),
        // 1.5 M12: the flag fell (its team panics), a formation came.
        [BattleEventKind.Panicked] = ("surprised", 5), [BattleEventKind.FormationArrived] = ("sparkle", 4),
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
    // A link made by a tether's holder (a rider mounting makes one too).
    private bool IsRope(BattleEvent e) => EventSlime(e)?.Link?.Kind == EnemyLinks.Tether;
    private bool IsRide(BattleEvent e) => EventSlime(e)?.Link?.Kind == EnemyLinks.Ride;
    private bool FlagOnField()
    {
        if (battle == null) return false;
        foreach (var s in battle.Slimes) if (s.Link?.Kind == EnemyLinks.Aura && s.State != SlimeState.Defeated) return true;
        return false;
    }
    // The formation a FormationArrived event names (null: none).
    private FormationSpec? FormationOf(BattleEvent e) =>
        battle != null && (int)e.Value >= 0 && (int)e.Value < battle.Settings.FormationList.Count ? battle.Settings.FormationList[(int)e.Value] : null;
    private Slime? EventSlime(BattleEvent e)
    {
        if (battle == null) return null;
        foreach (var s in battle.Slimes) if (s.Id == (int)e.Value) return s;
        return null;
    }
    private bool IsUpdate(BattleEvent e) => battle?.FindHazard((int)e.Value) is { Kind: HazardKind.Update };

    private string BossName() => battle?.Boss?.Type.Name(preferences.Language) ?? "Boss";

    // Big moments get a banner across the HUD's centre. The first heal, far heal, rally call and attack
    // from behind of a battle say what is going on (once each per battle), and so does the first elite
    // of each kind; every helper a rally makes elite is named.
    private bool healerExplained, farHealExplained, rallyExplained, behindExplained, wardExplained, pushResistExplained, appExplained, popupExplained, virusExplained, loadingExplained, updateExplained, virusExeExplained, lockExplained, rageExplained, spitExplained, puddleExplained, springExplained, flatExplained, beatExplained, ropeExplained, cutExplained, mergeExplained, remergeExplained, rideExplained, fuseExplained, splitExplained, flagExplained, panicExplained;
    private readonly HashSet<string> elitesExplained = new();
    private readonly HashSet<string> formationsExplained = new();
    private void ShowBattleBanners()
    {
        if (battle == null || battleHud == null) return;
        if (battle.Time < 0.05) { healerExplained = farHealExplained = rallyExplained = behindExplained = wardExplained = pushResistExplained = appExplained = popupExplained = virusExplained = loadingExplained = updateExplained = virusExeExplained = lockExplained = rageExplained = spitExplained = puddleExplained = springExplained = flatExplained = beatExplained = ropeExplained = cutExplained = mergeExplained = remergeExplained = rideExplained = fuseExplained = splitExplained = flagExplained = panicExplained = false; elitesExplained.Clear(); formationsExplained.Clear(); }
        var rules = battle.AppRules;
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
                    : LF($"☣ {hit} nhiễm virus! Bấm icon {rules?.ScrubClicks ?? 3} lần hoặc hạ slime đeo ☣", $"☣ {hit} is infected! Click it {rules?.ScrubClicks ?? 3} times or defeat the slime wearing ☣"),
                BattleEventKind.AppCleaned when AppName(e) is string clean => LF($"✓ {clean} sạch virus!", $"✓ {clean} is clean!"),
                BattleEventKind.PopupOpened when !popupExplained && battle.PopupsLocked => L("Quảng cáo! Gỡ virus khỏi Tab rồi bấm × để tắt", "Ads! Clean the Tabs icon, then click × to close them"),
                BattleEventKind.PopupOpened when !popupExplained => L("Quảng cáo bật lên! Bấm × để tắt", "Ads popped up! Click × to close them"),
                BattleEventKind.PopupLocked when !lockExplained => L("Tab nhiễm virus giữ quảng cáo! Bấm icon Tab để gỡ trước", "The infected Tabs keeps the ads open! Clean its icon first"),
                BattleEventKind.PopupSpawned => L("Quảng cáo thả slime ra!", "The ad let a slime out!"),
                BattleEventKind.VirusFileAdded when !virusExeExplained && battle.AppRules is AppsSpec exe =>
                    LF($"virus.exe tự nhân bản! Bấm từng file để xóa trước khi đủ {exe.VirusExeCopies}", $"virus.exe copies itself! Click each file before there are {exe.VirusExeCopies}"),
                BattleEventKind.HazardOpened when IsUpdate(e) && !updateExplained => LF($"Cập nhật bắt buộc! Tường chắn mèo và slime. Bấm Nhắc sau {rules?.UpdateClicks ?? 2} lần", $"Forced update! It walls off the cat and the slimes. Click Later {rules?.UpdateClicks ?? 2} times"),
                BattleEventKind.HazardOpened when IsUpdate(e) => L("Đang cập nhật…", "Updating…"),
                BattleEventKind.HazardOpened when !loadingExplained && rules is { LoadingHold: > 0 } hold =>
                    LF($"⟳ Vòng loading! Mèo trong vòng bị chậm. Giữ chuột trên vòng xoay {hold.LoadingHold:0.#} giây để tắt", $"⟳ A loading ring! The cat is slow inside it. Hold the spinner {hold.LoadingHold:0.#} s to close it"),
                BattleEventKind.HazardOpened when !loadingExplained => L("⟳ Vòng loading! Mèo trong vòng bị chậm. Bấm vòng xoay để tắt", "⟳ A loading ring! The cat is slow inside it. Click the spinner to close it"),
                BattleEventKind.HazardOpened => L("⟳ Đang tải…", "⟳ Loading…"),
                BattleEventKind.RageFull when !rageExplained => L("Thanh nộ đầy! Bấm vào thanh để mèo nổi giận", "The rage bar is full! Click it to make the cat rage"),
                BattleEventKind.Raged => L("MÈO NỔI GIẬN!", "THE CAT RAGES!"),
                // 1.5 M09: the spitter and the spring trap, once each per battle.
                BattleEventKind.SpitAimed when !spitExplained => L("Slime phun! Nhấc mèo khỏi vòng cam", "A spitter! Lift the cat out of the orange ring"),
                BattleEventKind.PuddleBoost when !puddleExplained => L("Vũng nhầy làm slime lao xa hơn! Dùng Quạt làm khô", "Goo puddles make dashes longer! Dry them with the Fan"),
                BattleEventKind.SpringSqueezed when !springExplained => L("Lò xo đang nén! Nhấc mèo ra khỏi vòng", "The spring is squeezing! Lift the cat out of the ring"),
                BattleEventKind.SpringFlattened when !flatExplained => L("Lò xo bẹp rồi! Nó nằm im một lúc", "The spring is flattened! It lies still a while"),
                // 1.5 M10: the drummer and the chain slime, once each per battle.
                BattleEventKind.LinkBeat when !beatExplained => L("Nhạc trưởng đếm nhịp! Đẩy nó trước nhịp cuối", "The drummer counts! Push it before the last beat"),
                BattleEventKind.LinkFormed when !ropeExplained && IsRope(e) => L("Dây nhầy! Ném hoặc kéo mèo xuyên dây, hay dùng Kéo cắt", "A goo rope! Throw or drag the cat through it, or use the Scissors"),
                BattleEventKind.LinkCut when !cutExplained => L("Đứt dây! Đồng đội choáng", "Rope cut! Its ally is dazed"),
                // 1.5 M11: the fusing slimes, the bomb slime, the splitting slime, once each per battle.
                BattleEventKind.Merged when !remergeExplained && EventSlime(e)?.Type.Split != null => L("Hai nửa nhập lại! Hạ slime thảo dược trước", "The halves merged back! Take down the herb slime first"),
                BattleEventKind.Merged when !mergeExplained && EventSlime(e)?.Type.Split == null => L("Slime nhập thành khổng lồ! Lần sau Đẩy hoặc Quạt tách bầy ra", "The slimes merged into a giant! Push or fan the pack apart next time"),
                BattleEventKind.LinkFormed when !rideExplained && IsRide(e) => L("Slime bom cưỡi slime nhanh! Hạ con ngựa giữa đường", "A bomb rides a quick slime! Take the mount down on the way"),
                BattleEventKind.FuseLit when !fuseExplained => L("Bom châm ngòi! Nhấc mèo ra khỏi vòng đỏ", "The fuse is lit! Lift the cat out of the red ring"),
                BattleEventKind.SlimeSplit when !splitExplained => L("Slime tách đôi! Đừng để thảo dược hồi cho hai nửa", "It split in two! Don't let a herb slime heal the halves"),
                // 1.5 M12: the flag slime and its panic, once each per battle; each formation the first time it comes.
                BattleEventKind.SlimeSpawned when !flagExplained && FlagOnField() => L("Slime cờ hiệu! Quái trong vòng vàng chạy nhanh hơn. Hạ nó trước", "A flag slime! Enemies in the gold ring run faster. Take it down first"),
                BattleEventKind.Panicked when !panicExplained => L("Mất cờ! Cả đội hoảng loạn", "The flag fell! They all panic"),
                BattleEventKind.FormationArrived when FormationOf(e) is FormationSpec team && formationsExplained.Add(team.Id) =>
                    LF($"Đội hình {team.Name(PetLanguage.Vietnamese)}! {team.Tip(PetLanguage.Vietnamese)}", $"{team.Name(preferences.Language)} formation! {team.Tip(preferences.Language)}").TrimEnd(),
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
            if (e.Kind == BattleEventKind.PopupLocked) lockExplained = true;
            if (e.Kind == BattleEventKind.RageFull) rageExplained = true;
            if (e.Kind == BattleEventKind.SpitAimed) spitExplained = true;
            if (e.Kind == BattleEventKind.PuddleBoost) puddleExplained = true;
            if (e.Kind == BattleEventKind.SpringSqueezed) springExplained = true;
            if (e.Kind == BattleEventKind.SpringFlattened) flatExplained = true;
            if (e.Kind == BattleEventKind.LinkBeat) beatExplained = true;
            if (e.Kind == BattleEventKind.LinkFormed && IsRope(e)) ropeExplained = true;
            if (e.Kind == BattleEventKind.LinkCut) cutExplained = true;
            if (e.Kind == BattleEventKind.Merged && EventSlime(e)?.Type.Split != null) remergeExplained = true;
            else if (e.Kind == BattleEventKind.Merged) mergeExplained = true;
            if (e.Kind == BattleEventKind.LinkFormed && IsRide(e)) rideExplained = true;
            if (e.Kind == BattleEventKind.FuseLit) fuseExplained = true;
            if (e.Kind == BattleEventKind.SlimeSplit) splitExplained = true;
            if (e.Kind == BattleEventKind.SlimeSpawned && FlagOnField()) flagExplained = true;
            if (e.Kind == BattleEventKind.Panicked) panicExplained = true;
            if (e.Kind == BattleEventKind.VirusFileAdded) virusExeExplained = true;
            if (e.Kind == BattleEventKind.HazardOpened && IsUpdate(e)) updateExplained = true;
            else if (e.Kind == BattleEventKind.HazardOpened) loadingExplained = true;
            if (text != null) battleHud.ShowBanner(text, battle.Time, e.Kind is BattleEventKind.Won or BattleEventKind.Lost ? 3 : 1.6);
        }
    }
}
