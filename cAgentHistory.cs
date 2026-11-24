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
    // Other agents (Copilot) keep no such record that the dock knows, so ForPane returns null and
    // the compose box uses its own history. CLAUDE_CONFIG_DIR moves the folder, as Claude allows.
    internal static class cAgentHistory {

        // Only the end of history.jsonl is read: it holds every prompt ever, across all sessions.
        private const int CNST_TAIL_BYTES = 4 * 1024 * 1024;
        private static readonly Regex RX_SESSION = new Regex("\"sessionId\"\\s*:\\s*\"([^\"]+)\"");
        private static readonly Regex RX_PASTED  = new Regex(@"\[Pasted text #(\d+)[^\]]*\]");

        // The prompts of the Claude session in the pane shown by ahwndTerminal, oldest first, at
        // most aintMax of them. Null when no Claude session is found there.
        public static List<string> ForPane(IntPtr ahwndTerminal, int aintMax) {
            try {
                string strDir = ClaudeDir();
                if (strDir == null) return null;
                string strSession = null;
                foreach (uint intPid in cConsoleInput.ConsoleProcesses(ahwndTerminal)) {
                    string strFile = Path.Combine(Path.Combine(strDir, "sessions"), intPid + ".json");
                    if (!File.Exists(strFile)) continue;
                    Match m = RX_SESSION.Match(ReadShared(strFile));
                    if (m.Success) { strSession = m.Groups[1].Value; break; }
                }
                if (strSession == null) return null;
                return ReadSession(strDir, strSession, aintMax);
            } catch (Exception) {
                return null;
            }
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

        private static List<string> ReadSession(string astrDir, string astrSession, int aintMax) {
            List<string> lst = new List<string>();
            string strFile = Path.Combine(astrDir, "history.jsonl");
            if (!File.Exists(strFile)) return lst;
            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = Int32.MaxValue;
            string strQuoted = "\"" + astrSession + "\"";
            foreach (string strLine in ReadTailLines(strFile)) {
                if (strLine.IndexOf(strQuoted, StringComparison.Ordinal) < 0) continue;
                Dictionary<string, object> row;
                try { row = js.DeserializeObject(strLine) as Dictionary<string, object>; } catch (Exception) { continue; }
                if (row == null || !astrSession.Equals(row.ContainsKey("sessionId") ? row["sessionId"] as string : null)) continue;
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
        private static IEnumerable<string> ReadTailLines(string astrFile) {
            byte[] buf;
            using (FileStream fs = new FileStream(astrFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                long lngStart = Math.Max(0, fs.Length - CNST_TAIL_BYTES);
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

        private static string ReadShared(string astrFile) {
            using (FileStream fs = new FileStream(astrFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8)) {
                return sr.ReadToEnd();
            }
        }
    }
}
