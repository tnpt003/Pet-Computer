using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// Battle Mode's short first-battle guide: four cards (targets, skills, rescue, protection and
// cooldowns) in a small panel that never takes focus and is used with the mouse only. It opens
// by itself once, with the first battle setup panel (preferences.BattleTutorialSeen), and again
// from the panel's How to play button; Ready closes it. Should it be open during a battle, the
// battle is paused (BattlePause.Tutorial), so no timer, wave or slime attack runs behind it.
public sealed partial class PetWindow
{
    private Window? battleTutorialWindow;
    private int battleTutorialStep;
    private TextBlock? tutorialTitle, tutorialBody, tutorialCount, tutorialNextText;
    public const int BattleTutorialSteps = 4;
    // A stage tip (Adventure): the same panel with one card; null for the battle guide.
    private StageSpec? tipStage;
    public string? BattleTipStage => tipStage?.Id;
    private int TutorialSteps => tipStage != null ? 1 : BattleTutorialSteps;
    public bool BattleTutorialOpen => battleTutorialWindow != null;
    public int BattleTutorialStep => battleTutorialStep;
    public string BattleTutorialText => $"{tutorialTitle?.Text}\n{tutorialBody?.Text}";

    private (string Title, string Body) TutorialCard(int step)
    {
        if (tipStage != null) return (L("Mẹo · ", "Tip · ") + StageTitle(tipStage), tipStage.Tip(English) ?? "");
        var s = BattleTuning;
        string grace = s.LandingGraceSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        string again = s.RescueGraceCooldownSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        return step switch
        {
            0 => (L("Chọn mục tiêu", "Pick a target"),
                  L("Click một slime: mèo đánh con đó trước. Không chọn thì mèo tự đánh con gần nhất. Vũ khí và độ khó chọn trên bảng chuẩn bị trước khi bấm Sẵn sàng.",
                    "Click a slime: the cat goes for it first. Otherwise it fights the nearest one by itself. Pick the weapon and difficulty on the setup panel before pressing Ready.")),
            1 => (L("Kỹ năng", "Skills"),
                  L("Bấm một ô kỹ năng trên HUD. Đẩy và Bom len: bấm ô rồi click slime. Vòng quét trên ô là thời gian hồi; rê chuột lên ô để xem mô tả.",
                    "Press a skill slot on the HUD. Push and Yarn bomb: press the slot, then click a slime. The sweep on a slot is its cooldown; hover a slot for details.")),
            2 => (L("Cứu mèo", "Rescue the cat"),
                  L("Slime phồng lên với dấu ! đỏ là sắp đánh: kéo mèo lên. Đang bế thì mèo không bị đánh; thả mèo xuống trước khi đòn rơi thì thả ra ngoài vòng đỏ để đòn trượt.",
                    "A slime swelling with a red ! is about to hit: drag the cat up. A carried cat cannot be hit; put it down before the hit falls and do it outside the red ring to make it miss.")),
            _ => (L("Bảo vệ và hồi chiêu", "Protection and cooldowns"),
                  L($"Thả mèo xuống được {grace} giây an toàn, tối đa 1 lần mỗi {again} giây. Khi đang bế mèo, kỹ năng tạm khóa và không hồi. Mở lại hướng dẫn: nút Hướng dẫn trên bảng chuẩn bị.",
                    $"Putting the cat down gives {grace} s of safety, at most once every {again} s. While the cat is carried, skills wait and do not cool down. Open this again: How to play on the setup panel."))
        };
    }

    // Opens the guide at its first card (or keeps it open). During a battle it pauses it.
    public void ShowBattleTutorial() => ShowGuide(null);

    // A stage's tip: once by itself when the stage first starts, or again from its setup panel.
    // Closing it (Done or Skip) marks it seen in the Adventure progress.
    public bool ShowStageTip(StageSpec stage)
    {
        if (stage.Tip(English) is not { Length: > 0 }) return false;
        ShowGuide(stage);
        return BattleTutorialOpen;
    }

    private void ShowGuide(StageSpec? tip)
    {
        if (closed || context.Fullscreen) return;
        if (battleTutorialWindow != null && tipStage?.Id != tip?.Id) { battleTutorialWindow.Close(); battleTutorialWindow = null; }
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

    // finished: the user went through it or skipped it, so it does not open by itself again.
    // Otherwise (the battle ended, a fullscreen app, the pet reset) it just goes away.
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
            finished = false; // A tip is not the battle guide.
        }
        if (finished && !preferences.BattleTutorialSeen)
        {
            preferences.BattleTutorialSeen = true;
            SavePreferences();
        }
        battleTutorialWindow?.Close();
        battleTutorialWindow = null;
        SetBattlePause(BattlePause.Tutorial, false);
    }
}
