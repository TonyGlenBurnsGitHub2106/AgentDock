using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgentDockShared;

namespace AgentDockStandalone {

    // Conversation panes (Story 6846, Tony: "a mode where I can have every terminal hide and be
    // replaced with an instance of Run Conversation ... In this mode, I would only see compose
    // terminals"). Settings -> Conversation Panes. The dock hides its pane area and shows this
    // instead: one conversation pane (a cComposeBox that belongs to one terminal) for each
    // terminal, in the dock's pane order. The terminals keep running, hidden; each conversation
    // pane sends to its own.
    //
    // Laid out exactly as the terminals would be (Tony: "When conversation mode is on, window tile
    // options need to be honored, such as tile vertical"): the Window menu's Tile Vertical,
    // Tile Horizontal, Cascade, Quadrant Layout and Maximize / Restore Pane all apply, through the
    // arranger's own layout (cPaneArranger.Layout). Kept in step with the panes once a second.
    internal sealed class cConversationGrid : Panel {

        private const int CNST_GAP = 2;     // between conversation panes

        private readonly cPaneArranger    mPanes;
        private readonly Action<string>   mactStatus;
        private readonly Action<cComposeBox, Control, Point> mactThemeMenu;
        private readonly Label            lblEmpty = new Label();
        private readonly Timer            tmrSync  = new Timer();
        private readonly List<cComposeBox> mlstCards = new List<cComposeBox>();

        public cConversationGrid(cPaneArranger aPanes, Action<string> aactStatus,
                                 Action<cComposeBox, Control, Point> aactThemeMenu) {
            mPanes        = aPanes;
            mactStatus    = aactStatus;
            mactThemeMenu = aactThemeMenu;
            this.Dock    = DockStyle.Fill;
            this.Visible = false;
            this.BackColor = SystemColors.AppWorkspace;

            lblEmpty.Dock      = DockStyle.Fill;
            lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            lblEmpty.ForeColor = SystemColors.ControlLightLight;
            lblEmpty.Font      = new Font("Segoe UI", 11F);
            lblEmpty.Text      = "No panes yet. Open one with Agents -> Spawn Additional Pane: its conversation shows here.\r\n"
                               + "Settings -> Conversation Panes brings the terminals back.";
            this.Controls.Add(lblEmpty);

            tmrSync.Interval = 1000;
            tmrSync.Tick    += delegate { Sync(); };
            mPanes.LayoutChanged += new Action(PlaceCards);
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

        // Every conversation pane, in order (the theme viewer offers them).
        public List<cComposeBox> Cards { get { return new List<cComposeBox>(mlstCards); } }

        private void Sync() {
            if (!this.Visible || this.IsDisposed) return;
            List<fAgentAbsorbedTerminal> lstPanes = new List<fAgentAbsorbedTerminal>();
            foreach (Form f in mPanes.OrderedPanes()) {
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

        // A new set of panes (or a new order). A pane that is still there keeps its conversation
        // pane, and what was typed in it.
        private void Rebuild(List<fAgentAbsorbedTerminal> alstPanes) {
            List<cComposeBox> lstKeep = new List<cComposeBox>();
            List<cComposeBox> lstNew  = new List<cComposeBox>();
            foreach (fAgentAbsorbedTerminal pane in alstPanes) {
                cComposeBox card = CardFor(pane);
                if (card == null) { card = NewCard(pane); lstNew.Add(card); }
                lstKeep.Add(card);
            }
            this.SuspendLayout();
            foreach (cComposeBox old in mlstCards) {
                if (!lstKeep.Contains(old)) { this.Controls.Remove(old); old.SetShown(false); old.Dispose(); }
            }
            mlstCards.Clear();
            mlstCards.AddRange(lstKeep);
            foreach (cComposeBox card in mlstCards) if (card.Parent != this) this.Controls.Add(card);
            lblEmpty.Visible = mlstCards.Count == 0;
            this.ResumeLayout(false);
            // Started once inside the grid, so each has its window before its first read comes back.
            foreach (cComposeBox card in lstNew) card.SetShown(true);
            PlaceCards();
        }

        // Puts each conversation pane where its terminal would be: the arranger's tiling, or the
        // maximized pane alone.
        private void PlaceCards() {
            if (!this.Visible || this.IsDisposed || mlstCards.Count == 0) return;
            Rectangle area = this.ClientRectangle;
            if (area.Width <= 0 || area.Height <= 0) return;
            Form frmMax = mPanes.MaximizedPane;
            cComposeBox cardMax = frmMax == null ? null : CardFor(frmMax);
            this.SuspendLayout();
            if (cardMax != null) {
                foreach (cComposeBox c in mlstCards) c.Visible = c == cardMax;
                Place(cardMax, area);
                cardMax.BringToFront();
            } else {
                string strMode = mPanes.Tiling;
                Rectangle[] r = cPaneArranger.Layout(strMode, mlstCards.Count, area);
                for (int i = 0; i < mlstCards.Count; i++) {
                    mlstCards[i].Visible = true;
                    Place(mlstCards[i], r[i]);
                    if (strMode == cPaneArranger.CNST_TILE_CASCADE) mlstCards[i].BringToFront();
                }
            }
            this.ResumeLayout(true);
        }

        private static void Place(Control actl, Rectangle ar) {
            actl.Dock = DockStyle.None;
            actl.SetBounds(ar.X + CNST_GAP, ar.Y + CNST_GAP, Math.Max(40, ar.Width - 2 * CNST_GAP), Math.Max(40, ar.Height - 2 * CNST_GAP));
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            PlaceCards();
        }

        private cComposeBox NewCard(fAgentAbsorbedTerminal apane) {
            cComposeBox card = new cComposeBox(null, delegate { return mPanes.OrderedPanes(); },
                delegate(Form f) { mPanes.SetCurrentPane(f); }, mactStatus, apane);
            card.ThemeMenuWanted += delegate(cComposeBox c, Control ctl, Point pt) { if (mactThemeMenu != null) mactThemeMenu(c, ctl, pt); };
            // Cascade: the one you work in comes to the front.
            card.Enter += delegate { if (mPanes.Tiling == cPaneArranger.CNST_TILE_CASCADE) card.BringToFront(); };
            return card;
        }

        private void RemoveAll() {
            foreach (cComposeBox c in mlstCards) { this.Controls.Remove(c); c.SetShown(false); c.Dispose(); }
            mlstCards.Clear();
            lblEmpty.Visible = true;
        }

        // Puts the keyboard in the conversation pane of afrm (or the first one).
        public void FocusPane(Form afrm) {
            cComposeBox card = afrm == null ? null : CardFor(afrm);
            if (card == null && mlstCards.Count > 0) card = mlstCards[0];
            if (card != null) card.FocusText();
        }

        protected override void Dispose(bool ablnDisposing) {
            if (ablnDisposing) {
                mPanes.LayoutChanged -= new Action(PlaceCards);
                tmrSync.Dispose();
                RemoveAll();
            }
            base.Dispose(ablnDisposing);
        }
    }
}
