using System;
using System.Drawing;
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
        private const int DropdownPadding = 4;
        private const int InnerSidePadding = 6;
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

            int extra = DropdownHeight + DropdownPadding;
            var bounds = Bounds;
            bounds.Height = _naturalHeight.Value + extra;
            Bounds = bounds;

            _dropdownBounds = new RectangleF(
                Bounds.Left + InnerSidePadding,
                Bounds.Top + _naturalHeight.Value + DropdownPadding / 2f,
                Bounds.Width - 2 * InnerSidePadding,
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

            var capsule = GH_Capsule.CreateCapsule(_dropdownBounds, GH_Palette.Black);
            capsule.Render(graphics, Selected, Owner.Locked, false);
            capsule.Dispose();

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
                var glyphRect = new RectangleF(_dropdownBounds.X + 4, _dropdownBounds.Y, 12, _dropdownBounds.Height);
                graphics.DrawString("▼", glyphFont, textBrush, glyphRect, glyphFmt);

                var textRect = new RectangleF(
                    _dropdownBounds.X + 18,
                    _dropdownBounds.Y,
                    _dropdownBounds.Width - 22,
                    _dropdownBounds.Height);
                graphics.DrawString(displayName, labelFont, textBrush, textRect, textFmt);
            }
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left && _dropdownBounds.Contains(e.CanvasLocation))
            {
                var menu = new ToolStripDropDown();
                for (int i = 0; i < SelvagenPropertiesComponent.ModuleOptions.Length; i++)
                {
                    var option = SelvagenPropertiesComponent.ModuleOptions[i];
                    var display = SelvagenPropertiesComponent.ModuleDisplayNames[i];
                    var isSelected = PropertiesOwner.SelectedModule == option;
                    var item = new ToolStripMenuItem(display) { Checked = isSelected, Tag = option };
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
