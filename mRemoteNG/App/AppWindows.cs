#region Usings
using System;
using System.Runtime.Versioning;
using mRemoteNG.Resources.Language;
using mRemoteNG.UI;
using mRemoteNG.UI.Forms;
using mRemoteNG.UI.Window;
#endregion

namespace mRemoteNG.App
{
    [SupportedOSPlatform("windows")]
    public static class AppWindows
    {
        private static ActiveDirectoryImportWindow? _adimportForm;
        private static ExternalToolsWindow? _externalappsForm;
        private static PortScanWindow? _portscanForm;
        private static UltraVNCWindow? _ultravncscForm;
        private static ConnectionTreeWindow? _treeForm;

        internal static ConnectionTreeWindow TreeForm
        {
            get => _treeForm ?? (_treeForm = new ConnectionTreeWindow());
            set => _treeForm = value;
        }

        /// <summary>
        /// The connection tree window if one has been built, and null rather than a new one if not.
        /// </summary>
        /// <remarks>
        /// <see cref="TreeForm"/> builds a window as a side effect of being read, which is fine
        /// where the caller wants the window and wrong everywhere else - a caller that only wants
        /// to tell the tree something, from a focus or a message handler, has no business
        /// constructing a window while it does so.
        /// </remarks>
        internal static ConnectionTreeWindow? TreeFormIfBuilt => _treeForm;

        internal static ConfigWindow ConfigForm { get; set; } = new ConfigWindow();
        internal static ErrorAndInfoWindow ErrorsForm { get; set; } = new ErrorAndInfoWindow();
        internal static UpdateWindow UpdateForm { get; set; } = new UpdateWindow();
        internal static SSHTransferWindow SshtransferForm { get; private set; } = new SSHTransferWindow();
        internal static OptionsWindow? OptionsFormWindow { get; private set; }


        public static void Show(WindowType windowType)
        {
            try
            {
                WeifenLuo.WinFormsUI.Docking.DockPanel dockPanel = FrmMain.Default.pnlDock;
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (windowType)
                {
                    case WindowType.ActiveDirectoryImport:
                        if (_adimportForm == null || _adimportForm.IsDisposed)
                            _adimportForm = new ActiveDirectoryImportWindow();
                        _adimportForm.Show(dockPanel);
                        break;
                    case WindowType.Options:
                        if (OptionsFormWindow == null || OptionsFormWindow.IsDisposed)
                            OptionsFormWindow = new OptionsWindow();
                        OptionsFormWindow.SetActivatedPage(Language.StartupExit);
                        // Reload controls from stored settings before every show so that any
                        // edits left over from a previous hide (Tab-X without Apply/OK) are
                        // discarded.  Safe on first call — no-op until FrmOptions is embedded.
                        OptionsFormWindow.RefreshSettings();
                        OptionsFormWindow.Show(dockPanel);
                        break;
                    case WindowType.SSHTransfer:
                        if (SshtransferForm == null || SshtransferForm.IsDisposed)
                            SshtransferForm = new SSHTransferWindow();
                        SshtransferForm.Show(dockPanel);
                        break;
                    case WindowType.Update:
                        if (UpdateForm == null || UpdateForm.IsDisposed)
                            UpdateForm = new UpdateWindow();
                        UpdateForm.Show(dockPanel);
                        break;
                    case WindowType.ExternalApps:
                        if (_externalappsForm == null || _externalappsForm.IsDisposed)
                            _externalappsForm = new ExternalToolsWindow();
                        _externalappsForm.Show(dockPanel);
                        break;
                    case WindowType.PortScan:
                        _portscanForm = new PortScanWindow();
                        _portscanForm.Show(dockPanel);
                        break;
                    case WindowType.UltraVNCSC:
                        if (_ultravncscForm == null || _ultravncscForm.IsDisposed)
                            _ultravncscForm = new UltraVNCWindow();
                        _ultravncscForm.Show(dockPanel);
                        break;
                }

                // The window just shown is a panel, not a connection, and the tab strip is only
                // drawn when at least one of those is open - so this has to be recalculated now.
                // It was not: ShowHidePanelTabs ran once at startup and again only if the user
                // changed the setting that governs it. Open the SSH file transfer or the options
                // on a docking area that held nothing but connections and the panel arrived with
                // no tab, and therefore no close button and no way to get rid of it at all.
                //
                // Deferred for the reason TabsPanelsPage already gives for deferring its own call:
                // changing the document style underneath a window that is in the middle of being
                // shown corrupts it.
                FrmMain.Default.BeginInvoke(
                    new System.Windows.Forms.MethodInvoker(() => FrmMain.Default.ShowHidePanelTabs()));
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace("App.Runtime.Windows.Show() failed.", ex);
            }
        }
    }
}