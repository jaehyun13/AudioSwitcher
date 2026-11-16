; Audio Switcher 설치 프로그램 (NSIS 3)
; 빌드: makensis -INPUTCHARSET UTF8 installer\AudioSwitcher.nsi
;       (저장소 루트에 AudioSwitcher.exe 가 먼저 빌드되어 있어야 합니다)
;
; 관리자 권한 없이 현재 사용자용으로 설치합니다.
;   설치 위치: %LOCALAPPDATA%\Programs\AudioSwitcher
;   시작 메뉴 바로가기, 바탕화면 바로가기(선택), Windows 시작 시 자동 실행(선택)
;   설정 → 앱 에서 제거 가능

Unicode true
SetCompressor /SOLID lzma

!define APPNAME    "Audio Switcher"
!define EXENAME    "AudioSwitcher.exe"
!define VERSION    "1.0.1"
!define PUBLISHER  "jaehyun13"
!define UNINSTKEY  "Software\Microsoft\Windows\CurrentVersion\Uninstall\AudioSwitcher"
!define RUNKEY     "Software\Microsoft\Windows\CurrentVersion\Run"
!define RUNNAME    "NeonAudioSwitcher"   ; 앱 안의 자동 실행 설정과 같은 이름

!ifndef OUTFILE
  !define OUTFILE "..\AudioSwitcher-Setup.exe"
!endif

Name "${APPNAME}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\AudioSwitcher"
InstallDirRegKey HKCU "${UNINSTKEY}" "InstallLocation"
RequestExecutionLevel user
BrandingText "${APPNAME} ${VERSION}"

VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=1042 "ProductName"     "${APPNAME}"
VIAddVersionKey /LANG=1042 "FileDescription" "${APPNAME} 설치 프로그램"
VIAddVersionKey /LANG=1042 "FileVersion"     "${VERSION}"
VIAddVersionKey /LANG=1042 "ProductVersion"  "${VERSION}"
VIAddVersionKey /LANG=1042 "CompanyName"     "${PUBLISHER}"
VIAddVersionKey /LANG=1042 "LegalCopyright"  "${PUBLISHER}"

!include "MUI2.nsh"

!define MUI_ICON   "..\app.ico"
!define MUI_UNICON "..\app.ico"
!define MUI_ABORTWARNING
!define MUI_COMPONENTSPAGE_NODESC

!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXENAME}"
!define MUI_FINISHPAGE_RUN_TEXT "${APPNAME} 바로 실행"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_COMPONENTS
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Korean"

; 실행 중인 앱은 파일을 잠그므로 설치/제거 전에 종료
!macro CloseApp
  nsExec::Exec 'taskkill /f /im ${EXENAME}'
  Pop $0
  Sleep 500
!macroend

Section "Audio Switcher (필수)" SecMain
  SectionIn RO
  !insertmacro CloseApp

  SetOutPath "$INSTDIR"
  File "..\${EXENAME}"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APPNAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\${EXENAME}"
  CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME} 제거.lnk" "$INSTDIR\Uninstall.exe"

  ; 설정 → 앱 (프로그램 추가/제거) 등록
  WriteRegStr   HKCU "${UNINSTKEY}" "DisplayName"     "${APPNAME}"
  WriteRegStr   HKCU "${UNINSTKEY}" "DisplayVersion"  "${VERSION}"
  WriteRegStr   HKCU "${UNINSTKEY}" "Publisher"       "${PUBLISHER}"
  WriteRegStr   HKCU "${UNINSTKEY}" "DisplayIcon"     "$INSTDIR\${EXENAME}"
  WriteRegStr   HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr   HKCU "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "EstimatedSize" 300
SectionEnd

Section "바탕화면 바로가기" SecDesktop
  CreateShortcut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\${EXENAME}"
SectionEnd

Section "Windows 시작 시 자동 실행" SecAutorun
  WriteRegStr HKCU "${RUNKEY}" "${RUNNAME}" '"$INSTDIR\${EXENAME}"'
SectionEnd

; ---------------- 제거 ----------------

Section "un.Audio Switcher" UnSecMain
  SectionIn RO
  !insertmacro CloseApp

  Delete "$INSTDIR\${EXENAME}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir  "$INSTDIR"

  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
  Delete "$SMPROGRAMS\${APPNAME}\${APPNAME} 제거.lnk"
  RMDir  "$SMPROGRAMS\${APPNAME}"
  Delete "$DESKTOP\${APPNAME}.lnk"

  DeleteRegValue HKCU "${RUNKEY}" "${RUNNAME}"
  DeleteRegKey   HKCU "${UNINSTKEY}"
SectionEnd

Section /o "un.설정도 함께 삭제 (프로필 · 단축키 · 테마)" UnSecSettings
  RMDir /r "$APPDATA\AudioSwitcher"
SectionEnd
