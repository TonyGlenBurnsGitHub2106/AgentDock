using System;
using System.Drawing;
using System.Windows.Forms;

namespace AgentDockStandalone {

    // Popup Answer (Story 6828): the agent's most recent answer in a window of its own, in a plain
    // Windows text box you can click into, select, edit and copy, like the compose box. Copy puts
    // the whole text (with any edits) on the clipboard. Nothing typed here goes anywhere else.
    // Closes with Close, the X, or Escape. Escape asks first once the text has been changed.
    internal sealed class fAnswerPopup : Form {

        private readonly TextBox txtAnswer = new TextBox();
        private readonly Button  btnCopy   = new Button();
        private readonly Button  btnClose  = new Button();
        private readonly Label   lblStatus = new Label();
        private bool mblnEdited;

        public fAnswerPopup(string astrTitle, string astrAnswer) : this("Answer", astrTitle, astrAnswer) { }

        // astrKind heads the window: "Answer", or "Your prompt" for a prompt opened from Run
        // conversation (Story 6845).
        public fAnswerPopup(string astrKind, string astrTitle, string astrAnswer) {
            this.Text          = astrKind + " - " + astrTitle;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size          = new Size(900, 650);
            this.MinimumSize   = new Size(420, 260);
            this.ShowInTaskbar = false;
            this.KeyPreview    = true;
            this.Icon          = null;
            this.ShowIcon      = false;

            txtAnswer.Multiline     = true;
            txtAnswer.AcceptsReturn = true;
            txtAnswer.AcceptsTab    = true;
            txtAnswer.WordWrap      = true;
            txtAnswer.ScrollBars    = ScrollBars.Vertical;
            txtAnswer.Dock          = DockStyle.Fill;
            txtAnswer.Font          = new Font("Consolas", 10F);
            txtAnswer.MaxLength     = 0;
            txtAnswer.Text          = (astrAnswer ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
            txtAnswer.TextChanged  += delegate { mblnEdited = true; };
            txtAnswer.KeyDown      += delegate(object s, KeyEventArgs e) {
                // A multi-line TextBox ignores Ctrl+A on its own.
                if (e.KeyCode == Keys.A && e.Control && !e.Shift && !e.Alt) {
                    e.SuppressKeyPress = true;
                    txtAnswer.SelectAll();
                }
            };

            Panel pnlButtons = new Panel();
            pnlButtons.Dock    = DockStyle.Bottom;
            pnlButtons.Height  = 42;
            pnlButtons.Padding = new Padding(8, 6, 8, 6);

            btnClose.Text   = "Close";
            btnClose.Width  = 90;
            btnClose.Dock   = DockStyle.Right;
            btnClose.Click += delegate { this.Close(); };

            Panel pnlGap = new Panel();
            pnlGap.Width = 8;
            pnlGap.Dock  = DockStyle.Right;

            btnCopy.Text   = "Copy";
            btnCopy.Width  = 90;
            btnCopy.Dock   = DockStyle.Right;
            btnCopy.Click += delegate { CopyAll(); };

            lblStatus.Dock      = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.ForeColor = SystemColors.GrayText;

            pnlButtons.Controls.Add(lblStatus);
            pnlButtons.Controls.Add(btnCopy);
            pnlButtons.Controls.Add(pnlGap);
            pnlButtons.Controls.Add(btnClose);

            this.Controls.Add(txtAnswer);
            this.Controls.Add(pnlButtons);
            this.CancelButton = null;   // Escape is handled below, so edits are not lost to it
        }

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            mblnEdited = false;
            txtAnswer.SelectionStart  = 0;
            txtAnswer.SelectionLength = 0;
            txtAnswer.Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            if (e.KeyCode == Keys.Escape) {
                e.Handled = true;
                if (!mblnEdited || MessageBox.Show(this, "Close the answer? Your changes to it are not kept anywhere.",
                        "Agent Dock", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    this.Close();
                return;
            }
            base.OnKeyDown(e);
        }

        private void CopyAll() {
            try {
                if (txtAnswer.TextLength == 0) return;
                Clipboard.SetText(txtAnswer.Text);
                lblStatus.Text = "Copied.";
            } catch (Exception ex) {
                lblStatus.Text = "Not copied: " + ex.Message;
            }
            txtAnswer.Focus();
        }
    }
}
