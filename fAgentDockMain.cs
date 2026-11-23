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
        // (Story 5616). Shared with the Leonardo-menu AgentDock.
        private readonly cPaneArranger mPanes;

        // "Become admin": a pane asks, the dock reopens its conversation as an admin pane.
        private cAdminSwap mAdminSwap;

        // The window's icon (Story 6817): AgentDock.ico, the Leonardo menu's Agent Dock robot,
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
        }

        protected override void OnLoad(EventArgs e) {
            base.OnLoad(e);
            InstallMouseHook();
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
            mAdminSwap = new cAdminSwap(this, mstrCwd, SpawnAdminResume, delegate(string s) { this.tslStatus.Text = s; });
            UpdateStatus();

            // Drive any startup action from CLI flags. UI is otherwise idle.
            if (mintInitialSpawn > 0) {
                SpawnPanes(mintInitialSpawn);
            } else if (mblnInitialAbsorb) {
                AbsorbAllRunning();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e) {
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
        }

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
                            BringSelfForward();
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

            string strArgs = "--window new "
                           + (ablnAdmin ? "--title \"[ADMIN] Claude Code\" " : "")
                           + cTabColor.TabColorArgument()
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
            int intAbsorbed = AbsorbAvailable(Int32.MaxValue);
            if (intAbsorbed == 0) {
                MessageBox.Show(this,
                    "No Windows Terminal windows found to absorb.\r\n\r\n"
                    + "Open a Windows Terminal window (wt.exe) first, then try again.",
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ReTileIfMultiple();
            UpdateStatus();
        }

        // Absorb up to amaxCount unabsorbed top-level WT windows. Skips any
        // HWND already wrapped by an existing absorbed MDI child. Returns the
        // number actually absorbed. Caller is responsible for re-tile / status.
        private int AbsorbAvailable(int amaxCount) {
            if (amaxCount <= 0) return 0;
            HashSet<IntPtr> already = SnapshotAbsorbedHwnds();
            int intCount = 0;
            foreach (IntPtr h in FindTerminalHwnds()) {
                if (already.Contains(h)) continue;
                cTabColor.ColorExistingWindow(h);   // absorbed windows get a tab color too
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

        // New Pane = "always add one fresh cmd /k claude in the source folder."
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
            mnuEdit.ToolTipText = "The agent command, the start folder and the pane title bars.";
            mnuEdit.Click += delegate { ShowSettings(); };
            ToolStripMenuItem mnuFolder = new ToolStripMenuItem("&Open Config Folder");
            mnuFolder.ToolTipText = "Open the folder holding pane.bat and agentdock.ini in Explorer.";
            mnuFolder.Click += delegate { OpenConfigFolder(); };
            mnuSettings.DropDownItems.Add(mnuEdit);
            mnuSettings.DropDownItems.Add(mnuFolder);
            msMenu.Items.Insert(msMenu.Items.IndexOf(mnuWindow) + 1, mnuSettings);
        }

        private void ShowSettings() {
            bool blnWasHidden = cDockSettings.HidePaneTitleBars;
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
            this.tslStatus.Text = "Settings saved.";
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

            string strMsg = "Close Agent Dock AND terminate every Claude session?\n\n";
            if (intSpawned > 0)
                strMsg += intSpawned + " spawned pane" + (intSpawned == 1 ? "" : "s") + " will be terminated.\n";
            if (intAbsorbed > 0)
                strMsg += intAbsorbed + " absorbed terminal" + (intAbsorbed == 1 ? "" : "s")
                       + " will be closed (claude sessions end).";

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

        // Help -> Usage. Loads the markdown from Leonardo's Help folder at
        // runtime and shows it in a viewer. The standalone owns no embedded
        // copy of the help -- Tony wants the help text to live with the rest
        // of Leonardo's help topics so updates happen in one place.
        private const string CNST_HELP_FILE =
            @"C:\Tony\Dev\Dot_Net\Source\LeonardoConsolidated\Help\AgentDockStandalone.md";

        private void mnuHelpUsage_Click(object sender, EventArgs e) {
            string strContent;
            try {
                if (!System.IO.File.Exists(CNST_HELP_FILE)) {
                    MessageBox.Show(this,
                        "Help file not found:\r\n\r\n" + CNST_HELP_FILE
                        + "\r\n\r\nThe help topic lives in the Leonardo Help folder. "
                        + "Verify the file is present at the expected path.",
                        "AgentDockStandalone - Help",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                strContent = System.IO.File.ReadAllText(CNST_HELP_FILE);
            } catch (Exception ex) {
                MessageBox.Show(this,
                    "Could not read help file:\r\n\r\n" + CNST_HELP_FILE
                    + "\r\n\r\n" + ex.Message,
                    "AgentDockStandalone - Help",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (fHelpViewer frm = new fHelpViewer("Help: AgentDockStandalone", strContent)) {
                frm.ShowDialog(this);
            }
        }

        private void mnuHelpAbout_Click(object sender, EventArgs e) {
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
            string strVersion = asm.GetName().Version.ToString();
            string strBuild   = "";
            try {
                strBuild = System.IO.File.GetLastWriteTime(asm.Location).ToString("yyyy-MM-dd HH:mm");
            } catch { }

            string strMsg =
                "AgentDockStandalone\r\n"
              + "Version " + strVersion + "\r\n"
              + (strBuild.Length > 0 ? ("Built " + strBuild + "\r\n") : "")
              + "\r\n"
              + "MDI host for parallel Claude Code sessions.\r\n"
              + "Standalone - no Leonardo dependency.\r\n\r\n"
              + ".NET Framework 4.8, runs as administrator, PerMonitorV2.\r\n"
              + "Normal panes start with normal rights; admin panes say [ADMIN].\r\n\r\n"
              + "Press F1 or Help -> Usage for the menu reference.";

            MessageBox.Show(this, strMsg, "About AgentDockStandalone",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
