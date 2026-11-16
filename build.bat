@echo off
rem Windows 내장 .NET 컴파일러로 빌드 (설치 불필요)
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:app.ico /out:AudioSwitcher.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /resource:app.ico,app.ico /resource:fonts\SUIT-Regular.ttf,SUIT-Regular.ttf /resource:fonts\SUIT-Bold.ttf,SUIT-Bold.ttf src\*.cs
if %errorlevel%==0 (echo 빌드 성공: AudioSwitcher.exe) else (echo 빌드 실패)
pause
