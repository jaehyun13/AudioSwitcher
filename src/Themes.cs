// 색 조합 5가지 × 라이트/다크
using System;
using System.Drawing;
using Microsoft.Win32;

namespace NeonAudioSwitcher
{
    class Palette
    {
        public bool Dark;
        public Color Win, Side, Card, Chip, Line, Text, Dim, Faint, Acc, Ink, Sel, SelLine, OnAcc, Track;
    }

    static class Themes
    {
        public static readonly string[] Names = {
            "노랑 × 웜그레이", "로즈 × 플럼그레이", "블루베리 × 쿨그레이", "민트 × 그린그레이", "오렌지 × 차콜" };
        public static readonly string[] ModeNames = { "시스템 설정 따르기", "라이트", "다크" };

        public static event Action Changed;

        static Color H(string hex)
        {
            int v = Convert.ToInt32(hex.TrimStart('#'), 16);
            return Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255);
        }

        // win side card chip line text dim faint acc ink sel selLine onAcc track
        static Palette Make(bool dark, params string[] c)
        {
            var p = new Palette();
            p.Dark = dark;
            p.Win = H(c[0]); p.Side = H(c[1]); p.Card = H(c[2]); p.Chip = H(c[3]); p.Line = H(c[4]);
            p.Text = H(c[5]); p.Dim = H(c[6]); p.Faint = H(c[7]); p.Acc = H(c[8]); p.Ink = H(c[9]);
            p.Sel = H(c[10]); p.SelLine = H(c[11]); p.OnAcc = H(c[12]); p.Track = H(c[13]);
            return p;
        }

        static readonly Palette[] Light = {
            // 노랑: 흰 바탕 위 노랑 글자는 안 읽혀서 강조 글자는 진한 색
            Make(false, "#FFFFFF","#F4F3EE","#FFFFFF","#F2F0E8","#E6E4DC","#16150F","#56544A","#68665B","#F5C400","#8A6D00","#FFF6CC","#E0B400","#16150F","#E8E6DE"),
            Make(false, "#FFFFFF","#F6F4F6","#FFFFFF","#F6F1F4","#ECE7EB","#1E1520","#5D5161","#6F6373","#C2255C","#C2255C","#FBE7EF","#C2255C","#FFFFFF","#EEE6EB"),
            Make(false, "#FFFFFF","#F3F5FA","#FFFFFF","#EEF1F8","#E1E6F0","#121829","#4E5872","#626C86","#3B5BDB","#3B5BDB","#E8EDFF","#3B5BDB","#FFFFFF","#E2E7F2"),
            Make(false, "#FFFFFF","#F2F6F4","#FFFFFF","#EBF2EE","#DFE8E3","#11231C","#4F6159","#5F7169","#0B7A5E","#0B7A5E","#DDF1EA","#0B7A5E","#FFFFFF","#DCE6E1"),
            Make(false, "#FFFFFF","#F5F4F2","#FFFFFF","#F1EFEC","#E5E2DE","#1A1714","#56514B","#6A645D","#C2410C","#C2410C","#FDEADF","#C2410C","#FFFFFF","#E8E4DF"),
        };
        static readonly Palette[] DarkSet = {
            Make(true, "#1B1A16","#151410","#24221D","#2A2822","#33312A","#F6F4EC","#BDB9AA","#A9A596","#FFD43B","#FFD43B","#332E1C","#FFD43B","#16150F","#36342C"),
            Make(true, "#1A161B","#141115","#221D23","#2A232B","#2F2830","#F6EEF4","#BDAEB9","#A797A3","#FF6B9A","#FF6B9A","#3A2030","#FF6B9A","#2B0614","#352C34"),
            Make(true, "#141823","#0F121B","#1C2130","#232A3B","#2A3245","#EEF2FB","#A9B3CB","#949FB9","#7C9BFF","#7C9BFF","#232E52","#7C9BFF","#0A1440","#2C3448"),
            Make(true, "#111916","#0C1310","#18231F","#1F2C27","#25332D","#E9F3EF","#A3B6AE","#8FA49B","#4FE0B6","#4FE0B6","#1C3A31","#4FE0B6","#06261C","#2A3832"),
            Make(true, "#181716","#121110","#22201E","#2A2725","#33302D","#F5F2EE","#BAB3AB","#A69F97","#FF8A3D","#FF8A3D","#3A2618","#FF8A3D","#2A1200","#36322E"),
        };

        public static Palette Get(int combo, bool dark)
        {
            if (combo < 0 || combo >= Names.Length) combo = 0;
            return dark ? DarkSet[combo] : Light[combo];
        }

        // 미리 보기용 (설정의 색 조합 카드)
        public static Color AccentOf(int combo, bool dark) { return Get(combo, dark).Acc; }
        public static Color BgOf(int combo, bool dark) { return Get(combo, dark).Win; }

        public static bool SystemIsDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return true; }
        }

        public static bool ResolveDark(int mode)
        {
            if (mode == 1) return false;
            if (mode == 2) return true;
            return SystemIsDark();
        }

        static bool hooked;

        // 설정값으로 팔레트를 고르고, 바뀌었으면 화면들에 알림
        public static void Apply(Settings s, bool notify)
        {
            UI.Use(Get(s.ThemeCombo, ResolveDark(s.ThemeMode)));
            if (!hooked)
            {
                hooked = true;
                // Windows 다크/라이트 설정이 바뀌면 따라감
                SystemEvents.UserPreferenceChanged += delegate(object o, UserPreferenceChangedEventArgs e)
                {
                    if (e.Category != UserPreferenceCategory.General || App.S.ThemeMode != 0) return;
                    bool dark = SystemIsDark();
                    if (dark != UI.IsDark) Apply(App.S, true);
                };
            }
            if (notify && Changed != null) Changed();
        }
    }
}
