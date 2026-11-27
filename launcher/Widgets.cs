// The launcher's own drawn controls: a toggle switch, a mode card, a gradient button, a side
// navigation button and a gradient banner. Plain GDI+, .NET Framework 4, C# 5.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace VIctoryLauncher
{
    static class Theme
    {
        public static readonly Color Back = Color.FromArgb(16, 14, 24);
        public static readonly Color Side = Color.FromArgb(22, 19, 34);
        public static readonly Color Card = Color.FromArgb(30, 27, 45);
        public static readonly Color CardHover = Color.FromArgb(40, 36, 60);
        public static readonly Color Line = Color.FromArgb(58, 52, 84);
        public static readonly Color Pink = Color.FromArgb(255, 72, 160);
        public static readonly Color Violet = Color.FromArgb(136, 84, 255);
        public static readonly Color Cyan = Color.FromArgb(64, 210, 255);
        public static readonly Color Text = Color.FromArgb(240, 237, 250);
        public static readonly Color Dim = Color.FromArgb(158, 152, 184);
        public static readonly Color Good = Color.FromArgb(96, 224, 150);
        public static readonly Color Warn = Color.FromArgb(255, 176, 64);

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
    }

    // A switch with its text to the right; Checked as a CheckBox.
    class Toggle : CheckBox
    {
        bool hover;
        public Toggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            AutoSize = false;
            Height = 30;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            ForeColor = Theme.Text;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            var track = new RectangleF(1, (Height - 20) / 2f, 38, 20);
            using (var path = Theme.Round(track, 10))
            {
                if (Checked)
                    using (var b = new LinearGradientBrush(track, Theme.Pink, Theme.Violet, 0f)) g.FillPath(b, path);
                else
                    using (var b = new SolidBrush(hover ? Theme.CardHover : Theme.Line)) g.FillPath(b, path);
            }
            float kx = Checked ? track.Right - 18 : track.X + 2;
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, kx, track.Y + 2, 16, 16);
            var text = new RectangleF(48, 0, Width - 48, Height);
            using (var b = new SolidBrush(Enabled ? ForeColor : Theme.Dim))
            using (var f = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(Text, Font, b, text, f);
        }
    }

    // A mode to pick: name, a tag (resolution) and a short line.
    class ModeCard : Control
    {
        public string Title, Tag2, Line;
        bool selected, hover;
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }
        public ModeCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Size = new Size(160, 92);
            Font = new Font("Segoe UI", 9f);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var path = Theme.Round(r, 10))
            {
                using (var b = new SolidBrush(selected ? Color.FromArgb(48, 30, 64) : hover ? Theme.CardHover : Theme.Card)) g.FillPath(b, path);
                if (selected)
                    using (var b = new LinearGradientBrush(r, Theme.Pink, Theme.Violet, 45f))
                    using (var p = new Pen(b, 2f)) g.DrawPath(p, path);
                else
                    using (var p = new Pen(Theme.Line, 1f)) g.DrawPath(p, path);
            }
            using (var tf = new Font("Segoe UI Semibold", 10.5f))
            using (var b = new SolidBrush(Theme.Text)) g.DrawString(Title ?? "", tf, b, new RectangleF(10, 8, Width - 20, 22));
            if (!string.IsNullOrEmpty(Tag2))
            {
                var size = g.MeasureString(Tag2, Font);
                var pill = new RectangleF(10, 32, size.Width + 10, 18);
                using (var path = Theme.Round(pill, 9))
                using (var b = new SolidBrush(selected ? Theme.Pink : Theme.Line)) g.FillPath(b, path);
                using (var b = new SolidBrush(Color.White)) g.DrawString(Tag2, Font, b, pill.X + 5, pill.Y + 1);
            }
            using (var b = new SolidBrush(Theme.Dim))
            using (var f = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                g.DrawString(Line ?? "", Font, b, new RectangleF(10, 54, Width - 18, Height - 58), f);
        }
    }

    // A rounded button: a gradient for the main action, outlined otherwise.
    class Pill : Button
    {
        public bool Main;
        bool hover, down;
        public Pill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            ForeColor = Theme.Text;
            Height = 34;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            float radius = Main ? Height / 2f - 1 : 8;
            using (var path = Theme.Round(r, radius))
            {
                if (!Enabled)
                {
                    using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, path);
                    using (var p = new Pen(Theme.Line)) g.DrawPath(p, path);
                }
                else if (Main)
                {
                    var a = down ? Color.FromArgb(220, 50, 140) : hover ? Color.FromArgb(255, 110, 185) : Theme.Pink;
                    var z = down ? Color.FromArgb(110, 60, 220) : hover ? Color.FromArgb(160, 120, 255) : Theme.Violet;
                    using (var b = new LinearGradientBrush(r, a, z, 0f)) g.FillPath(b, path);
                }
                else
                {
                    using (var b = new SolidBrush(hover ? Theme.CardHover : Theme.Card)) g.FillPath(b, path);
                    using (var p = new Pen(hover ? Theme.Pink : Theme.Line, 1.2f)) g.DrawPath(p, path);
                }
            }
            using (var b = new SolidBrush(Enabled ? (Main ? Color.White : ForeColor) : Theme.Dim))
            using (var f = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(Text, Font, b, r, f);
        }
    }

    // A button of the side bar.
    class NavButton : Control
    {
        public string Glyph;
        bool active, hover;
        public bool Active { get { return active; } set { active = value; Invalidate(); } }
        public NavButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Size = new Size(196, 44);
            Font = new Font("Segoe UI Semibold", 10.5f);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Side);
            Theme.Smooth(g);
            var r = new RectangleF(10, 3, Width - 20, Height - 6);
            if (active || hover)
                using (var path = Theme.Round(r, 8))
                using (var b = new SolidBrush(active ? Color.FromArgb(44, 30, 62) : Theme.Card)) g.FillPath(b, path);
            if (active)
                using (var b = new LinearGradientBrush(new RectangleF(10, 10, 4, Height - 20), Theme.Pink, Theme.Violet, 90f))
                using (var path = Theme.Round(new RectangleF(10, 10, 4, Height - 20), 2)) g.FillPath(b, path);
            using (var gf = new Font("Segoe MDL2 Assets", 12f))
            using (var b = new SolidBrush(active ? Theme.Pink : Theme.Dim))
                g.DrawString(Glyph ?? "", gf, b, 26, Height / 2f - 9);
            using (var b = new SolidBrush(active ? Theme.Text : Theme.Dim))
            using (var f = new StringFormat { LineAlignment = StringAlignment.Center })
                g.DrawString(Text, Font, b, new RectangleF(56, 0, Width - 60, Height), f);
        }
    }

    // The top banner: a gradient, the title and a row of chips.
    class Banner : Control
    {
        public string Title = "", Subtitle = "";
        public string[] Chips = new string[0];
        public Color[] ChipColors = new Color[0];
        public Banner()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 128;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Round(r, 14))
            {
                using (var b = new LinearGradientBrush(r, Color.FromArgb(120, 24, 92), Color.FromArgb(40, 26, 110), 15f)) g.FillPath(b, path);
                var state = g.Save();
                g.SetClip(path);
                // a soft light in the upper right corner, and a sunset stripe
                using (var glow = new GraphicsPath())
                {
                    glow.AddEllipse(Width - 330, -160, 460, 360);
                    using (var pb = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(120, 255, 120, 200), SurroundColors = new[] { Color.FromArgb(0, 255, 120, 200) } })
                        g.FillPath(pb, glow);
                }
                using (var b = new LinearGradientBrush(new RectangleF(0, Height - 5, Width, 5), Theme.Pink, Theme.Cyan, 0f)) g.FillRectangle(b, 0, Height - 5, Width, 5);
                g.Restore(state);
            }
            using (var tf = new Font("Segoe UI Black", 22f))
            using (var b = new SolidBrush(Color.White)) g.DrawString(Title, tf, b, 22, 12);
            using (var sf = new Font("Segoe UI", 10f))
            using (var b = new SolidBrush(Color.FromArgb(225, 215, 240))) g.DrawString(Subtitle, sf, b, 25, 52);
            float x = 24;
            using (var cf = new Font("Segoe UI Semibold", 9f))
                for (int i = 0; i < Chips.Length; i++)
                {
                    var size = g.MeasureString(Chips[i], cf);
                    var pill = new RectangleF(x, 84, size.Width + 22, 24);
                    using (var path = Theme.Round(pill, 12))
                    using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillPath(b, path);
                    var dot = i < ChipColors.Length ? ChipColors[i] : Theme.Good;
                    using (var b = new SolidBrush(dot)) g.FillEllipse(b, pill.X + 8, pill.Y + 9, 7, 7);
                    using (var b = new SolidBrush(Color.White)) g.DrawString(Chips[i], cf, b, pill.X + 17, pill.Y + 4);
                    x += pill.Width + 8;
                }
        }
    }

    // A loading bar with its text.
    class LoadBar : Control
    {
        float done;
        public LoadBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = new Font("Segoe UI Semibold", 9f);
        }
        public void Set(float part, string text) { done = Math.Max(0f, Math.Min(1f, part)); Text = text; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            var track = new RectangleF(0, Height - 8, Width - 1, 7);
            using (var path = Theme.Round(track, 3.5f))
            using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, path);
            if (done > 0)
            {
                var fill = new RectangleF(0, Height - 8, Math.Max(7, (Width - 1) * done), 7);
                using (var path = Theme.Round(fill, 3.5f))
                using (var b = new LinearGradientBrush(track, Theme.Pink, Theme.Cyan, 0f)) g.FillPath(b, path);
            }
            using (var b = new SolidBrush(Theme.Text)) g.DrawString(Text, Font, b, 0, 0);
            var pct = ((int)(done * 100)).ToString() + "%";
            var size = g.MeasureString(pct, Font);
            using (var b = new SolidBrush(Theme.Dim)) g.DrawString(pct, Font, b, Width - size.Width, 0);
        }
    }

    // A section title with a thin line after it.
    class Heading : Control
    {
        public Heading(string text)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            Height = 26;
            Font = new Font("Segoe UI Semibold", 11f);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            var size = g.MeasureString(Text, Font);
            using (var b = new SolidBrush(Theme.Text)) g.DrawString(Text, Font, b, 0, 2);
            using (var p = new Pen(Theme.Line)) g.DrawLine(p, size.Width + 8, Height / 2f + 1, Width, Height / 2f + 1);
        }
    }
}
