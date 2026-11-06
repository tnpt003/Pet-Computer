using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

internal static class MotherMotionChecks
{
    private sealed class Air(BattleSim sim) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc) => BattleFieldView.DrawMotherAir(dc, sim,
            (x,h)=>new Point(x,398-h),new Point(430,278),sim.Time);
    }
    public static void Run(Action<bool,string> check)
    {
        var data=BattleData.Settings;
        var timings=new List<double>();
        for(int move=0;move<9;move++)
        {
            var settings=new BattleSettings { CatMaxHp=100000, Enemies=data.EnemyTypes.ToArray(),
                Weapons=new[]{data.Weapon("sword") with { Damage=0,StunChance=0 }}, RageAuto=false,RageHitGain=0,RageHurtGain=0 };
            var sim=new BattleSim(settings,0,600,seed:48);sim.SetSky(390);
            var s=sim.AddSlime(270,data.Enemy("mother"));s.Set(SlimeState.Moving);s.BroodClock=9999;
            void Step(double dt)=>sim.Step(dt,430,true,true,new CatPose(430,120,0,0,true,false));
            Step(.016);var m=s.Mother!;
            if(move==3)
                for(int i=0;i<2;i++)
                { var child=sim.AddSlime(180+i*70,data.Enemy("fuser"));child.Set(SlimeState.Moving);child.SummonedBy=s.Id;child.Link=null; }
            m.Phase=move/3+1;m.Thresholds=3;m.NextWrap=9999;m.Bag.Clear();m.Bag.Add(move%3);m.Clock=0;
            Step(.016);
            double age=m.ActionAge, hp=s.Hp, clock=m.Clock;
            var pose=BattleFieldView.SampleMother(s);
            foreach(int facing in new[]{-1,1})
            {
                s.Facing=facing;
                for(int frame=0;frame<12;frame++)
                {
                    m.ActionAge=m.ActionEnd*frame/11;
                    var centre=BattleFieldView.MotherBowCentre(s);
                    var rotation=new RotateTransform(BattleFieldView.MotherBowAngle(s));
                    double scale=BattleFieldView.MotherBowScale(s);
                    // Bow cloth and outward tails stay above the eye line, including their rim.
                    var bottom=rotation.Transform(new Point(0,10*scale)).Y+centre.Y+2;
                    check(bottom < -34*.7-4,$"Mother move {move}, facing {facing}, frame {frame}: bow clears both eyes");
                }
            }
            s.Facing=1;m.ActionAge=age;
            for(int i=0;i<50;i++) BattleFieldView.SampleMother(s);
            check(m.ActionAge==age&&s.Hp==hp&&m.Clock==clock,$"Mother move {move}: render sampling never changes combat state");
            var anchor=BattleFieldView.MotherEmission(s,MotherArt.Hand,(x,h)=>new Point(x,-h),-1);
            check(Math.Abs(anchor.X-(s.X+(pose.Left.X*pose.SX+pose.Lean)*s.Width/46))<.001,
                $"Mother move {move}: arm emission follows the exact body pose");
            m.ActionAge=m.ActionWarn-.0001;var before=BattleFieldView.SampleMother(s);
            m.ActionAge=m.ActionWarn+.0001;var after=BattleFieldView.SampleMother(s);
            check((before.Left-after.Left).Length<.05&&(before.Right-after.Right).Length<.05&&Math.Abs(before.SX-after.SX)<.005,
                $"Mother move {move}: windup-to-release has no pose discontinuity");
            m.ActionAge=m.ActionEnd-.0001;before=BattleFieldView.SampleMother(s);
            m.ActionAge=m.ActionEnd+.0001;after=BattleFieldView.SampleMother(s);
            check((before.Left-after.Left).Length<.05&&(before.Right-after.Right).Length<.05,
                $"Mother move {move}: recovery starts continuously");
            m.ActionAge=age;
            var view=new Grid();view.Children.Add(new BattleFieldView(sim){Height=BattleFieldView.FieldHeight,VerticalAlignment=VerticalAlignment.Bottom});view.Children.Add(new Air(sim));
            view.Measure(new Size(600,400));view.Arrange(new Rect(0,0,600,400));view.UpdateLayout();
            var sheet=new DrawingVisual();
            using(var dc=sheet.RenderOpen())
            {
                dc.DrawRectangle(PetUi.Game.Panel,null,new Rect(0,0,1800,800));
                dc.DrawRectangle(PetUi.Surface,null,new Rect(0,800,1800,800));
                var samples=new[]{m.ActionWarn*.2,m.ActionWarn*.8,m.ActionWarn+.08,m.ActionEnd-.02,m.ActionEnd+.12,m.ActionEnd+.55};
                for(int frame=0;frame<samples.Length;frame++)
                {
                    while(m.ActionAge+1e-7<samples[frame]) Step(Math.Min(1.0/60,samples[frame]-m.ActionAge));
                    var watch=Stopwatch.StartNew();
                    view.InvalidateVisual();foreach(UIElement child in view.Children)child.InvalidateVisual();view.UpdateLayout();
                    var bitmap=new RenderTargetBitmap(600,400,96,96,PixelFormats.Pbgra32);bitmap.Render(view);watch.Stop();timings.Add(watch.Elapsed.TotalMilliseconds);
                    double x=frame%3*600,y=frame/3*400;
                    dc.DrawImage(bitmap,new Rect(x,y,600,400));dc.DrawImage(bitmap,new Rect(x,y+800,600,400));
                    var label=new FormattedText($"Move {move} · {m.ActionAge:0.00}s",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),13,PetUi.Game.Soft,1);
                    dc.DrawText(label,new Point(x+12,y+10));
                }
            }
            if(Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string folder)
            {
                var bitmap=new RenderTargetBitmap(1800,1600,96,96,PixelFormats.Pbgra32);bitmap.Render(sheet);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream=System.IO.File.Create(System.IO.Path.Combine(folder,$"mother-motion-{move}.png"));encoder.Save(stream);
            }
            sim.Paused=true;double frozen=m.ActionAge;Step(.1);
            check(m.ActionAge==frozen,$"Mother move {move}: Pause freezes visual action age");
        }
        timings.Sort();
        Console.WriteLine($"Mother render benchmark (54 snapshots, not real-time FPS): median {timings[timings.Count/2]:0.00}ms, p95 {timings[(int)(timings.Count*.95)]:0.00}ms, max {timings[^1]:0.00}ms");
    }
}
