using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgentDockStandalone {

    // The Keys window (Story 6847, Tony: "The switch commands, like resume and skills and such, do
    // not pull up the options and such or allow selection, when typed in the compose box. I
    // suspect even questions you ask with menus of answers don't work either.").
    //
    // A menu an agent shows (/resume, /skills, /model, a question with choices, a permission
    // prompt) lives in the terminal and wants arrow keys, Enter and Esc. The compose box sends
    // whole prompts, and in Conversation Panes the terminal is hidden. This window shows the
    // pane's screen as it is now, and every key pressed in it goes to that pane: arrows, Enter,
    // Esc, Tab, Space, Backspace, letters (a menu's search box), Ctrl+letter. Buttons do the same
    // for the mouse.
    //
    // It opens by itself when a pane shows a menu (cComposeBox watches the screen for a menu's
    // footer, "Esc to cancel" and the like) and, opened that way, closes by itself once the pick
    // is made. The Keys button opens it any time.
    internal sealed class fPaneKeys : Form {

        private static readonly Dictionary<IntPtr, fPaneKeys> mdicOpen = new Dictionary<IntPtr, fPaneKeys>();

        private readonly fAgentAbsorbedTerminal mfrmPane;
        private readonly IntPtr mhwnd;
        private readonly cScreenView vwScreen = new cScreenView();
        private readonly Label lblHelp = new Label();
        private readonly Label lblStatus = new Label();
        private readonly CheckBox chkAuto = new CheckBox();
        private readonly Timer tmrRead = new Timer();
        private readonly bool mblnQuiet;
        private bool mblnAuto;           // opened by itself: closes by itself when the menu is gone
        private int mintGoneTicks;
        private string mstrLast = "";

        // Opens (or brings forward) the Keys window of apane. ablnAuto: opened because a menu
        // showed. ablnActivate false shows it without taking the keyboard from what you are doing.
        public static void Open(Form afrmOwner, fAgentAbsorbedTerminal apane, bool ablnAuto, bool ablnActivate) {
            if (apane == null || apane.IsDisposed) return;
            fPaneKeys frm;
            if (mdicOpen.TryGetValue(apane.AbsorbedHwnd, out frm) && !frm.IsDisposed) {
                if (!ablnAuto) frm.mblnAuto = false;     // asked for: stays until closed
                if (ablnActivate) frm.Activate();
                return;
            }
            frm = new fPaneKeys(apane, ablnAuto, !ablnActivate);
            mdicOpen[apane.AbsorbedHwnd] = frm;
            if (afrmOwner != null) {
                Rectangle rc = afrmOwner.Bounds;
                frm.StartPosition = FormStartPosition.Manual;
                frm.Location = new Point(rc.Left + Math.Max(0, (rc.Width - frm.Width) / 2),
                                         rc.Top + Math.Max(0, (rc.Height - frm.Height) / 3));
            }
            frm.Show(afrmOwner);
        }

        public static bool IsOpenFor(fAgentAbsorbedTerminal apane) {
            fPaneKeys frm;
            return apane != null && mdicOpen.TryGetValue(apane.AbsorbedHwnd, out frm) && !frm.IsDisposed;
        }

        // True when the screen's last lines are a menu's footer: Claude Code writes "Enter to
        // select", "Esc to cancel", "Esc to close", "to navigate" under every menu it shows.
        // "esc to interrupt" is the working line, not a menu.
        public static bool MenuShowing(string[] astrLines) {
            if (astrLines == null) return false;
            int intSeen = 0;
            for (int i = astrLines.Length - 1; i >= 0 && intSeen < 8; i--) {
                string s = (astrLines[i] ?? "").Trim().ToLowerInvariant();
                if (s.Length == 0) continue;
                intSeen++;
                if (s.IndexOf("esc to interrupt") >= 0) continue;
                if (s.IndexOf("enter to select") >= 0 || s.IndexOf("enter to confirm") >= 0 || s.IndexOf("enter to save") >= 0
                    || s.IndexOf("to navigate") >= 0 || s.IndexOf("esc to cancel") >= 0 || s.IndexOf("esc to close") >= 0
                    || s.IndexOf("esc to exit") >= 0 || s.IndexOf("esc to go back") >= 0 || s.IndexOf("esc to dismiss") >= 0)
                    return true;
            }
            return false;
        }

        private fPaneKeys(fAgentAbsorbedTerminal apane, bool ablnAuto, bool ablnQuiet) {
            mfrmPane  = apane;
            mhwnd     = apane.AbsorbedHwnd;
            mblnAuto  = ablnAuto;
            mblnQuiet = ablnQuiet;
            this.Text          = "Keys - " + apane.Text;
            this.Font          = new Font("Segoe UI", 9F);
            this.Size          = new Size(980, 560);
            this.MinimumSize   = new Size(520, 300);
            this.ShowInTaskbar = false;
            this.MinimizeBox   = false;
            this.KeyPreview    = true;
            this.TopMost       = true;

            lblHelp.Dock      = DockStyle.Top;
            lblHelp.AutoSize  = false;
            lblHelp.Height    = this.Font.Height * 2 + 10;
            lblHelp.Padding   = new Padding(8, 6, 8, 0);
            lblHelp.Text      = "Keys you press here go to " + apane.Text + ": the arrows, Enter, Esc, Tab, Space, Backspace and letters "
                              + "(a menu's search box). Use them to pick from a menu or answer a question. Close this window when you are done.";

            vwScreen.Dock = DockStyle.Fill;

            FlowLayoutPanel pnlKeys = new FlowLayoutPanel();
            pnlKeys.Dock          = DockStyle.Bottom;
            pnlKeys.WrapContents  = true;
            pnlKeys.AutoSize      = true;
            pnlKeys.AutoSizeMode  = AutoSizeMode.GrowAndShrink;
            pnlKeys.Padding       = new Padding(6, 4, 6, 2);
            AddKey(pnlKeys, "Up", Keys.Up);
            AddKey(pnlKeys, "Down", Keys.Down);
            AddKey(pnlKeys, "Left", Keys.Left);
            AddKey(pnlKeys, "Right", Keys.Right);
            AddKey(pnlKeys, "Enter", Keys.Return);
            AddKey(pnlKeys, "Esc", Keys.Escape);
            AddKey(pnlKeys, "Tab", Keys.Tab);
            AddKey(pnlKeys, "Space", Keys.Space);
            AddKey(pnlKeys, "1", Keys.D1);
            AddKey(pnlKeys, "2", Keys.D2);
            AddKey(pnlKeys, "3", Keys.D3);
            AddKey(pnlKeys, "4", Keys.D4);
            cNoFocusButton btnClose = new cNoFocusButton();
            btnClose.Text = "Close";
            SizeButton(btnClose);
            btnClose.Margin = new Padding(16, 3, 3, 3);
            btnClose.Click += delegate { this.Close(); };
            pnlKeys.Controls.Add(btnClose);

            chkAuto.Text     = "Open by itself when a pane shows a menu";
            chkAuto.AutoSize = true;
            chkAuto.Checked  = cDockSettings.KeysAutoOpen;
            chkAuto.Margin   = new Padding(16, 8, 3, 3);
            chkAuto.TabStop  = false;
            chkAuto.CheckedChanged += delegate {
                try { cDockSettings.SaveKeysAutoOpen(chkAuto.Checked); lblStatus.Text = chkAuto.Checked ? "It will open by itself." : "It opens only from the Keys button."; }
                catch (Exception ex) { lblStatus.Text = "Not remembered: " + ex.Message; }
                vwScreen.Focus();
            };
            pnlKeys.Controls.Add(chkAuto);

            lblStatus.Dock      = DockStyle.Bottom;
            lblStatus.Height    = this.Font.Height + 8;
            lblStatus.Padding   = new Padding(8, 2, 8, 2);
            lblStatus.ForeColor = SystemColors.GrayText;

            this.Controls.Add(vwScreen);
            this.Controls.Add(lblHelp);
            this.Controls.Add(pnlKeys);
            this.Controls.Add(lblStatus);

            tmrRead.Interval = 300;
            tmrRead.Tick += delegate { ReadScreen(); };
        }

        protected override bool ShowWithoutActivation { get { return mblnQuiet; } }

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            ReadScreen();
            tmrRead.Start();
            vwScreen.Focus();
        }

        protected override void OnFormClosed(FormClosedEventArgs e) {
            tmrRead.Stop();
            tmrRead.Dispose();
            fPaneKeys frm;
            if (mdicOpen.TryGetValue(mhwnd, out frm) && frm == this) mdicOpen.Remove(mhwnd);
            base.OnFormClosed(e);
        }

        private void AddKey(FlowLayoutPanel apnl, string astrText, Keys akey) {
            cNoFocusButton b = new cNoFocusButton();
            b.Text = astrText;
            SizeButton(b);
            b.Click += delegate { SendKey(akey); vwScreen.Focus(); };
            apnl.Controls.Add(b);
        }

        // Sized from the text, so nothing is cut off at any display scaling.
        private void SizeButton(Button ab) {
            ab.Font   = this.Font;
            ab.Height = this.Font.Height + 14;
            ab.Width  = Math.Max(ab.Height + 8, TextRenderer.MeasureText(ab.Text, this.Font).Width + 22);
            ab.Margin = new Padding(3);
        }

        // Every key the window gets goes to the pane, before any control can use it.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            Keys k = keyData & Keys.KeyCode;
            bool blnCtrl = (keyData & Keys.Control) == Keys.Control;
            bool blnAlt  = (keyData & Keys.Alt) == Keys.Alt;
            if (blnAlt) return base.ProcessCmdKey(ref msg, keyData);        // Alt+F4 and the like stay Windows'
            if (blnCtrl && k >= Keys.A && k <= Keys.Z) {
                Send(((char)(k - Keys.A + 1)).ToString(), "Ctrl+" + k);
                return true;
            }
            if (!blnCtrl && cConsoleInput.KeyCode(keyData) != null) {
                SendKey(keyData);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Letters, digits, Space and the rest of the typed characters.
        protected override void OnKeyPress(KeyPressEventArgs e) {
            base.OnKeyPress(e);
            if (e.KeyChar >= ' ' && e.KeyChar != '\u007f') {
                Send(e.KeyChar.ToString(), e.KeyChar == ' ' ? "Space" : e.KeyChar.ToString());
                e.Handled = true;
            }
        }

        private void SendKey(Keys akey) {
            string strCode = cConsoleInput.KeyCode(akey);
            if (strCode == null) {
                if ((akey & Keys.KeyCode) == Keys.Space) strCode = " ";
                else if ((akey & Keys.KeyCode) >= Keys.D0 && (akey & Keys.KeyCode) <= Keys.D9) strCode = ((char)('0' + (akey - Keys.D0))).ToString();
            }
            if (strCode == null) return;
            Send(strCode, KeyName(akey));
        }

        private static string KeyName(Keys akey) {
            Keys k = akey & Keys.KeyCode;
            if (k == Keys.Return) return "Enter";
            if (k == Keys.Escape) return "Esc";
            if (k == Keys.Back) return "Backspace";
            if (k >= Keys.D0 && k <= Keys.D9) return ((char)('0' + (k - Keys.D0))).ToString();
            return ((akey & Keys.Shift) == Keys.Shift ? "Shift+" : "") + k;
        }

        private void Send(string astrKeys, string astrName) {
            if (mfrmPane.IsDisposed) { lblStatus.Text = "That pane has closed."; return; }
            string strProblem = cConsoleInput.PressKeys(mhwnd, astrKeys);
            lblStatus.Text = strProblem == null ? "Sent " + astrName + "." : "Not sent: " + strProblem;
            mblnAuto = mblnAuto && strProblem == null;
            mintGoneTicks = 0;
            tmrRead.Stop();
            tmrRead.Interval = 120;      // show the answer to the key at once
            tmrRead.Start();
        }

        private void ReadScreen() {
            if (tmrRead.Interval != 300) { tmrRead.Interval = 300; }
            if (mfrmPane.IsDisposed) { tmrRead.Stop(); lblStatus.Text = "That pane has closed."; return; }
            string[] astrLines = cConsoleInput.ReadScreen(mhwnd);
            if (astrLines == null) { lblStatus.Text = "Could not read the pane's screen."; return; }
            string strNow = String.Join("\n", astrLines);
            if (strNow != mstrLast) {
                mstrLast = strNow;
                vwScreen.SetLines(astrLines);
            }
            // Opened by itself for a menu: once the menu is gone (the pick made), close.
            if (mblnAuto) {
                if (MenuShowing(astrLines)) mintGoneTicks = 0;
                else if (++mintGoneTicks >= 4) this.Close();
            }
        }

        // A button that never takes the keyboard, so Enter and Space always go to the pane.
        private sealed class cNoFocusButton : Button {
            public cNoFocusButton() {
                SetStyle(ControlStyles.Selectable, false);
                this.TabStop = false;
            }
        }

        // The pane's screen, drawn line by line in a terminal font. It holds the keyboard focus
        // so the window gets every key.
        private sealed class cScreenView : Control {
            private string[] mastrLines = new string[0];

            public cScreenView() {
                SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                this.BackColor = Color.FromArgb(12, 12, 12);
                this.ForeColor = Color.FromArgb(204, 204, 204);
                this.Font      = new Font("Cascadia Mono", 9.75F);
                if (this.Font.Name != "Cascadia Mono") this.Font = new Font("Consolas", 10F);
            }

            public void SetLines(string[] astrLines) {
                mastrLines = astrLines ?? new string[0];
                Invalidate();
            }

            protected override bool IsInputKey(Keys keyData) { return true; }

            protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); }

            protected override void OnPaint(PaintEventArgs e) {
                e.Graphics.Clear(this.BackColor);
                int intLine = this.Font.Height;
                int intFit = Math.Max(1, (this.ClientSize.Height - 8) / intLine);
                // The bottom of the screen is where a menu is: show the last lines that fit.
                int intLast = mastrLines.Length - 1;
                while (intLast > 0 && mastrLines[intLast].Trim().Length == 0) intLast--;
                int intFirst = Math.Max(0, intLast - intFit + 1);
                int y = 4;
                for (int i = intFirst; i <= intLast; i++) {
                    TextRenderer.DrawText(e.Graphics, mastrLines[i], this.Font, new Point(6, y), this.ForeColor,
                        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    y += intLine;
                }
                if (this.Focused) {
                    using (Pen pen = new Pen(Color.FromArgb(70, 110, 200))) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }

            protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
            protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        }
    }
}
