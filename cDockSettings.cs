using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AgentDockStandalone {

    // This machine's dock settings (Story 6817), all in the git-ignored config folder:
    //   config\agentdock.ini  "Name = value" lines, "#" starts a comment. Written with every
    //                         setting at its default, and a line saying what it does, when missing.
    //   config\pane.bat       the command each new pane runs: a "cd /d <folder>" line and the
    //                         agent's command line. Settings reads and writes those two.
    // Settings (the dialog) edits both, so nobody has to open the files by hand.
    internal static class cDockSettings {

        public const string CNST_FILE     = "agentdock.ini";
        public const string CNST_PANE_BAT = "pane.bat";

        // Hide the title bar of each pane inside the dock, for one more row of terminal.
        public const string KEY_HIDE_PANE_TITLE_BARS = "HidePaneTitleBars";
        private const bool  DEFAULT_HIDE_PANE_TITLE_BARS = true;

        private static readonly Dictionary<string, string> mdicValues =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string mstrConfigFolder;

        public static string ConfigFolder { get { return mstrConfigFolder; } }

        public static string PaneBatPath {
            get { return String.IsNullOrEmpty(mstrConfigFolder) ? null : Path.Combine(mstrConfigFolder, CNST_PANE_BAT); }
        }

        public static bool HidePaneTitleBars {
            get { return ReadBool(KEY_HIDE_PANE_TITLE_BARS, DEFAULT_HIDE_PANE_TITLE_BARS); }
        }

        // Reads config\agentdock.ini from the config folder, writing the defaults first when it
        // is missing. A null or unusable folder leaves every setting at its default.
        public static void Load(string astrConfigFolder) {
            mdicValues.Clear();
            mstrConfigFolder = astrConfigFolder;
            if (String.IsNullOrEmpty(astrConfigFolder)) return;
            string strFile = Path.Combine(astrConfigFolder, CNST_FILE);
            try {
                if (!File.Exists(strFile)) {
                    Directory.CreateDirectory(astrConfigFolder);
                    File.WriteAllText(strFile, FileText(DEFAULT_HIDE_PANE_TITLE_BARS));
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

        // Writes agentdock.ini with the given values. Throws when the file cannot be written.
        public static void Save(bool ablnHidePaneTitleBars) {
            if (String.IsNullOrEmpty(mstrConfigFolder)) throw new InvalidOperationException("No config folder was found.");
            Directory.CreateDirectory(mstrConfigFolder);
            File.WriteAllText(Path.Combine(mstrConfigFolder, CNST_FILE), FileText(ablnHidePaneTitleBars));
            mdicValues[KEY_HIDE_PANE_TITLE_BARS] = ablnHidePaneTitleBars ? "true" : "false";
        }

        // ---------------------------------------------------------------- pane.bat

        // The start folder and agent command from pane.bat. The folder is the first
        // "cd /d <folder>" line; the command is the last line that is not @echo, rem or cd.
        // ablnPlain is false when the file holds anything else (extra commands), which a save
        // from Settings would replace.
        public static void ReadPaneBat(out string astrFolder, out string astrCommand, out bool ablnPlain) {
            astrFolder = ""; astrCommand = ""; ablnPlain = true;
            string strPath = PaneBatPath;
            if (strPath == null || !File.Exists(strPath)) return;
            int intCommands = 0;
            foreach (string strRaw in File.ReadAllLines(strPath)) {
                string strLine = strRaw.Trim();
                string strLower = strLine.ToLowerInvariant();
                if (strLine.Length == 0 || strLower.StartsWith("@echo") || strLower.StartsWith("rem ")
                    || strLower == "rem" || strLower.StartsWith("::")) continue;
                if (strLower.StartsWith("cd ") || strLower.StartsWith("cd/") || strLower.StartsWith("pushd ")) {
                    if (astrFolder.Length == 0) astrFolder = FolderFromCd(strLine);
                    else ablnPlain = false;
                    continue;
                }
                astrCommand = strLine;
                intCommands++;
            }
            if (intCommands > 1) ablnPlain = false;
        }

        // Writes pane.bat as a start folder and one agent command. Throws on failure.
        public static void WritePaneBat(string astrFolder, string astrCommand) {
            string strPath = PaneBatPath;
            if (strPath == null) throw new InvalidOperationException("No config folder was found.");
            Directory.CreateDirectory(mstrConfigFolder);
            StringBuilder sb = new StringBuilder();
            sb.Append("@echo off\r\n");
            sb.Append("rem The command each new Agent Dock pane runs. Written by Settings in the dock;\r\n");
            sb.Append("rem change it there (Settings -> Agent Dock Settings), or edit this file by hand.\r\n");
            sb.Append("cd /d \"").Append(astrFolder).Append("\"\r\n");
            sb.Append(astrCommand).Append("\r\n");
            File.WriteAllText(strPath, sb.ToString());
        }

        // "cd /d C:\x", "cd /d "C:\x y"", "pushd C:\x" -> the folder.
        private static string FolderFromCd(string astrLine) {
            string s = astrLine.Trim();
            int intSpace = s.IndexOf(' ');
            s = intSpace < 0 ? "" : s.Substring(intSpace + 1).Trim();
            if (s.StartsWith("/d ", StringComparison.OrdinalIgnoreCase) || s.StartsWith("/D ")) s = s.Substring(3).Trim();
            // %USERPROFILE% and friends become the real folder (samples\Copilot.bat uses one).
            return Environment.ExpandEnvironmentVariables(s.Trim('"'));
        }

        // ---------------------------------------------------------------- ini helpers

        private static bool ReadBool(string astrKey, bool ablnDefault) {
            string strValue;
            if (!mdicValues.TryGetValue(astrKey, out strValue)) return ablnDefault;
            string v = strValue.ToLowerInvariant();
            if (v == "true"  || v == "yes" || v == "1" || v == "on")  return true;
            if (v == "false" || v == "no"  || v == "0" || v == "off") return false;
            return ablnDefault;
        }

        private static string FileText(bool ablnHidePaneTitleBars) {
            return "# Agent Dock settings for this machine. Lines are Name = value; # starts a comment.\r\n"
                 + "# Easiest: change them in the dock with Settings -> Agent Dock Settings.\r\n"
                 + "# The command a new pane runs, and its start folder, are in pane.bat beside this file.\r\n"
                 + "\r\n"
                 + "# true: panes inside the dock have no title bar, which gives the terminal one more row.\r\n"
                 + "#   Close a pane with Agents -> Close Active Pane, pop it out with Unparent Active Pane.\r\n"
                 + "# false: each pane keeps its own title bar (close button, Undock on its menu).\r\n"
                 + KEY_HIDE_PANE_TITLE_BARS + " = " + (ablnHidePaneTitleBars ? "true" : "false") + "\r\n";
        }
    }
}
