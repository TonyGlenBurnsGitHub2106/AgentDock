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
            return ConversationForPids(alstPids, ablnAnswersOnly, false);
        }

        // ablnStopAtClear (Story 6846, Run conversation): only what was said since the last clear,
        // as for Claude Code. Copilot's /clear is not documented, so both likely shapes are
        // handled: a new session (only the newest session is read), or a mark in the same file
        // (an event whose type names a clear, or a "/clear" prompt), which empties what came before.
        public static List<cHistoryEntry> ConversationForPids(List<uint> alstPids, bool ablnAnswersOnly, bool ablnStopAtClear) {
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
                    if (ablnStopAtClear && lstSessions.Count > 1) lstSessions.RemoveRange(0, lstSessions.Count - 1);
                    List<cHistoryEntry> lst = new List<cHistoryEntry>();
                    bool blnFound = false;
                    foreach (string strSession in lstSessions) {
                        string strFile = EventsFile(strStates, strSession);
                        if (strFile == null) continue;
                        blnFound = true;
                        foreach (string strLine in cAgentHistory.ReadTailLines(strFile)) {
                            if (ablnStopAtClear && IsClearLine(strLine)) { lst.Clear(); continue; }
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
                    // A session just cleared is an empty conversation, not "no Copilot here".
                    if (lst.Count > 0 || (ablnStopAtClear && blnFound)) return lst;
                }
                return null;
            } catch (Exception) {
                return null;
            }
        }

        private static string EventsFile(string astrStates, string astrSession) {
            string strFile = Path.Combine(Path.Combine(astrStates, astrSession), "events.jsonl");
            if (File.Exists(strFile)) return strFile;
            strFile = Path.Combine(astrStates, astrSession + ".jsonl");
            return File.Exists(strFile) ? strFile : null;
        }

        private static readonly Regex RX_TYPE = new Regex(@"""type""\s*:\s*""([^""]+)""");

        // The event's type, read without parsing the whole line (lines carrying tool output are long).
        private static string TypeOf(string astrLine) {
            Match m = RX_TYPE.Match(astrLine);
            return m.Success ? m.Groups[1].Value : null;
        }

        // An event marking a clear: a type that names one (session.clear, ...cleared), or a prompt
        // that is exactly "/clear" or "/new".
        private static bool IsClearLine(string astrLine) {
            string strType = TypeOf(astrLine);
            if (strType == null) return false;
            if (strType.IndexOf("clear", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (strType != "user.message") return false;
            return Regex.IsMatch(astrLine, @"""content""\s*:\s*""\s*/(clear|new)\s*""");
        }

        // The newest events file of the pane's Copilot session, or null when there is none.
        private static string NewestEventsFile(List<uint> alstPids) {
            string strDir = CopilotDir();
            if (strDir == null || alstPids == null) return null;
            string strStates = Path.Combine(strDir, "session-state");
            if (!Directory.Exists(strStates)) return null;
            foreach (uint intPid in alstPids) {
                List<string> lstSessions = SessionsOf(strDir, strStates, intPid);
                if (lstSessions.Count == 0) continue;
                string strFile = EventsFile(strStates, lstSessions[lstSessions.Count - 1]);
                if (strFile != null) return strFile;
            }
            return null;
        }

        // A cheap sign of whether the conversation changed: the events file's size and write time.
        public static string Stamp(List<uint> alstPids) {
            try {
                string strFile = NewestEventsFile(alstPids);
                if (strFile == null) return null;
                FileInfo fi = new FileInfo(strFile);
                return strFile + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
            } catch (Exception) {
                return null;
            }
        }

        // Whether Copilot is working on a prompt (1) or waiting for you (0), -1 when unknown
        // (Story 6846, Tony: "Right now everything needs to work for both copilot and claude code").
        // Copilot writes no "done" to its record (its session.idle is never saved, and turn_end
        // also falls between tool rounds), so it is read from the last events:
        //   task complete, shutdown, abort or an error -> idle;
        //   a prompt, a turn starting, a tool running or finished, a message being written -> working;
        //   a turn that ended without running a tool -> idle (that was the answer);
        //   a turn that ended after running tools -> working, as another turn follows, unless nothing
        //   has been written for two minutes (an interrupted turn).
        private const int CNST_TURN_GRACE_SECONDS = 120;

        public static int AgentState(List<uint> alstPids) {
            try {
                string strFile = NewestEventsFile(alstPids);
                if (strFile == null) return -1;
                List<string> lstLines = new List<string>(cAgentHistory.ReadTailLines(strFile, 2 * 1024 * 1024));
                bool blnSawTurnEnd = false;
                for (int i = lstLines.Count - 1; i >= 0; i--) {
                    string strType = TypeOf(lstLines[i]);
                    if (strType == null) continue;
                    if (!blnSawTurnEnd) {
                        if (strType == "session.task_complete" || strType == "session.shutdown" || strType == "abort"
                            || strType.EndsWith(".abort") || strType == "session.error") return 0;
                        if (strType == "assistant.turn_end") { blnSawTurnEnd = true; continue; }
                        if (strType == "user.message" || strType == "assistant.turn_start" || strType == "assistant.message"
                            || strType.StartsWith("tool.") || strType.StartsWith("subagent.")
                            || strType == "session.compaction_start") return 1;
                        continue;
                    }
                    // After the last turn_end, looking back through that turn.
                    if (strType.StartsWith("tool.")) {
                        double dblQuiet = (DateTime.UtcNow - File.GetLastWriteTimeUtc(strFile)).TotalSeconds;
                        return dblQuiet > CNST_TURN_GRACE_SECONDS ? 0 : 1;
                    }
                    if (strType == "assistant.turn_start" || strType == "user.message") return 0;   // a turn with no tools
                }
                return blnSawTurnEnd ? 0 : -1;
            } catch (Exception) {
                return -1;
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

        // Finding a pane's sessions is remembered for a few seconds (Story 6848, Tony: "when I
        // load conversation at work, my cursor becomes very sluggish"). It used to list every
        // session folder and every file in each, for every process in the pane, several times a
        // second for each pane. A Copilot that has been used for months keeps hundreds of session
        // folders: about 120 ms a check with 400 of them. Now one walk of the folders serves
        // every pane and process for CNST_SESSION_CACHE_MS, and each process's answer is kept
        // as long. A new session (a new Copilot, or after /clear) shows up within that time.
        private const int CNST_SESSION_CACHE_MS = 10000;
        private static readonly object moCacheLock = new object();
        private static readonly Dictionary<string, List<string>> mdicPidSessions = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, DateTime> mdicPidSessionsAt = new Dictionary<string, DateTime>();
        private static Dictionary<string, List<string>> mdicMarkers;     // pid digits -> session folders, oldest first
        private static string   mstrMarkersOf;
        private static DateTime mdtMarkers = DateTime.MinValue;
        private static readonly Regex RX_DIGITS = new Regex(@"[0-9]+");

        // The sessions of process aintPid, oldest first (remembered, see above).
        private static List<string> SessionsOf(string astrDir, string astrStates, uint aintPid) {
            string strKey = astrStates + "|" + aintPid;
            lock (moCacheLock) {
                List<string> lstHit;
                DateTime dtAt;
                if (mdicPidSessions.TryGetValue(strKey, out lstHit) && mdicPidSessionsAt.TryGetValue(strKey, out dtAt)
                    && (DateTime.UtcNow - dtAt).TotalMilliseconds < CNST_SESSION_CACHE_MS)
                    return new List<string>(lstHit);
            }
            List<string> lst = FindSessionsOf(astrDir, astrStates, aintPid);
            lock (moCacheLock) {
                if (mdicPidSessions.Count > 500) { mdicPidSessions.Clear(); mdicPidSessionsAt.Clear(); }
                mdicPidSessions[strKey]   = new List<string>(lst);
                mdicPidSessionsAt[strKey] = DateTime.UtcNow;
            }
            return lst;
        }

        // Every session folder with a marker file carrying a pid (inuse.<pid>.lock and the like),
        // by pid. One walk of the folders, remembered for CNST_SESSION_CACHE_MS.
        private static Dictionary<string, List<string>> MarkerMap(string astrStates) {
            lock (moCacheLock) {
                if (mdicMarkers != null && mstrMarkersOf == astrStates
                    && (DateTime.UtcNow - mdtMarkers).TotalMilliseconds < CNST_SESSION_CACHE_MS)
                    return mdicMarkers;
            }
            Dictionary<string, List<DirectoryInfo>> dicFound = new Dictionary<string, List<DirectoryInfo>>();
            foreach (DirectoryInfo diFolder in new DirectoryInfo(astrStates).GetDirectories()) {
                FileInfo[] afiles;
                try { afiles = diFolder.GetFiles(); } catch (Exception) { continue; }
                foreach (FileInfo fi in afiles) {
                    string strLower = fi.Name.ToLowerInvariant();
                    if (!(strLower.Contains("lock") || strLower.Contains("inuse") || strLower.Contains("pid"))) continue;
                    foreach (Match m in RX_DIGITS.Matches(fi.Name)) {
                        List<DirectoryInfo> lstFor;
                        if (!dicFound.TryGetValue(m.Value, out lstFor)) { lstFor = new List<DirectoryInfo>(); dicFound[m.Value] = lstFor; }
                        if (!lstFor.Contains(diFolder)) lstFor.Add(diFolder);
                    }
                }
            }
            Dictionary<string, List<string>> dicMap = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<string, List<DirectoryInfo>> kv in dicFound) {
                kv.Value.Sort(delegate(DirectoryInfo a, DirectoryInfo b) { return a.CreationTimeUtc.CompareTo(b.CreationTimeUtc); });
                List<string> lstNames = new List<string>();
                foreach (DirectoryInfo di in kv.Value) lstNames.Add(di.Name);
                dicMap[kv.Key] = lstNames;
            }
            lock (moCacheLock) {
                mdicMarkers   = dicMap;
                mstrMarkersOf = astrStates;
                mdtMarkers    = DateTime.UtcNow;
            }
            return dicMap;
        }

        private static List<string> FindSessionsOf(string astrDir, string astrStates, uint aintPid) {
            List<string> lst = new List<string>();
            string strPid = aintPid.ToString();

            // 1. A marker file carrying the pid in a session folder.
            List<string> lstMarked;
            if (MarkerMap(astrStates).TryGetValue(strPid, out lstMarked)) lst.AddRange(lstMarked);
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
