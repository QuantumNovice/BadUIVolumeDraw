Outfile "BadUIVolumeDraw.exe"
InstallDir "$PROGRAMFILES\BadUIVolumeDraw"
RequestExecutionLevel admin

Page directory
Page instfiles
Function .onInit
  InitPluginsDir
  File "/oname=$PluginsDir\spltmp.bmp" "${NSISDIR}\Contrib\Graphics\Wizard\llama.bmp"

; optional
; File /oname=$PluginsDir\spltmp.wav "my_splashsound.wav"

  advsplash::show 1000 600 400 -1 $PluginsDir\spltmp

  Pop $0 ; $0 has '1' if the user closed the splash screen early,
         ; '0' if everything closed normally, and '-1' if some error occurred.

FunctionEnd
Section "Install"

  ; Create install directory
  CreateDirectory "$INSTDIR"

  ; Set source path and copy all files recursively
  SetOutPath "$INSTDIR"
  File /r "bin\Release\net8.0-windows\win-x64\publish\*.*"

  ; Create a Start Menu shortcut
  CreateShortcut "$SMPROGRAMS\BadUIVolumeDraw.lnk" "$INSTDIR\BadUIVolumeDraw.exe"

  ; Create uninstaller
  WriteUninstaller "$INSTDIR\Uninstall.exe"

SectionEnd

Section "Uninstall"

  ; Remove installed files and shortcuts
  Delete "$INSTDIR\*.*"
  Delete "$SMPROGRAMS\BadUIVolumeDraw.lnk"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /r "$INSTDIR"

SectionEnd
