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
            if (intTop < 0) return ParseCopilot(aastrLines);
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

        // GitHub Copilot CLI draws it differently (2026-11-25, v1.0.108, captured by Rex at work):
        //
        //   (heavy bar) ----------------------------------   U+257B then U+2584s: the box's top
        //   | /model                                          U+2503, then the input line
        //   (heavy bar) ----------------------------------   U+2579 then U+2580s: the box's bottom
        //
        // The list sits ABOVE the box, one row per line, each starting with U+2503:
        //   | > /model          Select the AI model for this sessio...   the picked row (U+276F)
        //   |   /move           Move your uncommitted changes into ...   a row
        // Six rows show, with no "more" line. The input line shows the picked row's name, not
        // what was typed ("/mo" shows "/model"). After "/model " the rows are the command's
        // arguments ("/model plan  Set the model used while in plan mode", the first row being
        // "/model" itself) and the input line carries its usage: "/model [plan|fusion|...]".
        // Ctrl+U clears Copilot's input line too.
        private const char CNST_CP_TOP    = '\u257B';
        private const char CNST_CP_TOPBAR = '\u2584';
        private const char CNST_CP_BOTTOM = '\u2579';
        private const char CNST_CP_SIDE   = '\u2503';
        private static readonly Regex RX_CP_ROW = new Regex("^\\s*(?:(\u276F)\\s+)?(/\\S+(?: \\S+)*?)(?:\\s{2,}(.*))?$");

        public bool Copilot;                       // read from a Copilot screen

        private static cSlashScreen ParseCopilot(string[] aastrLines) {
            cSlashScreen r = new cSlashScreen();
            int intTop = -1, intBottom = -1;
            for (int i = 0; i < aastrLines.Length; i++) {
                string t = aastrLines[i].Trim();
                if (t.Length >= 20 && t[0] == CNST_CP_TOP && t[1] == CNST_CP_TOPBAR) {
                    for (int j = i + 1; j < aastrLines.Length; j++) {
                        string u = aastrLines[j].Trim();
                        if (u.Length > 0 && u[0] == CNST_CP_BOTTOM) { intTop = i; intBottom = j; break; }
                        if (u.Length == 0 || u[0] != CNST_CP_SIDE) break;
                    }
                }
            }
            if (intTop < 0) return r;
            r.FoundInput = true;
            r.Copilot    = true;
            List<string> lstInput = new List<string>();
            for (int k = intTop + 1; k < intBottom; k++) lstInput.Add(aastrLines[k].Trim().Substring(1).Trim());
            r.Input = String.Join("\n", lstInput.ToArray()).Trim();

            // The rows, read upward from the box. Copilot draws them two ways (Rex, 2026-11-25): in a
            // wide pane each starts with U+2503 and the last touches the box. In a narrow one (about
            // 50 columns) they have no U+2503 and a blank line or two sits between them and the box.
            // A U+2503 at the right edge is the scrollbar, so it is dropped too. The rows are one
            // unbroken block: the first line above it that is not a row ends it.
            List<Match> lstRows = new List<Match>();
            int intBlanks = 0;
            for (int k = intTop - 1; k >= 0; k--) {
                string s = CopilotRowText(aastrLines[k]);
                if (s.Length == 0) {
                    if (lstRows.Count > 0 || ++intBlanks > 2) break;
                    continue;
                }
                Match m = RX_CP_ROW.Match(s);
                if (!m.Success) break;
                lstRows.Insert(0, m);
            }
            foreach (Match m in lstRows) {
                if (m.Groups[1].Success && m.Groups[1].Value.Length > 0) r.Selected = r.Rows.Count;
                string strDesc = m.Groups[3].Success ? m.Groups[3].Value.Trim() : "";
                if (strDesc.EndsWith("\u2026", StringComparison.Ordinal)) strDesc = strDesc.Substring(0, strDesc.Length - 1).TrimEnd() + "...";
                r.Rows.Add(new cSlashCommand(m.Groups[2].Value.Substring(1), strDesc, null));
            }
            return r;
        }

        // A line with Copilot's side bar (left) and scrollbar (right) taken off, trimmed.
        private static string CopilotRowText(string astrLine) {
            string s = (astrLine ?? "").Trim();
            if (s.Length > 0 && s[0] == CNST_CP_SIDE) s = s.Substring(1).Trim();
            if (s.Length > 0 && (s[s.Length - 1] == CNST_CP_SIDE || s[s.Length - 1] == '\u2502')) s = s.Substring(0, s.Length - 1).Trim();
            return s;
        }

        // The argument hint the agent shows after astrTyped, or "" (see the class comment).
        public string HintAfter(string astrTyped) {
            string strTyped = (astrTyped ?? "").TrimEnd();
            if (strTyped.Length == 0 || !Input.StartsWith(strTyped, StringComparison.Ordinal)) return "";
            if (Copilot) {
                // Copilot's input line shows the picked name while the name is typed, so only
                // what follows a typed space is a hint.
                if (!(astrTyped ?? "").EndsWith(" ", StringComparison.Ordinal)) return "";
                string strUsage = Input.Substring(strTyped.Length).Trim();
                return strUsage.Length > 0 ? strTyped + "  " + strUsage : "";
            }
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
