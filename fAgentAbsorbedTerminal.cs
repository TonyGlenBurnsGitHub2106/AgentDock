using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace AgentDockStandalone {

    // MDI child that hosts a top-level Windows Terminal HWND inside the dock.
    // On Load: strips chrome and SetParents the foreign HWND into this child.
    // On Close: behaviour depends on `mblnOwnedSpawn` -- see OnFormClosed.
    //
    // ablnOwnedSpawn = false: the terminal predated us; we just absorbed it.
    //   On close we ONLY unparent and restore the chrome so the terminal
    //   returns to the desktop alive.
    //
    // ablnOwnedSpawn = true: WE shelled out to Claude-Code2.bat via wt.exe to
    //   create this terminal, so its cmd.exe + claude tree exists only because
    //   of the dock. On close we additionally PostMessage WM_CLOSE to the WT
    //   HWND so the whole tree dies with the pane and nothing is left
    //   orphaned on the desktop.
    public partial class fAgentAbsorbedTerminal : Form {

        private const int CNST_SC_UNDOCK = 0x1000;

        private IntPtr mhwndAbsorbed = IntPtr.Zero;

        // The frame around a pane with no title bar (Story 6817). 0 when title bars show.
        private const int   CNST_FRAME_PX = 2;
        private static readonly Color CNST_FRAME_IDLE   = Color.FromArgb(70, 70, 70);
        private int mintFrame;

        // Sizes the terminal to fill the pane inside its frame, aintShrink pixels smaller.
        private void PlaceAbsorbed(int aintShrink) {
            if (mhwndAbsorbed == IntPtr.Zero || !Win32.IsWindow(mhwndAbsorbed)) return;
            int w = Math.Max(1, this.ClientSize.Width  - 2 * mintFrame - aintShrink);
            int h = Math.Max(1, this.ClientSize.Height - 2 * mintFrame - aintShrink);
            Win32.MoveWindow(mhwndAbsorbed, mintFrame, mintFrame, w, h, true);
        }
        private int    mintOriginalStyle;
        private int    mintOriginalExStyle;
        private bool   mblnStripped;
        private bool   mblnRestored;
        private bool   mblnOwnedSpawn;
        private Timer  mtmrWatchdog;

        public fAgentAbsorbedTerminal(IntPtr ahwndAbsorbed)
            : this(ahwndAbsorbed, false, false) { }

        public fAgentAbsorbedTerminal(IntPtr ahwndAbsorbed, bool ablnOwnedSpawn)
            : this(ahwndAbsorbed, ablnOwnedSpawn, false) { }

        public fAgentAbsorbedTerminal(IntPtr ahwndAbsorbed, bool ablnOwnedSpawn, bool ablnAdmin) {
            InitializeComponent();
            mhwndAbsorbed  = ahwndAbsorbed;
            mblnOwnedSpawn = ablnOwnedSpawn;
            string strTitle = ReadWindowTitle(ahwndAbsorbed);
            if (ablnAdmin && !strTitle.StartsWith("[ADMIN]"))
                strTitle = "[ADMIN] " + strTitle;
            this.Text = strTitle;
            // config\agentdock.ini HidePaneTitleBars (default true, Story 6817): no title bar on
            // the pane, so the terminal gets that row. Close / Undock live on the Agents menu.
            if (cDockSettings.HidePaneTitleBars) {
                this.FormBorderStyle = FormBorderStyle.None;
                this.ControlBox      = false;
                // With no title bar the panes ran together in one black sheet, so each gets a
                // thin frame, the same on every pane (Tony preferred it to a blue active frame).
                mintFrame      = CNST_FRAME_PX;
                this.BackColor = CNST_FRAME_IDLE;
            }
        }

        public IntPtr AbsorbedHwnd  { get { return mhwndAbsorbed; } }
        public bool   IsOwnedSpawn  { get { return mblnOwnedSpawn; } }

        // Set by "Unparent All" before Close() so an owned-spawn pane is
        // released to the desktop alive instead of WM_CLOSE'd.
        public bool SuppressTerminateOnClose { get; set; }

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            try {
                AbsorbHwnd();
                AppendUndockMenuItem();
            } catch (Exception ex) {
                MessageBox.Show(this,
                    "Could not absorb terminal window:\r\n\r\n" + ex.Message,
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AppendUndockMenuItem() {
            IntPtr hMenu = Win32.GetSystemMenu(this.Handle, false);
            if (hMenu == IntPtr.Zero) return;
            Win32.AppendMenu(hMenu, Win32.MF_SEPARATOR, 0, null);
            Win32.AppendMenu(hMenu, Win32.MF_STRING, (uint)CNST_SC_UNDOCK, "Undock");
        }

        protected override void WndProc(ref Message m) {
            if (m.Msg == Win32.WM_SYSCOMMAND
                && (m.WParam.ToInt32() & 0xFFF0) == CNST_SC_UNDOCK) {
                UndockToDesktop();
                return;
            }
            base.WndProc(ref m);
        }

        // Pops this one terminal out of the dock, alive, centered on the screen. Reached from
        // the pane's own system menu (Undock) and from Agents -> Unparent Active Pane.
        public void UndockToDesktop() {
            SuppressTerminateOnClose = true;
            IntPtr hwnd = mhwndAbsorbed;
            UnparentAndRestore();

            if (hwnd != IntPtr.Zero && Win32.IsWindow(hwnd)) {
                Rectangle wa = Screen.FromControl(this).WorkingArea;
                int intW = (int)(wa.Width  * 0.5);
                int intH = (int)(wa.Height * 0.6);
                int intX = wa.Left + (wa.Width  - intW) / 2;
                int intY = wa.Top  + (wa.Height - intH) / 2;
                Win32.SetWindowPos(hwnd, IntPtr.Zero, intX, intY, intW, intH,
                    Win32.SWP_SHOWWINDOW | Win32.SWP_NOZORDER);
                Win32.SetForegroundWindow(hwnd);
            }

            this.Close();
        }

        private void AbsorbHwnd() {
            if (mhwndAbsorbed == IntPtr.Zero || !Win32.IsWindow(mhwndAbsorbed)) return;

            mintOriginalStyle   = Win32.GetWindowLong(mhwndAbsorbed, Win32.GWL_STYLE);
            mintOriginalExStyle = Win32.GetWindowLong(mhwndAbsorbed, Win32.GWL_EXSTYLE);

            int style = mintOriginalStyle;
            style &= ~(Win32.WS_CAPTION | Win32.WS_THICKFRAME | Win32.WS_SYSMENU
                       | Win32.WS_BORDER | Win32.WS_DLGFRAME);
            Win32.SetWindowLong(mhwndAbsorbed, Win32.GWL_STYLE, style);

            int ex = mintOriginalExStyle;
            ex &= ~(Win32.WS_EX_DLGMODALFRAME | Win32.WS_EX_CLIENTEDGE
                    | Win32.WS_EX_STATICEDGE | Win32.WS_EX_WINDOWEDGE);
            Win32.SetWindowLong(mhwndAbsorbed, Win32.GWL_EXSTYLE, ex);
            mblnStripped = true;

            Win32.SetParent(mhwndAbsorbed, this.Handle);
            PlaceAbsorbed(0);
            Win32.ShowWindow(mhwndAbsorbed, Win32.SW_SHOWNORMAL);
            FocusAbsorbed();
            ScheduleLayoutNudge();
            StartWatchdog();
        }

        // Windows Terminal's XAML pipeline does not lay out the terminal grid
        // properly on the first WM_SIZE after a reparent -- text renders with
        // stale font metrics and the input row is hidden until the user
        // maximize-restores the pane. A deferred "resize jiggle" (1 px
        // smaller, then back) fires a second WM_SIZE once WT's pipeline is
        // past initial init, and the terminal lays out correctly.
        private void ScheduleLayoutNudge() {
            if (mhwndAbsorbed == IntPtr.Zero || !Win32.IsWindow(mhwndAbsorbed)) return;
            Timer t  = new Timer();
            t.Interval = 75;
            t.Tick    += delegate(object s, EventArgs e) {
                t.Stop();
                t.Dispose();
                if (this.IsDisposed) return;
                if (mhwndAbsorbed == IntPtr.Zero || !Win32.IsWindow(mhwndAbsorbed)) return;
                if (this.ClientSize.Width <= 1 + 2 * mintFrame || this.ClientSize.Height <= 1 + 2 * mintFrame) return;
                PlaceAbsorbed(1);
                PlaceAbsorbed(0);
            };
            t.Start();
        }

        // Poll the absorbed HWND every 500 ms -- when it stops being a valid
        // window (user typed `exit` in cmd, claude terminated, WT itself
        // closed the tab), close this MDI child too so the dock doesn't
        // accumulate empty frames around dead terminals.
        private void StartWatchdog() {
            if (mtmrWatchdog != null) return;
            mtmrWatchdog          = new Timer();
            mtmrWatchdog.Interval = 500;
            mtmrWatchdog.Tick    += new EventHandler(Watchdog_Tick);
            mtmrWatchdog.Start();
        }

        private void Watchdog_Tick(object sender, EventArgs e) {
            if (this.IsDisposed) return;
            if (mhwndAbsorbed != IntPtr.Zero && Win32.IsWindow(mhwndAbsorbed)) return;
            mhwndAbsorbed = IntPtr.Zero;
            mblnRestored  = true;
            if (mtmrWatchdog != null) {
                mtmrWatchdog.Stop();
                mtmrWatchdog.Dispose();
                mtmrWatchdog = null;
            }
            try { this.Close(); } catch { /* form already closing */ }
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            if (mhwndAbsorbed != IntPtr.Zero && Win32.IsWindow(mhwndAbsorbed)) {
                PlaceAbsorbed(0);
            }
        }

        protected override void OnActivated(EventArgs e) {
            base.OnActivated(e);
            FocusAbsorbed();
        }

        private void FocusAbsorbed() {
            if (mhwndAbsorbed == IntPtr.Zero || !Win32.IsWindow(mhwndAbsorbed)) return;
            uint termThread = Win32.GetWindowThreadProcessId(mhwndAbsorbed, IntPtr.Zero);
            uint thisThread = Win32.GetCurrentThreadId();
            bool attached = termThread != thisThread
                         && Win32.AttachThreadInput(thisThread, termThread, true);
            Win32.SetFocus(mhwndAbsorbed);
            if (attached) Win32.AttachThreadInput(thisThread, termThread, false);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            if (keyData == (Keys.Alt | Keys.F4) || keyData == (Keys.Control | Keys.F4))
                return base.ProcessCmdKey(ref msg, keyData);
            if (mhwndAbsorbed != IntPtr.Zero && Win32.IsWindow(mhwndAbsorbed)) {
                Win32.PostMessage(mhwndAbsorbed, (uint)msg.Msg, msg.WParam, msg.LParam);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void UnparentAndRestore() {
            if (mblnRestored) return;
            if (mhwndAbsorbed == IntPtr.Zero || !Win32.IsWindow(mhwndAbsorbed)) {
                mblnRestored = true;
                return;
            }
            try {
                Win32.SetParent(mhwndAbsorbed, IntPtr.Zero);
                if (mblnStripped) {
                    Win32.SetWindowLong(mhwndAbsorbed, Win32.GWL_STYLE,   mintOriginalStyle);
                    Win32.SetWindowLong(mhwndAbsorbed, Win32.GWL_EXSTYLE, mintOriginalExStyle);
                    Win32.SetWindowPos(mhwndAbsorbed, IntPtr.Zero, 0, 0, 0, 0,
                        Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER
                        | Win32.SWP_FRAMECHANGED | Win32.SWP_SHOWWINDOW);
                }
                Win32.ShowWindow(mhwndAbsorbed, Win32.SW_SHOWNORMAL);
            } catch { }
            mblnRestored = true;
        }

        // Cleanup fires on OnFormClosed (post-close), not OnFormClosing (pre-close),
        // so the dock can cancel a close without stranding absorbed terminals.
        //
        // For owned-spawn panes we additionally PostMessage WM_CLOSE so the WT
        // window closes itself, which terminates the cmd.exe + claude tree we
        // launched. Without this the terminal would just float back to the
        // desktop on close (the historical absorb-only behaviour) -- that is
        // wrong for terminals we created, since the user expects "X" on a
        // pane to clean up everything that pane represents.
        protected override void OnFormClosed(FormClosedEventArgs e) {
            base.OnFormClosed(e);
            if (mtmrWatchdog != null) {
                mtmrWatchdog.Stop();
                mtmrWatchdog.Dispose();
                mtmrWatchdog = null;
            }
            UnparentAndRestore();
            if (mblnOwnedSpawn
                && !SuppressTerminateOnClose
                && mhwndAbsorbed != IntPtr.Zero
                && Win32.IsWindow(mhwndAbsorbed)) {
                Win32.PostMessage(mhwndAbsorbed, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private static string ReadWindowTitle(IntPtr hwnd) {
            StringBuilder sb = new StringBuilder(256);
            Win32.GetWindowText(hwnd, sb, sb.Capacity);
            string s = sb.ToString();
            return string.IsNullOrEmpty(s) ? "Absorbed Terminal" : s;
        }
    }
}
