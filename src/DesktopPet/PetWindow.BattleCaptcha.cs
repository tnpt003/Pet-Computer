using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using static DesktopPet.PetUi;

namespace DesktopPet;

// 1.5 M26 (docs/UPDATE-1.5.md A2, docs/APPROVED-EXCEPTIONS.md → App trong trận): the captcha a slime
// threw (BattleSim.Captcha) in its own small topmost window standing on the floor of the strip, kept
// above the cat's window: where it stands it covers the cat and the slimes (they still fight behind it);
// a click on the cat where it is not covered still lifts it. A click on a tile picks it (BattleSim
// decides). Pause, a menu or a fullscreen app hides it with the rest of the battle. Drawing and input only.
public sealed partial class PetWindow
{
    private Window? captchaWindow;
    private CaptchaView? captchaView;
    private int captchaFor;
    private Rect captchaPlaced;
    private double captchaScale, captchaRaiseClock;
    public bool BattleCaptchaShown => captchaWindow is { IsVisible: true };
    public Rect BattleCaptchaSpot => captchaPlaced;

    // The captcha's spot on the screen (DIPs): centred on its X, standing on the floor.
    public Rect CaptchaSpot(BattleCaptcha c) => new(c.X - c.Width / 2, battleArea.Bottom - 2 - BattleSim.CaptchaHeight, c.Width, BattleSim.CaptchaHeight);

    // Every frame of the battle windows. draw: a frame is due (or something changed).
    private void UpdateBattleCaptcha(double dt, bool draw)
    {
        if (battle is not BattleSim b) return;
        if (b.Captcha is not BattleCaptcha captcha) { CloseBattleCaptcha(); return; }
        if (captchaWindow == null || captchaFor != captcha.Id)
        {
            CloseBattleCaptcha();
            captchaFor = captcha.Id;
            captchaView = new CaptchaView(b) { Language = preferences.Language };
            captchaView.TileClicked += tile => BattleClickCaptcha(tile);
            captchaWindow = CreateOverlayWindow(captchaView, "Pet captcha");
            captchaWindow.DpiChanged += (_, _) => captchaScale = 0; // Placed again in pixels next frame.
            captchaPlaced = Rect.Empty;
        }
        if (b.Paused) { if (captchaWindow.IsVisible) captchaWindow.Hide(); return; }
        bool fresh = !captchaWindow.IsVisible;
        if (fresh) { captchaWindow.Show(); captchaPlaced = Rect.Empty; }
        var spot = CaptchaSpot(captcha);
        if (spot != captchaPlaced || captchaScale != battleScale) { captchaPlaced = spot; captchaScale = battleScale; PlaceAppWindow(captchaWindow, spot); }
        // Over the cat's window (clicking the cat raises it): put back on top now and then; not while the
        // user holds or throws the cat (a carried cat stays in front until it is down again).
        captchaRaiseClock -= dt;
        if ((fresh || captchaRaiseClock <= 0) && !BattleHoldsOff) { captchaRaiseClock = 0.3; RaiseBattleCaptcha(); }
        if (draw) captchaView!.InvalidateVisual();
    }

    private void RaiseBattleCaptcha()
    {
        if (captchaWindow is not { IsVisible: true }) return;
        var handle = new WindowInteropHelper(captchaWindow).Handle;
        const uint NoMove = 0x0002, NoSize = 0x0001, NoActivate = 0x0010;
        if (handle != IntPtr.Zero) SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, NoMove | NoSize | NoActivate);
    }

    // A click on a captcha tile.
    public bool BattleClickCaptcha(int tile)
    {
        if (battle == null || battle.Paused) return false;
        bool done = battle.ClickCaptcha(tile);
        RequestBattleRedraw();
        captchaView?.InvalidateVisual();
        return done;
    }

    // The HUD's buttons for the low battery and the frozen cursor.
    public bool BattlePlugIn()
    {
        if (battle == null || battle.Paused) return false;
        bool done = battle.PlugIn();
        RequestBattleRedraw();
        UpdateBattleHud();
        return done;
    }

    public bool BattleResumeCursor()
    {
        if (battle == null || battle.Paused) return false;
        bool done = battle.ResumeCursor();
        RequestBattleRedraw();
        UpdateBattleHud();
        return done;
    }

    private void HideBattleCaptcha() { if (captchaWindow is { IsVisible: true }) captchaWindow.Hide(); }

    private void CloseBattleCaptcha()
    {
        captchaWindow?.Close();
        captchaWindow = null; captchaView = null; captchaFor = 0; captchaPlaced = Rect.Empty; captchaScale = 0;
    }
}

// The captcha window's drawing: a fake "I am not a robot" check (the game's own look, no real
// service's) with its nine tiles, the cat ones ticked once picked, a bar of the time left. A wrong
// pick shakes it as it shuffles.
public sealed class CaptchaView : FrameworkElement
{
    private readonly BattleSim sim;
    public string Language { get; set; } = PetLanguage.English;
    public event Action<int>? TileClicked;
    public const double Bar = 22, Tile = 31, Gap = 3, Top = 40;
    private static readonly Brush Body = Frozen(250, 0xF6, 0xF4, 0xFA), TileBack = Frozen(255, 0xE4, 0xEC, 0xF6), Picked = Frozen(255, 0x5F, 0xA8, 0xD8);
    private static readonly Brush TimeFill = Frozen(255, 0x5F, 0xA8, 0xD8), TimeBack = Frozen(255, 0xD8, 0xD4, 0xE4), Coat = Frozen(255, 0xF2, 0xA9, 0x5C);
    private static readonly Brush FishBlue = Frozen(255, 0x7F, 0xB8, 0xE6), Yarn = Frozen(255, 0xE8, 0x7A, 0xB8), Slime = Frozen(255, 0x7C, 0xD3, 0x6A);
    private static readonly Pen Rim = FrozenPen(PetUi.ArtInk, 1.6), Tick = FrozenPen(Brushes.White, 2.6);
    private static readonly Typeface Face = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public CaptchaView(BattleSim sim)
    {
        this.sim = sim;
        Cursor = Cursors.Hand;
        MouseLeftButtonDown += (_, e) =>
        {
            if (TileAt(e.GetPosition(this)) is int tile) TileClicked?.Invoke(tile);
            e.Handled = true; // The rest of it covers what is behind.
        };
    }

    private string L(string vietnamese, string english) => PetText.Pick(Language, vietnamese, english);

    // The grid's top-left (centred under the words).
    private Point GridAt => new((ActualWidth - (3 * Tile + 2 * Gap)) / 2, Top);
    public Rect TileRect(int i) => new(GridAt.X + i % 3 * (Tile + Gap), GridAt.Y + i / 3 * (Tile + Gap), Tile, Tile);

    public int? TileAt(Point p)
    {
        for (int i = 0; i < 9; i++) if (TileRect(i).Contains(p)) return i;
        return null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (sim.Captcha is not BattleCaptcha c) return;
        double w = ActualWidth, h = ActualHeight;
        if (w < BattleSim.CaptchaMinWidth / 2 || h < BattleSim.CaptchaHeight / 2) return; // Not placed yet (the window starts tiny).
        double shake = c.ShuffledAt >= 0 && sim.Time - c.ShuffledAt < 0.3 ? Math.Sin((sim.Time - c.ShuffledAt) * 60) * 3 : 0;
        dc.PushTransform(new TranslateTransform(shake, 0));
        var box = new Rect(1, 1, w - 2, h - 2);
        dc.DrawRoundedRectangle(Body, Rim, box, 8, 8);
        dc.DrawRoundedRectangle(AppArt.WindowBar, null, new Rect(2, 2, w - 4, Bar - 2), 7, 7);
        Text(dc, L("Xác minh bạn không phải robot", "Verify you are not a robot"), 11, Brushes.White, new Point(w / 2, 5), w - 12);
        Text(dc, L("Chọn mọi ô có hình mèo", "Select every tile with a cat"), 10.5, PetUi.ArtInk, new Point(w / 2, Bar + 2), w - 12);
        for (int i = 0; i < 9; i++)
        {
            var r = TileRect(i);
            bool picked = c.Picked[i];
            dc.DrawRoundedRectangle(picked ? Picked : TileBack, Rim, r, 4, 4);
            var centre = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
            DrawPicture(dc, c.Tiles[i], centre);
            if (picked)
            {
                dc.DrawEllipse(Picked, Rim, new Point(r.Right - 6, r.Top + 6), 5.5, 5.5);
                dc.DrawLine(Tick, new Point(r.Right - 9, r.Top + 6), new Point(r.Right - 6.5, r.Top + 8.5));
                dc.DrawLine(Tick, new Point(r.Right - 6.5, r.Top + 8.5), new Point(r.Right - 2.5, r.Top + 3.5));
            }
        }
        // The time left, along the bottom.
        double left = Math.Clamp(1 - c.Age / Math.Max(0.1, c.Life), 0, 1);
        var bar = new Rect(10, h - 7, w - 20, 3.5);
        dc.DrawRoundedRectangle(TimeBack, null, bar, 1.5, 1.5);
        dc.DrawRoundedRectangle(TimeFill, null, new Rect(bar.X, bar.Y, bar.Width * left, bar.Height), 1.5, 1.5);
        dc.Pop();
    }

    // One tile's picture: a cat's face, or a slime, a fish, a ball of yarn, a file, a bell.
    public static void DrawPicture(DrawingContext dc, int kind, Point c)
    {
        switch (kind)
        {
            case BattleCaptcha.Cat:
                dc.DrawGeometry(Coat, Rim, Ears(c));
                dc.DrawEllipse(Coat, Rim, new Point(c.X, c.Y + 2), 9, 7.5);
                dc.DrawEllipse(PetUi.ArtInk, null, new Point(c.X - 3.5, c.Y + 1), 1.3, 1.6);
                dc.DrawEllipse(PetUi.ArtInk, null, new Point(c.X + 3.5, c.Y + 1), 1.3, 1.6);
                dc.DrawLine(Rim, new Point(c.X - 1.5, c.Y + 5), new Point(c.X + 1.5, c.Y + 5));
                break;
            case 1:
                AppArt.DrawBlob(dc, new Point(c.X, c.Y + 2), 9, Slime, 1);
                break;
            case 2:
                dc.PushTransform(new MatrixTransform(0.9, 0, 0, 0.9, c.X - 11, c.Y - 11));
                dc.DrawGeometry(FishBlue, Rim, FishShape);
                dc.Pop();
                break;
            case 3:
                dc.DrawEllipse(Yarn, Rim, c, 8.5, 8.5);
                dc.DrawLine(Rim, new Point(c.X - 6, c.Y - 3), new Point(c.X + 5, c.Y + 5));
                dc.DrawLine(Rim, new Point(c.X - 4, c.Y + 6), new Point(c.X + 6, c.Y - 4));
                break;
            case 4:
                dc.DrawGeometry(AppArt.Paper, Rim, File(c));
                break;
            default:
                AppArt.DrawBell(dc, new Point(c.X, c.Y + 2), 0.75, false);
                break;
        }
    }
    private static readonly Geometry FishShape = Freeze(Geometry.Parse("M 2,12 C 6,5 13,5 16,9 L 22,5 L 22,19 L 16,15 C 13,19 6,19 2,12 Z"));
    private static Geometry Ears(Point c) => Freeze(Geometry.Parse(string.Format(CultureInfo.InvariantCulture,
        "M {0},{1} L {2},{3} L {4},{5} Z M {6},{1} L {7},{3} L {8},{5} Z", c.X - 8, c.Y - 1, c.X - 7, c.Y - 10, c.X - 2, c.Y - 5, c.X + 8, c.X + 7, c.X + 2)));
    private static Geometry File(Point c) => Freeze(Geometry.Parse(string.Format(CultureInfo.InvariantCulture,
        "M {0},{1} L {2},{1} L {3},{4} L {3},{5} L {0},{5} Z", c.X - 7, c.Y - 9, c.X + 3, c.X + 7, c.Y - 5, c.Y + 9)));
    private static Geometry Freeze(Geometry g) { g.Freeze(); return g; }

    private void Text(DrawingContext dc, string text, double size, Brush brush, Point topCentre, double maxWidth)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
            { MaxTextWidth = Math.Max(10, maxWidth), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
        dc.DrawText(formatted, new Point(topCentre.X - formatted.MaxTextWidth / 2, topCentre.Y));
    }
}
