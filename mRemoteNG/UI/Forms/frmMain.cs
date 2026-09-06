#region Usings
using Microsoft.Win32;
using mRemoteNG.App;
using mRemoteNG.App.Info;
using mRemoteNG.App.Initialization;
using mRemoteNG.Config;
using mRemoteNG.Config.Connections;
using mRemoteNG.Config.DataProviders;
using mRemoteNG.Config.Putty;
using mRemoteNG.Config.Settings;
using mRemoteNG.Connection;
using mRemoteNG.Messages;
using mRemoteNG.Messages.MessageWriters;
using mRemoteNG.Themes;
using mRemoteNG.Tools;
using mRemoteNG.UI.Menu;
using mRemoteNG.UI.Tabs;
using mRemoteNG.UI.TaskDialog;
using mRemoteNG.UI.Window;
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using mRemoteNG.UI.Panels;
using WeifenLuo.WinFormsUI.Docking;
using mRemoteNG.UI.Controls;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;
using mRemoteNG.Config.Settings.Registry;
using System.Threading; // ADDED
#endregion

// ReSharper disable MemberCanBePrivate.Global

namespace mRemoteNG.UI.Forms
{
    [SupportedOSPlatform("windows")]
    public partial class FrmMain
    {
        // CHANGED: lazy, thread-safe, STA-enforced initialization
        private static readonly Lazy<FrmMain> s_default =
            new(InitializeOnSta, LazyThreadSafetyMode.ExecutionAndPublication);

        public static FrmMain Default => s_default.Value;

        public static bool IsCreated => s_default.IsValueCreated;

        private static FrmMain InitializeOnSta()
        {
            // Enforce STA to avoid OLE/WinForms threading violations
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                // If we're already on a WinForms UI thread with a sync context, marshal to it
                if (SynchronizationContext.Current is WindowsFormsSynchronizationContext ctx)
                {
                    FrmMain created = null;
                    ctx.Send(_ => created = new FrmMain(), null);
                    return created!;
                }

                throw new ThreadStateException("FrmMain must be created on an STA thread.");
            }

            return new FrmMain();
        }

        private static ClipboardchangeEventHandler _clipboardChangedEvent;
        private bool _inSizeMove;
        private bool _inMouseActivate;
        private bool _connectionHadFocusOnDeactivate;
        private IntPtr _fpChainedWindowHandle;
        private bool _usingSqlServer;
        private string _connectionsFileName;
        private bool _showFullPathInTitle;
        private readonly AdvancedWindowMenu _advancedWindowMenu;
        private ConnectionInfo _selectedConnection;
        private readonly IList<IMessageWriter> _messageWriters = [];
        private readonly ThemeManager _themeManager;
        private readonly FileBackupPruner _backupPruner = new();
        public static FrmOptions OptionsForm;

        /// <summary>
        /// Recreates the OptionsForm if it has been disposed.
        /// This method should be called when OptionsForm is in an invalid state.
        /// </summary>
        public static void RecreateOptionsForm()
        {
            Logger.Instance.Log?.Debug("[FrmMain.RecreateOptionsForm] Recreating OptionsForm");

            // Dispose the old form if it exists
            if (OptionsForm != null && !OptionsForm.IsDisposed)
            {
                Logger.Instance.Log?.Debug("[FrmMain.RecreateOptionsForm] Disposing old OptionsForm");
                OptionsForm.Dispose();
            }

            // Create a new instance
            OptionsForm = new FrmOptions();
            Logger.Instance.Log?.Debug("[FrmMain.RecreateOptionsForm] New OptionsForm created");
        }

        internal FullscreenHandler Fullscreen { get; set; }

        //Added theming support
        private readonly ToolStripRenderer _toolStripProfessionalRenderer = new ToolStripProfessionalRenderer();

        private FrmMain()
        {
            _showFullPathInTitle = Properties.OptionsAppearancePage.Default.ShowCompleteConsPathInTitle;
            InitializeComponent();

            Screen targetScreen = (Screen.AllScreens.Length > 1) ? Screen.AllScreens[1] : Screen.AllScreens[0];

            Rectangle viewport = targetScreen.WorkingArea;
            
            // normally it should be screens[1] however due DPI apply 1 size "same" as default with 100%
            this.Left = viewport.Left + (targetScreen.Bounds.Size.Width / 2) - (this.Width / 2);
            this.Top = viewport.Top + (targetScreen.Bounds.Size.Height / 2) - (this.Height / 2);

            Fullscreen = new FullscreenHandler(this);
            Fullscreen.Chrome.Add(tsContainer.TopToolStripPanel);
            Fullscreen.ChromeToggled = SetTabStripsForFullscreen;

            //Theming support
            _themeManager = ThemeManager.getInstance();
            vsToolStripExtender.DefaultRenderer = _toolStripProfessionalRenderer;
            ApplyTheme();

            _advancedWindowMenu = new AdvancedWindowMenu(this);
        }

        #region Properties

        public FormWindowState PreviousWindowState { get; set; }

        public bool IsClosing { get; private set; }

        public bool AreWeUsingSqlServerForSavingConnections
        {
            get => _usingSqlServer;
            set
            {
                if (_usingSqlServer == value)
                {
                    return;
                }

                _usingSqlServer = value;
                UpdateWindowTitle();
            }
        }

        public string ConnectionsFileName
        {
            get => _connectionsFileName;
            set
            {
                if (_connectionsFileName == value)
                {
                    return;
                }

                _connectionsFileName = value;
                UpdateWindowTitle();
            }
        }

        public bool ShowFullPathInTitle
        {
            get => _showFullPathInTitle;
            set
            {
                if (_showFullPathInTitle == value)
                {
                    return;
                }

                _showFullPathInTitle = value;
                UpdateWindowTitle();
            }
        }

        public ConnectionInfo SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                if (_selectedConnection == value)
                {
                    return;
                }

                _selectedConnection = value;
                UpdateWindowTitle();
            }
        }

        #endregion

        #region Startup & Shutdown

        private void FrmMain_Load(object sender, EventArgs e)
        {
            MessageCollector messageCollector = Runtime.MessageCollector;

            SettingsLoader settingsLoader = new(this, messageCollector, _quickConnectToolStrip, _externalToolsToolStrip, msMain);
            settingsLoader.LoadSettings();

            MessageCollectorSetup.SetupMessageCollector(messageCollector, _messageWriters);
            MessageCollectorSetup.BuildMessageWritersFromSettings(_messageWriters);

            Startup.Instance.InitializeProgram(messageCollector);

            SetMenuDependencies();

            DockPanelLayoutLoader uiLoader = new(this, messageCollector);
            uiLoader.LoadPanelsFromXml();

            LockToolbarPositions(Properties.Settings.Default.LockToolbars);
            Properties.Settings.Default.PropertyChanged += OnApplicationSettingChanged;

            _themeManager.ThemeChanged += ApplyTheme;

            _fpChainedWindowHandle = NativeMethods.SetClipboardViewer(Handle);

            Runtime.WindowList = [];

            if (Properties.App.Default.ResetPanels)
                SetDefaultLayout();
            else
                SetLayout();

            ShowHidePanelTabs();

            Runtime.ConnectionsService.ConnectionsLoaded += ConnectionsServiceOnConnectionsLoaded;
            Runtime.ConnectionsService.ConnectionsSaved += ConnectionsServiceOnConnectionsSaved;
            
            // The splash screen stays up until the main window is actually shown; the dialogs that
            // must not end up behind it (password prompt, compatibility and load errors) close it
            // themselves through ProgramRoot.CloseSplash(), which is safe to call more than once.
            CredsAndConsSetup credsAndConsSetup = new();
            credsAndConsSetup.LoadCredsAndCons();

            // Initialize panel binding for Connections and Config panels
            UI.Panels.PanelBinder.Instance.Initialize();

            AppWindows.TreeForm.Focus();

            PuttySessionsManager.Instance.StartWatcher();

            Startup.Instance.CreateConnectionsProvider(messageCollector);

            _advancedWindowMenu.BuildAdditionalMenuItems();
            SystemEvents.DisplaySettingsChanged += _advancedWindowMenu.OnDisplayChanged;
            ApplyLanguage();

            Opacity = 1;
            //Fix MagicRemove , revision on panel strategy for mdi

            pnlDock.ShowDocumentIcon = true;

            if (Properties.OptionsStartupExitPage.Default.StartMinimized)
            {
                WindowState = FormWindowState.Minimized;
                if (Properties.OptionsAppearancePage.Default.MinimizeToTray)
                    ShowInTaskbar = false;
            }
            if (Properties.OptionsStartupExitPage.Default.StartFullScreen)
            {
                Fullscreen.Value = true;
            }

            OptionsForm = new FrmOptions();

            if (!Properties.OptionsTabsPanelsPage.Default.CreateEmptyPanelOnStartUp)
            {
                return;
            }
            string panelName = !string.IsNullOrEmpty(Properties.OptionsTabsPanelsPage.Default.StartUpPanelName) ? Properties.OptionsTabsPanelsPage.Default.StartUpPanelName : Language.NewPanel;

            PanelAdder panelAdder = new();
            if (!panelAdder.DoesPanelExist(panelName))
                panelAdder.AddPanel(panelName);
        }

        private void ApplyLanguage()
        {
            fileMenu.ApplyLanguage();
            sessionsMenu.ApplyLanguage();
            viewMenu.ApplyLanguage();
            toolsMenu.ApplyLanguage();
            helpMenu.ApplyLanguage();
        }

        private void OnApplicationSettingChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case nameof(Properties.Settings.LockToolbars):
                    LockToolbarPositions(Properties.Settings.Default.LockToolbars);
                    break;
                case nameof(Properties.Settings.ViewMenuExternalTools):
                    LockToolbarPositions(Properties.Settings.Default.LockToolbars);
                    break;
                case nameof(Properties.Settings.ViewMenuMessages):
                    LockToolbarPositions(Properties.Settings.Default.LockToolbars);
                    break;
                case nameof(Properties.Settings.ViewMenuQuickConnect):
                    LockToolbarPositions(Properties.Settings.Default.LockToolbars);
                    break;
                default:
                    return;
            }
        }

        private void LockToolbarPositions(bool shouldBeLocked)
        {
            ToolStrip[] toolbars = [_quickConnectToolStrip, _externalToolsToolStrip, msMain];
            foreach (ToolStrip toolbar in toolbars)
            {
                toolbar.GripStyle = shouldBeLocked ? ToolStripGripStyle.Hidden : ToolStripGripStyle.Visible;
            }
        }

        private void ConnectionsServiceOnConnectionsLoaded(object? sender, ConnectionsLoadedEventArgs connectionsLoadedEventArgs)
        {
            UpdateWindowTitle();
        }

        private void ConnectionsServiceOnConnectionsSaved(object sender, ConnectionsSavedEventArgs connectionsSavedEventArgs)
        {
            if (connectionsSavedEventArgs.UsingDatabase)
                return;

            _backupPruner.PruneBackupFiles(connectionsSavedEventArgs.ConnectionFileName, Properties.OptionsBackupPage.Default.BackupFileKeepCount);
        }

        private void SetMenuDependencies()
        {
            fileMenu.TreeWindow = AppWindows.TreeForm;

            viewMenu.TsExternalTools = _externalToolsToolStrip;
            viewMenu.TsQuickConnect = _quickConnectToolStrip;
            viewMenu.FullscreenHandler = Fullscreen;
            viewMenu.MainForm = this;

            toolsMenu.MainForm = this;
            toolsMenu.CredentialProviderCatalog = Runtime.CredentialProviderCatalog;
        }

        // Apply the dark/light title bar before the window is shown to avoid a white flash.
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _themeManager.ApplyThemeToTitleBar(this);
        }

        //Theming support
        private void ApplyTheme()
        {
            _themeManager.ApplyThemeToTitleBar(this);

            if (!_themeManager.ThemingActive)
            {
                pnlDock.Theme = _themeManager.DefaultTheme.Theme;
                return;
            }

            try
            {
                // this will always throw when turning themes on from
                // the options menu.
                pnlDock.Theme = _themeManager.ActiveTheme.Theme;
            }
            catch (Exception)
            {
                // intentionally ignore exception
            }

            // Persist settings when rebuilding UI
            try
            {
                vsToolStripExtender.SetStyle(msMain, _themeManager.ActiveTheme.Version, _themeManager.ActiveTheme.Theme);
                vsToolStripExtender.SetStyle(_quickConnectToolStrip, _themeManager.ActiveTheme.Version, _themeManager.ActiveTheme.Theme);
                vsToolStripExtender.SetStyle(_externalToolsToolStrip, _themeManager.ActiveTheme.Version, _themeManager.ActiveTheme.Theme);

                if (!_themeManager.ActiveAndExtended) return;
                tsContainer.TopToolStripPanel.BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("CommandBarMenuDefault_Background");
                BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
                ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace("Error applying theme", ex, MessageClass.WarningMsg);
            }
        }

        private async void FrmMain_Shown(object sender, EventArgs e)
        {
            // Bring the main window to the front after splash screen closes
            ProgramRoot.CloseSplash();
            BringWindowToForeground();

            PromptForUpdatesPreference();
            await CheckForUpdates();
        }

        /// <summary>
        /// Claims the foreground for the main window at startup.
        /// </summary>
        /// <remarks>
        /// The splash screen is closed early in <see cref="FrmMain_Load"/> so that the password
        /// prompt can appear on top of it. Loading the connections afterwards takes long enough for
        /// Windows to hand the foreground to another application, and by the time this window
        /// becomes visible the process has lost its activation right - so a plain
        /// SetForegroundWindow is ignored and the window is left behind whatever is in front.
        /// Attaching to the input queue of the thread that currently owns the foreground restores
        /// the right for the duration of the call.
        /// </remarks>
        private void BringWindowToForeground()
        {
            // Starting minimized (optionally to the tray) is a deliberate choice - do not undo it.
            if (WindowState == FormWindowState.Minimized) return;

            uint currentThreadId = NativeMethods.GetCurrentThreadId();
            IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
            uint foregroundThreadId = foregroundWindow != IntPtr.Zero
                ? NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _)
                : 0;

            bool attached = foregroundThreadId != 0 && foregroundThreadId != currentThreadId &&
                            NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
            try
            {
                Activate();
                BringToFront();
                NativeMethods.SetForegroundWindow(Handle);
            }
            finally
            {
                if (attached) NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }

        private void PromptForUpdatesPreference()
        {
            if (!CommonRegistrySettings.AllowCheckForUpdates) return;
            if (!CommonRegistrySettings.AllowCheckForUpdatesAutomatical) return;

            if (Properties.OptionsUpdatesPage.Default.CheckForUpdatesAsked) return;

            // If the user has already explicitly disabled automatic updates via settings, don't ask again
            if (!Properties.OptionsUpdatesPage.Default.CheckForUpdatesOnStartup)
            {
                Properties.OptionsUpdatesPage.Default.CheckForUpdatesAsked = true;
                Properties.OptionsUpdatesPage.Default.Save();
                return;
            }

            string[] commandButtons =
            [
                Language.AskUpdatesCommandRecommended,
                Language.AskUpdatesCommandCustom,
                Language.AskUpdatesCommandAskLater
            ];

            CTaskDialog.ShowTaskDialogBox(this, GeneralAppInfo.ProductName, Language.AskUpdatesMainInstruction, string.Format(Language.AskUpdatesContent, GeneralAppInfo.ProductName), "", "", "", "", string.Join(" | ", commandButtons), ETaskDialogButtons.None, ESysIcons.Question, ESysIcons.Question);

            if (CTaskDialog.CommandButtonResult == 0)
            {
                // Use Recommended Settings: enable automatic updates with the default frequency
                Properties.OptionsUpdatesPage.Default.CheckForUpdatesOnStartup = true;
                if (Properties.OptionsUpdatesPage.Default.CheckForUpdatesFrequencyDays < 1)
                    Properties.OptionsUpdatesPage.Default.CheckForUpdatesFrequencyDays = 14;
                Properties.OptionsUpdatesPage.Default.CheckForUpdatesAsked = true;
                Properties.OptionsUpdatesPage.Default.Save();
            }
            else if (CTaskDialog.CommandButtonResult == 1)
            {
                // Customize: let the user configure update settings manually, then open Options
                Properties.OptionsUpdatesPage.Default.CheckForUpdatesAsked = true;
                Properties.OptionsUpdatesPage.Default.Save();
                AppWindows.Show(WindowType.Options);
                if (AppWindows.OptionsFormWindow != null)
                    AppWindows.OptionsFormWindow.SetActivatedPage(Language.Updates);
            }
            // For "Ask Later" (button 2), CheckForUpdatesAsked remains false so the dialog will show again next startup
        }

        private async Task CheckForUpdates()
        {
            if (!CommonRegistrySettings.AllowCheckForUpdates) return;
            if (!CommonRegistrySettings.AllowCheckForUpdatesAutomatical) return;

            if (!Properties.OptionsUpdatesPage.Default.CheckForUpdatesOnStartup) return;
            if (Properties.OptionsUpdatesPage.Default.CheckForUpdatesFrequencyDays == 0) return;

            DateTime nextUpdateCheck = Convert.ToDateTime(Properties.OptionsUpdatesPage.Default.CheckForUpdatesLastCheck.Add(TimeSpan.FromDays(Convert.ToDouble(Properties.OptionsUpdatesPage.Default.CheckForUpdatesFrequencyDays))));

            if (!Properties.OptionsUpdatesPage.Default.UpdatePending && DateTime.UtcNow <= nextUpdateCheck) return;
            if (!IsHandleCreated)
                CreateHandle(); // Make sure the handle is created so that InvokeRequired returns the correct result

            await Startup.Instance.CheckForUpdate();
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Everything below is ordered so that nothing is taken apart before the user has said
            // it may be. It used to run the other way round: the windows were closed first, then
            // IsClosing was set, then the form was hidden, and only then was the question asked.
            //
            // That produced two questions for one action. Closing the connection windows while
            // IsClosing was still false let each of them ask its own "close this panel?" - the
            // very question they check IsClosing to avoid at exit - and the exit question then
            // arrived afterwards, about panels that had already gone. Answering No to it set
            // e.Cancel on a form that was already hidden, with its windows closed and IsClosing
            // stuck true. The application stayed alive and gutted, which from the outside looks
            // exactly like having exited anyway.

            // The tray is not an exit at all: the window is going away, nothing is being closed.
            if (Properties.OptionsAppearancePage.Default.CloseToTray)
            {
                Runtime.NotificationAreaIcon ??= new NotificationAreaIcon();

                if (WindowState == FormWindowState.Normal || WindowState == FormWindowState.Maximized)
                {
                    Hide();
                    WindowState = FormWindowState.Minimized;
                    e.Cancel = true;
                    return;
                }
            }

            // Counted before anything closes, which is the only time the answer is true.
            int openConnections = 0;
            foreach (IDockContent dc in pnlDock.Contents)
            {
                if (dc is not ConnectionWindow cw) continue;
                if (cw.Controls.Count < 1) continue;
                // Not Controls[0]: see the note in ConnectionInitiator.FindConnectionContainer.
                if (cw.Controls.Cast<Control>().OfType<DockPanel>().FirstOrDefault() is not DockPanel dp) continue;
                openConnections += dp.Contents.Count;
            }

            if (openConnections > 0 &&
                (Properties.Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.All |
                 (Properties.Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.Multiple &
                  openConnections > 1) || Properties.Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.Exit))
            {
                DialogResult result = CTaskDialog.MessageBox(this, Application.ProductName, Language.ConfirmExitMainInstruction, "", "", "", Language.CheckboxDoNotShowThisMessageAgain, ETaskDialogButtons.YesNo, ESysIcons.Question, ESysIcons.Question);
                if (CTaskDialog.VerificationChecked)
                {
                    Properties.Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseEnum.Never;
                }

                if (result == DialogResult.No)
                {
                    // Nothing has been touched yet, so No leaves the application exactly as it was.
                    e.Cancel = true;
                    return;
                }
            }

            // Set before the windows are closed, not after: this is what tells each of them that
            // the closing is part of an exit the user has already agreed to, so they do not ask
            // again one by one.
            IsClosing = true;

            if (Runtime.WindowList != null)
            {
                // Copied out first: closing a window takes it off this list, and WindowList is a
                // CollectionBase being walked while it changes underneath.
                List<BaseWindow> windows = new();
                foreach (BaseWindow window in Runtime.WindowList)
                    windows.Add(window);

                foreach (BaseWindow window in windows)
                    window.Close();
            }

            Hide();

            NativeMethods.ChangeClipboardChain(Handle, _fpChainedWindowHandle);
            SystemEvents.DisplaySettingsChanged -= _advancedWindowMenu.OnDisplayChanged;
            Shutdown.Cleanup(_quickConnectToolStrip, _externalToolsToolStrip, this);

            Shutdown.StartUpdate();

            Debug.Print("[END] - " + Convert.ToString(DateTime.Now, CultureInfo.InvariantCulture));
        }

        #endregion

        #region Timer

        private void TmrAutoSave_Tick(object sender, EventArgs e)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg, "Doing AutoSave");
            Runtime.ConnectionsService.SaveConnectionsAsync();
        }

        #endregion

        #region Window Overrides and DockPanel Stuff

        private void FrmMain_ResizeBegin(object sender, EventArgs e)
        {
            _inSizeMove = true;
        }

        private void FrmMain_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                if (!Properties.OptionsAppearancePage.Default.MinimizeToTray) return;
                Runtime.NotificationAreaIcon ??= new NotificationAreaIcon();

                Hide();
            }
            else
            {
                PreviousWindowState = WindowState;
            }
        }

        private void FrmMain_ResizeEnd(object sender, EventArgs e)
        {
            _inSizeMove = false;
            // This handles activations from clicks that started a size/move operation
            ActivateConnection();
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            // Listen for and handle operating system messages
            try
            {
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (m.Msg)
                {
                    case NativeMethods.WM_MOUSEACTIVATE:
                        _inMouseActivate = true;
                        break;
                    case NativeMethods.WM_ACTIVATEAPP:
                        bool appIsActivating = m.WParam != IntPtr.Zero;
                        if (!appIsActivating)
                            _connectionHadFocusOnDeactivate = ConnectionHasFocus();

                        Control candidateTabToFocus = FromChildHandle(NativeMethods.WindowFromPoint(MousePosition))
                                               ?? GetChildAtPoint(MousePosition);
                        if (candidateTabToFocus is InterfaceControl)
                        {
                            candidateTabToFocus.Parent.Focus();
                        }
                        else if (appIsActivating && !_inMouseActivate && _connectionHadFocusOnDeactivate &&
                                 !Properties.OptionsStartupExitPage.Default.DisableRefocus)
                        {
                            // Keyboard activation (Alt+Tab, taskbar) never matches the mouse based lookup
                            // above, because the cursor is usually nowhere near the connection. The
                            // WM_WINDOWPOSCHANGED handler does call ActivateConnection(), but it runs
                            // before the activation sequence has finished, so the focus it sets is
                            // sometimes discarded. Retry once the message loop has settled.
                            BeginInvoke(new Action(ActivateConnection));
                        }

                        _inMouseActivate = false;
                        break;
                    case NativeMethods.WM_ACTIVATE:
                        // Only handle this msg if it was triggered by a click
                        if (NativeMethods.LOWORD(m.WParam) == NativeMethods.WA_CLICKACTIVE)
                        {
                            Control controlThatWasClicked = FromChildHandle(NativeMethods.WindowFromPoint(MousePosition))
                                                     ?? GetChildAtPoint(MousePosition);
                            if (controlThatWasClicked != null)
                            {
                                if (controlThatWasClicked is TreeView ||
                                    controlThatWasClicked is ComboBox ||
                                    controlThatWasClicked is MrngTextBox ||
                                    controlThatWasClicked is FrmMain)
                                {
                                    controlThatWasClicked.Focus();
                                }
                                else if (controlThatWasClicked.CanSelect ||
                                         controlThatWasClicked is MenuStrip ||
                                         controlThatWasClicked is ToolStrip)
                                {
                                    // Simulate a mouse event since one wasn't generated by Windows
                                    SimulateClick(controlThatWasClicked);
                                    controlThatWasClicked.Focus();
                                }
                                else if (controlThatWasClicked is AutoHideStripBase)
                                {
                                    // only focus the autohide toolstrip
                                    controlThatWasClicked.Focus();
                                }
                                else
                                {
                                    // This handles activations from clicks that did not start a size/move operation
                                    ActivateConnection();
                                }
                            }
                        }
                        break;
                    case NativeMethods.WM_WINDOWPOSCHANGED:
                        // Ignore this message if the window wasn't activated
                        NativeMethods.WINDOWPOS windowPos =
                            (NativeMethods.WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(NativeMethods.WINDOWPOS));
                        if ((windowPos.flags & NativeMethods.SWP_NOACTIVATE) == 0)
                        {
                            if (!_inMouseActivate && !_inSizeMove)
                                ActivateConnection();
                        }
                        break;
                    case NativeMethods.WM_SYSCOMMAND:
                        if (m.WParam == new IntPtr(0))
                            ShowHideMenu();
                        Screen screen = _advancedWindowMenu.GetScreenById(m.WParam.ToInt32());
                        if (screen != null)
                        {
                            Screens.SendFormToScreen(screen);
                            Console.WriteLine(_advancedWindowMenu.GetScreenById(m.WParam.ToInt32()).ToString());
                        }
                        break;
                    case NativeMethods.WM_DRAWCLIPBOARD:
                        NativeMethods.SendMessage(_fpChainedWindowHandle, m.Msg, m.LParam, m.WParam);
                        _clipboardChangedEvent?.Invoke();
                        break;
                    case NativeMethods.WM_CHANGECBCHAIN:
                        // When a clipboard viewer window receives the WM_CHANGECBCHAIN message, 
                        // it should call the SendMessage function to pass the message to the 
                        // next window in the chain, unless the next window is the window 
                        // being removed. In this case, the clipboard viewer should save 
                        // the handle specified by the lParam parameter as the next window in the chain. 
                        //
                        // wParam is the Handle to the window being removed from 
                        // the clipboard viewer chain 
                        // lParam is the Handle to the next window in the chain 
                        // following the window being removed. 
                        if (m.WParam == _fpChainedWindowHandle) {
                            // If wParam is the next clipboard viewer then it
                            // is being removed so update pointer to the next
                            // window in the clipboard chain
                            _fpChainedWindowHandle = m.LParam;
                        } else {
                            //Send to the next window
                            NativeMethods.SendMessage(_fpChainedWindowHandle, m.Msg, m.LParam, m.WParam);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace("frmMain WndProc failed", ex);
            }

            base.WndProc(ref m);
        }

        private static void SimulateClick(Control control)
        {
            Point clientMousePosition = control.PointToClient(MousePosition);
            int temp_wLow = clientMousePosition.X;
            int temp_wHigh = clientMousePosition.Y;
            NativeMethods.SendMessage(control.Handle, NativeMethods.WM_LBUTTONDOWN, (IntPtr)NativeMethods.MK_LBUTTON,
                                      (IntPtr)NativeMethods.MAKELPARAM(ref temp_wLow, ref temp_wHigh));
            clientMousePosition.X = temp_wLow;
            clientMousePosition.Y = temp_wHigh;
        }

        /// <summary>
        /// Determines whether the control that currently has the keyboard focus lives inside a
        /// <see cref="ConnectionTab"/>. Recorded when the application is deactivated so that the
        /// refocus fallback in WM_ACTIVATEAPP does not steal the focus away from e.g. the
        /// connection tree search box.
        /// </summary>
        /// <remarks>
        /// The WinForms ActiveControl chain is not usable here: once another panel has been clicked
        /// it keeps pointing at that panel even while the RDP ActiveX control holds the real
        /// keyboard focus. Ask the operating system instead and only fall back to ActiveControl
        /// when no window on this thread has the focus.
        /// </remarks>
        private bool ConnectionHasFocus()
        {
            Control focusedControl = FromChildHandle(NativeMethods.GetFocus());

            if (focusedControl == null)
            {
                focusedControl = this;
                int depth = 0;
                while (focusedControl is ContainerControl container &&
                       container.ActiveControl != null &&
                       container.ActiveControl != focusedControl &&
                       depth++ < 16)
                {
                    focusedControl = container.ActiveControl;
                }
            }

            for (Control control = focusedControl; control != null; control = control.Parent)
            {
                if (control is ConnectionTab) return true;
            }

            return false;
        }

        /// <summary>
        /// True while the foreground belongs to this application.
        /// </summary>
        /// <remarks>
        /// The process id of the foreground window is not enough. PuTTY is reparented into a tab
        /// with SetParent but keeps its overlapped window style instead of becoming a WS_CHILD, so
        /// while an SSH tab is in use GetForegroundWindow() returns a window owned by the PuTTY
        /// process. Walk up to the root of the parent chain, which is our own form in that case.
        /// </remarks>
        internal static bool ApplicationIsInForeground()
        {
            IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero) return false;

            IntPtr rootWindow = NativeMethods.GetAncestor(foregroundWindow, NativeMethods.GA_ROOT);
            if (rootWindow == IntPtr.Zero) rootWindow = foregroundWindow;

            _ = NativeMethods.GetWindowThreadProcessId(rootWindow, out uint rootProcessId);
            return rootProcessId == (uint)Environment.ProcessId;
        }

        private void ActivateConnection()
        {
            // Refocusing must never pull the application to the front. PuttyBase.Focus() and the
            // terminal, PowerShell, WSL and external program protocols all focus themselves with
            // SetForegroundWindow, and because their windows are reparented into this form that
            // call activates this window - cancelling an Alt+Tab the user just performed, or
            // getting refused and leaving the focus somewhere invisible.
            if (!ApplicationIsInForeground()) return;

            // Ask the docking library which document is active rather than going through
            // ConnectionWindow.ActiveControl - that goes stale as soon as another panel is clicked,
            // and then this method silently did nothing for the rest of the session.
            if (pnlDock.ActiveDocument is not ConnectionWindow cw) return;

            InterfaceControl ifc = cw.GetInterfaceControl();
            if (ifc == null) return;

            ifc.Protocol.Focus();
            Form conFormWindow = ifc.FindForm();
            (conFormWindow as ConnectionTab)?.RefreshInterfaceController();
        }

        private void PnlDock_ActiveDocumentChanged(object sender, EventArgs e)
        {
            ActivateConnection();
        }

        internal void UpdateWindowTitle()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(UpdateWindowTitle));
                return;
            }

            StringBuilder titleBuilder = new(Application.ProductName);
#if DEBUG
            // A debug and a release build looked identical in the title bar while sharing one
            // confCons.xml - whichever saved last overwrote the other. With two copies running
            // there was nothing on screen to say which window was which.
            titleBuilder.Append(" [Debug]");
#endif
            const string separator = " - ";

            if (Runtime.ConnectionsService.IsConnectionsFileLoaded)
            {
                if (Runtime.ConnectionsService.UsingDatabase)
                {
                    titleBuilder.Append(separator);
                    titleBuilder.Append(Language.SQLServer.TrimEnd(':'));
                }
                else
                {
                    if (!string.IsNullOrEmpty(Runtime.ConnectionsService.ConnectionFileName))
                    {
                        titleBuilder.Append(separator);
                        titleBuilder.Append(Properties.OptionsAppearancePage.Default.ShowCompleteConsPathInTitle ? Runtime.ConnectionsService.ConnectionFileName : Path.GetFileName(Runtime.ConnectionsService.ConnectionFileName));
                    }
                }
            }

            if (!string.IsNullOrEmpty(SelectedConnection?.Name))
            {
                titleBuilder.Append(separator);
                titleBuilder.Append(SelectedConnection.Name);

                if (Properties.Settings.Default.TrackActiveConnectionInConnectionTree)
                    AppWindows.TreeForm.JumpToNode(SelectedConnection);
            }

            Text = titleBuilder.ToString();
        }

        /// <summary>
        /// Catches F11 whatever has the focus, and whether or not the menu bar is on screen.
        /// </summary>
        /// <remarks>
        /// F11 is the View menu item's shortcut, and a ToolStripMenuItem's shortcut is only
        /// delivered while its menu strip is visible. Fullscreen hides the menu bar, so the very
        /// key that would leave fullscreen stopped working the moment it was used - one way in,
        /// no way out. Sessions in the native terminal are covered separately, by the page.
        /// </remarks>
        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
        {
            if (keyData == Keys.F11)
            {
                ToggleFullscreen();
                return true;
            }

            // Kept from the designer file, where it had no business being: a way back to the menu
            // bar if anything ever leaves it hidden.
            if (keyData == (Keys.Alt | Keys.Menu) && !msMain.Visible)
                msMain.Visible = true;

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// Turns fullscreen on or off.
        /// </summary>
        /// <remarks>
        /// Public because a native SSH session has to ask for it. The terminal is a page in a
        /// WebView2 control, the page holds the keyboard, and the WinForms control gives the host
        /// no way to see an accelerator first - so F11 never reaches the menu item that owns it.
        /// The page catches the key and calls this instead.
        /// </remarks>
        public void ToggleFullscreen()
        {
            Fullscreen.Value = !Fullscreen.Value;
            viewMenu.SetFullscreenChecked(Fullscreen.Value);
        }

        /// <summary>
        /// Takes the tab strips away while fullscreen, and puts them back afterwards.
        /// </summary>
        /// <remarks>
        /// Both of them: the panel tabs across the top of the main dock, and the session tabs
        /// inside every tab group. Hiding the menu and the toolbars alone still left two rows of
        /// tabs between the session and the edge of the screen.
        /// </remarks>
        private void SetTabStripsForFullscreen(bool fullscreen)
        {
            if (fullscreen)
            {
                if (pnlDock.DocumentStyle != DocumentStyle.DockingSdi)
                {
                    pnlDock.DocumentStyle = DocumentStyle.DockingSdi;
                    pnlDock.Size = new Size(1, 1);
                }
            }
            else
            {
                // Works out for itself which style belongs here, so a panel opened while
                // fullscreen is accounted for.
                ShowHidePanelTabs();
            }

            if (Runtime.WindowList == null) return;

            for (int i = 0; i < Runtime.WindowList.Count; i++)
                (Runtime.WindowList[i] as ConnectionWindow)?.ShowSessionTabs(!fullscreen);

            // Last, once the panes have been rebuilt: whatever had the keyboard before is gone
            // with them.
            (pnlDock.ActiveDocument as ConnectionWindow)?.FocusActiveSession();
        }

        public void ShowHidePanelTabs(DockContent closingDocument = null)
        {
            DocumentStyle newDocumentStyle;

            if (Properties.OptionsTabsPanelsPage.Default.AlwaysShowPanelTabs)
            {
                newDocumentStyle = DocumentStyle.DockingWindow; // Show the panel tabs
            }
            else
            {
                int nonConnectionPanelCount = 0;
                foreach (IDockContent dockContent in pnlDock.Documents)
                {
                    DockContent document = (DockContent)dockContent;
                    if ((closingDocument == null || document != closingDocument) && document is not ConnectionWindow)
                    {
                        nonConnectionPanelCount++;
                    }
                }

                newDocumentStyle = nonConnectionPanelCount == 0
                    ? DocumentStyle.DockingSdi
                    : DocumentStyle.DockingWindow;
            }

            // TODO: See if we can get this to work with DPS
#if false
            foreach (var dockContent in pnlDock.Documents)
			{
				var document = (DockContent)dockContent;
				if (document is ConnectionWindow)
				{
					var connectionWindow = (ConnectionWindow)document;
					if (Settings.Default.AlwaysShowConnectionTabs == false)
					{
						connectionWindow.TabController.HideTabsMode = TabControl.HideTabsModes.HidepnlDock.DockLeftPortion = Always;
					}
					else
					{
						connectionWindow.TabController.HideTabsMode = TabControl.HideTabsModes.ShowAlways;
					}
				}
			}
#endif

            if (pnlDock.DocumentStyle == newDocumentStyle) return;
            pnlDock.DocumentStyle = newDocumentStyle;
            pnlDock.Size = new Size(1, 1);
        }

        public void SetDefaultLayout()
        {
            pnlDock.Visible = false;

            AppWindows.TreeForm.Show(pnlDock, DockState.DockLeft);

            // Below the tree, not in the same place as it. Both were shown at DockLeft, which put
            // them in one pane as two tabs - and since the properties were shown second, they came
            // up in front and the connection tree was behind them, out of sight. On a fresh start
            // the program looked as though it had lost every connection.
            AppWindows.ConfigForm.Show(AppWindows.TreeForm.Pane, DockAlignment.Bottom, 0.4);

            AppWindows.ErrorsForm.Show(pnlDock, DockState.DockBottomAutoHide);

            // And the tree is what the window is for, so it is the one holding the focus.
            AppWindows.TreeForm.Activate();
            viewMenu._mMenViewErrorsAndInfos.Checked = true;

            ShowFileMenu();

            pnlDock.Visible = true;
        }

        /// <summary>
        /// Makes sure the menu bar is on screen.
        /// </summary>
        /// <remarks>
        /// Its counterpart is gone. HideFileMenu set msMain.Visible to false - the whole menu bar,
        /// not just the File menu - and the only way back was the View menu, which had just
        /// vanished with it. It told you to press Alt, which does nothing to a hidden MenuStrip.
        /// One click and the program had no menu until someone edited the settings by hand.
        /// </remarks>
        /// <summary>
        /// Moves to the next or previous open session, whoever is asking.
        /// </summary>
        /// <remarks>
        /// The Sessions menu is one caller; the native terminal is the other, and the important
        /// one - a shortcut for switching sessions is worth nothing if it only works when the
        /// focus is somewhere other than a session.
        /// </remarks>
        public void NextSession() => sessionsMenu.NextSession();

        public void PreviousSession() => sessionsMenu.PreviousSession();

        /// <summary>
        /// Jumps to the numbered session, counting from one as the menu labels it.
        /// </summary>
        public void JumpToSession(int number) => sessionsMenu.JumpToSession(number - 1);

        public void ShowFileMenu()
        {
            msMain.Visible = true;
        }


        public void SetLayout()
        {
            pnlDock.Visible = false;

            if (Properties.Settings.Default.ViewMenuMessages == true)
            {
                AppWindows.ErrorsForm.Show(pnlDock, DockState.DockBottomAutoHide);
                viewMenu._mMenViewErrorsAndInfos.Checked = true;
            }
            else
                viewMenu._mMenViewErrorsAndInfos.Checked = false;


            if (Properties.Settings.Default.ViewMenuExternalTools == true)
            {
                viewMenu.TsExternalTools.Visible = true;
                viewMenu._mMenViewExtAppsToolbar.Checked = true;
            }
            else
            {
                viewMenu.TsExternalTools.Visible = false;
                viewMenu._mMenViewExtAppsToolbar.Checked = false;
            }

            if (Properties.Settings.Default.ViewMenuQuickConnect == true)
            {
                viewMenu.TsQuickConnect.Visible = true;
                viewMenu._mMenViewQuickConnectToolbar.Checked = true;
            }
            else
            {
                viewMenu.TsQuickConnect.Visible = false;
                viewMenu._mMenViewQuickConnectToolbar.Checked = false;
            }

            pnlDock.Visible = true;
        }

        public void ShowHideMenu() => tsContainer.TopToolStripPanelVisible = !tsContainer.TopToolStripPanelVisible;

        #endregion

        #region Events

        public delegate void ClipboardchangeEventHandler();

        public static event ClipboardchangeEventHandler ClipboardChanged
        {
            add =>
                _clipboardChangedEvent =
                    (ClipboardchangeEventHandler)Delegate.Combine(_clipboardChangedEvent, value);
            remove =>
                _clipboardChangedEvent =
                    (ClipboardchangeEventHandler)Delegate.Remove(_clipboardChangedEvent, value);
        }

        #endregion

        private void ViewMenu_Opening(object sender, EventArgs e)
        {
            viewMenu.mMenView_DropDownOpening(sender, e);
        }

        private void TsModeUser_Click(object sender, EventArgs e)
        {
            Properties.OptionsRbac.Default.ActiveRole = "UserRole";
        }

        private void TsModeAdmin_Click(object sender, EventArgs e)
        {
            Properties.OptionsRbac.Default.ActiveRole = "AdminRole";
        }
    }
}