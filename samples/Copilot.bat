@echo off
rem Sample pane batch for GitHub Copilot CLI. On the first run the dock copies this file to
rem config\pane.bat, and every new pane runs that copy. Edit config\pane.bat, not this file:
rem change the folder below to where you want the agent to start.
cd /d "%USERPROFILE%"
copilot
