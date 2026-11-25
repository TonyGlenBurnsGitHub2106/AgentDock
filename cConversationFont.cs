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

        public static cConversationFont Named(string astrName) {
            foreach (cConversationFont f in maAll)
                if (String.Equals(f.Name, astrName, StringComparison.OrdinalIgnoreCase)) return f;
            return maAll[0];
        }

        public static bool Exists(string astrName) {
            foreach (cConversationFont f in maAll)
                if (String.Equals(f.Name, astrName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // A new Font for it. Windows quietly substitutes a font that is not installed.
        public Font Create() {
            try { return new Font(Family, Size); } catch (Exception) { return new Font("Segoe UI", Size); }
        }
    }
}
