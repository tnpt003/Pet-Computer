using DesktopPet;

// Reads public puzzle/telegraph state; uses real held weapon hits and falling/throwing CatPose contacts.
internal sealed class MutantPuzzlePlayer
{
    double height=BattleSim.CatStandHeight, flight, vx, vy;
    public bool Step(BattleSim sim,ref double cat,double dt,bool careful)
    {
        var boss=sim.Slimes.FirstOrDefault(s=>s.Targetable&&s.Mutant!=null);
        if(boss?.Mutant is not { } m)return false;
        if(sim.MutantNoise)sim.SetMutantPointer(cat+Math.Sin(sim.Time*20)*30,height);
        if(sim.MutantForcedFall)
        {
            height=Math.Max(BattleSim.CatStandHeight,height-600*dt);
            sim.Step(dt,cat,height>BattleSim.CatStandHeight,height>BattleSim.CatStandHeight,
                height>BattleSim.CatStandHeight?new CatPose(cat,height,0,600,false,true):null);return true;
        }
        if(sim.CatStunned)return false;
        if(flight>0)
        {
            flight-=dt;cat=Math.Clamp(cat+vx*dt,sim.Left+25,sim.Right-25);height=Math.Max(56,height-vy*dt);vy+=900*dt;
            sim.Step(dt,cat,true,true,new CatPose(cat,height,vx,vy,false,true));return true;
        }
        double x=cat,h=350;bool drop=false;
        switch(m.Mode)
        {
            case MutantMode.Bumps:
                if(m.NextBump>=m.Bumps.Count)return false;
                var b=m.Bumps[m.NextBump];x=b.X;h=b.Side==0?150:b.H;
                if(b.Side==0&&Math.Abs(cat-x)<25)
                { h=56;drop=true; }
                break;
            case MutantMode.Core:x=boss.X;h=boss.Height*.5;break;
            case MutantMode.Ping:
                if(m.Projectile&&m.ProjectileAge>.15)
                {
                    x=m.CoreX;h=m.CoreH;
                    if(Math.Abs(x-cat)<55&&Math.Abs(h-height)<45)
                    { flight=.2;vx=m.AimX>=m.FromX?-700:700;vy=-100; }
                }
                break;
            case MutantMode.Combat:
                if(!careful)return false;
                double catX=cat;
                if(!m.Zones.Any(z=>z.Age<z.Warn+z.Life&&z.Age>z.Warn*.5&&Math.Abs(catX-z.X)<z.W/2+40&&height+38>=z.H&&height-50<=z.H+z.Height))
                { height=56;return false; }
                goto case MutantMode.AllIn;
            case MutantMode.AllIn:
                double best=double.PositiveInfinity;
                for(double cx=sim.Left+60;cx<sim.Right-40;cx+=100)
                    for(double ch=100;ch<sim.SkyHeight-40;ch+=90)
                    {
                        bool unsafeSpot=m.Zones.Any(z=>z.Age<z.Warn+z.Life&&Math.Abs(cx-z.X)<z.W/2+40&&ch+38>=z.H&&ch-50<=z.H+z.Height);
                        double distance=Math.Abs(cx-cat)+Math.Abs(ch-height);
                        if(!unsafeSpot&&distance<best){best=distance;x=cx;h=ch;}
                    }
                break;
        }
        cat+=Math.Clamp(x-cat,-700*dt,700*dt);height+=Math.Clamp(h-height,-600*dt,600*dt);
        sim.Step(dt,cat,true,true,new CatPose(cat,height,0,drop?600:0,!drop,drop));
        return true;
    }
}
