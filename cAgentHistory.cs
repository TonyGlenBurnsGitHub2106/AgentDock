using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AgentDockStandalone {

    // The prompts already sent in a pane, wherever they were typed (Story 6826): in the compose
    // box or straight into the terminal. The compose box's Up arrow steps through them.
    //
    // Claude Code keeps every prompt it is sent. ~\.claude\history.jsonl has one JSON line per
    // prompt with its sessionId, and ~\.claude\sessions\<pid>.json names the session of each
    // running Claude. So: the processes on the pane's console -> the one with a session file ->
    // its sessionId -> that session's lines in history.jsonl, oldest first. Pasted text is stored
    // as "[Pasted text #N +M lines]" plus the text itself, and is put back.
    //
    // A pane with no Claude session is tried as GitHub Copilot CLI (cCopilotHistory, Story 6828).
    // When neither is found, ForPane returns null and the compose box uses its own history.
    // CLAUDE_CONFIG_DIR moves the folder, as Claude allows.
    internal static class cAgentHistory {

        // Only the end of history.jsonl is read: it holds every prompt ever, across all sessions.
        private const int CNST_TAIL_BYTES = 4 * 1024 * 1024;
        private static readonly Regex RX_SESSION = new Regex("\"sessionId\"\\s*:\\s*\"([^\"]+)\"");
        private static readonly Regex RX_PASTED  = new Regex(@"\[Pasted text #(\d+)[^\]]*\]");

        // The prompts of the Claude session in the pane shown by ahwndTerminal, oldest first, at
        // most aintMax of them. Null when no Claude session is found there.
        public static List<string> ForPane(IntPtr ahwndTerminal, int aintMax) {
            List<uint> lstPids;
            try { lstPids = cConsoleInput.ConsoleProcesses(ahwndTerminal); } catch (Exception) { return null; }
            List<string> lst = ClaudeForPids(lstPids, aintMax);
            if (lst != null) return lst;
            return cCopilotHistory.ForPids(lstPids, aintMax);
        }

        private static List<string> ClaudeForPids(List<uint> alstPids, int aintMax) {
            try {
                string strDir = ClaudeDir();
                if (strDir == null) return null;
                string strSession = ClaudeSession(strDir, alstPids);
                if (strSession == null) return null;
                return ReadSessions(strDir, SessionChain(strDir, strSession), aintMax);
            } catch (Exception) {
                return null;
            }
        }

        // Whether one of these processes is a Claude Code or Copilot session, even one where nothing
        // has been said yet (Story 6850). Claude Code writes no transcript before the first message,
        // so a new pane has a session but no conversation.
        public static bool HasSession(List<uint> alstPids) {
            try {
                string strDir = ClaudeDir();
                if (strDir != null && ClaudeSession(strDir, alstPids) != null) return true;
            } catch (Exception) { }
            return cCopilotHistory.Stamp(alstPids) != null;
        }

        // Claude Code's folder (CLAUDE_CONFIG_DIR, or .claude in the profile), or null.
        public static string ClaudeConfigDir() { return ClaudeDir(); }

        // The folder the Claude running as one of these processes was started in (its session
        // file's "cwd"), or null. The slash command list reads that folder's own skills (Story 6851).
        private static readonly Regex RX_CWD = new Regex("\"cwd\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");

        public static string ClaudeCwd(List<uint> alstPids) {
            try {
                string strDir = ClaudeDir();
                if (strDir == null) return null;
                foreach (uint intPid in alstPids) {
                    string strFile = Path.Combine(Path.Combine(strDir, "sessions"), intPid + ".json");
                    if (!File.Exists(strFile)) continue;
                    Match m = RX_CWD.Match(ReadShared(strFile));
                    if (m.Success) return Regex.Unescape(m.Groups[1].Value);
                }
            } catch (Exception) { }
            return null;
        }

        // Whether one of these processes is a Claude Code session (not Copilot).
        public static bool IsClaude(List<uint> alstPids) {
            try {
                string strDir = ClaudeDir();
                return strDir != null && ClaudeSession(strDir, alstPids) != null;
            } catch (Exception) { return false; }
        }

        // The session id of the Claude running as one of these processes, or null.
        private static string ClaudeSession(string astrDir, List<uint> alstPids) {
            foreach (uint intPid in alstPids) {
                string strFile = Path.Combine(Path.Combine(astrDir, "sessions"), intPid + ".json");
                if (!File.Exists(strFile)) continue;
                Match m = RX_SESSION.Match(ReadShared(strFile));
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        // The transcript of a Claude session, projects\<folder>\<id>.jsonl, or null.
        private static string TranscriptFile(string astrDir, string astrSession) {
            if (astrSession.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
            string strProjects = Path.Combine(astrDir, "projects");
            if (!Directory.Exists(strProjects)) return null;
            foreach (string strFolder in Directory.GetDirectories(strProjects)) {
                string strFile = Path.Combine(strFolder, astrSession + ".jsonl");
                if (File.Exists(strFile)) return strFile;
            }
            return null;
        }

        // ------------------------------------------------------------ the last answer

        // The agent's most recent answer in the pane (Story 6828, Popup Answer), or null when none
        // is found. Claude Code: the last assistant message with text in the session's transcript.
        // Copilot CLI: the last assistant.message in its session record (cCopilotHistory).
        public static string LastAnswerForPane(IntPtr ahwndTerminal) {
            List<uint> lstPids;
            try { lstPids = cConsoleInput.ConsoleProcesses(ahwndTerminal); } catch (Exception) { return null; }
            string str = ClaudeLastAnswer(lstPids);
            if (str != null) return str;
            return cCopilotHistory.LastAnswerForPids(lstPids);
        }

        // One answer can be written as several transcript lines (one per content block) that share
        // a message id, so the text blocks of the last message that has text are joined.
        private static string ClaudeLastAnswer(List<uint> alstPids) {
            try {
                string strDir = ClaudeDir();
                if (strDir == null) return null;
                string strSession = ClaudeSession(strDir, alstPids);
                if (strSession == null) return null;
                string strFile = TranscriptFile(strDir, strSession);
                if (strFile == null) return null;
                JavaScriptSerializer js = new JavaScriptSerializer();
                js.MaxJsonLength = Int32.MaxValue;
                string strLastId = null;
                List<string> lstText = new List<string>();
                foreach (string strLine in ReadTailLines(strFile)) {
                    if (strLine.IndexOf("\"assistant\"", StringComparison.Ordinal) < 0) continue;
                    Dictionary<string, object> row;
                    try { row = js.DeserializeObject(strLine) as Dictionary<string, object>; } catch (Exception) { continue; }
                    if (row == null || !"assistant".Equals(Str(row, "type"))) continue;
                    if (row.ContainsKey("isSidechain") && true.Equals(row["isSidechain"])) continue;
                    Dictionary<string, object> msg = row.ContainsKey("message") ? row["message"] as Dictionary<string, object> : null;
                    if (msg == null) continue;
                    object[] blocks = msg.ContainsKey("content") ? msg["content"] as object[] : null;
                    if (blocks == null) continue;
                    List<string> lstHere = new List<string>();
                    foreach (object o in blocks) {
                        Dictionary<string, object> b = o as Dictionary<string, object>;
                        if (b != null && "text".Equals(Str(b, "type")) && !String.IsNullOrEmpty(Str(b, "text")))
                            lstHere.Add(Str(b, "text"));
                    }
                    if (lstHere.Count == 0) continue;
                    string strId = Str(msg, "id");
                    if (strId == null || strId != strLastId) lstText.Clear();
                    strLastId = strId;
                    lstText.AddRange(lstHere);
                }
                return lstText.Count == 0 ? null : String.Join("\n\n", lstText.ToArray());
            } catch (Exception) {
                return null;
            }
        }

        private static string Str(Dictionary<string, object> adic, string astrKey) {
            object o;
            return adic.TryGetValue(astrKey, out o) ? o as string : null;
        }

        private static string ClaudeDir() {
            string strDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (String.IsNullOrEmpty(strDir)) {
                string strProfile = Environment.GetEnvironmentVariable("USERPROFILE");
                if (String.IsNullOrEmpty(strProfile)) return null;
                strDir = Path.Combine(strProfile, ".claude");
            }
            return Directory.Exists(strDir) ? strDir : null;
        }

        // The session and the ones it came from. /clear (and a resume) starts a new session, and
        // its transcript (projects\<folder>\<id>.jsonl) opens with a "derived-from" line naming
        // the one before. Following that line back means Up still finds the prompts sent before a
        // /clear, and the prompts sent while the compose box was off, all of them, in order.
        private const int CNST_CHAIN_MAX       = 20;
        private const int CNST_HEAD_BYTES      = 64 * 1024;
        private static readonly Regex RX_DERIVED = new Regex(@"""type""\s*:\s*""derived-from""");
        private static readonly Regex RX_FROM_ID = new Regex(@"""id""\s*:\s*""([^""]+)""");

        private static HashSet<string> SessionChain(string astrDir, string astrSession) {
            return new HashSet<string>(SessionChainNewestFirst(astrDir, astrSession));
        }

        // The same sessions in the order found: this one, then the one it came from, and so on.
        private static List<string> SessionChainNewestFirst(string astrDir, string astrSession) {
            return SessionChainNewestFirst(astrDir, astrSession, false);
        }

        // ablnStopAtClear (Story 6846, Run conversation): a session a /clear started is not followed
        // back past the clear, so the conversation shows starts empty, as the terminal does (Tony:
        // "if clear is entered via the slash command, the clear should also happen in the compose
        // dialog"). A resume is still followed. Up never stops: it reaches back past a /clear.
        private static List<string> SessionChainNewestFirst(string astrDir, string astrSession, bool ablnStopAtClear) {
            List<string> lst = new List<string>();
            Queue<string> todo = new Queue<string>();
            todo.Enqueue(astrSession);
            while (todo.Count > 0 && lst.Count < CNST_CHAIN_MAX) {
                string strId = todo.Dequeue();
                if (lst.Contains(strId)) continue;
                lst.Add(strId);
                foreach (string strFrom in DerivedFrom(astrDir, strId, ablnStopAtClear)) todo.Enqueue(strFrom);
            }
            return lst;
        }

        // ------------------------------------------------------------ the whole conversation

        // The pane's conversation, oldest first, at most aintMax entries (Story 6828, what Up
        // shows): the prompts you sent and the agent's answers, or with ablnAnswersOnly only the
        // answers. Null when the agent keeps no record the dock can read.
        public static List<cHistoryEntry> ConversationForPane(IntPtr ahwndTerminal, int aintMax, bool ablnAnswersOnly) {
            List<uint> lstPids;
            try { lstPids = cConsoleInput.ConsoleProcesses(ahwndTerminal); } catch (Exception) { return null; }
            List<cHistoryEntry> lst = ClaudeConversation(lstPids, ablnAnswersOnly);
            if (lst == null) lst = cCopilotHistory.ConversationForPids(lstPids, ablnAnswersOnly);
            if (lst != null && lst.Count > aintMax) lst.RemoveRange(0, lst.Count - aintMax);
            return lst;
        }

        // The same for processes already found (Story 6845, Run conversation): safe off the
        // window's thread, since it does not touch the console the way finding the processes does.
        public static List<cHistoryEntry> ConversationForPids(List<uint> alstPids, int aintMax) {
            List<cHistoryEntry> lst = ClaudeConversation(alstPids, false, true);   // starts at the last /clear
            if (lst == null) lst = cCopilotHistory.ConversationForPids(alstPids, false, true);
            if (lst != null && lst.Count > aintMax) lst.RemoveRange(0, lst.Count - aintMax);
            return lst;
        }

        // A cheap sign of whether the conversation changed (Story 6845): the current Claude
        // session's transcript, its size and when it was written. Null when no Claude session is
        // found (Copilot, or no agent), and Run conversation then rereads every few seconds.
        public static string ConversationStamp(List<uint> alstPids) {
            try {
                string strDir = ClaudeDir();
                if (strDir == null) return cCopilotHistory.Stamp(alstPids);
                string strSession = ClaudeSession(strDir, alstPids);
                if (strSession == null) return cCopilotHistory.Stamp(alstPids);
                string strFile = TranscriptFile(strDir, strSession);
                if (strFile == null) return strSession;
                FileInfo fi = new FileInfo(strFile);
                return strSession + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
            } catch (Exception) {
                return null;
            }
        }

        // Whether the agent is working on something or waiting for you (Story 6845, the status
        // bar): 1 working, 0 idle, -1 unknown (no Claude session, or nothing said yet). Read from
        // the end of the session's transcript: the last thing said decides it. An answer whose
        // stop_reason is end_turn means the turn is over. Anything else (your prompt, a tool
        // call, a tool's result) means the agent is still at it. An interrupt or a slash command
        // with no answer is idle too.
        private const int CNST_STATE_TAIL_BYTES = 512 * 1024;

        public static int AgentState(List<uint> alstPids) {
            try {
                string strDir = ClaudeDir();
                if (strDir == null) return cCopilotHistory.AgentState(alstPids);
                string strSession = ClaudeSession(strDir, alstPids);
                if (strSession == null) return cCopilotHistory.AgentState(alstPids);   // Copilot, or no agent
                string strFile = TranscriptFile(strDir, strSession);
                if (strFile == null) return 0;      // a new session, nothing asked yet
                // Claude Code now and then writes a megabyte or more of its own bookkeeping (copies
                // of the request it sent, "api-request-blob" lines) after the last message. Read
                // further back until a message decides it (Story 6849, Tony: "the status shows as
                // idle. I am sure you are thinking"), and never call it idle for want of a message.
                long lngLength = new FileInfo(strFile).Length;
                foreach (int intTail in new int[] { CNST_STATE_TAIL_BYTES, 8 * 1024 * 1024, 64 * 1024 * 1024 }) {
                    int intState = StateFromLines(new List<string>(ReadTailLines(strFile, intTail)));
                    if (intState != CNST_UNDECIDED) return intState;
                    if (lngLength <= intTail) return 0;      // the whole session read: nothing asked yet
                }
                return -1;
            } catch (Exception) {
                return -1;
            }
        }

        private const int CNST_UNDECIDED = -2;

        // The state from a transcript's last lines (oldest first), or CNST_UNDECIDED when none
        // of them is a message that decides it.
        private static int StateFromLines(List<string> lstLines) {
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                js.MaxJsonLength = Int32.MaxValue;
                for (int i = lstLines.Count - 1; i >= 0; i--) {
                    string strLine = lstLines[i];
                    // Bookkeeping lines can be large and mention "assistant" in the request they
                    // copy: skip them without reading them as JSON.
                    if (strLine.StartsWith("{\"type\":\"api-", StringComparison.Ordinal)
                        || strLine.StartsWith("{\"type\":\"data-flow", StringComparison.Ordinal)) continue;
                    bool blnAssistant = strLine.IndexOf("\"assistant\"", StringComparison.Ordinal) >= 0;
                    bool blnUser      = strLine.IndexOf("\"user\"", StringComparison.Ordinal) >= 0;
                    if (!blnAssistant && !blnUser) continue;
                    Dictionary<string, object> row;
                    try { row = js.DeserializeObject(strLine) as Dictionary<string, object>; } catch (Exception) { continue; }
                    if (row == null) continue;
                    if (row.ContainsKey("isSidechain") && true.Equals(row["isSidechain"])) continue;
                    string strType = Str(row, "type");
                    Dictionary<string, object> msg = row.ContainsKey("message") ? row["message"] as Dictionary<string, object> : null;
                    if (msg == null) continue;
                    if ("assistant".Equals(strType)) {
                        string strStop = Str(msg, "stop_reason");
                        return "end_turn".Equals(strStop) || "stop_sequence".Equals(strStop) ? 0 : 1;
                    }
                    if (!"user".Equals(strType)) continue;
                    if (row.ContainsKey("isMeta") && true.Equals(row["isMeta"])) continue;
                    if (row.ContainsKey("isCompactSummary") && true.Equals(row["isCompactSummary"])) continue;
                    string strText = TextBlocks(msg, true);
                    if (strText == null) return 1;                  // a tool's result: still working
                    if (strText.TrimStart().StartsWith("[Request interrupted")) return 0;
                    if (IsHarnessText(strText)) {
                        // A slash command (no answer follows) or a note the harness slipped in.
                        string t = strText.TrimStart();
                        if (t.StartsWith("<command-") || t.StartsWith("<local-command")) return 0;
                        continue;
                    }
                    return 1;                                       // your prompt, not yet answered
                }
                return CNST_UNDECIDED;
            }
        }

        // Claude Code: the transcripts of the session and the ones before it (a /clear), oldest
        // first. A prompt is a "user" line whose text is not a tool result, a command or a
        // harness note. An answer is the text of an "assistant" message, its lines joined by
        // message id.
        private static List<cHistoryEntry> ClaudeConversation(List<uint> alstPids, bool ablnAnswersOnly) {
            return ClaudeConversation(alstPids, ablnAnswersOnly, false);
        }

        // ablnStopAtClear: only what was said since the last /clear (Run conversation).
        private static List<cHistoryEntry> ClaudeConversation(List<uint> alstPids, bool ablnAnswersOnly, bool ablnStopAtClear) {
            try {
                string strDir = ClaudeDir();
                if (strDir == null) return null;
                string strSession = ClaudeSession(strDir, alstPids);
                if (strSession == null) return null;
                List<string> lstChain = SessionChainNewestFirst(strDir, strSession, ablnStopAtClear);
                lstChain.Reverse();
                JavaScriptSerializer js = new JavaScriptSerializer();
                js.MaxJsonLength = Int32.MaxValue;
                List<cHistoryEntry> lst = new List<cHistoryEntry>();
                foreach (string strId in lstChain) {
                    string strFile = TranscriptFile(strDir, strId);
                    if (strFile == null) continue;
                    string strLastMsgId = null;
                    foreach (string strLine in ReadTailLines(strFile, CNST_TRANSCRIPT_TAIL_BYTES)) {
                        // Tool output is most of a transcript and is never shown, so it is skipped
                        // before the costly parse. Neither are lines with no message at all.
                        // A prompt typed while the agent is still working is kept as a
                        // "queued_command" attachment, not a user message (Story 6846, Tony: "when
                        // I speak while you are talking, the words I said are never making it to
                        // the output window"). Shown as your prompt, where it reached the agent.
                        if (!ablnAnswersOnly && strLine.IndexOf("\"queued_command\"", StringComparison.Ordinal) >= 0) {
                            string strQueued = QueuedPrompt(js, strLine);
                            if (strQueued != null) { lst.Add(new cHistoryEntry(strQueued, false)); strLastMsgId = null; }
                            continue;
                        }
                        if (strLine.IndexOf("\"tool_result\"", StringComparison.Ordinal) >= 0) continue;
                        if (strLine.IndexOf("\"message\"", StringComparison.Ordinal) < 0) continue;
                        Dictionary<string, object> row;
                        try { row = js.DeserializeObject(strLine) as Dictionary<string, object>; } catch (Exception) { continue; }
                        if (row == null) continue;
                        if (row.ContainsKey("isSidechain") && true.Equals(row["isSidechain"])) continue;
                        string strType = Str(row, "type");
                        Dictionary<string, object> msg = row.ContainsKey("message") ? row["message"] as Dictionary<string, object> : null;
                        if (msg == null) continue;
                        if ("assistant".Equals(strType)) {
                            string strText = TextBlocks(msg, false);
                            if (strText == null) continue;
                            string strMsgId = Str(msg, "id");
                            if (strMsgId != null && strMsgId == strLastMsgId && lst.Count > 0 && lst[lst.Count - 1].IsAnswer)
                                lst[lst.Count - 1].Text += "\n\n" + strText;
                            else
                                lst.Add(new cHistoryEntry(strText, true));
                            strLastMsgId = strMsgId;
                        } else if ("user".Equals(strType) && !ablnAnswersOnly) {
                            if (row.ContainsKey("isMeta") && true.Equals(row["isMeta"])) continue;
                            if (row.ContainsKey("isCompactSummary") && true.Equals(row["isCompactSummary"])) continue;
                            string strText = TextBlocks(msg, true);
                            // A /clear written into this same transcript empties Run conversation too.
                            if (ablnStopAtClear && strText != null && strText.IndexOf("<command-name>/clear</command-name>", StringComparison.Ordinal) >= 0) {
                                lst.Clear();
                                strLastMsgId = null;
                                continue;
                            }
                            if (strText == null || IsHarnessText(strText)) continue;
                            lst.Add(new cHistoryEntry(strText, false));
                            strLastMsgId = null;
                        }
                    }
                }
                return lst;
            } catch (Exception) {
                return null;
            }
        }

        // The prompt of a "queued_command" attachment line, or null when the line is not one (or
        // holds no text). Its prompt is a string, or blocks like a message's content.
        private static string QueuedPrompt(JavaScriptSerializer ajs, string astrLine) {
            Dictionary<string, object> row;
            try { row = ajs.DeserializeObject(astrLine) as Dictionary<string, object>; } catch (Exception) { return null; }
            if (row == null) return null;
            if (row.ContainsKey("isSidechain") && true.Equals(row["isSidechain"])) return null;
            Dictionary<string, object> att = row.ContainsKey("attachment") ? row["attachment"] as Dictionary<string, object> : null;
            if (att == null || !"queued_command".Equals(Str(att, "type")) || !att.ContainsKey("prompt")) return null;
            Dictionary<string, object> fake = new Dictionary<string, object>();
            fake["content"] = att["prompt"];
            string strText = TextBlocks(fake, true);
            return strText == null || IsHarnessText(strText) ? null : strText;
        }

        // The text of a message: its content when that is a string, else its text blocks joined.
        // Null when there is no text, or (ablnRejectToolResults) when it carries a tool result.
        private static string TextBlocks(Dictionary<string, object> amsg, bool ablnRejectToolResults) {
            object o;
            if (!amsg.TryGetValue("content", out o) || o == null) return null;
            string str = o as string;
            if (str != null) return str.Trim().Length == 0 ? null : str;
            object[] blocks = o as object[];
            if (blocks == null) return null;
            List<string> lst = new List<string>();
            foreach (object ob in blocks) {
                Dictionary<string, object> b = ob as Dictionary<string, object>;
                if (b == null) continue;
                string strType = Str(b, "type");
                if (ablnRejectToolResults && "tool_result".Equals(strType)) return null;
                if ("text".Equals(strType) && !String.IsNullOrEmpty(Str(b, "text"))) lst.Add(Str(b, "text"));
            }
            return lst.Count == 0 ? null : String.Join("\n\n", lst.ToArray());
        }

        // Lines the harness writes as if from the user: slash commands, their output, notes.
        private static bool IsHarnessText(string astr) {
            string s = astr.TrimStart();
            return s.StartsWith("<command-") || s.StartsWith("<local-command") || s.StartsWith("<system-reminder>")
                || s.StartsWith("<task-notification>") || s.StartsWith("[Request interrupted")
                || s.StartsWith("Caveat:");
        }

        // One session named in a derived-from line: {"id":"...","via":"clear"}.
        private static readonly Regex RX_FROM_SESSION = new Regex(@"\{[^{}]*""id""\s*:\s*""[^""]+""[^{}]*\}");
        private static readonly Regex RX_VIA_CLEAR    = new Regex(@"""via""\s*:\s*""clear""");

        private static List<string> DerivedFrom(string astrDir, string astrSession, bool ablnStopAtClear) {
            if (!ablnStopAtClear) return DerivedFrom(astrDir, astrSession);
            List<string> lst = new List<string>();
            try {
                if (astrSession.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return lst;
                string strFile = TranscriptFile(astrDir, astrSession);
                if (strFile == null) return lst;
                foreach (string strLine in ReadHeadLines(strFile)) {
                    if (!RX_DERIVED.IsMatch(strLine)) continue;
                    foreach (Match mSession in RX_FROM_SESSION.Matches(strLine)) {
                        if (RX_VIA_CLEAR.IsMatch(mSession.Value)) continue;     // the clear: stop here
                        Match mId = RX_FROM_ID.Match(mSession.Value);
                        if (mId.Success) lst.Add(mId.Groups[1].Value);
                    }
                }
            } catch (Exception) { /* no chain, the session alone */ }
            return lst;
        }

        private static List<string> DerivedFrom(string astrDir, string astrSession) {
            List<string> lst = new List<string>();
            try {
                if (astrSession.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return lst;
                string strProjects = Path.Combine(astrDir, "projects");
                if (!Directory.Exists(strProjects)) return lst;
                foreach (string strFolder in Directory.GetDirectories(strProjects)) {
                    string strFile = Path.Combine(strFolder, astrSession + ".jsonl");
                    if (!File.Exists(strFile)) continue;
                    foreach (string strLine in ReadHeadLines(strFile)) {
                        if (!RX_DERIVED.IsMatch(strLine)) continue;
                        foreach (Match m in RX_FROM_ID.Matches(strLine)) lst.Add(m.Groups[1].Value);
                    }
                    break;
                }
            } catch (Exception) { /* no chain, the session alone */ }
            return lst;
        }

        // The first CNST_HEAD_BYTES of a transcript as whole lines: the derived-from line is near the top.
        private static List<string> ReadHeadLines(string astrFile) {
            List<string> lst = new List<string>();
            using (FileStream fs = new FileStream(astrFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                byte[] buf = new byte[(int)Math.Min(fs.Length, CNST_HEAD_BYTES)];
                int intRead = 0;
                while (intRead < buf.Length) {
                    int n = fs.Read(buf, intRead, buf.Length - intRead);
                    if (n <= 0) break;
                    intRead += n;
                }
                string[] lines = Encoding.UTF8.GetString(buf, 0, intRead).Split('\n');
                // The last line may be cut off unless the whole file was read.
                int intEnd = intRead < fs.Length ? lines.Length - 1 : lines.Length;
                for (int i = 0; i < intEnd; i++) lst.Add(lines[i].TrimEnd('\r'));
            }
            return lst;
        }

        private static List<string> ReadSessions(string astrDir, HashSet<string> asetSessions, int aintMax) {
            List<string> lst = new List<string>();
            string strFile = Path.Combine(astrDir, "history.jsonl");
            if (!File.Exists(strFile)) return lst;
            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = Int32.MaxValue;
            foreach (string strLine in ReadTailLines(strFile)) {
                Match mSession = RX_SESSION.Match(strLine);
                if (!mSession.Success || !asetSessions.Contains(mSession.Groups[1].Value)) continue;
                Dictionary<string, object> row;
                try { row = js.DeserializeObject(strLine) as Dictionary<string, object>; } catch (Exception) { continue; }
                string strRowSession = row == null || !row.ContainsKey("sessionId") ? null : row["sessionId"] as string;
                if (strRowSession == null || !asetSessions.Contains(strRowSession)) continue;
                string strText = Expand(astrDir, row);
                if (String.IsNullOrEmpty(strText)) continue;
                if (lst.Count > 0 && lst[lst.Count - 1] == strText) continue;
                lst.Add(strText);
            }
            if (lst.Count > aintMax) lst.RemoveRange(0, lst.Count - aintMax);
            return lst;
        }

        // The prompt as typed: "display", with each "[Pasted text #N ...]" replaced by the text.
        private static string Expand(string astrDir, Dictionary<string, object> arow) {
            string strDisplay = arow.ContainsKey("display") ? arow["display"] as string : null;
            if (strDisplay == null) return null;
            Dictionary<string, object> pasted = arow.ContainsKey("pastedContents")
                ? arow["pastedContents"] as Dictionary<string, object> : null;
            if (pasted == null || pasted.Count == 0) return strDisplay;
            return RX_PASTED.Replace(strDisplay, delegate(Match m) {
                object o;
                if (!pasted.TryGetValue(m.Groups[1].Value, out o)) return m.Value;
                Dictionary<string, object> paste = o as Dictionary<string, object>;
                if (paste == null) return m.Value;
                if (paste.ContainsKey("content") && paste["content"] is string) return (string)paste["content"];
                // Larger pastes may be kept apart, in paste-cache\<hash>.txt.
                string strHash = paste.ContainsKey("contentHash") ? paste["contentHash"] as string : null;
                if (!String.IsNullOrEmpty(strHash) && strHash.IndexOfAny(Path.GetInvalidFileNameChars()) < 0) {
                    string strCache = Path.Combine(Path.Combine(astrDir, "paste-cache"), strHash + ".txt");
                    if (File.Exists(strCache)) return ReadShared(strCache);
                }
                return m.Value;
            });
        }

        // The last CNST_TAIL_BYTES of the file as whole lines. Claude has it open, so share it.
        internal static IEnumerable<string> ReadTailLines(string astrFile) {
            return ReadTailLines(astrFile, CNST_TAIL_BYTES);
        }

        // A transcript holds the whole conversation, tool output and all: read up to this much of
        // its end for Up's conversation and answers lists (Story 6828).
        private const int CNST_TRANSCRIPT_TAIL_BYTES = 32 * 1024 * 1024;

        internal static IEnumerable<string> ReadTailLines(string astrFile, int aintTailBytes) {
            byte[] buf;
            using (FileStream fs = new FileStream(astrFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                long lngStart = Math.Max(0, fs.Length - aintTailBytes);
                fs.Seek(lngStart, SeekOrigin.Begin);
                buf = new byte[fs.Length - lngStart];
                int intRead = 0;
                while (intRead < buf.Length) {
                    int n = fs.Read(buf, intRead, buf.Length - intRead);
                    if (n <= 0) break;
                    intRead += n;
                }
                string strText = Encoding.UTF8.GetString(buf, 0, intRead);
                string[] lines = strText.Split('\n');
                // The first line is cut in half unless the read began at the start of the file.
                int intFirst = lngStart > 0 ? 1 : 0;
                List<string> lst = new List<string>();
                for (int i = intFirst; i < lines.Length; i++) {
                    string s = lines[i].TrimEnd('\r');
                    if (s.Length > 0) lst.Add(s);
                }
                return lst;
            }
        }

        internal static string ReadShared(string astrFile) {
            using (FileStream fs = new FileStream(astrFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8)) {
                return sr.ReadToEnd();
            }
        }
    }
}
