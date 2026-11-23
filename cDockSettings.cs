using System;
using System.Collections.Generic;
using System.IO;

namespace AgentDockStandalone {

    // This machine's dock settings (Story 6817): config\agentdock.ini, plain "Name = value"
    // lines, "#" starts a comment. The config folder is git-ignored. When the file is missing
    // the dock writes one holding every setting at its default, with a line saying what it does,
    // so the settings can be found and changed without reading code.
    internal static class cDockSettings {

        public const string CNST_FILE = "agentdock.ini";

        // Hide the title bar of each pane inside the dock, for one more row of terminal.
        public const string KEY_HIDE_PANE_TITLE_BARS = "HidePaneTitleBars";
        private const bool  DEFAULT_HIDE_PANE_TITLE_BARS = true;

        private static readonly Dictionary<string, string> mdicValues =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool HidePaneTitleBars {
            get { return ReadBool(KEY_HIDE_PANE_TITLE_BARS, DEFAULT_HIDE_PANE_TITLE_BARS); }
        }

        // Reads config\agentdock.ini from the config folder, writing the defaults first when it
        // is missing. A null or unusable folder leaves every setting at its default.
        public static void Load(string astrConfigFolder) {
            mdicValues.Clear();
            if (String.IsNullOrEmpty(astrConfigFolder)) return;
            string strFile = Path.Combine(astrConfigFolder, CNST_FILE);
            try {
                if (!File.Exists(strFile)) {
                    Directory.CreateDirectory(astrConfigFolder);
                    File.WriteAllText(strFile, DefaultFileText());
                }
                foreach (string strRaw in File.ReadAllLines(strFile)) {
                    string strLine = strRaw.Trim();
                    if (strLine.Length == 0 || strLine.StartsWith("#")) continue;
                    int intEq = strLine.IndexOf('=');
                    if (intEq <= 0) continue;
                    mdicValues[strLine.Substring(0, intEq).Trim()] = strLine.Substring(intEq + 1).Trim();
                }
            } catch { }
        }

        private static bool ReadBool(string astrKey, bool ablnDefault) {
            string strValue;
            if (!mdicValues.TryGetValue(astrKey, out strValue)) return ablnDefault;
            string v = strValue.ToLowerInvariant();
            if (v == "true"  || v == "yes" || v == "1" || v == "on")  return true;
            if (v == "false" || v == "no"  || v == "0" || v == "off") return false;
            return ablnDefault;
        }

        private static string DefaultFileText() {
            return "# Agent Dock settings for this machine. Lines are Name = value; # starts a comment.\r\n"
                 + "# Restart the dock after a change. The command a new pane runs is pane.bat, beside this file.\r\n"
                 + "\r\n"
                 + "# true: panes inside the dock have no title bar, which gives the terminal one more row.\r\n"
                 + "#   Close a pane with Agents -> Close Active Pane, pop it out with Unparent Active Pane.\r\n"
                 + "# false: each pane keeps its own title bar (close button, Undock on its menu).\r\n"
                 + KEY_HIDE_PANE_TITLE_BARS + " = " + (DEFAULT_HIDE_PANE_TITLE_BARS ? "true" : "false") + "\r\n";
        }
    }
}
