using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using AgentDockShared;

namespace AgentDockStandalone {

    public partial class fAgentDockMain : Form {

        private const string CNST_WT_CLASS    = "CASCADIA_HOSTING_WINDOW_CLASS";
        private const int    CNST_TIMEOUT_SEC = 15;

        private readonly int    mintInitialSpawn;
        private readonly bool   mblnInitialAbsorb;
        private readonly string mstrBatPath;
        private readonly string mstrCwd;
        private int             mintNextPaneNumber = 1;

        // Set by "Close Dock & Terminate All" so OnFormClosing kills absorbed
        // terminals instead of releasing them.
        private bool mblnTerminateOnClose;

        // Low-level mouse hook -- keeps a strong ref on the delegate so the GC
        // can't collect it while Windows still holds the function pointer.
        private Win32.LowLevelMouseProc mMouseHookProc;
        private IntPtr mMouseHookId = IntPtr.Zero;

        // Pane order, custom tiling, maximize, reorder dialog and the Ctrl+Shift chords
        // (Story 5616).
        private readonly cPaneArranger mPanes;

        // "Become admin": a pane asks, the dock reopens its conversation as an admin pane.
        private cAdminSwap mAdminSwap;

        // The window's icon (Story 6817): AgentDock.ico, the Agent Dock robot,
        // embedded in the exe as a resource and read from there. Reading it back out of the exe
        // file (Icon.ExtractAssociatedIcon) showed the standard icon on the work machine -- it
        // fails on some paths, such as a network share -- so that is only the fallback.
        private const string CNST_ICON_RESOURCE = "AgentDockStandalone.AgentDock.ico";

        private void LoadDockIcon() {
            try {
                using (Stream s = typeof(fAgentDockMain).Assembly.GetManifestResourceStream(CNST_ICON_RESOURCE)) {
                    if (s != null) { this.Icon = new Icon(s); return; }
                }
            } catch { }
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        }

        public fAgentDockMain(int aintInitialSpawn, bool ablnInitialAbsorb,
                              string astrBatPath, string astrCwd) {
            InitializeComponent();
            LoadDockIcon();
            AddCloseActivePaneMenuItem();
            AddSettingsMenu();
            mintInitialSpawn   = aintInitialSpawn;
            mblnInitialAbsorb  = ablnInitialAbsorb;
            mstrBatPath        = astrBatPath;
            mstrCwd            = astrCwd;
            mPanes = new cPaneArranger(this, this.mnuWindow,
                                       delegate(string s) { this.tslStatus.Text = s; });
            AddComposeBox();
        }

        // ---------------------------------------------------------------- compose box

        // Story 6826: the compose box across the bottom, above the status bar. Built always, shown
        // only when Settings turns it on (config\agentdock.ini ShowComposeBox, default false).
        private cComposeBox mCompose;

        private void AddComposeBox() {
            mCompose = new cComposeBox(ComposeTarget,
                delegate { return mPanes == null ? new List<Form>() : mPanes.OrderedPanes(); },
                delegate(Form f) { if (mPanes != null) mPanes.SetCurrentPane(f); },
                delegate(string s) { this.tslStatus.Text = s; });
            mCompose.Height = cDockSettings.ComposeBoxHeight;
            this.Controls.Add(mCompose);
            // Run conversation grows the box (Story 6845): the panes make room.
            mCompose.SizeChanged += delegate { if (mPanes != null && mCompose.Visible && !mCompose.IsFullScreen) mPanes.ReTile(); };
            mCompose.FullScreenChanged += new Action<bool>(SetComposeFullScreen);

            // The bar between the panes and the compose box: drag it up for a taller box and
            // smaller panes, down for the other way. The height is remembered (ComposeBoxHeight).
            msplCompose = new Splitter();
            msplCompose.Dock      = DockStyle.Bottom;
            msplCompose.Height    = 6;
            msplCompose.MinSize   = cDockSettings.CNST_COMPOSE_MIN_HEIGHT;
            msplCompose.MinExtra  = 120;            // always leave the panes this much
            msplCompose.BackColor = SystemColors.ControlDark;
            msplCompose.Cursor    = Cursors.HSplit;
            msplCompose.SplitterMoved += delegate {
                if (mPanes != null) mPanes.ReTile();
                try { cDockSettings.SaveComposeBoxHeight(mCompose.Height); } catch { /* not remembered, no harm */ }
            };
            this.Controls.Add(msplCompose);
            mCompose.VisibleChanged += delegate { msplCompose.Visible = mCompose.Visible && !mCompose.IsFullScreen; };

            // Docking runs from the back of the z-order to the front: the menu and status bar stay
            // outermost, then the compose box, its bar, and the pane area fills what is left.
            MdiClient mdi = null;
            foreach (Control c in this.Controls) { if (c is MdiClient) mdi = (MdiClient)c; }
            if (mdi != null) {
                this.Controls.SetChildIndex(mdi, 0);
                this.Controls.SetChildIndex(msplCompose, 1);
                this.Controls.SetChildIndex(mCompose, 2);
            }
            mCompose.SetShown(cDockSettings.ShowComposeBox);
            msplCompose.Visible = mCompose.Visible;
        }

        private Splitter msplCompose;

        // Where a prompt goes: the pane last clicked, or the only pane when there is one.
        private fAgentAbsorbedTerminal ComposeTarget() {
            Form pane = mPanes == null ? null : mPanes.CurrentPane();
            if (pane == null && this.MdiChildren != null && this.MdiChildren.Length == 1)
                pane = this.MdiChildren[0];
            return pane as fAgentAbsorbedTerminal;
        }

        protected override void OnLoad(EventArgs e) {
            base.OnLoad(e);
            InstallMouseHook();
            InstallFocusHook();
            StartClickPoll();
            mPanes.InstallKeyboardHook();
            // Ctrl+Shift+U works while a terminal has the keyboard (the arranger's hook).
            mPanes.AddChord(Keys.U, delegate { UnparentActivePane(); });
            // The rest of the menu shortcuts go through the same hook (Story 6817), so they work
            // while a terminal has the keyboard. The menu items show them as text only.
            mPanes.AddCtrlChord(Keys.N, delegate { mnuAgentsNewPane.PerformClick(); });
            mPanes.AddCtrlChord(Keys.U, delegate { mnuAgentsNewUnparentedPane.PerformClick(); });
            mPanes.AddChord(Keys.N,     delegate { mnuAgentsNewAdminPane.PerformClick(); });
            mPanes.AddChord(Keys.A,     delegate { mnuAgentsAbsorbAll.PerformClick(); });
            mPanes.AddBareKey(Keys.F1,  delegate { mnuHelpUsage.PerformClick(); });
            ApplyAdminSetting();
            UpdateStatus();

            // Drive any startup action from CLI flags. UI is otherwise idle.
            if (mintInitialSpawn > 0) {
                SpawnPanes(mintInitialSpawn);
            } else if (mblnInitialAbsorb) {
                AbsorbAllRunning();
            }
            // Restarted as administrator to open an admin pane (Story 6817): open it now.
            if (Program.SpawnAdminOnStart && IsRunningElevated()) {
                SpawnPane(true);
                ReTileIfMultiple();
                UpdateStatus();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e) {
            mtmrClickPoll.Stop();
            UninstallMouseHook();
            if (mAdminSwap != null) mAdminSwap.Dispose();
            mPanes.Dispose();
            base.OnFormClosed(e);
        }

        // -------------------------------------------------------------------
        // Mouse hook -- brings the dock forward when the user clicks on any
        // descendant of the dock (including absorbed Windows Terminal HWNDs
        // in another process, which don't propagate WM_MOUSEACTIVATE /
        // WM_PARENTNOTIFY back to us through the SetParent boundary).

        private void InstallMouseHook() {
            mMouseHookProc = new Win32.LowLevelMouseProc(MouseHookCallback);
            using (System.Diagnostics.Process p = System.Diagnostics.Process.GetCurrentProcess())
            using (System.Diagnostics.ProcessModule m = p.MainModule) {
                mMouseHookId = Win32.SetWindowsHookEx(
                    Win32.WH_MOUSE_LL, mMouseHookProc, Win32.GetModuleHandle(m.ModuleName), 0);
            }
        }

        private void UninstallMouseHook() {
            if (mMouseHookId != IntPtr.Zero) {
                Win32.UnhookWindowsHookEx(mMouseHookId);
                mMouseHookId = IntPtr.Zero;
            }
            if (mFocusHookId != IntPtr.Zero) {
                UnhookWinEvent(mFocusHookId);
                mFocusHookId = IntPtr.Zero;
            }
        }

        // A second way to know which pane you are in (Story 6826): Windows reports every change of
        // keyboard focus, and a focus landing inside a pane makes it the current pane. Unlike the
        // low-level mouse hook, Windows never drops this one, and it also follows keyboard moves.
        private WinEventProc mFocusHookProc;
        private IntPtr       mFocusHookId = IntPtr.Zero;

        // A third way, and the one that cannot fail (Story 6826): look at the mouse buttons 25 times
        // a second, and when one goes down over a pane, that pane is the current one. Testing found
        // clicks inside a terminal still did not move the compose box's target with title bars
        // hidden (a title bar click did, through the focus hook), so the hook alone is not trusted.
        private readonly Timer mtmrClickPoll = new Timer();
        private bool           mblnButtonWasDown;

        private void StartClickPoll() {
            mtmrClickPoll.Interval = 40;
            mtmrClickPoll.Tick    += delegate { PollClick(); };
            mtmrClickPoll.Start();
        }

        private void PollClick() {
            bool blnDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0
                        || (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
            if (blnDown && !mblnButtonWasDown && mPanes != null) {
                Point pt = Cursor.Position;
                Win32.POINT wp = new Win32.POINT();
                wp.x = pt.X; wp.y = pt.Y;
                IntPtr h = Win32.WindowFromPoint(wp);
                if (h != IntPtr.Zero && Win32.GetAncestor(h, Win32.GA_ROOT) == this.Handle) mPanes.NoteClick(h);
            }
            mblnButtonWasDown = blnDown;
        }

        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private void InstallFocusHook() {
            mFocusHookProc = new WinEventProc(FocusHookCallback);
            mFocusHookId = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, IntPtr.Zero,
                mFocusHookProc, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        private void FocusHookCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
                                       int idObject, int idChild, uint idEventThread, uint dwmsEventTime) {
            try {
                if (hwnd != IntPtr.Zero && mPanes != null) mPanes.NoteClick(hwnd);
            } catch { /* a hook must never throw */ }
        }

        private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
                                           int idObject, int idChild, uint idEventThread, uint dwmsEventTime);
        private const uint EVENT_OBJECT_FOCUS    = 0x8005;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            try {
                if (nCode < 0) return Win32.CallNextHookEx(mMouseHookId, nCode, wParam, lParam);
                int msg = wParam.ToInt32();
                if (msg == Win32.WM_LBUTTONDOWN || msg == Win32.WM_RBUTTONDOWN || msg == Win32.WM_MBUTTONDOWN) {
                    Win32.MSLLHOOKSTRUCT data = (Win32.MSLLHOOKSTRUCT)
                        Marshal.PtrToStructure(lParam, typeof(Win32.MSLLHOOKSTRUCT));
                    IntPtr clicked = Win32.WindowFromPoint(data.pt);
                    if (clicked != IntPtr.Zero) {
                        mPanes.NoteClick(clicked);
                        IntPtr root = Win32.GetAncestor(clicked, Win32.GA_ROOT);
                        if (root == this.Handle && Win32.GetForegroundWindow() != this.Handle) {
                            // Never inside the hook (Story 6826): BringSelfForward attaches to the
                            // terminal's input thread, which can take long enough that Windows
                            // silently removes a slow low-level hook. After that no click was seen,
                            // and "the pane last clicked" stuck on one pane. Post it instead.
                            this.BeginInvoke(new MethodInvoker(BringSelfForward));
                        }
                    }
                }
            } catch { /* hook must never throw */ }
            return Win32.CallNextHookEx(mMouseHookId, nCode, wParam, lParam);
        }

        // SetForegroundWindow alone gets refused when our process isn't already
        // the foreground process. AttachThreadInput merges input queues so the
        // call succeeds.
        private void BringSelfForward() {
            if (this.IsDisposed) return;
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;

            IntPtr fgHwnd = Win32.GetForegroundWindow();
            uint myThread = Win32.GetCurrentThreadId();
            uint fgThread = (fgHwnd == IntPtr.Zero) ? 0
                          : Win32.GetWindowThreadProcessId(fgHwnd, IntPtr.Zero);

            bool attached = false;
            if (fgThread != 0 && fgThread != myThread)
                attached = Win32.AttachThreadInput(myThread, fgThread, true);

            try {
                Win32.BringWindowToTop(this.Handle);
                Win32.SetForegroundWindow(this.Handle);
                Win32.ShowWindow(this.Handle, Win32.SW_SHOW);
            } finally {
                if (attached) Win32.AttachThreadInput(myThread, fgThread, false);
            }
        }

        // -------------------------------------------------------------------
        // Spawn pipeline
        //
        // "Spawn N" means "bring the total pane count up to N." If there are
        // already M panes in the dock, only (N - M) more are needed. Of those,
        // any unabsorbed Windows Terminal windows on the desktop are grabbed
        // first; only the remainder are spawned fresh as `cmd /k claude`.
        // This avoids both duplicating already-running sessions and stacking
        // the dock past what the user asked for.

        private void SpawnPanes(int aintTarget) {
            int intExisting = (this.MdiChildren == null) ? 0 : this.MdiChildren.Length;
            int intNeed     = Math.Max(0, aintTarget - intExisting);
            if (intNeed == 0) { UpdateStatus(); return; }

            int intAbsorbed = AbsorbAvailable(intNeed);
            intNeed -= intAbsorbed;

            for (int i = 0; i < intNeed; i++) SpawnPane();

            ReTileIfMultiple();
            UpdateStatus();
        }

        // Spawn one new agent by shelling out to the user's batch via
        // `wt.exe --window new cmd /k <bat>`, then polling for that
        // newcomer and absorbing it as an MDI child. Going through wt.exe
        // is what makes the new window inherit the user's Windows Terminal
        // settings -- the prior `cmd /k claude` embed path (still in
        // fAgentTerminal) bypassed WT entirely and produced raw conhost
        // visuals, which is the "jacked" pane the user kept seeing after
        // clicking Spawn 2/3/4.
        //
        // ablnAdmin = true launches the terminal with Verb = "runas" (UAC
        // prompts unless AgentDock is already elevated) and tags the WT
        // tab + MDI child caption with "[ADMIN]". An elevated WT can only
        // be embedded into the dock when AgentDock itself is elevated --
        // Windows UIPI blocks SetParent from a medium-integrity process
        // onto a high-integrity window. When AgentDock is medium and the
        // user requests admin, the elevated terminal launches as a free-
        // floating window (still marked [ADMIN] via wt's --title flag),
        // and the dock surfaces a status note explaining why.
        private void SpawnPane() {
            SpawnPane(false);
        }

        private void SpawnPane(bool ablnAdmin) {
            SpawnPane(ablnAdmin, null);
        }

        // cAdminSwap callback: reopen a conversation in an admin pane (become-admin skill).
        private void SpawnAdminResume(string astrSession, string astrCwd) {
            SpawnPane(true, cAdminSwap.ResumeCommand(astrSession, astrCwd));
            ReTileIfMultiple();
        }

        // astrCommand = what cmd /k runs; null runs the user's batch.
        private void SpawnPane(bool ablnAdmin, string astrCommand) {
            SpawnPane(ablnAdmin, astrCommand, false);
        }

        // ablnUnparented = true (Story 6805): the terminal is launched exactly like a pane --
        // same batch, so the same folder and startup flags, same tab color, same integrity
        // level -- but it is left on the desktop and never absorbed.
        private void SpawnPane(bool ablnAdmin, string astrCommand, bool ablnUnparented) {
            if (astrCommand == null && !File.Exists(mstrBatPath)) {
                MessageBox.Show(this,
                    "The pane batch file was not found at:\r\n\r\n" + mstrBatPath
                    + "\r\n\r\nPut the command a new pane should run in config\\pane.bat "
                    + "(see samples\\Copilot.bat), or start the dock with --bat <file>.",
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool blnSelfElevated = IsRunningElevated();
            bool blnEmbedAdmin   = ablnAdmin && blnSelfElevated;

            if (ablnAdmin && !blnSelfElevated && !ablnUnparented) {
                DialogResult dr = MessageBox.Show(this,
                    "Admin panes can only be embedded into the dock when "
                    + "AgentDock itself runs as administrator -- Windows UIPI "
                    + "blocks a medium-integrity dock from absorbing a high-"
                    + "integrity terminal.\r\n\r\n"
                    + "Launch the admin terminal anyway? It will appear as a "
                    + "separate window on the desktop with [ADMIN] in its tab "
                    + "title. To embed admin panes inside the dock, restart "
                    + "AgentDock as administrator.",
                    "Agent Dock", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                if (dr != DialogResult.Yes) return;
            }

            string strTabHex;
            string strColorArg = cTabColor.TabColorArgument(out strTabHex);
            string strArgs = "--window new "
                           + (ablnAdmin ? "--title \"[ADMIN] Agent\" " : "")
                           + strColorArg
                           + "cmd /k "
                           + (astrCommand ?? ("\"" + mstrBatPath + "\""));

            List<IntPtr> lstBefore = FindTerminalHwnds();
            bool blnRightLevel;
            try {
                // From an elevated dock a normal pane is started through Explorer so it is NOT
                // admin; an admin pane prompts UAC unless the dock is already admin (cPaneLauncher).
                blnRightLevel = cPaneLauncher.Start("wt.exe", strArgs, ablnAdmin);
            } catch (System.ComponentModel.Win32Exception wex) {
                MessageBox.Show(this,
                    "Could not launch "
                    + (ablnAdmin ? "admin " : "")
                    + "Windows Terminal:\r\n\r\n" + wex.Message,
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            } catch (Exception ex) {
                MessageBox.Show(this,
                    "Could not launch Windows Terminal:\r\n\r\n" + ex.Message,
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            mintNextPaneNumber++;   // bump for status-label feedback only

            if (ablnUnparented) {
                // Nothing to poll for and nothing to absorb: it stays a desktop window. Absorb
                // All (or a Spawn N top-up) can still pull it in later, like any terminal.
                this.tslStatus.Text = blnRightLevel
                    ? "Unparented terminal launched on the desktop."
                    : "Unparented terminal launched on the desktop (it came up as administrator).";
                return;
            }

            if (ablnAdmin && !blnEmbedAdmin) {
                // UIPI keeps us out -- don't poll, don't try to absorb.
                this.tslStatus.Text = "Admin terminal launched as a separate desktop window (UIPI blocks embedding).";
                return;
            }

            // 300 ms poll, 15 s ceiling. WinForms.Timer ticks serialize on
            // the UI thread, so even when several SpawnPane calls (Spawn 4)
            // each spin up their own timer, each tick claims one distinct
            // newcomer -- once SetParent'd into the dock the HWND drops out
            // of FindTerminalHwnds() for the next timer's pass.
            DateTime dtStart = DateTime.Now;
            Timer    tmr     = new Timer();
            tmr.Interval = 300;
            tmr.Tick += delegate {
                if (this.IsDisposed) { tmr.Stop(); tmr.Dispose(); return; }
                IntPtr hNew = IntPtr.Zero;
                foreach (IntPtr h in FindTerminalHwnds()) {
                    if (!lstBefore.Contains(h)) { hNew = h; break; }
                }
                if (hNew != IntPtr.Zero) {
                    tmr.Stop();
                    tmr.Dispose();
                    // ownedSpawn: true -- we launched this terminal, so closing
                    // the pane should kill its cmd.exe + claude, not release
                    // the WT window back to the desktop.
                    // A normal pane that fell back to an elevated launch is admin -- say so.
                    cPaneColors.Remember(hNew, strTabHex);   // the Send to list shows it (Story 6828)
                    fAgentAbsorbedTerminal frm = new fAgentAbsorbedTerminal(hNew, true, ablnAdmin || !blnRightLevel);
                    frm.MdiParent   = this;
                    frm.FormClosed += new FormClosedEventHandler(ChildPane_FormClosed);
                    frm.Show();
                    ReTileIfMultiple();
                    UpdateStatus();
                    return;
                }
                if ((DateTime.Now - dtStart).TotalSeconds > 15) {
                    tmr.Stop();
                    tmr.Dispose();
                    UpdateStatus();
                }
            };
            tmr.Start();
        }

        private static bool IsRunningElevated() {
            using (System.Security.Principal.WindowsIdentity id =
                       System.Security.Principal.WindowsIdentity.GetCurrent()) {
                System.Security.Principal.WindowsPrincipal p =
                    new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }

        // -------------------------------------------------------------------
        // Absorb pipeline -- pull every visible CASCADIA_HOSTING_WINDOW_CLASS

        private void AbsorbAllRunning() {
            List<string> lstAdmin;
            int intAbsorbed = AbsorbAvailable(Int32.MaxValue, out lstAdmin);
            if (intAbsorbed > 0) {
                ReTileIfMultiple();
                UpdateStatus();
            }
            // Windows does not let a dock without admin rights take in a terminal running as
            // administrator: the window stays where it is and the pane would sit empty. So those
            // are skipped, and the dock offers to restart as administrator to take them in.
            if (lstAdmin.Count > 0) {
                DialogResult dr = MessageBox.Show(this,
                    (lstAdmin.Count == 1 ? "This terminal runs" : "These terminals run") + " as administrator:\r\n\r\n    "
                    + String.Join("\r\n    ", lstAdmin.ToArray()) + "\r\n\r\n"
                    + "The dock is not running as administrator, so Windows will not let it take "
                    + (lstAdmin.Count == 1 ? "that window" : "those windows") + " in.\r\n\r\n"
                    + "Restart the dock as administrator and take every terminal in? Your open panes keep running "
                    + "and come back in. (Windows asks first.)",
                    "Agent Dock", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
                if (dr == DialogResult.Yes) RestartDock(true, "--absorb");
                return;
            }
            if (intAbsorbed == 0) {
                MessageBox.Show(this,
                    "No Windows Terminal windows found to absorb.\r\n\r\n"
                    + "Open a Windows Terminal window (wt.exe) first, then try again.",
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private int AbsorbAvailable(int amaxCount) {
            List<string> lstAdmin;
            return AbsorbAvailable(amaxCount, out lstAdmin);
        }

        // Absorb up to amaxCount unabsorbed top-level WT windows. Skips any
        // HWND already wrapped by an existing absorbed MDI child, and (when the dock is not
        // running as administrator) any terminal running as administrator, whose title goes in
        // alstAdmin. Returns the number actually absorbed. Caller is responsible for re-tile / status.
        private int AbsorbAvailable(int amaxCount, out List<string> alstAdmin) {
            alstAdmin = new List<string>();
            if (amaxCount <= 0) return 0;
            HashSet<IntPtr> already = SnapshotAbsorbedHwnds();
            bool blnDockAdmin = IsRunningElevated();
            int intCount = 0;
            foreach (IntPtr h in FindTerminalHwnds()) {
                if (already.Contains(h)) continue;
                if (!blnDockAdmin && IsWindowElevated(h)) {
                    StringBuilder sbTitle = new StringBuilder(256);
                    Win32.GetWindowText(h, sbTitle, sbTitle.Capacity);
                    alstAdmin.Add(sbTitle.Length > 0 ? sbTitle.ToString() : "Windows Terminal");
                    continue;
                }
                // Absorbed windows get a tab color too. One the dock colored before (coming back
                // after an update) keeps its color, so the Send to list still matches it.
                if (!cPaneColors.Known(h)) {
                    string strTabHex;
                    if (cTabColor.ColorExistingWindow(h, out strTabHex)) cPaneColors.Remember(h, strTabHex);
                }
                fAgentAbsorbedTerminal frm = new fAgentAbsorbedTerminal(h);
                frm.MdiParent   = this;
                frm.FormClosed += new FormClosedEventHandler(ChildPane_FormClosed);
                frm.Show();
                intCount++;
                if (intCount >= amaxCount) break;
            }
            return intCount;
        }

        private HashSet<IntPtr> SnapshotAbsorbedHwnds() {
            HashSet<IntPtr> set = new HashSet<IntPtr>();
            if (this.MdiChildren != null) {
                foreach (Form f in this.MdiChildren) {
                    fAgentAbsorbedTerminal abs = f as fAgentAbsorbedTerminal;
                    if (abs != null) set.Add(abs.AbsorbedHwnd);
                }
            }
            return set;
        }

        // True when the window's process runs as administrator. A process this dock cannot even
        // look at counts as administrator too, since the dock could not take its window in either.
        private static bool IsWindowElevated(IntPtr ahwnd) {
            uint intPid;
            GetWindowThreadProcessId(ahwnd, out intPid);
            if (intPid == 0) return false;
            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, intPid);
            if (hProc == IntPtr.Zero) return true;
            try {
                IntPtr hToken;
                if (!OpenProcessToken(hProc, TOKEN_QUERY, out hToken)) return true;
                try {
                    int intElevated, intLen;
                    if (!GetTokenInformation(hToken, TOKEN_ELEVATION_CLASS, out intElevated, 4, out intLen)) return true;
                    return intElevated != 0;
                } finally { CloseHandle(hToken); }
            } finally { CloseHandle(hProc); }
        }

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint TOKEN_QUERY                       = 0x0008;
        private const int  TOKEN_ELEVATION_CLASS             = 20;   // TokenElevation

        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass,
            out int TokenInformation, int TokenInformationLength, out int ReturnLength);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private static List<IntPtr> FindTerminalHwnds() {
            List<IntPtr> lst = new List<IntPtr>();
            Win32.EnumWindowsProc cb = delegate(IntPtr h, IntPtr l) {
                if (!Win32.IsWindowVisible(h)) return true;
                StringBuilder sb = new StringBuilder(256);
                Win32.GetClassName(h, sb, sb.Capacity);
                if (sb.ToString() == CNST_WT_CLASS) lst.Add(h);
                return true;
            };
            Win32.EnumWindows(cb, IntPtr.Zero);
            return lst;
        }

        // -------------------------------------------------------------------
        // Resize behavior -- retile when the dock changes shape

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            // mPanes is null during InitializeComponent, which can raise OnResize.
            if (mPanes != null && this.WindowState != FormWindowState.Minimized) mPanes.ReTile();
        }

        // Re-applies the remembered layout in the remembered pane order (cPaneArranger).
        private void ReTileIfMultiple() {
            mPanes.ReTile();
        }

        private void ChildPane_FormClosed(object sender, FormClosedEventArgs e) {
            UpdateStatus();
            // After the close finishes, let the remaining panes fill the gap.
            if (!this.IsDisposed) this.BeginInvoke(new MethodInvoker(delegate { mPanes.ReTile(); UpdateStatus(); }));
        }

        private void UpdateStatus() {
            int intCount = (this.MdiChildren == null) ? 0 : this.MdiChildren.Length;
            if (intCount == 0) {
                this.tslStatus.Text = "0 panes - use Agents menu to spawn or absorb";
            } else {
                this.tslStatus.Text = intCount + " pane" + (intCount == 1 ? "" : "s") + " active";
            }
        }

        // -------------------------------------------------------------------
        // Close -- confirm, then release absorbed terminals to the desktop

        protected override void OnFormClosing(FormClosingEventArgs e) {
            base.OnFormClosing(e);
            if (e.Cancel) return;

            List<fAgentAbsorbedTerminal> lstAbsorbed = new List<fAgentAbsorbedTerminal>();
            int intSpawned = 0;
            if (this.MdiChildren != null) {
                foreach (Form f in this.MdiChildren) {
                    fAgentAbsorbedTerminal abs = f as fAgentAbsorbedTerminal;
                    if (abs != null) lstAbsorbed.Add(abs);
                    else intSpawned++;
                }
            }

            if (lstAbsorbed.Count == 0 && intSpawned == 0) return;

            if (mblnTerminateOnClose) {
                foreach (fAgentAbsorbedTerminal abs in lstAbsorbed) {
                    abs.UnparentAndRestore();
                    IntPtr h = abs.AbsorbedHwnd;
                    if (h != IntPtr.Zero && Win32.IsWindow(h))
                        Win32.PostMessage(h, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    abs.Close();
                }
                return;
            }

            int intTotal = lstAbsorbed.Count + intSpawned;
            string strMsg = "Close Agent Dock?\n\n"
                          + intTotal + " pane" + (intTotal == 1 ? "" : "s")
                          + " will be released back to the desktop (sessions stay alive).";

            if (MessageBox.Show(this, strMsg, "Agent Dock",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) {
                e.Cancel = true;
                return;
            }

            // Release every pane to the desktop, alive -- owned-spawn panes
            // get SuppressTerminateOnClose so the cmd.exe + claude tree
            // survives the dock closing. Snap to quadrants for usable
            // positions.
            List<IntPtr> lstReleased = new List<IntPtr>();
            foreach (fAgentAbsorbedTerminal abs in lstAbsorbed) {
                lstReleased.Add(abs.AbsorbedHwnd);
                abs.SuppressTerminateOnClose = true;
                abs.UnparentAndRestore();
                abs.Close();
            }
            if (lstReleased.Count > 0) SnapToQuadrants(lstReleased);
        }

        // -------------------------------------------------------------------
        // Menu handlers

        private void mnuFileExit_Click(object sender, EventArgs e) {
            this.Close();
        }

        private void mnuAgentsSpawn1_Click(object sender, EventArgs e) {
            SpawnPanes(1);
        }

        // New Pane = "always add one fresh pane running config\pane.bat."
        // Bypasses the top-up logic so it works even when the dock is already
        // full, and never absorbs an existing desktop WT window.
        private void mnuAgentsNewPane_Click(object sender, EventArgs e) {
            SpawnPane(false);
            ReTileIfMultiple();
            UpdateStatus();
        }

        // Same as New Pane but launches the terminal elevated. See SpawnPane
        // for the UIPI caveat: a non-elevated dock cannot embed an elevated
        // terminal, so on a medium-integrity AgentDock the admin pane lives
        // as a free-floating desktop window.
        private void mnuAgentsNewAdminPane_Click(object sender, EventArgs e) {
            // Story 6817: the dock usually runs with normal rights, and Windows will not let it take
            // in an admin window. So it offers to restart itself as administrator first.
            if (!IsRunningElevated()) {
                DialogResult dr = MessageBox.Show(this,
                    "An admin pane can sit inside the dock only while the dock runs as administrator.\r\n\r\n"
                    + "Yes: restart the dock as administrator (Windows will ask), then open the admin pane in it."
                    + ((this.MdiChildren != null && this.MdiChildren.Length > 0)
                        ? " Your open panes keep running and come back in." : "") + "\r\n"
                    + "No: open the admin pane as a separate window.\r\n"
                    + "Cancel: no admin pane.",
                    "Agent Dock", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);
                if (dr == DialogResult.Cancel) return;
                if (dr == DialogResult.Yes) {
                    // Declined at Windows' prompt: stop there, rather than prompting again.
                    RestartDock(true, Program.CNST_ARG_SPAWN_ADMIN);
                    return;
                }
                SpawnPane(true, null, true);   // admin, left on the desktop (no second prompt)
                return;
            }
            SpawnPane(true);
            ReTileIfMultiple();
            UpdateStatus();
        }

        private void mnuAgentsSpawn2_Click(object sender, EventArgs e) {
            SpawnPanes(2);
        }

        private void mnuAgentsSpawn3_Click(object sender, EventArgs e) {
            SpawnPanes(3);
        }

        private void mnuAgentsSpawn4_Click(object sender, EventArgs e) {
            SpawnPanes(4);
        }

        private void mnuAgentsAbsorbAll_Click(object sender, EventArgs e) {
            AbsorbAllRunning();
        }

        // A fresh terminal started the way a pane is, left on the desktop (Story 6805).
        private void mnuAgentsNewUnparentedPane_Click(object sender, EventArgs e) {
            SpawnPane(false, null, true);
        }

        // Pops the pane the user last clicked out of the dock, alive (Story 6805). The same
        // action as Undock on the pane's own system menu; the rest stay and re-tile.
        private void mnuAgentsUnparentActive_Click(object sender, EventArgs e) {
            UnparentActivePane();
        }

        private void UnparentActivePane() {
            Form pane = mPanes.CurrentPane();
            if (pane == null && this.MdiChildren != null && this.MdiChildren.Length == 1)
                pane = this.MdiChildren[0];
            if (pane == null) {
                this.tslStatus.Text = (this.MdiChildren == null || this.MdiChildren.Length == 0)
                    ? "No pane to unparent."
                    : "Click inside a pane first, then unparent it.";
                return;
            }
            fAgentAbsorbedTerminal abs = pane as fAgentAbsorbedTerminal;
            if (abs == null) {
                this.tslStatus.Text = "That pane is not a Windows Terminal window, so it cannot leave the dock.";
                return;
            }
            abs.UndockToDesktop();
        }

        // Agents -> Close Active Pane (Story 6817). With pane title bars hidden there is no X on
        // a pane, so this closes the pane you last clicked, after asking. A pane the dock started
        // ends its agent session; an absorbed one goes back to the desktop, as the X did.
        private ToolStripMenuItem mnuAgentsCloseActive;

        private void AddCloseActivePaneMenuItem() {
            mnuAgentsCloseActive = new ToolStripMenuItem("&Close Active Pane");
            mnuAgentsCloseActive.ToolTipText = "Close the pane you last clicked. A pane the dock started ends its session.";
            mnuAgentsCloseActive.Click += delegate { CloseActivePane(); };
            int intAt = mnuAgents.DropDownItems.IndexOf(mnuAgentsUnparentActive);
            mnuAgents.DropDownItems.Insert(intAt + 1, mnuAgentsCloseActive);
        }

        // Settings menu, beside Window (Story 6817): the settings dialog, and the config folder
        // in Explorer.
        private void AddSettingsMenu() {
            ToolStripMenuItem mnuSettings = new ToolStripMenuItem("&Settings");
            ToolStripMenuItem mnuEdit = new ToolStripMenuItem("Agent Dock &Settings...");
            mnuEdit.ToolTipText = "The agent command, the start folder, the pane title bars and the compose box.";
            mnuEdit.Click += delegate { ShowSettings(); };
            ToolStripMenuItem mnuFolder = new ToolStripMenuItem("&Open Config Folder");
            mnuFolder.ToolTipText = "Open the folder holding pane.bat and agentdock.ini in Explorer.";
            mnuFolder.Click += delegate { OpenConfigFolder(); };
            ToolStripMenuItem mnuUpdate = new ToolStripMenuItem("&Update Agent Dock");
            mnuUpdate.ToolTipText = "Get the newest version with git, build it, and restart the dock. Open panes keep running.";
            mnuUpdate.Click += delegate { UpdateAgentDock(); };
            // Show Compose Box: the same switch as the one in the settings dialog, one click away.
            mnuShowCompose = new ToolStripMenuItem("Show &Compose Box");
            mnuShowCompose.ToolTipText = "Show or hide the compose box across the bottom of the dock. Remembered.";
            mnuShowCompose.Checked = cDockSettings.ShowComposeBox;
            mnuShowCompose.Click += delegate { ToggleComposeBox(); };
            mnuSettings.DropDownItems.Add(mnuShowCompose);
            mnuSettings.DropDownItems.Add(BuildThemeMenu());
            mnuSettings.DropDownItems.Add(new ToolStripSeparator());
            mnuSettings.DropDownItems.Add(mnuEdit);
            mnuSettings.DropDownItems.Add(mnuFolder);
            mnuSettings.DropDownItems.Add(new ToolStripSeparator());
            mnuSettings.DropDownItems.Add(mnuUpdate);
            msMenu.Items.Insert(msMenu.Items.IndexOf(mnuWindow) + 1, mnuSettings);
        }

        private ToolStripMenuItem mnuShowCompose;

        // Full Screen in the compose box (Story 6845): the pane area and its bar hide and the
        // box fills the dock. The terminals keep running, hidden, and Send still reaches them.
        // Released, the box goes back to its height and the panes are tiled again.
        private int mintComposeHeightBeforeFull = -1;

        private void SetComposeFullScreen(bool ablnFull) {
            MdiClient mdi = null;
            foreach (Control c in this.Controls) { if (c is MdiClient) mdi = (MdiClient)c; }
            this.SuspendLayout();
            if (ablnFull) {
                mintComposeHeightBeforeFull = mCompose.Height;
                if (mdi != null) mdi.Visible = false;
                msplCompose.Visible = false;
                mCompose.Dock = DockStyle.Fill;
            } else {
                mCompose.Dock = DockStyle.Bottom;
                if (mintComposeHeightBeforeFull > 0) mCompose.Height = mintComposeHeightBeforeFull;
                msplCompose.Visible = mCompose.Visible;
                if (mdi != null) mdi.Visible = true;
            }
            this.ResumeLayout(true);
            if (!ablnFull && mPanes != null) mPanes.ReTile();
        }

        // Settings -> Compose Box Theme (Story 6845): every theme, the current one ticked.
        // Picking one paints the box at once and is remembered (agentdock.ini ComposeTheme).
        private ToolStripMenuItem BuildThemeMenu() {
            ToolStripMenuItem mnuTheme = new ToolStripMenuItem("Compose Box &Theme");
            mnuTheme.ToolTipText = "The colors of the compose box and its conversation bubbles. Remembered.";
            foreach (cComposeTheme t in cComposeTheme.All) {
                cComposeTheme theme = t;
                ToolStripMenuItem mi = new ToolStripMenuItem(theme.Name);
                mi.Checked = theme.Name == cDockSettings.ComposeTheme;
                mi.Click += delegate {
                    string strProblem = null;
                    try { cDockSettings.SaveComposeTheme(theme.Name); } catch (Exception ex) { strProblem = ex.Message; }
                    foreach (ToolStripItem o in mnuTheme.DropDownItems) {
                        ToolStripMenuItem m = o as ToolStripMenuItem;
                        if (m != null) m.Checked = m == mi;
                    }
                    mCompose.ApplyTheme(theme);
                    this.tslStatus.Text = "Compose box theme: " + theme.Name + "."
                        + (strProblem == null ? "" : " Not remembered: " + strProblem)
                        + (mCompose.Visible ? "" : " Turn on Show Compose Box to see it.");
                };
                mnuTheme.DropDownItems.Add(mi);
            }
            return mnuTheme;
        }

        // Settings -> Show Compose Box: flips the setting, saves it and shows or hides the box at once.
        private void ToggleComposeBox() {
            bool blnShow = !cDockSettings.ShowComposeBox;
            string strProblem = null;
            try {
                cDockSettings.Save(cDockSettings.HidePaneTitleBars, cDockSettings.StartAsAdministrator, blnShow);
            } catch (Exception ex) {
                strProblem = ex.Message;
            }
            ApplyComposeBox(blnShow);
            this.tslStatus.Text = (blnShow ? "Compose box shown." : "Compose box hidden.")
                + (strProblem == null ? "" : " Not remembered: " + strProblem);
        }

        private void ApplyComposeBox(bool ablnShow) {
            mnuShowCompose.Checked = ablnShow;
            if (mCompose.Visible == ablnShow) return;
            mCompose.SetShown(ablnShow);
            if (mPanes != null) mPanes.ReTile();
            if (ablnShow) mCompose.FocusPrompt();
        }

        private void ShowSettings() {
            bool blnWasHidden = cDockSettings.HidePaneTitleBars;
            bool blnWasAdmin  = cDockSettings.StartAsAdministrator;
            bool blnWasCompose = cDockSettings.ShowComposeBox;
            using (fSettings dlg = new fSettings(mstrBatPath)) {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
            }
            bool blnHide = cDockSettings.HidePaneTitleBars;
            if (blnHide != blnWasHidden) {
                foreach (Form f in this.MdiChildren) {
                    fAgentAbsorbedTerminal abs = f as fAgentAbsorbedTerminal;
                    if (abs != null) abs.ApplyTitleBar(blnHide);
                }
                if (mPanes != null) mPanes.ReTile();
            }
            bool blnCompose = cDockSettings.ShowComposeBox;
            if (blnCompose != blnWasCompose) ApplyComposeBox(blnCompose);
            this.tslStatus.Text = "Settings saved.";
            bool blnAdmin = cDockSettings.StartAsAdministrator;
            if (blnAdmin != blnWasAdmin) {
                ApplyAdminSetting();
                OfferRestartForAdminSetting(blnAdmin);
            }
        }

        // Settings -> Update Agent Dock (Story 6817). The exe cannot be replaced while it runs, so
        // the dock hands off to update.cmd and closes: the script waits for this process to end,
        // runs git pull, build.cmd, then starts the new dock. Open panes are released to the
        // desktop alive first, and the new dock is started with --absorb to take them back.
        private const string CNST_UPDATE_SCRIPT = "update.cmd";
        private const string CNST_RELAUNCH_ENV  = "AGENTDOCK_RELAUNCH_ARGS";

        // Update on request (Story 6826): tools\Update-RunningDock.ps1 posts this registered
        // message to the dock's window, so an agent that has just pushed a dock change can bring
        // the running dock up to date. It is the same update as the menu's, with no Yes / No.
        // Allowed through to a dock running as administrator, since the script usually is not.
        private const string CNST_UPDATE_MESSAGE = "AgentDock.UpdateNow";
        private static readonly int WM_AGENTDOCK_UPDATE = RegisterWindowMessage(CNST_UPDATE_MESSAGE);
        private const uint MSGFLT_ALLOW = 1;
        private const long CNST_UPDATE_RELEASE_ONLY = 1;

        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            if (WM_AGENTDOCK_UPDATE != 0)
                ChangeWindowMessageFilterEx(this.Handle, (uint)WM_AGENTDOCK_UPDATE, MSGFLT_ALLOW, IntPtr.Zero);
        }

        protected override void WndProc(ref Message m) {
            if (WM_AGENTDOCK_UPDATE != 0 && m.Msg == WM_AGENTDOCK_UPDATE) {
                // wParam 1: release every pane to the desktop alive and close, nothing more. Sent
                // to every other dock when several run, so the one that updates takes all their
                // terminals back in. wParam 0: update (release, close, pull, build, absorb).
                // Not while a dialog of the dock's is open: the script sees the dock still
                // running and says so.
                bool blnReleaseOnly = m.WParam.ToInt64() == CNST_UPDATE_RELEASE_ONLY;
                if (this.Enabled) this.BeginInvoke(new MethodInvoker(delegate {
                    if (blnReleaseOnly) ReleaseAllPanesAndClose(); else UpdateAgentDock(false);
                }));
                return;
            }
            base.WndProc(ref m);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, uint msg, uint action, IntPtr pChangeFilterStruct);

        private void UpdateAgentDock() { UpdateAgentDock(true); }

        // This dock's version, as Help -> About shows it (1.0.N).
        private static string CurrentVersion() {
            Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v == null ? "unknown" : v.ToString(3);
        }

        private void UpdateAgentDock(bool ablnAsk) {
            string strRoot   = Program.RepoRoot;
            string strScript = strRoot == null ? null : Path.Combine(strRoot, CNST_UPDATE_SCRIPT);
            if (strScript == null || !File.Exists(strScript)) {
                MessageBox.Show(this, "update.cmd was not found beside this Agent Dock, so it cannot update itself.",
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int intPanes = (this.MdiChildren == null) ? 0 : this.MdiChildren.Length;
            DialogResult dr = !ablnAsk ? DialogResult.Yes : MessageBox.Show(this,
                "Update Agent Dock?\r\n\r\n"
                + "You are on version " + CurrentVersion() + ".\r\n\r\n"
                + "The dock closes, gets the newest version with git, builds it, and starts again. "
                + "A window shows the progress.\r\n\r\n"
                + (intPanes > 0
                    ? "Your " + intPanes + " open pane" + (intPanes == 1 ? "" : "s")
                      + " keep running: they go to the desktop and the new dock takes them back."
                    : "No panes are open."),
                "Agent Dock", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);
            if (dr != DialogResult.Yes) return;

            try {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
                    "/c \"\"" + strScript + "\" " + Process.GetCurrentProcess().Id + "\"");
                psi.UseShellExecute  = false;
                psi.WorkingDirectory = strRoot;
                // Asked from outside, other docks may have released their panes for this one, so
                // the new dock always takes every terminal back in.
                psi.EnvironmentVariables[CNST_RELAUNCH_ENV] = RelaunchArgs(intPanes > 0 || !ablnAsk);
                Process.Start(psi);
            } catch (Exception ex) {
                MessageBox.Show(this, "Could not start the update:\r\n\r\n" + ex.Message,
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ReleaseAllPanesAndClose();
        }

        // Every pane goes to the desktop alive (as Unparent All does), then the dock closes
        // without the terminate prompt -- there is nothing left to terminate.
        private void ReleaseAllPanesAndClose() {
            List<fAgentAbsorbedTerminal> lst = new List<fAgentAbsorbedTerminal>();
            foreach (Form f in this.MdiChildren) {
                fAgentAbsorbedTerminal abs = f as fAgentAbsorbedTerminal;
                if (abs != null) lst.Add(abs);
            }
            foreach (fAgentAbsorbedTerminal abs in lst) {
                abs.SuppressTerminateOnClose = true;
                abs.UnparentAndRestore();
                abs.Close();
            }
            mblnTerminateOnClose = true;
            this.Close();
        }

        // Become admin requests are always watched (Story 6817): admin panes are always available,
        // they are just never the default. Called once at load.
        private void ApplyAdminSetting() {
            if (mAdminSwap == null) {
                mAdminSwap = new cAdminSwap(this, mstrCwd, SpawnAdminResume, delegate(string s) { this.tslStatus.Text = s; });
            }
        }

        // After "Start the dock as administrator" changes: offer to restart at the new level now.
        private void OfferRestartForAdminSetting(bool ablnAdmin) {
            if (ablnAdmin == IsRunningElevated()) return;   // already running the way the setting asks
            int intPanes = (this.MdiChildren == null) ? 0 : this.MdiChildren.Length;
            DialogResult dr = MessageBox.Show(this,
                (ablnAdmin
                    ? "The dock will now start as administrator. Restart it as administrator now (Windows will ask)?"
                    : "The dock will now start with normal rights. Restart it with normal rights now?")
                + (intPanes > 0 ? "\r\n\r\nYour open panes keep running and come back into the restarted dock." : "")
                + "\r\n\r\nNo: it takes effect the next time the dock starts.",
                "Agent Dock", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (dr != DialogResult.Yes) return;
            RestartDock(ablnAdmin, null);
        }

        // Restarts the dock at the given level. Open panes go to the desktop alive; the new dock
        // waits for this one to close, then takes them back (--absorb). astrExtra: more arguments
        // for the new dock, such as --spawn-admin. False when the restart did not happen (Windows'
        // prompt declined), and this dock stays as it was.
        private bool RestartDock(bool ablnAdmin, string astrExtra) {
            int intPanes = (this.MdiChildren == null) ? 0 : this.MdiChildren.Length;
            string strArgs = (RelaunchArgs(intPanes > 0) + " --wait-pid " + Process.GetCurrentProcess().Id
                              + (String.IsNullOrEmpty(astrExtra) ? "" : " " + astrExtra)).Trim();
            try {
                // From an elevated dock a normal start goes through Explorer, so it really is normal.
                cPaneLauncher.Start(Application.ExecutablePath, strArgs, ablnAdmin);
            } catch (Exception ex) {
                MessageBox.Show(this, "The dock was not restarted:\r\n\r\n" + ex.Message,
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            ReleaseAllPanesAndClose();
            return true;
        }

        // The restarted dock gets the same --bat / --cwd this one was started with, plus
        // --absorb when panes were open.
        private static string RelaunchArgs(bool ablnAbsorb) {
            StringBuilder sb = new StringBuilder();
            string[] astr = Environment.GetCommandLineArgs();
            for (int i = 1; i < astr.Length; i++) {
                string a = astr[i];
                bool blnPair = (a == "--bat" || a == "-b" || a == "--cwd" || a == "-d");
                if (blnPair && i + 1 < astr.Length) {
                    sb.Append(a).Append(" \"").Append(astr[++i]).Append("\" ");
                }
            }
            if (ablnAbsorb) sb.Append("--absorb");
            return sb.ToString().Trim();
        }

        private void OpenConfigFolder() {
            string strFolder = cDockSettings.ConfigFolder;
            if (String.IsNullOrEmpty(strFolder)) {
                this.tslStatus.Text = "No config folder was found.";
                return;
            }
            try {
                Directory.CreateDirectory(strFolder);
                Process.Start("explorer.exe", "\"" + strFolder + "\"");
            } catch (Exception ex) {
                MessageBox.Show(this, "Could not open the config folder:\r\n\r\n" + ex.Message,
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CloseActivePane() {
            Form pane = mPanes.CurrentPane();
            if (pane == null && this.MdiChildren != null && this.MdiChildren.Length == 1)
                pane = this.MdiChildren[0];
            if (pane == null) {
                this.tslStatus.Text = (this.MdiChildren == null || this.MdiChildren.Length == 0)
                    ? "No pane to close."
                    : "Click inside a pane first, then close it.";
                return;
            }
            fAgentAbsorbedTerminal abs = pane as fAgentAbsorbedTerminal;
            bool blnEnds = abs == null || abs.IsOwnedSpawn;
            DialogResult dr = MessageBox.Show(this,
                "Close pane: " + pane.Text + "?\r\n\r\n"
                + (blnEnds ? "Its agent session ends. This cannot be undone."
                           : "The terminal goes back to the desktop, still running."),
                "Agent Dock", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (dr == DialogResult.Yes) pane.Close();
        }

        private void mnuAgentsCloseAndTerminate_Click(object sender, EventArgs e) {
            int intAbsorbed = 0;
            int intSpawned  = 0;
            if (this.MdiChildren != null) {
                foreach (Form f in this.MdiChildren) {
                    if (f is fAgentAbsorbedTerminal) intAbsorbed++;
                    else intSpawned++;
                }
            }
            int intTotal = intAbsorbed + intSpawned;
            if (intTotal == 0) { this.Close(); return; }

            string strMsg = "Close Agent Dock AND end every agent session?\n\n";
            if (intSpawned > 0)
                strMsg += intSpawned + " spawned pane" + (intSpawned == 1 ? "" : "s") + " will be terminated.\n";
            if (intAbsorbed > 0)
                strMsg += intAbsorbed + " absorbed terminal" + (intAbsorbed == 1 ? "" : "s")
                       + " will be closed (their sessions end).";

            if (MessageBox.Show(this, strMsg, "Agent Dock",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            mblnTerminateOnClose = true;
            this.Close();
        }

        // Release every pane back to the desktop, alive -- including owned-spawn
        // panes (we suppress their terminate-on-close so the cmd.exe + claude
        // tree survives). Then close the dock.
        private void mnuAgentsUnparentAll_Click(object sender, EventArgs e) {
            List<IntPtr> lstHwnds = new List<IntPtr>();
            if (this.MdiChildren != null) {
                List<fAgentAbsorbedTerminal> lst = new List<fAgentAbsorbedTerminal>();
                foreach (Form f in this.MdiChildren) {
                    fAgentAbsorbedTerminal abs = f as fAgentAbsorbedTerminal;
                    if (abs != null) lst.Add(abs);
                }
                foreach (fAgentAbsorbedTerminal abs in lst) {
                    lstHwnds.Add(abs.AbsorbedHwnd);
                    abs.SuppressTerminateOnClose = true;
                    abs.UnparentAndRestore();
                    abs.Close();
                }
            }
            UpdateStatus();
            if (lstHwnds.Count == 0) {
                MessageBox.Show(this, "No panes to release.",
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SnapToQuadrants(lstHwnds);

            // Unparent is the "I'm done" exit path. mblnTerminateOnClose tells
            // OnFormClosing to skip the confirm -- the user already committed
            // by choosing this menu item.
            mblnTerminateOnClose = true;
            this.Close();
        }

        private void SnapToQuadrants(List<IntPtr> ahwnd) {
            System.Drawing.Rectangle wa = Screen.FromControl(this).WorkingArea;
            int hw = wa.Width  / 2;
            int hh = wa.Height / 2;
            System.Drawing.Rectangle[] rects = new System.Drawing.Rectangle[] {
                new System.Drawing.Rectangle(wa.Left,      wa.Top,      hw, hh),
                new System.Drawing.Rectangle(wa.Left + hw, wa.Top,      hw, hh),
                new System.Drawing.Rectangle(wa.Left,      wa.Top + hh, hw, hh),
                new System.Drawing.Rectangle(wa.Left + hw, wa.Top + hh, hw, hh),
            };
            for (int i = 0; i < Math.Min(ahwnd.Count, 4); i++) {
                IntPtr h = ahwnd[i];
                if (h == IntPtr.Zero || !Win32.IsWindow(h)) continue;
                Win32.ShowWindow(h, Win32.SW_SHOWNORMAL);
                System.Drawing.Rectangle r = rects[i];
                Win32.SetWindowPos(h, IntPtr.Zero, r.X, r.Y, r.Width, r.Height,
                    Win32.SWP_SHOWWINDOW | Win32.SWP_NOZORDER);
                Win32.SetForegroundWindow(h);
            }
        }

        private void mnuWindowTileVertical_Click(object sender, EventArgs e) {
            mPanes.Tile(cPaneArranger.CNST_TILE_VERTICAL);
        }

        private void mnuWindowTileHorizontal_Click(object sender, EventArgs e) {
            mPanes.Tile(cPaneArranger.CNST_TILE_HORIZONTAL);
        }

        private void mnuWindowCascade_Click(object sender, EventArgs e) {
            mPanes.Tile(cPaneArranger.CNST_TILE_CASCADE);
        }

        private void mnuWindowQuadrantLayout_Click(object sender, EventArgs e) {
            mPanes.Tile(cPaneArranger.CNST_TILE_QUAD);
        }

        // -------------------------------------------------------------------
        // Help

        // Help -> Usage (Story 6817): docs\AgentDock.md in the Agent Dock folder, so the help
        // travels with the dock to any machine. It used to read a file on one machine only.
        private const string CNST_HELP_RELATIVE = @"docs\AgentDock.md";

        private static string HelpFilePath() {
            string strRoot = Program.RepoRoot;
            if (strRoot != null) return Path.Combine(strRoot, CNST_HELP_RELATIVE);
            return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), CNST_HELP_RELATIVE);
        }

        private void mnuHelpUsage_Click(object sender, EventArgs e) {
            string strFile = HelpFilePath();
            string strContent;
            try {
                if (!File.Exists(strFile)) {
                    MessageBox.Show(this,
                        "The help file was not found:\r\n\r\n" + strFile
                        + "\r\n\r\nIt comes with Agent Dock, in its docs folder. Get the newest "
                        + "version (Settings -> Update Agent Dock) or download it again.",
                        "Agent Dock - Help", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                strContent = File.ReadAllText(strFile);
            } catch (Exception ex) {
                MessageBox.Show(this,
                    "Could not read the help file:\r\n\r\n" + strFile + "\r\n\r\n" + ex.Message,
                    "Agent Dock - Help", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (fHelpViewer frm = new fHelpViewer("Help: Agent Dock", strContent)) {
                frm.ShowDialog(this);
            }
        }

        private void mnuHelpAbout_Click(object sender, EventArgs e) {
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
            string strVersion = asm.GetName().Version.ToString(3);   // Major.Minor.Build -- Build rises with every commit (AssemblyInfo.cs)
            string strBuild   = "";
            try {
                strBuild = System.IO.File.GetLastWriteTime(asm.Location).ToString("yyyy-MM-dd HH:mm");
            } catch { }

            string strMsg =
                "Agent Dock\r\n"
              + "Version " + strVersion + "\r\n"
              + (strBuild.Length > 0 ? ("Built " + strBuild + "\r\n") : "")
              + "\r\n"
              + "Holds several AI agent terminals in one window.\r\n\r\n"
              + "Running now: " + (IsRunningElevated() ? "as administrator" : "with normal rights") + ".\r\n"
              + "Starts as administrator: " + (cDockSettings.StartAsAdministrator ? "yes" : "no") + " (Settings -> Agent Dock Settings).\r\n"
              + "Normal panes always start with normal rights. Admin panes say [ADMIN].\r\n\r\n"
              + ".NET Framework 4.8.\r\n\r\n"
              + "Press F1 or Help -> Usage for the menu reference.";

            MessageBox.Show(this, strMsg, "About Agent Dock",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
