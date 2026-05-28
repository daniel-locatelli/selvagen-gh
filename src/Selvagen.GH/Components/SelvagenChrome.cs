using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Grasshopper.GUI.Canvas;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Shared drawing primitives + popup-menu factory for Selvagen in-canvas chrome
    /// (action buttons + selection/option dropdowns). One source of truth so the
    /// look stays consistent across SelvagenSelectorAttributes (List/Update components)
    /// and SelvagenActionAttributes (Upload/Delete).
    /// </summary>
    internal static class SelvagenChrome
    {
        // ── Palette ─────────────────────────────────────────────────────
        public static readonly Color ButtonGradientTop    = Color.FromArgb(130, 130, 130);
        public static readonly Color ButtonGradientBottom = Color.FromArgb( 50,  50,  50);
        public static readonly Color BorderColor          = Color.FromArgb( 30,  30,  30);
        public static readonly Color TextColorOnDark      = Color.White;

        // ── Public draw helpers ─────────────────────────────────────────

        /// <summary>
        /// Draw a chrome button with vertical gradient + centered label.
        /// Caller passes the gradient colors; pressed state lightens both stops by ~30.
        /// </summary>
        public static void DrawButton(
            Graphics g,
            RectangleF rect,
            string label,
            Color gradientTop,
            Color gradientBottom,
            bool pressed)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;

            Color top    = pressed ? LightenBy(gradientTop, 30)    : gradientTop;
            Color bottom = pressed ? LightenBy(gradientBottom, 30) : gradientBottom;

            using (var path = RoundedRect(rect, 3f))
            using (var fill = new LinearGradientBrush(rect, top, bottom, 90f))
            using (var pen  = new Pen(BorderColor, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            using (var font  = new Font("Verdana", 6f, FontStyle.Regular))
            using (var brush = new SolidBrush(TextColorOnDark))
            using (var fmt   = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            })
            {
                g.DrawString(label, font, brush, rect, fmt);
            }
        }

        /// <summary>
        /// Draw a chrome dropdown: dark gradient, white triangle disclosure glyph
        /// anchored at the left, supplied text centered in the rect.
        /// Single style for every dropdown.
        /// </summary>
        public static void DrawDropdown(Graphics g, RectangleF rect, string text)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using (var path = RoundedRect(rect, 3f))
            using (var fill = new LinearGradientBrush(rect, ButtonGradientTop, ButtonGradientBottom, 90f))
            using (var pen  = new Pen(BorderColor, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            DrawDropdownGlyph(g, rect);

            // Symmetric inset (~glyph width on both sides) so the text stays
            // visually centered in the dropdown but never collides with the glyph.
            var textRect = new RectangleF(
                rect.X + 14,
                rect.Y,
                rect.Width - 28,
                rect.Height);

            using (var font  = new Font("Verdana", 6f, FontStyle.Regular))
            using (var brush = new SolidBrush(TextColorOnDark))
            using (var fmt   = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming      = StringTrimming.EllipsisCharacter,
                FormatFlags   = StringFormatFlags.NoWrap,
            })
            {
                g.DrawString(text ?? string.Empty, font, brush, textRect, fmt);
            }
        }

        /// <summary>
        /// White triangle disclosure glyph anchored at the left of a dropdown rect.
        /// </summary>
        private static void DrawDropdownGlyph(Graphics g, RectangleF rect)
        {
            float cx = rect.X + 8f;
            float cy = rect.Y + rect.Height / 2f;
            var tri = new PointF[]
            {
                new PointF(cx - 3f, cy - 2f),
                new PointF(cx + 3f, cy - 2f),
                new PointF(cx,      cy + 2f),
            };
            g.FillPolygon(Brushes.White, tri);
        }

        /// <summary>
        /// Rounded rectangle path. Caller owns disposal.
        /// </summary>
        public static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X,             rect.Y,             d, d, 180, 90);
            path.AddArc(rect.Right - d,     rect.Y,             d, d, 270, 90);
            path.AddArc(rect.Right - d,     rect.Bottom - d,    d, d,   0, 90);
            path.AddArc(rect.X,             rect.Bottom - d,    d, d,  90, 90);
            path.CloseFigure();
            return path;
        }

        // ── Popup menu factory ─────────────────────────────────────────

        /// <summary>
        /// One item shown in a styled dropdown menu.
        /// </summary>
        public readonly struct DropdownItem
        {
            public readonly string Label;
            public readonly bool Selected;
            public readonly Action OnClick;

            public DropdownItem(string label, bool selected, Action onClick)
            {
                Label = label;
                Selected = selected;
                OnClick = onClick;
            }
        }

        /// <summary>
        /// Show a dark-themed popup menu anchored beneath the dropdown rect.
        /// Bold font marks the currently-selected item. An empty item list renders
        /// a single disabled "(no items)" placeholder.
        /// </summary>
        public static void ShowStyledMenu(
            GH_Canvas canvas,
            PointF anchorBottomLeftCanvas,
            IReadOnlyList<DropdownItem> items)
        {
            var menu = new ToolStripDropDownMenu
            {
                AutoClose = true,
                Renderer = new SelvagenDropdownRenderer(),
                ShowImageMargin = false,
                ShowCheckMargin = false,
                Padding = new Padding(0, 4, 0, 4),
            };

            Font boldFont = null;

            if (items == null || items.Count == 0)
            {
                menu.Items.Add(new ToolStripMenuItem("(no items)")
                {
                    Enabled = false,
                    Padding = SelvagenDropdownRenderer.ItemPadding,
                });
            }
            else
            {
                foreach (var entry in items)
                {
                    Font font;
                    if (entry.Selected)
                    {
                        if (boldFont == null) boldFont = new Font(menu.Font, FontStyle.Bold);
                        font = boldFont;
                    }
                    else
                    {
                        font = menu.Font;
                    }

                    var click = entry.OnClick;
                    var item = new ToolStripMenuItem(entry.Label)
                    {
                        Font = font,
                        Padding = SelvagenDropdownRenderer.ItemPadding,
                    };
                    item.Click += (s, ev) => click?.Invoke();
                    menu.Items.Add(item);
                }
            }

            menu.Closed += (s, ev) => boldFont?.Dispose();

            var screenPt = canvas.Viewport.ProjectPoint(anchorBottomLeftCanvas);
            menu.Show(canvas, new Point((int)screenPt.X, (int)screenPt.Y));
        }

        // ── Internals ────────────────────────────────────────────────────
        private static Color LightenBy(Color c, int delta)
        {
            return Color.FromArgb(
                Math.Min(255, c.R + delta),
                Math.Min(255, c.G + delta),
                Math.Min(255, c.B + delta));
        }
    }
}
