// exe에 넣어 둔 글꼴(SUIT, SIL OFL 1.1)을 불러온다
// PC에 글꼴을 설치하지 않아도 누구에게나 같은 글꼴로 보이게 하기 위함.
//  - GDI+ (Graphics.DrawString): PrivateFontCollection
//  - GDI  (TextRenderer, 기본 컨트롤): AddFontMemResourceEx 로 이 프로세스에만 등록
using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace NeonAudioSwitcher
{
    static class EmbeddedFonts
    {
        public const string Family = "SUIT";
        static readonly string[] Files = { "SUIT-Regular.ttf", "SUIT-Bold.ttf" };

        [DllImport("gdi32.dll")]
        static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);

        static PrivateFontCollection pfc;   // 프로그램이 끝날 때까지 살아 있어야 함
        static FontFamily family;

        public static FontFamily Get()
        {
            if (pfc != null) return family;
            pfc = new PrivateFontCollection();
            var asm = typeof(EmbeddedFonts).Assembly;
            foreach (var name in Files)
            {
                try
                {
                    using (var s = asm.GetManifestResourceStream(name))
                    {
                        if (s == null) continue;
                        var data = new byte[s.Length];
                        int read = 0;
                        while (read < data.Length)
                        {
                            int n = s.Read(data, read, data.Length - read);
                            if (n <= 0) break;
                            read += n;
                        }
                        // GDI+가 메모리를 계속 참조하므로 해제하지 않는다
                        IntPtr mem = Marshal.AllocCoTaskMem(data.Length);
                        Marshal.Copy(data, 0, mem, data.Length);
                        pfc.AddMemoryFont(mem, data.Length);
                        uint count = 0;
                        try { AddFontMemResourceEx(mem, (uint)data.Length, IntPtr.Zero, ref count); } catch { }
                    }
                }
                catch { }
            }
            foreach (var f in pfc.Families)
                if (f.Name == Family) { family = f; break; }
            return family;
        }
    }
}
