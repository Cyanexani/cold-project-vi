@echo off
rem Builds "Edu's Remake.exe" (the edu launcher) in the repository's root with the C# compiler that ships with Windows.
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo The .NET Framework 4 C# compiler was not found.
  exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"%~dp0icon.ico" /out:"%~dp0..\Edu's Remake.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.VisualBasic.dll /reference:System.Core.dll "%~dp0VIctoryLauncher.cs" "%~dp0Widgets.cs" "%~dp0SmoothMotion.cs"
if errorlevel 1 exit /b 1
echo Built: "%~dp0..\Edu's Remake.exe"
