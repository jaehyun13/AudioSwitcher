// 메인 창: 프로필 · 출력/마이크 2단 · 하단 옵션 · 트레이 · 전역 단축키
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NeonAudioSwitcher
{
    class MainForm : ChromeForm
    {
        const int WinW = 760;
        const int Pad = 18;
        const int Gap = 24;
        const int ColW = (WinW - Pad * 2 - Gap) / 2;
        const int LeftX = Pad;
        const int RightX = Pad + ColW + Gap;
        const int RowH = 60;

        const int WM_HOTKEY = 0x0312;
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, int mod, int vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

        Settings S { get { return App.S; } }

        public List<AudioDevice> OutDevices = new List<AudioDevice>();
        public List<AudioDevice> MicDevices = new List<AudioDevice>();

        List<Control> dyn = new List<Control>();
        NeonCheck chkComm, chkAuto, chkToast;
        NeonSlider volOut, volMic;
        NeonButton btnMixer;
        NoteLabel hint;
        FlatControl titleBar;
        NotifyIcon tray;
        ContextMenuStrip trayMenu;
        Timer pollTimer;
        string lastSig = "";
        bool trayTipShown;
        int colTop = 120, contentBottom = 400;
        bool hotkeysSuspended;
        int pollCount;

        Dictionary<int, Action> hotkeyActions = new Dictionary<int, Action>();

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "NeonAudioSwitcher";

        public MainForm()
        {
            Text = "Audio Switcher";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(WinW, 400);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            ToastForm.Position = S.ToastPos;

            BuildTitleBar();
            BuildFooter();
            BuildTray();

            pollTimer = new Timer();
            pollTimer.Interval = 2000;
            pollTimer.Tick += delegate
            {
                RefreshDevices(false);
                if (++pollCount % 2 == 0) AppRouting.Maintain(S.Routes);
            };
            pollTimer.Start();

            RefreshDevices(true);
            if (S.AlsoComm) SyncCommToDefaults();
            AppRouting.Maintain(S.Routes);
        }

        // ───────────── 제목줄 ─────────────
        void BuildTitleBar()
        {
            titleBar = new FlatControl();
            titleBar.Bounds = new Rectangle(1, 1, WinW - 2, 61);
            titleBar.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics;
                UI.Smooth(g);
                var tile = new Rectangle(Pad, 15, 32, 32);
                UI.FillRound(g, tile, 12, UI.Accent);
                UI.DrawKindIcon(g, tile, 0, Color.White);
                using (var b = new SolidBrush(UI.Text)) g.DrawString("Audio Switcher", UI.TitleFont, b, 60, 13);
                using (var b = new SolidBrush(UI.Faint)) g.DrawString("출력 · 마이크 빠른 전환", UI.HintFont, b, 61, 34);
            };
            titleBar.MouseDown += BeginDrag;
            Controls.Add(titleBar);

            var btnSet = new TitleButton(2);
            btnSet.Location = new Point(WinW - 122, 15);
            btnSet.Click += delegate { OpenSettings(0); };
            titleBar.Controls.Add(btnSet);
            var tip = new ToolTip();
            tip.SetToolTip(btnSet, "설정");

            var btnMin = new TitleButton(1);
            btnMin.Location = new Point(WinW - 86, 15);
            btnMin.Click += delegate { WindowState = FormWindowState.Minimized; };
            titleBar.Controls.Add(btnMin);

            var btnClose = new TitleButton(0);
            btnClose.Location = new Point(WinW - 50, 15);
            btnClose.Click += delegate { HideToTray(); };
            titleBar.Controls.Add(btnClose);
        }

        // ───────────── 하단 ─────────────
        void BuildFooter()
        {
            btnMixer = new NeonButton();
            btnMixer.Text = "앱별 볼륨 · 출력 장치";
            btnMixer.IconKind = 5;
            btnMixer.Height = 40;
            btnMixer.Click += delegate { MixerForm.Toggle(this); };
            Controls.Add(btnMixer);

            chkComm = MakeCheck("통신 장치도 함께 변경");
            chkComm.SetSilently(S.AlsoComm);
            chkComm.CheckedChanged += delegate
            {
                S.AlsoComm = chkComm.Checked;
                S.Save();
                if (chkComm.Checked) SyncCommToDefaults();
            };

            chkToast = MakeCheck("전환 알림 표시");
            chkToast.SetSilently(S.ToastEnabled);
            chkToast.CheckedChanged += delegate
            {
                S.ToastEnabled = chkToast.Checked;
                S.Save();
                if (S.ToastEnabled) ToastForm.Popup("알림 켜짐", "장치를 바꾸면 이렇게 알려드려요", 0, null);
            };

            chkAuto = MakeCheck("Windows 시작 시 자동 실행");
            chkAuto.SetSilently(IsAutoStart());
            chkAuto.CheckedChanged += delegate { SetAutoStart(chkAuto.Checked); };

            hint = new NoteLabel("");
            Controls.Add(hint);
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

        NeonCheck MakeCheck(string t)
        {
            var c = new NeonCheck();
            c.Text = t;
            Controls.Add(c);
            return c;
        }

        // 설정 창에서 바뀐 값을 화면에 반영
        public void OnSettingsChanged()
        {
            chkComm.SetSilently(S.AlsoComm);
            chkToast.SetSilently(S.ToastEnabled);
            chkAuto.SetSilently(IsAutoStart());
            ToastForm.Position = S.ToastPos;
            RebuildRows();
            RebuildTrayMenu();
            MixerForm.Reload();
        }

        // ───────────── 트레이 ─────────────
        void BuildTray()
        {
            trayMenu = Menus.Create();
            tray = new NotifyIcon();
            Icon exeIcon = null;
            try { exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            tray.Icon = exeIcon ?? MakeIcon();
            tray.Text = "Audio Switcher";
            tray.ContextMenuStrip = trayMenu;
            tray.Visible = true;
            tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) ToggleWindow();
            };
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

        void HideToTray()
        {
            Hide();
            if (!trayTipShown)
            {
                trayTipShown = true;
                tray.ShowBalloonTip(1500, "Audio Switcher", "트레이에서 계속 실행 중입니다. 종료는 우클릭 → 종료", ToolTipIcon.Info);
            }
        }

        public void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        void ToggleWindow()
        {
            if (Visible && WindowState != FormWindowState.Minimized) Hide();
            else ShowFromTray();
        }

        void RebuildTrayMenu()
        {
            trayMenu.Items.Clear();
            AddTraySection("출력 장치", OutDevices);
            trayMenu.Items.Add(new ToolStripSeparator());
            AddTraySection("마이크", MicDevices);
            if (S.Profiles.Count > 0)
            {
                trayMenu.Items.Add(new ToolStripSeparator());
                trayMenu.Items.Add(Menus.Header("프로필"));
                foreach (var p in S.Profiles)
                {
                    var prof = p;
                    var it = Menus.Item((IsActive(p) ? "●  " : "     ") + p.Name, delegate { ApplyProfile(prof); });
                    if (IsActive(p)) it.ForeColor = UI.Accent;
                    trayMenu.Items.Add(it);
                }
            }
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(Menus.Item("앱별 볼륨 · 출력 장치", delegate { MixerForm.Toggle(this); }));
            trayMenu.Items.Add(Menus.Item("설정", delegate { OpenSettings(0); }));
            trayMenu.Items.Add(Menus.Item("열기", delegate { ShowFromTray(); }));
            trayMenu.Items.Add(Menus.Item("종료", delegate { tray.Visible = false; Application.Exit(); }));
        }

        void AddTraySection(string title, List<AudioDevice> devs)
        {
            trayMenu.Items.Add(Menus.Header(title));
            foreach (var d in devs)
            {
                var dev = d;
                var item = Menus.Item((d.IsDefault ? "●  " : "     ") + d.Name, delegate { SwitchTo(dev); });
                if (d.IsDefault) item.ForeColor = UI.Accent;
                trayMenu.Items.Add(item);
            }
        }

        // ───────────── 장치 목록 ─────────────
        void RefreshDevices(bool force)
        {
            var outs = AudioManager.GetDevices(EDataFlow.eRender);
            var mics = AudioManager.GetDevices(EDataFlow.eCapture);
            string sig = string.Join("|", outs.Concat(mics).Select(d =>
                d.Id + (d.IsDefault ? "*" : "") + (d.IsDefaultComm ? "+" : "")).ToArray());
            if (force || sig != lastSig)
            {
                bool devicesChanged = DeviceIds(outs) != DeviceIds(OutDevices);
                lastSig = sig;
                OutDevices = outs;
                MicDevices = mics;
                RebuildRows();
                RebuildTrayMenu();
                if (devicesChanged) MixerForm.Reload();
            }
            UpdateVolumes();
        }

        static string DeviceIds(List<AudioDevice> l) { return string.Join("|", l.Select(d => d.Id).ToArray()); }

        void UpdateVolumes()
        {
            UpdateVol(OutDevices, volOut);
            UpdateVol(MicDevices, volMic);
        }

        void UpdateVol(List<AudioDevice> devs, NeonSlider s)
        {
            if (s == null || s.Dragging) return;
            var d = devs.Find(x => x.IsDefault);
            if (d == null) { s.SetState(0f, false); return; }
            s.SetState(AudioManager.GetVolume(d.Id), AudioManager.GetMute(d.Id));
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

        // ───────────── 화면 구성 ─────────────
        void AddDyn(Control c) { Controls.Add(c); dyn.Add(c); }

        int BuildProfiles(int y)
        {
            var head = new SectionLabel("프로필");
            head.RightText = S.Profiles.Count > 0 ? "클릭 = 적용  ·  우클릭 = 편집" : "";
            head.Bounds = new Rectangle(LeftX + 2, y, WinW - Pad * 2 - 4, 22);
            AddDyn(head);
            y += 28;

            int x = LeftX;
            int maxX = WinW - Pad;
            foreach (var p in S.Profiles)
            {
                var prof = p;
                var chip = new Chip();
                chip.Text = p.Name;
                chip.Active = IsActive(p);
                chip.Hint = p.Key.IsEmpty ? null : p.Key.ToString();
                chip.FitWidth();
                if (x + chip.Width > maxX && x > LeftX) { x = LeftX; y += 38; }
                chip.Location = new Point(x, y);
                chip.MouseUp += delegate(object s, MouseEventArgs e)
                {
                    if (e.Button == MouseButtons.Left) ApplyProfile(prof);
                    else if (e.Button == MouseButtons.Right) ShowProfileMenu(prof, chip);
                };
                AddDyn(chip);
                x += chip.Width + 8;
            }

            var add = new Chip();
            add.Dashed = true;
            add.Text = S.Profiles.Count == 0 ? "+  지금 설정을 프로필로 저장" : "+  새 프로필";
            add.FitWidth();
            if (x + add.Width > maxX && x > LeftX) { x = LeftX; y += 38; }
            add.Location = new Point(x, y);
            add.Click += delegate { CreateProfileFromCurrent(this); };
            AddDyn(add);

            if (S.Profiles.Count == 0)
            {
                var note = new NoteLabel("게임용 · 통화용처럼 자주 쓰는 출력/마이크/볼륨 조합을 저장해 두고 한 번에 바꿔요");
                note.Bounds = new Rectangle(add.Right + 12, y + 7, maxX - add.Right - 12, 18);
                if (note.Width > 120) AddDyn(note); else note.Dispose();
            }
            return y + 30;
        }

        int BuildColumn(int x, int y, string title, List<AudioDevice> devs, bool isOutput, out NeonSlider slider)
        {
            var head = new SectionLabel(title);
            head.RightText = devs.Count > 0 ? devs.Count + "개" : "";
            head.Bounds = new Rectangle(x + 2, y, ColW - 4, 22);
            AddDyn(head);
            y += 30;

            for (int i = 0; i < devs.Count; i++)
            {
                var row = new DeviceRow();
                row.Device = devs[i];
                row.Sub = (isOutput && S.DirectMods != 0 && i < 8) ? Hotkey.ModsText(S.DirectMods) + (i + 1) : "";
                row.Bounds = new Rectangle(x, y, ColW, RowH);
                var dev = devs[i];
                row.Click += delegate { SwitchTo(dev); };
                AddDyn(row);
                y += RowH + 8;
            }

            if (devs.Count == 0)
            {
                var none = new NoteLabel("연결된 장치가 없습니다");
                none.Color = UI.Dim;
                none.TextFont = UI.BodyFont;
                none.Bounds = new Rectangle(x + 4, y + 6, ColW - 8, 22);
                AddDyn(none);
                y += 40;
            }

            y += 4;
            var sl = new NeonSlider();
            sl.IconKind = isOutput ? 0 : 3;
            sl.Bounds = new Rectangle(x + 4, y, ColW - 8, 30);
            var list = devs;
            sl.ValueChanged += delegate
            {
                var d = list.Find(z => z.IsDefault);
                if (d != null) AudioManager.SetVolume(d.Id, sl.Value);
            };
            sl.MuteToggled += delegate
            {
                var d = list.Find(z => z.IsDefault);
                if (d == null) return;
                bool m = !AudioManager.GetMute(d.Id);
                AudioManager.SetMute(d.Id, m);
                sl.SetState(-1f, m);
            };
            AddDyn(sl);
            y += 34;

            slider = sl;
            return y;
        }

        void RebuildRows()
        {
            SuspendLayout();
            foreach (var c in dyn) { Controls.Remove(c); c.Dispose(); }
            dyn.Clear();

            int y = BuildProfiles(72);
            y += 22;
            colTop = y;

            NeonSlider sOut, sMic;
            int leftBottom = BuildColumn(LeftX, y, "재생 장치", OutDevices, true, out sOut);
            int rightBottom = BuildColumn(RightX, y, "마이크", MicDevices, false, out sMic);
            volOut = sOut;
            volMic = sMic;
            contentBottom = Math.Max(leftBottom, rightBottom);

            y = contentBottom + 22;
            btnMixer.Bounds = new Rectangle(LeftX, y, WinW - Pad * 2, 40);
            btnMixer.Hint = S.Key(Actions.Mixer).ToString();
            btnMixer.Invalidate();
            y += 56;

            chkComm.Location = new Point(LeftX + 2, y);
            chkToast.Location = new Point(LeftX + 2 + 250, y);
            chkAuto.Location = new Point(LeftX + 2 + 470, y);
            y += 38;

            hint.Text = HotkeySummary();
            hint.Bounds = new Rectangle(LeftX + 2, y, WinW - Pad * 2, 18);
            hint.Invalidate();
            ClientSize = new Size(WinW, y + 34);
            titleBar.Width = WinW - 2;
            ResumeLayout();
            ClampToScreen();
            Invalidate();
        }

        string HotkeySummary()
        {
            var parts = new List<string>();
            var a = S.Key(Actions.CycleOut);
            var m = S.Key(Actions.CycleMic);
            if (!a.IsEmpty) parts.Add(a + " 출력 전환");
            if (!m.IsEmpty) parts.Add(m + " 마이크 전환");
            parts.Add("단축키는 설정(⚙)에서 바꿀 수 있어요");
            return string.Join("     ", parts.ToArray());
        }

        void ClampToScreen()
        {
            if (!IsHandleCreated) return;
            var wa = Screen.FromControl(this).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(Left, wa.Right - Width));
            int y2 = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - Height));
            if (x != Left || y2 != Top) Location = new Point(x, y2);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ClampToScreen();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            using (var p = new Pen(UI.Border))
            {
                g.DrawLine(p, Pad, 62, WinW - Pad, 62);
                g.DrawLine(p, Pad, colTop - 12, WinW - Pad, colTop - 12);
                g.DrawLine(p, LeftX + ColW + Gap / 2, colTop + 4, LeftX + ColW + Gap / 2, contentBottom - 4);
            }
        }

        // ───────────── 전환 ─────────────
        void Notify(string title, string text, int kind, string detail)
        {
            if (S.ToastEnabled) ToastForm.Popup(title, text, kind, detail);
        }

        void SwitchTo(AudioDevice d)
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
                var d = OutDevices.Find(x => x.Id == p.OutId);
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
                var d = MicDevices.Find(x => x.Id == p.MicId);
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
            Notify(missing.Count > 0 && done.Count == 0 ? "프로필을 적용하지 못했어요" : "프로필 적용", p.Name, 4, detail);
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
            RebuildRows();
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
                S.Save(); RebuildRows(); RebuildTrayMenu();
            }));
            m.Items.Add(Menus.Item("지금 설정으로 덮어쓰기", delegate
            {
                CaptureCurrent(p, p.MicId != null, p.OutVol >= 0 || p.MicVol >= 0);
                S.Save(); RebuildRows(); RebuildTrayMenu();
                Notify("프로필 업데이트", p.Name, 4, null);
            }));
            m.Items.Add(Menus.Item("단축키 · 세부 설정…", delegate { OpenSettings(1); }));
            m.Items.Add(new ToolStripSeparator());
            var del = Menus.Item("삭제", delegate
            {
                S.Profiles.Remove(p);
                S.Save(); RebuildRows(); RebuildTrayMenu(); RegisterHotkeys();
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

        // ───────────── 설정 창 ─────────────
        public void OpenSettings(int tab)
        {
            if (!Visible) ShowFromTray();
            SettingsForm.Open(this, tab);
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
            if (ok) hotkeyActions[id] = act;
            else if (label != null) failed.Add(label + " (" + k + ")");
        }

        void RunAction(string a)
        {
            switch (a)
            {
                case Actions.CycleOut: CycleDevice(false); break;
                case Actions.CycleMic: CycleDevice(true); break;
                case Actions.Mixer: MixerForm.Toggle(this); break;
                case Actions.MuteOut: ToggleMute(false); break;
                case Actions.MuteMic: ToggleMute(true); break;
                case Actions.ShowWindow: ToggleWindow(); break;
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
