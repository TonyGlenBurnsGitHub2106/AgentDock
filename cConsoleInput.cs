using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AgentDockShared;

namespace AgentDockStandalone {

    // Types text into the agent running inside a docked Windows Terminal window (Story 6826, the
    // compose box; proven in Spike 6823).
    //
    // How: every tab's shell is a direct child of WindowsTerminal.exe, and its console window is
    // OWNED by the Windows Terminal window that shows it (the same search cTabColor uses). The dock
    // attaches to that console, opens CONIN$ and writes key events with WriteConsoleInputW. The
    // agent reads them exactly as if they had been typed.
    //
    // Text with more than one line goes in as a bracketed paste (ESC [ 200 ~ ... ESC [ 201 ~), the
    // same bytes Windows Terminal sends when you paste, so the agent keeps the lines instead of
    // sending at the first one. The Enter that sends is written separately, a moment later (the
    // caller waits), because an agent that gets text and Enter in one burst can take the burst for
    // a paste and add a new line instead of sending.
    //
    // Limits: a terminal running as administrator cannot be reached from a dock without admin
    // rights (the dock must be admin to hold such a pane anyway).
    internal static class cConsoleInput {

        private const string CNST_PASTE_START = "\u001b[200~";
        private const string CNST_PASTE_END   = "\u001b[201~";

        // Writes astrText to the console behind the Windows Terminal window ahwndTerminal. Returns
        // null when it was written, or a short reason it could not be.
        public static string TypeText(IntPtr ahwndTerminal, string astrText) {
            if (String.IsNullOrEmpty(astrText)) return null;
            string strText = astrText.Replace("\r\n", "\r").Replace('\n', '\r');
            if (strText.IndexOf('\r') >= 0) strText = CNST_PASTE_START + strText + CNST_PASTE_END;
            return Write(ahwndTerminal, strText);
        }

        // Presses Enter in the console behind ahwndTerminal.
        public static string PressEnter(IntPtr ahwndTerminal) {
            return Write(ahwndTerminal, "\r");
        }

        // Wipes the screen and the scrollback of the terminal behind ahwndTerminal (Story 6846,
        // Clear Screen), the way cls does: the terminal's own clear codes go to the console's
        // OUTPUT, so the agent never sees a key and keeps its conversation. Ctrl+L was tried first
        // and does not do it: Claude Code answers Ctrl+L by drawing the whole conversation again.
        // The agent's prompt box comes back at the top the next time it draws (a key, its timer).
        public static string ClearScreen(IntPtr ahwndTerminal) {
            return WithConsole(ahwndTerminal, delegate {
                IntPtr hOut = CreateFile("CONOUT$", GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (hOut == INVALID_HANDLE_VALUE) return "Could not open the pane's screen.";
                try {
                    // Home, erase the screen, erase the scrollback.
                    string strClear = "\u001b[H\u001b[2J\u001b[3J";
                    uint intWritten;
                    if (!WriteConsole(hOut, strClear, (uint)strClear.Length, out intWritten, IntPtr.Zero))
                        return "Windows refused (error " + Marshal.GetLastWin32Error() + ").";
                    return null;
                } finally {
                    CloseHandle(hOut);
                }
            });
        }

        // Presses keys in the pane (Story 6847, the Keys window): astrKeys goes in as typed, so
        // arrow keys and the like are sent as the codes a terminal sends for them (KeyCode).
        // Nothing is pasted and no Enter is added.
        public static string PressKeys(IntPtr ahwndTerminal, string astrKeys) {
            if (String.IsNullOrEmpty(astrKeys)) return null;
            return Write(ahwndTerminal, astrKeys);
        }

        // What a terminal sends for a key that is not a letter (the codes Windows Terminal sends).
        // Null for a key that has none here.
        public static string KeyCode(System.Windows.Forms.Keys akey) {
            System.Windows.Forms.Keys k = akey & System.Windows.Forms.Keys.KeyCode;
            bool blnShift = (akey & System.Windows.Forms.Keys.Shift) == System.Windows.Forms.Keys.Shift;
            switch (k) {
                case System.Windows.Forms.Keys.Up:       return "\u001b[A";
                case System.Windows.Forms.Keys.Down:     return "\u001b[B";
                case System.Windows.Forms.Keys.Right:    return "\u001b[C";
                case System.Windows.Forms.Keys.Left:     return "\u001b[D";
                case System.Windows.Forms.Keys.Home:     return "\u001b[H";
                case System.Windows.Forms.Keys.End:      return "\u001b[F";
                case System.Windows.Forms.Keys.PageUp:   return "\u001b[5~";
                case System.Windows.Forms.Keys.PageDown: return "\u001b[6~";
                case System.Windows.Forms.Keys.Delete:   return "\u001b[3~";
                case System.Windows.Forms.Keys.Tab:      return blnShift ? "\u001b[Z" : "\t";
                case System.Windows.Forms.Keys.Return:   return "\r";
                case System.Windows.Forms.Keys.Escape:   return "\u001b";
                case System.Windows.Forms.Keys.Back:     return "\u007f";
            }
            return null;
        }

        // The lines the pane shows now (its visible window), or null when it cannot be read.
        // Used by the Keys window and to notice a menu waiting for a pick (cComposeBox).
        public static string[] ReadScreen(IntPtr ahwndTerminal) {
            string[] astrLines = null;
            WithConsole(ahwndTerminal, delegate {
                IntPtr hOut = CreateFile("CONOUT$", GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (hOut == INVALID_HANDLE_VALUE) return "Could not open the pane's screen.";
                try {
                    CONSOLE_SCREEN_BUFFER_INFO info;
                    if (!GetConsoleScreenBufferInfo(hOut, out info)) return "Could not read the pane's screen.";
                    int intTop = info.srWindow.Top, intBottom = info.srWindow.Bottom;
                    int intLeft = info.srWindow.Left, intWidth = info.srWindow.Right - info.srWindow.Left + 1;
                    if (intWidth <= 0 || intBottom < intTop) return "The pane's screen is empty.";
                    if (intBottom - intTop > 300) intTop = intBottom - 300;
                    List<string> lst = new List<string>();
                    for (int y = intTop; y <= intBottom; y++) {
                        System.Text.StringBuilder sb = new System.Text.StringBuilder(intWidth + 1);
                        uint intRead;
                        uint intCoord = (uint)((y << 16) | (intLeft & 0xFFFF));
                        if (!ReadConsoleOutputCharacter(hOut, sb, (uint)intWidth, intCoord, out intRead)) { lst.Add(""); continue; }
                        string strLine = sb.ToString();
                        if (strLine.Length > intRead) strLine = strLine.Substring(0, (int)intRead);
                        lst.Add(strLine.TrimEnd());
                    }
                    astrLines = lst.ToArray();
                    return null;
                } finally {
                    CloseHandle(hOut);
                }
            });
            return astrLines;
        }

        // Attaches to the console behind ahwndTerminal, runs afnc there and lets go again.
        private static string WithConsole(IntPtr ahwndTerminal, Func<string> afnc) {
            if (ahwndTerminal == IntPtr.Zero || !IsWindow(ahwndTerminal)) return "That pane has closed.";
            List<uint> lstShells;
            try { lstShells = cTabColor.FindTerminalShells(); } catch (Exception) { lstShells = new List<uint>(); }
            bool blnAnyAttach = false;
            foreach (uint intPid in lstShells) {
                FreeConsole();
                if (!AttachConsole(intPid)) continue;
                blnAnyAttach = true;
                try {
                    IntPtr hwndConsole = GetConsoleWindow();
                    if (hwndConsole == IntPtr.Zero || GetWindow(hwndConsole, GW_OWNER) != ahwndTerminal) continue;
                    return afnc();
                } finally {
                    FreeConsole();
                }
            }
            return blnAnyAttach || lstShells.Count == 0
                ? "Could not find the agent behind that pane."
                : "Could not reach that pane. A pane running as administrator needs the dock running as administrator.";
        }

        private static string Write(IntPtr ahwndTerminal, string astrKeys) {
            return Write(ahwndTerminal, BuildRecords(astrKeys));
        }

        private static string Write(IntPtr ahwndTerminal, INPUT_RECORD[] arecs) {
            if (ahwndTerminal == IntPtr.Zero || !IsWindow(ahwndTerminal)) return "That pane has closed.";
            List<uint> lstShells;
            try { lstShells = cTabColor.FindTerminalShells(); } catch (Exception) { lstShells = new List<uint>(); }
            bool blnAnyAttach = false;
            foreach (uint intPid in lstShells) {
                FreeConsole();
                if (!AttachConsole(intPid)) continue;
                blnAnyAttach = true;
                try {
                    IntPtr hwndConsole = GetConsoleWindow();
                    if (hwndConsole == IntPtr.Zero || GetWindow(hwndConsole, GW_OWNER) != ahwndTerminal) continue;
                    IntPtr hIn = CreateFile("CONIN$", GENERIC_READ | GENERIC_WRITE,
                        FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                    if (hIn == INVALID_HANDLE_VALUE) return "Could not open the pane's input.";
                    try {
                        INPUT_RECORD[] recs = arecs;
                        uint intWritten;
                        if (!WriteConsoleInput(hIn, recs, (uint)recs.Length, out intWritten))
                            return "Windows refused the typing (error " + Marshal.GetLastWin32Error() + ").";
                        return null;
                    } finally {
                        CloseHandle(hIn);
                    }
                } finally {
                    FreeConsole();
                }
            }
            return blnAnyAttach || lstShells.Count == 0
                ? "Could not find the agent behind that pane."
                : "Could not reach that pane. A pane running as administrator needs the dock running as administrator.";
        }

        // Every process attached to the console behind ahwndTerminal (the shell, the agent and
        // anything they started). Empty when that console cannot be reached. Used to find the
        // agent's own prompt history (cAgentHistory).
        public static List<uint> ConsoleProcesses(IntPtr ahwndTerminal) {
            List<uint> lstFound = new List<uint>();
            if (ahwndTerminal == IntPtr.Zero || !IsWindow(ahwndTerminal)) return lstFound;
            List<uint> lstShells;
            try { lstShells = cTabColor.FindTerminalShells(); } catch (Exception) { return lstFound; }
            foreach (uint intPid in lstShells) {
                FreeConsole();
                if (!AttachConsole(intPid)) continue;
                try {
                    IntPtr hwndConsole = GetConsoleWindow();
                    if (hwndConsole == IntPtr.Zero || GetWindow(hwndConsole, GW_OWNER) != ahwndTerminal) continue;
                    uint[] pids = new uint[64];
                    uint intCount = GetConsoleProcessList(pids, (uint)pids.Length);
                    if (intCount > pids.Length) {
                        pids = new uint[intCount];
                        intCount = GetConsoleProcessList(pids, (uint)pids.Length);
                    }
                    for (int i = 0; i < intCount && i < pids.Length; i++)
                        if (!lstFound.Contains(pids[i])) lstFound.Add(pids[i]);
                } finally {
                    FreeConsole();
                }
            }
            return lstFound;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleProcessList([Out] uint[] lpdwProcessList, uint dwProcessCount);

        // A key down and a key up for each character. Enter carries its virtual key code, so an
        // agent that looks at the key (not the character) sees Enter.
        private static INPUT_RECORD[] BuildRecords(string astrKeys) {
            INPUT_RECORD[] recs = new INPUT_RECORD[astrKeys.Length * 2];
            int i = 0;
            foreach (char c in astrKeys) {
                for (int intDown = 1; intDown >= 0; intDown--) {
                    recs[i].EventType                 = KEY_EVENT;
                    recs[i].KeyEvent.bKeyDown         = intDown;
                    recs[i].KeyEvent.wRepeatCount     = 1;
                    recs[i].KeyEvent.UnicodeChar      = c;
                    recs[i].KeyEvent.wVirtualKeyCode  = (ushort)(c == '\r' ? VK_RETURN : (c == '\u001b' ? VK_ESCAPE : 0));
                    recs[i].KeyEvent.wVirtualScanCode = (ushort)(c == '\r' ? 0x1C : (c == '\u001b' ? 0x01 : 0));
                    i++;
                }
            }
            return recs;
        }

        // -------------------------------------------------------------------
        // P/Invoke

        private const ushort KEY_EVENT        = 1;
        private const ushort VK_RETURN        = 0x0D;
        private const ushort VK_ESCAPE        = 0x1B;
        private const uint   GW_OWNER         = 4;
        private const uint   GENERIC_READ     = 0x80000000;
        private const uint   GENERIC_WRITE    = 0x40000000;
        private const uint   FILE_SHARE_READ  = 0x00000001;
        private const uint   FILE_SHARE_WRITE = 0x00000002;
        private const uint   OPEN_EXISTING    = 3;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        private struct KEY_EVENT_RECORD {
            [FieldOffset(0)]  public int    bKeyDown;
            [FieldOffset(4)]  public ushort wRepeatCount;
            [FieldOffset(6)]  public ushort wVirtualKeyCode;
            [FieldOffset(8)]  public ushort wVirtualScanCode;
            [FieldOffset(10)] public char   UnicodeChar;
            [FieldOffset(12)] public uint   dwControlKeyState;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT_RECORD {
            [FieldOffset(0)] public ushort           EventType;
            [FieldOffset(4)] public KEY_EVENT_RECORD KeyEvent;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD { public short X; public short Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SMALL_RECT { public short Left; public short Top; public short Right; public short Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct CONSOLE_SCREEN_BUFFER_INFO {
            public COORD      dwSize;
            public COORD      dwCursorPosition;
            public ushort     wAttributes;
            public SMALL_RECT srWindow;
            public COORD      dwMaximumWindowSize;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpInfo);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "ReadConsoleOutputCharacterW")]
        private static extern bool ReadConsoleOutputCharacter(IntPtr hConsoleOutput, System.Text.StringBuilder lpCharacter,
            uint nLength, uint dwReadCoord, out uint lpNumberOfCharsRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "WriteConsoleInputW")]
        private static extern bool WriteConsoleInput(IntPtr hConsoleInput, INPUT_RECORD[] lpBuffer, uint nLength,
            out uint lpNumberOfEventsWritten);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "WriteConsoleW")]
        private static extern bool WriteConsole(IntPtr hConsoleOutput, string lpBuffer, uint nNumberOfCharsToWrite,
            out uint lpNumberOfCharsWritten, IntPtr lpReserved);
    }
}
