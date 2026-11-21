using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

// Starts a pane's terminal at the right integrity level (Tony, 2026-11-12).
//
// Both docks compile this one file: AgentDockStandalone owns it and AgentDock links it.
//
// The problem: Tony runs the dock as administrator so admin panes can be absorbed (Windows UIPI
// blocks a normal dock from reparenting an elevated window). But a process started by an
// elevated process is elevated too, so every NORMAL pane came up as admin as well -- and every
// pane runs with --dangerously-skip-permissions, so an all-admin dock has no gate left at all.
//
// The fix: when the dock is elevated and the pane is not meant to be admin, the terminal is
// started through Explorer's own ShellExecute (the desktop's IShellDispatch2), so it runs with
// Explorer's normal token. This is the standard way to launch un-elevated from an elevated
// process. If it fails (Explorer not running, a shell replacement) the pane starts the plain way
// and the caller is told it came up elevated.

namespace AgentDockShared {

    public static class cPaneLauncher {

        // Returns true when the process was started at the intended level; false when a normal
        // pane had to be started elevated after the un-elevated launch failed.
        public static bool Start(string astrFile, string astrArgs, bool ablnAdmin) {
            bool blnSelfElevated = IsRunningElevated();

            if (!ablnAdmin && blnSelfElevated) {
                try {
                    ShellExecuteAsExplorer(astrFile, astrArgs);
                    return true;
                } catch (Exception) {
                    // fall through to the plain launch below
                }
            }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName        = astrFile;
            psi.Arguments       = astrArgs;
            psi.UseShellExecute = true;
            if (ablnAdmin) psi.Verb = "runas";   // UAC prompt unless the dock is already admin
            Process.Start(psi);
            return ablnAdmin || !blnSelfElevated;
        }

        public static bool IsRunningElevated() {
            using (System.Security.Principal.WindowsIdentity id =
                       System.Security.Principal.WindowsIdentity.GetCurrent()) {
                System.Security.Principal.WindowsPrincipal p =
                    new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }

        // Desktop window -> its shell browser -> the active shell view -> the view's background
        // automation object -> .Application (IShellDispatch2) -> ShellExecute. The call runs in
        // Explorer's process, so the new process gets Explorer's (normal) token.
        private static void ShellExecuteAsExplorer(string astrFile, string astrArgs) {
            Type tShellWindows = Type.GetTypeFromCLSID(CLSID_ShellWindows, true);
            object objShellWindows = Activator.CreateInstance(tShellWindows);
            object objDesktop = null, objBackground = null, objApp = null;
            IServiceProvider sp = null;
            IShellBrowser browser = null;
            IShellView view = null;
            try {
                object objLoc  = CSIDL_DESKTOP;
                object objRoot = Missing.Value;
                object[] aobjArgs = new object[] { objLoc, objRoot, SWC_DESKTOP, 0, SWFO_NEEDDISPATCH };
                ParameterModifier pm = new ParameterModifier(5);
                pm[0] = true; pm[1] = true; pm[3] = true;
                objDesktop = tShellWindows.InvokeMember("FindWindowSW", BindingFlags.InvokeMethod,
                    null, objShellWindows, aobjArgs, new ParameterModifier[] { pm }, null, null);
                if (objDesktop == null) throw new InvalidOperationException("No desktop shell window.");

                sp = (IServiceProvider)objDesktop;
                Guid sidTop = SID_STopLevelBrowser;
                Guid iidBrowser = typeof(IShellBrowser).GUID;
                object objBrowser;
                sp.QueryService(ref sidTop, ref iidBrowser, out objBrowser);
                browser = (IShellBrowser)objBrowser;

                browser.QueryActiveShellView(out view);
                Guid iidDispatch = IID_IDispatch;
                view.GetItemObject(SVGIO_BACKGROUND, ref iidDispatch, out objBackground);

                objApp = objBackground.GetType().InvokeMember("Application",
                    BindingFlags.GetProperty, null, objBackground, null);
                objApp.GetType().InvokeMember("ShellExecute", BindingFlags.InvokeMethod, null, objApp,
                    new object[] { astrFile, astrArgs, "", "open", SW_SHOWNORMAL });
            } finally {
                Release(objApp); Release(objBackground); Release(view); Release(browser);
                Release(objDesktop); Release(objShellWindows);
            }
        }

        private static void Release(object aobj) {
            if (aobj != null && Marshal.IsComObject(aobj)) {
                try { Marshal.ReleaseComObject(aobj); } catch { }
            }
        }

        // -------------------------------------------------------------------
        // COM interop

        private static readonly Guid CLSID_ShellWindows   = new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
        private static readonly Guid SID_STopLevelBrowser = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
        private static readonly Guid IID_IDispatch        = new Guid("00020400-0000-0000-C000-000000000046");
        private const int CSIDL_DESKTOP     = 0;
        private const int SWC_DESKTOP       = 8;
        private const int SWFO_NEEDDISPATCH = 1;
        private const uint SVGIO_BACKGROUND = 0;
        private const int SW_SHOWNORMAL     = 1;

        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IServiceProvider {
            void QueryService(ref Guid guidService, ref Guid riid,
                              [MarshalAs(UnmanagedType.Interface)] out object ppvObject);
        }

        // Only QueryActiveShellView is called; the earlier slots are declared to keep the vtable.
        [ComImport, Guid("000214E2-0000-0000-C000-000000000046"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellBrowser {
            void GetWindow(out IntPtr phwnd);
            void ContextSensitiveHelp(bool fEnterMode);
            void InsertMenusSB(IntPtr hmenuShared, IntPtr lpMenuWidths);
            void SetMenuSB(IntPtr hmenuShared, IntPtr holemenuRes, IntPtr hwndActiveObject);
            void RemoveMenusSB(IntPtr hmenuShared);
            void SetStatusTextSB(IntPtr pszStatusText);
            void EnableModelessSB(bool fEnable);
            void TranslateAcceleratorSB(IntPtr pmsg, ushort wID);
            void BrowseObject(IntPtr pidl, uint wFlags);
            void GetViewStateStream(uint grfMode, IntPtr ppStrm);
            void GetControlWindow(uint id, out IntPtr phwnd);
            void SendControlMsg(uint id, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr pret);
            void QueryActiveShellView(out IShellView ppshv);
        }

        // Only GetItemObject is called; the earlier slots are declared to keep the vtable.
        [ComImport, Guid("000214E3-0000-0000-C000-000000000046"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellView {
            void GetWindow(out IntPtr phwnd);
            void ContextSensitiveHelp(bool fEnterMode);
            void TranslateAccelerator(IntPtr pmsg);
            void EnableModeless(bool fEnable);
            void UIActivate(uint uState);
            void Refresh();
            void CreateViewWindow(IntPtr psvPrevious, IntPtr pfs, IntPtr psb, IntPtr prcView, out IntPtr phWnd);
            void DestroyViewWindow();
            void GetCurrentInfo(IntPtr pfs);
            void AddPropertySheetPages(uint dwReserved, IntPtr pfn, IntPtr lparam);
            void SaveViewState();
            void SelectItem(IntPtr pidlItem, uint uFlags);
            void GetItemObject(uint uItem, ref Guid riid,
                               [MarshalAs(UnmanagedType.Interface)] out object ppv);
        }
    }
}
