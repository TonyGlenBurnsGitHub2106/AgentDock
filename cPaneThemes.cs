using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AgentDockStandalone {

    // The look of each pane's conversation (Story 6846). The dock's global look (Settings ->
    // Compose Box Theme, Bubble Style, the Theme Viewer's Save for All Panes) applies to every pane
    // unless a pane has a look of its own. A pane can override the colors, the bubbles, the font,
    // or all three; what it does not override follows the global look (Tony, 2026-11-25: "The
    // themes we set from settings should apply to all panes that don't have a theme override").
    //
    // Kept by the pane's PLACE in the dock, not by its terminal (Tony, 2026-11-25: "unless set back
    // to global, the first pane always loads with theme 1, bubble style 1, colors 1, etc. and theme
    // two with its set"): pane 1 always opens in pane 1's look, whatever terminal is there, and
    // "Use Global" puts a place back on the global look. Each place also remembers whether its
    // conversation pane hides the Send area at its right (Tony: "remembered options to hide the
    // pane to the right for each terminal").
    //
    // config\panethemes.txt, one line per place: "place <tab> theme <tab> style <tab> font <tab>
    // hidesend <tab> promptfont", "-" for follow the global look. (1.0.40 keyed the lines by terminal window; those
    // lines are ignored.)
    internal static class cPaneThemes {

        private const string CNST_FILE   = "panethemes.txt";
        private const string CNST_FOLLOW = "-";
        private const int    CNST_MAX_PLACE = 64;

        private sealed class Override {
            public string Theme;    // null: follow the global look
            public string Style;
            public string Font;
            public string PromptFont;   // the compose box's font
            public int    HideSend = -1;    // 1 hidden, 0 shown, -1 never chosen: hidden (Story 6850)
            public bool IsEmpty { get { return Theme == null && Style == null && Font == null && PromptFont == null && HideSend < 0; } }
        }

        private static Dictionary<int, Override> mdicPlaces;

        // The dock sets this: the place (1, 2, ...) of the pane showing a terminal window, or 0
        // when that window is not a pane of the dock.
        public static Func<IntPtr, int> PlaceOf;

        // Raised after any change (the global look, or a pane's own), so every compose box and
        // conversation pane paints itself again.
        public static event Action Changed;

        public static void RaiseChanged() {
            if (Changed != null) Changed();
        }

        private static int Place(IntPtr ahwnd) {
            if (ahwnd == IntPtr.Zero || PlaceOf == null) return 0;
            try { return PlaceOf(ahwnd); } catch (Exception) { return 0; }
        }

        private static Dictionary<int, Override> Places() {
            if (mdicPlaces != null) return mdicPlaces;
            mdicPlaces = new Dictionary<int, Override>();
            try {
                string strFile = FilePath();
                if (strFile != null && File.Exists(strFile)) {
                    foreach (string strLine in File.ReadAllLines(strFile)) {
                        string[] parts = strLine.Split('\t');
                        int intPlace;
                        if (parts.Length < 4 || !Int32.TryParse(parts[0].Trim(), out intPlace)) continue;
                        if (intPlace < 1 || intPlace > CNST_MAX_PLACE) continue;   // 1.0.40 window lines
                        Override o = new Override();
                        o.Theme = Known(parts[1], cComposeTheme.Exists) ? cComposeTheme.Named(parts[1].Trim()).Name : null;
                        o.Style = Known(parts[2], cBubbleStyle.Exists) ? cBubbleStyle.Named(parts[2].Trim()).Name : null;
                        o.Font  = Known(parts[3], cConversationFont.Exists) ? cConversationFont.Named(parts[3].Trim()).Name : null;
                        string strHide = parts.Length > 4 ? parts[4].Trim() : "";
                        o.HideSend = strHide == "1" ? 1 : (strHide == "0" ? 0 : -1);
                        o.PromptFont = parts.Length > 5 && Known(parts[5], cConversationFont.Exists) ? cConversationFont.Named(parts[5].Trim()).Name : null;
                        if (!o.IsEmpty) mdicPlaces[intPlace] = o;
                    }
                }
            } catch (Exception) { /* nothing remembered: every pane follows the global look */ }
            return mdicPlaces;
        }

        private static bool Known(string astr, Func<string, bool> afncExists) {
            string s = (astr ?? "").Trim();
            return s.Length > 0 && s != CNST_FOLLOW && afncExists(s);
        }

        private static string FilePath() {
            string strFolder = cDockSettings.ConfigFolder;
            return String.IsNullOrEmpty(strFolder) ? null : Path.Combine(strFolder, CNST_FILE);
        }

        private static Override Find(IntPtr ahwnd) {
            int intPlace = Place(ahwnd);
            Override o;
            return intPlace > 0 && Places().TryGetValue(intPlace, out o) ? o : null;
        }

        // The pane's own choice, or null when it follows the global look.
        public static string ThemeOverride(IntPtr ahwnd) { Override o = Find(ahwnd); return o == null ? null : o.Theme; }
        public static string StyleOverride(IntPtr ahwnd) { Override o = Find(ahwnd); return o == null ? null : o.Style; }
        public static string FontOverride(IntPtr ahwnd)  { Override o = Find(ahwnd); return o == null ? null : o.Font; }
        public static string PromptFontOverride(IntPtr ahwnd) { Override o = Find(ahwnd); return o == null ? null : o.PromptFont; }

        // True when the pane follows the global look in everything.
        public static bool FollowsGlobal(IntPtr ahwnd) {
            Override o = Find(ahwnd);
            return o == null || (o.Theme == null && o.Style == null && o.Font == null && o.PromptFont == null);
        }

        // What the pane shows: its own choice when it has one, else the global look.
        public static cComposeTheme ThemeFor(IntPtr ahwnd) {
            return cComposeTheme.Named(ThemeOverride(ahwnd) ?? cDockSettings.ComposeTheme);
        }

        public static cBubbleStyle StyleFor(IntPtr ahwnd) {
            return cBubbleStyle.Named(StyleOverride(ahwnd) ?? cDockSettings.BubbleStyle);
        }

        public static cConversationFont FontFor(IntPtr ahwnd) {
            return cConversationFont.Named(FontOverride(ahwnd) ?? cDockSettings.ConversationFont);
        }

        public static cConversationFont PromptFontFor(IntPtr ahwnd) {
            return cConversationFont.Named(PromptFontOverride(ahwnd) ?? cDockSettings.PromptFont);
        }

        public static string SetPromptFont(IntPtr ahwnd, string astrFont) {
            return Change(ahwnd, delegate(Override o) { o.PromptFont = astrFont == null ? null : cConversationFont.Named(astrFont).Name; });
        }

        // Whether the pane's conversation pane hides its Send area (remembered per place). A place
        // never set hides it (Story 6850, Tony: "It should spawn with hide options on, not off").
        public static bool HideSendArea(IntPtr ahwnd) {
            Override o = Find(ahwnd);
            return o == null || o.HideSend != 0;
        }

        // Each setter saves, repaints everything and returns null, or why it could not be saved.
        public static string SetTheme(IntPtr ahwnd, string astrTheme) {
            return Change(ahwnd, delegate(Override o) { o.Theme = astrTheme == null ? null : cComposeTheme.Named(astrTheme).Name; });
        }

        public static string SetStyle(IntPtr ahwnd, string astrStyle) {
            return Change(ahwnd, delegate(Override o) { o.Style = astrStyle == null ? null : cBubbleStyle.Named(astrStyle).Name; });
        }

        public static string SetFont(IntPtr ahwnd, string astrFont) {
            return Change(ahwnd, delegate(Override o) { o.Font = astrFont == null ? null : cConversationFont.Named(astrFont).Name; });
        }

        // The Theme Viewer's Save for This Pane: all three at once.
        public static string SetLook(IntPtr ahwnd, string astrTheme, string astrStyle, string astrFont, string astrPromptFont) {
            return Change(ahwnd, delegate(Override o) {
                o.Theme = cComposeTheme.Named(astrTheme).Name;
                o.Style = cBubbleStyle.Named(astrStyle).Name;
                o.Font  = cConversationFont.Named(astrFont).Name;
                o.PromptFont = cConversationFont.Named(astrPromptFont).Name;
            });
        }

        // Use Global: the pane's place follows the global look in everything again.
        public static string Clear(IntPtr ahwnd) {
            return Change(ahwnd, delegate(Override o) { o.Theme = null; o.Style = null; o.Font = null; o.PromptFont = null; });
        }

        public static string SetHideSendArea(IntPtr ahwnd, bool ablnHide) {
            return Change(ahwnd, delegate(Override o) { o.HideSend = ablnHide ? 1 : 0; });
        }

        private static string Change(IntPtr ahwnd, Action<Override> aactChange) {
            int intPlace = Place(ahwnd);
            if (intPlace <= 0) return "That pane was not found in the dock.";
            Dictionary<int, Override> dic = Places();
            Override o;
            if (!dic.TryGetValue(intPlace, out o)) { o = new Override(); dic[intPlace] = o; }
            aactChange(o);
            if (o.IsEmpty) dic.Remove(intPlace);
            string strProblem = Save();
            RaiseChanged();
            return strProblem;
        }

        private static string Save() {
            string strFile = FilePath();
            if (strFile == null) return "No config folder was found.";
            try {
                StringBuilder sb = new StringBuilder();
                List<int> lstPlaces = new List<int>(Places().Keys);
                lstPlaces.Sort();
                foreach (int intPlace in lstPlaces) {
                    Override o = Places()[intPlace];
                    sb.Append(intPlace)
                      .Append('\t').Append(o.Theme ?? CNST_FOLLOW)
                      .Append('\t').Append(o.Style ?? CNST_FOLLOW)
                      .Append('\t').Append(o.Font ?? CNST_FOLLOW)
                      .Append('\t').Append(o.HideSend == 1 ? "1" : (o.HideSend == 0 ? "0" : CNST_FOLLOW))
                      .Append('\t').Append(o.PromptFont ?? CNST_FOLLOW)
                      .Append("\r\n");
                }
                File.WriteAllText(strFile, sb.ToString());
                return null;
            } catch (Exception ex) {
                return ex.Message;
            }
        }
    }
}
