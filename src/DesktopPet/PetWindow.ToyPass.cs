using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;

namespace DesktopPet;

// Passing the yarn ball (docs/TOY-PLAY.md → M5). The user rolls the ball lying out toward the cat with
// a click or a light throw; the cat crouches, steps up to meet it and bats it back toward the cursor.
// Each bat right after the user's touch is a pass; the record shows in the basket. Allowed in Quiet and
// the wake cooldown since the user started it; a sleeping cat sleeps on. All C#: Python only hears the
// existing "toy_started" once per streak.
public sealed partial class PetWindow
{
    private enum Pass { None, Look, Ready, Wait }
    private Pass pass;
    private double passTime, passIdle, passAnchor, passLabelLeft;
    private int passCameFrom = 1;
    private ToyPassCount? passCount;
    private Window? passWindow;
    private TextBlock? passText;

    public string PassPhase => pass.ToString();
    public ToyPassCount PassCount => passCount ??= new ToyPassCount(preferences.ToyPassBest);
    public string? PassLabel => passLabelLeft > 0 ? passText?.Text : null;

    // The user touched the ball lying out: a click, or a throw too light to fetch (PetWindow.Toys.cs).
    private void PassTouched(DesktopThing thing)
    {
        if (thing.Id != DesktopToyRules.Ball) return;
        PassCount.Touch();
        if (pass == Pass.Wait) passIdle = 0;
    }

    private bool BallStill(ToyBall m) => Math.Abs(m.VX) < ToyPassRules.MovingSpeed && m.Height <= 0.5;

    // Rolling toward the cat, within reach.
    private bool RollingToCat(DesktopThing ball)
    {
        var m = ball.Motion;
        double dx = m.X - CatCenter;
        return Math.Abs(dx) <= ToyPassRules.Reach && Math.Abs(m.VX) >= ToyPassRules.MovingSpeed && Math.Sign(m.VX) == -Math.Sign(dx);
    }

    private bool PassBallFree(out DesktopThing ball)
    {
        ball = FetchBall!;
        return ball != null && !ball.Carried && !ball.Held && FetchBallWithCat;
    }

    private bool TryStartPass()
    {
        if (!PassBallFree(out var ball)) return false;
        var count = PassCount;
        if (!count.Touched) return false;
        // A touch the cat did not answer goes stale once the ball lies still.
        if (!RollingToCat(ball)) { if (ball.Motion.AtRest && !ball.Press) count.End(); return false; }
        var asked = PetAutonomy.BlockedBy(AutoKind.ToyPass);
        bool waiting = fetch == Fetch.Wait || FavoriteYields; // Sitting after a fetch, or by a toy (M4): the pass wins.
        if (waiting) asked &= ~AutoBlock.Feature;
        if (Blocking(asked) != AutoBlock.None || !CatOnFloor) return false;
        if (!waiting && pet.Action is not (PetAction.Idle or PetAction.Walking or PetAction.Sitting or PetAction.Looking)) return false;
        if (waiting) { StopToyFetch(drop: false); StopFavorite(); }
        if (SniffFirst(ball)) return true; // A first look before playing (PetWindow.ToyReact.cs).
        pass = toyChaseStreak.Resting ? Pass.Look : Pass.Ready;
        passTime = passIdle = 0;
        passAnchor = pet.X;
        passCameFrom = ball.Motion.X < CatCenter ? -1 : 1;
        if (pass == Pass.Ready) { Interact("toy_started"); CountToyPlay(DesktopToyRules.Ball); } // Counts as play for Python, like the laser; a ball round (M4).
        HoldStill(PetAction.Looking, passCameFrom);
        return true;
    }

    // From TickFeatures, after the chase. True while the streak owns the cat.
    private bool TickToyPass(double dt)
    {
        if (pass == Pass.None) return CanPlay && TryStartPass();
        if (!CanPlay || FetchBall is not { } ball) { StopToyPass(); return false; }
        var m = ball.Motion;
        double dx = m.X - CatCenter;
        int toward = dx < 0 ? -1 : 1;
        if (ball.Held) // In the user's hand: the streak waits for the release.
        {
            pet.PlayCrouch = 0; passIdle = 0;
            HoldStill(PetAction.Looking, toward);
            if (pass == Pass.Ready) pass = Pass.Wait;
            return true;
        }
        passTime += dt;
        if (pass != Pass.Look)
        {
            toyChaseStreak.Pass(dt);
            if (toyChaseStreak.Resting) { pet.PlayCrouch = 0; pass = Pass.Look; passTime = 0; }
        }
        switch (pass)
        {
            case Pass.Look: // Resting: it only watches the ball roll.
                HoldStill(PetAction.Looking, toward);
                if ((BallStill(m) && passTime > 0.5) || passTime > 8) EndPass(record: false);
                return true;
            case Pass.Ready:
                pet.PlayCrouch = Math.Min(1, passTime / 0.25) * 0.6;
                bool low = m.Height < ToyPassRules.BatHeight;
                if (Math.Abs(dx) < ToyPassRules.BatReach && low && m.BatCooldown <= 0) { BatPass(ball, toward); return true; }
                if (Math.Abs(dx) >= ToyPassRules.BatReach && toward != passCameFrom) // Rolled past the cat.
                {
                    EndPass(record: true);
                    MissReaction();
                    return true;
                }
                if (BallStill(m))
                {
                    if (Math.Abs(dx) <= ToyPassRules.ShortStop + ToyPassRules.BatReach) { pet.PlayCrouch = 0; WalkToward(PassSpot(m.X, toward)); }
                    else { pet.PlayCrouch = 0; pass = Pass.Wait; passIdle = 0; }
                    return true;
                }
                if (toward == passCameFrom && Math.Sign(m.VX) == Math.Sign(dx)) { pet.PlayCrouch = 0; pass = Pass.Wait; passIdle = 0; return true; } // Rolling away again.
                // Rolling in: step up to meet a ball that would stop short, no farther than StepUp from where it waited.
                double stop = ToyPassRules.StopsAt(m.X, m.VX);
                if (Math.Abs(stop - CatCenter) > ToyPassRules.BatReach && Math.Abs(pet.X - passAnchor) < ToyPassRules.StepUp)
                {
                    WalkToward(Math.Clamp(PassSpot(stop, toward), passAnchor - ToyPassRules.StepUp, passAnchor + ToyPassRules.StepUp));
                    return true;
                }
                pet.Action = PetAction.Sitting; pet.VX = pet.SmoothedWalkVelocity = 0; pet.Facing = toward;
                return true;
            case Pass.Wait:
                pet.PlayCrouch = 0;
                if (PassCount.Touched && (RollingToCat(ball) || (BallStill(m) && Math.Abs(dx) <= ToyPassRules.ShortStop + ToyPassRules.BatReach)))
                {
                    pass = Pass.Ready; passTime = 0; passAnchor = pet.X; passCameFrom = toward;
                    return true;
                }
                HoldStill(PetAction.Sitting, toward);
                if (BallStill(m) && !ball.Press && (passIdle += dt) > ToyPassRules.IdleSeconds) { EndPass(record: true); PlayEnded(); } // No more passes (bond M3).
                return true;
        }
        return false;
    }

    // Where the cat stands to bat a ball at x from this side.
    private double PassSpot(double x, int toward) => Math.Clamp(x - toward * (ToyPassRules.BatReach - 14) - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));

    private void BatPass(DesktopThing ball, int toward)
    {
        var m = ball.Motion;
        double cursor = CursorPosition().X;
        int dir = ToyPassRules.BatDirection(cursor, CatCenter, passCameFrom);
        // Toward the cursor's spot on the floor; with the cursor overhead, about as far as it came.
        double distance = Math.Abs(cursor - CatCenter) < ToyPassRules.OverheadBand ? 180 : cursor - m.X;
        m.VX = dir * ToyPassRules.BatSpeed(distance);
        m.VY = ToyPassRules.BatLift; m.Feedback = 0.35; m.BatCooldown = 0.65;
        ball.Glide = null; ball.Placed = false; ball.Unsaved = true; // Saved once it lies still.
        pet.Facing = toward; pet.BatRemaining = 0.3; pet.PlayCrouch = 0;
        HoldStill(PetAction.Sitting, toward);
        PlayPetSound("ball_tap");
        var count = PassCount;
        if (count.Bat())
        {
            toyChaseStreak.Passed();
            preferences.ToyPassBest = count.Best;
            if (count.Count >= 2) ShowPassLabel(count.Count.ToString());
        }
        pass = Pass.Wait; passIdle = 0;
    }

    // The streak is over: a new record of five or more says so (only when it ended on its own).
    private void EndPass(bool record)
    {
        if (pass == Pass.None) return;
        int best = preferences.ToyPassBest;
        bool beaten = PassCount.End();
        if (record && beaten) ShowPassLabel(L("Kỷ lục mới!", "New record!"));
        ClearPass();
        if (best > 0) SavePreferences();
    }

    private void StopToyPass() => EndPass(record: false);

    private void ClearPass()
    {
        pet.PlayCrouch = 0;
        if (pet.Action == PetAction.Walking && !dragging) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
        pass = Pass.None;
        passTime = passIdle = 0;
    }

    // Every frame from TickDesktopToys: the label, and what ends a streak whatever the cat is doing.
    private void GuardToyPass(double dt)
    {
        if (paused) return;
        if (pass != Pass.None && (FetchBall is not { } ball || ball.Carried || BattleActive || !FetchBallWithCat)) StopToyPass();
        if (passLabelLeft > 0) passLabelLeft -= dt;
        PlacePassLabel();
    }

    private void ShowPassLabel(string words)
    {
        EnsurePassLabel();
        passText!.Text = words;
        passLabelLeft = ToyPassRules.LabelSeconds;
        passWindow!.UpdateLayout();
        PlacePassLabel();
    }

    private void EnsurePassLabel()
    {
        if (passWindow != null) return;
        passText = new TextBlock { FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold, FontFamily = PetUi.TextFont, Foreground = PetUi.Ink, VerticalAlignment = VerticalAlignment.Center };
        var pill = new Border
        {
            Background = PetUi.CardBack, BorderBrush = PetUi.ArtInk, BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(PetUi.PillRadius), Padding = new Thickness(10, 2, 10, 3), Child = passText,
            Margin = new Thickness(6), Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Direction = 270, Opacity = 0.28 }
        };
        passWindow = CreateOverlayWindow(pill, "Pet pass count", clickThrough: true);
        passWindow.IsHitTestVisible = false;
        passWindow.SizeToContent = SizeToContent.WidthAndHeight;
        passWindow.Closed += (_, _) => { passWindow = null; passText = null; passLabelLeft = 0; };
    }

    // Above the ball while the label lasts; hidden with the toys (fullscreen app, battle) or when the ball is gone.
    private void PlacePassLabel()
    {
        if (passWindow == null) return;
        if (passLabelLeft <= 0 || FetchBall is not { } ball || ball.Carried || !ToysMayShow || ball.Window?.IsVisible != true)
        {
            if (passWindow.IsVisible) passWindow.Hide();
            if (passLabelLeft <= 0) passLabelLeft = 0;
            return;
        }
        if (!passWindow.IsVisible) { passWindow.Show(); passWindow.UpdateLayout(); }
        double width = passWindow.ActualWidth, height = passWindow.ActualHeight;
        var m = ball.Motion;
        double left = Math.Clamp(m.X - width / 2, ball.Area.Left, Math.Max(ball.Area.Left, ball.Area.Right - width));
        double top = Math.Max(ball.Area.Top, ball.Area.Bottom - 2 * DesktopToyRules.BallHalfWidth - m.Height - height - 4);
        MoveWindow(passWindow, left, top, ball.Scale);
    }
}
