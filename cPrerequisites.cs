using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AgentDockStandalone {

    // What Agent Dock needs on a machine, checked when it starts (Story 6817). When something is
    // missing the dock says what, and offers to copy instructions for an AI agent to the clipboard:
    // what to install and from where, so the user can paste them into Copilot, Claude or any
    // agent and let it do the install. A crash while starting gets the same offer (Program.Main).
    //
    // .NET Framework itself cannot be checked here: without it this code never runs, and Windows
    // shows its own "install .NET Framework" box. build.cmd checks for it before building.
    internal static class cPrerequisites {

        private const int CNST_NET48_RELEASE = 528040;   // registry Release value of .NET Framework 4.8

        private sealed class Missing {
            public string Name;      // what it is, in plain words
            public string Why;       // what fails without it
            public string HowToAi;   // install steps for an AI agent
        }

        // Runs the checks; when anything is missing, tells the user and offers the clipboard text.
        // Never throws. The dock opens either way.
        public static void CheckAtStartup(string astrPaneBat) {
            try {
                List<Missing> lst = FindMissing(astrPaneBat);
                if (lst.Count == 0) return;
                StringBuilder sbUser = new StringBuilder();
                sbUser.Append("Agent Dock is missing something it needs on this computer:\r\n\r\n");
                foreach (Missing m in lst) sbUser.Append("- ").Append(m.Name).Append(": ").Append(m.Why).Append("\r\n");
                sbUser.Append("\r\nCopy install instructions for an AI agent to the clipboard?\r\n")
                      .Append("Paste them into Copilot, Claude or any agent and it can install what is missing.\r\n\r\n")
                      .Append("The dock opens either way.");
                DialogResult dr = MessageBox.Show(sbUser.ToString(), "Agent Dock - something is missing",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
                if (dr == DialogResult.Yes) CopyToClipboard(InstructionsForAi(lst, null));
            } catch { /* a check must never stop the dock from opening */ }
        }

        // For Program.Main: the dock failed to start. Offers the details for an AI agent.
        public static void ReportStartupFailure(Exception aex) {
            try {
                DialogResult dr = MessageBox.Show(
                    "Agent Dock could not start:\r\n\r\n" + aex.Message
                    + "\r\n\r\nCopy the details and what to check to the clipboard, for an AI agent to fix?",
                    "Agent Dock", MessageBoxButtons.YesNo, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                if (dr == DialogResult.Yes) CopyToClipboard(InstructionsForAi(FindMissing(null), aex));
            } catch { }
        }

        private static List<Missing> FindMissing(string astrPaneBat) {
            List<Missing> lst = new List<Missing>();

            if (!WindowsTerminalFound()) {
                Missing m = new Missing();
                m.Name = "Windows Terminal";
                m.Why  = "every pane is a Windows Terminal window, so no pane can open";
                m.HowToAi = "Install Windows Terminal (Microsoft). Preferred: `winget install --id Microsoft.WindowsTerminal -e`. "
                          + "Or from the Microsoft Store (\"Windows Terminal\"), or the latest release at "
                          + "https://github.com/microsoft/terminal/releases. Afterwards `wt.exe` must start from a command prompt.";
                lst.Add(m);
            }

            int intRelease = NetFrameworkRelease();
            if (intRelease > 0 && intRelease < CNST_NET48_RELEASE) {
                Missing m = new Missing();
                m.Name = ".NET Framework 4.8";
                m.Why  = "this computer has an older .NET Framework 4 (registry Release " + intRelease + "), so parts of the dock may fail";
                m.HowToAi = "Install .NET Framework 4.8 or 4.8.1 (the runtime) from Microsoft: "
                          + "https://dotnet.microsoft.com/download/dotnet-framework/net48 . On Windows 10 it may also come through "
                          + "Windows Update. It needs a restart. Check: registry HKLM\\SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full, "
                          + "value Release, should be 528040 or higher.";
                lst.Add(m);
            }

            string strAgent = AgentProgram(astrPaneBat);
            if (strAgent != null && !OnPath(strAgent)) {
                Missing m = new Missing();
                m.Name = "the agent \"" + strAgent + "\"";
                m.Why  = "new panes run it (Settings -> Agent Dock Settings), but it was not found on this computer";
                m.HowToAi = AgentInstallSteps(strAgent);
                lst.Add(m);
            }
            return lst;
        }

        private static string InstructionsForAi(List<Missing> alst, Exception aex) {
            StringBuilder sb = new StringBuilder();
            sb.Append("I use Agent Dock (https://github.com/TonyGlenBurnsGitHub2106/AgentDock), a Windows program that ")
              .Append("holds AI agent terminals as panes. It runs on .NET Framework 4.8 and opens each pane as a Windows ")
              .Append("Terminal window running my agent. Please help me get it working on this computer.\r\n\r\n");
            if (aex != null) {
                sb.Append("It failed to start with this error:\r\n").Append(aex.GetType().FullName).Append(": ")
                  .Append(aex.Message).Append("\r\n").Append(aex.StackTrace).Append("\r\n\r\n");
            }
            if (alst.Count > 0) {
                sb.Append("Missing on this computer, with how to install each (check the source is current before installing):\r\n\r\n");
                int i = 1;
                foreach (Missing m in alst) {
                    sb.Append(i++).Append(". ").Append(m.Name).Append(" -- ").Append(m.Why).Append(".\r\n   ")
                      .Append(m.HowToAi).Append("\r\n\r\n");
                }
            } else if (aex == null) {
                sb.Append("Nothing obvious is missing.\r\n\r\n");
            }
            sb.Append("This computer: Windows ").Append(Environment.OSVersion.Version)
              .Append(", .NET Framework 4 registry Release ").Append(NetFrameworkRelease())
              .Append(", 64-bit OS ").Append(Environment.Is64BitOperatingSystem ? "yes" : "no").Append(".\r\n")
              .Append("Agent Dock folder: ").Append(Program.RepoRoot ?? Path.GetDirectoryName(Application.ExecutablePath)).Append("\r\n")
              .Append("Work with the user: explain each install before running it, and ask before installing anything. ")
              .Append("Afterwards, start Agent Dock again (bin\\AgentDockStandalone.exe; rebuild with build.cmd if needed).\r\n");
            return sb.ToString();
        }

        private static string AgentInstallSteps(string astrAgent) {
            string a = astrAgent.ToLowerInvariant();
            if (a == "copilot")
                return "Install GitHub Copilot CLI (the terminal agent, command `copilot`). Official instructions: "
                     + "https://docs.github.com/copilot (search \"Copilot CLI install\"). Usually `npm install -g @github/copilot`, "
                     + "which needs Node.js (https://nodejs.org, or `winget install OpenJS.NodeJS.LTS`); winget may also offer it. "
                     + "Then run `copilot` once in a command prompt and sign in with the GitHub account that has Copilot.";
            if (a == "claude")
                return "Install Claude Code (command `claude`). Official instructions: https://docs.anthropic.com/en/docs/claude-code "
                     + "(native Windows installer, or `npm install -g @anthropic-ai/claude-code` with Node.js from https://nodejs.org). "
                     + "Then run `claude` once in a command prompt and sign in.";
            if (a == "npx" || a == "npm" || a == "node")
                return "Install Node.js LTS from https://nodejs.org (or `winget install OpenJS.NodeJS.LTS`), then open a new command prompt.";
            return "The pane command starts `" + astrAgent + "`, which is not on the PATH. Find out which program that is, install it "
                 + "from its official source, and make sure it runs from a new command prompt. Or change the agent command in "
                 + "Agent Dock -> Settings -> Agent Dock Settings.";
        }

        // ---------------------------------------------------------------- the checks

        private static bool WindowsTerminalFound() {
            if (OnPath("wt")) return true;
            string strApps = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return File.Exists(Path.Combine(strApps, @"Microsoft\WindowsApps\wt.exe"));
        }

        private static int NetFrameworkRelease() {
            try {
                using (RegistryKey k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                                  .OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full")) {
                    object o = k == null ? null : k.GetValue("Release");
                    return o is int ? (int)o : 0;
                }
            } catch { return 0; }
        }

        // The program the pane batch runs: the first word of its command line (the line Settings
        // shows as "Agent command"). Null when there is no batch or no command.
        private static string AgentProgram(string astrPaneBat) {
            if (String.IsNullOrEmpty(astrPaneBat) || !File.Exists(astrPaneBat)) return null;
            string strFolder, strCommand; bool blnPlain;
            if (!String.Equals(Path.GetFullPath(astrPaneBat), Path.GetFullPath(cDockSettings.PaneBatPath ?? astrPaneBat),
                               StringComparison.OrdinalIgnoreCase)) return null;   // a --bat file: not ours to read
            cDockSettings.ReadPaneBat(out strFolder, out strCommand, out blnPlain);
            strCommand = strCommand.Trim();
            if (strCommand.Length == 0) return null;
            if (strCommand.StartsWith("\"")) {
                int intEnd = strCommand.IndexOf('"', 1);
                return intEnd > 1 ? strCommand.Substring(1, intEnd - 1) : null;
            }
            int intSpace = strCommand.IndexOf(' ');
            string strFirst = intSpace < 0 ? strCommand : strCommand.Substring(0, intSpace);
            string strLower = strFirst.ToLowerInvariant();
            if (strLower == "call" || strLower == "start" || strLower.StartsWith("%")) return null;   // too clever to check
            return strFirst;
        }

        // True when a command would be found: a full path that exists, or a name on the PATH with
        // any PATHEXT extension.
        private static bool OnPath(string astrName) {
            try {
                if (astrName.IndexOf('\\') >= 0 || astrName.IndexOf(':') >= 0) return File.Exists(astrName);
                string[] astrExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';');
                string strPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (string strDir in strPath.Split(';')) {
                    string d = strDir.Trim().Trim('"');
                    if (d.Length == 0) continue;
                    if (Path.HasExtension(astrName) && File.Exists(Path.Combine(d, astrName))) return true;
                    foreach (string e in astrExt) {
                        if (e.Length > 0 && File.Exists(Path.Combine(d, astrName + e))) return true;
                    }
                }
            } catch { }
            return false;
        }

        private static void CopyToClipboard(string astrText) {
            try {
                Clipboard.SetText(astrText);
                MessageBox.Show("The instructions are on the clipboard. Paste them into your AI agent.",
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) {
                MessageBox.Show("Could not copy to the clipboard: " + ex.Message, "Agent Dock",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
