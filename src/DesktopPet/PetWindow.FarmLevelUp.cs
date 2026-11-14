using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// The farm's level up (docs/FARM-GAME.md → Báo lên cấp): a card of its own over the field, or over
// the sign when the field is folded, that says the new level, the coins and what it opened. It pops
// in, stays a few seconds and no other label takes its place: weather and gift labels wait for it.
// A farm achievement uses the same card, smaller and mint-rimmed, after the level up. A star stays on the sign's field board until the field is out again. Words and a
// chime only: it takes no clicks and never touches the cat.
public sealed partial class PetWindow
{
    public const double FarmLevelCardSeconds = 4.5, FarmAchievementCardSeconds = 3.5;
    private const double FarmLevelPopSeconds = 0.3, FarmLevelFadeSeconds = 0.6, FarmLevelChimeDelay = 0.8, FarmLevelPopFrom = 0.6;
    private const int FarmNoticeLimit = 4;

    private sealed class FarmLevelCard(Border card, TextBlock title, TextBlock unlocks, ScaleTransform scale)
    {
        public Border Card { get; } = card;
        public TextBlock Title { get; } = title;
        public TextBlock Unlocks { get; } = unlocks;
        public ScaleTransform Scale { get; } = scale;
    }

    private FarmLevelCard? farmLevelField, farmLevelDock, farmLevelShown;
    private double farmLevelLeft, farmLevelTotal = FarmLevelCardSeconds, farmLevelChimeIn = -1;
    private bool farmLevelUnseen;
    private readonly Queue<string> farmNotices = new();

    public bool FarmLevelCardShowing => farmLevelLeft > 0;
    public bool FarmLevelUnseen => farmLevelUnseen;

    private static FarmLevelCard CreateFarmLevelCard(double width)
    {
        var title = new TextBlock { Foreground = PetUi.Game.Gold, FontFamily = PetUi.TextFont, FontSize = PetUi.HeadingSize, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        var unlocks = new TextBlock { Foreground = Brushes.White, FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
        var scale = new ScaleTransform(1, 1);
        var card = new Border
        {
            Background = PetUi.Game.Panel, BorderBrush = PetUi.Game.Gold, BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(PetUi.CardRadius), Padding = new Thickness(12, 4, 12, 6), MaxWidth = width,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0),
            Opacity = 0, IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = scale,
            Child = new StackPanel { Children = { title, unlocks } }
        };
        return new FarmLevelCard(card, title, unlocks, scale);
    }

    private void AddFarmLevelCard(Grid surface, bool field)
    {
        var card = CreateFarmLevelCard(surface.Width - (field ? PetFarmArt.Side * 2 : 16));
        surface.Children.Add(card.Card); // Last: over the labels and the plants.
        if (field) farmLevelField = card;
        else farmLevelDock = card;
    }

    private void ShowFarmLevelUp(int gained)
    {
        int level = farm!.Level, first = level - gained + 1, coins = Enumerable.Range(first, gained).Sum(PetFarm.LevelReward);
        for (int i = 0; i < farm.OpenTiles; i++) StartFarmEffect(i, FarmEffect.Harvest);
        string opens = string.Join(", ", Enumerable.Range(first, gained).Select(FarmNextUnlockNames).Where(names => names.Length > 0));
        string line = opens.Length > 0 ? LF($"Mở khóa: {opens}", $"Unlocked: {opens}")
            : level >= PetFarm.MaxLevel ? L("Đã đạt cấp cao nhất", "Top level reached") : "";
        if (!farmFieldFold.Showing)
        {
            farmLevelUnseen = true;
            RefreshFarmDock();
        }
        ShowFarmCard("★ " + LF($"Nông trại lên cấp {level}! +{coins} xu", $"Farm level {level}! +{coins} coins"), line, PetUi.Game.Gold, PetUi.HeadingSize, FarmLevelCardSeconds);
        farmLevelChimeIn = FarmLevelChimeDelay; // After the harvest's own sparkle, which holds off the next cue.
    }

    // A farm achievement in the same card, smaller, with a mint rim and no sound of its own.
    private void ShowFarmAchievement(AchievementSpec achievement) =>
        ShowFarmCard(L("★ Thành tựu: ", "★ Achievement: ") + achievement.Name(preferences.Language), "", PetUi.Mint, PetUi.TitleSize, FarmAchievementCardSeconds);

    private void ShowFarmCard(string title, string line, Brush rim, double size, double seconds)
    {
        var card = farmFieldFold.Showing ? farmLevelField : farmLevelDock;
        if (farmLevelShown != null && farmLevelShown != card) farmLevelShown.Card.Opacity = 0;
        farmLevelShown = card;
        farmLevelLeft = farmLevelTotal = seconds;
        if (card == null) return;
        card.Card.BorderBrush = rim;
        card.Title.FontSize = size;
        card.Title.Text = title;
        card.Unlocks.Text = line;
        card.Unlocks.Visibility = line.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        card.Card.Opacity = 1;
        card.Scale.ScaleX = card.Scale.ScaleY = FarmLevelPopFrom;
    }

    // Each farm frame: the pop in, the stay, the fade, and the chime once.
    private void StepFarmLevelCard(double dt)
    {
        if (farmLevelChimeIn > 0 && (farmLevelChimeIn -= dt) <= 0) PlayPetSound("chime");
        if (farmLevelLeft <= 0) return;
        farmLevelLeft = Math.Max(0, farmLevelLeft - dt);
        if (farmLevelShown is not { } card) return;
        double pop = Math.Min(1, (farmLevelTotal - farmLevelLeft) / FarmLevelPopSeconds) - 1;
        double scale = FarmLevelPopFrom + (1 - FarmLevelPopFrom) * (1 + 2.70158 * pop * pop * pop + 1.70158 * pop * pop); // A small overshoot.
        card.Scale.ScaleX = card.Scale.ScaleY = scale;
        card.Card.Opacity = farmLevelLeft >= FarmLevelFadeSeconds ? 1 : farmLevelLeft / FarmLevelFadeSeconds;
        if (farmLevelLeft > 0) return;
        card.Card.Opacity = 0;
        farmLevelShown = null;
    }

    // A label over the middle of the land (weather, a gift) that waits while the level card shows.
    private void ShowFarmNotice(string text)
    {
        if (FarmLevelCardShowing)
        {
            if (farmNotices.Count < FarmNoticeLimit) farmNotices.Enqueue(text);
            return;
        }
        ShowFarmLabel(PetFarm.TileCount / 2, text, FarmLevelUpSeconds);
    }

    private void ForgetFarmLevelCard()
    {
        farmLevelField = farmLevelShown = null;
        farmLevelLeft = 0;
        farmLevelChimeIn = -1;
        farmNotices.Clear();
    }
}
