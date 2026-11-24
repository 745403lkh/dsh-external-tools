using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DshExternalTools
{
    public enum ButtonKind
    {
        Primary,
        Secondary,
        Danger,
        Ghost
    }

    /// <summary>Windows Settings-like visual language: neutral surfaces, one accent.</summary>
    public static class Ui
    {
        public static readonly Color Window = Color.FromArgb(243, 243, 243);
        public static readonly Color SurfaceFill = Color.FromArgb(250, 251, 253);
        public static readonly Color CardFill = Color.FromArgb(251, 251, 251);
        public static readonly Color Ink = Color.FromArgb(27, 27, 27);
        public static readonly Color SecondaryInk = Color.FromArgb(95, 95, 95);
        public static readonly Color Muted = Color.FromArgb(138, 138, 138);
        public static readonly Color Line = Color.FromArgb(229, 229, 229);
        public static readonly Color LineStrong = Color.FromArgb(209, 209, 209);
        public static readonly Color Accent = Color.FromArgb(0, 103, 192);
        public static readonly Color AccentHover = Color.FromArgb(0, 90, 158);
        public static readonly Color AccentSoft = Color.FromArgb(232, 241, 251);
        public static readonly Color Danger = Color.FromArgb(196, 43, 28);
        public static readonly Color DangerSoft = Color.FromArgb(253, 236, 234);
        public static readonly Color Success = Color.FromArgb(15, 123, 15);
        public static readonly Color Warn = Color.FromArgb(157, 93, 0);

        public static string FontFamily
        {
            get { return L.IsEnglish ? "Segoe UI" : "Microsoft YaHei UI"; }
        }

        public static string BodyFontFamily
        {
            get { return L.IsEnglish ? "Segoe UI" : "Microsoft YaHei UI Light"; }
        }

        public static Label Label(Control parent, string text, int left, int top, int width, int height,
            float size, FontStyle style, Color color, bool ellipsis)
        {
            Label label = new Label();
            label.Text = L.T(text);
            label.Left = left;
            label.Top = top;
            label.Width = width;
            int minimumHeight = (int)Math.Ceiling(size * 2.0F);
            label.Height = Math.Max(height, minimumHeight);
            label.Font = new Font(FontFamily, size, style);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoEllipsis = ellipsis;
            label.TextAlign = ContentAlignment.MiddleLeft;
            parent.Controls.Add(label);
            return label;
        }

        public static Button Button(Control parent, string text, int left, int top, int width, int height,
            ButtonKind kind, EventHandler click)
        {
            RectButton button = new RectButton();
            button.Kind = kind;
            button.Text = L.T(text);
            button.Left = left;
            button.Top = top;
            button.Width = width;
            button.Height = height;
            button.Font = new Font(FontFamily, 9F, FontStyle.Regular);
            button.BackColor = parent != null ? parent.BackColor : SurfaceFill;
            button.Margin = new Padding(0, 0, 6, 0);
            button.Click += click;
            parent.Controls.Add(button);
            return button;
        }

        public static Panel Surface()
        {
            return MakePanel(SurfaceFill, 8);
        }

        public static Panel Card()
        {
            return MakePanel(CardFill, 8);
        }

        public static Panel Card(Control parent, int left, int top, int width, int height)
        {
            Panel card = Card();
            card.Left = left;
            card.Top = top;
            card.Width = width;
            card.Height = height;
            parent.Controls.Add(card);
            return card;
        }

        public static Panel Surface(Control parent, int left, int top, int width, int height)
        {
            Panel card = Surface();
            card.Left = left;
            card.Top = top;
            card.Width = width;
            card.Height = height;
            parent.Controls.Add(card);
            return card;
        }

        private static Panel MakePanel(Color backColor, int radius)
        {
            Panel panel = new Panel();
            panel.BackColor = backColor;
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Line))
                {
                    e.Graphics.DrawPath(pen, Rounded(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), radius));
                }
            };
            panel.Resize += delegate
            {
                if (panel.Width <= 0 || panel.Height <= 0) return;
                panel.Region = new Region(Rounded(new Rectangle(0, 0, panel.Width, panel.Height), radius));
            };
            return panel;
        }

        public static ListView List(Control parent)
        {
            ListView list = new ListView();
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.HideSelection = false;
            list.MultiSelect = false;
            list.GridLines = false;
            list.BorderStyle = BorderStyle.None;
            list.BackColor = SurfaceFill;
            list.Font = new Font(FontFamily, 9F);
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            list.ShowItemToolTips = true;
            parent.Controls.Add(list);
            return list;
        }

        public static void Column(ListView list, string text, int width)
        {
            list.Columns.Add(L.T(text), width, HorizontalAlignment.Left);
        }

        public static TextBox ReadOnlyText(Control parent, bool monospace)
        {
            TextBox box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.WordWrap = true;
            box.BorderStyle = BorderStyle.None;
            box.BackColor = parent != null ? parent.BackColor : SurfaceFill;
            box.ForeColor = Ink;
            box.Font = monospace ? new Font("Consolas", 9F) : new Font(BodyFontFamily, 9F);
            parent.Controls.Add(box);
            return box;
        }

        public static Label Hint(Control parent, string text, int left, int top, int width, int height)
        {
            SpacedLabel label = new SpacedLabel();
            label.Text = text;
            label.Left = left;
            label.Top = top;
            label.Width = width;
            label.Height = height;
            label.Font = new Font(BodyFontFamily, 8.5F, FontStyle.Regular);
            label.ForeColor = Muted;
            label.BackColor = Color.Transparent;
            label.TextAlign = ContentAlignment.MiddleLeft;
            parent.Controls.Add(label);
            return label;
        }

        public static List<string> WrapText(Graphics graphics, string text, Font font, int width)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            foreach (string paragraph in normalized.Split('\n'))
            {
                if (paragraph.Length == 0)
                {
                    lines.Add("");
                    continue;
                }
                string current = "";
                foreach (string word in paragraph.Split(' '))
                {
                    string candidate = current.Length == 0 ? word : current + " " + word;
                    if (Measure(graphics, candidate, font) <= width)
                    {
                        current = candidate;
                        continue;
                    }
                    if (current.Length > 0)
                    {
                        lines.Add(current);
                        current = "";
                    }
                    if (Measure(graphics, word, font) <= width)
                    {
                        current = word;
                        continue;
                    }
                    foreach (char character in word)
                    {
                        string piece = current + character;
                        if (current.Length > 0 && Measure(graphics, piece, font) > width)
                        {
                            lines.Add(current);
                            current = "";
                        }
                        current += character;
                    }
                }
                if (current.Length > 0) lines.Add(current);
            }
            return lines;
        }

        private static int Measure(Graphics graphics, string text, Font font)
        {
            return TextRenderer.MeasureText(graphics, text, font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        }

        public static Label Title(Control parent, string text, int left, int top, int width, int height)
        {
            return Label(parent, text, left, top, width, height, 12F, FontStyle.Bold, Ink, false);
        }

        public static Label Section(Control parent, string text, int left, int top, int width)
        {
            return Label(parent, text, left, top, width, 24, 14F, FontStyle.Bold, Ink, false);
        }

        public static Panel Divider(Control parent, int left, int top, int width)
        {
            Panel line = new Panel();
            line.Left = left;
            line.Top = top;
            line.Width = width;
            line.Height = 1;
            line.BackColor = Line;
            parent.Controls.Add(line);
            return line;
        }

        public static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Read-only RichTextBox with explicit 1.33-line leading. A standard
    /// TextBox cannot set leading and cannot be truly transparent, so this
    /// matches the parent surface and removes its border instead of adding a
    /// white slab.
    /// </summary>
    public class LineSpacedTextBox : RichTextBox
    {
        private readonly bool monospace;
        private bool applying;

        public LineSpacedTextBox(bool monospace)
        {
            this.monospace = monospace;
            BorderStyle = BorderStyle.None;
            ReadOnly = true;
            WordWrap = true;
            ScrollBars = RichTextBoxScrollBars.Vertical;
        }

        public override string Text
        {
            get { return base.Text; }
            set
            {
                applying = true;
                base.Text = value;
                applying = false;
                ApplyRtf();
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (!applying) ApplyRtf();
        }

        private void ApplyRtf()
        {
            if (applying) return;
            applying = true;
            try
            {
                int start = SelectionStart;
                string plain = base.Text;
                StringBuilder builder = new StringBuilder();
                builder.Append(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil\fcharset134 Microsoft YaHei UI;}{\f1\fmodern Consolas;}}\viewkind4\uc1\pard\slmult1\sl320");
                builder.Append(monospace ? @"\f1" : @"\f0");
                builder.Append(@"\fs18 ");
                foreach (char character in plain)
                {
                    if (character == '\r') continue;
                    if (character == '\n')
                    {
                        builder.Append(@"\par\n");
                    }
                    else if (character == '\\' || character == '{' || character == '}')
                    {
                        builder.Append('\\').Append(character);
                    }
                    else if (character > 127)
                    {
                        builder.Append(@"\u").Append((int)character).Append('?');
                    }
                    else
                    {
                        builder.Append(character);
                    }
                }
                builder.Append('}');
                Rtf = builder.ToString();
                SelectionStart = Math.Min(start, TextLength);
            }
            catch
            {
            }
            applying = false;
        }
    }
    /// <summary>
    /// Owner-drawn rectangular button with a complete 1px border and 10px inner text
    /// padding. The native Button + Region approach clips the border and makes
    /// long labels touch the edge, so this control paints both itself.
    /// </summary>
    public class RectButton : Button
    {
        public ButtonKind Kind { get; set; }

        private bool hover;
        private bool pressed;

        public RectButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
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
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill;
            Color border;
            Color text;
            if (!Enabled)
            {
                fill = Parent != null ? Parent.BackColor : Ui.SurfaceFill;
                border = Color.FromArgb(214, 219, 229);
                text = Color.FromArgb(170, 176, 188);
            }
            else if (Kind == ButtonKind.Primary)
            {
                fill = pressed ? Color.FromArgb(28, 64, 160) : hover ? Ui.AccentHover : Ui.Accent;
                border = fill;
                text = Color.White;
            }
            else if (Kind == ButtonKind.Danger)
            {
                fill = pressed ? Color.FromArgb(253, 226, 222) : hover ? Ui.DangerSoft : (Parent != null ? Parent.BackColor : Ui.SurfaceFill);
                border = hover ? Color.FromArgb(225, 150, 142) : Ui.LineStrong;
                text = Ui.Danger;
            }
            else if (Kind == ButtonKind.Ghost)
            {
                fill = hover ? Ui.AccentSoft : (Parent != null ? Parent.BackColor : Ui.SurfaceFill);
                border = hover ? Ui.Accent : Color.Transparent;
                text = Ui.Accent;
            }
            else
            {
                fill = pressed ? Ui.AccentSoft : hover ? Color.FromArgb(245, 248, 255) : (Parent != null ? Parent.BackColor : Ui.SurfaceFill);
                border = hover ? Ui.Accent : Ui.LineStrong;
                text = Ui.Ink;
            }

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush brush = new SolidBrush(fill))
            {
                graphics.FillRectangle(brush, bounds);
            }
            if (Kind != ButtonKind.Ghost || hover)
            {
                using (Pen pen = new Pen(border, 1.2F))
                {
                    graphics.DrawRectangle(pen, 1F, 1F, Width - 2F, Height - 2F);
                }
            }

            Rectangle textRect = new Rectangle(10, 0, Math.Max(1, Width - 20), Height);
            TextRenderer.DrawText(graphics, Text, Font, textRect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }
    /// <summary>Label that draws wrapped lines with explicit leading.</summary>
    public class SpacedLabel : Label
    {
        public float LineSpacing = 1.35F;

        public SpacedLabel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color background = Parent != null ? Parent.BackColor : Color.White;
            using (SolidBrush backgroundBrush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
            }
            if (string.IsNullOrEmpty(Text)) return;

            int width = Math.Max(1, ClientSize.Width - Padding.Horizontal);
            List<string> lines = Ui.WrapText(e.Graphics, Text, Font, width);
            float lineHeight = Font.GetHeight(e.Graphics) * LineSpacing;
            int maxLines = Math.Max(1, (int)((ClientSize.Height - Padding.Vertical) / lineHeight));
            int count = Math.Min(maxLines, lines.Count);
            float top = Padding.Top;
            if (TextAlign == ContentAlignment.MiddleLeft || TextAlign == ContentAlignment.MiddleCenter ||
                TextAlign == ContentAlignment.MiddleRight)
            {
                top = (ClientSize.Height - count * lineHeight) / 2F;
            }
            for (int index = 0; index < count; index++)
            {
                string line = lines[index];
                if (index == count - 1 && lines.Count > count) line += "…";
                TextRenderer.DrawText(e.Graphics, line, Font,
                    new Point(Padding.Left, (int)(top + index * lineHeight)), ForeColor,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.Left);
            }
        }
    }
}