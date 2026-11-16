// 독(화면 위쪽에 붙는 막대) + 트레이 + 전역 단축키 + 장치 전환 · 프로필 · 앱별 출력 로직
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NeonAudioSwitcher
{
    // 독의 둥근 정사각 버튼 (장치 · 마이크 · 펼치기)
    class DockKey : FlatControl
    {
        public int IconKind;        // -1 = 펼치기 화살표
        public bool Active, Muted;

        public DockKey(int kind) { IconKind = kind; Size = new Size(44, 44); Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            UI.FillRound(g, r, 16, Active ? UI.Accent : (hover ? UI.PanelHover : UI.Raised));
            Color fg = Active ? UI.OnAccent : (hover ? UI.Text : UI.Dim);
            if (IconKind < 0)
            {
                int cx = Width / 2, cy = Height / 2;
                using (var p = new Pen(fg, 2f))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    int d = Active ? -1 : 1;   // 펼친 상태면 위로 향함
                    g.DrawLines(p, new[] { new Point(cx - 5, cy - 2 * d), new Point(cx, cy + 3 * d), new Point(cx + 5, cy - 2 * d) });
                }
            }
            else UI.DrawKindIcon(g, new Rectangle(0, 0, Width, Height), IconKind, Muted ? UI.Faint : fg);
            if (Muted)
                using (var p = new Pen(UI.Danger, 2f))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, 14, Height - 14, Width - 14, 14);
                }
        }
    }

    // 독 가운데: 현재 장치 이름 + 얇은 볼륨 막대 (드래그 · 휠로 조절)
    class DockVolume : FlatControl
    {
        public string DeviceName = "";
        public float Value;
        public bool Muted;
        bool dragging;
        public bool Dragging { get { return dragging; } }
        public event EventHandler ValueChanged;

        public DockVolume() { Cursor = Cursors.Hand; Height = 44; }

        Rectangle Track { get { return new Rectangle(0, 30, Width, 6); } }

        void SetFromX(int x)
        {
            Value = Math.Max(0f, Math.Min(1f, x / (float)Width));
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e) { dragging = true; SetFromX(e.X); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromX(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; base.OnMouseUp(e); }
        protected override void OnMouseEnter(EventArgs e) { Focus(); base.OnMouseEnter(e); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Value = Math.Max(0f, Math.Min(1f, Value + (e.Delta > 0 ? 0.02f : -0.02f)));
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            string pct = Muted ? "음소거" : ((int)Math.Round(Value * 100)).ToString();
            var pw = g.MeasureString(pct, UI.BadgeFont).Width + 4;
            using (var b = new SolidBrush(UI.Text))
                g.DrawString(DeviceName, UI.BodyBold, b, new RectangleF(0, 4, Width - pw - 6, 20), UI.Ellipsis(StringAlignment.Near));
            using (var b = new SolidBrush(Muted ? UI.Danger : UI.Dim))
                g.DrawString(pct, Muted ? UI.HintFont : UI.BadgeFont, b, new RectangleF(0, 4, Width, 20), UI.Ellipsis(StringAlignment.Far));
            var t = Track;
            UI.FillRound(g, t, 6, UI.Track);
            int w = (int)(t.Width * Value);
            if (w > 2) UI.FillRound(g, new Rectangle(t.X, t.Y, w, t.Height), 6, Muted ? UI.Faint : UI.Accent);
            if (hover || dragging)
                using (var b = new SolidBrush(UI.Accent)) g.FillEllipse(b, t.X + w - 6, t.Y - 3, 12, 12);
        }
    }

    // 독 오른쪽 프로필 칩
    class DockChip : FlatControl
    {
        public DockChip() { Cursor = Cursors.Hand; Height = 44; Font = UI.BodyBold; }

        public void Fit() { Width = Math.Min(150, TextRenderer.MeasureText(Text, Font).Width + 46); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            UI.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), 16, hover ? UI.PanelHover : UI.Raised);
            UI.DrawKindIcon(g, new Rectangle(8, (Height - 22) / 2, 24, 22), 4, UI.Dim);
            using (var b = new SolidBrush(UI.Text))
                g.DrawString(Text, Font, b, new RectangleF(34, 0, Width - 42, Height), UI.Ellipsis(StringAlignment.Near));
        }
    }

    partial class Shell : ChromeForm
    {
        public const int DockW = 720, DockH = 64;
        const int WM_HOTKEY = 0x0312;
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, int mod, int vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }

        Settings S { get { return App.S; } }

        public List<AudioDevice> OutDevices = new List<AudioDevice>();
        public List<AudioDevice> MicDevices = new List<AudioDevice>();

        WorkspaceForm work;
        NotifyIcon tray;
        Timer pollTimer, fsTimer;
        string lastSig = "";
        bool hotkeysSuspended;
        int pollCount;
        bool autoHidden;
        Dictionary<int, Action> hotkeyActions = new Dictionary<int, Action>();
        Dictionary<int, Hotkey> hotkeyKeys = new Dictionary<int, Hotkey>();

        // 독 구성 요소
        List<DockKey> quick = new List<DockKey>();
        DockVolume vol;
        DockKey micBtn, expandBtn;
        DockChip profBtn;
        FlatControl grip;
        int dividerX;
        ToolTip tips = new ToolTip();

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "NeonAudioSwitcher";

        public Shell()
        {
            Text = "Audio Switcher";
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(DockW, DockH);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            ToastForm.Position = S.ToastPos;

            BuildTray();

            pollTimer = new Timer();
            pollTimer.Interval = 2000;
            pollTimer.Tick += delegate
            {
                RefreshDevices(false);
                CheckTriggers();
                if (++pollCount % 2 == 0) AppRouting.Maintain(S.Routes);
                if (pollCount % 5 == 0 && volDirty) { volDirty = false; S.Save(); }
            };
            pollTimer.Start();
            StartAutomation();

            fsTimer = new Timer();
            fsTimer.Interval = 1000;
            fsTimer.Tick += delegate { CheckFullscreen(); };
            fsTimer.Start();

            Themes.Changed += OnThemeChanged;
            HotkeyBox.ListenStart += SuspendHotkeys;
            HotkeyBox.ListenEnd += ResumeHotkeys;

            RefreshDevices(true);
            if (S.AlsoComm) SyncCommToDefaults();
            AppRouting.Maintain(S.Routes);
            PlaceDock();
        }

        // 독을 띄울지 (전체화면 숨김 · 사용자가 끈 경우 제외)
        public void ApplyDockVisibility()
        {
            bool want = S.ShowDock && !autoHidden;
            if (want && !Visible) Show();
            else if (!want && Visible) Hide();
        }

        // ───────────── 독 배치 ─────────────
        void PlaceDock()
        {
            Point p;
            if (S.DockX != int.MinValue) p = new Point(S.DockX, S.DockY);
            else
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                p = new Point(wa.Left + (wa.Width - DockW) / 2, wa.Top + 8);
            }
            var scr = Screen.FromPoint(new Point(p.X + DockW / 2, p.Y + DockH / 2)).WorkingArea;
            p.X = Math.Max(scr.Left, Math.Min(p.X, scr.Right - DockW));
            p.Y = Math.Max(scr.Top, Math.Min(p.Y, scr.Bottom - DockH));
            Location = p;
        }

        void BuildDock()
        {
            SuspendLayout();
            foreach (Control c in Controls.Cast<Control>().ToList()) { Controls.Remove(c); c.Dispose(); }
            quick.Clear();
            BackColor = UI.Bg;

            int x = 10;
            grip = new FlatControl();
            grip.Bounds = new Rectangle(x, 10, 14, 44);
            grip.Cursor = Cursors.SizeAll;
            grip.Paint += delegate(object s, PaintEventArgs e)
            {
                UI.Smooth(e.Graphics);
                using (var b = new SolidBrush(UI.Faint))
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 2; c++)
                            e.Graphics.FillEllipse(b, 3 + c * 5, 14 + r * 6, 3, 3);
            };
            grip.MouseDown += delegate(object s, MouseEventArgs e)
            {
                BeginDrag(s, e);           // 놓을 때까지 돌아오지 않음
                S.DockX = Left; S.DockY = Top;
                S.Save();
                if (work != null && work.Visible) PositionPanel();
            };
            tips.SetToolTip(grip, "끌어서 독 옮기기");
            Controls.Add(grip);
            x += 20;

            for (int i = 0; i < OutDevices.Count && i < 5; i++)
            {
                var d = OutDevices[i];
                var k = new DockKey(d.Kind);
                k.Active = d.IsDefault;
                k.Location = new Point(x, 10);
                k.Click += delegate { SwitchTo(d); };
                tips.SetToolTip(k, d.Name + (S.DirectMods != 0 && i < 8 ? "   " + Hotkey.ModsText(S.DirectMods) + (i + 1) : ""));
                Controls.Add(k);
                quick.Add(k);
                x += 52;
            }
            dividerX = x + 1;
            x += 12;

            // 오른쪽부터 배치
            int right = DockW - 10;
            expandBtn = new DockKey(-1);
            expandBtn.Location = new Point(right - 44, 10);
            expandBtn.Active = work != null && work.Visible;
            expandBtn.Click += delegate { TogglePanel(); };
            tips.SetToolTip(expandBtn, "펼치기 / 접기");
            Controls.Add(expandBtn);
            right -= 52;

            profBtn = new DockChip();
            var active = S.Profiles.FirstOrDefault(p => IsActive(p));
            profBtn.Text = active != null ? active.Name : "프로필";
            profBtn.Fit();
            profBtn.Location = new Point(right - profBtn.Width, 10);
            profBtn.Click += delegate { ShowProfilesMenu(profBtn); };
            Controls.Add(profBtn);
            right -= profBtn.Width + 8;

            micBtn = new DockKey(3);
            micBtn.Location = new Point(right - 44, 10);
            micBtn.MouseUp += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ToggleMute(true);
                else if (e.Button == MouseButtons.Right) ShowMicMenu(micBtn);
            };
            tips.SetToolTip(micBtn, "클릭 = 마이크 음소거  ·  우클릭 = 마이크 선택");
            Controls.Add(micBtn);
            right -= 52;

            vol = new DockVolume();
            vol.Bounds = new Rectangle(x, 10, Math.Max(60, right - x - 4), 44);
            vol.ValueChanged += delegate
            {
                var d = OutDevices.Find(z => z.IsDefault);
                if (d != null) AudioManager.SetVolume(d.Id, vol.Value);
            };
            Controls.Add(vol);

            ResumeLayout();
            UpdateVolumes();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(UI.Border)) e.Graphics.DrawLine(p, dividerX, 20, dividerX, DockH - 20);
        }

        public void ResetDockPosition()
        {
            S.DockX = S.DockY = int.MinValue;
            S.Save();
            PlaceDock();
            if (PanelVisible) PositionPanel();
        }

        // ───────────── 펼침 패널 ─────────────
        public bool PanelVisible { get { return work != null && work.Visible; } }

        public void OpenPanel(int page)
        {
            if (work == null || work.IsDisposed) work = new WorkspaceForm(this);
            work.ShowPage(page);
            PositionPanel();
            if (!work.Visible) work.Show();
            work.Activate();
            if (expandBtn != null) { expandBtn.Active = true; expandBtn.Invalidate(); }
        }

        public void TogglePanel()
        {
            if (PanelVisible) ClosePanel();
            else OpenPanel(work != null ? work.CurrentPage : WorkspaceForm.PageDevices);
        }

        public void ClosePanel()
        {
            if (work != null && work.Visible) work.Hide();
            if (expandBtn != null) { expandBtn.Active = false; expandBtn.Invalidate(); }
        }

        void PositionPanel()
        {
            if (work == null) return;
            var scr = Screen.FromPoint(new Point(Left + DockW / 2, Top + DockH / 2)).WorkingArea;
            int x = Left;
            int y = Top + DockH + 8;
            if (y + work.Height > scr.Bottom) y = Top - 8 - work.Height;   // 독이 아래쪽에 있으면 위로 펼침
            x = Math.Max(scr.Left, Math.Min(x, scr.Right - work.Width));
            y = Math.Max(scr.Top, Math.Min(y, scr.Bottom - work.Height));
            work.Location = new Point(x, y);
        }

        // ───────────── 전체화면 앱이면 독 숨김 ─────────────
        void CheckFullscreen()
        {
            bool hide = false;
            if (S.ShowDock && S.HideDockFullscreen && !PanelVisible) hide = ForegroundIsFullscreen();
            if (hide != autoHidden) { autoHidden = hide; ApplyDockVisibility(); }
        }

        bool ForegroundIsFullscreen()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero || fg == Handle || (work != null && fg == work.Handle)) return false;
                var sb = new StringBuilder(64);
                GetClassName(fg, sb, 64);
                string cls = sb.ToString();
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return false;
                RECT r;
                if (!GetWindowRect(fg, out r)) return false;
                var scr = Screen.FromHandle(fg);
                if (!scr.Equals(Screen.FromPoint(new Point(Left + DockW / 2, Top + DockH / 2)))) return false;
                var b = scr.Bounds;
                return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
            }
            catch { return false; }
        }

        // ───────────── 테마 ─────────────
        void OnThemeChanged()
        {
            // 테마 카드 클릭 처리가 끝난 뒤에 화면을 다시 만든다
            BeginInvoke((Action)delegate
            {
                BackColor = UI.Bg;
                ApplyDwmDark();
                BuildDock();
                RebuildTrayMenu();
                if (work != null && !work.IsDisposed) { work.ApplyDwmDark(); work.RebuildAll(); }
                Invalidate(true);
            });
        }

        // ───────────── 트레이 ─────────────
        void BuildTray()
        {
            tray = new NotifyIcon();
            Icon exeIcon = null;
            try { exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            tray.Icon = exeIcon ?? MakeIcon();
            tray.Text = "Audio Switcher";
            tray.Visible = true;
            tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) TogglePanel();
            };
        }

        void RebuildTrayMenu()
        {
            var old = tray.ContextMenuStrip;
            var m = Menus.Create();
            AddTraySection(m, "출력 장치", OutDevices);
            m.Items.Add(new ToolStripSeparator());
            AddTraySection(m, "마이크", MicDevices);
            if (S.Profiles.Count > 0)
            {
                m.Items.Add(new ToolStripSeparator());
                m.Items.Add(Menus.Header("프로필"));
                foreach (var p in S.Profiles)
                {
                    var prof = p;
                    var it = Menus.Item((IsActive(p) ? "●  " : "     ") + p.Name, delegate { ApplyProfile(prof); });
                    if (IsActive(p)) it.ForeColor = UI.Ink;
                    m.Items.Add(it);
                }
            }
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Menus.Item("패널 펼치기", delegate { OpenPanel(WorkspaceForm.PageDevices); }));
            m.Items.Add(Menus.Item("설정", delegate { OpenPanel(WorkspaceForm.PageSettings); }));
            var dockItem = Menus.Item((S.ShowDock ? "✓  " : "     ") + "독 표시", delegate
            {
                S.ShowDock = !S.ShowDock;
                S.Save();
                ApplyDockVisibility();
                RebuildTrayMenu();
            });
            m.Items.Add(dockItem);
            m.Items.Add(Menus.Item("종료", delegate { tray.Visible = false; Application.Exit(); }));
            tray.ContextMenuStrip = m;
            if (old != null) old.Dispose();
        }

        void AddTraySection(ContextMenuStrip m, string title, List<AudioDevice> devs)
        {
            m.Items.Add(Menus.Header(title));
            foreach (var d in devs)
            {
                var dev = d;
                var item = Menus.Item((d.IsDefault ? "●  " : "     ") + d.Name, delegate { SwitchTo(dev); });
                if (d.IsDefault) item.ForeColor = UI.Ink;
                m.Items.Add(item);
            }
        }

        void ShowProfilesMenu(Control anchor)
        {
            var m = Menus.Create();
            if (S.Profiles.Count == 0) m.Items.Add(Menus.Header("저장된 프로필이 없어요"));
            foreach (var p in S.Profiles)
            {
                var prof = p;
                string hk = p.Key.IsEmpty ? "" : "    " + p.Key;
                var it = Menus.Item((IsActive(p) ? "●  " : "     ") + p.Name + hk, delegate { ApplyProfile(prof); });
                if (IsActive(p)) it.ForeColor = UI.Ink;
                m.Items.Add(it);
            }
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Menus.Item("+  지금 설정을 프로필로 저장", delegate { CreateProfileFromCurrent(this); }));
            m.Items.Add(Menus.Item("프로필 관리…", delegate { OpenPanel(WorkspaceForm.PageProfiles); }));
            m.Show(anchor, new Point(0, anchor.Height + 6));
        }

        void ShowMicMenu(Control anchor)
        {
            var m = Menus.Create();
            m.Items.Add(Menus.Header("마이크"));
            foreach (var d in MicDevices)
            {
                var dev = d;
                var it = Menus.Item((d.IsDefault ? "●  " : "     ") + d.Name, delegate { SwitchTo(dev); });
                if (d.IsDefault) it.ForeColor = UI.Ink;
                m.Items.Add(it);
            }
            m.Show(anchor, new Point(0, anchor.Height + 6));
        }

        // ───────────── 장치 목록 ─────────────
        public void RefreshDevices(bool force)
        {
            var rawOuts = AudioManager.GetDevices(EDataFlow.eRender);
            var rawMics = AudioManager.GetDevices(EDataFlow.eCapture);
            // 장치가 새로 연결되거나 빠졌으면 자동 전환 · 볼륨 복원 (기본 장치가 바뀌면 다시 읽음)
            if (HandleConnectionChanges(rawOuts, rawMics))
            {
                rawOuts = AudioManager.GetDevices(EDataFlow.eRender);
                rawMics = AudioManager.GetDevices(EDataFlow.eCapture);
            }
            RawOut = rawOuts;
            RawMic = rawMics;
            var outs = Present(rawOuts);
            var mics = Present(rawMics);
            string sig = string.Join("|", outs.Concat(mics).Select(d =>
                d.Id + d.Name + (d.IsDefault ? "*" : "") + (d.IsDefaultComm ? "+" : "")).ToArray());
            if (force || sig != lastSig)
            {
                lastSig = sig;
                OutDevices = outs;
                MicDevices = mics;
                RefreshUI();
            }
            else UpdateVolumes();
        }

        // 장치 · 프로필이 바뀌었을 때 화면 전체 갱신
        bool dockQueued;

        public void RefreshUI()
        {
            if (IsHandleCreated)
            {
                if (!dockQueued)
                {
                    dockQueued = true;
                    BeginInvoke((Action)delegate { dockQueued = false; BuildDock(); });
                }
            }
            else BuildDock();
            RebuildTrayMenu();
            if (work != null && !work.IsDisposed && work.Visible) work.OnDataChanged();
        }

        public void UpdateVolumes()
        {
            var o = OutDevices.Find(x => x.IsDefault);
            if (vol != null && !vol.Dragging)
            {
                vol.DeviceName = o != null ? o.Name : "출력 장치 없음";
                vol.Value = o != null ? Math.Max(0f, AudioManager.GetVolume(o.Id)) : 0f;
                vol.Muted = o != null && AudioManager.GetMute(o.Id);
                vol.Invalidate();
            }
            var m = MicDevices.Find(x => x.IsDefault);
            if (micBtn != null)
            {
                bool muted = m != null && AudioManager.GetMute(m.Id);
                if (micBtn.Muted != muted) { micBtn.Muted = muted; micBtn.Invalidate(); }
            }
            if (work != null && !work.IsDisposed && work.Visible) work.SyncVolumes();
            RememberVolumes(o, m);
            UpdateMicOverlay(m != null && AudioManager.GetMute(m.Id));
        }

        public bool OwnsHotkey(Hotkey k)
        {
            foreach (var h in hotkeyKeys.Values) if (h.SameAs(k)) return true;
            return false;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 독은 닫지 않고 숨김 (Alt+F4 등)
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; S.ShowDock = false; S.Save(); ApplyDockVisibility(); RebuildTrayMenu(); return; }
            base.OnFormClosing(e);
        }

        public static bool IsAutoStart()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null; }
            catch { return false; }
        }

        public static void SetAutoStart(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(RunName, false);
                }
            }
            catch { }
        }

        static Icon MakeIcon()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                UI.Smooth(g);
                UI.FillRound(g, new Rectangle(1, 1, 30, 30), 12, UI.Accent);
                UI.DrawKindIcon(g, new Rectangle(1, 1, 30, 30), 0, Color.White);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        void SyncCommToDefaults()
        {
            bool changed = false;
            changed |= SyncCommOne(OutDevices);
            changed |= SyncCommOne(MicDevices);
            if (changed) RefreshDevices(true);
        }

        bool SyncCommOne(List<AudioDevice> devs)
        {
            var d = devs.Find(x => x.IsDefault);
            if (d == null || d.IsDefaultComm) return false;
            return AudioManager.SetDefault(d.Id, true);
        }

        // ───────────── 전환 ─────────────
        void Notify(string title, string text, int kind, string detail)
        {
            if (S.ToastEnabled) ToastForm.Popup(title, text, kind, detail);
        }

        public void SwitchTo(AudioDevice d)
        {
            if (d.IsDefault && !(S.AlsoComm && !d.IsDefaultComm)) return;
            if (AudioManager.SetDefault(d.Id, S.AlsoComm))
            {
                bool isMic = d.Flow == EDataFlow.eCapture;
                float v = AudioManager.GetVolume(d.Id);
                string detail = v >= 0 ? string.Format("볼륨 {0}%", (int)Math.Round(v * 100)) : null;
                if (AudioManager.GetMute(d.Id)) detail = "음소거 상태";
                if (S.AlsoComm) detail = (detail == null ? "" : detail + "  ·  ") + "통화 장치 포함";
                Notify(isMic ? "마이크 전환" : "출력 장치 전환", d.Name, d.Kind, detail);
                RefreshDevices(true);
            }
        }

        void CycleDevice(bool mic)
        {
            RefreshDevices(true);
            var devs = mic ? MicDevices : OutDevices;
            if (devs.Count < 2)
            {
                Notify(mic ? "마이크" : "출력 장치", devs.Count == 0 ? "연결된 장치가 없어요" : "전환할 다른 장치가 없어요", mic ? 3 : 0, null);
                return;
            }
            int cur = devs.FindIndex(d => d.IsDefault);
            SwitchTo(devs[(cur + 1) % devs.Count]);
        }

        void ToggleMute(bool mic)
        {
            var d = (mic ? MicDevices : OutDevices).Find(x => x.IsDefault);
            if (d == null) return;
            bool m = !AudioManager.GetMute(d.Id);
            AudioManager.SetMute(d.Id, m);
            UpdateVolumes();
            Notify(mic ? (m ? "마이크 음소거" : "마이크 켜짐") : (m ? "출력 음소거" : "소리 켜짐"), d.Name, d.Kind, null);
        }

        // ───────────── 프로필 ─────────────
        public bool IsActive(Profile p)
        {
            if (p.OutId == null && p.MicId == null) return false;
            if (p.OutId != null)
            {
                var d = OutDevices.Find(x => x.IsDefault);
                if (d == null || d.Id != p.OutId) return false;
            }
            if (p.MicId != null)
            {
                var d = MicDevices.Find(x => x.IsDefault);
                if (d == null || d.Id != p.MicId) return false;
            }
            return true;
        }

        public void ApplyProfile(Profile p)
        {
            RefreshDevices(true);
            var missing = new List<string>();
            var done = new List<string>();
            if (p.OutId != null)
            {
                var d = RawOut.Find(x => x.Id == p.OutId);
                if (d == null) missing.Add(p.OutName ?? "출력 장치");
                else
                {
                    AudioManager.SetDefault(d.Id, S.AlsoComm);
                    if (p.OutVol >= 0) AudioManager.SetVolume(d.Id, p.OutVol);
                    done.Add(p.OutVol >= 0 ? string.Format("{0} {1}%", d.Name, (int)Math.Round(p.OutVol * 100)) : d.Name);
                }
            }
            if (p.MicId != null)
            {
                var d = RawMic.Find(x => x.Id == p.MicId);
                if (d == null) missing.Add(p.MicName ?? "마이크");
                else
                {
                    AudioManager.SetDefault(d.Id, S.AlsoComm);
                    if (p.MicVol >= 0) AudioManager.SetVolume(d.Id, p.MicVol);
                    done.Add(d.Name);
                }
            }
            RefreshDevices(true);
            string detail = missing.Count > 0
                ? "연결 안 됨: " + string.Join(", ", missing.ToArray())
                : string.Join("  ·  ", done.ToArray());
            string title = profileTitle ?? "프로필 적용";
            profileTitle = null;
            Notify(missing.Count > 0 && done.Count == 0 ? "프로필을 적용하지 못했어요" : title, p.Name, 4, detail);
        }

        // 현재 기본 장치/볼륨을 프로필에 담음
        public void CaptureCurrent(Profile p, bool withMic, bool withVolume)
        {
            var o = OutDevices.Find(x => x.IsDefault);
            var m = MicDevices.Find(x => x.IsDefault);
            p.OutId = o != null ? o.Id : null;
            p.OutName = o != null ? o.Name : null;
            p.OutVol = (o != null && withVolume) ? AudioManager.GetVolume(o.Id) : -1f;
            if (withMic && m != null)
            {
                p.MicId = m.Id; p.MicName = m.Name;
                p.MicVol = withVolume ? AudioManager.GetVolume(m.Id) : -1f;
            }
            else { p.MicId = null; p.MicName = null; p.MicVol = -1f; }
        }

        public bool CreateProfileFromCurrent(IWin32Window owner)
        {
            RefreshDevices(true);
            var o = OutDevices.Find(x => x.IsDefault);
            string suggestion = "프로필 " + (S.Profiles.Count + 1);
            if (o != null)
            {
                if (o.Kind == 1) suggestion = "헤드셋";
                else if (o.Kind == 2) suggestion = "이어폰";
                else suggestion = "스피커";
                if (S.Profiles.Any(x => x.Name == suggestion)) suggestion += " " + (S.Profiles.Count + 1);
            }
            var dlg = new ProfileDialog("새 프로필", suggestion, true,
                "지금 사용 중인 장치" + (o != null ? " (" + o.Name + ")" : "") + "를 저장합니다.");
            if (dlg.ShowDialog(owner) != DialogResult.OK) return false;
            var p = new Profile();
            p.Name = dlg.ProfileName;
            CaptureCurrent(p, dlg.WithMic, dlg.WithVolume);
            S.Profiles.Add(p);
            S.Save();
            RefreshUI();
            RebuildTrayMenu();
            RegisterHotkeys();
            Notify("프로필 저장", p.Name, 4, "우클릭하면 단축키를 지정하거나 수정할 수 있어요");
            return true;
        }

        void ShowProfileMenu(Profile p, Control anchor)
        {
            var m = Menus.Create();
            m.Items.Add(Menus.Item("적용", delegate { ApplyProfile(p); }));
            m.Items.Add(Menus.Item("이름 바꾸기", delegate
            {
                var dlg = new ProfileDialog("이름 바꾸기", p.Name, false, null);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                p.Name = dlg.ProfileName;
                S.Save(); RefreshUI(); RebuildTrayMenu();
            }));
            m.Items.Add(Menus.Item("지금 설정으로 덮어쓰기", delegate
            {
                CaptureCurrent(p, p.MicId != null, p.OutVol >= 0 || p.MicVol >= 0);
                S.Save(); RefreshUI(); RebuildTrayMenu();
                Notify("프로필 업데이트", p.Name, 4, null);
            }));
            m.Items.Add(Menus.Item("단축키 · 세부 설정…", delegate { OpenPanel(WorkspaceForm.PageProfiles); }));
            m.Items.Add(new ToolStripSeparator());
            var del = Menus.Item("삭제", delegate
            {
                S.Profiles.Remove(p);
                S.Save(); RefreshUI(); RebuildTrayMenu(); RegisterHotkeys();
            });
            del.ForeColor = UI.Danger;
            m.Items.Add(del);
            m.Show(anchor, new Point(0, anchor.Height + 4));
        }

        // ───────────── 앱별 출력 ─────────────
        public void SetRoute(string exePath, string deviceId, string deviceName)
        {
            if (string.IsNullOrEmpty(exePath)) return;
            var rule = S.RouteFor(exePath);
            if (deviceId == null)
            {
                if (rule != null) S.Routes.Remove(rule);
            }
            else
            {
                if (rule == null) { rule = new AppRoute(); rule.ExePath = exePath; S.Routes.Add(rule); }
                rule.DeviceId = deviceId;
                rule.DeviceName = deviceName;
            }
            S.Save();
            AppRouting.Forget();
            int n = AppRouting.ApplyToExe(exePath, deviceId);
            string app = SessionManager.FriendlyName(exePath);
            if (n == 0 && deviceId != null)
                Notify("앱별 출력 저장", app, 5, "앱을 실행하면 " + deviceName + "(으)로 나옵니다");
            else
                Notify("앱별 출력 변경", app, 5, deviceId == null ? "기본 장치를 따릅니다" : deviceName + "(으)로 출력");
        }

        // ───────────── 단축키 ─────────────
        public void SuspendHotkeys()
        {
            hotkeysSuspended = true;
            UnregisterAll();
        }

        public void ResumeHotkeys()
        {
            hotkeysSuspended = false;
            RegisterHotkeys();
        }

        void UnregisterAll()
        {
            if (!IsHandleCreated) return;
            foreach (var id in hotkeyActions.Keys) { try { UnregisterHotKey(Handle, id); } catch { } }
            hotkeyActions.Clear();
            hotkeyKeys.Clear();
        }

        // 등록에 실패한(다른 프로그램이 쓰는) 단축키 이름 목록을 돌려줌
        public List<string> RegisterHotkeys()
        {
            var failed = new List<string>();
            if (!IsHandleCreated || hotkeysSuspended) return failed;
            UnregisterAll();

            if (S.DirectMods != 0)
                for (int i = 0; i < 8; i++)
                {
                    int idx = i;
                    Bind(1 + i, new Hotkey(S.DirectMods, 0x31 + i), delegate
                    {
                        RefreshDevices(true);
                        if (idx < OutDevices.Count) SwitchTo(OutDevices[idx]);
                    }, null, failed);
                }

            int n = 0;
            foreach (var a in Actions.All)
            {
                string act = a;
                Bind(100 + n++, S.Key(a), delegate { RunAction(act); }, Actions.Label(a), failed);
            }

            for (int i = 0; i < S.Profiles.Count; i++)
            {
                var p = S.Profiles[i];
                Bind(200 + i, p.Key, delegate { ApplyProfile(p); }, "프로필 " + p.Name, failed);
            }
            return failed;
        }

        void Bind(int id, Hotkey k, Action act, string label, List<string> failed)
        {
            if (k == null || k.IsEmpty) return;
            bool ok = false;
            try { ok = RegisterHotKey(Handle, id, k.Mods | 0x4000 /*MOD_NOREPEAT*/, k.Vk); } catch { }
            if (ok) { hotkeyActions[id] = act; hotkeyKeys[id] = k.Clone(); }
            else if (label != null) failed.Add(label + " (" + k + ")");
        }

        void RunAction(string a)
        {
            switch (a)
            {
                case Actions.CycleOut: CycleDevice(false); break;
                case Actions.CycleMic: CycleDevice(true); break;
                case Actions.Mixer: OpenPanel(WorkspaceForm.PageDevices); break;
                case Actions.MuteOut: ToggleMute(false); break;
                case Actions.MuteMic: ToggleMute(true); break;
                case Actions.ShowWindow: TogglePanel(); break;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            var failed = RegisterHotkeys();
            if (failed.Count > 0)
                BeginInvoke((Action)delegate
                {
                    Notify("일부 단축키를 쓸 수 없어요", "다른 프로그램이 사용 중", 0, string.Join(", ", failed.ToArray()));
                });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnregisterAll();
            tray.Visible = false;
            base.OnFormClosed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                Action act;
                if (hotkeyActions.TryGetValue(m.WParam.ToInt32(), out act))
                {
                    try { act(); } catch { }
                }
            }
            base.WndProc(ref m);
        }
    }
}
