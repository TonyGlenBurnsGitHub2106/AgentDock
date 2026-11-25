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
            ApplyTheme(aTheme, mStyle);
        }

        // The colors and the bubble style (Story 6846). A new style can change every bubble's
        // size (Plain is full width, a tail takes room), so the view is laid out again.
        public void ApplyTheme(cComposeTheme aTheme, cBubbleStyle aStyle) {
            ApplyTheme(aTheme, aStyle, mFont);
        }

        // The colors, the bubble style and the font (Story 6846). A new style or font can change
        // every bubble's size, so the view is laid out again.
        public void ApplyTheme(cComposeTheme aTheme, cBubbleStyle aStyle, cConversationFont aFont) {
            mTheme = aTheme;
            bool blnNewLayout = aStyle != mStyle || (aFont != null && aFont != mFont);
            mStyle = aStyle ?? cBubbleStyle.Named(cBubbleStyle.CNST_DEFAULT);
            if (aFont != null && aFont != mFont) {
                mFont = aFont;
                Font fntOld = this.Font;
                this.Font = aFont.Create();
                if (fntOld != null && fntOld != this.Font) fntOld.Dispose();
            }
            this.BackColor = aTheme.IsSystem ? SystemColors.Window : aTheme.InputBack;
            cComposeTheme.ThemeScrollBars(this, aTheme);
            if (blnNewLayout) Relayout();
            Invalidate();
        }

        private cConversationFont mFont = cConversationFont.Named(cConversationFont.CNST_DEFAULT);
        private const int CNST_BAND   = 4;     // Card: room under the band's text
        private const int CNST_ACCENT = 6;     // Accent: the bar's width

        private cBubbleStyle mStyle = cBubbleStyle.Named(cBubbleStyle.CNST_DEFAULT);
        private const int CNST_TAIL   = 8;     // how far a tail sticks out from its bubble
        private const int CNST_BAR    = 4;     // Plain: the bar at the left of each row
        private const int CNST_SHADOW = 3;     // how far a shadow falls

        // Working (Story 6846): while the agent has not finished its answer, a marker sits at the
        // bottom of the conversation, "Agent is working...", with dots that move.
        private bool  mblnWorking;
        private int   mintDots;
        private readonly Timer mtmrDots = new Timer();
        private const int CNST_WORKING_H = 30;

        public void SetWorking(bool ablnWorking) {
            if (ablnWorking == mblnWorking) return;
            mblnWorking = ablnWorking;
            if (mtmrDots.Interval != 450) {
                mtmrDots.Interval = 450;
                mtmrDots.Tick += delegate { mintDots = (mintDots + 1) % 4; Invalidate(); };
            }
            if (ablnWorking) mtmrDots.Start(); else mtmrDots.Stop();
            Relayout();
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
            // Plain and Notebook rows run the full width. A tail takes room at the speaker's side.
            bool blnFull = mStyle.Flat || mStyle.Underline;
            int intSide  = CNST_MARGIN + (mStyle.Tail ? CNST_TAIL : 0) + (mStyle.Glow ? 3 : 0);
            int intMaxBubble = blnFull ? Math.Max(80, intWidth - 2 * CNST_MARGIN)
                                       : Math.Max(80, (int)(intWidth * 0.82) - intSide);
            int intGap = mStyle.Gap + (mStyle.Shadow ? CNST_SHADOW : 0) + (mStyle.HardShadow ? 4 : 0);
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
                Padding pad = Insets(entry.IsAnswer);
                Size szText = TextRenderer.MeasureText(b.Shown, this.Font,
                    new Size(Math.Max(20, intMaxBubble - pad.Horizontal), Int32.MaxValue), flags);
                int w = blnFull ? intMaxBubble : Math.Max(60, Math.Min(intMaxBubble, szText.Width + pad.Horizontal));
                if (mStyle.HeaderBand) w = Math.Max(w, TextRenderer.MeasureText("Agent", mfntCaption).Width + 2 * mStyle.Pad + 8);
                int h = szText.Height + pad.Vertical;
                int x = blnFull ? CNST_MARGIN : (entry.IsAnswer ? intSide : intWidth - intSide - w);
                b.CaptionAt = new Point(blnFull ? x + (mStyle.Flat ? mStyle.Pad + CNST_BAR : 0) : (entry.IsAnswer ? x + 4 : x + w - 4), y);
                // Card puts the speaker in its band, so nothing goes above the bubble.
                if (b.ShowCaption && !mStyle.HeaderBand) y += intCaptionH + 2;
                else if (!mStyle.HeaderBand) y -= intGap / 2;       // the same speaker again: closer together
                b.Box = new Rectangle(x, y, w, h);
                y += h + intGap;
                mlstBubbles.Add(b);
            }
            mintContentEnd = y;
            this.AutoScrollMinSize = new Size(0, mlstBubbles.Count == 0 ? 0 : y + (mblnWorking ? CNST_WORKING_H : 0));
            if (mblnPinned) ScrollToBottom();
            Invalidate();
        }

        // The room between a bubble's edge and its text, for the current style.
        private Padding Insets(bool ablnAnswer) {
            int p = mStyle.Pad;
            Padding pad = new Padding(p, p, p, p);
            if (mStyle.Flat) pad.Left += CNST_BAR;
            if (mStyle.Accent) { if (ablnAnswer) pad.Left += CNST_ACCENT; else pad.Right += CNST_ACCENT; }
            if (mStyle.HeaderBand) pad.Top += mfntCaption.Height + CNST_BAND;
            if (mStyle.Underline) { pad.Left = 2; pad.Right = 2; }
            // A pill's round ends would cut into the text.
            if (mStyle.PromptRadius >= 99) { pad.Left += 6; pad.Right += 6; }
            return pad;
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
                int intRadius = blnAnswer ? mStyle.AnswerRadius : mStyle.PromptRadius;
                bool blnHover = b == mHover;
                string strCaption = blnAnswer ? "Agent" : "You";

                if (b.ShowCaption && !mStyle.HeaderBand) {
                    Size szCap = TextRenderer.MeasureText(strCaption, mfntCaption);
                    bool blnLeft = blnAnswer || mStyle.Flat || mStyle.Underline;
                    int xCap = blnLeft ? b.CaptionAt.X : b.CaptionAt.X - szCap.Width;
                    TextRenderer.DrawText(g, strCaption, mfntCaption, new Point(xCap, b.CaptionAt.Y + intDy), mTheme.Caption);
                }

                if (mStyle.Flat) {
                    // Plain: no bubble. A bar in the speaker's color at the left, and a faint
                    // band behind the row only while the mouse is over it.
                    if (blnHover) using (SolidBrush brHover = new SolidBrush(Color.FromArgb(28, clrEdge))) g.FillRectangle(brHover, rc);
                    using (SolidBrush brBar = new SolidBrush(clrEdge)) g.FillRectangle(brBar, rc.X, rc.Y, CNST_BAR, rc.Height);
                    clrText = ForeColorFor();
                } else if (mStyle.Underline) {
                    // Notebook: a rule under each message in the speaker's color.
                    if (blnHover) using (SolidBrush brHover = new SolidBrush(Color.FromArgb(24, clrEdge))) g.FillRectangle(brHover, rc);
                    using (Pen pen = new Pen(clrEdge, blnHover ? 2.5F : 1.25F)) g.DrawLine(pen, rc.X, rc.Bottom - 1, rc.Right, rc.Bottom - 1);
                    clrText = ForeColorFor();
                } else {
                    PaintBubble(g, rc, intRadius, blnAnswer, clrFill, clrEdge, blnHover, strCaption);
                    if (mStyle.Solid) clrText = Contrast(clrEdge);
                    else if (!mStyle.Fill) clrText = ForeColorFor();
                }
                Padding pad = Insets(blnAnswer);
                Rectangle rcText = new Rectangle(rc.X + pad.Left, rc.Y + pad.Top,
                    Math.Max(1, rc.Width - pad.Horizontal), Math.Max(1, rc.Height - pad.Vertical));
                TextRenderer.DrawText(g, b.Shown, this.Font, rcText, clrText, TextFlags());
            }
            if (mblnWorking) PaintWorking(g, mintContentEnd + intDy);
        }

        private int mintContentEnd;

        // "Agent is working" in a small pill at the left, under the last message.
        private void PaintWorking(Graphics g, int y) {
            string strText = "Agent is working" + new string('.', mintDots);
            Size sz = TextRenderer.MeasureText("Agent is working...", mfntCaption);
            Rectangle rc = new Rectangle(CNST_MARGIN, y - 4, sz.Width + 18, sz.Height + 8);
            Color clrEdge = mTheme.AnswerEdge;
            using (GraphicsPath path = Rounded(rc, rc.Height / 2))
            using (SolidBrush br = new SolidBrush(Color.FromArgb(40, clrEdge)))
            using (Pen pen = new Pen(clrEdge, 1F)) {
                g.FillPath(br, path);
                g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, strText, mfntCaption, new Rectangle(rc.X + 9, rc.Y, rc.Width - 9, rc.Height),
                ForeColorFor(), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        // Black or white, whichever reads better on aclr.
        private static Color Contrast(Color aclr) {
            return 0.299 * aclr.R + 0.587 * aclr.G + 0.114 * aclr.B > 150 ? Color.Black : Color.White;
        }

        // Text with no bubble color behind it (Plain, Outline) reads in the view's own text color.
        private Color ForeColorFor() {
            return mTheme.IsSystem ? SystemColors.WindowText : mTheme.InputFore;
        }

        // One bubble in the current style: glow and shadows, fill (plain, shaded or solid), the
        // accent bar or header band, then the outline (dashed, ink or double).
        private void PaintBubble(Graphics g, Rectangle rc, int aintRadius, bool ablnAnswer,
                                 Color aclrFill, Color aclrEdge, bool ablnHover, string astrCaption) {
            using (GraphicsPath path = BubblePath(rc, aintRadius, ablnAnswer)) {
                if (mStyle.Glow) {
                    // Wide, faint strokes of the outline color, widest first.
                    for (int i = 4; i >= 1; i--)
                        using (Pen penGlow = new Pen(Color.FromArgb(mTheme.IsDark ? 34 : 24, aclrEdge), i * 3F))
                        { penGlow.LineJoin = LineJoin.Round; g.DrawPath(penGlow, path); }
                }
                if (mStyle.HardShadow) {
                    using (GraphicsPath hard = (GraphicsPath)path.Clone())
                    using (Matrix m = new Matrix()) {
                        m.Translate(4F, 4F);
                        hard.Transform(m);
                        using (SolidBrush brHard = new SolidBrush(mTheme.IsDark ? Color.FromArgb(150, 0, 0, 0) : Color.FromArgb(200, 40, 40, 40)))
                            g.FillPath(brHard, hard);
                    }
                }
                if (mStyle.Shadow) {
                    using (Matrix m = new Matrix()) {
                        // Two soft layers, the farther one fainter.
                        for (int i = 2; i >= 1; i--) {
                            using (GraphicsPath shadow = (GraphicsPath)path.Clone()) {
                                m.Reset();
                                m.Translate(i * CNST_SHADOW / 2F, i * CNST_SHADOW / 1.5F);
                                shadow.Transform(m);
                                using (SolidBrush brShadow = new SolidBrush(Color.FromArgb(mTheme.IsDark ? 70 : 34, Color.Black)))
                                    g.FillPath(brShadow, shadow);
                            }
                        }
                    }
                }
                if (mStyle.Solid) {
                    using (SolidBrush br = new SolidBrush(aclrEdge)) g.FillPath(br, path);
                } else if (mStyle.Fill) {
                    if (mStyle.Gradient) {
                        Rectangle rcGrad = new Rectangle(rc.X, rc.Y - 1, rc.Width, rc.Height + 2);
                        using (LinearGradientBrush brGrad = new LinearGradientBrush(rcGrad,
                            Blend(aclrFill, Color.White, mTheme.IsDark ? 0.10 : 0.55), aclrFill, LinearGradientMode.Vertical))
                            g.FillPath(brGrad, path);
                    } else {
                        using (SolidBrush br = new SolidBrush(aclrFill)) g.FillPath(br, path);
                    }
                }
                if (mStyle.Accent || mStyle.HeaderBand) {
                    Region rgnOld = g.Clip;
                    g.SetClip(path, CombineMode.Intersect);
                    using (SolidBrush brEdge = new SolidBrush(aclrEdge)) {
                        if (mStyle.Accent)
                            g.FillRectangle(brEdge, ablnAnswer ? rc.X : rc.Right - CNST_ACCENT, rc.Y, CNST_ACCENT, rc.Height);
                        if (mStyle.HeaderBand) {
                            int intBand = mfntCaption.Height + CNST_BAND;
                            g.FillRectangle(brEdge, rc.X, rc.Y, rc.Width, intBand);
                            TextRenderer.DrawText(g, astrCaption, mfntCaption,
                                new Rectangle(rc.X + mStyle.Pad, rc.Y, rc.Width - 2 * mStyle.Pad, intBand), Contrast(aclrEdge),
                                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                        }
                    }
                    g.Clip = rgnOld;
                }
                float fltEdge = mStyle.EdgeWidth;
                if (ablnHover) fltEdge = Math.Max(2.5F, fltEdge + 1F);   // the bubble under the mouse stands out
                if (fltEdge > 0) {
                    Color clrPen = mStyle.InkEdge ? (mTheme.IsDark ? Color.FromArgb(235, 235, 235) : Color.FromArgb(25, 25, 25)) : aclrEdge;
                    using (Pen pen = new Pen(clrPen, fltEdge)) {
                        if (mStyle.Dashed) pen.DashStyle = DashStyle.Dash;
                        g.DrawPath(pen, path);
                    }
                    if (mStyle.DoubleEdge) {
                        Rectangle rcIn = Rectangle.Inflate(rc, -4, -4);
                        using (GraphicsPath inner = Rounded(rcIn, Math.Max(1, aintRadius - 3)))
                        using (Pen pen2 = new Pen(clrPen, Math.Max(1F, fltEdge - 0.5F)))
                            g.DrawPath(pen2, inner);
                    }
                } else if (ablnHover) {
                    using (Pen pen = new Pen(aclrEdge, 2F)) g.DrawPath(pen, path);
                }
            }
        }

        private static Color Blend(Color a, Color b, double adblB) {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * adblB), (int)(a.G + (b.G - a.G) * adblB), (int)(a.B + (b.B - a.B) * adblB));
        }

        // The bubble's outline: a rounded (or square) box, with a tail at the bottom corner on the
        // speaker's side when the style has one (answers at the left, your prompts at the right).
        private GraphicsPath BubblePath(Rectangle arc, int aintRadius, bool ablnAnswer) {
            if (!mStyle.Tail) return aintRadius > 0 ? Rounded(arc, aintRadius) : Square(arc);
            int d = Math.Max(2, Math.Min(aintRadius * 2, Math.Min(arc.Width, arc.Height)));
            int t = CNST_TAIL;
            GraphicsPath p = new GraphicsPath();
            if (ablnAnswer) {
                // Clockwise from the top left. The bottom left corner is the tail.
                p.AddArc(arc.X, arc.Y, d, d, 180, 90);
                p.AddArc(arc.Right - d, arc.Y, d, d, 270, 90);
                p.AddArc(arc.Right - d, arc.Bottom - d, d, d, 0, 90);
                p.AddLine(arc.Right - d / 2, arc.Bottom, arc.X - t, arc.Bottom);
                p.AddLine(arc.X - t, arc.Bottom, arc.X, arc.Bottom - t);
                p.AddLine(arc.X, arc.Bottom - t, arc.X, arc.Y + d / 2);
            } else {
                // Clockwise from the top left. The bottom right corner is the tail.
                p.AddArc(arc.X, arc.Y, d, d, 180, 90);
                p.AddArc(arc.Right - d, arc.Y, d, d, 270, 90);
                p.AddLine(arc.Right, arc.Y + d / 2, arc.Right, arc.Bottom - t);
                p.AddLine(arc.Right, arc.Bottom - t, arc.Right + t, arc.Bottom);
                p.AddLine(arc.Right + t, arc.Bottom, arc.X + d / 2, arc.Bottom);
                p.AddArc(arc.X, arc.Bottom - d, d, d, 90, 90);
            }
            p.CloseFigure();
            return p;
        }

        private static GraphicsPath Square(Rectangle arc) {
            GraphicsPath p = new GraphicsPath();
            p.AddRectangle(arc);
            return p;
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
