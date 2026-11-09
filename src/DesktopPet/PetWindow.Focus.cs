using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DesktopPet.AI;

namespace DesktopPet;

// The focus timer (docs/WINDOWS.md). While a session runs the cat behaves as in Quiet mode without
// changing the saved setting. The end never wakes a sleeping cat: the chime rings and the label
// changes, and the line and the stretch wait until it is awake.
public sealed partial class PetWindow
{
    private readonly PetFocus focus = new();
    private Window? focusWindow;
    private TextBlock? focusText, focusIcon;
    private (FocusPhase Phase, int Seconds, string Language) focusShown;

    private bool FocusStartAllowed => Blocking(AutoBlock.Paused | AutoBlock.AnyBattle) == AutoBlock.None;
    private bool FocusChimeAllowed => Blocking(AutoBlock.Paused | AutoBlock.Fullscreen | AutoBlock.AnyBattle) == AutoBlock.None;
    private bool FocusLabelAllowed => focus.Running && !closed && !pet.Speech.Visible && Blocking(AutoBlock.Fullscreen | AutoBlock.AnyBattle) == AutoBlock.None;

    public bool BreakRemindersOn => preferences.BreakReminders;
    public int BreakMinutes => preferences.BreakMinutes;

    public void SetBreakReminders(bool on)
    {
        if (preferences.BreakReminders == on) return;
        preferences.BreakReminders = on;
        breaks.Configure(preferences.BreakReminders, preferences.BreakMinutes);
        if (settingsForm != null) settingsForm.BreakReminders.IsChecked = on;
        SavePreferences();
    }

    private void InitializeFocusMenu(ContextMenu menu)
    {
        var root = PetUi.MenuItem(PetUi.Focus, PetUi.Care);
        var start25 = new MenuItem();
        start25.Click += (_, _) => StartFocus(PetFocus.ShortMinutes);
        var start50 = new MenuItem();
        start50.Click += (_, _) => StartFocus(PetFocus.LongMinutes);
        var stop = new MenuItem();
        stop.Click += (_, _) => StopFocus();
        var reminders = new MenuItem { IsCheckable = true };
        reminders.Click += (_, _) => SetBreakReminders(reminders.IsChecked);
        root.Items.Add(start25); root.Items.Add(start50); root.Items.Add(stop); root.Items.Add(new Separator()); root.Items.Add(reminders);
        menu.Items.Add(root);
        void Refresh()
        {
            root.Header = L("Giờ tập trung", "Focus time");
            start25.Header = LF($"Bắt đầu {PetFocus.ShortMinutes} phút", $"Start {PetFocus.ShortMinutes} minutes");
            start50.Header = LF($"Bắt đầu {PetFocus.LongMinutes} phút", $"Start {PetFocus.LongMinutes} minutes");
            int left = (int)Math.Ceiling(focus.Remaining / 60);
            stop.Header = focus.Focusing ? LF($"Dừng phiên (còn {left} phút)", $"Stop session ({left} min left)") : L("Dừng nghỉ", "End the rest");
            reminders.Header = L("Nhắc nghỉ giải lao", "Break reminders");
            reminders.ToolTip = L("Mèo nhắc bạn nghỉ sau một khoảng thời gian dùng máy; đổi khoảng cách trong Cài đặt.", "The pet reminds you to rest after a stretch at the computer; change how often in Settings.");
            reminders.IsChecked = preferences.BreakReminders;
            start25.IsEnabled = start50.IsEnabled = FocusStartAllowed && !focus.Focusing;
            stop.Visibility = focus.Running ? Visibility.Visible : Visibility.Collapsed;
        }
        Refresh();
        menu.Opened += (_, _) => Refresh();
    }

    public bool StartFocus(int minutes)
    {
        if (!FocusStartAllowed || !focus.Start(minutes)) return false;
        breaks.Delivered(); // A reminder must not arrive in the middle; the clock starts over.
        CancelFeatureActivity();
        ApplyQuiet();
        PlayPetSound("pop");
        return true;
    }

    public void StopFocus()
    {
        focus.Stop();
        ApplyQuiet();
    }

    // Every frame with the real time since the last one; 0 in Pause, which freezes it.
    private void TickFocus(double elapsed)
    {
        var happened = focus.Step(elapsed);
        if (happened == FocusEvent.None) return;
        if (happened == FocusEvent.Done)
        {
            ApplyQuiet();
            breaks.Delivered();
            RewardFocus(focus.Minutes);
        }
        System.Diagnostics.Trace.WriteLine($"Focus timer: {happened}.");
    }

    // Seeds and growth for the focus bloom, given at once even if the cat cannot say so yet.
    private void RewardFocus(int minutes)
    {
        if (farm == null) return; // The field is switched off.
        var grown = farm.CompleteFocus(minutes);
        foreach (int tile in grown) StartFarmEffect(tile, FarmEffect.Plant);
        int seeds = PetFarm.FocusStages(minutes);
        ShowFarmLabel(grown.Count > 0 ? grown[0] : PetFarm.TileCount / 2, LF($"Hạt Hoa tập trung +{seeds}", $"Focus bloom seeds +{seeds}"), FarmTipLastSeconds);
        FarmChanged();
    }

    // Once per second, from TickContext.
    private void DeliverFocusNews(double elapsed)
    {
        if (focus.ChimeWaiting && FocusChimeAllowed) focus.ChimeStep(PlayPetSound("sparkle"), elapsed);
        if (focus.LineWaiting == FocusEvent.None || !CanDeliverBreak) return;
        var (id, text) = PetFocus.LocalLine(focus.LineWaiting, preferences.Language, preferences.Name, featureRandom);
        ShowSpeech(new PetLine(id, text));
    }

    private void FocusLineShown(string id)
    {
        focus.LineSaid();
        if (PetFocus.IsDoneLine(id) && MayAct(AutoKind.Stretch)) behavior.StartStretch(pet); // Stretch along with the user.
    }

    private void EnsureFocusLabel()
    {
        if (focusWindow != null) return;
        focusIcon = PetUi.Icon(PetUi.Focus, PetUi.Care);
        focusIcon.Margin = new Thickness(0, 0, 4, 0);
        focusText = new TextBlock { FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold, FontFamily = PetUi.TextFont, Foreground = PetUi.Ink, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { focusIcon, focusText } };
        var pill = new Border
        {
            Background = PetUi.CardBack, BorderBrush = PetUi.ArtInk, BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(PetUi.PillRadius), Padding = new Thickness(8, 3, 12, 4), Child = row,
            Margin = new Thickness(6), Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Direction = 270, Opacity = 0.28 }
        };
        focusWindow = CreateOverlayWindow(pill, "Pet focus timer", clickThrough: true);
        focusWindow.IsHitTestVisible = false;
        focusWindow.SizeToContent = SizeToContent.WidthAndHeight;
        focusWindow.Closed += (_, _) => { focusWindow = null; focusText = focusIcon = null; focusShown = default; };
    }

    // Called from Present with the drawn position. The words change once per second.
    private void UpdateFocusLabel(double petX, double petY)
    {
        if (!FocusLabelAllowed)
        {
            if (focusWindow?.IsVisible == true) focusWindow.Hide();
            return;
        }
        EnsureFocusLabel();
        int seconds = (int)Math.Ceiling(focus.Remaining);
        var shown = (focus.Phase, seconds, preferences.Language);
        bool changed = focusShown != shown;
        if (changed)
        {
            focusShown = shown;
            string time = $"{seconds / 60}:{seconds % 60:00}";
            focusText!.Text = focus.Focusing ? time : L("Xong · nghỉ", "Done · rest") + " " + time;
            focusIcon!.Foreground = focus.Focusing ? PetUi.AccentText : PetUi.Care;
        }
        if (!focusWindow!.IsVisible) { focusWindow.Show(); changed = true; }
        if (changed) focusWindow.UpdateLayout();
        double width = focusWindow.ActualWidth, height = focusWindow.ActualHeight;
        double left = Math.Clamp(petX + PetSettings.CenterX - width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width));
        MoveWindow(focusWindow, left, Math.Max(bounds.Top, petY + PetSettings.SpeechAnchorY - height));
    }
}
