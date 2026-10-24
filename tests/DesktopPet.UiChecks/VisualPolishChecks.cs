using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet;

internal static class VisualPolishChecks
{
    public static void Run(Action<bool,string> check)
    {
        double L(Brush b) {
            var c=((SolidColorBrush)b).Color;
            double F(byte x) { double v=x/255.0; return v<=0.04045 ? v/12.92 : Math.Pow((v+0.055)/1.055,2.4); }
            return .2126*F(c.R)+.7152*F(c.G)+.0722*F(c.B);
        }
        foreach(var ink in new[]{PetUi.Care,PetUi.Mystery,PetUi.Danger,PetUi.Reward})
            check((L(PetUi.Surface)+.05)/(L(ink)+.05)>=4.5,"Semantic ink is readable on the cream surface");
        var panel=new StackPanel();
        foreach(var background in new[]{PetUi.Surface,PetUi.Ink}) {
            var row=new StackPanel { Orientation=Orientation.Horizontal, Background=background };
            foreach(var motif in Enum.GetValues<PetMotif>()) {
                var art=new PetIllustration(motif,64);
                check(!art.IsHitTestVisible && !art.Focusable,"Decorative artwork never captures input: "+motif);
                row.Children.Add(art);
            }
            panel.Children.Add(row);
        }
        panel.Measure(new Size(384,128));panel.Arrange(new Rect(0,0,384,128));panel.UpdateLayout();
        var image=new RenderTargetBitmap(384,128,96,96,PixelFormats.Pbgra32);image.Render(panel);
        if(Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string dir) {
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream=File.Create(Path.Combine(dir,"visual-polish-icons.png"));encoder.Save(stream);
        }
    }
}
