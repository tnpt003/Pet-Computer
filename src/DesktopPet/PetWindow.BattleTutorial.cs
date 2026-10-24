using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// A stage's tip: one card in a small panel that never takes focus and is used with the mouse only,
// opened from the stage's setup panel (Stage tip). Should it be open during a battle, the battle is
// paused (BattlePause.Tutorial), so no timer, wave or slime attack runs behind it. The battle guide's
// cards are gone since 2026-10-24: the Adventure Guide itself teaches the game (PetWindow.BattleGuide.cs).
public sealed partial class PetWindow
{
    private Window? battleTutorialWindow;
    private int battleTutorialStep;
    private TextBlock? tutorialTitle, tutorialBody, tutorialCount, tutorialNextText;
    private StageSpec? tipStage;
    public string? BattleTipStage => tipStage?.Id;
    private const int TutorialSteps = 1;
    public bool BattleTutorialOpen => battleTutorialWindow != null;
    public int BattleTutorialStep => battleTutorialStep;
    public string BattleTutorialText => $"{tutorialTitle?.Text}\n{tutorialBody?.Text}";

    private (string Title, string Body) TutorialCard(int step) =>
        tipStage == null ? ("", "") : (L("Bài học · ", "Lessons · ") + StageTitle(tipStage), tipStage.Tip(preferences.Language) ?? "");

    // A stage's tip, from its setup panel. Closing it (Done or Skip) marks it seen in the Adventure progress.
    public bool ShowStageTip(StageSpec stage)
    {
        if (stage.Tip(preferences.Language) is not { Length: > 0 }) return false;
        ShowGuide(stage);
        return BattleTutorialOpen;
    }

    private void ShowGuide(StageSpec tip)
    {
        if (closed || context.Fullscreen) return;
        if (battleTutorialWindow != null && tipStage?.Id != tip.Id) { battleTutorialWindow.Close(); battleTutorialWindow = null; }
        tipStage = tip;
        battleTutorialStep = 0;
        if (battleTutorialWindow == null)
        {
            tutorialTitle = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.HeadingSize, FontWeight = FontWeights.Bold };
            tutorialBody = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.BodySize, TextWrapping = TextWrapping.Wrap, Width = 320, Margin = new Thickness(0, 4, 0, 0) };
            tutorialCount = new TextBlock { Foreground = Brushes.White, Opacity = 0.7, FontSize = PetUi.SmallSize, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            var (skip, skipText, _) = HudButton(() => CloseBattleTutorial(finished: true));
            skipText.Text = L("Bỏ qua", "Skip");
            var (next, nextText, _) = HudButton(BattleTutorialNext);
            tutorialNextText = nextText;
            var panel = new Border
            {
                Background = HudBack, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(6),
                Child = new StackPanel { Children = { tutorialTitle, tutorialBody,
                    new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), Children = { tutorialCount, skip, next } } } },
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.4 }
            };
            panel.SetValue(TextBlock.FontFamilyProperty, PetUi.TextFont);
            var window = CreateOverlayWindow(panel, "Pet tutorial");
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.Closed += (_, _) => { if (battleTutorialWindow == window) battleTutorialWindow = null; };
            battleTutorialWindow = window;
            window.Show();
        }
        ShowTutorialCard();
        SetBattlePause(BattlePause.Tutorial, true);
    }

    public void BattleTutorialNext()
    {
        if (battleTutorialWindow == null) return;
        if (battleTutorialStep >= TutorialSteps - 1) { CloseBattleTutorial(finished: true); return; }
        battleTutorialStep++;
        ShowTutorialCard();
    }

    private void ShowTutorialCard()
    {
        if (battleTutorialWindow == null || tutorialTitle == null || tutorialBody == null || tutorialCount == null || tutorialNextText == null) return;
        var (title, body) = TutorialCard(battleTutorialStep);
        tutorialTitle.Text = title;
        tutorialBody.Text = body;
        tutorialCount.Text = TutorialSteps > 1 ? $"{battleTutorialStep + 1}/{TutorialSteps}" : "";
        tutorialNextText.Text = battleTutorialStep >= TutorialSteps - 1 ? L("Xong", "Done") : L("Tiếp", "Next");
        battleTutorialWindow.UpdateLayout();
        // Above the battle's floor strip (or above the cat without a battle), centred.
        var area = battle != null ? battleArea : bounds;
        double at = battle != null ? battleScale : scale;
        double width = battleTutorialWindow.ActualWidth, height = battleTutorialWindow.ActualHeight;
        double left = Math.Clamp((area.Left + area.Right - width) / 2, area.Left, Math.Max(area.Left, area.Right - width));
        double top = Math.Max(area.Top, area.Bottom - BattleFieldView.FieldHeight - height - 12);
        MoveWindow(battleTutorialWindow, left, top, at);
    }

    // finished: the user went through it or skipped it (the tip is marked seen). Otherwise (the
    // battle ended, a fullscreen app, the pet reset) it just goes away.
    public void CloseBattleTutorial(bool finished = false)
    {
        if (tipStage != null)
        {
            if (finished && !AdventureProgress.Seen.Contains(tipStage.Id))
            {
                AdventureProgress.Seen.Add(tipStage.Id);
                if (AdventureProgressFile != null) adventureSaveFailed = !AdventureProgress.Save(AdventureProgressFile);
            }
            tipStage = null;
        }
        battleTutorialWindow?.Close();
        battleTutorialWindow = null;
        SetBattlePause(BattlePause.Tutorial, false);
    }
}
