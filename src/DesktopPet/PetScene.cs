using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

public enum PetSceneKind { Home, Adventure, Expedition, Infinity, Workshop, Diary, Speech, Sound, Settings, Treasure, Victory, Rest }

// Shared, frozen scenes: built once per kind, not once per frame or per window.
// Decoration only. Height belongs to the scrollable content, never to an overlay above controls.
public sealed class PetScene : FrameworkElement
{
    private static readonly Dictionary<PetSceneKind, DrawingGroup> Scenes = new();
    private readonly PetSceneKind kind;
    private static readonly Brush Ink = PetUi.Ink, Cream = PetUi.Surface, Orange = PetUi.AccentBright,
        Gold = PetUi.Game.Gold, Mint = PetUi.Hex("#9BD2AE"), Rose = PetUi.Hex("#F3A1A0"), Lilac = PetUi.Hex("#C5ADE5"),
        Wood = PetUi.Hex("#B4815E"), DarkWood = PetUi.Hex("#6D4E40");
    private static readonly Pen Rim = PetUi.FrozenPen(Ink, 2), Fine = PetUi.FrozenPen(Ink, 1.3);
    public PetScene(PetSceneKind kind, double height = 96)
    {
        this.kind = kind; Height = height; MinWidth = 0;
        IsHitTestVisible = false; Focusable = false; Margin = new Thickness(0,0,0,12);
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        Draw(dc, kind, ActualWidth, ActualHeight);
    }
    internal static void Draw(DrawingContext dc, PetSceneKind kind, double width, double height)
    {
        if (!Scenes.TryGetValue(kind, out var scene)) { scene = Build(kind); Scenes.Add(kind,scene); }
        double ActualWidth = width, ActualHeight = height;
        dc.PushClip(new RectangleGeometry(new Rect(0,0,ActualWidth,ActualHeight),12,12));
        // Fill the banner while preserving shapes, never stretch the cat.
        double scale = Math.Max(ActualWidth / 480, ActualHeight / 120);
        dc.PushTransform(new TranslateTransform((ActualWidth-480*scale)/2,(ActualHeight-120*scale)/2));
        dc.PushTransform(new ScaleTransform(scale,scale)); dc.DrawDrawing(scene);
        dc.Pop(); dc.Pop(); dc.Pop();
    }
    private static void Path(DrawingContext d, string path, Brush? fill, Pen? pen = null) => d.DrawGeometry(fill,pen,PetUi.FrozenGeometry(path));
    private static void Box(DrawingContext d, double x,double y,double w,double h,Brush fill,double r=5) => d.DrawRoundedRectangle(fill,Rim,new Rect(x,y,w,h),r,r);
    private static void Star(DrawingContext d,double x,double y,double r) {
        d.DrawLine(PetUi.FrozenPen(Gold,2),new Point(x-r,y),new Point(x+r,y));
        d.DrawLine(PetUi.FrozenPen(Gold,2),new Point(x,y-r),new Point(x,y+r));
    }
    private static void Plant(DrawingContext d,double x,double y) {
        Box(d,x-10,y,20,17,Wood,3); d.DrawLine(Rim,new Point(x,y),new Point(x,y-28));
        d.DrawEllipse(Mint,Fine,new Point(x-7,y-16),8,4);d.DrawEllipse(Mint,Fine,new Point(x+7,y-24),8,4);
    }
    private static void Cat(DrawingContext d,double x,double y,bool asleep=false) {
        d.DrawEllipse(PetUi.Hex("#25000000"),null,new Point(x,y+29),29,5);
        d.DrawEllipse(Orange,Rim,new Point(x,y+12),18,19);
        d.DrawEllipse(Cream,null,new Point(x,y+14),11,13);
        Path(d,$"M{x-20},{y-6} L{x-21},{y-29} L{x-6},{y-18} M{x+6},{y-18} L{x+21},{y-29} L{x+20},{y-6}",Orange,Rim);
        d.DrawEllipse(Orange,Rim,new Point(x,y-6),23,19);
        d.DrawEllipse(Cream,null,new Point(x,y+1),11,8);
        foreach(int side in new[]{-1,1}) {
            double eye=x+side*9;
            if(asleep) d.DrawLine(Rim,new Point(eye-3,y-7),new Point(eye+3,y-7));
            else d.DrawEllipse(Ink,null,new Point(eye,y-8),2,3);
            d.DrawEllipse(Rose,null,new Point(x+side*15,y),3,2);
        }
        d.DrawEllipse(Ink,null,new Point(x,y),2.5,1.7);
        d.DrawLine(Fine,new Point(x,y+2),new Point(x,y+5));
        d.DrawEllipse(Orange,Rim,new Point(x-12,y+29),10,4);d.DrawEllipse(Orange,Rim,new Point(x+12,y+29),10,4);
        Path(d,$"M{x+18},{y+21} C{x+48},{y+26} {x+45},{y-4} {x+30},{y+4}",null,PetUi.FrozenPen(Orange,8));
    }
    private static void Slime(DrawingContext d,double x,double y,Brush color) {
        d.DrawEllipse(color,Rim,new Point(x,y),16,13);d.DrawEllipse(Cream,null,new Point(x-6,y-6),4,2);
        d.DrawEllipse(Ink,null,new Point(x-5,y),1.5,2);d.DrawEllipse(Ink,null,new Point(x+5,y),1.5,2);
    }
    private static void Chest(DrawingContext d,double x,double y) {
        Box(d,x,y,48,32,Wood);Box(d,x,y,48,13,Gold);Box(d,x+20,y+10,8,13,Gold,2);
        Star(d,x+7,y-10,4);Star(d,x+42,y-15,5);
    }
    private static DrawingGroup Build(PetSceneKind k)
    {
        var g=new DrawingGroup();using(var d=g.Open()) {
            bool night=k is PetSceneKind.Expedition or PetSceneKind.Infinity;
            var sky=new LinearGradientBrush(night?Color.FromRgb(57,49,90):Color.FromRgb(250,214,174),night?Color.FromRgb(130,110,164):Color.FromRgb(255,244,221),90);
            sky.Freeze();d.DrawRectangle(sky,null,new Rect(0,0,480,120));
            for(int i=0;i<8;i++) { if(night) Star(d,22+i*60,16+(i%3)*10,2); else d.DrawEllipse(Cream,null,new Point(25+i*67,21+(i%2)*9),18,5); }
            d.DrawEllipse(night?Cream:Gold,null,new Point(410,23),13,13);
            if(k == PetSceneKind.Expedition) {
                // Central composition stays readable when the wide banner is cropped to a mode card.
                d.DrawRectangle(DarkWood,null,new Rect(0,94,480,26));
                Box(d,182,25,104,70,Cream,4);
                Box(d,176,20,12,80,Gold,5);Box(d,280,20,12,80,Gold,5);
                Path(d,"M195,79 C209,79 204,47 225,51 S246,77 265,38",null,PetUi.FrozenPen(Wood,3));
                for(int i=0;i<3;i++)d.DrawEllipse(Orange,Rim,new Point(204+i*23,70-i*12),3,3);
                Path(d,"M258,32 L270,44 M270,32 L258,44",null,PetUi.FrozenPen(PetUi.Danger,3));
                d.DrawEllipse(Gold,Rim,new Point(203,31),18,18);
                d.DrawEllipse(Cream,Fine,new Point(203,31),14,14);
                Path(d,"M203,17 L209,34 L203,31 L197,28Z",PetUi.Danger,Fine);
                Path(d,"M203,45 L197,28 L203,31 L209,34Z",Mint,Fine);
                Box(d,266,61,39,43,Mint,9);Box(d,271,85,29,15,Wood,4);
                Path(d,"M277,61 L277,54 Q286,48 295,54 L295,61",null,Rim);
                Box(d,263,57,44,12,Lilac,5);
                Star(d,160,31,4);Star(d,323,52,3);
                Plant(d,82,81);Chest(d,350,76);
            } else if(k == PetSceneKind.Infinity) {
                d.DrawEllipse(PetUi.Hex("#35264F"),null,new Point(240,111),150,24);
                d.DrawEllipse(null,PetUi.FrozenPen(PetUi.Hex("#5548D6C7"),16),new Point(240,54),45,42);
                d.DrawEllipse(PetUi.Hex("#251F43"),PetUi.FrozenPen(Mint,5),new Point(240,54),40,39);
                d.DrawEllipse(null,PetUi.FrozenPen(Lilac,2),new Point(240,54),32,31);
                Path(d,"M240,48 C252,31 274,32 274,48 C274,64 252,65 240,48 C228,31 206,32 206,48 C206,64 228,65 240,48Z",null,PetUi.FrozenPen(Gold,5));
                // Three incoming enemies, rather than another cat on a landscape.
                Slime(d,207,96,Mint);Slime(d,241,104,Lilac);Slime(d,275,96,Rose);
                Path(d,"M184,32 L176,44 L185,48 M295,62 L304,70 L295,80",null,PetUi.FrozenPen(Gold,2));
                for(int i=0;i<3;i++) { double x=96+i*16;Path(d,$"M{x},69 L{x+7},77 L{x},85",null,PetUi.FrozenPen(Lilac,2)); }
                Star(d,328,34,5);Star(d,160,77,4);
            } else if(k == PetSceneKind.Adventure) {
                Path(d,"M0,88 Q90,22 180,82 Q290,12 390,76 Q440,42 480,66 L480,120 L0,120Z",night?Lilac:Mint);
                Path(d,"M0,103 Q100,68 220,100 Q340,74 480,96 L480,120 L0,120Z",night?DarkWood:PetUi.Care);
                Path(d,"M80,120 C120,88 180,115 235,93 S330,68 362,58",null,PetUi.FrozenPen(Cream,5));
                for(int i=0;i<4;i++)d.DrawEllipse(Gold,Rim,new Point(148+i*52,104-i*12),4,4);
                d.DrawLine(Rim,new Point(360,60),new Point(360,24));Path(d,"M360,24 L390,32 L360,40Z",Orange,Rim);
                Cat(d,235,67);Slime(d,105,99,Mint);Chest(d,319,83);
            } else {
                Box(d,0,97,480,27,Wood,0);d.DrawLine(Fine,new Point(0,105),new Point(480,105));
                Plant(d,46,81);Plant(d,432,81);
                switch(k) {
                    case PetSceneKind.Workshop:
                        Box(d,100,23,115,67,DarkWood);for(int i=0;i<4;i++)d.DrawEllipse(Gold,null,new Point(112+i*27,32),2,2);
                        Path(d,"M130,39 L130,72 M119,63 L141,63",null,PetUi.FrozenPen(Cream,5));
                        Box(d,160,43,30,14,Lilac,2);d.DrawLine(PetUi.FrozenPen(Wood,5),new Point(175,56),new Point(175,79));Cat(d,275,65);Chest(d,340,67);break;
                    case PetSceneKind.Diary:
                        Box(d,108,29,130,67,Lilac);Box(d,115,35,55,54,Cream);Box(d,176,35,55,54,Cream);
                        for(int i=0;i<4;i++){d.DrawLine(Fine,new Point(123,46+i*9),new Point(160,46+i*9));d.DrawLine(Fine,new Point(184,46+i*9),new Point(218,46+i*9));}Cat(d,300,65);break;
                    case PetSceneKind.Sound:
                        Box(d,111,29,52,67,Lilac);d.DrawEllipse(DarkWood,Rim,new Point(137,69),16,16);d.DrawEllipse(Cream,null,new Point(137,69),7,7);d.DrawEllipse(Gold,Rim,new Point(137,41),5,5);
                        Cat(d,263,65);Path(d,"M242,58 C222,8 302,8 286,58",null,PetUi.FrozenPen(Lilac,6));
                        for(int i=0;i<3;i++){double x=335+i*18;d.DrawEllipse(Gold,Rim,new Point(x,49+i*8),5,3);d.DrawLine(Rim,new Point(x+5,49+i*8),new Point(x+5,29+i*8));}break;
                    case PetSceneKind.Speech:
                        Cat(d,205,65);Box(d,254,23,105,49,Cream,12);Path(d,"M271,71 L259,84 L293,71",Cream,Rim);
                        for(int i=0;i<3;i++)d.DrawEllipse(i==0?Mint:i==1?Rose:Lilac,null,new Point(280+i*24,47),5,5);break;
                    case PetSceneKind.Settings:
                        Box(d,104,21,126,72,Cream);for(int i=0;i<3;i++){d.DrawLine(PetUi.FrozenPen(Lilac,4),new Point(119,38+i*20),new Point(214,38+i*20));d.DrawEllipse(i==1?Mint:Orange,Rim,new Point(i==1?155:190,38+i*20),6,6);}Cat(d,302,65);break;
                    case PetSceneKind.Treasure:
                    case PetSceneKind.Victory:
                        Chest(d,112,65);Cat(d,245,65);Box(d,326,82,45,13,DarkWood);d.DrawLine(PetUi.FrozenPen(Gold,6),new Point(348,57),new Point(348,82));Box(d,330,27,36,32,Gold,10);
                        for(int i=0;i<7;i++)Star(d,108+i*39,15+(i%3)*13,3);
                        if(k==PetSceneKind.Victory) {
                            d.DrawLine(Fine,new Point(80,8),new Point(400,8));
                            for(int i=0;i<8;i++) { double x=90+i*39;Path(d,$"M{x},8 L{x+20},8 L{x+10},22Z",i%2==0?Rose:Mint,Fine); }
                        }
                        break;
                    case PetSceneKind.Rest:
                        d.DrawEllipse(Lilac,Rim,new Point(245,94),56,11);Cat(d,237,61,true);Box(d,320,65,30,30,Cream);Plant(d,335,58);break;
                    default:
                        Box(d,110,19,90,66,Cream);Box(d,117,26,76,46,Mint);d.DrawLine(Rim,new Point(155,85),new Point(155,94));Cat(d,261,65);d.DrawEllipse(Lilac,Rim,new Point(345,84),13,13);
                        Path(d,"M335,79 Q350,95 356,75 M333,86 Q346,71 358,86",null,Fine);break;
                }
            }
        }g.Freeze();return g;
    }
}
