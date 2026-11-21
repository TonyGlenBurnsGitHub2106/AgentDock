using System;
using System.Drawing;
using System.Windows.Forms;

namespace AgentDockStandalone {

    // Plain modal viewer for the help markdown. Markdown is shown as raw text
    // in a monospace RichTextBox -- no rendering. Good enough for our one-topic
    // help system; avoids any HTML / markdown library dependency.
    public class fHelpViewer : Form {

        public fHelpViewer(string astrTitle, string astrContent) {
            this.Text          = astrTitle;
            this.Size          = new Size(960, 720);
            this.MinimumSize   = new Size(480, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.ShowIcon      = false;

            RichTextBox txt = new RichTextBox();
            txt.Dock         = DockStyle.Fill;
            txt.ReadOnly     = true;
            txt.WordWrap     = true;
            txt.Font         = new Font("Consolas", 10F);
            txt.BackColor    = Color.White;
            txt.BorderStyle  = BorderStyle.None;
            txt.DetectUrls   = true;
            txt.Text         = astrContent ?? "";

            Panel pnl = new Panel();
            pnl.Dock    = DockStyle.Bottom;
            pnl.Height  = 40;
            pnl.Padding = new Padding(8);

            Button btnClose = new Button();
            btnClose.Text         = "Close";
            btnClose.Width        = 96;
            btnClose.Height       = 24;
            btnClose.Anchor       = AnchorStyles.Right | AnchorStyles.Top;
            btnClose.DialogResult = DialogResult.OK;
            btnClose.Left         = pnl.ClientSize.Width - btnClose.Width - 8;
            btnClose.Top          = 6;
            pnl.Resize += delegate {
                btnClose.Left = pnl.ClientSize.Width - btnClose.Width - 8;
            };
            pnl.Controls.Add(btnClose);

            this.Controls.Add(txt);
            this.Controls.Add(pnl);
            this.AcceptButton = btnClose;
            this.CancelButton = btnClose;
        }
    }
}
