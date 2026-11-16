// 장치 열거 · 기본 장치 변경 · 볼륨 · 앱별 세션
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace NeonAudioSwitcher
{
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

        // 테스트용으로 장치 목록을 바꿔 끼울 수 있게 (평소에는 null)
        public static Func<EDataFlow, List<AudioDevice>> Override;

        public static List<AudioDevice> GetDevices(EDataFlow flow)
        {
            if (Override != null) return Override(flow);
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

        public static string GetProcessPath(int pid)
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

        // 모든 재생 장치의 세션을 모아 실행 파일 단위로 묶는다.
        // (앱별 출력 장치를 지정하면 그 앱의 세션은 기본 장치가 아닌 곳에 생기기 때문)
        public static List<AppGroup> GetGroups()
        {
            var result = new List<AppGroup>();
            var map = new Dictionary<string, AppGroup>();
            var systemSeen = false;
            foreach (var d in AudioManager.GetDevices(EDataFlow.eRender))
                CollectFrom(d.Id, d.IsDefault, map, result, ref systemSeen);
            // 시스템 소리는 항상 맨 아래, 나머지는 이름순 (목록 순서 안정화)
            result.Sort(delegate(AppGroup a, AppGroup b)
            {
                if (a.IsSystem != b.IsSystem) return a.IsSystem ? 1 : -1;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }
        static void CollectFrom(string deviceId, bool isDefaultDevice, Dictionary<string, AppGroup> map,
                                List<AppGroup> result, ref bool systemSeen)
        {
            IMMDeviceEnumerator en = null;
            IMMDevice dev = null;
            object o = null;
            try
            {
                en = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                en.GetDevice(deviceId, out dev);
                Guid iid = IID_SessionManager2;
                dev.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out o);
                var mgr = (IAudioSessionManager2)o;

                IAudioSessionEnumerator se;
                mgr.GetSessionEnumerator(out se);
                int count;
                se.GetCount(out count);

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
                        // 시스템 소리는 기본 장치 것 하나만 보여준다
                        if (isSys && (!isDefaultDevice || systemSeen)) { Marshal.ReleaseComObject(ctl); continue; }
                        if (isSys) systemSeen = true;

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
        }
    }
}
