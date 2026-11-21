@echo off
rem Sample pane batch for GitHub Copilot CLI. Each new pane in the dock runs this file.
rem Change the folder to where you want the agent to start, then start the dock with:
rem   AgentDockStandalone.exe --bat "<full path to this file>"
cd /d "%USERPROFILE%"
copilot
