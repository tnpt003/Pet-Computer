using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace DesktopPet;

// The tray icon's menu in the same warm look as the pet's right-click menu (PetUi palette):
// white panel, rounded cream-orange hover rows, an orange check, soft separators and, on
// Windows 11, rounded window corners. WinForms only; item text and order are untouched.
public static class PetTrayMenuStyle
{
    public static void Apply(Forms.ContextMenuStrip menu)
    {
        menu.Renderer = new Renderer();
        menu.Font = new Font("Segoe UI", 9.75f);
        menu.BackColor = Color.White;
        menu.ForeColor = ToColor(PetUi.InkHex);
        menu.Padding = new Forms.Padding(4, 4, 4, 4);
        menu.ShowImageMargin = true;
        menu.Opened += (_, _) => RoundCorners(menu.Handle);
        foreach (Forms.ToolStripItem item in menu.Items)
            if (item is Forms.ToolStripMenuItem) item.Padding = new Forms.Padding(2, 5, 2, 5);
    }

    private static Color ToColor(string hex) => ColorTranslator.FromHtml(hex);

    private sealed class Renderer : Forms.ToolStripRenderer
    {
        private static readonly Color Ink = ToColor(PetUi.InkHex), Muted = ToColor(PetUi.MutedHex), Line = ToColor(PetUi.LineHex),
            Accent = ToColor(PetUi.AccentHex), Soft = ToColor(PetUi.AccentSoftHex), Softer = ToColor(PetUi.AccentSofterHex);

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e) => e.Graphics.Clear(Color.White);

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Line);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(Forms.ToolStripRenderEventArgs e) { } // No grey icon column.

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRect(new RectangleF(2, 0.5f, e.Item.Width - 4, e.Item.Height - 1), 6);
            using var brush = new SolidBrush(Soft);
            g.FillPath(brush, path);
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Ink : Muted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var box = e.ImageRectangle;
            box.Inflate(3, 3);
            using (var path = RoundedRect(box, 5))
            using (var back = new SolidBrush(Softer))
                g.FillPath(back, path);
            using var pen = new Pen(Accent, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            float x = box.Left + box.Width * 0.27f, y = box.Top + box.Height * 0.52f;
            g.DrawLines(pen, new[] { new PointF(x, y), new PointF(box.Left + box.Width * 0.44f, box.Top + box.Height * 0.68f),
                new PointF(box.Left + box.Width * 0.74f, box.Top + box.Height * 0.34f) });
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Line);
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // Windows 11 rounds popup corners on request; older Windows ignores the call.
    private static void RoundCorners(IntPtr handle)
    {
        const int CornerPreference = 33, Round = 2;
        int value = Round;
        try { _ = DwmSetWindowAttribute(handle, CornerPreference, ref value, sizeof(int)); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
