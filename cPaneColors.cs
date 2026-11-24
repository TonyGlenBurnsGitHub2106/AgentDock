using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace AgentDockStandalone {

    // The tab color each pane's terminal was given (Story 6828), so the compose box's Send to list
    // can show each pane in its own tab's color. Windows Terminal has no way to ask a tab its
    // color, so the dock remembers the one it chose (cTabColor), by terminal window.
    //
    // Kept in config\tabcolors.txt, one "window color" line each, because the terminals outlive the
    // dock: an update releases them, the new dock takes them back, and they keep their colors.
    // A terminal the dock already colored is not colored again on the way back in, so the list and
    // the tab stay alike. Lines for windows that no longer exist are dropped on the next save.
    internal static class cPaneColors {

        private const string CNST_FILE = "tabcolors.txt";

        private static Dictionary<long, string> mdicColors;

        private static Dictionary<long, string> Colors() {
            if (mdicColors != null) return mdicColors;
            mdicColors = new Dictionary<long, string>();
            try {
                string strFile = FilePath();
                if (strFile != null && File.Exists(strFile)) {
                    foreach (string strLine in File.ReadAllLines(strFile)) {
                        string[] parts = strLine.Trim().Split(' ');
                        long lngHwnd;
                        if (parts.Length == 2 && Int64.TryParse(parts[0], out lngHwnd) && IsHex(parts[1])
                            && Win32.IsWindow(new IntPtr(lngHwnd)))
                            mdicColors[lngHwnd] = parts[1];
                    }
                }
            } catch (Exception) { /* no colors remembered, the list stays plain */ }
            return mdicColors;
        }

        private static string FilePath() {
            string strFolder = cDockSettings.ConfigFolder;
            return String.IsNullOrEmpty(strFolder) ? null : Path.Combine(strFolder, CNST_FILE);
        }

        private static bool IsHex(string astr) {
            int intValue;
            return astr != null && astr.Length == 7 && astr[0] == '#'
                && Int32.TryParse(astr.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out intValue);
        }

        // True when the dock gave this terminal window its color before.
        public static bool Known(IntPtr ahwnd) {
            return Colors().ContainsKey(ahwnd.ToInt64());
        }

        public static void Remember(IntPtr ahwnd, string astrHex) {
            if (ahwnd == IntPtr.Zero || !IsHex(astrHex)) return;
            Colors()[ahwnd.ToInt64()] = astrHex;
            Save();
        }

        // The pane's tab color, or Color.Empty when the dock does not know it.
        public static Color ColorOf(IntPtr ahwnd) {
            string strHex;
            if (!Colors().TryGetValue(ahwnd.ToInt64(), out strHex)) return Color.Empty;
            try { return ColorTranslator.FromHtml(strHex); } catch (Exception) { return Color.Empty; }
        }

        private static void Save() {
            try {
                string strFile = FilePath();
                if (strFile == null) return;
                StringBuilder sb = new StringBuilder();
                List<long> lstGone = new List<long>();
                foreach (KeyValuePair<long, string> kv in mdicColors) {
                    if (!Win32.IsWindow(new IntPtr(kv.Key))) { lstGone.Add(kv.Key); continue; }
                    sb.Append(kv.Key).Append(' ').Append(kv.Value).Append("\r\n");
                }
                foreach (long lng in lstGone) mdicColors.Remove(lng);
                File.WriteAllText(strFile, sb.ToString());
            } catch (Exception) { /* not remembered, no harm */ }
        }
    }
}
