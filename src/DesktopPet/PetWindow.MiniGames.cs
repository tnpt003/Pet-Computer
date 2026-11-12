using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;

namespace DesktopPet;

// What the mini-games share (docs/MINI-GAMES.md): one game at a time, the same blocks, Esc and a click
// elsewhere read from the key state, and a little label that can stand anywhere on the cat's monitor
// (hide-and-seek must not put it over the hidden cat's head).
public sealed partial class PetWindow
{
    private bool miniKeysDown;
    private Window? miniLabelWindow;
    private TextBlock? miniLabelText;
    private double miniLabelLeft, miniLabelX, miniLabelBottom;

    public bool MiniGameActive => CupGameActive || HideSeekActive;
    private bool MiniGameOwnsMovement => CupOwnsMovement || SeekOwnsMovement;
    // Pause, a battle or its screens, a fullscreen app: no mini-game (the rows are greyed out).
    private bool MiniGameBlocked => closed || Blocking(AutoBlock.Paused | AutoBlock.Fullscreen | AutoBlock.AnyBattle) != AutoBlock.None;
    public string MiniGameMenuNote => MiniGameBlocked
        ? paused ? L("Mèo đang tạm dừng.", "The pet is paused.")
        : context.Fullscreen ? L("Đang có ứng dụng toàn màn hình.", "A fullscreen app is open.")
        : L("Đang ở Battle Mode.", "Battle Mode is open.")
        : "";

    // The click that chose a menu row, or a key already down, does not count as a new press.
    private void ResetMiniGameKeys() => miniKeysDown = AnyMiniKeyDown();

    // This frame's new presses: Esc, and a mouse button (left or right).
    private (bool Esc, bool Click) MiniGameKeyPresses()
    {
        bool down = AnyMiniKeyDown(), fresh = down && !miniKeysDown;
        miniKeysDown = down;
        return fresh ? (MiniKeyDown(0x1B), MiniKeyDown(0x01) || MiniKeyDown(0x02)) : (false, false);
    }

    private bool MiniKeyDown(int key) => keyProbe?.Invoke(key) ?? (GetAsyncKeyState(key) & 0x8000) != 0;
    private bool AnyMiniKeyDown() => MiniKeyDown(0x1B) || MiniKeyDown(0x01) || MiniKeyDown(0x02);

    public string? MiniLabel => miniLabelLeft > 0 ? miniLabelText?.Text : null;

    // A label centred on x with its bottom at y (DIPs), for this many seconds (or until replaced).
    private void ShowMiniLabel(string words, double x, double bottom, double seconds)
    {
        if (miniLabelWindow == null)
        {
            miniLabelText = new TextBlock { FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold, FontFamily = PetUi.TextFont, Foreground = PetUi.Ink, VerticalAlignment = VerticalAlignment.Center };
            var pill = new Border
            {
                Background = PetUi.CardBack, BorderBrush = PetUi.ArtInk, BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(PetUi.PillRadius), Padding = new Thickness(10, 2, 10, 3), Child = miniLabelText,
                Margin = new Thickness(6), Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Direction = 270, Opacity = 0.28 }
            };
            miniLabelWindow = CreateOverlayWindow(pill, "Pet mini-game label", clickThrough: true);
            miniLabelWindow.IsHitTestVisible = false;
            miniLabelWindow.SizeToContent = SizeToContent.WidthAndHeight;
            miniLabelWindow.Closed += (_, _) => { miniLabelWindow = null; miniLabelText = null; miniLabelLeft = 0; };
        }
        miniLabelText!.Text = words;
        miniLabelLeft = seconds; miniLabelX = x; miniLabelBottom = bottom;
        miniLabelWindow.UpdateLayout();
        PlaceMiniLabel();
    }

    private void HideMiniLabel() { miniLabelLeft = 0; PlaceMiniLabel(); }

    // Every unpaused frame from TickFeatures.
    private void TickMiniLabel(double dt)
    {
        if (miniLabelLeft > 0 && (miniLabelLeft -= dt) <= 0) miniLabelLeft = 0;
        PlaceMiniLabel();
    }

    private void PlaceMiniLabel()
    {
        if (miniLabelWindow == null) return;
        if (miniLabelLeft <= 0 || context.Fullscreen || BattleActive)
        {
            if (miniLabelWindow.IsVisible) miniLabelWindow.Hide();
            return;
        }
        if (!miniLabelWindow.IsVisible) { miniLabelWindow.Show(); miniLabelWindow.UpdateLayout(); }
        double width = miniLabelWindow.ActualWidth, height = miniLabelWindow.ActualHeight;
        double left = Math.Clamp(miniLabelX - width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width));
        MoveWindow(miniLabelWindow, left, Math.Max(bounds.Top, miniLabelBottom - height));
    }
}
