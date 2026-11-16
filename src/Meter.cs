// 마이크 입력 레벨 미터
// 녹음 장치의 미터는 누군가 그 장치를 열고 있어야 값이 나오기 때문에,
// 패널이 열려 있는 동안만 아주 짧은 녹음 스트림을 열어 둔다 (데이터는 읽지 않음).
using System;
using System.Runtime.InteropServices;

namespace NeonAudioSwitcher
{
    [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioMeterInformation
    {
        void GetPeakValue(out float peak);
        void GetMeteringChannelCount(out int count);
        void GetChannelsPeakValues(int count, IntPtr values);
        void QueryHardwareSupport(out int mask);
    }

    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient
    {
        void Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
        void GetBufferSize(out uint frames);
        void GetStreamLatency(out long latency);
        void GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        void GetMixFormat(out IntPtr format);
        void GetDevicePeriod(out long def, out long min);
        void Start();
        void Stop();
        void Reset();
        void SetEventHandle(IntPtr h);
        void GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    class MicMeter : IDisposable
    {
        static readonly Guid IID_Meter = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
        static readonly Guid IID_Client = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");

        public string DeviceId;
        IAudioMeterInformation meter;
        IAudioClient client;
        bool started;

        public static MicMeter Open(string deviceId)
        {
            var m = new MicMeter();
            m.DeviceId = deviceId;
            IMMDeviceEnumerator en = null;
            IMMDevice dev = null;
            try
            {
                en = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                en.GetDevice(deviceId, out dev);
                object o;
                Guid iid = IID_Meter;
                dev.Activate(ref iid, 23, IntPtr.Zero, out o);
                m.meter = (IAudioMeterInformation)o;
                try
                {
                    Guid ic = IID_Client;
                    dev.Activate(ref ic, 23, IntPtr.Zero, out o);
                    m.client = (IAudioClient)o;
                    IntPtr fmt;
                    m.client.GetMixFormat(out fmt);
                    try { m.client.Initialize(0 /*공유*/, 0, 2000000 /*200ms*/, 0, fmt, IntPtr.Zero); }
                    finally { Marshal.FreeCoTaskMem(fmt); }
                    m.client.Start();
                    m.started = true;
                }
                catch { }   // 스트림을 못 열어도 다른 앱이 쓰는 중이면 값은 나온다
            }
            catch { m.meter = null; }
            finally
            {
                if (dev != null) Marshal.ReleaseComObject(dev);
                if (en != null) Marshal.ReleaseComObject(en);
            }
            return m;
        }

        public float Peak()
        {
            if (meter == null) return 0f;
            try { float p; meter.GetPeakValue(out p); return p; }
            catch { return 0f; }
        }

        public void Dispose()
        {
            try { if (client != null && started) client.Stop(); } catch { }
            if (client != null) { try { Marshal.ReleaseComObject(client); } catch { } client = null; }
            if (meter != null) { try { Marshal.ReleaseComObject(meter); } catch { } meter = null; }
        }
    }
}
