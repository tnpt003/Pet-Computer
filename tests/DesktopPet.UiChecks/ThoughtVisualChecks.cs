using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

internal static class ThoughtVisualChecks
{
    private const int Size = 192;

    public static void Run(Action<bool, string> check)
    {
        bool edgesClear = true;
        string Render(PetModel pet, string label)
        {
            var visual = new PetRenderer(pet);
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
        PetModel Pet(int facing = 1) => new() { Action = PetAction.Idle, Grounded = true, Facing = facing, AnimationTime = 1, ActionTime = 1 };

        string plain = Render(Pet(), "thought-none");
        var hashes = new List<string>();
        foreach (var kind in new[] { ThoughtKind.Hungry, ThoughtKind.Bored, ThoughtKind.Sleepy, ThoughtKind.Love })
        {
            var pet = Pet();
            pet.Thought.TryShow(kind, pet);
            pet.Thought.Step(pet, 0.6); // Fully faded in.
            hashes.Add(Render(pet, "thought-" + kind.ToString().ToLowerInvariant()));
        }
        check(hashes.Distinct().Count() == 4 && !hashes.Contains(plain), "Four thought bubbles render distinct icons");
        var left = Pet(-1);
        left.Thought.TryShow(ThoughtKind.Love, left); left.Thought.Step(left, 0.6);
        Render(left, "thought-love-facing-left");
        var fading = Pet();
        fading.Thought.TryShow(ThoughtKind.Hungry, fading); fading.Thought.Step(fading, 0.05);
        check(Render(fading, "thought-fade-in") != hashes[0], "Bubble fades in instead of popping");
        check(edgesClear, "Thought bubbles stay inside the transparent window surface");
        var sleeping = Pet();
        sleeping.Thought.TryShow(ThoughtKind.Sleepy, sleeping);
        sleeping.Thought.Step(sleeping, 0.6);
        sleeping.Action = PetAction.Sleeping; sleeping.Emotions.EnterSleep();
        var asleep = Pet(); asleep.Action = PetAction.Sleeping; asleep.Emotions.EnterSleep();
        check(Render(sleeping, "thought-sleeping") == Render(asleep, "thought-sleeping-plain"),
            "A bubble is never drawn over the sleeping pose");
    }
}
