using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgentDockStandalone {

    // Conversation panes (Story 6846, Tony: "a mode where I can have every terminal hide and be
    // replaced with an instance of Run Conversation ... In this mode, I would only see compose
    // terminals"). Settings -> Conversation Panes. The dock hides its pane area and shows this
    // instead: one conversation pane (a cComposeBox that belongs to one terminal) for each
    // terminal, in the dock's pane order, in a grid. The terminals keep running, hidden; each
    // conversation pane sends to its own.
    //
    // Kept in step with the panes once a second: a pane opened, absorbed or closed adds or drops
    // its conversation pane, and the order follows Move Pane.
    internal sealed class cConversationGrid : Panel {

        private readonly Func<List<Form>> mfncPanes;
        private readonly Action<Form>     mactSetTarget;
        private readonly Action<string>   mactStatus;
        private readonly Action<cComposeBox, Control, Point> mactThemeMenu;
        private readonly TableLayoutPanel tblGrid = new TableLayoutPanel();
        private readonly Label            lblEmpty = new Label();
        private readonly Timer            tmrSync  = new Timer();
        private readonly List<cComposeBox> mlstCards = new List<cComposeBox>();

        public cConversationGrid(Func<List<Form>> afncPanes, Action<Form> aactSetTarget,
                                 Action<string> aactStatus, Action<cComposeBox, Control, Point> aactThemeMenu) {
            mfncPanes     = afncPanes;
            mactSetTarget = aactSetTarget;
            mactStatus    = aactStatus;
            mactThemeMenu = aactThemeMenu;
            this.Dock    = DockStyle.Fill;
            this.Visible = false;
            this.BackColor = SystemColors.AppWorkspace;

            tblGrid.Dock    = DockStyle.Fill;
            tblGrid.Padding = new Padding(2);
            lblEmpty.Dock      = DockStyle.Fill;
            lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            lblEmpty.ForeColor = SystemColors.ControlLightLight;
            lblEmpty.Font      = new Font("Segoe UI", 11F);
            lblEmpty.Text      = "No panes yet. Open one with Agents -> New Pane: its conversation shows here.\r\n"
                               + "Settings -> Conversation Panes brings the terminals back.";
            this.Controls.Add(tblGrid);
            this.Controls.Add(lblEmpty);

            tmrSync.Interval = 1000;
            tmrSync.Tick    += delegate { Sync(); };
        }

        // Shows the conversation panes (and starts keeping them in step) or hides them all.
        public void SetShown(bool ablnShow) {
            this.Visible = ablnShow;
            if (ablnShow) {
                Sync();
                tmrSync.Start();
            } else {
                tmrSync.Stop();
                RemoveAll();
            }
        }

        // The conversation pane for afrm, when there is one.
        public cComposeBox CardFor(Form afrm) {
            foreach (cComposeBox c in mlstCards) if (c.Pane == afrm) return c;
            return null;
        }

        private void Sync() {
            if (!this.Visible || this.IsDisposed) return;
            List<fAgentAbsorbedTerminal> lstPanes = new List<fAgentAbsorbedTerminal>();
            foreach (Form f in mfncPanes()) {
                fAgentAbsorbedTerminal abs = f as fAgentAbsorbedTerminal;
                if (abs != null && !abs.IsDisposed) lstPanes.Add(abs);
            }
            bool blnSame = lstPanes.Count == mlstCards.Count;
            for (int i = 0; blnSame && i < lstPanes.Count; i++) blnSame = mlstCards[i].Pane == lstPanes[i];
            if (!blnSame) Rebuild(lstPanes);
            for (int i = 0; i < mlstCards.Count; i++) {
                mlstCards[i].Number = i + 1;
                mlstCards[i].RefreshHeader();
            }
        }

        // Lays the conversation panes out again for a new set of panes. A pane that is still there
        // keeps its conversation pane (and what was typed in it).
        private void Rebuild(List<fAgentAbsorbedTerminal> alstPanes) {
            List<cComposeBox> lstKeep = new List<cComposeBox>();
            foreach (fAgentAbsorbedTerminal pane in alstPanes) {
                cComposeBox card = CardFor(pane);
                if (card == null) card = NewCard(pane);
                lstKeep.Add(card);
            }
            this.SuspendLayout();
            tblGrid.SuspendLayout();
            tblGrid.Controls.Clear();
            foreach (cComposeBox old in mlstCards) {
                if (!lstKeep.Contains(old)) { old.SetShown(false); old.Dispose(); }
            }
            mlstCards.Clear();
            mlstCards.AddRange(lstKeep);

            int n = mlstCards.Count;
            int intCols = n <= 1 ? 1 : (n <= 4 ? 2 : (int)Math.Ceiling(Math.Sqrt(n)));
            int intRows = n == 0 ? 1 : (int)Math.Ceiling(n / (double)intCols);
            if (n == 2) { intCols = 2; intRows = 1; }
            tblGrid.ColumnStyles.Clear();
            tblGrid.RowStyles.Clear();
            tblGrid.ColumnCount = intCols;
            tblGrid.RowCount    = intRows;
            for (int c = 0; c < intCols; c++) tblGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / intCols));
            for (int r = 0; r < intRows; r++) tblGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / intRows));
            for (int i = 0; i < n; i++) {
                cComposeBox card = mlstCards[i];
                card.Margin = new Padding(2);
                tblGrid.Controls.Add(card, i % intCols, i / intCols);
                // The last one of an odd count spans the rest of its row, so no hole is left.
                int intLeft = intCols - (i % intCols);
                tblGrid.SetColumnSpan(card, i == n - 1 ? intLeft : 1);
            }
            tblGrid.ResumeLayout(true);
            lblEmpty.Visible = n == 0;
            tblGrid.Visible  = n > 0;
            this.ResumeLayout(true);
            // Started once placed, so each has its window before its first read comes back.
            foreach (cComposeBox card in mlstCards) if (!card.Visible) card.SetShown(true);
        }

        private cComposeBox NewCard(fAgentAbsorbedTerminal apane) {
            cComposeBox card = new cComposeBox(null, mfncPanes, mactSetTarget, mactStatus, apane);
            card.ThemeMenuWanted += delegate(cComposeBox c, Control ctl, Point pt) { if (mactThemeMenu != null) mactThemeMenu(c, ctl, pt); };
            return card;
        }

        private void RemoveAll() {
            tblGrid.Controls.Clear();
            foreach (cComposeBox c in mlstCards) { c.SetShown(false); c.Dispose(); }
            mlstCards.Clear();
        }

        // Puts the keyboard in the conversation pane of afrm (or the first one).
        public void FocusPane(Form afrm) {
            cComposeBox card = afrm == null ? null : CardFor(afrm);
            if (card == null && mlstCards.Count > 0) card = mlstCards[0];
            if (card != null) card.FocusText();
        }

        protected override void Dispose(bool ablnDisposing) {
            if (ablnDisposing) { tmrSync.Dispose(); RemoveAll(); }
            base.Dispose(ablnDisposing);
        }
    }
}
