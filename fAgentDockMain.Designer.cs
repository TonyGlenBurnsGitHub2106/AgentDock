namespace AgentDockStandalone {

    partial class fAgentDockMain {

        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.MenuStrip            msMenu;
        private System.Windows.Forms.StatusStrip          ssStatus;
        private System.Windows.Forms.ToolStripStatusLabel tslStatus;

        private System.Windows.Forms.ToolStripMenuItem  mnuFile;
        private System.Windows.Forms.ToolStripMenuItem  mnuFileExit;

        private System.Windows.Forms.ToolStripMenuItem  mnuAgents;
        private System.Windows.Forms.ToolStripMenuItem  mnuAgentsSpawn1;
        private System.Windows.Forms.ToolStripMenuItem  mnuAgentsSpawn2;
        private System.Windows.Forms.ToolStripMenuItem  mnuAgentsSpawn3;
        private System.Windows.Forms.ToolStripMenuItem  mnuAgentsSpawn4;
        private System.Windows.Forms.ToolStripMenuItem  mnuAgentsNewPane;
        private System.Windows.Forms.ToolStripMenuItem  mnuAgentsNewAdminPane;
        private System.Windows.Forms.ToolStripSeparator mnuAgentsSepA;
        private System.Windows.Forms.ToolStripMenuItem  mnuAgentsAbsorbAll;
        private System.Windows.Forms.ToolStripMenuItem  mnuAgentsNewUnparentedPane;
        private System.Windows.Forms.ToolStripMenuItem  mnuAgentsUnparentActive;
        private System.Windows.Forms.ToolStripMenuItem  mnuAgentsUnparentAll;
        private System.Windows.Forms.ToolStripSeparator mnuAgentsSepB;
        private System.Windows.Forms.ToolStripMenuItem  mnuAgentsCloseAndTerminate;

        private System.Windows.Forms.ToolStripMenuItem  mnuWindow;
        private System.Windows.Forms.ToolStripMenuItem  mnuWindowTileVertical;
        private System.Windows.Forms.ToolStripMenuItem  mnuWindowTileHorizontal;
        private System.Windows.Forms.ToolStripMenuItem  mnuWindowCascade;
        private System.Windows.Forms.ToolStripMenuItem  mnuWindowQuadrantLayout;

        private System.Windows.Forms.ToolStripMenuItem  mnuHelp;
        private System.Windows.Forms.ToolStripMenuItem  mnuHelpUsage;
        private System.Windows.Forms.ToolStripMenuItem  mnuHelpAbout;

        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent() {
            this.msMenu                       = new System.Windows.Forms.MenuStrip();
            this.ssStatus                     = new System.Windows.Forms.StatusStrip();
            this.tslStatus                    = new System.Windows.Forms.ToolStripStatusLabel();

            this.mnuFile                      = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuFileExit                  = new System.Windows.Forms.ToolStripMenuItem();

            this.mnuAgents                    = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAgentsSpawn1              = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAgentsSpawn2              = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAgentsSpawn3              = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAgentsSpawn4              = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAgentsNewPane             = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAgentsNewAdminPane        = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAgentsSepA                = new System.Windows.Forms.ToolStripSeparator();
            this.mnuAgentsAbsorbAll           = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAgentsNewUnparentedPane = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAgentsUnparentActive    = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAgentsUnparentAll         = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuAgentsSepB                = new System.Windows.Forms.ToolStripSeparator();
            this.mnuAgentsCloseAndTerminate   = new System.Windows.Forms.ToolStripMenuItem();

            this.mnuWindow                    = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuWindowTileVertical        = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuWindowTileHorizontal      = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuWindowCascade             = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuWindowQuadrantLayout      = new System.Windows.Forms.ToolStripMenuItem();

            this.mnuHelp                      = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuHelpUsage                 = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuHelpAbout                 = new System.Windows.Forms.ToolStripMenuItem();

            this.msMenu.SuspendLayout();
            this.ssStatus.SuspendLayout();
            this.SuspendLayout();

            //
            // msMenu
            //
            this.msMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.mnuFile,
                this.mnuAgents,
                this.mnuWindow,
                this.mnuHelp});
            this.msMenu.Location          = new System.Drawing.Point(0, 0);
            this.msMenu.Name              = "msMenu";
            this.msMenu.Size              = new System.Drawing.Size(1200, 24);
            this.msMenu.TabIndex          = 0;
            this.msMenu.MdiWindowListItem = this.mnuWindow;

            //
            // mnuFile
            //
            this.mnuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.mnuFileExit});
            this.mnuFile.Name = "mnuFile";
            this.mnuFile.Size = new System.Drawing.Size(38, 20);
            this.mnuFile.Text = "&File";

            this.mnuFileExit.Name         = "mnuFileExit";
            this.mnuFileExit.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.F4)));
            this.mnuFileExit.Size         = new System.Drawing.Size(180, 22);
            this.mnuFileExit.Text         = "E&xit";
            this.mnuFileExit.Click       += new System.EventHandler(this.mnuFileExit_Click);

            //
            // mnuAgents
            //
            this.mnuAgents.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.mnuAgentsSpawn1,
                this.mnuAgentsSpawn2,
                this.mnuAgentsSpawn3,
                this.mnuAgentsSpawn4,
                this.mnuAgentsNewPane,
                this.mnuAgentsNewAdminPane,
                this.mnuAgentsNewUnparentedPane,
                this.mnuAgentsSepA,
                this.mnuAgentsAbsorbAll,
                this.mnuAgentsUnparentActive,
                this.mnuAgentsUnparentAll,
                this.mnuAgentsSepB,
                this.mnuAgentsCloseAndTerminate});
            this.mnuAgents.Name = "mnuAgents";
            this.mnuAgents.Size = new System.Drawing.Size(58, 20);
            this.mnuAgents.Text = "&Agents";

            this.mnuAgentsSpawn1.Name         = "mnuAgentsSpawn1";
            this.mnuAgentsSpawn1.Size         = new System.Drawing.Size(260, 22);
            this.mnuAgentsSpawn1.Text         = "Spawn &1 Pane";
            this.mnuAgentsSpawn1.ToolTipText  = "Open one new Claude Code pane via cmd /k claude.";
            this.mnuAgentsSpawn1.Click       += new System.EventHandler(this.mnuAgentsSpawn1_Click);

            this.mnuAgentsSpawn2.Name         = "mnuAgentsSpawn2";
            this.mnuAgentsSpawn2.Size         = new System.Drawing.Size(260, 22);
            this.mnuAgentsSpawn2.Text         = "Spawn &2 Panes";
            this.mnuAgentsSpawn2.ToolTipText  = "Open two new Claude Code panes (side by side).";
            this.mnuAgentsSpawn2.Click       += new System.EventHandler(this.mnuAgentsSpawn2_Click);

            this.mnuAgentsSpawn3.Name         = "mnuAgentsSpawn3";
            this.mnuAgentsSpawn3.Size         = new System.Drawing.Size(260, 22);
            this.mnuAgentsSpawn3.Text         = "Spawn &3 Panes";
            this.mnuAgentsSpawn3.ToolTipText  = "Open three new Claude Code panes.";
            this.mnuAgentsSpawn3.Click       += new System.EventHandler(this.mnuAgentsSpawn3_Click);

            this.mnuAgentsSpawn4.Name         = "mnuAgentsSpawn4";
            this.mnuAgentsSpawn4.Size         = new System.Drawing.Size(260, 22);
            this.mnuAgentsSpawn4.Text         = "Spawn &4 Panes";
            this.mnuAgentsSpawn4.ToolTipText  = "Open four new Claude Code panes (quadrant default).";
            this.mnuAgentsSpawn4.Click       += new System.EventHandler(this.mnuAgentsSpawn4_Click);

            this.mnuAgentsNewPane.Name         = "mnuAgentsNewPane";
            this.mnuAgentsNewPane.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
            this.mnuAgentsNewPane.Size         = new System.Drawing.Size(260, 22);
            this.mnuAgentsNewPane.Text         = "&Spawn Additional Pane";
            this.mnuAgentsNewPane.ToolTipText  = "Add one more pane to the dock (current count + 1).";
            this.mnuAgentsNewPane.Click       += new System.EventHandler(this.mnuAgentsNewPane_Click);

            this.mnuAgentsNewAdminPane.Name         = "mnuAgentsNewAdminPane";
            this.mnuAgentsNewAdminPane.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) | System.Windows.Forms.Keys.N)));
            this.mnuAgentsNewAdminPane.Size         = new System.Drawing.Size(260, 22);
            this.mnuAgentsNewAdminPane.Text         = "Spawn Additional &Admin Pane";
            this.mnuAgentsNewAdminPane.ToolTipText  = "Add one elevated pane (UAC prompts). Title shows [ADMIN].";
            this.mnuAgentsNewAdminPane.Click       += new System.EventHandler(this.mnuAgentsNewAdminPane_Click);

            this.mnuAgentsSepA.Name = "mnuAgentsSepA";
            this.mnuAgentsSepA.Size = new System.Drawing.Size(257, 6);

            this.mnuAgentsAbsorbAll.Name         = "mnuAgentsAbsorbAll";
            this.mnuAgentsAbsorbAll.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) | System.Windows.Forms.Keys.A)));
            this.mnuAgentsAbsorbAll.Size         = new System.Drawing.Size(260, 22);
            this.mnuAgentsAbsorbAll.Text         = "&Absorb All Running Terminals";
            this.mnuAgentsAbsorbAll.ToolTipText  = "Pull every visible Windows Terminal window into the dock as an MDI child.";
            this.mnuAgentsAbsorbAll.Click       += new System.EventHandler(this.mnuAgentsAbsorbAll_Click);

            this.mnuAgentsNewUnparentedPane.Name         = "mnuAgentsNewUnparentedPane";
            this.mnuAgentsNewUnparentedPane.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.U)));
            this.mnuAgentsNewUnparentedPane.Size         = new System.Drawing.Size(260, 22);
            this.mnuAgentsNewUnparentedPane.Text         = "Spawn Un&parented Pane";
            this.mnuAgentsNewUnparentedPane.ToolTipText  = "Start one more Claude terminal the same way, but leave it on the desktop instead of in the dock.";
            this.mnuAgentsNewUnparentedPane.Click       += new System.EventHandler(this.mnuAgentsNewUnparentedPane_Click);
            //
            this.mnuAgentsUnparentActive.Name                     = "mnuAgentsUnparentActive";
            this.mnuAgentsUnparentActive.ShortcutKeyDisplayString = "Ctrl+Shift+U";
            this.mnuAgentsUnparentActive.Size                     = new System.Drawing.Size(260, 22);
            this.mnuAgentsUnparentActive.Text                     = "Unparent Acti&ve Pane";
            this.mnuAgentsUnparentActive.ToolTipText              = "Release the pane you last clicked to the desktop, alive. The other panes stay in the dock.";
            this.mnuAgentsUnparentActive.Click                   += new System.EventHandler(this.mnuAgentsUnparentActive_Click);
            //
            this.mnuAgentsUnparentAll.Name        = "mnuAgentsUnparentAll";
            this.mnuAgentsUnparentAll.Size        = new System.Drawing.Size(260, 22);
            this.mnuAgentsUnparentAll.Text        = "&Unparent All";
            this.mnuAgentsUnparentAll.ToolTipText = "Release every pane back to the desktop alive (spawned and absorbed alike), then close the dock.";
            this.mnuAgentsUnparentAll.Click      += new System.EventHandler(this.mnuAgentsUnparentAll_Click);

            this.mnuAgentsSepB.Name = "mnuAgentsSepB";
            this.mnuAgentsSepB.Size = new System.Drawing.Size(257, 6);

            this.mnuAgentsCloseAndTerminate.Name        = "mnuAgentsCloseAndTerminate";
            this.mnuAgentsCloseAndTerminate.Size        = new System.Drawing.Size(260, 22);
            this.mnuAgentsCloseAndTerminate.Text        = "Close Dock && &Terminate All";
            this.mnuAgentsCloseAndTerminate.ToolTipText = "Close the dock AND end every Claude session \u2014 absorbed terminals are killed, not released.";
            this.mnuAgentsCloseAndTerminate.Click      += new System.EventHandler(this.mnuAgentsCloseAndTerminate_Click);

            //
            // mnuWindow
            //
            this.mnuWindow.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.mnuWindowTileVertical,
                this.mnuWindowTileHorizontal,
                this.mnuWindowCascade,
                this.mnuWindowQuadrantLayout});
            this.mnuWindow.Name = "mnuWindow";
            this.mnuWindow.Size = new System.Drawing.Size(63, 20);
            this.mnuWindow.Text = "&Window";

            this.mnuWindowTileVertical.Name   = "mnuWindowTileVertical";
            this.mnuWindowTileVertical.Size   = new System.Drawing.Size(180, 22);
            this.mnuWindowTileVertical.Text   = "Tile &Vertical";
            this.mnuWindowTileVertical.Click += new System.EventHandler(this.mnuWindowTileVertical_Click);

            this.mnuWindowTileHorizontal.Name   = "mnuWindowTileHorizontal";
            this.mnuWindowTileHorizontal.Size   = new System.Drawing.Size(180, 22);
            this.mnuWindowTileHorizontal.Text   = "Tile &Horizontal";
            this.mnuWindowTileHorizontal.Click += new System.EventHandler(this.mnuWindowTileHorizontal_Click);

            this.mnuWindowCascade.Name   = "mnuWindowCascade";
            this.mnuWindowCascade.Size   = new System.Drawing.Size(180, 22);
            this.mnuWindowCascade.Text   = "&Cascade";
            this.mnuWindowCascade.Click += new System.EventHandler(this.mnuWindowCascade_Click);

            this.mnuWindowQuadrantLayout.Name        = "mnuWindowQuadrantLayout";
            this.mnuWindowQuadrantLayout.Size        = new System.Drawing.Size(180, 22);
            this.mnuWindowQuadrantLayout.Text        = "&Quadrant Layout";
            this.mnuWindowQuadrantLayout.ToolTipText = "Reset every pane to a clean 2x2 grid.";
            this.mnuWindowQuadrantLayout.Click      += new System.EventHandler(this.mnuWindowQuadrantLayout_Click);

            //
            // mnuHelp
            //
            this.mnuHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.mnuHelpUsage,
                this.mnuHelpAbout});
            this.mnuHelp.Name = "mnuHelp";
            this.mnuHelp.Size = new System.Drawing.Size(44, 20);
            this.mnuHelp.Text = "&Help";

            this.mnuHelpUsage.Name         = "mnuHelpUsage";
            this.mnuHelpUsage.ShortcutKeys = System.Windows.Forms.Keys.F1;
            this.mnuHelpUsage.Size         = new System.Drawing.Size(200, 22);
            this.mnuHelpUsage.Text         = "&Usage and Menu Reference";
            this.mnuHelpUsage.Click       += new System.EventHandler(this.mnuHelpUsage_Click);

            this.mnuHelpAbout.Name   = "mnuHelpAbout";
            this.mnuHelpAbout.Size   = new System.Drawing.Size(200, 22);
            this.mnuHelpAbout.Text   = "&About AgentDockStandalone";
            this.mnuHelpAbout.Click += new System.EventHandler(this.mnuHelpAbout_Click);

            //
            // ssStatus
            //
            this.ssStatus.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.tslStatus});
            this.ssStatus.Location = new System.Drawing.Point(0, 768);
            this.ssStatus.Name     = "ssStatus";
            this.ssStatus.Size     = new System.Drawing.Size(1200, 22);
            this.ssStatus.TabIndex = 1;

            this.tslStatus.Name = "tslStatus";
            this.tslStatus.Size = new System.Drawing.Size(200, 17);
            this.tslStatus.Text = "0 panes - use Agents menu to spawn or absorb";

            //
            // fAgentDockMain
            //
            this.AutoScaleDimensions  = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode        = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize           = new System.Drawing.Size(1200, 720);
            this.Controls.Add(this.ssStatus);
            this.Controls.Add(this.msMenu);
            this.Font                 = new System.Drawing.Font("Segoe UI", 9F);
            this.IsMdiContainer       = true;
            this.MainMenuStrip        = this.msMenu;
            this.Name                 = "fAgentDockMain";
            this.StartPosition        = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text                 = "Agent Dock";

            this.msMenu.ResumeLayout(false);
            this.msMenu.PerformLayout();
            this.ssStatus.ResumeLayout(false);
            this.ssStatus.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
