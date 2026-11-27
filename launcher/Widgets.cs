// The launcher's own drawn controls, in the Cold Project style: a dark navy window, panels one
// shade up with a 1 px border, small corners, one cyan accent, monospaced labels and numbers.
// Plain GDI+, .NET Framework 4, C# 5.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace VIctoryLauncher
{
    static class Theme
    {
        // PayDrop palette: deep plum, a magenta glow, butter yellow as the one accent (the main button,
        // switches on), peach and hot pink beside it, white hairlines and orbit rings.
        public static readonly Color Back = Color.FromArgb(36, 12, 32);
        public static readonly Color Side = Color.FromArgb(28, 9, 25);
        public static readonly Color Card = Color.FromArgb(52, 20, 46);
        public static readonly Color CardHover = Color.FromArgb(66, 26, 58);
        public static readonly Color Selected = Color.FromArgb(82, 26, 68);
        public static readonly Color Line = Color.FromArgb(82, 55, 77);
        // The one accent (the names stay from the older themes: Pink, Cyan are the accent, Violet its pressed side).
        public static readonly Color Pink = Color.FromArgb(255, 244, 163);
        public static readonly Color Violet = Color.FromArgb(236, 220, 120);
        public static readonly Color Cyan = Color.FromArgb(255, 244, 163);
        public static readonly Color AccentHover = Color.FromArgb(255, 249, 200);
        // Text drawn on the accent.
        public static readonly Color OnAccent = Color.FromArgb(42, 10, 36);
        public static readonly Color Text = Color.FromArgb(255, 246, 252);
        public static readonly Color Dim = Color.FromArgb(214, 186, 206);
        // Text between the two: descriptions that still need to read well.
        public static readonly Color Soft = Color.FromArgb(236, 220, 231);
        public static readonly Color Good = Color.FromArgb(255, 244, 163);
        public static readonly Color Warn = Color.FromArgb(255, 150, 136);
        public static readonly Color Bad = Color.FromArgb(255, 110, 140);
        // PayDrop's other colours: the hot pink and the peach of its milestone dots.
        public static readonly Color HotPink = Color.FromArgb(255, 79, 184);
        public static readonly Color Peach = Color.FromArgb(255, 178, 166);
        public static readonly Color Plum = Color.FromArgb(106, 20, 80);
        // Switches: the track when off, hovered, disabled; a disabled knob.
        public static readonly Color TrackOff = Color.FromArgb(96, 58, 90);
        public static readonly Color TrackHover = Color.FromArgb(116, 72, 108);
        public static readonly Color TrackDisabled = Color.FromArgb(62, 34, 57);
        public static readonly Color KnobDisabled = Color.FromArgb(124, 98, 118);
        // A quiet fill (an unpicked pill, a segmented track) and a stronger hairline (hover, outlined buttons).
        public static readonly Color Quiet = Color.FromArgb(74, 36, 66);
        public static readonly Color LineStrong = Color.FromArgb(150, 112, 142);
        // The "Tested" badge.
        public static readonly Color BadgeBack = Color.FromArgb(92, 28, 76);
        public static readonly Color BadgeText = Color.FromArgb(255, 168, 222);
        // The badge an option tested by the developers shows (set to the form's language).
        public static string TestedText = "Tested";

        // PayDrop's backdrop inside `r` (a card's own rectangle): a magenta glow at the left, a peach one
        // at the top right, two thin orbit rings at the right with its four milestone dots on them.
        public static void DrawOrbits(Graphics g, RectangleF r)
        {
            Action<float, float, float, Color, int> glow = (cx, cy, radius, color, alpha) => {
                using (var p = new GraphicsPath())
                {
                    p.AddEllipse(cx - radius, cy - radius, radius * 2, radius * 2);
                    using (var b = new PathGradientBrush(p) { CenterColor = Color.FromArgb(alpha, color), SurroundColors = new[] { Color.FromArgb(0, color) } })
                        g.FillPath(b, p);
                }
            };
            glow(r.X + r.Width * 0.16f, r.Y + r.Height * 0.70f, r.Height * 2.2f, Color.FromArgb(224, 64, 168), 120);
            glow(r.Right - r.Width * 0.04f, r.Y + r.Height * 0.05f, r.Height * 1.3f, Color.FromArgb(255, 170, 160), 80);
            float ox = r.X + r.Width * 0.80f, oy = r.Y + r.Height * 0.62f;
            float[] rings = { r.Height * 1.05f, r.Height * 0.66f };
            using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1f))
                foreach (var rr in rings) g.DrawEllipse(pen, ox - rr, oy - rr, rr * 2, rr * 2);
            // The dots, as on PayDrop's milestone orbit: hot pink, plum, peach, butter.
            var dots = new[] {
                Tuple.Create(rings[0], 200f, HotPink), Tuple.Create(rings[1], 300f, Plum),
                Tuple.Create(rings[1], 150f, Peach), Tuple.Create(rings[0], 330f, Cyan),
            };
            foreach (var d in dots)
            {
                double a = d.Item2 * Math.PI / 180.0;
                float x = ox + (float)Math.Cos(a) * d.Item1, y = oy + (float)Math.Sin(a) * d.Item1;
                using (var halo = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) g.FillEllipse(halo, x - 9, y - 9, 18, 18);
                using (var b = new SolidBrush(d.Item3)) g.FillEllipse(b, x - 6, y - 6, 12, 12);
            }
        }

        static string mono;
        // Cascadia Mono where Windows has it, Consolas otherwise.
        public static string MonoName
        {
            get
            {
                if (mono != null) return mono;
                mono = "Consolas";
                try
                {
                    using (var fonts = new InstalledFontCollection())
                        foreach (var f in fonts.Families)
                            if (f.Name == "Cascadia Mono") { mono = f.Name; break; }
                }
                catch (Exception) { }
                return mono;
            }
        }
        public static Font Mono(float size) { return new Font(MonoName, size); }
        public static Font Ui(float size) { return new Font("Segoe UI", size); }
        public static Font UiBold(float size) { return new Font("Segoe UI Semibold", size); }

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

        public static void Panel(Graphics g, RectangleF r, Color fill, Color border) { Panel(g, r, fill, border, 10); }

        public static void Panel(Graphics g, RectangleF r, Color fill, Color border, float radius)
        {
            using (var path = Round(r, radius))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                using (var p = new Pen(border)) g.DrawPath(p, path);
            }
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
        public static void DrawSwitch(Graphics g, RectangleF track, bool on, bool hover, bool enabled)
        {
            using (var path = Theme.Round(track, track.Height / 2))
            using (var b = new SolidBrush(!enabled ? Theme.TrackDisabled : on ? Theme.Cyan : hover ? Theme.TrackHover : Theme.TrackOff)) g.FillPath(b, path);
            float k = track.Height - 4;
            float kx = on ? track.Right - k - 2 : track.X + 2;
            using (var b = new SolidBrush(!enabled ? Theme.KnobDisabled : on ? Theme.OnAccent : Color.White)) g.FillEllipse(b, kx, track.Y + 2, k, k);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            DrawSwitch(g, new RectangleF(1, (Height - 20) / 2f, 36, 20), Checked, hover, Enabled);
            var text = new RectangleF(46, 0, Width - 46, Height);
            using (var b = new SolidBrush(Enabled ? ForeColor : Theme.Dim))
            using (var f = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(Text, Font, b, text, f);
        }
    }

    // A launch option: a panel with the title, a "Tested" badge (a title starting with "★ "), up to
    // two lines saying what it does, and the switch at the right.
    class OptionCard : CheckBox
    {
        public string Line = "";
        bool hover;
        public OptionCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Size = new Size(400, 66);
            Cursor = Cursors.Hand;
            Font = Theme.UiBold(10f);
            ForeColor = Theme.Text;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            Theme.Panel(g, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), hover && Enabled ? Theme.CardHover : Theme.Card, Checked && Enabled ? Color.FromArgb(150, 92, 134) : Theme.Line);
            const int sw = 38;
            Toggle.DrawSwitch(g, new RectangleF(Width - sw - 14, 12, sw, 22), Checked, hover, Enabled);
            var title = Text ?? "";
            bool tested = title.StartsWith("★");
            if (tested) title = title.TrimStart('★', ' ');
            float textW = Width - sw - 36;
            using (var b = new SolidBrush(Enabled ? Theme.Text : Theme.Dim))
            using (var f = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (var bf = Theme.UiBold(7.5f))
            {
                var size = g.MeasureString(title, Font);
                var ts = g.MeasureString(Theme.TestedText, bf);
                float badgeW = tested ? ts.Width + 6 : 0;
                float tw = Math.Min(size.Width, textW - (tested ? badgeW + 6 : 0));
                g.DrawString(title, Font, b, new RectangleF(14, 10, tw + 2, 22), f);
                if (tested)
                    {
                        var badge = new RectangleF(14 + tw + 4, 13, badgeW, 17);
                        using (var path = Theme.Round(badge, 5))
                        using (var bb = new SolidBrush(Theme.BadgeBack)) g.FillPath(bb, path);
                        using (var tb = new SolidBrush(Theme.BadgeText))
                        using (var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString(Theme.TestedText, bf, tb, badge, cf);
                    }
            }
            using (var lf = Theme.Ui(8.75f))
            using (var b = new SolidBrush(Theme.Dim))
            using (var f = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                g.DrawString(Line ?? "", lf, b, new RectangleF(14, 33, textW + sw - 4, Height - 38), f);
        }
    }

    // A preset to pick: name, the resolution in the accent colour, a grey line.
    class ModeCard : Control
    {
        public string Title, Tag2, Line;
        bool selected, hover;
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }
        public ModeCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Size = new Size(150, 74);
            Font = Theme.Mono(8.5f);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            var r = new RectangleF(0.75f, 0.75f, Width - 2f, Height - 2f);
            using (var path = Theme.Round(r, 8))
            {
                using (var b = new SolidBrush(selected ? Theme.Selected : hover ? Theme.CardHover : Theme.Card)) g.FillPath(b, path);
                using (var p = new Pen(selected ? Theme.Cyan : Theme.Line, selected ? 1.6f : 1f)) g.DrawPath(p, path);
            }
            var nf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            using (var tf = Theme.UiBold(10.5f))
            using (var b = new SolidBrush(Theme.Text)) g.DrawString(Title ?? "", tf, b, new RectangleF(10, 9, Width - 16, 22), nf);
            // The resolution as a small pill: the accent when picked.
            using (var pf = Theme.UiBold(8f))
            {
                var tag = Tag2 ?? "";
                var ts = g.MeasureString(tag, pf);
                var pill = new RectangleF(10, 32, Math.Min(ts.Width + 8, Width - 20), 18);
                using (var path = Theme.Round(pill, 5))
                using (var b = new SolidBrush(selected ? Theme.Cyan : Theme.Quiet)) g.FillPath(b, path);
                using (var b = new SolidBrush(selected ? Theme.OnAccent : Theme.Text))
                using (var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(tag, pf, b, pill, cf);
            }
            using (var lf = Theme.Ui(8.5f))
            using (var b = new SolidBrush(Theme.Dim)) g.DrawString(Line ?? "", lf, b, new RectangleF(10, 54, Width - 14, 18), nf);
        }
    }

    // A button: the accent fill for the main action, a bordered panel otherwise.
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
            Font = Theme.Ui(9.5f);
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
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            // Nimbus's buttons are whole pills.
            float pr = r.Height / 2f;
            if (!Enabled) Theme.Panel(g, r, Theme.Card, Theme.Line, pr);
            else if (Main)
            {
                var fill = down ? Theme.Violet : hover ? Theme.AccentHover : Theme.Cyan;
                Theme.Panel(g, r, fill, fill, pr);
            }
            else Theme.Panel(g, r, down ? Theme.Side : hover ? Theme.CardHover : (Parent != null ? Parent.BackColor : Theme.Back), hover || down ? Theme.Text : Theme.LineStrong, pr);
            // Keyboard focus (Tab): a ring inside the edge.
            if (Focused && ShowFocusCues && Enabled)
                using (var p = new Pen(Main ? Theme.OnAccent : Theme.Cyan, 1.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
                using (var path = Theme.Round(new RectangleF(3.5f, 3.5f, Width - 7.5f, Height - 7.5f), 6)) g.DrawPath(p, path);
            using (var b = new SolidBrush(Enabled ? (Main ? Theme.OnAccent : ForeColor) : Theme.Dim))
            using (var f = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, HotkeyPrefix = HotkeyPrefix.None })
            {
                // A label longer than the button (Portuguese and French ones often are) gets a smaller font
                // before it is cut with "...".
                var font = Font;
                Font smaller = null;
                for (float size = Font.Size; size > 7.5f && g.MeasureString(Text, font).Width > r.Width - 12; size -= 0.5f)
                {
                    if (smaller != null) smaller.Dispose();
                    font = smaller = new Font(Font.FontFamily, size - 0.5f, Font.Style);
                }
                g.DrawString(Text, font, b, r, f);
                if (smaller != null) smaller.Dispose();
            }
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    // A key (or a controller button) drawn as a key cap: "Left Ctrl / C" becomes two caps with a slash between.
    class KeyCap : Control
    {
        public bool Pad;
        public KeyCap()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = Theme.Mono(8.75f);
        }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            var text = Text ?? "";
            if (text == "-" || text.Length == 0)
            {
                using (var b = new SolidBrush(Theme.Dim)) g.DrawString("—", Font, b, 4, 5);
                return;
            }
            float x = 0;
            var parts = text.Split(new[] { " / ", " + " }, StringSplitOptions.None);
            var joins = new List<string>();
            for (int i = 0, at = 0; i < parts.Length - 1; i++)
            {
                at = text.IndexOf(parts[i], at, StringComparison.Ordinal) + parts[i].Length;
                joins.Add(text.Substring(at, 3).Trim());
            }
            for (int i = 0; i < parts.Length; i++)
            {
                var w = g.MeasureString(parts[i], Font).Width + 14;
                if (x + w > Width) w = Math.Max(20, Width - x - 1);
                var r = new RectangleF(x + 0.5f, 1.5f, w, Height - 4f);
                Theme.Panel(g, r, Pad ? Theme.Back : Theme.Card, Pad ? Theme.Line : Theme.Cyan);
                using (var b = new SolidBrush(Pad ? Theme.Dim : Theme.Text))
                using (var f = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(parts[i], Font, b, r, f);
                x += w + 4;
                if (i < joins.Count)
                {
                    using (var b = new SolidBrush(Theme.Dim)) g.DrawString(joins[i], Font, b, x, 6);
                    x += g.MeasureString(joins[i], Font).Width + 2;
                }
                if (x >= Width) break;
            }
        }
    }
    // A button of the side bar: icon and name; the active one has a lighter fill and a cyan bar.
    class NavButton : Control
    {
        public string Glyph;
        bool active, hover;
        public bool Active { get { return active; } set { active = value; Invalidate(); } }
        public NavButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Size = new Size(196, 38);
            Font = Theme.Ui(10.5f);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Side);
            Theme.Smooth(g);
            var r = new RectangleF(8, 2, Width - 16, Height - 4);
            if (active || hover)
                using (var path = Theme.Round(r, 8))
                using (var b = new SolidBrush(active ? Theme.Selected : Color.FromArgb(44, 16, 40))) g.FillPath(b, path);
            using (var gf = new Font("Segoe MDL2 Assets", 11f))
            using (var b = new SolidBrush(active ? Theme.Cyan : Theme.Dim))
                g.DrawString(Glyph ?? "", gf, b, 22, Height / 2f - 8);
            using (var b = new SolidBrush(active ? Theme.Text : Theme.Dim))
            using (var f = new StringFormat { LineAlignment = StringAlignment.Center })
                g.DrawString(Text, Font, b, new RectangleF(50, 0, Width - 54, Height), f);
        }
    }

    // The head of the Play page: the page's name, one line under it, the hardware as rounded chips.
    class Banner : Control
    {
        public string Title = "", Subtitle = "", Accent = "";
        public string[] Chips = new string[0];
        public Color[] ChipColors = new Color[0];
        public Banner()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 116;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            // A plum card with PayDrop's glow and orbit rings.
            var card = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.Round(card, 16))
            {
                using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, path);
                var clip = g.Clip;
                g.SetClip(path);
                Theme.DrawOrbits(g, card);
                g.Clip = clip;
                using (var p = new Pen(Theme.Line)) g.DrawPath(p, path);
            }
            // PayDrop's headline: a heavy word, then an italic serif run in butter yellow.
            using (var tf = new Font("Segoe UI Black", 22f))
            using (var b = new SolidBrush(Theme.Text))
            {
                g.DrawString(Title ?? "", tf, b, 16, 6);
                var tw = g.MeasureString(Title ?? "", tf).Width;
                if (!string.IsNullOrEmpty(Accent))
                    using (var af = new Font("Georgia", 19f, FontStyle.Italic))
                    using (var ab = new SolidBrush(Theme.Cyan)) g.DrawString(Accent, af, ab, 16 + tw - 4, 11);
            }
            using (var sf = Theme.Ui(9.75f))
            using (var b = new SolidBrush(Theme.Soft)) g.DrawString(Subtitle ?? "", sf, b, 21, 52);
            using (var cf = Theme.Ui(8.75f))
            {
                float cx = 21;
                for (int i = 0; i < Chips.Length; i++)
                {
                    var text = Chips[i] ?? "";
                    if (text.Length == 0) continue;
                    var size = g.MeasureString(text, cf);
                    var chip = new RectangleF(cx + 0.5f, 78.5f, size.Width + 16, 24);
                    if (chip.Right > Width - 12) break;
                    var c = i < ChipColors.Length ? ChipColors[i] : Theme.Dim;
                    using (var path = Theme.Round(chip, 12))
                    {
                        using (var b = new SolidBrush(Color.FromArgb(34, 255, 255, 255))) g.FillPath(b, path);
                        using (var p = new Pen(Theme.Line)) g.DrawPath(p, path);
                    }
                    // Plain facts in light grey; a warning or the engine keeps its colour.
                    using (var b = new SolidBrush(c == Theme.Dim ? Theme.Soft : c))
                    using (var f = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(text, cf, b, chip, f);
                    cx = chip.Right + 8;
                }
            }
        }
    }

    // "Ready to play" and its checks ("✓ Dump verified  ✓ Binary ready ..."), drawn in a bordered panel.
    class StatusStrip : Control
    {
        public string[] Items = new string[0];
        public int[] States = new int[0];   // 1 good, 0 bad, -1 neutral
        public string ReadyText = "Ready to play", NotReadyText = "Not ready yet";
        // Room kept at the right for the strip's own buttons.
        public int RightRoom = 232;
        public StatusStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 52;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            Theme.Panel(g, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Theme.Card, Theme.Line);
            bool ready = Items.Length > 0;
            for (int i = 0; i < Items.Length; i++) if ((i < States.Length ? States[i] : -1) != 1) ready = false;
            using (var b = new SolidBrush(ready ? Theme.Good : Theme.Warn)) g.FillEllipse(b, 16, Height / 2f - 5, 10, 10);
            float x = 34;
            using (var tf = Theme.UiBold(10.5f))
            using (var b = new SolidBrush(Theme.Text))
            {
                var title = ready ? ReadyText : NotReadyText;
                g.DrawString(title, tf, b, x, Height / 2f - 10);
                x += g.MeasureString(title, tf).Width + 16;
            }
            using (var f = Theme.Ui(9f))
                for (int i = 0; i < Items.Length; i++)
                {
                    int s = i < States.Length ? States[i] : -1;
                    var mark = s == 1 ? "✓" : s == 0 ? "!" : "•";
                    var text = mark + "  " + Items[i];
                    var w = g.MeasureString(text, f).Width;
                    if (x + w > Width - RightRoom) break;
                    using (var b = new SolidBrush(s == 1 ? Theme.Soft : s == 0 ? Theme.Warn : Theme.Dim))
                        g.DrawString(text, f, b, x, Height / 2f - 8);
                    x += w + 12;
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
            Font = Theme.Mono(8.75f);
        }
        public void Set(float part, string text) { done = Math.Max(0f, Math.Min(1f, part)); Text = text; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            var track = new RectangleF(0, Height - 7, Width - 1, 6);
            using (var path = Theme.Round(track, 3f))
            using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, path);
            if (done > 0)
            {
                var fill = new RectangleF(0, Height - 7, Math.Max(6, (Width - 1) * done), 6);
                using (var path = Theme.Round(fill, 3f))
                using (var b = new SolidBrush(Theme.Cyan)) g.FillPath(b, path);
            }
            using (var b = new SolidBrush(Theme.Text)) g.DrawString(Text, Font, b, 0, 0);
            var pct = ((int)(done * 100)).ToString() + "%";
            var size = g.MeasureString(pct, Font);
            using (var b = new SolidBrush(Theme.Dim)) g.DrawString(pct, Font, b, Width - size.Width, 0);
        }
    }

    // A section title ("Quality preset"), with an optional grey note at the right.
    class Heading : Control
    {
        public string Note = "";
        public Heading(string text)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            Height = 26;
            Font = new Font("Segoe UI Semibold", 11.5f);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Theme.Smooth(g);
            using (var b = new SolidBrush(Theme.Text)) g.DrawString(Text ?? "", Font, b, -1, 1);
            if (!string.IsNullOrEmpty(Note))
                using (var nf = Theme.Ui(8.5f))
                using (var b = new SolidBrush(Theme.Dim))
                {
                    var size = g.MeasureString(Note, nf);
                    g.DrawString(Note, nf, b, Width - size.Width, 5);
                }
        }
    }
}
