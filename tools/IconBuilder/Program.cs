using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
        string assets = Path.Combine(root, "src", "DesktopPet", "Assets");
        using var xaml = File.OpenRead(Path.Combine(assets, "PetIcon.xaml"));
        var icon = (DrawingImage)XamlReader.Load(xaml);
        byte[] Render(int size)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) dc.DrawImage(icon, new Rect(0, 0, size, size));
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var images = sizes.Select(Render).ToArray();
        using (var writer = new BinaryWriter(File.Create(Path.Combine(assets, "Pet.ico"))))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(images[i].Length); writer.Write(offset);
                offset += images[i].Length;
            }
            foreach (var image in images) writer.Write(image);
        }
        File.WriteAllBytes(Path.Combine(assets, "PetIcon-preview.png"), Render(512));
        Console.WriteLine("Generated 32-bit icon sizes: " + string.Join(", ", sizes));
    }
}
