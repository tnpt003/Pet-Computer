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
    private static readonly Pen Outline = new(Ink, 2.5);
    private static readonly Brush EffectInk = Frozen("#302B48");
    private static readonly Pen EffectBorder = new(EffectInk, 3) { LineJoin = PenLineJoin.Round };
    private static readonly Pen EffectHalo = new(Brushes.White, 5.5) { LineJoin = PenLineJoin.Round };

    private static readonly Geometry FishGeometry = CachedGeometry("M -11,0 C -7,-7 1,-7 5,-3 L 13,-7 L 13,7 L 5,3 C 1,7 -7,7 -11,0 Z");
    private static readonly Geometry HeartGeometry = CachedGeometry("M 83,9 C 70,0 69,-7 76,-8 C 80,-9 83,-5 83,-5 C 83,-5 87,-10 91,-8 C 98,-5 95,1 83,9 Z");
    private static readonly Geometry AngerGeometry = CachedGeometry("M 77,-6 L 77,0 L 83,0 M 93,-6 L 93,0 L 87,0 M 77,12 L 77,6 L 83,6 M 93,12 L 93,6 L 87,6");
    private static readonly Geometry SurpriseGeometry = CachedGeometry("M 87,-9 L 87,1 M 87,5 L 87,7");
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

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double time = pet.AnimationTime;
        bool walking = pet.Action == PetAction.Walking && !pet.Emotions.BlocksAutonomy;
        bool sleeping = pet.Action == PetAction.Sleeping;
        bool waking = pet.Action == PetAction.Waking;
        bool pickedUp = pet.Action == PetAction.Dragging;
        double pickupBlend = pickedUp ? pet.Pickup.PoseBlend : 0;
        double dizzy = pickedUp ? pet.Pickup.Dizziness : 0;
        dc.PushTransform(new TranslateTransform(PickupAnimation.Padding, PickupAnimation.Padding));
        if (pickedUp)
        {
            dc.PushTransform(new RotateTransform(pet.Pickup.Angle, PickupAnimation.AnchorX, PickupAnimation.AnchorY));
        }
        double wakeProgress = Math.Clamp(pet.ActionTime / PetSettings.WakingDuration, 0, 1);
        bool sitting = pet.Action is PetAction.Sitting or PetAction.Grooming || sleeping;
        bool airborne = pet.Action is PetAction.Thrown or PetAction.Jumping;
        PetEmotion emotion = pet.Emotions.Current;
        double emotionStrength = !pickedUp && !sleeping && !waking && !airborne && emotion is not (PetEmotion.Neutral or PetEmotion.Dizzy)
            ? pet.Emotions.Strength : 0;
        double emotionTime = pet.Emotions.Time;
        bool seeking = pet.Action == PetAction.SeekingAttention && !pet.Emotions.BlocksAutonomy;
        double pettingLean = emotion == PetEmotion.Happy && emotionStrength > 0 ? pet.Emotions.PettingLean * emotionStrength : 0;
        bool eating = pet.TreatRemaining > 0 && !pickedUp && !sleeping && !waking && !airborne;
        bool selfCare = !pickedUp && !sleeping && !waking && !airborne && !eating && !pet.Emotions.BlocksAutonomy;
        bool grooming = selfCare && pet.Action == PetAction.Grooming;
        double groomingBlend = grooming ? Math.Sin(Math.PI * Math.Clamp(pet.ActionTime / 3.6, 0, 1)) : 0;
        double selfStretch = selfCare && pet.Action == PetAction.Stretching ? Math.Sin(Math.PI * Math.Clamp(pet.ActionTime / 2.4, 0, 1)) : 0;
        double playCrouch = selfCare ? pet.PlayCrouch : 0;
        double eatTime = PetModel.TreatDuration - pet.TreatRemaining;
        if (eating) emotionStrength = 0;
        // Inactivity Sleepy is an emotion over Idle, not the timed Sleeping action.
        // Settle its whole body into the same resting pose as sleep, with the
        // expression fade controlling the transition. Input/airborne poses win.
        double sleepyBlend = emotion == PetEmotion.Sleepy ? emotionStrength : 0;
        double influence = pickedUp ? 1 - pickupBlend : 1;
        double restBlend = pet.Motion.Ready ? pet.Motion.RestBlend * influence : sitting ? 1 : sleepyBlend;
        double gait = pet.Motion.Ready ? pet.Motion.WalkPhase : time * 13;
        double gaitWeight = pet.Motion.Ready ? pet.Motion.WalkBlend * influence : walking ? 1 : 0;
        if (sleeping || waking || sleepyBlend > 0 || airborne || pet.Action == PetAction.PreparingJump) gaitWeight = 0;
        double stride = Math.Sin(gait) * 6 * gaitWeight;
        double bob = sleeping || waking ? 0 : walking ? -Math.Abs(Math.Sin(gait)) * 3 * gaitWeight : Math.Sin(time * 2.4) * 1.2 * (1 - pickupBlend);
        bob *= 1 - Math.Max(sleepyBlend, restBlend);
        if (emotion == PetEmotion.Happy) bob -= Math.Abs(Math.Sin(emotionTime * 5)) * 3 * emotionStrength;
        double shake = emotion == PetEmotion.Angry ? Math.Sin(emotionTime * 24) * 1.4 * emotionStrength : 0;
        double facingScale = pet.Motion.Ready ? pet.Motion.FacingScale : pet.Facing;
        dc.PushTransform(new ScaleTransform(facingScale, 1, 56, 56));
        dc.PushTransform(new TranslateTransform(shake, bob));
        // Scale around planted feet: breathing/stretching never changes world position.
        double stretch = sleeping || sleepyBlend > 0 ? Math.Sin(pet.ActionTime * 2) * 0.025 * (sleeping ? 1 : sleepyBlend) :
            waking ? Math.Sin(wakeProgress * Math.PI) * 0.08 : 0;
        double preparation = pet.Action == PetAction.PreparingJump ? Math.Sin(Math.Clamp(pet.ActionTime / PetSettings.JumpPreparationDuration, 0, 1) * Math.PI / 2) : 0;
        double flight = airborne ? Math.Clamp(Math.Abs(pet.VY) / 700, 0, 1) * 0.065 : 0;
        double impact = pet.Motion.Ready ? pet.Motion.LandingSquash * influence : 0;
        if (sleeping || waking || sleepyBlend > 0) { preparation = flight = impact = 0; }
        dc.PushTransform(new ScaleTransform(1 + selfStretch * 0.045 + preparation * 0.04 + impact * 0.045 - flight * 0.3,
            1 + stretch + selfStretch * 0.12 - playCrouch * 0.07 - preparation * 0.09 + flight - impact * 0.09, 56, 107));

        // Tail, body, and feet are drawn back-to-front.
        var tail = new StreamGeometry();
        using (var context = tail.Open())
        {
            context.BeginFigure(new Point(77, 84), false, false);
            context.BezierTo(new Point(108, 88), new Point(104, 59),
                new Point(92, 62 + (pet.Motion.Ready ? pet.Motion.TailLag * influence * pet.Motion.Facing : 0) + Math.Sin(time * (3 - 2 * (sleeping ? 1 : sleepyBlend))) * (5 - 4 * (sleeping ? 1 : sleepyBlend))), true, false);
        }
        dc.DrawGeometry(null, new Pen(Ink, 13) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, tail);
        dc.DrawGeometry(null, new Pen(Fur, 8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, tail);
        Oval(dc, Fur, 55, 80 + 8 * restBlend, 26, 23 - 7 * restBlend);
        Oval(dc, Cream, 55, 86, 15, 16 - 5 * restBlend);
        if (pickedUp && pickupBlend > 0)
        {
            dc.PushOpacity(pickupBlend);
            Oval(dc, Fur, 36, 93, 6, 12);
            Oval(dc, Fur, 75, 93, 6, 12);
            dc.Pop();
        }
        Oval(dc, Fur, 40 + stride, pickedUp ? 104 + pickupBlend * 2 : airborne ? 95 : 104 - Math.Max(0, stride), 11, 5);
        Oval(dc, Fur, 70 - stride, pickedUp ? 104 + pickupBlend * 2 : airborne ? 95 : 104 + Math.Min(0, stride), 11, 5);

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
        if (eating)
        {
            glanceX = 4 * Math.Clamp(eatTime / 0.6, 0, 1);
            glanceY = 2;
        }
        if (seeking)
        {
            double envelope = Math.Sin(Math.PI * Math.Clamp(pet.ActionTime / 2.4, 0, 1));
            glanceX = 3 * envelope; glanceY = -2 * envelope;
            if (pet.ActionTime > 0.65)
                Oval(dc, Fur, 79 + 2 * Math.Sin(pet.ActionTime * 9), 78 - 15 * envelope, 6, 9);
        }
        if (grooming)
        {
            glanceY = 3 * groomingBlend;
            
        }
        glanceX += pettingLean * 3;
        double headY = 49 + 10 * restBlend + (emotion == PetEmotion.Sad ? 2 * emotionStrength : 0);
        dc.PushTransform(new TranslateTransform(glanceX * (eating ? 1.4 : 0.45), headY - 49 + glanceY * 0.6 +
            (eating && eatTime > 1.3 ? Math.Sin(eatTime * 16) * 1.2 : 0)));
        dc.PushTransform(new RotateTransform(pickedUp ? Math.Sin(pet.Pickup.Time * 10) * dizzy * 3 + (pet.Motion.Ready ? pet.Motion.HeadTilt * influence * pet.Motion.Facing : 0) : glanceDirection * glanceBlend * 2.5 + (pet.Motion.Ready ? pet.Motion.HeadTilt * influence * pet.Motion.Facing : 0) + pettingLean * 7 + (seeking ? Math.Sin(Math.PI * Math.Clamp(pet.ActionTime / 2.4, 0, 1)) * 6 : 0), 55, 49));
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

        bool blink = time % 4.7 < 0.14;
        double look = pet.Action == PetAction.Looking ? Math.Sin(time * 2) * 3 : 1;
        look += glanceX;
        dc.PushOpacity(1 - emotionStrength);
        foreach (double eyeX in new[] { 42.0, 68.0 })
        {
            if (dizzy > 0.45)
            {
                double eyeSize = 2 + dizzy * 2;
                dc.DrawLine(Outline, new Point(eyeX - eyeSize, 46 - eyeSize), new Point(eyeX + eyeSize, 46 + eyeSize));
                dc.DrawLine(Outline, new Point(eyeX + eyeSize, 46 - eyeSize), new Point(eyeX - eyeSize, 46 + eyeSize));
            }
            else if (sleeping || (waking && wakeProgress < 0.3) || (!waking && blink))
                dc.DrawLine(Outline, new Point(eyeX - 4, 47), new Point(eyeX + 4, 47));
            else
            {
                dc.DrawEllipse(Ink, null, new Point(eyeX + look, 46 + glanceY), 3.5,
                    waking ? 1 + wakeProgress * 3.5 : airborne ? 6 : 4.5 + pickupBlend * 1.5);
                dc.DrawEllipse(Brushes.White, null, new Point(eyeX + look + 1, 44 + glanceY), 1, 1.5);
            }
        }
        Triangle(dc, new Point(51, 54), new Point(59, 54), new Point(55, 58), Pink);
        if (eating && eatTime > 1.3)
            dc.DrawEllipse(Ink, null, new Point(55, 63), 3.5, 1.5 + Math.Abs(Math.Sin(eatTime * 16)) * 2);
        else if (waking)
            dc.DrawEllipse(Ink, null, new Point(55, 63), 3, 2 + Math.Sin(wakeProgress * Math.PI) * 4);
        else
        {
            dc.DrawLine(Outline, new Point(55, 58), new Point(55, 62));
            dc.DrawLine(Outline, new Point(55, 62), new Point(50, 64));
            dc.DrawLine(Outline, new Point(55, 62), new Point(60, 64));
        }
        dc.Pop();
        if (emotionStrength > 0)
        {
            dc.PushOpacity(emotionStrength);
            DrawEmotionFace(dc, emotion, emotionTime);
            dc.Pop();
        }
        dc.DrawLine(Outline, new Point(32, 56), new Point(17, 53));
        dc.DrawLine(Outline, new Point(32, 61), new Point(17, 63));
        dc.DrawLine(Outline, new Point(78, 56), new Point(93, 53));
        dc.DrawLine(Outline, new Point(78, 61), new Point(93, 63));
        if (grooming)
        {
            double pawY = pet.ActionTime < 1.6 ? 65 : 48 + Math.Sin(pet.ActionTime * 8) * 5;
            Oval(dc, Fur, 65, 86 + (pawY - 86) * groomingBlend, 6, 9);
            if (pet.ActionTime > 0.5 && pet.ActionTime < 1.6)
                dc.DrawEllipse(Pink, null, new Point(59, 62), 2, 2 + Math.Abs(Math.Sin(pet.ActionTime * 12)) * 2);
        }
        dc.Pop();
        dc.Pop();
        dc.Pop();
        dc.Pop();
        dc.Pop();
        if (pickedUp)
        {
            if (dizzy > 0.02)
            {
                dc.PushOpacity(Math.Min(1, dizzy * 2));
                var orbit = new EllipseGeometry(new Point(55, 6), 37, 9);
                dc.DrawGeometry(null, EffectHalo, orbit);
                dc.DrawGeometry(null, EffectBorder, orbit);
                dc.DrawGeometry(null, new Pen(Brushes.Gold, 1.2), orbit);
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
            dc.Pop();
        }
        if (emotionStrength > 0 && emotion is PetEmotion.Happy or PetEmotion.Angry or PetEmotion.Surprised)
        {
            dc.PushOpacity(emotionStrength);
            DrawEmotionSymbol(dc, emotion, emotionTime);
            dc.Pop();
        }
        if (sleeping || (emotion == PetEmotion.Sleepy && emotionStrength > 0))
        {
            dc.PushOpacity(sleeping ? 1 : emotionStrength);
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
        if (pet.BatRemaining > 0 && !pickedUp && !sleeping && !airborne && !waking)
        {
            dc.PushTransform(new ScaleTransform(facingScale, 1, 56, 56));
            Oval(dc, Fur, 84 + Math.Sin(pet.BatRemaining / 0.3 * Math.PI) * 12, 98, 10, 6);
            dc.Pop();
        }
        if (eating)
        {
            dc.PushTransform(new ScaleTransform(facingScale, 1, 56, 56));
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
        dc.Pop(); // Permanent transparent margin, identical for every state.
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
            dc.DrawGeometry(null, new Pen(Brushes.Tomato, 1.5), symbol);
        }
        else
        {
            var line = SurpriseGeometry;
            dc.DrawGeometry(null, EffectHalo, line);
            dc.DrawGeometry(null, EffectBorder, line);
            dc.DrawGeometry(null, new Pen(Brushes.Gold, 1.5), line);
        }
    }
}
