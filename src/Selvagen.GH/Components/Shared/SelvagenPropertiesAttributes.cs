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
        private RectangleF _dropdownBounds;

        public SelvagenPropertiesAttributes(SelvagenPropertiesComponent owner) : base(owner) { }

        private SelvagenPropertiesComponent PropertiesOwner => (SelvagenPropertiesComponent)Owner;

        protected override void Layout()
        {
            base.Layout();
            var bounds = Bounds;
            _dropdownBounds = new RectangleF(
                bounds.X + 2,
                bounds.Bottom,
                bounds.Width - 4,
                DropdownHeight - 2);
            bounds.Height += DropdownHeight;
            Bounds = bounds;
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

            using (var fill = new SolidBrush(Color.FromArgb(240, 240, 240)))
            using (var border = new Pen(Color.FromArgb(160, 160, 160)))
            using (var textBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
            {
                graphics.FillRectangle(fill, _dropdownBounds);
                graphics.DrawRectangle(border,
                    _dropdownBounds.X, _dropdownBounds.Y,
                    _dropdownBounds.Width, _dropdownBounds.Height);

                var textRect = new RectangleF(
                    _dropdownBounds.X + 4, _dropdownBounds.Y,
                    _dropdownBounds.Width - 20, _dropdownBounds.Height);
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                graphics.DrawString(displayName, GH_FontServer.Standard, textBrush, textRect, sf);
                graphics.DrawString("▼", GH_FontServer.Small, textBrush,
                    _dropdownBounds.Right - 16, _dropdownBounds.Y + 4);
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
