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
    /// Custom canvas attributes that paint an Update button and an inline dropdown
    /// rectangle inside the component face.
    /// </summary>
    internal class SelvagenSelectorAttributes : GH_ComponentAttributes
    {
        private const int ButtonHeight = 22;
        private const int DropdownHeight = 22;
        private const int Padding = 2;
        private const int ElementGap = 2;

        private RectangleF _buttonRect;
        private RectangleF _filterRect;
        private RectangleF _dropdownRect;
        private float? _naturalHeight;
        private bool _buttonPressed;

        public SelvagenSelectorAttributes(GH_Component owner) : base(owner) { }

        private ISelectorComponent Selector => (ISelectorComponent)Owner;

        private bool HasFilter => Owner is IFilterDropdownComponent;

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
            {
                _naturalHeight = Bounds.Height;
            }

            int filterExtra = HasFilter ? ElementGap + DropdownHeight : 0;
            int extra = Padding + ButtonHeight + filterExtra + ElementGap + DropdownHeight + Padding;
            var bounds = Bounds;
            bounds.Height = _naturalHeight.Value + extra;
            Bounds = bounds;

            float left = Bounds.Left + Padding;
            float width = Bounds.Width - 2 * Padding;

            _buttonRect = new RectangleF(
                left,
                Bounds.Top + _naturalHeight.Value + Padding,
                width,
                ButtonHeight);

            float nextY = _buttonRect.Bottom + ElementGap;

            if (HasFilter)
            {
                _filterRect = new RectangleF(left, nextY, width, DropdownHeight);
                nextY = _filterRect.Bottom + ElementGap;
            }

            _dropdownRect = new RectangleF(left, nextY, width, DropdownHeight);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel != GH_CanvasChannel.Objects) return;

            RenderButton(graphics);
            if (HasFilter) RenderFilterDropdown(graphics);
            RenderDropdown(graphics);
        }

        private void RenderButton(Graphics graphics)
        {
            SelvagenChrome.DrawButton(
                graphics,
                _buttonRect,
                "Update",
                SelvagenChrome.ButtonGradientTop,
                SelvagenChrome.ButtonGradientBottom,
                _buttonPressed);
        }

        private void RenderFilterDropdown(Graphics graphics)
        {
            var filter = (IFilterDropdownComponent)Owner;
            int idx = Array.IndexOf(filter.FilterOptions, filter.SelectedFilter);
            string displayText = idx >= 0 ? filter.FilterDisplayNames[idx] : filter.SelectedFilter;
            SelvagenChrome.DrawDropdown(graphics, _filterRect, displayText);
        }

        private void RenderDropdown(Graphics graphics)
        {
            SelvagenChrome.DrawDropdown(graphics, _dropdownRect, Selector.CurrentDisplayText);
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_buttonRect.Contains(e.CanvasLocation))
                {
                    _buttonPressed = true;
                    sender.Refresh();

                    var timer = new Timer { Interval = 100 };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        timer.Dispose();
                        _buttonPressed = false;
                        sender.Refresh();
                    };
                    timer.Start();

                    Selector.RequestUpdate();
                    return GH_ObjectResponse.Handled;
                }

                if (HasFilter && _filterRect.Contains(e.CanvasLocation))
                {
                    ShowFilterMenu(sender);
                    return GH_ObjectResponse.Handled;
                }

                if (_dropdownRect.Contains(e.CanvasLocation))
                {
                    ShowDropdownMenu(sender);
                    return GH_ObjectResponse.Handled;
                }
            }
            return base.RespondToMouseDown(sender, e);
        }

        private void ShowFilterMenu(GH_Canvas canvas)
        {
            var filter = (IFilterDropdownComponent)Owner;
            var menu = new ToolStripDropDownMenu
            {
                AutoClose = true,
                Renderer = new SelvagenDropdownRenderer(),
                ShowImageMargin = false,
                ShowCheckMargin = false,
                Padding = new Padding(0, 4, 0, 4),
            };
            Font boldFont = null;

            for (int i = 0; i < filter.FilterOptions.Length; i++)
            {
                string option = filter.FilterOptions[i];
                string display = filter.FilterDisplayNames[i];
                bool isSelected = filter.SelectedFilter == option;

                Font itemFont;
                if (isSelected)
                {
                    if (boldFont == null) boldFont = new Font(menu.Font, FontStyle.Bold);
                    itemFont = boldFont;
                }
                else
                {
                    itemFont = menu.Font;
                }

                var item = new ToolStripMenuItem(display)
                {
                    Tag = option,
                    Font = itemFont,
                    Padding = SelvagenDropdownRenderer.ItemPadding,
                };
                item.Click += (s, ev) =>
                {
                    filter.SelectedFilter = ((ToolStripMenuItem)s).Tag.ToString();
                    canvas.Refresh();
                };
                menu.Items.Add(item);
            }

            menu.Closed += (s, ev) => boldFont?.Dispose();

            var canvasPt = new PointF(_filterRect.Left, _filterRect.Bottom);
            var screenPt = canvas.Viewport.ProjectPoint(canvasPt);
            menu.Show(canvas, new Point((int)screenPt.X, (int)screenPt.Y));
        }

        private void ShowDropdownMenu(GH_Canvas canvas)
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

            if (!Selector.HasItems)
            {
                var empty = new ToolStripMenuItem("(no items)")
                {
                    Enabled = false,
                    Padding = SelvagenDropdownRenderer.ItemPadding,
                };
                menu.Items.Add(empty);
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
                    var item = new ToolStripMenuItem(name)
                    {
                        Font = itemFont,
                        Padding = SelvagenDropdownRenderer.ItemPadding,
                    };
                    item.Click += (s, ev) => Selector.SetSelectedId(capturedId);
                    menu.Items.Add(item);
                }
            }

            menu.Closed += (s, ev) => boldFont?.Dispose();

            var canvasPt = new PointF(_dropdownRect.Left, _dropdownRect.Bottom);
            var screenPt = canvas.Viewport.ProjectPoint(canvasPt);
            menu.Show(canvas, new Point((int)screenPt.X, (int)screenPt.Y));
        }
    }
}
