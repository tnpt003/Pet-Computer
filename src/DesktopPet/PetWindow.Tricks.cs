using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;

namespace DesktopPet;

// Calling the cat and teaching it tricks (docs/LIVING-CORNER.md → M5). Both are the user's requests, like
// "Do now": they run in Quiet, Focus and the wake cooldown, but never cut a drag, a flight, the sleep lock or
// waking (a sleeping cat is not woken: a call waits up to 30 seconds for it to wake by itself, a lesson waits
// like the "Do now" queue). Pause, a battle, its screens and a fullscreen app keep them off. A click, a drag or
// an emotion that owns the cat ends them (CancelFeatureActivity, CanPlay). All C#: lessons live in the
// preferences, Python takes no part; the cat never does a trick on its own.
public sealed partial class PetWindow
{
    private enum Call { None, Wait, Turn, Walk, Sit, Look }
    private enum Lesson { None, Queued, Try, Pet }
    private Call call;
    private Lesson lesson;
    private string? lessonTrick;
    private double callTime, lessonTime, lessonReadyAt = double.NegativeInfinity, trickLabelLeft;
    private long lessonPetted;
    private int callFacing = 1;
    private Window? trickWindow;
    private TextBlock? trickText;

    public string CallPhase => call.ToString();
    public string LessonPhase => lesson.ToString();
    public string? LessonTrick => lessonTrick;
    public string? TrickLabel => trickLabelLeft > 0 ? trickText?.Text : null;

    // Pause, a battle or its screens, a fullscreen app: no call, no lesson (the menu rows are greyed out).
    private bool TricksBlocked => closed || Blocking(AutoBlock.Paused | AutoBlock.Fullscreen | AutoBlock.AnyBattle) != AutoBlock.None;
    public bool CallMenuEnabled => !TricksBlocked;
    // Asleep, waking, held, in the air or owned by an emotion: a request waits for the cat.
    private bool TrickCatBusy => dragging || pressPending || !PetActionRules.IsSettled(pet) || pet.Emotions.BlocksAutonomy;

    // Tray → "Call the cat". False when it may not now.
    public bool CallCat()
    {
        if (TricksBlocked) return false;
        revision++; // Like DoNow: AI replies chosen before this request are dropped.
        StopLesson();
        if (TrickCatBusy) { StopCall(); call = Call.Wait; callTime = 0; return true; } // Woken by nobody: it comes once awake.
        StartCall();
        return true;
    }

    private void StartCall()
    {
        CancelFeatureActivity();
        var cursor = CursorPosition();
        callFacing = cursor.X < CatCenter ? -1 : 1;
        // The cursor on another monitor: only a look toward it.
        bool here = FarmMonitorAt(ScreenCursor()) is { } over && over.Name == CurrentMonitor().DeviceName;
        call = here ? Call.Turn : Call.Look;
        callTime = 0;
        HoldStill(PetAction.Looking, callFacing);
        System.Diagnostics.Trace.WriteLine(here ? "Called: the cat comes to the cursor." : "Called from another monitor: the cat looks that way.");
    }

    // The trick menu: a lesson now (or once the cat is free). False when it may not.
    public bool TeachTrick(string id)
    {
        if (Array.IndexOf(TrickRules.All, id) < 0 || TricksBlocked || !TrickRules.MayLearnToday(preferences, id, DateTime.Now)
            || clock.Elapsed.TotalSeconds < lessonReadyAt) return false;
        revision++;
        StopCall(); StopLesson();
        lessonTrick = id;
        lesson = Lesson.Queued; lessonTime = 0;
        if (!TrickCatBusy) StartLesson();
        return true;
    }

    private void StartLesson()
    {
        string id = lessonTrick!;
        CancelFeatureActivity(); // Also clears this lesson: kept below.
        lessonTrick = id;
        var action = TrickRules.ActionOf(lessonTrick!);
        pet.Facing = CursorPosition().X < CatCenter ? -1 : 1;
        pet.TrickClumsy = true;
        if (!behavior.StartManual(pet, action)) { pet.TrickClumsy = false; lesson = Lesson.Queued; return; }
        lesson = Lesson.Try; lessonTime = 0;
        System.Diagnostics.Trace.WriteLine($"Lesson: the cat tries {lessonTrick}.");
    }

    // Learned tricks from "Do now" (PetWindow.Commands.cs): the tidy version.
    private bool TrickAllowed(PetAction action) => TrickRules.IdOf(action) is not string id || TrickRules.Learned(preferences, id);

    // Every unpaused frame from TickFeatures, before CanPlay: the waits, the label, and what ends both.
    private void TickTrickClock(double dt)
    {
        if (trickLabelLeft > 0 && (trickLabelLeft -= dt) <= 0) trickLabelLeft = 0;
        PlaceTrickLabel();
        if (call == Call.None && lesson == Lesson.None) return;
        if (TricksBlocked) { StopCall(); StopLesson(); return; }
        if (call == Call.Wait)
        {
            if ((callTime += dt) > TrickRules.CallSleepWaitSeconds) { call = Call.None; return; }
            if (!TrickCatBusy && CanPlay) StartCall();
        }
        if (lesson == Lesson.Queued)
        {
            if ((lessonTime += dt) > CommandQueueSeconds) { StopLesson(); return; }
            if (!TrickCatBusy) StartLesson();
        }
    }

    // From TickFeatures with the cat free (CanPlay), ahead of the other features. True while it owns the cat.
    private bool TickCall(double dt)
    {
        if (call is Call.None or Call.Wait) return false;
        callTime += dt;
        var cursor = CursorPosition();
        int toward = cursor.X < CatCenter ? -1 : 1;
        switch (call)
        {
            case Call.Look: // On another monitor: a long look that way.
                HoldStill(PetAction.Looking, callFacing);
                if (callTime < TrickRules.CallLookSeconds) return true;
                break;
            case Call.Turn: // Ears up: a glance at the user first.
                HoldStill(PetAction.Looking, toward);
                if (callTime < TrickRules.CallTurnSeconds) return true;
                call = Call.Walk; callTime = 0;
                return true;
            case Call.Walk: // Along its floor (or the window top it is on) to under the cursor, following it.
                double target = Math.Clamp(cursor.X - settings.Width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - settings.Width));
                bool near = Math.Abs(cursor.X - CatCenter) <= TrickRules.CallNear;
                bool edge = CatCanStand(pet.X + toward * 6) == null; // A window top's end: it does not jump down.
                if (!near && !edge && callTime < TrickRules.CallWalkSeconds) { WalkToward(target); return true; }
                call = Call.Sit; callTime = 0;
                HoldStill(PetAction.Sitting, toward);
                return true;
            case Call.Sit: // Sitting, looking up at the user.
                HoldStill(PetAction.Sitting, toward);
                pet.ReactDip = -3 * Math.Sin(Math.PI * Math.Min(1, callTime / TrickRules.CallSitSeconds));
                if (callTime < TrickRules.CallSitSeconds) return true;
                break;
        }
        StopCall();
        return false;
    }

    // From TickFeatures with the cat free, next to the call. True while it owns the cat.
    private bool TickLesson(double dt)
    {
        if (lesson is Lesson.None or Lesson.Queued) return false;
        lessonTime += dt;
        int toward = CursorPosition().X < CatCenter ? -1 : 1;
        var action = TrickRules.ActionOf(lessonTrick!);
        if (lesson == Lesson.Try)
        {
            // The clumsy try (timed here: the lesson owns the cat, so PetBehavior is not stepped), then sitting looking at the user.
            if (pet.Action == action && pet.ActionTime < PetSettings.GestureDuration(action)) return true;
            pet.TrickClumsy = false;
            HoldStill(PetAction.Sitting, toward);
            if (lessonTime < TrickRules.TrySeconds) return true;
            lesson = Lesson.Pet; lessonTime = 0;
            lessonPetted = pet.Interactions.Petted;
            return true;
        }
        // Waiting for a stroke: petting it now counts one lesson.
        HoldStill(PetAction.Sitting, toward);
        pet.ReactTilt = 5 * toward * Math.Max(0, Math.Sin(lessonTime * 2.2));
        if (pet.Interactions.Petted > lessonPetted)
        {
            string id = lessonTrick!;
            bool learned = TrickRules.Record(preferences, id, DateTime.Now);
            SavePreferences();
            lessonReadyAt = clock.Elapsed.TotalSeconds + TrickRules.LessonGapSeconds;
            ShowTrickLabel(learned ? L("Đã thuộc!", "Learned!") : $"{TrickRules.Lessons(preferences, id)}/{TrickRules.LessonsToLearn}");
            if (learned) ShowSpeech(new AI.PetLine("trick_learned", LF($"{preferences.Name} thuộc {TrickName(id)} rồi nè!", $"{preferences.Name} learned {TrickName(id)}!")));
            System.Diagnostics.Trace.WriteLine($"Lesson counted: {id} {TrickRules.Lessons(preferences, id)}/{TrickRules.LessonsToLearn}.");
            StopLesson();
            return false;
        }
        if (lessonTime < TrickRules.PetSeconds) return true;
        lessonReadyAt = clock.Elapsed.TotalSeconds + TrickRules.LessonGapSeconds;
        StopLesson();
        return false;
    }

    // A learned high five with the cursor by the raised paw (in front of the head, about the head's height).
    public bool HighFiveMet
    {
        get
        {
            if (pet.Action != PetAction.HighFive || pet.TrickClumsy || dragging) return false;
            var cursor = CursorPosition();
            double dx = cursor.X - (CatCenter + pet.Facing * 30), dy = cursor.Y - (pet.Y + 42);
            return dx * dx + dy * dy <= TrickRules.HighFiveReach * TrickRules.HighFiveReach;
        }
    }

    private string TrickName(string id) => L(TrickRules.Name(id, "vi"), TrickRules.Name(id, "en"));

    private bool TrickOwnsMovement => call is Call.Turn or Call.Walk or Call.Sit or Call.Look || lesson is Lesson.Try or Lesson.Pet;

    private void StopCall()
    {
        if (call == Call.None) return;
        var was = call;
        call = Call.None;
        callTime = 0;
        pet.ReactDip = 0;
        bool own = was switch
        {
            Call.Walk => pet.Action == PetAction.Walking,
            Call.Turn or Call.Look => pet.Action == PetAction.Looking,
            Call.Sit => pet.Action == PetAction.Sitting,
            _ => false
        };
        if (own && !dragging) { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
    }

    private void StopLesson()
    {
        if (lesson == Lesson.None) return;
        var was = lesson;
        lesson = Lesson.None;
        lessonTrick = null;
        lessonTime = 0;
        pet.TrickClumsy = false;
        pet.ReactTilt = 0;
        if (was is Lesson.Try or Lesson.Pet && !dragging && (pet.Action == PetAction.Sitting || PetActionRules.IsTrick(pet.Action)))
        { pet.Action = PetAction.Idle; pet.VX = 0; behavior.Reset(pet); }
    }

    // "2/5" or "Learned!" over the cat's head for a moment (like the pass count over the ball).
    private void ShowTrickLabel(string words)
    {
        if (trickWindow == null)
        {
            trickText = new TextBlock { FontSize = PetUi.BodySize, FontWeight = FontWeights.SemiBold, FontFamily = PetUi.TextFont, Foreground = PetUi.Ink, VerticalAlignment = VerticalAlignment.Center };
            var pill = new Border
            {
                Background = PetUi.CardBack, BorderBrush = PetUi.ArtInk, BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(PetUi.PillRadius), Padding = new Thickness(10, 2, 10, 3), Child = trickText,
                Margin = new Thickness(6), Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Direction = 270, Opacity = 0.28 }
            };
            trickWindow = CreateOverlayWindow(pill, "Pet trick lesson", clickThrough: true);
            trickWindow.IsHitTestVisible = false;
            trickWindow.SizeToContent = SizeToContent.WidthAndHeight;
            trickWindow.Closed += (_, _) => { trickWindow = null; trickText = null; trickLabelLeft = 0; };
        }
        trickText!.Text = words;
        trickLabelLeft = TrickRules.LabelSeconds;
        trickWindow.UpdateLayout();
        PlaceTrickLabel();
    }

    private void PlaceTrickLabel()
    {
        if (trickWindow == null) return;
        if (trickLabelLeft <= 0 || dragging || context.Fullscreen || BattleActive)
        {
            if (trickWindow.IsVisible) trickWindow.Hide();
            return;
        }
        if (!trickWindow.IsVisible) { trickWindow.Show(); trickWindow.UpdateLayout(); }
        double width = trickWindow.ActualWidth, height = trickWindow.ActualHeight;
        double left = Math.Clamp(CatCenter - width / 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - width));
        MoveWindow(trickWindow, left, Math.Max(bounds.Top, pet.Y + PetSettings.SpeechAnchorY - height));
    }
}
