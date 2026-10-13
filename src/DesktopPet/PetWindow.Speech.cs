using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DesktopPet.AI;

namespace DesktopPet;

// Speech bubble in its own small transparent window, so the pet window keeps its
// fixed size (no pickup flicker). The bubble is click-through and never activates.
public sealed partial class PetWindow
{
    private Window? speechWindow;
    private TextBlock? speechText;
    private FrameworkElement? speechRoot;
    private System.Windows.Shapes.Path? speechTail;
    private string? speechShownText;
    private PetLearned? aiLearned;

    // Break reminders were enabled on their own, so they speak even with chatter off.
    private bool SpeechAllowed(string id) => !closed && !context.Fullscreen &&
        (PetBreaks.IsBreakLine(id) ? breaks.Due : preferences.Speech);

    private void ShowSpeech(PetLine line)
    {
        if (!SpeechAllowed(line.Id)) return;
        // A line the pet cannot say right now (eating, flying, just spoke) waits briefly.
        if (pet.Speech.Offer(line.Id, line.Text, pet)) SpeechShown(line.Id);
    }

    // Every frame while not paused: a pending line appears as soon as the pet is free.
    private void TickPendingSpeech()
    {
        if (pet.Speech.PendingId is not string id) return;
        // Sleep, a fullscreen app or a disabled setting drops it instead of saying it late.
        // The greeting is the exception for sleep: it waits (silently) until the pet is awake.
        bool asleep = pet.Action == PetAction.Sleeping || pet.Emotions.SleepLocked;
        if (!SpeechAllowed(id) || (asleep && !PetSpeech.IsGreeting(id))) { pet.Speech.DropPending(); return; }
        if (pet.Speech.TryShowPending(pet)) SpeechShown(id);
    }

    private void SpeechShown(string id)
    {
        pet.Thought.Clear(); // Words replace the icon bubble.
        PlayPetSound("meow");
        if (PetBreaks.IsBreakLine(id))
        {
            breaks.Delivered();
            if (CanPlay && !FeatureOwnsMovement()) behavior.StartStretch(pet); // Stretch along with the user.
        }
    }

    private void EnsureSpeechWindow()
    {
        if (speechWindow != null) return;
        var ink = new SolidColorBrush(Color.FromRgb(0x49, 0x37, 0x2E));
        var border = new SolidColorBrush(Color.FromRgb(0x30, 0x2B, 0x48));
        speechText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, MaxWidth = 210, FontSize = 13.5, FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI"), Foreground = ink, TextAlignment = TextAlignment.Center
        };
        var bubble = new Border
        {
            Background = Brushes.White, BorderBrush = border, BorderThickness = new Thickness(2.5),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 7, 12, 8), Child = speechText
        };
        // Open figure: filled white, stroked only on its two slanted sides.
        speechTail = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 0,0 L 7,12 L 16,0"), Fill = Brushes.White, Stroke = border, StrokeThickness = 2.5,
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(20, -2.5, 0, 0)
        };
        var root = new StackPanel { Margin = new Thickness(6), Children = { bubble, speechTail },
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.35 } };
        speechRoot = root;
        speechWindow = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true,
            Background = Brushes.Transparent, Topmost = true, ShowInTaskbar = false, ShowActivated = false,
            Focusable = false, IsHitTestVisible = false, SizeToContent = SizeToContent.WidthAndHeight, Content = root,
            Title = "Pet speech"
        };
        speechWindow.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(speechWindow).Handle;
            const int ExStyle = -20;
            const long Transparent = 0x20, ToolWindow = 0x80, NoActivate = 0x08000000;
            long style = GetWindowLongPtr(handle, ExStyle).ToInt64();
            SetWindowLongPtr(handle, ExStyle, new IntPtr(style | Transparent | ToolWindow | NoActivate));
        };
        speechWindow.Closed += (_, _) => { speechWindow = null; speechShownText = null; };
    }

    // Called from Present with the drawn (interpolated) pet position.
    private void UpdateSpeechWindow(double petX, double petY)
    {
        if (!pet.Speech.Visible || closed)
        {
            if (speechWindow?.IsVisible == true) speechWindow.Hide();
            return;
        }
        EnsureSpeechWindow();
        if (speechShownText != pet.Speech.Text)
        {
            speechText!.Text = speechShownText = pet.Speech.Text;
            if (!speechWindow!.IsVisible) speechWindow.Show();
            speechWindow.UpdateLayout();
        }
        if (!speechWindow!.IsVisible) { speechWindow.Show(); speechWindow.UpdateLayout(); }
        double width = speechWindow.ActualWidth, height = speechWindow.ActualHeight;
        // Centered over the head, kept inside the work area; the tail still points at the cat.
        double anchorX = petX + 56, anchorY = petY + 12;
        double left = Math.Clamp(anchorX - width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width));
        double top = Math.Max(bounds.Top, anchorY - height);
        double tail = Math.Round(Math.Clamp(anchorX - left - 6 - 7, 14, Math.Max(14, width - 12 - 30)));
        if (Math.Abs(speechTail!.Margin.Left - tail) >= 1) speechTail.Margin = new Thickness(tail, -2.5, 0, 0);
        speechRoot!.Opacity = pet.Speech.Opacity;
        MoveWindow(speechWindow, left, top);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
}
