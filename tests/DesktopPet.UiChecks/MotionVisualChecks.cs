using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;
internal static class MotionVisualChecks
{
    public static void Run(Action<bool,string> check)
    {
        bool clear=true;
        string Render(PetModel pet,string label)
        {
            var visual=new PetRenderer(pet); visual.Measure(new Size(192,192)); visual.Arrange(new Rect(0,0,192,192));
            var bitmap=new RenderTargetBitmap(192,192,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
            byte[] pixels=new byte[192*192*4]; bitmap.CopyPixels(pixels,192*4,0);
            for(int i=0;i<192;i++)
                clear &= pixels[(i*192)*4+3]==0 && pixels[(i*192+191)*4+3]==0 && pixels[i*4+3]==0 && pixels[((191*192)+i)*4+3]==0;
            string? review=Environment.GetEnvironmentVariable("PET_RENDER_REVIEW");
            if(review!=null) { Directory.CreateDirectory(review); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream=File.Create(Path.Combine(review,label+".png")); encoder.Save(stream); }
            return Convert.ToHexString(SHA256.HashData(pixels));
        }
        var pet=new PetModel { Grounded=true,AnimationTime=1 };
        pet.Motion.Reset(pet); string idle=Render(pet,"motion-idle");
        pet.Action=PetAction.Sitting;
        check(Render(pet,"motion-sit-start")==idle,"First sitting frame preserves the previous body pose");
        pet.Motion.Step(pet,0.1); string midway=Render(pet,"motion-sit-middle");
        for(int i=0;i<12;i++) pet.Motion.Step(pet,0.05);
        check(midway!=idle && midway!=Render(pet,"motion-sit-end"),"Sitting renders an intermediate pose before settling");
        pet.Action=PetAction.Sleeping; pet.Emotions.EnterSleep(); pet.Motion.Step(pet,0.033);
        Render(pet,"motion-sleep");
        pet.Emotions.Reset(); pet.Action=PetAction.Waking; pet.ActionTime=0.4; pet.Motion.Step(pet,0.033);
        Render(pet,"motion-wake");
        pet.Action=PetAction.PreparingJump; pet.ActionTime=0.12; pet.Motion.Step(pet,0.1);
        string crouch=Render(pet,"motion-prejump");
        pet.Action=PetAction.Jumping; pet.Grounded=false; pet.VY=-580; pet.Motion.Step(pet,0.05);
        check(crouch!=Render(pet,"motion-flight"),"Jump preparation and flight have distinct poses");
        pet.Motion.Contact(650); pet.VY=0; pet.Grounded=true; pet.Action=PetAction.Idle; pet.Motion.Step(pet,0.08);
        string contact=Render(pet,"motion-contact");
        for(int i=0;i<30;i++)pet.Motion.Step(pet,0.01);
        check(contact!=Render(pet,"motion-contact-settled"),"Landing compression visibly recovers");
        pet.Motion.Contact(1200,pet); pet.Motion.Step(pet,0.08);
        string wince=Render(pet,"motion-landing-wince");
        for(int i=0;i<60;i++)pet.Motion.Step(pet,0.01);
        check(wince!=contact && wince!=Render(pet,"motion-landing-wince-settled"),"A hard landing winces (eyes squeezed, ears flat, tail up), then settles");
        pet.Action=PetAction.Walking; pet.VX=72;
        for(int i=0;i<20;i++)pet.Motion.Step(pet,0.016);
        string moving=Render(pet,"motion-walk"); pet.Motion.Step(pet,0.1);
        check(moving!=Render(pet,"motion-walk-next"),"Runtime speed-driven gait advances visible steps");
        pet.Facing=-1; pet.VX=-10;
        for(int i=0;i<12;i++) { pet.Motion.Step(pet,0.016); Render(pet,"motion-turn-"+i); }
        check(clear,"Motion transition frames keep the full artwork clear of transparent surface edges");
    }
}
