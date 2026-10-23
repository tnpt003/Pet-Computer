using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// All art is local vector drawing. Replace this element with a sprite renderer later.
public sealed class PetRenderer(PetModel pet) : FrameworkElement
{
    // Coat colours come from the chosen palette (default Orange = original artwork).
    private CoatPalette palette = CoatPalette.Orange;
    private string coat = "Orange", accessory = "None";
    public string Coat
    {
        get => coat;
        set { coat = PetPreferences.NormalizeCoat(value); palette = CoatPalette.For(coat); InvalidateVisual(); }
    }
    public string Accessory
    {
        get => accessory;
        set { accessory = PetPreferences.NormalizeAccessory(value); InvalidateVisual(); }
    }
    private Brush Fur => palette.Fur;
    private Brush Cream => palette.Cream;
    private Brush Ink => palette.Ink;
    private Brush Pink => palette.Pink;
    private Pen Outline => palette.Outline;
    // The cat's share of the landing springs (the keepsake shows them at 1): a hard fall flattens it
    // by up to ~22%; the wobble tips it at most this many degrees; its dust puffs are a bit bigger.
    private const double LandingSquashScale = 0.75, LandingTiltLimit = 12, LandingDustScale = 1.25;
    private static readonly Brush EffectInk = Frozen("#302B48");
    private static readonly Pen EffectBorder = FrozenPen(EffectInk, 3, PenLineJoin.Round);
    private static readonly Pen EffectHalo = FrozenPen(Brushes.White, 5.5, PenLineJoin.Round);
    // Frozen, shared pens avoid allocating new pens for every frame at 60 FPS.
    private Pen TailOutline => palette.TailOutline;
    private Pen TailFill => palette.TailFill;
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

    private void Oval(DrawingContext dc, Brush fill, double x, double y, double rx, double ry)
        => dc.DrawEllipse(fill, Outline, new Point(x, y), rx, ry);

    private void Triangle(DrawingContext dc, Point a, Point b, Point c, Brush fill)
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
        public double Shake, Bob, ScaleX, ScaleY, ImpactTilt;
        // Gestures: the active one, its time and a 0..1 envelope; Roll is the eased roll-over amount.
        public PetAction Gesture;
        public double GestureTime, GestureBlend, Roll, AngryBlend, SneezeTilt, SneezeLift;
        public bool Walking, Sleeping, Waking, PickedUp, Airborne, Seeking, Eating, Grooming, Climbing;
        public PetEmotion Emotion;
        // Battle Mode: the wooden sword's grip (front paw) and angle (degrees from upright, toward the front).
        public bool Sword;
        public double SwordAngle, GripX, GripY;
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
        // The landing's dust stays where the feet touched down while the cat bounces on.
        if (pet.Motion.Ready) LandingDust.Draw(dc, pet.Motion.Landing, 56 + pet.Motion.Landing.X - pet.X, 107 + pet.Motion.Landing.Y - pet.Y, 26, LandingDustScale);
        if (p.PickedUp)
        {
            DrawDizzyOrbit(dc, p);
            dc.Pop();
        }
        // Lost the battle, or stunned by a dash: seeing stars, nothing worse. A stunned cat that is
        // picked up gets the one orbit above (the stronger of the two dizziness amounts).
        else if (pet.BattleDazed || pet.BattleStun > 0 || pet.Bonk > 0) DrawDizzyOrbit(dc, p);
        DrawOverlays(dc, p);
        if (pet.BattleBuff) DrawCheerSparkles(dc, pet.BattleTime);
        if (pet.BattleShield) DrawShieldBubble(dc, pet.BattleTime);
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
        // A battle stun: dazed eyes and stars, fading out over its last quarter second.
        if (pet.BattleStun > 0) p.Dizzy = Math.Max(p.Dizzy, 0.75 * Math.Min(1, pet.BattleStun / 0.25));
        // Bonked by the falling keepsake: the same stars, a little stronger, fading out the same way.
        if (pet.Bonk > 0) p.Dizzy = Math.Max(p.Dizzy, 0.85 * Math.Min(1, pet.Bonk / 0.3));
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
        p.Gesture = selfCare && PetActionRules.IsGesture(pet.Action) ? pet.Action : PetAction.Idle;
        if (p.Gesture != PetAction.Idle)
        {
            double duration = PetSettings.GestureDuration(p.Gesture);
            p.GestureTime = Math.Clamp(pet.ActionTime, 0, duration);
            double ramp = Math.Clamp(Math.Min(p.GestureTime / 0.35, (duration - p.GestureTime) / 0.35), 0, 1);
            p.GestureBlend = ramp * ramp * (3 - 2 * ramp);
            if (p.Gesture == PetAction.Scratching) selfStretch = 0.6 * p.GestureBlend; // Reach up to scratch.
            if (p.Gesture == PetAction.RollingOver) p.Roll = p.GestureBlend;
            if (p.Gesture == PetAction.Sneezing)
            {
                // Wind up (head back), sneeze (snap forward and down), recover.
                double t = p.GestureTime;
                p.SneezeTilt = t < 0.35 ? -7 * Math.Sin(t / 0.35 * Math.PI / 2) : t < 0.5 ? -7 + 17 * (t - 0.35) / 0.15 : 10 * Math.Max(0, 1 - (t - 0.5) / 0.5);
                p.SneezeLift = t < 0.35 ? -2 * t / 0.35 : t < 0.5 ? -2 + 6 * (t - 0.35) / 0.15 : 4 * Math.Max(0, 1 - (t - 0.5) / 0.5);
            }
        }
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
        p.AngryBlend = p.Emotion == PetEmotion.Angry ? p.EmotionStrength : 0;
        if (p.Gesture == PetAction.ChasingTail)
        {
            // Four full turns in place: the horizontal flip passes through edge-on, which
            // reads as spinning; whole turns end facing the original way.
            p.FacingScale *= Math.Cos(2 * Math.PI * p.GestureTime / (PetSettings.TailChaseDuration / 4));
            p.Bob = p.Bob * (1 - p.GestureBlend) - Math.Abs(Math.Sin(p.GestureTime * 8.4)) * 3 * p.GestureBlend;
        }
        if (p.Gesture == PetAction.RollingOver) { p.Stride = Math.Sin(p.GestureTime * 9) * 4 * p.Roll; p.Bob *= 1 - p.Roll; }
        p.Climbing = pet.Action == PetAction.Climbing;
        if (p.Climbing) { p.Stride = Math.Sin(p.Time * 9) * 4; p.Bob = 0; } // Paws and hind feet push in turn.
        // Scale around planted feet: breathing/stretching never changes world position.
        double stretch = p.Sleeping || p.SleepyBlend > 0 ? Math.Sin(pet.ActionTime * 2) * 0.025 * (p.Sleeping ? 1 : p.SleepyBlend) :
            p.Waking ? Math.Sin(p.WakeProgress * Math.PI) * 0.08 : 0;
        double preparation = pet.Action == PetAction.PreparingJump ? Math.Sin(Math.Clamp(pet.ActionTime / PetSettings.JumpPreparationDuration, 0, 1) * Math.PI / 2) : 0;
        double flight = p.Airborne ? Math.Clamp(Math.Abs(pet.VY) / 700, 0, 1) * 0.065 : 0;
        // The landing springs are the keepsake's (PetImpact.cs), scaled down for the cat: + squash
        // flattens and widens, the rebound's − stretches it; the wobble tips it around its feet.
        double impact = pet.Motion.Ready ? pet.Motion.LandingSquash * p.Influence * LandingSquashScale : 0;
        p.ImpactTilt = pet.Motion.Ready ? Math.Clamp(pet.Motion.LandingTilt * p.Influence, -LandingTiltLimit, LandingTiltLimit) : 0;
        if (p.Sleeping || p.Waking || p.SleepyBlend > 0) { preparation = flight = impact = p.ImpactTilt = 0; }
        p.ScaleX = 1 + selfStretch * 0.045 + preparation * 0.04 + impact * 0.6 - flight * 0.3;
        p.ScaleY = 1 + stretch + selfStretch * 0.12 - playCrouch * 0.07 - preparation * 0.09 + flight - impact;
        if (p.Climbing) { p.ScaleX *= 0.95; p.ScaleY *= 1.06; } // Stretched long up the wall.
        if (pet.Bonk > 0 && !p.PickedUp)
        {
            // The bonk squashes the cat flat, then it springs back with a wobble.
            double since = PetModel.BonkDuration - pet.Bonk, bonk = since < 0.7 ? Math.Exp(-since * 5) * Math.Cos(since * 20) : 0;
            p.ScaleY *= 1 - 0.2 * bonk; p.ScaleX *= 1 + 0.1 * bonk;
        }
        if (pet.SwordOut) PoseSword(ref p);
        return p;
    }

    // Swing keyframes (progress, angle), timed so the weapon reaches its target at the hit moment
    // (pet.SwordHitAt). Sword: a quick wind-up over the head and a slash down in front. Hammer:
    // raised higher, held there (the heavy wind-up), then a short slam and a slow recovery.
    private (double At, double Angle)[] SwingKeys()
    {
        double h = Math.Clamp(pet.SwordHitAt, 0.2, 0.85);
        if (swingKeys != null && swingKeysFor == (h, pet.Hammer)) return swingKeys;
        swingKeysFor = (h, pet.Hammer);
        if (pet.Hammer)
        {
            double raised = Math.Max(0.12, h - 0.3), held = Math.Max(raised + 0.05, h - 0.08);
            return swingKeys = new[] { (0.0, 30.0), (raised, -40.0), (held, -46.0), (h + 0.02, 132.0), (Math.Min(0.95, h + 0.16), 126.0), (1.0, 30.0) };
        }
        return swingKeys = new[] { (0.0, 30.0), (Math.Max(0.1, h - 0.1), -25.0), (h + 0.05, 125.0), (Math.Min(0.95, h + 0.17), 120.0), (1.0, 30.0) };
    }
    private (double At, double Angle)[]? swingKeys;
    private (double, bool) swingKeysFor;

    private void PoseSword(ref Pose p)
    {
        p.Sword = true;
        double angle = 30 + p.Stride * 0.7, gripX = 80, gripY = 88 + 6 * p.RestBlend;
        if (p.Airborne) angle = 10;
        double swing = pet.SwordSwing;
        if (swing >= 0)
        {
            var keys = SwingKeys();
            for (int i = 1; i < keys.Length; i++)
            {
                if (swing > keys[i].At && i < keys.Length - 1) continue;
                double k = Math.Clamp((swing - keys[i - 1].At) / (keys[i].At - keys[i - 1].At), 0, 1);
                // The hammer's slam accelerates into the hit instead of easing out.
                k = pet.Hammer && keys[i].Angle > keys[i - 1].Angle + 100 ? k * k : k * k * (3 - 2 * k);
                angle = keys[i - 1].Angle + (keys[i].Angle - keys[i - 1].Angle) * k;
                break;
            }
            // The hammer's held wind-up reads as straining: the cat crouches a little and trembles.
            if (pet.Hammer && keys.Length > 3 && swing > keys[1].At && swing < keys[3].At)
            {
                double strain = Math.Clamp((swing - keys[1].At) / Math.Max(0.01, keys[2].At - keys[1].At), 0, 1);
                p.Bob += 3 * strain;
                p.Shake += Math.Sin(p.Time * 55) * 1.3 * strain;
                angle += Math.Sin(p.Time * 40) * 3 * strain;
            }
        }
        if (pet.BattleCheer) { angle = -5 + Math.Sin(p.Time * 6) * 6; gripX = 84; gripY = 68; p.Bob -= Math.Abs(Math.Sin(p.Time * 6)) * 4; }
        if (pet.BattleDazed) { angle = 115; gripY = 97; p.Dizzy = 0.7; }
        if (pet.BattleHurt > 0 && !p.PickedUp) p.Shake += Math.Sin(p.Time * 45) * 2.2 * Math.Min(1, pet.BattleHurt / 0.35);
        if (p.PickedUp)
        {
            // Dangling forward from the paw, inside the window margin at any swing angle.
            double blend = p.PickupBlend;
            angle += (100 - angle) * blend; gripX += (72 - gripX) * blend; gripY += (92 - gripY) * blend;
        }
        p.SwordAngle = angle; p.GripX = gripX; p.GripY = gripY;
    }

    private static readonly Brush SwordWood = Frozen("#E8C48A");
    private static readonly Brush SwordHandle = Frozen("#8A5A3B");
    private static readonly Pen SwordPen = FrozenPen(EffectInk, 1.8, PenLineJoin.Round);
    private static readonly Pen SwordGrainPen = FrozenPen(Frozen("#C79A5B"), 1.2);
    private static readonly Pen SwooshPen = FrozenPen(Frozen("#B0FFFFFF"), 5, PenLineJoin.Round, true);
    private static readonly Pen ComboSwooshPen = FrozenPen(Frozen("#E0FFD23F"), 8, PenLineJoin.Round, true);
    private static readonly Geometry SwordBlade = CachedGeometry("M -3.2,-8 L 3.2,-8 L 3.2,-33 L 0,-38.5 L -3.2,-33 Z");
    private static readonly Geometry SwordGuard = CachedGeometry("M -8,-9 L 8,-9 L 8,-4.5 L -8,-4.5 Z");
    private static readonly Geometry SwordGrip = CachedGeometry("M -2.2,-4.5 L 2.2,-4.5 L 2.2,6 L -2.2,6 Z");
    // The toy wooden hammer: a long handle and a mallet head (same grip, so the same poses).
    private static readonly Geometry HammerHandle = CachedGeometry("M -2.2,-29 L 2.2,-29 L 2.2,6 L -2.2,6 Z");
    private static readonly Geometry HammerHead = CachedGeometry("M -9,-39 L 9,-39 L 9,-27 L -9,-27 Z");
    private static readonly Pen HammerSwooshPen = FrozenPen(Frozen("#B0FFFFFF"), 7, PenLineJoin.Round, true);

    // Battle Mode skills on the cat. Shield: a soap bubble around it. Cheer: golden sparkles rising.
    private static readonly Brush BubbleFill = Frozen("#3062D6FF");
    private static readonly Pen BubbleRim = FrozenPen(Frozen("#D0FFFFFF"), 2.5);
    private static readonly Pen BubbleEdge = FrozenPen(Frozen("#9062B8E8"), 1.5);
    private static readonly Pen BubbleShine = FrozenPen(Frozen("#E0FFFFFF"), 3, PenLineJoin.Round, true);
    private static readonly Geometry SparkleGeometry = CachedGeometry("M 0,-6 L 1.6,-1.6 L 6,0 L 1.6,1.6 L 0,6 L -1.6,1.6 L -6,0 L -1.6,-1.6 Z");

    // Skill effects follow the battle's clock (time), so they stand still while it is paused.
    private void DrawShieldBubble(DrawingContext dc, double time)
    {
        double wobble = Math.Sin(time * 4) * 1.5;
        var center = new Point(56, 62);
        dc.DrawEllipse(BubbleFill, BubbleEdge, center, 60 + wobble, 57 - wobble);
        dc.DrawEllipse(null, BubbleRim, center, 58 + wobble, 55 - wobble);
        var shine = new StreamGeometry();
        using (var context = shine.Open())
        {
            context.BeginFigure(new Point(20, 40), false, false);
            context.ArcTo(new Point(42, 14), new Size(44, 44), 0, false, SweepDirection.Clockwise, true, false);
        }
        dc.DrawGeometry(null, BubbleShine, shine);
    }

    private void DrawCheerSparkles(DrawingContext dc, double time)
    {
        for (int i = 0; i < 4; i++)
        {
            double phase = (time * 0.9 + i / 4.0) % 1;
            double x = (i % 2 == 0 ? 14 : 98) + Math.Sin(time * 3 + i) * 5, y = 104 - phase * 70;
            dc.PushOpacity(Math.Sin(phase * Math.PI));
            dc.PushTransform(new TranslateTransform(x, y));
            dc.DrawGeometry(Brushes.Gold, EffectBorder, SparkleGeometry);
            dc.DrawGeometry(Brushes.Gold, null, SparkleGeometry);
            dc.Pop(); dc.Pop();
        }
    }

    // The weapon alone, for the battle setup cards: the same wooden art as in the paw, tilted,
    // about 46 DIPs long with its grip end at (0, 0).
    public static void DrawWeaponIcon(DrawingContext dc, bool hammer)
    {
        dc.PushTransform(new RotateTransform(35));
        DrawWeaponShape(dc, hammer);
        dc.Pop();
    }

    private static void DrawWeaponShape(DrawingContext dc, bool hammer)
    {
        if (hammer)
        {
            dc.DrawGeometry(SwordHandle, SwordPen, HammerHandle);
            dc.DrawGeometry(SwordWood, SwordPen, HammerHead);
            dc.DrawLine(SwordGrainPen, new Point(-5, -37), new Point(-5, -29));
            dc.DrawLine(SwordGrainPen, new Point(5, -37), new Point(5, -29));
        }
        else
        {
            dc.DrawGeometry(SwordHandle, SwordPen, SwordGrip);
            dc.DrawGeometry(SwordWood, SwordPen, SwordBlade);
            dc.DrawLine(SwordGrainPen, new Point(0, -12), new Point(0, -31));
            dc.DrawGeometry(SwordHandle, SwordPen, SwordGuard);
        }
    }

    // A toy wooden sword (or hammer) held in the front paw (inside the facing flip, so it turns with the cat).
    private void DrawSword(DrawingContext dc, in Pose p)
    {
        double swing = pet.SwordSwing, hitAt = Math.Clamp(pet.SwordHitAt, 0.2, 0.85);
        if (swing > hitAt - 0.08 && swing < hitAt + 0.26 && !p.PickedUp)
        {
            // A soft swoosh along the slash (the hammer's is wider), brightest just after the hit.
            double fade = 1 - Math.Abs(swing - (hitAt + 0.06)) / 0.2;
            var arc = new StreamGeometry();
            using (var context = arc.Open())
            {
                double a0 = -20 * Math.PI / 180, a1 = Math.Min(p.SwordAngle, 125) * Math.PI / 180;
                context.BeginFigure(new Point(p.GripX + Math.Sin(a0) * 34, p.GripY - Math.Cos(a0) * 34), false, false);
                context.ArcTo(new Point(p.GripX + Math.Sin(a1) * 34, p.GripY - Math.Cos(a1) * 34), new Size(34, 34), 0, false, SweepDirection.Clockwise, true, false);
            }
            dc.PushOpacity(Math.Clamp(fade, 0, 1));
            dc.DrawGeometry(null, pet.SwordCombo ? ComboSwooshPen : pet.Hammer ? HammerSwooshPen : SwooshPen, arc);
            dc.Pop();
        }
        dc.PushTransform(new TranslateTransform(p.GripX, p.GripY));
        dc.PushTransform(new RotateTransform(p.SwordAngle));
        DrawWeaponShape(dc, pet.Hammer);
        dc.Pop(); dc.Pop();
        Oval(dc, Fur, p.GripX, p.GripY, 5.5, 5); // The paw around the grip.
    }

    // Body and head share the facing, bob/shake and squash transforms.
    private void DrawCat(DrawingContext dc, in Pose p)
    {
        if (p.ImpactTilt != 0) dc.PushTransform(new RotateTransform(p.ImpactTilt, 56, 107));
        dc.PushTransform(new ScaleTransform(p.FacingScale, 1, 56, 56));
        // Roll onto the back: lie down sideways, lifted and shifted so the cat stays above
        // its floor line and inside the window margin.
        if (p.Roll > 0)
        {
            dc.PushTransform(new TranslateTransform(22 * p.Roll, -14 * p.Roll));
            dc.PushTransform(new RotateTransform(-80 * p.Roll, 56, 104));
        }
        dc.PushTransform(new TranslateTransform(p.Shake, p.Bob));
        dc.PushTransform(new ScaleTransform(p.ScaleX, p.ScaleY, 56, 107));
        // Climbing: lean into the window's side (in front, after the facing flip), shifted back
        // so the head just touches the edge while the paws hook over it.
        if (p.Climbing) dc.PushTransform(ClimbLean);
        DrawBody(dc, p);
        // Raised high (wind-up, cheering) the sword passes behind the head instead of over the face.
        bool swordBehind = p.Sword && p.SwordAngle < 15;
        if (swordBehind) DrawSword(dc, p);
        DrawHead(dc, p);
        if (p.Sword && !swordBehind) DrawSword(dc, p);
        if (p.Climbing) { DrawClimbingPaws(dc, p); dc.Pop(); }
        dc.Pop();
        dc.Pop();
        if (p.Roll > 0) { dc.Pop(); dc.Pop(); }
        dc.Pop();
        if (p.ImpactTilt != 0) dc.Pop();
    }

    private static readonly Transform ClimbLean = CreateClimbLean();
    private static Transform CreateClimbLean()
    {
        var lean = new TransformGroup { Children = { new RotateTransform(10, 56, 104), new TranslateTransform(-8, 0) } };
        lean.Freeze(); return lean;
    }

    private void DrawBody(DrawingContext dc, in Pose p)
    {
        // Tail, body, and feet are drawn back-to-front.
        var tail = new StreamGeometry();
        using (var context = tail.Open())
        {
            if (p.Climbing)
            {
                // Hanging away from the wall as a counterweight, swaying gently.
                double sway = Math.Sin(p.Time * 3) * 4;
                context.BeginFigure(new Point(38, 92), false, false);
                context.BezierTo(new Point(22, 98), new Point(14 + sway, 110), new Point(20 + sway, 124), true, false);
            }
            else
            {
                context.BeginFigure(new Point(77, 84), false, false);
                context.BezierTo(new Point(108, 88), new Point(104, 59),
                    new Point(92, 62 + (pet.Motion.Ready ? pet.Motion.TailLag * p.Influence * pet.Motion.Facing : 0) + Math.Sin(p.Time * (3 - 2 * (p.Sleeping ? 1 : p.SleepyBlend))) * (5 - 4 * (p.Sleeping ? 1 : p.SleepyBlend)) + TailSwish(p)), true, false);
            }
        }
        dc.DrawGeometry(null, TailOutline, tail);
        dc.DrawGeometry(null, TailFill, tail);
        Oval(dc, Fur, 55, 80 + 8 * p.RestBlend, 26, 23 - 7 * p.RestBlend);
        if (palette.Markings != CoatMarkings.None) DrawBodyMarkings(dc, 80 + 8 * p.RestBlend, 23 - 7 * p.RestBlend);
        Oval(dc, Cream, 55, 86, 15, 16 - 5 * p.RestBlend);
        if (p.PickedUp && p.PickupBlend > 0)
        {
            dc.PushOpacity(p.PickupBlend);
            Oval(dc, Fur, 36, 93, 6, 12);
            Oval(dc, Fur, 75, 93, 6, 12);
            dc.Pop();
        }
        if (p.Climbing)
        {
            // Hind feet braced against the wall, pushing in turn.
            Oval(dc, palette.Paw, 82, 100 + p.Stride, 5, 9);
            Oval(dc, palette.Paw, 88, 88 - p.Stride, 5, 9);
            return;
        }
        Oval(dc, palette.Paw, 40 + p.Stride, p.PickedUp ? 104 + p.PickupBlend * 2 : p.Airborne ? 95 : 104 - Math.Max(0, p.Stride), 11, 5);
        Oval(dc, palette.Paw, 70 - p.Stride, p.PickedUp ? 104 + p.PickupBlend * 2 : p.Airborne ? 95 : 104 + Math.Min(0, p.Stride), 11, 5);
        if (p.Gesture is PetAction.Kneading or PetAction.Scratching) DrawGesturePaws(dc, p);
    }

    // Front paws gripping the window's side above the head, reaching up in turn, with claws
    // hooked onto the edge.
    private void DrawClimbingPaws(DrawingContext dc, in Pose p)
    {
        double reach = 7 * Math.Sin(p.Time * 9);
        foreach (var (x, y) in new[] { (84.0, 14 + reach), (88.0, 32 - reach) })
        {
            Oval(dc, Fur, x, y, 6, 9);
            for (int i = -1; i <= 1; i++)
                dc.DrawLine(Outline, new Point(x + 4, y - 4 + i * 3), new Point(x + 8, y - 5 + i * 3));
        }
    }

    // Fast swish while chasing the tail, an irritated lash while angry; zero otherwise.
    private static double TailSwish(in Pose p) =>
        p.Gesture == PetAction.ChasingTail ? Math.Sin(p.GestureTime * 14) * 9 * p.GestureBlend :
        p.AngryBlend > 0 ? Math.Sin(p.Time * 9) * 8 * p.AngryBlend : 0;

    // Front paws for kneading (pressing in turn) and scratching (raking down in front).
    private void DrawGesturePaws(DrawingContext dc, in Pose p)
    {
        double t = p.GestureTime;
        dc.PushOpacity(p.GestureBlend);
        if (p.Gesture == PetAction.Kneading)
        {
            Oval(dc, Fur, 46, 97 - 4 * Math.Max(0, Math.Sin(t * 6)), 6, 7);
            Oval(dc, Fur, 64, 97 - 4 * Math.Max(0, Math.Sin(t * 6 + Math.PI)), 6, 7);
        }
        else
        {
            // On the side away from the tail, so marks and paws stay readable.
            for (int i = 0; i < 3; i++)
            {
                double x = 16 - i * 5;
                dc.DrawLine(Outline, new Point(x, 70 + i * 2), new Point(x + 3, 90 + i * 2));
            }
            Oval(dc, Fur, 31, 76 + 10 * Math.Abs(Math.Sin(t * 7)), 6, 8);
            Oval(dc, Fur, 23, 76 + 10 * Math.Abs(Math.Sin(t * 7 + Math.PI / 2)), 6, 8);
        }
        dc.Pop();
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
        if (p.Gesture == PetAction.ChasingTail) { glanceX -= 4 * p.GestureBlend; glanceY += 2 * p.GestureBlend; } // Eyes on the tail.
        if (p.Climbing) { glanceX = 2; glanceY = -3; } // Eyes on the top edge.
        double headY = 49 + 10 * p.RestBlend + (p.Emotion == PetEmotion.Sad ? 2 * p.EmotionStrength : 0) + p.SneezeLift;
        dc.PushTransform(new TranslateTransform(glanceX * (p.Eating ? 1.4 : 0.45), headY - 49 + glanceY * 0.6 +
            (p.Eating && p.EatTime > 1.3 ? Math.Sin(p.EatTime * 16) * 1.2 : 0)));
        dc.PushTransform(new RotateTransform(p.PickedUp ? Math.Sin(pet.Pickup.Time * 10) * p.Dizzy * 3 + (pet.Motion.Ready ? pet.Motion.HeadTilt * p.Influence * pet.Motion.Facing : 0) : glanceDirection * glanceBlend * 2.5 + (pet.Motion.Ready ? pet.Motion.HeadTilt * p.Influence * pet.Motion.Facing : 0) + p.PettingLean * 7 + (p.Seeking ? Math.Sin(Math.PI * Math.Clamp(pet.ActionTime / PetSettings.AttentionGestureDuration, 0, 1)) * 6 : 0) + p.SneezeTilt, 55, 49));
        DrawAccessory(dc, AccessoryLayer.Neck);
        dc.PushTransform(new RotateTransform(twitch - 28 * p.AngryBlend, 37, 33)); // Angry: ears flatten.
        Triangle(dc, new Point(27, 38), new Point(27, 13), new Point(47, 31), palette.Ear);
        Triangle(dc, new Point(31, 30), new Point(31, 21), new Point(40, 30), Pink);
        dc.Pop();
        dc.PushTransform(new RotateTransform(-twitch * 0.6 + 28 * p.AngryBlend, 75, 33));
        Triangle(dc, new Point(64, 30), new Point(83, 13), new Point(84, 41), palette.Ear);
        Triangle(dc, new Point(72, 30), new Point(79, 21), new Point(80, 33), Pink);
        dc.Pop();
        Oval(dc, Fur, 55, 48, 31, 25);
        if (palette.Markings != CoatMarkings.None) DrawHeadMarkings(dc);
        Oval(dc, palette.Muzzle, 55, 58, 19, 12);
        DrawFace(dc, p, glanceX, glanceY);
        dc.DrawLine(Outline, new Point(32, 56), new Point(17, 53));
        dc.DrawLine(Outline, new Point(32, 61), new Point(17, 63));
        dc.DrawLine(Outline, new Point(78, 56), new Point(93, 53));
        dc.DrawLine(Outline, new Point(78, 61), new Point(93, 63));
        DrawAccessory(dc, AccessoryLayer.Head);
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

    // Blissful closed eyes while kneading or rolling over.
    private static bool Content(in Pose p) => p.GestureBlend > 0.3 && p.Gesture is PetAction.Kneading or PetAction.RollingOver;

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
            else if (Content(p))
                Curve(dc, new Point(eyeX - 4, 48), new Point(eyeX, 42), new Point(eyeX + 4, 48));
            else if (p.Sleeping || (p.Waking && p.WakeProgress < 0.3) || (!p.Waking && blink) ||
                (p.Gesture == PetAction.Sneezing && p.GestureTime > 0.25 && p.GestureTime < 0.65))
                dc.DrawLine(Outline, new Point(eyeX - 4, 47), new Point(eyeX + 4, 47));
            else
            {
                double eyeHeight = p.Waking ? 1 + p.WakeProgress * 3.5 : p.Airborne ? 6 : 4.5 + p.PickupBlend * 1.5;
                if (palette.Iris != null)
                {
                    // Coloured iris around a narrower pupil keeps dark coats' eyes readable.
                    dc.DrawEllipse(palette.Iris, null, new Point(eyeX + look, 46 + glanceY), 4.6, eyeHeight + 1.1);
                    dc.DrawEllipse(Ink, null, new Point(eyeX + look, 46 + glanceY), 2, eyeHeight);
                }
                else dc.DrawEllipse(Ink, null, new Point(eyeX + look, 46 + glanceY), 3.5, eyeHeight);
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

    // Patches and stripes are clipped just inside the outline so they never spill out.
    private void DrawBodyMarkings(DrawingContext dc, double centerY, double radiusY)
    {
        dc.PushClip(new EllipseGeometry(new Point(55, centerY), 24.8, radiusY - 1.2));
        if (palette.Markings == CoatMarkings.Calico)
        {
            dc.DrawEllipse(palette.PatchA, null, new Point(68, centerY - 9), 13, 11);
            dc.DrawEllipse(palette.PatchB, null, new Point(40, centerY + 2), 10, 10);
        }
        else
        {
            double top = centerY - radiusY;
            foreach (double x in new[] { 44.0, 55.0, 66.0 })
                dc.DrawLine(MarkingPen, new Point(x, top), new Point(x + 1.5, top + 9));
        }
        dc.Pop();
    }

    private void DrawHeadMarkings(DrawingContext dc)
    {
        dc.PushClip(HeadClip);
        if (palette.Markings == CoatMarkings.Calico)
        {
            dc.DrawEllipse(palette.PatchA, null, new Point(33, 35), 15, 13);
            dc.DrawEllipse(palette.PatchB, null, new Point(78, 33), 11, 10);
        }
        else
        {
            // Classic tabby "M" on the forehead plus a stripe over each eye.
            dc.DrawLine(MarkingPen, new Point(49, 24), new Point(50, 32));
            dc.DrawLine(MarkingPen, new Point(55, 23), new Point(55, 33));
            dc.DrawLine(MarkingPen, new Point(61, 24), new Point(60, 32));
            dc.DrawLine(MarkingPen, new Point(36, 30), new Point(40, 36));
            dc.DrawLine(MarkingPen, new Point(74, 30), new Point(70, 36));
        }
        dc.Pop();
    }

    private static readonly Geometry HeadClip = Frozen(new EllipseGeometry(new Point(55, 48), 29.8, 23.8));
    private Pen MarkingPen => palette.MarkingPen!;
    // One accessory from accessories.json, compiled once into frozen geometries, brushes and pens.
    private sealed record DrawnShape(ShapeKind Kind, Geometry? Geometry, Point A, Point B, double RadiusX, double RadiusY,
        Brush? Fill, StrokeKind Stroke, Pen? Pen);
    private static readonly Dictionary<string, (AccessoryLayer Layer, DrawnShape[] Shapes)> Accessories = CompileAccessories();

    private static Dictionary<string, (AccessoryLayer, DrawnShape[])> CompileAccessories()
    {
        var compiled = new Dictionary<string, (AccessoryLayer, DrawnShape[])>();
        foreach (var item in PetAccessories.All)
        {
            try
            {
                compiled[item.Id] = (item.Layer, item.Shapes.Select(shape =>
                {
                    var v = shape.Values;
                    Pen? pen = shape.Stroke.Kind == StrokeKind.Custom
                        ? FrozenPen(Frozen(shape.Stroke.Color!), shape.Stroke.Width, shape.Stroke.RoundJoin ? PenLineJoin.Round : PenLineJoin.Miter, shape.Stroke.Round)
                        : null;
                    return shape.Kind switch
                    {
                        ShapeKind.Path => new DrawnShape(shape.Kind, CachedGeometry(shape.Path!), default, default, 0, 0,
                            shape.Fill == null ? null : Frozen(shape.Fill), shape.Stroke.Kind, pen),
                        ShapeKind.Ellipse => new DrawnShape(shape.Kind, null, new Point(v[0], v[1]), default, v[2], v[3],
                            shape.Fill == null ? null : Frozen(shape.Fill), shape.Stroke.Kind, pen),
                        _ => new DrawnShape(shape.Kind, null, new Point(v[0], v[1]), new Point(v[2], v[3]), 0, 0, null, shape.Stroke.Kind, pen)
                    };
                }).ToArray());
            }
            // Bad path data (accessories.json) skips only that accessory.
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            {
                System.Diagnostics.Trace.WriteLine($"Accessory {item.Id} could not be drawn: {ex.Message}");
            }
        }
        return compiled;
    }

    // Neck items sit inside the head transform before the ears (so they follow tilts and rest
    // poses and only show below the chin); head items are drawn over the face.
    private void DrawAccessory(DrawingContext dc, AccessoryLayer layer)
    {
        if (!Accessories.TryGetValue(accessory, out var item) || item.Layer != layer) return;
        foreach (var shape in item.Shapes)
        {
            var pen = shape.Stroke switch { StrokeKind.Outline => Outline, StrokeKind.Accent => palette.AccentOutline, _ => shape.Pen };
            switch (shape.Kind)
            {
                case ShapeKind.Path: dc.DrawGeometry(shape.Fill, pen, shape.Geometry); break;
                case ShapeKind.Ellipse: dc.DrawEllipse(shape.Fill, pen, shape.A, shape.RadiusX, shape.RadiusY); break;
                default: dc.DrawLine(pen, shape.A, shape.B); break;
            }
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
            double phase = (p.PickedUp ? pet.Pickup.Time : p.Time) * (2 + dizzy * 2) + i * Math.PI * 2 / 3;
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
        if (pet.BattleHurt > 0 && !p.PickedUp)
        {
            // Bonk: a short cartoon impact burst beside the head.
            double k = Math.Min(1, pet.BattleHurt / 0.35);
            dc.PushOpacity(k);
            for (int i = 0; i < 5; i++)
            {
                double a = (i * 72 + 18) * Math.PI / 180, r0 = 5 + (1 - k) * 4, r1 = 11 + (1 - k) * 6;
                var from = new Point(92 + Math.Cos(a) * r0, 30 + Math.Sin(a) * r0);
                var to = new Point(92 + Math.Cos(a) * r1, 30 + Math.Sin(a) * r1);
                dc.DrawLine(EffectHalo, from, to);
                dc.DrawLine(EffectBorder, from, to);
                dc.DrawLine(SurprisePen, from, to);
            }
            dc.Pop();
        }
        if (p.Gesture == PetAction.Sneezing && p.GestureTime > 0.38 && p.GestureTime < 0.95) DrawSneezePuff(dc, p);
        if (pet.Thought.Visible && !p.PickedUp && !p.Sleeping && !p.Airborne) DrawThoughtBubble(dc);
    }

    private void DrawSneezePuff(DrawingContext dc, in Pose p)
    {
        double k = (p.GestureTime - 0.38) / 0.57;
        dc.PushTransform(new ScaleTransform(p.FacingScale, 1, 56, 56));
        dc.PushOpacity(1 - k);
        for (int i = 0; i < 3; i++)
        {
            var puff = new EllipseGeometry(new Point(90 + i * 6 + k * 10, 60 + (i - 1) * 5 + p.SneezeLift), 2.5 + k * 3, 2.5 + k * 3);
            DrawOutlinedEffect(dc, puff, Brushes.White);
        }
        dc.Pop(); dc.Pop();
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

    private void Curve(DrawingContext dc, Point start, Point control, Point end)
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

    private void DrawEmotionFace(DrawingContext dc, PetEmotion emotion, double time)
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
