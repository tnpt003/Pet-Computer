using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// Static vector artwork: no timers, input or model mutation. Coordinates are a 64 DIP tile.
public enum PetMotif
{
    Cat, Book, Heart, Leaf, Spark, Trophy, Fish, Ball, Clock,
    // 2026-10-26: icons for settings cards, reward sections, the two battle maps, empty states and dialogs.
    Palette, Bubble, Note, Speaker, Gear, Gift, Cup, Gem, Moon, Sword, Shield, Crown, Campfire, Scroll, Mushroom, Box, Flag, Question, Warning
}
public sealed class PetIllustration : FrameworkElement
{
    private PetMotif motif;
    private Brush color;
    private readonly bool ownColor;
    private static readonly Brush Rose = PetUi.Rose, Mint = PetUi.Mint, Lilac = PetUi.Lilac, Gold = PetUi.Game.Gold;
    private static readonly Brush Wood = PetUi.Hex("#B4815E"), Card = PetUi.Hex("#E8C9A0"), Paper = PetUi.Hex("#FFF3E2");
    private static readonly Pen Outline = PetUi.FrozenPen(PetUi.Ink, 2), Bold = PetUi.FrozenPen(PetUi.Ink, 4.5), QuestionInk = PetUi.FrozenPen(PetUi.Ink, 9);
    public PetIllustration(PetMotif motif, double size = 40, Brush? color = null)
    {
        this.motif = motif; ownColor = color != null; this.color = color ?? DefaultColor(motif);
        Width = Height = size; IsHitTestVisible = false; Focusable = false;
    }
    // The picture can change in place (map nodes change kind as a run goes on).
    public PetMotif Motif
    {
        get => motif;
        set { if (motif == value) return; motif = value; if (!ownColor) color = DefaultColor(value); InvalidateVisual(); }
    }
    // The cream disc behind the picture; off where the picture sits on its own round node.
    public bool Backdrop { get; init; } = true;
    private static Brush DefaultColor(PetMotif motif) => motif switch
    {
        PetMotif.Heart or PetMotif.Gift or PetMotif.Mushroom or PetMotif.Warning => Rose,
        PetMotif.Leaf or PetMotif.Fish or PetMotif.Clock or PetMotif.Cup or PetMotif.Shield or PetMotif.Flag => Mint,
        PetMotif.Book or PetMotif.Ball or PetMotif.Note or PetMotif.Gem or PetMotif.Question => Lilac,
        PetMotif.Spark or PetMotif.Trophy or PetMotif.Moon or PetMotif.Crown => Gold,
        PetMotif.Palette or PetMotif.Box => Card, PetMotif.Bubble or PetMotif.Sword or PetMotif.Scroll => Paper,
        _ => PetUi.AccentBright
    };
    private static Geometry Shape(string path) { var g = Geometry.Parse(path); g.Freeze(); return g; }
    private static readonly Geometry Ears = Shape("M 13,29 L 13,9 L 28,21 M 36,21 L 51,9 L 51,29");
    private static readonly Geometry Heart = Shape("M32,51 C10,38 10,20 22,20 C28,20 31,23 32,27 C36,15 54,18 53,30 C52,39 40,47 32,51 Z");
    private static readonly Geometry Leaf = Shape("M16,49 C5,25 25,13 51,13 C51,39 38,55 16,49 Z M17,47 L41,23");
    private static readonly Geometry Spark = Shape("M32,10 L38,25 L54,31 L38,37 L32,54 L26,37 L10,31 L26,25 Z");
    private static readonly Geometry Fish = Shape("M12,32 C20,20 38,19 46,32 C38,45 20,44 12,32 Z M46,32 L56,23 L55,41 Z");
    private static readonly Geometry YarnLines = Shape("M19,24 C29,30 37,40 40,51 M17,35 C27,36 37,44 44,50 M30,14 C34,24 44,32 51,35");
    private static readonly Geometry Palette = Shape("M32,12 C46,12 54,21 53,32 C52,41 45,42 40,40 C35,38 31,42 34,47 C36,51 34,54 29,54 C17,54 10,44 11,32 C12,20 21,12 32,12 Z");
    private static readonly Geometry Bubble = Shape("M22,14 L42,14 C48,14 52,18 52,24 L52,32 C52,38 48,42 42,42 L31,42 L19,51 L22,42 C16,42 12,38 12,32 L12,24 C12,18 16,14 22,14 Z");
    private static readonly Geometry NoteBeam = Shape("M27,20 L49,15 L49,22 L27,27 Z");
    private static readonly Geometry Speaker = Shape("M13,26 L21,26 L32,16 L32,48 L21,38 L13,38 Z");
    private static readonly Geometry Waves = Shape("M39,25 C42,28 42,36 39,39 M45,19 C51,25 51,39 45,45");
    private static readonly Geometry Gear = GearShape();
    private static readonly Geometry Cup = Shape("M15,25 L45,25 L42,43 C41,48 37,50 32,50 L28,50 C23,50 19,48 18,43 Z");
    private static readonly Geometry Steam = Shape("M25,20 C22,16 28,14 25,10 M35,20 C32,16 38,14 35,10");
    private static readonly Geometry Gem = Shape("M20,17 L44,17 L54,28 L32,53 L10,28 Z");
    private static readonly Geometry Facets = Shape("M10,28 L54,28 M26,28 L32,53 L38,28 M20,17 L26,28 L32,17 L38,28 L44,17");
    private static readonly Geometry Moon = Shape("M37,11 C23,13 15,25 17,38 C19,50 33,56 46,50 C34,48 26,38 27,26 C28,19 32,14 37,11 Z");
    private static readonly Geometry Twinkle = Shape("M47,14 L49,20 L55,22 L49,24 L47,30 L45,24 L39,22 L45,20 Z");
    private static readonly Geometry Blade = Shape("M50,12 L48,20 L28,40 L24,36 L44,16 Z");
    private static readonly Geometry Guard = Shape("M18,36 L22,32 L32,42 L28,46 Z");
    private static readonly Geometry Grip = Shape("M22,40 L24,42 L18,48 L16,46 Z");
    private static readonly Geometry Shield = Shape("M32,11 L50,17 C50,34 44,46 32,53 C20,46 14,34 14,17 Z");
    private static readonly Geometry Crown = Shape("M12,46 L14,22 L24,34 L32,15 L40,34 L50,22 L52,46 Z");
    private static readonly Geometry Logs = Shape("M14,47 L47,40 L49,46 L16,53 Z M50,47 L17,40 L15,46 L48,53 Z");
    private static readonly Geometry Flame = Shape("M32,11 C40,21 46,28 44,36 C43,42 38,45 32,45 C26,45 21,42 20,36 C19,29 26,26 28,19 C30,23 31,25 32,27 C33,21 32,16 32,11 Z");
    private static readonly Geometry Ember = Shape("M32,30 C36,34 38,37 37,40 C36,43 34,44 32,44 C30,44 27,43 27,40 C27,37 30,35 32,30 Z");
    private static readonly Geometry Cap = Shape("M10,34 C10,20 20,12 32,12 C44,12 54,20 54,34 Z");
    private static readonly Geometry Stem = Shape("M24,34 L40,34 L42,49 C42,52 38,54 32,54 C26,54 22,52 22,49 Z");
    private static readonly Geometry BoxFront = Shape("M14,30 L50,30 L48,52 L16,52 Z");
    private static readonly Geometry Flaps = Shape("M14,30 L7,21 L22,18 L28,30 Z M50,30 L57,21 L42,18 L36,30 Z");
    private static readonly Geometry Pennant = Shape("M21,14 C30,10 38,20 48,16 L48,32 C38,36 30,26 21,30 Z");
    private static readonly Geometry QuestionHook = Shape("M24,24 C24,16 29,13 33,13 C39,13 42,17 42,22 C42,28 33,30 33,37");
    private static readonly Geometry Triangle = Shape("M32,11 C34,11 35,12 36,14 L54,47 C55,50 53,52 50,52 L14,52 C11,52 9,50 10,47 L28,14 C29,12 30,11 32,11 Z");
    private static readonly Dictionary<Brush, Pen> ColorPens = new();

    // Eight square teeth round a wheel.
    private static Geometry GearShape()
    {
        var figure = new PathFigure { IsClosed = true };
        for (int i = 0; i < 32; i++)
        {
            double angle = (i - 1.5) * Math.PI / 16, radius = i % 4 < 2 ? 21 : 15;
            var point = new Point(32 + radius * Math.Cos(angle), 32 + radius * Math.Sin(angle));
            if (i == 0) figure.StartPoint = point; else figure.Segments.Add(new LineSegment(point, true));
        }
        var gear = new PathGeometry(new[] { figure });
        gear.Freeze();
        return gear;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.PushTransform(new ScaleTransform(ActualWidth / 64, ActualHeight / 64));
        if (Backdrop) dc.DrawEllipse(PetUi.AccentSoft, null, new Point(32,32),30,30);
        switch (motif)
        {
            case PetMotif.Cat:
                dc.DrawGeometry(color, Outline, Ears);
                dc.DrawEllipse(color, Outline, new Point(32,34),23,19);
                dc.DrawEllipse(PetUi.Surface, null, new Point(32,42),12,9);
                dc.DrawEllipse(PetUi.Ink,null,new Point(23,32),2,3);
                dc.DrawEllipse(PetUi.Ink,null,new Point(41,32),2,3);
                dc.DrawEllipse(PetUi.Ink,null,new Point(32,40),3,2);
                dc.DrawLine(Outline,new Point(32,42),new Point(32,46));
                dc.DrawLine(Outline,new Point(32,46),new Point(27,44));
                dc.DrawLine(Outline,new Point(32,46),new Point(37,44));
                break;
            case PetMotif.Book:
                dc.DrawRoundedRectangle(color,Outline,new Rect(14,13,36,40),5,5);
                dc.DrawRoundedRectangle(PetUi.Surface,null,new Rect(19,17,26,32),2,2);
                dc.DrawLine(Outline,new Point(24,26),new Point(40,26));
                dc.DrawLine(Outline,new Point(24,33),new Point(37,33));
                dc.DrawLine(Outline,new Point(24,40),new Point(34,40));
                break;
            case PetMotif.Heart: dc.DrawGeometry(color,Outline,Heart); break;
            case PetMotif.Leaf: dc.DrawGeometry(color,Outline,Leaf); break;
            case PetMotif.Spark: dc.DrawGeometry(color,Outline,Spark); break;
            case PetMotif.Fish:
                dc.DrawGeometry(color,Outline,Fish);
                dc.DrawEllipse(PetUi.Ink,null,new Point(22,30),2,2);
                dc.DrawLine(Outline,new Point(32,25),new Point(32,39));
                break;
            case PetMotif.Ball:
                dc.DrawEllipse(color,Outline,new Point(32,33),19,19);
                dc.DrawGeometry(null,Outline,YarnLines);
                dc.DrawLine(Outline,new Point(48,45),new Point(56,52));
                break;
            case PetMotif.Clock:
                dc.DrawEllipse(color,Outline,new Point(32,33),19,19);
                dc.DrawEllipse(PetUi.Surface,null,new Point(32,33),14,14);
                dc.DrawLine(Outline,new Point(32,33),new Point(32,22));
                dc.DrawLine(Outline,new Point(32,33),new Point(40,37));
                break;
            case PetMotif.Trophy:
                dc.DrawEllipse(null,Outline,new Point(17,27),8,9);
                dc.DrawEllipse(null,Outline,new Point(47,27),8,9);
                dc.DrawRoundedRectangle(color,Outline,new Rect(19,15,26,26),6,6);
                dc.DrawLine(Outline,new Point(32,41),new Point(32,49));
                dc.DrawRoundedRectangle(color,Outline,new Rect(22,49,20,5),2,2);
                break;
            case PetMotif.Palette: // A painter's palette with four dabs.
                dc.DrawGeometry(color,Outline,Palette);
                dc.DrawEllipse(Rose,Outline,new Point(23,23),4.5,4.5);
                dc.DrawEllipse(Mint,Outline,new Point(36,20),4.5,4.5);
                dc.DrawEllipse(Lilac,Outline,new Point(19,35),4.5,4.5);
                dc.DrawEllipse(Gold,Outline,new Point(25,46),4.5,4.5);
                break;
            case PetMotif.Bubble: // A speech bubble with three dots.
                dc.DrawGeometry(color,Outline,Bubble);
                for (int i = 0; i < 3; i++) dc.DrawEllipse(PetUi.Ink,null,new Point(23 + i * 9,28),2.6,2.6);
                break;
            case PetMotif.Note: // Two beamed notes.
                dc.DrawLine(Bold,new Point(27,46),new Point(27,22));
                dc.DrawLine(Bold,new Point(49,41),new Point(49,17));
                dc.DrawGeometry(PetUi.Ink,null,NoteBeam);
                dc.DrawEllipse(color,Outline,new Point(21,46),7,5.5);
                dc.DrawEllipse(color,Outline,new Point(43,41),7,5.5);
                break;
            case PetMotif.Speaker: // A loudspeaker with two sound waves.
                dc.DrawGeometry(color,Outline,Speaker);
                dc.DrawGeometry(null,Bold,Waves);
                break;
            case PetMotif.Gear:
                dc.DrawGeometry(color,Outline,Gear);
                dc.DrawEllipse(PetUi.Surface,Outline,new Point(32,32),6,6);
                break;
            case PetMotif.Gift: // A wrapped box with a gold bow.
                dc.DrawRectangle(color,Outline,new Rect(15,29,34,23));
                dc.DrawRectangle(color,Outline,new Rect(12,22,40,8));
                dc.DrawRectangle(Gold,Outline,new Rect(29,22,6,30));
                dc.DrawEllipse(Gold,Outline,new Point(25,17),6,4.5);
                dc.DrawEllipse(Gold,Outline,new Point(39,17),6,4.5);
                break;
            case PetMotif.Cup: // A steaming cup of tea on a saucer.
                dc.DrawGeometry(null,Outline,Steam);
                dc.DrawEllipse(null,Bold,new Point(46,33),5.5,5.5);
                dc.DrawEllipse(PetUi.Surface,Outline,new Point(30,51),19,3.5);
                dc.DrawGeometry(color,Outline,Cup);
                break;
            case PetMotif.Gem:
                dc.DrawGeometry(color,Outline,Gem);
                dc.DrawGeometry(null,Outline,Facets);
                dc.DrawLine(PenFor(PetUi.Surface),new Point(16,26),new Point(21,21));
                break;
            case PetMotif.Moon: // A crescent and a twinkle.
                dc.DrawGeometry(color,Outline,Moon);
                dc.DrawGeometry(color,Outline,Twinkle);
                break;
            case PetMotif.Sword:
                dc.DrawGeometry(color,Outline,Blade);
                dc.DrawGeometry(Wood,Outline,Grip);
                dc.DrawGeometry(Gold,Outline,Guard);
                dc.DrawEllipse(Gold,Outline,new Point(15,49),3.5,3.5);
                break;
            case PetMotif.Shield:
                dc.DrawGeometry(color,Outline,Shield);
                dc.DrawLine(Outline,new Point(32,14),new Point(32,50));
                dc.DrawLine(Outline,new Point(17,28),new Point(47,28));
                break;
            case PetMotif.Crown:
                dc.DrawGeometry(color,Outline,Crown);
                foreach (var tip in new[] { new Point(14,22), new Point(32,15), new Point(50,22) }) dc.DrawEllipse(color,Outline,tip,3.5,3.5);
                dc.DrawEllipse(Rose,Outline,new Point(32,39),3.5,3.5);
                break;
            case PetMotif.Campfire:
                dc.DrawGeometry(Wood,Outline,Logs);
                dc.DrawGeometry(color,Outline,Flame);
                dc.DrawGeometry(Gold,null,Ember);
                break;
            case PetMotif.Scroll: // A note between two rollers, marked "!".
                dc.DrawRectangle(color,Outline,new Rect(17,16,30,32));
                dc.DrawRoundedRectangle(Card,Outline,new Rect(13,11,38,8),4,4);
                dc.DrawRoundedRectangle(Card,Outline,new Rect(13,45,38,8),4,4);
                dc.DrawLine(Bold,new Point(32,24),new Point(32,33));
                dc.DrawEllipse(PetUi.Ink,null,new Point(32,39),2.5,2.5);
                break;
            case PetMotif.Mushroom:
                dc.DrawGeometry(PetUi.Surface,Outline,Stem);
                dc.DrawGeometry(color,Outline,Cap);
                dc.DrawEllipse(PetUi.Surface,null,new Point(22,25),4,3.5);
                dc.DrawEllipse(PetUi.Surface,null,new Point(38,20),3.5,3);
                dc.DrawEllipse(PetUi.Surface,null,new Point(44,29),3,2.5);
                dc.DrawEllipse(PetUi.Ink,null,new Point(28,43),1.6,2);
                dc.DrawEllipse(PetUi.Ink,null,new Point(36,43),1.6,2);
                break;
            case PetMotif.Box: // An empty cardboard box, flaps open.
                dc.DrawGeometry(Wood,Outline,Flaps);
                dc.DrawGeometry(color,Outline,BoxFront);
                dc.DrawLine(Outline,new Point(24,38),new Point(40,38));
                break;
            case PetMotif.Flag:
                dc.DrawEllipse(Wood,Outline,new Point(21,53),8,3);
                dc.DrawLine(Bold,new Point(21,12),new Point(21,52));
                dc.DrawGeometry(color,Outline,Pennant);
                break;
            case PetMotif.Question: // A thick "?" with a brown rim.
                dc.DrawGeometry(null,QuestionInk,QuestionHook);
                dc.DrawGeometry(null,PenFor(color),QuestionHook);
                dc.DrawEllipse(color,Outline,new Point(33,47),4.5,4.5);
                break;
            case PetMotif.Warning:
                dc.DrawGeometry(color,Outline,Triangle);
                dc.DrawLine(Bold,new Point(32,24),new Point(32,37));
                dc.DrawEllipse(PetUi.Ink,null,new Point(32,44),2.6,2.6);
                break;
        }
        dc.Pop();
    }
    // A thick stroke in an art colour (the "?" inside its rim, a gem's gleam), made once per colour.
    private static Pen PenFor(Brush brush)
    {
        lock (ColorPens)
        {
            if (!ColorPens.TryGetValue(brush, out var pen)) ColorPens[brush] = pen = PetUi.FrozenPen(brush, 5);
            return pen;
        }
    }
    public static DockPanel Beside(UIElement text, PetMotif motif, double size = 32)
    {
        var row = new DockPanel { LastChildFill = true };
        var art = new PetIllustration(motif,size) { Margin = new Thickness(0,0,10,0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(art,Dock.Left); row.Children.Add(art); row.Children.Add(text); return row;
    }
}
