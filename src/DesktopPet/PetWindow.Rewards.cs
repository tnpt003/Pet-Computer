using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// The battle hub's Rewards: what was won and the best results, in place of the mode cards (the
// hub's header, Close and rules stay). A tile per Adventure accessory (this cat wearing it) and per
// Expedition keepsake (as it stands on the keepsake) and per Infinity milestone (2026-10-22), won ones in colour with where they show, the
// others greyed with a lock and what wins them (said when the mouse is over a tile). Rewards not
// shown here before carry a "New" badge, and the hub's button counts them; showing the page marks
// them seen (AdventureProgress.RewardsSeen, saved with the Adventure progress). Then the records:
// each Adventure stage (fastest win, wins) and the Expedition per difficulty (furthest step, wins,
// fastest run), over every weapon. View only otherwise.
public sealed partial class PetWindow
{
    private bool hubRewardsShown;
    private string? hubRewardsLanguage;
    private StackPanel? hubRewards;
    private FrameworkElement? hubCardsRow;
    private TextBlock? hubRewardsText;
    private TextBlock? rewardsDetail;
    // The rewards that were new when the page opened (their badges stay while it is open).
    private HashSet<string> rewardsFresh = new();
    private const double RewardsWidth = 536;

    public bool BattleRewardsShown => battleHubOpen && hubRewardsShown;
    // For checks: the page's texts, and how many rewards are new now.
    public string BattleRewardsText => hubRewards is null || !hubRewardsShown ? "" : string.Join("\n", BagTexts(hubRewards));
    public int BattleRewardsNew => NewRewards().Count;

    // Every reward held: accessories won in the Adventure, keepsakes won in the Expedition.
    private List<string> HeldRewards()
    {
        var held = new List<string>();
        if (Adventure is Adventure adventure)
            foreach (var stage in adventure.Campaign.Chapters.SelectMany(c => c.Stages))
                if (stage.Reward is string id && adventure.HasReward(id) && !held.Contains(id)) held.Add(id);
        if (Expedition is Expedition x)
            foreach (var k in x.Spec.Keepsakes)
                if (x.Progress.Keepsakes.Contains(k.Id) && !held.Contains(k.Id)) held.Add(k.Id);
        foreach (var m in EndlessEarned)
            if (!held.Contains(m.Id)) held.Add(m.Id);
        return held;
    }

    private List<string> NewRewards() => HeldRewards().Where(id => !AdventureProgress.RewardsSeen.Contains(id)).ToList();

    private string RewardsButtonText()
    {
        int fresh = BattleRewardsNew;
        return L("Phần thưởng", "Rewards") + (fresh > 0 ? LF($" · {fresh} mới", $" · {fresh} new") : "");
    }

    // The hub's Rewards button (and its ‹ Hub): null on success, otherwise why not.
    public string? ShowBattleRewards(bool show)
    {
        if (!battleHubOpen || hubRewards == null || hubCardsRow == null) return L("Sảnh chiến đấu chưa mở.", "The battle hub is not open.");
        if (show == hubRewardsShown) return null;
        hubRewardsShown = show;
        if (show)
        {
            // New ones keep their badge while the page is open; from now on they count as seen.
            var fresh = NewRewards();
            rewardsFresh = fresh.ToHashSet();
            FillRewards();
            if (fresh.Count > 0)
            {
                AdventureProgress.RewardsSeen.AddRange(fresh);
                if (!BattleDataCustom && AdventureProgressFile != null) adventureSaveFailed = !AdventureProgress.Save(AdventureProgressFile);
            }
        }
        hubRewards.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        hubCardsRow.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        UpdateBattleHub();
        PlaceGamePanel(battleHubWindow);
        System.Diagnostics.Trace.WriteLine(show ? "Battle rewards opened." : "Battle rewards closed.");
        return null;
    }

    // Every hub refresh: only a language change rebuilds it (nothing it shows changes while open).
    private void RefreshRewards()
    {
        if (hubRewardsShown && hubRewardsLanguage != preferences.Language) FillRewards();
    }

    private void FillRewards()
    {
        if (hubRewards == null) return;
        hubRewardsLanguage = preferences.Language;
        hubRewards.Children.Clear();
        var adventure = Adventure;
        var expedition = Expedition;
        rewardsDetail = BagLine(L("Rê chuột lên một ô để xem cách nhận hoặc nơi dùng.", "Hover a tile to see how to win it or where it shows."), PetUi.CaptionSize, PetUi.Game.Soft);
        rewardsDetail.Margin = new Thickness(2, 2, 0, 0); rewardsDetail.MinHeight = 18;

        // The Adventure's accessories, by the stage that gives each.
        if (adventure != null)
        {
            var tiles = new WrapPanel();
            foreach (var stage in adventure.Campaign.Chapters.SelectMany(c => c.Stages))
                if (stage.Reward is string id && PetAccessories.Find(id) is AccessorySpec accessory)
                {
                    bool won = adventure.HasReward(id);
                    tiles.Children.Add(RewardTile(WearingCat(id), accessory.Name(preferences.Language), won, rewardsFresh.Contains(id),
                        won ? L("Phụ kiện: đeo trong Cài đặt → Mèo → Phụ kiện.", "Accessory: wear it from Settings → Pet → Accessory.")
                            : LF($"Thắng {StageTitle(stage)} để nhận.", $"Win {StageTitle(stage)} to get it.")));
                }
            if (tiles.Children.Count > 0) RewardsSection(L("Phụ kiện Phiêu lưu", "Adventure accessories"), tiles);
        }

        // The Expedition's keepsakes: a cup or cushion as on the keepsake, an accessory on the cat.
        if (expedition != null && expedition.Spec.Keepsakes.Length > 0)
        {
            var tiles = new WrapPanel();
            foreach (var k in expedition.Spec.Keepsakes)
            {
                bool won = expedition.Progress.Keepsakes.Contains(k.Id);
                FrameworkElement art = k.Kind == KeepsakeSpec.AccessoryKind && k.Accessory != null ? WearingCat(k.Accessory)
                    : new Viewbox { Width = 64, Height = 56, Child = new KeepsakeArt { Items = new[] { k }, Width = KeepsakeArt.WidthFor(new[] { k }), Height = KeepsakeArt.ArtHeight } };
                string level = Enum.TryParse<BattleDifficulty>(k.Difficulty, out var at) ? DifficultyName(at) : k.Difficulty;
                string where = k.Kind switch
                {
                    KeepsakeSpec.Cup => L("Cúp: trưng bày trên màn hình (chọn trong Cài đặt → Mèo).", "Cup: displayed on the screen (choose it in Settings → Pet)."),
                    KeepsakeSpec.Cushion => L("Đệm: trưng bày trên màn hình; mèo ngủ và nảy trên đệm.", "Cushion: displayed on the screen; the cat naps and bounces on it."),
                    _ => L("Phụ kiện: đeo trong Cài đặt → Mèo → Phụ kiện.", "Accessory: wear it from Settings → Pet → Accessory.")
                };
                tiles.Children.Add(RewardTile(art, k.Name(preferences.Language), won, rewardsFresh.Contains(k.Id), won ? where
                    : k.Exact ? LF($"Thắng một chuyến Viễn chinh {level} để nhận.", $"Win an Expedition run on {level} to get it.")
                    : LF($"Thắng một chuyến Viễn chinh {level} trở lên để nhận.", $"Win an Expedition run on {level} or harder to get it.")));
            }
            RewardsSection(L("Kỷ vật Viễn chinh", "Expedition keepsakes"), tiles);
        }

        // Infinity's milestones: one per wave to clear.
        if (EndlessRewards is EndlessSpec infinity)
        {
            var tiles = new WrapPanel();
            var earned = EndlessEarned;
            foreach (var m in infinity.Milestones)
            {
                bool won = earned.Contains(m);
                FrameworkElement art = m.Kind == KeepsakeSpec.AccessoryKind && m.Accessory != null ? WearingCat(m.Accessory)
                    : new Viewbox { Width = 64, Height = 56, Child = new KeepsakeArt { Items = new[] { m }, Width = KeepsakeArt.WidthFor(new[] { m }), Height = KeepsakeArt.ArtHeight } };
                string where = m.Kind == KeepsakeSpec.AccessoryKind
                    ? L("Phụ kiện: đeo trong Cài đặt → Mèo → Phụ kiện.", "Accessory: wear it from Settings → Pet → Accessory.")
                    : L("Kỷ vật: trưng bày trên màn hình (chọn trong Cài đặt → Mèo).", "Keepsake: displayed on the screen (choose it in Settings → Pet).");
                tiles.Children.Add(RewardTile(art, LF($"Đợt {m.Wave} · ", $"Wave {m.Wave} · ") + m.Name(preferences.Language), won, rewardsFresh.Contains(m.Id), won ? where : EndlessMilestoneText(m)));
            }
            RewardsSection(L("Phần thưởng Vô tận", "Infinity rewards"), tiles);
        }
        if (hubRewards.Children.Count == 0) hubRewards.Children.Add(BagCard(BagLine(L("Chưa có phần thưởng nào trong dữ liệu.", "No rewards in the data."), PetUi.SmallSize, PetUi.Game.Soft)));
        else hubRewards.Children.Add(rewardsDetail);

        // Records, over every weapon.
        var weapons = BattleTuning.WeaponList;
        var levels = new[] { BattleDifficulty.Easy, BattleDifficulty.Normal, BattleDifficulty.Hard };
        var records = new StackPanel();
        if (adventure?.Chapter() is ChapterSpec chapter)
        {
            records.Children.Add(BagLine(chapter.Name(preferences.Language), PetUi.SmallSize, Brushes.White, bold: true));
            foreach (var stage in chapter.Stages)
            {
                var all = weapons.SelectMany(w => levels.Select(l => adventure.Record(stage, w.Id, l))).ToList();
                int wins = all.Sum(r => r.Wins);
                double? best = all.Where(r => r.BestSeconds is double).Select(r => r.BestSeconds!.Value).DefaultIfEmpty().Min() is double s && s > 0 ? s : null;
                records.Children.Add(RecordLine(StageTitle(stage), wins == 0 ? L("chưa thắng", "not won yet")
                    : LF($"{wins} lần thắng", $"{wins} win{(wins == 1 ? "" : "s")}") + (best is double b ? LF($" · nhanh nhất {Clock(b)}", $" · fastest {Clock(b)}") : ""), wins > 0));
            }
        }
        if (expedition != null && expedition.IsUnlocked(AdventureProgress))
        {
            var heading = BagLine(L("Viễn chinh", "Expedition"), PetUi.SmallSize, Brushes.White, bold: true);
            heading.Margin = new Thickness(0, records.Children.Count == 0 ? 0 : 8, 0, 0);
            records.Children.Add(heading);
            foreach (var level in levels)
            {
                var all = weapons.Select(w => expedition.Record(w.Id, level)).ToList();
                int runs = all.Sum(r => r.Runs), wins = all.Sum(r => r.Wins), steps = all.Max(r => r.BestSteps);
                double? best = all.Where(r => r.BestSeconds is double).Select(r => r.BestSeconds!.Value).DefaultIfEmpty().Min() is double s && s > 0 ? s : null;
                records.Children.Add(RecordLine(DifficultyName(level), runs == 0 ? L("chưa đi chuyến nào", "no runs yet")
                    : LF($"xa nhất bậc {steps}/{expedition.Spec.StepCount} · {wins}/{runs} chuyến thắng", $"furthest step {steps}/{expedition.Spec.StepCount} · {wins}/{runs} runs won")
                      + (best is double b ? LF($" · nhanh nhất {Clock(b)}", $" · fastest {Clock(b)}") : ""), wins > 0));
            }
        }
        if (BattleTuning.Endless is EndlessSpec endless && EndlessRefusal() == null)
        {
            var heading = BagLine(L("Vô tận", "Infinity"), PetUi.SmallSize, Brushes.White, bold: true);
            heading.Margin = new Thickness(0, records.Children.Count == 0 ? 0 : 8, 0, 0);
            records.Children.Add(heading);
            int best = BattleRecords.EndlessBest(endless);
            records.Children.Add(RecordLine(L("Xa nhất", "Furthest"), best == 0 ? L("chưa chơi lần nào", "not played yet") : LF($"đợt {best}", $"wave {best}"), best > 0));
        }
        if (records.Children.Count > 0) RewardsSection(L("Kỷ lục", "Records"), records);
    }

    private void RewardsSection(string title, UIElement content)
    {
        var heading = PanelLine(PetUi.TitleSize, true, RewardsWidth);
        heading.Text = title;
        heading.Margin = new Thickness(0, hubRewards!.Children.Count == 0 ? 0 : 10, 0, 6);
        hubRewards.Children.Add(heading);
        hubRewards.Children.Add(BagCard(content));
    }

    private static UIElement RecordLine(string what, string result, bool done)
    {
        var line = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        var icon = new BagIcon { Kind = "cup", Width = 16, Height = 16, Opacity = done ? 1 : 0.35, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Top };
        var name = BagLine(what, PetUi.SmallSize, Brushes.White); name.Margin = new Thickness(0); name.Width = 190;
        var text = BagLine(result, PetUi.SmallSize, done ? PetUi.Game.Gold : PetUi.Game.Soft); text.Margin = new Thickness(0);
        DockPanel.SetDock(icon, Dock.Left); DockPanel.SetDock(name, Dock.Left);
        line.Children.Add(icon); line.Children.Add(name); line.Children.Add(text);
        return line;
    }

    // This cat wearing an accessory, as a small picture.
    private FrameworkElement WearingCat(string accessory)
    {
        var view = new PetRenderer(new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1 })
            { Coat = renderer.Coat, Accessory = accessory, Width = 192, Height = 192 };
        var picture = CatPicture(new Grid { Width = 192, Height = 192, Children = { view } });
        picture.Width = 64; picture.Height = 64;
        return picture;
    }

    // A reward's tile: the picture (greyed with a lock when not won), its name, a "New" badge.
    private Border RewardTile(FrameworkElement art, string name, bool won, bool fresh, string about)
    {
        art.HorizontalAlignment = HorizontalAlignment.Center;
        art.Opacity = won ? 1 : 0.3;
        var picture = new Grid { Height = 66, Children = { art } };
        if (!won) picture.Children.Add(new BagIcon { Kind = "lock", Width = 22, Height = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        var label = new TextBlock { Text = (won ? "✓ " : "") + name, Foreground = won ? PetUi.Game.Gold : PetUi.Game.Soft, FontSize = PetUi.CaptionSize, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0), MaxWidth = 104 };
        var face = new Grid { Children = { new StackPanel { Children = { picture, label } } } };
        if (fresh)
            face.Children.Add(new Border { Background = PetUi.Accent, CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 0, 6, 1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = L("Mới", "New"), Foreground = Brushes.White, FontSize = PetUi.CaptionSize, FontWeight = FontWeights.Bold } });
        var tile = new Border
        {
            Width = 118, MinHeight = 104, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(6), CornerRadius = new CornerRadius(10),
            Background = won ? PetUi.Game.CardBack : Brushes.Transparent, BorderBrush = won ? PetUi.Game.Gold : PetUi.Game.CardRim, BorderThickness = new Thickness(won ? 1.8 : 1.2),
            Child = face
        };
        tile.MouseEnter += (_, _) => { if (rewardsDetail != null) { rewardsDetail.Text = name + ": " + about; rewardsDetail.Foreground = Brushes.White; } };
        return tile;
    }
}
