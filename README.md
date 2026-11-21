# Agent Dock

A Windows program that holds several AI agent terminals in one window. Each pane is a
Windows Terminal window running a batch file (an agent's command line). The dock pulls
those windows inside itself so you can arrange, maximize, color and pop them out.

## What it needs

- Windows 10 or 11
- Windows Terminal
- .NET Framework 4.8 (built into Windows 10 and 11)

## Starting it

    AgentDockStandalone.exe [--bat <file>] [--cwd <folder>]

- `--bat` is the batch file each new pane runs.
- `--cwd` is the folder each new pane starts in.

## Building it

No Visual Studio needed. Double-click `build.cmd` (or run it in a command prompt). It uses
the C# compiler that ships inside Windows and writes `bin\AgentDockStandalone.exe`.

With Visual Studio you can instead open `AgentDockStandalone.csproj`, or run
`MSBuild.exe AgentDockStandalone.csproj -p:Configuration=Release` (output in `bin\Release\`).

## A pane batch file

Each pane runs one batch file. `samples\Copilot.bat` starts GitHub Copilot CLI in your
profile folder. Copy it, change the folder, and pass it with `--bat`.

## Status

This is the starting copy of the dock, taken as is. The next steps are to turn the
defaults for this one machine into settings, add profiles so any terminal agent can run
in a pane, and publish a ready-built release.
