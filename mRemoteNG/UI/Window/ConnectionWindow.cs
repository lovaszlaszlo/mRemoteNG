using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using mRemoteNG.App;
using mRemoteNG.App.Info;
using mRemoteNG.Config;
using BrightIdeasSoftware;
using mRemoteNG.Container;
using mRemoteNG.Tree.Root;
using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Connection.Protocol.RDP;
using mRemoteNG.Connection.Protocol.VNC;
using mRemoteNG.Messages;
using mRemoteNG.Properties;
using mRemoteNG.Themes;
using mRemoteNG.Tools;
using mRemoteNG.UI.Forms;
using mRemoteNG.UI.Tabs;
using mRemoteNG.UI.TaskDialog;
using WeifenLuo.WinFormsUI.Docking;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;
using mRemoteNG.Security;

namespace mRemoteNG.UI.Window
{
    [SupportedOSPlatform("windows")]
    public partial class ConnectionWindow : BaseWindow
    {
        private VisualStudioToolStripExtender _vsToolStripExtender;
        private readonly ToolStripRenderer _toolStripProfessionalRenderer = new ToolStripProfessionalRenderer();

        #region Public Methods

        public ConnectionWindow(DockContent panel, string formText = "")
        {
            if (formText == "")
            {
                formText = Language.NewPanel;
            }

            WindowType = WindowType.Connection;
            DockPnl = panel;
            InitializeComponent();
            SetEventHandlers();
            // ReSharper disable once VirtualMemberCallInConstructor
            Text = formText;
            TabText = formText;
            connDock.DocumentStyle = DocumentStyle.DockingWindow;
            connDock.ShowDocumentIcon = true;

            connDock.ActiveContentChanged += ConnDockOnActiveContentChanged;
        }

        internal InterfaceControl GetInterfaceControl()
        {
            return InterfaceControl.FindInterfaceControl(connDock);
        }

        private void SetEventHandlers()
        {
            SetFormEventHandlers();
            SetContextMenuEventHandlers();
        }

        private void SetFormEventHandlers()
        {
            Load += Connection_Load;
            DockStateChanged += Connection_DockStateChanged;
            FormClosing += Connection_FormClosing;

            SetUpDropTarget();

            // The hint goes the moment anything is added, without asking the dock panel how many
            // documents it has - during ContentAdded it still answers zero, so the hint was put
            // back and brought to the front, on top of the session that had just opened. The first
            // connection appeared to open an empty page repeating the same text.
            Load += (_, _) => UpdateEmptyHint();
            connDock.ContentAdded += (_, _) =>
            {
                _everHadTabs = true;
                if (_emptyHint != null) _emptyHint.Visible = false;

                // Posted: the pane and its tab strip are built as part of this event, so walking
                // the tree now would find nothing to wire up.
                BeginInvoke(new Action(() => SpreadDropTargets(connDock)));
            };
        }

        private void SetContextMenuEventHandlers()
        {
            // event handler to adjust the items within the context menu
            cmenTab.Opening += ShowHideMenuButtons;

            // event handlers for all context menu items...
            cmenTabFullscreen.Click += (sender, args) => ToggleFullscreen();
            cmenTabSmartSize.Click += (sender, args) => ToggleSmartSize();
            cmenTabViewOnly.Click += (sender, args) => ToggleViewOnly();
            cmenTabStartChat.Click += (sender, args) => StartChat();
            cmenTabTransferFile.Click += (sender, args) => TransferFile();
            cmenTabRefreshScreen.Click += (sender, args) => RefreshScreen();
            cmenTabSendSpecialKeysCtrlAltDel.Click += (sender, args) => SendSpecialKeys(ProtocolVNC.SpecialKeys.CtrlAltDel);
            cmenTabSendSpecialKeysCtrlEsc.Click += (sender, args) => SendSpecialKeys(ProtocolVNC.SpecialKeys.CtrlEsc);
            cmenTabRenameTab.Click += (sender, args) => RenameTab();
            cmenTabDuplicateTab.Click += (sender, args) => DuplicateTab();
            cmenTabReconnect.Click += (sender, args) => Reconnect();
            cmenTabDisconnect.Click += (sender, args) => CloseTabMenu();
            cmenTabDisconnectOthers.Click += (sender, args) => CloseOtherTabs();
            cmenTabDisconnectOthersRight.Click += (sender, args) => CloseOtherTabsToTheRight();
            cmenTabPuttySettings.Click += (sender, args) => ShowPuttySettingsDialog();
            GotFocus += ConnectionWindow_GotFocus;
        }

        private void ConnectionWindow_GotFocus(object sender, EventArgs e)
        {
            TabHelper.Instance.CurrentPanel = this;
        }

        public ConnectionTab AddConnectionTab(ConnectionInfo connectionInfo)
        {
            try
            {
                //Set the connection text based on name and preferences
                string titleText;
                if (Properties.OptionsTabsPanelsPage.Default.ShowProtocolOnTabs)
                    titleText = connectionInfo.Protocol + @": ";
                else
                    titleText = "";

                titleText += connectionInfo.Name;

                if (Properties.OptionsTabsPanelsPage.Default.ShowLogonInfoOnTabs)
                {
                    titleText += @" (";
                    if (connectionInfo.Domain != "")
                        titleText += connectionInfo.Domain;

                    if (connectionInfo.Username != "")
                    {
                        if (connectionInfo.Domain != "")
                            titleText += @"\";
                        titleText += connectionInfo.Username;
                    }

                    titleText += @")";
                }

                titleText = titleText.Replace("&", "&&");

                ConnectionTab conTab = new()
                {
                    Tag = connectionInfo,
                    DockAreas = DockAreas.Document | DockAreas.Float,
                    Icon = ConnectionIcon.FromString(connectionInfo.Icon),
                    TabText = titleText,
                    TabPageContextMenuStrip = cmenTab
                };

                //if (Settings.Default.AlwaysShowConnectionTabs == false)
                // TODO: See if we can make this work with DPS...
                //TabController.HideTabsMode = TabControl.HideTabsModes.HideAlways;

                // Ensure the ConnectionWindow is visible before adding the tab
                // This prevents visibility issues when the window was created but not yet shown
                // Check DockState instead of Visible to properly detect if window is shown in DockPanel
                if (DockState == DockState.Unknown || DockState == DockState.Hidden || !Visible)
                {
                    Show(FrmMain.Default.pnlDock, DockState.Document);
                }

                //Show the tab
                conTab.Show(connDock, DockState.Document);
                conTab.Focus();
                return conTab;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("AddConnectionTab (UI.Window.ConnectionWindow) failed", ex);
            }

            return null;
        }

        #endregion

        public void ReconnectAll(IConnectionInitiator initiator)
        {
            List<InterfaceControl> controlList = new();
            try
            {
                foreach (IDockContent dockContent in connDock.DocumentsToArray())
                {
                    ConnectionTab tab = (ConnectionTab)dockContent;
                    controlList.Add((InterfaceControl)tab.Tag);
                }

                foreach (InterfaceControl iControl in controlList)
                {
                    iControl.Protocol.Close();
                    initiator.OpenConnection(iControl.Info, ConnectionInfo.Force.DoNotJump);
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("reconnectAll (UI.Window.ConnectionWindow) failed", ex);
            }

            // ReSharper disable once RedundantAssignment
            controlList = null;
        }

        #region Form

        private void Connection_Load(object sender, EventArgs e)
        {
            ApplyTheme();
            ThemeManager.getInstance().ThemeChanged += ApplyTheme;
            ApplyLanguage();
        }

        private new void ApplyTheme()
        {
            if (!ThemeManager.getInstance().ThemingActive)
            {
                connDock.Theme = ThemeManager.getInstance().DefaultTheme.Theme;
                return;
            }

            base.ApplyTheme();
            try
            {
                connDock.Theme = ThemeManager.getInstance().ActiveTheme.Theme;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.ConnectionWindow.ApplyTheme() failed", ex);
            }

            _vsToolStripExtender = new VisualStudioToolStripExtender(components)
            {
                DefaultRenderer = _toolStripProfessionalRenderer
            };
            _vsToolStripExtender.SetStyle(cmenTab, ThemeManager.getInstance().ActiveTheme.Version, ThemeManager.getInstance().ActiveTheme.Theme);

            if (!ThemeManager.getInstance().ActiveAndExtended) return;
            connDock.DockBackColor = ThemeManager.getInstance().ActiveTheme.ExtendedPalette.getColor("Tab_Item_Background");
        }

        private bool _documentHandlersAdded;
        private bool _floatHandlersAdded;

        private void Connection_DockStateChanged(object sender, EventArgs e)
        {
            switch (DockState)
            {
                case DockState.Float:
                    {
                        if (_documentHandlersAdded)
                        {
                            FrmMain.Default.ResizeBegin -= Connection_ResizeBegin;
                            FrmMain.Default.ResizeEnd -= Connection_ResizeEnd;
                            _documentHandlersAdded = false;
                        }

                        DockHandler.FloatPane.FloatWindow.ResizeBegin += Connection_ResizeBegin;
                        DockHandler.FloatPane.FloatWindow.ResizeEnd += Connection_ResizeEnd;
                        _floatHandlersAdded = true;
                        break;
                    }
                case DockState.Document:
                    {
                        if (_floatHandlersAdded)
                        {
                            DockHandler.FloatPane.FloatWindow.ResizeBegin -= Connection_ResizeBegin;
                            DockHandler.FloatPane.FloatWindow.ResizeEnd -= Connection_ResizeEnd;
                            _floatHandlersAdded = false;
                        }

                        FrmMain.Default.ResizeBegin += Connection_ResizeBegin;
                        FrmMain.Default.ResizeEnd += Connection_ResizeEnd;
                        _documentHandlersAdded = true;
                        break;
                    }
            }
        }

        private void ApplyLanguage()
        {
            cmenTabFullscreen.Text = Language.Fullscreen;
            cmenTabSmartSize.Text = Language.SmartSize;
            cmenTabViewOnly.Text = Language.ViewOnly;
            cmenTabStartChat.Text = Language.StartChat;
            cmenTabTransferFile.Text = Language.TransferFile;
            cmenTabRefreshScreen.Text = Language.RefreshScreen;
            cmenTabSendSpecialKeys.Text = Language.SendSpecialKeys;
            cmenTabSendSpecialKeysCtrlAltDel.Text = Language.CtrlAltDel;
            cmenTabSendSpecialKeysCtrlEsc.Text = Language.CtrlEsc;
            cmenTabExternalApps.Text = Language._Tools;
            cmenTabRenameTab.Text = Language.RenameTab;
            // Not "Duplicate Tab": nothing here is duplicated. It opens a second, independent
            // session to the same machine, and leaves the tab it was invoked from alone. The
            // connection tree already has an entry for exactly this action, worded this way, so
            // the two places now read the same. (The tree's own "Duplicate" is a different thing
            // again - it copies the connection into a new entry.)
            cmenTabDuplicateTab.Text = Language.ConnectNewSession;
            cmenTabReconnect.Text = Language.Reconnect;
            cmenTabDisconnect.Text = Language.Disconnect;
            cmenTabDisconnectOthers.Text = Language.DisconnectOthers;
            cmenTabDisconnectOthersRight.Text = Language.DisconnectOthersRight;
            cmenTabPuttySettings.Text = Language.PuttySettings;
        }

        private void Connection_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!FrmMain.Default.IsClosing &&
                (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.All & connDock.Documents.Any() ||
                 Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.Multiple &
                 connDock.Documents.Count() > 1))
            {
                DialogResult result = CTaskDialog.MessageBox(this, GeneralAppInfo.ProductName, string.Format(Language.ConfirmCloseConnectionPanelMainInstruction, Text), "", "", "", Language.CheckboxDoNotShowThisMessageAgain, ETaskDialogButtons.YesNo, ESysIcons.Question, ESysIcons.Question);
                if (CTaskDialog.VerificationChecked)
                {
                    Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseEnum.Never;
                    Settings.Default.Save();
                }

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            try
            {
                foreach (IDockContent dockContent in connDock.Documents.ToArray())
                {
                    ConnectionTab tabP = (ConnectionTab)dockContent;
                    if (tabP.Tag == null) continue;
                    tabP.silentClose = true;
                    tabP.Close();
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.Connection.Connection_FormClosing() failed", ex);
            }
        }

        public new event EventHandler ResizeBegin;

        private void Connection_ResizeBegin(object sender, EventArgs e)
        {
            ResizeBegin?.Invoke(this, e);
        }

        public new event EventHandler ResizeEnd;

        private void Connection_ResizeEnd(object sender, EventArgs e)
        {
            ResizeEnd?.Invoke(sender, e);
        }

        /// <summary>
        /// Opens connections dropped here from the connection tree.
        /// </summary>
        /// <remarks>
        /// The only way to put a connection in a particular tab group was to type that group's
        /// name into the connection's own properties, which is not something anybody guesses. The
        /// tree already offers its selection as a drag source; this is the other half.
        ///
        /// A folder dropped here opens everything inside it, so it asks first - the same question
        /// the tree's own Connect asks, and for the same reason.
        /// </remarks>
        /// <summary>
        /// The controls already wired for dropping, so none is wired twice.
        /// </summary>
        private readonly HashSet<Control> _dropTargets = new();

        private void SetUpDropTarget()
        {
            // A child control that does not accept drops does not pass them up to its parent, and
            // the form's whole surface is covered by connDock - so the form alone was never
            // reached. Once a tab is open the dock panel is covered in turn, by the pane and its
            // tab strip, and a drop worked only on a group that was still empty.
            AcceptDrops(this);
            AcceptDrops(connDock);
        }

        /// <summary>
        /// Extends the drop target over whatever the dock panel has built inside itself.
        /// </summary>
        /// <remarks>
        /// The session's own control is left out on purpose: the terminal and the RDP client
        /// handle the mouse themselves, and a drop over a live session would have to be taken away
        /// from them. The tab strip and the empty space around it are enough - that is where a tab
        /// is dropped in every other program too.
        /// </remarks>
        private void SpreadDropTargets(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is InterfaceControl) continue;

                AcceptDrops(child);
                SpreadDropTargets(child);
            }
        }

        private void AcceptDrops(Control target)
        {
            if (!_dropTargets.Add(target)) return;

            target.AllowDrop = true;

            target.DragEnter += (_, e) =>
                e.Effect = DroppedConnections(e).Any() ? DragDropEffects.Copy : DragDropEffects.None;

            target.DragOver += (_, e) =>
                e.Effect = DroppedConnections(e).Any() ? DragDropEffects.Copy : DragDropEffects.None;

            target.DragDrop += (_, e) => OpenDropped(DroppedConnections(e).ToArray());
        }

        private static IEnumerable<ConnectionInfo> DroppedConnections(DragEventArgs e)
        {
            // The dragged object itself, not a format stored inside it: ObjectListView hands its
            // OLVDataObject straight to DoDragDrop, so within one process this is that instance.
            // Asking for it by format name found nothing, which is why nothing could be dropped.
            OLVDataObject data = e.Data as OLVDataObject
                                 ?? e.Data?.GetData(typeof(OLVDataObject)) as OLVDataObject;

            if (data?.ModelObjects == null) yield break;

            foreach (object model in data.ModelObjects)
            {
                // The PuTTY sessions are read-only mirrors of another program's registry, and
                // nothing here can open one into a chosen group.
                if (model is RootPuttySessionsNodeInfo or PuttySessionInfo) continue;

                if (model is ConnectionInfo connection) yield return connection;
            }
        }

        private void OpenDropped(ConnectionInfo[] dropped)
        {
            if (dropped.Length == 0) return;

            int count = dropped.Sum(CountConnections);
            if (count == 0) return;

            // Asked once for the whole drop, not once per item.
            if (count > 1)
            {
                string question = string.Format(Language.ConfirmOpenDroppedConnections, count, TabText);

                if (!Confirm.Ask(this, question))
                    return;
            }

            foreach (ConnectionInfo connection in dropped)
            {
                // This window is passed as the target, which is what actually decides where the
                // session opens. Force.OverridePanel is deliberately NOT set: despite the name it
                // does not mean "use the panel I gave you" but "ask which panel to use", and it
                // put a chooser dialog in front of a drop that had already said where to go.
                Runtime.ConnectionInitiator.OpenConnection(
                    connection,
                    ConnectionInfo.Force.DoNotJump,
                    this);
            }
        }

        private static int CountConnections(ConnectionInfo node) =>
            node is ContainerInfo container
                ? container.Children.Sum(CountConnections)
                : 1;

        private Label _emptyHint;

        /// <summary>
        /// Tells an empty tab group what it is for.
        /// </summary>
        /// <remarks>
        /// "New tab group" produced an empty container and stopped there. Nothing said that a
        /// connection arrives in it only when its own Tab group property names this one, or when a
        /// tab is dragged in - so the feature looked broken, and the only way to learn otherwise
        /// was to be told. The group now says it itself, and says its own name, which is the part
        /// that has to be typed into the connection.
        /// </remarks>
        private void ShowEmptyHint()
        {
            if (_emptyHint == null)
            {
                _emptyHint = new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    Padding = new Padding(24),
                    BackColor = connDock.DockBackColor
                };

                Controls.Add(_emptyHint);
                AcceptDrops(_emptyHint);
            }

            _emptyHint.Text = string.Format(Language.EmptyTabGroupHint, TabText);
            _emptyHint.ForeColor = ForeColor;
            _emptyHint.BackColor = BackColor;
            _emptyHint.Visible = true;
            _emptyHint.BringToFront();
        }

        /// <summary>
        /// Whether anything has ever been opened in this group.
        /// </summary>
        /// <remarks>
        /// The hint belongs on a group that has never held a tab - a freshly made one, which is
        /// exactly when nobody knows what to do with it. Closing the last session of the day is a
        /// different moment: there the same text is a lecture about a feature that was not being
        /// used, in the space where the work just was.
        /// </remarks>
        private bool _everHadTabs;

        private void UpdateEmptyHint()
        {
            if (connDock.DocumentsCount > 0)
            {
                _everHadTabs = true;
                if (_emptyHint != null) _emptyHint.Visible = false;
                return;
            }

            if (_everHadTabs)
            {
                if (_emptyHint != null) _emptyHint.Visible = false;
                return;
            }

            ShowEmptyHint();
        }

        internal void NavigateToNextTab()
        {
            try
            {
                var documents = connDock.DocumentsToArray();
                if (documents.Length <= 1) return;

                // ActiveDocument first: ActiveContent is whatever last had the focus inside
                // this dock panel, and while the focus is on the tree or a settings page it is
                // not one of the tabs at all. Asking only that question made the shortcut give up
                // with a line in the notifications panel and nothing on screen.
                var current = connDock.ActiveDocument ?? connDock.ActiveContent;
                var currentIndex = Array.IndexOf(documents, current);
                if (currentIndex == -1)
                    currentIndex = 0;

                var nextIndex = (currentIndex + 1) % documents.Length;
                documents[nextIndex].DockHandler.Activate();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("NavigateToNextTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        internal void NavigateToPreviousTab()
        {
            try
            {
                var documents = connDock.DocumentsToArray();
                if (documents.Length <= 1) return;

                // ActiveDocument first: ActiveContent is whatever last had the focus inside
                // this dock panel, and while the focus is on the tree or a settings page it is
                // not one of the tabs at all. Asking only that question made the shortcut give up
                // with a line in the notifications panel and nothing on screen.
                var current = connDock.ActiveDocument ?? connDock.ActiveContent;
                var currentIndex = Array.IndexOf(documents, current);
                if (currentIndex == -1)
                    currentIndex = 0;

                var previousIndex = currentIndex - 1;
                if (previousIndex < 0)
                    previousIndex = documents.Length - 1;
                documents[previousIndex].DockHandler.Activate();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("NavigateToPreviousTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        internal void NavigateToTab(int index)
        {
            try
            {
                var documents = connDock.DocumentsToArray();
                if (index < 0 || index >= documents.Length) return;

                documents[index].DockHandler.Activate();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("NavigateToTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        internal IDockContent[] GetDocuments()
        {
            try
            {
                return connDock.DocumentsToArray();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("GetDocuments (UI.Window.ConnectionWindow) failed", ex);
                return Array.Empty<IDockContent>();
            }
        }

        #endregion

        #region Events

        private void ConnDockOnActiveContentChanged(object sender, EventArgs e)
        {
            InterfaceControl ic = GetInterfaceControl();
            if (ic?.Info == null) return;
            FrmMain.Default.SelectedConnection = ic.Info;

            // Switching tabs leaves the focus on the ConnectionTab itself and never passes it on to
            // the connection, so an RDP session had to be clicked before it took the keyboard.
            // Alt+Tab did not suffer from this because WM_ACTIVATEAPP calls ActivateConnection(),
            // which focuses the protocol explicitly.
            //
            // This event also fires while the application is activated or deactivated with the same
            // tab still in front, so only a real change of the active document is acted on.
            IDockContent activeDocument = connDock.ActiveDocument;
            if (activeDocument == null || ReferenceEquals(activeDocument, _lastFocusedDocument)) return;
            _lastFocusedDocument = activeDocument;

            FocusActiveConnection(activeDocument);
        }

        private IDockContent _lastFocusedDocument;

        /// <summary>
        /// Hands the keyboard to the connection in the tab that just became active.
        /// </summary>
        /// <remarks>
        /// Two earlier attempts at this had to be reverted, and the reason is now known: PuTTY is
        /// embedded without the WS_CHILD style, so focusing it also activates it and drags the tab
        /// selection back - the handler then fed itself. PuTTY is therefore left alone here and
        /// still needs a click; the RDP client and the native SSH terminal are ordinary controls
        /// and have no such behaviour.
        /// </remarks>
        private void FocusActiveConnection(IDockContent expectedDocument)
        {
            if (!IsHandleCreated || IsDisposed) return;

            BeginInvoke(new Action(() =>
            {
                if (!ReferenceEquals(connDock.ActiveDocument, expectedDocument)) return;
                if (!FrmMain.ApplicationIsInForeground()) return;

                ProtocolBase protocol = GetInterfaceControl()?.Protocol;
                if (protocol == null || protocol is PuttyBase) return;

                protocol.Focus();
            }));
        }

        #endregion

        #region Tab Menu

        private void ShowHideMenuButtons(object sender, CancelEventArgs e)
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl == null) return;

                if (interfaceControl.Protocol is ISupportsViewOnly viewOnly)
                {
                    cmenTabViewOnly.Visible = true;
                    cmenTabViewOnly.Checked = viewOnly.ViewOnly;
                }
                else
                {
                    cmenTabViewOnly.Visible = false;
                }

                if (interfaceControl.Info.Protocol == ProtocolType.RDP)
                {
                    RdpProtocol rdp = (RdpProtocol)interfaceControl.Protocol;
                    cmenTabFullscreen.Visible = true;
                    cmenTabFullscreen.Checked = rdp.Fullscreen;
                    cmenTabSmartSize.Visible = true;
                    cmenTabSmartSize.Checked = rdp.SmartSize;
                }
                else
                {
                    cmenTabFullscreen.Visible = false;
                    cmenTabSmartSize.Visible = false;
                }

                if (interfaceControl.Info.Protocol == ProtocolType.VNC)
                {
                    ProtocolVNC vnc = (ProtocolVNC)interfaceControl.Protocol;
                    cmenTabSendSpecialKeys.Visible = true;
                    cmenTabSmartSize.Visible = true;
                    cmenTabStartChat.Visible = true;
                    cmenTabRefreshScreen.Visible = true;
                    cmenTabTransferFile.Visible = false;
                }
                else
                {
                    cmenTabSendSpecialKeys.Visible = false;
                    cmenTabStartChat.Visible = false;
                    cmenTabRefreshScreen.Visible = false;
                    cmenTabTransferFile.Visible = false;
                }

                if (interfaceControl.Info.Protocol == ProtocolType.SSH1 |
                    interfaceControl.Info.Protocol == ProtocolType.SSH2)
                {
                    cmenTabTransferFile.Visible = true;
                }

                cmenTabPuttySettings.Visible = interfaceControl.Protocol is PuttyBase;

                AddExternalApps();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("ShowHideMenuButtons (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        #endregion

        #region Tab Actions

        private void ToggleSmartSize()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();

                switch (interfaceControl.Protocol)
                {
                    case RdpProtocol rdp:
                        rdp.ToggleSmartSize();
                        break;
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("ToggleSmartSize (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void TransferFile()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl == null) return;

                if (interfaceControl.Info.Protocol == ProtocolType.SSH1 |
                    interfaceControl.Info.Protocol == ProtocolType.SSH2)
                    SshTransferFile();
                else if (interfaceControl.Info.Protocol == ProtocolType.VNC)
                    VncTransferFile();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("TransferFile (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void SshTransferFile()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl == null) return;

                AppWindows.Show(WindowType.SSHTransfer);
                AppWindows.SshtransferForm.LoadFrom(interfaceControl.Info);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("SSHTransferFile (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void VncTransferFile()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                ProtocolVNC vnc = interfaceControl?.Protocol as ProtocolVNC;
                vnc?.StartFileTransfer();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("VNCTransferFile (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void ToggleViewOnly()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (!(interfaceControl?.Protocol is ISupportsViewOnly viewOnly))
                    return;

                cmenTabViewOnly.Checked = !cmenTabViewOnly.Checked;
                viewOnly.ToggleViewOnly();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("ToggleViewOnly (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void StartChat()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                ProtocolVNC vnc = interfaceControl?.Protocol as ProtocolVNC;
                vnc?.StartChat();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("StartChat (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void RefreshScreen()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                ProtocolVNC vnc = interfaceControl?.Protocol as ProtocolVNC;
                vnc?.RefreshScreen();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("RefreshScreen (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void SendSpecialKeys(ProtocolVNC.SpecialKeys keys)
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                ProtocolVNC vnc = interfaceControl?.Protocol as ProtocolVNC;
                vnc?.SendSpecialKeys(keys);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("SendSpecialKeys (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void ToggleFullscreen()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                RdpProtocol rdp = interfaceControl?.Protocol as RdpProtocol;
                rdp?.ToggleFullscreen();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("ToggleFullscreen (UI.Window.ConnectionWindow) failed",
                                                             ex);
            }
        }

        private void ShowPuttySettingsDialog()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                PuttyBase puttyBase = interfaceControl?.Protocol as PuttyBase;
                puttyBase?.ShowSettingsDialog();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                                                             "ShowPuttySettingsDialog (UI.Window.ConnectionWindow) failed",
                                                             ex);
            }
        }

        private void AddExternalApps()
        {
            try
            {
                //clean up. since new items are added below, we have to dispose of any previous items first
                if (cmenTabExternalApps.DropDownItems.Count > 0)
                {
                    for (int i = cmenTabExternalApps.DropDownItems.Count - 1; i >= 0; i--)
                        cmenTabExternalApps.DropDownItems[i].Dispose();
                    cmenTabExternalApps.DropDownItems.Clear();
                }

                //add ext apps
                foreach (ExternalTool externalTool in Runtime.ExternalToolsService.ExternalTools)
                {
                    ToolStripMenuItem nItem = new()
                    {
                        Text = externalTool.DisplayName,
                        Tag = externalTool,
                        /* rare failure here. While ExternalTool.Image already tries to default this
                         * try again so it's not null/doesn't crash.
                         */
                        Image = externalTool.Image ?? Properties.Resources.mRemoteNG_Icon.ToBitmap()
                    };

                    nItem.Click += (sender, args) => StartExternalApp(((ToolStripMenuItem)sender)?.Tag as ExternalTool);
                    cmenTabExternalApps.DropDownItems.Add(nItem);
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace("cMenTreeTools_DropDownOpening failed (UI.Window.ConnectionWindow)", ex);
            }
        }

        private void StartExternalApp(ExternalTool externalTool)
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                externalTool.Start(interfaceControl?.Info);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("cmenTabExternalAppsEntry_Click failed (UI.Window.ConnectionWindow)", ex);
            }
        }


        private void CloseTabMenu()
        {
            ConnectionTab selectedTab = (ConnectionTab)GetInterfaceControl()?.Parent;
            if (selectedTab == null) return;

            try
            {
                selectedTab.Close();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("CloseTabMenu (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void CloseOtherTabs()
        {
            ConnectionTab selectedTab = (ConnectionTab)GetInterfaceControl()?.Parent;
            if (selectedTab == null) return;
            if (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.Multiple)
            {
                DialogResult result = CTaskDialog.MessageBox(this, GeneralAppInfo.ProductName,
                                                    string.Format(Language.ConfirmCloseConnectionOthersInstruction,
                                                                  selectedTab.TabText), "", "", "",
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
                    return;
                }
            }

            foreach (IDockContent dockContent in connDock.Documents.ToArray())
            {
                ConnectionTab tab = (ConnectionTab)dockContent;
                if (selectedTab != tab)
                {
                    tab.Close();
                }
            }
        }

        private void CloseOtherTabsToTheRight()
        {
            try
            {
                ConnectionTab selectedTab = (ConnectionTab)GetInterfaceControl()?.Parent;
                if (selectedTab == null) return;
                DockPane dockPane = selectedTab.Pane;

                bool pastTabToKeepAlive = false;
                List<ConnectionTab> connectionsToClose = new();
                foreach (IDockContent dockContent in dockPane.Contents)
                {
                    ConnectionTab tab = (ConnectionTab)dockContent;
                    if (pastTabToKeepAlive)
                        connectionsToClose.Add(tab);

                    if (selectedTab == tab)
                        pastTabToKeepAlive = true;
                }

                foreach (ConnectionTab tab in connectionsToClose)
                {
                    tab.Close();
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("CloseTabMenu (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void DuplicateTab()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl == null) return;
                Runtime.ConnectionInitiator.OpenConnection(interfaceControl.Info, ConnectionInfo.Force.DoNotJump);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("DuplicateTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void Reconnect()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl == null)
                {
                    Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, "Reconnect (UI.Window.ConnectionWindow) failed. Could not find InterfaceControl.");
                    return;
                }

                Invoke(new Action(() => Prot_Event_Closed(interfaceControl.Protocol)));
                Runtime.ConnectionInitiator.OpenConnection(interfaceControl.Info, ConnectionInfo.Force.DoNotJump);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Reconnect (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        private void RenameTab()
        {
            try
            {
                InterfaceControl interfaceControl = GetInterfaceControl();
                if (interfaceControl == null) return;
                using (FrmInputBox frmInputBox = new(Language.NewTitle, Language.NewTitle,
                                                         ((ConnectionTab)interfaceControl.Parent).TabText))
                {
                    DialogResult dr = frmInputBox.ShowDialog();
                    if (dr != DialogResult.OK) return;
                    if (!string.IsNullOrEmpty(frmInputBox.returnValue))
                        ((ConnectionTab)interfaceControl.Parent).TabText = frmInputBox.returnValue.Replace("&", "&&");
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("RenameTab (UI.Window.ConnectionWindow) failed", ex);
            }
        }

        #endregion

        #region Protocols

        public void Prot_Event_Closed(object sender)
        {
            ProtocolBase protocolBase = sender as ProtocolBase;
            if (!(protocolBase?.InterfaceControl.Parent is ConnectionTab tabPage)) return;
            if (tabPage.Disposing || tabPage.IsDisposed) return;
            if (IsDisposed || Disposing) return;
            tabPage.protocolClose = true;
            Invoke(new Action(() => tabPage.Close()));
        }

        #endregion
    }
}
