// 공용 카드 · 탭 항목 · 프로필 이름 입력 창
using System;
using System.Drawing;
using System.Windows.Forms;

namespace NeonAudioSwitcher
{
    class Card : FlatControl
    {
        public string Title, Subtitle, Warning;
        public Image IconImage;
        public int IconKind = -1;
        public int TextLeft = 16;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            UI.FillRound(g, r, 14, UI.Panel);
            UI.StrokeRound(g, r, 14, string.IsNullOrEmpty(Warning) ? UI.Border : UI.Alpha(UI.Danger, 120), 1f);

            int x = TextLeft;
            if (IconImage != null)
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(IconImage, new Rectangle(16, 16, 22, 22));
                x = 50;
            }
            else if (IconKind >= 0)
            {
                UI.DrawIconTile(g, new Rectangle(14, 12, 30, 30), IconKind, false);
                x = 54;
            }
            if (string.IsNullOrEmpty(Title)) return;

            bool two = !string.IsNullOrEmpty(Subtitle) || !string.IsNullOrEmpty(Warning);
            int ty = two ? 10 : (Math.Min(Height, 54) - 20) / 2;
            float maxW = Width - x - 16 - (Tag is int ? (int)Tag : 0);
            using (var b = new SolidBrush(UI.Text))
                g.DrawString(Title, UI.BodyBold, b, new RectangleF(x, ty, maxW, 20), UI.Ellipsis(StringAlignment.Near));
            if (!string.IsNullOrEmpty(Warning))
                using (var b = new SolidBrush(UI.Danger))
                    g.DrawString(Warning, UI.HintFont, b, new RectangleF(x, ty + 20, maxW, 18), UI.Ellipsis(StringAlignment.Near));
            else if (!string.IsNullOrEmpty(Subtitle))
                using (var b = new SolidBrush(UI.Faint))
                    g.DrawString(Subtitle, UI.HintFont, b, new RectangleF(x, ty + 20, maxW, 18), UI.Ellipsis(StringAlignment.Near));
        }
    }

    // 왼쪽 탭 항목
    // 사이드바 탭 항목 (선택되면 강조색으로 채움)
    class NavItem : FlatControl
    {
        public bool Selected;
        public int IconKind;
        public string Count;
        public NavItem(string text, int icon) { Text = text; IconKind = icon; Height = 44; Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Selected) UI.FillRound(g, r, 14, UI.Accent);
            else if (hover) UI.FillRound(g, r, 14, UI.PanelHover);
            Color fg = Selected ? UI.OnAccent : (hover ? UI.Text : UI.Dim);
            UI.DrawKindIcon(g, new Rectangle(10, (Height - 22) / 2, 24, 22), IconKind, fg);
            using (var b = new SolidBrush(fg))
                g.DrawString(Text, Selected ? UI.BodyBold : UI.BodyFont, b, 42, (Height - UI.BodyFont.Height) / 2f);
            if (!string.IsNullOrEmpty(Count))
                using (var b = new SolidBrush(Selected ? UI.OnAccent : UI.Faint))
                    g.DrawString(Count, UI.HintFont, b, new RectangleF(0, 0, Width - 12, Height), UI.Ellipsis(StringAlignment.Far));
        }
    }

    // ───────────── 프로필 이름 입력 창 ─────────────
    class ProfileDialog : ChromeForm
    {
        DarkTextBox name;
        NeonCheck chkMic, chkVol;
        public string ProfileName { get { return name.Text.Trim(); } }
        public bool WithMic { get { return chkMic == null || chkMic.Checked; } }
        public bool WithVolume { get { return chkVol == null || chkVol.Checked; } }

        bool allowEmpty;

        public ProfileDialog(string title, string initial, bool options, string note)
            : this(title, initial, options, note, false) { }

        public ProfileDialog(string title, string initial, bool options, string note, bool allowEmpty)
        {
            this.allowEmpty = allowEmpty;
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            KeyPreview = true;
            int w = 400;
            int y = 0;

            var head = new FlatControl();
            head.Bounds = new Rectangle(1, 1, w - 2, 53);
            head.Paint += delegate(object s, PaintEventArgs e)
            {
                UI.Smooth(e.Graphics);
                using (var b = new SolidBrush(UI.Text)) e.Graphics.DrawString(title, UI.TitleFont, b, 20, 18);
            };
            head.MouseDown += BeginDrag;
            Controls.Add(head);
            y = 58;

            if (!string.IsNullOrEmpty(note))
            {
                var n = new NoteLabel(note);
                n.Bounds = new Rectangle(22, y, w - 44, 18);
                Controls.Add(n);
                n.FitHeight();
                y += n.Height + 10;
            }

            name = new DarkTextBox();
            name.Text = initial;
            name.Bounds = new Rectangle(20, y, w - 40, 36);
            Controls.Add(name);
            y += 50;

            if (options)
            {
                chkMic = new NeonCheck();
                chkMic.Text = "마이크도 함께 저장";
                chkMic.SetSilently(true);
                chkMic.Location = new Point(22, y);
                Controls.Add(chkMic);
                y += 32;
                chkVol = new NeonCheck();
                chkVol.Text = "지금 볼륨도 함께 저장";
                chkVol.SetSilently(true);
                chkVol.Location = new Point(22, y);
                Controls.Add(chkVol);
                y += 40;
            }

            var cancel = new NeonButton();
            cancel.Text = "취소";
            cancel.Bounds = new Rectangle(w - 20 - 100 - 8 - 100, y, 100, 36);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
            Controls.Add(cancel);
            var ok = new NeonButton();
            ok.Primary = true;
            ok.Text = "저장";
            ok.Bounds = new Rectangle(w - 20 - 100, y, 100, 36);
            ok.Click += delegate { Confirm(); };
            Controls.Add(ok);
            y += 36 + 20;

            ClientSize = new Size(w, y);
            Shown += delegate { name.Box.Focus(); name.Box.SelectAll(); };
        }

        void Confirm()
        {
            if (ProfileName.Length == 0 && !allowEmpty) { name.Box.Focus(); return; }
            DialogResult = DialogResult.OK;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Confirm(); }
            else if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
            base.OnKeyDown(e);
        }
    }
}
