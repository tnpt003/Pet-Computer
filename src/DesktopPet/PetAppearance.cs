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
    // Point colours (Siamese) and white mittens (tuxedo); by default the same brushes as
    // Fur/Cream, so the original coats draw exactly as before.
    public Brush Ear { get; }
    public Brush Muzzle { get; }
    public Brush Paw { get; }
    // Small second colour for the swatch in the settings list.
    public Brush? Swatch { get; }

    private CoatPalette(string fur, string cream, string pink, string ink = "#49372E", string? iris = null,
        CoatMarkings markings = CoatMarkings.None, string? patchA = null, string? patchB = null,
        string? points = null, string? paw = null)
    {
        Fur = Brush(fur); Cream = Brush(cream); Pink = Brush(pink); Ink = Brush(ink);
        var pointBrush = points == null ? null : Brush(points);
        Ear = pointBrush ?? Fur;
        Muzzle = pointBrush ?? Cream;
        Paw = paw != null ? Brush(paw) : pointBrush ?? Fur;
        Outline = PetRendererPens.Create(Ink, 2.5);
        TailOutline = PetRendererPens.Create(Ink, 13, round: true);
        TailFill = PetRendererPens.Create(pointBrush ?? Fur, 8, round: true);
        Iris = iris == null ? null : Brush(iris);
        Markings = markings;
        PatchA = patchA == null ? null : Brush(patchA);
        PatchB = patchB == null ? null : Brush(patchB);
        MarkingPen = PatchA == null ? null : PetRendererPens.Create(PatchA, 3.2, round: true);
        AccentOutline = PetRendererPens.Create(Ink, 8, round: true);
        Swatch = PatchA ?? pointBrush ?? (paw != null ? Paw : null);
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

    public static readonly CoatPalette GingerTabby = new("#E9AE65", "#FFE4B5", "#E98990", markings: CoatMarkings.Tabby,
        patchA: "#C26E2E");
    // Tuxedo: near-black with a white chest, muzzle and mittens; green-gold eyes.
    public static readonly CoatPalette Tuxedo = new("#3F3E4B", "#FFFFFF", "#E79AA2", ink: "#16151B", iris: "#B9D66A",
        paw: "#FFFFFF");
    public static readonly CoatPalette RussianBlue = new("#8E9CB2", "#CBD4E1", "#D98F99", ink: "#323446", iris: "#7CC67F");
    // Siamese: cream body, dark ears, mask, tail and paws, blue eyes.
    public static readonly CoatPalette Siamese = new("#F3E6D0", "#FFF8EC", "#E9A0A6", ink: "#3B2B24", iris: "#6FB3F2",
        points: "#8A6A55");

    public static CoatPalette For(string? coat) => coat switch
    {
        "GingerTabby" => GingerTabby,
        "Tuxedo" => Tuxedo,
        "RussianBlue" => RussianBlue,
        "Siamese" => Siamese,
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
