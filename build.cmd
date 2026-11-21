@echo off
rem Builds Agent Dock with the C# compiler that ships inside Windows (.NET Framework 4.8).
rem No Visual Studio needed. The program lands in bin\AgentDockStandalone.exe (Story 6817).
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe in the .NET Framework folder.
  exit /b 1
)
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /optimize+ /out:bin\AgentDockStandalone.exe /win32manifest:app.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll *.cs
if errorlevel 1 (
  echo.
  echo Build FAILED.
  exit /b 1
)
echo.
echo Built bin\AgentDockStandalone.exe
