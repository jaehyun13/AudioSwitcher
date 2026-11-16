// 앱별 볼륨 + 앱별 출력 장치 창
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NeonAudioSwitcher
{
    class AppRow : FlatControl
    {
        public AppGroup Group;
        public NeonSlider Slider;
        public Dropdown Route;

        public AppRow(AppGroup g, Image icon)
        {
            Group = g;
            BackColor = UI.Bg;
            Height = 72;

            Slider = new NeonSlider();
            Slider.IconImage = icon;
            Slider.IconKind = g.IsSystem ? 0 : 5;
            Slider.BackColor = UI.Panel;
            Slider.ValueChanged += delegate { Group.SetVolume(Slider.Value); };
            Slider.MuteToggled += delegate
            {
                bool m = !Group.GetMute();
                Group.SetMute(m);
                Slider.SetState(-1f, m);
            };
            Controls.Add(Slider);

            if (!g.IsSystem && !string.IsNullOrEmpty(g.ExePath) && AppRouting.Available)
            {
                Route = new Dropdown();
                Route.Compact = true;
                Route.BackColor = UI.Panel;
                Route.Font = UI.HintFont;
                Route.Height = 26;
                Route.Add("기본 장치 따르기", "");
                Route.AddHeader("이 앱만 다른 장치로");
                foreach (var d in App.Main.OutDevices) Route.Add(d.Name, d.Id);
                var rule = App.S.RouteFor(g.ExePath);
                if (rule == null) Route.SelectValue("");
                else
                {
                    Route.SelectValue(rule.DeviceId);
                    if (Route.SelectedValue == null) Route.SelectText(rule.DeviceName + " (연결 안 됨)");
                }
                Route.SelectedChanged += delegate
                {
                    string id = Route.SelectedValue as string;
                    string name = null;
                    foreach (var d in App.Main.OutDevices) if (d.Id == id) name = d.Name;
                    App.Main.SetRoute(Group.ExePath, string.IsNullOrEmpty(id) ? null : id, name);
                    Invalidate();
                };
                Controls.Add(Route);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Slider == null) return;
            Slider.Bounds = new Rectangle(12, 38, Width - 24, 26);
            if (Route != null) Route.Bounds = new Rectangle(Width - 190, 8, 180, 26);
        }

        public void Sync()
        {
            if (!Slider.Dragging) Slider.SetState(Group.GetVolume(), Group.GetMute());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            bool routed = Route != null && !string.IsNullOrEmpty(Route.SelectedValue as string);
            UI.FillRound(g, rect, 14, UI.Panel);
            UI.StrokeRound(g, rect, 14, routed ? UI.Alpha(UI.Accent, 110) : UI.Border, 1f);

            float nameW = (Route != null ? Width - 210 : Width - 28);
            using (var b = new SolidBrush(UI.Text))
                g.DrawString(Group.Name, UI.NameFont, b, new RectangleF(14, 8, nameW, 24), UI.Ellipsis(StringAlignment.Near));
        }
    }

    class MixerForm : ChromeForm
    {
        static MixerForm instance;
        const int W = 440;

        Panel list;
        NoteLabel empty, foot;
        Timer listTimer;
        List<AppGroup> groups = new List<AppGroup>();
        List<AppRow> rows = new List<AppRow>();
        Form ownerRef;

        public static void Toggle(Form owner)
        {
            if (instance != null && !instance.IsDisposed)
            {
                instance.PositionNear(owner);
                if (!instance.Visible) instance.Show();
                instance.BringToFront();
                instance.Activate();
                return;
            }
            instance = new MixerForm();
            instance.PositionNear(owner);
            instance.Show();
            instance.BringToFront();
            instance.Activate();
        }

        // 메인 창이 바뀌었을 때 (장치 추가 등) 열려 있으면 다시 그림
        public static void Reload()
        {
            if (instance != null && !instance.IsDisposed) instance.RefreshList(true);
        }

        public void PositionNear(Form owner)
        {
            if (owner != null) ownerRef = owner;
            Form o = ownerRef;
            bool ownerShown = o != null && o.Visible && o.WindowState != FormWindowState.Minimized;
            var wa = (ownerShown ? Screen.FromControl(o) : Screen.FromPoint(Cursor.Position)).WorkingArea;

            int x, y;
            if (ownerShown)
            {
                x = o.Right + 12;
                if (x + Width > wa.Right) x = o.Left - Width - 12;
                y = o.Top;
            }
            else
            {
                x = wa.Right - Width - 24;
                y = wa.Bottom - Height - 24;
            }
            x = Math.Max(wa.Left, Math.Min(x, wa.Right - Width));
            y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - Height));
            Location = new Point(x, y);
        }

        MixerForm()
        {
            Text = "앱별 볼륨";
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(W, 300);
            ShowInTaskbar = false;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var bar = new FlatControl();
            bar.Bounds = new Rectangle(1, 1, W - 2, 55);
            bar.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics;
                UI.Smooth(g);
                using (var b = new SolidBrush(UI.Text)) g.DrawString("앱별 볼륨", UI.TitleFont, b, 18, 12);
                using (var b = new SolidBrush(UI.Faint))
                    g.DrawString(AppRouting.Available ? "볼륨과 출력 장치를 앱마다 따로" : "앱마다 볼륨을 따로",
                                 UI.HintFont, b, 19, 33);
            };
            bar.MouseDown += BeginDrag;
            Controls.Add(bar);

            var close = new TitleButton(0);
            close.Location = new Point(W - 44, 12);
            close.Click += delegate { Close(); };
            bar.Controls.Add(close);

            list = new Panel();
            list.Location = new Point(14, 62);
            list.Width = W - 28;
            list.BackColor = UI.Bg;
            list.AutoScroll = true;
            UI.DarkScrollbars(list);
            Controls.Add(list);

            empty = new NoteLabel("소리를 내고 있는 앱이 없습니다.\n음악이나 영상을 재생하면 여기에 나타납니다.");
            empty.Color = UI.Dim;
            empty.TextFont = UI.BodyFont;
            empty.Bounds = new Rectangle(18, 74, W - 36, 50);
            empty.Visible = false;
            Controls.Add(empty);

            foot = new NoteLabel("아이콘 클릭 = 음소거  ·  휠 = 2%씩 조절");
            Controls.Add(foot);

            RefreshList(true);

            listTimer = new Timer();
            listTimer.Interval = 1000;
            listTimer.Tick += delegate { RefreshList(false); };
            listTimer.Start();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        void RefreshList(bool force)
        {
            foreach (var r in rows) if (r.Slider.Dragging) return;

            var fresh = SessionManager.GetGroups();
            var oldGroups = groups;
            groups = fresh;

            string devSig = string.Join("|", App.Main.OutDevices.Select(d => d.Id).ToArray());
            string sigNew = devSig + "#" + string.Join("|", fresh.Select(g => g.Key).ToArray());
            string sigOld = (string)(list.Tag ?? "");

            if (force || sigNew != sigOld)
            {
                list.Tag = sigNew;
                list.SuspendLayout();
                foreach (var r in rows) { list.Controls.Remove(r); r.Dispose(); }
                rows.Clear();

                int y = 0;
                bool scroll = fresh.Count > 7;
                foreach (var g in fresh)
                {
                    var row = new AppRow(g, g.IsSystem ? null : SessionManager.GetIcon(g.ExePath));
                    row.Bounds = new Rectangle(0, y, list.Width - (scroll ? 18 : 0), 72);
                    list.Controls.Add(row);
                    rows.Add(row);
                    y += 80;
                }
                list.ResumeLayout();
                Relayout(fresh.Count);
            }
            else
            {
                for (int i = 0; i < rows.Count; i++) rows[i].Group = fresh[i];
            }

            foreach (var r in rows) r.Sync();
            foreach (var g in oldGroups) g.Dispose();
        }

        void Relayout(int count)
        {
            empty.Visible = count == 0;
            int listH = Math.Min(count, 7) * 80 - (count > 0 ? 8 : 0);
            list.Height = Math.Max(listH, 1);
            list.Visible = count > 0;
            int bodyBottom = 62 + (count > 0 ? listH : 70);
            foot.Bounds = new Rectangle(18, bodyBottom + 12, W - 36, 18);
            ClientSize = new Size(W, bodyBottom + 40);
            PositionNear(null);
            Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            listTimer.Stop();
            foreach (var g in groups) g.Dispose();
            groups.Clear();
            instance = null;
            base.OnFormClosed(e);
        }
    }
}
