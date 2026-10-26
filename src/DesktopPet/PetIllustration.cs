using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet;

// Static vector artwork: no timers, input or model mutation. Coordinates are a 64 DIP tile.
public enum PetMotif { Cat, Book, Heart, Leaf, Spark, Trophy, Fish, Ball, Clock }
public sealed class PetIllustration : FrameworkElement
{
    private readonly PetMotif motif;
    private readonly Brush color;
    private static readonly Brush Rose = PetUi.Hex("#F3A1A0"), Mint = PetUi.Hex("#9BD2AE"), Lilac = PetUi.Hex("#C5ADE5");
    private static readonly Pen Outline = PetUi.FrozenPen(PetUi.Ink, 2);
    public PetIllustration(PetMotif motif, double size = 40, Brush? color = null)
    {
        this.motif = motif; this.color = color ?? motif switch
        {
            PetMotif.Heart => Rose, PetMotif.Leaf => Mint, PetMotif.Book => Lilac,
            PetMotif.Spark or PetMotif.Trophy => PetUi.Game.Gold, PetMotif.Fish or PetMotif.Clock => Mint, PetMotif.Ball => Lilac, _ => PetUi.AccentBright
        };
        Width = Height = size; IsHitTestVisible = false; Focusable = false;
    }
    private static Geometry Shape(string path) { var g = Geometry.Parse(path); g.Freeze(); return g; }
    private static readonly Geometry Ears = Shape("M 13,29 L 13,9 L 28,21 M 36,21 L 51,9 L 51,29");
    private static readonly Geometry Heart = Shape("M32,51 C10,38 10,20 22,20 C28,20 31,23 32,27 C36,15 54,18 53,30 C52,39 40,47 32,51 Z");
    private static readonly Geometry Leaf = Shape("M16,49 C5,25 25,13 51,13 C51,39 38,55 16,49 Z M17,47 L41,23");
    private static readonly Geometry Spark = Shape("M32,10 L38,25 L54,31 L38,37 L32,54 L26,37 L10,31 L26,25 Z");
    private static readonly Geometry Fish = Shape("M12,32 C20,20 38,19 46,32 C38,45 20,44 12,32 Z M46,32 L56,23 L55,41 Z");
    private static readonly Geometry YarnLines = Shape("M19,24 C29,30 37,40 40,51 M17,35 C27,36 37,44 44,50 M30,14 C34,24 44,32 51,35");
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.PushTransform(new ScaleTransform(ActualWidth / 64, ActualHeight / 64));
        dc.DrawEllipse(PetUi.AccentSoft, null, new Point(32,32),30,30);
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
        }
        dc.Pop();
    }
    public static DockPanel Beside(UIElement text, PetMotif motif, double size = 32)
    {
        var row = new DockPanel { LastChildFill = true };
        var art = new PetIllustration(motif,size) { Margin = new Thickness(0,0,10,0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(art,Dock.Left); row.Children.Add(art); row.Children.Add(text); return row;
    }
}
