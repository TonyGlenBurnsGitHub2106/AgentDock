using System;
using System.IO;
using System.Windows.Forms;

namespace AgentDockStandalone {

    internal static class Program {

        // Standalone -- no Leonardo gate, no silent-exit. Double-clicking the exe
        // always opens an empty dock. Optional flags pre-populate it.
        //
        //   AgentDockStandalone.exe                        Empty dock.
        //   AgentDockStandalone.exe --spawn N              Spawn N panes on startup.
        //   AgentDockStandalone.exe --absorb               Absorb running WT windows.
        //   AgentDockStandalone.exe --bat <path>           Override default bat.
        //   AgentDockStandalone.exe --cwd <path>           Override working dir.

        [STAThread]
        static void Main(string[] args) {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // Anything that stops the dock from starting is caught here and offered to the user
            // as text for an AI agent to fix (Story 6817).
            try {
                Run(args);
            } catch (Exception ex) {
                cPrerequisites.ReportStartupFailure(ex);
            }
        }

        private static void Run(string[] args) {
            int    intSpawn  = 0;
            bool   blnAbsorb = false;
            string strBat    = DefaultBatPath();
            string strRoot   = FindRepoRoot();
            cDockSettings.Load(Path.Combine(strRoot ?? (ExeDir() ?? ""), CNST_CONFIG_FOLDER));
            string strCwd    = DefaultCwd();

            for (int i = 0; i < args.Length; i++) {
                string a = args[i];
                if ((a == "--spawn" || a == "-n") && i + 1 < args.Length) {
                    int parsed;
                    if (Int32.TryParse(args[++i], out parsed) && parsed > 0 && parsed <= 16) {
                        intSpawn = parsed;
                    }
                } else if (a == "--absorb") {
                    blnAbsorb = true;
                } else if ((a == "--bat" || a == "-b") && i + 1 < args.Length) {
                    strBat = args[++i];
                } else if ((a == "--cwd" || a == "-d") && i + 1 < args.Length) {
                    strCwd = args[++i];
                } else if (a == "--wait-pid" && i + 1 < args.Length) {
                    int intPid;
                    if (Int32.TryParse(args[++i], out intPid)) WaitForExit(intPid);
                } else if (a == "--help" || a == "-h" || a == "/?") {
                    ShowUsage();
                    return;
                }
            }

            // Settings -> "Allow administrator panes" (Story 6817, default off). The dock starts
            // with normal rights (app.manifest asInvoker). With the setting on, it restarts itself
            // as administrator so admin panes can dock inside it. When Windows' prompt is
            // declined, the dock carries on with normal rights.
            if (cDockSettings.AllowAdminPanes && !AgentDockShared.cPaneLauncher.IsRunningElevated()) {
                if (RestartAsAdministrator(args)) return;
            }

            // Windows Terminal, the agent and .NET 4.8 are checked; anything missing is offered to
            // the user as install instructions for an AI agent. The dock opens either way.
            cPrerequisites.CheckAtStartup(strBat);

            Application.Run(new fAgentDockMain(intSpawn, blnAbsorb, strBat, strCwd));
        }

        // The command a new pane runs (Story 6817): config\pane.bat. The config folder is
        // git-ignored, so each machine keeps its own. It is looked for beside the exe and up to
        // two folders above it (the exe sits in bin\ or bin\Release\ under the repo root).
        // When the repo has no config\pane.bat yet but has samples\Copilot.bat, the sample is
        // copied in, so the first run just works. --bat on the command line still wins.
        public const string CNST_CONFIG_FOLDER = "config";
        public const string CNST_PANE_BAT      = "pane.bat";
        private const string CNST_SAMPLE_BAT   = @"samples\Copilot.bat";
        private const int    CNST_LEVELS_UP    = 2;

        private static string DefaultBatPath() {
            string strRoot = FindRepoRoot();
            if (strRoot == null) {
                string strExeDir = ExeDir();
                return Path.Combine(Path.Combine(strExeDir ?? "", CNST_CONFIG_FOLDER), CNST_PANE_BAT);
            }
            string strConfigBat = Path.Combine(Path.Combine(strRoot, CNST_CONFIG_FOLDER), CNST_PANE_BAT);
            if (!File.Exists(strConfigBat)) {
                string strSample = Path.Combine(strRoot, CNST_SAMPLE_BAT);
                try {
                    if (File.Exists(strSample)) {
                        Directory.CreateDirectory(Path.GetDirectoryName(strConfigBat));
                        File.Copy(strSample, strConfigBat, false);
                    }
                } catch { }
            }
            return strConfigBat;   // returned even if missing -- New Pane says where it looked
        }

        // Starts this exe again as administrator with the same arguments. False when Windows'
        // prompt was declined or the start failed, so this copy should carry on.
        private static bool RestartAsAdministrator(string[] args) {
            try {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath);
                psi.Arguments       = JoinArgs(args);
                psi.UseShellExecute = true;
                psi.Verb            = "runas";
                System.Diagnostics.Process.Start(psi);
                return true;
            } catch (Exception) {
                return false;   // most often: the user said No to Windows' prompt
            }
        }

        internal static string JoinArgs(string[] args) {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (string a in args) {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append('"').Append(a.Replace("\"", "")).Append('"');
            }
            return sb.ToString();
        }

        // A restarted dock waits for the one it replaces to close, so the panes that one releases
        // are on the desktop before this one absorbs them (Settings restart, Story 6817).
        private static void WaitForExit(int aintPid) {
            try {
                using (System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById(aintPid)) {
                    p.WaitForExit(20000);
                }
            } catch { /* already gone */ }
        }

        // The repo root (where update.cmd and build.cmd live), or null. Used by Update Agent Dock.
        internal static string RepoRoot { get { return FindRepoRoot(); } }

        // The folder holding config\pane.bat, or else the one holding samples\, searching the
        // exe's folder and up to CNST_LEVELS_UP parents. Null when neither is found.
        private static string FindRepoRoot() {
            string strDir = ExeDir();
            string strSampleRoot = null;
            for (int i = 0; i <= CNST_LEVELS_UP && !String.IsNullOrEmpty(strDir); i++) {
                if (File.Exists(Path.Combine(Path.Combine(strDir, CNST_CONFIG_FOLDER), CNST_PANE_BAT))) return strDir;
                if (strSampleRoot == null && File.Exists(Path.Combine(strDir, CNST_SAMPLE_BAT))) strSampleRoot = strDir;
                DirectoryInfo diParent = Directory.GetParent(strDir);
                strDir = diParent == null ? null : diParent.FullName;
            }
            return strSampleRoot;
        }

        private static string ExeDir() {
            try {
                return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            } catch { return null; }
        }

        private static string DefaultCwd() {
            // Spawn panes in the .NET source root -- same as Claude-Code2.bat does
            // (cd /d C:\Tony\Dev\Dot_Net\Source then claude).
            const string SOURCE = @"C:\Tony\Dev\Dot_Net\Source";
            if (Directory.Exists(SOURCE)) return SOURCE;
            string strProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            return String.IsNullOrEmpty(strProfile) ? Environment.CurrentDirectory : strProfile;
        }

        private static void ShowUsage() {
            MessageBox.Show(
                "AgentDockStandalone -- Claude Code MDI pane host\r\n\r\n"
                + "Usage:\r\n"
                + "  AgentDockStandalone.exe                       Empty dock.\r\n"
                + "  AgentDockStandalone.exe --spawn N             Spawn N panes (1-16).\r\n"
                + "  AgentDockStandalone.exe --absorb              Absorb running WT windows.\r\n"
                + "  AgentDockStandalone.exe --bat <path>          Use a different bat.\r\n"
                + "  AgentDockStandalone.exe --cwd <path>          Override working dir.\r\n\r\n"
                + "Default bat: config\\pane.bat (copied from samples\\Copilot.bat on first run)\r\n"
                + "Default cwd: C:\\Tony\\Dev\\Dot_Net\\Source, else your profile folder",
                "AgentDockStandalone",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
