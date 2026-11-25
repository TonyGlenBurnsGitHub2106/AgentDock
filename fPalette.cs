using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgentDockStandalone {

    // The Palette (Story 6846, Tony: "a popup that is a settings viewer that lets us scroll through
    // bubble styles, themes, etc. and a save to global or save to individual active pane for
    // whatever combination we decided we like"; then "it needs to be on the palette also").
    //
    // Three lists, colors, bubbles and font, and a live preview of a short conversation drawn
    // the way the pick looks. Arrow keys step through a list, so a look can be tried in a second.
    // Save for All Panes makes it the global look, Save for Pane gives the chosen pane's place its
    // own, and Use Global sends that place back to the global look. Nothing changes until a save.
    internal sealed class fPalette : Form {

        private readonly ListBox lstColors = new ListBox();
        private readonly ListBox lstBubbles = new ListBox();
        private readonly ListBox lstFonts = new ListBox();
        private readonly ListBox lstPromptFonts = new ListBox();
        private readonly ComboBox cboFontSize = new ComboBox();
        private readonly ComboBox cboPromptSize = new ComboBox();
        private readonly CheckBox chkHideNotes = new CheckBox();
        private readonly Label   lblDescription = new Label();
        private readonly Panel   pnlPreview = new Panel();
        private readonly cConversationView vwPreview = new cConversationView();
        private readonly Panel   pnlInput = new Panel();
        private readonly ComboBox cboPane = new ComboBox();
        private readonly Button  btnSaveAll = new Button();
        private readonly Button  btnSavePane = new Button();
        private readonly Button  btnGlobal = new Button();
        private readonly Button  btnClose = new Button();
        private readonly Label   lblSaved = new Label();
        private readonly List<fAgentAbsorbedTerminal> mlstPanes;
        private bool mblnLoading;

        public fPalette(List<fAgentAbsorbedTerminal> alstPanes, fAgentAbsorbedTerminal afrmPane) {
            mlstPanes = alstPanes ?? new List<fAgentAbsorbedTerminal>();
            this.Text            = "Palette - Agent Dock";
            this.StartPosition   = FormStartPosition.CenterParent;
            this.Size            = new Size(1180, 660);
            this.MinimumSize     = new Size(760, 460);
            this.ShowInTaskbar   = false;
            this.KeyPreview      = true;
            this.MinimizeBox     = false;
            this.Font            = new Font("Segoe UI", 9F);
            BuildLayout();

            mblnLoading = true;
            foreach (cComposeTheme t in cComposeTheme.All) lstColors.Items.Add(t.Name);
            foreach (cBubbleStyle s in cBubbleStyle.All) lstBubbles.Items.Add(s.Name);
            foreach (cConversationFont f in cConversationFont.All) { lstFonts.Items.Add(f.Name); lstPromptFonts.Items.Add(f.Name); }
            chkHideNotes.Checked = cDockSettings.HideWorkingNotes;
            cboPane.Items.Add("Global look");
            for (int i = 0; i < mlstPanes.Count; i++) cboPane.Items.Add((i + 1) + ". " + mlstPanes[i].Text);
            int intAt = afrmPane == null ? -1 : mlstPanes.IndexOf(afrmPane);
            mblnLoading = false;
            cboPane.SelectedIndex = intAt >= 0 ? intAt + 1 : 0;   // loads that pane's look (or the global one)
            ShowPreview();
        }

        private void BuildLayout() {
            // The three lists, side by side at the left.
            TableLayoutPanel tblLists = new TableLayoutPanel();
            tblLists.Dock = DockStyle.Left;
            tblLists.Width = 600;
            tblLists.ColumnCount = 4;
            tblLists.RowCount = 3;
            tblLists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31F));
            tblLists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            tblLists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            tblLists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
            tblLists.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            tblLists.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblLists.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblLists.Padding = new Padding(8, 8, 4, 4);
            string[] astrHeads = new string[] { "Colors", "Bubbles", "Font", "Prompt Font" };
            ListBox[] alst = new ListBox[] { lstColors, lstBubbles, lstFonts, lstPromptFonts };
            for (int i = 0; i < 4; i++) {
                Label lbl = new Label();
                lbl.Text = astrHeads[i];
                lbl.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                lbl.Dock = DockStyle.Fill;
                tblLists.Controls.Add(lbl, i, 0);
                alst[i].Dock = DockStyle.Fill;
                alst[i].IntegralHeight = false;
                alst[i].SelectedIndexChanged += delegate { if (!mblnLoading) ShowPreview(); };
                tblLists.Controls.Add(alst[i], i, 1);
            }
            // A size under each font list (Tony: "pick font and font size, not just font").
            // Picking a font puts its own size here; then any size can be chosen.
            ComboBox[] acbo = new ComboBox[] { cboFontSize, cboPromptSize };
            for (int i = 0; i < 2; i++) {
                ComboBox cbo = acbo[i];
                cbo.DropDownStyle = ComboBoxStyle.DropDownList;
                cbo.Dock = DockStyle.Fill;
                foreach (float f in cConversationFont.Sizes) cbo.Items.Add(cConversationFont.SizeText(f) + " pt");
                cbo.SelectedIndexChanged += delegate { if (!mblnLoading) ShowPreview(); };
                tblLists.Controls.Add(cbo, i + 2, 2);
            }
            Label lblSize = new Label();
            lblSize.Text = "Size";
            lblSize.AutoSize = true;
            lblSize.Anchor = AnchorStyles.Right;
            tblLists.Controls.Add(lblSize, 1, 2);
            lstFonts.SelectedIndexChanged += delegate { if (!mblnLoading) { SyncSize(cboFontSize, cConversationFont.Named(lstFonts.SelectedItem as string)); ShowPreview(); } };
            lstPromptFonts.SelectedIndexChanged += delegate { if (!mblnLoading) { SyncSize(cboPromptSize, cConversationFont.Named(lstPromptFonts.SelectedItem as string)); ShowPreview(); } };
            // Each color theme drawn in its own colors, so the list itself is a palette.
            lstColors.DrawMode   = DrawMode.OwnerDrawFixed;
            lstColors.ItemHeight = 24;
            lstColors.DrawItem  += new DrawItemEventHandler(Colors_DrawItem);

            // The preview at the right: the conversation, and under it a strip in the theme's
            // text box colors.
            lblDescription.Dock = DockStyle.Top;
            lblDescription.Height = 40;
            lblDescription.Padding = new Padding(6, 8, 6, 0);
            vwPreview.Dock = DockStyle.Fill;
            pnlInput.Dock = DockStyle.Bottom;
            pnlInput.Height = 34;
            pnlInput.Paint += new PaintEventHandler(Input_Paint);
            pnlPreview.Dock = DockStyle.Fill;
            pnlPreview.Padding = new Padding(4, 0, 8, 4);
            pnlPreview.Controls.Add(vwPreview);
            pnlPreview.Controls.Add(pnlInput);
            pnlPreview.Controls.Add(lblDescription);

            // The bottom row: which pane, the saves, Close.
            FlowLayoutPanel pnlBottom = new FlowLayoutPanel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 46;
            pnlBottom.Padding = new Padding(8, 8, 8, 4);
            pnlBottom.WrapContents = false;
            Label lblFor = new Label();
            lblFor.Text = "Pane";
            lblFor.AutoSize = true;
            lblFor.Padding = new Padding(0, 6, 0, 0);
            cboPane.DropDownStyle = ComboBoxStyle.DropDownList;
            cboPane.Width = 220;
            cboPane.SelectedIndexChanged += delegate { if (!mblnLoading) LoadLookOfChoice(); };
            Btn(btnSavePane, "Save for Pane", 110);
            btnSavePane.Click += delegate { SaveForPane(); };
            Btn(btnGlobal, "Use Global", 90);
            btnGlobal.Click += delegate { UseGlobal(); };
            Btn(btnSaveAll, "Save for All Panes", 140);
            btnSaveAll.Click += delegate { SaveForAll(); };
            Btn(btnClose, "Close", 80);
            btnClose.Click += delegate { this.Close(); };
            lblSaved.AutoSize = true;
            lblSaved.Padding = new Padding(8, 6, 0, 0);
            lblSaved.ForeColor = Color.FromArgb(30, 120, 50);
            // Hide Working Notes, the same switch as on the Settings menu (it is for every pane).
            chkHideNotes.Text = "Hide Working Notes";
            chkHideNotes.AutoSize = true;
            chkHideNotes.Padding = new Padding(10, 5, 0, 0);
            chkHideNotes.CheckedChanged += delegate {
                if (mblnLoading) return;
                try { cDockSettings.SaveHideWorkingNotes(chkHideNotes.Checked); } catch (Exception ex) { lblSaved.Text = "Not saved: " + ex.Message; }
                cPaneThemes.RaiseChanged();
                vwPreview.SetEntries(Sample(), true);
            };
            pnlBottom.Controls.AddRange(new Control[] { lblFor, cboPane, btnSavePane, btnGlobal, btnSaveAll, btnClose, chkHideNotes, lblSaved });
            this.CancelButton = btnClose;

            this.Controls.Add(pnlPreview);
            this.Controls.Add(tblLists);
            this.Controls.Add(pnlBottom);

            vwPreview.SetEntries(SampleConversation(), true);
        }

        // The sample, without its working note when Hide Working Notes is ticked.
        private List<cHistoryEntry> Sample() {
            List<cHistoryEntry> lst = SampleConversation();
            if (chkHideNotes.Checked) lst.RemoveAt(1);   // "Looking at the build log now."
            return lst;
        }

        private static void Btn(Button ab, string astrText, int aintWidth) {
            ab.Text = astrText;
            ab.Width = aintWidth;
            ab.Height = 28;
        }

        // A short made-up conversation, enough to show both kinds of bubble, a run of answers and
        // a longer message.
        private static List<cHistoryEntry> SampleConversation() {
            List<cHistoryEntry> lst = new List<cHistoryEntry>();
            lst.Add(new cHistoryEntry("Can you check why the nightly build failed?", false));
            lst.Add(new cHistoryEntry("Looking at the build log now.", true));
            lst.Add(new cHistoryEntry("The test step timed out: one test waits on a network share that was not mounted.\r\n\r\nI made the test skip itself when the share is missing, and the build is green again.", true));
            lst.Add(new cHistoryEntry("Great. Push it, please.", false));
            lst.Add(new cHistoryEntry("Pushed. The next nightly run will pick it up.", true));
            return lst;
        }

        private cComposeTheme Theme() { return cComposeTheme.Named(lstColors.SelectedItem as string); }
        private cBubbleStyle  Style() { return cBubbleStyle.Named(lstBubbles.SelectedItem as string); }
        private cConversationFont FontPick() { return cConversationFont.Named(lstFonts.SelectedItem as string).WithSize(SizeOf(cboFontSize)); }
        private cConversationFont PromptFontPick() { return cConversationFont.Named(lstPromptFonts.SelectedItem as string).WithSize(SizeOf(cboPromptSize)); }

        private static float SizeOf(ComboBox acbo) {
            int i = acbo.SelectedIndex;
            return i >= 0 && i < cConversationFont.Sizes.Length ? cConversationFont.Sizes[i] : 0F;
        }

        // Shows afont's size in the size list (the nearest one offered), without a preview of its own.
        private void SyncSize(ComboBox acbo, cConversationFont afont) {
            bool blnWas = mblnLoading;
            mblnLoading = true;
            int intBest = 0;
            for (int i = 0; i < cConversationFont.Sizes.Length; i++)
                if (Math.Abs(cConversationFont.Sizes[i] - afont.Size) < Math.Abs(cConversationFont.Sizes[intBest] - afont.Size)) intBest = i;
            acbo.SelectedIndex = intBest;
            mblnLoading = blnWas;
        }

        private void ShowPreview() {
            cComposeTheme t = Theme();
            cBubbleStyle s = Style();
            vwPreview.ApplyTheme(t, s, FontPick());
            pnlPreview.BackColor = t.IsSystem ? SystemColors.Control : t.Back;
            lblDescription.ForeColor = t.IsSystem ? SystemColors.ControlText : t.Fore;
            lblDescription.Text = t.Name + " colors, " + s.Name + " bubbles, " + FontPick().Name + " font, "
                + PromptFontPick().Name + " prompt font. " + s.Description;
            vwPreview.SetEntries(Sample(), true);
            pnlInput.Invalidate();
            lblSaved.Text = "";
        }

        // The text box strip under the preview, in the theme's colors.
        private void Input_Paint(object sender, PaintEventArgs e) {
            cComposeTheme t = Theme();
            Rectangle rc = new Rectangle(0, 4, pnlInput.Width - 1, pnlInput.Height - 6);
            using (SolidBrush br = new SolidBrush(t.IsSystem ? SystemColors.Window : t.InputBack)) e.Graphics.FillRectangle(br, rc);
            using (Pen pen = new Pen(t.Border)) e.Graphics.DrawRectangle(pen, rc);
            using (Font fnt = PromptFontPick().Create())
            TextRenderer.DrawText(e.Graphics, "Write your prompt here", fnt, Rectangle.Inflate(rc, -6, 0),
                t.IsSystem ? SystemColors.GrayText : t.Caption, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private void Colors_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0) return;
            cComposeTheme t = cComposeTheme.Named((string)lstColors.Items[e.Index]);
            bool blnSel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color clrBack = t.IsSystem ? SystemColors.Window : t.InputBack;
            using (SolidBrush br = new SolidBrush(clrBack)) e.Graphics.FillRectangle(br, e.Bounds);
            // Two swatches: your prompts' color and the answers' outline.
            Rectangle rcA = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 5, 12, e.Bounds.Height - 10);
            Rectangle rcB = new Rectangle(rcA.Right + 2, rcA.Y, 12, rcA.Height);
            using (SolidBrush b1 = new SolidBrush(t.PromptEdge)) e.Graphics.FillRectangle(b1, rcA);
            using (SolidBrush b2 = new SolidBrush(t.AnswerEdge)) e.Graphics.FillRectangle(b2, rcB);
            Rectangle rcText = new Rectangle(rcB.Right + 6, e.Bounds.Y, e.Bounds.Width - (rcB.Right + 6 - e.Bounds.X), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, t.Name, lstColors.Font, rcText, t.IsSystem ? SystemColors.WindowText : t.InputFore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            if (blnSel) {
                Rectangle r = e.Bounds; r.Width -= 1; r.Height -= 1;
                using (Pen pen = new Pen(SystemColors.Highlight, 2)) e.Graphics.DrawRectangle(pen, r);
            }
        }

        private fAgentAbsorbedTerminal ChosenPane() {
            int i = cboPane.SelectedIndex - 1;
            return i >= 0 && i < mlstPanes.Count ? mlstPanes[i] : null;
        }

        // Picking a pane (or the global look) shows the look it has now.
        private void LoadLookOfChoice() {
            fAgentAbsorbedTerminal pane = ChosenPane();
            IntPtr hwnd = pane == null ? IntPtr.Zero : pane.AbsorbedHwnd;
            mblnLoading = true;
            Select(lstColors,  pane == null ? cDockSettings.ComposeTheme     : cPaneThemes.ThemeFor(hwnd).Name);
            Select(lstBubbles, pane == null ? cDockSettings.BubbleStyle      : cPaneThemes.StyleFor(hwnd).Name);
            cConversationFont font = pane == null ? cConversationFont.Named(cDockSettings.ConversationFont) : cPaneThemes.FontFor(hwnd);
            cConversationFont fontPrompt = pane == null ? cConversationFont.Named(cDockSettings.PromptFont) : cPaneThemes.PromptFontFor(hwnd);
            Select(lstFonts, font.BaseName);
            Select(lstPromptFonts, fontPrompt.BaseName);
            SyncSize(cboFontSize, font);
            SyncSize(cboPromptSize, fontPrompt);
            mblnLoading = false;
            btnSavePane.Enabled = pane != null;
            btnGlobal.Enabled   = pane != null && !cPaneThemes.FollowsGlobal(hwnd);
            ShowPreview();
        }

        private static void Select(ListBox alst, string astrName) {
            int i = alst.Items.IndexOf(astrName);
            alst.SelectedIndex = i >= 0 ? i : 0;
        }

        private void SaveForAll() {
            try {
                cDockSettings.SaveLook(Theme().Name, Style().Name, FontPick().Name, PromptFontPick().Name);
                cPaneThemes.RaiseChanged();
                lblSaved.Text = "Saved as the global look.";
            } catch (Exception ex) {
                lblSaved.Text = "Not saved: " + ex.Message;
            }
        }

        private void SaveForPane() {
            fAgentAbsorbedTerminal pane = ChosenPane();
            if (pane == null) return;
            string strProblem = cPaneThemes.SetLook(pane.AbsorbedHwnd, Theme().Name, Style().Name, FontPick().Name, PromptFontPick().Name);
            lblSaved.Text = strProblem == null ? "Saved for pane " + cboPane.SelectedIndex + "." : "Not saved: " + strProblem;
            btnGlobal.Enabled = strProblem == null;
        }

        private void UseGlobal() {
            fAgentAbsorbedTerminal pane = ChosenPane();
            if (pane == null) return;
            string strProblem = cPaneThemes.Clear(pane.AbsorbedHwnd);
            LoadLookOfChoice();
            lblSaved.Text = strProblem == null ? "Pane " + cboPane.SelectedIndex + " follows the global look." : "Not saved: " + strProblem;
        }

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            lstColors.Focus();
        }
    }
}
