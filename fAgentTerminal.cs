using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace AgentDockStandalone {

    public partial class fAgentTerminal : Form {

        private readonly int    mintPaneNumber;
        private readonly string mstrCwd;
        private Process         mproc;
        private IntPtr          mhwndConsole = IntPtr.Zero;
        private bool            mblnClosing;

        public fAgentTerminal(int aintPaneNumber, string astrCwd) {
            InitializeComponent();
            mintPaneNumber = aintPaneNumber;
            mstrCwd        = astrCwd;
            this.Text      = "Agent " + mintPaneNumber + "  \u00B7  " + mstrCwd;
        }

        // -------------------------------------------------------------------
        // DISABLED LAUNCH PATH -- DO NOT RE-ENABLE.
        //
        // This file used to start an agent directly with cmd.exe and reparent the raw console
        // window into this MDI child. That path is wrong for three reasons:
        //
        //   1. It bypasses the pane batch (config\pane.bat), the one place that says which
        //      agent runs, in which folder, with which options.
        //   2. It uses the raw console instead of Windows Terminal (wt.exe): wrong font, no
        //      profile, no colors, broken scroll. The wt.exe path inherits the user's Windows
        //      Terminal settings and looks right.
        //   3. The dock must have EXACTLY ONE way to launch a pane, through wt.exe and the pane
        //      batch. A second mechanism silently changes behavior depending on which path fires.
        //
        // The launch lives in fAgentDockMain.SpawnPane():
        //     wt.exe --window new cmd /k "<config\pane.bat>"
        //
        // The original code is preserved below inside /* ... */ for archaeology.
        // If anything ever needs the reparent-into-MDI trick again, REWRITE it
        // to launch wt.exe with the bat, not cmd.exe with claude.
        // -------------------------------------------------------------------

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            // Disabled per the comment above. fAgentTerminal is intentionally
            // inert -- the active code paths use fAgentAbsorbedTerminal and the
            // wt.exe + Claude-Code2.bat spawn in fAgentDockMain.
            this.Close();
        }

        /*  ORIGINAL bypass code -- DO NOT UNCOMMENT without rewriting to use
            wt.exe + Claude-Code2.bat. See the disabled-launch note above.

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            try {
                LaunchAndEmbed();
            } catch (Exception ex) {
                MessageBox.Show(this,
                    "Could not launch the agent in this pane:\r\n\r\n" + ex.Message,
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LaunchAndEmbed() {
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/k claude");
            psi.UseShellExecute  = true;
            psi.WindowStyle      = ProcessWindowStyle.Minimized;
            psi.WorkingDirectory = Directory.Exists(mstrCwd)
                                     ? mstrCwd
                                     : (Environment.GetEnvironmentVariable("USERPROFILE") ?? "");

            mproc = Process.Start(psi);
            if (mproc == null) {
                throw new InvalidOperationException("Process.Start returned null.");
            }

            DateTime tEnd = DateTime.Now.AddSeconds(5);
            while (DateTime.Now < tEnd) {
                mproc.Refresh();
                if (mproc.MainWindowHandle != IntPtr.Zero) break;
                Thread.Sleep(50);
            }
            mhwndConsole = mproc.MainWindowHandle;
            if (mhwndConsole == IntPtr.Zero) {
                throw new InvalidOperationException(
                    "Timed out waiting for the console window.");
            }

            int style = Win32.GetWindowLong(mhwndConsole, Win32.GWL_STYLE);
            style &= ~(Win32.WS_CAPTION | Win32.WS_THICKFRAME | Win32.WS_SYSMENU
                       | Win32.WS_BORDER | Win32.WS_DLGFRAME | Win32.WS_MINIMIZE | Win32.WS_MAXIMIZE);
            Win32.SetWindowLong(mhwndConsole, Win32.GWL_STYLE, style);

            int ex = Win32.GetWindowLong(mhwndConsole, Win32.GWL_EXSTYLE);
            ex &= ~(Win32.WS_EX_DLGMODALFRAME | Win32.WS_EX_CLIENTEDGE
                    | Win32.WS_EX_STATICEDGE | Win32.WS_EX_WINDOWEDGE);
            Win32.SetWindowLong(mhwndConsole, Win32.GWL_EXSTYLE, ex);

            Win32.SetParent(mhwndConsole, this.Handle);
            Win32.MoveWindow(mhwndConsole, 0, 0, this.ClientSize.Width, this.ClientSize.Height, true);
            Win32.ShowWindow(mhwndConsole, Win32.SW_SHOWNORMAL);
            EnableVirtualTerminalInput();
            FocusConsole();
            ScrollToCursor();

            mproc.EnableRaisingEvents = true;
            mproc.Exited += new EventHandler(Process_Exited);
        }

        */

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            if (mhwndConsole != IntPtr.Zero && Win32.IsWindow(mhwndConsole)) {
                Win32.MoveWindow(mhwndConsole, 0, 0,
                                 this.ClientSize.Width, this.ClientSize.Height, true);
                ScrollToCursor();
            }
        }

        // Scroll the embedded console so the cursor is visible near the bottom
        // of the viewport. SB_BOTTOM overshoots after long agent runs because
        // Claude Code renders its TUI above the raw tool output.
        private void ScrollToCursor() {
            if (mhwndConsole == IntPtr.Zero || !Win32.IsWindow(mhwndConsole)) return;
            if (mproc == null || mproc.HasExited) return;

            bool attached = false;
            try {
                if (!Win32.AttachConsole((uint)mproc.Id)) {
                    Win32.SendMessage(mhwndConsole, Win32.WM_VSCROLL,
                                      (IntPtr)Win32.SB_BOTTOM, IntPtr.Zero);
                    return;
                }
                attached = true;

                IntPtr hOut = Win32.GetStdHandle(Win32.STD_OUTPUT_HANDLE);
                Win32.CONSOLE_SCREEN_BUFFER_INFO info;
                if (!Win32.GetConsoleScreenBufferInfo(hOut, out info)) {
                    Win32.SendMessage(mhwndConsole, Win32.WM_VSCROLL,
                                      (IntPtr)Win32.SB_BOTTOM, IntPtr.Zero);
                    return;
                }

                int curY   = info.dwCursorPosition.Y;
                int winH   = info.srWindow.Bottom - info.srWindow.Top;
                int newTop = Math.Max(0, curY - winH + 2);
                int newBot = newTop + winH;
                if (newBot >= info.dwSize.Y) {
                    newBot = info.dwSize.Y - 1;
                    newTop = Math.Max(0, newBot - winH);
                }

                var rect = new Win32.SMALL_RECT {
                    Top   = (short)newTop,
                    Bottom = (short)newBot,
                    Left  = info.srWindow.Left,
                    Right = info.srWindow.Right,
                };
                // Plain positional argument so Windows' own C# 5 compiler (csc.exe in the .NET
                // Framework folder) builds this on a machine with no Visual Studio (Story 6817).
                Win32.SetConsoleWindowInfo(hOut, true, ref rect);
            } catch { }
            finally {
                if (attached) Win32.FreeConsole();
            }
        }

        protected override void OnActivated(EventArgs e) {
            base.OnActivated(e);
            FocusConsole();
        }

        private void FocusConsole() {
            if (mhwndConsole == IntPtr.Zero || !Win32.IsWindow(mhwndConsole)) return;
            uint consoleThread = Win32.GetWindowThreadProcessId(mhwndConsole, IntPtr.Zero);
            uint thisThread    = Win32.GetCurrentThreadId();
            bool attached = consoleThread != thisThread
                         && Win32.AttachThreadInput(thisThread, consoleThread, true);
            Win32.SetFocus(mhwndConsole);
            if (attached) Win32.AttachThreadInput(thisThread, consoleThread, false);
        }

        private void EnableVirtualTerminalInput() {
            bool attached = false;
            try {
                if (!Win32.AttachConsole((uint)mproc.Id)) return;
                attached = true;
                IntPtr hIn = Win32.GetStdHandle(Win32.STD_INPUT_HANDLE);
                uint mode;
                if (Win32.GetConsoleMode(hIn, out mode))
                    Win32.SetConsoleMode(hIn, mode | Win32.ENABLE_VIRTUAL_TERMINAL_INPUT);
            } catch { }
            finally { if (attached) Win32.FreeConsole(); }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            if (keyData == (Keys.Alt | Keys.F4) || keyData == (Keys.Control | Keys.F4))
                return base.ProcessCmdKey(ref msg, keyData);
            if (mhwndConsole != IntPtr.Zero && Win32.IsWindow(mhwndConsole)) {
                Win32.PostMessage(mhwndConsole, (uint)msg.Msg, msg.WParam, msg.LParam);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Process_Exited(object sender, EventArgs e) {
            if (mblnClosing || this.IsDisposed) return;
            try {
                if (this.InvokeRequired) {
                    this.BeginInvoke((MethodInvoker)delegate {
                        if (!this.IsDisposed) this.Close();
                    });
                } else {
                    this.Close();
                }
            } catch { }
        }

        // taskkill MUST run in OnFormClosed (post-commit), NOT OnFormClosing
        // (pre-commit). WinForms MDI raises the child's FormClosing BEFORE the
        // parent's -- so doing destructive work in the child's FormClosing kills
        // claude even when the user is about to click Cancel on the dock's
        // confirm dialog. OnFormClosed only fires after the close has actually
        // committed; if the parent cancels, this never runs.
        protected override void OnFormClosed(FormClosedEventArgs e) {
            base.OnFormClosed(e);
            mblnClosing = true;
            try {
                if (mproc != null && !mproc.HasExited) {
                    ProcessStartInfo kill = new ProcessStartInfo(
                        "taskkill.exe",
                        "/F /T /PID " + mproc.Id);
                    kill.CreateNoWindow  = true;
                    kill.UseShellExecute = false;
                    kill.WindowStyle     = ProcessWindowStyle.Hidden;
                    Process p = Process.Start(kill);
                    if (p != null) p.WaitForExit(2000);
                }
            } catch { }
        }
    }
}
