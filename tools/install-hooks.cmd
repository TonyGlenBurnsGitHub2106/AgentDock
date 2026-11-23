@echo off
rem Turns on Agent Dock's git hooks for this clone (Story 6817): the pre-commit hook raises the
rem version in AssemblyInfo.cs with every commit. Only needed where commits are made.
cd /d "%~dp0.."
git config core.hooksPath tools/hooks
if errorlevel 1 exit /b 1
echo Hooks turned on: every commit now raises the version.
