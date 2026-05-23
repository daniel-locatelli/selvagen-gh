using System;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace Selvagen.GH.Components
{
    /// <summary>
    /// Custom canvas attributes that paint an inline dropdown rectangle inside the
    /// component face and route clicks into <see cref="ISelectorComponent.SetSelectedId"/>.
    /// </summary>
    internal class SelvagenSelectorAttributes : GH_ComponentAttributes
    {
        private const int DropdownHeight = 22;
        private const int DropdownPadding = 4;
        private const int InnerSidePadding = 6;

        private RectangleF _dropdownRect;
        private float? _naturalHeight;

        public SelvagenSelectorAttributes(GH_Component owner) : base(owner) { }

        private ISelectorComponent Selector => (ISelectorComponent)Owner;

        protected override void Layout()
        {
            // Reset bounds to natural height so base.Layout positions params from scratch.
            if (_naturalHeight.HasValue)
            {
                var resetBounds = Bounds;
                resetBounds.Height = _naturalHeight.Value;
                Bounds = resetBounds;
            }

            base.Layout();

            if (!_naturalHeight.HasValue)
            {
                _naturalHeight = Bounds.Height;
            }

            // Expand bounds downward to make room for the dropdown strip.
            // Inputs and outputs keep their natural positions in the upper portion;
            // the dropdown sits as a Karamba-style tag at the bottom.
            int extra = DropdownHeight + DropdownPadding;
            var bounds = Bounds;
            bounds.Height = _naturalHeight.Value + extra;
            Bounds = bounds;

            _dropdownRect = new RectangleF(
                Bounds.Left + InnerSidePadding,
                Bounds.Top + _naturalHeight.Value + DropdownPadding / 2f,
                Bounds.Width - 2 * InnerSidePadding,
                DropdownHeight);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel != GH_CanvasChannel.Objects) return;

            // Capsule looks like a Value List dropdown.
            var capsule = GH_Capsule.CreateCapsule(_dropdownRect, GH_Palette.Black);
            capsule.Render(graphics, Selected, Owner.Locked, false);
            capsule.Dispose();

            // ▼ glyph
            using (var glyphFont = GH_FontServer.NewFont("Verdana", 7f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            using (var glyphFmt = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center })
            using (var textFmt = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
            })
            using (var labelFont = GH_FontServer.NewFont("Verdana", 7.0f, FontStyle.Regular))
            {
                var glyphRect = new RectangleF(_dropdownRect.X + 4, _dropdownRect.Y, 12, _dropdownRect.Height);
                graphics.DrawString("▼", glyphFont, textBrush, glyphRect, glyphFmt);

                var textRect = new RectangleF(
                    _dropdownRect.X + 18,
                    _dropdownRect.Y,
                    _dropdownRect.Width - 22,
                    _dropdownRect.Height);
                graphics.DrawString(Selector.CurrentDisplayText, labelFont, textBrush, textRect, textFmt);
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && _dropdownRect.Contains(e.CanvasLocation))
            {
                ShowDropdownMenu(sender);
                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseDown(sender, e);
        }

        private void ShowDropdownMenu(GH_Canvas canvas)
        {
            var menu = new ToolStripDropDown { AutoClose = true };
            Font boldFont = null;

            if (!Selector.HasItems)
            {
                menu.Items.Add(new ToolStripMenuItem("(no items)") { Enabled = false });
            }
            else
            {
                foreach (var (id, name) in Selector.GetMenuItems())
                {
                    string capturedId = id;
                    Font itemFont;
                    if (id == Selector.SelectedId)
                    {
                        if (boldFont == null) boldFont = new Font(menu.Font, FontStyle.Bold);
                        itemFont = boldFont;
                    }
                    else
                    {
                        itemFont = menu.Font;
                    }
                    var item = new ToolStripMenuItem(name) { Font = itemFont };
                    item.Click += (s, ev) => Selector.SetSelectedId(capturedId);
                    menu.Items.Add(item);
                }
            }

            if (boldFont != null)
            {
                menu.Closed += (s, ev) => boldFont.Dispose();
            }

            // Anchor at the bottom-left of the dropdown rect, in screen coordinates.
            var canvasPt = new PointF(_dropdownRect.Left, _dropdownRect.Bottom);
            var screenPt = canvas.Viewport.ProjectPoint(canvasPt);
            menu.Show(canvas, new Point((int)screenPt.X, (int)screenPt.Y));
        }
    }
}
