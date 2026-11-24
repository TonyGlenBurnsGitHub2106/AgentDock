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

        private static string Write(IntPtr ahwndTerminal, string astrKeys) {
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
                        INPUT_RECORD[] recs = BuildRecords(astrKeys);
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
    }
}
