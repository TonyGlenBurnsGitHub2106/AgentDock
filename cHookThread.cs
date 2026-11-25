using System;
using System.Threading;
using System.Windows.Forms;

namespace AgentDockShared {

    // A thread of its own for the dock's low-level mouse and keyboard hooks (Story 6848, Tony:
    // "when I load conversation at work, my cursor becomes very sluggish").
    //
    // Windows calls a low-level hook on the thread that installed it, and every mouse move and
    // key press on the whole machine waits until that thread takes the call. The hooks used to
    // live on the dock's window thread, so anything that kept the window thread busy (reading
    // conversations, a long layout) made the cursor crawl everywhere, not just in the dock. Here
    // the hooks sit on a thread that does nothing else, so it always answers at once. A callback
    // must never touch a window or control directly: it hands the work to the window thread
    // with BeginInvoke.
    internal static class cHookThread {

        // Runs afnInstall on a new background thread and keeps that thread's message loop
        // running, which is what lets Windows call the hooks installed there.
        public static void Start(string astrName, Action afnInstall) {
            Thread t = new Thread(delegate() {
                try { afnInstall(); } catch (Exception) { return; }
                Application.Run();
            });
            t.Name = astrName;
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }
    }
}
