using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Story 5616 -- pane ordering and maximize for the Agent Dock.
//
// Both docks (AgentDockStandalone, the Start Menu one, and AgentDock, the Leonardo menu one)
// compile this one file: AgentDockStandalone owns it and AgentDock links it. It is deliberately
// self-contained -- its own P/Invokes, its own menu items appended to the dock's Window menu at
// run time -- so neither dock's Designer file or Win32.cs had to change, and a fix here reaches
// both on the next build of each.
//
// What it adds:
//   * An explicit pane order. WinForms' LayoutMdi tiles in whatever order it keeps internally,
//     so the docks had no way to put a pane on the left. Every tiling here walks mlstOrder.
//   * Window -> Reorder Panes (Move Up / Move Down / OK / Cancel), Reverse Pane Order,
//     Move Pane Earlier / Later (Ctrl+Shift+Left / Right), Maximize / Restore Pane (Ctrl+Shift+M).
//   * "The pane" means the one last CLICKED. The dock's mouse hook reports every click
//     (NoteClick); a click inside an absorbed terminal never makes its MDI child "active", so
//     ActiveMdiChild alone would move the wrong pane -- the caveat in Copilot's dock.
//   * The shortcuts work while a terminal has the keyboard. A low-level keyboard hook sees the
//     three chords only while the dock is the foreground window, acts on them, and swallows them,
//     because the embedded terminal would otherwise eat Ctrl+Shift+arrows first. The menu items
//     show the chord as text only (no ShortcutKeys), so a chord never fires twice.
//
// Tiling is SetBounds math against the MdiClient, like LayoutMdi but in our order. Modes:
// "vertical" (side by side, the docks' default), "horizontal" (stacked), "cascade", "quad"
// (2 x 2, extra panes fall back to vertical). The mode is remembered, so a resize, a new pane or
// a restore re-applies it.

namespace AgentDockShared {

    public class cPaneArranger : IDisposable {

        public const string CNST_TILE_VERTICAL   = "vertical";
        public const string CNST_TILE_HORIZONTAL = "horizontal";
        public const string CNST_TILE_CASCADE    = "cascade";
        public const string CNST_TILE_QUAD       = "quad";

        private const int CNST_CASCADE_STEP = 30;

        private readonly Form           mfrmHost;
        private readonly Action<string> mactStatus;
        private readonly List<Form>     mlstOrder = new List<Form>();
        private string                  mstrTiling = CNST_TILE_VERTICAL;
        private Form                    mfrmMaximized;
        private Form                    mfrmLastClicked;

        // Extra Ctrl+Shift+<key> chords a dock registers (AddChord), keyed by virtual key.
        private readonly Dictionary<int, Action> mdicChords = new Dictionary<int, Action>();

        private ToolStripMenuItem mnuReorder;
        private ToolStripMenuItem mnuReverse;
        private ToolStripMenuItem mnuEarlier;
        private ToolStripMenuItem mnuLater;
        private ToolStripMenuItem mnuMaximize;

        private LowLevelKeyboardProc mKeyProc;   // strong ref -- Windows holds the pointer
        private IntPtr               mKeyHookId = IntPtr.Zero;

        public cPaneArranger(Form afrmHost, ToolStripMenuItem amnuWindow, Action<string> aactStatus) {
            mfrmHost   = afrmHost;
            mactStatus = aactStatus;
            AddMenuItems(amnuWindow);
        }

        public string Tiling { get { return mstrTiling; } }

        // -------------------------------------------------------------------
        // Order

        // The live panes in display order. Self-heals: drops closed panes and appends any the
        // list has not seen yet (a new spawn or absorb), so callers never have to register one.
        public List<Form> OrderedPanes() {
            List<Form> lstLive = new List<Form>();
            if (mfrmHost.MdiChildren != null) {
                foreach (Form f in mfrmHost.MdiChildren) {
                    if (!f.IsDisposed && !f.Disposing) lstLive.Add(f);
                }
            }
            mlstOrder.RemoveAll(delegate(Form f) { return !lstLive.Contains(f); });
            foreach (Form f in lstLive) {
                if (!mlstOrder.Contains(f)) mlstOrder.Add(f);
            }
            if (mfrmMaximized != null && !mlstOrder.Contains(mfrmMaximized)) mfrmMaximized = null;
            if (mfrmLastClicked != null && !mlstOrder.Contains(mfrmLastClicked)) mfrmLastClicked = null;
            return new List<Form>(mlstOrder);
        }

        // The pane the user means: the one last clicked, else WinForms' active child.
        public Form CurrentPane() {
            List<Form> lst = OrderedPanes();
            if (mfrmLastClicked != null && lst.Contains(mfrmLastClicked)) return mfrmLastClicked;
            Form act = mfrmHost.ActiveMdiChild;
            if (act != null && lst.Contains(act)) return act;
            return null;
        }

        // A dock's own Ctrl+Shift+<key> chord (Story 6805). Caught by the same keyboard hook as
        // the arranger's three, so it works while a terminal has the keyboard. Show it on the
        // menu item as ShortcutKeyDisplayString only, never ShortcutKeys, or it fires twice.
        public void AddChord(Keys akey, Action aact) {
            mdicChords[(int)akey] = aact;
        }

        // Ctrl+<key> (no Shift) and bare keys such as F1, caught by the same hook (Story 6817).
        // As ordinary menu ShortcutKeys they never fired while a terminal had the keyboard,
        // because the terminal's own window takes the key first. The same rule applies: show
        // them on the menu as ShortcutKeyDisplayString only.
        public void AddCtrlChord(Keys akey, Action aact) {
            mdicCtrlChords[(int)akey] = aact;
        }

        public void AddBareKey(Keys akey, Action aact) {
            mdicBareKeys[(int)akey] = aact;
        }

        private readonly Dictionary<int, Action> mdicCtrlChords = new Dictionary<int, Action>();
        private readonly Dictionary<int, Action> mdicBareKeys   = new Dictionary<int, Action>();

        // Called from the dock's low-level mouse hook with the HWND under the click.
        public void NoteClick(IntPtr ahwndClicked) {
            if (ahwndClicked == IntPtr.Zero) return;
            foreach (Form f in OrderedPanes()) {
                IntPtr h = f.Handle;
                if (h == ahwndClicked || IsChild(h, ahwndClicked)) { mfrmLastClicked = f; return; }
            }
        }

        public void MovePane(int aintDirection) {
            List<Form> lst = OrderedPanes();
            if (lst.Count < 2) { Say("Need at least two panes to move one."); return; }
            Form pane = CurrentPane();
            if (pane == null) { Say("Click inside a pane first, then move it."); return; }
            int i = mlstOrder.IndexOf(pane);
            int j = i + aintDirection;
            if (j < 0 || j >= mlstOrder.Count) {
                Say(aintDirection < 0 ? "That pane is already first." : "That pane is already last.");
                return;
            }
            mlstOrder[i] = mlstOrder[j];
            mlstOrder[j] = pane;
            mfrmMaximized = null;
            Apply(mstrTiling);
            pane.Activate();
            mfrmLastClicked = pane;
            Say("Moved \"" + PaneLabel(pane) + "\" to position " + (j + 1) + " of " + mlstOrder.Count + ".");
        }

        public void ReverseOrder() {
            if (OrderedPanes().Count < 2) { Say("Need at least two panes to reverse."); return; }
            mlstOrder.Reverse();
            mfrmMaximized = null;
            Apply(mstrTiling);
            Say("Pane order reversed.");
        }

        // -------------------------------------------------------------------
        // Tiling

        // Lay the panes out in the given mode and remember it. Clears any maximized pane.
        public void Tile(string astrMode) {
            mfrmMaximized = null;
            Apply(astrMode);
        }

        // Re-apply whatever is current: the maximized pane if there is one, else the last tiling.
        // The docks call this after a spawn, an absorb, a close, or a resize.
        public void ReTile() {
            List<Form> lst = OrderedPanes();
            if (lst.Count == 0) return;
            if (mfrmMaximized != null) { FillWith(mfrmMaximized); return; }
            Apply(mstrTiling);
        }

        public void ToggleMaximize() {
            List<Form> lst = OrderedPanes();
            if (lst.Count == 0) { Say("No pane to maximize."); return; }
            Form pane = CurrentPane();
            if (pane == null) pane = (lst.Count == 1) ? lst[0] : null;
            if (pane == null) { Say("Click inside a pane first, then maximize it."); return; }
            if (mfrmMaximized == pane) {
                mfrmMaximized = null;
                Apply(mstrTiling);
                pane.Activate();
                Say("Restored the tiling.");
                return;
            }
            mfrmMaximized = pane;
            FillWith(pane);
            Say("\"" + PaneLabel(pane) + "\" fills the dock. Ctrl+Shift+M again restores the tiling.");
        }

        private void Apply(string astrMode) {
            mstrTiling = astrMode;
            List<Form> lst = OrderedPanes();
            Rectangle area = ClientArea();
            if (lst.Count == 0 || area.Width <= 0 || area.Height <= 0) return;

            mfrmHost.SuspendLayout();
            try {
                int n = lst.Count;
                if (astrMode == CNST_TILE_HORIZONTAL) {
                    for (int i = 0; i < n; i++) {
                        int top = area.Height * i / n, bottom = area.Height * (i + 1) / n;
                        Place(lst[i], 0, top, area.Width, bottom - top);
                    }
                } else if (astrMode == CNST_TILE_CASCADE) {
                    int w = Math.Max(area.Width  - CNST_CASCADE_STEP * (n - 1), area.Width  / 2);
                    int h = Math.Max(area.Height - CNST_CASCADE_STEP * (n - 1), area.Height / 2);
                    for (int i = 0; i < n; i++) {
                        Place(lst[i], CNST_CASCADE_STEP * i, CNST_CASCADE_STEP * i, w, h);
                        lst[i].BringToFront();
                    }
                } else if (astrMode == CNST_TILE_QUAD && n <= 4) {
                    int hw = area.Width / 2, hh = area.Height / 2;
                    Rectangle[] r = new Rectangle[] {
                        new Rectangle(0,  0,  hw,              hh),
                        new Rectangle(hw, 0,  area.Width - hw, hh),
                        new Rectangle(0,  hh, hw,              area.Height - hh),
                        new Rectangle(hw, hh, area.Width - hw, area.Height - hh) };
                    for (int i = 0; i < n; i++) Place(lst[i], r[i].X, r[i].Y, r[i].Width, r[i].Height);
                } else {
                    // Vertical: side-by-side columns. Also quad with more than four panes.
                    for (int i = 0; i < n; i++) {
                        int left = area.Width * i / n, right = area.Width * (i + 1) / n;
                        Place(lst[i], left, 0, right - left, area.Height);
                    }
                }
            } finally {
                mfrmHost.ResumeLayout();
            }
        }

        private void FillWith(Form afrmPane) {
            Rectangle area = ClientArea();
            if (area.Width <= 0 || area.Height <= 0) return;
            Place(afrmPane, 0, 0, area.Width, area.Height);
            afrmPane.BringToFront();
            afrmPane.Activate();
        }

        private static void Place(Form f, int x, int y, int w, int h) {
            if (f.WindowState != FormWindowState.Normal) f.WindowState = FormWindowState.Normal;
            f.SetBounds(x, y, Math.Max(w, 1), Math.Max(h, 1));
        }

        private Rectangle ClientArea() {
            foreach (Control c in mfrmHost.Controls) {
                MdiClient mc = c as MdiClient;
                if (mc != null) return new Rectangle(0, 0, mc.ClientSize.Width, mc.ClientSize.Height);
            }
            return Rectangle.Empty;
        }

        // -------------------------------------------------------------------
        // Reorder dialog

        public void ShowReorderDialog() {
            List<Form> lst = OrderedPanes();
            if (lst.Count < 2) { Say("Need at least two panes to reorder."); return; }

            using (Form dlg = new Form()) {
                dlg.Text            = "Reorder Panes";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition   = FormStartPosition.CenterParent;
                dlg.MinimizeBox     = false;
                dlg.MaximizeBox     = false;
                dlg.ShowInTaskbar   = false;
                dlg.ClientSize      = new Size(420, 260);

                Label lbl = new Label();
                lbl.Text     = "First is left (or top). Select a pane and move it.";
                lbl.AutoSize = true;
                lbl.Location = new Point(12, 10);

                ListBox lb = new ListBox();
                lb.Location            = new Point(12, 32);
                lb.Size                = new Size(300, 178);
                lb.IntegralHeight      = false;
                lb.HorizontalScrollbar = true;
                List<Form> lstWork = new List<Form>(lst);
                foreach (Form f in lstWork) lb.Items.Add(PaneLabel(f));
                Form cur = CurrentPane();
                lb.SelectedIndex = (cur != null && lstWork.Contains(cur)) ? lstWork.IndexOf(cur) : 0;

                Button btnUp   = MakeButton("Move &Up",   new Point(322, 32));
                Button btnDown = MakeButton("Move &Down", new Point(322, 70));
                Button btnOK     = MakeButton("OK",     new Point(234, 220));
                Button btnCancel = MakeButton("Cancel", new Point(322, 220));
                btnOK.DialogResult     = DialogResult.OK;
                btnCancel.DialogResult = DialogResult.Cancel;
                dlg.AcceptButton = btnOK;
                dlg.CancelButton = btnCancel;

                EventHandler move = delegate(object sender, EventArgs e) {
                    int dir = (sender == btnUp) ? -1 : 1;
                    int i = lb.SelectedIndex, j = i + dir;
                    if (i < 0 || j < 0 || j >= lstWork.Count) return;
                    Form t = lstWork[i]; lstWork[i] = lstWork[j]; lstWork[j] = t;
                    object o = lb.Items[i]; lb.Items[i] = lb.Items[j]; lb.Items[j] = o;
                    lb.SelectedIndex = j;
                };
                btnUp.Click   += move;
                btnDown.Click += move;

                dlg.Controls.AddRange(new Control[] { lbl, lb, btnUp, btnDown, btnOK, btnCancel });

                if (dlg.ShowDialog(mfrmHost) != DialogResult.OK) return;

                // Panes may have closed while the dialog was open; keep only live ones, then
                // append anything new so nothing drops out of the layout.
                List<Form> lstLive = OrderedPanes();
                mlstOrder.Clear();
                foreach (Form f in lstWork) if (lstLive.Contains(f)) mlstOrder.Add(f);
                foreach (Form f in lstLive) if (!mlstOrder.Contains(f)) mlstOrder.Add(f);
                mfrmMaximized = null;
                Apply(mstrTiling);
                Say("Pane order applied.");
            }
        }

        private static Button MakeButton(string astrText, Point apt) {
            Button b = new Button();
            b.Text     = astrText;
            b.Location = apt;
            b.Size     = new Size(80, 28);
            return b;
        }

        private static string PaneLabel(Form f) {
            string s = (f.Text ?? "").Trim();
            return s.Length > 0 ? s : "Pane";
        }

        // -------------------------------------------------------------------
        // Menu

        private void AddMenuItems(ToolStripMenuItem amnuWindow) {
            if (amnuWindow == null) return;
            mnuReorder  = MakeItem("&Reorder Panes", null, delegate { ShowReorderDialog(); });
            mnuReverse  = MakeItem("Re&verse Pane Order", null, delegate { ReverseOrder(); });
            mnuEarlier  = MakeItem("Move Pane &Earlier", "Ctrl+Shift+Left",  delegate { MovePane(-1); });
            mnuLater    = MakeItem("Move Pane &Later",   "Ctrl+Shift+Right", delegate { MovePane(1); });
            mnuMaximize = MakeItem("&Maximize / Restore Pane", "Ctrl+Shift+M", delegate { ToggleMaximize(); });

            amnuWindow.DropDownItems.Add(new ToolStripSeparator());
            amnuWindow.DropDownItems.Add(mnuReorder);
            amnuWindow.DropDownItems.Add(mnuReverse);
            amnuWindow.DropDownItems.Add(mnuEarlier);
            amnuWindow.DropDownItems.Add(mnuLater);
            amnuWindow.DropDownItems.Add(mnuMaximize);
            amnuWindow.DropDownOpening += delegate {
                int n = OrderedPanes().Count;
                mnuReorder.Enabled  = n >= 2;
                mnuReverse.Enabled  = n >= 2;
                mnuEarlier.Enabled  = n >= 2;
                mnuLater.Enabled    = n >= 2;
                mnuMaximize.Enabled = n >= 1;
                mnuMaximize.Checked = mfrmMaximized != null;
            };
        }

        private static ToolStripMenuItem MakeItem(string astrText, string astrKeys, EventHandler aonClick) {
            ToolStripMenuItem mi = new ToolStripMenuItem(astrText);
            if (astrKeys != null) mi.ShortcutKeyDisplayString = astrKeys;
            mi.Click += aonClick;
            return mi;
        }

        private void Say(string astrText) {
            if (mactStatus != null) {
                try { mactStatus(astrText); } catch { }
            }
        }

        // -------------------------------------------------------------------
        // Keyboard hook -- the three chords, only while the dock is in front

        public void InstallKeyboardHook() {
            if (mKeyHookId != IntPtr.Zero) return;
            mKeyProc = new LowLevelKeyboardProc(KeyHookCallback);
            using (System.Diagnostics.Process p = System.Diagnostics.Process.GetCurrentProcess())
            using (System.Diagnostics.ProcessModule m = p.MainModule) {
                mKeyHookId = SetWindowsHookEx(WH_KEYBOARD_LL, mKeyProc, GetModuleHandle(m.ModuleName), 0);
            }
        }

        public void Dispose() {
            if (mKeyHookId != IntPtr.Zero) {
                UnhookWindowsHookEx(mKeyHookId);
                mKeyHookId = IntPtr.Zero;
            }
        }

        private IntPtr KeyHookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            try {
                int msg = wParam.ToInt32();
                if (nCode >= 0 && (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    && !mfrmHost.IsDisposed && GetForegroundWindow() == mfrmHost.Handle
                    && !Down(VK_MENU)) {
                    KBDLLHOOKSTRUCT k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    bool blnCtrl  = Down(VK_CONTROL);
                    bool blnShift = Down(VK_SHIFT);
                    Action act = null;
                    if (blnCtrl && blnShift) {
                        if (k.vkCode == VK_LEFT)       act = delegate { MovePane(-1); };
                        else if (k.vkCode == VK_RIGHT) act = delegate { MovePane(1); };
                        else if (k.vkCode == VK_M)     act = delegate { ToggleMaximize(); };
                        else mdicChords.TryGetValue(k.vkCode, out act);
                    } else if (blnCtrl) {
                        mdicCtrlChords.TryGetValue(k.vkCode, out act);
                    } else if (!blnShift) {
                        mdicBareKeys.TryGetValue(k.vkCode, out act);
                    }
                    if (act != null) {
                        // Never do UI work inside the hook -- Windows times the callback out.
                        mfrmHost.BeginInvoke(act);
                        return (IntPtr)1;
                    }
                }
            } catch { /* a hook must never throw */ }
            return CallNextHookEx(mKeyHookId, nCode, wParam, lParam);
        }

        private static bool Down(int aintVk) {
            return (GetAsyncKeyState(aintVk) & 0x8000) != 0;
        }

        // -------------------------------------------------------------------
        // Win32 -- kept here so this file needs nothing from either dock's Win32.cs

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_SYSKEYDOWN  = 0x0104;
        private const int VK_SHIFT       = 0x10;
        private const int VK_CONTROL     = 0x11;
        private const int VK_MENU        = 0x12;
        private const int VK_LEFT        = 0x25;
        private const int VK_RIGHT       = 0x27;
        private const int VK_M           = 0x4D;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT {
            public int    vkCode;
            public int    scanCode;
            public int    flags;
            public int    time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);
    }
}
