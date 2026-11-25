using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AgentDockStandalone {

    // The slash commands a pane's agent takes, for the compose box's pick list (Story 6851, Tony:
    // "I have no way to enter /skill and the dropdown of options appear and be selectable").
    //
    // Claude Code: its own commands (a fixed list, below), then the skills and commands it finds
    // on disk, the way it does itself:
    //   <claude folder>\skills\<name>\SKILL.md            your skills
    //   <claude folder>\skills\synced\<id>\<name>\SKILL.md  synced skills, as anthropic-skills:<name>
    //   <claude folder>\commands\<name>.md                 your commands
    //   <pane's folder>\.claude\skills\<name>\SKILL.md     the project's skills
    //   <pane's folder>\.claude\commands\<name>.md         the project's commands
    // A skill's name and description come from its front matter (name:, description:,
    // argument-hint:). GitHub Copilot CLI: its own commands only.
    internal sealed class cSlashCommand {
        public string Name;          // without the slash
        public string Description;
        public string ArgumentHint;  // what to type after it, when the command says
        public cSlashCommand(string astrName, string astrDescription, string astrHint) {
            Name = astrName; Description = astrDescription ?? ""; ArgumentHint = astrHint ?? "";
        }
    }

    internal static class cSlashCommands {

        private static readonly string[] CLAUDE_BUILT_IN = new string[] {
            "add-dir", "Add a working folder",
            "agents", "Manage agents",
            "clear", "Clear the conversation and start fresh",
            "compact", "Shrink the conversation into a summary, keeping its gist",
            "config", "Open the settings",
            "context", "Show how full the context is",
            "cost", "Show what this session has cost",
            "doctor", "Check the Claude Code install",
            "exit", "Quit Claude Code",
            "export", "Save or copy the conversation",
            "help", "Show help",
            "hooks", "Manage hooks",
            "init", "Write a CLAUDE.md for this folder",
            "login", "Sign in",
            "logout", "Sign out",
            "mcp", "Manage MCP servers",
            "memory", "Edit the memory files",
            "model", "Pick the model",
            "permissions", "Manage permissions",
            "plugin", "Manage plugins",
            "release-notes", "Show the release notes",
            "resume", "Resume an earlier conversation",
            "rewind", "Go back to an earlier point in the conversation",
            "status", "Show version, model and account",
            "statusline", "Set up the status line",
            "terminal-setup", "Set up Shift+Enter for new lines",
            "todos", "Show the to-do list",
            "usage", "Show plan usage",
            "vim", "Turn vim editing on or off",
        };

        private static readonly string[] COPILOT_BUILT_IN = new string[] {
            "add-dir", "Add a folder Copilot may use",
            "agent", "Pick an agent",
            "clear", "Clear the conversation",
            "compact", "Shrink the conversation into a summary",
            "context", "Show how full the context is",
            "cwd", "Change the working folder",
            "delegate", "Hand the task to Copilot coding agent",
            "exit", "Quit Copilot",
            "feedback", "Send feedback",
            "help", "Show help",
            "list-dirs", "List the folders Copilot may use",
            "login", "Sign in",
            "logout", "Sign out",
            "mcp", "Manage MCP servers",
            "model", "Pick the model",
            "reset-allowed-tools", "Forget the tools you allowed",
            "resume", "Resume an earlier session",
            "session", "Show this session",
            "share", "Share the session",
            "theme", "Pick the theme",
            "usage", "Show usage",
            "user", "Manage the signed-in user",
        };

        // Read from disk at most every CNST_CACHE_SECONDS per folder: the list opens on a keystroke.
        private const int CNST_CACHE_SECONDS = 30;
        private static readonly Dictionary<string, List<cSlashCommand>> mdicCache = new Dictionary<string, List<cSlashCommand>>();
        private static readonly Dictionary<string, DateTime> mdicCacheAt = new Dictionary<string, DateTime>();

        // Every command for the agent in a pane, by name. ablnCopilot picks Copilot's list,
        // astrCwd is the pane's folder (null when not known).
        public static List<cSlashCommand> For(bool ablnCopilot, string astrCwd) {
            string strKey = (ablnCopilot ? "copilot|" : "claude|") + (astrCwd ?? "");
            DateTime dt;
            if (mdicCache.ContainsKey(strKey) && mdicCacheAt.TryGetValue(strKey, out dt)
                && (DateTime.Now - dt).TotalSeconds < CNST_CACHE_SECONDS)
                return mdicCache[strKey];

            Dictionary<string, cSlashCommand> dic = new Dictionary<string, cSlashCommand>(StringComparer.OrdinalIgnoreCase);
            string[] aBuiltIn = ablnCopilot ? COPILOT_BUILT_IN : CLAUDE_BUILT_IN;
            for (int i = 0; i + 1 < aBuiltIn.Length; i += 2) dic[aBuiltIn[i]] = new cSlashCommand(aBuiltIn[i], aBuiltIn[i + 1], null);

            if (!ablnCopilot) {
                string strClaude = cAgentHistory.ClaudeConfigDir();
                if (strClaude != null) {
                    AddSkills(dic, Path.Combine(strClaude, "skills"), null);
                    string strSynced = Path.Combine(Path.Combine(strClaude, "skills"), "synced");
                    if (Directory.Exists(strSynced)) {
                        try { foreach (string d in Directory.GetDirectories(strSynced)) AddSkills(dic, d, "anthropic-skills:"); }
                        catch (Exception) { }
                    }
                    AddCommands(dic, Path.Combine(strClaude, "commands"));
                }
                if (!String.IsNullOrEmpty(astrCwd)) {
                    string strProject = Path.Combine(astrCwd, ".claude");
                    AddSkills(dic, Path.Combine(strProject, "skills"), null);
                    AddCommands(dic, Path.Combine(strProject, "commands"));
                }
            }

            List<cSlashCommand> lst = new List<cSlashCommand>(dic.Values);
            lst.Sort(delegate(cSlashCommand a, cSlashCommand b) { return String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            mdicCache[strKey]   = lst;
            mdicCacheAt[strKey] = DateTime.Now;
            return lst;
        }

        // The commands matching what is typed after the slash: names starting with it first,
        // then names holding it, each in name order. Everything when nothing is typed yet.
        public static List<cSlashCommand> Matching(List<cSlashCommand> alst, string astrTyped) {
            List<cSlashCommand> lstStart = new List<cSlashCommand>();
            List<cSlashCommand> lstHold  = new List<cSlashCommand>();
            string strTyped = astrTyped ?? "";
            foreach (cSlashCommand c in alst) {
                if (c.Name.StartsWith(strTyped, StringComparison.OrdinalIgnoreCase)) lstStart.Add(c);
                else if (c.Name.IndexOf(strTyped, StringComparison.OrdinalIgnoreCase) >= 0) lstHold.Add(c);
            }
            lstStart.AddRange(lstHold);
            return lstStart;
        }

        // Each <folder>\<name>\SKILL.md. A skill whose front matter says user-invocable: false
        // cannot be typed as a command, so it is left out.
        private static void AddSkills(Dictionary<string, cSlashCommand> adic, string astrFolder, string astrPrefix) {
            if (!Directory.Exists(astrFolder)) return;
            string[] aDirs;
            try { aDirs = Directory.GetDirectories(astrFolder); } catch (Exception) { return; }
            foreach (string d in aDirs) {
                string strFile = Path.Combine(d, "SKILL.md");
                if (!File.Exists(strFile)) continue;
                Dictionary<string, string> fm = FrontMatter(strFile);
                string strInvocable;
                if (fm.TryGetValue("user-invocable", out strInvocable) && strInvocable.Trim().ToLowerInvariant() == "false") continue;
                string strName;
                if (!fm.TryGetValue("name", out strName) || strName.Trim().Length == 0) strName = Path.GetFileName(d);
                strName = (astrPrefix ?? "") + strName.Trim();
                string strDesc, strHint;
                fm.TryGetValue("description", out strDesc);
                fm.TryGetValue("argument-hint", out strHint);
                adic[strName] = new cSlashCommand(strName, strDesc, strHint);
            }
        }

        // Each <folder>\<name>.md. The description is its front matter's, else its first line.
        private static void AddCommands(Dictionary<string, cSlashCommand> adic, string astrFolder) {
            if (!Directory.Exists(astrFolder)) return;
            string[] aFiles;
            try { aFiles = Directory.GetFiles(astrFolder, "*.md"); } catch (Exception) { return; }
            foreach (string f in aFiles) {
                Dictionary<string, string> fm = FrontMatter(f);
                string strName = Path.GetFileNameWithoutExtension(f);
                string strDesc, strHint;
                if (!fm.TryGetValue("description", out strDesc)) strDesc = FirstLine(f);
                fm.TryGetValue("argument-hint", out strHint);
                adic[strName] = new cSlashCommand(strName, strDesc, strHint);
            }
        }

        // The simple "key: value" lines between the first two "---" lines. A value given as
        // ">" or "|" takes the indented lines under it, joined by spaces.
        private static Dictionary<string, string> FrontMatter(string astrFile) {
            Dictionary<string, string> dic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try {
                using (StreamReader sr = new StreamReader(astrFile, Encoding.UTF8)) {
                    string strLine = sr.ReadLine();
                    if (strLine == null || strLine.Trim() != "---") return dic;
                    string strKey = null;
                    StringBuilder sbBlock = null;
                    int intLines = 0;
                    while ((strLine = sr.ReadLine()) != null && intLines++ < 200) {
                        if (strLine.Trim() == "---") break;
                        if (sbBlock != null && strLine.Length > 0 && Char.IsWhiteSpace(strLine[0])) {
                            if (sbBlock.Length > 0) sbBlock.Append(' ');
                            sbBlock.Append(strLine.Trim());
                            continue;
                        }
                        if (sbBlock != null) { dic[strKey] = sbBlock.ToString(); sbBlock = null; }
                        if (strLine.Length == 0 || Char.IsWhiteSpace(strLine[0])) continue;
                        int intColon = strLine.IndexOf(':');
                        if (intColon <= 0) continue;
                        strKey = strLine.Substring(0, intColon).Trim();
                        string strValue = strLine.Substring(intColon + 1).Trim();
                        if (strValue == ">" || strValue == "|" || strValue == ">-" || strValue == "|-") { sbBlock = new StringBuilder(); continue; }
                        if (strValue.Length >= 2 && (strValue[0] == '"' || strValue[0] == '\'') && strValue[strValue.Length - 1] == strValue[0])
                            strValue = strValue.Substring(1, strValue.Length - 2);
                        dic[strKey] = strValue;
                    }
                    if (sbBlock != null) dic[strKey] = sbBlock.ToString();
                }
            } catch (Exception) { }
            return dic;
        }

        private static string FirstLine(string astrFile) {
            try {
                using (StreamReader sr = new StreamReader(astrFile, Encoding.UTF8)) {
                    string s;
                    bool blnInFront = false;
                    int intLines = 0;
                    while ((s = sr.ReadLine()) != null && intLines++ < 50) {
                        if (intLines == 1 && s.Trim() == "---") { blnInFront = true; continue; }
                        if (blnInFront) { if (s.Trim() == "---") blnInFront = false; continue; }
                        s = s.Trim().TrimStart('#').Trim();
                        if (s.Length > 0) return s;
                    }
                }
            } catch (Exception) { }
            return "";
        }
    }
}
