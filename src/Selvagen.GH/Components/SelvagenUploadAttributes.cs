using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;

namespace Selvagen.GH.Components
{
    public class SelvagenUploadAttributes : GH_ComponentAttributes
    {
        private const int ButtonHeight = 22;
        private const int TopPadding = 4;
        private const int InnerSidePadding = 6;

        private RectangleF _buttonRect;
        private bool _buttonPressed;
        private float? _naturalHeight;

        public SelvagenUploadAttributes(SelvagenUploadComponentBase owner) : base(owner) { }

        private SelvagenUploadComponentBase UploadOwner => (SelvagenUploadComponentBase)Owner;

        protected override void Layout()
        {
            base.Layout();

            if (!_naturalHeight.HasValue)
                _naturalHeight = Bounds.Height;

            var extra = TopPadding + ButtonHeight;
            var bounds = Bounds;
            bounds.Height = _naturalHeight.Value + extra;
            Bounds = bounds;

            _buttonRect = new RectangleF(
                Bounds.Left + InnerSidePadding,
                Bounds.Top + _naturalHeight.Value + TopPadding / 2f,
                Bounds.Width - 2 * InnerSidePadding,
                ButtonHeight);
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);
            if (channel != GH_CanvasChannel.Objects) return;
            RenderButton(graphics);
        }

        private void RenderButton(Graphics g)
        {
            var r = _buttonRect;
            if (r.Width <= 0 || r.Height <= 0) return;

            var path = RoundedRect(r, 3f);

            Color topColor, bottomColor;
            if (_buttonPressed)
            {
                topColor = Color.FromArgb(100, 160, 140);
                bottomColor = Color.FromArgb(40, 100, 80);
            }
            else
            {
                topColor = Color.FromArgb(60, 140, 120);
                bottomColor = Color.FromArgb(20, 80, 60);
            }

            using (var fill = new LinearGradientBrush(r, topColor, bottomColor, 90f))
                g.FillPath(fill, path);
            using (var pen = new Pen(Color.FromArgb(10, 50, 40), 1f))
                g.DrawPath(pen, path);

            var label = UploadOwner.IsUploading ? "Uploading..." : "Upload";
            using (var font = GH_FontServer.NewFont(GH_FontServer.Standard, 6f))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(label, font, Brushes.White, r, fmt);

            path.Dispose();
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
            if (e.Button == System.Windows.Forms.MouseButtons.Left && _buttonRect.Contains(e.CanvasLocation))
            {
                if (UploadOwner.IsUploading) return GH_ObjectResponse.Handled;

                _buttonPressed = true;
                sender.Refresh();

                var timer = new System.Windows.Forms.Timer { Interval = 100 };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    _buttonPressed = false;
                    sender.Refresh();
                };
                timer.Start();

                UploadOwner.RequestUpload();
                return GH_ObjectResponse.Handled;
            }

            return base.RespondToMouseDown(sender, e);
        }
    }
}
