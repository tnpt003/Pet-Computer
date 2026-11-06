using DesktopPet;

internal static class ContentLearningTests
{
    static BattleSim Field(string lesson,bool guide,string[]? pool=null)
    {
        var data=BattleData.Settings;
        var dummy=new EnemyType { Id="dummy",MaxHp=100000,Damage=0,Speed=0,AttackCooldown=999 };
        var settings=new BattleSettings { FirstWaveDelay=0,CatMaxHp=100000,Apps=data.Apps,Enemies=new[]{dummy},Weapons=new[]{data.Weapon("sword") with { Damage=0,StunChance=0 }},RageAuto=false };
        var stage=new StageSpec { Id="learning",Threat=2,NewAppsFrom=1,Lesson=lesson,MalwareKinds=pool,MaxMalware=4,
            Waves=new[]{new WaveSpec { Groups=new[]{new WaveGroup { Enemy="dummy",Count=1 }} }} };
        var sim=new BattleSim(settings,0,1600,seed:17,stage:stage);sim.SetLearningGuide(guide);return sim;
    }
    static void RunFor(BattleSim sim,double seconds)
    {for(int i=0;i<seconds*60;i++)sim.Step(1.0/60,800,false);}
    public static void Run(Action<bool,string> check)
    {
        using(var embedded=typeof(BattleData).Assembly.GetManifestResourceStream("DesktopPet.battle.json")!) BattleData.Parse(embedded);
        foreach(var lesson in new[]{"captcha","battery","cursor","screenshot"})
        {
            var sim=Field(lesson,true);RunFor(sim,8);
            bool visible=lesson switch { "captcha"=>sim.Captcha!=null,"battery"=>sim.BatteryLow,"cursor"=>sim.CursorFrozen,_=>sim.Desktop.Any(a=>a.Kind==BattleAppKind.Screenshot&&a.Charge>=1) };
            check(visible&&sim.LearningBusy,$"Guided {lesson} lesson appears without a random draw and waits for user action");
            double time=sim.Time;sim.Paused=true;RunFor(sim,1);
            check(sim.Time==time&&sim.LearningBusy,$"Pause freezes {lesson} lesson");sim.Paused=false;
            if(lesson=="battery")sim.PlugIn();
            else if(lesson=="cursor")sim.ResumeCursor();
            else if(lesson=="screenshot")sim.UseApp(sim.Desktop.First(a=>a.Kind==BattleAppKind.Screenshot).Id,800);
            else {sim.SetLearningGuide(false);check(!sim.LearningBusy,"Skipping guide releases lesson gating without deleting the active captcha");continue;}
            RunFor(sim,.1);check(sim.LessonCompleted&&!sim.LearningBusy,$"Solving {lesson} lesson releases ordinary combat");
            check(sim.Desktop.Count<=5,"Lessons never add a sixth app icon");
        }
        var off=Field("battery",false);RunFor(off,5);check(!off.BatteryLow&&!off.LearningBusy,"Disabled guide never forces its lesson");
        var limited=Field("battery",false,new[]{"ads"});RunFor(limited,60);
        check(!limited.BatteryLow&&!limited.CursorFrozen&&limited.Captcha==null,"Stage malware pool excludes later mechanics even after many random draws");
        var data=BattleData.Settings;var stages=data.Campaign!.Chapters[1].Stages;
        check(stages.Select(s=>s.UnlocksSkill).SequenceEqual(new[]{"fan","laser","grabber","scissors","spray"}),"Chapter 2 teaches exactly the five new HUD skills in order");
        check(stages.Take(4).Select(s=>s.Lesson).SequenceEqual(new[]{"screenshot","captcha","battery","cursor"}),"Chapter 2 app lessons follow a deterministic introduction order");
        check(!stages[3].Waves.SelectMany(w=>w.Groups).Any(g=>g.Enemy=="mother")&&stages[4].Keepsake?.Id=="zip_plush","Chapter 2 finishes with ZIP while preserving the earned trophy ID");
        var endless=data.Endless!;
        check(endless.Stage().MaxMalware==4&&new EndlessSpec().Stage().MaxMalware==null,"Infinity forwards its explicit malware cap and preserves old custom fallback");
        check(data.Expedition!.Route("dump_trail")!.KeepRuns.Contains(17),"Existing r17 expedition runs remain resumable under the new content");
    }
}
