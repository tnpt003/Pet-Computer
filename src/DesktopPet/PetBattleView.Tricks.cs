using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M13 (PetBattle.Tricks.cs): the four slimes the cursor beats. The ghost: pale, an "o" mouth and a
// wisp on its head; faded out it is barely there (a faint wobbling outline still shows where it is), and
// brought out by the cursor or the laser it glows softly. The bubble slime floats in a pale blue bubble with
// a shine, one crack for every click on it. The mirage slime: a violet gem on its brow; its copies look
// just the same and no copy or real one has a shadow until the cursor shows them: a copy blurs, the real
// one gets a dark shadow. The sulky slime: knitted brows, a pout and pink cheeks, puffed up as it is hit,
// a pink bar of how soothed it is, and soothed it waves goodbye walking off. Drawing only.
public sealed partial class BattleFieldView
{
    private static readonly Brush GhostGlow = Frozen(120, 0xFF, 0xF4, 0xB0);
    private static readonly Pen WispPen = FrozenPen(Frozen(255, 0x9A, 0xA8, 0xD0), 2.2);
    private static readonly Pen FadedEdge = FrozenPen(Frozen(150, 0xFF, 0xFF, 0xFF), 1.6, new DashStyle(new double[] { 2, 2 }, 0));
    private static readonly Brush BubbleFill = Frozen(46, 0xB8, 0xE8, 0xFF);
    private static readonly Pen BubbleEdge = FrozenPen(Frozen(235, 0x6C, 0xC4, 0xEE), 2.4);
    private static readonly Pen BubbleGleam = FrozenPen(Frozen(230, 255, 255, 255), 2.4);
    private static readonly Pen BubbleCrack = FrozenPen(Frozen(255, 0x2E, 0x6E, 0x96), 1.6);
    private static readonly Brush GemViolet = Frozen(255, 0x9B, 0x6C, 0xF0);
    private static readonly Brush BlurFill = Frozen(90, 0xB8, 0x9C, 0xF0);
    private static readonly Brush CheekPink = Frozen(200, 0xFF, 0x8F, 0xA8);
    private static readonly Brush CalmPink = Frozen(255, 0xFF, 0x8F, 0xB8);
    private static readonly Brush MissGrey = Frozen(255, 0xC8, 0xCC, 0xD6);
    private static readonly Brush HeartRed = Frozen(255, 0xFF, 0x6B, 0x8E);
    private static readonly Pen RipplePen = FrozenPen(Frozen(220, 0xC8, 0xCC, 0xD6), 2);
    private static readonly Geometry Wisp = Parse("M -4,-33 C -6,-40 2,-44 4,-40 C 6,-36 0,-35 2,-31");
    private static readonly Geometry Gem = Parse("M 0,-31 L 4,-27 L 0,-23 L -4,-27 Z");
    private static readonly Geometry Pout = Parse("M -4,-8 Q 0,-11 4,-8");
    private static readonly Geometry Smile = Parse("M -4,-10 Q 0,-6 4,-10");
    private static readonly Geometry Heart = Parse("M 0,4 C -7,-1 -8,-6 -4,-8 C -2,-9 0,-7 0,-5 C 0,-7 2,-9 4,-8 C 8,-6 7,-1 0,4 Z");

    // How a trick slime looks this frame: its shape and see-through (before its body is drawn).
    private static void TrickLook(Slime slime, double time, ref double sx, ref double sy, ref double opacity, ref double jitter)
    {
        if (!slime.Type.UsesMouse) return;
        if (slime.Faded) opacity = 0;
        else if (slime.Type.Ghost != null) opacity *= 0.88;
        if (slime.Type.Sulk != null && slime.State != SlimeState.Defeated) { sx *= slime.Puff; sy *= slime.Puff; }
        // A soothed one leaves whole, waving (no squash of a defeat); a copy that vanished smears sideways.
        if (slime.Calmed && slime.State == SlimeState.Defeated) { sx = 1; sy = 1; }
        if (slime.Dispelled) { sx = 1 + Math.Clamp(slime.StateTime * 3, 0, 1); sy = 1; opacity *= 0.6; }
        if (slime.Decoy && slime.Revealed > 0 && slime.State != SlimeState.Defeated) opacity *= 0.5;
    }

    // A soothed sulky slime walks off as it fades (screen offset).
    private static double TrickDrift(Slime slime) => slime.Calmed && slime.State == SlimeState.Defeated ? slime.Facing * 50 * slime.StateTime : 0;

    // Has a contact shadow: every slime but a faded ghost and a mirage's look-alikes, whose real one shows
    // its shadow only once the cursor gives it away.
    private static bool CastsShadow(Slime slime) => !slime.Faded && (slime.Type.Mirage == null || !slime.Decoy && slime.Revealed > 0);

    // Behind the body (screen coordinates): a ghost's glow while brought out, a copy's blur once seen through.
    private void DrawTrickBehind(DrawingContext dc, Slime slime, double x, double floor, double w, double h, double time)
    {
        if (slime.State == SlimeState.Defeated) return;
        if (slime.Type.Ghost != null && !slime.Faded && slime.Revealed > 0)
        {
            double breath = 1 + 0.06 * Math.Sin(time * 6);
            dc.DrawEllipse(GhostGlow, null, new Point(x, floor - h / 2), (w / 2 + 14) * breath, (h / 2 + 12) * breath);
        }
        if (slime.Decoy && slime.Revealed > 0)
            foreach (int side in new[] { -1, 1 })
                dc.DrawEllipse(BlurFill, null, new Point(x + side * (5 + Math.Sin(time * 9) * 1.5), floor - h / 2), w / 2, h / 2);
    }

    // Its marks (body coordinates).
    private static void DrawTrickMarks(DrawingContext dc, Slime slime, double time)
    {
        switch (slime.Type.Marks)
        {
            case "ghost":
                dc.DrawGeometry(null, Halo, Wisp);
                dc.DrawGeometry(null, WispPen, Wisp);
                dc.DrawEllipse(null, Ink, new Point(slime.Facing * 3, -9), 2.6, 3.2);
                break;
            case "mirage":
                dc.DrawGeometry(GemViolet, ThinInk, Gem);
                break;
            case "sulk":
                double look = slime.Facing * 3;
                if (slime.Calmed)
                {
                    // Happy at last: a smile and a paw waving goodbye.
                    dc.DrawGeometry(null, Ink, Smile);
                    double wave = Math.Sin(time * 14) * 0.5;
                    dc.PushTransform(new RotateTransform(wave * 40, -slime.Facing * 22, -18));
                    dc.DrawEllipse(Brushes.White, Outline, new Point(-slime.Facing * 26, -30), 5, 5);
                    dc.Pop();
                    break;
                }
                dc.DrawLine(Ink, new Point(-11 + look, -26), new Point(-4 + look, -23));
                dc.DrawLine(Ink, new Point(11 + look, -26), new Point(4 + look, -23));
                dc.DrawGeometry(null, Ink, Pout);
                dc.DrawEllipse(CheekPink, null, new Point(-13 + look, -12), 4, 2.6);
                dc.DrawEllipse(CheekPink, null, new Point(13 + look, -12), 4, 2.6);
                break;
        }
    }

    // In front of the body (screen coordinates, floor already raised by the bubble): the bubble itself.
    private void DrawBubbleShell(DrawingContext dc, Slime slime, double x, double floor, double w, double h, double time)
    {
        if (!slime.Bubbled || slime.State == SlimeState.Defeated) return;
        double wobble = 1 + 0.03 * Math.Sin(time * 5 + slime.Id) + (slime.HitFlash > 0 ? 0.06 : 0);
        double rx = (w / 2 + 9) * wobble, ry = (h / 2 + 10) / wobble;
        var centre = new Point(x, floor - h / 2 - 2);
        dc.DrawEllipse(BubbleFill, Halo, centre, rx, ry);
        dc.DrawEllipse(null, BubbleEdge, centre, rx, ry);
        var gleam = new StreamGeometry();
        using (var c = gleam.Open())
        {
            c.BeginFigure(new Point(centre.X - rx * 0.7, centre.Y - ry * 0.3), false, false);
            c.ArcTo(new Point(centre.X - rx * 0.2, centre.Y - ry * 0.85), new Size(rx * 0.8, ry * 0.8), 0, false, SweepDirection.Clockwise, true, false);
        }
        dc.DrawGeometry(null, BubbleGleam, gleam);
        // One crack for each click so far, round its right side.
        for (int i = 0; i < slime.Pokes; i++)
        {
            double a = -0.9 + i * 0.45;
            var on = new Point(centre.X + Math.Cos(a) * rx, centre.Y + Math.Sin(a) * ry);
            var inward = new Point(centre.X + Math.Cos(a) * rx * 0.78, centre.Y + Math.Sin(a) * ry * 0.78);
            dc.DrawLine(BubbleCrack, on, inward);
            dc.DrawLine(BubbleCrack, inward, new Point(inward.X - 3, inward.Y + 3));
        }
    }

    // Over its head: how soothed a sulky slime is (a pink bar), where its HP bar would be.
    private static void DrawCalmBar(DrawingContext dc, Slime slime, double x, double top, double size)
    {
        if (slime.Type.Sulk == null || slime.Calm <= 0 || !slime.Targetable) return;
        double barY = top - 9, barW = 36 * size, fill = barW * Math.Clamp(slime.Calm, 0, 1);
        dc.DrawRoundedRectangle(BarBack, null, new Rect(x - barW / 2 - 1, barY - 1, barW + 2, 7), 3, 3);
        if (fill > 0) dc.DrawRoundedRectangle(CalmPink, null, new Rect(x - barW / 2, barY, fill, 5), 2.5, 2.5);
    }

    // The two M13 effects (screen coordinates, already faded by DrawEffect).
    private void DrawTrickEffect(DrawingContext dc, BattleEffect effect, double x, double y, double k)
    {
        if (effect.Kind == BattleEffectKind.Miss)
        {
            double r = 10 + 14 * k;
            dc.DrawEllipse(null, RipplePen, new Point(x, y), r, r * 0.7);
            bool sulk = effect.Value > 0;
            DrawWord(dc, sulk ? PetText.Pick(Language, "DỖI!", "SULK!") : PetText.Pick(Language, "TRƯỢT", "MISS"), sulk ? CalmPink : MissGrey, x, y - 14 - 20 * k);
            return;
        }
        for (int i = 0; i < 3; i++)
        {
            double hx = x + (i - 1) * 14, hy = y - 6 - 30 * k - (i % 2) * 8;
            dc.PushTransform(new MatrixTransform(1.1, 0, 0, 1.1, hx, hy));
            dc.DrawGeometry(HeartRed, Outline, Heart);
            dc.Pop();
        }
    }
}
