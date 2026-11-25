using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AgentDockStandalone {

    // Run conversation (Story 6845): the pane's conversation as chat bubbles, newest at the
    // bottom. Your prompts sit at the right in rounder bubbles, the agent's answers at the left in
    // squarer ones, each in its own theme colors and outlined. Only what was said is shown: no
    // thinking, no tool calls. A long one is cut short in its bubble. Right-clicking a bubble
    // offers Popup (the whole text in the answer window, BubbleClicked) and Copy (Tony,
    // 2026-11-25: "don't do the popup on click ... right click a message, then popup from a
    // context menu, or copy"). A left click does nothing.
    internal sealed class cConversationView : Panel {

        private const int CNST_MARGIN        = 10;   // from the sides of the view
        private const int CNST_PAD           = 9;    // inside a bubble, around the text
        private const int CNST_GAP           = 10;   // between one bubble and the next caption
        private const int CNST_PROMPT_RADIUS = 14;
        private const int CNST_ANSWER_RADIUS = 4;
        private const int CNST_SHOW_LINES    = 14;   // a longer one is cut short in its bubble
        private const int CNST_SHOW_CHARS    = 1500;
        private const string CNST_MORE       = "\r\n... right-click, Popup, to read all of it";

        private sealed class Bubble {
            public cHistoryEntry Entry;
            public string        Shown;
            public Rectangle     Box;       // in content coordinates, the bubble itself
            public Point         CaptionAt;
            public bool          ShowCaption;   // only the first of a run from the same speaker
        }

        private readonly List<Bubble> mlstBubbles = new List<Bubble>();
        private List<cHistoryEntry> mlstEntries = new List<cHistoryEntry>();
        private readonly Font mfntCaption = new Font("Segoe UI", 8F);
        private cComposeTheme mTheme = cComposeTheme.Named(cComposeTheme.CNST_DEFAULT);
        private Bubble mHover;
        private string mstrMessage = "";
        private int    mintLaidOutWidth = -1;

        public event Action<cHistoryEntry> BubbleClicked;

        public cConversationView() {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.AutoScroll = true;
            this.Font       = new Font("Segoe UI", 9.75F);
            ApplyTheme(mTheme);
        }

        public void ApplyTheme(cComposeTheme aTheme) {
            mTheme = aTheme;
            this.BackColor = aTheme.IsSystem ? SystemColors.Window : aTheme.InputBack;
            cComposeTheme.ThemeScrollBars(this, aTheme);
            Invalidate();
        }

        // A line shown in the middle when there is nothing to show ("Pick a pane ...").
        public void SetMessage(string astrMessage) {
            mstrMessage = astrMessage ?? "";
            if (mlstEntries.Count > 0) { mlstEntries = new List<cHistoryEntry>(); mblnPinned = true; Relayout(); }
            Invalidate();
        }

        // The conversation, oldest first, read from the bottom up (Tony, 2026-11-25: "It should
        // be ascending"): the view opens at the newest message, at the bottom, and older ones are
        // above it. It stays pinned to the newest while new messages come in, until you scroll up
        // to read; scrolling back to the bottom pins it again. ablnToBottom (a new pane) pins it.
        public void SetEntries(List<cHistoryEntry> alstEntries, bool ablnToBottom) {
            List<cHistoryEntry> lst = alstEntries ?? new List<cHistoryEntry>();
            if (ablnToBottom) mblnPinned = true;
            if (!ablnToBottom && SameAs(lst)) return;
            mlstEntries = lst;
            mstrMessage = lst.Count == 0 ? "No conversation yet." : "";
            Relayout();
        }

        // True while the view follows the newest message (see SetEntries).
        private bool mblnPinned = true;

        private bool SameAs(List<cHistoryEntry> alst) {
            if (alst.Count != mlstEntries.Count) return false;
            for (int i = 0; i < alst.Count; i++)
                if (alst[i].IsAnswer != mlstEntries[i].IsAnswer || alst[i].Text != mlstEntries[i].Text) return false;
            return true;
        }

        private bool IsAtBottom() {
            int intMax = this.AutoScrollMinSize.Height - this.ClientSize.Height;
            return intMax <= 0 || -this.AutoScrollPosition.Y >= intMax - 6;
        }

        // To the newest message. Done again once the window has laid itself out, because a
        // scroll set while the view is still being sized (the dock starting, the box growing)
        // is lost: that left the view sitting on the oldest messages (1.0.35).
        public void ScrollToBottom() {
            mblnPinned = true;
            ScrollNow();
            if (this.IsHandleCreated) this.BeginInvoke(new MethodInvoker(delegate { if (mblnPinned) ScrollNow(); }));
        }

        private void ScrollNow() {
            int intMax = Math.Max(0, this.AutoScrollMinSize.Height - this.ClientSize.Height);
            this.AutoScrollPosition = new Point(0, intMax);
            Invalidate();
        }

        // Scrolling by hand decides whether the view keeps following the newest message.
        protected override void OnScroll(ScrollEventArgs se) {
            base.OnScroll(se);
            mblnPinned = IsAtBottom();
        }

        protected override void OnMouseWheel(MouseEventArgs e) {
            base.OnMouseWheel(e);
            mblnPinned = IsAtBottom();
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            if (this.ClientSize.Width != mintLaidOutWidth) Relayout();
            else if (mblnPinned) ScrollNow();
        }

        protected override void OnVisibleChanged(EventArgs e) {
            base.OnVisibleChanged(e);
            if (this.Visible && mblnPinned) ScrollToBottom();
        }

        // Places every bubble for the current width.
        private void Relayout() {
            mlstBubbles.Clear();
            mHover = null;
            int intWidth = Math.Max(120, this.ClientSize.Width);
            mintLaidOutWidth = this.ClientSize.Width;
            int intMaxBubble = Math.Max(80, (int)(intWidth * 0.82) - CNST_MARGIN);
            int intCaptionH  = mfntCaption.Height;
            int y = CNST_MARGIN;
            TextFormatFlags flags = TextFlags();
            cHistoryEntry prev = null;
            foreach (cHistoryEntry entry in mlstEntries) {
                Bubble b = new Bubble();
                b.Entry = entry;
                b.ShowCaption = prev == null || prev.IsAnswer != entry.IsAnswer;
                prev = entry;
                b.Shown = Shorten(entry.Text);
                Size szText = TextRenderer.MeasureText(b.Shown, this.Font,
                    new Size(intMaxBubble - 2 * CNST_PAD, Int32.MaxValue), flags);
                int w = Math.Max(60, Math.Min(intMaxBubble, szText.Width + 2 * CNST_PAD));
                int h = szText.Height + 2 * CNST_PAD;
                int x = entry.IsAnswer ? CNST_MARGIN : intWidth - CNST_MARGIN - w;
                b.CaptionAt = new Point(entry.IsAnswer ? x + 4 : x + w - 4, y);
                if (b.ShowCaption) y += intCaptionH + 2;
                else y -= CNST_GAP / 2;             // the same speaker again: closer together
                b.Box = new Rectangle(x, y, w, h);
                y += h + CNST_GAP;
                mlstBubbles.Add(b);
            }
            this.AutoScrollMinSize = new Size(0, mlstBubbles.Count == 0 ? 0 : y);
            if (mblnPinned) ScrollToBottom();
            Invalidate();
        }

        private static TextFormatFlags TextFlags() {
            return TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix
                | TextFormatFlags.Left | TextFormatFlags.Top;
        }

        // The text as shown in its bubble: Windows line breaks, and cut short when long.
        private static string Shorten(string astrText) {
            string s = (astrText ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Trim('\n', ' ');
            bool blnCut = false;
            if (s.Length > CNST_SHOW_CHARS) { s = s.Substring(0, CNST_SHOW_CHARS); blnCut = true; }
            int intLines = 0;
            for (int i = 0; i < s.Length; i++) {
                if (s[i] != '\n') continue;
                if (++intLines >= CNST_SHOW_LINES) { s = s.Substring(0, i); blnCut = true; break; }
            }
            s = s.TrimEnd().Replace("\n", "\r\n");
            return blnCut ? s + CNST_MORE : s;
        }

        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (mlstBubbles.Count == 0) {
                TextRenderer.DrawText(g, mstrMessage, this.Font, this.ClientRectangle, mTheme.Caption,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                return;
            }
            int intDy = this.AutoScrollPosition.Y;
            Rectangle rcClip = e.ClipRectangle;
            foreach (Bubble b in mlstBubbles) {
                Rectangle rc = b.Box;
                rc.Offset(0, intDy);
                if (rc.Bottom < rcClip.Top - 30 || rc.Top - 30 > rcClip.Bottom) continue;
                bool blnAnswer = b.Entry.IsAnswer;
                Color clrFill = blnAnswer ? mTheme.AnswerFill : mTheme.PromptFill;
                Color clrEdge = blnAnswer ? mTheme.AnswerEdge : mTheme.PromptEdge;
                Color clrText = blnAnswer ? mTheme.AnswerText : mTheme.PromptText;
                int intRadius = blnAnswer ? CNST_ANSWER_RADIUS : CNST_PROMPT_RADIUS;

                if (b.ShowCaption) {
                    string strCaption = blnAnswer ? "Agent" : "You";
                    Size szCap = TextRenderer.MeasureText(strCaption, mfntCaption);
                    int xCap = blnAnswer ? b.CaptionAt.X : b.CaptionAt.X - szCap.Width;
                    TextRenderer.DrawText(g, strCaption, mfntCaption, new Point(xCap, b.CaptionAt.Y + intDy), mTheme.Caption);
                }

                using (GraphicsPath path = Rounded(rc, intRadius))
                using (SolidBrush br = new SolidBrush(clrFill))
                using (Pen pen = new Pen(clrEdge, b == mHover ? 2.5F : 1.25F)) {
                    g.FillPath(br, path);
                    g.DrawPath(pen, path);
                }
                Rectangle rcText = Rectangle.Inflate(rc, -CNST_PAD, -CNST_PAD);
                TextRenderer.DrawText(g, b.Shown, this.Font, rcText, clrText, TextFlags());
            }
        }

        private static GraphicsPath Rounded(Rectangle arc, int aintRadius) {
            int d = Math.Min(aintRadius * 2, Math.Min(arc.Width, arc.Height));
            GraphicsPath p = new GraphicsPath();
            p.AddArc(arc.X, arc.Y, d, d, 180, 90);
            p.AddArc(arc.Right - d, arc.Y, d, d, 270, 90);
            p.AddArc(arc.Right - d, arc.Bottom - d, d, d, 0, 90);
            p.AddArc(arc.X, arc.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private Bubble HitTest(Point aptClient) {
            Point pt = new Point(aptClient.X - this.AutoScrollPosition.X, aptClient.Y - this.AutoScrollPosition.Y);
            foreach (Bubble b in mlstBubbles) if (b.Box.Contains(pt)) return b;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (mMenu != null && mMenu.Visible) return;     // keep the bubble the menu is for outlined
            Bubble b = HitTest(e.Location);
            if (b == mHover) return;
            mHover = b;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) {
            base.OnMouseLeave(e);
            if (mMenu != null && mMenu.Visible) return;
            if (mHover != null) { mHover = null; Invalidate(); }
        }

        // Right-click on a bubble: Popup or Copy.
        private ContextMenuStrip mMenu;
        private cHistoryEntry    mMenuEntry;

        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Right) return;
            Bubble b = HitTest(e.Location);
            if (b == null) return;
            mHover = b;
            Invalidate();
            mMenuEntry = b.Entry;
            if (mMenu == null) {
                mMenu = new ContextMenuStrip();
                ToolStripMenuItem miPopup = new ToolStripMenuItem("&Popup");
                miPopup.ToolTipText = "Open the whole message in a window you can select, edit and copy in.";
                miPopup.Click += delegate { if (mMenuEntry != null && BubbleClicked != null) BubbleClicked(mMenuEntry); };
                ToolStripMenuItem miCopy = new ToolStripMenuItem("&Copy");
                miCopy.ToolTipText = "Copy the whole message.";
                miCopy.Click += delegate { CopyEntry(mMenuEntry); };
                mMenu.Items.Add(miPopup);
                mMenu.Items.Add(miCopy);
                mMenu.Closed += delegate { mHover = null; Invalidate(); };
            }
            mMenu.Show(this, e.Location);
        }

        // Raised with a short line for the status bar after Copy.
        public event Action<string> Status;

        private void CopyEntry(cHistoryEntry aEntry) {
            if (aEntry == null) return;
            string strText = (aEntry.Text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
            if (strText.Length == 0) return;
            try {
                Clipboard.SetText(strText);
                if (Status != null) Status(aEntry.IsAnswer ? "Copied the answer." : "Copied your prompt.");
            } catch (Exception ex) {
                if (Status != null) Status("Not copied: " + ex.Message);
            }
        }

        protected override void Dispose(bool ablnDisposing) {
            if (ablnDisposing) { mfntCaption.Dispose(); if (mMenu != null) mMenu.Dispose(); }
            base.Dispose(ablnDisposing);
        }
    }
}
