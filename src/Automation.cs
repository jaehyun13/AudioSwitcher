// 자동화: 장치 연결 시 자동 전환 · 장치별 볼륨 기억 · 최대 볼륨 제한 · 앱 실행 시 프로필 · 마이크 꺼짐 표시
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NeonAudioSwitcher
{
    partial class Shell
    {
        // 숨김 처리 전 전체 장치 (설정의 장치 관리에서 씀)
        public List<AudioDevice> RawOut = new List<AudioDevice>();
        public List<AudioDevice> RawMic = new List<AudioDevice>();

        HashSet<string> prevOut, prevMic;
        bool volDirty;
        string profileTitle;            // 다음 프로필 알림 제목 (자동 적용일 때)
        Timer limitTimer;
        MicOverlayForm overlay;

        void StartAutomation()
        {
            // 최대 볼륨은 빨리 잡아야 귀가 안 놀라므로 짧은 간격으로 확인
            limitTimer = new Timer();
            limitTimer.Interval = 300;
            limitTimer.Tick += delegate { EnforceLimit(); };
            limitTimer.Start();
            CheckTriggers();
        }

        // ───────────── 이름 바꾸기 · 숨기기 ─────────────
        List<AudioDevice> Present(List<AudioDevice> raw)
        {
            var list = new List<AudioDevice>();
            foreach (var d in raw)
            {
                var info = S.Dev(d.Id);
                string rawName = d.RawName ?? d.Name;
                if (info.Name != rawName) { info.Name = rawName; volDirty = true; }
                if (!string.IsNullOrEmpty(info.Alias)) d.Name = info.Alias;
                if (info.Hidden && !d.IsDefault) continue;   // 사용 중인 장치는 숨겨도 보여줌
                list.Add(d);
            }
            return list;
        }

        public string DisplayName(string id, string fallback)
        {
            var info = S.DevIfAny(id);
            if (info == null) return fallback;
            if (!string.IsNullOrEmpty(info.Alias)) return info.Alias;
            return info.Name ?? fallback;
        }

        // ───────────── 연결 / 해제 감지 ─────────────
        bool HandleConnectionChanges(List<AudioDevice> outs, List<AudioDevice> mics)
        {
            var curOut = new HashSet<string>(outs.Select(d => d.Id));
            var curMic = new HashSet<string>(mics.Select(d => d.Id));
            bool first = prevOut == null;
            var addedOut = first ? new List<string>() : curOut.Where(id => !prevOut.Contains(id)).ToList();
            var addedMic = first ? new List<string>() : curMic.Where(id => !prevMic.Contains(id)).ToList();
            bool changedOut = !first && (addedOut.Count > 0 || prevOut.Any(id => !curOut.Contains(id)));
            bool changedMic = !first && (addedMic.Count > 0 || prevMic.Any(id => !curMic.Contains(id)));
            prevOut = curOut;
            prevMic = curMic;
            if (first) return false;

            // 다시 연결된 장치는 마지막으로 쓰던 볼륨으로 (블루투스 기기가 100%로 붙는 경우 등)
            if (S.RememberVolume)
                foreach (var id in addedOut.Concat(addedMic))
                {
                    var info = S.DevIfAny(id);
                    if (info == null || info.LastVol < 0) continue;
                    float v = info.LastVol;
                    if (info.MaxVol >= 0) v = Math.Min(v, info.MaxVol);
                    AudioManager.SetVolume(id, v);
                }

            if (!S.AutoSwitch) return false;
            bool switched = false;
            if (changedOut) switched |= SwitchToPreferred(outs, S.OutPriority, false);
            if (changedMic) switched |= SwitchToPreferred(mics, S.MicPriority, true);
            return switched;
        }

        bool SwitchToPreferred(List<AudioDevice> devs, List<string> order, bool mic)
        {
            AudioDevice best = null;
            foreach (var id in order)
            {
                best = devs.Find(d => d.Id == id);
                if (best != null) break;
            }
            if (best == null || best.IsDefault) return false;
            if (!AudioManager.SetDefault(best.Id, S.AlsoComm)) return false;
            Notify(mic ? "마이크 자동 전환" : "출력 장치 자동 전환", DisplayName(best.Id, best.Name), mic ? 3 : best.Kind, "연결 상태가 바뀌어 우선순위대로 바꿨어요");
            return true;
        }

        // 설정 화면용: 우선순위 목록에 지금 연결된 장치가 빠져 있으면 뒤에 붙임
        public void EnsurePriority()
        {
            bool changed = false;
            foreach (var d in RawOut) if (!S.OutPriority.Contains(d.Id)) { S.OutPriority.Add(d.Id); changed = true; }
            foreach (var d in RawMic) if (!S.MicPriority.Contains(d.Id)) { S.MicPriority.Add(d.Id); changed = true; }
            if (changed) S.Save();
        }

        // ───────────── 장치별 볼륨 기억 · 최대 볼륨 ─────────────
        void RememberVolumes(AudioDevice o, AudioDevice m)
        {
            if (!S.RememberVolume) return;
            foreach (var d in new[] { o, m })
            {
                if (d == null) continue;
                float v = AudioManager.GetVolume(d.Id);
                if (v < 0) continue;
                var info = S.Dev(d.Id);
                if (Math.Abs(info.LastVol - v) > 0.004f) { info.LastVol = v; volDirty = true; }
            }
        }

        void EnforceLimit()
        {
            var o = OutDevices.Find(x => x.IsDefault);
            if (o == null) return;
            var info = S.DevIfAny(o.Id);
            if (info == null || info.MaxVol < 0) return;
            float v = AudioManager.GetVolume(o.Id);
            if (v > info.MaxVol + 0.005f)
            {
                AudioManager.SetVolume(o.Id, info.MaxVol);
                UpdateVolumes();
            }
        }

        // ───────────── 앱 실행 시 프로필 ─────────────
        class Snapshot { public string Out, Mic; public float OutVol = -1, MicVol = -1; }
        Dictionary<string, bool> trigRunning = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        List<string> activeTriggers = new List<string>();
        Snapshot restorePoint;

        Snapshot TakeSnapshot()
        {
            var s = new Snapshot();
            var o = OutDevices.Find(x => x.IsDefault);
            var m = MicDevices.Find(x => x.IsDefault);
            if (o != null) { s.Out = o.Id; s.OutVol = AudioManager.GetVolume(o.Id); }
            if (m != null) { s.Mic = m.Id; s.MicVol = AudioManager.GetVolume(m.Id); }
            return s;
        }

        void Restore(Snapshot s)
        {
            if (s.Out != null && RawOut.Any(d => d.Id == s.Out))
            {
                AudioManager.SetDefault(s.Out, S.AlsoComm);
                if (s.OutVol >= 0) AudioManager.SetVolume(s.Out, s.OutVol);
            }
            if (s.Mic != null && RawMic.Any(d => d.Id == s.Mic))
            {
                AudioManager.SetDefault(s.Mic, S.AlsoComm);
                if (s.MicVol >= 0) AudioManager.SetVolume(s.Mic, s.MicVol);
            }
            RefreshDevices(true);
        }

        void CheckTriggers()
        {
            if (S.Triggers.Count == 0) { trigRunning.Clear(); activeTriggers.Clear(); return; }
            foreach (var t in S.Triggers.ToList())
            {
                bool running = AppRouting.PidsOf(t.ExePath).Count > 0;
                bool was;
                trigRunning.TryGetValue(t.ExePath, out was);
                trigRunning[t.ExePath] = running;
                string app = SessionManager.FriendlyName(t.ExePath);

                if (running && !was)
                {
                    var p = S.ProfileNamed(t.ProfileName);
                    if (p == null) continue;
                    if (activeTriggers.Count == 0) restorePoint = TakeSnapshot();
                    if (!activeTriggers.Contains(t.ExePath)) activeTriggers.Add(t.ExePath);
                    profileTitle = app + " 실행 → 자동 적용";
                    ApplyProfile(p);
                }
                else if (!running && was && activeTriggers.Remove(t.ExePath))
                {
                    if (activeTriggers.Count == 0 && S.TriggerRestore && restorePoint != null)
                    {
                        var rp = restorePoint;
                        restorePoint = null;
                        Restore(rp);
                        var o = OutDevices.Find(x => x.IsDefault);
                        Notify(app + " 종료 → 원래대로", o != null ? o.Name : "이전 장치", o != null ? o.Kind : 0, null);
                    }
                }
            }
            // 목록에서 지운 앱 정리
            var keys = new HashSet<string>(S.Triggers.Select(t => t.ExePath), StringComparer.OrdinalIgnoreCase);
            foreach (var k in trigRunning.Keys.ToList()) if (!keys.Contains(k)) trigRunning.Remove(k);
            activeTriggers.RemoveAll(k => !keys.Contains(k));
        }

        // ───────────── 마이크 꺼짐 표시 ─────────────
        public void UpdateMicOverlay(bool muted)
        {
            bool want = S.MicOverlay && muted;
            if (!want)
            {
                if (overlay != null && overlay.Visible) overlay.Hide();
                return;
            }
            if (overlay == null || overlay.IsDisposed) overlay = new MicOverlayForm();
            overlay.Place(S.MicOverlayPos);
            if (!overlay.Visible) overlay.Show();
            overlay.Invalidate();
        }

        public void RefreshMicOverlay()
        {
            if (overlay != null && !overlay.IsDisposed) { overlay.Close(); overlay = null; }
            var m = MicDevices.Find(x => x.IsDefault);
            UpdateMicOverlay(m != null && AudioManager.GetMute(m.Id));
        }
    }

    // 화면 구석의 "마이크 꺼짐" 표시 (클릭이 통과하고 포커스를 뺏지 않음)
    class MicOverlayForm : Form
    {
        public MicOverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = UI.Panel;
            Size = new Size(138, 36);
            Opacity = 0.92;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // NOACTIVATE | TOOLWINDOW | LAYERED | TRANSPARENT(클릭 통과) | TOPMOST
                cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00080000 | 0x00000020 | 0x00000008;
                return cp;
            }
        }

        public void Place(int corner)
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            const int m = 16;
            int x = (corner == 2 || corner == 3) ? wa.Left + m : wa.Right - Width - m;
            int y = (corner == 1 || corner == 3) ? wa.Top + m : wa.Bottom - Height - m;
            Location = new Point(x, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            g.Clear(UI.Panel);
            using (var p = new Pen(UI.Danger, 1.5f)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            UI.DrawKindIcon(g, new Rectangle(8, 6, 26, 24), 3, UI.Danger);
            using (var p = new Pen(UI.Danger, 2f))
            {
                p.StartCap = System.Drawing.Drawing2D.LineCap.Round; p.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawLine(p, 12, 28, 30, 8);
            }
            using (var b = new SolidBrush(UI.Text))
                g.DrawString("마이크 꺼짐", UI.BodyBold, b, 38, (Height - UI.BodyBold.Height) / 2f);
        }
    }
}
