using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using mRemoteNG.App;
using mRemoteNG.App.Info;
using mRemoteNG.Config;
using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Connection.Protocol.RDP;
using mRemoteNG.Container;
using mRemoteNG.Properties;
using mRemoteNG.Tools;
using mRemoteNG.Tools.Clipboard;
using mRemoteNG.Tree;
using mRemoteNG.Tree.Root;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;
using mRemoteNG.Security;
using mRemoteNG.UI.TaskDialog;

// ReSharper disable UnusedParameter.Local


namespace mRemoteNG.UI.Controls
{
    [SupportedOSPlatform("windows")]
    public sealed class ConnectionContextMenu : ContextMenuStrip
    {
        private ToolStripMenuItem _cMenTreeAddConnection;
        private ToolStripMenuItem _cMenTreeAddFolder;
        private ToolStripMenuItem _cMenTreeAddRoot;
        private ToolStripSeparator _cMenTreeSep1;
        private ToolStripMenuItem _cMenTreeConnect;
        private ToolStripMenuItem _cMenTreeConnectWithOptions;
        private ToolStripMenuItem _cMenTreeConnectWithOptionsConnectToConsoleSession;
        private ToolStripMenuItem _cMenTreeConnectWithOptionsNoCredentials;
        private ToolStripMenuItem _cMenTreeConnectWithOptionsConnectInFullscreen;
        private ToolStripMenuItem _cMenTreeConnectWithOptionsViewOnly;
        private ToolStripMenuItem _cMenTreeDisconnect;
        private ToolStripSeparator _cMenTreeSep2;
        private ToolStripMenuItem _cMenTreeToolsTransferFile;
        private ToolStripMenuItem _cMenTreeToolsSort;
        private ToolStripMenuItem _cMenTreeToolsSortAscending;
        private ToolStripMenuItem _cMenTreeToolsSortDescending;
        private ToolStripSeparator _cMenTreeSep3;
        private ToolStripMenuItem _cMenTreeRename;
        private ToolStripMenuItem _cMenTreeDelete;
        private ToolStripMenuItem _cMenTreeCopyHostname;
        private ToolStripMenuItem _cMenTreeClearCachedRdpCredentials;
        private ToolStripSeparator _cMenTreeSep4;
        private ToolStripMenuItem _cMenTreeMoveUp;
        private ToolStripMenuItem _cMenTreeMoveDown;
        private ToolStripMenuItem _cMenTreeToolsExternalApps;
        private ToolStripMenuItem _cMenTreeDuplicate;
        private ToolStripMenuItem _cMenInheritanceSubMenu;
        private ToolStripMenuItem _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting;
        private ToolStripMenuItem _cMenTreeConnectWithOptionsDontConnectToConsoleSession;
        private ToolStripMenuItem _cMenTreeImport;
        private ToolStripMenuItem _cMenTreeExportFile;
        private ToolStripSeparator _toolStripSeparator1;
        private ToolStripMenuItem _cMenTreeImportFile;
        private ToolStripMenuItem _cMenTreeImportFromRemoteDesktopManager;
        private ToolStripMenuItem _cMenTreeImportActiveDirectory;
        private ToolStripMenuItem _cMenTreeImportPortScan;
        private ToolStripMenuItem _cMenTreeImportPutty;
        private ToolStripMenuItem _cMenTreeApplyInheritanceToChildren;
        private ToolStripMenuItem _cMenTreeApplyDefaultInheritance;
        private readonly ConnectionTree.ConnectionTree _connectionTree;


        public ConnectionContextMenu(ConnectionTree.ConnectionTree connectionTree)
        {
            _connectionTree = connectionTree;
            InitializeComponent();
            ApplyLanguage();
            EnableShortcutKeys();
            Opening += (sender, args) =>
            {
                AddExternalApps();
                if (_connectionTree.SelectedNode == null)
                {
                    args.Cancel = true;
                    return;
                }

                ShowHideMenuItems();
            };

            _focusWatchdog.Tick += OnFocusWatchdogTick;
            Opened += (sender, args) =>
            {
                _focusWhenOpened = NativeMethods.GetFocus();
                _focusWatchdog.Start();
            };
            Closed += (sender, args) => _focusWatchdog.Stop();
        }

        /// <summary>
        /// Watches where the keyboard focus went while the menu is open.
        /// </summary>
        /// <remarks>
        /// A menu closes itself when the focus leaves, which it learns from the messages the
        /// application dispatches. A session does not go through those: the RDP client is an
        /// ActiveX with its own input window and the native SSH terminal is a WebView2, and a
        /// click that lands in either is nothing the menu can see. Right-clicking the tree and
        /// then working in the session left the menu sitting on top of it - reported twice, on
        /// 2026-08-28, for RDP after the first attempt only covered SSH.
        ///
        /// Asking who has the focus is the one question that gets a straight answer whatever
        /// draws the session, which is why it is asked here rather than at each protocol.
        /// </remarks>
        private readonly Timer _focusWatchdog = new() { Interval = 150 };

        /// <summary>
        /// Where the focus was when the menu opened, which is the thing that must not change.
        /// </summary>
        /// <remarks>
        /// A baseline rather than a test against the menu's own handle: a ToolStripDropDown does
        /// not necessarily take the focus when it opens, so "the menu does not have the focus"
        /// would be true from the start and would close the menu the instant it appeared.
        /// </remarks>
        private IntPtr _focusWhenOpened;

        private void OnFocusWatchdogTick(object sender, EventArgs e)
        {
            IntPtr focus = NativeMethods.GetFocus();

            if (focus == _focusWhenOpened) return;

            // A submenu of our own moving the focus about is not the focus leaving.
            if (FromHandle(focus) is ToolStripDropDown) return;

            Close(ToolStripDropDownCloseReason.AppFocusChange);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _focusWatchdog.Tick -= OnFocusWatchdogTick;
                _focusWatchdog.Dispose();
                _unavailableFont?.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Why a menu item is not applicable

        /// <summary>
        /// Marks a menu item as not applicable to the selected node, and says why.
        /// </summary>
        /// <remarks>
        /// Nothing here is ever disabled. A greyed out menu item states that a rule exists and
        /// refuses to say what it is; the rule then lives only in the developer's head, and the
        /// only way to learn it is to ask one. That is what this replaces: the item stays
        /// clickable, looks unavailable, and answers the question when asked - by hovering, or by
        /// clicking it.
        ///
        /// It also puts the rule into the source in words rather than as a bare false. On
        /// 2026-08-29 the file transfer item had been silently unavailable for every SSH
        /// connection in the tree, because its condition listed SSH1 and SSH2 and the native
        /// protocol had never been added to it. Nobody noticed for days. A condition that has to
        /// be given a sentence is a condition somebody reads.
        /// </remarks>
        private void Unavailable(ToolStripItem item, string reason)
        {
            if (item == null) return;

            item.Enabled = true;
            item.Tag = reason;
            item.ToolTipText = reason;

            // Italics rather than a grey, tested on 2026-08-29. The theme renderer from
            // DockPanelSuite paints menu text in its own colours and picks them from Enabled -
            // and nothing here is ever disabled - so a ForeColor set on the item was simply
            // discarded and the entry looked no different at all. The font it cannot discard: it
            // has to measure and draw with the item's own, so the italic comes through whatever
            // the theme does about colour.
            //
            // It also reads better than a grey. Grey says forbidden; italic says this one is not
            // about the thing you have selected, which is what these actually mean.
            item.Font = UnavailableFont;

            // Down into a submenu as well. Clicking a submenu parent only opens it, so a parent
            // marked on its own would lead to a list of entries that look perfectly usable and
            // are not - and there the explanation would never be reached at all.
            if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems)
                foreach (ToolStripItem child in menuItem.DropDownItems)
                    Unavailable(child, reason);
        }

        /// <summary>
        /// The italic used for an entry that does not apply. Built once and kept: a font made per
        /// call would be a new handle every time the menu opens.
        /// </summary>
        private Font _unavailableFont;

        private Font UnavailableFont => _unavailableFont ??= new Font(Font, FontStyle.Italic);

        /// <summary>
        /// Shows why the clicked item does nothing here, and reports whether it was such an item.
        /// </summary>
        private bool Explain(object sender)
        {
            if (sender is not ToolStripItem item || item.Tag is not string reason ||
                string.IsNullOrEmpty(reason))
                return false;

            MessageBox.Show(reason, item.Text?.Replace("&", ""), MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
            return true;
        }

        /// <summary>
        /// Wraps a menu action so that an item marked unavailable explains itself instead of
        /// running.
        /// </summary>
        /// <remarks>
        /// Applied where the handlers are subscribed rather than written into each of the thirty
        /// or so handler bodies. A guard that lives in the wiring cannot be forgotten when a
        /// handler is added later; one that lives in the body can, and would be.
        /// </remarks>
        private EventHandler Guarded(EventHandler action) =>
            (sender, args) =>
            {
                if (Explain(sender)) return;
                if (!ConfirmBulkAction(sender)) return;
                action(sender, args);
            };

        /// <summary>
        /// Asks before an action on a folder reaches every connection inside it.
        /// </summary>
        /// <remarks>
        /// Connecting a folder opens everything in it, subfolders included, and nothing used to
        /// stand between a stray click and a dozen sessions. The count is in the question because
        /// that is the part worth knowing before answering it - the name of the folder does not
        /// tell you how much is in there.
        ///
        /// Asked here rather than in <c>ConnectionInitiator</c> deliberately. The initiator is
        /// also what reopens the previous session at startup, and a confirmation placed there
        /// would interrogate the user on every launch.
        /// </remarks>
        private bool ConfirmBulkAction(object sender)
        {
            if (_connectionTree.SelectedNode is not ContainerInfo container) return true;
            if (sender is not ToolStripItem item) return true;

            bool opens = item == _cMenTreeConnect || _cMenTreeConnectWithOptions.DropDownItems.Contains(item);
            bool closes = item == _cMenTreeDisconnect;

            if (!opens && !closes) return true;

            int count = opens ? CountConnections(container) : CountOpenConnections(container);

            // One is not a crowd: a folder holding a single connection behaves like the connection
            // itself, and asking about it would only train the habit of dismissing the question.
            if (count <= 1) return true;

            string message = string.Format(
                opens ? Language.ConfirmConnectAllInFolder : Language.ConfirmDisconnectAllInFolder,
                count, container.Name);

            return MessageBox.Show(message, GeneralAppInfo.ProductName, MessageBoxButtons.YesNo,
                                   MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
                   == DialogResult.Yes;
        }

        /// <summary>
        /// How many connections in this folder, subfolders included, currently have a session open.
        /// </summary>
        internal static int CountOpenConnections(ContainerInfo container) =>
            container.Children.Sum(child => child is ContainerInfo sub
                                                ? CountOpenConnections(sub)
                                                : child.OpenConnections.Count > 0 ? 1 : 0);

        #endregion

        private void InitializeComponent()
        {
            _cMenTreeConnect = new ToolStripMenuItem();
            _cMenTreeConnectWithOptions = new ToolStripMenuItem();
            _cMenTreeConnectWithOptionsConnectToConsoleSession = new ToolStripMenuItem();
            _cMenTreeConnectWithOptionsDontConnectToConsoleSession = new ToolStripMenuItem();
            _cMenTreeConnectWithOptionsConnectInFullscreen = new ToolStripMenuItem();
            _cMenTreeConnectWithOptionsNoCredentials = new ToolStripMenuItem();
            _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting = new ToolStripMenuItem();
            _cMenTreeConnectWithOptionsViewOnly = new ToolStripMenuItem();
            _cMenTreeDisconnect = new ToolStripMenuItem();
            _cMenTreeSep1 = new ToolStripSeparator();
            _cMenTreeToolsExternalApps = new ToolStripMenuItem();
            _cMenTreeToolsTransferFile = new ToolStripMenuItem();
            _cMenTreeSep2 = new ToolStripSeparator();
            _cMenTreeDuplicate = new ToolStripMenuItem();
            _cMenTreeRename = new ToolStripMenuItem();
            _cMenTreeDelete = new ToolStripMenuItem();
            _cMenTreeCopyHostname = new ToolStripMenuItem();
            _cMenTreeClearCachedRdpCredentials = new ToolStripMenuItem();
            _cMenTreeSep3 = new ToolStripSeparator();
            _cMenTreeImport = new ToolStripMenuItem();
            _cMenTreeImportFile = new ToolStripMenuItem();
            _cMenTreeImportFromRemoteDesktopManager = new ToolStripMenuItem();
            _cMenTreeImportActiveDirectory = new ToolStripMenuItem();
            _cMenTreeImportPortScan = new ToolStripMenuItem();
            _cMenTreeImportPutty = new ToolStripMenuItem();
            _cMenInheritanceSubMenu = new ToolStripMenuItem();
            _cMenTreeApplyInheritanceToChildren = new ToolStripMenuItem();
            _cMenTreeApplyDefaultInheritance = new ToolStripMenuItem();
            _cMenTreeExportFile = new ToolStripMenuItem();
            _cMenTreeSep4 = new ToolStripSeparator();
            _cMenTreeAddConnection = new ToolStripMenuItem();
            _cMenTreeAddFolder = new ToolStripMenuItem();
            _cMenTreeAddRoot = new ToolStripMenuItem();
            _toolStripSeparator1 = new ToolStripSeparator();
            _cMenTreeToolsSort = new ToolStripMenuItem();
            _cMenTreeToolsSortAscending = new ToolStripMenuItem();
            _cMenTreeToolsSortDescending = new ToolStripMenuItem();
            _cMenTreeMoveUp = new ToolStripMenuItem();
            _cMenTreeMoveDown = new ToolStripMenuItem();


            //
            // cMenTree
            //
            Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular,
                                           System.Drawing.GraphicsUnit.Point, 0);
            Items.AddRange(new ToolStripItem[]
            {
                _cMenTreeConnect,
                _cMenTreeConnectWithOptions,
                _cMenTreeDisconnect,
                _cMenTreeSep1,
                _cMenTreeToolsExternalApps,
                _cMenTreeToolsTransferFile,
                _cMenTreeSep2,
                _cMenTreeDuplicate,
                _cMenTreeRename,
                _cMenTreeDelete,
                _cMenTreeCopyHostname,
                _cMenTreeClearCachedRdpCredentials,
                _cMenInheritanceSubMenu,
                _cMenTreeSep3,
                _cMenTreeImport,
                _cMenTreeExportFile,
                _cMenTreeSep4,
                _cMenTreeAddConnection,
                _cMenTreeAddFolder,
                _cMenTreeAddRoot,
                _toolStripSeparator1,
                _cMenTreeToolsSort,
                _cMenTreeMoveUp,
                _cMenTreeMoveDown
            });
            Name = "cMenTree";
            RenderMode = ToolStripRenderMode.Professional;
            Size = new System.Drawing.Size(200, 364);
            //
            // cMenTreeConnect
            //
            _cMenTreeConnect.Image = Properties.Resources.Run_16x;
            _cMenTreeConnect.Name = "_cMenTreeConnect";
            _cMenTreeConnect.Size = new System.Drawing.Size(199, 22);
            _cMenTreeConnect.Text = "Connect";
            _cMenTreeConnect.Click += Guarded(OnConnectClicked);
            //
            // cMenTreeConnectWithOptions
            //
            _cMenTreeConnectWithOptions.DropDownItems.AddRange(new ToolStripItem[]
            {
                _cMenTreeConnectWithOptionsConnectToConsoleSession,
                _cMenTreeConnectWithOptionsDontConnectToConsoleSession,
                _cMenTreeConnectWithOptionsConnectInFullscreen,
                _cMenTreeConnectWithOptionsNoCredentials,
                _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting,
                _cMenTreeConnectWithOptionsViewOnly
            });
            _cMenTreeConnectWithOptions.Name = "_cMenTreeConnectWithOptions";
            _cMenTreeConnectWithOptions.Size = new System.Drawing.Size(199, 22);
            _cMenTreeConnectWithOptions.Text = "Connect (with options)";
            //
            // cMenTreeConnectWithOptionsConnectToConsoleSession
            //
            _cMenTreeConnectWithOptionsConnectToConsoleSession.Name =
                "_cMenTreeConnectWithOptionsConnectToConsoleSession";
            _cMenTreeConnectWithOptionsConnectToConsoleSession.Size = new System.Drawing.Size(245, 22);
            _cMenTreeConnectWithOptionsConnectToConsoleSession.Text = "Connect to console session";
            _cMenTreeConnectWithOptionsConnectToConsoleSession.Click += Guarded(OnConnectToConsoleSessionClicked);
            //
            // cMenTreeConnectWithOptionsDontConnectToConsoleSession
            //
            _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Name =
                "_cMenTreeConnectWithOptionsDontConnectToConsoleSession";
            _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Size = new System.Drawing.Size(245, 22);
            _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Text = "Don\'t connect to console session";
            _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Visible = false;
            _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Click += Guarded(OnDontConnectToConsoleSessionClicked);
            //
            // cMenTreeConnectWithOptionsConnectInFullscreen
            //
            _cMenTreeConnectWithOptionsConnectInFullscreen.Image = Properties.Resources.FullScreen_16x;
            _cMenTreeConnectWithOptionsConnectInFullscreen.Name = "_cMenTreeConnectWithOptionsConnectInFullscreen";
            _cMenTreeConnectWithOptionsConnectInFullscreen.Size = new System.Drawing.Size(245, 22);
            _cMenTreeConnectWithOptionsConnectInFullscreen.Text = "Connect in fullscreen";
            _cMenTreeConnectWithOptionsConnectInFullscreen.Click += Guarded(OnConnectInFullscreenClicked);
            //
            // cMenTreeConnectWithOptionsNoCredentials
            //
            _cMenTreeConnectWithOptionsNoCredentials.Image = Properties.Resources.UniqueKeyError_16x;
            _cMenTreeConnectWithOptionsNoCredentials.Name = "_cMenTreeConnectWithOptionsNoCredentials";
            _cMenTreeConnectWithOptionsNoCredentials.Size = new System.Drawing.Size(245, 22);
            _cMenTreeConnectWithOptionsNoCredentials.Text = "Connect without credentials";
            _cMenTreeConnectWithOptionsNoCredentials.Click += Guarded(OnConnectWithNoCredentialsClick);
            //
            // cMenTreeConnectWithOptionsChoosePanelBeforeConnecting
            //
            _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Image = Properties.Resources.Panel_16x;
            _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Name =
                "_cMenTreeConnectWithOptionsChoosePanelBeforeConnecting";
            _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Size = new System.Drawing.Size(245, 22);
            _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Text = "Choose panel before connecting";
            _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Click += Guarded(OnChoosePanelBeforeConnectingClicked);
            //
            // cMenTreeConnectWithOptionsViewOnly
            //
            _cMenTreeConnectWithOptionsViewOnly.Image = Properties.Resources.Monitor_16x;
            _cMenTreeConnectWithOptionsViewOnly.Name =
                "_cMenTreeConnectWithOptionsViewOnly";
            _cMenTreeConnectWithOptionsViewOnly.Size = new System.Drawing.Size(245, 22);
            _cMenTreeConnectWithOptionsViewOnly.Text = Language.ConnectInViewOnlyMode;
            _cMenTreeConnectWithOptionsViewOnly.Click += Guarded(ConnectWithOptionsViewOnlyOnClick);
            //
            // cMenTreeDisconnect
            //
            _cMenTreeDisconnect.Image = Properties.Resources.Stop_16x;
            _cMenTreeDisconnect.Name = "_cMenTreeDisconnect";
            _cMenTreeDisconnect.Size = new System.Drawing.Size(199, 22);
            _cMenTreeDisconnect.Text = "Disconnect";
            _cMenTreeDisconnect.Click += Guarded(OnDisconnectClicked);
            //
            // cMenTreeSep1
            //
            _cMenTreeSep1.Name = "_cMenTreeSep1";
            _cMenTreeSep1.Size = new System.Drawing.Size(196, 6);
            //
            // cMenTreeToolsExternalApps
            //
            _cMenTreeToolsExternalApps.Image = Properties.Resources.Console_16x;
            _cMenTreeToolsExternalApps.Name = "_cMenTreeToolsExternalApps";
            _cMenTreeToolsExternalApps.Size = new System.Drawing.Size(199, 22);
            _cMenTreeToolsExternalApps.Text = "External Applications";
            //
            // cMenTreeToolsTransferFile
            //
            _cMenTreeToolsTransferFile.Image = Properties.Resources.SyncArrow_16x;
            _cMenTreeToolsTransferFile.Name = "_cMenTreeToolsTransferFile";
            _cMenTreeToolsTransferFile.Size = new System.Drawing.Size(199, 22);
            _cMenTreeToolsTransferFile.Text = "Transfer File (SSH)";
            _cMenTreeToolsTransferFile.Click += Guarded(OnTransferFileClicked);
            //
            // cMenTreeSep2
            //
            _cMenTreeSep2.Name = "_cMenTreeSep2";
            _cMenTreeSep2.Size = new System.Drawing.Size(196, 6);
            //
            // cMenTreeDuplicate
            //
            _cMenTreeDuplicate.Image = Properties.Resources.Copy_16x;
            _cMenTreeDuplicate.Name = "_cMenTreeDuplicate";
            _cMenTreeDuplicate.Size = new System.Drawing.Size(199, 22);
            _cMenTreeDuplicate.Text = "Duplicate";
            _cMenTreeDuplicate.Click += Guarded(OnDuplicateClicked);
            //
            // cMenTreeRename
            //
            _cMenTreeRename.Image = Properties.Resources.Rename_16x;
            _cMenTreeRename.Name = "_cMenTreeRename";
            _cMenTreeRename.Size = new System.Drawing.Size(199, 22);
            _cMenTreeRename.Text = "Rename";
            _cMenTreeRename.Click += Guarded(OnRenameClicked);
            //
            // cMenTreeDelete
            //
            _cMenTreeDelete.Image = Properties.Resources.Close_16x;
            _cMenTreeDelete.Name = "_cMenTreeDelete";
            _cMenTreeDelete.Size = new System.Drawing.Size(199, 22);
            _cMenTreeDelete.Text = "Delete";
            _cMenTreeDelete.Click += Guarded(OnDeleteClicked);
            //
            // cMenTreeCopyHostname
            //
            _cMenTreeCopyHostname.Name = "_cMenTreeCopyHostname";
            _cMenTreeCopyHostname.Size = new System.Drawing.Size(199, 22);
            _cMenTreeCopyHostname.Text = "Copy Hostname";
            _cMenTreeCopyHostname.Click += Guarded(OnCopyHostnameClicked);
            //
            // cMenTreeClearCachedRdpCredentials
            //
            _cMenTreeClearCachedRdpCredentials.Name = "_cMenTreeClearCachedRdpCredentials";
            _cMenTreeClearCachedRdpCredentials.Size = new System.Drawing.Size(199, 22);
            _cMenTreeClearCachedRdpCredentials.Text = "Clear Cached RDP Credentials";
            _cMenTreeClearCachedRdpCredentials.ToolTipText =
                "If RDP connection fails with an authentication error, Windows may be substituting " +
                "a stale cached credential. Use this to delete the TERMSRV/<hostname> entry from " +
                "the Windows Credential Manager so the credentials configured on this connection " +
                "are sent unchanged on the next attempt.";
            _cMenTreeClearCachedRdpCredentials.Click += Guarded(OnClearCachedRdpCredentialsClicked);
            //
            // cMenTreeSep3
            //
            _cMenTreeSep3.Name = "_cMenTreeSep3";
            _cMenTreeSep3.Size = new System.Drawing.Size(196, 6);
            //
            // cMenTreeImport
            //
            _cMenTreeImport.DropDownItems.AddRange(new ToolStripItem[]
            {
                _cMenTreeImportFile,
                _cMenTreeImportFromRemoteDesktopManager,
                _cMenTreeImportActiveDirectory,
                _cMenTreeImportPutty,
                _cMenTreeImportPortScan
            });
            _cMenTreeImport.Name = "_cMenTreeImport";
            _cMenTreeImport.Size = new System.Drawing.Size(199, 22);
            _cMenTreeImport.Text = "&Import";
            //
            // cMenTreeImportFile
            //
            _cMenTreeImportFile.Name = "_cMenTreeImportFile";
            _cMenTreeImportFile.Size = new System.Drawing.Size(226, 22);
            _cMenTreeImportFile.Text = "Import from &File...";
            _cMenTreeImportFile.Click += Guarded(OnImportFileClicked);

            // cMenTreeImportFromRemoteDesktopManager
            _cMenTreeImportFromRemoteDesktopManager.Name = "_cMenTreeImportFromRemoteDesktopManager";
            _cMenTreeImportFromRemoteDesktopManager.Size = new System.Drawing.Size(226, 22);
            _cMenTreeImportFromRemoteDesktopManager.Text = "Import from &Remote Desktop Manager";
            _cMenTreeImportFromRemoteDesktopManager.Click += Guarded(OnImportRemoteDesktopManagerClicked);
            //
            // cMenTreeImportActiveDirectory
            //
            _cMenTreeImportActiveDirectory.Name = "_cMenTreeImportActiveDirectory";
            _cMenTreeImportActiveDirectory.Size = new System.Drawing.Size(226, 22);
            _cMenTreeImportActiveDirectory.Text = "Import from &Active Directory...";
            _cMenTreeImportActiveDirectory.Click += Guarded(OnImportActiveDirectoryClicked);
            //
            // cMenTreeImportPortScan
            //
            _cMenTreeImportPortScan.Name = "_cMenTreeImportPortScan";
            _cMenTreeImportPortScan.Size = new System.Drawing.Size(226, 22);
            _cMenTreeImportPortScan.Text = "Import from &Port Scan...";
            _cMenTreeImportPortScan.Click += Guarded(OnImportPortScanClicked);
            //
            // cMenTreeImportPutty
            //
            _cMenTreeImportPutty.Name = "_cMenTreeImportPutty";
            _cMenTreeImportPutty.Size = new System.Drawing.Size(226, 22);
            _cMenTreeImportPutty.Text = "Import from &Putty...";
            _cMenTreeImportPutty.Click += Guarded(OnImportPuttyClicked);
            //
            // cMenTreeExportFile
            //
            _cMenTreeExportFile.Name = "_cMenTreeExportFile";
            _cMenTreeExportFile.Size = new System.Drawing.Size(199, 22);
            _cMenTreeExportFile.Text = "&Export to File...";
            _cMenTreeExportFile.Click += Guarded(OnExportFileClicked);
            //
            // cMenTreeSep4
            //
            _cMenTreeSep4.Name = "_cMenTreeSep4";
            _cMenTreeSep4.Size = new System.Drawing.Size(196, 6);
            //
            // cMenTreeAddConnection
            //
            _cMenTreeAddConnection.Image = Properties.Resources.AddItem_16x;
            _cMenTreeAddConnection.Name = "_cMenTreeAddConnection";
            _cMenTreeAddConnection.Size = new System.Drawing.Size(199, 22);
            _cMenTreeAddConnection.Text = "New Connection";
            _cMenTreeAddConnection.Click += Guarded(OnAddConnectionClicked);
            //
            // cMenTreeAddFolder
            //
            _cMenTreeAddFolder.Image = Properties.Resources.AddFolder_16x;
            _cMenTreeAddFolder.Name = "_cMenTreeAddFolder";
            _cMenTreeAddFolder.Size = new System.Drawing.Size(199, 22);
            _cMenTreeAddFolder.Text = "New Folder";
            _cMenTreeAddFolder.Click += Guarded(OnAddFolderClicked);
            //
            // cMenTreeAddRoot
            //
            _cMenTreeAddRoot.Image = Properties.Resources.ASPWebSite_16x;
            _cMenTreeAddRoot.Name = "_cMenTreeAddRoot";
            _cMenTreeAddRoot.Size = new System.Drawing.Size(199, 22);
            _cMenTreeAddRoot.Text = "Add Root";
            _cMenTreeAddRoot.Click += Guarded(OnAddRootClicked);
            //
            // ToolStripSeparator1
            //
            _toolStripSeparator1.Name = "_toolStripSeparator1";
            _toolStripSeparator1.Size = new System.Drawing.Size(196, 6);
            //
            // cMenTreeToolsSort
            //
            _cMenTreeToolsSort.DropDownItems.AddRange(new ToolStripItem[]
            {
                _cMenTreeToolsSortAscending,
                _cMenTreeToolsSortDescending
            });
            _cMenTreeToolsSort.Name = "_cMenTreeToolsSort";
            _cMenTreeToolsSort.Size = new System.Drawing.Size(199, 22);
            _cMenTreeToolsSort.Text = "Sort";
            //
            // cMenTreeToolsSortAscending
            //
            _cMenTreeToolsSortAscending.Image = Properties.Resources.SortAscending_16x;
            _cMenTreeToolsSortAscending.Name = "_cMenTreeToolsSortAscending";
            _cMenTreeToolsSortAscending.Size = new System.Drawing.Size(161, 22);
            _cMenTreeToolsSortAscending.Text = "Ascending (A-Z)";
            _cMenTreeToolsSortAscending.Click += Guarded(OnSortAscendingClicked);
            //
            // cMenTreeToolsSortDescending
            //
            _cMenTreeToolsSortDescending.Image = Properties.Resources.SortDescending_16x;
            _cMenTreeToolsSortDescending.Name = "_cMenTreeToolsSortDescending";
            _cMenTreeToolsSortDescending.Size = new System.Drawing.Size(161, 22);
            _cMenTreeToolsSortDescending.Text = "Descending (Z-A)";
            _cMenTreeToolsSortDescending.Click += Guarded(OnSortDescendingClicked);
            //
            // cMenTreeMoveUp
            //
            _cMenTreeMoveUp.Image = Properties.Resources.GlyphUp_16x;
            _cMenTreeMoveUp.Name = "_cMenTreeMoveUp";
            _cMenTreeMoveUp.Size = new System.Drawing.Size(199, 22);
            _cMenTreeMoveUp.Text = "Move up";
            _cMenTreeMoveUp.Click += Guarded(OnMoveUpClicked);
            //
            // cMenTreeMoveDown
            //
            _cMenTreeMoveDown.Image = Properties.Resources.GlyphDown_16x;
            _cMenTreeMoveDown.Name = "_cMenTreeMoveDown";
            _cMenTreeMoveDown.Size = new System.Drawing.Size(199, 22);
            _cMenTreeMoveDown.Text = "Move down";
            _cMenTreeMoveDown.Click += Guarded(OnMoveDownClicked);
            //
            // cMenEditSubMenu
            //
            _cMenInheritanceSubMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                _cMenTreeApplyInheritanceToChildren,
                _cMenTreeApplyDefaultInheritance
            });
            _cMenInheritanceSubMenu.Name = "_cMenInheritanceSubMenu";
            _cMenInheritanceSubMenu.Size = new System.Drawing.Size(199, 22);
            _cMenInheritanceSubMenu.Text = "Inheritance";
            //
            // _cMenTreeApplyInheritanceToChildren
            //
            _cMenTreeApplyInheritanceToChildren.Name = "_cMenTreeApplyInheritanceToChildren";
            _cMenTreeApplyInheritanceToChildren.Size = new System.Drawing.Size(199, 22);
            _cMenTreeApplyInheritanceToChildren.Text = "Apply inheritance to children";
            _cMenTreeApplyInheritanceToChildren.Click += Guarded(OnApplyInheritanceToChildrenClicked);
            //
            // _cMenTreeApplyDefaultInheritance
            //
            _cMenTreeApplyDefaultInheritance.Name = "_cMenTreeApplyDefaultInheritance";
            _cMenTreeApplyDefaultInheritance.Size = new System.Drawing.Size(199, 22);
            _cMenTreeApplyDefaultInheritance.Text = "Apply default inheritance";
            _cMenTreeApplyDefaultInheritance.Click += Guarded(OnApplyDefaultInheritanceClicked);
        }


        private void ApplyLanguage()
        {
            _cMenTreeConnect.Text = Language.Connect;
            _cMenTreeConnectWithOptions.Text = Language.ConnectWithOptions;
            _cMenTreeConnectWithOptionsConnectToConsoleSession.Text = Language.ConnectToConsoleSession;
            _cMenTreeConnectWithOptionsDontConnectToConsoleSession.Text = Language.DontConnectToConsoleSession;
            _cMenTreeConnectWithOptionsConnectInFullscreen.Text = Language.ConnectInFullscreen;
            _cMenTreeConnectWithOptionsNoCredentials.Text = Language.ConnectNoCredentials;
            _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting.Text = Language.ChoosePanelBeforeConnecting;
            _cMenTreeConnectWithOptionsViewOnly.Text = Language.ConnectInViewOnlyMode;
            _cMenTreeDisconnect.Text = Language.Disconnect;

            _cMenTreeToolsExternalApps.Text = Language._Tools;
            _cMenTreeToolsTransferFile.Text = Language.TransferFile;

            _cMenTreeDuplicate.Text = Language.Duplicate;
            _cMenTreeRename.Text = Language.Rename;
            _cMenTreeDelete.Text = Language.Delete;
            _cMenTreeCopyHostname.Text = Language.CopyHostname;
            _cMenTreeClearCachedRdpCredentials.Text = Language.ClearCachedRdpCredentials;
            _cMenTreeClearCachedRdpCredentials.ToolTipText = Language.PropertyDescriptionClearCachedRdpCredentials;

            _cMenTreeImport.Text = Language._Import;
            _cMenTreeImportFile.Text = Language.ImportFromFile;
            _cMenTreeImportActiveDirectory.Text = Language.ImportAD;
            _cMenTreeImportPortScan.Text = Language.ImportPortScan;
            _cMenTreeExportFile.Text = Language._ExportToFile;

            _cMenTreeAddConnection.Text = Language.NewConnection;
            _cMenTreeAddFolder.Text = Language.NewFolder;
            _cMenTreeAddRoot.Text = Language.AddRoot;

            _cMenTreeToolsSort.Text = Language.Sort;
            _cMenTreeToolsSortAscending.Text = Language.SortAsc;
            _cMenTreeToolsSortDescending.Text = Language.SortDesc;
            _cMenTreeMoveUp.Text = Language.MoveUp;
            _cMenTreeMoveDown.Text = Language.MoveDown;

            _cMenInheritanceSubMenu.Text = Language.Inheritance;
            _cMenTreeApplyInheritanceToChildren.Text = Language.ApplyInheritanceToChildren;
            _cMenTreeApplyDefaultInheritance.Text = Language.ApplyDefaultInheritance;
        }

        internal void ShowHideMenuItems()
        {
            try
            {
                Enabled = true;
                EnableMenuItemsRecursive(Items);

                // Put back the labels and the one standing tooltip that the reset above clears.
                // The folder branch changes these three to say that it acts on everything inside,
                // and the next node selected must not inherit that wording.
                _cMenTreeConnect.Text = Language.Connect;
                _cMenTreeConnectWithOptions.Text = Language.ConnectWithOptions;
                _cMenTreeDisconnect.Text = Language.Disconnect;
                _cMenTreeClearCachedRdpCredentials.ToolTipText =
                    Language.PropertyDescriptionClearCachedRdpCredentials;

                if (_connectionTree.SelectedNode is RootPuttySessionsNodeInfo)
                {
                    ShowHideMenuItemsForRootPuttyNode();
                }
                else if (_connectionTree.SelectedNode is RootNodeInfo)
                {
                    ShowHideMenuItemsForRootConnectionNode();
                }
                else if (_connectionTree.SelectedNode is ContainerInfo containerInfo)
                {
                    ShowHideMenuItemsForContainer(containerInfo);
                }
                else if (_connectionTree.SelectedNode is PuttySessionInfo puttyNode)
                {
                    ShowHideMenuItemsForPuttyNode(puttyNode);
                }
                else
                {
                    ShowHideMenuItemsForConnectionNode(_connectionTree.SelectedNode);
                }

                _cMenInheritanceSubMenu.Enabled = _cMenInheritanceSubMenu.DropDownItems
                    .OfType<ToolStripMenuItem>().Any(i => i.Enabled);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace(
                                                                "ShowHideMenuItems (UI.Controls.ConnectionContextMenu) failed",
                                                                ex);
            }
        }

        internal void ShowHideMenuItemsForRootPuttyNode()
        {
            string reason = Language.MenuReasonPuttyRootNode;

            foreach (ToolStripItem item in new ToolStripItem[]
                     {
                         _cMenTreeAddConnection, _cMenTreeAddFolder, _cMenTreeAddRoot,
                         _cMenTreeConnect, _cMenTreeConnectWithOptions, _cMenTreeDisconnect,
                         _cMenTreeToolsTransferFile, _cMenTreeToolsSort, _cMenTreeToolsExternalApps,
                         _cMenTreeDuplicate, _cMenTreeImport, _cMenTreeExportFile, _cMenTreeRename,
                         _cMenTreeDelete, _cMenTreeMoveUp, _cMenTreeMoveDown,
                         _cMenTreeConnectWithOptionsViewOnly, _cMenTreeApplyInheritanceToChildren,
                         _cMenTreeApplyDefaultInheritance, _cMenTreeCopyHostname,
                         _cMenTreeClearCachedRdpCredentials
                     })
                Unavailable(item, reason);
        }

        internal void ShowHideMenuItemsForRootConnectionNode()
        {
            string notAConnection = Language.MenuReasonRootNode;

            foreach (ToolStripItem item in new ToolStripItem[]
                     {
                         _cMenTreeConnect, _cMenTreeConnectWithOptions,
                         _cMenTreeConnectWithOptionsConnectInFullscreen,
                         _cMenTreeConnectWithOptionsConnectToConsoleSession,
                         _cMenTreeConnectWithOptionsChoosePanelBeforeConnecting,
                         _cMenTreeDisconnect, _cMenTreeToolsTransferFile,
                         _cMenTreeToolsExternalApps, _cMenTreeDuplicate,
                         _cMenTreeConnectWithOptionsViewOnly,
                         _cMenTreeApplyInheritanceToChildren, _cMenTreeApplyDefaultInheritance
                     })
                Unavailable(item, notAConnection);

            string immovable = Language.MenuReasonRootNodeImmovable;
            Unavailable(_cMenTreeDelete, immovable);
            Unavailable(_cMenTreeMoveUp, immovable);
            Unavailable(_cMenTreeMoveDown, immovable);
        }

        internal void ShowHideMenuItemsForContainer(ContainerInfo containerInfo)
        {
            string singleSessionOnly = Language.MenuReasonSingleSessionOnly;

            Unavailable(_cMenTreeConnectWithOptionsConnectInFullscreen, singleSessionOnly);
            Unavailable(_cMenTreeConnectWithOptionsConnectToConsoleSession, singleSessionOnly);
            Unavailable(_cMenTreeConnectWithOptionsViewOnly, singleSessionOnly);
            Unavailable(_cMenTreeToolsTransferFile, singleSessionOnly);

            bool hasOpenConnections = containerInfo.Children.Any(child => child.OpenConnections.Count > 0);
            if (!hasOpenConnections)
                Unavailable(_cMenTreeDisconnect, Language.MenuReasonFolderNothingOpen);

            // A folder connects and disconnects everything inside it, subfolders included. Leaving
            // the label as plain "Connect" reads exactly as it does on a single connection, which
            // is how one stray click opens a dozen sessions.
            _cMenTreeConnect.Text = string.Format(Language.ConnectAllInFolder, CountConnections(containerInfo));
            _cMenTreeConnectWithOptions.Text = Language.ConnectAllInFolderWithOptions;
            _cMenTreeDisconnect.Text = Language.DisconnectAllInFolder;
        }

        /// <summary>
        /// How many connections a folder would open, counting the folders inside it too.
        /// </summary>
        internal static int CountConnections(ContainerInfo container) =>
            container.Children.Sum(child => child is ContainerInfo sub ? CountConnections(sub) : 1);

        internal void ShowHideMenuItemsForPuttyNode(PuttySessionInfo connectionInfo)
        {
            string readOnly = Language.MenuReasonPuttySession;

            foreach (ToolStripItem item in new ToolStripItem[]
                     {
                         _cMenTreeAddConnection, _cMenTreeAddFolder, _cMenTreeAddRoot,
                         _cMenTreeToolsSort, _cMenTreeDuplicate, _cMenTreeRename, _cMenTreeDelete,
                         _cMenTreeMoveUp, _cMenTreeMoveDown, _cMenTreeImport, _cMenTreeExportFile,
                         _cMenTreeApplyInheritanceToChildren, _cMenTreeApplyDefaultInheritance
                     })
                Unavailable(item, readOnly);

            if (connectionInfo.OpenConnections.Count == 0)
                Unavailable(_cMenTreeDisconnect, Language.MenuReasonNothingOpen);

            if (!SupportsFileTransfer(connectionInfo.Protocol))
                Unavailable(_cMenTreeToolsTransferFile,
                            string.Format(Language.MenuReasonTransferNeedsSsh, connectionInfo.Protocol));

            string rdpOnly = string.Format(Language.MenuReasonRdpOnly, connectionInfo.Protocol);
            Unavailable(_cMenTreeConnectWithOptionsConnectInFullscreen, rdpOnly);
            Unavailable(_cMenTreeConnectWithOptionsConnectToConsoleSession, rdpOnly);
            Unavailable(_cMenTreeConnectWithOptionsViewOnly,
                        string.Format(Language.MenuReasonRdpVncOnly, connectionInfo.Protocol));
        }

        internal void ShowHideMenuItemsForConnectionNode(ConnectionInfo connectionInfo)
        {
            if (connectionInfo.OpenConnections.Count == 0)
            {
                Unavailable(_cMenTreeDisconnect, Language.MenuReasonNothingOpen);
            }
            else
            {
                // This item passes DoNotJump, so on a connection that is already open it starts a
                // second session rather than going to the first - the opposite of what a double
                // click does, and nothing in the word "Connect" said so. It says so now, and only
                // when there is in fact something to open a second one alongside.
                _cMenTreeConnect.Text = Language.ConnectNewSession;
            }

            if (!SupportsFileTransfer(connectionInfo.Protocol))
                Unavailable(_cMenTreeToolsTransferFile,
                            string.Format(Language.MenuReasonTransferNeedsSsh, connectionInfo.Protocol));

            if (connectionInfo.Protocol != ProtocolType.RDP)
            {
                string rdpOnly = string.Format(Language.MenuReasonRdpOnly, connectionInfo.Protocol);
                Unavailable(_cMenTreeConnectWithOptionsConnectInFullscreen, rdpOnly);
                Unavailable(_cMenTreeConnectWithOptionsConnectToConsoleSession, rdpOnly);
                Unavailable(_cMenTreeClearCachedRdpCredentials, rdpOnly);
            }

            if (connectionInfo.Protocol == ProtocolType.IntApp)
                Unavailable(_cMenTreeConnectWithOptionsNoCredentials,
                            Language.MenuReasonNotForExternalApp);

            if (connectionInfo.Protocol != ProtocolType.RDP && connectionInfo.Protocol != ProtocolType.VNC)
                Unavailable(_cMenTreeConnectWithOptionsViewOnly,
                            string.Format(Language.MenuReasonRdpVncOnly, connectionInfo.Protocol));

            Unavailable(_cMenTreeApplyInheritanceToChildren, Language.MenuReasonNoChildren);
        }

        /// <summary>
        /// Whether the file transfer window can serve this protocol.
        /// </summary>
        /// <remarks>
        /// Every SSH protocol, the native one included - it was left off this test when SSHNative
        /// was added, which quietly took file transfer away from every SSH connection in the tree
        /// the moment they were moved over. The transfer does not care which protocol the tab
        /// uses: SecureTransfer opens its own SSH.NET connection from host, port, user and
        /// password.
        /// </remarks>
        private static bool SupportsFileTransfer(ProtocolType protocol) =>
            protocol is ProtocolType.SSH1 or ProtocolType.SSH2 or ProtocolType.SSHNative;

        internal void DisableShortcutKeys()
        {
            _cMenTreeConnect.ShortcutKeys = Keys.None;
            _cMenTreeDuplicate.ShortcutKeys = Keys.None;
            _cMenTreeRename.ShortcutKeys = Keys.None;
            _cMenTreeDelete.ShortcutKeys = Keys.None;
            _cMenTreeMoveUp.ShortcutKeys = Keys.None;
            _cMenTreeMoveDown.ShortcutKeys = Keys.None;
        }

        internal void EnableShortcutKeys()
        {
            _cMenTreeConnect.ShortcutKeys = ((Keys.Control | Keys.Shift) | Keys.C);
            _cMenTreeDuplicate.ShortcutKeys = Keys.Control | Keys.D;
            _cMenTreeRename.ShortcutKeys = Keys.F2;
            _cMenTreeDelete.ShortcutKeys = Keys.Delete;
            _cMenTreeMoveUp.ShortcutKeys = Keys.Control | Keys.Up;
            _cMenTreeMoveDown.ShortcutKeys = Keys.Control | Keys.Down;
        }

        /// <summary>
        /// Returns every item to plain, applicable, unexplained - the state the per node rules
        /// then depart from.
        /// </summary>
        /// <remarks>
        /// The reason and the grey have to be cleared as well as the enabling. The menu is one
        /// object reused for every node in the tree, so a reason left behind by the last node
        /// would be shown for the next one, about a rule that no longer applies.
        /// </remarks>
        private static void EnableMenuItemsRecursive(ToolStripItemCollection items, bool enable = true,
                                                    Font ownerFont = null)
        {
            ownerFont ??= items.Count > 0 ? items[0].Owner?.Font : null;

            foreach (ToolStripItem item in items)
            {
                ToolStripMenuItem menuItem = item as ToolStripMenuItem;
                if (menuItem == null)
                {
                    continue;
                }

                menuItem.Enabled = enable;
                menuItem.Tag = null;
                menuItem.ToolTipText = null;
                menuItem.Font = ownerFont;

                if (menuItem.HasDropDownItems)
                {
                    EnableMenuItemsRecursive(menuItem.DropDownItems, enable, ownerFont);
                }
            }
        }

        private void AddExternalApps()
        {
            try
            {
                ResetExternalAppMenu();

                foreach (ExternalTool extA in Runtime.ExternalToolsService.ExternalTools)
                {
                    ToolStripMenuItem menuItem = new()
                    {
                        Text = extA.DisplayName,
                        Tag = extA,
                        Image = extA.Image
                    };

                    menuItem.Click += Guarded(OnExternalToolClicked);
                    _cMenTreeToolsExternalApps.DropDownItems.Add(menuItem);
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace(
                                                                "cMenTreeTools_DropDownOpening failed (UI.Window.ConnectionTreeWindow)",
                                                                ex);
            }
        }

        private void ResetExternalAppMenu()
        {
            if (_cMenTreeToolsExternalApps.DropDownItems.Count <= 0) return;
            for (int i = _cMenTreeToolsExternalApps.DropDownItems.Count - 1; i >= 0; i--)
                _cMenTreeToolsExternalApps.DropDownItems[i].Dispose();

            _cMenTreeToolsExternalApps.DropDownItems.Clear();
        }

        #region Click handlers

        private void OnConnectClicked(object sender, EventArgs e)
        {
            ContainerInfo selectedNodeAsContainer = _connectionTree.SelectedNode as ContainerInfo;
            if (selectedNodeAsContainer != null)
                Runtime.ConnectionInitiator.OpenConnection(selectedNodeAsContainer, ConnectionInfo.Force.DoNotJump);
            else
                Runtime.ConnectionInitiator.OpenConnection(_connectionTree.SelectedNode, ConnectionInfo.Force.DoNotJump);
        }

        private void OnConnectToConsoleSessionClicked(object sender, EventArgs e)
        {
            ContainerInfo selectedNodeAsContainer = _connectionTree.SelectedNode as ContainerInfo;
            if (selectedNodeAsContainer != null)
                Runtime.ConnectionInitiator.OpenConnection(selectedNodeAsContainer,
                                                           ConnectionInfo.Force.UseConsoleSession |
                                                           ConnectionInfo.Force.DoNotJump);
            else
                Runtime.ConnectionInitiator.OpenConnection(_connectionTree.SelectedNode,
                                                           ConnectionInfo.Force.UseConsoleSession |
                                                           ConnectionInfo.Force.DoNotJump);

        }

        private void OnDontConnectToConsoleSessionClicked(object sender, EventArgs e)
        {
            ContainerInfo selectedNodeAsContainer = _connectionTree.SelectedNode as ContainerInfo;
            if (selectedNodeAsContainer != null)
                Runtime.ConnectionInitiator.OpenConnection(selectedNodeAsContainer,
                                                           ConnectionInfo.Force.DontUseConsoleSession |
                                                           ConnectionInfo.Force.DoNotJump);
            else
                Runtime.ConnectionInitiator.OpenConnection(_connectionTree.SelectedNode,
                                                           ConnectionInfo.Force.DontUseConsoleSession |
                                                           ConnectionInfo.Force.DoNotJump);
        }

        private void OnConnectInFullscreenClicked(object sender, EventArgs e)
        {
            ContainerInfo selectedNodeAsContainer = _connectionTree.SelectedNode as ContainerInfo;
            if (selectedNodeAsContainer != null)
                Runtime.ConnectionInitiator.OpenConnection(selectedNodeAsContainer,
                                                           ConnectionInfo.Force.Fullscreen | ConnectionInfo.Force.DoNotJump);
            else
                Runtime.ConnectionInitiator.OpenConnection(_connectionTree.SelectedNode,
                                                           ConnectionInfo.Force.Fullscreen | ConnectionInfo.Force.DoNotJump);
        }

        private void OnConnectWithNoCredentialsClick(object sender, EventArgs e)
        {
            ContainerInfo selectedNodeAsContainer = _connectionTree.SelectedNode as ContainerInfo;
            if (selectedNodeAsContainer != null)
                Runtime.ConnectionInitiator.OpenConnection(selectedNodeAsContainer, ConnectionInfo.Force.NoCredentials);
            else
                Runtime.ConnectionInitiator.OpenConnection(_connectionTree.SelectedNode, ConnectionInfo.Force.NoCredentials);
        }

        private void OnChoosePanelBeforeConnectingClicked(object sender, EventArgs e)
        {
            ContainerInfo selectedNodeAsContainer = _connectionTree.SelectedNode as ContainerInfo;
            if (selectedNodeAsContainer != null)
                Runtime.ConnectionInitiator.OpenConnection(selectedNodeAsContainer,
                                                           ConnectionInfo.Force.OverridePanel |
                                                           ConnectionInfo.Force.DoNotJump);
            else
                Runtime.ConnectionInitiator.OpenConnection(_connectionTree.SelectedNode,
                                                           ConnectionInfo.Force.OverridePanel |
                                                           ConnectionInfo.Force.DoNotJump);
        }

        private void ConnectWithOptionsViewOnlyOnClick(object sender, EventArgs e)
        {
            ConnectionInfo connectionTarget = _connectionTree.SelectedNode as ContainerInfo
                                   ?? _connectionTree.SelectedNode;
            Runtime.ConnectionInitiator.OpenConnection(connectionTarget, ConnectionInfo.Force.ViewOnly);
        }

        private void OnDisconnectClicked(object sender, EventArgs e)
        {
            DisconnectConnection(_connectionTree.SelectedNode);
        }

        public void DisconnectConnection(ConnectionInfo connectionInfo)
        {
            try
            {
                if (connectionInfo == null) return;
                
                // Check if confirmation is needed based on settings
                if (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.All)
                {
                    string confirmMessage = string.Format(Language.ConfirmDisconnectConnection, connectionInfo.Name);
                    DialogResult result = CTaskDialog.MessageBox(this, GeneralAppInfo.ProductName,
                                                        confirmMessage, "", "", "",
                                                        Language.CheckboxDoNotShowThisMessageAgain,
                                                        ETaskDialogButtons.YesNo, ESysIcons.Question,
                                                        ESysIcons.Question);
                    if (CTaskDialog.VerificationChecked)
                    {
                        Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseEnum.Never;
                        Settings.Default.Save();
                    }

                    if (result == DialogResult.No)
                    {
                        return; // User cancelled the disconnect
                    }
                }
                
                ContainerInfo nodeAsContainer = connectionInfo as ContainerInfo;
                if (nodeAsContainer != null)
                {
                    foreach (ConnectionInfo child in nodeAsContainer.Children)
                    {
                        for (int i = 0; i <= child.OpenConnections.Count - 1; i++)
                        {
                            child.OpenConnections[i].Disconnect();
                        }
                    }
                }
                else
                {
                    for (int i = 0; i <= connectionInfo.OpenConnections.Count - 1; i++)
                    {
                        connectionInfo.OpenConnections[i].Disconnect();
                    }
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace(
                                                                "DisconnectConnection (UI.Window.ConnectionTreeWindow) failed",
                                                                ex);
            }
        }

        private void OnTransferFileClicked(object sender, EventArgs e)
        {
            SshTransferFile();
        }

        public void SshTransferFile()
        {
            try
            {
                AppWindows.Show(WindowType.SSHTransfer);
                AppWindows.SshtransferForm.Hostname = _connectionTree.SelectedNode.Hostname;
                AppWindows.SshtransferForm.Username = _connectionTree.SelectedNode.Username;
                //App.Windows.SshtransferForm.Password = _connectionTree.SelectedNode.Password.ConvertToUnsecureString();
                AppWindows.SshtransferForm.Password = _connectionTree.SelectedNode.Password;
                AppWindows.SshtransferForm.Port = Convert.ToString(_connectionTree.SelectedNode.Port);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace(
                                                                "SSHTransferFile (UI.Window.ConnectionTreeWindow) failed",
                                                                ex);
            }
        }

        private void OnDuplicateClicked(object sender, EventArgs e)
        {
            _connectionTree.DuplicateSelectedNode();
        }

        private void OnRenameClicked(object sender, EventArgs e)
        {
            _connectionTree.RenameSelectedNode();
        }

        private void OnDeleteClicked(object sender, EventArgs e)
        {
            _connectionTree.DeleteSelectedNode();
        }

        private void OnCopyHostnameClicked(object sender, EventArgs e)
        {
            _connectionTree.CopyHostnameSelectedNode(new WindowsClipboard());
        }

        private void OnClearCachedRdpCredentialsClicked(object sender, EventArgs e)
        {
            ConnectionInfo selected = _connectionTree.SelectedNode;
            if (selected == null || selected.Protocol != ProtocolType.RDP) return;
            string hostname = selected.Hostname;
            if (string.IsNullOrWhiteSpace(hostname)) return;

            string target = "TERMSRV/" + hostname;

            // Single-dialog confirmation showing both the explanation and the target.
            string mainInstruction = string.Format(Language.ConfirmDeleteCachedRdpCredential, target);
            DialogResult confirm = CTaskDialog.MessageBox(
                this,
                Language.ClearCachedRdpCredentials,
                mainInstruction,
                Language.PropertyDescriptionClearCachedRdpCredentials,
                "",
                "",
                "",
                ETaskDialogButtons.YesNo,
                ESysIcons.Question,
                ESysIcons.Question);

            if (confirm != DialogResult.Yes) return;

            ClearCachedCredentialsResult outcome = RdpCredentialCacheCleaner.ClearCachedCredentials(hostname);
            switch (outcome)
            {
                case ClearCachedCredentialsResult.Deleted:
                    CTaskDialog.MessageBox(
                        this,
                        Language.ClearCachedRdpCredentials,
                        string.Format(Language.ClearedCachedRdpCredentials, target),
                        "", "", "", "",
                        ETaskDialogButtons.Ok, ESysIcons.Information, ESysIcons.Information);
                    break;
                case ClearCachedCredentialsResult.NotFound:
                    CTaskDialog.MessageBox(
                        this,
                        Language.ClearCachedRdpCredentials,
                        string.Format(Language.NoCachedRdpCredentialFound, target),
                        "", "", "", "",
                        ETaskDialogButtons.Ok, ESysIcons.Information, ESysIcons.Information);
                    break;
                case ClearCachedCredentialsResult.Failed:
                    CTaskDialog.MessageBox(
                        this,
                        Language.ClearCachedRdpCredentials,
                        string.Format(Language.FailedToClearCachedRdpCredential, target),
                        "", "", "", "",
                        ETaskDialogButtons.Ok, ESysIcons.Warning, ESysIcons.Warning);
                    break;
            }
        }

        private void OnImportFileClicked(object sender, EventArgs e)
        {
            ContainerInfo selectedNodeAsContainer;
            if (_connectionTree.SelectedNode == null)
                selectedNodeAsContainer = Runtime.ConnectionsService.ConnectionTreeModel.RootNodes.First();
            else
                selectedNodeAsContainer =
                    _connectionTree.SelectedNode as ContainerInfo ?? _connectionTree.SelectedNode.Parent;
            Import.ImportFromFile(selectedNodeAsContainer);
        }

        private void OnImportPuttyClicked(object sender, EventArgs e)
        {
            ContainerInfo selectedNodeAsContainer;
            if (_connectionTree.SelectedNode == null)
                selectedNodeAsContainer = Runtime.ConnectionsService.ConnectionTreeModel.RootNodes.First();
            else
                selectedNodeAsContainer =
                    _connectionTree.SelectedNode as ContainerInfo ?? _connectionTree.SelectedNode.Parent;
            Import.ImportFromPutty(selectedNodeAsContainer);
        }

        private void OnImportRemoteDesktopManagerClicked(object sender, EventArgs e)
        {
            ContainerInfo selectedNodeAsContainer;
            if (_connectionTree.SelectedNode == null)
                selectedNodeAsContainer = Runtime.ConnectionsService.ConnectionTreeModel.RootNodes.First();
            else
                selectedNodeAsContainer =
                    _connectionTree.SelectedNode as ContainerInfo ?? _connectionTree.SelectedNode.Parent;
            Import.ImportFromRemoteDesktopManagerCsv(selectedNodeAsContainer);
        }

        private void OnImportActiveDirectoryClicked(object sender, EventArgs e)
        {
            AppWindows.Show(WindowType.ActiveDirectoryImport);
        }

        private void OnImportPortScanClicked(object sender, EventArgs e)
        {
            AppWindows.Show(WindowType.PortScan);
        }

        private void OnExportFileClicked(object sender, EventArgs e)
        {
            Export.ExportToFile(_connectionTree.SelectedNode, Runtime.ConnectionsService.ConnectionTreeModel);
        }

        private void OnAddConnectionClicked(object sender, EventArgs e)
        {
            _connectionTree.AddConnection();
        }

        private void OnAddFolderClicked(object sender, EventArgs e)
        {
            _connectionTree.AddFolder();
        }

        private void OnAddRootClicked(object sender, EventArgs e)
        {
            _connectionTree.AddRoot();
        }

        private void OnSortAscendingClicked(object sender, EventArgs e)
        {
            _connectionTree.SortRecursive(_connectionTree.SelectedNode, ListSortDirection.Ascending);
        }

        private void OnSortDescendingClicked(object sender, EventArgs e)
        {
            _connectionTree.SortRecursive(_connectionTree.SelectedNode, ListSortDirection.Descending);
        }

        private void OnMoveUpClicked(object sender, EventArgs e)
        {
            _connectionTree.SelectedNode.Parent.PromoteChild(_connectionTree.SelectedNode);
        }

        private void OnMoveDownClicked(object sender, EventArgs e)
        {
            _connectionTree.SelectedNode.Parent.DemoteChild(_connectionTree.SelectedNode);
        }

        private void OnExternalToolClicked(object sender, EventArgs e)
        {
            StartExternalApp((ExternalTool)((ToolStripMenuItem)sender).Tag);
        }

        private void StartExternalApp(ExternalTool externalTool)
        {
            try
            {
                if (_connectionTree.SelectedNode.GetTreeNodeType() == TreeNodeType.Connection |
                    _connectionTree.SelectedNode.GetTreeNodeType() == TreeNodeType.PuttySession |
                    _connectionTree.SelectedNode.GetTreeNodeType() == TreeNodeType.Container)
                    externalTool.Start(_connectionTree.SelectedNode);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace(
                                                                "cMenTreeToolsExternalAppsEntry_Click failed (UI.Window.ConnectionTreeWindow)",
                                                                ex);
            }
        }

        private void OnApplyInheritanceToChildrenClicked(object sender, EventArgs e)
        {
            if (!(_connectionTree.SelectedNode is ContainerInfo container))
                return;

            container.ApplyInheritancePropertiesToChildren();
        }

        private void OnApplyDefaultInheritanceClicked(object sender, EventArgs e)
        {
            if (_connectionTree.SelectedNode == null)
                return;

            DefaultConnectionInheritance.Instance.SaveTo(_connectionTree.SelectedNode.Inheritance);
        }

        #endregion
    }
}