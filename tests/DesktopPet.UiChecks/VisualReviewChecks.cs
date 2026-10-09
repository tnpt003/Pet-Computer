using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

internal static class VisualReviewChecks
{
    private const int Size = 192;
    private static RenderTargetBitmap Render(PetModel model)
    {
        var visual = new PetRenderer(model);
        visual.Measure(new Size(Size, Size));
        visual.Arrange(new Rect(0, 0, Size, Size));
        var bitmap = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }
    private static byte[] Pixels(BitmapSource bitmap)
    {
        var bytes = new byte[Size * Size * 4];
        bitmap.CopyPixels(bytes, Size * 4, 0);
        return bytes;
    }

    public static void Run(Action<bool, string> check)
    {
        var idle = new PetModel { Action = PetAction.Idle, AnimationTime = 1 };
        var picked = new PetModel { Action = PetAction.Dragging, AnimationTime = 1 };
        picked.Pickup.Begin(0, 0);
        check(Pixels(Render(idle)).SequenceEqual(Pixels(Render(picked))),
            "First pickup frame is pixel-identical to idle before pose easing begins");
        bool clearEdges = true;
        for (int frame = 0; frame < 120; frame++)
        {
            picked.Pickup.Step(frame < 60 ? frame * 40 : (120 - frame) * 40, 0, 1.0 / 120);
            byte[] pixels = Pixels(Render(picked));
            for (int i = 0; i < Size; i++)
                clearEdges &= pixels[i * 4 + 3] == 0 && pixels[((Size - 1) * Size + i) * 4 + 3] == 0 &&
                    pixels[i * Size * 4 + 3] == 0 && pixels[(i * Size + Size - 1) * 4 + 3] == 0;
        }
        check(clearEdges, "All pickup/reversal frames keep full artwork clear of surface edges");

        var sleeping = new PetModel { Action = PetAction.Sleeping, AnimationTime = 1, ActionTime = 0.8 };
        var dizzy = new PetModel { Action = PetAction.Dragging, AnimationTime = 1 };
        dizzy.Pickup.Begin(0, 0);
        for (int i = 1; i <= 120; i++) dizzy.Pickup.Step(0, i * 20, 1.0 / 120);
        // Both effect areas must contain opaque light and dark pixels, not just a
        // low-opacity single-color overlay that disappears on a matching background.
        foreach (var model in new[] { dizzy, sleeping })
        {
            var pixels = Pixels(Render(model));
            int light = 0, dark = 0;
            int fromX = model == dizzy ? 45 : 118, toX = model == dizzy ? 150 : 155;
            int fromY = model == dizzy ? 25 : 35, toY = model == dizzy ? 49 : 70;
            for (int y = fromY; y < toY; y++)
            for (int x = fromX; x < toX; x++)
            {
                int index = (y * Size + x) * 4;
                if (pixels[index + 3] < 230) continue;
                if (pixels[index] > 210 && pixels[index + 1] > 210 && pixels[index + 2] > 210) light++;
                if (pixels[index] < 110 && pixels[index + 1] < 110 && pixels[index + 2] < 110) dark++;
            }
            check(light > 10 && dark > 10, $"{model.Action} indicators contain opaque light borders and dark outlines");
        }

        // Save a visual review sheet against backgrounds representative of the video
        // and high/low luminance desktops. No external artwork packages are needed.
        var sheet = new DrawingVisual();
        using (var dc = sheet.RenderOpen())
        {
            Brush[] backgrounds = { Brushes.WhiteSmoke, new SolidColorBrush(Color.FromRgb(22, 25, 40)),
                new LinearGradientBrush(Colors.DodgerBlue, Colors.MediumVioletRed, 45) };
            string[] labels = { "Light background", "Dark background", "Color background" };
            for (int column = 0; column < 3; column++)
            {
                dc.DrawRectangle(backgrounds[column], null, new Rect(column * Size, 0, Size, Size * 2 + 30));
                dc.DrawImage(Render(dizzy), new Rect(column * Size, 30, Size, Size));
                dc.DrawImage(Render(sleeping), new Rect(column * Size, 30 + Size, Size, Size));
                var label = new FormattedText(labels[column], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 13, column == 0 ? Brushes.Black : Brushes.White, 1);
                dc.DrawText(label, new Point(column * Size + 12, 8));
            }
        }
        var output = new RenderTargetBitmap(Size * 3 * 2, (Size * 2 + 30) * 2, 192, 192, PixelFormats.Pbgra32);
        output.Render(sheet);
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string directory = Path.Combine(root, "artifacts", "visual-review");
        Directory.CreateDirectory(directory);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(output));
        using var file = File.Create(Path.Combine(directory, "animation-contrast.png"));
        png.Save(file);
    }
}
