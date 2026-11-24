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

        // Reads the tab's real color off the screen and remembers it (Story 6828, second try).
        // What the dock chose is not always what the tab shows: a tab started with its own color
        // ignores a later recolor, and an absorbed tab shows its color scheme's version of the
        // color. Each pane shows its Windows Terminal tab row at the top, and the selected tab is
        // filled with the tab color, so the most common strongly colored pixel there is it.
        // Call only while the dock is in front, so nothing covers the panes. Returns true when the
        // remembered color changed.
        public static bool SampleFromScreen(IntPtr ahwnd) {
            try {
                if (ahwnd == IntPtr.Zero || !Win32.IsWindowVisible(ahwnd) || IsIconic(ahwnd)) return false;
                RECT r;
                if (!GetWindowRect(ahwnd, out r)) return false;
                int intDpi = 96;
                try { intDpi = (int)GetDpiForWindow(ahwnd); } catch (EntryPointNotFoundException) { }
                if (intDpi <= 0) intDpi = 96;
                int intWidth  = Math.Min(r.Right - r.Left, 320 * intDpi / 96);
                int intHeight = Math.Min(r.Bottom - r.Top, 40 * intDpi / 96);
                if (intWidth < 20 || intHeight < 10) return false;
                Color clr;
                using (Bitmap bmp = new Bitmap(intWidth, intHeight)) {
                    using (Graphics g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(r.Left, r.Top, 0, 0, bmp.Size);
                    clr = MostCommonStrongColor(bmp);
                }
                if (clr.IsEmpty) return false;
                string strHex = ColorTranslator.ToHtml(Color.FromArgb(clr.R, clr.G, clr.B));
                if (strHex.Length != 7) return false;
                string strOld;
                if (Colors().TryGetValue(ahwnd.ToInt64(), out strOld)
                    && String.Equals(strOld, strHex, StringComparison.OrdinalIgnoreCase)) return false;
                Remember(ahwnd, strHex);
                return true;
            } catch (Exception) {
                return false;
            }
        }

        // The most common clearly colored (not gray, black or white) pixel, when enough of them
        // agree to be a tab and not an icon. Color.Empty otherwise.
        private static Color MostCommonStrongColor(Bitmap abmp) {
            Dictionary<int, int> dicCount = new Dictionary<int, int>();
            for (int y = 0; y < abmp.Height; y++) {
                for (int x = 0; x < abmp.Width; x++) {
                    Color c = abmp.GetPixel(x, y);
                    int intMax = Math.Max(c.R, Math.Max(c.G, c.B));
                    int intMin = Math.Min(c.R, Math.Min(c.G, c.B));
                    if (intMax - intMin < 60 || intMax < 60) continue;
                    int intKey = (c.R << 16) | (c.G << 8) | c.B;
                    int n;
                    dicCount.TryGetValue(intKey, out n);
                    dicCount[intKey] = n + 1;
                }
            }
            int intBest = -1, intBestCount = 0;
            foreach (KeyValuePair<int, int> kv in dicCount)
                if (kv.Value > intBestCount) { intBest = kv.Key; intBestCount = kv.Value; }
            if (intBestCount < 200) return Color.Empty;
            return Color.FromArgb((intBest >> 16) & 0xFF, (intBest >> 8) & 0xFF, intBest & 0xFF);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

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
