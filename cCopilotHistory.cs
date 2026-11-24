using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AgentDockStandalone {

    // The prompts sent in a pane running GitHub Copilot CLI (Story 6828), for the compose box's Up
    // arrow, as cAgentHistory does for Claude Code.
    //
    // Copilot keeps each session in ~\.copilot\session-state\<session id>\events.jsonl, one JSON
    // event per line, and a prompt is {"type":"user.message","data":{"content":"..."}}. Older
    // versions kept one flat file per session, session-state\<session id>.jsonl, same lines.
    //
    // Which session a pane runs is not written down in any documented place, so this tries, for
    // each process on the pane's console:
    //   1. a file in a session folder whose name holds the process id (a lock or marker file),
    //   2. Copilot's own log for that process, ~\.copilot\logs\process-<time>-<pid>.log: every
    //      session id it names that has a session folder, in the order named, so a new session
    //      started in the same pane comes after the one before it.
    // Not tested against a live Copilot when written (it is not installed on the machine this was
    // built on). If Up shows nothing on a Copilot pane, look at those folders on that machine.
    // COPILOT_HOME moves the folder.
    internal static class cCopilotHistory {

        private const int CNST_LOG_TAIL_SESSIONS = 20;
        private static readonly Regex RX_GUID = new Regex(
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

        // The pane's Copilot prompts, oldest first, at most aintMax of them. Null when no Copilot
        // session is found for any of these processes.
        public static List<string> ForPids(List<uint> alstPids, int aintMax) {
            try {
                string strDir = CopilotDir();
                if (strDir == null || alstPids == null || alstPids.Count == 0) return null;
                string strStates = Path.Combine(strDir, "session-state");
                if (!Directory.Exists(strStates)) return null;
                foreach (uint intPid in alstPids) {
                    List<string> lstSessions = SessionsOf(strDir, strStates, intPid);
                    if (lstSessions.Count == 0) continue;
                    List<string> lst = new List<string>();
                    foreach (string strSession in lstSessions) ReadPrompts(strStates, strSession, lst);
                    if (lst.Count == 0) continue;
                    if (lst.Count > aintMax) lst.RemoveRange(0, lst.Count - aintMax);
                    return lst;
                }
                return null;
            } catch (Exception) {
                return null;
            }
        }

        // The pane's most recent Copilot answer (Story 6828, Popup Answer): the last
        // assistant.message with text in its newest session. Null when none is found.
        public static string LastAnswerForPids(List<uint> alstPids) {
            try {
                string strDir = CopilotDir();
                if (strDir == null || alstPids == null) return null;
                string strStates = Path.Combine(strDir, "session-state");
                if (!Directory.Exists(strStates)) return null;
                JavaScriptSerializer js = new JavaScriptSerializer();
                js.MaxJsonLength = Int32.MaxValue;
                foreach (uint intPid in alstPids) {
                    List<string> lstSessions = SessionsOf(strDir, strStates, intPid);
                    for (int i = lstSessions.Count - 1; i >= 0; i--) {
                        string strFile = Path.Combine(Path.Combine(strStates, lstSessions[i]), "events.jsonl");
                        if (!File.Exists(strFile)) strFile = Path.Combine(strStates, lstSessions[i] + ".jsonl");
                        if (!File.Exists(strFile)) continue;
                        string strLast = null;
                        foreach (string strLine in cAgentHistory.ReadTailLines(strFile)) {
                            if (strLine.IndexOf("assistant.message", StringComparison.Ordinal) < 0) continue;
                            Dictionary<string, object> row;
                            try { row = js.DeserializeObject(strLine) as Dictionary<string, object>; } catch (Exception) { continue; }
                            if (row == null || !"assistant.message".Equals(row.ContainsKey("type") ? row["type"] as string : null)) continue;
                            Dictionary<string, object> data = row.ContainsKey("data") ? row["data"] as Dictionary<string, object> : null;
                            string strText = data != null && data.ContainsKey("content") ? data["content"] as string : null;
                            if (!String.IsNullOrEmpty(strText) && strText.Trim().Length > 0) strLast = strText;
                        }
                        if (strLast != null) return strLast;
                    }
                }
                return null;
            } catch (Exception) {
                return null;
            }
        }

        // The pane's Copilot conversation, oldest first (Story 6828): user.message and
        // assistant.message lines in order, or only the answers. Null when no session is found.
        public static List<cHistoryEntry> ConversationForPids(List<uint> alstPids, bool ablnAnswersOnly) {
            try {
                string strDir = CopilotDir();
                if (strDir == null || alstPids == null) return null;
                string strStates = Path.Combine(strDir, "session-state");
                if (!Directory.Exists(strStates)) return null;
                JavaScriptSerializer js = new JavaScriptSerializer();
                js.MaxJsonLength = Int32.MaxValue;
                foreach (uint intPid in alstPids) {
                    List<string> lstSessions = SessionsOf(strDir, strStates, intPid);
                    if (lstSessions.Count == 0) continue;
                    List<cHistoryEntry> lst = new List<cHistoryEntry>();
                    foreach (string strSession in lstSessions) {
                        string strFile = Path.Combine(Path.Combine(strStates, strSession), "events.jsonl");
                        if (!File.Exists(strFile)) strFile = Path.Combine(strStates, strSession + ".jsonl");
                        if (!File.Exists(strFile)) continue;
                        foreach (string strLine in cAgentHistory.ReadTailLines(strFile)) {
                            if (strLine.IndexOf(".message", StringComparison.Ordinal) < 0) continue;
                            Dictionary<string, object> row;
                            try { row = js.DeserializeObject(strLine) as Dictionary<string, object>; } catch (Exception) { continue; }
                            string strType = row != null && row.ContainsKey("type") ? row["type"] as string : null;
                            bool blnAnswer = "assistant.message".Equals(strType);
                            if (!blnAnswer && (ablnAnswersOnly || !"user.message".Equals(strType))) continue;
                            Dictionary<string, object> data = row.ContainsKey("data") ? row["data"] as Dictionary<string, object> : null;
                            string strText = data != null && data.ContainsKey("content") ? data["content"] as string : null;
                            if (String.IsNullOrEmpty(strText) || strText.Trim().Length == 0) continue;
                            lst.Add(new cHistoryEntry(strText, blnAnswer));
                        }
                    }
                    if (lst.Count > 0) return lst;
                }
                return null;
            } catch (Exception) {
                return null;
            }
        }

        private static string CopilotDir() {
            string strDir = Environment.GetEnvironmentVariable("COPILOT_HOME");
            if (String.IsNullOrEmpty(strDir)) {
                string strProfile = Environment.GetEnvironmentVariable("USERPROFILE");
                if (String.IsNullOrEmpty(strProfile)) return null;
                strDir = Path.Combine(strProfile, ".copilot");
            }
            return Directory.Exists(strDir) ? strDir : null;
        }

        // The sessions of process aintPid, oldest first.
        private static List<string> SessionsOf(string astrDir, string astrStates, uint aintPid) {
            List<string> lst = new List<string>();
            string strPid = aintPid.ToString();
            Regex rxPid = new Regex(@"(^|[^0-9])" + strPid + @"([^0-9]|$)");

            // 1. A marker file carrying the pid in a session folder.
            foreach (string strFolder in Directory.GetDirectories(astrStates)) {
                foreach (string strFile in Directory.GetFiles(strFolder)) {
                    string strName = Path.GetFileName(strFile);
                    string strLower = strName.ToLowerInvariant();
                    bool blnMarker = strLower.Contains("lock") || strLower.Contains("inuse") || strLower.Contains("pid");
                    if (blnMarker && rxPid.IsMatch(strName)) { lst.Add(Path.GetFileName(strFolder)); break; }
                }
            }
            if (lst.Count > 0) return lst;

            // 2. The process's own log names its sessions.
            string strLogs = Path.Combine(astrDir, "logs");
            if (!Directory.Exists(strLogs)) return lst;
            foreach (string strLog in Directory.GetFiles(strLogs, "process-*-" + strPid + ".log")) {
                foreach (string strLine in cAgentHistory.ReadTailLines(strLog)) {
                    foreach (Match m in RX_GUID.Matches(strLine)) {
                        string strId = m.Value;
                        if (lst.Contains(strId)) continue;
                        if (Directory.Exists(Path.Combine(astrStates, strId))
                            || File.Exists(Path.Combine(astrStates, strId + ".jsonl"))) lst.Add(strId);
                    }
                }
            }
            if (lst.Count > CNST_LOG_TAIL_SESSIONS) lst.RemoveRange(0, lst.Count - CNST_LOG_TAIL_SESSIONS);
            return lst;
        }

        private static void ReadPrompts(string astrStates, string astrSession, List<string> alst) {
            string strFile = Path.Combine(Path.Combine(astrStates, astrSession), "events.jsonl");
            if (!File.Exists(strFile)) strFile = Path.Combine(astrStates, astrSession + ".jsonl");
            if (!File.Exists(strFile)) return;
            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = Int32.MaxValue;
            foreach (string strLine in cAgentHistory.ReadTailLines(strFile)) {
                if (strLine.IndexOf("user.message", StringComparison.Ordinal) < 0) continue;
                Dictionary<string, object> row;
                try { row = js.DeserializeObject(strLine) as Dictionary<string, object>; } catch (Exception) { continue; }
                if (row == null || !"user.message".Equals(row.ContainsKey("type") ? row["type"] as string : null)) continue;
                Dictionary<string, object> data = row.ContainsKey("data") ? row["data"] as Dictionary<string, object> : null;
                string strText = data != null && data.ContainsKey("content") ? data["content"] as string : null;
                if (String.IsNullOrEmpty(strText)) continue;
                if (alst.Count > 0 && alst[alst.Count - 1] == strText) continue;
                alst.Add(strText);
            }
        }
    }
}
