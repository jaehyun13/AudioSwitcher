// NEON AUDIO SWITCHER - 오디오 출력/마이크 장치 빠른 전환 프로그램
// 빌드: build.bat (Windows 내장 .NET 4.x csc.exe 사용, 외부 의존성 없음)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NeonAudioSwitcher
{
    // ─────────────────────────── COM interop (Core Audio) ───────────────────────────
    enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey
    {
        public Guid fmtid;
        public int pid;
        public PropertyKey(Guid f, int p) { fmtid = f; pid = p; }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PropVariant
    {
        public ushort vt;
        public ushort r1, r2, r3;
        public IntPtr ptr;
        public int i1, i2;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IMMDeviceCollection devices);
        void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        void RegisterEndpointNotificationCallback(IntPtr client);
        void UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        void GetCount(out int count);
        void Item(int index, out IMMDevice device);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        void OpenPropertyStore(int stgmAccess, out IPropertyStore properties);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetState(out int state);
    }

    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        void GetCount(out int count);
        void GetAt(int index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value);
        void Commit();
    }

    // 기본 장치 변경용 비공개 인터페이스 (Win7~11 동일 동작, 출력/입력 모두 지원)
    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    class PolicyConfigClient { }

    [Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        void GetMixFormat(IntPtr a, IntPtr b);
        void GetDeviceFormat(IntPtr a, int b, IntPtr c);
        void ResetDeviceFormat(IntPtr a);
        void SetDeviceFormat(IntPtr a, IntPtr b, IntPtr c);
        void GetProcessingPeriod(IntPtr a, int b, IntPtr c, IntPtr d);
        void SetProcessingPeriod(IntPtr a, IntPtr b);
        void GetShareMode(IntPtr a, IntPtr b);
        void SetShareMode(IntPtr a, IntPtr b);
        void GetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
        void SetPropertyValue(IntPtr a, int b, IntPtr c, IntPtr d);
        void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
        void SetEndpointVisibility(IntPtr a, int b);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(IntPtr client);
        void UnregisterControlChangeNotify(IntPtr client);
        void GetChannelCount(out int count);
        void SetMasterVolumeLevel(float level, IntPtr eventContext);
        void SetMasterVolumeLevelScalar(float level, IntPtr eventContext);
        void GetMasterVolumeLevel(out float level);
        void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(int ch, float level, IntPtr eventContext);
        void SetChannelVolumeLevelScalar(int ch, float level, IntPtr eventContext);
        void GetChannelVolumeLevel(int ch, out float level);
        void GetChannelVolumeLevelScalar(int ch, out float level);
        void SetMute(int mute, IntPtr eventContext);
        void GetMute(out int mute);
    }

    // ── 앱별 볼륨 (Audio Session API) ──
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        void GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr session);
        void GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);
        void GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
        void RegisterSessionNotification(IntPtr notify);
        void UnregisterSessionNotification(IntPtr notify);
        void RegisterDuckNotification(IntPtr sessionId, IntPtr notify);
        void UnregisterDuckNotification(IntPtr notify);
    }

    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        void GetCount(out int count);
        void GetSession(int index, out IAudioSessionControl session);
    }

    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl
    {
        void GetState(out int state);
        void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid ctx);
        void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid ctx);
        void GetGroupingParam(out Guid group);
        void SetGroupingParam(ref Guid group, ref Guid ctx);
        void RegisterAudioSessionNotification(IntPtr notify);
        void UnregisterAudioSessionNotification(IntPtr notify);
    }

    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        // IAudioSessionControl (vtable 순서 유지 필수)
        void GetState(out int state);
        void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid ctx);
        void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid ctx);
        void GetGroupingParam(out Guid group);
        void SetGroupingParam(ref Guid group, ref Guid ctx);
        void RegisterAudioSessionNotification(IntPtr notify);
        void UnregisterAudioSessionNotification(IntPtr notify);
        // IAudioSessionControl2
        void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetProcessId(out int pid);
        [PreserveSig] int IsSystemSoundsSession();   // S_OK(0)=시스템 소리, S_FALSE(1)=일반 앱
        void SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        void SetMasterVolume(float level, ref Guid ctx);
        void GetMasterVolume(out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }


    class AudioDevice
    {
        public string Id;
        public string Name;
        public bool IsDefault;
        public bool IsDefaultComm;
        public int Kind; // 0=스피커 1=헤드셋 2=이어폰 3=마이크
        public EDataFlow Flow;
    }

    static class AudioManager
    {
        static readonly PropertyKey PKEY_FriendlyName =
            new PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);

        [DllImport("ole32.dll")]
        static extern int PropVariantClear(ref PropVariant pvar);

        static readonly string[] EarWords = { "buds", "이어폰", "earphone", "airpod", "에어팟", "earbud" };
        static readonly string[] HeadWords = { "headset", "헤드셋", "headphone", "헤드폰", "hyperx", "arctis",
                                               "kraken", "barracuda", "블랙샤크", "blackshark", "cloud", "virtuoso" };

        static int KindOf(string name)
        {
            string n = name.ToLowerInvariant();
            for (int i = 0; i < EarWords.Length; i++) if (n.Contains(EarWords[i])) return 2;
            for (int i = 0; i < HeadWords.Length; i++) if (n.Contains(HeadWords[i])) return 1;
            return 0;
        }

        static string GetDefaultId(IMMDeviceEnumerator en, EDataFlow flow, ERole role)
        {
            try
            {
                IMMDevice dev;
                en.GetDefaultAudioEndpoint(flow, role, out dev);
                string id;
                dev.GetId(out id);
                Marshal.ReleaseComObject(dev);
                return id;
            }
            catch { return null; }
        }

        public static List<AudioDevice> GetDevices(EDataFlow flow)
        {
            var list = new List<AudioDevice>();
            IMMDeviceEnumerator en = null;
            try
            {
                en = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                string defId = GetDefaultId(en, flow, ERole.eMultimedia);
                string commId = GetDefaultId(en, flow, ERole.eCommunications);

                IMMDeviceCollection coll;
                en.EnumAudioEndpoints(flow, 1 /*DEVICE_STATE_ACTIVE*/, out coll);
                int count;
                coll.GetCount(out count);
                for (int i = 0; i < count; i++)
                {
                    IMMDevice dev;
                    coll.Item(i, out dev);
                    string id;
                    dev.GetId(out id);

                    IPropertyStore ps;
                    dev.OpenPropertyStore(0 /*STGM_READ*/, out ps);
                    PropertyKey key = PKEY_FriendlyName;
                    PropVariant pv;
                    ps.GetValue(ref key, out pv);
                    string name = (pv.vt == 31 && pv.ptr != IntPtr.Zero)
                        ? Marshal.PtrToStringUni(pv.ptr) : "(알 수 없는 장치)";
                    PropVariantClear(ref pv);
                    Marshal.ReleaseComObject(ps);
                    Marshal.ReleaseComObject(dev);

                    var d = new AudioDevice();
                    d.Id = id;
                    d.Name = name;
                    d.IsDefault = (id == defId);
                    d.IsDefaultComm = (id == commId);
                    d.Kind = flow == EDataFlow.eCapture ? 3 : KindOf(name);
                    d.Flow = flow;
                    list.Add(d);
                }
                Marshal.ReleaseComObject(coll);
            }
            catch { }
            finally { if (en != null) Marshal.ReleaseComObject(en); }
            return list;
        }

        static readonly Guid IID_EndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");

        // 장치의 IAudioEndpointVolume에 대해 action 실행 (COM 개체 수명 관리 포함)
        static bool WithVolume(string id, Action<IAudioEndpointVolume> action)
        {
            IMMDeviceEnumerator en = null;
            IMMDevice dev = null;
            object o = null;
            try
            {
                en = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                en.GetDevice(id, out dev);
                Guid iid = IID_EndpointVolume;
                dev.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out o);
                action((IAudioEndpointVolume)o);
                return true;
            }
            catch { return false; }
            finally
            {
                if (o != null) Marshal.ReleaseComObject(o);
                if (dev != null) Marshal.ReleaseComObject(dev);
                if (en != null) Marshal.ReleaseComObject(en);
            }
        }

        public static float GetVolume(string id)
        {
            float f = -1f;
            WithVolume(id, delegate(IAudioEndpointVolume v) { v.GetMasterVolumeLevelScalar(out f); });
            return f;
        }

        public static void SetVolume(string id, float level)
        {
            WithVolume(id, delegate(IAudioEndpointVolume v) { v.SetMasterVolumeLevelScalar(level, IntPtr.Zero); });
        }

        public static bool GetMute(string id)
        {
            int m = 0;
            WithVolume(id, delegate(IAudioEndpointVolume v) { v.GetMute(out m); });
            return m != 0;
        }

        public static void SetMute(string id, bool mute)
        {
            WithVolume(id, delegate(IAudioEndpointVolume v) { v.SetMute(mute ? 1 : 0, IntPtr.Zero); });
        }

        public static bool SetDefault(string id, bool alsoComm)
        {
            try
            {
                IPolicyConfig pc = (IPolicyConfig)new PolicyConfigClient();
                pc.SetDefaultEndpoint(id, ERole.eConsole);
                pc.SetDefaultEndpoint(id, ERole.eMultimedia);
                if (alsoComm) pc.SetDefaultEndpoint(id, ERole.eCommunications);
                Marshal.ReleaseComObject(pc);
                return true;
            }
            catch { return false; }
        }
    }

    // ─────────────────────────── 앱별 오디오 세션 ───────────────────────────
    // 같은 실행 파일의 세션들(크롬 탭 등)을 하나로 묶어서 관리
    class AppGroup
    {
        public string Key;
        public string Name;
        public string ExePath;
        public int Pid;
        public bool IsSystem;
        public List<IAudioSessionControl> Sessions = new List<IAudioSessionControl>();
        public List<ISimpleAudioVolume> Vols = new List<ISimpleAudioVolume>();

        public float GetVolume()
        {
            try { float f; Vols[0].GetMasterVolume(out f); return f; }
            catch { return 0f; }
        }

        public bool GetMute()
        {
            try { bool m; Vols[0].GetMute(out m); return m; }
            catch { return false; }
        }

        public void SetVolume(float v)
        {
            Guid g = Guid.Empty;
            foreach (var s in Vols) { try { s.SetMasterVolume(v, ref g); } catch { } }
        }

        public void SetMute(bool m)
        {
            Guid g = Guid.Empty;
            foreach (var s in Vols) { try { s.SetMute(m, ref g); } catch { } }
        }


        public void Dispose()
        {
            foreach (var s in Sessions) { try { Marshal.ReleaseComObject(s); } catch { } }
            Sessions.Clear(); Vols.Clear();
        }
    }

    static class SessionManager
    {
        static readonly Guid IID_SessionManager2 = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(int access, bool inherit, int pid);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder name, ref int size);

        static Dictionary<string, Image> iconCache = new Dictionary<string, Image>();
        static Dictionary<string, string> nameCache = new Dictionary<string, string>();

        static string GetProcessPath(int pid)
        {
            if (pid <= 0) return null;
            IntPtr h = OpenProcess(0x1000 /*QUERY_LIMITED_INFORMATION*/, false, pid);
            if (h == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024);
                int cap = sb.Capacity;
                if (QueryFullProcessImageName(h, 0, sb, ref cap)) return sb.ToString();
                return null;
            }
            catch { return null; }
            finally { CloseHandle(h); }
        }

        public static string FriendlyName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "알 수 없는 앱";
            if (nameCache.ContainsKey(path)) return nameCache[path];
            string name = null;
            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(path);
                if (fvi != null && !string.IsNullOrEmpty(fvi.FileDescription))
                    name = fvi.FileDescription.Trim();
            }
            catch { }
            if (string.IsNullOrEmpty(name))
            {
                try { name = Path.GetFileNameWithoutExtension(path); } catch { name = "앱"; }
            }
            nameCache[path] = name;
            return name;
        }

        public static Image GetIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (iconCache.ContainsKey(path)) return iconCache[path];
            Image img = null;
            try
            {
                if (File.Exists(path))
                    using (var ic = Icon.ExtractAssociatedIcon(path)) img = ic.ToBitmap();
            }
            catch { }
            iconCache[path] = img;
            return img;
        }

        public static List<AppGroup> GetGroups()
        {
            var result = new List<AppGroup>();
            IMMDeviceEnumerator en = null;
            IMMDevice dev = null;
            object o = null;
            try
            {
                en = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                en.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out dev);
                Guid iid = IID_SessionManager2;
                dev.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out o);
                var mgr = (IAudioSessionManager2)o;

                IAudioSessionEnumerator se;
                mgr.GetSessionEnumerator(out se);
                int count;
                se.GetCount(out count);

                var map = new Dictionary<string, AppGroup>();
                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl ctl = null;
                    try
                    {
                        se.GetSession(i, out ctl);
                        int state;
                        ctl.GetState(out state);
                        if (state == 2 /*Expired*/) { Marshal.ReleaseComObject(ctl); continue; }

                        var c2 = (IAudioSessionControl2)ctl;
                        bool isSys = (c2.IsSystemSoundsSession() == 0);
                        int pid = 0;
                        try { c2.GetProcessId(out pid); } catch { }

                        string path = isSys ? null : GetProcessPath(pid);
                        string key = isSys ? "::system"
                            : (!string.IsNullOrEmpty(path) ? path.ToLowerInvariant() : "pid:" + pid);

                        AppGroup g;
                        if (!map.TryGetValue(key, out g))
                        {
                            g = new AppGroup();
                            g.Key = key;
                            g.IsSystem = isSys;
                            g.ExePath = path;
                            g.Pid = pid;
                            g.Name = isSys ? "시스템 소리" : FriendlyName(path);
                            map[key] = g;
                            result.Add(g);
                        }
                        g.Sessions.Add(ctl);
                        g.Vols.Add((ISimpleAudioVolume)ctl);
                    }
                    catch
                    {
                        if (ctl != null) { try { Marshal.ReleaseComObject(ctl); } catch { } }
                    }
                }
                Marshal.ReleaseComObject(se);
            }
            catch { }
            finally
            {
                if (o != null) Marshal.ReleaseComObject(o);
                if (dev != null) Marshal.ReleaseComObject(dev);
                if (en != null) Marshal.ReleaseComObject(en);
            }

            // 시스템 소리는 항상 맨 아래, 나머지는 이름순 (목록 순서 안정화)
            result.Sort(delegate(AppGroup a, AppGroup b)
            {
                if (a.IsSystem != b.IsSystem) return a.IsSystem ? 1 : -1;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }
    }

    // ─────────────────────────── 테마 ───────────────────────────
    static class UI
    {
        // 강조색은 파랑 하나만 쓰고 나머지는 전부 무채색으로 유지한다
        public static readonly Color Bg        = Color.FromArgb(23, 25, 31);
        public static readonly Color Panel     = Color.FromArgb(32, 35, 43);
        public static readonly Color PanelHover= Color.FromArgb(45, 49, 59);
        public static readonly Color PanelOn   = Color.FromArgb(34, 42, 56);   // 선택된 카드
        public static readonly Color Accent    = Color.FromArgb(82, 139, 235); // 유일한 강조색
        public static readonly Color Text      = Color.FromArgb(233, 236, 241);
        public static readonly Color Dim       = Color.FromArgb(150, 158, 172);
        public static readonly Color Border    = Color.FromArgb(56, 61, 74);

        // 설치된 폰트 중 앞에서부터 있는 것을 고름 (없으면 다음 후보로)
        static bool HasFamily(string name)
        {
            try { using (var ff = new FontFamily(name)) return true; }
            catch { return false; }
        }

        public static Font Pick(float size, FontStyle style, params string[] names)
        {
            foreach (var n in names)
                if (HasFamily(n)) return new Font(n, size, style);
            return new Font(FontFamily.GenericSansSerif, size, style);
        }

        // 한글이 섞인 문구: 한글·라틴을 한 서체가 모두 커버해야 굵기가 어긋나지 않음
        const string KR = "Noto Sans KR";
        const string KRFallback = "맑은 고딕";
        // 라틴 전용 문구(로고·배지·단축키): Segoe UI Semibold가 가장 정갈함
        const string EN = "Segoe UI Semibold";
        const string ENFallback = "Segoe UI";

        public static readonly Font TitleFont  = Pick(12.5f, FontStyle.Regular, EN, ENFallback);
        public static readonly Font HeaderFont = Pick(8.5f, FontStyle.Bold, KR, KRFallback);
        public static readonly Font NameFont   = Pick(10.5f, FontStyle.Bold, KR, KRFallback);
        public static readonly Font BodyFont   = Pick(9f, FontStyle.Regular, KR, KRFallback);
        public static readonly Font HintFont   = Pick(8f, FontStyle.Regular, KR, KRFallback);
        public static readonly Font SubFont    = Pick(7.5f, FontStyle.Regular, EN, ENFallback);
        public static readonly Font BadgeFont  = Pick(7.5f, FontStyle.Regular, EN, ENFallback);
        public static readonly Font GlyphFont  = Pick(10f, FontStyle.Regular, ENFallback);

        // 한 글자씩 그리면 공백은 폭이 0으로 측정되므로 실제 공백 폭을 따로 구함
        static float SpaceWidth(Graphics g, Font f)
        {
            var fmt = StringFormat.GenericTypographic;
            return g.MeasureString("i i", f, PointF.Empty, fmt).Width
                 - g.MeasureString("ii", f, PointF.Empty, fmt).Width;
        }

        static float CharWidth(Graphics g, char c, Font f, float space)
        {
            if (c == ' ') return space;
            return g.MeasureString(c.ToString(), f, PointF.Empty, StringFormat.GenericTypographic).Width;
        }

        // 자간을 벌려 그리기 (로고·헤더·배지처럼 짧은 대문자 문구용)
        public static void DrawTracked(Graphics g, string text, Font f, Brush b,
                                       float x, float y, float tracking)
        {
            float space = SpaceWidth(g, f);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != ' ')
                    g.DrawString(c.ToString(), f, b, x, y, StringFormat.GenericTypographic);
                x += CharWidth(g, c, f, space) + tracking;
            }
        }

        public static float TrackedWidth(Graphics g, string text, Font f, float tracking)
        {
            float space = SpaceWidth(g, f);
            float w = 0;
            for (int i = 0; i < text.Length; i++)
                w += CharWidth(g, text[i], f, space) + tracking;
            return w;
        }

        public static GraphicsPath Rounded(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, rad, rad, 180, 90);
            p.AddArc(r.Right - rad, r.Y, rad, rad, 270, 90);
            p.AddArc(r.Right - rad, r.Bottom - rad, rad, rad, 0, 90);
            p.AddArc(r.X, r.Bottom - rad, rad, rad, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void DrawKindIcon(Graphics g, Rectangle r, int kind, Color c)
        {
            using (var pen = new Pen(c, 2f))
            using (var br = new SolidBrush(c))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (kind == 1) // 헤드셋
                {
                    g.DrawArc(pen, r.X + 3, r.Y + 4, r.Width - 6, r.Height - 6, 180, 180);
                    g.FillRectangle(br, r.X + 2, r.Y + r.Height / 2 + 1, 5, 9);
                    g.FillRectangle(br, r.Right - 7, r.Y + r.Height / 2 + 1, 5, 9);
                    g.DrawLine(pen, r.Right - 5, r.Bottom - 3, r.X + r.Width / 2 + 2, r.Bottom - 1);
                }
                else if (kind == 2) // 이어폰
                {
                    g.DrawEllipse(pen, r.X + 3, r.Y + 3, 7, 7);
                    g.DrawEllipse(pen, r.Right - 10, r.Y + 3, 7, 7);
                    g.DrawLine(pen, r.X + 6, r.Y + 11, r.X + 6, r.Y + 18);
                    g.DrawLine(pen, r.Right - 7, r.Y + 11, r.Right - 7, r.Y + 18);
                }
                else if (kind == 3) // 마이크
                {
                    int cx = r.X + r.Width / 2;
                    g.FillEllipse(br, cx - 3, r.Y + 1, 7, 6);
                    g.FillRectangle(br, cx - 3, r.Y + 4, 7, 6);
                    g.FillEllipse(br, cx - 3, r.Y + 7, 7, 6);
                    g.DrawArc(pen, cx - 7, r.Y + 4, 15, 12, 0, 180);
                    g.DrawLine(pen, cx, r.Y + 16, cx, r.Y + 19);
                    g.DrawLine(pen, cx - 4, r.Y + 19, cx + 4, r.Y + 19);
                }
                else // 스피커
                {
                    int cy = r.Y + r.Height / 2;
                    Point[] pts = {
                        new Point(r.X + 2, cy - 4), new Point(r.X + 7, cy - 4),
                        new Point(r.X + 12, cy - 9), new Point(r.X + 12, cy + 9),
                        new Point(r.X + 7, cy + 4), new Point(r.X + 2, cy + 4) };
                    g.FillPolygon(br, pts);
                    g.DrawArc(pen, r.X + 12, cy - 5, 6, 10, -60, 120);
                    g.DrawArc(pen, r.X + 15, cy - 9, 8, 18, -60, 120);
                }
            }
        }
    }

    // 트레이 메뉴 다크 테마
    class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return UI.Panel; } }
        public override Color MenuItemSelected { get { return UI.PanelHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return UI.PanelHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return UI.PanelHover; } }
        public override Color MenuItemBorder { get { return UI.Accent; } }
        public override Color MenuBorder { get { return UI.Border; } }
        public override Color ImageMarginGradientBegin { get { return UI.Panel; } }
        public override Color ImageMarginGradientMiddle { get { return UI.Panel; } }
        public override Color ImageMarginGradientEnd { get { return UI.Panel; } }
        public override Color SeparatorDark { get { return UI.Border; } }
        public override Color SeparatorLight { get { return UI.Border; } }
    }

    // ─────────────────────────── 장치 행 ───────────────────────────
    class DeviceRow : Control
    {
        public AudioDevice Device;
        public string Sub = "";
        public Color Accent = UI.Accent;
        bool hover;

        public DeviceRow()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 선택된 것만 또렷하게, 나머지는 배경에 가라앉힌다 (테두리를 남발하지 않음)
            Color bg = Device.IsDefault ? UI.PanelOn : (hover ? UI.PanelHover : UI.Panel);
            using (var path = UI.Rounded(rect, 8))
            {
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                if (Device.IsDefault)
                    using (var p = new Pen(Accent, 1.4f)) g.DrawPath(p, path);
                else if (hover)
                    using (var p = new Pen(UI.Border)) g.DrawPath(p, path);
            }

            // 선택 표시는 왼쪽 세로 막대 하나로 (배지 대신)
            if (Device.IsDefault)
                using (var b = new SolidBrush(Accent))
                using (var bar = UI.Rounded(new Rectangle(1, Height / 2 - 11, 3, 22), 3))
                    g.FillPath(b, bar);

            Color ic = Device.IsDefault ? Accent : UI.Dim;
            UI.DrawKindIcon(g, new Rectangle(20, (Height - 22) / 2, 24, 22), Device.Kind, ic);

            // 오른쪽: 단축키를 옅게 (선택된 행에는 COMM 표시가 대신 들어감)
            float rightEdge = Width - 16;
            if (Device.IsDefaultComm)
            {
                float cw = UI.TrackedWidth(g, "통화", UI.HintFont, 0.5f);
                using (var mb = new SolidBrush(Accent))
                    UI.DrawTracked(g, "통화", UI.HintFont, mb, rightEdge - cw, Height / 2 - 7, 0.5f);
                rightEdge -= cw + 14;
            }
            if (!string.IsNullOrEmpty(Sub))
            {
                float sw = UI.TrackedWidth(g, Sub, UI.SubFont, 1.0f);
                using (var sb = new SolidBrush(UI.Border))
                    UI.DrawTracked(g, Sub, UI.SubFont, sb, rightEdge - sw, Height / 2 - 6, 1.0f);
                rightEdge -= sw + 14;
            }

            // 이름 (오른쪽 요소와 겹치지 않게 말줄임)
            var fmt = new StringFormat();
            fmt.Trimming = StringTrimming.EllipsisCharacter;
            fmt.FormatFlags = StringFormatFlags.NoWrap;
            fmt.LineAlignment = StringAlignment.Center;
            var nameRect = new RectangleF(56, 0, Math.Max(40f, rightEdge - 56), Height);
            using (var tb = new SolidBrush(Device.IsDefault ? UI.Text : UI.Dim))
                g.DrawString(Device.Name, UI.NameFont, tb, nameRect, fmt);
        }
    }

    // ─────────────────────────── 네온 볼륨 슬라이더 ───────────────────────────
    class NeonSlider : Control
    {
        public Color Accent = UI.Accent;
        public int IconKind = 0;        // 0=스피커 3=마이크
        public Image IconImage;         // 지정 시 앱 아이콘을 대신 그림
        float val;
        bool muted;
        bool dragging;
        public bool Dragging { get { return dragging; } }
        public float Value { get { return val; } }
        public event EventHandler ValueChanged;
        public event EventHandler MuteToggled;

        const int IconW = 30;
        const int PctW = 46;

        public NeonSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            Height = 28;
            BackColor = UI.Bg;
        }

        // 폴링 갱신용 (이벤트 발생 없음). v < 0이면 기존 값 유지
        public void SetState(float v, bool m)
        {
            if (v >= 0f) val = v;
            muted = m;
            Invalidate();
        }

        Rectangle TrackRect
        {
            get { return new Rectangle(IconW + 6, Height / 2 - 3, Width - IconW - PctW - 12, 6); }
        }

        void SetFromX(int x)
        {
            var t = TrackRect;
            float v = (x - t.X) / (float)t.Width;
            val = Math.Max(0f, Math.Min(1f, v));
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.X < IconW)
            {
                if (MuteToggled != null) MuteToggled(this, EventArgs.Empty);
            }
            else
            {
                dragging = true;
                SetFromX(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) SetFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseEnter(EventArgs e) { Focus(); base.OnMouseEnter(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            val = Math.Max(0f, Math.Min(1f, val + (e.Delta > 0 ? 0.02f : -0.02f)));
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // 아이콘 (클릭 = 음소거 토글)
            Color ic = muted ? UI.Dim : Accent;
            var iconRect = new Rectangle(2, (Height - 22) / 2, 24, 22);
            if (IconImage != null)
            {
                var prev = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                var ir = new Rectangle(iconRect.X + 1, (Height - 20) / 2, 20, 20);
                if (muted)
                {
                    // 음소거 상태에서는 아이콘을 흐리게
                    var cm = new System.Drawing.Imaging.ColorMatrix();
                    cm.Matrix33 = 0.35f;
                    using (var attr = new System.Drawing.Imaging.ImageAttributes())
                    {
                        attr.SetColorMatrix(cm);
                        g.DrawImage(IconImage, ir, 0, 0, IconImage.Width, IconImage.Height,
                                    GraphicsUnit.Pixel, attr);
                    }
                }
                else g.DrawImage(IconImage, ir);
                g.InterpolationMode = prev;
            }
            else UI.DrawKindIcon(g, iconRect, IconKind, ic);
            if (muted)
                using (var p = new Pen(UI.Dim, 2f))
                    g.DrawLine(p, iconRect.X, iconRect.Bottom, iconRect.Right, iconRect.Y);

            // 트랙
            var t = TrackRect;
            using (var path = UI.Rounded(t, 6))
            {
                using (var b = new SolidBrush(UI.Panel)) g.FillPath(b, path);
                using (var p = new Pen(UI.Border)) g.DrawPath(p, path);
            }

            // 채워진 부분
            int fillW = (int)(t.Width * val);
            Color fc = muted ? Color.FromArgb(120, 128, 145) : Accent;
            if (fillW > 3)
            {
                var fr = new Rectangle(t.X, t.Y, fillW, t.Height);
                using (var path = UI.Rounded(fr, 6))
                using (var b = new SolidBrush(fc))
                    g.FillPath(b, path);
            }

            // 썸
            int cx = t.X + fillW;
            using (var b = new SolidBrush(fc))
                g.FillEllipse(b, cx - 6, Height / 2 - 6, 12, 12);

            // 퍼센트 표시 (오른쪽 정렬)
            string pct = muted ? "MUTE" : string.Format("{0}%", (int)Math.Round(val * 100));
            float pw = UI.TrackedWidth(g, pct, UI.BadgeFont, 0.6f);
            using (var b = new SolidBrush(muted ? UI.Dim : Accent))
                UI.DrawTracked(g, pct, UI.BadgeFont, b, Width - pw - 4, Height / 2 - 7, 0.6f);
        }
    }

    // ─────────────────────────── 네온 체크박스 ───────────────────────────
    class NeonCheck : Control
    {
        bool isChecked;
        bool hover;
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public NeonCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = 22;
            Font = UI.BodyFont;
            BackColor = UI.Bg;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Width = TextRenderer.MeasureText(Text, Font).Width + 32;
        }

        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var box = new Rectangle(0, (Height - 16) / 2, 16, 16);

            using (var path = UI.Rounded(box, 6))
            {
                using (var b = new SolidBrush(isChecked ? UI.Accent : UI.Panel))
                    g.FillPath(b, path);
                Color bc = isChecked ? UI.Accent : (hover ? UI.Accent : UI.Border);
                using (var p = new Pen(bc, 1.5f))
                    g.DrawPath(p, path);
            }

            if (isChecked)
            {
                using (var p = new Pen(UI.Bg, 2.4f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    int bx = box.X, by = box.Y;
                    g.DrawLines(p, new[] {
                        new Point(bx + 4, by + 8), new Point(bx + 7, by + 11), new Point(bx + 12, by + 5) });
                }
            }

            using (var tb = new SolidBrush(isChecked ? UI.Text : UI.Dim))
                g.DrawString(Text, Font, tb, 24, (Height - Font.Height) / 2f);
        }
    }

    // ─────────────────────────── 네온 버튼 ───────────────────────────
    class NeonButton : Control
    {
        public Color Accent = UI.Accent;
        bool hover;

        public NeonButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = UI.NameFont;   // 한글이 섞이므로 한글 서체로
            BackColor = UI.Bg;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = UI.Rounded(r, 8))
            {
                using (var b = new SolidBrush(hover ? Color.FromArgb(38, Accent) : UI.Panel))
                    g.FillPath(b, path);
                using (var p = new Pen(hover ? Accent : UI.Border, 1.5f))
                    g.DrawPath(p, path);
            }
            var sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            using (var b = new SolidBrush(hover ? Accent : UI.Dim))
                g.DrawString(Text, Font, b, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    // ─────────────────────────── 전환 알림 토스트 ───────────────────────────
    class ToastForm : Form
    {
        static ToastForm current;
        Timer timer;
        int phase;
        int holdTicks;
        string text;
        string label;
        int kind;
        Color accent;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void Popup(string deviceName, int kind, Color accent, string label)
        {
            if (current != null) { try { current.Close(); } catch { } }
            current = new ToastForm(deviceName, kind, accent, label);
            current.Show();
        }

        ToastForm(string t, int k, Color a, string l)
        {
            text = t; kind = k; accent = a; label = l;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = UI.Bg;
            Opacity = 0;
            Size = new Size(360, 68);
            // 주 모니터 고정이 아니라 사용자가 지금 보고 있는 모니터에 띄운다
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(wa.Right - Width - 18, wa.Bottom - Height - 18);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            timer = new Timer();
            timer.Interval = 30;
            timer.Tick += OnTick;
            timer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000; return cp; } // WS_EX_NOACTIVATE
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { }
        }

        // 애니메이션 도중 새 토스트가 뜨면 이 폼은 즉시 Close() 되므로,
        // 타이머를 반드시 함께 멈춰야 파괴된 폼의 Opacity 를 건드리지 않는다
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
            if (current == this) current = null;
            base.OnFormClosed(e);
        }

        void OnTick(object s, EventArgs e)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                if (timer != null) timer.Stop();
                return;
            }

            if (phase == 0)
            {
                Opacity = Math.Min(1.0, Opacity + 0.12);
                if (Opacity >= 1.0) phase = 1;
            }
            else if (phase == 1)
            {
                holdTicks++;
                if (holdTicks > 45) phase = 2; // 약 1.3초 유지
            }
            else
            {
                Opacity -= 0.08;
                if (Opacity <= 0.05) { timer.Stop(); Close(); return; }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (var p = new Pen(accent, 1.5f))
                g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            using (var b = new SolidBrush(accent))
                g.FillRectangle(b, 0, 0, 4, Height);
            UI.DrawKindIcon(g, new Rectangle(18, 23, 24, 22), kind, accent);
            using (var db = new SolidBrush(UI.Dim))
                UI.DrawTracked(g, label, UI.SubFont, db, 54, 11, 1.2f);
            using (var tb = new SolidBrush(UI.Text))
                g.DrawString(text, UI.NameFont, tb, 52, 26);
        }
    }

    // ─────────────────────────── 앱별 볼륨 행 ───────────────────────────
    class AppRow : Control
    {
        public AppGroup Group;
        public NeonSlider Slider;

        public AppRow(AppGroup g, Image icon)
        {
            Group = g;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UI.Panel;
            Height = 60;

            Slider = new NeonSlider();
            Slider.Accent = UI.Accent;
            Slider.IconImage = icon;
            Slider.IconKind = 0;
            Slider.BackColor = UI.Panel;
            Slider.ValueChanged += delegate { Group.SetVolume(Slider.Value); };
            Slider.MuteToggled += delegate
            {
                bool m = !Group.GetMute();
                Group.SetMute(m);
                Slider.SetState(-1f, m);
            };
            Controls.Add(Slider);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Slider == null) return;   // 생성자에서 Height 설정 시 먼저 호출됨
            Slider.Bounds = new Rectangle(14, 30, Width - 28, 26);
        }

        public void Sync()
        {
            if (!Slider.Dragging) Slider.SetState(Group.GetVolume(), Group.GetMute());
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            using (var path = UI.Rounded(rect, 10))
            {
                using (var b = new SolidBrush(UI.Panel)) g.FillPath(b, path);
                using (var p = new Pen(UI.Border)) g.DrawPath(p, path);
            }

            using (var b = new SolidBrush(UI.Text))
                g.DrawString(Group.Name, UI.NameFont, b, 14, 7);
        }
    }

    // ─────────────────────────── 앱별 볼륨 믹서 창 ───────────────────────────
    class MixerForm : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr wp, IntPtr lp);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        static MixerForm instance;

        Panel list;
        Label empty;
        Timer listTimer;
        List<AppGroup> groups = new List<AppGroup>();
        List<AppRow> rows = new List<AppRow>();

        public static void Toggle(Form owner)
        {
            if (instance != null && !instance.IsDisposed)
            {
                // 이미 열려 있으면 보이는 자리로 다시 끌어옴 (다른 창에 가려졌을 수 있음)
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

        Form ownerRef;

        // 믹서를 메인 창과 같은 모니터에, 화면 밖으로 나가지 않게 배치
        public void PositionNear(Form owner)
        {
            if (owner != null) ownerRef = owner;
            Form o = ownerRef;

            Screen scr;
            if (o != null && o.Visible && o.WindowState != FormWindowState.Minimized)
                scr = Screen.FromControl(o);
            else
                scr = Screen.FromPoint(Cursor.Position);   // 트레이/단축키로 연 경우
            var wa = scr.WorkingArea;

            int x, y;
            if (o != null && o.Visible && o.WindowState != FormWindowState.Minimized)
            {
                x = o.Right + 12;                                  // 메인 창 오른쪽에 나란히
                if (x + Width > wa.Right) x = o.Left - Width - 12; // 자리가 없으면 왼쪽에
                y = o.Top;
            }
            else
            {
                x = wa.Right - Width - 24;
                y = wa.Bottom - Height - 24;
            }

            // 어떤 경우에도 작업 영역 안으로 밀어 넣음
            x = Math.Max(wa.Left, Math.Min(x, wa.Right - Width));
            y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - Height));
            Location = new Point(x, y);
        }

        MixerForm()
        {
            Text = "APP MIXER";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = UI.Bg;
            ClientSize = new Size(400, 300);
            DoubleBuffered = true;
            ShowInTaskbar = false;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildTitleBar();

            list = new Panel();
            list.Location = new Point(12, 50);
            list.Width = ClientSize.Width - 24;
            list.BackColor = UI.Bg;
            list.AutoScroll = true;
            Controls.Add(list);

            empty = new Label();
            empty.AutoSize = false;
            empty.TextAlign = ContentAlignment.MiddleCenter;
            empty.Font = UI.BodyFont;
            empty.ForeColor = UI.Dim;
            empty.BackColor = UI.Bg;
            empty.Text = "소리를 내고 있는 앱이 없습니다.\n음악이나 영상을 재생하면 여기에 나타납니다.";
            empty.Bounds = new Rectangle(12, 60, ClientSize.Width - 24, 70);
            empty.Visible = false;
            Controls.Add(empty);

            RefreshList();

            listTimer = new Timer();
            listTimer.Interval = 1000;
            listTimer.Tick += delegate { RefreshList(); };
            listTimer.Start();

        }

        void BuildTitleBar()
        {
            var bar = new Panel();
            bar.Bounds = new Rectangle(0, 0, ClientSize.Width, 46);
            bar.BackColor = UI.Bg;
            bar.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (var b = new SolidBrush(UI.Text))
                    UI.DrawTracked(g, "앱별 볼륨", UI.TitleFont, b, 18, 13, 0.6f);
                using (var p = new Pen(UI.Border)) g.DrawLine(p, 14, 44, ClientSize.Width - 14, 44);
            };
            bar.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                }
            };
            Controls.Add(bar);

            var close = new Label();
            close.Text = "✕";
            close.Bounds = new Rectangle(ClientSize.Width - 38, 10, 26, 24);
            close.ForeColor = UI.Dim;
            close.BackColor = UI.Bg;
            close.TextAlign = ContentAlignment.MiddleCenter;
            close.Font = UI.GlyphFont;
            close.Cursor = Cursors.Hand;
            close.MouseEnter += delegate { close.ForeColor = UI.Accent; };
            close.MouseLeave += delegate { close.ForeColor = UI.Dim; };
            close.Click += delegate { Close(); };
            bar.Controls.Add(close);
        }

        void RefreshList()
        {
            // 드래그 중에는 COM 개체를 교체하지 않음
            foreach (var r in rows) if (r.Slider.Dragging) return;

            var fresh = SessionManager.GetGroups();
            var oldGroups = groups;
            groups = fresh;

            string sigNew = string.Join("|", fresh.Select(g => g.Key).ToArray());
            string sigOld = string.Join("|", rows.Select(r => r.Group.Key).ToArray());

            if (sigNew != sigOld)
            {
                foreach (var r in rows) { list.Controls.Remove(r); r.Dispose(); }
                rows.Clear();

                int y = 0;
                foreach (var g in fresh)
                {
                    var row = new AppRow(g, g.IsSystem ? null : SessionManager.GetIcon(g.ExePath));
                    row.Bounds = new Rectangle(0, y, list.ClientSize.Width - 20, 60);
                    list.Controls.Add(row);
                    rows.Add(row);
                    y += 68;
                }
                Relayout(fresh.Count);
            }
            else
            {
                // 같은 앱 목록이면 COM 참조만 최신 것으로 교체
                for (int i = 0; i < rows.Count; i++) rows[i].Group = fresh[i];
            }

            foreach (var r in rows) r.Sync();
            foreach (var g in oldGroups) g.Dispose();
        }

        void Relayout(int count)
        {
            empty.Visible = (count == 0);
            int listH = Math.Min(Math.Max(count, 0) * 68, 8 * 68);
            list.Height = Math.Max(listH, 1);
            list.Visible = count > 0;
            int bodyBottom = 50 + (count > 0 ? listH : 84);
            ClientSize = new Size(400, bodyBottom + 34);
            empty.Bounds = new Rectangle(12, 60, ClientSize.Width - 24, 70);
            PositionNear(null);   // 높이가 바뀌었으니 화면 밖으로 나가지 않게 재배치
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            listTimer.Stop();
            foreach (var g in groups) g.Dispose();
            groups.Clear();
            instance = null;
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            using (var b = new SolidBrush(Color.FromArgb(132, 140, 156)))
                g.DrawString("아이콘 클릭 = 음소거   ·   휠 = 미세 조절", UI.HintFont, b, 16, ClientSize.Height - 24);
            using (var p = new Pen(UI.Border))
                g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
    }

    // ─────────────────────────── 메인 창 ───────────────────────────
    class MainForm : Form
    {
        // 2단 레이아웃 치수
        const int WinW = 780;
        const int ColW = 364;
        const int LeftX = 16;
        const int RightX = 400;
        const int DividerX = 390;

        const int WM_HOTKEY = 0x0312;
        const int HK_CYCLE = 100;
        const int HK_CYCLE_MIC = 101;
        const int HK_MIXER = 102;

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, int mod, int vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr wp, IntPtr lp);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        List<Control> dyn = new List<Control>();       // 장치 행 + 섹션 헤더 (매 갱신마다 재생성)
        List<AudioDevice> outDevices = new List<AudioDevice>();
        List<AudioDevice> micDevices = new List<AudioDevice>();
        NeonCheck chkComm, chkAuto;
        NeonSlider volOut, volMic;
        NeonButton btnMixer;
        Label hint;
        Panel titleBar;
        NotifyIcon tray;
        ContextMenuStrip trayMenu;
        Timer pollTimer;
        string lastSig = "";
        bool trayTipShown;
        int contentBottom = 400;

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "NeonAudioSwitcher";

        public MainForm()
        {
            Text = "AUDIO SWITCHER";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UI.Bg;
            ClientSize = new Size(440, 300);
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildTitleBar();
            BuildFooter();
            BuildTray();

            pollTimer = new Timer();
            pollTimer.Interval = 2000;
            pollTimer.Tick += delegate { RefreshDevices(false); };
            pollTimer.Start();

            RefreshDevices(true);
            // 체크되어 있는데 실제로는 어긋나 있는 상태로 시작하지 않도록 시작 시 한 번 맞춤
            if (chkComm.Checked) SyncCommToDefaults();
        }

        // ── 타이틀바 ──
        void BuildTitleBar()
        {
            titleBar = new Panel();
            titleBar.Bounds = new Rectangle(0, 0, WinW, 46);
            titleBar.BackColor = UI.Bg;
            titleBar.Paint += delegate(object s, PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (var b = new SolidBrush(UI.Text))
                    UI.DrawTracked(g, "Audio Switcher", UI.TitleFont, b, 18, 13, 0.6f);
                using (var p = new Pen(UI.Border)) g.DrawLine(p, 14, 44, WinW - 14, 44);
            };
            titleBar.MouseDown += DragWindow;
            Controls.Add(titleBar);

            var btnMin = MakeTitleButton("—", WinW - 70);
            btnMin.Click += delegate { WindowState = FormWindowState.Minimized; };
            var btnClose = MakeTitleButton("✕", WinW - 38);
            btnClose.Click += delegate { HideToTray(); };
        }

        Label MakeTitleButton(string t, int x)
        {
            var l = new Label();
            l.Text = t;
            l.Bounds = new Rectangle(x, 10, 26, 24);
            l.ForeColor = UI.Dim;
            l.BackColor = UI.Bg;
            l.TextAlign = ContentAlignment.MiddleCenter;
            l.Font = UI.GlyphFont;
            l.Cursor = Cursors.Hand;
            l.MouseEnter += delegate { l.ForeColor = t == "✕" ? UI.Accent : UI.Accent; };
            l.MouseLeave += delegate { l.ForeColor = UI.Dim; };
            titleBar.Controls.Add(l);
            return l;
        }

        void DragWindow(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
            }
        }

        // ── 하단 옵션 ──
        void BuildFooter()
        {
            chkComm = MakeCheck("통신 장치도 함께 변경 (디스코드/게임 보이스)");
            chkComm.Checked = true;
            // 체크하는 순간 현재 기본 장치에 통신 역할을 바로 맞춰준다.
            // (이게 없으면 다음에 장치를 바꿀 때까지 아무 일도 안 일어나 고장처럼 보임)
            chkComm.CheckedChanged += delegate
            {
                if (chkComm.Checked) SyncCommToDefaults();
            };

            chkAuto = MakeCheck("Windows 시작 시 자동 실행");
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                chkAuto.Checked = (k != null && k.GetValue(RunName) != null);
            chkAuto.CheckedChanged += delegate
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    {
                        if (chkAuto.Checked) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                        else k.DeleteValue(RunName, false);
                    }
                }
                catch { }
            };

            btnMixer = new NeonButton();
            btnMixer.Text = "앱별 볼륨 조절     Ctrl+Alt+X";
            btnMixer.Accent = UI.Accent;
            btnMixer.Click += delegate { MixerForm.Toggle(this); };
            Controls.Add(btnMixer);

            hint = new Label();
            hint.AutoSize = true;
            hint.Font = UI.HintFont;
            hint.ForeColor = Color.FromArgb(112, 119, 133);
            hint.BackColor = UI.Bg;
            // 행마다 단축키가 이미 보이므로 순환 단축키만 짧게 안내
            hint.Text = "Ctrl+Alt+A 출력 전환      Ctrl+Alt+M 마이크 전환";
            Controls.Add(hint);
        }

        NeonCheck MakeCheck(string t)
        {
            var c = new NeonCheck();
            c.Text = t;
            Controls.Add(c);
            return c;
        }

        // ── 트레이 ──
        void BuildTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
            trayMenu.ShowImageMargin = false;
            tray = new NotifyIcon();
            Icon exeIcon = null;
            try { exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            tray.Icon = exeIcon != null ? exeIcon : MakeIcon();
            tray.Text = "Audio Switcher";
            tray.ContextMenuStrip = trayMenu;
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowFromTray(); };
        }

        static Icon MakeIcon()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Point[] pts = { new Point(4, 12), new Point(11, 12), new Point(18, 5),
                                new Point(18, 27), new Point(11, 20), new Point(4, 20) };
                using (var b = new SolidBrush(UI.Accent)) g.FillPolygon(b, pts);
                using (var p = new Pen(UI.Accent, 2.5f))
                {
                    g.DrawArc(p, 19, 9, 7, 14, -60, 120);
                    g.DrawArc(p, 22, 5, 10, 22, -60, 120);
                }
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

        void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        // ── 장치 목록 ──
        void RefreshDevices(bool force)
        {
            var outs = AudioManager.GetDevices(EDataFlow.eRender);
            var mics = AudioManager.GetDevices(EDataFlow.eCapture);
            string sig = string.Join("|", outs.Concat(mics).Select(d =>
                d.Id + (d.IsDefault ? "*" : "") + (d.IsDefaultComm ? "+" : "")).ToArray());
            if (force || sig != lastSig)
            {
                lastSig = sig;
                outDevices = outs;
                micDevices = mics;
                RebuildRows();
                RebuildTrayMenu();
            }
            UpdateVolumes();
        }

        // ── 볼륨 ──
        void UpdateVolumes()
        {
            UpdateVol(outDevices, volOut);
            UpdateVol(micDevices, volMic);
        }

        void UpdateVol(List<AudioDevice> devs, NeonSlider s)
        {
            if (s == null || s.Dragging) return;
            var d = devs.Find(x => x.IsDefault);
            if (d == null) { s.SetState(0f, false); return; }
            s.SetState(AudioManager.GetVolume(d.Id), AudioManager.GetMute(d.Id));
        }

        // 출력·마이크의 통신 역할을 현재 기본 장치와 일치시킴
        void SyncCommToDefaults()
        {
            bool changed = false;
            changed |= SyncCommOne(outDevices);
            changed |= SyncCommOne(micDevices);
            if (changed) RefreshDevices(true);
        }

        bool SyncCommOne(List<AudioDevice> devs)
        {
            var d = devs.Find(x => x.IsDefault);
            if (d == null || d.IsDefaultComm) return false;
            return AudioManager.SetDefault(d.Id, true);
        }

        void SetDefVolume(List<AudioDevice> devs, NeonSlider s)
        {
            var d = devs.Find(x => x.IsDefault);
            if (d != null) AudioManager.SetVolume(d.Id, s.Value);
        }

        void ToggleDefMute(List<AudioDevice> devs, NeonSlider s)
        {
            var d = devs.Find(x => x.IsDefault);
            if (d == null) return;
            bool m = !AudioManager.GetMute(d.Id);
            AudioManager.SetMute(d.Id, m);
            s.SetState(-1f, m);
        }

        Label MakeHeader(string text, Color c, int x, int y)
        {
            var l = new Label();
            l.AutoSize = true;
            l.Font = UI.HeaderFont;
            l.ForeColor = c;
            l.BackColor = UI.Bg;
            l.Text = text;
            l.Location = new Point(x, y);
            return l;
        }

        // 한 단(출력 또는 입력)을 구성하고 그 단의 아래쪽 y좌표를 돌려줌
        // 소제목은 무채색, 강조는 항상 단일 강조색을 쓴다
        int BuildColumn(int x, string title, List<AudioDevice> devs,
                        bool isOutput, out NeonSlider slider)
        {
            Color accent = UI.Accent;
            int y = 54;
            var head = MakeHeader(title, UI.Dim, x + 2, y);
            Controls.Add(head); dyn.Add(head);
            y += 26;

            for (int i = 0; i < devs.Count; i++)
            {
                var row = new DeviceRow();
                row.Device = devs[i];
                row.Accent = accent;
                row.Sub = isOutput ? string.Format("Ctrl+Alt+{0}", i + 1) : "";
                row.Bounds = new Rectangle(x, y, ColW, 52);
                var dev = devs[i];
                row.Click += delegate { SwitchTo(dev); };
                Controls.Add(row); dyn.Add(row);
                y += 58;
            }

            if (devs.Count == 0)
            {
                var none = MakeHeader("연결된 장치가 없습니다", UI.Dim, x + 4, y + 6);
                none.Font = UI.BodyFont;
                Controls.Add(none); dyn.Add(none);
                y += 34;
            }

            y += 6;
            var sl = new NeonSlider();
            sl.Accent = accent;
            sl.IconKind = isOutput ? 0 : 3;
            sl.Bounds = new Rectangle(x, y, ColW, 28);
            var list = devs;
            sl.ValueChanged += delegate { SetDefVolume(list, sl); };
            sl.MuteToggled += delegate { ToggleDefMute(list, sl); };
            Controls.Add(sl); dyn.Add(sl);
            y += 32;

            slider = sl;
            return y;
        }

        void RebuildRows()
        {
            foreach (var c in dyn) { Controls.Remove(c); c.Dispose(); }
            dyn.Clear();

            NeonSlider sOut, sMic;
            int leftBottom = BuildColumn(LeftX, "재생 장치", outDevices, true, out sOut);
            int rightBottom = BuildColumn(RightX, "마이크", micDevices, false, out sMic);
            volOut = sOut;
            volMic = sMic;
            contentBottom = Math.Max(leftBottom, rightBottom);

            int y = contentBottom + 18;
            btnMixer.Bounds = new Rectangle(LeftX, y, WinW - 32, 34);
            y += 46;

            chkComm.Location = new Point(20, y);
            chkAuto.Location = new Point(RightX + 4, y);
            hint.Location = new Point(20, y + 30);
            ClientSize = new Size(WinW, y + 30 + 28);
            titleBar.Width = WinW;
            ClampToScreen();
            Invalidate();
        }

        // 창 크기가 늘어난 뒤 모니터 밖으로 삐져나가지 않게 보정
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

        void RebuildTrayMenu()
        {
            trayMenu.Items.Clear();
            AddTraySection("─ 출력 장치 ─", outDevices);
            trayMenu.Items.Add(new ToolStripSeparator());
            AddTraySection("─ 마이크 ─", micDevices);
            trayMenu.Items.Add(new ToolStripSeparator());
            var mixer = new ToolStripMenuItem("앱별 볼륨 조절");
            mixer.ForeColor = UI.Accent;
            mixer.Click += delegate { MixerForm.Toggle(this); };
            trayMenu.Items.Add(mixer);
            var open = new ToolStripMenuItem("열기");
            open.ForeColor = UI.Text;
            open.Click += delegate { ShowFromTray(); };
            trayMenu.Items.Add(open);
            var exit = new ToolStripMenuItem("종료");
            exit.ForeColor = UI.Text;
            exit.Click += delegate { tray.Visible = false; Application.Exit(); };
            trayMenu.Items.Add(exit);
        }

        void AddTraySection(string title, List<AudioDevice> devs)
        {
            var head = new ToolStripMenuItem(title);
            head.Enabled = false;
            trayMenu.Items.Add(head);
            foreach (var d in devs)
            {
                var dev = d;
                var item = new ToolStripMenuItem((d.IsDefault ? "▶  " : "     ") + d.Name);
                item.ForeColor = d.IsDefault ? UI.Accent : UI.Text;
                if (d.IsDefault) item.Font = new Font(item.Font, FontStyle.Bold);
                item.Click += delegate { SwitchTo(dev); };
                trayMenu.Items.Add(item);
            }
        }

        void SwitchTo(AudioDevice d)
        {
            if (d.IsDefault && !(chkComm.Checked && !d.IsDefaultComm)) return;
            if (AudioManager.SetDefault(d.Id, chkComm.Checked))
            {
                bool isMic = d.Flow == EDataFlow.eCapture;
                ToastForm.Popup(d.Name, d.Kind,
                    isMic ? UI.Accent : UI.Accent,
                    isMic ? "MIC SWITCHED >>" : "OUTPUT SWITCHED >>");
                RefreshDevices(true);
            }
        }

        void CycleDevice(List<AudioDevice> devs)
        {
            RefreshDevices(true);
            if (devs.Count < 2) return;
            int cur = devs.FindIndex(d => d.IsDefault);
            SwitchTo(devs[(cur + 1) % devs.Count]);
        }

        // ── 단축키 ──
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { }
            RegisterHotKey(Handle, HK_CYCLE, 0x2 | 0x1, (int)'A');           // Ctrl+Alt+A
            RegisterHotKey(Handle, HK_CYCLE_MIC, 0x2 | 0x1, (int)'M');       // Ctrl+Alt+M
            RegisterHotKey(Handle, HK_MIXER, 0x2 | 0x1, (int)'X');           // Ctrl+Alt+X
            for (int i = 0; i < 8; i++)
                RegisterHotKey(Handle, i + 1, 0x2 | 0x1, 0x31 + i);          // Ctrl+Alt+1~8
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnregisterHotKey(Handle, HK_CYCLE);
            UnregisterHotKey(Handle, HK_CYCLE_MIC);
            UnregisterHotKey(Handle, HK_MIXER);
            for (int i = 0; i < 8; i++) UnregisterHotKey(Handle, i + 1);
            tray.Visible = false;
            base.OnFormClosed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HK_CYCLE) CycleDevice(outDevices);
                else if (id == HK_CYCLE_MIC) CycleDevice(micDevices);
                else if (id == HK_MIXER) MixerForm.Toggle(this);
                else if (id >= 1 && id <= 8 && id <= outDevices.Count) SwitchTo(outDevices[id - 1]);
            }
            base.WndProc(ref m);
        }

        // ── 외곽 테두리 + 단 구분선 + 코너 장식 ──
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            // 좌우 단 구분선
            using (var p = new Pen(UI.Border))
                g.DrawLine(p, DividerX, 54, DividerX, contentBottom);
            // 본문과 하단 옵션 구분선
            using (var p = new Pen(UI.Border))
                g.DrawLine(p, LeftX, contentBottom + 10, WinW - LeftX, contentBottom + 10);

            using (var p = new Pen(UI.Border))
                g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new System.Threading.Mutex(true, "NeonAudioSwitcher_Single", out created))
            {
                if (!created) return; // 중복 실행 방지
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // 트레이에 상주하는 도구라, 예기치 못한 예외로 오류창을 띄우고 죽는 것보다
                // 그 동작만 건너뛰고 계속 살아있는 편이 낫다
                Application.ThreadException += delegate { };
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.Run(new MainForm());
            }
        }
    }
}
