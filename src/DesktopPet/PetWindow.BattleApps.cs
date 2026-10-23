using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopPet;

// The battle's desktop apps on the screen (BattleSim.Desktop): each icon in its own small topmost
// window on a desktop-like grid of cells above the strip (cells under the HUD, the card offer or
// the bag left out; an icon there moves to a free cell when one opens), one
// click-through canvas for what flies between the icons and the field, and a note beside the icon
// under the mouse. Icons fade back while the cursor is far, so they do not glare while the user
// works. A fullscreen app hides them with the rest of the battle. Drawing and input only: what the
// apps do is BattleSim (PetBattle.Apps.cs).
public sealed partial class PetWindow
{
    public const double AppCellWidth = 84, AppCellHeight = 108;
    // Icon opacity with the cursor near (≤ AppNear DIPs from the tile) and far (≥ AppFar).
    public const double AppNear = 110, AppFar = 280, AppFarOpacity = 0.5;
    private sealed class AppIconWindow
    {
        public required Window Window;
        public required DesktopAppIcon View;
        public Rect Placed;
        // The DPI scale it was placed at: a new one (Windows scale changed) places it again.
        public double PlacedScale;
        public double Opacity = 1;
    }
    private readonly Dictionary<int, AppIconWindow> appIcons = new();
    private Window? appFlightWindow;
    private AppFlightView? appFlightView;
    private bool appFlightShown;
    private Point appFlightOrigin;
    private double appFlightScale;
    private Window? appHintWindow;
    private TextBlock? appHintText;
    private int appHintFor;
    // What the grid was last worked out from: the battle's area and the windows it keeps clear of.
    private Area appGridArea;
    private Rect[] appGridBlockers = Array.Empty<Rect>();

    public IReadOnlyCollection<int> BattleAppWindows => appIcons.Keys;
    public double BattleAppOpacity(int id) => appIcons.TryGetValue(id, out var icon) ? icon.Opacity : 0;

    // The cells across the battle monitor and up to three rows above the strip (fewer on a low screen).
    private static (int Cols, int Rows) AppGridSize(Area area) =>
        (Math.Max(1, (int)((area.Right - area.Left) / AppCellWidth)),
         Math.Clamp((int)((area.Bottom - BattleFieldView.FieldHeight - area.Top - 60) / AppCellHeight), 1, 3));

    // A cell's top-left on the screen (DIPs): columns share the width, rows go up from the strip.
    private Point AppCellTopLeft(int col, int row)
    {
        var area = battleArea;
        double width = (area.Right - area.Left) / Math.Max(1, battle?.GridCols ?? 1);
        return new Point(area.Left + (col + 0.5) * width - DesktopAppIcon.ViewWidth / 2,
            area.Bottom - BattleFieldView.FieldHeight - (row + 1) * AppCellHeight + 6);
    }

    // The tile's centre on the screen now: hopping in an arc from its old cell, or inching along when it crawls.
    public Point BattleAppCentre(DesktopApp app)
    {
        var to = AppCellTopLeft(app.Col, app.Row);
        var at = to;
        if (battle != null && app.MovedAt >= 0)
        {
            double since = battle.Time - app.MovedAt, length = app.Crawled ? DesktopAppIcon.CrawlSeconds : DesktopAppIcon.HopSeconds;
            if (since < length)
            {
                var from = AppCellTopLeft(app.FromCol, app.FromRow);
                double k = since / length;
                // A crawl goes in little pushes; a hop eases across in an arc.
                double e = app.Crawled ? k - Math.Sin(k * Math.PI * 6) / (Math.PI * 6) : k * k * (3 - 2 * k);
                at = new Point(from.X + (to.X - from.X) * e, from.Y + (to.Y - from.Y) * e - (app.Crawled ? 0 : Math.Sin(k * Math.PI) * 42));
            }
        }
        return new Point(at.X + DesktopAppIcon.Centre.X, at.Y + DesktopAppIcon.Centre.Y);
    }

    // The battle windows the icons keep clear of (DIPs): the HUD, and the card offer and the bag while open.
    private Rect[] AppBlockers()
    {
        var list = new List<Rect>();
        if (battleHudWindow is { ActualWidth: > 0 } hud) list.Add(new Rect(battleHudAt.X, battleHudAt.Y, hud.ActualWidth, hud.ActualHeight));
        foreach (var panel in new[] { upgradeWindow, bagWindow })
            if (panel is { IsVisible: true, ActualWidth: > 0 }) list.Add(new Rect(panel.Left, panel.Top, panel.ActualWidth, panel.ActualHeight));
        return list.ToArray();
    }

    private void UpdateAppGrid()
    {
        if (battle == null) return;
        var area = battleArea;
        var blockers = AppBlockers();
        if (appGridArea == area && appGridBlockers.SequenceEqual(blockers) && battle.Desktop.Count > 0) return;
        appGridArea = area;
        appGridBlockers = blockers;
        var (cols, rows) = AppGridSize(area);
        var blocked = new List<(int, int)>();
        double width = (area.Right - area.Left) / cols;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var cell = new Rect(area.Left + c * width, area.Bottom - BattleFieldView.FieldHeight - (r + 1) * AppCellHeight, width, AppCellHeight);
                foreach (var blocker in blockers)
                {
                    var guard = blocker; guard.Inflate(6, 6);
                    if (cell.IntersectsWith(guard)) { blocked.Add((c, r)); break; }
                }
            }
        battle.SetAppGrid(cols, rows, blocked);
    }

    // Every frame of the battle windows: the icons in place and faded by the cursor, the flights drawn.
    // draw: a frame is due (or something changed).
    private void UpdateBattleApps(double dt, bool draw)
    {
        if (battle is not BattleSim b || b.AppRules == null) return;
        UpdateAppGrid();
        foreach (var app in b.Desktop)
            if (!appIcons.ContainsKey(app.Id)) CreateAppIcon(b, app);
        var cursor = ScreenCursor();
        var mouse = new Point(cursor.X / battleScale, cursor.Y / battleScale);
        foreach (var app in b.Desktop)
        {
            if (!appIcons.TryGetValue(app.Id, out var icon)) continue;
            var centre = BattleAppCentre(app);
            var spot = new Rect(centre.X - DesktopAppIcon.Centre.X, centre.Y - DesktopAppIcon.Centre.Y, DesktopAppIcon.ViewWidth, DesktopAppIcon.ViewHeight);
            if (spot != icon.Placed || battleScale != icon.PlacedScale) { icon.Placed = spot; icon.PlacedScale = battleScale; PlaceAppWindow(icon.Window, spot); }
            // Far from the cursor it fades back; a charged or infected one stays easier to see.
            double distance = (mouse - centre).Length;
            double target = icon.View.Hovered ? 1 : distance <= AppNear ? 1 : distance >= AppFar ? AppFarOpacity : 1 - (1 - AppFarOpacity) * (distance - AppNear) / (AppFar - AppNear);
            if (app.Ready) target = Math.Max(target, 0.75);
            if (app.Infected) target = Math.Max(target, 0.85);
            double opacity = icon.Opacity + (target - icon.Opacity) * Math.Min(1, dt * 8);
            if (dt <= 0 || Math.Abs(target - opacity) < 0.02) opacity = target; // Settles on it instead of creeping.
            if (opacity != icon.Opacity) { icon.Opacity = opacity; icon.View.Opacity = opacity; }
            if (draw) icon.View.InvalidateVisual();
        }
        UpdateAppFlights(b, draw);
        if (appHintFor != 0) UpdateAppHint();
    }

    private void CreateAppIcon(BattleSim b, DesktopApp app)
    {
        int id = app.Id;
        var view = new DesktopAppIcon(b, id) { Language = preferences.Language };
        view.Clicked += () => BattleClickApp(id);
        view.HoverChanged += over => { if (over) ShowAppHint(id); else if (appHintFor == id) CloseAppHint(); };
        var window = CreateOverlayWindow(view, "Pet app");
        window.Width = DesktopAppIcon.ViewWidth; window.Height = DesktopAppIcon.ViewHeight;
        var icon = new AppIconWindow { Window = window, View = view };
        window.DpiChanged += (_, _) => icon.PlacedScale = 0; // Placed again in pixels next frame.
        appIcons[id] = icon;
        window.Show();
        icon.Placed = Rect.Empty;
    }

    // Moves and sizes a battle-app window in pixels of the battle's monitor.
    private void PlaceAppWindow(Window window, Rect dips)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) { window.Left = dips.X; window.Top = dips.Y; return; }
        const uint NoZOrder = 0x0004, NoActivate = 0x0010;
        double s = battleScale;
        SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(dips.X * s), (int)Math.Round(dips.Y * s), (int)Math.Round(dips.Width * s), (int)Math.Round(dips.Height * s), NoZOrder | NoActivate);
    }

    // The flights' canvas: over the icon rows and the strip, shown while something flies.
    private void UpdateAppFlights(BattleSim b, bool draw)
    {
        bool flying = b.Launches.Count > 0;
        if (!flying && !appFlightShown) return;
        var area = battleArea;
        double top = area.Bottom - BattleFieldView.FieldHeight - b.GridRows * AppCellHeight - 80;
        if (appFlightWindow == null)
        {
            appFlightView = new AppFlightView(b) { Language = preferences.Language };
            appFlightView.IconCentre = id => b.FindApp(id) is DesktopApp app ? BattleAppCentre(app) - (Vector)appFlightOrigin : null;
            appFlightView.FloorPoint = (x, h) => new Point(x - appFlightOrigin.X, battleArea.Bottom - 2 - h - appFlightOrigin.Y); // The area of now (it can change mid-battle).
            appFlightWindow = CreateOverlayWindow(appFlightView, "Pet app flights", clickThrough: true);
            appFlightWindow.DpiChanged += (_, _) => appFlightScale = 0;
            appFlightWindow.Show();
        }
        var origin = new Point(area.Left, Math.Max(area.Top, top));
        if (origin != appFlightOrigin || !appFlightShown || appFlightScale != battleScale)
        {
            appFlightOrigin = origin;
            appFlightScale = battleScale;
            PlaceAppWindow(appFlightWindow, new Rect(origin.X, origin.Y, area.Right - area.Left, area.Bottom - origin.Y));
        }
        appFlightShown = flying;
        if (draw || !flying) appFlightView!.InvalidateVisual(); // One more frame once empty, to clear it.
    }

    private void HideBattleApps()
    {
        foreach (var icon in appIcons.Values) if (icon.Window.IsVisible) icon.Window.Hide();
        appFlightWindow?.Hide();
        CloseAppHint();
    }

    private void ShowBattleApps()
    {
        foreach (var icon in appIcons.Values) if (!icon.Window.IsVisible) { icon.Window.Show(); icon.Placed = Rect.Empty; }
        if (appFlightWindow is { IsVisible: false }) { appFlightWindow.Show(); appFlightShown = true; }
    }

    private void CloseBattleApps()
    {
        foreach (var icon in appIcons.Values) icon.Window.Close();
        appIcons.Clear();
        appFlightWindow?.Close();
        appFlightWindow = null; appFlightView = null; appFlightShown = false; appFlightScale = 0;
        appGridArea = default; appGridBlockers = Array.Empty<Rect>();
        CloseAppHint();
    }

    // The note beside the icon under the mouse: its name, what it does (or, infected, what it does
    // now and the clicks left), and its charge.
    private void ShowAppHint(int id)
    {
        CloseAppHint();
        appHintFor = id;
        appHintText = new TextBlock { Foreground = Brushes.White, FontSize = PetUi.SmallSize, TextWrapping = TextWrapping.Wrap, MaxWidth = 220, FontFamily = PetUi.TextFont };
        var panel = new Border { Background = HudBack, BorderBrush = PetUi.Game.Rim, BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 5, 9, 6), Margin = new Thickness(4), Child = appHintText };
        appHintWindow = CreateOverlayWindow(panel, "Pet app note", clickThrough: true);
        appHintWindow.SizeToContent = SizeToContent.WidthAndHeight;
        appHintWindow.IsHitTestVisible = false;
        UpdateAppHint();
        appHintWindow.Show();
        UpdateAppHint();
    }

    private void UpdateAppHint()
    {
        if (appHintWindow == null || appHintText == null || battle?.FindApp(appHintFor) is not DesktopApp app || battle.AppRules is not AppsSpec rules) { CloseAppHint(); return; }
        var spec = rules.App(app.Kind);
        string state = app.Infected ? LF($"☣ Nhiễm virus! Bấm thêm {app.ScrubsLeft} lần để quét (hoặc hạ slime đeo ☣).", $"☣ Infected! Click {app.ScrubsLeft} more times to scrub it (or defeat the slime wearing ☣).")
            : app.Ready ? L("Sẵn sàng — bấm để dùng.", "Ready — click to use.")
            : LF($"Đang sạc {Math.Floor(app.Charge * 100)}%", $"Charging {Math.Floor(app.Charge * 100)}%");
        string text = $"{spec.Name(English)}\n{(app.Infected ? spec.Bad(English) : spec.Desc(English))}\n{state}";
        if (appHintText.Tag as string != text)
        {
            appHintText.Inlines.Clear();
            appHintText.Inlines.Add(new System.Windows.Documents.Run(spec.Name(English)) { FontWeight = FontWeights.Bold, FontSize = PetUi.BodySize });
            appHintText.Inlines.Add(new System.Windows.Documents.LineBreak());
            appHintText.Inlines.Add(new System.Windows.Documents.Run(app.Infected ? spec.Bad(English) : spec.Desc(English)));
            appHintText.Inlines.Add(new System.Windows.Documents.LineBreak());
            appHintText.Inlines.Add(new System.Windows.Documents.Run(state) { Foreground = app.Infected ? AppArt.GooGreen : app.Ready ? AppArt.Gold : Brushes.LightGray });
            appHintText.Tag = text;
            appHintWindow.UpdateLayout();
        }
        var centre = BattleAppCentre(app);
        double width = appHintWindow.ActualWidth, height = appHintWindow.ActualHeight;
        var area = battleArea;
        double left = centre.X + DesktopAppIcon.TileSize / 2 + 12;
        if (left + width > area.Right) left = centre.X - DesktopAppIcon.TileSize / 2 - 12 - width;
        double top = Math.Clamp(centre.Y - height / 2, area.Top, Math.Max(area.Top, area.Bottom - height));
        MoveWindow(appHintWindow, left, top, battleScale);
    }

    private void CloseAppHint()
    {
        appHintWindow?.Close();
        appHintWindow = null;
        appHintText = null;
        appHintFor = 0;
    }
}
