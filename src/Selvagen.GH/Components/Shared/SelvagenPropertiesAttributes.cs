using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace Selvagen.GH.Components
{
    public class SelvagenPropertiesAttributes : GH_ComponentAttributes
    {
        private const int DropdownHeight = 22;
        private const int Padding = 2;
        private RectangleF _dropdownBounds;
        private float? _naturalHeight;

        public SelvagenPropertiesAttributes(SelvagenPropertiesComponent owner) : base(owner) { }

        private SelvagenPropertiesComponent PropertiesOwner => (SelvagenPropertiesComponent)Owner;

        protected override void Layout()
        {
            if (_naturalHeight.HasValue)
            {
                var resetBounds = Bounds;
                resetBounds.Height = _naturalHeight.Value;
                Bounds = resetBounds;
            }

            base.Layout();

            if (!_naturalHeight.HasValue)
                _naturalHeight = Bounds.Height;

            int extra = Padding + DropdownHeight + Padding;
            var bounds = Bounds;
            bounds.Height = _naturalHeight.Value + extra;
            Bounds = bounds;

            _dropdownBounds = new RectangleF(
                Bounds.Left + Padding,
                Bounds.Top + _naturalHeight.Value + Padding,
                Bounds.Width - 2 * Padding,
                DropdownHeight);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel != GH_CanvasChannel.Objects) return;

            var selectedIndex = Array.IndexOf(
                SelvagenPropertiesComponent.ModuleOptions,
                PropertiesOwner.SelectedModule);
            var displayName = selectedIndex >= 0
                ? SelvagenPropertiesComponent.ModuleDisplayNames[selectedIndex]
                : PropertiesOwner.SelectedModule;

            float radius = 3f;
            using (var path = CreateRoundedRect(_dropdownBounds, radius))
            {
                using (var fill = new LinearGradientBrush(
                    _dropdownBounds,
                    Color.FromArgb(130, 130, 130),
                    Color.FromArgb(50, 50, 50),
                    90f))
                {
                    graphics.FillPath(fill, path);
                }
                using (var border = new Pen(Color.FromArgb(30, 30, 30), 1f))
                {
                    graphics.DrawPath(border, path);
                }
            }

            DrawDropdownGlyph(graphics, _dropdownBounds);

            using (var textBrush = new SolidBrush(Color.White))
            using (var textFmt = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
            })
            using (var labelFont = new Font("Verdana", 6f, FontStyle.Regular))
            {
                var textRect = new RectangleF(
                    _dropdownBounds.X + 14,
                    _dropdownBounds.Y,
                    _dropdownBounds.Width - 18,
                    _dropdownBounds.Height);
                graphics.DrawString(displayName, labelFont, textBrush, textRect, textFmt);
            }
        }

        private static void DrawDropdownGlyph(Graphics g, RectangleF rect)
        {
            float cx = rect.X + 8f;
            float cy = rect.Y + rect.Height / 2f;
            var tri = new PointF[]
            {
                new PointF(cx - 3f, cy - 2f),
                new PointF(cx + 3f, cy - 2f),
                new PointF(cx, cy + 2f),
            };
            g.FillPolygon(Brushes.White, tri);
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

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && _dropdownBounds.Contains(e.CanvasLocation))
            {
                var menu = new ToolStripDropDownMenu
                {
                    Renderer = new SelvagenDropdownRenderer(),
                    ShowImageMargin = false,
                    ShowCheckMargin = false,
                    Padding = new Padding(0, 4, 0, 4),
                };
                for (int i = 0; i < SelvagenPropertiesComponent.ModuleOptions.Length; i++)
                {
                    var option = SelvagenPropertiesComponent.ModuleOptions[i];
                    var display = SelvagenPropertiesComponent.ModuleDisplayNames[i];
                    var isSelected = PropertiesOwner.SelectedModule == option;
                    var item = new ToolStripMenuItem(display)
                    {
                        Tag = option,
                        Padding = SelvagenDropdownRenderer.ItemPadding,
                        Font = isSelected ? new Font(menu.Font, FontStyle.Bold) : menu.Font,
                    };
                    item.Click += (s, args) =>
                    {
                        PropertiesOwner.SelectedModule = ((ToolStripMenuItem)s).Tag.ToString();
                        sender.Refresh();
                    };
                    menu.Items.Add(item);
                }
                menu.Show(sender, sender.PointToClient(Cursor.Position));
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDown(sender, e);
        }
    }
}
