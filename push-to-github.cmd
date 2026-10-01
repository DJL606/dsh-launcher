@echo off
rem ============================================================
rem  Push this repository to GitHub.
rem
rem  Usage: double-click, or drag the GitHub repository URL onto
rem  this file. Example:
rem      https://github.com/DJL606/your-repo.git
rem
rem  The first push opens the Git Credential Manager sign-in
rem  (a browser window); complete it once and it is remembered.
rem
rem  NOTE: keep this file PURE ASCII (cmd.exe reads .cmd with the
rem  OEM code page; UTF-8 Chinese comments would break parsing).
rem ============================================================
setlocal
cd /d "%~dp0"

set "REPO=%~1"
if "%REPO%"=="" (
  echo.
  echo Paste the GitHub repository URL and press Enter.
  echo Example: https://github.com/DJL606/dsh-launcher.git
  echo.
  set /p "REPO=URL: "
)

if "%REPO%"=="" (
  echo No URL given. Nothing to do.
  goto :end
)

echo.
echo Repository: %REPO%
git remote remove origin 2>nul
git remote add origin "%REPO%"
git branch -M main
echo.
echo Pushing... (a sign-in window may appear on first use)
git push -u origin main
echo.
echo git_exit=%ERRORLEVEL%
echo.
echo Done. Open the repository page to check the files.

:end
if not defined DSH_BUILD_NOPAUSE pause
