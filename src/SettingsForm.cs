// 설정 창 (단축키 · 프로필 · 앱별 출력 · 일반) + 프로필 이름 입력 창
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NeonAudioSwitcher
{
    // 둥근 카드 (자식 컨트롤은 BackColor = UI.Panel 로)
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
    class NavItem : FlatControl
    {
        public bool Selected;
        public int IconKind;
        public NavItem(string text, int icon) { Text = text; IconKind = icon; Height = 40; Cursor = Cursors.Hand; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            UI.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Selected) UI.FillRound(g, r, 12, UI.PanelOn);
            else if (hover) UI.FillRound(g, r, 12, UI.Panel);
            if (Selected)
                UI.FillRound(g, new Rectangle(0, 11, 3, Height - 22), 3, UI.Accent);
            UI.DrawKindIcon(g, new Rectangle(10, (Height - 22) / 2, 24, 22), IconKind, Selected ? UI.Accent : UI.Dim);
            using (var b = new SolidBrush(Selected ? UI.Text : (hover ? UI.Text : UI.Dim)))
                g.DrawString(Text, Selected ? UI.BodyBold : UI.BodyFont, b, 42, (Height - UI.BodyFont.Height) / 2f);
        }
    }

    class SettingsForm : ChromeForm
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, int mod, int vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

        static SettingsForm instance;
        const int W = 760, H = 600, NavW = 176;
        const int CX = NavW + 24;               // 내용 시작 x
        const int CW = W - CX - 24;             // 내용 폭

        MainForm main;
        Settings S { get { return App.S; } }
        Panel content;
        List<NavItem> navs = new List<NavItem>();
        int tab;
        int y;

        // 단축키 충돌 검사용
        class KeySlot { public HotkeyBox Box; public Card Card; public Func<Hotkey> Get; public string Label; }
        List<KeySlot> slots = new List<KeySlot>();

        public static void Open(MainForm main, int tab)
        {
            if (instance != null && !instance.IsDisposed)
            {
                instance.ShowTab(tab);
                instance.Activate();
                return;
            }
            instance = new SettingsForm(main);
            instance.ShowTab(tab);
            instance.Show(main);
        }

        SettingsForm(MainForm main)
        {
            this.main = main;
            Text = "Audio Switcher 설정";
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(W, H);
            ShowInTaskbar = false;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var wa = Screen.FromControl(main).WorkingArea;
            Location = new Point(
                Math.Max(wa.Left, Math.Min(main.Left + (main.Width - W) / 2, wa.Right - W)),
                Math.Max(wa.Top, Math.Min(main.Top + 40, wa.Bottom - H)));

            // 제목줄
            var bar = new FlatControl();
            bar.Bounds = new Rectangle(1, 1, W - 2, 55);
            bar.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics;
                UI.Smooth(g);
                using (var b = new SolidBrush(UI.Text)) g.DrawString("설정", UI.TitleFont, b, 20, 17);
            };
            bar.MouseDown += BeginDrag;
            Controls.Add(bar);
            var close = new TitleButton(0);
            close.Location = new Point(W - 48, 12);
            close.Click += delegate { Close(); };
            bar.Controls.Add(close);

            // 왼쪽 탭
            string[] names = { "단축키", "프로필", "앱별 출력", "일반" };
            int[] icons = { 6, 4, 5, 7 };
            for (int i = 0; i < names.Length; i++)
            {
                var n = new NavItem(names[i], icons[i]);
                n.Bounds = new Rectangle(12, 64 + i * 46, NavW - 12, 40);
                int idx = i;
                n.Click += delegate { ShowTab(idx); };
                Controls.Add(n);
                navs.Add(n);
            }

            content = new Panel();
            content.Bounds = new Rectangle(CX, 64, W - CX - 6, H - 64 - 14);
            content.BackColor = UI.Bg;
            content.AutoScroll = true;
            UI.DarkScrollbars(content);
            Controls.Add(content);

            // 단축키를 새로 입력받는 동안 기존 전역 단축키가 가로채지 않도록 잠시 끈다
            main.SuspendHotkeys();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && !(ActiveControl is HotkeyBox)) Close();
            base.OnKeyDown(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            S.Save();
            main.ResumeHotkeys();
            main.OnSettingsChanged();
            instance = null;
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(UI.Border))
            {
                e.Graphics.DrawLine(p, 0, 56, W, 56);
                e.Graphics.DrawLine(p, NavW + 8, 64, NavW + 8, H - 16);
            }
        }

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

        void ShowTab(int t)
        {
            tab = t;
            for (int i = 0; i < navs.Count; i++) { navs[i].Selected = i == t; navs[i].Invalidate(); }
            content.SuspendLayout();
            foreach (Control c in content.Controls.Cast<Control>().ToList()) { content.Controls.Remove(c); c.Dispose(); }
            content.AutoScrollPosition = new Point(0, 0);
            slots.Clear();
            y = 0;
            if (t == 0) BuildHotkeys();
            else if (t == 1) BuildProfiles();
            else if (t == 2) BuildRoutes();
            else BuildGeneral();
            // 아래 여백
            var spacer = new Control();
            spacer.Bounds = new Rectangle(0, y, 1, 12);
            Add(spacer);
            content.ResumeLayout();
            ValidateKeys();
        }

        void Rebuild()
        {
            int scroll = -content.AutoScrollPosition.Y;
            ShowTab(tab);
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
                    if (warn == null && IsHandleCreated)
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
            Note("입력칸을 클릭한 뒤 원하는 조합을 누르세요.  Backspace = 지우기  ·  Esc = 취소\n설정 창이 열려 있는 동안에는 전역 단축키가 잠시 꺼집니다.");

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

            var outs = main.OutDevices;
            var mics = main.MicDevices;

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
                box.ValueChanged += delegate { p.Key = box.Value.Clone(); S.Save(); ValidateKeys(); };
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
                if (main.CreateProfileFromCurrent(this)) { main.SuspendHotkeys(); Rebuild(); }
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

            var outs = main.OutDevices;
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
                    main.SetRoute(r.ExePath, id, NameOf(outs, id, r.DeviceName));
                };
                card.Controls.Add(dd);

                var rm = new TitleButton(0);
                rm.BackColor = UI.Panel;
                rm.Location = new Point(card.Width - 16 - 32, 14);
                rm.Click += delegate
                {
                    main.SetRoute(r.ExePath, null, null);
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
            var outs = main.OutDevices;
            if (outs.Count == 0) return;
            // 기본 장치가 아닌 첫 장치로 시작 (바로 효과가 보이도록)
            var d = outs.FirstOrDefault(x => !x.IsDefault) ?? outs[0];
            main.SetRoute(exePath, d.Id, d.Name);
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
                var d = main.OutDevices.Find(x => x.IsDefault);
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
            tAuto.SetSilently(MainForm.IsAutoStart());
            tAuto.CheckedChanged += delegate { MainForm.SetAutoStart(tAuto.Checked); };
            c4.Controls.Add(tAuto);

            y += 10;
            Section("정보", null);
            Note("설정 파일: %APPDATA%\\AudioSwitcher\\settings.ini\n디스코드에 마이크 변경이 반영되지 않으면 디스코드 음성 설정의 입력/출력 장치를 '기본값'으로 바꿔 주세요.");
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

        public ProfileDialog(string title, string initial, bool options, string note)
        {
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
            if (ProfileName.Length == 0) { name.Box.Focus(); return; }
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
