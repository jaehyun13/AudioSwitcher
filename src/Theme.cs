// 테마 · 공용 그리기 도구 · 직접 그린 컨트롤들
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NeonAudioSwitcher
{
    static class UI
    {
        // 무채색 바탕에 강조색 하나. 면은 밝기 차이로만 층을 나눈다.
        public static readonly Color Bg         = Color.FromArgb(16, 17, 21);
        public static readonly Color Panel      = Color.FromArgb(25, 27, 33);
        public static readonly Color PanelHover = Color.FromArgb(33, 36, 44);
        public static readonly Color PanelOn    = Color.FromArgb(28, 34, 52);
        public static readonly Color Raised     = Color.FromArgb(38, 41, 50);
        public static readonly Color Accent     = Color.FromArgb(110, 142, 255);
        public static readonly Color AccentDeep = Color.FromArgb(84, 112, 230);
        public static readonly Color Text       = Color.FromArgb(237, 239, 244);
        public static readonly Color Dim        = Color.FromArgb(148, 154, 168);
        public static readonly Color Faint      = Color.FromArgb(98, 104, 119);
        public static readonly Color Border     = Color.FromArgb(40, 43, 53);
        public static readonly Color BorderHi   = Color.FromArgb(58, 62, 75);
        public static readonly Color Danger     = Color.FromArgb(240, 98, 98);
        public static readonly Color Warn       = Color.FromArgb(240, 180, 90);

        public static Color Alpha(Color c, int a) { return Color.FromArgb(a, c.R, c.G, c.B); }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        static bool HasFamily(string name)
        {
            try { using (var ff = new FontFamily(name)) return true; }
            catch { return false; }
        }

        public static Font Pick(float size, FontStyle style, params string[] names)
        {
            foreach (var n in names)
                if (HasFamily(n)) return new Font(n, size, style);
            return new Font(FontFamily.GenericSansSerif, size, style);
        }

        // 한글 문구는 한 서체가 한글·라틴을 모두 그려야 굵기가 어긋나지 않는다
        const string KR = "Pretendard";
        const string KR2 = "Noto Sans KR";
        const string KR3 = "맑은 고딕";
        const string EN = "Segoe UI Semibold";
        const string EN2 = "Segoe UI";

        public static readonly Font TitleFont  = Pick(11.5f, FontStyle.Bold, KR, KR2, KR3);
        public static readonly Font HeaderFont = Pick(8.5f, FontStyle.Bold, KR, KR2, KR3);
        public static readonly Font NameFont   = Pick(10f, FontStyle.Bold, KR, KR2, KR3);
        public static readonly Font BodyFont   = Pick(9f, FontStyle.Regular, KR, KR2, KR3);
        public static readonly Font BodyBold   = Pick(9f, FontStyle.Bold, KR, KR2, KR3);
        public static readonly Font HintFont   = Pick(8f, FontStyle.Regular, KR, KR2, KR3);
        public static readonly Font BigFont    = Pick(13f, FontStyle.Bold, KR, KR2, KR3);
        public static readonly Font SubFont    = Pick(7.5f, FontStyle.Regular, EN, EN2);
        public static readonly Font BadgeFont  = Pick(8f, FontStyle.Regular, EN, EN2);

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        static float SpaceWidth(Graphics g, Font f)
        {
            var fmt = StringFormat.GenericTypographic;
            return g.MeasureString("i i", f, PointF.Empty, fmt).Width
                 - g.MeasureString("ii", f, PointF.Empty, fmt).Width;
        }

        static float CharWidth(Graphics g, char c, Font f, float space)
        {
            if (c == ' ') return space;
            return g.MeasureString(c.ToString(), f, PointF.Empty, StringFormat.GenericTypographic).Width;
        }

        // 자간을 벌려 그리기 (짧은 라틴 문구용)
        public static void DrawTracked(Graphics g, string text, Font f, Brush b, float x, float y, float tracking)
        {
            float space = SpaceWidth(g, f);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != ' ') g.DrawString(c.ToString(), f, b, x, y, StringFormat.GenericTypographic);
                x += CharWidth(g, c, f, space) + tracking;
            }
        }

        public static float TrackedWidth(Graphics g, string text, Font f, float tracking)
        {
            float space = SpaceWidth(g, f);
            float w = 0;
            for (int i = 0; i < text.Length; i++) w += CharWidth(g, text[i], f, space) + tracking;
            return w;
        }

        // rad = 모서리 지름
        public static GraphicsPath Rounded(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            rad = Math.Max(2, Math.Min(rad, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, rad, rad, 180, 90);
            p.AddArc(r.Right - rad, r.Y, rad, rad, 270, 90);
            p.AddArc(r.Right - rad, r.Bottom - rad, rad, rad, 0, 90);
            p.AddArc(r.X, r.Bottom - rad, rad, rad, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int rad, Color fill)
        {
            using (var p = Rounded(r, rad))
            using (var b = new SolidBrush(fill)) g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Rectangle r, int rad, Color c, float w)
        {
            using (var p = Rounded(r, rad))
            using (var pen = new Pen(c, w)) g.DrawPath(pen, p);
        }

        public static StringFormat Ellipsis(StringAlignment h)
        {
            var f = new StringFormat();
            f.Trimming = StringTrimming.EllipsisCharacter;
            f.FormatFlags = StringFormatFlags.NoWrap;
            f.LineAlignment = StringAlignment.Center;
            f.Alignment = h;
            return f;
        }

        // 작은 단축키 배지
        public static int DrawKeyBadge(Graphics g, string text, float right, int cy, Color fg)
        {
            var sz = g.MeasureString(text, BadgeFont, PointF.Empty, StringFormat.GenericTypographic);
            int w = (int)Math.Ceiling(sz.Width) + 14;
            var r = new Rectangle((int)right - w, cy - 10, w, 20);
            FillRound(g, r, 8, Alpha(Color.White, 8));
            StrokeRound(g, r, 8, Border, 1f);
            using (var b = new SolidBrush(fg))
                g.DrawString(text, BadgeFont, b, r.X + 7, cy - sz.Height / 2f, StringFormat.GenericTypographic);
            return w;
        }

        // 장치 종류 아이콘. kind: 0 스피커 1 헤드셋 2 이어폰 3 마이크 4 프로필 5 앱
        public static void DrawKindIcon(Graphics g, Rectangle r, int kind, Color c)
        {
            using (var pen = new Pen(c, 1.8f))
            using (var br = new SolidBrush(c))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                if (kind == 1) // 헤드셋
                {
                    g.DrawArc(pen, cx - 8, cy - 8, 16, 16, 180, 180);
                    FillRound(g, new Rectangle(cx - 9, cy - 1, 5, 9), 4, c);
                    FillRound(g, new Rectangle(cx + 4, cy - 1, 5, 9), 4, c);
                    g.DrawLine(pen, cx - 8, cy, cx - 8, cy + 2);
                    g.DrawLine(pen, cx + 8, cy, cx + 8, cy + 2);
                }
                else if (kind == 2) // 이어폰
                {
                    g.DrawEllipse(pen, cx - 9, cy - 8, 6, 6);
                    g.DrawEllipse(pen, cx + 3, cy - 8, 6, 6);
                    g.DrawLine(pen, cx - 6, cy - 2, cx - 6, cy + 8);
                    g.DrawLine(pen, cx + 6, cy - 2, cx + 6, cy + 8);
                }
                else if (kind == 3) // 마이크
                {
                    FillRound(g, new Rectangle(cx - 3, cy - 9, 7, 12), 7, c);
                    g.DrawArc(pen, cx - 6, cy - 4, 13, 10, 0, 180);
                    g.DrawLine(pen, cx, cy + 6, cx, cy + 9);
                    g.DrawLine(pen, cx - 3, cy + 9, cx + 4, cy + 9);
                }
                else if (kind == 4) // 프로필 (겹친 카드)
                {
                    StrokeRound(g, new Rectangle(cx - 9, cy - 3, 14, 11), 5, c, 1.8f);
                    g.DrawLines(pen, new[] { new Point(cx - 5, cy - 7), new Point(cx + 7, cy - 7), new Point(cx + 9, cy - 5),
                                             new Point(cx + 9, cy + 4) });
                }
                else if (kind == 6) // 키보드
                {
                    StrokeRound(g, new Rectangle(cx - 10, cy - 6, 20, 13), 5, c, 1.8f);
                    for (int i = 0; i < 4; i++) g.FillRectangle(br, cx - 6 + i * 4, cy - 3, 2, 2);
                    g.DrawLine(pen, cx - 4, cy + 3, cx + 4, cy + 3);
                }
                else if (kind == 7) // 슬라이더 (일반 설정)
                {
                    g.DrawLine(pen, cx - 8, cy - 5, cx + 8, cy - 5);
                    g.DrawLine(pen, cx - 8, cy + 5, cx + 8, cy + 5);
                    g.FillEllipse(br, cx + 1, cy - 8, 6, 6);
                    g.FillEllipse(br, cx - 7, cy + 2, 6, 6);
                }
                else if (kind == 5) // 앱
                {
                    StrokeRound(g, new Rectangle(cx - 8, cy - 7, 16, 14), 5, c, 1.8f);
                    g.DrawLine(pen, cx - 8, cy - 3, cx + 8, cy - 3);
                }
                else // 스피커
                {
                    Point[] pts = {
                        new Point(cx - 9, cy - 3), new Point(cx - 5, cy - 3), new Point(cx, cy - 8),
                        new Point(cx, cy + 8), new Point(cx - 5, cy + 3), new Point(cx - 9, cy + 3) };
                    g.FillPolygon(br, pts);
                    g.DrawArc(pen, cx - 1, cy - 4, 6, 8, -60, 120);
                    g.DrawArc(pen, cx + 1, cy - 8, 9, 16, -60, 120);
                }
            }
        }

        // 아이콘을 둥근 타일 안에 그림
        public static void DrawIconTile(Graphics g, Rectangle r, int kind, bool on)
        {
            FillRound(g, r, 12, on ? Alpha(Accent, 46) : Raised);
            DrawKindIcon(g, r, kind, on ? Accent : Dim);
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        // 스크롤바를 어두운 테마로 (Windows 10 1809 이상, 그 외에는 무시됨)
        public static void DarkScrollbars(Control c)
        {
            EventHandler apply = delegate { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } };
            if (c.IsHandleCreated) apply(c, EventArgs.Empty);
            else c.HandleCreated += apply;
        }

        public static void DrawChevron(Graphics g, int cx, int cy, Color c)
        {
            using (var p = new Pen(c, 1.6f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawLines(p, new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
            }
        }
    }

    // ─────────────── 테두리 없는 창 공통 (그림자 · 둥근 모서리 · 드래그) ───────────────
    class ChromeForm : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr wp, IntPtr lp);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public ChromeForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            BackColor = UI.Bg;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;   // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { }   // Win11 둥근 모서리
            try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }   // 다크 모드 힌트
        }

        public void BeginDrag(object s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(UI.Border))
                e.Graphics.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
    }

    // 트레이·드롭다운 메뉴 다크 테마
    class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return UI.Panel; } }
        public override Color MenuItemSelected { get { return UI.PanelHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return UI.PanelHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return UI.PanelHover; } }
        public override Color MenuItemBorder { get { return UI.PanelHover; } }
        public override Color MenuBorder { get { return UI.BorderHi; } }
        public override Color ImageMarginGradientBegin { get { return UI.Panel; } }
        public override Color ImageMarginGradientMiddle { get { return UI.Panel; } }
        public override Color ImageMarginGradientEnd { get { return UI.Panel; } }
        public override Color SeparatorDark { get { return UI.Border; } }
        public override Color SeparatorLight { get { return UI.Panel; } }
        public override Color CheckBackground { get { return UI.Panel; } }
        public override Color CheckSelectedBackground { get { return UI.PanelHover; } }
    }

    class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled) e.TextColor = UI.Faint;
            else if (e.TextColor == SystemColors.ControlText || e.TextColor == SystemColors.MenuText) e.TextColor = UI.Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = e.ImageRectangle;
            using (var p = new Pen(UI.Accent, 2f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawLines(p, new[] {
                    new Point(r.X + 3, r.Y + r.Height / 2), new Point(r.X + 6, r.Y + r.Height / 2 + 3),
                    new Point(r.Right - 3, r.Y + 4) });
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = UI.Dim;
            base.OnRenderArrow(e);
        }
    }

    static class Menus
    {
        public static ContextMenuStrip Create()
        {
            var m = new ContextMenuStrip();
            m.Renderer = new DarkMenuRenderer();
            m.ShowImageMargin = false;
            m.ShowCheckMargin = false;
            m.Font = UI.BodyFont;
            m.BackColor = UI.Panel;
            m.ForeColor = UI.Text;
            return m;
        }

        public static ToolStripMenuItem Item(string text, EventHandler onClick)
        {
            var it = new ToolStripMenuItem(text);
            it.ForeColor = UI.Text;
            it.Padding = new Padding(4, 3, 4, 3);
            if (onClick != null) it.Click += onClick;
            return it;
        }

        public static ToolStripMenuItem Header(string text)
        {
            var it = new ToolStripMenuItem(text);
            it.Enabled = false;
            it.Font = UI.HintFont;
            return it;
        }
    }

    // ─────────────── 직접 그리는 컨트롤 공통 ───────────────
    class FlatControl : Control
    {
        protected bool hover;
        public FlatControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = UI.Bg;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    }

    // ─────────────── 장치 카드 ───────────────
    class DeviceRow : FlatControl
    {
        public AudioDevice Device;
        public string Sub = "";        // 단축키 배지

        public DeviceRow() { Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            bool on = Device.IsDefault;

            UI.FillRound(g, rect, 14, on ? UI.PanelOn : (hover ? UI.PanelHover : UI.Panel));
            if (on) UI.StrokeRound(g, rect, 14, UI.Alpha(UI.Accent, 120), 1f);

            UI.DrawIconTile(g, new Rectangle(12, (Height - 34) / 2, 34, 34), Device.Kind, on);

            // 오른쪽: 선택 표시 원 + 단축키 배지
            int cy = Height / 2;
            int rx = Width - 30;
            if (on)
            {
                using (var b = new SolidBrush(UI.Accent)) g.FillEllipse(b, rx, cy - 8, 16, 16);
                using (var p = new Pen(UI.Bg, 2f))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLines(p, new[] { new Point(rx + 4, cy), new Point(rx + 7, cy + 3), new Point(rx + 12, cy - 3) });
                }
            }
            else
                using (var p = new Pen(hover ? UI.Faint : UI.BorderHi, 1.5f)) g.DrawEllipse(p, rx, cy - 8, 16, 16);

            float right = rx - 12;
            if (!string.IsNullOrEmpty(Sub) && (hover || !on))
                right -= UI.DrawKeyBadge(g, Sub, right, cy, hover ? UI.Dim : UI.Faint) + 10;

            // 이름 + 상태
            string status;
            if (on) status = Device.IsDefaultComm ? "사용 중 · 통화 포함" : "사용 중";
            else if (Device.IsDefaultComm) status = "통화 장치";
            else status = Device.Flow == EDataFlow.eCapture ? "입력 장치" : "출력 장치";

            var nameRect = new RectangleF(58, cy - 19, Math.Max(40f, right - 58), 20);
            using (var tb = new SolidBrush(on ? UI.Text : (hover ? UI.Text : UI.Mix(UI.Dim, UI.Text, 0.4f))))
                g.DrawString(Device.Name, UI.NameFont, tb, nameRect, UI.Ellipsis(StringAlignment.Near));
            using (var sb = new SolidBrush(on ? UI.Accent : UI.Faint))
                g.DrawString(status, UI.HintFont, sb, new RectangleF(58, cy + 1, Math.Max(40f, right - 58), 18),
                             UI.Ellipsis(StringAlignment.Near));
        }
    }

    // ─────────────── 볼륨 슬라이더 ───────────────
    class NeonSlider : FlatControl
    {
        public Color Accent = UI.Accent;
        public int IconKind = 0;
        public Image IconImage;
        float val;
        bool muted;
        bool dragging;
        public bool Dragging { get { return dragging; } }
        public float Value { get { return val; } }
        public bool Muted { get { return muted; } }
        public event EventHandler ValueChanged;
        public event EventHandler MuteToggled;
        public bool ShowIcon = true;

        int IconW { get { return ShowIcon ? 30 : 0; } }
        const int PctW = 42;

        public NeonSlider()
        {
            SetStyle(ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            Height = 28;
        }

        public void SetState(float v, bool m)
        {
            if (v >= 0f) val = v;
            muted = m;
            Invalidate();
        }

        public void SetValue(float v)
        {
            val = Math.Max(0f, Math.Min(1f, v));
            Invalidate();
        }

        Rectangle TrackRect
        {
            get { return new Rectangle(IconW + 8, Height / 2 - 2, Math.Max(10, Width - IconW - PctW - 16), 4); }
        }

        void SetFromX(int x)
        {
            var t = TrackRect;
            val = Math.Max(0f, Math.Min(1f, (x - t.X) / (float)t.Width));
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (ShowIcon && e.X < IconW) { if (MuteToggled != null) MuteToggled(this, EventArgs.Empty); }
            else { dragging = true; SetFromX(e.X); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) SetFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Invalidate(); base.OnMouseUp(e); }
        // 휠 조절을 위해 포커스를 가져오되, 글자 입력 · 단축키 입력 중이면 빼앗지 않는다
        protected override void OnMouseEnter(EventArgs e)
        {
            var f = FindForm();
            var ac = f != null ? f.ActiveControl : null;
            if (!(ac is TextBox) && !(ac is HotkeyBox)) Focus();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            val = Math.Max(0f, Math.Min(1f, val + (e.Delta > 0 ? 0.02f : -0.02f)));
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);

            if (ShowIcon)
            {
                var iconRect = new Rectangle(0, (Height - 24) / 2, 26, 24);
                if (IconImage != null)
                {
                    var prev = g.InterpolationMode;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    var ir = new Rectangle(3, (Height - 20) / 2, 20, 20);
                    if (muted)
                    {
                        var cm = new System.Drawing.Imaging.ColorMatrix();
                        cm.Matrix33 = 0.35f;
                        using (var attr = new System.Drawing.Imaging.ImageAttributes())
                        {
                            attr.SetColorMatrix(cm);
                            g.DrawImage(IconImage, ir, 0, 0, IconImage.Width, IconImage.Height, GraphicsUnit.Pixel, attr);
                        }
                    }
                    else g.DrawImage(IconImage, ir);
                    g.InterpolationMode = prev;
                }
                else UI.DrawKindIcon(g, iconRect, IconKind, muted ? UI.Faint : (hover ? UI.Text : UI.Dim));
                if (muted)
                    using (var p = new Pen(UI.Danger, 1.8f))
                    {
                        p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                        g.DrawLine(p, iconRect.X + 3, iconRect.Bottom - 3, iconRect.Right - 3, iconRect.Y + 3);
                    }
            }

            var t = TrackRect;
            UI.FillRound(g, t, 4, UI.Raised);
            int fillW = (int)(t.Width * val);
            Color fc = muted ? UI.Faint : Accent;
            if (fillW > 1) UI.FillRound(g, new Rectangle(t.X, t.Y, fillW, t.Height), 4, fc);

            int cx = t.X + fillW;
            int r = (hover || dragging) ? 8 : 6;
            if (hover || dragging)
                using (var b = new SolidBrush(UI.Alpha(fc, 50))) g.FillEllipse(b, cx - r - 4, Height / 2 - r - 4, (r + 4) * 2, (r + 4) * 2);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, cx - r, Height / 2 - r, r * 2, r * 2);
            using (var p = new Pen(fc, 2f)) g.DrawEllipse(p, cx - r + 1, Height / 2 - r + 1, r * 2 - 2, r * 2 - 2);

            string pct = muted ? "음소거" : string.Format("{0}%", (int)Math.Round(val * 100));
            var sf = UI.Ellipsis(StringAlignment.Far);
            using (var b = new SolidBrush(muted ? UI.Danger : UI.Dim))
                g.DrawString(pct, muted ? UI.HintFont : UI.BadgeFont, b, new RectangleF(Width - PctW - 2, 0, PctW, Height), sf);
        }
    }

    // ─────────────── 토글 스위치 (+ 설명 문구) ───────────────
    class NeonCheck : FlatControl
    {
        bool isChecked;
        public event EventHandler CheckedChanged;
        public string Description;   // 아래 줄에 옅게 (선택)

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public void SetSilently(bool v) { isChecked = v; Invalidate(); }

        public NeonCheck()
        {
            Cursor = Cursors.Hand;
            Height = 24;
            Font = UI.BodyFont;
        }

        public bool AutoWidth = true;

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (AutoWidth) Width = TextRenderer.MeasureText(Text, Font).Width + 48;
        }

        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            int top = string.IsNullOrEmpty(Description) ? (Height - 18) / 2 : 3;
            var track = new Rectangle(0, top, 32, 18);
            Color tc = isChecked ? UI.Accent : (hover ? UI.BorderHi : UI.Raised);
            UI.FillRound(g, track, 18, tc);
            int kx = isChecked ? track.Right - 16 : track.X + 2;
            using (var b = new SolidBrush(isChecked ? Color.White : UI.Dim))
                g.FillEllipse(b, kx, track.Y + 2, 14, 14);

            using (var tb = new SolidBrush(isChecked || hover ? UI.Text : UI.Dim))
                g.DrawString(Text, Font, tb, 42, top + 9 - Font.Height / 2f);
            if (!string.IsNullOrEmpty(Description))
                using (var db = new SolidBrush(UI.Faint))
                    g.DrawString(Description, UI.HintFont, db, new RectangleF(42, top + 20, Width - 42, Height - top - 20));
        }
    }

    // ─────────────── 버튼 ───────────────
    class NeonButton : FlatControl
    {
        public Color Accent = UI.Accent;
        public bool Primary;
        public bool Danger;
        public string Hint;       // 오른쪽에 옅게 (단축키 등)
        public int IconKind = -1; // 왼쪽 아이콘 (선택)

        public NeonButton()
        {
            Cursor = Cursors.Hand;
            Font = UI.BodyBold;
            Height = 34;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill, fg, border;
            if (Primary)
            {
                fill = hover ? UI.Mix(UI.Accent, Color.White, 0.12f) : UI.Accent;
                fg = Color.White; border = fill;
            }
            else if (Danger)
            {
                fill = hover ? UI.Alpha(UI.Danger, 40) : UI.Panel;
                fg = hover ? UI.Danger : UI.Dim; border = hover ? UI.Alpha(UI.Danger, 140) : UI.Border;
            }
            else
            {
                fill = hover ? UI.PanelHover : UI.Panel;
                fg = hover ? UI.Text : UI.Mix(UI.Dim, UI.Text, 0.35f); border = hover ? UI.BorderHi : UI.Border;
            }
            UI.FillRound(g, r, 12, fill);
            UI.StrokeRound(g, r, 12, border, 1f);

            var sz = g.MeasureString(Text, Font);
            int iconW = IconKind >= 0 ? 26 : 0;
            float hintW = 0;
            if (!string.IsNullOrEmpty(Hint))
                hintW = g.MeasureString(Hint, UI.BadgeFont).Width + 12;
            float total = iconW + sz.Width;
            float x = (Width - total) / 2f;
            if (hintW > 0 && x + total > Width - hintW - 8) x = Math.Max(10, Width - hintW - 8 - total);
            if (IconKind >= 0)
                UI.DrawKindIcon(g, new Rectangle((int)x, (Height - 22) / 2, 22, 22), IconKind, fg);
            using (var b = new SolidBrush(fg))
                g.DrawString(Text, Font, b, x + iconW, (Height - sz.Height) / 2f + 0.5f);
            if (hintW > 0)
                using (var b = new SolidBrush(Primary ? UI.Alpha(Color.White, 170) : UI.Faint))
                    g.DrawString(Hint, UI.BadgeFont, b, Width - hintW, (Height - UI.BadgeFont.Height) / 2f);
        }
    }

    // ─────────────── 알약 모양 칩 (프로필) ───────────────
    class Chip : FlatControl
    {
        public bool Active;
        public bool Dashed;      // "+ 추가" 용
        public string Hint;      // 단축키

        public Chip()
        {
            Cursor = Cursors.Hand;
            Font = UI.BodyBold;
            Height = 30;
        }

        public void FitWidth()
        {
            int w = TextRenderer.MeasureText(Text, Font).Width + 28;
            if (!string.IsNullOrEmpty(Hint)) w += TextRenderer.MeasureText(Hint, UI.BadgeFont).Width + 6;
            if (Active) w += 8;
            Width = w;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Dashed)
            {
                if (hover) UI.FillRound(g, r, Height, UI.Panel);
                using (var path = UI.Rounded(r, Height))
                using (var p = new Pen(hover ? UI.Accent : UI.BorderHi, 1.2f))
                {
                    p.DashStyle = DashStyle.Dash;
                    g.DrawPath(p, path);
                }
            }
            else
            {
                Color fill = Active ? UI.Alpha(UI.Accent, 52) : (hover ? UI.PanelHover : UI.Panel);
                UI.FillRound(g, r, Height, fill);
                UI.StrokeRound(g, r, Height, Active ? UI.Alpha(UI.Accent, 170) : (hover ? UI.BorderHi : UI.Border), 1f);
            }

            Color fg = Active ? UI.Text : (Dashed ? (hover ? UI.Accent : UI.Dim) : (hover ? UI.Text : UI.Dim));
            float x = 14;
            if (Active)
            {
                using (var b = new SolidBrush(UI.Accent)) g.FillEllipse(b, 11, Height / 2 - 3, 6, 6);
                x = 22;
            }
            using (var b = new SolidBrush(fg))
                g.DrawString(Text, Font, b, x - 2, (Height - Font.Height) / 2f);
            if (!string.IsNullOrEmpty(Hint))
            {
                float tw = TextRenderer.MeasureText(Text, Font).Width;
                using (var b = new SolidBrush(UI.Faint))
                    g.DrawString(Hint, UI.BadgeFont, b, x + tw - 2, (Height - UI.BadgeFont.Height) / 2f + 1);
            }
        }
    }

    // ─────────────── 드롭다운 (메뉴로 펼침) ───────────────
    class Dropdown : FlatControl
    {
        public class Option
        {
            public string Text; public object Value; public bool Header;
            public Option(string t, object v) { Text = t; Value = v; }
        }

        public List<Option> Options = new List<Option>();
        Option selected;
        public event EventHandler SelectedChanged;
        public string Placeholder = "선택";
        public bool Compact;   // 배경 없이 글자 + 화살표만

        public Dropdown()
        {
            Cursor = Cursors.Hand;
            Height = 32;
            Font = UI.BodyFont;
        }

        public object SelectedValue { get { return selected == null ? null : selected.Value; } }

        public void Add(string text, object value) { Options.Add(new Option(text, value)); }

        public void AddHeader(string text)
        {
            var o = new Option(text, null);
            o.Header = true;
            Options.Add(o);
        }

        public void SelectValue(object v)
        {
            selected = null;
            foreach (var o in Options)
                if (!o.Header && Equals(o.Value, v)) { selected = o; break; }
            Invalidate();
        }

        public void SelectText(string text) { selected = new Option(text, null); Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var m = Menus.Create();
            foreach (var o in Options)
            {
                if (o.Header)
                {
                    if (m.Items.Count > 0) m.Items.Add(new ToolStripSeparator());
                    m.Items.Add(Menus.Header(o.Text));
                    continue;
                }
                var opt = o;
                var it = Menus.Item((selected != null && Equals(selected.Value, o.Value) && selected.Text == o.Text ? "✓  " : "     ") + o.Text,
                    delegate
                    {
                        selected = opt;
                        Invalidate();
                        if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
                    });
                if (selected != null && Equals(selected.Value, o.Value)) it.ForeColor = UI.Accent;
                m.Items.Add(it);
            }
            m.MinimumSize = new Size(Width, 0);
            m.Show(this, new Point(0, Height + 2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (!Compact)
            {
                UI.FillRound(g, r, 12, hover ? UI.PanelHover : UI.Raised);
                UI.StrokeRound(g, r, 12, hover ? UI.BorderHi : UI.Border, 1f);
            }
            else if (hover) UI.FillRound(g, r, 12, UI.PanelHover);
            string t = selected != null ? selected.Text : Placeholder;
            using (var b = new SolidBrush(selected != null ? (Compact && !hover ? UI.Dim : UI.Text) : UI.Faint))
                g.DrawString(t, Font, b, new RectangleF(10, 0, Width - 34, Height), UI.Ellipsis(StringAlignment.Near));
            UI.DrawChevron(g, Width - 16, Height / 2, hover ? UI.Text : UI.Dim);
        }
    }

    // ─────────────── 단축키 입력칸 ───────────────
    class HotkeyBox : FlatControl
    {
        Hotkey value = new Hotkey();
        bool listening;
        int liveMods;
        public string Warning;        // 충돌 등 (빨간 글씨)
        public event EventHandler ValueChanged;

        public Hotkey Value
        {
            get { return value; }
            set { this.value = value == null ? new Hotkey() : value.Clone(); Invalidate(); }
        }

        public HotkeyBox()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Height = 32;
            Width = 170;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            listening = true;
            liveMods = 0;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            listening = false;
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData) { return true; }

        static int ModsOf(Keys k)
        {
            int m = 0;
            if ((k & Keys.Control) != 0) m |= Hotkey.MOD_CONTROL;
            if ((k & Keys.Alt) != 0) m |= Hotkey.MOD_ALT;
            if ((k & Keys.Shift) != 0) m |= Hotkey.MOD_SHIFT;
            return m;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!listening) return base.ProcessCmdKey(ref msg, keyData);
            Keys code = keyData & Keys.KeyCode;
            int mods = ModsOf(keyData);

            if (code == Keys.ControlKey || code == Keys.Menu || code == Keys.ShiftKey ||
                code == Keys.LWin || code == Keys.RWin)
            {
                liveMods = mods;
                Invalidate();
                return true;
            }
            if (mods == 0 && code == Keys.Escape) { listening = false; Invalidate(); return true; }
            if (mods == 0 && (code == Keys.Back || code == Keys.Delete))
            {
                value = new Hotkey();
                listening = false;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                return true;
            }
            // 기능키(F1~F24)·미디어키는 단독 허용, 나머지는 조합키 필수 (타자 칠 때 오작동 방지)
            bool standalone = (code >= Keys.F1 && code <= Keys.F24) || code == Keys.Pause || code == Keys.Scroll;
            if (mods == 0 && !standalone) { liveMods = 0; Invalidate(); return true; }

            value = new Hotkey(mods, (int)code);
            listening = false;
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            return true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (listening) { liveMods = ModsOf(Control.ModifierKeys); Invalidate(); }
            base.OnKeyUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            UI.FillRound(g, r, 12, listening ? UI.Alpha(UI.Accent, 30) : (hover ? UI.PanelHover : UI.Raised));
            Color bc = listening ? UI.Accent : (!string.IsNullOrEmpty(Warning) ? UI.Alpha(UI.Danger, 160) : (hover ? UI.BorderHi : UI.Border));
            UI.StrokeRound(g, r, 12, bc, listening ? 1.5f : 1f);

            string t;
            Color fg;
            if (listening)
            {
                t = liveMods != 0 ? Hotkey.ModsText(liveMods) + "…" : "키 조합을 누르세요";
                fg = UI.Accent;
            }
            else if (value.IsEmpty) { t = "없음"; fg = UI.Faint; }
            else { t = value.ToString(); fg = UI.Text; }

            using (var b = new SolidBrush(fg))
                g.DrawString(t, listening || value.IsEmpty ? UI.BodyFont : UI.BadgeFont, b,
                             new RectangleF(12, 0, Width - 24, Height), UI.Ellipsis(StringAlignment.Near));
        }
    }

    // ─────────────── 다크 입력칸 ───────────────
    class DarkTextBox : FlatControl
    {
        public TextBox Box;

        public DarkTextBox()
        {
            Height = 32;
            Box = new TextBox();
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = UI.Raised;
            Box.ForeColor = UI.Text;
            Box.Font = UI.BodyFont;
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Controls.Add(Box);
            Cursor = Cursors.IBeam;
        }

        public override string Text { get { return Box.Text; } set { Box.Text = value; } }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Box != null) Box.Bounds = new Rectangle(11, (Height - Box.PreferredHeight) / 2 + 1, Width - 22, Box.PreferredHeight);
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Box.Focus(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            UI.FillRound(g, r, 12, UI.Raised);
            UI.StrokeRound(g, r, 12, Box.Focused ? UI.Accent : UI.Border, Box.Focused ? 1.5f : 1f);
        }
    }

    // ─────────────── 제목줄 아이콘 버튼 (설정 · 최소화 · 닫기) ───────────────
    class TitleButton : FlatControl
    {
        public int Glyph;   // 0 닫기 1 최소화 2 설정

        public TitleButton(int glyph)
        {
            Glyph = glyph;
            Size = new Size(32, 32);
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            if (hover) UI.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 12, Glyph == 0 ? UI.Alpha(UI.Danger, 200) : UI.PanelHover);
            Color c = hover ? (Glyph == 0 ? Color.White : UI.Text) : UI.Dim;
            int cx = Width / 2, cy = Height / 2;
            using (var p = new Pen(c, 1.6f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                if (Glyph == 0)
                {
                    g.DrawLine(p, cx - 5, cy - 5, cx + 5, cy + 5);
                    g.DrawLine(p, cx + 5, cy - 5, cx - 5, cy + 5);
                }
                else if (Glyph == 1) g.DrawLine(p, cx - 5, cy, cx + 5, cy);
                else
                {
                    // 톱니바퀴
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(p, (float)(cx + Math.Cos(a) * 5.5), (float)(cy + Math.Sin(a) * 5.5),
                                      (float)(cx + Math.Cos(a) * 8), (float)(cy + Math.Sin(a) * 8));
                    }
                    g.DrawEllipse(p, cx - 5.5f, cy - 5.5f, 11, 11);
                    g.DrawEllipse(p, cx - 2f, cy - 2f, 4, 4);
                }
            }
        }
    }

    // 섹션 제목 라벨 (배경 투명 처리 문제를 피하려고 직접 그림)
    class SectionLabel : FlatControl
    {
        public string RightText;    // 오른쪽 보조 문구
        public SectionLabel(string text)
        {
            Text = text;
            Height = 22;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            using (var b = new SolidBrush(UI.Dim))
                g.DrawString(Text, UI.HeaderFont, b, 0, (Height - UI.HeaderFont.Height) / 2f);
            if (!string.IsNullOrEmpty(RightText))
                using (var b = new SolidBrush(UI.Faint))
                    g.DrawString(RightText, UI.HintFont, b, new RectangleF(0, 0, Width, Height), UI.Ellipsis(StringAlignment.Far));
        }
    }

    // 여러 줄 설명문 (옅은 글씨)
    class NoteLabel : FlatControl
    {
        public Color Color = UI.Faint;
        public Font TextFont = UI.HintFont;
        public NoteLabel(string text) { Text = text; }

        public void FitHeight()
        {
            using (var g = CreateGraphics())
                Height = (int)Math.Ceiling(g.MeasureString(Text, TextFont, Width).Height) + 2;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            using (var b = new SolidBrush(Color))
                g.DrawString(Text, TextFont, b, new RectangleF(0, 0, Width, Height));
        }
    }
}
