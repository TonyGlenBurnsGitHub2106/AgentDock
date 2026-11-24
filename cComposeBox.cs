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
        private readonly Label    lblTarget = new Label();
        private readonly ComboBox cboTarget = new ComboBox();
        private readonly Timer   tmrTarget = new Timer();

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
                           Action<Form> aactSetTarget, Action<string> aactStatus) {
            mfncTarget    = afncTarget;
            mfncPanes     = afncPanes;
            mactSetTarget = aactSetTarget;
            mactStatus    = aactStatus;
            BuildLayout();
        }

        private void BuildLayout() {
            this.Dock    = DockStyle.Bottom;
            this.Height  = CNST_HEIGHT;
            this.Padding = new Padding(4);
            this.Visible = false;

            Panel pnlRight = new Panel();
            pnlRight.Dock    = DockStyle.Right;
            pnlRight.Width   = 170;
            pnlRight.Padding = new Padding(6, 0, 0, 0);

            btnSend.Text   = "Send";
            btnSend.Dock   = DockStyle.Top;
            btnSend.Height = 30;
            btnSend.Click += delegate { Send(); txtPrompt.Focus(); };

            // Send to: the pane a prompt goes to. It follows the pane you click in, and picking
            // one here does the same as clicking in it.
            lblTarget.Text      = "Send to:";
            lblTarget.Dock      = DockStyle.Top;
            lblTarget.Height    = 20;
            lblTarget.ForeColor = SystemColors.GrayText;
            lblTarget.TextAlign = ContentAlignment.BottomLeft;

            cboTarget.Dock          = DockStyle.Top;
            cboTarget.DropDownStyle = ComboBoxStyle.DropDownList;
            cboTarget.DropDownWidth = 360;
            // Any change the box did not make itself is the user's pick (mouse or keyboard).
            cboTarget.SelectedIndexChanged += delegate {
                if (mblnSyncing) return;
                TargetItem item = cboTarget.SelectedItem as TargetItem;
                if (item != null) mactSetTarget(item.Pane);
            };
            cboTarget.DropDownClosed += delegate { txtPrompt.Focus(); };

            pnlRight.Controls.Add(cboTarget);
            pnlRight.Controls.Add(lblTarget);
            pnlRight.Controls.Add(btnSend);

            txtPrompt.Multiline     = true;
            txtPrompt.AcceptsReturn = true;
            txtPrompt.AcceptsTab    = false;
            txtPrompt.WordWrap      = true;
            txtPrompt.ScrollBars    = ScrollBars.Vertical;
            txtPrompt.Dock          = DockStyle.Fill;
            txtPrompt.Font          = new Font("Consolas", 10F);
            txtPrompt.KeyDown      += new KeyEventHandler(Prompt_KeyDown);

            this.Controls.Add(txtPrompt);
            this.Controls.Add(pnlRight);

            tmrTarget.Interval = 400;
            tmrTarget.Tick    += delegate { ShowTarget(); };
        }

        // Shows or hides the box. Hidden, it does nothing at all.
        public void SetShown(bool ablnShow) {
            this.Visible = ablnShow;
            if (ablnShow) { ShowTarget(); tmrTarget.Start(); }
            else tmrTarget.Stop();
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
        private List<string> mlstBrowse = new List<string>();

        private List<string> HistoryForTarget() {
            fAgentAbsorbedTerminal pane = mfncTarget();
            List<string> lstAgent = pane == null ? null : cAgentHistory.ForPane(pane.AbsorbedHwnd, CNST_HISTORY_MAX);
            return (lstAgent != null && lstAgent.Count > 0) ? lstAgent : new List<string>(mlstHistory);
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
            SetPrompt(mlstBrowse[intAt]);
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

        protected override void Dispose(bool ablnDisposing) {
            if (ablnDisposing) tmrTarget.Dispose();
            base.Dispose(ablnDisposing);
        }
    }
}
