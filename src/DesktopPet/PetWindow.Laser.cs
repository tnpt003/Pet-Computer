using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DesktopPet;

// Laser-pointer game: a red dot follows the mouse cursor (click-through window) and the cat
// chases it along the floor, stalks when it rests nearby and pounces with the normal jump.
// C# only; Python just hears "toy_started" like the ball.
public sealed partial class PetWindow
{
    public const double LaserDuration = 60, LaserDotSize = 24;
    // The dot must be at most this far above the cat's top edge to be worth a pounce.
    public const double LaserReachAbove = 120;
    private Window? laserWindow;
    private readonly LaserChase laserChase = new();
    private double laserTime;
    private Point? laserLastCursor;

    public bool LaserActive => laserWindow != null;

    public bool StartLaser()
    {
        if (laserWindow != null || !CanPlay || context.Fullscreen || BattleActive) return false; // Allowed in Quiet mode.
        CancelFeatureActivity();
        Interact("toy_started"); // Counts as play for Python (boredom, play lines).
        laserChase.Reset();
        laserTime = LaserDuration;
        laserLastCursor = null;
        var dot = new Grid { Width = LaserDotSize, Height = LaserDotSize, IsHitTestVisible = false };
        dot.Children.Add(new Ellipse { Width = 18, Height = 18, Fill = new SolidColorBrush(Color.FromArgb(70, 255, 40, 40)) });
        dot.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(Color.FromRgb(255, 40, 40)),
            Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 200, 200)), StrokeThickness = 1,
            Effect = new DropShadowEffect { Color = Colors.Red, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9 } });
        laserWindow = new Window
        {
            Width = LaserDotSize, Height = LaserDotSize, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true, Background = Brushes.Transparent, Topmost = true, ShowInTaskbar = false,
            ShowActivated = false, Focusable = false, IsHitTestVisible = false, Content = dot, Title = "Pet laser",
            Left = -10000, Top = -10000
        };
        laserWindow.SourceInitialized += (_, _) =>
        {
            // Click-through and never activated, like the speech bubble.
            var handle = new WindowInteropHelper(laserWindow).Handle;
            const int ExStyle = -20;
            const long Transparent = 0x20, ToolWindow = 0x80, NoActivate = 0x08000000;
            long style = GetWindowLongPtr(handle, ExStyle).ToInt64();
            SetWindowLongPtr(handle, ExStyle, new IntPtr(style | Transparent | ToolWindow | NoActivate));
        };
        laserWindow.Closed += (_, _) => laserWindow = null;
        laserWindow.Show();
        PlayPetSound("excited");
        return true;
    }

    private void StopLaser()
    {
        if (laserWindow == null) return;
        laserWindow.Close();
        laserWindow = null;
        laserChase.Reset();
        pet.PlayCrouch = 0;
        pet.Emotions.ForgetCursor();
    }

    // Called from TickFeatures before the generic "cannot play" cancel. True when the laser
    // game handled this frame.
    private bool TickLaser(double dt)
    {
        if (laserWindow == null) return false;
        bool interrupted = dragging || pressPending || context.Fullscreen || pet.Emotions.SleepLocked ||
            pet.Action is PetAction.Sleeping or PetAction.Waking or PetAction.Dragging or PetAction.Thrown;
        laserTime -= dt;
        if (interrupted || laserTime <= 0) { StopLaser(); return false; }

        var cursor = CursorPosition();
        bool onScreen = cursor.X >= bounds.Left && cursor.X <= bounds.Right && cursor.Y >= bounds.Top && cursor.Y <= bounds.Bottom + 48;
        double speed = laserLastCursor is Point last && dt > 0 ? (cursor - last).Length / dt : 0;
        laserLastCursor = cursor;
        laserWindow.Opacity = onScreen ? 1 : 0; // Hidden on other monitors (their DPI may differ).
        if (onScreen) MoveWindow(laserWindow, cursor.X - LaserDotSize / 2, cursor.Y - LaserDotSize / 2);
        double dx = cursor.X - (pet.X + settings.Width / 2);

        if (laserChase.Phase == LaserPhase.Pounce)
        {
            // The game's own jump: keep playing while airborne; land, then recover.
            if (pet.Grounded && pet.Action != PetAction.Jumping)
            {
                laserChase.Landed();
                if (Math.Abs(dx) < 45 && onScreen) PlayPetSound("ball_tap"); // Caught it.
            }
            return true;
        }
        if (!CanPlay) return true; // A real reaction (startled, angry) runs first; the game waits.

        bool reachable = onScreen && cursor.Y >= pet.Y - LaserReachAbove;
        bool pounce = laserChase.Step(dt, dx, reachable, speed, preferences.Personality);
        pet.PlayCrouch = laserChase.CrouchBlend;
        if (Math.Abs(dx) > 4) pet.Facing = dx < 0 ? -1 : 1;
        if (pounce)
        {
            pet.PlayCrouch = 0;
            pet.Action = PetAction.Jumping; pet.Grounded = false;
            pet.VY = -settings.JumpSpeed; pet.VX = pet.Facing * settings.WalkSpeed; // Same impulse as a normal jump.
            return true;
        }
        switch (laserChase.Phase)
        {
            case LaserPhase.Chase:
                double target = Math.Clamp(cursor.X - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
                if (Math.Abs(target - pet.X) < 12) { pet.Action = PetAction.Idle; pet.VX = pet.SmoothedWalkVelocity = 0; }
                else WalkToward(target);
                break;
            case LaserPhase.Stalk:
                pet.Action = PetAction.Sitting; pet.VX = pet.SmoothedWalkVelocity = 0;
                break;
            default: // Watch or recover: sit and follow the dot with the eyes.
                pet.Action = laserChase.Phase == LaserPhase.Watch ? PetAction.Looking : PetAction.Sitting;
                pet.VX = pet.SmoothedWalkVelocity = 0;
                break;
        }
        return true;
    }
}
