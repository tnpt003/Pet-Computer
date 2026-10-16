using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

internal static class GestureVisualChecks
{
    private const int Size = 192;

    public static void Run(Action<bool, string> check)
    {
        bool edgesClear = true;
        string Render(PetModel pet, string label, string coat = "Orange", string accessory = "None")
        {
            var visual = new PetRenderer(pet) { Coat = coat, Accessory = accessory };
            visual.Measure(new Size(Size, Size)); visual.Arrange(new Rect(0, 0, Size, Size));
            var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            var pixels = new byte[Size * Size * 4]; bitmap.CopyPixels(pixels, Size * 4, 0);
            for (int i = 0; i < Size; i++)
                edgesClear &= pixels[i * Size * 4 + 3] == 0 && pixels[(i * Size + Size - 1) * 4 + 3] == 0 &&
                              pixels[i * 4 + 3] == 0 && pixels[((Size - 1) * Size + i) * 4 + 3] == 0;
            string? review = Environment.GetEnvironmentVariable("PET_RENDER_REVIEW");
            if (review != null)
            {
                Directory.CreateDirectory(review);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(review, label + ".png")); encoder.Save(stream);
            }
            return Convert.ToHexString(SHA256.HashData(pixels));
        }
        PetModel Pose(PetAction action, double time, int facing = 1)
        {
            var pet = new PetModel { Action = action, Grounded = true, Facing = facing, AnimationTime = 2 };
            pet.ActionTime = time;
            return pet;
        }

        string idle = Render(Pose(PetAction.Idle, 0), "gesture-idle");
        var gestures = new[] { PetAction.Kneading, PetAction.RollingOver, PetAction.ChasingTail, PetAction.Scratching, PetAction.Sneezing };
        var middles = new List<string>();
        foreach (var gesture in gestures)
        {
            double duration = PetSettings.GestureDuration(gesture);
            string name = gesture.ToString().ToLowerInvariant();
            string start = Render(Pose(gesture, 0), $"gesture-{name}-start");
            middles.Add(Render(Pose(gesture, gesture == PetAction.Sneezing ? 0.45 : duration / 2 + 0.1), $"gesture-{name}-mid"));
            for (int frame = 0; frame <= 12; frame++)
                foreach (int facing in new[] { 1, -1 })
                    foreach (string accessory in new[] { "None", "Hat" })
                        Render(Pose(gesture, duration * frame / 12, facing), $"gesture-{name}-{frame:00}-{(facing > 0 ? "r" : "l")}-{accessory.ToLowerInvariant()}", "Calico", accessory);
            check(start == idle, $"{gesture} starts from the idle pose without a visible pop");
        }
        check(middles.Distinct().Count() == gestures.Length && !middles.Contains(idle), "Each gesture has its own distinct pose");
        check(edgesClear, "Gestures (incl. rolling over with a hat, both directions) stay inside the window");

        var climbFrames = new List<string>();
        for (int frame = 0; frame < 6; frame++)
            foreach (int facing in new[] { 1, -1 })
                foreach (string accessory in new[] { "None", "Hat" })
                {
                    var climber = Pose(PetAction.Climbing, 0, facing); climber.AnimationTime = 2 + frame * 0.12;
                    climbFrames.Add(Render(climber, $"climbing-{frame}-{(facing > 0 ? "r" : "l")}-{accessory.ToLowerInvariant()}", "Calico", accessory));
                }
        check(edgesClear && !climbFrames.Contains(idle) && climbFrames.Distinct().Count() > 4,
            "Climbing has its own animated pose (paws above the head, tail hanging) inside the window, both sides, with a hat");
        RenderClimbScene();

        var angry = Pose(PetAction.Idle, 1);
        angry.Emotions.AggressiveInteraction(); angry.Emotions.Step(angry, 0.1); angry.Emotions.Step(angry, 0.5); // Switch, then fade in.
        var calm = Pose(PetAction.Idle, 1);
        check(Render(angry, "gesture-angry-ears") != Render(calm, "gesture-calm"), "Angry pose flattens the ears and lashes the tail");
        var interrupted = Pose(PetAction.Kneading, 1);
        interrupted.Emotions.EnterSleep(); interrupted.Action = PetAction.Sleeping;
        var asleep = Pose(PetAction.Sleeping, 0); asleep.Emotions.EnterSleep();
        asleep.ActionTime = interrupted.ActionTime; asleep.AnimationTime = interrupted.AnimationTime;
        check(Render(interrupted, "gesture-sleep-a") == Render(asleep, "gesture-sleep-b"), "No gesture art leaks into the sleeping pose");
    }

    // Review only: the climbing pose against a dark window's side, as on the desktop.
    private static void RenderClimbScene()
    {
        string? review = Environment.GetEnvironmentVariable("PET_RENDER_REVIEW");
        if (review == null) return;
        var scene = new DrawingVisual();
        using (var dc = scene.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x9C, 0xB8, 0xD2)), null, new Rect(0, 0, 3 * Size, Size));
            for (int i = 0; i < 3; i++)
            {
                double edge = i * Size + PickupAnimation.Padding + 56 + PetSettings.ClimbOffset;
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)), null, new Rect(edge, 10, (i + 1) * Size - edge, Size), 8, 8);
                var pet = new PetModel { Action = PetAction.Climbing, Facing = 1, AnimationTime = 2 + i * 0.12 };
                var visual = new PetRenderer(pet) { Accessory = i == 1 ? "Hat" : "None", Coat = i == 2 ? "Calico" : "Orange" };
                visual.Measure(new Size(Size, Size)); visual.Arrange(new Rect(0, 0, Size, Size));
                var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
                dc.DrawImage(bitmap, new Rect(i * Size, 0, Size, Size));
            }
        }
        var output = new RenderTargetBitmap(3 * Size, Size, 96, 96, PixelFormats.Pbgra32); output.Render(scene);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(output));
        using var stream = File.Create(Path.Combine(review, "climbing-scene.png")); encoder.Save(stream);
    }
}
