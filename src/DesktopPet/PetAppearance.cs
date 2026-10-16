using System.Linq;
using System.Windows.Media;

namespace DesktopPet;

// Colors for one coat. Brushes and pens are frozen and shared, so switching coats
// never allocates per frame. Orange is the original artwork, value for value.
public sealed class CoatPalette
{
    public Brush Fur { get; }
    public Brush Cream { get; }
    public Brush Pink { get; }
    public Brush Ink { get; }
    public Pen Outline { get; }
    public Pen TailOutline { get; }
    public Pen TailFill { get; }
    // Coloured iris drawn behind the pupil; null keeps plain ink eyes.
    public Brush? Iris { get; }
    public CoatMarkings Markings { get; }
    public Brush? PatchA { get; }
    public Brush? PatchB { get; }
    public Pen? MarkingPen { get; }
    public Pen AccentOutline { get; }
    // Point colours (Siamese) and white mittens (tuxedo); by default the same brushes as
    // Fur/Cream, so the original coats draw exactly as before.
    public Brush Ear { get; }
    public Brush Muzzle { get; }
    public Brush Paw { get; }
    // Small second colour for the swatch in the settings list.
    public Brush? Swatch { get; }

    private CoatPalette(CoatSpec spec)
    {
        Fur = Brush(spec.Fur); Cream = Brush(spec.Cream); Pink = Brush(spec.Pink); Ink = Brush(spec.Ink);
        var pointBrush = spec.Points == null ? null : Brush(spec.Points);
        Ear = pointBrush ?? Fur;
        Muzzle = pointBrush ?? Cream;
        Paw = spec.Paw != null ? Brush(spec.Paw) : pointBrush ?? Fur;
        Outline = PetRendererPens.Create(Ink, 2.5);
        TailOutline = PetRendererPens.Create(Ink, 13, round: true);
        TailFill = PetRendererPens.Create(pointBrush ?? Fur, 8, round: true);
        Iris = spec.Iris == null ? null : Brush(spec.Iris);
        Markings = spec.Markings;
        PatchA = spec.PatchA == null ? null : Brush(spec.PatchA);
        PatchB = spec.PatchB == null ? null : Brush(spec.PatchB);
        MarkingPen = PatchA == null ? null : PetRendererPens.Create(PatchA, 3.2, round: true);
        AccentOutline = PetRendererPens.Create(Ink, 8, round: true);
        Swatch = PatchA ?? pointBrush ?? (spec.Paw != null ? Paw : null);
    }

    // One palette per coat in PetCoats.All (Assets/coats.json), same order.
    private static readonly CoatPalette[] Palettes = PetCoats.All.Select(spec => new CoatPalette(spec)).ToArray();
    public static CoatPalette Orange => Palettes[0];

    public static CoatPalette For(string? coat)
    {
        for (int i = 0; i < Palettes.Length; i++)
            if (PetCoats.All[i].Id == coat) return Palettes[i];
        return Palettes[0];
    }

    private static Brush Brush(string color)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        return brush;
    }
}

internal static class PetRendererPens
{
    public static Pen Create(Brush brush, double thickness, PenLineJoin join = PenLineJoin.Miter, bool round = false)
    {
        var pen = new Pen(brush, thickness) { LineJoin = join };
        if (round) { pen.StartLineCap = PenLineCap.Round; pen.EndLineCap = PenLineCap.Round; }
        pen.Freeze(); return pen;
    }
}
