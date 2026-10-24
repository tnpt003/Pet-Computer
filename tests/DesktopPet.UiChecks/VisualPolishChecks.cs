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
        var scenes = new StackPanel { Background = PetUi.Surface };
        foreach (var kind in Enum.GetValues<PetSceneKind>()) {
            var art = new PetScene(kind, 120) { Width = 480, Margin = new Thickness(0) };
            scenes.Children.Add(art);
            check(!art.IsHitTestVisible && !art.Focusable, "Scene is decorative: " + kind);
        }
        scenes.Measure(new Size(480, double.PositiveInfinity)); scenes.Arrange(new Rect(0,0,480,1440)); scenes.UpdateLayout();
        foreach (double scale in new[] { 1.0, 1.5, 2.0 }) {
            var sheet = new RenderTargetBitmap((int)(480*scale),(int)(1440*scale),96*scale,96*scale,PixelFormats.Pbgra32);
            sheet.Render(scenes);
            check(sheet.PixelWidth == (int)(480*scale), "Vector scene sheet renders at " + scale);
            if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string output && scale == 1) {
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(sheet));
                using var stream = File.Create(Path.Combine(output,"scene-catalog.png")); png.Save(stream);
            }
        }
        var narrow = new PetScene(PetSceneKind.Home, 80);
        narrow.Measure(new Size(180,80)); narrow.Arrange(new Rect(0,0,180,80));
        check(narrow.DesiredSize.Width <= 180 && narrow.ActualWidth == 180, "Scene does not force narrow panels wider");
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
