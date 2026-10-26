using System;
using System.IO;
using System.Linq;
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
        // The scene cat wears the pet's coat: each coat draws differently, one row per coat for review.
        string? coatBefore = PetScene.Coat; string accessoryBefore = PetScene.Accessory;
        byte[] Pixels(PetSceneKind kind, string coat) {
            PetScene.Coat = coat;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) PetScene.Draw(dc, kind, 240, 60);
            var bitmap = new RenderTargetBitmap(240,60,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
            var bytes = new byte[240*60*4]; bitmap.CopyPixels(bytes, 240*4, 0); return bytes;
        }
        check(!Pixels(PetSceneKind.Home,"Orange").AsSpan().SequenceEqual(Pixels(PetSceneKind.Home,"Black")), "Scene cat follows the pet's coat");
        check(Pixels(PetSceneKind.Infinity,"Orange").AsSpan().SequenceEqual(Pixels(PetSceneKind.Infinity,"Black")), "Scenes without the cat ignore the coat");
        PetScene.Accessory = "Hat"; var hat = Pixels(PetSceneKind.Home,"Orange"); PetScene.Accessory = "None";
        check(!hat.AsSpan().SequenceEqual(Pixels(PetSceneKind.Home,"Orange")), "Scene cat wears the pet's accessory");
        if (Environment.GetEnvironmentVariable("PET_RENDER_REVIEW") is string coatDir) {
            var coats = PetCoats.All; var sheetVisual = new DrawingVisual();
            using (var dc = sheetVisual.RenderOpen())
                for (int i = 0; i < coats.Count; i++) {
                    PetScene.Coat = coats[i].Id;
                    foreach (var (kind, column) in new[]{(PetSceneKind.Home,0),(PetSceneKind.Rest,1),(PetSceneKind.Sound,2)}) {
                        dc.PushTransform(new TranslateTransform(column*240, i*60)); PetScene.Draw(dc, kind, 240, 60); dc.Pop();
                    }
                }
            var coatSheet = new RenderTargetBitmap(720, coats.Count*60, 96, 96, PixelFormats.Pbgra32); coatSheet.Render(sheetVisual);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(coatSheet));
            using (var stream = File.Create(Path.Combine(coatDir,"scene-coats.png"))) png.Save(stream);
            // Every accessory on the sitting and the sleeping cat.
            var ids = PetAccessories.All.Select(a => a.Id).ToList(); var wearVisual = new DrawingVisual();
            PetScene.Coat = "Orange";
            using (var dc = wearVisual.RenderOpen())
                for (int i = 0; i < ids.Count; i++) {
                    PetScene.Accessory = ids[i];
                    foreach (var (kind, column) in new[]{(PetSceneKind.Home,0),(PetSceneKind.Rest,1)}) {
                        dc.PushTransform(new TranslateTransform(column*240+(i%2)*480, (i/2)*60)); PetScene.Draw(dc, kind, 240, 60); dc.Pop();
                    }
                }
            var wearSheet = new RenderTargetBitmap(960, (ids.Count+1)/2*60, 96, 96, PixelFormats.Pbgra32); wearSheet.Render(wearVisual);
            var wearPng = new PngBitmapEncoder(); wearPng.Frames.Add(BitmapFrame.Create(wearSheet));
            using (var stream = File.Create(Path.Combine(coatDir,"scene-accessories.png"))) wearPng.Save(stream);
        }
        PetScene.Coat = coatBefore; PetScene.Accessory = accessoryBefore;
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
        check((L(PetUi.Surface)+.05)/(L(PetUi.AccentText)+.05)>=4.5,"Accent-coloured text is readable on the cream surface");
        var edge=PetUi.Hex(PetUi.ControlEdgeHex);
        check((L(PetUi.CardBack)+.05)/(L(edge)+.05)>=3 && (L(PetUi.Surface)+.05)/(L(edge)+.05)>=3,"Input outlines stand out 3:1 on cards and cream");
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
