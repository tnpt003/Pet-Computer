using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// Infinity (PetEndless.cs, 2026-10-22): its hub card, its setup panel's words and its result. The
// battle itself is a normal battle on EndlessSpec.Stage(): the HUD, cards, bag and End know it by
// BattleSim.Endless. Records: BattleRecords (the furthest wave per weapon and difficulty).
public sealed partial class PetWindow
{
    // Milestone rewards: the built-in definitions (as the Expedition's keepsakes), earned from the
    // saved records' waves cleared.
    private static EndlessSpec? EndlessRewards => BattleData.Settings.Endless is { Milestones.Length: > 0 } spec ? spec : null;
    // Read every frame by the keepsake on the desktop: kept until an Infinity run ends.
    private IReadOnlyList<KeepsakeSpec>? endlessEarned;
    private IReadOnlyList<KeepsakeSpec> EndlessEarned => endlessEarned ??=
        EndlessRewards is EndlessSpec spec ? spec.EarnedBy(BattleRecords.EndlessCleared(spec)).ToList() : Array.Empty<KeepsakeSpec>();

    private string EndlessMilestoneText(KeepsakeSpec m) => L($"Vượt qua đợt {m.Wave} ở Vô tận để nhận.", $"Clear wave {m.Wave} in Infinity to get it.");

    // Null when Infinity can be played now, otherwise why not.
    private string? EndlessRefusal()
    {
        if (BattleTuning.Endless is not EndlessSpec spec) return L("Dữ liệu trận không có chế độ Vô tận.", "The battle data has no Infinity mode.");
        if (!spec.IsUnlocked(AdventureProgress, BattleTuning)) return L("Thắng Vua Slime ở Phiêu lưu để mở Vô tận.", "Win the Slime King in the Adventure to open Infinity.");
        return null;
    }

    private string EndlessAbout(EndlessSpec spec) =>
        L("Đợt quái không ngừng, mạnh dần.", "Waves that never stop and keep getting stronger.")
        + (spec.UpgradeEvery > 0 ? L($" Cứ {spec.UpgradeEvery} đợt chọn 1 thẻ.", $" A card every {spec.UpgradeEvery} waves.") : "")
        + (spec.BossEvery > 0 ? L($" Trùm mỗi {spec.BossEvery} đợt.", $" A boss every {spec.BossEvery} waves.") : "")
        + L(" Kết thúc khi mèo thua hoặc bạn bấm Kết thúc.", " It ends when the cat falls or you press End.");

    // "\nRecord (Sword · Normal): wave 12" for the loadout, or nothing yet.
    private string EndlessRecordLine(EndlessSpec spec, string weapon, BattleDifficulty level)
    {
        var best = BattleRecords.ForEndless(spec, weapon, level, BattleDataCustom);
        if (best.MostWaves == 0) return "";
        string loadout = $"{BattleTuning.Weapon(weapon).Name(English)} · {DifficultyName(level)}";
        return "\n" + L($"Kỷ lục ({loadout}): đợt {best.MostWaves} · hạ {best.MostDefeated} slime", $"Record ({loadout}): wave {best.MostWaves} · {best.MostDefeated} slime{(best.MostDefeated == 1 ? "" : "s")}");
    }

    // The hub's card: locked until its Adventure stage is won, then the furthest wave so far.
    private (string Name, string About, string? State, double? Progress, bool Open, string? Ribbon) EndlessCard()
    {
        string title = L("Vô tận", "Infinity");
        string about = L("Trụ càng lâu càng tốt trước những đợt quái không ngừng.", "Hold out as long as you can against endless waves.");
        if (BattleTuning.Endless is not EndlessSpec spec) return (title, about, L("Dữ liệu chưa có chế độ Vô tận", "No Infinity in the data"), null, false, L("Chưa có", "Not available"));
        if (!spec.IsUnlocked(AdventureProgress, BattleTuning)) return (title, about, L("Thắng Vua Slime ở Phiêu lưu để mở", "Win the Slime King in the Adventure to open"), null, false, L("Đang khóa", "Locked"));
        int best = BattleRecords.EndlessBest(spec);
        string state = best > 0 ? L($"Xa nhất: đợt {best}", $"Furthest: wave {best}") : L("Chưa chơi lần nào", "Not played yet");
        // The milestone rewards: how many are won, and the next one's wave.
        if (EndlessRewards is not EndlessSpec rewards) return (title, about, state, null, true, null);
        int had = EndlessEarned.Count, all = rewards.Milestones.Length;
        var next = rewards.Milestones.FirstOrDefault(m => m.Wave > BattleRecords.EndlessCleared(rewards));
        state += L($" · Thưởng {had}/{all}", $" · Rewards {had}/{all}") + (next != null ? L($" · tiếp: qua đợt {next.Wave}", $" · next: clear wave {next.Wave}") : "");
        return (title, about, state, (double)had / all, true, null);
    }

    // The result of an Infinity run: the wave reached, the records, and Play again / Change gear / Hub / Back to desktop.
    private UIElement EndlessResult(BattleSummary result, StackPanel info, string arms, string level, Action<Action> Choose)
    {
        TextBlock Line(double size, Brush? brush = null, bool bold = false, double opacity = 1) => new()
        {
            Foreground = brush ?? Brushes.White, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Opacity = opacity,
            TextWrapping = TextWrapping.Wrap, MaxWidth = 300, Margin = new Thickness(0, 2, 0, 0)
        };
        var reached = Line(PetUi.TitleSize, bold: true);
        reached.Text = L($"Đạt đợt {result.EndlessWave}", $"Reached wave {result.EndlessWave}");
        info.Children.Insert(2, reached);
        if (result.News.Any)
        {
            var news = Line(PetUi.BodySize, RecordGold, true);
            news.Text = result.News.FurthestWave && result.News.MostDefeated ? L("Kỷ lục mới: xa nhất, hạ nhiều slime nhất!", "New record: furthest wave, most slimes!")
                : result.News.FurthestWave ? L("Kỷ lục mới: đợt xa nhất!", "New record: furthest wave!") : L("Kỷ lục mới: hạ nhiều slime nhất!", "New record: most slimes!");
            info.Children.Add(news);
        }
        if (result.EndlessUnlocked is { Count: > 0 } unlocked)
        {
            var prize = Line(PetUi.BodySize, RecordGold, true);
            prize.Text = L("Mở khóa: ", "Unlocked: ") + string.Join(", ", unlocked.Select(m => m.Name(English)))
                + L(" (xem ở Sảnh → Phần thưởng)", " (see Hub → Rewards)");
            info.Children.Add(prize);
        }
        if (BattleTuning.Endless is EndlessSpec spec && BattleRecords.ForEndless(spec, result.Weapon, result.Difficulty, result.Custom) is { MostWaves: > 0 } best)
        {
            var records = Line(PetUi.SmallSize, opacity: 0.85);
            records.Text = L($"Kỷ lục ({arms} · {level}{(result.Custom ? ", dữ liệu tùy chỉnh" : "")}): đợt {best.MostWaves} · hạ {best.MostDefeated} slime",
                $"Records ({arms} · {level}{(result.Custom ? ", custom data" : "")}): wave {best.MostWaves} · {best.MostDefeated} slime{(best.MostDefeated == 1 ? "" : "s")}");
            info.Children.Add(records);
        }
        if (result.Upgrades.Length > 0)
        {
            var build = Line(PetUi.CaptionSize, opacity: 0.7);
            build.Text = L("Build: ", "Build: ") + result.Upgrades;
            info.Children.Add(build);
        }
        var (again, againText, _) = HudButton(() => Choose(() => { if (BattleReplay(result) is string reason) ShowBattleNotice(reason); }), plain: true);
        again.Background = ResultMain; again.Padding = new Thickness(16, 6, 16, 5); again.Margin = new Thickness(0);
        againText.FontSize = PetUi.TitleSize; againText.FontWeight = FontWeights.Bold; againText.Text = L("Chơi lại", "Play again");
        var (gear, gearText, _) = HudButton(() => Choose(() => { if (OpenBattlePrep(result.Weapon, result.Difficulty, forEndless: true) is string reason) ShowBattleNotice(reason); }), plain: true);
        gearText.Text = L("Đổi trang bị", "Change gear");
        var (hub, hubText, _) = HudButton(() => Choose(() => { if (OpenBattleHub() is string reason) ShowBattleNotice(reason); }), plain: true);
        hubText.Text = L("‹ Sảnh", "‹ Hub");
        var (home, homeText, _) = HudButton(() => Choose(() => { }), plain: true);
        homeText.Text = L("Về desktop", "Back to desktop");
        var actions = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
        DockPanel.SetDock(home, Dock.Left); DockPanel.SetDock(hub, Dock.Left); DockPanel.SetDock(again, Dock.Right); DockPanel.SetDock(gear, Dock.Right);
        actions.Children.Add(home); actions.Children.Add(hub); actions.Children.Add(again); actions.Children.Add(gear);
        return actions;
    }
}
