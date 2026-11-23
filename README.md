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

## The config folder

Each new pane runs one batch file: `config\pane.bat`. The `config` folder belongs to this
machine and is never committed. On the first run the dock copies `samples\Copilot.bat`
there, which starts GitHub Copilot CLI in your profile folder. Edit `config\pane.bat` to
change the folder or run a different agent. `--bat <file>` on the command line overrides it.

`config\agentdock.ini` holds the dock's settings, written with their defaults on the first
run. Each one has a comment saying what it does. Restart the dock after a change.

- `HidePaneTitleBars = true` (the default): panes have no title bar, so the terminal gets
  one more row. Close a pane with Agents -> Close Active Pane, and pop one out with
  Agents -> Unparent Active Pane. Set it to `false` to bring the title bars back.

## Status

This is the starting copy of the dock, taken as is. The next steps are to turn the
defaults for this one machine into settings, add profiles so any terminal agent can run
in a pane, and publish a ready-built release.
