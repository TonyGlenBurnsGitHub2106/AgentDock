using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AgentDockStandalone {

    // The look of each pane's conversation (Story 6846). The dock's theme (Settings -> Compose Box
    // Theme) and bubble style (Settings -> Bubble Style) apply to every pane, unless a pane has an
    // override of its own (Settings -> Pane Theme, or right-click a conversation pane's header).
    // A pane can override the colors, the bubbles, or both; what it does not override follows the
    // dock (Tony, 2026-11-25: "The themes we set from settings should apply to all panes that don't
    // have a theme override").
    //
    // Kept by terminal window in config\panethemes.txt ("window <tab> theme <tab> style", "-" for
    // follow the dock), like cPaneColors, so a pane keeps its look through an update: the terminals
    // outlive the dock and the new dock takes them back. Lines for windows that no longer exist are
    // dropped on the next save.
    internal static class cPaneThemes {

        private const string CNST_FILE   = "panethemes.txt";
        private const string CNST_FOLLOW = "-";

        private sealed class Override {
            public string Theme;    // null: follow the dock
            public string Style;
        }

        private static Dictionary<long, Override> mdicOverrides;

        // Raised after any change (the dock's theme or style, or a pane's override), so every
        // compose box and conversation pane paints itself again.
        public static event Action Changed;

        public static void RaiseChanged() {
            if (Changed != null) Changed();
        }

        private static Dictionary<long, Override> Overrides() {
            if (mdicOverrides != null) return mdicOverrides;
            mdicOverrides = new Dictionary<long, Override>();
            try {
                string strFile = FilePath();
                if (strFile != null && File.Exists(strFile)) {
                    foreach (string strLine in File.ReadAllLines(strFile)) {
                        string[] parts = strLine.Split('\t');
                        long lngHwnd;
                        if (parts.Length != 3 || !Int64.TryParse(parts[0].Trim(), out lngHwnd)) continue;
                        if (!Win32.IsWindow(new IntPtr(lngHwnd))) continue;
                        Override o = new Override();
                        string strTheme = parts[1].Trim(), strStyle = parts[2].Trim();
                        o.Theme = strTheme != CNST_FOLLOW && cComposeTheme.Exists(strTheme) ? cComposeTheme.Named(strTheme).Name : null;
                        o.Style = strStyle != CNST_FOLLOW && cBubbleStyle.Exists(strStyle) ? cBubbleStyle.Named(strStyle).Name : null;
                        if (o.Theme != null || o.Style != null) mdicOverrides[lngHwnd] = o;
                    }
                }
            } catch (Exception) { /* no overrides remembered: every pane follows the dock */ }
            return mdicOverrides;
        }

        private static string FilePath() {
            string strFolder = cDockSettings.ConfigFolder;
            return String.IsNullOrEmpty(strFolder) ? null : Path.Combine(strFolder, CNST_FILE);
        }

        // The pane's own theme name, or null when it follows the dock.
        public static string ThemeOverride(IntPtr ahwnd) {
            Override o;
            return Overrides().TryGetValue(ahwnd.ToInt64(), out o) ? o.Theme : null;
        }

        public static string StyleOverride(IntPtr ahwnd) {
            Override o;
            return Overrides().TryGetValue(ahwnd.ToInt64(), out o) ? o.Style : null;
        }

        // What the pane shows: its own theme when it has one, else the dock's.
        public static cComposeTheme ThemeFor(IntPtr ahwnd) {
            string strName = ahwnd == IntPtr.Zero ? null : ThemeOverride(ahwnd);
            return cComposeTheme.Named(strName ?? cDockSettings.ComposeTheme);
        }

        public static cBubbleStyle StyleFor(IntPtr ahwnd) {
            string strName = ahwnd == IntPtr.Zero ? null : StyleOverride(ahwnd);
            return cBubbleStyle.Named(strName ?? cDockSettings.BubbleStyle);
        }

        // Sets the pane's own theme (null: follow the dock again), saves, and repaints everything.
        // Returns null when saved, or why it could not be remembered.
        public static string SetTheme(IntPtr ahwnd, string astrTheme) {
            return Set(ahwnd, true, astrTheme);
        }

        public static string SetStyle(IntPtr ahwnd, string astrStyle) {
            return Set(ahwnd, false, astrStyle);
        }

        // Drops both overrides: the pane follows the dock in everything.
        public static string Clear(IntPtr ahwnd) {
            if (ahwnd == IntPtr.Zero) return null;
            Overrides().Remove(ahwnd.ToInt64());
            string strProblem = Save();
            RaiseChanged();
            return strProblem;
        }

        private static string Set(IntPtr ahwnd, bool ablnTheme, string astrName) {
            if (ahwnd == IntPtr.Zero) return null;
            Dictionary<long, Override> dic = Overrides();
            Override o;
            if (!dic.TryGetValue(ahwnd.ToInt64(), out o)) { o = new Override(); dic[ahwnd.ToInt64()] = o; }
            if (ablnTheme) o.Theme = astrName == null ? null : cComposeTheme.Named(astrName).Name;
            else o.Style = astrName == null ? null : cBubbleStyle.Named(astrName).Name;
            if (o.Theme == null && o.Style == null) dic.Remove(ahwnd.ToInt64());
            string strProblem = Save();
            RaiseChanged();
            return strProblem;
        }

        private static string Save() {
            string strFile = FilePath();
            if (strFile == null) return "No config folder was found.";
            try {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<long, Override> kv in Overrides()) {
                    if (!Win32.IsWindow(new IntPtr(kv.Key))) continue;
                    sb.Append(kv.Key).Append('\t').Append(kv.Value.Theme ?? CNST_FOLLOW)
                      .Append('\t').Append(kv.Value.Style ?? CNST_FOLLOW).Append("\r\n");
                }
                File.WriteAllText(strFile, sb.ToString());
                return null;
            } catch (Exception ex) {
                return ex.Message;
            }
        }
    }
}
