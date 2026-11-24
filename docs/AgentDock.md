# Agent Dock

Agent Dock holds several AI agent terminals in one window. Each pane is a Windows Terminal
window running your agent (GitHub Copilot CLI, Claude Code, or any program that runs in a
terminal). The dock pulls those windows inside itself, so you can tile them, maximize one, pop
one out, and close them together.

Open this help with **Help -> Usage and Menu Reference** (F1).

---

## What it needs

- **Windows 10 or 11** with **.NET Framework 4.8** (built into Windows 10 since the May 2019
  update, and into every Windows 11). Nothing to install on most computers.
- **Windows Terminal**, because every pane is a Windows Terminal window.
- **Your agent**, for example GitHub Copilot CLI (`copilot`) or Claude Code (`claude`).
- **git**, only for Settings -> Update Agent Dock.

**When something is missing, the dock tells you.** As it starts, it checks for Windows Terminal,
.NET Framework 4.8 and the agent your panes run. If one is missing, it names it and offers to
copy install instructions for an AI agent to the clipboard: paste them into Copilot, Claude or
any agent, and it can walk you through the install. The dock opens either way. If the dock
cannot start at all, it offers the error details the same way. If .NET itself is missing,
`build.cmd` says so and puts the instructions on the clipboard, since the dock cannot run
without it.

---

## Getting started

1. Build it once: double-click `build.cmd` in the Agent Dock folder.
2. Start `bin\AgentDockStandalone.exe` (a shortcut to it works too).
3. **Agents -> Spawn Additional Pane** (Ctrl+N) opens a pane running your agent.
4. Open **Settings -> Agent Dock Settings** to choose the agent and the folder it starts in.

The first time it runs, the dock makes a `config` folder holding this machine's settings. It
starts out running GitHub Copilot CLI (`copilot`) in your profile folder.

---

## Settings

**Settings -> Agent Dock Settings...** changes everything in plain fields:

| Field | What it does |
|---|---|
| Agent command | What each new pane runs, for example `copilot` or `claude`. |
| Start folder | Where each new pane starts. **Browse...** picks it. |
| Hide the title bar on each pane | On (the default): panes have no title bar, just a thin frame, so the terminal gets one more row. Off: each pane has its own title bar with a close button and an Undock menu. |
| Start the dock as administrator | Off (the default): the dock starts with normal rights and Windows does not ask. Asking for an admin pane offers to restart it as administrator then. On: the dock starts as administrator (Windows asks), so admin panes open with no restart. Panes are never admin unless you ask for one. See **Admin panes**. |
| Show the compose box | Off (the default): there is no compose box. On: a text box across the bottom of the dock for writing prompts. See **The compose box**. |

**OK** saves, **Cancel** or Escape changes nothing. New panes use the new command and folder.
The title bar and compose box changes show at once. Changing **Start the dock as administrator** offers
to restart the dock right away; your open panes keep running and come back in.

**Settings -> Open Config Folder** opens the folder holding the settings in Explorer.

**Settings -> Show Compose Box** turns the compose box on or off in one click, no dialog. The tick
shows whether it is on, and the dock remembers it.

**Settings -> Update Agent Dock** gets the newest version. The dock asks first, and the question
says which version you are on now. Then:

1. Your open panes go to the desktop. Their sessions keep running.
2. The dock closes, and a window shows the progress: it gets the newest code with git, then builds it.
3. The new dock starts and takes the panes back in.

If anything goes wrong (git is not installed, the folder was downloaded as a ZIP instead of
cloned, or the build fails), the window says why and the dock you had starts again. You can
also run `update.cmd` by hand with the dock closed.

**Updating from outside the dock.** `tools\Update-RunningDock.ps1` does the same update with no
question asked. It is there for an AI agent that has just changed the dock: it runs the script
and the dock you are using brings itself up to date, panes and all. An update is always the same
sequence: every pane is released to the desktop still running, the dock closes, the new version is
pulled and built, and the new dock takes every terminal back in. With several docks running, the
others release their panes and close first, so the one new dock holds them all. A dock running as
administrator is preferred for the update, so admin terminals come back in too. If no dock is
running, the script only builds. It waits while one of the dock's own dialogs is open. Docks older
than 1.0.27 cannot take part, so update those once from the menu.

---

## Menu reference

### File
- **Exit** (Alt+F4): closes the dock. See **Closing the dock** below.

### Agents

| Item | Shortcut | What it does |
|---|---|---|
| Spawn 1 / 2 / 3 / 4 Panes | | Brings the number of panes **up to** that many. Terminals already open on the desktop are taken in first, then new panes are started for the rest. |
| Spawn Additional Pane | Ctrl+N | Always adds one new pane, however many there are. |
| Spawn Additional Admin Pane | Ctrl+Shift+N | One new pane running as administrator. If the dock is not running as administrator, it offers to restart as administrator first. See **Admin panes** below. |
| Spawn Unparented Pane | Ctrl+U | Starts one more terminal the same way as a pane (same agent, same folder) but leaves it on the desktop. |
| Absorb All Running Terminals | Ctrl+Shift+A | Pulls every open Windows Terminal window into the dock. |
| Unparent Active Pane | Ctrl+Shift+U | Sends the pane you last clicked out to the desktop, still running. The rest re-tile. |
| Close Active Pane | | Closes the pane you last clicked, after asking. A pane the dock started ends its session. A terminal the dock took in goes back to the desktop, still running. |
| Unparent All | | Sends every pane out to the desktop, still running, then closes the dock. |
| Close Dock & Terminate All | | Closes the dock **and** ends every session. Asks first. |

### Window
- **Tile Vertical**, **Tile Horizontal**, **Cascade**, **Quadrant Layout**: lay the panes out.
- **Reorder Panes**: a list of the panes in order. Select one, Move Up / Move Down, OK.
- **Reverse Pane Order**: flips the order.
- **Move Pane Earlier** (Ctrl+Shift+Left) / **Move Pane Later** (Ctrl+Shift+Right): moves the pane you last clicked. While you are writing in the compose box these keys select words
there instead.
- **Maximize / Restore Pane** (Ctrl+Shift+M): the pane you last clicked fills the dock. The same again puts the tiling back.

### Settings
- **Show Compose Box**, **Agent Dock Settings...**, **Open Config Folder**, **Update Agent Dock**: see **Settings** above.

### Help
- **Usage and Menu Reference** (F1): this help.
- **About**: version and build date.

**"The pane" always means the one you last clicked**, even when the click landed inside the
terminal.

**Shortcuts work while you are typing in a pane.** The dock catches them first whenever it is
the window in front, so the terminal never sees them. That also means Ctrl+N and Ctrl+U do not
reach your agent while the dock is in front.

---

## The compose box

Turn it on with **Settings -> Show Compose Box** (or in **Settings -> Agent Dock Settings**). Off, the dock shows
no compose box at all.

It is a plain Windows text box across the bottom of the dock. Write your prompt there the way
you write in any Windows app: click anywhere in the text, select with the mouse, Ctrl+A, Ctrl+X,
Ctrl+C, Ctrl+V, Ctrl+Z. The usual keys move and select: Ctrl+Left / Right by word, Home / End,
Ctrl+Home / End, and with Shift each of them selects (Ctrl+Shift+Left selects a word, Ctrl+Shift+End
to the end).

- **Enter** (or **Send**) types the prompt into the agent of the pane you last clicked and sends
  it. The box empties and is ready for the next one.
- **Shift+Enter** starts a new line. A prompt with several lines reaches the agent as one prompt.
- **Up** on the first line brings back earlier prompts, newest first. **Down** goes forward again,
  back to what you were writing. With Claude Code in the pane, these are every prompt that pane's
  session was sent, whether you wrote it in the box or typed it straight into the terminal (read
  from Claude Code's own prompt history). That includes prompts sent while the box was off, and the
  ones from before a `/clear` in that pane. Prompts come back with their line breaks, as they were
  written. With another agent, they are the prompts sent from the box.
  Pick another pane while stepping back, and Up starts again with that pane's own prompts.
- **The bar above the box** resizes it: drag it up for a taller box and smaller panes, down for the
  other way. The dock remembers the height.
- **Send to**, under **Send**, lists the panes and shows which one the prompt goes to. It follows
  the pane you click in. Pick a different pane in the list to send there instead.
  Each pane is listed by its terminal's title, so renaming a tab in Windows Terminal renames
  the pane in the list too.
- You can still type in the terminals directly. The answers show in the panes as always.

If a prompt cannot be sent, it stays in the box and the status bar says why. A pane running as
administrator can only be reached while the dock itself runs as administrator.

---

## Admin panes

**Panes are never admin unless you ask for one.** Out of the box the dock itself also runs with
normal rights, and Windows does not ask when it starts. That is the safer way to run it.

**Agents -> Spawn Additional Admin Pane** (Ctrl+Shift+N) is always there. Windows does not let a
normal program take in a window running as administrator, so when the dock runs with normal
rights it asks first:

- **Yes:** the dock restarts as administrator (Windows asks), and the admin pane opens in it.
  Your open panes keep running and come back in.
- **No:** the admin pane opens as a separate window.
- **Cancel:** nothing happens.

If you use admin panes often, turn on **Settings -> Agent Dock Settings -> Start the dock as
administrator**. The dock then starts as administrator (Windows asks each time; say No and it
runs normally), and admin panes open with no restart. Regular panes still start with normal
rights. Admin panes show `[ADMIN]` in their title.

**Become admin** (Claude Code only): a pane can ask the dock to reopen its conversation as an
admin pane. The dock asks you first. Other agents cannot do this yet.

---

## Tab colors

Every terminal the dock opens or takes in gets its own tab color, picked at random from Windows
Terminal's twelve colors, with no repeats until all twelve are used. A terminal that already
had its own tab color keeps it.

---

## Closing the dock

The close button, File -> Exit and Alt+F4 all ask first:

- **Yes**: every pane goes back to the desktop, still running, and the dock closes.
- **No**: nothing changes.

**Unparent All** does the same without asking. **Close Dock & Terminate All** ends every session.

---

## Starting it from a shortcut or a command line

```
AgentDockStandalone.exe                  An empty dock
AgentDockStandalone.exe --spawn 4        Start with 4 panes
AgentDockStandalone.exe --absorb         Take in the Windows Terminal windows already open
AgentDockStandalone.exe --bat <file>     New panes run this batch file instead of config\pane.bat
AgentDockStandalone.exe --cwd <folder>   The dock's working folder
AgentDockStandalone.exe --help           Show these options
```

---

## Troubleshooting

| What you see | Why | What to do |
|---|---|---|
| New Pane says the pane batch file was not found | No `config\pane.bat` yet | Open **Settings -> Agent Dock Settings**, fill in the agent command and start folder, OK. |
| A pane opens and closes at once, or says the command is not recognized | The agent is not installed, or not on the PATH | In a normal command prompt, type the agent command (for example `copilot`). Install it, or fix the command in Settings. |
| Update says git is not installed, or the folder is not a git clone | The dock was downloaded as a ZIP | Download the newest ZIP and run `build.cmd`, or install git and clone the repository. |
| `build.cmd` says it could not write the exe | The dock is running and has the exe open | Close the dock, then build. Or use Settings -> Update Agent Dock, which closes it for you. |
| Absorb All says no terminals were found | No Windows Terminal windows are open | Open one (Windows key, type `wt`), then try again. |
| Clicking a pane does not bring the dock forward | The mouse hook did not install (security software) | Restart the dock. |

---

## Technical details

- **Folder layout:** `bin\` (the built program, ignored by git), `config\` (this machine's
  settings, ignored by git), `docs\` (this help), `samples\` (example pane batch files),
  `build.cmd`, `update.cmd`, and the C# source.
- **Config files:** `config\pane.bat` is what each new pane runs (a `cd /d "<folder>"` line and
  the agent command). It is made from `samples\Copilot.bat` the first time. `config\agentdock.ini`
  holds the other settings as `Name = value` lines. Settings edits both, and both can also be
  edited by hand.
- **How a pane starts:** `wt.exe --window new cmd /k "<pane.bat>"`, then the new Windows
  Terminal window is taken into the dock with `SetParent`.
- **Build:** `build.cmd` uses the C# compiler built into Windows (.NET Framework 4.8), so no
  Visual Studio is needed. The code stays within C# 5 for that reason. Visual Studio or MSBuild
  can build `AgentDockStandalone.csproj` too (output in `bin\Release\`).
- **Help:** this file is read from `docs\AgentDock.md` in the Agent Dock folder each time Help is opened.
