using System.Windows.Media;

namespace DesktopPet;

public enum CoatMarkings { None, Calico, Tabby }

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

    private CoatPalette(string fur, string cream, string pink, string ink = "#49372E", string? iris = null,
        CoatMarkings markings = CoatMarkings.None, string? patchA = null, string? patchB = null)
    {
        Fur = Brush(fur); Cream = Brush(cream); Pink = Brush(pink); Ink = Brush(ink);
        Outline = PetRendererPens.Create(Ink, 2.5);
        TailOutline = PetRendererPens.Create(Ink, 13, round: true);
        TailFill = PetRendererPens.Create(Fur, 8, round: true);
        Iris = iris == null ? null : Brush(iris);
        Markings = markings;
        PatchA = patchA == null ? null : Brush(patchA);
        PatchB = patchB == null ? null : Brush(patchB);
        MarkingPen = PatchA == null ? null : PetRendererPens.Create(PatchA, 3.2, round: true);
        AccentOutline = PetRendererPens.Create(Ink, 8, round: true);
    }

    public static readonly CoatPalette Orange = new("#E9AE65", "#FFE4B5", "#E98990");
    public static readonly CoatPalette Gray = new("#A9ACB8", "#E8E9EE", "#E9989E");
    // Charcoal rather than pure black, with a darker outline and amber eyes, so the
    // face lines and pupils stay readable.
    public static readonly CoatPalette Black = new("#4E4D5C", "#6F6D80", "#D98A94", ink: "#1C1B22", iris: "#F2C14E");
    public static readonly CoatPalette White = new("#F5F2EC", "#FFFFFF", "#F0A3AA", iris: "#8CC7EE");
    public static readonly CoatPalette Calico = new("#F7F1E6", "#FFFFFF", "#EE9BA2", markings: CoatMarkings.Calico,
        patchA: "#E59A52", patchB: "#45403F");
    public static readonly CoatPalette Tabby = new("#B9A588", "#EFE4D2", "#E39196", markings: CoatMarkings.Tabby,
        patchA: "#7A6650");

    public static CoatPalette For(string? coat) => coat switch
    {
        "Gray" => Gray,
        "Black" => Black,
        "White" => White,
        "Calico" => Calico,
        "Tabby" => Tabby,
        _ => Orange
    };

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
