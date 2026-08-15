using System;
using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using mRemoteNG.App;
using System.Threading;
using System.Globalization;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.App.Info;
using mRemoteNG.Messages;
using mRemoteNG.Tools;
using mRemoteNG.UI.Controls;
using mRemoteNG.UI.Forms;
using System.Runtime.Versioning;

namespace mRemoteNG.Config.Settings
{
    [SupportedOSPlatform("windows")]
    public class SettingsLoader
    {
        private readonly ExternalAppsLoader _externalAppsLoader;
        private readonly MessageCollector _messageCollector;
        private readonly MenuStrip _mainMenu;
        private readonly QuickConnectToolStrip _quickConnectToolStrip;
        private readonly ExternalToolsToolStrip _externalToolsToolStrip;
        private readonly MultiSshToolStrip _multiSshToolStrip;

        private FrmMain MainForm { get; }


        public SettingsLoader(FrmMain mainForm, MessageCollector messageCollector, QuickConnectToolStrip quickConnectToolStrip, ExternalToolsToolStrip externalToolsToolStrip, MultiSshToolStrip multiSshToolStrip, MenuStrip mainMenu)
        {
            MainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));
            _quickConnectToolStrip = quickConnectToolStrip ?? throw new ArgumentNullException(nameof(quickConnectToolStrip));
            _externalToolsToolStrip = externalToolsToolStrip ?? throw new ArgumentNullException(nameof(externalToolsToolStrip));
            _multiSshToolStrip = multiSshToolStrip ?? throw new ArgumentNullException(nameof(multiSshToolStrip));
            _mainMenu = mainMenu ?? throw new ArgumentNullException(nameof(mainMenu));
            _externalAppsLoader = new ExternalAppsLoader(MainForm, messageCollector, _externalToolsToolStrip);
        }

        #region Public Methods

        public void LoadSettings()
        {
            try
            {
                EnsureSettingsAreSavedInNewestVersion();

                SetSupportedCulture();
                SetApplicationWindowPositionAndSize();
                SetKioskMode();

                SetPuttyPath();
                SetShowSystemTrayIcon();
                SetAutoSave();
                LoadExternalAppsFromXml();

                if (Properties.App.Default.ResetToolbars)
                    SetToolbarsDefault();
                else
                    LoadToolbarsFromSettings();
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("Loading settings failed", ex);
            }
        }


        private void SetSupportedCulture()
        {
            if (Properties.Settings.Default.OverrideUICulture == "" || !SupportedCultures.IsNameSupported(Properties.Settings.Default.OverrideUICulture)) return;
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(Properties.Settings.Default.OverrideUICulture);
            _messageCollector.AddMessage(MessageClass.InformationMsg, $"Override Culture: {Thread.CurrentThread.CurrentUICulture.Name}/{Thread.CurrentThread.CurrentUICulture.NativeName}", true);
        }

        private void SetApplicationWindowPositionAndSize()
        {
            MainForm.WindowState = FormWindowState.Normal;

            bool startsMaximized = Properties.App.Default.MainFormState == FormWindowState.Maximized;
            Size savedSize = startsMaximized
                ? Properties.App.Default.MainFormRestoreSize
                : Properties.App.Default.MainFormSize;
            Point savedLocation = startsMaximized
                ? Properties.App.Default.MainFormRestoreLocation
                : Properties.App.Default.MainFormLocation;

            if (!savedLocation.IsEmpty)
                MainForm.Location = savedLocation;
            if (!savedSize.IsEmpty)
                MainForm.Size = savedSize;
            else
                SizeToMostOfTheScreen();

            if (startsMaximized)
            {
                MainForm.WindowState = FormWindowState.Maximized;
            }

            // Make sure the form is visible on the screen
            const int minHorizontal = 300;
            const int minVertical = 150;
            Rectangle screenBounds = Screen.FromHandle(MainForm.Handle).Bounds;
            Rectangle newBounds = MainForm.Bounds;

            if (newBounds.Right < screenBounds.Left + minHorizontal)
                newBounds.X = screenBounds.Left + minHorizontal - newBounds.Width;
            if (newBounds.Left > screenBounds.Right - minHorizontal)
                newBounds.X = screenBounds.Right - minHorizontal;
            if (newBounds.Bottom < screenBounds.Top + minVertical)
                newBounds.Y = screenBounds.Top + minVertical - newBounds.Height;
            if (newBounds.Top > screenBounds.Bottom - minVertical)
                newBounds.Y = screenBounds.Bottom - minVertical;

            MainForm.Location = newBounds.Location;
        }

        /// <summary>
        /// Fraction of the screen the main window gets when it has no remembered size.
        /// </summary>
        private const double DefaultScreenFraction = 0.9;

        /// <summary>
        /// Sizes the main window to most of its screen, centred.
        /// </summary>
        /// <remarks>
        /// Only used when nothing has been remembered yet. Without it the window falls back to the
        /// size the designer gave it, which is a small dialog - so restoring a window that started
        /// maximised produced something far too small to work in.
        /// </remarks>
        private void SizeToMostOfTheScreen()
        {
            Rectangle workingArea = Screen.FromHandle(MainForm.Handle).WorkingArea;

            Size size = new((int)(workingArea.Width * DefaultScreenFraction),
                            (int)(workingArea.Height * DefaultScreenFraction));

            MainForm.Size = size;
            MainForm.Location = new Point(workingArea.Left + (workingArea.Width - size.Width) / 2,
                                          workingArea.Top + (workingArea.Height - size.Height) / 2);
        }

        private void SetAutoSave()
        {
            if (Properties.OptionsConnectionsPage.Default.AutoSaveEveryMinutes <= 0) return;
            MainForm.tmrAutoSave.Interval = Properties.OptionsConnectionsPage.Default.AutoSaveEveryMinutes * 60000;
            MainForm.tmrAutoSave.Enabled = true;
        }

        private void SetKioskMode()
        {
            if (!Properties.App.Default.MainFormKiosk) return;
            MainForm.Fullscreen.Value = true;
        }

        private static void SetShowSystemTrayIcon()
        {
            if (Properties.OptionsAppearancePage.Default.ShowSystemTrayIcon)
                Runtime.NotificationAreaIcon = new NotificationAreaIcon();
        }

        private static void SetPuttyPath()
        {
            PuttyBase.PuttyPath = Properties.OptionsAdvancedPage.Default.UseCustomPuttyPath ? Properties.OptionsAdvancedPage.Default.CustomPuttyPath : GeneralAppInfo.PuttyPath;
        }

        private void EnsureSettingsAreSavedInNewestVersion()
        {
            // TODO: is this ever true and run?
            if (Properties.App.Default.DoUpgrade)
                UpgradeSettingsVersion();
        }

        private void UpgradeSettingsVersion()
        {
            try
            {
                Properties.Settings.Default.Save();
                Properties.Settings.Default.Upgrade();
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("Settings.Upgrade() failed", ex);
            }

            Properties.App.Default.DoUpgrade = false;

            // Clear pending update flag
            // This is used for automatic updates, not for settings migration, but it
            // needs to be cleared here because we know that we just updated.
            Properties.OptionsUpdatesPage.Default.UpdatePending = false;
        }

        private void SetToolbarsDefault()
        {
            ToolStripPanelFromString("top").Join(_quickConnectToolStrip, new Point(300, 0));
            _quickConnectToolStrip.Visible = true;
            ToolStripPanelFromString("bottom").Join(_externalToolsToolStrip, new Point(3, 0));
            _externalToolsToolStrip.Visible = false;
        }

        private void LoadToolbarsFromSettings()
        {
            ResetAllToolbarLocations();
            AddMainMenuPanel();
            AddExternalAppsPanel();
            AddQuickConnectPanel();
            AddMultiSshPanel();
        }

        /// <summary>
        /// This prevents odd positioning issues due to toolbar load order.
        /// Since all toolbars start in this temp panel, no toolbar load
        /// can be blocked by pre-existing toolbars.
        /// </summary>
        private void ResetAllToolbarLocations()
        {
            ToolStripPanel tempToolStrip = new();
            tempToolStrip.Join(_mainMenu);
            tempToolStrip.Join(_quickConnectToolStrip);
            tempToolStrip.Join(_externalToolsToolStrip);
            tempToolStrip.Join(_multiSshToolStrip);
        }

        private void AddMainMenuPanel()
        {
            SetToolstripGripStyle(_mainMenu);
            ToolStripPanel toolStripPanel = ToolStripPanelFromString("top");
            toolStripPanel.Join(_mainMenu, new Point(3, 0));
        }

        private void AddQuickConnectPanel()
        {
            SetToolstripGripStyle(_quickConnectToolStrip);
            _quickConnectToolStrip.Visible = Properties.Settings.Default.QuickyTBVisible;
            ToolStripPanel toolStripPanel = ToolStripPanelFromString(Properties.Settings.Default.QuickyTBParentDock);
            toolStripPanel.Join(_quickConnectToolStrip, Properties.Settings.Default.QuickyTBLocation);
        }

        private void AddExternalAppsPanel()
        {
            SetToolstripGripStyle(_externalToolsToolStrip);
            _externalToolsToolStrip.Visible = Properties.Settings.Default.ExtAppsTBVisible;
            ToolStripPanel toolStripPanel = ToolStripPanelFromString(Properties.Settings.Default.ExtAppsTBParentDock);
            toolStripPanel.Join(_externalToolsToolStrip, Properties.Settings.Default.ExtAppsTBLocation);
        }

        private void AddMultiSshPanel()
        {
            SetToolstripGripStyle(_multiSshToolStrip);
            _multiSshToolStrip.Visible = Properties.Settings.Default.MultiSshToolbarVisible;
            ToolStripPanel toolStripPanel = ToolStripPanelFromString(Properties.Settings.Default.MultiSshToolbarParentDock);
            toolStripPanel.Join(_multiSshToolStrip, Properties.Settings.Default.MultiSshToolbarLocation);
        }

        private void SetToolstripGripStyle(ToolStrip toolbar)
        {
            toolbar.GripStyle = Properties.Settings.Default.LockToolbars ? ToolStripGripStyle.Hidden : ToolStripGripStyle.Visible;
        }

        private ToolStripPanel ToolStripPanelFromString(string panel)
        {
            switch (panel.ToLower())
            {
                case "top":
                    return MainForm.tsContainer.TopToolStripPanel;
                case "bottom":
                    return MainForm.tsContainer.BottomToolStripPanel;
                case "left":
                    return MainForm.tsContainer.LeftToolStripPanel;
                case "right":
                    return MainForm.tsContainer.RightToolStripPanel;
                default:
                    return MainForm.tsContainer.TopToolStripPanel;
            }
        }

        private void LoadExternalAppsFromXml()
        {
            _externalAppsLoader.LoadExternalAppsFromXML();
        }

        #endregion
    }
}