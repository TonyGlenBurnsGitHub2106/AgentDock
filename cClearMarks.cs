using System;
using System.Collections.Generic;

namespace AgentDockStandalone {

    // Where Clear Screen was last pressed in each pane's conversation (Story 6846, Tony: "Clear
    // screen is not clearing the upper screen"). Clear Screen wipes the terminal and also empties
    // the conversation shown above the text box: from then on Run conversation shows only what
    // was said after the clear. Kept by terminal window for as long as the dock runs.
    //
    // The mark is the last message at the moment of the clear (its text and speaker), found again
    // from the end of the conversation each time it is shown. If it has scrolled out of what the
    // dock reads, everything read is newer than the clear and all of it shows.
    internal static class cClearMarks {

        private sealed class Mark {
            public string Text;
            public bool   IsAnswer;
            public bool   Empty;      // the conversation was empty when cleared
        }

        private static readonly Dictionary<long, Mark> mdicMarks = new Dictionary<long, Mark>();

        // Remembers alst's last message as the clear point of the pane behind ahwnd.
        public static void Set(IntPtr ahwnd, List<cHistoryEntry> alst) {
            if (ahwnd == IntPtr.Zero) return;
            Mark m = new Mark();
            if (alst == null || alst.Count == 0) m.Empty = true;
            else { m.Text = alst[alst.Count - 1].Text; m.IsAnswer = alst[alst.Count - 1].IsAnswer; }
            mdicMarks[ahwnd.ToInt64()] = m;
        }

        // The messages of alst that came after the pane's last clear (all of them with no clear).
        public static List<cHistoryEntry> After(IntPtr ahwnd, List<cHistoryEntry> alst) {
            if (alst == null) return new List<cHistoryEntry>();
            Mark m;
            if (ahwnd == IntPtr.Zero || !mdicMarks.TryGetValue(ahwnd.ToInt64(), out m) || m.Empty) return alst;
            for (int i = alst.Count - 1; i >= 0; i--) {
                if (alst[i].IsAnswer == m.IsAnswer && alst[i].Text == m.Text)
                    return alst.GetRange(i + 1, alst.Count - i - 1);
            }
            return alst;
        }
    }
}
