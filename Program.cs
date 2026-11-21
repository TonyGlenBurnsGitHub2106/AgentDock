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

            int    intSpawn  = 0;
            bool   blnAbsorb = false;
            string strBat    = DefaultBatPath();
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
                } else if (a == "--help" || a == "-h" || a == "/?") {
                    ShowUsage();
                    return;
                }
            }

            Application.Run(new fAgentDockMain(intSpawn, blnAbsorb, strBat, strCwd));
        }

        // Default bat lives in C:\Tony\Batches alongside this exe's normal home.
        // If a bat sits beside the exe we prefer that (portable install). Then we
        // fall back to the well-known C:\Tony\Batches path.
        private static string DefaultBatPath() {
            try {
                string strExeDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string strBeside = Path.Combine(strExeDir, "Claude-Code2.bat");
                if (File.Exists(strBeside)) return strBeside;
            } catch { }
            string strWellKnown = @"C:\Tony\Batches\Claude-Code2.bat";
            if (File.Exists(strWellKnown)) return strWellKnown;
            return strWellKnown;   // return the well-known path even if missing -- UI surfaces the error
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
                + "Default bat: C:\\Tony\\Batches\\Claude-Code2.bat\r\n"
                + "Default cwd: C:\\Tony\\Dev\\Dot_Net\\Source",
                "AgentDockStandalone",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
