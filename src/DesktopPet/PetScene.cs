using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DesktopPet;

public enum PetSceneKind { Home, Adventure, Expedition, Infinity, Workshop, Diary, Speech, Sound, Settings, Treasure, Victory, Rest }

// Shared, frozen scenes: built once per kind and coat, not once per frame or per window.
// Decoration only. Height belongs to the scrollable content, never to an overlay above controls.
public sealed class PetScene : FrameworkElement
{
    private static readonly Dictionary<(PetSceneKind, CoatPalette, string), DrawingGroup> Scenes = new();
    private readonly PetSceneKind kind;
    private static readonly Brush Ink = PetUi.Ink, Cream = PetUi.Surface, Orange = PetUi.AccentBright,
        Gold = PetUi.Game.Gold, Mint = PetUi.Mint, Rose = PetUi.Rose, Lilac = PetUi.Lilac,
        Wood = PetUi.Hex("#B4815E"), DarkWood = PetUi.Hex("#6D4E40"), Steel = PetUi.Hex("#E4E2EC"), Glow = PetUi.Hex("#55FFE7A6");
    private static readonly Pen Rim = PetUi.FrozenPen(Ink, 2), Fine = PetUi.FrozenPen(Ink, 1.3);
    private static readonly Color NightTop = Color.FromRgb(57,49,90);
    // The scene cat wears the pet's coat and accessory; PetWindow sets these when they are loaded or saved.
    public static string? Coat { get; set; }
    public static string Accessory { get; set; } = "None";
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
        var key = (kind, CoatPalette.For(Coat), Accessory ?? "None");
        if (!Scenes.TryGetValue(key, out var scene)) { scene = Build(kind,key.Item2,key.Item3); Scenes.Add(key,scene); }
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
    private static Pen Round(Brush brush,double thickness) {
        var pen=new Pen(brush,thickness){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};pen.Freeze();return pen;
    }
    private static void Star(DrawingContext d,double x,double y,double r) {
        d.DrawLine(PetUi.FrozenPen(Gold,2),new Point(x-r,y),new Point(x+r,y));
        d.DrawLine(PetUi.FrozenPen(Gold,2),new Point(x,y-r),new Point(x,y+r));
    }
    private static void Sky(DrawingContext d,Color top,Color bottom) {
        var sky=new LinearGradientBrush(top,bottom,90);sky.Freeze();d.DrawRectangle(sky,null,new Rect(0,0,480,120));
    }
    private static void Clouds(DrawingContext d) { for(int i=0;i<8;i++) d.DrawEllipse(Cream,null,new Point(25+i*67,21+(i%2)*9),18,5); }
    private static void Stars(DrawingContext d) { for(int i=0;i<8;i++) Star(d,22+i*60,16+(i%3)*10,2); }
    private static void Moon(DrawingContext d,double x,double y) {
        d.DrawEllipse(Cream,null,new Point(x,y),13,13);d.DrawEllipse(PetUi.Frozen(NightTop),null,new Point(x+6,y-4),11,11);
    }
    private static void Floor(DrawingContext d,Brush wood) { Box(d,0,97,480,27,wood,0);d.DrawLine(Fine,new Point(0,105),new Point(480,105)); }
    private static void Plant(DrawingContext d,double x,double y) {
        Box(d,x-10,y,20,17,Wood,3); d.DrawLine(Rim,new Point(x,y),new Point(x,y-28));
        d.DrawEllipse(Mint,Fine,new Point(x-7,y-16),8,4);d.DrawEllipse(Mint,Fine,new Point(x+7,y-24),8,4);
    }
    private static void Z(DrawingContext d,double x,double y,double s) =>
        d.DrawGeometry(null,Round(Cream,2.4),PetUi.FrozenGeometry($"M{x},{y} L{x+s},{y} L{x},{y+s} L{x+s},{y+s}"));
    // Head details shared by the sitting and lying cats: markings clipped to the head, muzzle, eyes, cheeks, nose.
    private static void Face(DrawingContext d,CoatPalette c,double x,double y,double rx,double ry,bool asleep) {
        if(c.PatchA!=null) {
            d.PushClip(new EllipseGeometry(new Point(x,y),rx-1,ry-1));
            if(c.Markings==CoatMarkings.Calico) {
                d.DrawEllipse(c.PatchA,null,new Point(x-rx*0.6,y-ry*0.7),rx*0.6,ry*0.6);
                if(c.PatchB!=null) d.DrawEllipse(c.PatchB,null,new Point(x+rx*0.7,y-ry*0.8),rx*0.4,ry*0.45);
            } else for(int i=-1;i<=1;i++) d.DrawLine(Round(c.PatchA,2.6),new Point(x+i*6,y-ry+1),new Point(x+i*5,y-ry*0.45));
            d.Pop();
        }
        d.DrawEllipse(c.Muzzle,null,new Point(x,y+7),11,8);
        foreach(int side in new[]{-1,1}) {
            double eye=x+side*9;
            if(asleep) d.DrawGeometry(null,Round(c.Ink,1.8),PetUi.FrozenGeometry($"M{eye-4},{y-3} Q{eye},{y+1} {eye+4},{y-3}"));
            else {
                if(c.Iris!=null) d.DrawEllipse(c.Iris,null,new Point(eye,y-2),3.2,3.8);
                d.DrawEllipse(c.Ink,null,new Point(eye,y-2),c.Iris!=null?1.5:2,3);
                d.DrawEllipse(Brushes.White,null,new Point(eye+0.8,y-3.6),0.8,0.8);
            }
            d.DrawEllipse(c.Pink,null,new Point(x+side*15,y+6),3,2);
        }
        d.DrawEllipse(c.Pink,PetUi.FrozenPen(c.Ink,1),new Point(x,y+6),2.6,1.8);
        d.DrawLine(PetUi.FrozenPen(c.Ink,1.3),new Point(x,y+8),new Point(x,y+11));
    }
    // The pet's accessory from PetRenderer, its head (centre 55,48) mapped onto this smaller head.
    private static void Wear(DrawingContext d,CoatPalette c,string accessory,AccessoryLayer layer,double hx,double hy) {
        d.PushTransform(new TranslateTransform(hx-55*0.75,hy-48*0.75));d.PushTransform(new ScaleTransform(0.75,0.75));
        PetRenderer.DrawAccessory(d,accessory,layer,c);d.Pop();d.Pop();
    }
    private static void Cat(DrawingContext d,CoatPalette c,string a,double x,double y) {
        var rim=PetUi.FrozenPen(c.Ink,2);
        d.DrawEllipse(PetUi.Hex("#25000000"),null,new Point(x,y+29),29,5);
        // Tail behind the body, outlined like the pet's own.
        var tail=$"M{x+12},{y+22} C{x+48},{y+26} {x+45},{y-4} {x+30},{y+4}";
        Path(d,tail,null,Round(c.Ink,11));Path(d,tail,null,Round(c.Ear,7));
        d.DrawEllipse(c.Fur,rim,new Point(x,y+12),18,19);
        d.DrawEllipse(c.Cream,null,new Point(x,y+14),11,13);
        Wear(d,c,a,AccessoryLayer.Neck,x,y-6);
        Path(d,$"M{x-20},{y-6} L{x-21},{y-29} L{x-6},{y-18} M{x+6},{y-18} L{x+21},{y-29} L{x+20},{y-6}",c.Ear,rim);
        Path(d,$"M{x-17},{y-12} L{x-18},{y-23} L{x-10},{y-17}Z M{x+17},{y-12} L{x+18},{y-23} L{x+10},{y-17}Z",c.Pink);
        d.DrawEllipse(c.Fur,rim,new Point(x,y-6),23,19);
        Face(d,c,x,y-6,23,19,false);
        d.DrawEllipse(c.Paw,rim,new Point(x-12,y+29),10,4);d.DrawEllipse(c.Paw,rim,new Point(x+12,y+29),10,4);
        Wear(d,c,a,AccessoryLayer.Head,x,y-6);
    }
    // Curled up asleep: head resting on the paws, tail wrapped in front.
    private static void SleepingCat(DrawingContext d,CoatPalette c,string a,double x,double y) {
        var rim=PetUi.FrozenPen(c.Ink,2);double hx=x-26,hy=y+6;
        d.DrawEllipse(PetUi.Hex("#25000000"),null,new Point(x+4,y+31),46,5);
        d.DrawEllipse(c.Fur,rim,new Point(x+8,y+16),36,15);
        if(c.Markings==CoatMarkings.Calico && c.PatchB!=null) {
            d.PushClip(new EllipseGeometry(new Point(x+8,y+16),35,14));d.DrawEllipse(c.PatchB,null,new Point(x+24,y+8),14,10);d.Pop();
        }
        Wear(d,c,a,AccessoryLayer.Neck,hx,hy);
        Path(d,$"M{hx-18},{hy-4} L{hx-19},{hy-25} L{hx-5},{hy-15} M{hx+5},{hy-15} L{hx+19},{hy-25} L{hx+18},{hy-4}",c.Ear,rim);
        d.DrawEllipse(c.Fur,rim,new Point(hx,hy),22,18);
        Face(d,c,hx,hy,22,18,true);
        Wear(d,c,a,AccessoryLayer.Head,hx,hy);
        d.DrawEllipse(c.Paw,rim,new Point(hx+20,y+28),10,4);
        var tail=$"M{x+42},{y+20} C{x+56},{y+34} {x+20},{y+34} {x-2},{y+29}";
        Path(d,tail,null,Round(c.Ink,11));Path(d,tail,null,Round(c.Ear,7));
    }
    private static void Headphones(DrawingContext d,double x,double y) {
        var band=$"M{x-24},{y-6} C{x-28},{y-44} {x+28},{y-44} {x+24},{y-6}";
        Path(d,band,null,Round(Ink,8));Path(d,band,null,Round(Lilac,4.5));
        Box(d,x-31,y-16,11,20,Lilac,5);Box(d,x+20,y-16,11,20,Lilac,5);
    }
    private static void Slime(DrawingContext d,double x,double y,Brush color) {
        d.DrawEllipse(color,Rim,new Point(x,y),16,13);d.DrawEllipse(Cream,null,new Point(x-6,y-6),4,2);
        d.DrawEllipse(Ink,null,new Point(x-5,y),1.5,2);d.DrawEllipse(Ink,null,new Point(x+5,y),1.5,2);
    }
    private static void Chest(DrawingContext d,double x,double y) {
        Box(d,x,y,48,32,Wood);Box(d,x,y,48,13,Gold);Box(d,x+20,y+10,8,13,Gold,2);
        Star(d,x+7,y-10,4);Star(d,x+42,y-15,5);
    }
    private static void OpenChest(DrawingContext d,double x,double y) {
        d.DrawEllipse(Glow,null,new Point(x+24,y+4),36,20);
        Path(d,$"M{x+2},{y+10} L{x-4},{y-12} L{x+52},{y-12} L{x+46},{y+10}Z",Gold,Rim);
        d.DrawEllipse(Gold,Rim,new Point(x+16,y+11),7,4);d.DrawEllipse(Gold,Rim,new Point(x+30,y+9),7,4);
        Box(d,x,y+10,48,26,Wood);Box(d,x,y+10,48,8,Gold,3);Box(d,x+20,y+16,8,11,Gold,2);
        Star(d,x+4,y-20,4);Star(d,x+40,y-24,5);
    }
    private static void Coins(DrawingContext d,double x,double floor,int count) {
        for(int i=0;i<count;i++) d.DrawEllipse(Gold,Rim,new Point(x+(i%3)*12-(i/3)*4+(i/3%2)*6,floor-4-(i/3)*6),8,4);
    }
    private static void Gem(DrawingContext d,double x,double y,Brush color) =>
        Path(d,$"M{x-7},{y-4} L{x-3},{y-9} L{x+3},{y-9} L{x+7},{y-4} L{x},{y+6}Z M{x-7},{y-4} L{x+7},{y-4}",color,Fine);
    // Cup with two handles on a stem and a stepped base, so it reads as a trophy at banner size.
    private static void Trophy(DrawingContext d,double x,double floor) {
        Box(d,x-17,floor-9,34,9,DarkWood,3);Box(d,x-11,floor-15,22,7,Gold,2);
        d.DrawRectangle(Gold,Rim,new Rect(x-3,floor-26,6,12));
        Path(d,$"M{x-14},{floor-60} C{x-30},{floor-60} {x-28},{floor-36} {x-12},{floor-40} M{x+14},{floor-60} C{x+30},{floor-60} {x+28},{floor-36} {x+12},{floor-40}",null,Round(Ink,6));
        Path(d,$"M{x-14},{floor-60} C{x-30},{floor-60} {x-28},{floor-36} {x-12},{floor-40} M{x+14},{floor-60} C{x+30},{floor-60} {x+28},{floor-36} {x+12},{floor-40}",null,Round(Gold,3));
        Path(d,$"M{x-17},{floor-64} L{x+17},{floor-64} C{x+17},{floor-38} {x+6},{floor-28} {x},{floor-27} C{x-6},{floor-28} {x-17},{floor-38} {x-17},{floor-64}Z",Gold,Rim);
        d.DrawEllipse(Cream,null,new Point(x-8,floor-54),2.5,6);Star(d,x+3,floor-48,4);
    }
    // Blade, cross-guard, grip and pommel, point up.
    private static void Sword(DrawingContext d,double x,double top,double length) {
        double guard=top+length*0.62;
        Path(d,$"M{x},{top} L{x+5},{top+8} L{x+5},{guard} L{x-5},{guard} L{x-5},{top+8}Z",Steel,Rim);
        d.DrawLine(Fine,new Point(x,top+6),new Point(x,guard-3));
        Box(d,x-13,guard,26,6,Gold,3);Box(d,x-3,guard+6,6,length*0.26,Wood,2);
        d.DrawEllipse(Gold,Rim,new Point(x,guard+length*0.26+9),4,4);
    }
    private static void Gear(DrawingContext d,double x,double y,double r,Brush fill) {
        var path=new System.Text.StringBuilder();
        for(int k=0;k<8;k++) foreach(var (a,rad) in new[]{(-15.0,r*0.78),(-9.0,r),(9.0,r),(15.0,r*0.78)}) {
            double t=(k*45+a)*Math.PI/180;path.Append(path.Length==0?"M":" L").Append($"{x+Math.Cos(t)*rad:0.##},{y+Math.Sin(t)*rad:0.##}");
        }
        Path(d,path.Append('Z').ToString(),fill,Rim);d.DrawEllipse(Cream,Fine,new Point(x,y),r*0.3,r*0.3);
    }
    private static DrawingGroup Build(PetSceneKind k,CoatPalette coat,string wear)
    {
        var g=new DrawingGroup();using(var d=g.Open()) {
            switch(k) {
                case PetSceneKind.Expedition:
                    Sky(d,NightTop,Color.FromRgb(130,110,164));Stars(d);d.DrawEllipse(Cream,null,new Point(410,23),13,13);
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
                    break;
                case PetSceneKind.Infinity:
                    Sky(d,NightTop,Color.FromRgb(130,110,164));Stars(d);d.DrawEllipse(Cream,null,new Point(410,23),13,13);
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
                    break;
                case PetSceneKind.Adventure:
                    Sky(d,Color.FromRgb(250,214,174),Color.FromRgb(255,244,221));Clouds(d);d.DrawEllipse(Gold,null,new Point(410,23),13,13);
                    Path(d,"M0,88 Q90,22 180,82 Q290,12 390,76 Q440,42 480,66 L480,120 L0,120Z",Mint);
                    Path(d,"M0,103 Q100,68 220,100 Q340,74 480,96 L480,120 L0,120Z",PetUi.Care);
                    Path(d,"M80,120 C120,88 180,115 235,93 S330,68 362,58",null,PetUi.FrozenPen(Cream,5));
                    for(int i=0;i<4;i++)d.DrawEllipse(Gold,Rim,new Point(148+i*52,104-i*12),4,4);
                    d.DrawLine(Rim,new Point(372,60),new Point(372,24));Path(d,"M372,24 L402,32 L372,40Z",Orange,Rim);
                    // Slime, cat and chest sit inside x 152–328, the part a mode card keeps.
                    Cat(d,coat,wear,222,67);Slime(d,170,99,Mint);Chest(d,272,83);
                    break;
                case PetSceneKind.Workshop:
                    Sky(d,Color.FromRgb(242,211,179),Color.FromRgb(253,235,216));
                    Floor(d,Wood);
                    Box(d,92,14,150,76,DarkWood,6);
                    for(int i=0;i<6;i++)d.DrawEllipse(Gold,null,new Point(104+i*25,22),2,2);
                    Sword(d,122,26,56);
                    d.DrawLine(Round(Wood,5),new Point(168,42),new Point(168,82));Box(d,154,30,28,14,Steel,3);
                    d.DrawEllipse(Rose,Rim,new Point(213,56),18,18);d.DrawEllipse(Gold,Rim,new Point(213,56),6,6);
                    d.DrawLine(Rim,new Point(312,0),new Point(312,12));Path(d,"M300,24 L305,12 L319,12 L324,24Z",Gold,Rim);
                    d.DrawEllipse(Glow,null,new Point(312,30),22,8);
                    Box(d,330,70,118,9,Wood,3);d.DrawLine(Round(DarkWood,5),new Point(342,79),new Point(342,97));d.DrawLine(Round(DarkWood,5),new Point(436,79),new Point(436,97));
                    Chest(d,350,38);Box(d,410,56,26,14,Steel,3);
                    Cat(d,coat,wear,276,65);Plant(d,46,81);
                    break;
                case PetSceneKind.Diary:
                    Sky(d,Color.FromRgb(244,198,168),Color.FromRgb(255,240,223));
                    Floor(d,Wood);
                    // Window onto a sunset.
                    Box(d,372,16,76,58,PetUi.Hex("#F9CDA9"),4);
                    d.PushClip(new RectangleGeometry(new Rect(374,18,72,54)));d.DrawEllipse(Gold,null,new Point(410,72),16,16);
                    d.DrawEllipse(Rose,null,new Point(388,40),14,4);d.Pop();
                    d.DrawLine(Rim,new Point(410,16),new Point(410,74));d.DrawLine(Rim,new Point(372,44),new Point(448,44));
                    // Bookshelf with coloured spines.
                    Box(d,28,18,62,79,Wood,4);d.DrawLine(Rim,new Point(28,56),new Point(90,56));
                    var spines=new[]{Rose,Mint,Lilac,Gold,Orange,Mint,Rose,Lilac};
                    for(int i=0;i<4;i++){Box(d,34+i*13,28,10,28,spines[i],2);Box(d,34+i*13,66,10,31,spines[i+4],2);}
                    // Desk with the open diary and a pen.
                    Box(d,118,72,160,8,DarkWood,3);d.DrawLine(Round(DarkWood,5),new Point(128,80),new Point(128,97));d.DrawLine(Round(DarkWood,5),new Point(268,80),new Point(268,97));
                    Box(d,136,44,124,30,Lilac);Box(d,142,38,56,32,Cream,3);Box(d,198,38,56,32,Cream,3);
                    for(int i=0;i<3;i++){d.DrawLine(Fine,new Point(150,47+i*8),new Point(190,47+i*8));d.DrawLine(Fine,new Point(206,47+i*8),new Point(244,47+i*8));}
                    d.DrawLine(Round(Ink,5),new Point(232,34),new Point(256,20));d.DrawLine(Round(Orange,3),new Point(232,34),new Point(256,20));
                    Cat(d,coat,wear,318,65);
                    break;
                case PetSceneKind.Speech:
                    Sky(d,Color.FromRgb(214,234,240),Color.FromRgb(255,246,230));Clouds(d);d.DrawEllipse(Gold,null,new Point(60,26),13,13);
                    Path(d,"M0,92 Q240,80 480,92 L480,120 L0,120Z",Mint,Rim);
                    Box(d,404,40,14,57,Wood,3);d.DrawEllipse(Mint,Rim,new Point(411,34),36,26);d.DrawEllipse(Rose,null,new Point(398,28),3,3);d.DrawEllipse(Rose,null,new Point(424,40),3,3);
                    for(int i=0;i<6;i++){double x=40+i*30+(i%2)*8;d.DrawLine(PetUi.FrozenPen(PetUi.Care,2),new Point(x,104),new Point(x,94));d.DrawEllipse(i%3==0?Rose:i%3==1?Lilac:Gold,Fine,new Point(x,92),4,4);}
                    Cat(d,coat,wear,205,63);Box(d,254,21,105,49,Cream,12);Path(d,"M271,69 L259,82 L293,69",Cream,Rim);
                    for(int i=0;i<3;i++)d.DrawEllipse(i==0?Mint:i==1?Rose:Lilac,null,new Point(280+i*24,45),5,5);
                    break;
                case PetSceneKind.Sound:
                    // A small stage: curtains, a spotlight on the cat, headphones on.
                    Sky(d,Color.FromRgb(107,70,96),Color.FromRgb(156,107,126));
                    Path(d,"M218,0 L262,0 L330,97 L150,97Z",PetUi.Hex("#40FFF3D6"));
                    Floor(d,DarkWood);d.DrawEllipse(PetUi.Hex("#40FFF3D6"),null,new Point(240,98),80,7);
                    Path(d,"M0,0 L74,0 Q54,48 66,97 L0,97Z",Rose,Rim);Path(d,"M480,0 L406,0 Q426,48 414,97 L480,97Z",Rose,Rim);
                    Path(d,"M20,0 Q30,50 22,97 M44,0 Q52,50 46,97 M460,0 Q450,50 458,97 M436,0 Q428,50 434,97",null,Fine);
                    Box(d,108,29,52,68,Lilac);d.DrawEllipse(DarkWood,Rim,new Point(134,72),16,16);d.DrawEllipse(Cream,null,new Point(134,72),7,7);d.DrawEllipse(Gold,Rim,new Point(134,43),6,6);
                    Cat(d,coat,wear,240,65);Headphones(d,240,59);
                    for(int i=0;i<3;i++){double x=318+i*20;d.DrawEllipse(Gold,Rim,new Point(x,49+i*8),5,3.5);d.DrawLine(Rim,new Point(x+5,49+i*8),new Point(x+5,29+i*8));}
                    break;
                case PetSceneKind.Settings:
                    Sky(d,Color.FromRgb(239,227,245),Color.FromRgb(255,248,241));
                    Floor(d,Wood);
                    Box(d,104,21,126,72,Cream);for(int i=0;i<3;i++){d.DrawLine(PetUi.FrozenPen(Lilac,4),new Point(119,38+i*20),new Point(214,38+i*20));d.DrawEllipse(i==1?Mint:Orange,Rim,new Point(i==1?155:190,38+i*20),6,6);}
                    Gear(d,372,40,18,Gold);Gear(d,398,64,12,Mint);
                    d.DrawEllipse(Cream,Rim,new Point(446,30),15,15);d.DrawLine(Rim,new Point(446,30),new Point(446,20));d.DrawLine(Rim,new Point(446,30),new Point(453,33));
                    Cat(d,coat,wear,302,65);Plant(d,46,81);
                    break;
                case PetSceneKind.Treasure:
                    Sky(d,Color.FromRgb(247,217,154),Color.FromRgb(255,243,217));
                    Floor(d,Wood);
                    for(int i=0;i<7;i++)Star(d,108+i*39,15+(i%3)*13,3);
                    OpenChest(d,92,61);Coins(d,170,97,8);
                    Cat(d,coat,wear,245,65);Trophy(d,340,97);
                    Gem(d,392,90,Mint);Gem(d,410,86,Lilac);Gem(d,428,90,Rose);Coins(d,440,97,4);
                    break;
                case PetSceneKind.Victory:
                    Sky(d,Color.FromRgb(250,214,174),Color.FromRgb(255,244,221));Clouds(d);
                    Floor(d,Wood);
                    d.DrawLine(Fine,new Point(60,5),new Point(420,5));
                    for(int i=0;i<10;i++) { double x=66+i*36;Path(d,$"M{x},5 L{x+18},5 L{x+9},17Z",i%2==0?Rose:Mint,Fine); }
                    var confetti=new[]{Rose,Mint,Lilac,Gold};
                    for(int i=0;i<14;i++) { double x=30+i*33+(i*17%11),y=26+(i*29%50);
                        d.PushTransform(new RotateTransform(i*37%90-45,x,y));d.DrawRectangle(confetti[i%4],null,new Rect(x-3,y-1.5,6,3));d.Pop(); }
                    // Podium: the cat stands on the top step.
                    Box(d,170,86,50,11,Lilac,2);Box(d,218,80,56,17,Gold,2);Box(d,272,89,46,8,Mint,2);Star(d,246,88,4);
                    Cat(d,coat,wear,246,47);Trophy(d,372,97);Plant(d,46,81);
                    break;
                case PetSceneKind.Rest:
                    Sky(d,NightTop,Color.FromRgb(130,110,164));Stars(d);Moon(d,410,25);
                    Floor(d,Wood);
                    d.DrawEllipse(Lilac,Rim,new Point(244,94),66,11);
                    SleepingCat(d,coat,wear,252,64);
                    Z(d,206,46,7);Z(d,192,30,9);Z(d,174,12,11);
                    Box(d,330,66,34,31,Cream,4);d.DrawLine(Rim,new Point(347,66),new Point(347,52));
                    d.DrawEllipse(Glow,null,new Point(347,46),24,16);Path(d,"M336,52 L340,36 L354,36 L358,52Z",Gold,Rim);
                    Plant(d,46,81);
                    break;
                default:
                    Sky(d,Color.FromRgb(250,214,174),Color.FromRgb(255,244,221));Clouds(d);d.DrawEllipse(Gold,null,new Point(410,23),13,13);
                    Floor(d,Wood);Plant(d,46,81);Plant(d,432,81);
                    Box(d,110,19,90,66,Cream);Box(d,117,26,76,46,Mint);d.DrawLine(Rim,new Point(155,85),new Point(155,94));Cat(d,coat,wear,261,65);d.DrawEllipse(Lilac,Rim,new Point(345,84),13,13);
                    Path(d,"M335,79 Q350,95 356,75 M333,86 Q346,71 358,86",null,Fine);
                    break;
            }
        }g.Freeze();return g;
    }
}
