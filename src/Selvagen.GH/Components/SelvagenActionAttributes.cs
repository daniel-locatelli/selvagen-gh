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
    /// <summary>
    /// Component painter that draws an action button (Upload, Delete, etc.)
    /// below the standard component body, and optionally a single-line dropdown
    /// above it when the host implements <see cref="IInlineTypeDropdown"/>.
    /// </summary>
    public class SelvagenActionAttributes : GH_ComponentAttributes
    {
        private const int ButtonHeight = 22;
        private const int DropdownHeight = 20;
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
            if (_dropdown != null) RenderDropdown(graphics);
            RenderButton(graphics);
        }

        private void RenderDropdown(Graphics g)
        {
            var r = _dropdownRect;
            if (r.Width <= 0 || r.Height <= 0) return;

            using (var path = RoundedRect(r, 2f))
            using (var fill = new SolidBrush(Color.FromArgb(240, 240, 240)))
            using (var pen = new Pen(Color.FromArgb(120, 120, 120), 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            var label = _dropdown.DropdownSelected ?? "";
            using (var font = new Font("Verdana", 6f, FontStyle.Regular))
            using (var brush = new SolidBrush(Color.FromArgb(40, 40, 40)))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(label + "  ▾", font, brush, r, fmt);
        }

        private void RenderButton(Graphics g)
        {
            var r = _buttonRect;
            if (r.Width <= 0 || r.Height <= 0) return;

            using (var path = RoundedRect(r, 3f))
            {
                Color topColor = _button.ButtonGradientTop;
                Color bottomColor = _button.ButtonGradientBottom;
                if (_buttonPressed)
                {
                    topColor    = LightenBy(topColor, 30);
                    bottomColor = LightenBy(bottomColor, 30);
                }

                using (var fill = new LinearGradientBrush(r, topColor, bottomColor, 90f))
                    g.FillPath(fill, path);
                using (var pen = new Pen(Color.FromArgb(30, 30, 30), 1f))
                    g.DrawPath(pen, path);

                var label = _button.IsRunning ? _button.ActionLabelRunning : _button.ActionLabel;
                using (var font = new Font("Verdana", 6f, FontStyle.Regular))
                using (var brush = new SolidBrush(Color.White))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(label, font, brush, r, fmt);
            }
        }

        private static Color LightenBy(Color c, int delta)
        {
            return Color.FromArgb(
                Math.Min(255, c.R + delta),
                Math.Min(255, c.G + delta),
                Math.Min(255, c.B + delta));
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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
