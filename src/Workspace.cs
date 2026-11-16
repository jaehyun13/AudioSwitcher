// 독 아래로 펼쳐지는 워크스페이스: 왼쪽 탭(장치 · 앱별 출력 · 프로필 · 설정) + 오른쪽 내용
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NeonAudioSwitcher
{
    // 장치 카드 (3열 격자)
    class DeviceCard : FlatControl
    {
        public AudioDevice Device;
        public string KeyHint;

        public DeviceCard() { Cursor = Cursors.Hand; }

        static string KindName(AudioDevice d)
        {
            if (d.Flow == EDataFlow.eCapture) return "마이크";
            switch (d.Kind) { case 1: return "헤드셋"; case 2: return "이어폰"; default: return "스피커"; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            bool on = Device.IsDefault;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            UI.FillRound(g, r, 18, on ? UI.PanelOn : (hover ? UI.PanelHover : UI.Panel));
            UI.StrokeRound(g, r, 18, on ? UI.SelLine : (hover ? UI.BorderHi : UI.Border), on ? 1.5f : 1f);
            UI.DrawKindIcon(g, new Rectangle(12, 12, 26, 24), Device.Kind, on ? UI.Text : UI.Dim);

            if (on)
            {
                string t = "사용 중";
                var sz = g.MeasureString(t, UI.HintFont);
                var pill = new Rectangle(Width - 14 - (int)sz.Width - 12, 14, (int)sz.Width + 12, 20);
                UI.FillRound(g, pill, 20, UI.Accent);
                using (var b = new SolidBrush(UI.OnAccent)) g.DrawString(t, UI.HintFont, b, pill.X + 6, pill.Y + (20 - sz.Height) / 2f);
            }
            else if (!string.IsNullOrEmpty(KeyHint))
                using (var b = new SolidBrush(UI.Faint))
                    g.DrawString(KeyHint, UI.SubFont, b, new RectangleF(0, 16, Width - 14, 16), UI.Ellipsis(StringAlignment.Far));

            using (var b = new SolidBrush(UI.Text))
                g.DrawString(Device.Name, UI.BodyBold, b, new RectangleF(14, Height - 46, Width - 26, 20), UI.Ellipsis(StringAlignment.Near));
            string sub = KindName(Device) + (Device.IsDefaultComm ? " · 통화" : "");
            using (var b = new SolidBrush(UI.Faint))
                g.DrawString(sub, UI.HintFont, b, new RectangleF(14, Height - 26, Width - 26, 18), UI.Ellipsis(StringAlignment.Near));
        }
    }

    // 앱 한 줄: 아이콘 · 이름 · 볼륨 · 출력 장치
    class AppLine : FlatControl
    {
        public AppGroup Group;
        public NeonSlider Slider;
        public Dropdown Route;
        Image icon;
        bool muted;

        public AppLine(AppGroup g, Image icon, Shell shell)
        {
            Group = g;
            this.icon = icon;
            BackColor = UI.Bg;
            Height = 54;

            Slider = new NeonSlider();
            Slider.ShowIcon = false;
            Slider.BackColor = UI.Bg;
            Slider.ValueChanged += delegate { Group.SetVolume(Slider.Value); };
            Controls.Add(Slider);

            if (!g.IsSystem && !string.IsNullOrEmpty(g.ExePath) && AppRouting.Available)
            {
                Route = new Dropdown();
                Route.BackColor = UI.Bg;
                Route.Font = UI.HintFont;
                Route.Height = 30;
                Route.Add("기본 장치", "");
                Route.AddHeader("이 앱만 다른 장치로");
                foreach (var d in shell.OutDevices) Route.Add(d.Name, d.Id);
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
                    foreach (var d in shell.OutDevices) if (d.Id == id) name = d.Name;
                    shell.SetRoute(Group.ExePath, string.IsNullOrEmpty(id) ? null : id, name);
                };
                Controls.Add(Route);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Slider == null) return;
            int right = Route != null ? Width - 132 : Width;
            Slider.Bounds = new Rectangle(140, 12, Math.Max(40, right - 140 - 8), 30);
            if (Route != null) Route.Bounds = new Rectangle(Width - 124, 12, 124, 30);
        }

        public void Sync()
        {
            if (Slider.Dragging) return;
            muted = Group.GetMute();
            Slider.SetState(Group.GetVolume(), muted);
            Invalidate(new Rectangle(0, 0, 40, Height));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.X < 40)
            {
                muted = !Group.GetMute();
                Group.SetMute(muted);
                Slider.SetState(-1f, muted);
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            using (var p = new Pen(UI.Border)) g.DrawLine(p, 0, 0, Width, 0);
            var tile = new Rectangle(0, 11, 32, 32);
            UI.FillRound(g, tile, 12, UI.Raised);
            if (icon != null)
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                if (muted)
                {
                    var cm = new System.Drawing.Imaging.ColorMatrix();
                    cm.Matrix33 = 0.35f;
                    using (var attr = new System.Drawing.Imaging.ImageAttributes())
                    {
                        attr.SetColorMatrix(cm);
                        g.DrawImage(icon, new Rectangle(6, 17, 20, 20), 0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, attr);
                    }
                }
                else g.DrawImage(icon, new Rectangle(6, 17, 20, 20));
            }
            else UI.DrawKindIcon(g, tile, Group.IsSystem ? 0 : 5, muted ? UI.Faint : UI.Dim);
            if (muted)
                using (var p = new Pen(UI.Danger, 2f)) g.DrawLine(p, 6, 39, 26, 15);
            using (var b = new SolidBrush(UI.Text))
                g.DrawString(Group.Name, UI.BodyBold, b, new RectangleF(42, 0, 94, Height), UI.Ellipsis(StringAlignment.Near));
        }
    }

    // 사이드바의 프로필 바로가기 한 줄
    class ProfileLink : FlatControl
    {
        public bool Active, Add;
        public ProfileLink(string text) { Text = text; Height = 30; Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            if (hover) UI.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 10, UI.PanelHover);
            if (Add)
            {
                using (var b = new SolidBrush(UI.Ink))
                    g.DrawString(Text, UI.BodyBold, b, 12, (Height - UI.BodyBold.Height) / 2f);
                return;
            }
            using (var b = new SolidBrush(Active ? UI.Accent : UI.BorderHi)) g.FillEllipse(b, 12, Height / 2 - 4, 8, 8);
            using (var b = new SolidBrush(Active ? UI.Text : UI.Dim))
                g.DrawString(Text, Active ? UI.BodyBold : UI.BodyFont, b, new RectangleF(28, 0, Width - 32, Height), UI.Ellipsis(StringAlignment.Near));
        }
    }

    // 설정의 색 조합 카드
    class ThemeCard : FlatControl
    {
        public int Combo;
        public bool Selected;
        public ThemeCard(int combo) { Combo = combo; Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            UI.FillRound(g, r, 16, Selected ? UI.PanelOn : (hover ? UI.PanelHover : UI.Panel));
            UI.StrokeRound(g, r, 16, Selected ? UI.SelLine : (hover ? UI.BorderHi : UI.Border), Selected ? 2f : 1f);

            // 라이트 · 다크 미리보기 조각
            var pl = Themes.Get(Combo, false);
            var pd = Themes.Get(Combo, true);
            var a = new Rectangle(12, 12, (Width - 30) / 2, 34);
            var b2 = new Rectangle(a.Right + 6, 12, a.Width, 34);
            Swatch(g, a, pl);
            Swatch(g, b2, pd);

            using (var b = new SolidBrush(UI.Text))
                g.DrawString(Themes.Names[Combo], UI.BodyBold, b, new RectangleF(12, 54, Width - 40, 22), UI.Ellipsis(StringAlignment.Near));
            if (Selected)
                using (var p = new Pen(UI.Ink, 2f))
                {
                    p.StartCap = System.Drawing.Drawing2D.LineCap.Round; p.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    int cx = Width - 20, cy = 65;
                    g.DrawLines(p, new[] { new Point(cx - 5, cy), new Point(cx - 1, cy + 4), new Point(cx + 6, cy - 4) });
                }
        }

        static void Swatch(Graphics g, Rectangle r, Palette p)
        {
            UI.FillRound(g, r, 10, p.Win);
            UI.StrokeRound(g, r, 10, p.Line, 1f);
            UI.FillRound(g, new Rectangle(r.X + 8, r.Y + 9, 16, 16), 16, p.Acc);
            UI.FillRound(g, new Rectangle(r.X + 30, r.Y + 11, r.Width - 40, 5), 5, p.Text);
            UI.FillRound(g, new Rectangle(r.X + 30, r.Y + 20, (r.Width - 40) / 2, 4), 4, p.Faint);
        }
    }

    partial class WorkspaceForm : ChromeForm
    {
        public const int PageDevices = 0, PageRoutes = 1, PageProfiles = 2, PageAuto = 3, PageSettings = 4;
        public const int W = 720, H = 640, SideW = 188;
        const int CX = SideW + 20;              // 내용 시작 x
        const int CW = W - CX - 6 - 22;         // 내용 폭 (스크롤바 자리 제외)

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, int mod, int vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

        Shell shell;
        Settings S { get { return App.S; } }
        FlatControl side;
        Panel content;
        List<NavItem> navs = new List<NavItem>();
        int page;
        int y;
        public int CurrentPage { get { return page; } }

        // 장치 탭에서 쓰는 것들
        NeonSlider outSlider, micSlider;
        List<AppLine> appLines = new List<AppLine>();
        List<AppGroup> groups = new List<AppGroup>();
        string appSig = "";
        Timer appTimer;

        class KeySlot { public HotkeyBox Box; public Card Card; public Func<Hotkey> Get; public string Label; }
        List<KeySlot> slots = new List<KeySlot>();

        public WorkspaceForm(Shell shell)
        {
            this.shell = shell;
            Text = "Audio Switcher";
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(W, H);
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            appTimer = new Timer();
            appTimer.Interval = 1000;
            appTimer.Tick += delegate { if (Visible && page == PageDevices) RefreshApps(); };
            appTimer.Start();

            RebuildAll();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && !(ActiveControl is HotkeyBox) && !(ActiveControl is TextBox)) shell.ClosePanel();
            base.OnKeyDown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; shell.ClosePanel(); return; }
            base.OnFormClosing(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) ShowPage(page);
            else { S.Save(); DisposeGroups(); StartMeter(false); }
        }

        // 테마가 바뀌면 전부 새로 그림
        public void RebuildAll()
        {
            BackColor = UI.Bg;
            if (content == null)
            {
                content = new Panel();
                content.Bounds = new Rectangle(CX, 14, W - CX - 6, H - 28);
                content.AutoScroll = true;
                Controls.Add(content);
            }
            content.BackColor = UI.Bg;
            UI.DarkScrollbars(content);
            BuildSide();
            ShowPage(page);
            Invalidate(true);
        }

        // 장치 · 프로필 변경 등 데이터가 바뀌었을 때.
        // 버튼 클릭 처리 도중에 그 버튼을 지우지 않도록 다음 순간으로 미룬다.
        bool sideQueued, pageQueued;

        public void OnDataChanged()
        {
            if (!IsHandleCreated) { BuildSide(); RebuildNow(); return; }
            if (!sideQueued)
            {
                sideQueued = true;
                BeginInvoke((Action)delegate { sideQueued = false; BuildSide(); });
            }
            Rebuild();
        }

        void Rebuild()
        {
            if (!IsHandleCreated) { RebuildNow(); return; }
            if (pageQueued) return;
            pageQueued = true;
            BeginInvoke((Action)delegate { pageQueued = false; RebuildNow(); });
        }

        // ───────────── 사이드바 ─────────────
        void BuildSide()
        {
            if (side != null) { Controls.Remove(side); side.Dispose(); }
            navs.Clear();
            side = new FlatControl();
            side.BackColor = UI.Side;
            side.Bounds = new Rectangle(1, 1, SideW - 1, H - 2);
            side.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics;
                UI.Smooth(g);
                var tile = new Rectangle(20, 20, 28, 28);
                UI.FillRound(g, tile, 10, UI.Accent);
                UI.DrawKindIcon(g, tile, 0, UI.OnAccent);
                using (var b = new SolidBrush(UI.Text)) g.DrawString("Audio Switcher", UI.BodyBold, b, 56, 24);
            };
            side.MouseDown += BeginDrag;
            Controls.Add(side);

            string[] names = { "장치", "앱별 출력", "프로필", "자동화", "설정" };
            int[] icons = { 0, 5, 4, 8, 7 };
            string[] counts = { null, S.Routes.Count > 0 ? S.Routes.Count.ToString() : null,
                                S.Profiles.Count > 0 ? S.Profiles.Count.ToString() : null,
                                S.Triggers.Count > 0 ? S.Triggers.Count.ToString() : null, null };
            for (int i = 0; i < names.Length; i++)
            {
                var n = new NavItem(names[i], icons[i]);
                n.BackColor = UI.Side;
                n.Count = counts[i];
                n.Selected = i == page;
                n.Bounds = new Rectangle(12, 66 + i * 48, SideW - 24, 44);
                int idx = i;
                n.Click += delegate { ShowPage(idx); };
                side.Controls.Add(n);
                navs.Add(n);
            }

            int py = 66 + names.Length * 48 + 18;
            var lab = new SectionLabel("프로필");
            lab.BackColor = UI.Side;
            lab.Bounds = new Rectangle(24, py, SideW - 40, 22);
            side.Controls.Add(lab);
            py += 26;
            foreach (var p in S.Profiles.Take(5))
            {
                var prof = p;
                var l = new ProfileLink(p.Name);
                l.BackColor = UI.Side;
                l.Active = shell.IsActive(p);
                l.Bounds = new Rectangle(12, py, SideW - 24, 30);
                l.Click += delegate { shell.ApplyProfile(prof); };
                side.Controls.Add(l);
                py += 32;
            }
            var add = new ProfileLink("+ 새 프로필");
            add.Add = true;
            add.BackColor = UI.Side;
            add.Bounds = new Rectangle(12, py, SideW - 24, 30);
            add.Click += delegate { if (shell.CreateProfileFromCurrent(this)) OnDataChanged(); };
            side.Controls.Add(add);
        }

        // ───────────── 페이지 ─────────────
        public void ShowPage(int p)
        {
            page = p;
            for (int i = 0; i < navs.Count; i++) { navs[i].Selected = i == p; navs[i].Invalidate(); }
            content.SuspendLayout();
            foreach (Control c in content.Controls.Cast<Control>().ToList()) { content.Controls.Remove(c); c.Dispose(); }
            content.AutoScrollPosition = new Point(0, 0);
            slots.Clear();
            appLines.Clear();
            outSlider = micSlider = null;
            y = 6;
            if (p == PageDevices) BuildDevices();
            else if (p == PageRoutes) BuildRoutes();
            else if (p == PageProfiles) BuildProfiles();
            else if (p == PageAuto) BuildAuto();
            else { BuildTheme(); BuildDock(); BuildGeneral(); BuildOverlay(); BuildDeviceManager(); y += 10; BuildHotkeys(); BuildInfo(); }
            StartMeter(p == PageDevices && Visible);
            var spacer = new Control();
            spacer.Bounds = new Rectangle(0, y, 1, 16);
            Add(spacer);
            content.ResumeLayout();
            ValidateKeys();
        }

        // ───────────── 장치 탭 ─────────────
        void BuildDevices()
        {
            var cyc = S.Key(Actions.CycleOut);
            Section("출력 장치", cyc.IsEmpty ? null : cyc + " 로 다음 장치");
            DeviceGrid(shell.OutDevices, true);
            outSlider = VolumeSlider(0, shell.OutDevices);
            y += 14;
            var cm = S.Key(Actions.CycleMic);
            Section("마이크", cm.IsEmpty ? null : cm + " 로 다음 마이크");
            DeviceGrid(shell.MicDevices, false);
            micSlider = VolumeSlider(3, shell.MicDevices);
            micLevel = new LevelBar();
            micLevel.Bounds = new Rectangle(34, y - 4, CW - 34 - 50, 10);
            Add(micLevel);
            y += 10;
            y += 14;

            DisposeGroups();
            groups = SessionManager.GetGroups();
            appSig = AppSignature(groups);
            Section("앱별 볼륨 · 출력", groups.Count > 0 ? "소리 내는 앱 " + groups.Count + "개" : null);
            if (groups.Count == 0)
                Note("소리를 내고 있는 앱이 없어요. 음악이나 영상을 재생하면 여기에 나타나요.");
            foreach (var g in groups)
            {
                var line = new AppLine(g, g.IsSystem ? null : SessionManager.GetIcon(g.ExePath), shell);
                line.Bounds = new Rectangle(0, y, CW, 54);
                Add(line);
                appLines.Add(line);
                y += 54;
            }
            if (groups.Count > 0 && AppRouting.Available)
            {
                y += 8;
                Note("오른쪽 장치를 바꾸면 그 앱만 다른 장치로 소리가 나가요. 일부 앱은 다시 실행해야 적용돼요.");
            }
            SyncVolumes();
        }

        void DeviceGrid(List<AudioDevice> devs, bool output)
        {
            if (devs.Count == 0) { Note("연결된 장치가 없어요."); return; }
            int cols = 3, gap = 10;
            int w = (CW - gap * (cols - 1)) / cols;
            for (int i = 0; i < devs.Count; i++)
            {
                var d = devs[i];
                var c = new DeviceCard();
                c.Device = d;
                c.KeyHint = output && S.DirectMods != 0 && i < 8 ? Hotkey.ModsText(S.DirectMods) + (i + 1) : null;
                c.Bounds = new Rectangle((i % cols) * (w + gap), y + (i / cols) * (96 + gap), w, 96);
                c.MouseUp += delegate(object s, MouseEventArgs e)
                {
                    if (e.Button == MouseButtons.Left) shell.SwitchTo(d);
                    else if (e.Button == MouseButtons.Right) DeviceMenu(d, c, e.Location);
                };
                Add(c);
            }
            y += ((devs.Count + cols - 1) / cols) * (96 + gap) + 2;
        }

        NeonSlider VolumeSlider(int iconKind, List<AudioDevice> devs)
        {
            var sl = new NeonSlider();
            sl.IconKind = iconKind;
            sl.Bounds = new Rectangle(0, y, CW, 32);
            sl.ValueChanged += delegate
            {
                var d = devs.Find(z => z.IsDefault);
                if (d != null) AudioManager.SetVolume(d.Id, sl.Value);
            };
            sl.MuteToggled += delegate
            {
                var d = devs.Find(z => z.IsDefault);
                if (d == null) return;
                bool m = !AudioManager.GetMute(d.Id);
                AudioManager.SetMute(d.Id, m);
                sl.SetState(-1f, m);
                shell.UpdateVolumes();
            };
            Add(sl);
            y += 36;
            return sl;
        }

        public void SyncVolumes()
        {
            SyncOne(outSlider, shell.OutDevices);
            SyncOne(micSlider, shell.MicDevices);
            foreach (var l in appLines) l.Sync();
        }

        void SyncOne(NeonSlider s, List<AudioDevice> devs)
        {
            if (s == null || s.Dragging) return;
            var d = devs.Find(x => x.IsDefault);
            if (d == null) { s.SetState(0f, false); return; }
            s.SetState(AudioManager.GetVolume(d.Id), AudioManager.GetMute(d.Id));
        }

        static string AppSignature(List<AppGroup> gs) { return string.Join("|", gs.Select(g => g.Key).ToArray()); }

        void RefreshApps()
        {
            foreach (var l in appLines) if (l.Slider.Dragging) return;
            if (outSlider != null && outSlider.Dragging) return;
            var fresh = SessionManager.GetGroups();
            if (AppSignature(fresh) != appSig)
            {
                foreach (var g in fresh) g.Dispose();
                Rebuild();     // 앱이 생기거나 사라지면 탭을 다시 그림 (스크롤 유지)
                return;
            }
            var old = groups;
            groups = fresh;
            for (int i = 0; i < appLines.Count && i < fresh.Count; i++) appLines[i].Group = fresh[i];
            foreach (var g in old) g.Dispose();
            SyncVolumes();
        }

        void DisposeGroups()
        {
            foreach (var g in groups) g.Dispose();
            groups = new List<AppGroup>();
        }

        // ───────────── 설정 탭: 테마 · 독 ─────────────
        void BuildTheme()
        {
            Section("테마", null);
            int cols = 3, gap = 10;
            int w = (CW - gap * (cols - 1)) / cols;
            for (int i = 0; i < Themes.Names.Length; i++)
            {
                var c = new ThemeCard(i);
                c.Selected = i == S.ThemeCombo;
                c.Bounds = new Rectangle((i % cols) * (w + gap), y + (i / cols) * (88 + gap), w, 88);
                int idx = i;
                c.Click += delegate
                {
                    if (S.ThemeCombo == idx) return;
                    S.ThemeCombo = idx;
                    S.Save();
                    Themes.Apply(S, true);
                };
                Add(c);
            }
            y += ((Themes.Names.Length + cols - 1) / cols) * (88 + gap) + 4;

            var card = AddCard(56);
            card.Title = "밝기";
            card.Tag = 330;
            int bx = card.Width - 16;
            for (int i = Themes.ModeNames.Length - 1; i >= 0; i--)
            {
                var chip = new Chip();
                chip.BackColor = UI.Panel;
                chip.Text = i == 0 ? "시스템" : Themes.ModeNames[i];
                chip.Active = S.ThemeMode == i;
                chip.FitWidth();
                bx -= chip.Width;
                chip.Location = new Point(bx, 13);
                bx -= 6;
                int mode = i;
                chip.Click += delegate
                {
                    if (S.ThemeMode == mode) return;
                    S.ThemeMode = mode;
                    S.Save();
                    Themes.Apply(S, true);
                };
                card.Controls.Add(chip);
            }
            y += 10;
        }

        void BuildDock()
        {
            Section("독", null);
            var c1 = AddCard(64);
            var tDock = new NeonCheck();
            tDock.AutoWidth = false;
            tDock.BackColor = UI.Panel;
            tDock.Text = "화면 위쪽에 독 표시";
            tDock.Description = "끄면 트레이 아이콘을 눌러서만 열 수 있어요";
            tDock.Bounds = new Rectangle(16, 12, c1.Width - 32, 42);
            tDock.SetSilently(S.ShowDock);
            tDock.CheckedChanged += delegate { S.ShowDock = tDock.Checked; S.Save(); shell.ApplyDockVisibility(); };
            c1.Controls.Add(tDock);

            var c2 = AddCard(64);
            var tFs = new NeonCheck();
            tFs.AutoWidth = false;
            tFs.BackColor = UI.Panel;
            tFs.Text = "전체화면 게임 · 영상일 때 숨기기";
            tFs.Description = "게임 화면을 가리지 않도록 잠시 숨겼다가 나오면 다시 보여줘요";
            tFs.Bounds = new Rectangle(16, 12, c2.Width - 32, 42);
            tFs.SetSilently(S.HideDockFullscreen);
            tFs.CheckedChanged += delegate { S.HideDockFullscreen = tFs.Checked; S.Save(); };
            c2.Controls.Add(tFs);

            var c3 = AddCard(56);
            c3.Title = "독 위치";
            c3.Subtitle = "독 왼쪽 점 부분을 끌어서 옮길 수 있어요";
            c3.Tag = 150;
            var reset = new NeonButton();
            reset.Text = "위 가운데로";
            reset.BackColor = UI.Panel;
            reset.Bounds = new Rectangle(c3.Width - 16 - 120, 11, 120, 34);
            reset.Click += delegate { shell.ResetDockPosition(); };
            c3.Controls.Add(reset);
            y += 10;
        }

        // ───────────── 이전 설정 창에서 옮겨 온 부분 ─────────────
        // ───────────── 공용 배치 도구 ─────────────
        void Add(Control c) { content.Controls.Add(c); }

        void Section(string title, string right)
        {
            var l = new SectionLabel(title);
            l.RightText = right;
            l.Bounds = new Rectangle(2, y, CW - 4, 22);
            Add(l);
            y += 30;
        }

        void Note(string text)
        {
            var n = new NoteLabel(text);
            n.Bounds = new Rectangle(2, y, CW - 4, 18);
            Add(n);
            n.FitHeight();
            y += n.Height + 10;
        }

        Card AddCard(int h)
        {
            var c = new Card();
            c.Bounds = new Rectangle(0, y, CW, h);
            Add(c);
            y += h + 8;
            return c;
        }

        void RebuildNow()
        {
            int scroll = -content.AutoScrollPosition.Y;
            ShowPage(page);
            content.AutoScrollPosition = new Point(0, scroll);
        }

        HotkeyBox KeyRow(Card card, Hotkey initial, Func<Hotkey> get, Action<Hotkey> set, string label)
        {
            var box = new HotkeyBox();
            box.BackColor = UI.Panel;
            box.Value = initial;
            box.Bounds = new Rectangle(card.Width - 196, (card.Height - 32) / 2, 180, 32);
            card.Tag = 200;
            box.ValueChanged += delegate
            {
                set(box.Value.Clone());
                S.Save();
                ValidateKeys();
                shell.RegisterHotkeys();
            };
            card.Controls.Add(box);
            slots.Add(new KeySlot { Box = box, Card = card, Get = get, Label = label });
            return box;
        }

        // 겹치는 조합 · 다른 프로그램이 쓰는 조합을 찾아 경고
        void ValidateKeys()
        {
            if (slots.Count == 0) return;
            var all = new List<KeyValuePair<string, Hotkey>>();
            foreach (var a in Actions.All) all.Add(new KeyValuePair<string, Hotkey>(Actions.Label(a), S.Key(a)));
            foreach (var p in S.Profiles) all.Add(new KeyValuePair<string, Hotkey>("프로필 '" + p.Name + "'", p.Key));
            if (S.DirectMods != 0)
                for (int i = 0; i < 8; i++)
                    all.Add(new KeyValuePair<string, Hotkey>((i + 1) + "번 출력 장치", new Hotkey(S.DirectMods, 0x31 + i)));

            foreach (var s in slots)
            {
                string warn = null;
                var k = s.Get();
                if (k != null && !k.IsEmpty)
                {
                    foreach (var kv in all)
                        if (!ReferenceEquals(kv.Value, k) && kv.Value.SameAs(k)) { warn = "'" + kv.Key + "'와(과) 겹쳐요"; break; }
                    if (warn == null && IsHandleCreated && !shell.OwnsHotkey(k))
                    {
                        try
                        {
                            if (RegisterHotKey(Handle, 0xBFFF, k.Mods, k.Vk)) UnregisterHotKey(Handle, 0xBFFF);
                            else warn = "다른 프로그램이 이미 쓰고 있어요";
                        }
                        catch { }
                    }
                }
                s.Box.Warning = warn;
                s.Box.Invalidate();
                if (s.Card.Title != null) { s.Card.Warning = warn; s.Card.Invalidate(); }
            }
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); ValidateKeys(); }

        // ───────────── 단축키 탭 ─────────────
        void BuildHotkeys()
        {
            Section("단축키", null);
            Note("입력칸을 클릭한 뒤 원하는 조합을 누르세요.  Backspace = 지우기  ·  Esc = 취소");

            foreach (var a in Actions.All)
            {
                string act = a;
                var card = AddCard(56);
                card.Title = Actions.Label(a);
                KeyRow(card, S.Key(a), delegate { return S.Key(act); }, delegate(Hotkey h) { S.Keys[act] = h; }, Actions.Label(a));
            }

            y += 10;
            Section("번호로 바로 전환", null);
            var dcard = AddCard(56);
            dcard.Title = "출력 장치 1~8번";
            dcard.Subtitle = "메인 창 목록 순서대로 숫자 키에 연결";
            dcard.Tag = 200;
            var dd = new Dropdown();
            dd.BackColor = UI.Panel;
            dd.Bounds = new Rectangle(dcard.Width - 196, 12, 180, 32);
            int CA = Hotkey.MOD_CONTROL | Hotkey.MOD_ALT;
            dd.Add("Ctrl+Alt+숫자", CA);
            dd.Add("Ctrl+Shift+숫자", Hotkey.MOD_CONTROL | Hotkey.MOD_SHIFT);
            dd.Add("Alt+Shift+숫자", Hotkey.MOD_ALT | Hotkey.MOD_SHIFT);
            dd.Add("Ctrl+Alt+Shift+숫자", CA | Hotkey.MOD_SHIFT);
            dd.Add("사용 안 함", 0);
            dd.SelectValue(S.DirectMods);
            dd.SelectedChanged += delegate
            {
                S.DirectMods = (int)dd.SelectedValue;
                S.Save();
                ValidateKeys();
            };
            dcard.Controls.Add(dd);

            y += 10;
            var reset = new NeonButton();
            reset.Text = "기본값으로 되돌리기";
            reset.Bounds = new Rectangle(0, y, 180, 34);
            reset.Click += delegate
            {
                foreach (var a in Actions.All) S.Keys[a] = Actions.Default(a);
                S.DirectMods = CA;
                S.Save();
                Rebuild();
            };
            Add(reset);
            y += 44;
        }

        // ───────────── 프로필 탭 ─────────────
        Dropdown DeviceDropdown(List<AudioDevice> devs, string currentId, string currentName, string noneText)
        {
            var dd = new Dropdown();
            dd.BackColor = UI.Panel;
            dd.Add(noneText, "");
            dd.AddHeader("장치");
            foreach (var d in devs) dd.Add(d.Name, d.Id);
            if (string.IsNullOrEmpty(currentId)) dd.SelectValue("");
            else
            {
                dd.SelectValue(currentId);
                if (dd.SelectedValue == null)
                {
                    dd.Add((currentName ?? "알 수 없는 장치") + "  (연결 안 됨)", currentId);
                    dd.SelectValue(currentId);
                }
            }
            return dd;
        }

        static string NameOf(List<AudioDevice> devs, string id, string fallback)
        {
            foreach (var d in devs) if (d.Id == id) return d.Name;
            return fallback;
        }

        void VolumeRow(Card card, int top, string label, float initial, Action<float> set)
        {
            var chk = new NeonCheck();
            chk.AutoWidth = false;
            chk.BackColor = UI.Panel;
            chk.Text = label;
            chk.Bounds = new Rectangle(16, top + 4, 150, 24);
            chk.SetSilently(initial >= 0);
            var sl = new NeonSlider();
            sl.ShowIcon = false;
            sl.BackColor = UI.Panel;
            sl.Bounds = new Rectangle(170, top, card.Width - 186, 32);
            sl.SetValue(initial >= 0 ? initial : 0.5f);
            sl.Visible = initial >= 0;
            chk.CheckedChanged += delegate
            {
                sl.Visible = chk.Checked;
                set(chk.Checked ? sl.Value : -1f);
                S.Save();
            };
            sl.ValueChanged += delegate { set(sl.Value); };
            sl.MouseUp += delegate { S.Save(); };
            card.Controls.Add(chk);
            card.Controls.Add(sl);
        }

        void LabelAt(Card card, string text, int top)
        {
            var l = new NoteLabel(text);
            l.Color = UI.Dim;
            l.TextFont = UI.BodyFont;
            l.BackColor = UI.Panel;
            l.Bounds = new Rectangle(16, top + 7, 140, 20);
            card.Controls.Add(l);
        }

        void BuildProfiles()
        {
            Section("프로필", S.Profiles.Count > 0 ? S.Profiles.Count + "개" : null);
            Note("출력 장치 · 마이크 · 볼륨을 묶어 두고 클릭이나 단축키 한 번에 바꿉니다.\n'변경 안 함'으로 두면 그 항목은 건드리지 않아요.");

            var outs = shell.OutDevices;
            var mics = shell.MicDevices;

            foreach (var p0 in S.Profiles)
            {
                var p = p0;
                var card = AddCard(16 + 32 + 14 + 44 * 5 + 6);
                int top = 14;

                var name = new DarkTextBox();
                name.BackColor = UI.Panel;
                name.Text = p.Name;
                name.Box.Font = UI.BodyBold;
                name.Bounds = new Rectangle(16, top, card.Width - 16 - 112, 32);
                name.Box.TextChanged += delegate
                {
                    string t = name.Text.Trim();
                    if (t.Length > 0) p.Name = t;
                };
                name.Box.LostFocus += delegate { S.Save(); ValidateKeys(); };
                card.Controls.Add(name);

                var del = new NeonButton();
                del.Danger = true;
                del.Text = "삭제";
                del.BackColor = UI.Panel;
                del.Bounds = new Rectangle(card.Width - 16 - 84, top, 84, 32);
                del.Click += delegate
                {
                    S.Profiles.Remove(p);
                    S.Save();
                    shell.RegisterHotkeys();
                    shell.RefreshUI();
                    Rebuild();
                };
                card.Controls.Add(del);
                top += 32 + 14;

                LabelAt(card, "출력 장치", top);
                var dOut = DeviceDropdown(outs, p.OutId, p.OutName, "변경 안 함");
                dOut.Bounds = new Rectangle(170, top, card.Width - 186, 32);
                dOut.SelectedChanged += delegate
                {
                    string id = dOut.SelectedValue as string;
                    p.OutId = string.IsNullOrEmpty(id) ? null : id;
                    p.OutName = p.OutId == null ? null : NameOf(outs, p.OutId, p.OutName);
                    S.Save();
                };
                card.Controls.Add(dOut);
                top += 44;

                LabelAt(card, "마이크", top);
                var dMic = DeviceDropdown(mics, p.MicId, p.MicName, "변경 안 함");
                dMic.Bounds = new Rectangle(170, top, card.Width - 186, 32);
                dMic.SelectedChanged += delegate
                {
                    string id = dMic.SelectedValue as string;
                    p.MicId = string.IsNullOrEmpty(id) ? null : id;
                    p.MicName = p.MicId == null ? null : NameOf(mics, p.MicId, p.MicName);
                    S.Save();
                };
                card.Controls.Add(dMic);
                top += 44;

                VolumeRow(card, top, "출력 볼륨 지정", p.OutVol, delegate(float v) { p.OutVol = v; });
                top += 44;
                VolumeRow(card, top, "마이크 볼륨 지정", p.MicVol, delegate(float v) { p.MicVol = v; });
                top += 44;

                LabelAt(card, "단축키", top);
                var box = new HotkeyBox();
                box.BackColor = UI.Panel;
                box.Value = p.Key;
                box.Bounds = new Rectangle(170, top, 200, 32);
                box.ValueChanged += delegate { p.Key = box.Value.Clone(); S.Save(); ValidateKeys(); shell.RegisterHotkeys(); };
                card.Controls.Add(box);
                var warnLbl = new Card();   // 경고 문구만 그리는 용도 (Title 없음)
                slots.Add(new KeySlot { Box = box, Card = warnLbl, Get = delegate { return p.Key; }, Label = "프로필 '" + p.Name + "'" });
            }

            var add = new NeonButton();
            add.Primary = S.Profiles.Count == 0;
            add.Text = "+  지금 설정으로 새 프로필";
            add.Bounds = new Rectangle(0, y + 4, 240, 38);
            add.Click += delegate
            {
                if (shell.CreateProfileFromCurrent(this)) Rebuild();
            };
            Add(add);
            y += 50;
        }

        // ───────────── 앱별 출력 탭 ─────────────
        void BuildRoutes()
        {
            Section("앱별 출력 장치", S.Routes.Count > 0 ? S.Routes.Count + "개" : null);
            if (!AppRouting.Available)
            {
                var c = AddCard(64);
                c.Title = "이 Windows 버전에서는 쓸 수 없어요";
                c.Subtitle = "Windows 10 1803 이상에서 지원됩니다.";
                c.Warning = null;
                return;
            }
            Note("지정한 앱은 기본 장치와 상관없이 항상 그 장치로 소리가 나갑니다.\n예) 디스코드는 헤드셋, 유튜브(브라우저)는 스피커.  일부 앱은 다시 실행해야 적용돼요.");

            var outs = shell.OutDevices;
            foreach (var r0 in S.Routes.ToList())
            {
                var r = r0;
                var card = AddCard(60);
                card.IconImage = SessionManager.GetIcon(r.ExePath);
                if (card.IconImage == null) card.IconKind = 5;
                card.Title = SessionManager.FriendlyName(r.ExePath);
                card.Subtitle = System.IO.Path.GetFileName(r.ExePath);
                card.Tag = 280;

                var dd = new Dropdown();
                dd.BackColor = UI.Panel;
                foreach (var d in outs) dd.Add(d.Name, d.Id);
                dd.SelectValue(r.DeviceId);
                if (dd.SelectedValue == null)
                {
                    dd.Add((r.DeviceName ?? "알 수 없는 장치") + "  (연결 안 됨)", r.DeviceId);
                    dd.SelectValue(r.DeviceId);
                }
                dd.Bounds = new Rectangle(card.Width - 16 - 36 - 8 - 220, 14, 220, 32);
                dd.SelectedChanged += delegate
                {
                    string id = dd.SelectedValue as string;
                    shell.SetRoute(r.ExePath, id, NameOf(outs, id, r.DeviceName));
                };
                card.Controls.Add(dd);

                var rm = new TitleButton(0);
                rm.BackColor = UI.Panel;
                rm.Location = new Point(card.Width - 16 - 32, 14);
                rm.Click += delegate
                {
                    shell.SetRoute(r.ExePath, null, null);
                    Rebuild();
                };
                new ToolTip().SetToolTip(rm, "규칙 삭제 (기본 장치 따르기)");
                card.Controls.Add(rm);
            }

            if (S.Routes.Count == 0)
            {
                var empty = AddCard(60);
                empty.Title = "아직 지정한 앱이 없어요";
                empty.Subtitle = "아래에서 앱을 추가하거나, 앱별 볼륨 창에서 바로 고를 수 있어요.";
            }

            y += 4;
            var add = new NeonButton();
            add.Text = "+  앱 추가";
            add.Bounds = new Rectangle(0, y, 160, 36);
            add.Click += delegate { ShowAddAppMenu(add); };
            Add(add);
            y += 46;
        }

        void ShowAddAppMenu(Control anchor)
        {
            var m = Menus.Create();
            m.Items.Add(Menus.Header("지금 소리를 내는 앱"));
            var groups = SessionManager.GetGroups();
            int n = 0;
            foreach (var g in groups)
            {
                if (g.IsSystem || string.IsNullOrEmpty(g.ExePath) || S.RouteFor(g.ExePath) != null) continue;
                string path = g.ExePath;
                var it = Menus.Item(g.Name, delegate { AddRoute(path); });
                var ic = SessionManager.GetIcon(path);
                if (ic != null) { it.Image = ic; m.ShowImageMargin = true; }
                m.Items.Add(it);
                n++;
            }
            foreach (var g in groups) g.Dispose();
            if (n == 0) m.Items.Add(Menus.Header("    (없음 — 앱에서 소리를 재생해 보세요)"));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Menus.Item("실행 파일 직접 찾기…", delegate
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "프로그램 (*.exe)|*.exe";
                    dlg.Title = "출력 장치를 지정할 프로그램 선택";
                    if (dlg.ShowDialog(this) == DialogResult.OK) AddRoute(dlg.FileName);
                }
            }));
            m.Show(anchor, new Point(0, anchor.Height + 4));
        }

        void AddRoute(string exePath)
        {
            var outs = shell.OutDevices;
            if (outs.Count == 0) return;
            // 기본 장치가 아닌 첫 장치로 시작 (바로 효과가 보이도록)
            var d = outs.FirstOrDefault(x => !x.IsDefault) ?? outs[0];
            shell.SetRoute(exePath, d.Id, d.Name);
            Rebuild();
        }

        // ───────────── 일반 탭 ─────────────
        void BuildGeneral()
        {
            Section("전환 알림", null);
            var c1 = AddCard(64);
            var tToast = new NeonCheck();
            tToast.AutoWidth = false;
            tToast.BackColor = UI.Panel;
            tToast.Text = "장치를 바꿀 때 알림 표시";
            tToast.Description = "단축키로 바꿔도 지금 어떤 장치인지 화면에 잠깐 보여줘요";
            tToast.Bounds = new Rectangle(16, 12, c1.Width - 32, 42);
            tToast.SetSilently(S.ToastEnabled);
            tToast.CheckedChanged += delegate { S.ToastEnabled = tToast.Checked; S.Save(); };
            c1.Controls.Add(tToast);

            var c2 = AddCard(56);
            c2.Title = "알림 위치";
            c2.Tag = 300;
            var pos = new Dropdown();
            pos.BackColor = UI.Panel;
            for (int i = 0; i < Settings.ToastPosNames.Length; i++) pos.Add(Settings.ToastPosNames[i], i);
            pos.SelectValue(S.ToastPos);
            pos.Bounds = new Rectangle(c2.Width - 16 - 100 - 8 - 170, 12, 170, 32);
            pos.SelectedChanged += delegate
            {
                S.ToastPos = (int)pos.SelectedValue;
                ToastForm.Position = S.ToastPos;
                S.Save();
                ToastForm.Popup("알림 위치", Settings.ToastPosNames[S.ToastPos], 0, "여기에 표시됩니다");
            };
            c2.Controls.Add(pos);
            var preview = new NeonButton();
            preview.Text = "미리 보기";
            preview.BackColor = UI.Panel;
            preview.Bounds = new Rectangle(c2.Width - 16 - 100, 12, 100, 32);
            preview.Click += delegate
            {
                var d = shell.OutDevices.Find(x => x.IsDefault);
                ToastForm.Popup("출력 장치 전환", d != null ? d.Name : "스피커", d != null ? d.Kind : 0, "미리 보기");
            };
            c2.Controls.Add(preview);

            y += 10;
            Section("동작", null);
            var c3 = AddCard(64);
            var tComm = new NeonCheck();
            tComm.AutoWidth = false;
            tComm.BackColor = UI.Panel;
            tComm.Text = "통신 장치도 함께 변경";
            tComm.Description = "디스코드 · 게임 보이스가 쓰는 '통신 장치'까지 같이 바꿉니다";
            tComm.Bounds = new Rectangle(16, 12, c3.Width - 32, 42);
            tComm.SetSilently(S.AlsoComm);
            tComm.CheckedChanged += delegate { S.AlsoComm = tComm.Checked; S.Save(); };
            c3.Controls.Add(tComm);

            var c4 = AddCard(64);
            var tAuto = new NeonCheck();
            tAuto.AutoWidth = false;
            tAuto.BackColor = UI.Panel;
            tAuto.Text = "Windows 시작 시 자동 실행";
            tAuto.Description = "로그인하면 트레이에서 조용히 시작합니다";
            tAuto.Bounds = new Rectangle(16, 12, c4.Width - 32, 42);
            tAuto.SetSilently(Shell.IsAutoStart());
            tAuto.CheckedChanged += delegate { Shell.SetAutoStart(tAuto.Checked); };
            c4.Controls.Add(tAuto);

        }
        void BuildInfo()
        {
            y += 10;
            Section("정보", null);
            Note("설정 파일: %APPDATA%\\AudioSwitcher\\settings.ini\n디스코드에 마이크 변경이 반영되지 않으면 디스코드 음성 설정의 입력/출력 장치를 '기본값'으로 바꿔 주세요.");
        }

    }
}
