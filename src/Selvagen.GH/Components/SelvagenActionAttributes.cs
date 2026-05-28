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
    /// Component painter that draws an action button (Upload, Delete, etc.)
    /// below the standard component body, and optionally a single-line dropdown
    /// above it when the host implements <see cref="IInlineTypeDropdown"/>.
    /// </summary>
    public class SelvagenActionAttributes : GH_ComponentAttributes
    {
        private const int ButtonHeight = 22;
        private const int DropdownHeight = 22;   // match SelvagenSelectorAttributes
        private const int Padding = 2;

        private readonly ISelvagenActionButton _button;
        private readonly IInlineTypeDropdown _dropdown; // may be null

        private RectangleF _dropdownRect;
        private RectangleF _buttonRect;
        private bool _buttonPressed;
        private float? _naturalHeight;

        public SelvagenActionAttributes(IGH_Component owner) : base(owner)
        {
            _button = owner as ISelvagenActionButton
                ?? throw new ArgumentException("Owner must implement ISelvagenActionButton", nameof(owner));
            _dropdown = owner as IInlineTypeDropdown; // optional
        }

        protected override void Layout()
        {
            base.Layout();

            if (!_naturalHeight.HasValue)
                _naturalHeight = Bounds.Height;

            int extra = Padding + ButtonHeight + Padding;
            if (_dropdown != null) extra += DropdownHeight + Padding;

            var bounds = Bounds;
            bounds.Height = _naturalHeight.Value + extra;
            Bounds = bounds;

            float y = Bounds.Top + _naturalHeight.Value + Padding;

            if (_dropdown != null)
            {
                _dropdownRect = new RectangleF(
                    Bounds.Left + Padding,
                    y,
                    Bounds.Width - 2 * Padding,
                    DropdownHeight);
                y += DropdownHeight + Padding;
            }

            _buttonRect = new RectangleF(
                Bounds.Left + Padding,
                y,
                Bounds.Width - 2 * Padding,
                ButtonHeight);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel != GH_CanvasChannel.Objects) return;
            if (_dropdown != null) SelvagenChrome.DrawDropdown(graphics, _dropdownRect, _dropdown.DropdownSelected, centered: true);
            var label = _button.IsRunning ? _button.ActionLabelRunning : _button.ActionLabel;
            SelvagenChrome.DrawButton(
                graphics,
                _buttonRect,
                label,
                _button.ButtonGradientTop,
                _button.ButtonGradientBottom,
                _buttonPressed);
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_dropdown != null && _dropdownRect.Contains(e.CanvasLocation))
                {
                    ShowDropdownMenu(sender);
                    return GH_ObjectResponse.Handled;
                }

                if (_buttonRect.Contains(e.CanvasLocation))
                {
                    if (_button.IsRunning) return GH_ObjectResponse.Handled;

                    _buttonPressed = true;
                    sender.Refresh();

                    var timer = new Timer { Interval = 100 };
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();
                        timer.Dispose();
                        _buttonPressed = false;
                        sender.Refresh();
                    };
                    timer.Start();

                    _button.RequestAction();
                    return GH_ObjectResponse.Handled;
                }
            }
            return base.RespondToMouseDown(sender, e);
        }

        private void ShowDropdownMenu(GH_Canvas sender)
        {
            var menu = new ContextMenuStrip();
            foreach (var option in _dropdown.DropdownOptions)
            {
                string captured = option;
                var item = new ToolStripMenuItem(option)
                {
                    Checked = option == _dropdown.DropdownSelected,
                };
                item.Click += (s, e) =>
                {
                    _dropdown.DropdownSelected = captured;
                    sender.Refresh();
                };
                menu.Items.Add(item);
            }
            var screenPt = sender.Viewport.ProjectPoint(new PointF(_dropdownRect.Left, _dropdownRect.Bottom));
            menu.Show(sender, new Point((int)screenPt.X, (int)screenPt.Y));
        }
    }
}
