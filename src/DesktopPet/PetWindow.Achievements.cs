using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// 1.5 M28 (docs/UPDATE-1.5.md L2, L3): battle achievements (Assets/achievements.json). What a battle did
// is counted when it ends or is left (once per battle, never with custom battle data) into the
// AchievementBook (achievements.json, .tmp + .bak). Earned ones show on the result panel and at the top
// of the hub's Rewards page, where each carries "New" until seen (AdventureProgress.RewardsSeen, as
// "a:<id>"); six of them give a coat or an accessory that Customize offers from then on. View only
// otherwise; Python has no part in it.
public sealed partial class PetWindow
{
    private AchievementBook? achievementBook;
    private BattleSim? achievementsCounted;
    private const double AchievementWidth = 252;

    public AchievementBook AchievementBook =>
        achievementBook ??= persistPreferences ? AchievementBook.Load(AchievementBook.DefaultPath) : new();

    private void SaveAchievements() { if (persistPreferences && achievementBook != null) achievementBook.Save(AchievementBook.DefaultPath); }

    // The Rewards page's key for an earned achievement (its "New" badge).
    private static string AchievementKey(AchievementSpec a) => "a:" + a.Id;

    // A coat or accessory an earned achievement gave.
    public bool AchievementReward(string id) => AchievementBook.HasReward(id, PetAchievements.All);

    // Coats offered in Settings: the plain ones, earned rewards and the one already worn.
    public bool CoatAvailable(string id) =>
        PetCoats.All.FirstOrDefault(c => c.Id == id) is not { Reward: not null } || preferences.Coat == id || AchievementReward(id);

    // Once per battle (its end, or the battle left early). Returns the achievements it earned.
    private List<AchievementSpec> CountAchievements(BattleSim b)
    {
        if (achievementsCounted == b) return new();
        achievementsCounted = b;
        // 1.5 M29a: nor a Custom Fight (it gives no rewards; the Default one is the old free battle).
        if (battleCustomData || b.Stage is { Custom: true }) return new();
        var earned = AchievementBook.Add(PetAchievements.All, AchievementStats.From(b), out bool changed);
        if (changed) SaveAchievements();
        if (earned.Count > 0) System.Diagnostics.Trace.WriteLine($"Achievements earned: {string.Join(", ", earned.Select(a => a.Id))}.");
        return earned;
    }

    // What an achievement's reward is, as one line ("" without one).
    private string AchievementPrize(AchievementSpec a)
    {
        if (PetCoats.All.FirstOrDefault(c => c.Id == a.Reward) is CoatSpec coat) return L("Bộ lông ", "Coat ") + L(coat.NameVi, coat.NameEn);
        if (PetAccessories.Find(a.Reward) is AccessorySpec item) return L("Phụ kiện ", "Accessory ") + item.Name(preferences.Language);
        return "";
    }

    // The Rewards page's first section: every achievement, two to a row.
    private void AchievementsSection()
    {
        var all = PetAchievements.All;
        if (all.Count == 0) return;
        var book = AchievementBook;
        var tiles = new WrapPanel();
        foreach (var a in all) tiles.Children.Add(AchievementTile(a, book));
        RewardsSection(LF($"Thành tựu · {all.Count(a => book.Has(a.Id))}/{all.Count}", $"Achievements · {all.Count(a => book.Has(a.Id))}/{all.Count}"), PetMotif.Trophy, tiles);
    }

    private Border AchievementTile(AchievementSpec a, AchievementBook book)
    {
        bool done = book.Has(a.Id);
        // The reward on the cat (a coat, or this cat wearing the accessory), otherwise a cup.
        FrameworkElement art = a.Reward == null ? new BagIcon { Kind = "cup", Width = 26, Height = 26 }
            : PetCoats.All.Any(c => c.Id == a.Reward) ? WearingCat(renderer.Accessory, a.Reward, 44) : WearingCat(a.Reward, null, 44);
        art.Opacity = done ? 1 : 0.35;
        art.HorizontalAlignment = HorizontalAlignment.Center; art.VerticalAlignment = VerticalAlignment.Center;
        var picture = new Grid { Width = 46, Height = 46, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top, Children = { art } };
        var text = new StackPanel();
        var name = BagLine((done ? "✓ " : "") + a.Name(preferences.Language), PetUi.SmallSize, done ? PetUi.Game.Gold : Brushes.White, bold: true); name.Margin = new Thickness(0);
        text.Children.Add(name);
        text.Children.Add(BagLine(a.Describe(preferences.Language), PetUi.CaptionSize, PetUi.Game.Soft));
        string state = done ? L("Đã đạt", "Done") : $"{Math.Floor(book.Progress(a)):0}/{a.Goal:0}";
        string prize = AchievementPrize(a);
        if (prize.Length > 0) state += L(" · thưởng: ", " · reward: ") + prize;
        text.Children.Add(BagLine(state, PetUi.CaptionSize, done ? PetUi.Game.Gold : PetUi.Game.Soft));
        var row = new DockPanel { Children = { picture, text } };
        DockPanel.SetDock(picture, Dock.Left);
        var face = new Grid { Children = { row } };
        if (rewardsFresh.Contains(AchievementKey(a)))
            face.Children.Add(new Border { Background = PetUi.Accent, CornerRadius = new CornerRadius(8), Padding = new Thickness(6, 0, 6, 1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = L("Mới", "New"), Foreground = Brushes.White, FontSize = PetUi.CaptionSize, FontWeight = FontWeights.Bold } });
        var tile = new Border
        {
            Width = AchievementWidth - 8, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(6), CornerRadius = new CornerRadius(10),
            Background = done ? PetUi.Game.CardBack : Brushes.Transparent, BorderBrush = done ? PetUi.Game.Gold : PetUi.Game.CardRim, BorderThickness = new Thickness(done ? 1.6 : 1),
            Child = face
        };
        if (prize.Length > 0)
            tile.MouseEnter += (_, _) =>
            {
                if (rewardsDetail == null) return;
                rewardsDetail.Text = prize + ": " + (done ? (PetCoats.All.Any(c => c.Id == a.Reward) ? L("chọn trong Tùy chỉnh → Bộ lông.", "choose it from Customize → Coat.")
                    : L("đeo trong Tùy chỉnh → Phụ kiện.", "wear it from Customize → Accessory.")) : LF($"đạt \"{a.Name(PetLanguage.Vietnamese)}\" để nhận.", $"earn \"{a.Name(preferences.Language)}\" to get it."));
                rewardsDetail.Foreground = Brushes.White;
            };
        return tile;
    }
}
