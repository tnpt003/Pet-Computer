using System;
using System.Windows;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

public sealed partial class BattleFieldView
{
    private static readonly Pen MotherImpactLine = FrozenPen(MotherEgg, 2.2);
    private static readonly Pen MotherSoftRim = FrozenPen(BowPink, 1.2);
    private static readonly Geometry MotherEggShape = Parse("M 0,-16 C -8,-16 -13,-2 -11,5 C -9,16 9,16 11,5 C 13,-2 8,-16 0,-16 Z");
    private static readonly Geometry MotherCrack = Parse("M -7,-2 L -2,1 L 1,-4 L 4,2 L 9,0");
    private static readonly Geometry MotherPalm = Parse("M -12,10 C -21,3 -21,-4 -16,-5 C -12,-6 -12,0 -10,0 L -12,-17 C -12,-24 -5,-24 -4,-17 L -2,-23 C 0,-29 6,-26 6,-20 L 9,-17 C 13,-22 18,-18 15,-10 L 12,6 C 9,17 -5,18 -12,10 Z");

    private static Point MotherLerp(Point a, Point b, double t) => a + (b - a) * t;
    private static void DrawMotherEgg(DrawingContext dc, Point p, double scale, double angle, bool shell = false)
    {
        dc.PushTransform(new TranslateTransform(p.X, p.Y)); dc.PushTransform(new RotateTransform(angle)); dc.PushTransform(new ScaleTransform(scale, scale));
        var shape = shell ? MotherShell : MotherEggShape;
        dc.DrawGeometry(null, Halo, shape); dc.DrawGeometry(MotherEgg, Outline, shape);
        if (!shell) { dc.DrawEllipse(BellyHeart, null, new Point(-4, -5), 3, 5); dc.DrawEllipse(BowPink, null, new Point(5, 6), 2, 2); }
        else dc.DrawGeometry(null, MotherLine, MotherCrack);
        dc.Pop(); dc.Pop(); dc.Pop();
    }
    private static void MotherImpact(DrawingContext dc, Point p, double radius, double t)
    {
        if (t < 0 || t > .4) return;
        double k = MotherEase(t / .4);
        dc.PushOpacity(1 - k);
        dc.DrawEllipse(null, MotherImpactLine, p, radius * (.3 + .7 * k), radius * (.16 + .23 * k));
        for (int i = 0; i < 5; i++)
        {
            double a = (i + .5) * Math.PI * 2 / 5, r = radius * (.2 + .7 * k);
            dc.DrawEllipse(MotherGel, MotherSoftRim, new Point(p.X + Math.Cos(a) * r, p.Y + Math.Sin(a) * r * .5 - k * (1-k) * 14), 3 * (1-k) + 1, 2 * (1-k) + 1);
        }
        dc.Pop();
    }
    private static void DrawMotherZone(DrawingContext dc, Slime s, MotherZone z, Func<double, double, Point> at)
    {
        double after = z.Age - z.Warn, end = z.Warn + z.Life;
        bool warning = after < 0;
        double fade = 1 - MotherEase((z.Age - end) / .38);
        if (z.Art == MotherArt.Heartbeat && z.Age >= end)
        {
            // The body itself opens around its heart; this halo indicates the existing hit window.
            double open = SampleMother(s).Open;
            if (open > 0) { dc.PushOpacity(open); var belly = MotherEmission(s, MotherArt.Egg, at); dc.DrawEllipse(null, MotherImpactLine, belly, 28, 21); dc.Pop(); }
            return;
        }
        if (fade <= 0) return;
        dc.PushOpacity(fade * MotherEase(z.Age / .1));
        int emissionSide = z.Art == MotherArt.Hand ? (z.Warn <= (s.Mother?.ActionWarn ?? z.Warn) + .01 ? -1 : 1) : z.X < s.X ? -1 : 1;
        var source = MotherEmission(s, z.Art, at, emissionSide);
        if (z.ActorId != 0) source = at(z.FromX,z.FromH+z.Height/2);
        if (z.Thread)
        {
            var a = at(z.FromX, z.FromH); var b = at(z.ToX, z.ToH);
            MotherCord(dc, source, a, 1);
            if (warning) { dc.DrawLine(Halo, a, b); dc.DrawLine(MotherDash, a, b); }
            else { MotherCord(dc, a, b, Math.Sin(z.Age * 8) * 2); MotherImpact(dc, b, 22, after); }
            dc.Pop(); return;
        }
        var r = new Rect(at(z.X - z.Width / 2, z.H + z.Height), at(z.X + z.Width / 2, z.H));
        var target = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
        double load = MotherEase(z.Age / Math.Max(.01, z.Warn));
        // A faint exact footprint remains legible; artwork no longer uses filled debug boxes as its body.
        dc.PushOpacity(warning ? .65 : .18);
        dc.DrawRoundedRectangle(MotherWarn, warning ? MotherDash : MotherSoftRim, r, Math.Min(12, r.Width / 2), Math.Min(12, r.Height / 2));
        dc.Pop();
        if (warning && z.Moving)
        {
            Point to = at(z.ToX, z.ToH + z.Height / 2);
            dc.PushOpacity(.6); dc.DrawLine(MotherDash, source, to); dc.Pop();
        }
        switch (z.Art)
        {
            case MotherArt.Hand:
                var palm = warning ? MotherLerp(source, target, load) : z.Age >= end ? MotherLerp(target, source, MotherEase((z.Age-end)/.38)) : target;
                MotherCord(dc, source, palm, Math.Sin(z.Age * 4) * 10, true);
                dc.PushTransform(new TranslateTransform(palm.X, palm.Y));
                dc.PushTransform(new RotateTransform((z.X < s.X ? -1 : 1) * (warning ? -25 * (1-load) : 8 * Math.Sin(after*20) * Math.Exp(-after * 10))));
                double handScale = Math.Min(z.Width / 42, z.Height / 54) * (.8 + .2 * load);
                dc.PushTransform(new ScaleTransform(handScale, handScale));
                dc.DrawGeometry(null, Halo, MotherPalm); dc.DrawGeometry(MotherGel, Outline, MotherPalm);
                dc.DrawEllipse(BellyHeart, null, new Point(0, 5), 6, 5);
                dc.Pop(); dc.Pop(); dc.Pop(); MotherImpact(dc, target, z.Width * .55, after); break;
            case MotherArt.Egg:
            case MotherArt.Child:
                if (z.ActorId != 0) break; // The real escort is already rendered, with the same simulated flight.
                var p = warning && z.Moving ? MotherLerp(source, at(z.FromX, z.FromH + z.Height / 2), load) : target;
                if (z.Art == MotherArt.Egg && z.Age >= end)
                {
                    double burst = MotherEase((z.Age-end)/.38);
                    for(int part=-1;part<=1;part++) DrawMotherEgg(dc,new Point(p.X+part*burst*28,p.Y-burst*(1-burst)*45),.45,part*(15+burst*40),true);
                }
                else if (z.Art == MotherArt.Egg) DrawMotherEgg(dc, p, .7 + .3 * load, Math.Sin(z.Age * 7) * 9);
                else { dc.DrawEllipse(MotherGel, Outline, p, 22, 18); dc.DrawEllipse(MotherEgg, null, new Point(p.X-6,p.Y-4),3,4);dc.DrawEllipse(MotherEgg,null,new Point(p.X+6,p.Y-4),3,4); }
                if (!warning && z.Moving)
                {
                    double k = Math.Clamp(after / Math.Max(.01,z.Life),0,1);
                    var tail = at(z.FromX+(z.ToX-z.FromX)*Math.Max(0,k-.1),z.FromH+(z.ToH-z.FromH)*k+160*4*k*(1-k)+z.Height/2);
                    dc.PushOpacity(.35); MotherCord(dc, tail, p, 3, true); dc.Pop();
                }
                MotherImpact(dc, at(z.ToX,z.ToH), 35, z.Age-end); break;
            case MotherArt.Shell:
                for (int i=0;i<3;i++)
                {
                    double k=warning ? 0 : Math.Clamp(after/Math.Max(.01,z.Life)+i*.08,0,1);
                    double y=r.Height>180 ? r.Top+14+(r.Height-28)*k : target.Y+(i-1)*9;
                    DrawMotherEgg(dc,new Point(target.X+(i-1)*r.Width*.2,y),.6, (i-1)*28+Math.Sin(z.Age*5)*12,true);
                }
                MotherImpact(dc,at(z.X,z.H),z.Width*.65,after-z.Life*.8); break;
            case MotherArt.Hug:
                // The mother's own arms close. The lane only gets wake streaks and the landing splash.
                if (warning) { dc.DrawEllipse(null,MotherDash,at(z.Moving?z.ToX:z.X,z.H),Math.Min(55,z.Width/2),10); }
                else
                {
                    dc.PushOpacity(.45);
                    for(int side=-1;side<=1;side+=2) MotherCord(dc,at(z.X-side*22,z.H+15),at(z.X-side*35,z.H+40),side*9,true);
                    dc.Pop(); MotherImpact(dc,at(z.X,z.H),z.Width*.6,after);
                }
                break;
            case MotherArt.Ribbon:
                double y0=r.Top+r.Height*.5;
                var left=new Point(r.Left,y0);var right=new Point(r.Right,y0);
                MotherCord(dc,source,MotherLerp(left,right,Math.Clamp((source.X-r.Left)/Math.Max(1,r.Width),0,1)),Math.Sin(z.Age*4)*8);
                dc.PushOpacity(warning?.4+.6*load:1);
                MotherCord(dc,left,right,Math.Sin(z.Age*6)*Math.Min(8,r.Height*.15));dc.Pop();
                break;
            case MotherArt.Flood:
                Point foot=at(z.X,z.H+z.Height*.3);
                dc.PushOpacity(warning?.3+.4*load:1);
                MotherCord(dc,MotherEmission(s,MotherArt.Flood,at),foot,8,true);
                dc.DrawEllipse(MotherGel,MotherSoftRim,foot,z.Width*.5,z.Height*.35);
                dc.DrawEllipse(null,MotherImpactLine,new Point(foot.X-8,foot.Y-4),z.Width*.23,z.Height*.17);
                dc.Pop(); MotherImpact(dc,at(z.X,z.H),z.Width*.7,after);break;
            default: // Heartbeat and crying: coherent gel ripples across the exact unsafe floor band.
                dc.PushOpacity(warning?.25+.6*load:1);
                double rise=warning?load:1;
                for(double x=r.Left+24;x<r.Right;x+=60)
                {
                    var beat=new Point(x,r.Bottom-r.Height*.45);
                    dc.DrawEllipse(null,MotherLine,beat,25*rise,r.Height*.3*rise);
                    dc.DrawLine(MotherImpactLine,new Point(x-18,beat.Y),new Point(x-5,beat.Y));
                    dc.DrawLine(MotherImpactLine,new Point(x-5,beat.Y),new Point(x,beat.Y-12*rise));
                    dc.DrawLine(MotherImpactLine,new Point(x,beat.Y-12*rise),new Point(x+5,beat.Y));
                }
                dc.Pop();break;
        }
        dc.Pop();
    }
    private static void DrawMotherPuddle(DrawingContext dc, double x, double floor, double radius, double time)
    {
        var p=new Point(x,floor-3);
        dc.DrawEllipse(MotherInside,Halo,p,radius,7);
        dc.DrawEllipse(null,MotherLine,p,radius,7);
        dc.DrawEllipse(null,MotherCreamLine,new Point(x-radius*.3,floor-5),radius*.23,1.5);
        for(int i=-1;i<=1;i++) DrawMotherEgg(dc,new Point(x+i*radius*.45,floor-5),.22,i*30,true);
    }
}
