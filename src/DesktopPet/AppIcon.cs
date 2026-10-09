using System;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DesktopPet;

public static class AppIcon
{
    private static readonly Uri ResourceUri = new("pack://application:,,,/DesktopPet;component/Assets/Pet.ico");

    public static BitmapFrame WindowImage()
    {
        using var stream = Application.GetResourceStream(ResourceUri)!.Stream;
        var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.OrderByDescending(image => image.PixelWidth).First();
        frame.Freeze();
        return frame;
    }

    public static System.Drawing.Icon TrayImage()
    {
        using var stream = Application.GetResourceStream(ResourceUri)!.Stream;
        using var icon = new System.Drawing.Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
        return (System.Drawing.Icon)icon.Clone();
    }
}
