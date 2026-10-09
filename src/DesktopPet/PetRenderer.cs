using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// All art is local vector drawing. Replace this element with a sprite renderer later.
public sealed class PetRenderer(PetModel pet) : FrameworkElement
{
    private static readonly Brush Fur = Frozen("#E9AE65");
    private static readonly Brush Cream = Frozen("#FFE4B5");
    private static readonly Brush Ink = Frozen("#49372E");
    private static readonly Brush Pink = Frozen("#E98990");
    private static readonly Pen Outline = FrozenPen(Ink, 2.5);
    private static readonly Brush EffectInk = Frozen("#302B48");
    private static readonly Pen EffectBorder = FrozenPen(EffectInk, 3, PenLineJoin.Round);
    private static readonly Pen EffectHalo = FrozenPen(Brushes.White, 5.5, PenLineJoin.Round);
    // Frozen, shared pens avoid allocating new pens for every frame at 60 FPS.
    private static readonly Pen TailOutline = FrozenPen(Ink, 13, round: true);
    private static readonly Pen TailFill = FrozenPen(Fur, 8, round: true);
    private static readonly Pen OrbitPen = FrozenPen(Brushes.Gold, 1.2);
    private static readonly Pen AngerPen = FrozenPen(Brushes.Tomato, 1.5);
    private static readonly Pen SurprisePen = FrozenPen(Brushes.Gold, 1.5);
    private static Pen FrozenPen(Brush brush, double thickness, PenLineJoin join = PenLineJoin.Miter, bool round = false)
    {
        var pen = new Pen(brush, thickness) { LineJoin = join };
        if (round) { pen.StartLineCap = PenLineCap.Round; pen.EndLineCap = PenLineCap.Round; }
        pen.Freeze(); return pen;
    }

    private static readonly Geometry FishGeometry = CachedGeometry("M -11,0 C -7,-7 1,-7 5,-3 L 13,-7 L 13,7 L 5,3 C 1,7 -7,7 -11,0 Z");
    private static readonly Geometry HeartGeometry = CachedGeometry("M 83,9 C 70,0 69,-7 76,-8 C 80,-9 83,-5 83,-5 C 83,-5 87,-10 91,-8 C 98,-5 95,1 83,9 Z");
    private static readonly Geometry AngerGeometry = CachedGeometry("M 77,-6 L 77,0 L 83,0 M 93,-6 L 93,0 L 87,0 M 77,12 L 77,6 L 83,6 M 93,12 L 93,6 L 87,6");
    private static readonly Geometry SurpriseGeometry = CachedGeometry("M 87,-9 L 87,1 M 87,5 L 87,7");
    // Thought bubble sits above the left of the head, clear of emotion symbols on the right.
    private static readonly Point BubbleCenter = new(30, -12);
    private static readonly Geometry BubbleGeometry = Frozen(new EllipseGeometry(BubbleCenter, 17, 14));
    private static readonly Geometry BubbleDotLarge = Frozen(new EllipseGeometry(new Point(44, 5), 3.5, 3.5));
    private static readonly Geometry BubbleDotSmall = Frozen(new EllipseGeometry(new Point(49, 11), 2, 2));
    private static readonly Geometry MoonGeometry = Frozen(new CombinedGeometry(GeometryCombineMode.Exclude,
        new EllipseGeometry(BubbleCenter, 8, 8), new EllipseGeometry(new Point(34, -15), 7, 7)));
    private static readonly Pen BallStripePen = FrozenPen(Brushes.DarkSlateBlue, 1.6);
    private static Geometry Frozen(Geometry geometry) { geometry.Freeze(); return geometry; }
    private static Geometry CachedGeometry(string path)
    {
        var geometry = Geometry.Parse(path); geometry.Freeze(); return geometry;
    }
    private static readonly Geometry SleepGlyph = CreateSleepGlyph();
    private static Geometry CreateSleepGlyph()
    {
        var text = new FormattedText("Z", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 18, Brushes.White, 1);
        var glyph = text.BuildGeometry(new Point()); glyph.Freeze(); return glyph;
    }

    private static Brush Frozen(string color)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        return brush;
    }

    private static void Oval(DrawingContext dc, Brush fill, double x, double y, double rx, double ry)
        => dc.DrawEllipse(fill, Outline, new Point(x, y), rx, ry);

    private static void Triangle(DrawingContext dc, Point a, Point b, Point c, Brush fill)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(a, true, true);
            context.LineTo(b, true, false);
            context.LineTo(c, true, false);
        }
        dc.DrawGeometry(fill, Outline, geometry);
    }

    // Values derived once per frame from the model and shared by the drawing passes.
    private struct Pose
    {
        public double Time, PickupBlend, Dizzy, WakeProgress, EmotionStrength, EmotionTime, PettingLean;
        public double GroomingBlend, EatTime, SleepyBlend, Influence, RestBlend, Stride, FacingScale;
        public double Shake, Bob, ScaleX, ScaleY;
        public bool Walking, Sleeping, Waking, PickedUp, Airborne, Seeking, Eating, Grooming;
        public PetEmotion Emotion;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var p = ComputePose();
        dc.PushTransform(new TranslateTransform(PickupAnimation.Padding, PickupAnimation.Padding));
        if (p.PickedUp)
        {
            dc.PushTransform(new RotateTransform(pet.Pickup.Angle, PickupAnimation.AnchorX, PickupAnimation.AnchorY));
        }
        DrawCat(dc, p);
        if (p.PickedUp)
        {
            DrawDizzyOrbit(dc, p);
            dc.Pop();
        }
        DrawOverlays(dc, p);
        dc.Pop(); // Permanent transparent margin, identical for every state.
    }

    private Pose ComputePose()
    {
        var p = new Pose();
        p.Time = pet.AnimationTime;
        p.Walking = pet.Action == PetAction.Walking && !pet.Emotions.BlocksAutonomy;
        p.Sleeping = pet.Action == PetAction.Sleeping;
        p.Waking = pet.Action == PetAction.Waking;
        p.PickedUp = pet.Action == PetAction.Dragging;
        p.PickupBlend = p.PickedUp ? pet.Pickup.PoseBlend : 0;
        p.Dizzy = p.PickedUp ? pet.Pickup.Dizziness : 0;
        p.WakeProgress = Math.Clamp(pet.ActionTime / PetSettings.WakingDuration, 0, 1);
        bool sitting = pet.Action is PetAction.Sitting or PetAction.Grooming || p.Sleeping;
        p.Airborne = pet.Action is PetAction.Thrown or PetAction.Jumping;
        p.Emotion = pet.Emotions.Current;
        p.EmotionStrength = !p.PickedUp && !p.Sleeping && !p.Waking && !p.Airborne && p.Emotion is not (PetEmotion.Neutral or PetEmotion.Dizzy)
            ? pet.Emotions.Strength : 0;
        p.EmotionTime = pet.Emotions.Time;
        p.Seeking = pet.Action == PetAction.SeekingAttention && !pet.Emotions.BlocksAutonomy;
        p.PettingLean = p.Emotion == PetEmotion.Happy && p.EmotionStrength > 0 ? pet.Emotions.PettingLean * p.EmotionStrength : 0;
        p.Eating = pet.TreatRemaining > 0 && !p.PickedUp && !p.Sleeping && !p.Waking && !p.Airborne;
        bool selfCare = !p.PickedUp && !p.Sleeping && !p.Waking && !p.Airborne && !p.Eating && !pet.Emotions.BlocksAutonomy;
        p.Grooming = selfCare && pet.Action == PetAction.Grooming;
        p.GroomingBlend = p.Grooming ? Math.Sin(Math.PI * Math.Clamp(pet.ActionTime / PetSettings.GroomingDuration, 0, 1)) : 0;
        double selfStretch = selfCare && pet.Action == PetAction.Stretching ? Math.Sin(Math.PI * Math.Clamp(pet.ActionTime / PetSettings.StretchingDuration, 0, 1)) : 0;
        double playCrouch = selfCare ? pet.PlayCrouch : 0;
        p.EatTime = PetModel.TreatDuration - pet.TreatRemaining;
        if (p.Eating) p.EmotionStrength = 0;
        // Inactivity Sleepy is an emotion over Idle, not the timed Sleeping action.
        // Settle its whole body into the same resting pose as sleep, with the
        // expression fade controlling the transition. Input/airborne poses win.
        p.SleepyBlend = p.Emotion == PetEmotion.Sleepy ? p.EmotionStrength : 0;
        p.Influence = p.PickedUp ? 1 - p.PickupBlend : 1;
        p.RestBlend = pet.Motion.Ready ? pet.Motion.RestBlend * p.Influence : sitting ? 1 : p.SleepyBlend;
        double gait = pet.Motion.Ready ? pet.Motion.WalkPhase : p.Time * 13;
        double gaitWeight = pet.Motion.Ready ? pet.Motion.WalkBlend * p.Influence : p.Walking ? 1 : 0;
        if (p.Sleeping || p.Waking || p.SleepyBlend > 0 || p.Airborne || pet.Action == PetAction.PreparingJump) gaitWeight = 0;
        p.Stride = Math.Sin(gait) * 6 * gaitWeight;
        p.Bob = p.Sleeping || p.Waking ? 0 : p.Walking ? -Math.Abs(Math.Sin(gait)) * 3 * gaitWeight : Math.Sin(p.Time * 2.4) * 1.2 * (1 - p.PickupBlend);
        p.Bob *= 1 - Math.Max(p.SleepyBlend, p.RestBlend);
        if (p.Emotion == PetEmotion.Happy) p.Bob -= Math.Abs(Math.Sin(p.EmotionTime * 5)) * 3 * p.EmotionStrength;
        p.Shake = p.Emotion == PetEmotion.Angry ? Math.Sin(p.EmotionTime * 24) * 1.4 * p.EmotionStrength : 0;
        p.FacingScale = pet.Motion.Ready ? pet.Motion.FacingScale : pet.Facing;
        // Scale around planted feet: breathing/stretching never changes world position.
        double stretch = p.Sleeping || p.SleepyBlend > 0 ? Math.Sin(pet.ActionTime * 2) * 0.025 * (p.Sleeping ? 1 : p.SleepyBlend) :
            p.Waking ? Math.Sin(p.WakeProgress * Math.PI) * 0.08 : 0;
        double preparation = pet.Action == PetAction.PreparingJump ? Math.Sin(Math.Clamp(pet.ActionTime / PetSettings.JumpPreparationDuration, 0, 1) * Math.PI / 2) : 0;
        double flight = p.Airborne ? Math.Clamp(Math.Abs(pet.VY) / 700, 0, 1) * 0.065 : 0;
        double impact = pet.Motion.Ready ? pet.Motion.LandingSquash * p.Influence : 0;
        if (p.Sleeping || p.Waking || p.SleepyBlend > 0) { preparation = flight = impact = 0; }
        p.ScaleX = 1 + selfStretch * 0.045 + preparation * 0.04 + impact * 0.045 - flight * 0.3;
        p.ScaleY = 1 + stretch + selfStretch * 0.12 - playCrouch * 0.07 - preparation * 0.09 + flight - impact * 0.09;
        return p;
    }

    // Body and head share the facing, bob/shake and squash transforms.
    private void DrawCat(DrawingContext dc, in Pose p)
    {
        dc.PushTransform(new ScaleTransform(p.FacingScale, 1, 56, 56));
        dc.PushTransform(new TranslateTransform(p.Shake, p.Bob));
        dc.PushTransform(new ScaleTransform(p.ScaleX, p.ScaleY, 56, 107));
        DrawBody(dc, p);
        DrawHead(dc, p);
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private void DrawBody(DrawingContext dc, in Pose p)
    {
        // Tail, body, and feet are drawn back-to-front.
        var tail = new StreamGeometry();
        using (var context = tail.Open())
        {
            context.BeginFigure(new Point(77, 84), false, false);
            context.BezierTo(new Point(108, 88), new Point(104, 59),
                new Point(92, 62 + (pet.Motion.Ready ? pet.Motion.TailLag * p.Influence * pet.Motion.Facing : 0) + Math.Sin(p.Time * (3 - 2 * (p.Sleeping ? 1 : p.SleepyBlend))) * (5 - 4 * (p.Sleeping ? 1 : p.SleepyBlend))), true, false);
        }
        dc.DrawGeometry(null, TailOutline, tail);
        dc.DrawGeometry(null, TailFill, tail);
        Oval(dc, Fur, 55, 80 + 8 * p.RestBlend, 26, 23 - 7 * p.RestBlend);
        Oval(dc, Cream, 55, 86, 15, 16 - 5 * p.RestBlend);
        if (p.PickedUp && p.PickupBlend > 0)
        {
            dc.PushOpacity(p.PickupBlend);
            Oval(dc, Fur, 36, 93, 6, 12);
            Oval(dc, Fur, 75, 93, 6, 12);
            dc.Pop();
        }
        Oval(dc, Fur, 40 + p.Stride, p.PickedUp ? 104 + p.PickupBlend * 2 : p.Airborne ? 95 : 104 - Math.Max(0, p.Stride), 11, 5);
        Oval(dc, Fur, 70 - p.Stride, p.PickedUp ? 104 + p.PickupBlend * 2 : p.Airborne ? 95 : 104 + Math.Min(0, p.Stride), 11, 5);
    }

    private void DrawHead(DrawingContext dc, in Pose p)
    {
        double time = p.Time;
        bool quietIdle = pet.Action == PetAction.Idle && !pet.Emotions.SleepLocked &&
            pet.Emotions.Current == PetEmotion.Neutral && pet.Emotions.Target == PetEmotion.Neutral;
        // Uneven gaps and alternating directions avoid a metronomic idle loop.
        double idlePhase = time % 43;
        int idleBeat = idlePhase >= 30 ? 2 : idlePhase >= 17 ? 1 : 0;
        double beatTime = idlePhase - (idleBeat == 2 ? 30 : idleBeat == 1 ? 17 : 6);
        double twitch = quietIdle && beatTime > 0 && beatTime < 0.85
            ? Math.Sin(beatTime / 0.85 * Math.PI * 2) * (idleBeat == 1 ? -8 : 9) : 0;
        double glanceTime = beatTime - 1.1;
        double glanceBlend = 0;
        if (quietIdle && glanceTime > 0 && glanceTime < 2.8)
        {
            double ramp = Math.Clamp(Math.Min(glanceTime / 0.55, (2.8 - glanceTime) / 0.8), 0, 1);
            glanceBlend = ramp * ramp * (3 - 2 * ramp); // Ease in, hold, ease out.
        }
        int glanceDirection = ((int)(time / 43) + idleBeat) % 2 == 0 ? -1 : 1;
        double glanceX = glanceDirection * 4 * glanceBlend;
        double glanceY = (idleBeat == 1 ? 1.5 : -1.8) * glanceBlend;
        if (p.Eating)
        {
            glanceX = 4 * Math.Clamp(p.EatTime / 0.6, 0, 1);
            glanceY = 2;
        }
        if (p.Seeking)
        {
            double envelope = Math.Sin(Math.PI * Math.Clamp(pet.ActionTime / PetSettings.AttentionGestureDuration, 0, 1));
            glanceX = 3 * envelope; glanceY = -2 * envelope;
            if (pet.ActionTime > 0.65)
                Oval(dc, Fur, 79 + 2 * Math.Sin(pet.ActionTime * 9), 78 - 15 * envelope, 6, 9);
        }
        if (p.Grooming) glanceY = 3 * p.GroomingBlend;
        glanceX += p.PettingLean * 3;
        double headY = 49 + 10 * p.RestBlend + (p.Emotion == PetEmotion.Sad ? 2 * p.EmotionStrength : 0);
        dc.PushTransform(new TranslateTransform(glanceX * (p.Eating ? 1.4 : 0.45), headY - 49 + glanceY * 0.6 +
            (p.Eating && p.EatTime > 1.3 ? Math.Sin(p.EatTime * 16) * 1.2 : 0)));
        dc.PushTransform(new RotateTransform(p.PickedUp ? Math.Sin(pet.Pickup.Time * 10) * p.Dizzy * 3 + (pet.Motion.Ready ? pet.Motion.HeadTilt * p.Influence * pet.Motion.Facing : 0) : glanceDirection * glanceBlend * 2.5 + (pet.Motion.Ready ? pet.Motion.HeadTilt * p.Influence * pet.Motion.Facing : 0) + p.PettingLean * 7 + (p.Seeking ? Math.Sin(Math.PI * Math.Clamp(pet.ActionTime / PetSettings.AttentionGestureDuration, 0, 1)) * 6 : 0), 55, 49));
        dc.PushTransform(new RotateTransform(twitch, 37, 33));
        Triangle(dc, new Point(27, 38), new Point(27, 13), new Point(47, 31), Fur);
        Triangle(dc, new Point(31, 30), new Point(31, 21), new Point(40, 30), Pink);
        dc.Pop();
        dc.PushTransform(new RotateTransform(-twitch * 0.6, 75, 33));
        Triangle(dc, new Point(64, 30), new Point(83, 13), new Point(84, 41), Fur);
        Triangle(dc, new Point(72, 30), new Point(79, 21), new Point(80, 33), Pink);
        dc.Pop();
        Oval(dc, Fur, 55, 48, 31, 25);
        Oval(dc, Cream, 55, 58, 19, 12);
        DrawFace(dc, p, glanceX, glanceY);
        dc.DrawLine(Outline, new Point(32, 56), new Point(17, 53));
        dc.DrawLine(Outline, new Point(32, 61), new Point(17, 63));
        dc.DrawLine(Outline, new Point(78, 56), new Point(93, 53));
        dc.DrawLine(Outline, new Point(78, 61), new Point(93, 63));
        if (p.Grooming)
        {
            double pawY = pet.ActionTime < 1.6 ? 65 : 48 + Math.Sin(pet.ActionTime * 8) * 5;
            Oval(dc, Fur, 65, 86 + (pawY - 86) * p.GroomingBlend, 6, 9);
            if (pet.ActionTime > 0.5 && pet.ActionTime < 1.6)
                dc.DrawEllipse(Pink, null, new Point(59, 62), 2, 2 + Math.Abs(Math.Sin(pet.ActionTime * 12)) * 2);
        }
        dc.Pop();
        dc.Pop();
    }

    // Neutral eyes/mouth fade out while an emotion's expression fades in.
    private void DrawFace(DrawingContext dc, in Pose p, double glanceX, double glanceY)
    {
        bool blink = p.Time % 4.7 < 0.14;
        double look = pet.Action == PetAction.Looking ? Math.Sin(p.Time * 2) * 3 : 1;
        look += glanceX;
        dc.PushOpacity(1 - p.EmotionStrength);
        foreach (double eyeX in new[] { 42.0, 68.0 })
        {
            if (p.Dizzy > 0.45)
            {
                double eyeSize = 2 + p.Dizzy * 2;
                dc.DrawLine(Outline, new Point(eyeX - eyeSize, 46 - eyeSize), new Point(eyeX + eyeSize, 46 + eyeSize));
                dc.DrawLine(Outline, new Point(eyeX + eyeSize, 46 - eyeSize), new Point(eyeX - eyeSize, 46 + eyeSize));
            }
            else if (p.Sleeping || (p.Waking && p.WakeProgress < 0.3) || (!p.Waking && blink))
                dc.DrawLine(Outline, new Point(eyeX - 4, 47), new Point(eyeX + 4, 47));
            else
            {
                dc.DrawEllipse(Ink, null, new Point(eyeX + look, 46 + glanceY), 3.5,
                    p.Waking ? 1 + p.WakeProgress * 3.5 : p.Airborne ? 6 : 4.5 + p.PickupBlend * 1.5);
                dc.DrawEllipse(Brushes.White, null, new Point(eyeX + look + 1, 44 + glanceY), 1, 1.5);
            }
        }
        Triangle(dc, new Point(51, 54), new Point(59, 54), new Point(55, 58), Pink);
        if (p.Eating && p.EatTime > 1.3)
            dc.DrawEllipse(Ink, null, new Point(55, 63), 3.5, 1.5 + Math.Abs(Math.Sin(p.EatTime * 16)) * 2);
        else if (p.Waking)
            dc.DrawEllipse(Ink, null, new Point(55, 63), 3, 2 + Math.Sin(p.WakeProgress * Math.PI) * 4);
        else
        {
            dc.DrawLine(Outline, new Point(55, 58), new Point(55, 62));
            dc.DrawLine(Outline, new Point(55, 62), new Point(50, 64));
            dc.DrawLine(Outline, new Point(55, 62), new Point(60, 64));
        }
        dc.Pop();
        if (p.EmotionStrength > 0)
        {
            dc.PushOpacity(p.EmotionStrength);
            DrawEmotionFace(dc, p.Emotion, p.EmotionTime);
            dc.Pop();
        }
    }

    // Drawn inside the pickup rotation, above the head.
    private void DrawDizzyOrbit(DrawingContext dc, in Pose p)
    {
        double dizzy = p.Dizzy;
        if (dizzy <= 0.02) return;
        dc.PushOpacity(Math.Min(1, dizzy * 2));
        var orbit = new EllipseGeometry(new Point(55, 6), 37, 9);
        dc.DrawGeometry(null, EffectHalo, orbit);
        dc.DrawGeometry(null, EffectBorder, orbit);
        dc.DrawGeometry(null, OrbitPen, orbit);
        for (int i = 0; i < 3; i++)
        {
            double phase = pet.Pickup.Time * (2 + dizzy * 2) + i * Math.PI * 2 / 3;
            double x = 55 + Math.Cos(phase) * 37;
            double y = 6 + Math.Sin(phase) * 9;
            double radius = 3.5 + dizzy * 2;
            var star = new StreamGeometry();
            using (var context = star.Open())
            {
                context.BeginFigure(new Point(x, y - radius), true, true);
                for (int point = 1; point < 8; point++)
                {
                    double a = point * Math.PI / 4 - Math.PI / 2;
                    double r = point % 2 == 0 ? radius : radius * 0.35;
                    context.LineTo(new Point(x + Math.Cos(a) * r, y + Math.Sin(a) * r), true, false);
                }
            }
            dc.DrawGeometry(Brushes.White, EffectHalo, star);
            dc.DrawGeometry(Brushes.Gold, EffectBorder, star);
            dc.DrawGeometry(Brushes.Gold, null, star);
        }
        dc.Pop();
    }

    // Unrotated effects: emotion symbols, sleep Zs, ball paw swipe and treat.
    private void DrawOverlays(DrawingContext dc, in Pose p)
    {
        if (p.EmotionStrength > 0 && p.Emotion is PetEmotion.Happy or PetEmotion.Angry or PetEmotion.Surprised)
        {
            dc.PushOpacity(p.EmotionStrength);
            DrawEmotionSymbol(dc, p.Emotion, p.EmotionTime);
            dc.Pop();
        }
        if (p.Sleeping || (p.Emotion == PetEmotion.Sleepy && p.EmotionStrength > 0))
        {
            dc.PushOpacity(p.Sleeping ? 1 : p.EmotionStrength);
            for (int i = 0; i < 3; i++)
            {
                double phase = (pet.ActionTime / 2.4 + i / 3.0) % 1;
                // Short fade at either end, mostly opaque for readability while rising.
                dc.PushOpacity(Math.Min(1, Math.Min(phase / 0.15, (1 - phase) / 0.2)));
                dc.PushTransform(new TranslateTransform(78 + phase * 12, 25 - phase * 54));
                dc.PushTransform(new ScaleTransform((12 + phase * 6) / 18, (12 + phase * 6) / 18));
                var glyph = SleepGlyph;
                dc.DrawGeometry(Brushes.White, EffectHalo, glyph);
                dc.DrawGeometry(Brushes.White, EffectBorder, glyph);
                dc.DrawGeometry(Brushes.White, null, glyph);
                dc.Pop(); dc.Pop(); dc.Pop();
            }
            dc.Pop();
        }
        if (pet.BatRemaining > 0 && !p.PickedUp && !p.Sleeping && !p.Airborne && !p.Waking)
        {
            dc.PushTransform(new ScaleTransform(p.FacingScale, 1, 56, 56));
            Oval(dc, Fur, 84 + Math.Sin(pet.BatRemaining / 0.3 * Math.PI) * 12, 98, 10, 6);
            dc.Pop();
        }
        if (p.Eating) DrawTreat(dc, p);
        if (pet.Thought.Visible && !p.PickedUp && !p.Sleeping && !p.Airborne) DrawThoughtBubble(dc);
    }

    // Unflipped so icons read the same whichever way the cat faces.
    private void DrawThoughtBubble(DrawingContext dc)
    {
        dc.PushOpacity(pet.Thought.Opacity);
        dc.PushTransform(new TranslateTransform(0, pet.Thought.Rise));
        DrawOutlinedEffect(dc, BubbleDotSmall, Brushes.White);
        DrawOutlinedEffect(dc, BubbleDotLarge, Brushes.White);
        DrawOutlinedEffect(dc, BubbleGeometry, Brushes.White);
        switch (pet.Thought.Kind)
        {
            case ThoughtKind.Hungry:
                dc.PushTransform(new TranslateTransform(BubbleCenter.X - 1, BubbleCenter.Y));
                dc.PushTransform(new ScaleTransform(0.85, 0.85));
                dc.DrawGeometry(Brushes.Salmon, Outline, FishGeometry);
                dc.DrawEllipse(Ink, null, new Point(-6, -1), 1.4, 1.4);
                dc.Pop(); dc.Pop();
                break;
            case ThoughtKind.Love:
                dc.PushTransform(new TranslateTransform(BubbleCenter.X - 83, BubbleCenter.Y + 0.5));
                dc.PushTransform(new ScaleTransform(0.7, 0.7, 83, -0.5));
                dc.DrawGeometry(Brushes.HotPink, Outline, HeartGeometry);
                dc.Pop(); dc.Pop();
                break;
            case ThoughtKind.Bored:
                dc.DrawEllipse(Brushes.Coral, Outline, BubbleCenter, 8, 8);
                dc.DrawEllipse(null, BallStripePen, BubbleCenter, 2.5, 7);
                break;
            case ThoughtKind.Sleepy:
                dc.DrawGeometry(Brushes.Gold, Outline, MoonGeometry);
                break;
        }
        dc.Pop(); dc.Pop();
    }

    private void DrawTreat(DrawingContext dc, in Pose p)
    {
        double eatTime = p.EatTime;
        dc.PushTransform(new ScaleTransform(p.FacingScale, 1, 56, 56));
        double reach = Math.Clamp((eatTime - 0.65) / 0.9, 0, 1);
        reach = reach * reach * (3 - 2 * reach);
        double foodX = 104 - reach * 35, foodY = 97 - reach * 21;
        double bites = Math.Clamp((eatTime - 1.7) / 1.7, 0, 1);
        dc.PushOpacity(Math.Min(1, eatTime / 0.3) * (1 - bites));
        dc.PushTransform(new TranslateTransform(foodX, foodY));
        dc.PushTransform(new ScaleTransform(1 - bites * 0.45, 1 - bites * 0.25));
        var fish = FishGeometry;
        DrawOutlinedEffect(dc, fish, Brushes.Salmon);
        dc.DrawEllipse(Ink, null, new Point(-6, -1), 1.4, 1.4);
        dc.DrawLine(Outline, new Point(0, -3), new Point(0, 3));
        dc.Pop(); dc.Pop(); dc.Pop();
        if (eatTime > 2.2)
        {
            dc.PushOpacity(Math.Sin(Math.Clamp((eatTime - 2.2) / 2, 0, 1) * Math.PI));
            for (int i = 0; i < 3; i++)
            {
                double x = 78 + i * 10, y = 67 - Math.Sin(eatTime * 3 + i) * 4;
                dc.DrawLine(EffectHalo, new Point(x - 3, y), new Point(x + 3, y));
                dc.DrawLine(EffectHalo, new Point(x, y - 3), new Point(x, y + 3));
                dc.DrawLine(EffectBorder, new Point(x - 3, y), new Point(x + 3, y));
                dc.DrawLine(EffectBorder, new Point(x, y - 3), new Point(x, y + 3));
            }
            dc.Pop();
        }
        dc.Pop();
    }

    private static void Curve(DrawingContext dc, Point start, Point control, Point end)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.QuadraticBezierTo(control, end, true, false);
        }
        dc.DrawGeometry(null, Outline, geometry);
    }

    private static void DrawOutlinedEffect(DrawingContext dc, Geometry shape, Brush fill)
    {
        dc.DrawGeometry(fill, EffectHalo, shape);
        dc.DrawGeometry(fill, EffectBorder, shape);
        dc.DrawGeometry(fill, null, shape);
    }

    private static void DrawEmotionFace(DrawingContext dc, PetEmotion emotion, double time)
    {
        foreach (double x in new[] { 42.0, 68.0 })
        {
            if (emotion == PetEmotion.Happy)
                Curve(dc, new Point(x - 4, 48), new Point(x, 40), new Point(x + 4, 48));
            else if (emotion == PetEmotion.Sleepy)
                dc.DrawLine(Outline, new Point(x - 5, 47), new Point(x + 5, 48));
            else if (emotion == PetEmotion.Surprised)
            {
                dc.DrawEllipse(Brushes.White, Outline, new Point(x, 46), 6, 7);
                dc.DrawEllipse(Ink, null, new Point(x, 46), 2.5, 4);
            }
            else dc.DrawEllipse(Ink, null, new Point(x, emotion == PetEmotion.Sad ? 49 : 47), 3, 4);
        }
        Triangle(dc, new Point(51, 54), new Point(59, 54), new Point(55, 58), Pink);
        if (emotion == PetEmotion.Happy)
            Curve(dc, new Point(48, 60), new Point(55, 71), new Point(62, 60));
        else if (emotion == PetEmotion.Angry)
        {
            dc.DrawLine(Outline, new Point(35, 38), new Point(47, 43));
            dc.DrawLine(Outline, new Point(63, 43), new Point(75, 38));
            Curve(dc, new Point(49, 65), new Point(55, 58), new Point(61, 65));
        }
        else if (emotion == PetEmotion.Sad)
        {
            dc.DrawLine(Outline, new Point(36, 42), new Point(47, 37));
            dc.DrawLine(Outline, new Point(63, 37), new Point(74, 42));
            Curve(dc, new Point(49, 65), new Point(55, 59), new Point(61, 65));
            double tearY = 56 + (Math.Sin(time * 2) + 1) * 2;
            var tear = new StreamGeometry();
            using (var context = tear.Open())
            {
                context.BeginFigure(new Point(76, tearY - 4), true, true);
                context.BezierTo(new Point(66, tearY + 7), new Point(86, tearY + 7), new Point(76, tearY - 4), true, false);
            }
            DrawOutlinedEffect(dc, tear, Brushes.DeepSkyBlue);
        }
        else if (emotion == PetEmotion.Surprised)
            dc.DrawEllipse(Ink, null, new Point(55, 64), 4, 6);
        else dc.DrawEllipse(Ink, null, new Point(55, 63), 3, 2.5);
    }

    private static void DrawEmotionSymbol(DrawingContext dc, PetEmotion emotion, double time)
    {
        if (emotion == PetEmotion.Happy)
        {
            var heart = HeartGeometry;
            dc.PushTransform(new TranslateTransform(0, -Math.Sin(time * 3) * 2));
            DrawOutlinedEffect(dc, heart, Brushes.HotPink);
            dc.Pop();
        }
        else if (emotion == PetEmotion.Angry)
        {
            var symbol = AngerGeometry;
            dc.DrawGeometry(null, EffectHalo, symbol);
            dc.DrawGeometry(null, EffectBorder, symbol);
            dc.DrawGeometry(null, AngerPen, symbol);
        }
        else
        {
            var line = SurpriseGeometry;
            dc.DrawGeometry(null, EffectHalo, line);
            dc.DrawGeometry(null, EffectBorder, line);
            dc.DrawGeometry(null, SurprisePen, line);
        }
    }
}
