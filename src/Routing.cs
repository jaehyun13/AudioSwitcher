// 앱별 출력 장치 지정
// Windows 설정 → 소리 → 볼륨 믹서의 "앱별 출력 장치"와 같은 기능.
// 공개 API가 없어 Windows 내부 WinRT 팩토리(AudioPolicyConfig)를 vtable로 직접 호출한다.
// Windows 10 1803 이상에서 동작하며, 그보다 낮으면 Available 이 false.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace NeonAudioSwitcher
{
    static class AppRouting
    {
        [DllImport("combase.dll")]
        static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string src, int length, out IntPtr hstring);
        [DllImport("combase.dll")]
        static extern int WindowsDeleteString(IntPtr hstring);
        [DllImport("combase.dll")]
        static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out int length);
        [DllImport("combase.dll")]
        static extern int RoGetActivationFactory(IntPtr classId, ref Guid iid, out IntPtr factory);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int SetPersistedFn(IntPtr self, int pid, int flow, int role, IntPtr deviceId);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetPersistedFn(IntPtr self, int pid, int flow, int role, out IntPtr deviceId);

        // 21H2 이후 / 이전 빌드의 인터페이스 GUID (메서드 배치는 같음)
        static readonly Guid[] FactoryIids = {
            new Guid("ab3d4648-e242-459f-b02f-541c70306324"),
            new Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258"),
        };

        // IInspectable 6개 + 미사용 메서드 19개 다음
        const int SlotSet = 25;
        const int SlotGet = 26;

        const string Prefix = @"\\?\SWD#MMDEVAPI#";
        const string RenderSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";

        static IntPtr factory = IntPtr.Zero;
        static bool tried;
        static SetPersistedFn setFn;
        static GetPersistedFn getFn;

        public static bool Available
        {
            get { Ensure(); return factory != IntPtr.Zero; }
        }

        static void Ensure()
        {
            if (tried) return;
            tried = true;
            IntPtr cls = IntPtr.Zero;
            try
            {
                const string name = "Windows.Media.Internal.AudioPolicyConfig";
                if (WindowsCreateString(name, name.Length, out cls) != 0) return;
                foreach (var g in FactoryIids)
                {
                    Guid iid = g;
                    IntPtr f;
                    if (RoGetActivationFactory(cls, ref iid, out f) == 0 && f != IntPtr.Zero)
                    {
                        factory = f;   // 프로그램이 끝날 때까지 들고 있음
                        IntPtr vt = Marshal.ReadIntPtr(factory);
                        setFn = (SetPersistedFn)Marshal.GetDelegateForFunctionPointer(
                            Marshal.ReadIntPtr(vt, SlotSet * IntPtr.Size), typeof(SetPersistedFn));
                        getFn = (GetPersistedFn)Marshal.GetDelegateForFunctionPointer(
                            Marshal.ReadIntPtr(vt, SlotGet * IntPtr.Size), typeof(GetPersistedFn));
                        break;
                    }
                }
            }
            catch { factory = IntPtr.Zero; }
            finally { if (cls != IntPtr.Zero) WindowsDeleteString(cls); }
        }

        // deviceId 가 null 이면 "기본 장치 따르기"로 되돌림
        public static bool SetForPid(int pid, string deviceId)
        {
            Ensure();
            if (factory == IntPtr.Zero || pid <= 0) return false;
            IntPtr h = IntPtr.Zero;
            try
            {
                if (!string.IsNullOrEmpty(deviceId))
                {
                    string full = Prefix + deviceId + RenderSuffix;
                    if (WindowsCreateString(full, full.Length, out h) != 0) return false;
                }
                int hr1 = setFn(factory, pid, (int)EDataFlow.eRender, (int)ERole.eConsole, h);
                int hr2 = setFn(factory, pid, (int)EDataFlow.eRender, (int)ERole.eMultimedia, h);
                return hr1 == 0 && hr2 == 0;
            }
            catch { return false; }
            finally { if (h != IntPtr.Zero) WindowsDeleteString(h); }
        }

        // 현재 지정된 장치 ID (없으면 null)
        public static string GetForPid(int pid)
        {
            Ensure();
            if (factory == IntPtr.Zero || pid <= 0) return null;
            IntPtr h = IntPtr.Zero;
            try
            {
                if (getFn(factory, pid, (int)EDataFlow.eRender, (int)ERole.eMultimedia, out h) != 0 || h == IntPtr.Zero)
                    return null;
                int len;
                IntPtr buf = WindowsGetStringRawBuffer(h, out len);
                string s = Marshal.PtrToStringUni(buf, len);
                if (s.StartsWith(Prefix)) s = s.Substring(Prefix.Length);
                int hash = s.IndexOf("#{");
                if (hash >= 0) s = s.Substring(0, hash);
                return s;
            }
            catch { return null; }
            finally { if (h != IntPtr.Zero) WindowsDeleteString(h); }
        }

        // 해당 실행 파일로 떠 있는 모든 프로세스 (크롬처럼 여러 개인 경우 포함)
        public static List<int> PidsOf(string exePath)
        {
            var list = new List<int>();
            if (string.IsNullOrEmpty(exePath)) return list;
            string name;
            try { name = Path.GetFileNameWithoutExtension(exePath); } catch { return list; }
            Process[] procs;
            try { procs = Process.GetProcessesByName(name); } catch { return list; }
            foreach (var p in procs)
            {
                try
                {
                    string path = SessionManager.GetProcessPath(p.Id);
                    if (path != null && string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase))
                        list.Add(p.Id);
                }
                catch { }
                finally { p.Dispose(); }
            }
            return list;
        }

        public static int ApplyToExe(string exePath, string deviceId)
        {
            int n = 0;
            foreach (int pid in PidsOf(exePath))
                if (SetForPid(pid, deviceId)) n++;
            return n;
        }

        // ── 새로 뜬 프로세스에도 규칙을 적용 (주기적으로 호출) ──
        static HashSet<string> applied = new HashSet<string>();

        public static void Maintain(List<AppRoute> routes)
        {
            if (routes.Count == 0 || !Available) return;
            var alive = new HashSet<string>();
            foreach (var r in routes)
            {
                foreach (int pid in PidsOf(r.ExePath))
                {
                    string key = pid + "|" + r.DeviceId;
                    alive.Add(key);
                    if (applied.Contains(key)) continue;
                    if (SetForPid(pid, r.DeviceId)) applied.Add(key);
                }
            }
            applied.IntersectWith(alive);   // 종료된 프로세스 기록은 정리
        }

        public static void Forget() { applied.Clear(); }
    }
}
