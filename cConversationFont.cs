using System;
using System.Collections.Generic;
using System.Drawing;

namespace AgentDockStandalone {

    // The font of the conversation's messages (Story 6846, "more theming option in general").
    // A third choice beside the colors (cComposeTheme) and the bubbles (cBubbleStyle), for every
    // pane or one pane, picked in Settings -> Theme Viewer. Every family here ships with Windows.
    internal sealed class cConversationFont {

        public readonly string Name;
        public readonly string Family;
        public readonly float  Size;

        private cConversationFont(string astrName, string astrFamily, float afltSize) {
            Name = astrName; Family = astrFamily; Size = afltSize;
        }

        // Segoe UI 9.75 is the font the conversation had before fonts were a choice.
        public const string CNST_DEFAULT = "Segoe UI";

        private static readonly cConversationFont[] maAll = new cConversationFont[] {
            new cConversationFont("Segoe UI",          "Segoe UI",        9.75F),
            new cConversationFont("Segoe UI Small",    "Segoe UI",        8.5F),
            new cConversationFont("Segoe UI Large",    "Segoe UI",        11.5F),
            new cConversationFont("Calibri",           "Calibri",         11F),
            new cConversationFont("Verdana",           "Verdana",         9F),
            new cConversationFont("Tahoma",            "Tahoma",          9.75F),
            new cConversationFont("Trebuchet",         "Trebuchet MS",    9.75F),
            new cConversationFont("Georgia",           "Georgia",         10F),
            new cConversationFont("Cambria",           "Cambria",         11F),
            new cConversationFont("Palatino",          "Palatino Linotype", 10.5F),
            new cConversationFont("Consolas",          "Consolas",        10F),
            new cConversationFont("Cascadia Mono",     "Cascadia Mono",   9.75F),
            new cConversationFont("Lucida Console",    "Lucida Console",  9.5F),
            new cConversationFont("Comic Sans",        "Comic Sans MS",   10F),
        };

        public static IEnumerable<cConversationFont> All { get { return maAll; } }

        // The sizes offered beside each font (Tony: "pick font and font size, not just font").
        public static readonly float[] Sizes = new float[] { 7F, 7.5F, 8F, 8.5F, 9F, 9.5F, 9.75F, 10F, 10.5F, 11F,
            11.5F, 12F, 13F, 14F, 15F, 16F, 18F, 20F, 22F, 24F };

        // The name of the font in the list, without a size of its own ("Georgia" for "Georgia, 14pt").
        public string BaseName { get { int i = Name.IndexOf(CNST_SIZE_SEP); return i < 0 ? Name : Name.Substring(0, i); } }

        // A size other than the font's own is kept in the name: "Georgia, 14pt". Every place that
        // stores a font name (the settings, the per-pane looks) keeps the size with it that way.
        private const string CNST_SIZE_SEP = ", ";

        // The font astrName names, with the size its name carries ("Georgia, 14pt"), or the
        // first font when the name is unknown.
        public static cConversationFont Named(string astrName) {
            string strBase = astrName ?? "";
            float fltSize = 0F;
            int intSep = strBase.IndexOf(CNST_SIZE_SEP);
            if (intSep >= 0) {
                string strSize = strBase.Substring(intSep + CNST_SIZE_SEP.Length).Trim();
                if (strSize.EndsWith("pt", StringComparison.OrdinalIgnoreCase)) strSize = strSize.Substring(0, strSize.Length - 2);
                float.TryParse(strSize, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fltSize);
                strBase = strBase.Substring(0, intSep).Trim();
            }
            cConversationFont found = maAll[0];
            foreach (cConversationFont f in maAll)
                if (String.Equals(f.Name, strBase, StringComparison.OrdinalIgnoreCase)) { found = f; break; }
            return found.WithSize(fltSize);
        }

        public static bool Exists(string astrName) {
            string strBase = astrName ?? "";
            int intSep = strBase.IndexOf(CNST_SIZE_SEP);
            if (intSep >= 0) strBase = strBase.Substring(0, intSep).Trim();
            foreach (cConversationFont f in maAll)
                if (String.Equals(f.Name, strBase, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // The same font at afltSize points (its own size when afltSize is 0 or out of range).
        public cConversationFont WithSize(float afltSize) {
            cConversationFont plain = Plain();
            if (afltSize < 6F || afltSize > 48F || Math.Abs(afltSize - plain.Size) < 0.01F) return plain;
            return new cConversationFont(plain.Name + CNST_SIZE_SEP + SizeText(afltSize) + "pt", plain.Family, afltSize);
        }

        // The font as it is in the list, at its own size.
        public cConversationFont Plain() {
            string strBase = BaseName;
            foreach (cConversationFont f in maAll)
                if (String.Equals(f.Name, strBase, StringComparison.OrdinalIgnoreCase)) return f;
            return maAll[0];
        }

        public static string SizeText(float afltSize) {
            return afltSize.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        // A new Font for it. Windows quietly substitutes a font that is not installed.
        public Font Create() {
            try { return new Font(Family, Size); } catch (Exception) { return new Font("Segoe UI", Size); }
        }
    }
}
