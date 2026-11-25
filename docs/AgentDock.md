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

A dock that is still running (another dock window, or one slow to close) no longer stops the
build. Windows will not overwrite a running program, so the build renames it
`bin\AgentDockStandalone.old.exe` and puts the new one in its place. That dock keeps running the
old version until it is closed. Every dock started after that gets the new one.

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
- **Conversation Panes**: see **Conversation panes** below.
- **Hide Working Notes**, **Palette**, **Compose Box Theme**, **Bubble Style**, **Pane Theme**: see **The compose box** below.

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
- **Slash commands:** type **/** as the first thing in the box and the agent's own command list
  opens above it, the same list the agent shows in its terminal: its commands, your skills, the
  project's skills and plugins, matched its way. The dock keeps the agent's input line the same as
  the box while you type a command (never pressing Enter) and reads the list off its screen.
  **Up / Down** move the agent's pick, **Tab** or **Enter** put the picked command in the box with a
  space after it, and the agent's hint for what the command takes (for example
  `[low|medium|high]`) shows above the box. Type the arguments, then Enter sends it. A command typed
  in full is sent by Enter at once. **Escape** closes the list, and emptying the box empties the
  agent's input line too. If the pane's screen cannot be read, the dock shows its own list instead.
  Works in the compose box and in every conversation pane.
- **Shift+Enter** starts a new line. A prompt with several lines reaches the agent as one prompt.
- **Up** on the first line brings back earlier prompts, newest first. **Down** goes forward again,
  back to what you were writing. With Claude Code in the pane, these are every prompt that pane's
  session was sent, whether you wrote it in the box or typed it straight into the terminal (read
  from Claude Code's own prompt history). That includes prompts sent while the box was off, and the
  ones from before a `/clear` in that pane. Prompts come back with their line breaks, as they were
  written. With GitHub Copilot CLI in the pane, the dock reads Copilot's own session records the
  same way (new in 1.0.31, and not yet tried against a live Copilot). With any other agent, they
  are the prompts sent from the box.
  Pick another pane while stepping back, and Up starts again with that pane's own prompts.
- **What Up steps through** is the small list above Send to: **Up: my prompts** (the default, as
  above), **Up: conversation** (your prompts and the agent's answers, in order) or **Up: answers**
  (only the agent's answers). The dock remembers the choice. The status bar says what each step
  is ("Your prompt" or "Agent's answer") and where you are ("12 of 40"). Up leaves the cursor at
  the top of the text, so a long answer does not need an Up for every line before the next step.
  Conversation and answers come from Claude Code's or GitHub Copilot CLI's record of the session.
- **Run conversation**, the last choice in that list, shows the conversation of the pane in Send
  to above the text, as chat bubbles. It reads from the bottom up: it opens at the newest message,
  at the bottom, with older ones above it, and follows each new message as it arrives. Scroll up
  to read older ones and it stays where you are; scroll back to the bottom and it follows again.
  Pick another pane in Send to (or click inside one) and the conversation empties at once, then
  shows that pane's conversation, opening at its newest message. What you have typed stays. Your prompts sit at the right in
  rounder bubbles, the agent's answers at the left in squarer ones, each kind in its own color and
  outlined. Only what was said shows: no thinking and no tool calls. It stays current while the
  agent works (it looks every second or two), and stays at the bottom unless you have scrolled up
  to read. A long message is cut short in its bubble. **Right-click any bubble** for **Popup** (the whole message in the answer window, where you can select, edit and copy) or **Copy** (the whole message). A left click does nothing. Drag the bar between the conversation and the text to
  give either more room. The box grows to make room when Run conversation is picked, and goes back
  to its height when you pick another choice. While it is on, Up steps through your prompts.
- **The agent's state** shows at the right of the status bar while the compose box is on: the pane
  in Send to and **Thinking...** (amber) while it works on your prompt, or **Idle** (green) when it
  is waiting for you. It is checked about once a second. In Run conversation an **Agent is
  working...** marker also sits under the last message until the agent has finished its answer.
  It works for Claude Code and GitHub Copilot CLI. Copilot writes down no "finished" mark, so for
  Copilot the dock works it out from what was written last (a turn that ran tools counts as still
  working, until two minutes pass with nothing new). With another agent nothing shows.
- **`/clear` empties Run conversation too.** Type `/clear` in a pane (in the terminal or the
  compose box) and its conversation above the text starts empty, like the terminal, for Claude
  Code and Copilot alike. Up still reaches the prompts from before it.
- **Full Screen**, under Send, hides the panes and lets the compose box fill the dock, so with Run
  conversation picked the conversation is all you see. The button then reads **Dock**: press it to
  bring the panes back, and it reads Full Screen again. The agents keep running behind it and Send
  still reaches the pane in Send to. Hiding the compose box also brings the panes back.
- **Clear Screen**, beside Full Screen, wipes the screen and the scrollback of the pane in Send to,
  the way `cls` does, and empties the conversation shown above the text: from then on Run
  conversation shows only what is said after the clear. The agent keeps its whole conversation, so
  it is safe at any moment, even while the agent works. Its prompt line comes back when you type or
  it next writes. One limit: Claude Code draws its whole conversation again in the terminal whenever
  the terminal changes size, so a re-tile or a resized dock brings the old terminal screen back.
  Press Clear Screen again then.
- **Settings -> Hide Working Notes** (also in the Palette) hides the notes the agent writes while it
  works ("Looking at the log now", "Building..."): of a run of agent messages, only the last one
  shows. Untick it to see them all again. It is for every pane, and the dock remembers it.
- **What you send shows at once** at the bottom of the conversation, before the agent has taken
  it up. The conversation is read from the agent's own record, and an agent writes a message typed
  while it works into its record only when it gets to it. Then the message moves to where the
  agent took it. Commands such as `/model` are not echoed.
- **A resumed session** (`/resume`, or starting the agent with `--resume`) shows its whole
  conversation. For GitHub Copilot CLI the dock follows the session Copilot is writing to, which
  can take up to ten seconds after a resume.
- **Messages you type while the agent is working** (Claude Code keeps them apart from ordinary
  prompts) show in the conversation as your prompts, where the agent received them.
- **How it looks** is four choices: **Colors** (44 themes, from Windows, the plain look, through
  light ones like Daylight, Paper, Mint and Sky to dark ones like Dracula, Tokyo Night, Monokai and
  Synthwave), **Bubbles** (21 styles from plain to fancy: Plain, Notebook, Minimal, Compact, Boxed,
  Classic, the default, Roomy, Outline, Dashed, Soft, Pill, Accent, Double, Chat, Solid, Card,
  Shadowed, Glow, Neon, Comic and Fancy), **Font** (the conversation's letters) and **Prompt Font**
  (the compose box's letters). Each font comes with a **size**, from 7 to 24 points: picking a font
  puts its usual size in the Size list under it, and any other size can be picked from there.
  Colors and bubbles are also on the Settings menu as **Compose Box Theme** and **Bubble Style**.
- **Settings -> Palette** is where to try them: four lists, a Size list under each font list, and a live preview of a short
  conversation. Step through a list with the arrow keys and the preview changes at once. Pick
  **Global look** or a pane in **Pane**, then **Save for All Panes** (the global look) or **Save for
  Pane** (that pane's own). **Use Global** puts the pane back on the global look. Nothing changes
  until you save.
- **The global look applies to every pane** unless a pane has its own. A pane's look belongs to its
  **place** in the dock: pane 1 always opens in pane 1's look, pane 2 in pane 2's, whatever terminal
  is there, until you press **Use Global** for it. So the panes can always load in different styles.
  A pane can override any of the four and follow the global look in the rest. **Settings -> Pane
  Theme** (for the pane you last clicked) has the same choices as menus, plus Use Global and Palette.
  Each font menu ends in **Size**, which changes the size of the font the pane shows now.
  The compose box shows each pane in its own look as you move between panes.

- **Copy**, beside Popup Answer, copies what is selected in the box, or all of it when nothing is.
- **The bar above the box** resizes it: drag it up for a taller box and smaller panes, down for the
  other way. The dock remembers the height.
- **The bar between the text and Send** sets how much room each gets: drag it left for a wider
  Send to list, right for more text. The dock remembers the width.
- **The thin strip at the box's right edge** hides the options (Send, Popup Answer, Copy, Full
  Screen, Clear Screen, Send to): click **>** and the text and conversation take the whole width,
  click **<** to bring them back. Enter still sends while they are hidden. The dock remembers it,
  and each conversation pane remembers its own (its header's Hide Options / Show Options does the
  same).
- **Send to**, under **Send**, lists the panes and shows which one the prompt goes to. It follows
  the pane you click in. Pick a different pane in the list to send there instead.
  Each pane is listed by its terminal's title, so renaming a tab in Windows Terminal renames
  the pane in the list too. Each pane is shown in its tab's color, read from the tab itself
  while the dock is in front (a newly opened pane may take a couple of seconds to get its color).
- **Popup Answer**, beside **Send**, opens the most recent answer of the pane in Send to in a
  window of its own. It is a plain text box like the compose box: click anywhere, select with the
  mouse or the usual keys, edit it. **Copy** puts the whole text, with your edits, on the clipboard.
  Close it with **Close** or Escape (Escape asks first if you changed the text). Your edits are not
  kept anywhere. It reads the answer from Claude Code's or GitHub Copilot CLI's own record of the
  session, so it shows the answer as the agent wrote it, without the terminal's wrapping. While the
  agent is still working, it shows the latest thing it has written so far.
- **Keys**, beside Clear Screen, is for menus. See *Menus and the Keys window* below.
- You can still type in the terminals directly. The answers show in the panes as always.

If a prompt cannot be sent, it stays in the box and the status bar says why. A pane running as
administrator can only be reached while the dock itself runs as administrator.

---

## Conversation panes

**Settings -> Conversation Panes** hides every terminal and shows each one as its conversation
instead: one conversation pane per terminal, in the dock's pane order. Each has its own text box,
Send, Popup Answer, Copy, Clear Screen and Keys, and sends only to its own terminal. Its header shows the
pane's number and name on its tab's color and **Thinking...** or **Idle**, and has these buttons:

- **Theme**: that pane's own colors, bubbles and fonts, Use Global, and the Palette (a right-click
  on the header opens the same menu).
- **Use Global**: shows while the pane has a look of its own. One click puts its place back on the
  global look.
- **Hide Options / Show Options**: hides the column of options at the right of that pane (Send,
  Popup, Copy, Clear) for more room, and shows it again. Enter still sends. Remembered for that
  pane's place. The **>** strip at the pane's right edge does the same.
  A new pane starts with the options hidden.
- **Close**: closes the pane and its terminal together, after asking. The agent session in it ends.
  Use Agents -> Unparent Active Pane instead to keep the terminal running on the desktop.

**The Window menu lays them out** as it does the terminals: Tile Vertical, Tile Horizontal,
Cascade, Quadrant Layout, and Maximize / Restore Pane (the pane you last wrote in fills the dock).
In Cascade the one you click in comes to the front. Writing in a conversation pane makes it the pane
you last clicked, so **Agents -> Close Active Pane** and the rest act on it.

Until the agent in a pane has started a session, its conversation pane shows the terminal's own
screen, so a question the agent asks before it starts (a folder to trust, a sign-in) can be seen and
answered (in the Keys window). A pane just opened says **New session** until something has been said in it: the agent keeps
no record before the first message. Write in its box and the conversation appears.

The terminals keep running, hidden. A pane opened or closed while conversation panes are on gets or
loses its conversation pane within a second. The dock's own compose box is hidden meanwhile and comes
back as it was when you turn conversation panes off. The dock remembers the setting.

## Menus and the Keys window

Some commands open a menu in the terminal instead of answering: `/resume`, `/skills`, `/model`,
`/config` and the like, and an agent's question with choices or a permission prompt. A menu wants
the arrow keys, Enter and Esc, which a typed prompt cannot give, and in conversation panes the
terminal is hidden. The **Keys** window is for them.

- **It opens by itself** when the pane you are writing to shows a menu, a second or two after it
  appears. It shows the pane's screen as it is now, and **every key you press in it goes to the
  pane**: the arrows, Enter, Esc, Tab, Space, Backspace, letters (many menus have a search box) and
  Ctrl+letter. The buttons along its bottom (Up, Down, Left, Right, Enter, Esc, Tab, Space, 1 to 4)
  do the same for the mouse.
- **It closes by itself** once the pick is made and the menu is gone. Close it yourself any time.
- **When it opens by itself:** always for a conversation pane, since its terminal is hidden. For the
  dock's own compose box, when you were writing in the box or sent from it in the last half minute,
  because otherwise you may be working in the terminal itself. It takes the keyboard only if you were
  writing in the dock.
- **The Keys button** (on the compose box, and on each conversation pane) opens it at any time.
- Untick **Open by itself when a pane shows a menu** in the window to have it open only from the
  button. The dock remembers it.

It spots a menu by the hint line agents print under one ("Enter to select", "Esc to cancel", "Esc to
close").

## Popups and Resend

**Popup Answer** and a bubble's right-click **Popup** open the message in a window you can edit.
**Resend** sends the text as it is now, with your changes, to that pane as a new prompt, and the
window closes. **Copy** copies it, **Close** or Escape closes it.

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
- **The compose box and the Keys window** reach a pane through its console: the dock attaches to
  the console behind the Windows Terminal window, writes key events to its input (prompts, and the
  codes a terminal sends for the arrows, Esc and the like) and reads its screen back.
- **Mouse and keyboard hooks:** the dock watches clicks and its shortcut keys with Windows
  low-level hooks. They run on threads of their own, so a busy dock never slows the cursor or
  typing elsewhere. The agents' records are read off the window's thread too, and GitHub Copilot
  CLI's session folders are looked through at most once every ten seconds, however many panes
  there are.
- **Help:** this file is read from `docs\AgentDock.md` in the Agent Dock folder each time Help is opened.
