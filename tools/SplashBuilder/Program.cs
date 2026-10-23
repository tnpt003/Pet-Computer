using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

// Builds src/DesktopPet/Assets/Splash.gif, the picture Setup shows while it unpacks (scripts/pack.ps1):
// the app's own vector cat (PetRenderer, idle and happy: breathing, tail sway, blinks, a little hop)
// on a warm card, with "getting your cat ready" and three bouncing dots. One loop is two tail sways
// (~4 s); Setup shows it for a few seconds, so the tiny seams are never seen.
// Run: dotnet run --project tools/SplashBuilder -- <repo root>
internal static class Program
{
    private const int Width = 360, Height = 250, Fps = 12;
    // Two tail sways (sin 3t); the idle bob is 1.2 px, so its seam does not show.
    private static readonly double Loop = 4 * Math.PI / 3;

    [STAThread]
    private static void Main(string[] args)
    {
        string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
        var cat = new PetModel { Action = PetAction.Idle, Grounded = true, Facing = 1, AnimationTime = 1, ActionTime = 1 };
        // Happy (held like the menu's manual emotion): a smile, a little hop and hearts. Warmed up to full strength first.
        cat.Emotions.SetManualMode(true);
        cat.Emotions.Perform(PetEmotion.Happy);
        for (int warm = 0; warm < 3 * Fps; warm++) cat.Emotions.Step(cat, 1.0 / Fps);
        var view = new PetRenderer(cat) { Coat = "Orange", Width = 170, Height = 170 };
        var dots = Enumerable.Range(0, 3).Select(_ => new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Fill = PetUi.Accent, Margin = new Thickness(3, 0, 3, 0) }).ToArray();
        var dotRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var dot in dots) dotRow.Children.Add(dot);
        var text = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        text.Children.Add(new TextBlock { Text = "Desktop Pet", FontFamily = PetUi.TextFont, FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = PetUi.Ink, HorizontalAlignment = HorizontalAlignment.Center });
        text.Children.Add(new TextBlock { Text = "Đang chuẩn bị mèo của bạn · Getting your cat ready", FontFamily = PetUi.TextFont, FontSize = 12, Foreground = PetUi.Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
        text.Children.Add(dotRow);
        // A soft floor shadow under the cat.
        var stage = new Grid { Width = 170, Height = 150, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        stage.Children.Add(new System.Windows.Shapes.Ellipse { Width = 110, Height = 14, Fill = PetUi.AccentSoft, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 6) });
        view.Margin = new Thickness(0, -16, 0, 0);
        view.VerticalAlignment = VerticalAlignment.Top;
        stage.Children.Add(view);
        var column = new StackPanel();
        column.Children.Add(stage); column.Children.Add(text);
        var card = new Border { Width = Width, Height = Height, Background = PetUi.Surface, BorderBrush = PetUi.Line, BorderThickness = new Thickness(1), Child = column };
        TextOptions.SetTextFormattingMode(card, TextFormattingMode.Display);

        var frames = new List<BitmapSource>();
        int count = (int)Math.Round(Loop * Fps);
        for (int i = 0; i < count; i++)
        {
            double t = i * Loop / count;
            cat.AnimationTime = 4.6 + t; // Starts just before the first blink.
            if (i > 0) cat.Emotions.Step(cat, Loop / count);
            for (int d = 0; d < dots.Length; d++)
            {
                double phase = (t * 1.2 - d * 0.18) % 1;
                dots[d].RenderTransform = new TranslateTransform(0, phase < 0.35 ? -6 * Math.Sin(phase / 0.35 * Math.PI) : 0);
                dots[d].Opacity = phase < 0.35 ? 1 : 0.45;
            }
            view.InvalidateVisual();
            card.Measure(new Size(Width, Height)); card.Arrange(new Rect(0, 0, Width, Height)); card.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(card);
            frames.Add(bitmap);
        }

        // One shared palette (from a frame with the dots up and the eyes open) so colours do not flicker.
        var palette = new BitmapPalette(frames[5], 256);
        var encoder = new GifBitmapEncoder();
        for (int i = 0; i < frames.Count; i++)
        {
            var indexed = new FormatConvertedBitmap(frames[i], PixelFormats.Indexed8, palette, 0);
            var metadata = new BitmapMetadata("gif");
            metadata.SetQuery("/grctlext/Delay", (ushort)Math.Round(100.0 / Fps));
            metadata.SetQuery("/grctlext/Disposal", (byte)1);
            if (i == 0)
            {
                metadata.SetQuery("/appext/Application", System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                metadata.SetQuery("/appext/Data", new byte[] { 3, 1, 0, 0, 0 }); // Loop forever.
            }
            encoder.Frames.Add(BitmapFrame.Create(indexed, null, metadata, null));
        }
        string output = Path.Combine(root, "src", "DesktopPet", "Assets", "Splash.gif");
        using (var stream = File.Create(output)) encoder.Save(stream);
        // Four frames side by side in artifacts/ to look at.
        var sheet = new RenderTargetBitmap(Width * 4, Height, 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
            for (int k = 0; k < 4; k++) dc.DrawImage(frames[k * frames.Count / 4 + 3], new Rect(k * Width, 0, Width, Height));
        sheet.Render(drawing);
        var preview = new PngBitmapEncoder(); preview.Frames.Add(BitmapFrame.Create(sheet));
        using (var stream = File.Create(Path.Combine(root, "artifacts", "splash-preview.png"))) preview.Save(stream);
        Console.WriteLine($"Splash.gif: {frames.Count} frames at {Fps} fps, {new FileInfo(output).Length / 1024} KB");
    }
}
