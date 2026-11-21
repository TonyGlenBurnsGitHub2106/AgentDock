using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// Random Windows Terminal tab colors for the Agent Dock (Tony, 2026-10-16).
//
// Both docks compile this one file: AgentDockStandalone owns it and AgentDock links it, the same
// arrangement as cPaneArranger.cs. It is self-contained -- its own P/Invokes -- so neither dock's
// Win32.cs had to change. Admin panes are treated like any other pane -- no fixed admin color --
// because every terminal may end up launched as admin.
//
// Two ways a tab gets its color, both drawn from the same bag so they never collide:
//   * A terminal the dock LAUNCHES gets wt.exe --tabColor "#RRGGBB" (TabColorArgument).
//   * A terminal the dock ABSORBS already exists, so wt.exe cannot reach it. ColorExistingWindow
//     writes the DECAC escape sequence ESC [ 2 ; 15 ; <index> , | into the tab's own console,
//     which Windows Terminal reads as "set this tab's color to palette entry <index>". To find
//     that console: every tab's shell is a direct child of WindowsTerminal.exe (all windows share
//     one WindowsTerminal.exe process), and each shell's console window is OWNED by the
//     Windows Terminal window hosting it -- so attach to each candidate shell's console and keep
//     the one whose console window's owner is the window being absorbed. Verified 2026-10-16.
//     DECAC takes palette indexes, not RGB, which is why the palette is the twelve color-scheme
//     colors (indexes 1-6 and 9-14); the hex values are the default Campbell scheme's, so a
//     launched tab and an absorbed tab with the same entry look alike.
//
// Known limits: a tab launched with --tabColor ignores DECAC (Windows Terminal precedence), and
// an elevated terminal cannot be attached to from a non-elevated dock (it keeps its color).
//
// The palette is drawn as a shuffle bag: every color is used once, in random order, before any
// color repeats, so panes opened together never share a color.

namespace AgentDockShared {

    public static class cTabColor {

        private struct TabColorEntry {
            public string Hex;
            public int    PaletteIndex;
            public TabColorEntry(string astrHex, int aintPaletteIndex) {
                Hex          = astrHex;
                PaletteIndex = aintPaletteIndex;
            }
        }

        private static readonly TabColorEntry[] CNST_PALETTE = new TabColorEntry[] {
            new TabColorEntry("#C50F1F",  1),   // red
            new TabColorEntry("#13A10E",  2),   // green
            new TabColorEntry("#C19C00",  3),   // yellow
            new TabColorEntry("#0037DA",  4),   // blue
            new TabColorEntry("#881798",  5),   // purple
            new TabColorEntry("#3A96DD",  6),   // cyan
            new TabColorEntry("#E74856",  9),   // bright red
            new TabColorEntry("#16C60C", 10),   // bright green
            new TabColorEntry("#F9F1A5", 11),   // bright yellow
            new TabColorEntry("#3B78FF", 12),   // bright blue
            new TabColorEntry("#B4009E", 13),   // bright purple
            new TabColorEntry("#61D6D6", 14)    // bright cyan
        };

        private const string CNST_TERMINAL_EXE = "WindowsTerminal.exe";
        private const string CNST_CONHOST_EXE  = "OpenConsole.exe";

        // DECAC (assign color): ESC [ 2 ; <fg> ; <bg> , |  -- item 2 is the tab; Windows Terminal
        // ignores the foreground, so 15 is a placeholder. ESC is written as the \u001b escape to stay ASCII.
        private const string CNST_DECAC_PREFIX = "\u001b[2;15;";
        private const string CNST_DECAC_SUFFIX = ",|";

        private static readonly Random              mrnd    = new Random();
        private static readonly List<TabColorEntry> mlstBag = new List<TabColorEntry>();

        // The next color from the bag, refilling it when empty.
        private static TabColorEntry Next() {
            if (mlstBag.Count == 0) mlstBag.AddRange(CNST_PALETTE);
            int           intIndex = mrnd.Next(mlstBag.Count);
            TabColorEntry entry    = mlstBag[intIndex];
            mlstBag.RemoveAt(intIndex);
            return entry;
        }

        // The wt.exe argument for the next color, with a trailing space so it can be
        // placed directly ahead of the command line.
        public static string TabColorArgument() {
            return "--tabColor \"" + Next().Hex + "\" ";
        }

        // Colors the tab(s) of an already-running Windows Terminal window. Returns true when at
        // least one tab was colored. Never throws -- a tab that cannot be colored keeps its color.
        public static bool ColorExistingWindow(IntPtr ahwndTerminal) {
            if (ahwndTerminal == IntPtr.Zero) return false;
            try {
                List<uint> lstShells = FindTerminalShells();
                if (lstShells.Count == 0) return false;

                // Drawn on the first matching tab only, so a window that cannot be reached
                // (elevated, already closed) does not use up a color.
                string strSequence = null;
                bool   blnColored  = false;
                foreach (uint intPid in lstShells) {
                    if (WriteToShellConsole(intPid, ahwndTerminal, ref strSequence)) blnColored = true;
                }
                return blnColored;
            } catch (Exception) {
                return false;
            }
        }

        // Writes the DECAC sequence to the console of process aintPid when that console belongs to
        // ahwndTerminal, building the sequence from the bag the first time it is needed. Always
        // detaches before returning.
        private static bool WriteToShellConsole(uint aintPid, IntPtr ahwndTerminal, ref string astrSequence) {
            FreeConsole();
            if (!AttachConsole(aintPid)) return false;
            try {
                IntPtr hwndConsole = GetConsoleWindow();
                if (hwndConsole == IntPtr.Zero || GetWindow(hwndConsole, GW_OWNER) != ahwndTerminal) return false;

                IntPtr hOut = CreateFile("CONOUT$", GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (hOut == INVALID_HANDLE_VALUE) return false;
                try {
                    if (astrSequence == null) astrSequence = CNST_DECAC_PREFIX + Next().PaletteIndex + CNST_DECAC_SUFFIX;
                    uint intMode;
                    if (GetConsoleMode(hOut, out intMode))
                        SetConsoleMode(hOut, intMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
                    uint intWritten;
                    return WriteConsole(hOut, astrSequence, (uint)astrSequence.Length, out intWritten, IntPtr.Zero);
                } finally {
                    CloseHandle(hOut);
                }
            } finally {
                FreeConsole();
            }
        }

        // Every process whose parent is WindowsTerminal.exe, except its OpenConsole.exe hosts --
        // that is, the shell running in each tab.
        private static List<uint> FindTerminalShells() {
            List<uint>           lstShells   = new List<uint>();
            List<uint>           lstTerminal = new List<uint>();
            List<PROCESSENTRY32> lstAll      = new List<PROCESSENTRY32>();

            IntPtr hSnap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (hSnap == INVALID_HANDLE_VALUE) return lstShells;
            try {
                PROCESSENTRY32 pe = new PROCESSENTRY32();
                pe.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                if (Process32First(hSnap, ref pe)) {
                    do {
                        lstAll.Add(pe);
                        if (string.Equals(pe.szExeFile, CNST_TERMINAL_EXE, StringComparison.OrdinalIgnoreCase))
                            lstTerminal.Add(pe.th32ProcessID);
                    } while (Process32Next(hSnap, ref pe));
                }
            } finally {
                CloseHandle(hSnap);
            }

            foreach (PROCESSENTRY32 pe in lstAll) {
                if (lstTerminal.Contains(pe.th32ParentProcessID)
                    && !string.Equals(pe.szExeFile, CNST_CONHOST_EXE, StringComparison.OrdinalIgnoreCase))
                    lstShells.Add(pe.th32ProcessID);
            }
            return lstShells;
        }

        // -------------------------------------------------------------------
        // P/Invoke

        private const uint   TH32CS_SNAPPROCESS                 = 0x00000002;
        private const uint   GW_OWNER                           = 4;
        private const uint   GENERIC_READ                       = 0x80000000;
        private const uint   GENERIC_WRITE                      = 0x40000000;
        private const uint   FILE_SHARE_READ                    = 0x00000001;
        private const uint   FILE_SHARE_WRITE                   = 0x00000002;
        private const uint   OPEN_EXISTING                      = 3;
        private const uint   ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private static readonly IntPtr INVALID_HANDLE_VALUE     = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PROCESSENTRY32 {
            public uint   dwSize;
            public uint   cntUsage;
            public uint   th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint   th32ModuleID;
            public uint   cntThreads;
            public uint   th32ParentProcessID;
            public int    pcPriClassBase;
            public uint   dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool WriteConsole(IntPtr hConsoleOutput, string lpBuffer, uint nNumberOfCharsToWrite,
            out uint lpNumberOfCharsWritten, IntPtr lpReserved);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
