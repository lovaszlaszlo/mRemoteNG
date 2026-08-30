using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using mRemoteNG.App;
using mRemoteNG.App.Info;
using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Container;
using mRemoteNG.Messages;
using mRemoteNG.Tools;
using mRemoteNG.Tree.Root;
using WeifenLuo.WinFormsUI.Docking;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;

namespace mRemoteNG.UI.Window
{
    [SupportedOSPlatform("windows")]
    public partial class PortScanWindow
    {
        #region Constructors

        public PortScanWindow()
        {
            InitializeComponent();
            Icon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.SearchAndApps_16x);
            WindowType = WindowType.PortScan;
            DockPnl = new DockContent();
            ApplyTheme();
            DisplayProperties display = new();
            btnScan.Image = display.ScaleImage(btnScan.Image);
        }

        #endregion

        private new void ApplyTheme()
        {
            base.ApplyTheme();
        }

        #region Private Properties

        private bool IpsValid
        {
            get
            {
                if (string.IsNullOrEmpty(ipStart.Octet1.Text))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(ipStart.Octet2.Text))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(ipStart.Octet3.Text))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(ipStart.Octet4.Text))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(ipEnd.Octet1.Text))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(ipEnd.Octet2.Text))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(ipEnd.Octet3.Text))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(ipEnd.Octet4.Text))
                {
                    return false;
                }

                return true;
            }
        }

        #endregion

        #region Private Fields

        private PortScanner _portScanner;
        private bool _scanning;

        /// <summary>
        /// Every host the scan has reported, filtered or not.
        /// </summary>
        /// <remarks>
        /// Kept because the grid cannot be the record: it shows a subset, and the subset changes
        /// when the checkbox does. Filtering was tried through ObjectListView's own ModelFilter
        /// and did not survive a scan in progress - AddObject appends whatever it is given, so
        /// every dead address arrived on screen anyway and only a toggle of the box rebuilt the
        /// list correctly. Deciding at the point of insertion is predictable; live filtering here
        /// was not.
        /// </remarks>
        private readonly List<ScanHost> _scanned = new();

        #endregion

        #region Private Methods

        #region Event Handlers

        private void PortScan_Load(object sender, EventArgs e)
        {
            ApplyLanguage();

            try
            {
                olvHosts.Columns.AddRange(new ColumnHeader[]
                {
                    clmHost, clmSSH, clmTelnet, clmHTTP, clmHTTPS, clmRlogin, clmRDP, clmVNC, clmOpenPorts,
                    clmClosedPorts
                });

                // Which of these are already in the tree. Scanning a subnet you have been using
                // for years turns up mostly things you added long ago, and without this the only
                // way to tell is to remember - so the useful few are lost among them, and
                // importing the lot quietly creates duplicates.
                BrightIdeasSoftware.OLVColumn alreadyAdded = new(Language.PortScanAlreadyAdded, null)
                {
                    Width = 110,
                    TextAlign = HorizontalAlignment.Center,
                    AspectGetter = row => row is ScanHost scanned && IsAlreadyInTree(scanned)
                                              ? "✔"
                                              : string.Empty
                };

                // Second, right after the host itself: that is where the eye goes when reading a
                // row, and it is what decides whether the rest of the row is worth reading at all.
                olvHosts.Columns.Insert(1, alreadyAdded);

                chkOnlyResponding.Text = Language.PortScanOnlyResponding;
                chkOnlyNew.Text = Language.PortScanOnlyNew;

                lblDestination.Text = Language.PortScanDestination;
                cbDestination.DropDown += (_, _) => FillDestinations();
                FillDestinations();
                ApplyRespondingFilter();
                ShowImportControls(true);
                cbProtocol.SelectedIndex = 0;
                numericSelectorTimeout.Value = 5;

                // The first port has no value in the designer, so it came up as zero - and
                // PortScanner reads a zero start as "only one port was given" and scans the end
                // port alone. Ticking the two port boxes therefore scanned exactly port 65535 on
                // every host, found nothing anywhere, and looked for all the world like a feature
                // that does nothing. One is the lowest port there is, and it stops that reading
                // from ever arising.
                portStart.Minimum = 1;
                portStart.Value = 1;

                portStart.ValueChanged += (_, _) => UpdateScanScope();
                portEnd.ValueChanged += (_, _) => UpdateScanScope();
                UpdateScanScope();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(Language.PortScanCouldNotLoadPanel, ex);
            }
        }

        /// <summary>
        /// Writes out what the Scan button is actually going to do.
        /// </summary>
        /// <remarks>
        /// None of this could be worked out from the window. The two port boxes read "first port"
        /// and "last port", but what they really do is switch off the scan of the well known
        /// ports - and either one on its own is enough to do it, leaving the other field disabled
        /// at whatever it happens to hold. Ticking one and setting it to 22 quietly asked for
        /// every port from 22 to 65535 on every host in the range.
        ///
        /// The user's reason for wanting this said plainly is the right one: they will hand this
        /// build to a colleague who has not read the source, and in two days they will not
        /// remember it either.
        /// </remarks>
        private void UpdateScanScope()
        {
            if (!ngCheckFirstPort.Checked && !ngCheckLastPort.Checked)
            {
                lblScanScope.Text = Language.PortScanScopeDefaultPorts;
                return;
            }

            lblScanScope.Text = string.Format(Language.PortScanScopeRange,
                                              (int)portStart.Value, (int)portEnd.Value,
                                              (int)portEnd.Value - (int)portStart.Value + 1);
        }

        /// <summary>
        /// Whether a scanned host already has a connection somewhere in the tree.
        /// </summary>
        /// <remarks>
        /// Matched on the address or the name, whichever the connection was created with, and
        /// case is ignored - a host is the same host however it was typed. This is a hint for the
        /// eye, not a guarantee: a connection reaching the same machine by another name will not
        /// be recognised.
        /// </remarks>
        private static bool IsAlreadyInTree(ScanHost host)
        {
            try
            {
                // Gathered once and kept: this is asked for every host in the scan, once per
                // filter change and again for every cell the grid draws, and walking the whole
                // connection tree each time would show.
                _knownHosts ??= new HashSet<string>(
                    AllConnections(Runtime.ConnectionsService.ConnectionTreeModel.RootNodes)
                        .Select(existing => existing.Hostname?.Trim())
                        .Where(name => !string.IsNullOrEmpty(name)),
                    StringComparer.OrdinalIgnoreCase);

                return Known(host.HostIp) || Known(host.HostName);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                    "Could not check the scanned hosts against the connection tree", ex,
                    MessageClass.WarningMsg, false);
                return false;
            }
        }

        private static HashSet<string> _knownHosts;

        private static bool Known(string candidate) =>
            !string.IsNullOrEmpty(candidate) && _knownHosts.Contains(candidate.Trim());

        private static IEnumerable<ConnectionInfo> AllConnections(IEnumerable<ConnectionInfo> nodes)
        {
            foreach (ConnectionInfo node in nodes)
            {
                if (node is ContainerInfo container)
                {
                    foreach (ConnectionInfo child in AllConnections(container.Children))
                        yield return child;
                }
                else
                {
                    yield return node;
                }
            }
        }

        /// <summary>
        /// Hides the hosts that answered on nothing at all.
        /// </summary>
        /// <remarks>
        /// Scanning a /24 produces two hundred and fifty rows, and on a normal network all but a
        /// dozen of them are addresses with nothing behind them. They are not a result; they are
        /// the absence of one, and they bury the hosts that did answer. Left on by default for
        /// that reason, and switchable for the times when the question is which addresses are
        /// free.
        /// </remarks>
        private void ChkOnlyResponding_CheckedChanged(object sender, EventArgs e)
        {
            ApplyRespondingFilter();
        }

        /// <summary>
        /// Whether a scanned host belongs on screen under the current setting.
        /// </summary>
        private bool Shown(ScanHost host)
        {
            if (chkOnlyResponding.Checked && host.OpenPorts.Count == 0) return false;

            // The second question people actually ask of a scan: not what is out there, but what
            // is out there that I have not got yet. Off by default - hiding what you already have
            // is a choice, and the column says so anyway.
            if (chkOnlyNew.Checked && IsAlreadyInTree(host)) return false;

            return true;
        }

        private void ApplyRespondingFilter()
        {
            // Dropped so it is rebuilt: connections may have been imported since the last look,
            // by this very window.
            _knownHosts = null;

                        olvHosts.SetObjects(_scanned.Where(Shown).ToList());

        }

        private void portStart_Enter(object sender, EventArgs e)
        {
            portStart.Select(0, portStart.Text.Length);
        }

        private void portEnd_Enter(object sender, EventArgs e)
        {
            portEnd.Select(0, portEnd.Text.Length);
        }

        private void btnScan_Click(object sender, EventArgs e)
        {
            if (_scanning)
            {
                StopScan();
            }
            else
            {
                if (IpsValid)
                {
                    StartScan();
                }
                else
                {
                    // Said here, not only in the notifications. Pressing Scan with an octet left
                    // empty did nothing whatsoever: the refusal went to a panel the user is not
                    // looking at, and the window sat there as though the button were dead. Both
                    // addresses have to be complete - all four octets of each - and that is worth
                    // being told at the moment it is asked for.
                    Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, Language.CannotStartPortScan);

                    MessageBox.Show(this, Language.CannotStartPortScan, GeneralAppInfo.ProductName,
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            ProtocolType protocol =
                (ProtocolType)Enum.Parse(typeof(ProtocolType), Convert.ToString(cbProtocol.SelectedItem), true);
            importSelectedHosts(protocol);
        }

        #endregion

        private void ApplyLanguage()
        {
            lblStartIP.Text = Language.FirstIp;
            lblEndIP.Text = Language.LastIp;
            btnScan.Text = Language._Scan;
            btnImport.Text = Language._Import;
            lblOnlyImport.Text = Language.ProtocolToImport;
            clmHost.Text = Language.HostnameIp;
            clmOpenPorts.Text = Language.OpenPorts;
            clmClosedPorts.Text = Language.ClosedPorts;
            ngCheckFirstPort.Text = Language.FirstPort;
            ngCheckLastPort.Text = Language.LastPort;
            lblTimeout.Text = Language.TimeoutInSeconds;
            TabText = Language.PortScan;
            Text = Language.PortScan;
        }

        private void ShowImportControls(bool controlsVisible)
        {
            // The list used to be resized by hand here, which never did anything: it is docked to
            // fill its cell, so the layout overrode the height on the next pass. The import row
            // sizes itself to its contents now, so hiding the panel collapses the row and the list
            // takes the space back on its own.
            pnlImport.Visible = controlsVisible;
        }

        private void StartScan()
        {
            try
            {
                _scanning = true;
                SwitchButtonText();
                _knownHosts = null;
                _scanned.Clear();
                olvHosts.Items.Clear();

                prgBar.Maximum = 100;
                prgBar.Value = 0;
                prgBar.Caption = string.Empty;

                IPAddress ipAddressStart = IPAddress.Parse(ipStart.Text);
                IPAddress ipAddressEnd = IPAddress.Parse(ipEnd.Text);

                if (!ngCheckFirstPort.Checked && !ngCheckLastPort.Checked)
                    _portScanner = new PortScanner(ipAddressStart, ipAddressEnd, (int)portStart.Value,
                                                   (int)portEnd.Value, (int)numericSelectorTimeout.Value * 1000, true);
                else
                    _portScanner = new PortScanner(ipAddressStart, ipAddressEnd, (int)portStart.Value,
                                                   (int)portEnd.Value, (int)numericSelectorTimeout.Value * 1000);

                _portScanner.BeginHostScan += PortScanner_BeginHostScan;
                _portScanner.HostScanned += PortScanner_HostScanned;
                _portScanner.ScanComplete += PortScanner_ScanComplete;

                _portScanner.StartScan();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("StartScan failed (UI.Window.PortScan)", ex);
            }
        }

        private void StopScan()
        {
            _portScanner.BeginHostScan -= PortScanner_BeginHostScan;
            _portScanner.HostScanned -= PortScanner_HostScanned;
            _portScanner.ScanComplete -= PortScanner_ScanComplete;

            _portScanner?.StopScan();
            _scanning = false;
            SwitchButtonText();
        }

        private void SwitchButtonText()
        {
            // The bar is not reset here any more. This is called when the scan ends as well, and
            // emptying the bar was the only thing that marked the end - a blue strip that silently
            // disappeared, leaving nothing to say whether the scan had finished, been stopped or
            // fallen over. Starting a scan clears it; finishing one fills it and says so.
            btnScan.Text = _scanning ? Language._Stop : Language._Scan;
        }

        private static void PortScanner_BeginHostScan(string host)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, "Scanning " + host, true);
        }

        private delegate void PortScannerHostScannedDelegate(ScanHost host, int scannedCount, int totalCount);

        private void PortScanner_HostScanned(ScanHost host, int scannedCount, int totalCount)
        {
            if (InvokeRequired)
            {
                Invoke(new PortScannerHostScannedDelegate(PortScanner_HostScanned),
                       new object[] {host, scannedCount, totalCount});
                return;
            }

            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, "Host scanned " + host.HostIp, true);

            _scanned.Add(host);
            if (Shown(host)) olvHosts.AddObject(host);

            prgBar.Maximum = totalCount;
            prgBar.Value = scannedCount;
            prgBar.Caption = string.Format(Language.PortScanProgress, scannedCount, totalCount,
                                           totalCount > 0 ? scannedCount * 100 / totalCount : 0);
        }

        private delegate void PortScannerScanComplete(List<ScanHost> hosts);

        private void PortScanner_ScanComplete(List<ScanHost> hosts)
        {
            if (InvokeRequired)
            {
                Invoke(new PortScannerScanComplete(PortScanner_ScanComplete), new object[] {hosts});
                return;
            }

            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, Language.PortScanComplete);

            // Left full and labelled. The notification panel was told it was complete, which is no
            // use to somebody watching the window.
            prgBar.Value = prgBar.Maximum;
            prgBar.Caption = string.Format(Language.PortScanFinished, hosts.Count,
                                           hosts.Count(scanned => scanned.OpenPorts.Count > 0));

            _scanning = false;
            SwitchButtonText();
        }

        #endregion

        private void importSelectedHosts(ProtocolType protocol)
        {
            List<ScanHost> hosts = new();
            foreach (ScanHost host in olvHosts.SelectedObjects)
            {
                hosts.Add(host);
            }

            if (hosts.Count < 1)
            {
                // Said out loud rather than into the notifications, where the previous version of
                // this left it - in English, untranslated, on a panel the user is not looking at.
                MessageBox.Show(this, Language.PortScanNothingSelected, GeneralAppInfo.ProductName,
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ContainerInfo destinationContainer = SelectedDestination ?? GetDestinationContainerForImportedHosts();

            // Where they will land is decided by what happens to be selected in the connection
            // tree - a folder takes them directly, a connection passes them to the folder holding
            // it - and that selection is read now, not when this window was opened. Nothing on
            // this window shows any of it, so it is put in the question instead: the folder by
            // name, and how many connections are about to appear in it.
            DialogResult answer = MessageBox.Show(
                this,
                string.Format(Language.PortScanConfirmImport, hosts.Count, protocol, destinationContainer.Name),
                GeneralAppInfo.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
                return;

            Import.ImportFromPortScan(hosts, protocol, destinationContainer);
        }

        /// <summary>
        /// Determines where the imported hosts will be placed
        /// in the connection tree.
        /// </summary>
        /// <summary>
        /// Offers every folder as a destination, starting on the one the import was launched from.
        /// </summary>
        /// <remarks>
        /// It used to be read from the connection tree at the moment Import was pressed, and shown
        /// nowhere. So the connections landed wherever the tree selection happened to be by then -
        /// a folder if one was selected, the folder holding it if a connection was, the root if a
        /// PuTTY node - and the only way to learn any of that was to read the source. Selecting in
        /// the tree while this window was open moved the destination silently.
        ///
        /// Rebuilt whenever the list is opened, because folders can be added while this window is
        /// up - by this window, among other things.
        /// </remarks>
        private void FillDestinations()
        {
            ContainerInfo current = SelectedDestination ?? GetDestinationContainerForImportedHosts();

            cbDestination.BeginUpdate();
            cbDestination.Items.Clear();

            foreach (ContainerInfo folder in AllFolders(Runtime.ConnectionsService.ConnectionTreeModel.RootNodes))
                cbDestination.Items.Add(new DestinationEntry(folder));

            foreach (DestinationEntry entry in cbDestination.Items)
            {
                if (!ReferenceEquals(entry.Folder, current)) continue;

                cbDestination.SelectedItem = entry;
                break;
            }

            if (cbDestination.SelectedItem == null && cbDestination.Items.Count > 0)
                cbDestination.SelectedIndex = 0;

            cbDestination.EndUpdate();
        }

        private ContainerInfo SelectedDestination =>
            (cbDestination.SelectedItem as DestinationEntry)?.Folder;

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

        /// <summary>
        /// A folder in the destination list, shown by its path so two folders of the same name are
        /// telling apart.
        /// </summary>
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

        private ContainerInfo GetDestinationContainerForImportedHosts()
        {
            ConnectionInfo selectedNode = AppWindows.TreeForm.SelectedNode ?? AppWindows.TreeForm.ConnectionTree.ConnectionTreeModel.RootNodes.OfType<RootNodeInfo>().First();

            // if a putty node is selected, place imported connections in the root connection node
            if (selectedNode is RootPuttySessionsNodeInfo || selectedNode is PuttySessionInfo)
                selectedNode = AppWindows.TreeForm.ConnectionTree.ConnectionTreeModel.RootNodes.OfType<RootNodeInfo>()
                                      .First();

            // if the selected node is a connection, use its parent container
            ContainerInfo selectedTreeNodeAsContainer = selectedNode as ContainerInfo ?? selectedNode.Parent;

            return selectedTreeNodeAsContainer;
        }

        private void importVNCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            importSelectedHosts(ProtocolType.VNC);
        }

        private void importTelnetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            importSelectedHosts(ProtocolType.Telnet);
        }

        private void importSSH2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            importSelectedHosts(ProtocolType.SSH2);
        }

        private void importRloginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            importSelectedHosts(ProtocolType.Rlogin);
        }

        private void importRDPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            importSelectedHosts(ProtocolType.RDP);
        }

        private void importHTTPSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            importSelectedHosts(ProtocolType.HTTPS);
        }

        private void importHTTPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            importSelectedHosts(ProtocolType.HTTP);
        }

        private void NgCheckFirstPort_CheckedChanged(object sender, EventArgs e)
        {
            portStart.Enabled = ngCheckFirstPort.Checked;
            UpdateScanScope();
        }

        private void NgCheckLastPort_CheckedChanged(object sender, EventArgs e)
        {
            portEnd.Enabled = ngCheckLastPort.Checked;
            UpdateScanScope();

            portEnd.Value = portEnd.Enabled ? 65535 : 0;
        }
    }
}