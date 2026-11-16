// 전환 알림 (화면 모서리에 잠깐 떴다 사라지는 카드)
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NeonAudioSwitcher
{
    class ToastForm : Form
    {
        static ToastForm current;
        public static int Position = 0;      // Settings.ToastPos

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        Timer timer;
        int phase;          // 0 등장 · 1 유지 · 2 퇴장
        int tick;
        const int HoldTicks = 55;   // 약 1.6초
        string title, text, detail;
        int kind;
        Point target;
        int slideDir;

        public static void Popup(string title, string text, int kind, string detail)
        {
            if (current != null && !current.IsDisposed)
            {
                // 떠 있는 카드를 새로 만들지 않고 내용만 바꿔 깜빡임을 없앤다
                current.Refill(title, text, kind, detail);
                return;
            }
            current = new ToastForm(title, text, kind, detail);
            current.Show();
        }

        ToastForm(string ti, string t, int k, string d)
        {
            title = ti; text = t; kind = k; detail = d;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = UI.Panel;
            Opacity = 0;
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Measure();
            Place();
            timer = new Timer();
            timer.Interval = 16;
            timer.Tick += OnTick;
            timer.Start();
        }

        void Refill(string ti, string t, int k, string d)
        {
            title = ti; text = t; kind = k; detail = d;
            Measure();
            Place();
            Location = target;
            if (phase == 2) { phase = 1; Opacity = 1; }
            tick = 0;
            Invalidate();
        }

        void Measure()
        {
            using (var g = CreateGraphics())
            {
                float w1 = g.MeasureString(text ?? "", UI.NameFont).Width;
                float w2 = g.MeasureString(title ?? "", UI.HintFont).Width;
                float w3 = string.IsNullOrEmpty(detail) ? 0 : g.MeasureString(detail, UI.HintFont).Width;
                int w = (int)Math.Max(Math.Max(w1, w2), w3) + 96;
                Size = new Size(Math.Max(280, Math.Min(460, w)), string.IsNullOrEmpty(detail) ? 70 : 86);
            }
        }

        void Place()
        {
            // 주 모니터가 아니라 지금 보고 있는(마우스가 있는) 모니터에 띄운다
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            const int m = 20;
            int x, y;
            switch (Position)
            {
                case 1: x = wa.Right - Width - m; y = wa.Top + m; slideDir = -1; break;
                case 2: x = wa.Left + (wa.Width - Width) / 2; y = wa.Bottom - Height - m * 3; slideDir = 1; break;
                case 3: x = wa.Left + (wa.Width - Width) / 2; y = wa.Top + m * 2; slideDir = -1; break;
                default: x = wa.Right - Width - m; y = wa.Bottom - Height - m; slideDir = 1; break;
            }
            target = new Point(x, y);
            if (phase == 0) Location = new Point(x, y + slideDir * 12);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080;   // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                cp.ClassStyle |= 0x20000;                // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
            if (current == this) current = null;
            base.OnFormClosed(e);
        }

        // 마우스를 올리면 사라지지 않고 기다린다
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); if (phase == 2) { phase = 1; Opacity = 1; } tick = 0; }
        protected override void OnClick(EventArgs e) { base.OnClick(e); phase = 2; }

        void OnTick(object s, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) { if (timer != null) timer.Stop(); return; }
            if (phase == 0)
            {
                tick++;
                double t = Math.Min(1.0, tick / 12.0);
                double ease = 1 - Math.Pow(1 - t, 3);
                Opacity = ease;
                Location = new Point(target.X, target.Y + (int)Math.Round(slideDir * 12 * (1 - ease)));
                if (t >= 1.0) { phase = 1; tick = 0; }
            }
            else if (phase == 1)
            {
                if (!Bounds.Contains(Cursor.Position)) tick++;
                if (tick % 4 == 0) Invalidate(new Rectangle(0, Height - 4, Width, 4));
                if (tick > HoldTicks) { phase = 2; tick = 0; }
            }
            else
            {
                Opacity -= 0.07;
                if (Opacity <= 0.05) { timer.Stop(); Close(); }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            using (var p = new Pen(UI.BorderHi)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

            int tile = 40;
            var tr = new Rectangle(16, (Height - tile) / 2, tile, tile);
            UI.FillRound(g, tr, 14, UI.Alpha(UI.Accent, 46));
            UI.DrawKindIcon(g, tr, kind, UI.Ink);

            int x = tr.Right + 14;
            int top = string.IsNullOrEmpty(detail) ? 15 : 14;
            using (var b = new SolidBrush(UI.Dim))
                g.DrawString(title, UI.HintFont, b, x, top);
            using (var b = new SolidBrush(UI.Text))
                g.DrawString(text, UI.NameFont, b, new RectangleF(x - 1, top + 17, Width - x - 14, 22), UI.Ellipsis(StringAlignment.Near));
            if (!string.IsNullOrEmpty(detail))
                using (var b = new SolidBrush(UI.Faint))
                    g.DrawString(detail, UI.HintFont, b, new RectangleF(x, top + 40, Width - x - 14, 18), UI.Ellipsis(StringAlignment.Near));

            // 남은 표시 시간 막대
            if (phase >= 1)
            {
                float left = phase == 1 ? Math.Max(0f, 1f - tick / (float)HoldTicks) : 0f;
                using (var b = new SolidBrush(UI.Alpha(UI.Accent, 140)))
                    g.FillRectangle(b, 1, Height - 3, (Width - 2) * left, 2);
            }
        }
    }
}
