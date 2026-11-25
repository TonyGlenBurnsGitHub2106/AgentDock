using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgentDockStandalone {

    // The pick list of slash commands over a compose box (Story 6851). It never takes the
    // keyboard: the box keeps it, and steers the list with Up, Down, Tab, Enter and Escape. A
    // click on a row picks it. Each row is the command and what it does.
    internal sealed class fSlashList : Form {

        private const int CNST_ROWS_MAX = 10;
        private readonly ListBox lst = new ListBox();
        private List<cSlashCommand> mlstShown = new List<cSlashCommand>();
        private Font mfntName;

        public event Action<cSlashCommand> Picked;

        public fSlashList() {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar   = false;
            this.StartPosition   = FormStartPosition.Manual;
            this.BackColor       = SystemColors.ControlDark;   // the 1 px edge
            this.Padding         = new Padding(1);
            lst.Dock          = DockStyle.Fill;
            lst.BorderStyle   = BorderStyle.None;
            lst.DrawMode      = DrawMode.OwnerDrawFixed;
            lst.IntegralHeight = false;
            lst.Font          = new Font("Segoe UI", 9.5F);
            mfntName          = new Font(lst.Font, FontStyle.Bold);
            lst.ItemHeight    = lst.Font.Height + 6;
            lst.DrawItem     += new DrawItemEventHandler(List_DrawItem);
            lst.MouseClick   += delegate(object s, MouseEventArgs e) {
                int i = lst.IndexFromPoint(e.Location);
                if (i >= 0 && i < mlstShown.Count && Picked != null) Picked(mlstShown[i]);
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

        public cSlashCommand Selected {
            get { return lst.SelectedIndex >= 0 && lst.SelectedIndex < mlstShown.Count ? mlstShown[lst.SelectedIndex] : null; }
        }

        public int Count { get { return mlstShown.Count; } }

        // Fills the list and places it just above arcAnchor (screen), as wide as it, or below
        // it when there is no room above. Colors follow the box.
        public void ShowFor(List<cSlashCommand> alst, Rectangle arcAnchor, Color aclrBack, Color aclrText, Form afrmOwner) {
            string strWas = (!this.Visible || Selected == null) ? null : Selected.Name;   // a fresh list starts at the top
            mlstShown = alst;
            lst.BeginUpdate();
            lst.Items.Clear();
            foreach (cSlashCommand c in alst) lst.Items.Add(c.Name);
            lst.EndUpdate();
            int intAt = 0;
            if (strWas != null) for (int i = 0; i < alst.Count; i++) if (alst[i].Name == strWas) { intAt = i; break; }
            if (alst.Count > 0) lst.SelectedIndex = intAt;
            lst.BackColor = aclrBack;
            lst.ForeColor = aclrText;

            int intRows = Math.Max(1, Math.Min(CNST_ROWS_MAX, alst.Count));
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
        }

        public void Step(int aintBy) {
            if (mlstShown.Count == 0) return;
            int i = lst.SelectedIndex + aintBy;
            lst.SelectedIndex = Math.Max(0, Math.Min(mlstShown.Count - 1, i));
        }

        private void List_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0 || e.Index >= mlstShown.Count) return;
            cSlashCommand c = mlstShown[e.Index];
            bool blnSel = (e.State & DrawItemState.Selected) != 0;
            Color clrBack = blnSel ? SystemColors.Highlight : lst.BackColor;
            Color clrText = blnSel ? SystemColors.HighlightText : lst.ForeColor;
            Color clrDim  = blnSel ? SystemColors.HighlightText : Blend(lst.ForeColor, lst.BackColor, 0.45);
            using (SolidBrush b = new SolidBrush(clrBack)) e.Graphics.FillRectangle(b, e.Bounds);
            string strName = "/" + c.Name;
            Size szName = TextRenderer.MeasureText(e.Graphics, strName, mfntName);
            int intNameW = Math.Min(Math.Max(szName.Width, 150), e.Bounds.Width / 2);
            Rectangle rcName = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top, intNameW, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, strName, mfntName, rcName, clrText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            string strDesc = c.ArgumentHint.Length > 0 ? c.ArgumentHint + "   " + c.Description : c.Description;
            Rectangle rcDesc = new Rectangle(rcName.Right + 12, e.Bounds.Top, e.Bounds.Right - rcName.Right - 18, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, strDesc, lst.Font, rcDesc, clrDim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        private static Color Blend(Color a, Color b, double t) {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }
}
