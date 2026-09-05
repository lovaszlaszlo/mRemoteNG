using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;
using BrightIdeasSoftware;
using mRemoteNG.App;
using mRemoteNG.Config.Import;
using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Container;
using mRemoteNG.Resources.Language;
using mRemoteNG.Themes;
using mRemoteNG.UI.Controls;
using mRemoteNG.Tree.Root;

namespace mRemoteNG.UI.Forms
{
    /// <summary>
    /// Picks which of the Windows Remote Desktop client's remembered machines to take.
    /// </summary>
    /// <remarks>
    /// Importing the lot unasked is what the other importers do, and on a machine used for years
    /// that means a folder of twenty entries most of which are already in the tree. So: tick what
    /// you want. The ones already present start unticked and say so in their own column, which is
    /// the same question the port scan window answers.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public class RdpRegistryImportWindow : Form
    {
        private readonly ContainerInfo _startedFrom;
        private ObjectListView _list;
        private MrngCheckBox _all;
        private ComboBox _destination;
        private Button _import;
        private MrngCheckBox _onlyNew;

        /// <summary>
        /// Everything read out of the registry, filtered or not.
        /// </summary>
        private IReadOnlyList<RdpRegistryEntry> _entries = [];

        /// <summary>
        /// What is ticked, kept apart from the grid.
        /// </summary>
        /// <remarks>
        /// The grid cannot be the record once rows can be hidden: hiding a row throws away its
        /// tick, so ticking a machine, turning the filter on and turning it off again would lose
        /// the tick with nothing said. Held here and put back whenever the list is rebuilt.
        /// </remarks>
        private readonly HashSet<RdpRegistryEntry> _ticked = [];

        public RdpRegistryImportWindow(ContainerInfo startedFrom)
        {
            _startedFrom = startedFrom;

            BuildLayout();
            ApplyTheme();
            ThemeManager.getInstance().ThemeChanged += ApplyTheme;

            Load += (_, _) => Populate();
        }

        #region Layout

        private void BuildLayout()
        {
            Text = Language.ImportFromRdpRegistry;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(720, 460);
            MinimumSize = new Size(560, 340);

            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label explanation = new()
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(3, 3, 3, 8),
                Text = Language.ImportFromRdpRegistryExplanation
            };

            _list = new ObjectListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                ShowGroups = false,
                UseCompatibleStateImageBehavior = false,
                // Clickable, so the headers behave like headers and the list can be sorted. On
                // twenty-odd machines that is the difference between reading the list and
                // searching it.
                HeaderStyle = ColumnHeaderStyle.Clickable
            };

            // The All box has to follow the ticks as well as drive them, or it goes stale the
            // moment a single row is clicked.
            _list.ItemChecked += List_ItemChecked;

            _list.Columns.AddRange(new ColumnHeader[]
            {
                new OLVColumn(Language.Hostname, "Address") { Width = 200 },

                // Second, beside the name. It was last and filling the free space, which put the
                // ticks half a window away from the rows they explain - and since these are
                // exactly the rows that start unticked, the selection read as arbitrary.
                // (AlreadyInTree reads a set gathered once - see Populate.)
                new OLVColumn(Language.PortScanAlreadyAdded, null)
                {
                    Width = 110,
                    TextAlign = HorizontalAlignment.Center,
                    AspectGetter = row => row is RdpRegistryEntry entry && AlreadyInTree(entry)
                                              ? "✔"
                                              : string.Empty
                },
                new OLVColumn(Language.Port, "Port") { Width = 70, TextAlign = HorizontalAlignment.Right },
                new OLVColumn(Language.Username, "Account") { Width = 190, FillsFreeSpace = true }
            });

            layout.Controls.Add(explanation, 0, 0);
            layout.Controls.Add(_list, 0, 1);
            layout.Controls.Add(BuildChoices(), 0, 2);
            layout.Controls.Add(BuildButtons(), 0, 3);

            Controls.Add(layout);
        }

        private Control BuildChoices()
        {
            TableLayoutPanel choices = new()
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Margin = new Padding(0, 8, 0, 0)
            };
            choices.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            choices.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            choices.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            choices.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _all = new MrngCheckBox
            {
                AutoSize = true,
                Text = Language.SelectAllEntries,
                Margin = new Padding(3, 6, 3, 3)
            };
            _all.CheckedChanged += All_CheckedChanged;

            _onlyNew = new MrngCheckBox
            {
                AutoSize = true,
                Text = Language.PortScanOnlyNew,
                Margin = new Padding(16, 6, 3, 3)
            };
            _onlyNew.CheckedChanged += (_, _) => ShowEntries();

            _destination = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 420,
                Margin = new Padding(3, 3, 3, 3)
            };
            _destination.DropDown += (_, _) => FillDestinations();

            choices.Controls.Add(_all, 1, 0);
            choices.Controls.Add(_onlyNew, 2, 0);

            choices.Controls.Add(new Label
            {
                AutoSize = true,
                Margin = new Padding(3, 7, 8, 3),
                Text = Language.PortScanDestination
            }, 0, 1);
            choices.Controls.Add(_destination, 1, 1);

            return choices;
        }

        private Control BuildButtons()
        {
            FlowLayoutPanel buttons = new()
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0, 8, 0, 0),
                WrapContents = false
            };

            Button cancel = new()
            {
                AutoSize = true,
                DialogResult = DialogResult.Cancel,
                Text = Language._Cancel
            };

            _import = new Button
            {
                AutoSize = true,
                Text = Language._Import
            };
            _import.Click += Import_Click;

            buttons.Controls.Add(cancel);
            buttons.Controls.Add(_import);

            AcceptButton = _import;
            CancelButton = cancel;

            return buttons;
        }

        private void ApplyTheme()
        {
            ThemeManager themeManager = ThemeManager.getInstance();
            themeManager.ApplyThemeToTitleBar(this);

            if (!themeManager.ActiveAndExtended) return;

            BackColor = themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
            ForeColor = themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");

            StyleHeader();
        }

        /// <summary>
        /// Gives the column headers a colour of their own.
        /// </summary>
        /// <remarks>
        /// Left alone, the header takes the same background as the rows, and on a dark theme the
        /// two are indistinguishable - the grid reads as having no header at all. Lightened from
        /// the list's own background rather than fixed, so it holds up in a light theme too.
        /// </remarks>
        private void StyleHeader()
        {
            HeaderFormatStyle header = new();
            header.SetBackColor(ControlPaint.Light(BackColor, 0.35f));
            header.SetForeColor(ForeColor);

            _list.HeaderUsesThemes = false;
            _list.HeaderFormatStyle = header;
        }

        #endregion

        #region Contents

        private void Populate()
        {
            _entries = RdpRegistryReader.Read();

            GatherKnownHosts();

            // Nothing ticked. Pre-ticking the ones missing from the tree looked like an arbitrary
            // scattering of ticks - the evidence for it sat in another column, and a list that
            // arrives with a selection already made invites pressing Import without reading it.
            // The tick column says what is already there; the filter beside it hides them.
            _ticked.Clear();

            ShowEntries();
            FillDestinations();

            if (_entries.Count == 0)
                MessageBox.Show(this, Language.ImportFromRdpRegistryNothingFound, Text,
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Rebuilds the list under the current filter, putting the ticks back.
        /// </summary>
        private void ShowEntries()
        {
            List<RdpRegistryEntry> shown = _entries
                                           .Where(entry => !_onlyNew.Checked || !AlreadyInTree(entry))
                                           .ToList();

            bool wasSyncing = _syncing;
            _syncing = true;

            _list.SetObjects(shown);
            _list.CheckedObjects = shown.Where(_ticked.Contains).ToList();

            _syncing = wasSyncing;
            SyncAllBox();
        }

        /// <summary>
        /// A tick in the grid is a tick in the record - but only when it came from a click.
        /// </summary>
        private void List_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (!_syncing && e.Item is OLVListItem item && item.RowObject is RdpRegistryEntry entry)
            {
                if (item.Checked)
                    _ticked.Add(entry);
                else
                    _ticked.Remove(entry);
            }

            SyncAllBox();
        }

        /// <summary>
        /// Ticks or unticks everything on screen - not what the filter is hiding.
        /// </summary>
        private void All_CheckedChanged(object sender, EventArgs e)
        {
            if (_syncing) return;

            foreach (RdpRegistryEntry entry in _list.Objects.Cast<RdpRegistryEntry>())
            {
                if (_all.Checked)
                    _ticked.Add(entry);
                else
                    _ticked.Remove(entry);
            }

            bool wasSyncing = _syncing;
            _syncing = true;
            _list.CheckedObjects = _all.Checked ? _list.Objects.Cast<object>().ToList() : new List<object>();
            _syncing = wasSyncing;

            SyncAllBox();
        }

        private bool _syncing;

        private void SyncAllBox()
        {
            int total = _list.GetItemCount();
            int ticked = _list.CheckedObjects.Count;

            // Saved and restored rather than simply cleared: this runs once per row while the All
            // box is ticking them, so clearing the flag on the first row would let the second row
            // drive the All box, which drives the rows, and so on.
            bool wasSyncing = _syncing;
            _syncing = true;

            _all.CheckState = ticked == 0
                                  ? CheckState.Unchecked
                                  : ticked == total
                                      ? CheckState.Checked
                                      : CheckState.Indeterminate;

            _syncing = wasSyncing;
        }

        #endregion

        #region Destination

        private void FillDestinations()
        {
            ContainerInfo current = SelectedDestination ?? _startedFrom;

            _destination.BeginUpdate();
            _destination.Items.Clear();

            foreach (ContainerInfo folder in AllFolders(Runtime.ConnectionsService.ConnectionTreeModel.RootNodes))
                _destination.Items.Add(new DestinationEntry(folder));

            foreach (DestinationEntry entry in _destination.Items)
            {
                if (!ReferenceEquals(entry.Folder, current)) continue;

                _destination.SelectedItem = entry;
                break;
            }

            if (_destination.SelectedItem == null && _destination.Items.Count > 0)
                _destination.SelectedIndex = 0;

            _destination.EndUpdate();
        }

        private ContainerInfo SelectedDestination => (_destination.SelectedItem as DestinationEntry)?.Folder;

        private static IEnumerable<ContainerInfo> AllFolders(IEnumerable<ConnectionInfo> nodes)
        {
            foreach (ConnectionInfo node in nodes)
            {
                if (node is not ContainerInfo container) continue;
                if (node is RootPuttySessionsNodeInfo) continue;

                yield return container;

                foreach (ContainerInfo child in AllFolders(container.Children))
                    yield return child;
            }
        }

        private sealed class DestinationEntry(ContainerInfo folder)
        {
            public ContainerInfo Folder { get; } = folder;

            public override string ToString()
            {
                List<string> parts = new();

                for (ConnectionInfo node = Folder; node != null; node = node.Parent)
                    parts.Insert(0, node.Name);

                return string.Join(" / ", parts);
            }
        }

        #endregion

        #region Importing

        private void Import_Click(object sender, EventArgs e)
        {
            RdpRegistryEntry[] chosen = _list.CheckedObjects.Cast<RdpRegistryEntry>().ToArray();
            ContainerInfo destination = SelectedDestination;

            if (chosen.Length == 0 || destination == null)
            {
                MessageBox.Show(this, Language.ImportFromRdpRegistryNothingTicked, Text,
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (Runtime.ConnectionsService.BatchedSavingContext())
            {
                foreach (RdpRegistryEntry entry in chosen)
                    destination.AddChild(new ConnectionInfo
                    {
                        Name = entry.Address,
                        Hostname = entry.Address,
                        Port = entry.Port,
                        Protocol = ProtocolType.RDP,
                        Username = entry.Username,
                        Domain = entry.Domain,
                        // They all came from the Windows Remote Desktop client, and the tree reads
                        // better when that is visible at a glance.
                        Icon = "Windows",
                        Parent = destination
                    });
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// Every hostname already in the tree, gathered once when the list is filled.
        /// </summary>
        /// <remarks>
        /// The tick column asks this for every cell the grid draws, so walking the whole
        /// connection tree each time would show. The window is modal and short-lived, so the set
        /// cannot go stale while it is open.
        /// </remarks>
        private static HashSet<string> _knownHosts;

        private static bool AlreadyInTree(RdpRegistryEntry entry) =>
            _knownHosts != null && _knownHosts.Contains(entry.Address);

        private static void GatherKnownHosts()
        {
            try
            {
                _knownHosts = new HashSet<string>(
                    AllConnections(Runtime.ConnectionsService.ConnectionTreeModel.RootNodes)
                        .Select(existing => existing.Hostname?.Trim())
                        .Where(hostname => !string.IsNullOrEmpty(hostname)),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("GatherKnownHosts() failed.", ex);
                _knownHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static IEnumerable<ConnectionInfo> AllConnections(IEnumerable<ConnectionInfo> nodes)
        {
            foreach (ConnectionInfo node in nodes)
            {
                if (node is ContainerInfo container)
                {
                    foreach (ConnectionInfo child in AllConnections(container.Children))
                        yield return child;

                    continue;
                }

                yield return node;
            }
        }

        #endregion

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ThemeManager.getInstance().ThemeChanged -= ApplyTheme;
            base.OnFormClosed(e);
        }
    }
}
