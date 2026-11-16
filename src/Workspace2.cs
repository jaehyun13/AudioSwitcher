// 워크스페이스: 자동화 탭 · 마이크 표시 · 장치 관리 · 입력 레벨
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NeonAudioSwitcher
{
    // 마이크 입력 레벨 막대
    class LevelBar : FlatControl
    {
        float level;
        public void SetLevel(float v)
        {
            // 올라갈 땐 바로, 내려갈 땐 천천히
            level = v > level ? v : level * 0.82f + v * 0.18f;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new Rectangle(0, 3, Width - 1, 4);
            UI.FillRound(g, r, 4, UI.Track);
            // 소리 크기는 로그 스케일이 체감과 맞음
            float shown = level <= 0.0005f ? 0f : (float)Math.Max(0, Math.Min(1, 1 + Math.Log10(level) / 2.5));
            int w = (int)(r.Width * shown);
            if (w > 2) UI.FillRound(g, new Rectangle(r.X, r.Y, w, r.Height), 4, shown > 0.9f ? UI.Warn : UI.Accent);
        }
    }

    partial class WorkspaceForm
    {
        LevelBar micLevel;
        MicMeter meter;
        Timer meterTimer;

        // 장치 탭이 보일 때만 마이크를 열어 둔다 (작업 표시줄에 마이크 사용 표시가 뜨기 때문)
        void StartMeter(bool on)
        {
            var mic = shell.MicDevices.Find(x => x.IsDefault);
            if (!on || mic == null || micLevel == null)
            {
                if (meter != null) { meter.Dispose(); meter = null; }
                if (meterTimer != null) meterTimer.Stop();
                return;
            }
            if (meter == null || meter.DeviceId != mic.Id)
            {
                if (meter != null) meter.Dispose();
                meter = MicMeter.Open(mic.Id);
            }
            if (meterTimer == null)
            {
                meterTimer = new Timer();
                meterTimer.Interval = 50;
                meterTimer.Tick += delegate
                {
                    if (meter == null || micLevel == null || micLevel.IsDisposed) return;
                    micLevel.SetLevel(meter.Peak());
                };
            }
            meterTimer.Start();
        }

        // 장치 카드 우클릭
        void DeviceMenu(AudioDevice d, Control anchor, Point at)
        {
            var info = S.Dev(d.Id);
            var m = Menus.Create();
            m.Items.Add(Menus.Header(info.Name ?? d.Name));
            m.Items.Add(Menus.Item("이름 바꾸기…", delegate { RenameDevice(d.Id); }));
            m.Items.Add(Menus.Item("목록에서 숨기기", delegate
            {
                info.Hidden = true;
                S.Save();
                shell.RefreshDevices(true);
            }));
            m.Items.Add(Menus.Item("장치 관리 · 최대 볼륨…", delegate { ShowPage(PageSettings); ScrollTo("장치 관리"); }));
            m.Show(anchor, at);
        }

        void RenameDevice(string id)
        {
            var info = S.Dev(id);
            var dlg = new ProfileDialog("장치 이름 바꾸기", info.Alias ?? info.Name ?? "", false,
                "원래 이름: " + (info.Name ?? "") + "\n비워 두고 저장하면 원래 이름으로 돌아가요.", true);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            string t = dlg.ProfileName;
            info.Alias = (t.Length == 0 || t == info.Name) ? null : t;
            S.Save();
            shell.RefreshDevices(true);
        }

        void ScrollTo(string sectionTitle)
        {
            foreach (Control c in content.Controls)
                if (c is SectionLabel && c.Text == sectionTitle)
                {
                    content.ScrollControlIntoView(c);
                    content.AutoScrollPosition = new Point(0, c.Top - content.AutoScrollPosition.Y - 6);
                    return;
                }
        }

        // ───────────── 자동화 탭 ─────────────
        void BuildAuto()
        {
            // 앱 실행 시 프로필
            Section("앱을 켜면 프로필 자동 적용", S.Triggers.Count > 0 ? S.Triggers.Count + "개" : null);
            Note("게임을 켜면 '게임' 프로필로 바뀌는 식이에요. 앱이 실행 중인지 2초마다 확인해요.");
            if (S.Profiles.Count == 0)
            {
                var c = AddCard(60);
                c.Title = "먼저 프로필을 만들어 주세요";
                c.Subtitle = "독의 프로필 버튼 → 지금 설정을 프로필로 저장";
            }
            else
            {
                foreach (var t0 in S.Triggers.ToList())
                {
                    var t = t0;
                    var card = AddCard(60);
                    card.IconImage = SessionManager.GetIcon(t.ExePath);
                    if (card.IconImage == null) card.IconKind = 5;
                    card.Title = SessionManager.FriendlyName(t.ExePath);
                    card.Subtitle = Path.GetFileName(t.ExePath);
                    card.Tag = 250;

                    var dd = new Dropdown();
                    dd.BackColor = UI.Panel;
                    foreach (var p in S.Profiles) dd.Add(p.Name, p.Name);
                    dd.SelectValue(t.ProfileName);
                    if (dd.SelectedValue == null) dd.SelectText((t.ProfileName ?? "") + " (없음)");
                    dd.Bounds = new Rectangle(card.Width - 16 - 32 - 8 - 190, 14, 190, 32);
                    dd.SelectedChanged += delegate { t.ProfileName = dd.SelectedValue as string; S.Save(); };
                    card.Controls.Add(dd);

                    var rm = new TitleButton(0);
                    rm.BackColor = UI.Panel;
                    rm.Location = new Point(card.Width - 16 - 32, 14);
                    rm.Click += delegate { S.Triggers.Remove(t); S.Save(); OnDataChanged(); };
                    card.Controls.Add(rm);
                }
                var add = new NeonButton();
                add.Text = "+  앱 추가";
                add.Bounds = new Rectangle(0, y, 150, 36);
                add.Click += delegate { ShowTriggerAppMenu(add); };
                Add(add);
                y += 46;

                var rc = AddCard(64);
                var tr = new NeonCheck();
                tr.AutoWidth = false;
                tr.BackColor = UI.Panel;
                tr.Text = "앱을 끄면 원래 장치로 되돌리기";
                tr.Description = "켜기 전에 쓰던 출력 · 마이크 · 볼륨으로 돌아가요";
                tr.Bounds = new Rectangle(16, 12, rc.Width - 32, 42);
                tr.SetSilently(S.TriggerRestore);
                tr.CheckedChanged += delegate { S.TriggerRestore = tr.Checked; S.Save(); };
                rc.Controls.Add(tr);
            }

            // 장치 자동 전환
            y += 14;
            Section("장치 연결 시 자동 전환", null);
            var ac = AddCard(64);
            var ta = new NeonCheck();
            ta.AutoWidth = false;
            ta.BackColor = UI.Panel;
            ta.Text = "연결 · 해제되면 우선순위대로 바꾸기";
            ta.Description = "버즈를 연결하면 이어폰으로, 빼면 다음 순위 장치로 넘어가요";
            ta.Bounds = new Rectangle(16, 12, ac.Width - 32, 42);
            ta.SetSilently(S.AutoSwitch);
            ta.CheckedChanged += delegate { S.AutoSwitch = ta.Checked; S.Save(); Rebuild(); };
            ac.Controls.Add(ta);

            if (S.AutoSwitch)
            {
                shell.EnsurePriority();
                PriorityList("출력 장치 순서", S.OutPriority, shell.RawOut, false);
                PriorityList("마이크 순서", S.MicPriority, shell.RawMic, true);
            }

            // 장치별 볼륨
            y += 14;
            Section("장치별 볼륨", null);
            var vc = AddCard(64);
            var tv = new NeonCheck();
            tv.AutoWidth = false;
            tv.BackColor = UI.Panel;
            tv.Text = "다시 연결하면 마지막 볼륨으로";
            tv.Description = "블루투스 기기가 큰 소리로 연결되는 걸 막아줘요. 최대 볼륨은 설정 → 장치 관리에서";
            tv.Bounds = new Rectangle(16, 12, vc.Width - 32, 42);
            tv.SetSilently(S.RememberVolume);
            tv.CheckedChanged += delegate { S.RememberVolume = tv.Checked; S.Save(); };
            vc.Controls.Add(tv);
        }

        void PriorityList(string title, List<string> order, List<AudioDevice> present, bool mic)
        {
            y += 6;
            var lab = new SectionLabel(title);
            lab.RightText = "위에 있을수록 먼저";
            lab.Bounds = new Rectangle(2, y, CW - 4, 22);
            Add(lab);
            y += 28;
            for (int i = 0; i < order.Count; i++)
            {
                string id = order[i];
                int idx = i;
                var dev = present.Find(d => d.Id == id);
                var card = AddCard(52);
                card.IconKind = mic ? 3 : (dev != null ? dev.Kind : 0);
                card.Title = (i + 1) + ".  " + shell.DisplayName(id, dev != null ? dev.Name : "알 수 없는 장치");
                card.Subtitle = dev != null ? (dev.IsDefault ? "연결됨 · 사용 중" : "연결됨") : "연결 안 됨";
                card.Tag = 120;
                int bx = card.Width - 12;
                if (dev == null)
                {
                    var rm = new TitleButton(0);
                    rm.BackColor = UI.Panel;
                    bx -= 32;
                    rm.Location = new Point(bx, 10);
                    rm.Click += delegate { order.RemoveAt(idx); S.Save(); Rebuild(); };
                    card.Controls.Add(rm);
                    bx -= 4;
                }
                bx = MoveButton(card, bx, idx < order.Count - 1, false, delegate { Swap(order, idx, idx + 1); });
                MoveButton(card, bx, idx > 0, true, delegate { Swap(order, idx, idx - 1); });
            }
        }

        int MoveButton(Card card, int right, bool enabled, bool up, Action act)
        {
            var b = new NeonButton();
            b.Text = up ? "▲" : "▼";
            b.Font = UI.HintFont;
            b.BackColor = UI.Panel;
            b.Enabled = enabled;
            b.Bounds = new Rectangle(right - 36, 9, 36, 32);
            if (enabled) b.Click += delegate { act(); S.Save(); Rebuild(); };
            else b.Visible = false;
            card.Controls.Add(b);
            return right - 40;
        }

        static void Swap(List<string> l, int a, int b)
        {
            var t = l[a]; l[a] = l[b]; l[b] = t;
        }

        void ShowTriggerAppMenu(Control anchor)
        {
            var m = Menus.Create();
            m.Items.Add(Menus.Header("지금 실행 중인 프로그램"));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in S.Triggers) seen.Add(t.ExePath);
            string self = Application.ExecutablePath;
            int n = 0;
            Process[] procs;
            try { procs = Process.GetProcesses(); } catch { procs = new Process[0]; }
            var items = new List<KeyValuePair<string, string>>();
            foreach (var p in procs)
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero || string.IsNullOrEmpty(p.MainWindowTitle)) continue;
                    string path = SessionManager.GetProcessPath(p.Id);
                    if (string.IsNullOrEmpty(path) || seen.Contains(path)) continue;
                    if (string.Equals(path, self, StringComparison.OrdinalIgnoreCase)) continue;
                    if (path.IndexOf(@"\Windows\", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    seen.Add(path);
                    items.Add(new KeyValuePair<string, string>(SessionManager.FriendlyName(path), path));
                }
                catch { }
                finally { p.Dispose(); }
            }
            foreach (var kv in items.OrderBy(k => k.Key))
            {
                string path = kv.Value;
                var it = Menus.Item(kv.Key, delegate { AddTrigger(path); });
                var ic = SessionManager.GetIcon(path);
                if (ic != null) { it.Image = ic; m.ShowImageMargin = true; }
                m.Items.Add(it);
                n++;
            }
            if (n == 0) m.Items.Add(Menus.Header("    (창이 있는 프로그램이 없어요)"));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Menus.Item("실행 파일 직접 찾기…", delegate
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "프로그램 (*.exe)|*.exe";
                    dlg.Title = "실행되면 프로필을 적용할 프로그램";
                    if (dlg.ShowDialog(this) == DialogResult.OK) AddTrigger(dlg.FileName);
                }
            }));
            m.Show(anchor, new Point(0, anchor.Height + 4));
        }

        void AddTrigger(string path)
        {
            if (S.Profiles.Count == 0) return;
            var t = new AppTrigger();
            t.ExePath = path;
            var game = S.ProfileNamed("게임");
            t.ProfileName = (game ?? S.Profiles[0]).Name;
            S.Triggers.Add(t);
            S.Save();
            OnDataChanged();
        }

        // ───────────── 설정 탭: 마이크 꺼짐 표시 ─────────────
        void BuildOverlay()
        {
            y += 10;
            Section("마이크 꺼짐 표시", null);
            var c1 = AddCard(64);
            var t = new NeonCheck();
            t.AutoWidth = false;
            t.BackColor = UI.Panel;
            t.Text = "마이크를 끄면 화면 구석에 표시";
            t.Description = "게임 중에도 마이크가 꺼져 있는지 바로 보여요 (클릭은 통과돼요)";
            t.Bounds = new Rectangle(16, 12, c1.Width - 32, 42);
            t.SetSilently(S.MicOverlay);
            t.CheckedChanged += delegate { S.MicOverlay = t.Checked; S.Save(); shell.RefreshMicOverlay(); };
            c1.Controls.Add(t);

            var c2 = AddCard(56);
            c2.Title = "표시 위치";
            c2.Tag = 200;
            var pos = new Dropdown();
            pos.BackColor = UI.Panel;
            for (int i = 0; i < Settings.CornerNames.Length; i++) pos.Add(Settings.CornerNames[i], i);
            pos.SelectValue(S.MicOverlayPos);
            pos.Bounds = new Rectangle(c2.Width - 16 - 170, 12, 170, 32);
            pos.SelectedChanged += delegate { S.MicOverlayPos = (int)pos.SelectedValue; S.Save(); shell.RefreshMicOverlay(); };
            c2.Controls.Add(pos);
        }

        // ───────────── 설정 탭: 장치 관리 ─────────────
        void BuildDeviceManager()
        {
            y += 10;
            Section("장치 관리", null);
            Note("장치 이름을 짧게 바꾸거나 안 쓰는 장치를 목록에서 숨겨요. 출력 장치는 최대 볼륨을 정할 수 있어요.\n장치 카드를 우클릭해도 돼요.");
            foreach (var d in shell.RawOut) DeviceManageCard(d, true);
            foreach (var d in shell.RawMic) DeviceManageCard(d, false);
        }

        void DeviceManageCard(AudioDevice d, bool output)
        {
            var info = S.Dev(d.Id);
            int h = 16 + 32 + 10 + 30 + (output ? 38 : 0) + 12;
            var card = AddCard(h);
            int top = 16;

            var name = new DarkTextBox();
            name.BackColor = UI.Panel;
            name.Text = info.Alias ?? info.Name ?? d.Name;
            name.Bounds = new Rectangle(16, top, card.Width - 32 - 140, 32);
            Action commit = delegate
            {
                string t = name.Text.Trim();
                string alias = (t.Length == 0 || t == info.Name) ? null : t;
                if (alias == info.Alias) return;
                info.Alias = alias;
                S.Save();
                shell.RefreshDevices(true);
            };
            name.Box.LostFocus += delegate { commit(); };
            name.Box.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; commit(); } };
            card.Controls.Add(name);

            var kind = new NoteLabel((output ? "출력" : "마이크") + "\n" + (info.Name ?? ""));
            kind.BackColor = UI.Panel;
            kind.Bounds = new Rectangle(card.Width - 16 - 128, top - 2, 128, 36);
            card.Controls.Add(kind);
            top += 42;

            var hide = new NeonCheck();
            hide.AutoWidth = false;
            hide.BackColor = UI.Panel;
            hide.Text = "목록에서 숨기기";
            hide.Bounds = new Rectangle(16, top, 220, 26);
            hide.SetSilently(info.Hidden);
            hide.CheckedChanged += delegate { info.Hidden = hide.Checked; S.Save(); shell.RefreshDevices(true); };
            card.Controls.Add(hide);
            top += 32;

            if (output)
            {
                var lim = new NeonCheck();
                lim.AutoWidth = false;
                lim.BackColor = UI.Panel;
                lim.Text = "최대 볼륨 제한";
                lim.Bounds = new Rectangle(16, top + 4, 150, 26);
                lim.SetSilently(info.MaxVol >= 0);
                var sl = new NeonSlider();
                sl.ShowIcon = false;
                sl.BackColor = UI.Panel;
                sl.Bounds = new Rectangle(170, top, card.Width - 186, 32);
                sl.SetValue(info.MaxVol >= 0 ? info.MaxVol : 0.6f);
                sl.Visible = info.MaxVol >= 0;
                lim.CheckedChanged += delegate
                {
                    sl.Visible = lim.Checked;
                    info.MaxVol = lim.Checked ? Math.Max(0.05f, sl.Value) : -1f;
                    S.Save();
                };
                sl.ValueChanged += delegate { info.MaxVol = Math.Max(0.05f, sl.Value); };
                sl.MouseUp += delegate { S.Save(); };
                card.Controls.Add(lim);
                card.Controls.Add(sl);
            }
        }
    }
}
