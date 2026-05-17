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

            // Dropdown sits just below the input row(s).
            float topOfDropdown = Bounds.Top + ComputeInputRowsHeight() + DropdownPadding / 2f;
            float bottomOfDropdown = topOfDropdown + DropdownHeight + DropdownPadding / 2f;

            // Find where the topmost output landed after base.Layout positioned everything.
            float firstOutputY = float.MaxValue;
            foreach (var output in Owner.Params.Output)
            {
                if (output.Attributes == null) continue;
                if (output.Attributes.Bounds.Y < firstOutputY)
                    firstOutputY = output.Attributes.Bounds.Y;
            }

            // Shift outputs only as far as needed to clear the dropdown's bottom edge.
            float shift = firstOutputY < float.MaxValue
                ? Math.Max(0f, bottomOfDropdown - firstOutputY)
                : 0f;

            if (shift > 0f)
            {
                foreach (var output in Owner.Params.Output)
                {
                    if (output.Attributes == null) continue;
                    var b = output.Attributes.Bounds;
                    b.Y += shift;
                    output.Attributes.Bounds = b;
                    var p = output.Attributes.Pivot;
                    p.Y += shift;
                    output.Attributes.Pivot = p;
                }
            }

            // Expand bounds height to encompass the last output plus a small bottom pad.
            float outputsBottom = Bounds.Top;
            foreach (var output in Owner.Params.Output)
            {
                if (output.Attributes == null) continue;
                if (output.Attributes.Bounds.Bottom > outputsBottom)
                    outputsBottom = output.Attributes.Bounds.Bottom;
            }
            var bounds = Bounds;
            float requiredHeight = outputsBottom - Bounds.Top + DropdownPadding;
            if (requiredHeight > bounds.Height)
            {
                bounds.Height = requiredHeight;
                Bounds = bounds;
            }

            _dropdownRect = new RectangleF(
                Bounds.Left + InnerSidePadding,
                topOfDropdown,
                Bounds.Width - 2 * InnerSidePadding,
                DropdownHeight);
        }

        /// <summary>
        /// Approximate height occupied by input parameter rows. Grasshopper uses
        /// ~20 px per row plus a small header band; this estimate is good enough
        /// for placing the dropdown directly under the input row(s).
        /// </summary>
        private int ComputeInputRowsHeight()
        {
            // Use the actual span of input attributes if available.
            float top = Bounds.Top;
            float bottom = top;
            foreach (var input in Owner.Params.Input)
            {
                if (input.Attributes == null) continue;
                var b = input.Attributes.Bounds;
                if (b.Bottom > bottom) bottom = b.Bottom;
            }
            return (int)Math.Max(20, bottom - top);
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
