using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgentDockStandalone {

    // The pick list of slash commands over a compose box (Story 6851). It shows what the pane's
    // agent shows under its own input line (cSlashScreen): its rows, the one it has picked, its
    // "N more", and, once a command is typed, what the command takes (its argument hint). It
    // never takes the keyboard: the box keeps it and steers the agent with Up, Down, Tab, Enter
    // and Escape. A click on a row picks it.
    internal sealed class fSlashList : Form {

        private const int CNST_ROWS_MAX = 12;
        private readonly ListBox lst = new ListBox();
        private List<cSlashCommand> mlstRows = new List<cSlashCommand>();
        private string mstrHint = "";
        private string mstrMore = "";
        private Font mfntName;

        public event Action<cSlashCommand> Picked;

        public fSlashList() {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar   = false;
            this.StartPosition   = FormStartPosition.Manual;
            this.BackColor       = SystemColors.ControlDark;   // the 1 px edge
            this.Padding         = new Padding(1);
            lst.Dock           = DockStyle.Fill;
            lst.BorderStyle    = BorderStyle.None;
            lst.DrawMode       = DrawMode.OwnerDrawFixed;
            lst.IntegralHeight = false;
            lst.Font           = new Font("Segoe UI", 9.5F);
            mfntName           = new Font(lst.Font, FontStyle.Bold);
            lst.ItemHeight     = lst.Font.Height + 6;
            lst.DrawItem      += new DrawItemEventHandler(List_DrawItem);
            lst.MouseClick    += delegate(object s, MouseEventArgs e) {
                cSlashCommand c = RowAt(lst.IndexFromPoint(e.Location));
                if (c != null && Picked != null) Picked(c);
            };
            this.Controls.Add(lst);
        }

        // Never activated: the compose box keeps the keyboard.
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080;   // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                return cp;
            }
        }

        // Items in the list: the hint first (when there is one), the rows, then "N more".
        private int RowOffset { get { return mstrHint.Length > 0 ? 1 : 0; } }

        private cSlashCommand RowAt(int aintItem) {
            int i = aintItem - RowOffset;
            return i >= 0 && i < mlstRows.Count ? mlstRows[i] : null;
        }

        public cSlashCommand Selected { get { return RowAt(lst.SelectedIndex); } }

        public int RowCount { get { return mlstRows.Count; } }

        // Shows the rows with row aintSelected picked (-1: none), the agent's "N more" and the
        // argument hint, just above arcAnchor (screen), as wide as it, or below it when there is
        // no room above. Colors follow the box.
        public void ShowFor(List<cSlashCommand> arows, int aintSelected, string astrMore, string astrHint,
                            Rectangle arcAnchor, Color aclrBack, Color aclrText, Form afrmOwner) {
            mlstRows = arows ?? new List<cSlashCommand>();
            mstrHint = astrHint ?? "";
            mstrMore = astrMore ?? "";
            lst.BeginUpdate();
            lst.Items.Clear();
            if (mstrHint.Length > 0) lst.Items.Add("hint");
            foreach (cSlashCommand c in mlstRows) lst.Items.Add(c.Name);
            if (mstrMore.Length > 0) lst.Items.Add("more");
            lst.EndUpdate();
            if (aintSelected >= 0 && aintSelected < mlstRows.Count) lst.SelectedIndex = aintSelected + RowOffset;
            else lst.SelectedIndex = -1;
            lst.BackColor = aclrBack;
            lst.ForeColor = aclrText;

            int intRows = Math.Max(1, Math.Min(CNST_ROWS_MAX, lst.Items.Count));
            int intH = intRows * lst.ItemHeight + 2;
            int intW = Math.Max(420, arcAnchor.Width);
            Rectangle rcScreen = Screen.FromRectangle(arcAnchor).WorkingArea;
            int intX = Math.Max(rcScreen.Left, Math.Min(arcAnchor.Left, rcScreen.Right - intW));
            int intY = arcAnchor.Top - intH;
            if (intY < rcScreen.Top) intY = arcAnchor.Bottom;
            this.Bounds = new Rectangle(intX, intY, intW, intH);
            if (!this.Visible) {
                if (afrmOwner != null && this.Owner != afrmOwner) this.Owner = afrmOwner;
                this.Show();
            }
            lst.Invalidate();
        }

        // Moves the pick within the rows (used when the list is the dock's own, not the agent's).
        public void Step(int aintBy) {
            if (mlstRows.Count == 0) return;
            int i = (lst.SelectedIndex < RowOffset ? -1 : lst.SelectedIndex - RowOffset) + aintBy;
            lst.SelectedIndex = Math.Max(0, Math.Min(mlstRows.Count - 1, i)) + RowOffset;
        }

        private void List_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0 || e.Index >= lst.Items.Count) return;
            Color clrDimBase = Blend(lst.ForeColor, lst.BackColor, 0.45);
            using (SolidBrush b = new SolidBrush(lst.BackColor)) e.Graphics.FillRectangle(b, e.Bounds);
            Rectangle rcAll = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top, e.Bounds.Width - 12, e.Bounds.Height);
            TextFormatFlags fl = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            if (mstrHint.Length > 0 && e.Index == 0) {
                TextRenderer.DrawText(e.Graphics, mstrHint, lst.Font, rcAll, clrDimBase, fl);
                return;
            }
            cSlashCommand c = RowAt(e.Index);
            if (c == null) {
                TextRenderer.DrawText(e.Graphics, "    " + mstrMore, lst.Font, rcAll, clrDimBase, fl);
                return;
            }
            bool blnSel = (e.State & DrawItemState.Selected) != 0;
            if (blnSel) using (SolidBrush b = new SolidBrush(SystemColors.Highlight)) e.Graphics.FillRectangle(b, e.Bounds);
            Color clrText = blnSel ? SystemColors.HighlightText : lst.ForeColor;
            Color clrDim  = blnSel ? SystemColors.HighlightText : clrDimBase;
            string strName = "/" + c.Name;
            Size szName = TextRenderer.MeasureText(e.Graphics, strName, mfntName);
            int intNameW = Math.Min(Math.Max(szName.Width, 150), e.Bounds.Width / 2);
            Rectangle rcName = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top, intNameW, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, strName, mfntName, rcName, clrText, fl);
            string strDesc = c.ArgumentHint.Length > 0 ? c.ArgumentHint + "   " + c.Description : c.Description;
            Rectangle rcDesc = new Rectangle(rcName.Right + 12, e.Bounds.Top, e.Bounds.Right - rcName.Right - 18, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, strDesc, lst.Font, rcDesc, clrDim, fl);
        }

        private static Color Blend(Color a, Color b, double t) {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }
}
