// AUDIO SWITCHER - 오디오 출력/마이크 장치 빠른 전환 프로그램
// 빌드: build.bat (Windows 내장 .NET 4.x csc.exe 사용, 외부 의존성 없음)
using System;
using System.Windows.Forms;

namespace NeonAudioSwitcher
{
    // 창들이 함께 쓰는 상태
    static class App
    {
        public static Settings S;
        public static Shell Shell;
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

                App.S = Settings.Load();
                Themes.Apply(App.S, false);
                App.Shell = new Shell();
                var h = App.Shell.Handle;       // 독을 숨겨 둬도 단축키가 동작하도록 창 핸들은 만들어 둠
                App.Shell.ApplyDockVisibility();
                if (!App.S.ShowDock) App.Shell.OpenPanel(WorkspaceForm.PageDevices);
                Application.Run(new ApplicationContext());
            }
        }
    }
}
