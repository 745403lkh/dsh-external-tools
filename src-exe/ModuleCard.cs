using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DshExternalTools
{
    /// <summary>A quiet module card: line icon, title, description, chevron.</summary>
    public class ModuleCard : Control
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string IconId { get; set; }
        public string Category { get; set; }
        public event EventHandler Activated;

        private bool hover;

        public ModuleCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            Size = new Size(310, 116);
            BackColor = Color.Transparent;
            Font = new Font(Ui.FontFamily, 9F);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            EventHandler handler = Activated;
            if (handler != null) handler(this, EventArgs.Empty);
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Ui.Rounded(bounds, 8))
            {
                using (SolidBrush brush = new SolidBrush(hover ? Color.FromArgb(250, 251, 253) : Ui.SurfaceFill))
                {
                    graphics.FillPath(brush, path);
                }
                using (Pen pen = new Pen(hover ? Ui.Accent : Ui.Line, 1F))
                {
                    graphics.DrawPath(pen, path);
                }
            }

            Rectangle icon = new Rectangle(18, 20, 36, 36);
            using (GraphicsPath path = Ui.Rounded(icon, 8))
            using (SolidBrush brush = new SolidBrush(hover ? Ui.AccentSoft : Color.FromArgb(244, 246, 249)))
            {
                graphics.FillPath(brush, path);
            }
            ModuleIcon.Draw(graphics, IconId, icon, hover ? Ui.Accent : Ui.SecondaryInk, 1.7F);

            using (Font font = new Font(Ui.FontFamily, 10.5F, FontStyle.Bold))
            using (SolidBrush brush = new SolidBrush(Ui.Ink))
            {
                graphics.DrawString(L.T(Title ?? ""), font, brush, new PointF(68, 18));
            }
            if (!string.IsNullOrEmpty(Description))
            {
                using (Font font = new Font(Ui.BodyFontFamily, 8.5F))
                {
                    TextRenderer.DrawText(graphics, L.T(Description), font,
                        new Rectangle(70, 42, Width - 104, 48), Ui.Muted,
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                }
            }
            if (!string.IsNullOrEmpty(Category))
            {
                using (Font font = new Font(Ui.FontFamily, 7.5F, FontStyle.Regular))
                {
                    TextRenderer.DrawText(graphics, L.T(Category), font,
                        new Rectangle(Width - 72, 14, 58, 18), Ui.Muted,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                }
            }
            using (Font font = new Font(Ui.FontFamily, 12F, FontStyle.Regular))
            using (SolidBrush brush = new SolidBrush(hover ? Ui.Accent : Ui.Muted))
            {
                graphics.DrawString("›", font, brush, new PointF(Width - 24, Height - 39));
            }
        }
    }

    /// <summary>An outlined pill, used for the header's About action.</summary>
    public class PillButton : Button
    {
        private bool hover;

        public PillButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Ui.SurfaceFill;
            ForeColor = Ui.Ink;
            Font = new Font(Ui.FontFamily, 9F, FontStyle.Regular);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Ui.Rounded(bounds, Height / 2))
            {
                using (SolidBrush brush = new SolidBrush(hover ? Ui.AccentSoft : BackColor))
                {
                    graphics.FillPath(brush, path);
                }
                using (Pen pen = new Pen(hover ? Ui.Accent : Ui.LineStrong, 1F))
                {
                    graphics.DrawPath(pen, path);
                }
            }
            TextRenderer.DrawText(graphics, Text, Font, ClientRectangle, hover ? Ui.Accent : ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>A minimal status pill with a dot and text.</summary>
    public class StatusPill : Control
    {
        public Color Tone { get; set; }

        public StatusPill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(104, 24);
            Font = new Font(Ui.FontFamily, 8.5F, FontStyle.Regular);
            Tone = Ui.Muted;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Ui.Rounded(bounds, Height / 2))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(250, 250, 250)))
            {
                graphics.FillPath(brush, path);
            }
            using (Pen pen = new Pen(Ui.Line))
            using (GraphicsPath path = Ui.Rounded(bounds, Height / 2))
            {
                graphics.DrawPath(pen, path);
            }
            using (SolidBrush brush = new SolidBrush(Tone))
            {
                graphics.FillEllipse(brush, 10, Height / 2 - 3, 6, 6);
            }
            TextRenderer.DrawText(graphics, Text, Font,
                new Rectangle(21, 0, Width - 26, Height), Ui.SecondaryInk,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>A compact dashboard value with a line icon.</summary>
    public class StatCard : Control
    {
        public string Caption { get; set; }
        public string Value { get; set; }
        public string IconId { get; set; }

        public StatCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(236, 70);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Ui.Rounded(bounds, 8))
            {
                using (SolidBrush brush = new SolidBrush(Ui.SurfaceFill))
                {
                    graphics.FillPath(brush, path);
                }
                using (Pen pen = new Pen(Ui.Line))
                {
                    graphics.DrawPath(pen, path);
                }
            }
            Rectangle icon = new Rectangle(14, 24, 22, 22);
            ModuleIcon.Draw(graphics, IconId, icon, Ui.SecondaryInk, 1.5F);
            using (Font font = new Font(Ui.FontFamily, 8F, FontStyle.Regular))
            using (SolidBrush brush = new SolidBrush(Ui.Muted))
            {
                graphics.DrawString(L.T(Caption ?? ""), font, brush, new PointF(46, 17));
            }
            using (Font font = new Font(Ui.FontFamily, 11.5F, FontStyle.Bold))
            using (SolidBrush brush = new SolidBrush(Ui.Ink))
            {
                graphics.DrawString(L.T(Value ?? "—"), font, brush, new PointF(46, 35));
            }
        }
    }
}
