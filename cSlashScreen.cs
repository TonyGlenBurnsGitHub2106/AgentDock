using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AgentDockStandalone {

    // What an agent shows under its input line while a slash command is being typed (Story 6851),
    // read from its screen. Tony: "When I typed /skill, I should have seen this" (Claude Code's own
    // list). Claude Code draws it like this (2026-11-25, v2.1.331):
    //
    //   ----------------------------------------------   a rule line
    //   > /skill                                          the input line (the prompt mark, then the text)
    //   ----------------------------------------------   a rule line
    //     (a warning or two may come first)
    //     > /skills                     List available skills          the picked row
    //       /skill-creator              Create new skills, modify ...  a row
    //                                   ... and the rest of it         a long description goes on
    //       (down arrow) 20 more                                       there are more
    //
    // After "/code-review " with nothing more typed, the input line carries the command's
    // argument hint after the text: "/code-review  [low|medium|high ...]". ParseFor finds it by
    // comparing the input line with what was typed.
    internal sealed class cSlashScreen {

        public string Input = "";                 // the input line, without its prompt mark
        public List<cSlashCommand> Rows = new List<cSlashCommand>();
        public int    Selected = -1;              // the row the agent has picked
        public string More = "";                  // "20 more" and the like
        public bool   FoundInput;                 // an input box was on the screen at all

        private const char CNST_MARK  = '\u276F';  // the prompt / pick mark
        private const char CNST_RULE  = '\u2500';
        private static readonly Regex RX_ROW = new Regex("^\\s{1,6}(?:(\u276F|>)\\s+)?(/\\S+)(?:\\s{2,}(.*))?$");

        public static cSlashScreen Parse(string[] aastrLines) {
            cSlashScreen r = new cSlashScreen();
            if (aastrLines == null) return r;
            // The input box: the last pair of rule lines with a prompt-mark line right under the first.
            int intTop = -1, intBottom = -1;
            for (int i = 0; i < aastrLines.Length - 1; i++) {
                if (!IsRule(aastrLines[i])) continue;
                string strNext = aastrLines[i + 1].TrimStart();
                if (strNext.Length == 0 || (strNext[0] != CNST_MARK && strNext[0] != '>')) continue;
                for (int j = i + 2; j < aastrLines.Length; j++) {
                    if (IsRule(aastrLines[j])) { intTop = i; intBottom = j; break; }
                }
            }
            if (intTop < 0) return r;
            r.FoundInput = true;
            string strFirst = aastrLines[intTop + 1].TrimStart();
            r.Input = strFirst.Substring(1).TrimStart().TrimEnd();
            for (int k = intTop + 2; k < intBottom; k++) r.Input += "\n" + aastrLines[k].Trim();

            bool blnRows = false;
            int intDescCol = -1;
            for (int k = intBottom + 1; k < aastrLines.Length && k <= intBottom + 40; k++) {
                string s = aastrLines[k];
                Match m = RX_ROW.Match(s);
                if (m.Success) {
                    blnRows = true;
                    if (m.Groups[1].Success && m.Groups[1].Value.Length > 0) r.Selected = r.Rows.Count;
                    string strDesc = m.Groups[3].Success ? m.Groups[3].Value.Trim() : "";
                    if (m.Groups[3].Success) intDescCol = m.Groups[3].Index;
                    r.Rows.Add(new cSlashCommand(m.Groups[2].Value.Substring(1), strDesc, null));
                    continue;
                }
                string t = s.Trim();
                if (blnRows && t.Length > 0 && (t[0] == '\u2193' || t[0] == '\u2191')) {
                    r.More = t; continue;
                }
                // A long description goes on under itself, indented to its column.
                if (blnRows && t.Length > 0 && intDescCol > 0 && s.Length > intDescCol
                    && s.Substring(0, intDescCol).Trim().Length == 0 && r.Rows.Count > 0) {
                    cSlashCommand last = r.Rows[r.Rows.Count - 1];
                    last.Description = (last.Description + " " + t).Trim();
                    continue;
                }
                if (blnRows) break;            // the list is over
            }
            return r;
        }

        // The argument hint the agent shows after astrTyped, or "" (see the class comment).
        public string HintAfter(string astrTyped) {
            string strTyped = (astrTyped ?? "").TrimEnd();
            if (strTyped.Length == 0 || !Input.StartsWith(strTyped, StringComparison.Ordinal)) return "";
            if (Rows.Count > 0) return "";
            string strRest = Input.Substring(strTyped.Length).Trim();
            return strRest.Length > 0 ? strTyped + "  " + strRest : "";
        }

        private static bool IsRule(string s) {
            string t = s.Trim();
            if (t.Length < 20) return false;
            foreach (char c in t) if (c != CNST_RULE) return false;
            return true;
        }
    }
}
