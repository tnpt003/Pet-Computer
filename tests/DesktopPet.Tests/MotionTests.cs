using DesktopPet;
using DesktopPet.AI;
internal static class MotionTests
{
    public static void Run(Action<bool,string> check)
    {
        var slow = new PetModel { Action=PetAction.Walking, Grounded=true, VX=18 };
        var fast = new PetModel { Action=PetAction.Walking, Grounded=true, VX=72 };
        for(int i=0;i<30;i++) { slow.Motion.Step(slow,0.01); fast.Motion.Step(fast,0.01); }
        check(Math.Abs(fast.Motion.WalkPhase - slow.Motion.WalkPhase*4)<0.00001,
            "Foot cycle tracks actual speed, including slow acceleration");
        check(slow.Motion.WalkBlend < fast.Motion.WalkBlend, "Slow walking has smaller steps and bob");
        double phase = fast.Motion.WalkPhase; fast.VX=0;
        for(int i=0;i<100;i++) fast.Motion.Step(fast,0.01);
        check(fast.Motion.WalkPhase==phase && fast.Motion.WalkBlend<0.001, "Stopped walking stops cycling and settles its stride");
        var pet=new PetModel { Grounded=true, X=300, Y=928, Facing=1 };
        pet.Motion.Reset(pet); pet.Action=PetAction.Sitting; pet.Motion.Step(pet,0.016);
        check(pet.Motion.RestBlend>0 && pet.Motion.RestBlend<0.2 && pet.X==300 && pet.Y==928,
            "Sitting eases in without changing the floor anchor");
        for(int i=0;i<70;i++) pet.Motion.Step(pet,0.016);
        check(pet.Motion.RestBlend>0.99,"Rest pose reaches its target");
        pet.Action=PetAction.Waking; pet.Motion.Step(pet,0.016);
        check(pet.Motion.RestBlend>0.8 && pet.Motion.RestBlend<1,"Waking rises gradually out of the sleeping pose");
        pet.Action=PetAction.Walking; pet.VX=72; pet.Facing=-1; pet.Motion.Reset(pet); pet.Facing=1; pet.Motion.Reset(pet); pet.Facing=-1;
        pet.Motion.Step(pet,0.02);
        check(pet.Motion.Facing==1,"Walking does not flip while still moving in the old direction");
        pet.VX=-12; pet.Motion.Step(pet,0.02);
        check(pet.Motion.Facing==1 && Math.Abs(pet.Motion.FacingScale)<1,"Turn begins with a small compression before facing changes");
        for(int i=0;i<12;i++) pet.Motion.Step(pet,0.02);
        check(pet.Motion.Facing==-1 && Math.Abs(pet.Motion.FacingScale)==1,"Turn completes without collapsing the body");
        pet.VX=0; pet.Action=PetAction.Idle;
        for(int i=0;i<200;i++) pet.Motion.Step(pet,0.01);
        check(Math.Abs(pet.Motion.HeadTilt)<0.02 && Math.Abs(pet.Motion.TailLag)<0.02,"Head and tail inertia settle after stopping");
        var settings=new PetSettings(); var behavior=new PetBehavior(settings);
        var turnPet=new PetModel { X=400,Y=928,Grounded=true,Facing=1,Action=PetAction.Walking,VX=72,SmoothedWalkVelocity=72 };
        var turnPhysics=new PetPhysics(settings);
        behavior.TryApply(turnPet,new("WALK_LEFT",3,"content"));
        behavior.Step(turnPet,0.01); turnPhysics.Step(turnPet,new Area(0,0,1920,1040),0.01);
        check(turnPet.VX>0 && turnPet.VX<72 && turnPet.Facing==-1,"Direction request first brakes existing walking velocity");
        for(int i=0;i<80;i++) { behavior.Step(turnPet,0.01); turnPhysics.Step(turnPet,new Area(0,0,1920,1040),0.01); }
        check(turnPet.VX< -60,"Walk reversal preserves intent until acceleration changes direction");
        pet.Emotions.Reset(); pet.Action=PetAction.Idle; pet.Grounded=true;
        check(behavior.TryApply(pet,new("JUMP",2,"playful")) && pet.Action==PetAction.PreparingJump,"AI jump starts a grounded preparation");
        behavior.Step(pet,0.08);
        check(pet.Grounded && pet.VY==0 && !DecisionGuard.CanApply(1,1,0.1,false,pet),"Preparation holds position and rejects delayed AI");
        pet.Action=PetAction.Dragging; behavior.Reset(); behavior.Step(pet,0.2);
        check(pet.Action==PetAction.Dragging && pet.VY==0,"Pickup cancels preparation without a delayed impulse");
        pet.Action=PetAction.Idle; behavior.TryApply(pet,new("JUMP",2,"playful")); pet.Emotions.EnterSleep(); behavior.Step(pet,0.2);
        check(pet.Action==PetAction.Sleeping && pet.VY==0,"Sleep lock cancels preparation");
        pet.Emotions.Reset(); pet.Action=PetAction.Idle; behavior.TryApply(pet,new("JUMP",2,"playful")); behavior.Step(pet,0.17);
        check(pet.Action==PetAction.Jumping && pet.VY==-settings.JumpSpeed && !pet.Grounded,"Prepared jump uses original impulse");
        pet.Action=PetAction.Thrown; pet.Y=927; pet.VY=500; pet.VX=100; pet.Grounded=false; pet.Motion.Reset(pet);
        new PetPhysics(settings).Step(pet,new Area(0,0,1920,1040),0.01);
        double bouncedVY=pet.VY, bouncedVX=pet.VX; pet.Motion.Step(pet,0.04);
        check(pet.Motion.LandingSquash>0 && pet.VY==bouncedVY && pet.VX==bouncedVX && bouncedVY<0,
            "Landing squash observes impact without changing bounce velocity");
        double rebound=0; for(int i=0;i<200;i++) { pet.Motion.Step(pet,0.01); rebound=Math.Min(rebound,pet.Motion.LandingSquash); }
        check(rebound<0 && pet.Motion.LandingSquash==0,"Landing squash rebounds into a stretch, then fully recovers");
        // One landing rule for the cat and the keepsake (PetImpact.cs): the same spring kick, dust and sound.
        var keepsake=new KeepsakeMotion{Height=300};
        var cat=new PetModel{Action=PetAction.Thrown,VY=800}; cat.Motion.Reset(cat);
        cat.Motion.Contact(800,cat); while(keepsake.Height>0||keepsake.VY>0) keepsake.Step(0.001,0,1000);
        var catSpring=typeof(PetMotion).GetField("spring",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(cat.Motion) as ImpactSpring;
        check(catSpring!=null && Math.Abs(catSpring.SquashV-Landing.SquashKick(800))<1e-9 && keepsake.Landed>700,"The cat and the keepsake share the landing spring kick");
        check(cat.Motion.Landing.Dust==0 && cat.Motion.Landing.TakeSound() && !cat.Motion.Landing.TakeSound(),"A hard landing raises dust and asks for the sound once");
        cat.Motion.Contact(500,cat);
        check(!cat.Motion.Landing.TakeSound(),"The bounce right after does not sound again");
        for(int i=0;i<5;i++) cat.Motion.Step(cat,0.1); cat.Motion.Contact(500,cat);
        check(cat.Motion.Landing.TakeSound(),"A later landing sounds again");
        var sleeper=new PetModel{Action=PetAction.Sleeping}; sleeper.Motion.Reset(sleeper); sleeper.Motion.Contact(900,sleeper);
        check(sleeper.Motion.LandingSquash==0 && sleeper.Motion.Landing.Dust<0 && !sleeper.Motion.Landing.TakeSound(),"A sleeping cat lands softly: no squash, dust or sound");
        var walled=new PetModel{Action=PetAction.Thrown,X=1800,VX=900,Y=300,Grounded=false}; walled.Motion.Reset(walled);
        new PetPhysics(settings).Step(walled,new Area(0,0,1920,1040),0.01); walled.Motion.Step(walled,0.05);
        check(walled.VX<0 && walled.Motion.LandingTilt<0,"Bouncing off a screen side tips the cat back without changing the bounce");
    }
}
