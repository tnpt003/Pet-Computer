using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

internal static class AppearanceVisualChecks
{
    private const int Size = 192;

    public static void Run(Action<bool, string> check)
    {
        bool edgesClear = true;
        string Render(PetModel pet, string coat, string accessory, string label)
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
        PetModel Pose(string pose)
        {
            var pet = new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1 };
            switch (pose)
            {
                case "sleeping": pet.Action = PetAction.Sleeping; pet.Emotions.EnterSleep(); pet.ActionTime = 1; break;
                case "happy": pet.Emotions.Attention(); pet.Emotions.Step(pet, 0.5); break;
                case "thrown": pet.Action = PetAction.Thrown; pet.Grounded = false; pet.VY = -700; break;
                case "dragging":
                    pet.Action = PetAction.Dragging; pet.Grounded = false; pet.Pickup.Begin(0, 0);
                    for (int frame = 0; frame < 90; frame++) pet.Pickup.Step(frame < 45 ? frame * 40 : (90 - frame) * 40, 0, 1.0 / 120);
                    break;
                case "left": pet.Facing = -1; break;
            }
            return pet;
        }

        string original = Render(Pose("idle"), "Orange", "None", "coat-default");
        check(Render(Pose("idle"), "Unknown", "Cape", "coat-unknown") == original &&
              Render(Pose("idle"), "Orange", "None", "coat-orange") == new Func<string>(() =>
              {
                  var plain = new PetRenderer(Pose("idle"));
                  plain.Measure(new Size(Size, Size)); plain.Arrange(new Rect(0, 0, Size, Size));
                  var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32); bitmap.Render(plain);
                  var pixels = new byte[Size * Size * 4]; bitmap.CopyPixels(pixels, Size * 4, 0);
                  return Convert.ToHexString(SHA256.HashData(pixels));
              })(),
            "Default and invalid appearance render exactly the original orange cat");

        var coats = PetPreferences.Coats.Select(coat => Render(Pose("idle"), coat, "None", "coat-" + coat.ToLowerInvariant())).ToArray();
        check(coats.Distinct().Count() == coats.Length, "Every coat renders differently");
        var accessories = PetPreferences.Accessories.Select(item => Render(Pose("idle"), "Orange", item, "accessory-" + item.ToLowerInvariant())).ToArray();
        check(accessories.Distinct().Count() == accessories.Length, "Every accessory renders differently");

        foreach (string pose in new[] { "idle", "sleeping", "happy", "thrown", "dragging", "left" })
            foreach (string coat in PetPreferences.Coats)
                foreach (string item in PetPreferences.Accessories)
                    Render(Pose(pose), coat, item, $"look-{pose}-{coat.ToLowerInvariant()}-{item.ToLowerInvariant()}");
        check(edgesClear, "All coats and accessories stay inside the window in every pose (incl. dragging)");
    }
}
