@echo off
rem ============================================================
rem  Build script for DeepSeek launcher v0.6.6
rem
rem  IMPORTANT: keep this file PURE ASCII. cmd.exe reads .cmd files
rem  using the OEM code page (GBK/936 on Chinese Windows), so Chinese
rem  characters saved as UTF-8 get mangled and the rest of the script
rem  is parsed as garbage commands.
rem
rem  The Chinese-named copy is matched with a wildcard, so no
rem  non-ASCII literal is needed anywhere in this file.
rem ============================================================
setlocal
cd /d "%~dp0"

set OUT=launcher-v066.exe
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /utf8output /r:System.Windows.Forms.dll /r:System.Drawing.dll /win32icon:app.ico /resource:app.ico,dsh_app.ico /win32manifest:app.manifest /out:%OUT% launcher.cs
echo csc_exit=%ERRORLEVEL%

if exist "%OUT%" (
  echo built: %~dp0%OUT%
  rem Refresh the Chinese-named copy too, if that file already exists.
  for %%F in ("%~dp0DeepSeek*-v0.6.6.exe") do copy /y "%OUT%" "%%~fF" >nul
) else (
  echo BUILD FAILED - read the compiler errors above
)
if not defined DSH_BUILD_NOPAUSE pause
