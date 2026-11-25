using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgentDockStandalone {

    // The compose box (Story 6826, from Spike 6823): a plain Windows text box across the bottom of
    // the dock for writing prompts the way you write in any Windows app -- click anywhere in the
    // text, select with the mouse, Ctrl+A / X / C / V / Z. Enter types the text into the agent of
    // the pane you last clicked and presses Enter there. Shift+Enter starts a new line. Up on the
    // first line brings back earlier prompts, Down goes forward again.
    //
    // Off by default and hidden entirely until switched on (Settings -> Agent Dock Settings,
    // "Show the compose box"). The terminals are untouched: you can still type in them directly.
    internal sealed class cComposeBox : Panel {

        private const int CNST_HEIGHT        = 92;
        private const int CNST_HISTORY_MAX   = 50;
        // The pause between typing the text and pressing Enter (see cConsoleInput).
        private const int CNST_ENTER_DELAY_MS = 150;

        private readonly TextBox txtPrompt = new TextBox();
        private readonly Button  btnSend   = new Button();
        private readonly Button  btnAnswer = new Button();
        private readonly Button  btnCopy   = new Button();
        // Full Screen (Story 6845): pressed, the panes hide and the compose box fills the dock.
        private readonly CheckBox chkFull  = new CheckBox();
        private readonly Button  btnClear  = new Button();
        private readonly ToolTip mtipButtons = new ToolTip();
        private TableLayoutPanel mpnlFull;
        private readonly ComboBox cboUpMode = new ComboBox();
        private readonly Label    lblTarget = new Label();
        private readonly ComboBox cboTarget = new ComboBox();
        private readonly Timer   tmrTarget = new Timer();

        // Run conversation (Story 6845): the conversation as bubbles above the text box, kept
        // current by tmrLive. The text box then sits under it, with a bar between to resize.
        private readonly Panel             pnlLeft   = new Panel();
        private readonly cConversationView vwLive    = new cConversationView();
        private readonly Splitter          splPrompt = new Splitter();
        private readonly Splitter          splSend   = new Splitter();
        private readonly Panel             pnlRight  = new Panel();
        private readonly Panel             pnlLabelRow = new Panel();
        private readonly Timer             tmrLive   = new Timer();
        private const int CNST_LIVE_PROMPT_HEIGHT = 70;
        private const int CNST_LIVE_MIN_HEIGHT    = 320;   // the box grows to this for Run conversation
        private cComposeTheme mTheme = cComposeTheme.Named(cComposeTheme.CNST_DEFAULT);

        private readonly Func<fAgentAbsorbedTerminal> mfncTarget;
        private readonly Func<List<Form>>             mfncPanes;
        private readonly Action<Form>                 mactSetTarget;
        private readonly Action<string>               mactStatus;

        private readonly List<string> mlstHistory = new List<string>();
        private int    mintHistoryAt = -1;     // -1: not browsing history
        private string mstrDraft     = "";
        private bool   mblnSending;

        // afncTarget: the pane a prompt goes to (the one last clicked), or null when there is none.
        // afncPanes: the panes in order, for the Send to list. aactSetTarget: makes a pane the
        // target, the same as clicking inside it.
        public cComposeBox(Func<fAgentAbsorbedTerminal> afncTarget, Func<List<Form>> afncPanes,
                           Action<Form> aactSetTarget, Action<string> aactStatus)
            : this(afncTarget, afncPanes, aactSetTarget, aactStatus, null) { }

        // A conversation pane (Story 6846, Settings -> Conversation Panes): a box that belongs to
        // one pane for good, standing in for its terminal. It always shows Run conversation, has
        // its pane's name and state in a header instead of the Send to list, and no Full Screen.
        public cComposeBox(Func<fAgentAbsorbedTerminal> afncTarget, Func<List<Form>> afncPanes,
                           Action<Form> aactSetTarget, Action<string> aactStatus, fAgentAbsorbedTerminal afrmPane) {
            mfrmPane      = afrmPane;
            mfncTarget    = afrmPane == null ? afncTarget : (Func<fAgentAbsorbedTerminal>)delegate { return afrmPane; };
            mfncPanes     = afncPanes;
            mactSetTarget = aactSetTarget;
            mactStatus    = aactStatus;
            BuildLayout();
            if (afrmPane != null) BuildCard();
            cPaneThemes.Changed += new Action(RefreshTheme);
            RefreshTheme();
        }

        // The pane a conversation pane belongs to, or null for the dock's own compose box.
        private readonly fAgentAbsorbedTerminal mfrmPane;
        public fAgentAbsorbedTerminal Pane { get { return mfrmPane; } }
        private bool IsCard { get { return mfrmPane != null; } }

        private void BuildLayout() {
            this.Dock    = DockStyle.Bottom;
            this.Height  = CNST_HEIGHT;
            this.Padding = new Padding(4);
            this.Visible = false;

            pnlRight.Dock    = DockStyle.Right;
            pnlRight.Width   = cDockSettings.ComposeSendWidth;
            pnlRight.Padding = new Padding(6, 0, 0, 0);

            btnSend.Text   = "Send";
            btnSend.Dock   = DockStyle.Fill;
            btnSend.Click += delegate { Send(); txtPrompt.Focus(); };

            // Popup Answer (Story 6828), beside Send: the target pane's latest answer in a window
            // you can select, edit and copy in.
            btnAnswer.Text   = "Popup Answer";
            btnAnswer.Dock   = DockStyle.Fill;
            btnAnswer.Click += delegate { PopupAnswer(); };

            // Copy (Story 6828): what is selected in the box, or all of it when nothing is.
            btnCopy.Text   = "Copy";
            btnCopy.Dock   = DockStyle.Fill;
            btnCopy.Click += delegate { CopySelection(); };

            // Full Screen (Story 6845): a toggle that stays pressed while the panes are hidden.
            chkFull.Text       = "Full Screen";
            chkFull.Appearance = Appearance.Button;
            chkFull.TextAlign  = ContentAlignment.MiddleCenter;
            chkFull.Dock       = DockStyle.Fill;
            chkFull.Margin     = new Padding(0);
            chkFull.CheckedChanged += delegate {
                if (FullScreenChanged != null) FullScreenChanged(chkFull.Checked);
                mactStatus(chkFull.Checked
                    ? "Full screen: the panes are hidden and keep running. Press Full Screen again to bring them back."
                    : "The panes are back.");
                txtPrompt.Focus();
            };

            // Clear Screen (Story 6846, Tony: "a clear button that just clears the terminal, that I
            // can run anytime"): wipes the pane's screen and scrollback, as cls does. The agent keeps the
            // conversation, so it is safe at any moment, even while the agent works.
            btnClear.Text   = "Clear Screen";
            btnClear.Dock   = DockStyle.Fill;
            btnClear.Margin = new Padding(0);
            btnClear.Click += delegate { ClearScreen(); };
            mtipButtons.SetToolTip(btnClear, "Wipe the screen and scrollback of the pane in Send to, as cls does. The agent keeps its conversation.");

            // Their own row under the buttons, so the row stays readable when the Send area is
            // narrow: Full Screen and Clear Screen side by side.
            TableLayoutPanel pnlFull = new TableLayoutPanel();
            pnlFull.Dock        = DockStyle.Top;
            pnlFull.Height      = 30;
            pnlFull.Padding     = new Padding(0, 3, 0, 0);
            pnlFull.ColumnCount = 2;
            pnlFull.RowCount    = 1;
            pnlFull.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlFull.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            pnlFull.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            chkFull.Margin = new Padding(0, 0, 3, 0);
            pnlFull.Controls.Add(chkFull, 0, 0);
            pnlFull.Controls.Add(btnClear, 1, 0);
            mpnlFull = pnlFull;

            // Send, Popup Answer and Copy share the top row, whatever the width.
            TableLayoutPanel tblButtons = new TableLayoutPanel();
            tblButtons.Dock        = DockStyle.Top;
            tblButtons.Height      = 30;
            tblButtons.ColumnCount = 3;
            tblButtons.RowCount    = 1;
            tblButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            tblButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            tblButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tblButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            foreach (Button b in new Button[] { btnSend, btnAnswer, btnCopy }) b.Margin = new Padding(0, 0, 3, 0);
            btnCopy.Margin = new Padding(0);
            tblButtons.Controls.Add(btnSend, 0, 0);
            tblButtons.Controls.Add(btnAnswer, 1, 0);
            tblButtons.Controls.Add(btnCopy, 2, 0);

            // Send to: the pane a prompt goes to. It follows the pane you click in, and picking
            // one here does the same as clicking in it.
            lblTarget.Text      = "Send to:";
            lblTarget.AutoSize  = true;
            lblTarget.Dock      = DockStyle.Left;
            lblTarget.ForeColor = SystemColors.GrayText;
            lblTarget.TextAlign = ContentAlignment.BottomLeft;
            lblTarget.Padding   = new Padding(0, 6, 0, 0);

            // What Up steps through (Story 6828), small, at the right of the Send to label.
            cboUpMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cboUpMode.Dock          = DockStyle.Right;
            cboUpMode.Width         = 130;
            cboUpMode.DropDownWidth = 170;
            cboUpMode.Items.AddRange(new object[] { "Up: my prompts", "Up: conversation", "Up: answers", "Run conversation" });
            cboUpMode.SelectedIndex = (int)cDockSettings.ComposeUpMode;
            cboUpMode.SelectedIndexChanged += delegate {
                eUpMode eMode = (eUpMode)Math.Max(0, cboUpMode.SelectedIndex);
                try { cDockSettings.SaveComposeUpMode(eMode); } catch { /* not remembered, no harm */ }
                // Stepping starts over in the new list. What you were writing comes back.
                if (mintHistoryAt >= 0) { mintHistoryAt = -1; SetPrompt(mstrDraft); }
                ApplyLive();
                mactStatus(eMode == eUpMode.Live
                    ? "Run conversation: the conversation shows above the text and stays current. Right-click a bubble to open or copy it. Up steps through your prompts."
                    : "Up now steps through " + UpModeWords(eMode) + ".");
            };
            cboUpMode.DropDownClosed += delegate { txtPrompt.Focus(); };

            pnlLabelRow.Dock    = DockStyle.Top;
            pnlLabelRow.Height  = 28;
            pnlLabelRow.Padding = new Padding(0, 2, 0, 4);
            pnlLabelRow.Controls.Add(lblTarget);
            pnlLabelRow.Controls.Add(cboUpMode);

            cboTarget.Dock          = DockStyle.Top;
            cboTarget.DropDownStyle = ComboBoxStyle.DropDownList;
            cboTarget.DropDownWidth = 360;
            // Any change the box did not make itself is the user's pick (mouse or keyboard).
            cboTarget.SelectedIndexChanged += delegate {
                if (mblnSyncing) return;
                TargetItem item = cboTarget.SelectedItem as TargetItem;
                if (item != null) { mactSetTarget(item.Pane); FollowTarget(); }
            };
            cboTarget.DropDownClosed += delegate { txtPrompt.Focus(); };
            // Each pane is shown in its own tab's color (Story 6828).
            cboTarget.DrawMode   = DrawMode.OwnerDrawFixed;
            cboTarget.ItemHeight = cboTarget.Font.Height + 4;
            cboTarget.DrawItem  += new DrawItemEventHandler(Target_DrawItem);

            pnlRight.Controls.Add(cboTarget);
            pnlRight.Controls.Add(pnlLabelRow);
            pnlRight.Controls.Add(pnlFull);
            pnlRight.Controls.Add(tblButtons);

            txtPrompt.Multiline     = true;
            txtPrompt.AcceptsReturn = true;
            txtPrompt.AcceptsTab    = false;
            txtPrompt.WordWrap      = true;
            txtPrompt.ScrollBars    = ScrollBars.Vertical;
            txtPrompt.Dock          = DockStyle.Fill;
            txtPrompt.Font          = new Font("Consolas", 10F);
            txtPrompt.KeyDown      += new KeyEventHandler(Prompt_KeyDown);

            // The bar between the text and the Send area: drag it left for a wider Send to list,
            // right for more text. The width is remembered (ComposeSendWidth, Story 6828).
            splSend.Dock      = DockStyle.Right;
            splSend.Width     = 6;
            splSend.MinSize   = cDockSettings.CNST_COMPOSE_SEND_MIN_WIDTH;
            splSend.MinExtra  = 120;               // always leave the text this much
            splSend.BackColor = SystemColors.ControlDark;
            splSend.Cursor    = Cursors.VSplit;
            splSend.SplitterMoved += SendWidthMoved;

            // The left side: the text alone, or (Run conversation) the conversation over the
            // text with a bar between them. Docked from the back: the text, its bar, the view.
            vwLive.Dock    = DockStyle.Fill;
            vwLive.Visible = false;
            vwLive.BubbleClicked += new Action<cHistoryEntry>(OpenBubble);
            vwLive.Status        += delegate(string astr) { mactStatus(astr); };
            splPrompt.Dock      = DockStyle.Bottom;
            splPrompt.Height    = 5;
            splPrompt.MinSize   = 34;
            splPrompt.MinExtra  = 60;
            splPrompt.BackColor = SystemColors.ControlDark;
            splPrompt.Cursor    = Cursors.HSplit;
            splPrompt.Visible   = false;
            pnlLeft.Dock = DockStyle.Fill;
            pnlLeft.Controls.Add(vwLive);
            pnlLeft.Controls.Add(splPrompt);
            pnlLeft.Controls.Add(txtPrompt);

            // Docked from the back of the z-order: the Send area, then its bar, then the text fills.
            this.Controls.Add(pnlLeft);
            this.Controls.Add(splSend);
            this.Controls.Add(pnlRight);

            tmrLive.Interval = CNST_LIVE_TICK_MS;
            tmrLive.Tick    += delegate { RefreshLive(false); };

            tmrTarget.Interval = 400;
            tmrTarget.Tick    += delegate {
                ShowTarget();
                FollowTarget();
                RefreshTheme();            // another pane may have its own theme
                if (++mintTicks % CNST_SAMPLE_EVERY_TICKS == 0) SampleTabColors();
                if (mintTicks % CNST_STATE_EVERY_TICKS == 0) CheckAgentState();
            };
            cboTarget.DropDown += delegate { SampleTabColors(); };
        }

        // The dock's own box remembers the Send area's width. A conversation pane does not.
        private void SendWidthMoved(object sender, SplitterEventArgs e) {
            try { cDockSettings.SaveComposeSendWidth(pnlRight.Width); } catch { /* not remembered, no harm */ }
        }

        // Shows or hides the box. Hidden, it does nothing at all.
        // Raised when Full Screen is pressed or released: the dock hides or shows the panes.
        public event Action<bool> FullScreenChanged;

        public bool IsFullScreen { get { return chkFull.Checked; } }

        // Releases Full Screen (the box being hidden, for one), which brings the panes back.
        public void LeaveFullScreen() {
            if (chkFull.Checked) chkFull.Checked = false;
        }

        // Whether the box is switched on. Visible alone cannot say: it is false while the dock
        // itself is not on screen yet, and Run conversation then never started (1.0.35).
        private bool mblnShown;

        public void SetShown(bool ablnShow) {
            if (!ablnShow) {
                LeaveFullScreen();
                mstrLastState = null;
                if (AgentStateChanged != null) AgentStateChanged("", -1);
            }
            mblnShown    = ablnShow;
            this.Visible = ablnShow;
            if (ablnShow) { ShowTarget(); tmrTarget.Start(); }
            else tmrTarget.Stop();
            ApplyLive();
        }

        // ------------------------------------------------------------ Run conversation

        private const int CNST_LIVE_TICK_MS      = 1500;
        private const int CNST_LIVE_UNSTAMPED_TICKS = 4;    // no change sign (Copilot): reread this often
        private const int CNST_PIDS_KEEP_MS      = 15000;   // how long a pane's processes are trusted

        private bool        mblnLive;
        private int         mintHeightBeforeLive = -1;    // the height to go back to, when the box grew
        private bool        mblnLiveLoading;
        private Form        mfrmLivePane;
        private string      mstrLiveStamp;
        private int         mintLiveTicks;
        private IntPtr      mhwndPids = IntPtr.Zero;
        private List<uint>  mlstPids;
        private DateTime    mdtPids = DateTime.MinValue;

        // Shows or hides the conversation to match the list and whether the box is shown.
        private void ApplyLive() {
            bool blnLive = mblnShown && UpMode() == eUpMode.Live;
            if (blnLive == mblnLive) return;
            mblnLive = blnLive;
            pnlLeft.SuspendLayout();
            if (blnLive) {
                txtPrompt.Dock   = DockStyle.Bottom;
                txtPrompt.Height = CNST_LIVE_PROMPT_HEIGHT;
                splPrompt.Visible = true;
                vwLive.Visible    = true;
            } else {
                vwLive.Visible    = false;
                splPrompt.Visible = false;
                txtPrompt.Dock    = DockStyle.Fill;
            }
            pnlLeft.ResumeLayout(true);
            if (blnLive) {
                // A conversation needs room: a short box grows, and goes back when this is turned
                // off. A conversation pane fills its cell and never grows.
                if (!IsCard && this.Height < CNST_LIVE_MIN_HEIGHT) {
                    mintHeightBeforeLive = this.Height;
                    this.Height = CNST_LIVE_MIN_HEIGHT;
                }
                mfrmLivePane    = null;
                mfrmLiveShowing = null;
                mstrLiveStamp   = null;
                vwLive.SetMessage("Reading the conversation...");
                RefreshLive(true);
                tmrLive.Start();
            } else {
                tmrLive.Stop();
                if (mintHeightBeforeLive > 0) { this.Height = mintHeightBeforeLive; mintHeightBeforeLive = -1; }
            }
        }

        // The pane's processes, found again every few seconds (finding them borrows its console,
        // so it is done here, on the window's thread, and not too often).
        private List<uint> PidsFor(IntPtr ahwnd) {
            if (ahwnd == mhwndPids && mlstPids != null && (DateTime.Now - mdtPids).TotalMilliseconds < CNST_PIDS_KEEP_MS)
                return mlstPids;
            List<uint> lst;
            try { lst = cConsoleInput.ConsoleProcesses(ahwnd); } catch (Exception) { lst = null; }
            mhwndPids = ahwnd; mlstPids = lst; mdtPids = DateTime.Now;
            return lst;
        }

        // Rereads the conversation when it changed (or the pane did), off the window's thread,
        // since a long transcript takes a moment to read.
        private void RefreshLive(bool ablnForce) {
            if (!mblnLive || mblnLiveLoading) return;
            fAgentAbsorbedTerminal pane = mfncTarget();
            if (pane == null) {
                mfrmLivePane = null; mstrLiveStamp = null;
                vwLive.SetMessage("Pick a pane in Send to (or click inside one) to see its conversation.");
                return;
            }
            bool blnNewPane = pane != mfrmLivePane;
            List<uint> lstPids = PidsFor(pane.AbsorbedHwnd);
            if (lstPids == null || lstPids.Count == 0) {
                vwLive.SetMessage("No agent was found in " + pane.Text + ".");
                mfrmLivePane = pane;
                return;
            }
            string strStamp = cAgentHistory.ConversationStamp(lstPids);
            mintLiveTicks++;
            if (!ablnForce && !blnNewPane) {
                if (strStamp != null && strStamp == mstrLiveStamp) return;
                if (strStamp == null && mintLiveTicks % CNST_LIVE_UNSTAMPED_TICKS != 0) return;
            }
            mblnLiveLoading = true;
            List<uint> lstFor = new List<uint>(lstPids);
            string strPaneName = pane.Text;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate {
                List<cHistoryEntry> lst = null;
                try { lst = cAgentHistory.ConversationForPids(lstFor, CNST_CONVERSATION_MAX); } catch (Exception) { lst = null; }
                try {
                    // No window yet (a conversation pane read before it was placed): never leave
                    // the load marked running, or the view would wait for it forever (1.0.40).
                    if (this.IsDisposed || !this.IsHandleCreated) { mblnLiveLoading = false; return; }
                    this.BeginInvoke(new MethodInvoker(delegate {
                        mblnLiveLoading = false;
                        if (!mblnLive) return;                           // turned off meanwhile
                        if (mfncTarget() != pane) { RefreshLive(true); return; }   // another pane picked: read that one
                        mfrmLivePane  = pane;
                        mstrLiveStamp = strStamp;
                        if (lst == null) vwLive.SetMessage("No conversation found for " + strPaneName
                            + ". The dock reads Claude Code and GitHub Copilot CLI.");
                        else vwLive.SetEntries(lst, blnNewPane);
                    }));
                } catch (Exception) {
                    mblnLiveLoading = false;
                }
            });
        }

        // Another pane picked (in Send to, or by clicking in it): Run conversation empties at
        // once and reads that pane's conversation, opening at its newest message (Tony,
        // 2026-11-25: "clear the compose window when we select a new terminal").
        private void FollowTarget() {
            if (!mblnLive) return;
            fAgentAbsorbedTerminal pane = mfncTarget();
            if (pane == null || pane == mfrmLivePane || pane == mfrmLiveShowing) return;
            mfrmLiveShowing = pane;
            vwLive.SetMessage("Reading the conversation of " + pane.Text + "...");
            RefreshLive(true);
        }

        private Form mfrmLiveShowing;   // the pane the view was last emptied for

        // Popup on a bubble's right-click menu: its whole text in the answer window.
        private void OpenBubble(cHistoryEntry aEntry) {
            fAgentAbsorbedTerminal pane = mfncTarget();
            string strPane = pane == null ? "Agent Dock" : pane.Text;
            using (fAnswerPopup dlg = new fAnswerPopup(aEntry.IsAnswer ? "Answer" : "Your prompt", strPane, aEntry.Text)) {
                dlg.ShowDialog(this.FindForm());
            }
            txtPrompt.Focus();
        }

        // ------------------------------------------------------------ themes

        // The look of the pane being shown (Story 6846): its own theme and bubble style when it
        // has them (cPaneThemes), else the dock's. Called when any theme changes and when the
        // box moves to another pane. Painting again only when something changed.
        private cBubbleStyle mStyle = cBubbleStyle.Named(cBubbleStyle.CNST_DEFAULT);
        private string mstrThemeKey;

        public void RefreshTheme() {
            if (this.IsDisposed) return;
            fAgentAbsorbedTerminal pane = mfncTarget();
            IntPtr hwnd = pane == null ? IntPtr.Zero : pane.AbsorbedHwnd;
            cComposeTheme theme = cPaneThemes.ThemeFor(hwnd);
            cBubbleStyle  style = cPaneThemes.StyleFor(hwnd);
            string strKey = theme.Name + "|" + style.Name;
            if (strKey == mstrThemeKey) return;
            mstrThemeKey = strKey;
            mStyle = style;
            ApplyTheme(theme);
        }

        // Paints the box in a theme (Story 6845, Settings -> Compose Box Theme). Windows puts
        // every control back in its system colors.
        private void ApplyTheme(cComposeTheme aTheme) {
            mTheme = aTheme;
            bool blnSys = aTheme.IsSystem;
            this.BackColor        = blnSys ? SystemColors.Control : aTheme.Back;
            pnlLeft.BackColor     = this.BackColor;
            pnlRight.BackColor    = this.BackColor;
            pnlLabelRow.BackColor = this.BackColor;
            txtPrompt.BackColor   = blnSys ? SystemColors.Window : aTheme.InputBack;
            txtPrompt.ForeColor   = blnSys ? SystemColors.WindowText : aTheme.InputFore;
            txtPrompt.BorderStyle = blnSys ? BorderStyle.Fixed3D : BorderStyle.FixedSingle;
            lblTarget.ForeColor   = blnSys ? SystemColors.GrayText : aTheme.Caption;
            foreach (Button b in new Button[] { btnSend, btnAnswer, btnCopy, btnClear }) {
                if (blnSys) {
                    b.FlatStyle = FlatStyle.Standard;
                    b.BackColor = SystemColors.Control;
                    b.ForeColor = SystemColors.ControlText;
                    b.UseVisualStyleBackColor = true;
                } else {
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = aTheme.Border;
                    b.FlatAppearance.MouseOverBackColor = Blend(aTheme.ButtonBack, aTheme.ButtonFore, 0.15);
                    b.FlatAppearance.MouseDownBackColor = Blend(aTheme.ButtonBack, aTheme.ButtonFore, 0.30);
                    b.BackColor = aTheme.ButtonBack;
                    b.ForeColor = aTheme.ButtonFore;
                }
            }
            if (blnSys) {
                chkFull.FlatStyle = FlatStyle.Standard;
                chkFull.BackColor = SystemColors.Control;
                chkFull.ForeColor = SystemColors.ControlText;
                chkFull.UseVisualStyleBackColor = true;
            } else {
                chkFull.FlatStyle = FlatStyle.Flat;
                chkFull.FlatAppearance.BorderColor = aTheme.Border;
                chkFull.FlatAppearance.CheckedBackColor   = Blend(aTheme.ButtonBack, aTheme.PromptEdge, 0.55);
                chkFull.FlatAppearance.MouseOverBackColor = Blend(aTheme.ButtonBack, aTheme.ButtonFore, 0.15);
                chkFull.BackColor = aTheme.ButtonBack;
                chkFull.ForeColor = aTheme.ButtonFore;
            }
            foreach (ComboBox c in new ComboBox[] { cboUpMode, cboTarget }) {
                c.FlatStyle = blnSys ? FlatStyle.Standard : FlatStyle.Flat;
                c.BackColor = blnSys ? SystemColors.Window : aTheme.InputBack;
                c.ForeColor = blnSys ? SystemColors.WindowText : aTheme.InputFore;
            }
            Color clrBar = blnSys ? SystemColors.ControlDark : aTheme.Border;
            splSend.BackColor   = clrBar;
            splPrompt.BackColor = clrBar;
            vwLive.ApplyTheme(aTheme, mStyle);
            if (IsCard) PaintHeader();
            cComposeTheme.ThemeScrollBars(txtPrompt, aTheme);
            this.Invalidate(true);
        }

        private static Color Blend(Color a, Color b, double adblB) {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * adblB), (int)(a.G + (b.G - a.G) * adblB), (int)(a.B + (b.B - a.B) * adblB));
        }

        public void FocusPrompt() {
            if (this.Visible) txtPrompt.Focus();
        }

        // One entry in the Send to list: the pane's number and title.
        private sealed class TargetItem {
            public readonly Form   Pane;
            public readonly string Caption;
            public TargetItem(Form afrm, int aintNumber) { Pane = afrm; Caption = aintNumber + ". " + afrm.Text; }
            public override string ToString() { return Caption; }
        }

        // Popup Answer: the target pane's most recent answer, read from its agent's own record
        // (cAgentHistory.LastAnswerForPane), in fAnswerPopup.
        private void PopupAnswer() {
            fAgentAbsorbedTerminal pane = mfncTarget();
            if (pane == null) {
                mactStatus("Pick a pane in Send to (or click inside one) first.");
                return;
            }
            string strAnswer;
            Cursor curOld = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try { strAnswer = cAgentHistory.LastAnswerForPane(pane.AbsorbedHwnd); }
            finally { Cursor.Current = curOld; }
            if (String.IsNullOrEmpty(strAnswer)) {
                MessageBox.Show(this.FindForm(), "No answer was found for " + pane.Text + ".\r\n\r\n"
                    + "The dock reads answers from Claude Code and GitHub Copilot CLI. The agent in this pane "
                    + "may not have answered yet, or keeps no record the dock can read.",
                    "Agent Dock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (fAnswerPopup dlg = new fAnswerPopup(pane.Text, strAnswer)) {
                dlg.ShowDialog(this.FindForm());
            }
        }

        // Every few ticks, and when the list opens, the panes' tab colors are read off the screen
        // (cPaneColors.SampleFromScreen), only while the dock is the window in front.
        private const int CNST_SAMPLE_EVERY_TICKS = 5;

        // The agent's state in the status bar (Story 6845, Tony: "showed 'thinking' or something
        // when I have asked a question and you have not yet responded, and idle or something
        // otherwise"). Every 2 ticks (0.8 s), for the pane in Send to. Raised with the words to
        // show and 1 working, 0 idle, -1 unknown (nothing shown).
        private const int CNST_STATE_EVERY_TICKS = 2;
        public event Action<string, int> AgentStateChanged;
        private string mstrLastState;

        private void CheckAgentState() {
            fAgentAbsorbedTerminal pane = mfncTarget();
            int intState = -1;
            if (pane != null) {
                List<uint> lstPids = PidsFor(pane.AbsorbedHwnd);
                if (lstPids != null && lstPids.Count > 0) intState = cAgentHistory.AgentState(lstPids);
            }
            string strText = intState < 0 || pane == null ? ""
                : pane.Text + ": " + (intState == 1 ? "Thinking..." : "Idle");
            string strKey = intState + "|" + strText;
            if (strKey == mstrLastState) return;
            mstrLastState = strKey;
            if (AgentStateChanged != null) AgentStateChanged(strText, intState);
        }
        private int mintTicks;

        private void SampleTabColors() {
            Form frmDock = this.FindForm();
            if (frmDock == null || !frmDock.IsHandleCreated || Win32.GetForegroundWindow() != frmDock.Handle) return;
            bool blnChanged = false;
            foreach (Form f in mfncPanes()) {
                fAgentAbsorbedTerminal abs = f as fAgentAbsorbedTerminal;
                if (abs != null && cPaneColors.SampleFromScreen(abs.AbsorbedHwnd)) blnChanged = true;
            }
            if (blnChanged) cboTarget.Invalidate();
        }

        // Draws a Send to entry on its pane's tab color, with black or white text, whichever
        // reads better. The chosen entry in the open list gets a frame instead of the usual blue,
        // so its color still shows. A pane whose color the dock does not know is drawn plainly.
        private void Target_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0 || e.Index >= cboTarget.Items.Count) { e.DrawBackground(); return; }
            TargetItem item = (TargetItem)cboTarget.Items[e.Index];
            fAgentAbsorbedTerminal abs = item.Pane as fAgentAbsorbedTerminal;
            Color clrBack = abs == null ? Color.Empty : cPaneColors.ColorOf(abs.AbsorbedHwnd);
            bool  blnSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color clrText;
            if (clrBack.IsEmpty) {
                e.DrawBackground();
                clrText = e.ForeColor;
            } else {
                using (SolidBrush br = new SolidBrush(clrBack)) e.Graphics.FillRectangle(br, e.Bounds);
                double dblLight = 0.299 * clrBack.R + 0.587 * clrBack.G + 0.114 * clrBack.B;
                clrText = dblLight > 150 ? Color.Black : Color.White;
                if (blnSelected && (e.State & DrawItemState.ComboBoxEdit) != DrawItemState.ComboBoxEdit) {
                    Rectangle r = e.Bounds;
                    r.Width -= 1; r.Height -= 1;
                    using (Pen pen = new Pen(clrText, 2)) e.Graphics.DrawRectangle(pen, r);
                }
            }
            TextRenderer.DrawText(e.Graphics, item.Caption, cboTarget.Font,
                Rectangle.Inflate(e.Bounds, -3, 0), clrText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        // Keeps the Send to list in step with the panes and the current pane. Left alone while it
        // is open, so it never changes under the mouse.
        private bool mblnSyncing;

        private void ShowTarget() {
            if (cboTarget.DroppedDown) return;
            mblnSyncing = true;
            try { SyncTargetList(); } finally { mblnSyncing = false; }
        }

        private void SyncTargetList() {
            List<Form> lstPanes = mfncPanes();
            bool blnSame = cboTarget.Items.Count == lstPanes.Count;
            for (int i = 0; blnSame && i < lstPanes.Count; i++) {
                TargetItem it = (TargetItem)cboTarget.Items[i];
                blnSame = it.Pane == lstPanes[i] && it.Caption == (i + 1) + ". " + lstPanes[i].Text;
            }
            if (!blnSame) {
                cboTarget.BeginUpdate();
                cboTarget.Items.Clear();
                for (int i = 0; i < lstPanes.Count; i++) cboTarget.Items.Add(new TargetItem(lstPanes[i], i + 1));
                cboTarget.EndUpdate();
            }
            Form current = mfncTarget();
            int intAt = -1;
            for (int i = 0; i < cboTarget.Items.Count; i++)
                if (((TargetItem)cboTarget.Items[i]).Pane == current) intAt = i;
            if (cboTarget.SelectedIndex != intAt) cboTarget.SelectedIndex = intAt;
        }

        private void Prompt_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter && !e.Shift && !e.Control && !e.Alt) {
                e.SuppressKeyPress = true;
                Send();
                return;
            }
            // A multi-line TextBox ignores Ctrl+A on its own.
            if (e.KeyCode == Keys.A && e.Control && !e.Shift && !e.Alt) {
                e.SuppressKeyPress = true;
                txtPrompt.SelectAll();
                return;
            }
            if (e.KeyCode == Keys.Up && !e.Shift && !e.Control && !e.Alt
                && txtPrompt.GetLineFromCharIndex(txtPrompt.SelectionStart) == 0) {
                if (StepHistory(-1)) e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Down && !e.Shift && !e.Control && !e.Alt && mintHistoryAt >= 0
                && txtPrompt.GetLineFromCharIndex(txtPrompt.SelectionStart)
                   == txtPrompt.GetLineFromCharIndex(txtPrompt.TextLength)) {
                if (StepHistory(+1)) e.SuppressKeyPress = true;
            }
        }

        // Moves through earlier prompts. Leaving the newest end puts the unsent draft back.
        // The list being stepped through. Taken fresh on the first Up: the target pane's own
        // prompts from its agent (cAgentHistory), so prompts typed straight into the terminal are
        // there too, in the order they were sent. A pane whose agent keeps no record the dock can
        // read uses the prompts sent from this box.
        // What it holds follows the Up list beside Send to (Story 6828): your prompts (the
        // default), the whole conversation, or only the agent's answers.
        private List<cHistoryEntry> mlstBrowse = new List<cHistoryEntry>();

        private eUpMode UpMode() {
            if (IsCard) return eUpMode.Live;       // a conversation pane always shows the conversation
            return (eUpMode)Math.Max(0, cboUpMode.SelectedIndex);
        }

        private static string UpModeWords(eUpMode aeMode) {
            if (aeMode == eUpMode.Conversation) return "the whole conversation";
            if (aeMode == eUpMode.Answers) return "the agent's answers";
            if (aeMode == eUpMode.Live) return "your prompts (the conversation shows above)";
            return "your prompts";
        }

        private List<cHistoryEntry> HistoryForTarget() {
            fAgentAbsorbedTerminal pane = mfncTarget();
            eUpMode eMode = UpMode();
            List<cHistoryEntry> lst = new List<cHistoryEntry>();
            // Run conversation shows the whole conversation already, so Up there brings back prompts.
            if (eMode == eUpMode.Prompts || eMode == eUpMode.Live) {
                List<string> lstAgent = pane == null ? null : cAgentHistory.ForPane(pane.AbsorbedHwnd, CNST_HISTORY_MAX);
                if (lstAgent == null || lstAgent.Count == 0) lstAgent = new List<string>(mlstHistory);
                foreach (string s in lstAgent) lst.Add(new cHistoryEntry(s, false));
                return lst;
            }
            List<cHistoryEntry> lstConv = pane == null ? null
                : cAgentHistory.ConversationForPane(pane.AbsorbedHwnd, CNST_CONVERSATION_MAX, eMode == eUpMode.Answers);
            if (lstConv == null || lstConv.Count == 0)
                mactStatus("No " + (eMode == eUpMode.Answers ? "answers" : "conversation")
                    + " found for this pane. The dock reads Claude Code and GitHub Copilot CLI.");
            return lstConv ?? lst;
        }

        private const int CNST_CONVERSATION_MAX = 200;

        // Copy: the selection, or the whole box when nothing is selected.
        private void CopySelection() {
            string strText = txtPrompt.SelectionLength > 0 ? txtPrompt.SelectedText : txtPrompt.Text;
            if (strText.Length == 0) { mactStatus("Nothing to copy."); txtPrompt.Focus(); return; }
            try {
                Clipboard.SetText(strText);
                mactStatus(txtPrompt.SelectionLength > 0 ? "Copied the selection." : "Copied all of it (nothing was selected).");
            } catch (Exception ex) {
                mactStatus("Not copied: " + ex.Message);
            }
            txtPrompt.Focus();
        }

        private Form mfrmBrowsePane;           // the pane mlstBrowse came from

        private bool StepHistory(int aintStep) {
            // Another pane picked while stepping: its own prompts, from the newest again.
            // What you were writing before you started stepping stays the draft.
            Form frmTarget = mfncTarget();
            bool blnSwitched = mintHistoryAt >= 0 && frmTarget != mfrmBrowsePane;
            if (blnSwitched) mintHistoryAt = -1;
            if (mintHistoryAt < 0) {
                mfrmBrowsePane = frmTarget;
                if (aintStep > 0) return false;
                mlstBrowse = HistoryForTarget();
            }
            if (mlstBrowse.Count == 0) return false;
            int intAt = mintHistoryAt < 0 ? mlstBrowse.Count : mintHistoryAt;
            intAt += aintStep;
            if (intAt < 0) return true;                       // already at the oldest
            if (intAt >= mlstBrowse.Count) {
                mintHistoryAt = -1;
                SetPrompt(mstrDraft);
                return true;
            }
            if (mintHistoryAt < 0 && !blnSwitched) mstrDraft = txtPrompt.Text;
            mintHistoryAt = intAt;
            cHistoryEntry entry = mlstBrowse[intAt];
            SetPrompt(entry.Text);
            // Up leaves the cursor at the top so the next Up steps again at once, Down at the end.
            if (aintStep < 0) {
                txtPrompt.SelectionStart = 0;
                txtPrompt.ScrollToCaret();
            }
            mactStatus((entry.IsAnswer ? "Agent's answer" : "Your prompt") + ", " + (intAt + 1) + " of " + mlstBrowse.Count + ".");
            return true;
        }

        // A Windows text box breaks lines only at CR LF. Prompts come back from the agent's record
        // with bare LF (or CR) line breaks, which the box would run together, so they become CR LF.
        private void SetPrompt(string astrText) {
            string strText = (astrText ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
            txtPrompt.Text           = strText;
            txtPrompt.SelectionStart = txtPrompt.TextLength;
            txtPrompt.ScrollToCaret();
        }

        // Types the prompt into the target pane, then presses Enter there a moment later. The text
        // stays in the box when it cannot be sent.
        private void Send() {
            if (mblnSending) return;
            string strText = txtPrompt.Text;
            if (strText.Trim().Length == 0) return;
            fAgentAbsorbedTerminal pane = mfncTarget();
            if (pane == null) {
                mactStatus("Pick a pane in Send to (or click inside one) first.");
                return;
            }
            IntPtr hwnd = pane.AbsorbedHwnd;
            string strProblem = cConsoleInput.TypeText(hwnd, strText.TrimEnd('\r', '\n'));
            if (strProblem != null) {
                mactStatus("Not sent: " + strProblem);
                return;
            }
            mblnSending = true;
            txtPrompt.ReadOnly = true;
            Timer t = new Timer();
            t.Interval = CNST_ENTER_DELAY_MS;
            t.Tick += delegate {
                t.Stop();
                t.Dispose();
                string strEnter = cConsoleInput.PressEnter(hwnd);
                mblnSending        = false;
                txtPrompt.ReadOnly = false;
                if (strEnter != null) {
                    mactStatus("The text was typed, but Enter did not reach the pane: " + strEnter);
                    return;
                }
                Remember(strText);
                txtPrompt.Clear();
                mactStatus("Sent to " + pane.Text + ".");
            };
            t.Start();
        }

        private void Remember(string astrText) {
            mintHistoryAt = -1;
            mstrDraft     = "";
            if (mlstHistory.Count > 0 && mlstHistory[mlstHistory.Count - 1] == astrText) return;
            mlstHistory.Add(astrText);
            if (mlstHistory.Count > CNST_HISTORY_MAX) mlstHistory.RemoveAt(0);
        }

        // Clear Screen: wipes the screen of the pane in Send to (cConsoleInput.ClearScreen).
        private void ClearScreen() {
            fAgentAbsorbedTerminal pane = mfncTarget();
            if (pane == null) {
                mactStatus("Pick a pane in Send to (or click inside one) first.");
                return;
            }
            string strProblem = cConsoleInput.ClearScreen(pane.AbsorbedHwnd);
            mactStatus(strProblem == null
                ? "Cleared the screen of " + pane.Text + ". The agent keeps its conversation. Its prompt line comes back when you type or it next writes."
                : "Not cleared: " + strProblem);
            txtPrompt.Focus();
        }

        // ------------------------------------------------------------ conversation pane

        // The header of a conversation pane: its number and name on its tab's color, and what its
        // agent is doing at the right. Right-click it (or use the Theme button) for the pane's look.
        private readonly Panel  pnlHeader  = new Panel();
        private readonly Label  lblHeader  = new Label();
        private readonly Label  lblState   = new Label();
        private readonly Button btnTheme   = new Button();
        private int    mintCardState = -1;

        // Raised when a conversation pane's Theme button or header is right-clicked, with where the
        // menu goes. The dock builds the menu (the same one as Settings -> Pane Theme).
        public event Action<cComposeBox, Control, Point> ThemeMenuWanted;

        // The pane's place in the order, for the header ("2. Pane title").
        public int Number { get { return mintNumber; } set { mintNumber = value; PaintHeader(); } }
        private int mintNumber;

        private void BuildCard() {
            this.Dock    = DockStyle.Fill;
            this.Height  = 200;
            this.Padding = new Padding(3);
            pnlLabelRow.Visible = false;
            cboTarget.Visible   = false;
            chkFull.Visible     = false;
            mpnlFull.ColumnStyles[0].Width = 0F;     // Clear Screen gets the whole row
            mpnlFull.ColumnStyles[1].Width = 100F;
            splSend.SplitterMoved -= SendWidthMoved;
            pnlRight.Width = Math.Min(pnlRight.Width, 200);

            pnlHeader.Dock    = DockStyle.Top;
            pnlHeader.Height  = 26;
            pnlHeader.Padding = new Padding(6, 0, 2, 0);
            lblHeader.Dock      = DockStyle.Fill;
            lblHeader.TextAlign = ContentAlignment.MiddleLeft;
            lblHeader.AutoEllipsis = true;
            lblHeader.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblState.Dock       = DockStyle.Right;
            lblState.AutoSize   = false;
            lblState.Width      = 86;
            lblState.TextAlign  = ContentAlignment.MiddleCenter;
            lblState.Font       = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnTheme.Text       = "Theme";
            btnTheme.Dock       = DockStyle.Right;
            btnTheme.Width      = 62;
            btnTheme.FlatStyle  = FlatStyle.Flat;
            btnTheme.FlatAppearance.BorderSize = 0;
            btnTheme.Font       = new Font("Segoe UI", 8.25F);
            mtipButtons.SetToolTip(btnTheme, "This pane's own colors and bubbles, or the dock's.");
            btnTheme.Click += delegate {
                if (ThemeMenuWanted != null) ThemeMenuWanted(this, btnTheme, new Point(0, btnTheme.Height));
            };
            MouseEventHandler rightClick = delegate(object o, MouseEventArgs e) {
                if (e.Button == MouseButtons.Right && ThemeMenuWanted != null) ThemeMenuWanted(this, (Control)o, e.Location);
            };
            lblHeader.MouseUp += rightClick;
            pnlHeader.MouseUp += rightClick;
            pnlHeader.Controls.Add(lblHeader);
            pnlHeader.Controls.Add(btnTheme);
            pnlHeader.Controls.Add(lblState);
            this.Controls.Add(pnlHeader);           // docked last of all: across the top

            // Writing in a conversation pane makes its pane the dock's current one, so the Agents
            // menu (Close Active Pane, Unparent Active Pane) acts on it.
            txtPrompt.Enter += delegate { mactSetTarget(mfrmPane); };
            AgentStateChanged += delegate(string astrText, int aintState) { mintCardState = aintState; PaintHeader(); };
            PaintHeader();
        }

        private void PaintHeader() {
            if (!IsCard) return;
            Color clrTab = cPaneColors.ColorOf(mfrmPane.AbsorbedHwnd);
            Color clrBack = clrTab.IsEmpty ? (mTheme.IsSystem ? SystemColors.ControlDark : mTheme.Border) : clrTab;
            double dblLight = 0.299 * clrBack.R + 0.587 * clrBack.G + 0.114 * clrBack.B;
            Color clrText = dblLight > 150 ? Color.Black : Color.White;
            pnlHeader.BackColor = clrBack;
            lblHeader.ForeColor = clrText;
            btnTheme.BackColor  = clrBack;
            btnTheme.ForeColor  = clrText;
            btnTheme.FlatAppearance.MouseOverBackColor = Blend(clrBack, clrText, 0.2);
            string strTitle = (mintNumber > 0 ? mintNumber + ". " : "") + (mfrmPane.IsDisposed ? "" : mfrmPane.Text);
            if (lblHeader.Text != strTitle) lblHeader.Text = strTitle;
            lblState.Visible   = mintCardState >= 0;
            lblState.Text      = mintCardState == 1 ? "Thinking..." : "Idle";
            lblState.BackColor = mintCardState == 1 ? Color.FromArgb(255, 214, 120) : Color.FromArgb(170, 225, 170);
            lblState.ForeColor = Color.Black;
        }

        // Called by the dock about once a second: the pane's title and tab color may have changed.
        public void RefreshHeader() {
            if (IsCard) PaintHeader();
        }

        // Puts the keyboard in this pane's text box.
        public void FocusText() { txtPrompt.Focus(); }

        protected override void Dispose(bool ablnDisposing) {
            if (ablnDisposing) {
                cPaneThemes.Changed -= new Action(RefreshTheme);
                tmrTarget.Dispose(); tmrLive.Dispose(); mtipButtons.Dispose();
            }
            base.Dispose(ablnDisposing);
        }
    }
}
