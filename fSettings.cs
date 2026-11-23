using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AgentDockStandalone {

    // Settings -> Agent Dock Settings (Story 6817): the dock's settings in plain fields, so
    // nobody has to open config\pane.bat or config\agentdock.ini by hand. OK saves both files;
    // Cancel or Escape changes nothing. Built in code (no designer file), like the reorder dialog.
    internal sealed class fSettings : Form {

        private readonly TextBox  txtCommand  = new TextBox();
        private readonly TextBox  txtFolder   = new TextBox();
        private readonly Button   btnBrowse   = new Button();
        private readonly CheckBox chkHideBars = new CheckBox();
        private readonly CheckBox chkAdmin    = new CheckBox();
        private readonly Button   btnOK       = new Button();
        private readonly Button   btnCancel   = new Button();

        private readonly string mstrOverrideBat;

        // astrActiveBat: the batch file new panes actually run. When it is not config\pane.bat
        // (the dock was started with --bat), the dialog says so, since the pane fields then
        // only take effect without --bat.
        public fSettings(string astrActiveBat) {
            string strConfigBat = cDockSettings.PaneBatPath;
            mstrOverrideBat = (strConfigBat != null && !String.IsNullOrEmpty(astrActiveBat)
                && !String.Equals(Path.GetFullPath(astrActiveBat), Path.GetFullPath(strConfigBat),
                                  StringComparison.OrdinalIgnoreCase)) ? astrActiveBat : null;
            BuildLayout();
            LoadValues();
        }

        private void BuildLayout() {
            this.Text            = "Agent Dock Settings";
            this.Font            = new Font("Segoe UI", 9F);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.ShowInTaskbar   = false;
            this.StartPosition   = FormStartPosition.CenterParent;
            this.AutoScaleMode   = AutoScaleMode.Font;
            this.ClientSize      = new Size(520, 356);

            int intLeft = 16, intWidth = 488, y = 16;

            y = AddCaption("New panes", intLeft, y, true);
            y = AddCaption("Agent command (what each new pane runs, for example copilot or claude)", intLeft, y, false);
            txtCommand.SetBounds(intLeft, y, intWidth, 23);
            this.Controls.Add(txtCommand);
            y += 34;

            y = AddCaption("Start folder (where each new pane starts)", intLeft, y, false);
            txtFolder.SetBounds(intLeft, y, intWidth - 92, 23);
            btnBrowse.Text = "Browse...";
            btnBrowse.SetBounds(intLeft + intWidth - 86, y - 1, 86, 25);
            btnBrowse.Click += delegate { BrowseFolder(); };
            this.Controls.Add(txtFolder);
            this.Controls.Add(btnBrowse);
            y += 38;

            y = AddCaption("Panes", intLeft, y, true);
            chkHideBars.Text = "Hide the title bar on each pane (one more row for the terminal)";
            chkHideBars.SetBounds(intLeft, y, intWidth, 24);
            this.Controls.Add(chkHideBars);
            y += 28;

            chkAdmin.Text = "Start the dock as administrator (Windows asks). Admin panes then open with no restart. Panes are never admin unless you ask for one.";
            chkAdmin.SetBounds(intLeft, y, intWidth, 36);
            chkAdmin.CheckAlign = ContentAlignment.TopLeft;
            chkAdmin.TextAlign  = ContentAlignment.TopLeft;
            this.Controls.Add(chkAdmin);
            y += 46;

            Label lblNote = new Label();
            lblNote.ForeColor = SystemColors.GrayText;
            lblNote.SetBounds(intLeft, y, intWidth, 36);
            lblNote.Text = mstrOverrideBat != null
                ? "This dock was started with --bat " + Path.GetFileName(mstrOverrideBat)
                  + ", so new panes run that file. The pane fields above apply when it starts without --bat."
                : "Saved in the config folder (Settings, Open Config Folder). New panes use the changes. The title bar change shows at once.";
            this.Controls.Add(lblNote);

            btnOK.Text     = "OK";
            btnCancel.Text = "Cancel";
            btnOK.SetBounds(this.ClientSize.Width - 16 - 75 - 8 - 75, this.ClientSize.Height - 16 - 27, 75, 27);
            btnCancel.SetBounds(this.ClientSize.Width - 16 - 75, this.ClientSize.Height - 16 - 27, 75, 27);
            btnOK.Click += delegate { SaveAndClose(); };
            btnCancel.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private int AddCaption(string astrText, int x, int y, bool ablnHeading) {
            Label lbl = new Label();
            lbl.Text     = astrText;
            lbl.AutoSize = true;
            if (ablnHeading) lbl.Font = new Font(this.Font, FontStyle.Bold);
            lbl.Location = new Point(x, y);
            this.Controls.Add(lbl);
            return y + (ablnHeading ? 24 : 20);
        }

        private void LoadValues() {
            string strFolder, strCommand;
            bool   blnPlain;
            cDockSettings.ReadPaneBat(out strFolder, out strCommand, out blnPlain);
            txtFolder.Text      = strFolder;
            txtCommand.Text     = strCommand;
            chkHideBars.Checked = cDockSettings.HidePaneTitleBars;
            chkAdmin.Checked    = cDockSettings.StartAsAdministrator;
            mblnPlainBat        = blnPlain;
        }

        private bool mblnPlainBat;

        private void BrowseFolder() {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog()) {
                dlg.Description = "Where should each new pane start?";
                if (Directory.Exists(txtFolder.Text.Trim())) dlg.SelectedPath = txtFolder.Text.Trim();
                if (dlg.ShowDialog(this) == DialogResult.OK) txtFolder.Text = dlg.SelectedPath;
            }
        }

        private void SaveAndClose() {
            string strCommand = txtCommand.Text.Trim();
            string strFolder  = txtFolder.Text.Trim().Trim('"');
            if (strCommand.Length == 0) {
                MessageBox.Show(this, "Enter the agent command, for example copilot.",
                    "Agent Dock Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCommand.Focus();
                return;
            }
            if (!Directory.Exists(strFolder)) {
                MessageBox.Show(this, "The start folder was not found:\r\n\r\n" + strFolder,
                    "Agent Dock Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtFolder.Focus();
                return;
            }
            if (!mblnPlainBat) {
                DialogResult dr = MessageBox.Show(this,
                    "pane.bat holds more than a start folder and one command. Saving replaces it "
                    + "with just these two.\r\n\r\nReplace pane.bat?",
                    "Agent Dock Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (dr != DialogResult.Yes) return;
            }
            try {
                cDockSettings.WritePaneBat(strFolder, strCommand);
                cDockSettings.Save(chkHideBars.Checked, chkAdmin.Checked);
            } catch (Exception ex) {
                MessageBox.Show(this, "Could not save the settings:\r\n\r\n" + ex.Message,
                    "Agent Dock Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
