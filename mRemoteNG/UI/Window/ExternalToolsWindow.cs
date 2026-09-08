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
        }

        /// <summary>
        /// Writes the tools to disk. Called after anything that changes them.
        /// </summary>
        /// <remarks>
        /// Everything here takes effect at once, the way the connection tree does - rename,
        /// duplicate, delete, and it is done. There is no Save button and nothing to save: an
        /// explicit save alongside a New that already added the row, and fields that already
        /// committed as the focus left them, meant two rules at the same time and no way to tell
        /// which one applied to what.
        ///
        /// It also has to be this way round. The external tools toolbar rebuilds itself from this
        /// same collection whenever it changes, and a connection names the tool to run before it
        /// opens - so a tool that existed only in an unsaved copy could not be picked there.
        /// </remarks>
        private void Persist()
        {
            _externalAppsSaver.Save(Runtime.ExternalToolsService.ExternalTools);
        }

        private static ExternalTool Copy(ExternalTool tool) =>
            new(tool.DisplayName, tool.FileName, tool.Arguments, tool.WorkingDir, tool.RunElevated)
            {
                WaitForExit = tool.WaitForExit,
                TryIntegrate = tool.TryIntegrate,
                ShowOnToolbar = tool.ShowOnToolbar
            };

        private void ApplyLanguage()
        {
            Text = Language.ExternalTool;
            TabText = Language.ExternalTool;

            NewToolToolstripButton.Text = Language._New;
            DeleteToolToolstripButton.Text = Language.Delete;
            DuplicateToolToolstripButton.Text = Language.Duplicate;
            DuplicateToolMenuItem.Text = Language.DuplicateExternalTool;
            LaunchToolToolstripButton.Text = Language._Launch;

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

            // One at a time: duplicating a handful at once would put a pile of copies in the list
            // with nothing to say which came from what.
            bool exactlyOneSelected = _currentlySelectedExternalTools.Count == 1;
            DuplicateToolMenuItem.Enabled = exactlyOneSelected;
            DuplicateToolToolstripButton.Enabled = exactlyOneSelected;
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

                Persist();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.ExternalTools.NewTool_Click() failed.", ex);
            }
        }

        private void DuplicateTool_Click(object sender, EventArgs e)
        {
            try
            {
                ExternalTool original = _currentlySelectedExternalTools.FirstOrDefault();
                if (original == null) return;

                ExternalTool copy = Copy(original);
                copy.DisplayName = UnusedName(original.DisplayName + Language.ExternalToolCopySuffix);

                Runtime.ExternalToolsService.ExternalTools.Add(copy);
                UpdateToolsListObjView();

                ToolsListObjView.Focus();
                ToolsListObjView.SelectedObject = copy;
                ToolsListObjView.EnsureModelVisible(copy);

                Persist();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("UI.Window.ExternalTools.DuplicateTool_Click() failed.",
                                                             ex);
            }
        }

        /// <summary>
        /// The given name, or the first numbered variant of it that no tool is using.
        /// </summary>
        /// <remarks>
        /// Tools are looked up by their display name - that is how a connection names the one to
        /// run before it opens - so two with the same name would make which one runs a matter of
        /// which was found first.
        /// </remarks>
        private static string UnusedName(string wanted)
        {
            bool Taken(string name) =>
                Runtime.ExternalToolsService.ExternalTools.Any(
                    tool => string.Equals(tool.DisplayName, name, StringComparison.CurrentCultureIgnoreCase));

            if (!Taken(wanted)) return wanted;

            for (int i = 2; i < 1000; i++)
            {
                string candidate = $"{wanted} ({i})";
                if (!Taken(candidate)) return candidate;
            }

            return wanted;
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
                Persist();
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

                // Once per field, as the focus leaves it, or per checkbox click - not per
                // keystroke: nothing here is wired to TextChanged.
                Persist();
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