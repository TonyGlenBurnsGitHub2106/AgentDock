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

## Settings

**Settings -> Agent Dock Settings** sets the agent command each new pane runs, the folder it
starts in, and whether panes show a title bar. No file editing needed.
**Settings -> Open Config Folder** opens the folder holding those files in Explorer.
**Settings -> Update Agent Dock** gets the newest version: the dock closes (open panes go to
the desktop, still running), `update.cmd` runs `git pull`, builds, and starts the new dock,
which takes the panes back. Needs a git clone. If anything fails, the window says why and the
dock you had starts again. `update.cmd` can also be run by hand with the dock closed.

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
- `AllowAdminPanes = false` (the default): the dock and every pane run with normal rights.
  `true`: the dock runs as administrator (Windows asks at start) so admin panes dock inside it.

## Status

Works with any terminal agent through Settings. Still to come: profiles (several agents,
each new pane picks one) and a ready-built release to download.

## Help and version

Help -> Usage reads `docs\AgentDock.md` from this folder. The version (Help -> About) rises by one with every commit: run `tools\install-hooks.cmd` once in a clone where commits are made.
