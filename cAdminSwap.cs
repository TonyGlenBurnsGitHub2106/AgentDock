using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// "Become admin" -- a running Claude pane restarts as an administrator pane and resumes the same
// conversation (2026-11-12).
//
// Both docks compile this one file: AgentDockStandalone owns it and AgentDock links it, the same
// arrangement as cPaneArranger.cs and cTabColor.cs. It is self-contained -- its own P/Invokes.
//
// How it works:
//   1. A pane runs the become-admin skill, whose script drops a small request file into
//      <source>\LeonardoConsolidated\XmlDataStorage\AgentDock\AdminRequests\ holding three lines:
//      session=<conversation id>, pid=<the pane's claude.exe process id>, cwd=<its folder>.
//   2. The dock polls that folder once a second. On a request it ASKS the user (Yes / No) -- this
//      dialog is the gate that stands in for the UAC prompt, because the dock is already elevated
//      and anything that can write a file could otherwise get an admin pane.
//   3. On Yes it waits for the conversation's transcript to go quiet (the turn has ended), closes
//      the old pane's terminal window, waits for the old claude.exe to exit (two processes must
//      never write the same transcript), then opens an admin pane running
//      `claude --dangerously-skip-permissions --resume <id>` in the same folder.
//   4. Each request file is moved to AdminRequests\Done\ afterwards (never deleted).
//
// The old pane's terminal window is found from the claude.exe process: attach to its console,
// and the console window's OWNER is the Windows Terminal window hosting the tab (the same
// technique cTabColor uses). Posting WM_CLOSE to that window ends the tab and its processes;
// when the window is absorbed in the dock, the pane's watchdog then closes the empty MDI child.
// If the window cannot be found the dock kills the claude.exe process instead.

namespace AgentDockShared {

    public sealed class cAdminSwap : IDisposable {

        public const string CNST_REQUEST_SUBFOLDER = @"LeonardoConsolidated\XmlDataStorage\AgentDock\AdminRequests";
        private const string CNST_REQUEST_PATTERN  = "*.req";
        private const string CNST_CLAUDE_EXE       = "claude";

        private const int CNST_POLL_MS           = 1000;
        private const int CNST_STEP_MS           = 500;
        private const int CNST_QUIET_SEC         = 8;    // transcript unchanged this long = turn over (a model can think 5 s+ before its last reply)
        private const int CNST_QUIET_MAX_SEC     = 120;  // give up waiting for quiet after this
        private const int CNST_EXIT_MAX_SEC      = 15;   // then kill the old claude.exe

        private enum SwapStep { Idle, WaitQuiet, WaitExit }

        private readonly Form                   mfrmOwner;
        private readonly string                 mstrRequestDir;
        private readonly Action<string, string> mactSpawnResume;   // (sessionId, cwd)
        private readonly Action<string>         mactStatus;
        private readonly Timer                  mtmrPoll;
        private readonly Timer                  mtmrStep;

        private SwapStep mstep = SwapStep.Idle;
        private string   mstrReqFile;
        private string   mstrSession;
        private string   mstrCwd;
        private int      mintPid;
        private string   mstrTranscript;
        private DateTime mdtStepStart;
        private bool     mblnBusy;

        public cAdminSwap(Form afrmOwner, string astrSourceRoot,
                          Action<string, string> aactSpawnResume, Action<string> aactStatus) {
            mfrmOwner       = afrmOwner;
            mstrRequestDir  = Path.Combine(astrSourceRoot, CNST_REQUEST_SUBFOLDER);
            mactSpawnResume = aactSpawnResume;
            mactStatus      = aactStatus;

            try { Directory.CreateDirectory(mstrRequestDir); } catch { }

            mtmrPoll          = new Timer();
            mtmrPoll.Interval = CNST_POLL_MS;
            mtmrPoll.Tick    += new EventHandler(Poll_Tick);
            mtmrPoll.Start();

            mtmrStep          = new Timer();
            mtmrStep.Interval = CNST_STEP_MS;
            mtmrStep.Tick    += new EventHandler(Step_Tick);
        }

        public void Dispose() {
            mtmrPoll.Stop();
            mtmrPoll.Dispose();
            mtmrStep.Stop();
            mtmrStep.Dispose();
        }

        // -------------------------------------------------------------------
        // Request intake

        private void Poll_Tick(object sender, EventArgs e) {
            if (mblnBusy || mstep != SwapStep.Idle || mfrmOwner.IsDisposed) return;
            string[] astrFiles;
            try {
                if (!Directory.Exists(mstrRequestDir)) return;
                astrFiles = Directory.GetFiles(mstrRequestDir, CNST_REQUEST_PATTERN);
            } catch { return; }
            if (astrFiles.Length == 0) return;

            Array.Sort(astrFiles, StringComparer.OrdinalIgnoreCase);
            mblnBusy = true;   // the Yes / No dialog pumps messages; keep the poll from re-entering
            try {
                StartRequest(astrFiles[0]);
            } finally {
                mblnBusy = false;
            }
        }

        private void StartRequest(string astrFile) {
            string strSession = null, strCwd = null, strPid = null;
            try {
                foreach (string strLine in File.ReadAllLines(astrFile)) {
                    int intEq = strLine.IndexOf('=');
                    if (intEq <= 0) continue;
                    string strKey = strLine.Substring(0, intEq).Trim().ToLowerInvariant();
                    string strVal = strLine.Substring(intEq + 1).Trim();
                    if (strKey == "session") strSession = strVal;
                    else if (strKey == "cwd") strCwd = strVal;
                    else if (strKey == "pid") strPid = strVal;
                }
            } catch (IOException) {
                return;   // still being written -- try again next tick
            } catch (Exception ex) {
                Finish(astrFile, "unreadable", "Admin request could not be read: " + ex.Message);
                return;
            }

            string strProblem = Validate(strSession, strCwd, strPid);
            if (strProblem != null) {
                Finish(astrFile, "refused", "Admin request refused: " + strProblem);
                return;
            }

            DialogResult dr = MessageBox.Show(mfrmOwner,
                "A Claude Code pane asks to restart as ADMINISTRATOR.\r\n\r\n"
                + "Folder:  " + strCwd + "\r\n"
                + "Conversation:  " + strSession + "\r\n\r\n"
                + "Yes closes that pane when its turn ends and reopens the same conversation "
                + "in an admin pane, with full administrator rights and no permission prompts.\r\n\r\n"
                + "Allow it?",
                "Agent Dock - Become Admin", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (dr != DialogResult.Yes) {
                Finish(astrFile, "declined", "Admin request declined.");
                return;
            }

            mstrReqFile    = astrFile;
            mstrSession    = strSession;
            mstrCwd        = strCwd;
            mintPid        = Int32.Parse(strPid);
            mstrTranscript = FindTranscript(strSession);
            mdtStepStart   = DateTime.Now;
            mstep          = SwapStep.WaitQuiet;
            mactStatus("Become admin: waiting for the pane's turn to end...");
            mtmrStep.Start();
        }

        // Every value lands on an elevated command line, so each is checked strictly.
        // Returns null when the request is good, otherwise what is wrong with it.
        private string Validate(string astrSession, string astrCwd, string astrPid) {
            Guid g;
            if (string.IsNullOrEmpty(astrSession) || !Guid.TryParseExact(astrSession, "D", out g))
                return "the conversation id is not a GUID.";
            if (string.IsNullOrEmpty(astrCwd) || astrCwd.IndexOfAny("\"&|<>^%!;\r\n".ToCharArray()) >= 0)
                return "the folder is missing or holds characters that are not allowed.";
            if (!Directory.Exists(astrCwd))
                return "the folder does not exist: " + astrCwd;
            int intPid;
            if (!Int32.TryParse(astrPid, out intPid) || intPid <= 0)
                return "the process id is not a number.";
            try {
                using (Process p = Process.GetProcessById(intPid)) {
                    if (!string.Equals(p.ProcessName, CNST_CLAUDE_EXE, StringComparison.OrdinalIgnoreCase))
                        return "process " + intPid + " is not claude.exe.";
                }
            } catch (ArgumentException) {
                return "process " + intPid + " is not running.";
            }
            if (FindTranscript(astrSession) == null)
                return "no saved conversation has that id.";
            return null;
        }

        // %USERPROFILE%\.claude\projects\<project>\<id>.jsonl, or null.
        private static string FindTranscript(string astrSession) {
            try {
                string strProjects = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
                if (!Directory.Exists(strProjects)) return null;
                foreach (string strDir in Directory.GetDirectories(strProjects)) {
                    string strFile = Path.Combine(strDir, astrSession + ".jsonl");
                    if (File.Exists(strFile)) return strFile;
                }
            } catch { }
            return null;
        }

        // -------------------------------------------------------------------
        // The swap itself

        private void Step_Tick(object sender, EventArgs e) {
            if (mfrmOwner.IsDisposed) { mtmrStep.Stop(); return; }
            double dblElapsed = (DateTime.Now - mdtStepStart).TotalSeconds;

            if (mstep == SwapStep.WaitQuiet) {
                bool blnQuiet = true;
                try {
                    if (mstrTranscript != null && File.Exists(mstrTranscript))
                        blnQuiet = (DateTime.Now - File.GetLastWriteTime(mstrTranscript)).TotalSeconds >= CNST_QUIET_SEC;
                } catch { }
                if (!blnQuiet && dblElapsed < CNST_QUIET_MAX_SEC) return;
                if (!blnQuiet) mactStatus("Become admin: the pane is still busy -- closing it anyway.");

                CloseOldPane(mintPid);
                mdtStepStart = DateTime.Now;
                mstep        = SwapStep.WaitExit;
                return;
            }

            if (mstep == SwapStep.WaitExit) {
                if (IsRunning(mintPid)) {
                    if (dblElapsed < CNST_EXIT_MAX_SEC) return;
                    KillQuietly(mintPid);
                    if (IsRunning(mintPid)) {
                        mtmrStep.Stop();
                        mstep = SwapStep.Idle;
                        Finish(mstrReqFile, "failed",
                            "Become admin: the old pane would not close, so nothing was reopened.");
                        return;
                    }
                }
                mtmrStep.Stop();
                mstep = SwapStep.Idle;
                try {
                    mactSpawnResume(mstrSession, mstrCwd);
                    Finish(mstrReqFile, "done", "Become admin: reopening the conversation in an admin pane...");
                } catch (Exception ex) {
                    Finish(mstrReqFile, "failed", "Become admin: could not open the admin pane: " + ex.Message);
                }
            }
        }

        // WM_CLOSE to the Windows Terminal window hosting the pane, or a kill when it cannot be found.
        private static void CloseOldPane(int aintPid) {
            IntPtr hwndTerminal = FindTerminalWindow(aintPid);
            if (hwndTerminal != IntPtr.Zero && IsWindow(hwndTerminal)) {
                PostMessage(hwndTerminal, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            } else {
                KillQuietly(aintPid);
            }
        }

        private static IntPtr FindTerminalWindow(int aintPid) {
            FreeConsole();
            if (!AttachConsole((uint)aintPid)) return IntPtr.Zero;
            try {
                IntPtr hwndConsole = GetConsoleWindow();
                if (hwndConsole == IntPtr.Zero) return IntPtr.Zero;
                return GetWindow(hwndConsole, GW_OWNER);
            } finally {
                FreeConsole();
            }
        }

        private static bool IsRunning(int aintPid) {
            try {
                using (Process p = Process.GetProcessById(aintPid)) return !p.HasExited;
            } catch { return false; }
        }

        private static void KillQuietly(int aintPid) {
            try {
                using (Process p = Process.GetProcessById(aintPid)) {
                    p.Kill();
                    p.WaitForExit(3000);
                }
            } catch { }
        }

        // Moves the request into Done\ with its outcome in the name -- request files are never deleted.
        private void Finish(string astrFile, string astrOutcome, string astrStatus) {
            mactStatus(astrStatus);
            try {
                string strDone = Path.Combine(mstrRequestDir, "Done");
                Directory.CreateDirectory(strDone);
                string strTarget = Path.Combine(strDone,
                    Path.GetFileNameWithoutExtension(astrFile) + "." + astrOutcome + "."
                    + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                File.Move(astrFile, strTarget);
            } catch { }
        }

        // The command a resumed admin pane runs inside cmd /k. Matches Claude-Code2.bat's flags.
        public static string ResumeCommand(string astrSession, string astrCwd) {
            return "cd /d \"" + astrCwd + "\" && claude --dangerously-skip-permissions --resume " + astrSession;
        }

        // -------------------------------------------------------------------
        // P/Invoke

        private const uint GW_OWNER = 4;
        private const uint WM_CLOSE = 0x0010;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
