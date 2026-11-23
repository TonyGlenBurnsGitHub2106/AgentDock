@echo off
rem Builds Agent Dock with the C# compiler that ships inside Windows (.NET Framework 4.8).
rem No Visual Studio needed. The program lands in bin\AgentDockStandalone.exe (Story 6817).
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" goto nonet
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /optimize+ /out:bin\AgentDockStandalone.exe /win32manifest:app.manifest /win32icon:AgentDock.ico /resource:AgentDock.ico,AgentDockStandalone.AgentDock.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll *.cs
if errorlevel 1 (
  echo.
  echo Build FAILED.
  exit /b 1
)
echo.
echo Built bin\AgentDockStandalone.exe
exit /b 0

:nonet
rem No .NET Framework 4 on this computer: say so, and put install steps for an AI agent on the
rem clipboard (Story 6817). The dock itself cannot do this, since it needs .NET to run.
set "HELPFILE=%TEMP%\agentdock-needs-dotnet.txt"
> "%HELPFILE%" echo I am trying to build Agent Dock (https://github.com/TonyGlenBurnsGitHub2106/AgentDock), a Windows program
>>"%HELPFILE%" echo that needs .NET Framework 4.8. Its build.cmd could not find the C# compiler that comes with it:
>>"%HELPFILE%" echo %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
>>"%HELPFILE%" echo.
>>"%HELPFILE%" echo Please help me install the .NET Framework 4.8 runtime from Microsoft:
>>"%HELPFILE%" echo https://dotnet.microsoft.com/download/dotnet-framework/net48
>>"%HELPFILE%" echo On Windows 10 it may also come through Windows Update. It is built into Windows 11.
>>"%HELPFILE%" echo Explain each step and ask me before installing anything. A restart may be needed.
>>"%HELPFILE%" echo Afterwards run build.cmd in the Agent Dock folder again: %~dp0
echo.
echo Agent Dock needs .NET Framework 4.8, and it was not found on this computer.
echo It is built into Windows 10 (May 2019 update or later) and Windows 11.
echo Get it from: https://dotnet.microsoft.com/download/dotnet-framework/net48
echo.
clip < "%HELPFILE%"
if errorlevel 1 (
  echo Install steps for an AI agent are in: %HELPFILE%
) else (
  echo Install steps for an AI agent are on the clipboard. Paste them into your agent.
)
echo.
pause
exit /b 1
