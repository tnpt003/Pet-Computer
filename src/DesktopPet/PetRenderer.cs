using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

// All art is local vector drawing. Replace this element with a sprite renderer later.
public sealed partial class PetRenderer(PetModel pet) : FrameworkElement
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
    // The landing's jolt: ears flatten, front paws splay, the tail flicks up (full from JoltSpeed,
    // fading in ~0.45 s); from WinceSpeed (a fall of ~330 DIPs) the eyes squeeze shut for WinceSeconds.
    private const double LandingJoltSpeed = 1200, LandingWinceSpeed = 1000, LandingWinceSeconds = 0.3;
    private static readonly Brush EffectInk = PetUi.ArtInk;
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
    private static readonly Brush RageGlow = MakeRageGlow();
    private static Brush MakeRageGlow()
    {
        var brush = new RadialGradientBrush(Color.FromArgb(0x70, 0xF0, 0x40, 0x2C), Color.FromArgb(0, 0xF0, 0x40, 0x2C));
        brush.Freeze(); return brush;
    }
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
        public FarmDish? Dish;
        public double Shake, Bob, ScaleX, ScaleY, ImpactTilt, Jolt;
        public bool Wince;
        // Gestures: the active one, its time and a 0..1 envelope; Roll is the eased roll-over amount.
        public PetAction Gesture;
        public double GestureTime, GestureBlend, Roll, AngryBlend, SneezeTilt, SneezeLift;
        // The portal spell: 0..1 envelope and the seconds since it began.
        public double CastBlend, CastTime;
        public double WaterBlend, WaterTime, StirBlend, StirTime, FishBlend, FishTime, FishBiteAt;
        public FarmToolPose FarmTool;
        public double FarmToolBlend, FarmToolTime, FarmToolHit, FarmToolLength;
        public FarmCrop? FarmToolCrop;
        public bool FarmToolGold;
        public int ToolTier;
        public bool Walking, Sleeping, Waking, PickedUp, Airborne, Seeking, Eating, Grooming, Climbing;
        public PetEmotion Emotion;
        // Battle Mode: the wooden sword's grip (front paw) and angle (degrees from upright, toward the front).
        public bool Sword;
        public double SwordAngle, GripX, GripY;
        // The yarn's strand: the direction (degrees, same convention) from the handle's tip to the ball.
        public double YarnAngle;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var p = ComputePose();
        dc.PushTransform(new TranslateTransform(PickupAnimation.Padding, PickupAnimation.Padding));
        // Brought to the main screen (PetWindow.BringHome): shrinks or pops around the feet, falls in.
        bool hop = TeleportPop != 1 || TeleportDrop != 0;
        if (hop)
        {
            dc.PushTransform(new TranslateTransform(0, -TeleportDrop));
            dc.PushTransform(new ScaleTransform(TeleportPop, TeleportPop, 56, 107));
        }
        // The battle's rage stance (1.5 M07): drawn bigger around its feet (around the scruff while
        // held, the same point the dangle turns around, and only 30 % as much more, so a shaken cat's
        // swing stays inside the window), a red glow behind, the fur reddened.
        bool squeezed = pet.BattleSqueeze < 1;
        if (squeezed) dc.PushTransform(p.PickedUp ? new ScaleTransform(pet.BattleSqueeze, pet.BattleSqueeze, PickupAnimation.AnchorX, PickupAnimation.AnchorY) : new ScaleTransform(pet.BattleSqueeze, pet.BattleSqueeze, 56, 107));
        bool rage = pet.BattleRage > 0;
        var calm = palette;
        if (rage)
        {
            double grow = 1 + (pet.BattleRageScale - 1) * pet.BattleRage * (p.PickedUp ? 0.3 : 1);
            dc.PushTransform(p.PickedUp ? new ScaleTransform(grow, grow, PickupAnimation.AnchorX, PickupAnimation.AnchorY) : new ScaleTransform(grow, grow, 56, 107));
            dc.PushOpacity(pet.BattleRage * (0.75 + 0.25 * Math.Sin(pet.BattleTime * 9)));
            dc.DrawEllipse(RageGlow, null, new Point(56, p.PickedUp ? 66 : 70), 62, 52);
            dc.Pop();
            if (pet.BattleRage > 0.35) palette = CoatPalette.Enraged(coat);
        }
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
        // Asleep in the cardboard box (PetWindow.ToyBox.cs): the same front panel as the box's window right under this one.
        if (BoxFrontDrop >= 0 && !p.PickedUp) ToyBoxArt.DrawFront(dc, 56, 112 + BoxFrontDrop);
        DrawOverlays(dc, p);
        if (pet.BattleBuff) DrawCheerSparkles(dc, pet.BattleTime);
        if (pet.BattleShield) DrawShieldBubble(dc, pet.BattleTime);
        if (pet.BattleNotResponding > 0) DrawNotResponding(dc, pet.BattleTime);
        if (pet.BattleOffBeat) DrawOffBeat(dc, pet.BattleTime);
        if (rage) { dc.Pop(); palette = calm; }
        if (squeezed) dc.Pop();
        if (pet.BattleZip == (int)ZipDebuff.Squeezed) DrawZipSqueeze(dc, pet);
        else if (pet.BattleZip == (int)ZipDebuff.Locked) DrawZipShell(dc, pet);
        if (hop) { dc.Pop(); dc.Pop(); }
        // The tab tornado's near half, over the cat (the far half is on the battle strip below this
        // window): the same ring, on the floor 2 DIPs under the window's bottom edge like the strip's.
        if (pet.BattleTabs > 0 && !p.PickedUp && !p.Airborne)
        {
            dc.PushOpacity(Math.Min(1, pet.BattleTabs / 0.5));
            AppArt.DrawTabRing(dc, 56, 110 - 26, pet.BattleTabRing, pet.BattleTime, true);
            dc.Pop();
        }
        if (TeleportPuff is >= 0 and < 1) KeepsakeArt.DrawPuff(dc, TeleportPuff, 56, 66, 92);
        if (HeartsTime >= 0 && !p.PickedUp) DrawBondHearts(dc, HeartsTime);
        dc.Pop(); // Permanent transparent margin, identical for every state.
    }

    // A new bond milestone (PetWindow.Bond.cs): three small hearts rising over the head and fading.
    public double HeartsTime = -1;
    private static readonly Brush HeartFill = PetUi.Frozen(Color.FromRgb(0xF2, 0x7A, 0x8F));
    private static readonly Pen HeartLine = PetUi.FrozenPen(PetUi.ArtInk, 1.1);
    private static void DrawBondHearts(DrawingContext dc, double t)
    {
        double life = Math.Clamp(t / PetBondRules.HeartsSeconds, 0, 1);
        for (int i = 0; i < 3; i++)
        {
            double local = Math.Clamp(life * 1.4 - i * 0.2, 0, 1);
            if (local <= 0) continue;
            double x = 56 + (i - 1) * 18 + Math.Sin(local * 6 + i) * 3, y = 18 - local * 30, size = 5 + 2 * Math.Sin(local * Math.PI);
            dc.PushOpacity(Math.Min(1, (1 - local) * 2.5));
            var heart = new StreamGeometry();
            using (var g = heart.Open())
            {
                g.BeginFigure(new Point(x, y + size), true, true);
                g.BezierTo(new Point(x - size * 1.6, y), new Point(x - size * 0.8, y - size * 1.1), new Point(x, y - size * 0.3), true, true);
                g.BezierTo(new Point(x + size * 0.8, y - size * 1.1), new Point(x + size * 1.6, y), new Point(x, y + size), true, true);
            }
            heart.Freeze();
            dc.DrawGeometry(HeartFill, HeartLine, heart);
            dc.Pop();
        }
    }

    // The "Bring the pet to the main screen" hop, set by PetWindow every frame of it: the cat's
    // scale around its feet, how far above the floor it is, and the puff (0..1, negative: none).
    public double TeleportPop = 1, TeleportDrop, TeleportPuff = -1;
    // The cardboard box's front panel over the cat: how far below the cat's floor line it stands (negative: none).
    public double BoxFrontDrop = -1;
    // The yarn ball in the cat's mouth while it fetches.
    public bool CarryBall;

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
        if (pet.CastRemaining > 0 && selfCare)
        {
            p.CastTime = PetModel.CastDuration - pet.CastRemaining;
            double ramp = Math.Clamp(Math.Min(p.CastTime / 0.3, pet.CastRemaining / 0.2), 0, 1);
            p.CastBlend = ramp * ramp * (3 - 2 * ramp);
        }
        if (pet.WateringRemaining > 0 && selfCare)
        {
            p.WaterTime = PetModel.WaterDuration - pet.WateringRemaining;
            double ramp = Math.Clamp(Math.Min(p.WaterTime / 0.25, pet.WateringRemaining / 0.2), 0, 1);
            p.WaterBlend = ramp * ramp * (3 - 2 * ramp);
        }
        if (pet.StirringRemaining > 0 && selfCare)
        {
            p.StirTime = PetModel.StirDuration - pet.StirringRemaining;
            double ramp = Math.Clamp(Math.Min(p.StirTime / 0.25, pet.StirringRemaining / 0.2), 0, 1);
            p.StirBlend = ramp * ramp * (3 - 2 * ramp);
        }
        if (pet.FishingTime >= 0 && selfCare)
        {
            p.FishTime = pet.FishingTime;
            p.FishBiteAt = pet.FishingBiteAt;
            double ramp = Math.Clamp(p.FishTime / 0.25, 0, 1);
            p.FishBlend = ramp * ramp * (3 - 2 * ramp);
        }
        p.ToolTier = selfCare ? pet.FarmToolTier : 0;
        if (pet.FarmTool != FarmToolPose.None && selfCare)
        {
            p.FarmTool = pet.FarmTool;
            p.FarmToolTime = pet.FarmToolTime;
            p.FarmToolHit = pet.FarmToolHit;
            p.FarmToolLength = pet.FarmToolLength;
            p.FarmToolCrop = pet.FarmToolCrop;
            p.FarmToolGold = pet.FarmToolGold;
            double ramp = pet.FarmToolHold ? 1 : Math.Clamp(Math.Min(pet.FarmToolTime / 0.15, (pet.FarmToolLength - pet.FarmToolTime) / 0.15), 0, 1);
            p.FarmToolBlend = ramp * ramp * (3 - 2 * ramp);
        }
        p.EatTime = PetModel.TreatDuration - pet.TreatRemaining;
        p.Dish = pet.TreatDish;
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
        // Motion intentionally freezes while held; combat still turns the cat around its neck anchor.
        if (p.PickedUp && pet.SwordOut) p.FacingScale = pet.Facing;
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
        if (pet.Motion.Ready && pet.Motion.Landing.Since < 0.45)
        {
            var landing = pet.Motion.Landing;
            p.Jolt = Math.Clamp(landing.Impact / LandingJoltSpeed, 0, 1) * Math.Min(1, landing.Since / 0.04) * Math.Exp(-landing.Since * 7) * p.Influence;
            p.Wince = landing.Impact >= LandingWinceSpeed && landing.Since < LandingWinceSeconds && p.Influence > 0.5;
        }
        if (p.Sleeping || p.Waking || p.SleepyBlend > 0) { preparation = flight = impact = p.ImpactTilt = p.Jolt = 0; p.Wince = false; }
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
    // raised higher, held there (the heavy wind-up), then a short slam and a slow recovery. Yarn: the
    // handle leads a whip (YarnKeys swings the strand). Glove: a short jab straight ahead and back.
    private (double At, double Angle)[] SwingKeys()
    {
        double h = Math.Clamp(pet.SwordHitAt, 0.2, 0.85);
        if (swingKeys != null && swingKeysFor == (h, pet.WeaponStyle)) return swingKeys;
        swingKeysFor = (h, pet.WeaponStyle);
        if (pet.WeaponStyle == "yarn")
            return swingKeys = new[] { (0.0, 30.0), (Math.Max(0.08, h - 0.16), -45.0), (Math.Max(0.12, h - 0.05), -30.0), (h, 95.0), (Math.Min(0.95, h + 0.18), 90.0), (1.0, 30.0) };
        if (pet.WeaponStyle == "glove")
            return swingKeys = new[] { (0.0, 40.0), (Math.Max(0.1, h - 0.08), 20.0), (h + 0.02, 95.0), (Math.Min(0.95, h + 0.25), 90.0), (1.0, 40.0) };
        if (pet.Hammer)
        {
            double raised = Math.Max(0.12, h - 0.3), held = Math.Max(raised + 0.05, h - 0.08);
            return swingKeys = new[] { (0.0, 30.0), (raised, -40.0), (held, -46.0), (h + 0.02, 132.0), (Math.Min(0.95, h + 0.16), 126.0), (1.0, 30.0) };
        }
        return swingKeys = new[] { (0.0, 30.0), (Math.Max(0.1, h - 0.1), -25.0), (h + 0.05, 125.0), (Math.Min(0.95, h + 0.17), 120.0), (1.0, 30.0) };
    }
    private (double At, double Angle)[]? swingKeys;
    private (double, string) swingKeysFor;

    // The yarn's strand over a swing, like an overhand whip: it hangs, is lifted up in front by the
    // wind-up, trails the handle back over the head (loaded behind it), then cracks straight out in
    // front at the hit, overshoots a little and drops back to hanging.
    private static (double At, double Angle)[] YarnKeys(double h)
    {
        double lifted = Math.Max(0.1, h - 0.12), loaded = Math.Max(lifted + 0.02, h - 0.05);
        return new[] { (0.0, 175.0), (lifted, 15.0), (loaded, -55.0), (h, 102.0), (h + 0.05, 118.0), (Math.Min(0.95, h + 0.2), 135.0), (1.0, 175.0) };
    }

    // Eased between keys; a rise of more than 100° (a slam, the whip's crack) accelerates into its end.
    private static double KeyedAngle((double At, double Angle)[] keys, double t, bool slam)
    {
        for (int i = 1; i < keys.Length; i++)
        {
            if (t > keys[i].At && i < keys.Length - 1) continue;
            double k = Math.Clamp((t - keys[i - 1].At) / (keys[i].At - keys[i - 1].At), 0, 1);
            k = slam && keys[i].Angle > keys[i - 1].Angle + 100 ? k * k : k * k * (3 - 2 * k);
            return keys[i - 1].Angle + (keys[i].Angle - keys[i - 1].Angle) * k;
        }
        return keys[^1].Angle;
    }

    private void PoseSword(ref Pose p)
    {
        p.Sword = true;
        double angle = 30 + p.Stride * 0.7, gripX = 80, gripY = 88 + 6 * p.RestBlend;
        if (p.Airborne) angle = 10;
        double swing = pet.SwordSwing;
        bool yarn = pet.WeaponStyle == "yarn";
        // The strand hangs from the handle's tip, swaying a little (a walk swings it more).
        double sway = Math.Sin(p.Time * 2.4) * 5 + (p.Walking ? Math.Sin(p.Time * 9) * 8 : 0);
        double yarnAngle = (p.Airborne ? 205 : 178) + sway;
        if (swing >= 0)
        {
            var keys = SwingKeys();
            // The hammer's slam accelerates into the hit instead of easing out.
            angle = KeyedAngle(keys, swing, pet.Hammer);
            if (yarn) yarnAngle = KeyedAngle(YarnKeys(Math.Clamp(pet.SwordHitAt, 0.2, 0.85)), swing, true) + sway * (1 - Math.Sin(Math.PI * swing));
            // The hammer's held wind-up reads as straining: the cat crouches a little and trembles.
            if (pet.Hammer && keys.Length > 3 && swing > keys[1].At && swing < keys[3].At)
            {
                double strain = Math.Clamp((swing - keys[1].At) / Math.Max(0.01, keys[2].At - keys[1].At), 0, 1);
                p.Bob += 3 * strain;
                p.Shake += Math.Sin(p.Time * 55) * 1.3 * strain;
                angle += Math.Sin(p.Time * 40) * 3 * strain;
            }
        }
        // Cheering twirls the ball round the raised handle like a lasso; dazed, it just hangs.
        if (pet.BattleCheer) { angle = -5 + Math.Sin(p.Time * 6) * 6; gripX = 84; gripY = 68; p.Bob -= Math.Abs(Math.Sin(p.Time * 6)) * 4; yarnAngle = p.Time * 540; }
        if (pet.BattleDazed) { angle = 115; gripY = 97; p.Dizzy = 0.7; yarnAngle = 180; }
        if (pet.BattleHurt > 0 && !p.PickedUp) p.Shake += Math.Sin(p.Time * 45) * 2.2 * Math.Min(1, pet.BattleHurt / 0.35);
        if (p.PickedUp)
        {
            // Dangling forward from the paw, inside the window margin at any swing angle.
            double blend = p.PickupBlend;
            double stroke = swing >= 0 ? Math.Sin(Math.PI * Math.Clamp(swing, 0, 1)) : 0;
            double heldAngle = 100 + (Math.Clamp(angle, -75, 115) - 100) * stroke;
            angle += (heldAngle - angle) * blend; gripX += (72 - gripX) * blend; gripY += (92 - 12 * stroke - gripY) * blend;
            // Held, the strand trails the paw's flick: behind it on the way out, flung past it on the way back.
            double heldYarn = swing >= 0 ? angle + 50 * Math.Cos(Math.PI * Math.Clamp(swing, 0, 1)) * stroke + 40 * stroke : 180 + sway;
            yarnAngle = heldYarn + Math.IEEERemainder(yarnAngle - heldYarn, 360) * (1 - blend);
        }
        p.SwordAngle = angle; p.GripX = gripX; p.GripY = gripY; p.YarnAngle = yarnAngle;
    }

    private static readonly Brush SwordWood = PetUi.Hex("#E8C48A");
    private static readonly Brush SwordHandle = PetUi.Hex("#8A5A3B");
    private static readonly Pen SwordPen = FrozenPen(EffectInk, 1.8, PenLineJoin.Round);
    private static readonly Pen SwordGrainPen = FrozenPen(PetUi.Hex("#C79A5B"), 1.2);
    private static readonly Pen SwooshPen = FrozenPen(PetUi.Hex("#B0FFFFFF"), 5, PenLineJoin.Round, true);
    private static readonly Pen ComboSwooshPen = FrozenPen(PetUi.Hex("#E0FFD23F"), 8, PenLineJoin.Round, true);
    private static readonly Geometry SwordBlade = CachedGeometry("M -3.2,-8 L 3.2,-8 L 3.2,-33 L 0,-38.5 L -3.2,-33 Z");
    private static readonly Geometry SwordGuard = CachedGeometry("M -8,-9 L 8,-9 L 8,-4.5 L -8,-4.5 Z");
    private static readonly Geometry SwordGrip = CachedGeometry("M -2.2,-4.5 L 2.2,-4.5 L 2.2,6 L -2.2,6 Z");
    // The toy wooden hammer: a long handle and a mallet head (same grip, so the same poses).
    private static readonly Geometry HammerHandle = CachedGeometry("M -2.2,-29 L 2.2,-29 L 2.2,6 L -2.2,6 Z");
    private static readonly Geometry HammerHead = CachedGeometry("M -9,-39 L 9,-39 L 9,-27 L -9,-27 Z");
    private static readonly Pen HammerSwooshPen = FrozenPen(PetUi.Hex("#B0FFFFFF"), 7, PenLineJoin.Round, true);
    // 1.5 M08: the yarn (a wooden handle, a soft strand of wool and its wound ball) and the boxing glove.
    private static readonly Brush YarnWool = PetUi.Hex("#F28AB2"), YarnShine = PetUi.Hex("#B0FFFFFF"), GloveRed = PetUi.Hex("#E2483C"), GloveCuff = PetUi.Hex("#FFF3E2");
    private static readonly Pen YarnStrandPen = FrozenPen(PetUi.Hex("#D9608F"), 2.2, PenLineJoin.Round, true);
    private static readonly Pen YarnLinePen = FrozenPen(PetUi.Hex("#C04F7C"), 1.2, PenLineJoin.Round, true);
    private static readonly Pen YarnSwooshPen = FrozenPen(PetUi.Hex("#B0FFC8DC"), 4, PenLineJoin.Round, true);
    private static readonly Pen YarnCrackPen = FrozenPen(PetUi.Hex("#FFFFF3E2"), 2, PenLineJoin.Round, true);
    private static readonly Geometry YarnHandle = CachedGeometry("M -2.2,-12 L 2.2,-12 L 2.2,6 L -2.2,6 Z");
    private const double YarnHandleLength = 12, YarnStrandLength = 27, YarnBallRadius = 8.5;
    private static Point Along(Point from, double degrees, double length) =>
        new(from.X + Math.Sin(degrees * Math.PI / 180) * length, from.Y - Math.Cos(degrees * Math.PI / 180) * length);
    private static readonly Geometry GloveBody = CachedGeometry("M -7,-3 C -11,-9 -10,-20 -2,-22 C 6,-24 11,-17 9,-9 C 8,-5 6,-3 5,-3 Z");
    private static readonly Geometry GloveThumb = CachedGeometry("M -7,-6 C -12,-8 -12,-14 -8,-15");
    private static readonly Geometry GloveBand = CachedGeometry("M -7,-3 L 5,-3 L 5,4 L -7,4 Z");

    // Battle Mode skills on the cat. Shield: a soap bubble around it. Cheer: golden sparkles rising.
    private static readonly Brush BubbleFill = PetUi.Hex("#3062D6FF");
    private static readonly Pen BubbleRim = FrozenPen(PetUi.Hex("#D0FFFFFF"), 2.5);
    private static readonly Pen BubbleEdge = FrozenPen(PetUi.Hex("#9062B8E8"), 1.5);
    private static readonly Pen BubbleShine = FrozenPen(PetUi.Hex("#E0FFFFFF"), 3, PenLineJoin.Round, true);
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
    // 2026-11-06, the Zip Slime's debuffs on the cat: outlines round it, clear of its face.
    private static readonly Pen ZipPen = FrozenZipPen(0x8C, 0x6A, 0x2E, 3.2), ZipGold = FrozenZipPen(0xFF, 0xC8, 0x3A, 3), ZipShellEdge = FrozenZipPen(0xFF, 0xC8, 0x3A, 2.2), ZipInk = FrozenZipPen(0x4A, 0x36, 0x1C, 1.4);
    private static readonly SolidColorBrush ZipShellFill = FrozenZipBrush(46, 0xFF, 0xE0, 0x90), ZipLockBody = FrozenZipBrush(255, 0xFF, 0xC8, 0x3A), ZipPipEmpty = FrozenZipBrush(255, 0xFF, 0xFB, 0xF2), ZipPaw = FrozenZipBrush(255, 0x5B, 0x8D, 0xEF);
    private static Pen FrozenZipPen(byte r, byte g, byte b, double width) { var pen = new Pen(FrozenZipBrush(255, r, g, b), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; pen.Freeze(); return pen; }
    private static SolidColorBrush FrozenZipBrush(byte a, byte r, byte g, byte b) { var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b)); brush.Freeze(); return brush; }

    private static void DrawZipSqueeze(DrawingContext dc, PetModel pet)
    {
        double inset = 26 * pet.BattleZipLook + 4, top = 42, bottom = 110;
        foreach (int side in new[] { -1, 1 })
        {
            double x = 56 + side * (56 - inset);
            dc.DrawLine(ZipPen, new Point(x, top), new Point(x, bottom));
            dc.DrawLine(ZipPen, new Point(x, top), new Point(x - side * 10, top));
            dc.DrawLine(ZipPen, new Point(x, bottom), new Point(x - side * 10, bottom));
            for (int i = 0; i < 3; i++)
                dc.DrawLine(ZipInk, new Point(x + side * 6, 58 + i * 18), new Point(x + side * 16, 58 + i * 18));
        }
        var text = new FormattedText(pet.BattleZipText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Black"), 13, Brushes.White, 1.25);
        var pen = new Pen(ZipPen.Brush, 3) { LineJoin = PenLineJoin.Round };
        var shape = text.BuildGeometry(new Point(56 - text.Width / 2, 20));
        dc.DrawGeometry(null, pen, shape);
        dc.DrawGeometry(Brushes.White, null, shape);
    }

    private void DrawZipShell(DrawingContext dc, PetModel pet)
    {
        double shut = pet.BattleZipLook;
        var box = new Rect(8, 24, 96, 88);
        dc.DrawRoundedRectangle(ZipShellFill, ZipShellEdge, box, 16, 16);
        // The zipper closes along the foot of the shell, teeth first, tab last.
        double end = box.Left + 10 + (box.Width - 20) * shut;
        dc.DrawLine(ZipPen, new Point(box.Left + 10, box.Bottom), new Point(end, box.Bottom));
        for (double x = box.Left + 12; x < end; x += 7) dc.DrawLine(ZipInk, new Point(x, box.Bottom - 4), new Point(x, box.Bottom + 3));
        dc.DrawRoundedRectangle(ZipLockBody, ZipInk, new Rect(end - 4, box.Bottom - 7, 8, 14), 2, 2);
        dc.DrawLine(ZipPen, new Point(14, 83), new Point(98, 83));
        dc.DrawRoundedRectangle(null, ZipPen, new Rect(44, 48, 24, 28), 12, 12);
        double shake = pet.BattleZipHitFlash > 0 ? 2 * Math.Sin(pet.BattleZipHitFlash * 120) : 0;
        dc.PushTransform(new TranslateTransform(shake, 0));
        dc.DrawRoundedRectangle(pet.BattleZipHitFlash > 0.08 ? ZipPipEmpty : ZipLockBody, ZipInk, new Rect(36, 68, 40, 31), 5, 5);
        dc.DrawEllipse(ZipInk.Brush, null, new Point(56, 80), 4, 4);
        dc.DrawRectangle(ZipInk.Brush, null, new Rect(54, 80, 4, 10));
        for (int i = 0; i < pet.BattleZipSwings; i++)
        {
            double x = 39 + i * 7;
            dc.DrawLine(ZipInk, new Point(x, 94), new Point(x + 3, 89));
            dc.DrawLine(ZipInk, new Point(x + 3, 89), new Point(x + 5, 93));
        }
        dc.Pop();
        // The weapon turns inward to the cat's own lock; its usual enemy-facing drawing is hidden.
        double progress = pet.BattleZipStrike, hitAt = Math.Clamp(pet.SwordHitAt, 0.2, 0.85);
        double strike = progress < 0 ? 0 : progress < hitAt ? Math.Pow(progress / hitAt, 2) : 1 - 0.6 * (progress - hitAt) / (1 - hitAt);
        dc.PushTransform(new TranslateTransform(82, 104));
        dc.PushTransform(new RotateTransform(20 - 75 * strike));
        dc.PushTransform(new ScaleTransform(0.8, 0.8));
        DrawWeaponShape(dc, pet.WeaponStyle);
        dc.Pop(); dc.Pop(); dc.Pop();
        Oval(dc, Fur, 82, 104, 5, 5);
        // The padlock over its head, and each way out shown apart: gold clicks, blue swings.
        dc.DrawRoundedRectangle(null, ZipPen, new Rect(49, 3, 14, 15), 6, 6);
        dc.DrawRoundedRectangle(ZipLockBody, ZipInk, new Rect(44, 11, 24, 16), 3, 3);
        dc.DrawEllipse(ZipInk.Brush, null, new Point(56, 19), 2.4, 2.4);
        for (int i = 0; i < pet.BattleZipClicksAll; i++)
            dc.DrawEllipse(i < pet.BattleZipClicks ? ZipLockBody : ZipPipEmpty, ZipInk, new Point(38 - i * 12, 18), 4.5, 4.5);
        for (int i = 0; i < pet.BattleZipSwingsAll; i++)
            dc.DrawRectangle(i < pet.BattleZipSwings ? ZipPaw : ZipPipEmpty, ZipInk, new Rect(73 + i * 9, 14, 7, 8));
    }

    public static void DrawWeaponIcon(DrawingContext dc, string style)
    {
        dc.PushTransform(new RotateTransform(35));
        DrawWeaponShape(dc, style);
        dc.Pop();
    }

    private static void DrawWeaponShape(DrawingContext dc, string style)
    {
        if (style == "yarn") DrawYarn(dc, 0, 40); // At rest on a card: the strand bending forward.
        else if (style == "glove")
        {
            dc.DrawGeometry(GloveRed, SwordPen, GloveBody);
            dc.DrawGeometry(null, SwordPen, GloveThumb);
            dc.DrawGeometry(GloveCuff, SwordPen, GloveBand);
        }
        else if (style == "hammer")
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

    // The yarn around its grip at (0, 0): the handle at handleAngle, the strand leaving its tip and
    // curving round to the ball at ballAngle from there (both in degrees from upright, toward the front).
    private static void DrawYarn(DrawingContext dc, double handleAngle, double ballAngle)
    {
        dc.PushTransform(new RotateTransform(handleAngle));
        dc.DrawGeometry(SwordHandle, SwordPen, YarnHandle);
        dc.DrawEllipse(SwordHandle, SwordPen, new Point(0, -YarnHandleLength), 2.8, 2.8); // The knob the strand is tied to.
        dc.Pop();
        Point tip = Along(new Point(0, 0), handleAngle, YarnHandleLength), ball = Along(tip, ballAngle, YarnStrandLength);
        // Leaving the knob along the handle and reaching the ball along its own direction: the more the
        // ball trails the handle, the more the strand bends, like a whip.
        var strand = new StreamGeometry();
        using (var context = strand.Open())
        {
            context.BeginFigure(tip, false, false);
            context.BezierTo(Along(tip, handleAngle, 10), Along(ball, ballAngle + 180, 12), ball, true, true);
        }
        strand.Freeze();
        dc.DrawGeometry(null, YarnStrandPen, strand);
        // The ball: wound strands across it (turned with the strand, so it rolls as it swings) and a shine.
        double r = YarnBallRadius;
        dc.DrawEllipse(YarnWool, SwordPen, ball, r, r);
        dc.PushClip(new EllipseGeometry(ball, r - 0.8, r - 0.8));
        dc.PushTransform(new RotateTransform(ballAngle, ball.X, ball.Y));
        for (int i = -1; i <= 1; i++)
            dc.DrawEllipse(null, YarnLinePen, new Point(ball.X + i * r * 0.55, ball.Y), r * 0.4, r * 1.1);
        dc.DrawLine(YarnLinePen, new Point(ball.X - r, ball.Y - r * 0.4), new Point(ball.X + r, ball.Y + r * 0.1));
        dc.DrawLine(YarnLinePen, new Point(ball.X - r, ball.Y + r * 0.3), new Point(ball.X + r, ball.Y + r * 0.8));
        dc.Pop(); dc.Pop();
        dc.DrawEllipse(YarnShine, null, new Point(ball.X - r * 0.4, ball.Y - r * 0.45), r * 0.24, r * 0.15);
    }

    // A toy wooden sword (or hammer) held in the front paw (inside the facing flip, so it turns with the cat).
    private void DrawSword(DrawingContext dc, in Pose p)
    {
        if (pet.BattleZip == (int)ZipDebuff.Locked) return;
        double swing = pet.SwordSwing, hitAt = Math.Clamp(pet.SwordHitAt, 0.2, 0.85);
        bool yarn = pet.WeaponStyle == "yarn";
        if (yarn && swing > hitAt - 0.04 && swing < hitAt + 0.26)
        {
            // The whip's lash: a swoosh along the ball's path round the cat, and a crack where it snaps.
            Point grip = new(p.GripX, p.GripY), tip = Along(grip, p.SwordAngle, YarnHandleLength), ball = Along(tip, p.YarnAngle, YarnStrandLength);
            double reach = (ball - grip).Length, end = Math.Clamp(Math.Atan2(ball.X - grip.X, grip.Y - ball.Y) * 180 / Math.PI, 25, 165);
            if (end > 45)
            {
                var arc = new StreamGeometry();
                using (var context = arc.Open())
                {
                    context.BeginFigure(Along(grip, 25, reach), false, false);
                    context.ArcTo(Along(grip, end, reach), new Size(reach, reach), 0, false, SweepDirection.Clockwise, true, false);
                }
                dc.PushOpacity(Math.Clamp(1 - Math.Abs(swing - (hitAt + 0.04)) / 0.22, 0, 1));
                dc.DrawGeometry(null, pet.SwordCombo ? ComboSwooshPen : YarnSwooshPen, arc);
                dc.Pop();
            }
            double crack = 1 - Math.Abs(swing - (hitAt + 0.02)) / 0.045;
            if (crack > 0)
            {
                dc.PushOpacity(crack);
                for (int i = 0; i < 5; i++)
                {
                    double a = p.YarnAngle - 70 + i * 35;
                    dc.DrawLine(YarnCrackPen, Along(ball, a, YarnBallRadius + 3), Along(ball, a, YarnBallRadius + 3 + 6 * crack));
                }
                dc.Pop();
            }
        }
        // A punch has no slash arc (the glove's jab reads by itself).
        else if (!yarn && pet.WeaponStyle != "glove" && swing > hitAt - 0.08 && swing < hitAt + 0.26)
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
        // The glove is worn: the paw goes in first, the glove over it.
        bool glove = pet.WeaponStyle == "glove";
        if (glove) Oval(dc, Fur, p.GripX, p.GripY, 5.5, 5);
        dc.PushTransform(new TranslateTransform(p.GripX, p.GripY));
        if (yarn) DrawYarn(dc, p.SwordAngle, p.YarnAngle);
        else
        {
            dc.PushTransform(new RotateTransform(p.SwordAngle));
            DrawWeaponShape(dc, pet.WeaponStyle);
            dc.Pop();
        }
        dc.Pop();
        if (!glove) Oval(dc, Fur, p.GripX, p.GripY, 5.5, 5); // The paw around the grip.
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
        // So does the yarn's ball while it is flung up over the head (the whip's wind-up).
        bool swordBehind = p.Sword && (p.SwordAngle < 15 || pet.WeaponStyle == "yarn" && Math.Cos(p.YarnAngle * Math.PI / 180) > 0.2);
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
                    new Point(92, 62 + (pet.Motion.Ready ? pet.Motion.TailLag * p.Influence * pet.Motion.Facing : 0) + Math.Sin(p.Time * (3 - 2 * (p.Sleeping ? 1 : p.SleepyBlend))) * (5 - 4 * (p.Sleeping ? 1 : p.SleepyBlend)) + TailSwish(p) - 16 * p.Jolt), true, false);
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
        Oval(dc, palette.Paw, 40 + p.Stride - 5 * p.Jolt, p.PickedUp ? 104 + p.PickupBlend * 2 : p.Airborne ? 95 : 104 - Math.Max(0, p.Stride), 11, 5);
        Oval(dc, palette.Paw, 70 - p.Stride + 5 * p.Jolt, p.PickedUp ? 104 + p.PickupBlend * 2 : p.Airborne ? 95 : 104 + Math.Min(0, p.Stride), 11, 5);
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
        double headY = 49 + 10 * p.RestBlend + (p.Emotion == PetEmotion.Sad ? 2 * p.EmotionStrength : 0) + p.SneezeLift + (p.PickedUp || p.Sleeping ? 0 : pet.ReactDip);
        dc.PushTransform(new TranslateTransform(glanceX * (p.Eating ? 1.4 : 0.45), headY - 49 + glanceY * 0.6 +
            (p.Eating && p.EatTime > 1.3 ? Math.Sin(p.EatTime * 16) * 1.2 : 0)));
        dc.PushTransform(new RotateTransform(p.PickedUp ? Math.Sin(pet.Pickup.Time * 10) * p.Dizzy * 3 + (pet.Motion.Ready ? pet.Motion.HeadTilt * p.Influence * pet.Motion.Facing : 0) : glanceDirection * glanceBlend * 2.5 + (pet.Motion.Ready ? pet.Motion.HeadTilt * p.Influence * pet.Motion.Facing : 0) + p.PettingLean * 7 + (p.Seeking ? Math.Sin(Math.PI * Math.Clamp(pet.ActionTime / PetSettings.AttentionGestureDuration, 0, 1)) * 6 : 0) + p.SneezeTilt + pet.ReactTilt, 55, 49));
        DrawAccessory(dc, AccessoryLayer.Neck);
        dc.PushTransform(new RotateTransform(twitch - 28 * p.AngryBlend - 24 * p.Jolt, 37, 33)); // Angry or a hard landing: ears flatten.
        Triangle(dc, new Point(27, 38), new Point(27, 13), new Point(47, 31), palette.Ear);
        Triangle(dc, new Point(31, 30), new Point(31, 21), new Point(40, 30), Pink);
        dc.Pop();
        dc.PushTransform(new RotateTransform(-twitch * 0.6 + 28 * p.AngryBlend + 24 * p.Jolt, 75, 33));
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
        // Fetch (PetWindow.ToyFetch.cs): the yarn ball held in the mouth, a little ahead (the facing flip carries it).
        if (CarryBall && !p.PickedUp) YarnBallArt.Draw(dc, new Point(56 + PetWindow.FetchMouthAhead, 68), 8.5);
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
            else if (p.Wince)
            {
                // Squeezed shut, "> <": each eye's point toward the nose.
                double toward = eyeX < 55 ? 1 : -1;
                dc.DrawLine(Outline, new Point(eyeX - 3.5 * toward, 43), new Point(eyeX + 3.5 * toward, 46.5));
                dc.DrawLine(Outline, new Point(eyeX + 3.5 * toward, 46.5), new Point(eyeX - 3.5 * toward, 50));
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
                        ? FrozenPen(PetUi.Hex(shape.Stroke.Color!), shape.Stroke.Width, shape.Stroke.RoundJoin ? PenLineJoin.Round : PenLineJoin.Miter, shape.Stroke.Round)
                        : null;
                    return shape.Kind switch
                    {
                        ShapeKind.Path => new DrawnShape(shape.Kind, CachedGeometry(shape.Path!), default, default, 0, 0,
                            shape.Fill == null ? null : PetUi.Hex(shape.Fill), shape.Stroke.Kind, pen),
                        ShapeKind.Ellipse => new DrawnShape(shape.Kind, null, new Point(v[0], v[1]), default, v[2], v[3],
                            shape.Fill == null ? null : PetUi.Hex(shape.Fill), shape.Stroke.Kind, pen),
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
    private void DrawAccessory(DrawingContext dc, AccessoryLayer layer) => DrawAccessory(dc, accessory, layer, palette);

    // Also used by PetScene's banner cat, which maps this head's coordinates onto its own.
    internal static void DrawAccessory(DrawingContext dc, string accessory, AccessoryLayer layer, CoatPalette palette)
    {
        if (!Accessories.TryGetValue(accessory, out var item) || item.Layer != layer) return;
        foreach (var shape in item.Shapes)
        {
            var pen = shape.Stroke switch { StrokeKind.Outline => palette.Outline, StrokeKind.Accent => palette.AccentOutline, _ => shape.Pen };
            switch (shape.Kind)
            {
                case ShapeKind.Path: dc.DrawGeometry(shape.Fill, pen, shape.Geometry); break;
                case ShapeKind.Ellipse: dc.DrawEllipse(shape.Fill, pen, shape.A, shape.RadiusX, shape.RadiusY); break;
                default: dc.DrawLine(pen, shape.A, shape.B); break;
            }
        }
    }

    // Drawn inside the pickup rotation, above the head.
    private static readonly Brush HungWhite = Freeze(new SolidColorBrush(Color.FromArgb(150, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush HungBar = Freeze(new SolidColorBrush(Color.FromArgb(235, 0xF4, 0xF4, 0xF4)));
    private static readonly Pen HungEdge = FreezePen(new Pen(new SolidColorBrush(Color.FromArgb(200, 0x6A, 0x6A, 0x6A)), 1));
    private static readonly Pen HungSpin = FreezePen(new Pen(new SolidColorBrush(Color.FromRgb(0x3E, 0x7F, 0xB0)), 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
    private static readonly Typeface HungFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static Brush Freeze(Brush b) { b.Freeze(); return b; }
    private static Pen FreezePen(Pen p) { p.Freeze(); return p; }

    // An infected End Task (2026-11-04): the cat greyed out like a hung app, a title bar saying it is not
    // responding and a spinning wait cursor.
    private void DrawNotResponding(DrawingContext dc, double time)
    {
        dc.DrawRoundedRectangle(HungWhite, HungEdge, new Rect(6, 16, 100, 94), 6, 6);
        dc.DrawRectangle(HungBar, HungEdge, new Rect(6, 16, 100, 14));
        var text = new FormattedText(pet.BattleNotRespondingText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, HungFace, 8, Brushes.Black, 1);
        text.MaxTextWidth = 96; text.MaxLineCount = 1; text.Trimming = TextTrimming.CharacterEllipsis;
        dc.DrawText(text, new Point(56 - Math.Min(96, text.Width) / 2, 17.5));
        var c = new Point(56, 66);
        double a = time * 6;
        var arc = new StreamGeometry();
        using (var g = arc.Open())
        {
            g.BeginFigure(new Point(c.X + 9 * Math.Cos(a), c.Y + 9 * Math.Sin(a)), false, false);
            g.ArcTo(new Point(c.X + 9 * Math.Cos(a + 4.4), c.Y + 9 * Math.Sin(a + 4.4)), new Size(9, 9), 0, true, SweepDirection.Clockwise, true, false);
        }
        arc.Freeze();
        dc.DrawGeometry(null, HungSpin, arc);
    }

    private static readonly Brush NoteFill = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0x6A, 0x3A)));
    private static readonly Pen NoteStem = FreezePen(new Pen(NoteFill, 1.8));

    // Off the beat (a big drum, 2026-11-04): three notes going round the cat's head, out of step.
    private static void DrawOffBeat(DrawingContext dc, double time)
    {
        for (int i = 0; i < 3; i++)
        {
            double a = time * 3.2 + i * 2.1, x = 56 + Math.Cos(a) * 34, y = 12 + Math.Sin(a * 1.7) * 7;
            dc.DrawEllipse(NoteFill, null, new Point(x, y), 3.2, 2.4);
            dc.DrawLine(NoteStem, new Point(x + 3, y), new Point(x + 3, y - 9));
            dc.DrawLine(NoteStem, new Point(x + 3, y - 9), new Point(x + 7, y - 7));
        }
    }

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
        if (pet.BatRemaining > 0 && !p.PickedUp && !p.Sleeping && !p.Airborne && !p.Waking && p.FarmTool is FarmToolPose.None or FarmToolPose.Pounce or FarmToolPose.Carry or FarmToolPose.Drop)
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
        if (p.CastBlend > 0) DrawPortalSpell(dc, p);
        if (p.WaterBlend > 0) DrawWateringCan(dc, p);
        if (p.StirBlend > 0) DrawStirStick(dc, p);
        if (p.FishBlend > 0) DrawFishingRod(dc, p);
        if (p.WaterBlend > 0) DrawWorkSweat(dc, p, p.WaterTime, p.WaterBlend);
        if (p.StirBlend > 0) DrawWorkSweat(dc, p, p.StirTime, p.StirBlend);
        if (p.FishBlend > 0) DrawFishingMoments(dc, p);
        if (p.FarmToolBlend > 0) DrawFarmTool(dc, p);
        if (pet.Thought.Visible && !p.PickedUp && !p.Sleeping && !p.Airborne) DrawThoughtBubble(dc);
    }

    private static readonly Geometry SpellStar = PetSpellStar();
    private static Geometry PetSpellStar()
    {
        var star = Geometry.Parse("M0,-6.5 L1.8,-1.8 L6.5,0 L1.8,1.8 L0,6.5 L-1.8,1.8 L-6.5,0 L-1.8,-1.8 Z");
        star.Freeze();
        return star;
    }

    // Both front paws raised and waving, with a ring of stars turning over the head (never across the face).
    private void DrawPortalSpell(DrawingContext dc, in Pose p)
    {
        double t = p.CastTime, wave = Math.Sin(t * 8) * 3, lift = 14 * p.CastBlend;
        dc.PushOpacity(p.CastBlend);
        Oval(dc, Fur, 22 - wave, 84 - lift, 6.5, 9.5);
        Oval(dc, Fur, 90 + wave, 84 - lift, 6.5, 9.5);
        for (int i = 0; i < 6; i++)
        {
            double angle = t * 3.2 + i * Math.PI / 3, scale = 0.75 + 0.35 * Math.Sin(t * 7 + i * 1.9);
            dc.PushTransform(new TranslateTransform(56 + Math.Cos(angle) * 58, 8 + Math.Sin(angle) * 13));
            dc.PushTransform(new ScaleTransform(scale, scale));
            DrawOutlinedEffect(dc, SpellStar, i % 2 == 0 ? PetUi.Game.Gold : PetUi.Lilac);
            dc.Pop(); dc.Pop();
        }
        dc.Pop();
    }

    private static readonly Pen StirInk = PetUi.FrozenPen(PetUi.ArtInk, 5.6), StirWood = PetUi.FrozenPen(PetUi.Hex("#B4815E"), 3);

    // A wooden stick held in the front paw, its end going round in the pot beside the cat.
    private void DrawStirStick(DrawingContext dc, in Pose p)
    {
        dc.PushTransform(new ScaleTransform(p.FacingScale, 1, 56, 56));
        dc.PushOpacity(p.StirBlend);
        double turn = p.StirTime * 3.6;
        var paw = new Point(86 + Math.Cos(turn) * 3, 50 + Math.Sin(turn) * 1.2);
        var end = new Point(113 + Math.Cos(turn) * 8, 58 + Math.Sin(turn) * 1.5);
        dc.DrawLine(StirInk, paw, end);
        dc.DrawLine(StirWood, paw, end);
        Oval(dc, Fur, paw.X, paw.Y, 7, 6.5);
        dc.Pop(); dc.Pop();
    }

    private static readonly Pen FishLine = PetUi.FrozenPen(PetUi.ArtInk, 1);
    private static readonly Brush FloatRed = PetUi.Hex("#E2503F"), FloatWhite = PetUi.Hex("#FFF3EC");

    // The rod in the front paw, its line down to a float on the pond beside the cat. The float dips
    // at a bite and the rod comes up as the cat reels in.
    private void DrawFishingRod(DrawingContext dc, in Pose p)
    {
        const double castSeconds = 0.8, reelSeconds = 0.8;
        dc.PushTransform(new ScaleTransform(p.FacingScale, 1, 56, 56));
        dc.PushOpacity(p.FishBlend);
        double cast = Math.Clamp(p.FishTime / castSeconds, 0, 1), sinceBite = p.FishTime - p.FishBiteAt;
        double reel = sinceBite > 0 ? Math.Clamp(sinceBite / reelSeconds, 0, 1) : 0, jerk = sinceBite > 0 ? Math.Sin(sinceBite * 34) * (1 - reel) : 0;
        var paw = new Point(86, 72);
        var tip = new Point(128 - reel * 12, 42 - reel * 16 + jerk * 3);
        double water = 94 + Math.Sin(p.FishTime * 2.6) * 1 + (sinceBite > 0 ? Math.Max(0, jerk) * 5 : 0);
        double floatX = tip.X + (140 - tip.X) * cast * (1 - reel), floatY = tip.Y + 8 + (water - tip.Y - 8) * cast * (1 - reel);
        dc.DrawLine(StirInk, paw, tip);
        dc.DrawLine(StirWood, paw, tip);
        dc.DrawLine(FishLine, tip, new Point(floatX, floatY));
        dc.DrawEllipse(FloatRed, CanInkThin, new Point(floatX, floatY - 2), 3.4, 3.4);
        dc.DrawEllipse(FloatWhite, null, new Point(floatX, floatY - 3.8), 2.1, 1.3);
        Oval(dc, Fur, paw.X, paw.Y, 7, 6.5);
        dc.Pop(); dc.Pop();
    }

    private static Geometry FrozenPath(string text) { var path = Geometry.Parse(text); path.Freeze(); return path; }
    private static readonly Geometry CanSpout = FrozenPath("M9,3 L25,-10 L29,-6 L10,10 Z");
    private static readonly Geometry CanHandle = FrozenPath("M-11,-7 C-23,-13 -23,8 -11,4");
    private static readonly Geometry CanDrop = FrozenPath("M0,-3.4 C1.4,-1.5 2.4,-0.4 2.4,1 C2.4,2.4 1.3,3.2 0,3.2 C-1.3,3.2 -2.4,2.4 -2.4,1 C-2.4,-0.4 -1.4,-1.5 0,-3.4 Z");
    private static readonly Brush CanDropFill = PetUi.Hex("#6FC0EE");
    private static readonly Pen CanInk = PetUi.FrozenPen(PetUi.ArtInk, 2), CanInkThin = PetUi.FrozenPen(PetUi.ArtInk, 1.2);

    // A small watering can held out in front, tipping forward, with drops falling from its rose.
    private void DrawWateringCan(DrawingContext dc, in Pose p)
    {
        dc.PushTransform(new ScaleTransform(p.FacingScale, 1, 56, 56));
        dc.PushOpacity(p.WaterBlend);
        double tip = 20 * p.WaterBlend + Math.Sin(p.WaterTime * 7) * 2.5;
        dc.PushTransform(new TranslateTransform(92, 74));
        dc.PushTransform(new RotateTransform(tip));
        dc.DrawGeometry(null, CanInk, CanHandle);
        var body = ToolShade(p.ToolTier, PetUi.Mint);
        dc.DrawGeometry(body, CanInk, CanSpout);
        dc.DrawRoundedRectangle(body, CanInk, new Rect(-12, -11, 23, 22), 4, 4);
        dc.DrawEllipse(PetUi.Game.Gold, CanInkThin, new Point(27.5, -8.5), 3.4, 5.6);
        dc.Pop(); dc.Pop();
        Oval(dc, Fur, 80, 77, 7, 6.5);
        if (p.WaterBlend > 0.6)
        {
            for (int i = 0; i < 5; i++)
            {
                double fall = (p.WaterTime * 2.1 + i * 0.2) % 1;
                dc.PushTransform(new TranslateTransform(121 + (i * 3 % 5 - 2) * 2.6, 81 + fall * 30)); // Under the rose, scattered.
                dc.DrawGeometry(CanDropFill, CanInkThin, CanDrop);
                dc.Pop();
            }
        }
        dc.Pop(); dc.Pop();
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
        if (p.Dish is FarmDish dish)
        {
            dc.PushTransform(new MatrixTransform(0.8, 0, 0, 0.8, 0, 9));
            PetFarmArt.DrawDish(dc, dish);
            PetFarmArt.DrawSteam(dc, eatTime);
            dc.Pop();
        }
        else
        {
            var fish = FishGeometry;
            DrawOutlinedEffect(dc, fish, Brushes.Salmon);
            dc.DrawEllipse(Ink, null, new Point(-6, -1), 1.4, 1.4);
            dc.DrawLine(Outline, new Point(0, -3), new Point(0, 3));
        }
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
