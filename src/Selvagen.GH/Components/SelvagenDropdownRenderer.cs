using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Selvagen.GH.Components
{
    internal sealed class SelvagenDropdownRenderer : ToolStripRenderer
    {
        private static readonly Color BackgroundTop = Color.FromArgb(90, 90, 90);
        private static readonly Color BackgroundBottom = Color.FromArgb(50, 50, 50);
        private static readonly Color BorderColor = Color.FromArgb(30, 30, 30);
        private static readonly Color HoverTop = Color.FromArgb(110, 110, 110);
        private static readonly Color HoverBottom = Color.FromArgb(80, 80, 80);
        private static readonly Color TextColor = Color.White;
        private static readonly Color TextDisabled = Color.FromArgb(140, 140, 140);
        private static readonly Color Separator = Color.FromArgb(70, 70, 70);

        private const int ItemHeight = 24;
        private const int CornerRadius = 4;
        private const int TextLeftPadding = 10;

        internal static readonly Padding ItemPadding = new Padding(TextLeftPadding, 4, 8, 4);

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            var rect = e.AffectedBounds;
            using (var path = CreateRoundedRect(rect, CornerRadius))
            using (var fill = new LinearGradientBrush(rect, BackgroundTop, BackgroundBottom, 90f))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(fill, path);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var rect = e.AffectedBounds;
            rect.Width -= 1;
            rect.Height -= 1;
            using (var path = CreateRoundedRect(rect, CornerRadius))
            using (var pen = new Pen(BorderColor, 1f))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var rect = e.Item.ContentRectangle;
            rect.X += 3;
            rect.Width -= 6;
            rect.Y = 1;
            rect.Height = e.Item.Height - 2;

            if (e.Item.Selected && e.Item.Enabled)
            {
                float r = 3f;
                using (var path = CreateRoundedRect(rect, r))
                using (var fill = new LinearGradientBrush(rect, HoverTop, HoverBottom, 90f))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillPath(fill, path);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? TextColor : TextDisabled;
            e.TextFormat |= TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            int left = 8;
            int right = e.Item.Width - 8;
            using (var pen = new Pen(Separator))
            {
                e.Graphics.DrawLine(pen, left, y, right, y);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // suppress the default white image-margin column
        }

        private static GraphicsPath CreateRoundedRect(Rectangle rect, float radius)
        {
            return CreateRoundedRect(
                new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radius);
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
