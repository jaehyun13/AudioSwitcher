// 설정 저장/불러오기 (%APPDATA%\AudioSwitcher\settings.ini)
// 외부 라이브러리 없이 읽고 쓸 수 있도록 단순한 key=value 형식을 쓴다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NeonAudioSwitcher
{
    class Hotkey
    {
        public const int MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8;
        public int Mods;
        public int Vk;

        public Hotkey() { }
        public Hotkey(int mods, int vk) { Mods = mods; Vk = vk; }

        public bool IsEmpty { get { return Vk == 0; } }
        public Hotkey Clone() { return new Hotkey(Mods, Vk); }
        public bool SameAs(Hotkey o) { return o != null && !IsEmpty && o.Mods == Mods && o.Vk == Vk; }

        public static string ModsText(int mods)
        {
            var sb = new StringBuilder();
            if ((mods & MOD_CONTROL) != 0) sb.Append("Ctrl+");
            if ((mods & MOD_ALT) != 0) sb.Append("Alt+");
            if ((mods & MOD_SHIFT) != 0) sb.Append("Shift+");
            if ((mods & MOD_WIN) != 0) sb.Append("Win+");
            return sb.ToString();
        }

        public static string KeyName(int vk)
        {
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            if (vk >= 0x60 && vk <= 0x69) return "Num" + (vk - 0x60);
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
            switch (vk)
            {
                case 0x20: return "Space";
                case 0x0D: return "Enter";
                case 0x09: return "Tab";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "PgUp";
                case 0x22: return "PgDn";
                case 0x25: return "←";
                case 0x26: return "↑";
                case 0x27: return "→";
                case 0x28: return "↓";
                case 0x6A: return "Num*";
                case 0x6B: return "Num+";
                case 0x6D: return "Num-";
                case 0x6E: return "Num.";
                case 0x6F: return "Num/";
                case 0xBA: return ";";
                case 0xBB: return "=";
                case 0xBC: return ",";
                case 0xBD: return "-";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xC0: return "`";
                case 0xDB: return "[";
                case 0xDC: return "\\";
                case 0xDD: return "]";
                case 0xDE: return "'";
                case 0xAD: return "음소거키";
                case 0xAE: return "볼륨-키";
                case 0xAF: return "볼륨+키";
                case 0xB3: return "재생키";
                case 0x13: return "Pause";
                case 0x91: return "ScrollLock";
            }
            return ((Keys)vk).ToString();
        }

        public override string ToString()
        {
            if (IsEmpty) return "";
            return ModsText(Mods) + KeyName(Vk);
        }

        public string Serialize() { return Mods + "," + Vk; }

        public static Hotkey Parse(string s)
        {
            var h = new Hotkey();
            if (string.IsNullOrEmpty(s)) return h;
            var p = s.Split(',');
            if (p.Length == 2)
            {
                int.TryParse(p[0], out h.Mods);
                int.TryParse(p[1], out h.Vk);
            }
            return h;
        }
    }

    class Profile
    {
        public string Name = "프로필";
        public string OutId, OutName;      // null = 변경 안 함
        public string MicId, MicName;      // null = 변경 안 함
        public float OutVol = -1f;         // < 0 = 볼륨 유지
        public float MicVol = -1f;
        public Hotkey Key = new Hotkey();
    }

    // 장치별로 기억하는 것 (이름 바꾸기 · 숨기기 · 최대 볼륨 · 마지막 볼륨)
    class DeviceInfo
    {
        public string Id;
        public string Name;          // 마지막으로 본 원래 이름
        public string Alias;         // 사용자가 붙인 이름 (없으면 null)
        public bool Hidden;
        public float MaxVol = -1f;   // < 0 = 제한 없음
        public float LastVol = -1f;  // 마지막으로 쓰던 볼륨
    }

    // 이 프로그램이 실행되면 이 프로필 적용
    class AppTrigger
    {
        public string ExePath;
        public string ProfileName;
    }

    class AppRoute
    {
        public string ExePath;
        public string DeviceId;
        public string DeviceName;
    }

    // 단축키로 실행할 수 있는 동작 목록 (설정 화면 순서 그대로)
    static class Actions
    {
        public const string CycleOut = "cycleOut";
        public const string CycleMic = "cycleMic";
        public const string Mixer = "mixer";
        public const string MuteOut = "muteOut";
        public const string MuteMic = "muteMic";
        public const string ShowWindow = "showWindow";

        public static readonly string[] All = { CycleOut, CycleMic, Mixer, MuteOut, MuteMic, ShowWindow };

        public static string Label(string id)
        {
            switch (id)
            {
                case CycleOut: return "다음 출력 장치로 전환";
                case CycleMic: return "다음 마이크로 전환";
                case Mixer: return "앱별 볼륨 열기";
                case MuteOut: return "출력 음소거 켜기/끄기";
                case MuteMic: return "마이크 음소거 켜기/끄기";
                case ShowWindow: return "패널 펼치기/접기";
            }
            return id;
        }

        public static Hotkey Default(string id)
        {
            const int CA = Hotkey.MOD_CONTROL | Hotkey.MOD_ALT;
            switch (id)
            {
                case CycleOut: return new Hotkey(CA, 'A');
                case CycleMic: return new Hotkey(CA, 'M');
                case Mixer: return new Hotkey(CA, 'X');
            }
            return new Hotkey();
        }
    }

    class Settings
    {
        public bool AlsoComm = true;
        public bool ToastEnabled = true;
        public int ToastPos = 0;           // 0 오른쪽 아래 · 1 오른쪽 위 · 2 가운데 아래 · 3 가운데 위
        public int DirectMods = Hotkey.MOD_CONTROL | Hotkey.MOD_ALT;   // 0 = 번호 단축키 끔
        public int ThemeCombo = 0;         // Themes.Names 순서
        public int ThemeMode = 0;          // 0 시스템 따르기 · 1 라이트 · 2 다크
        public bool ShowDock = true;       // 화면 위쪽 독 표시
        public bool HideDockFullscreen = true;   // 전체화면 앱(게임 등)일 때 독 숨김
        public int DockX = int.MinValue, DockY = int.MinValue;   // 독 위치 (없으면 화면 위 가운데)
        public Dictionary<string, Hotkey> Keys = new Dictionary<string, Hotkey>();
        public List<Profile> Profiles = new List<Profile>();
        public List<AppRoute> Routes = new List<AppRoute>();
        public List<AppTrigger> Triggers = new List<AppTrigger>();
        public bool TriggerRestore = true;          // 앱이 꺼지면 원래 장치로
        public bool AutoSwitch = false;             // 장치 연결/해제 시 우선순위대로 자동 전환
        public List<string> OutPriority = new List<string>();
        public List<string> MicPriority = new List<string>();
        public bool RememberVolume = true;          // 장치별 볼륨 기억 (다시 연결되면 복원)
        public bool MicOverlay = true;              // 마이크 꺼짐 표시
        public int MicOverlayPos = 0;               // 0 오른쪽 아래 · 1 오른쪽 위 · 2 왼쪽 아래 · 3 왼쪽 위
        public Dictionary<string, DeviceInfo> Devices = new Dictionary<string, DeviceInfo>();

        public static readonly string[] CornerNames = { "오른쪽 아래", "오른쪽 위", "왼쪽 아래", "왼쪽 위" };

        public DeviceInfo Dev(string id)
        {
            DeviceInfo d;
            if (!Devices.TryGetValue(id, out d)) { d = new DeviceInfo(); d.Id = id; Devices[id] = d; }
            return d;
        }

        public DeviceInfo DevIfAny(string id)
        {
            DeviceInfo d;
            return id != null && Devices.TryGetValue(id, out d) ? d : null;
        }

        public Profile ProfileNamed(string name)
        {
            foreach (var p in Profiles) if (p.Name == name) return p;
            return null;
        }

        public static readonly string[] ToastPosNames = { "오른쪽 아래", "오른쪽 위", "가운데 아래", "가운데 위" };

        public Settings()
        {
            foreach (var a in Actions.All) Keys[a] = Actions.Default(a);
        }

        public Hotkey Key(string action)
        {
            Hotkey h;
            if (!Keys.TryGetValue(action, out h)) { h = new Hotkey(); Keys[action] = h; }
            return h;
        }

        public AppRoute RouteFor(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return null;
            foreach (var r in Routes)
                if (string.Equals(r.ExePath, exePath, StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AudioSwitcher"); }
        }

        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        static string F(float f) { return f.ToString("0.###", CultureInfo.InvariantCulture); }
        static float PF(string s)
        {
            float f;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : -1f;
        }
        static string Esc(string s) { return s == null ? "" : s.Replace("\r", " ").Replace("\n", " "); }
        static string Opt(string s) { return string.IsNullOrEmpty(s) ? null : s; }
        static List<string> SplitIds(string v)
        {
            var l = new List<string>();
            foreach (var x in v.Split(';')) if (x.Trim().Length > 0) l.Add(x.Trim());
            return l;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                sb.AppendLine("[general]");
                sb.AppendLine("comm=" + (AlsoComm ? 1 : 0));
                sb.AppendLine("toast=" + (ToastEnabled ? 1 : 0));
                sb.AppendLine("toastPos=" + ToastPos);
                sb.AppendLine("directMods=" + DirectMods);
                sb.AppendLine("themeCombo=" + ThemeCombo);
                sb.AppendLine("themeMode=" + ThemeMode);
                sb.AppendLine("showDock=" + (ShowDock ? 1 : 0));
                sb.AppendLine("hideDockFullscreen=" + (HideDockFullscreen ? 1 : 0));
                if (DockX != int.MinValue) { sb.AppendLine("dockX=" + DockX); sb.AppendLine("dockY=" + DockY); }
                sb.AppendLine("triggerRestore=" + (TriggerRestore ? 1 : 0));
                sb.AppendLine("autoSwitch=" + (AutoSwitch ? 1 : 0));
                sb.AppendLine("outPriority=" + string.Join(";", OutPriority.ToArray()));
                sb.AppendLine("micPriority=" + string.Join(";", MicPriority.ToArray()));
                sb.AppendLine("rememberVolume=" + (RememberVolume ? 1 : 0));
                sb.AppendLine("micOverlay=" + (MicOverlay ? 1 : 0));
                sb.AppendLine("micOverlayPos=" + MicOverlayPos);
                sb.AppendLine();
                sb.AppendLine("[hotkeys]");
                foreach (var kv in Keys) sb.AppendLine(kv.Key + "=" + kv.Value.Serialize());
                foreach (var p in Profiles)
                {
                    sb.AppendLine();
                    sb.AppendLine("[profile]");
                    sb.AppendLine("name=" + Esc(p.Name));
                    sb.AppendLine("outId=" + Esc(p.OutId));
                    sb.AppendLine("outName=" + Esc(p.OutName));
                    sb.AppendLine("micId=" + Esc(p.MicId));
                    sb.AppendLine("micName=" + Esc(p.MicName));
                    sb.AppendLine("outVol=" + F(p.OutVol));
                    sb.AppendLine("micVol=" + F(p.MicVol));
                    sb.AppendLine("key=" + p.Key.Serialize());
                }
                foreach (var t in Triggers)
                {
                    sb.AppendLine();
                    sb.AppendLine("[trigger]");
                    sb.AppendLine("exe=" + Esc(t.ExePath));
                    sb.AppendLine("profile=" + Esc(t.ProfileName));
                }
                foreach (var d in Devices.Values)
                {
                    sb.AppendLine();
                    sb.AppendLine("[device]");
                    sb.AppendLine("id=" + Esc(d.Id));
                    sb.AppendLine("name=" + Esc(d.Name));
                    if (!string.IsNullOrEmpty(d.Alias)) sb.AppendLine("alias=" + Esc(d.Alias));
                    if (d.Hidden) sb.AppendLine("hidden=1");
                    if (d.MaxVol >= 0) sb.AppendLine("maxVol=" + F(d.MaxVol));
                    if (d.LastVol >= 0) sb.AppendLine("lastVol=" + F(d.LastVol));
                }
                foreach (var r in Routes)
                {
                    sb.AppendLine();
                    sb.AppendLine("[route]");
                    sb.AppendLine("exe=" + Esc(r.ExePath));
                    sb.AppendLine("dev=" + Esc(r.DeviceId));
                    sb.AppendLine("devName=" + Esc(r.DeviceName));
                }
                // 쓰는 도중 꺼져도 기존 파일이 깨지지 않도록 임시 파일에 쓰고 교체
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch { }
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                string section = "";
                Profile prof = null;
                AppRoute route = null;
                AppTrigger trig = null;
                DeviceInfo dev = null;
                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2);
                        prof = null; route = null; trig = null; dev = null;
                        if (section == "profile") { prof = new Profile(); s.Profiles.Add(prof); }
                        else if (section == "route") { route = new AppRoute(); s.Routes.Add(route); }
                        else if (section == "trigger") { trig = new AppTrigger(); s.Triggers.Add(trig); }
                        else if (section == "device") dev = new DeviceInfo();
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();

                    if (section == "general")
                    {
                        if (k == "comm") s.AlsoComm = v == "1";
                        else if (k == "toast") s.ToastEnabled = v == "1";
                        else if (k == "toastPos") int.TryParse(v, out s.ToastPos);
                        else if (k == "directMods") int.TryParse(v, out s.DirectMods);
                        else if (k == "themeCombo") int.TryParse(v, out s.ThemeCombo);
                        else if (k == "themeMode") int.TryParse(v, out s.ThemeMode);
                        else if (k == "showDock") s.ShowDock = v == "1";
                        else if (k == "hideDockFullscreen") s.HideDockFullscreen = v == "1";
                        else if (k == "dockX") int.TryParse(v, out s.DockX);
                        else if (k == "dockY") int.TryParse(v, out s.DockY);
                        else if (k == "triggerRestore") s.TriggerRestore = v == "1";
                        else if (k == "autoSwitch") s.AutoSwitch = v == "1";
                        else if (k == "outPriority") s.OutPriority = SplitIds(v);
                        else if (k == "micPriority") s.MicPriority = SplitIds(v);
                        else if (k == "rememberVolume") s.RememberVolume = v == "1";
                        else if (k == "micOverlay") s.MicOverlay = v == "1";
                        else if (k == "micOverlayPos") int.TryParse(v, out s.MicOverlayPos);
                    }
                    else if (section == "hotkeys") s.Keys[k] = Hotkey.Parse(v);
                    else if (prof != null)
                    {
                        if (k == "name") prof.Name = v;
                        else if (k == "outId") prof.OutId = Opt(v);
                        else if (k == "outName") prof.OutName = Opt(v);
                        else if (k == "micId") prof.MicId = Opt(v);
                        else if (k == "micName") prof.MicName = Opt(v);
                        else if (k == "outVol") prof.OutVol = PF(v);
                        else if (k == "micVol") prof.MicVol = PF(v);
                        else if (k == "key") prof.Key = Hotkey.Parse(v);
                    }
                    else if (trig != null)
                    {
                        if (k == "exe") trig.ExePath = v;
                        else if (k == "profile") trig.ProfileName = v;
                    }
                    else if (dev != null)
                    {
                        if (k == "id") { dev.Id = v; if (v.Length > 0) s.Devices[v] = dev; }
                        else if (k == "name") dev.Name = v;
                        else if (k == "alias") dev.Alias = Opt(v);
                        else if (k == "hidden") dev.Hidden = v == "1";
                        else if (k == "maxVol") dev.MaxVol = PF(v);
                        else if (k == "lastVol") dev.LastVol = PF(v);
                    }
                    else if (route != null)
                    {
                        if (k == "exe") route.ExePath = v;
                        else if (k == "dev") route.DeviceId = v;
                        else if (k == "devName") route.DeviceName = v;
                    }
                }
                s.Routes.RemoveAll(r => string.IsNullOrEmpty(r.ExePath) || string.IsNullOrEmpty(r.DeviceId));
                s.Triggers.RemoveAll(t => string.IsNullOrEmpty(t.ExePath));
                if (s.MicOverlayPos < 0 || s.MicOverlayPos > 3) s.MicOverlayPos = 0;
                if (s.ToastPos < 0 || s.ToastPos > 3) s.ToastPos = 0;
                if (s.ThemeCombo < 0 || s.ThemeCombo >= Themes.Names.Length) s.ThemeCombo = 0;
                if (s.ThemeMode < 0 || s.ThemeMode > 2) s.ThemeMode = 0;
            }
            catch { }
            return s;
        }
    }
}
