@echo off
rem Updates Agent Dock (Story 6817): waits for the dock to close, pulls the newest code with git,
rem builds it, and starts the new dock. Started by the dock's Settings -> Update Agent Dock, which
rem passes the dock's process id; it can also be run by hand with the dock closed.
rem Arguments for the restarted dock come in the AGENTDOCK_RELAUNCH_ARGS environment variable.
setlocal
cd /d "%~dp0"
title Updating Agent Dock
set "DOCKEXE=%~dp0bin\AgentDockStandalone.exe"
set "DOCKPID=%~1"

if "%DOCKPID%"=="" goto pull
echo Waiting for Agent Dock to close...
set /a WAITED=0
:wait
tasklist /FI "PID eq %DOCKPID%" 2>nul | find " %DOCKPID% " >nul
if errorlevel 1 goto pull
set /a WAITED+=1
if %WAITED% geq 60 (
  echo Agent Dock did not close within a minute.
  goto fail
)
ping -n 2 127.0.0.1 >nul
goto wait

:pull
echo.
echo Getting the newest code...
where git >nul 2>nul
if errorlevel 1 (
  echo git is not installed on this machine, so the newest code cannot be pulled.
  echo Download the ZIP from GitHub instead, then run build.cmd.
  goto fail
)
if not exist ".git" (
  echo This folder is not a git clone, so the newest code cannot be pulled.
  echo Clone it with git, or download the ZIP from GitHub and run build.cmd.
  goto fail
)
git pull --ff-only
if errorlevel 1 (
  echo The pull failed. Nothing was built.
  goto fail
)

echo.
echo Building...
call "%~dp0build.cmd"
if errorlevel 1 goto fail

echo.
echo Starting the new Agent Dock...
start "" "%DOCKEXE%" %AGENTDOCK_RELAUNCH_ARGS%
exit /b 0

:fail
echo.
echo The update did not finish. Starting the Agent Dock you had.
if exist "%DOCKEXE%" start "" "%DOCKEXE%" %AGENTDOCK_RELAUNCH_ARGS%
echo.
pause
exit /b 1
