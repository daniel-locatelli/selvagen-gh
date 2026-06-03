using System;
using System.Collections.Generic;
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
        private bool _buttonPressed;

        public SelvagenSelectorAttributes(GH_Component owner) : base(owner) { }

        private ISelectorComponent Selector => (ISelectorComponent)Owner;

        private bool HasFilter => Owner is IFilterDropdownComponent;

        protected override void Layout()
        {
            base.Layout();

            // Read the body height fresh on every Layout. base.Layout() recomputes
            // Bounds from scratch for the current display mode (icon vs. full-name),
            // so caching this value would freeze the icon-mode height and leave the
            // button/dropdowns painted over the taller name-mode body.
            float naturalHeight = Bounds.Height;

            int filterExtra = HasFilter ? ElementGap + DropdownHeight : 0;
            int extra = Padding + ButtonHeight + filterExtra + ElementGap + DropdownHeight + Padding;
            var bounds = Bounds;
            bounds.Height = naturalHeight + extra;
            Bounds = bounds;

            float left = Bounds.Left + Padding;
            float width = Bounds.Width - 2 * Padding;

            _buttonRect = new RectangleF(
                left,
                Bounds.Top + naturalHeight + Padding,
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
            var items = new List<SelvagenChrome.DropdownItem>(filter.FilterOptions.Length);
            for (int i = 0; i < filter.FilterOptions.Length; i++)
            {
                string option = filter.FilterOptions[i];
                string display = filter.FilterDisplayNames[i];
                items.Add(new SelvagenChrome.DropdownItem(
                    label: display,
                    selected: filter.SelectedFilter == option,
                    onClick: () =>
                    {
                        filter.SelectedFilter = option;
                        canvas.Refresh();
                    }));
            }
            SelvagenChrome.ShowStyledMenu(canvas, new PointF(_filterRect.Left, _filterRect.Bottom), items);
        }

        private void ShowDropdownMenu(GH_Canvas canvas)
        {
            var items = new List<SelvagenChrome.DropdownItem>();
            if (Selector.HasItems)
            {
                foreach (var (id, name) in Selector.GetMenuItems())
                {
                    string capturedId = id;
                    items.Add(new SelvagenChrome.DropdownItem(
                        label: name,
                        selected: id == Selector.SelectedId,
                        onClick: () => Selector.SetSelectedId(capturedId)));
                }
            }
            SelvagenChrome.ShowStyledMenu(canvas, new PointF(_dropdownRect.Left, _dropdownRect.Bottom), items);
        }
    }
}
