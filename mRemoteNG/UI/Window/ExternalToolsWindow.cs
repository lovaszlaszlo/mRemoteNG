using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using BrightIdeasSoftware;
using mRemoteNG.App;
using mRemoteNG.Config.Settings;
using mRemoteNG.Tools;
using WeifenLuo.WinFormsUI.Docking;
using mRemoteNG.UI.Forms;
using mRemoteNG.Themes;
using mRemoteNG.Tools.CustomCollections;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;

namespace mRemoteNG.UI.Window
{
    [SupportedOSPlatform("windows")]
    public partial class ExternalToolsWindow
    {
        private readonly ExternalAppsSaver _externalAppsSaver;
        private readonly ThemeManager _themeManager;
        private readonly FullyObservableCollection<ExternalTool> _currentlySelectedExternalTools;

        /// <summary>
        /// The tools as they stand on disk, to go back to.
        /// </summary>
        private List<ExternalTool> _savedState = [];

        public ExternalToolsWindow()
        {
            InitializeComponent();
            Icon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.Console_16x);
            WindowType = WindowType.ExternalApps;
            DockPnl = new DockContent();
            _themeManager = ThemeManager.getInstance();
            _themeManager.ThemeChanged += ApplyTheme;
            _externalAppsSaver = new ExternalAppsSaver();
            _currentlySelectedExternalTools = [];
            _currentlySelectedExternalTools.CollectionUpdated += CurrentlySelectedExternalToolsOnCollectionUpdated;
            BrowseButton.Height = FilenameTextBox.Height;
            BrowseWorkingDir.Height = WorkingDirTextBox.Height;
        }


        #region Private Methods

        private void ExternalTools_Load(object sender, EventArgs e)
        {
            ApplyLanguage();
            ApplyTheme();
            UpdateToolsListObjView();
            TakeSnapshot();
        }

        /// <summary>
        /// Remembers the tools as they are now, so a discard has something to go back to.
        /// </summary>
        private void TakeSnapshot()
        {
            _savedState = Runtime.ExternalToolsService.ExternalTools.Select(Copy).ToList();
        }

        private static ExternalTool Copy(ExternalTool tool) =>
            new(tool.DisplayName, tool.FileName, tool.Arguments, tool.WorkingDir, tool.RunElevated)
            {
                WaitForExit = tool.WaitForExit,
                TryIntegrate = tool.TryIntegrate,
                ShowOnToolbar = tool.ShowOnToolbar
            };

        private static bool Same(ExternalTool a, ExternalTool b) =>
            a.DisplayName == b.DisplayName &&
            a.FileName == b.FileName &&
            a.Arguments == b.Arguments &&
            a.WorkingDir == b.WorkingDir &&
            a.WaitForExit == b.WaitForExit &&
            a.TryIntegrate == b.TryIntegrate &&
            a.ShowOnToolbar == b.ShowOnToolbar &&
            a.RunElevated == b.RunElevated;

        private bool HasUnsavedChanges()
        {
            IList<ExternalTool> current = Runtime.ExternalToolsService.ExternalTools;

            if (current.Count != _savedState.Count) return true;

            return current.Where((tool, i) => !Same(tool, _savedState[i])).Any();
        }

        private void SaveTools()
        {
            _externalAppsSaver.Save(Runtime.ExternalToolsService.ExternalTools);
            TakeSnapshot();
        }

        private void RestoreSnapshot()
        {
            _currentlySelectedExternalTools.Clear();
            Runtime.ExternalToolsService.ExternalTools.Clear();
            Runtime.ExternalToolsService.ExternalTools.AddRange(_savedState.Select(Copy));

            UpdateToolsListObjView();
            UpdateToolstipControls();
        }

        private void ApplyLanguage()
        {
            Text = Language.ExternalTool;
            TabText = Language.ExternalTool;

            NewToolToolstripButton.Text = Language._New;
            DeleteToolToolstripButton.Text = Language.Delete;
            LaunchToolToolstripButton.Text = Language._Launch;
            SaveToolstripButton.Text = Language.Save;
            DiscardToolstripButton.Text = Language.ExternalToolDiscard;

            DisplayNameColumnHeader.Text = Language.DisplayName;
            FilenameColumnHeader.Text = Language.Filename;
            ArgumentsColumnHeader.Text = Language.Arguments;
            WorkingDirColumnHeader.Text = Language.WorkingDirColumnHeader;
            WaitForExitColumnHeader.Text = Language.WaitForExit;
            TryToIntegrateColumnHeader.Text = Language.TryToIntegrate;
            RunElevateHeader.Text = Language.RunElevated;
            ShowOnToolbarColumnHeader.Text = Language.ShowOnToolbarColumnHeader;

            TryToIntegrateCheckBox.Text = Language.TryToIntegrate;
            ShowOnToolbarCheckBox.Text = Language.ShowOnToolbar;
            RunElevatedCheckBox.Text = Language.RunElevated;

            PropertiesGroupBox.Text = Language.ExternalToolProperties;

            DisplayNameLabel.Text = Language.DisplayName;
            FilenameLabel.Text = Language.Filename;
            ArgumentsLabel.Text = Language.Arguments;
            WorkingDirLabel.Text = Language.WorkingDirectory;
            OptionsLabel.Text = Language.Options;

            WaitForExitCheckBox.Text = Language.WaitForExit;
            BrowseButton.Text = Language._Browse;
            BrowseWorkingDir.Text = Language._Browse;
            NewToolMenuItem.Text = Language.NewExternalTool;
            DeleteToolMenuItem.Text = Language.DeleteExternalTool;
            LaunchToolMenuItem.Text = Language.LaunchExternalTool;
        }

        private new void ApplyTheme()
        {
            if (!_themeManager.ThemingActive) return;
            vsToolStripExtender.SetStyle(ToolStrip, _themeManager.ActiveTheme.Version, _themeManager.ActiveTheme.Theme);
            vsToolStripExtender.SetStyle(ToolsContextMenuStrip, _themeManager.ActiveTheme.Version,
                                         _themeManager.ActiveTheme.Theme);
            //Apply the extended palette

            ToolStripContainer.TopToolStripPanel.BackColor =
                _themeManager.ActiveTheme.Theme.ColorPalette.CommandBarMenuDefault.Background;
            ToolStripContainer.TopToolStripPanel.ForeColor =
                _themeManager.ActiveTheme.Theme.ColorPalette.CommandBarMenuDefault.Text;
            PropertiesGroupBox.BackColor =
                _themeManager.ActiveTheme.Theme.ColorPalette.CommandBarMenuDefault.Background;
            PropertiesGroupBox.ForeColor = _themeManager.ActiveTheme.Theme.ColorPalette.CommandBarMenuDefault.Text;

            // Windows draws the selection of an unfocused list in a pale grey that all but
            // disappears against a dark row, so a row selected while the editor below has the
            // focus looked like no row at all. Same colours as the focused selection.
            ToolsListObjView.UnfocusedSelectedBackColor = ToolsListObjView.SelectedBackColorOrDefault;
            ToolsListObjView.UnfocusedSelectedForeColor = ToolsListObjView.SelectedForeColorOrDefault;
        }

        private void UpdateToolsListObjView()
        {
            try
            {
                ToolsListObjView.BeginUpdate();
                ToolsListObjView.SetObjects(Runtime.ExternalToolsService.ExternalTools, true);
                ToolsListObjView.AutoResizeColumns();
                ToolsListObjView.EndUpdate();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.ExternalTools.PopulateToolsListObjView()", ex);
            }
        }

        private void LaunchTool()
        {
            try
            {
                foreach (ExternalTool externalTool in _currentlySelectedExternalTools)
                {
                    externalTool.Start();
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.ExternalTools.LaunchTool() failed.", ex);
            }
        }

        private void UpdateEditorControls()
        {
            ExternalTool selectedTool = _currentlySelectedExternalTools.FirstOrDefault();

            DisplayNameTextBox.Text = selectedTool?.DisplayName;
            FilenameTextBox.Text = selectedTool?.FileName;
            ArgumentsCheckBox.Text = selectedTool?.Arguments;
            WorkingDirTextBox.Text = selectedTool?.WorkingDir;
            WaitForExitCheckBox.Checked = selectedTool?.WaitForExit ?? false;
            TryToIntegrateCheckBox.Checked = selectedTool?.TryIntegrate ?? false;
            ShowOnToolbarCheckBox.Checked = selectedTool?.ShowOnToolbar ?? false;
            RunElevatedCheckBox.Checked = selectedTool?.RunElevated ?? false;
            WaitForExitCheckBox.Enabled = !TryToIntegrateCheckBox.Checked;
        }

        private void UpdateToolstipControls()
        {
            _currentlySelectedExternalTools.Clear();
            _currentlySelectedExternalTools.AddRange(ToolsListObjView.SelectedObjects.OfType<ExternalTool>());
            PropertiesGroupBox.Enabled = _currentlySelectedExternalTools.Count == 1;

            bool atleastOneToolSelected = _currentlySelectedExternalTools.Count > 0;
            DeleteToolMenuItem.Enabled = atleastOneToolSelected;
            DeleteToolToolstripButton.Enabled = atleastOneToolSelected;
            LaunchToolMenuItem.Enabled = atleastOneToolSelected;
            LaunchToolToolstripButton.Enabled = atleastOneToolSelected;
        }

        #endregion

        #region Event Handlers

        private void CurrentlySelectedExternalToolsOnCollectionUpdated(object sender,
                                                                       CollectionUpdatedEventArgs<ExternalTool>
                                                                           collectionUpdatedEventArgs)
        {
            UpdateEditorControls();
        }

        /// <summary>
        /// Asks what to do with unsaved edits instead of deciding alone.
        /// </summary>
        /// <remarks>
        /// This window has no OK and no Cancel, because it is a docked panel and not a dialog:
        /// New added a row on the spot, every field was committed as the focus left it, and the
        /// lot was written to disk when the tab was closed. There was no way to back out of a
        /// mistake and nothing said when anything had been stored.
        /// </remarks>
        private void ExternalTools_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!HasUnsavedChanges()) return;

            DialogResult answer = MessageBox.Show(FrmMain.Default, Language.ExternalToolUnsavedOnClose,
                                                  Language.ExternalTool, MessageBoxButtons.YesNoCancel,
                                                  MessageBoxIcon.Question);

            switch (answer)
            {
                case DialogResult.Yes:
                    SaveTools();
                    break;
                case DialogResult.No:
                    RestoreSnapshot();
                    break;
                default:
                    e.Cancel = true;
                    break;
            }
        }

        private void ExternalTools_FormClosed(object sender, FormClosedEventArgs e)
        {
            _themeManager.ThemeChanged -= ApplyTheme;
            _currentlySelectedExternalTools.CollectionUpdated -= CurrentlySelectedExternalToolsOnCollectionUpdated;
        }

        private void NewTool_Click(object sender, EventArgs e)
        {
            try
            {
                // Asked for up front rather than dropping a row called "New External Tool" into
                // the list and leaving it there to be found later.
                string name;
                using (FrmInputBox nameBox = new(Language.NewExternalTool, Language.ExternalToolNamePrompt,
                                                 Language.ExternalToolDefaultName))
                {
                    if (nameBox.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(nameBox.returnValue))
                        return;

                    name = nameBox.returnValue.Trim();
                }

                ExternalTool externalTool = new(name);
                Runtime.ExternalToolsService.ExternalTools.Add(externalTool);
                UpdateToolsListObjView();

                // The list keeps the focus, so the new row is drawn as the active row. The name
                // was asked for up front, so there is nothing left to type in the editor below -
                // focusing it there only left the new row marked in a colour barely visible on a
                // dark theme.
                ToolsListObjView.Focus();
                ToolsListObjView.SelectedObject = externalTool;
                ToolsListObjView.EnsureModelVisible(externalTool);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.ExternalTools.NewTool_Click() failed.", ex);
            }
        }

        private void DeleteTool_Click(object sender, EventArgs e)
        {
            try
            {
                string message;
                if (_currentlySelectedExternalTools.Count == 1)
                    message = string.Format(Language.ConfirmDeleteExternalTool,
                                            _currentlySelectedExternalTools[0].DisplayName);
                else if (_currentlySelectedExternalTools.Count > 1)
                    message = string.Format(Language.ConfirmDeleteExternalToolMultiple,
                                            _currentlySelectedExternalTools.Count);
                else
                    return;

                if (!Confirm.Ask(FrmMain.Default, message, Language.ExternalTool))
                    return;

                foreach (ExternalTool externalTool in _currentlySelectedExternalTools)
                {
                    Runtime.ExternalToolsService.ExternalTools.Remove(externalTool);
                }

                ExternalTool firstDeletedNode = _currentlySelectedExternalTools.FirstOrDefault();
                int oldSelectedIndex = ToolsListObjView.IndexOf(firstDeletedNode);
                _currentlySelectedExternalTools.Clear();
                UpdateToolsListObjView();

                int maxIndex = ToolsListObjView.GetItemCount() - 1;
                ToolsListObjView.SelectedIndex = oldSelectedIndex <= maxIndex
                    ? oldSelectedIndex
                    : maxIndex;

                UpdateToolstipControls();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.ExternalTools.DeleteTool_Click() failed.", ex);
            }
        }

        private void LaunchTool_Click(object sender, EventArgs e)
        {
            LaunchTool();
        }

        private void SaveTools_Click(object sender, EventArgs e)
        {
            try
            {
                SaveTools();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.ExternalTools.SaveTools_Click() failed.", ex);
            }
        }

        private void DiscardTools_Click(object sender, EventArgs e)
        {
            try
            {
                if (!HasUnsavedChanges()) return;

                if (!Confirm.Ask(FrmMain.Default, Language.ExternalToolConfirmDiscard, Language.ExternalTool))
                    return;

                RestoreSnapshot();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.ExternalTools.DiscardTools_Click() failed.",
                                                             ex);
            }
        }

        private void ToolsListObjView_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                UpdateToolstipControls();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                                                             "UI.Window.ExternalTools.ToolsListObjView_SelectedIndexChanged() failed.",
                                                             ex);
            }
        }

        private void ToolsListObjView_DoubleClick(object sender, EventArgs e)
        {
            if (ToolsListObjView.SelectedItems.Count > 0)
            {
                LaunchTool();
            }
        }

        private void PropertyControl_ChangedOrLostFocus(object sender, EventArgs e)
        {
            ExternalTool selectedTool = _currentlySelectedExternalTools.FirstOrDefault();
            if (selectedTool == null)
                return;

            try
            {
                selectedTool.DisplayName = DisplayNameTextBox.Text;
                selectedTool.FileName = FilenameTextBox.Text;
                selectedTool.Arguments = ArgumentsCheckBox.Text;
                selectedTool.WorkingDir = WorkingDirTextBox.Text;
                selectedTool.WaitForExit = WaitForExitCheckBox.Checked;
                selectedTool.TryIntegrate = TryToIntegrateCheckBox.Checked;
                selectedTool.ShowOnToolbar = ShowOnToolbarCheckBox.Checked;
                selectedTool.RunElevated = RunElevatedCheckBox.Checked;

                UpdateToolsListObjView();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                                                             "UI.Window.ExternalTools.PropertyControl_ChangedOrLostFocus() failed.",
                                                             ex);
            }
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            try
            {
                using (OpenFileDialog browseDialog = new())
                {
                    browseDialog.Filter = string.Join("|", Language.FilterApplication, "*.exe",
                                                      Language.FilterAll, "*.*");
                    if (browseDialog.ShowDialog() != DialogResult.OK)
                        return;
                    ExternalTool selectedItem = _currentlySelectedExternalTools.FirstOrDefault();
                    if (selectedItem == null)
                        return;
                    selectedItem.FileName = browseDialog.FileName;
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.ExternalTools.BrowseButton_Click() failed.",
                                                             ex);
            }
        }

        private void BrowseWorkingDir_Click(object sender, EventArgs e)
        {
            try
            {
                using (FolderBrowserDialog browseDialog = new())
                {
                    if (browseDialog.ShowDialog() != DialogResult.OK)
                        return;
                    ExternalTool selectedItem = _currentlySelectedExternalTools.FirstOrDefault();
                    if (selectedItem == null)
                        return;
                    selectedItem.WorkingDir = browseDialog.SelectedPath;
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.ExternalTools.BrowseButton_Click() failed.",
                                                             ex);
            }
        }

        private void ToolsListObjView_CellToolTipShowing(object sender, ToolTipShowingEventArgs e)
        {
            if (e.Column != WaitForExitColumnHeader)
                return;

            if (!(e.Model is ExternalTool rowItemAsExternalTool) || !rowItemAsExternalTool.TryIntegrate)
                return;

            e.Text =
                $"'{Language.WaitForExit}' cannot be enabled if '{Language.TryToIntegrate}' is enabled";
        }

        #endregion
    }
}