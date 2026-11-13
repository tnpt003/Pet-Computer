using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;

namespace DesktopPet;

// The cat-led guide (M2b, docs/APPROVED-EXCEPTIONS.md → Hành vi mèo): from the first-run welcome
// ("Go with the cat") or Settings → General ("Replay the guide"). A user command like calling the cat:
// C# only, nothing to Python. A card beside the cat says each step, with "Next ›" and "Skip"; dragging
// the cat or opening its menu also passes those steps. The cat walks to the farm sign and to a thing
// on its monitor and sits by it. While it runs nothing automatic moves the cat and no automatic emotion
// starts; it never wakes the cat (a sleeping cat does not start it). A fullscreen app or a battle ends
// it; Pause holds it (TickFeatures is not called).
public sealed partial class PetWindow
{
    private readonly PetTour tour = new();
    private Window? tourWindow;
    private TextBlock? tourTitle, tourText;
    private Button? tourNext;
    private bool tourMenuHooked;

    public bool TourActive => tour.Active;
    public TourStep TourStepNow => tour.Step;
    private bool TourOwnsMovement => tour.Active;

    // Null when the guide began, otherwise why not.
    public string? StartTour()
    {
        if (closed || Blocking(AutoBlock.Paused | AutoBlock.Fullscreen | AutoBlock.AnyBattle) != AutoBlock.None)
            return L("Lúc này chưa mở được hướng dẫn.", "The guide cannot start right now.");
        if (pet.Action is PetAction.Sleeping or PetAction.Waking || pet.Emotions.SleepLocked)
            return L("Mèo đang ngủ, thử lại khi mèo thức nhé.", "The pet is asleep; try again when it wakes.");
        SkipIntro();
        CancelFeatureActivity();
        HookTourMenu();
        tour.Start(TourAvailable);
        ShowTourCard();
        System.Diagnostics.Trace.WriteLine($"Guide: started ({tour.Total} steps).");
        return null;
    }

    // Tray "Guide": the same blocks as calling the cat; greyed while the guide runs.
    public bool TourMenuEnabled => CallMenuEnabled && !tour.Active;

    // Settings → General → "Open the welcome again" (PetWelcome.Replay).
    public void ReplayWelcome()
    {
        if (closed) return;
        PetWelcome.Replay(this, startup, persistPreferences);
    }

    // The steps that need something on the cat's monitor.
    private bool TourAvailable(TourStep step) => step switch
    {
        TourStep.Sign => TourSignX() != null,
        TourStep.Thing => TourThingX() != null,
        _ => true
    };

    // The middle of the farm sign, when it stands on the cat's monitor.
    private double? TourSignX()
    {
        var sign = FarmDockPatch;
        if (!FarmVisible || sign.Window is not { IsVisible: true } window || sign.Monitor != CurrentMonitor().DeviceName) return null;
        return window.Left + window.Width / 2;
    }

    // The keepsake, else the toy basket, on the cat's monitor.
    private double? TourThingX()
    {
        if (KeepsakeBesideCat && keepsakeWindow is { IsVisible: true } shelf) return shelf.Left + shelf.Width / 2;
        if (toyBasket?.Window is { IsVisible: true } basket && toyBasket.Monitor == CurrentMonitor().DeviceName) return basket.Left + basket.Width / 2;
        return null;
    }

    private void HookTourMenu()
    {
        if (tourMenuHooked || ContextMenu == null) return;
        tourMenuHooked = true;
        ContextMenu.Opened += (_, _) => tour.Did(TourStep.Menu);
        ContextMenu.Closed += (_, _) => { if (tour.Step == TourStep.Menu && tour.Done) NextTourStep(); };
    }

    // A drag that ended in a release (EndDrag): the drag step is done once the cat is down again.
    private void TourSawDrag() => tour.Did(TourStep.Drag);

    // Every unpaused frame from TickFeatures: what ends the guide, the drag step, the card's place.
    private void TickTourClock(double dt)
    {
        if (!tour.Active) return;
        if (closed || Blocking(AutoBlock.Fullscreen | AutoBlock.AnyBattle) != AutoBlock.None) { StopTour(); return; }
        tour.Tick(dt);
        if (tour.Step == TourStep.Drag && tour.Done && !dragging && !pressPending && pet.Grounded && pet.Action != PetAction.Thrown) NextTourStep();
        PlaceTourCard();
    }

    // From TickFeatures with the cat free (CanPlay). True while the guide owns the cat.
    private bool TickTour()
    {
        if (!tour.Active) return false;
        int toward = CursorPosition().X < CatCenter ? -1 : 1;
        double? spot = tour.Step switch { TourStep.Sign => TourSignX(), TourStep.Thing => TourThingX(), _ => null };
        if (spot is double middle)
        {
            // Beside it, on the cat's side, within the work area.
            int side = CatCenter < middle ? -1 : 1;
            double half = tour.Step == TourStep.Sign ? (FarmDockPatch.Window?.Width ?? 160) / 2 : 40;
            double target = Math.Clamp(middle + side * (half + settings.Width / 2 - 10) - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
            int way = target < pet.X ? -1 : 1;
            bool edge = CatCanStand(pet.X + way * 6) == null; // A window top's end: it does not jump down.
            if (Math.Abs(target - pet.X) > PetTour.Near && !edge && tour.Time < PetTour.WalkSeconds) { WalkToward(target); return true; }
            HoldStill(PetAction.Sitting, middle < CatCenter ? -1 : 1);
            return true;
        }
        HoldStill(PetAction.Sitting, toward);
        return true;
    }

    private void NextTourStep()
    {
        tour.Next(TourAvailable);
        if (!tour.Active) { StopTour(); return; }
        ShowTourCard();
    }

    // Skip, the last "Done", a fullscreen app, a battle, closing the app.
    private void StopTour()
    {
        bool was = tour.Active || tourWindow != null; // After the last step the clock is already over; the card is not.
        tour.Stop();
        tourWindow?.Close();
        if (!was) return;
        if (!dragging && pet.Action is PetAction.Sitting or PetAction.Walking) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
        System.Diagnostics.Trace.WriteLine("Guide: over.");
    }

    private string TourStepText(TourStep step) => step switch
    {
        TourStep.Drag => L("Kéo mình để bế lên, thả ra hoặc vung chuột để ném. Thử đi!", "Drag me to pick me up; let go or swing to throw. Try it!"),
        TourStep.Menu => L("Chuột phải vào mình để chơi, làm động tác, Giờ tập trung… Cho ăn và cảm xúc ở mục tên mình.",
                           "Right-click me to play, do actions, Focus time… Feeding and emotions are under my name."),
        TourStep.Sign => L("Đây là biển nông trại: bấm một ô để mở hoặc gập vùng đất. Chuột phải vào vùng đất để dùng nó.",
                           "This is the farm sign: click a board to open or fold a plot. Right-click a plot to use it."),
        TourStep.Thing => L("Chuột phải vào đồ vật (kỷ vật, giỏ, hộp, trụ cào…) để dùng nó, kéo để dời chỗ.",
                            "Right-click a thing (keepsake, basket, box, post…) to use it; drag it to move it."),
        TourStep.Tray => L("Biểu tượng mèo ở khay, góc phải dưới ↘: Cài đặt, Tạm dừng, Chơi, Kiểm tra cập nhật.",
                           "My tray icon, bottom right ↘: Settings, Pause, Play, Check for updates."),
        _ => L("Xong rồi! Muốn xem lại thì vào Cài đặt → Chung → Xem lại hướng dẫn.", "All done! To see this again: Settings → General → Replay the guide.")
    };

    private void ShowTourCard()
    {
        if (tourWindow == null)
        {
            tourTitle = new TextBlock { FontFamily = PetUi.TextFont, FontSize = PetUi.SmallSize, Foreground = PetUi.Muted, Margin = new Thickness(0, 0, 0, 3) };
            tourText = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 250, FontFamily = PetUi.TextFont, FontSize = PetUi.BodySize, Foreground = PetUi.Ink };
            var skip = PetUi.SecondaryButton(L("Bỏ qua", "Skip"));
            tourNext = PetUi.PrimaryButton(L("Tiếp ›", "Next ›"));
            skip.Click += (_, _) => StopTour();
            tourNext.Click += (_, _) => NextTourStep();
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            skip.Margin = new Thickness(0, 0, 6, 0);
            buttons.Children.Add(skip); buttons.Children.Add(tourNext);
            var column = new StackPanel { Children = { tourTitle, tourText, buttons } };
            var row = new DockPanel();
            var badge = new PetIllustration(PetMotif.Cat, 26) { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top };
            DockPanel.SetDock(badge, Dock.Left);
            row.Children.Add(badge); row.Children.Add(column);
            var card = new Border
            {
                Background = PetUi.CardBack, BorderBrush = PetUi.ArtInk, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(PetUi.CardRadius),
                Padding = new Thickness(12, 8, 12, 9), Child = row, Margin = new Thickness(6),
                Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Direction = 270, Opacity = 0.28 }
            };
            var window = CreateOverlayWindow(card, "Pet guide");
            window.SizeToContent = SizeToContent.WidthAndHeight;
            PetControlStyle.Apply(window);
            window.Closed += (_, _) => { if (tourWindow == window) { tourWindow = null; tourTitle = tourText = null; tourNext = null; } };
            tourWindow = window;
        }
        tourTitle!.Text = LF($"Hướng dẫn · {tour.Number}/{tour.Total}", $"Guide · {tour.Number}/{tour.Total}");
        tourText!.Text = TourStepText(tour.Step);
        tourNext!.Content = tour.Step == TourStep.Done ? L("Xong", "Done") : L("Tiếp ›", "Next ›");
        if (!tourWindow.IsVisible) tourWindow.Show();
        tourWindow.UpdateLayout();
        PlaceTourCard();
    }

    // Over the cat's head; the tray step sits in the work area's bottom-right corner, by the tray.
    private void PlaceTourCard()
    {
        if (tourWindow is not { IsVisible: true } window) return;
        double width = window.ActualWidth, height = window.ActualHeight;
        double left, top;
        if (tour.Step == TourStep.Tray) { left = bounds.Right - width - 8; top = bounds.Bottom - height - 8; }
        else
        {
            left = CatCenter - width / 2;
            top = pet.Y + PetSettings.SpeechAnchorY - height - 4;
        }
        left = Math.Clamp(left, bounds.Left, Math.Max(bounds.Left, bounds.Right - width));
        top = Math.Clamp(top, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - height));
        MoveWindow(window, left, top);
    }
}
