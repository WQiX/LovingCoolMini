@echo off
setlocal
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set CSC=%FW%\csc.exe

if not exist "%CSC%" (
  echo Cannot find .NET Framework compiler: %CSC%
  exit /b 1
)
if not exist "lib\LibreHardwareMonitorLib.dll" (
  echo Missing lib\LibreHardwareMonitorLib.dll
  exit /b 1
)
if not exist "assets\app.ico" (
  echo Missing assets\app.ico
  exit /b 1
)

"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ ^
  /out:LovingCoolMini.exe ^
  /win32manifest:app.manifest ^
  /win32icon:assets\app.ico ^
  /r:System.dll ^
  /r:System.Core.dll ^
  /r:System.Drawing.dll ^
  /r:System.Windows.Forms.dll ^
  /r:System.Management.dll ^
  /r:System.Runtime.Serialization.dll ^
  /r:System.Xml.dll ^
  /r:Microsoft.CSharp.dll ^
  /r:lib\LibreHardwareMonitorLib.dll ^
  src\*.cs

if errorlevel 1 (
  echo Build failed.
  exit /b 1
)

copy /Y "lib\LibreHardwareMonitorLib.dll" "LibreHardwareMonitorLib.dll" >nul
if errorlevel 1 (
  echo Failed to copy LibreHardwareMonitorLib.dll
  exit /b 1
)

if not exist "publish" mkdir "publish"
copy /Y "LovingCoolMini.exe" "publish\LovingCoolMini.exe" >nul
copy /Y "LovingCoolMini.exe.config" "publish\LovingCoolMini.exe.config" >nul
copy /Y "LibreHardwareMonitorLib.dll" "publish\LibreHardwareMonitorLib.dll" >nul
if exist "README.md" copy /Y "README.md" "publish\README.md" >nul
if exist "LICENSE" copy /Y "LICENSE" "publish\LICENSE" >nul
if exist "Start LovingCoolMini.cmd" copy /Y "Start LovingCoolMini.cmd" "publish\Start LovingCoolMini.cmd" >nul
if exist "licenses\LibreHardwareMonitor.txt" copy /Y "licenses\LibreHardwareMonitor.txt" "publish\LibreHardwareMonitor-LICENSE.txt" >nul

echo.
echo Built LovingCoolMini.exe
echo Output: %CD%\publish
