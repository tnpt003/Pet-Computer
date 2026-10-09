using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

internal static class EmotionVisualChecks
{
    private const int Size = 192;
    public static void Run(Action<bool, string> check)
    {
        PetModel Make(PetEmotion emotion)
        {
            var pet = new PetModel { Action = PetAction.Idle, Grounded = true, AnimationTime = 1, ActionTime = 1 };
            switch (emotion)
            {
                case PetEmotion.Happy: pet.Emotions.Attention(); break;
                case PetEmotion.Angry: pet.Emotions.AggressiveInteraction(); break;
                case PetEmotion.Sad: pet.Emotions.Step(pet, 301); break;
                case PetEmotion.Sleepy: pet.Emotions.Step(pet, 121); break;
                case PetEmotion.Surprised:
                    pet.Emotions.Step(pet, 0.1, -200, 50);
                    pet.Emotions.Step(pet, 0.05, 56, 50);
                    break;
                case PetEmotion.Dizzy:
                    pet.Action = PetAction.Dragging;
                    pet.Pickup.Begin(0, 0);
                    for (int i = 1; i <= 120; i++) pet.Pickup.Step(0, i * 20, 1.0 / 120);
                    break;
            }
            for (int i = 0; i < 30; i++) pet.Emotions.Step(pet, 1.0 / 60);
            return pet;
        }
        RenderTargetBitmap Render(PetModel model)
        {
            var visual = new PetRenderer(model);
            visual.Measure(new Size(Size, Size));
            visual.Arrange(new Rect(0, 0, Size, Size));
            var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            return bitmap;
        }
        string Hash(BitmapSource frame)
        {
            var bytes = new byte[Size * Size * 4];
            frame.CopyPixels(bytes, Size * 4, 0);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        var emotions = new[] { PetEmotion.Happy, PetEmotion.Angry, PetEmotion.Sad,
            PetEmotion.Surprised, PetEmotion.Sleepy, PetEmotion.Dizzy };
        var frames = emotions.Select(emotion => Render(Make(emotion))).ToArray();
        check(frames.Select(Hash).Distinct().Count() == 6, "All six emotions render distinct readable expressions");
        var locked = Make(PetEmotion.Sleepy);
        string lockedFrame = Hash(Render(locked));
        locked.Emotions.Attention();
        locked.Emotions.AggressiveInteraction();
        locked.Emotions.ApplyAiHint("sad");
        check(Hash(Render(locked)) == lockedFrame, "Background reactions do not replace the locked sleep rendering");
        var drowsy = Make(PetEmotion.Sleepy);
        var asleep = Make(PetEmotion.Neutral);
        asleep.Action = PetAction.Sleeping;
        asleep.ActionTime = drowsy.ActionTime;
        byte[] LowerBody(PetModel model)
        {
            var pixels = new byte[112 * 32 * 4];
            Render(model).CopyPixels(new Int32Rect(40, 120, 112, 32), pixels, 112 * 4, 0);
            return pixels;
        }
        check(LowerBody(drowsy).SequenceEqual(LowerBody(asleep)),
            "Inactivity Sleepy settles into the sleeping body pose instead of standing");
        string sleepyFrame = Hash(Render(drowsy));
        drowsy.Action = PetAction.Walking;
        drowsy.ActionTime = 1; // Compare the same Z animation phase.
        check(Hash(Render(drowsy)) == sleepyFrame, "Sleepy suppresses walking strides even during state transitions");
        var neutral = Make(PetEmotion.Neutral);
        string before = Hash(Render(neutral));
        neutral.Emotions.Attention();
        check(Hash(Render(neutral)) == before, "Emotion activation does not abruptly change the first rendered frame");
        neutral.Emotions.Step(neutral, 0.1);
        neutral.Emotions.Step(neutral, 0.1);
        neutral.Emotions.Step(neutral, 0.1);
        check(Hash(Render(neutral)) != before, "Emotion fades into its expression over subsequent frames");
        foreach (var emotion in emotions.Where(e => e != PetEmotion.Dizzy))
        {
            var emotional = Make(emotion);
            var plain = Make(PetEmotion.Neutral);
            emotional.Action = plain.Action = PetAction.Dragging;
            check(Hash(Render(emotional)) == Hash(Render(plain)), $"{emotion} facial/body effects do not alter the dangling pose");
        }
        var sheet = new DrawingVisual();
        using (var dc = sheet.RenderOpen())
        {
            Brush[] backgrounds = { Brushes.WhiteSmoke, new SolidColorBrush(Color.FromRgb(22, 25, 40)),
                new LinearGradientBrush(Colors.DodgerBlue, Colors.MediumVioletRed, 45) };
            for (int row = 0; row < backgrounds.Length; row++)
            for (int column = 0; column < emotions.Length; column++)
            {
                double x = column * Size, y = row * (Size + 26);
                dc.DrawRectangle(backgrounds[row], null, new Rect(x, y, Size, Size + 26));
                var label = new FormattedText(emotions[column].ToString(), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 14, row == 0 ? Brushes.Black : Brushes.White, 1);
                dc.DrawText(label, new Point(x + 14, y + 5));
                dc.DrawImage(frames[column], new Rect(x, y + 26, Size, Size));
            }
        }
        var output = new RenderTargetBitmap(Size * 6, (Size + 26) * 3, 96, 96, PixelFormats.Pbgra32);
        output.Render(sheet);
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string directory = Path.Combine(root, "artifacts", "visual-review");
        Directory.CreateDirectory(directory);
        var treatSheet = new DrawingVisual();
        var treatHashes = new System.Collections.Generic.HashSet<string>();
        using (var dc = treatSheet.RenderOpen())
        {
            double[] stages = { 0.4, 1.2, 2.1, 3.2, 3.8 };
            for (int i = 0; i < stages.Length; i++)
            {
                var model = new PetModel { Grounded = true, Action = PetAction.Sitting,
                    ActionTime = stages[i], AnimationTime = 1, TreatRemaining = PetModel.TreatDuration - stages[i] };
                var frame = Render(model); treatHashes.Add(Hash(frame));
                dc.DrawRectangle(i % 2 == 0 ? Brushes.WhiteSmoke : Brushes.SlateGray, null, new Rect(i * Size, 0, Size, Size));
                dc.DrawImage(frame, new Rect(i * Size, 0, Size, Size));
            }
        }
        check(treatHashes.Count == 5, "Treat reveal, reach, bites and consumption have distinct rendered stages");
        var treatOutput = new RenderTargetBitmap(Size * 5, Size, 96, 96, PixelFormats.Pbgra32);
        treatOutput.Render(treatSheet);
        var treatEncoder = new PngBitmapEncoder(); treatEncoder.Frames.Add(BitmapFrame.Create(treatOutput));
        using (var treatFile = File.Create(Path.Combine(directory, "treat-stages.png"))) treatEncoder.Save(treatFile);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(output));
        using var file = File.Create(Path.Combine(directory, "emotions.png"));
        encoder.Save(file);
    }
}
