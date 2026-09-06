using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;
using mRemoteNG.App;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Container;
using mRemoteNG.Messages;
using mRemoteNG.Properties;
using mRemoteNG.UI.Forms;
using mRemoteNG.UI.Panels;
using mRemoteNG.UI.Tabs;
using mRemoteNG.UI.Window;
using WeifenLuo.WinFormsUI.Docking;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;

namespace mRemoteNG.Connection
{
    [SupportedOSPlatform("windows")]
    public class ConnectionInitiator : IConnectionInitiator
    {
        private readonly PanelAdder _panelAdder = new();
        private readonly List<string> _activeConnections = [];

        public IEnumerable<string> ActiveConnections => _activeConnections;

        public bool SwitchToOpenConnection(ConnectionInfo connectionInfo)
        {
            InterfaceControl interfaceControl = FindConnectionContainer(connectionInfo);
            if (interfaceControl == null) return false;

            // A tab whose session has died is not somewhere to jump to. Closing it takes the
            // protocol out of the connection's open list and takes the tab with it, and saying no
            // here lets the caller go on and connect - so double-clicking a connection that failed
            // or dropped tries it again, which is the only thing anybody wants at that point.
            if (interfaceControl.Protocol?.IsSessionAlive == false)
            {
                interfaceControl.Protocol.Close();
                return false;
            }

            if (interfaceControl.FindForm() is not ConnectionTab tab) return true;

            // Activate, not Show(DockPanel): showing a tab in the panel it came from drags a
            // floating one back into the main window, which is not what jumping to a session
            // should do to it.
            tab.Activate();

            if (tab.TopLevelControl is Form window && window != FrmMain.Default)
            {
                window.Activate();
                window.BringToFront();
            }

            // The session itself takes the keyboard, not the tab around it.
            interfaceControl.Protocol?.Focus();
            return true;
        }

        public void OpenConnection(
            ContainerInfo containerInfo,
            ConnectionInfo.Force force = ConnectionInfo.Force.None,
            ConnectionWindow conForm = null)
        {
            if (containerInfo == null || containerInfo.Children.Count == 0)
                return;

            foreach (ConnectionInfo child in containerInfo.Children)
            {
                if (child is ContainerInfo childAsContainer)
                    OpenConnection(childAsContainer, force, conForm);
                else
                    OpenConnection(child, force, conForm);
            }
        }

        // async is necessary so UI can update while OpenConnection waits for tunnel connection to get ready in case of connection through SSH tunnel
        public async void OpenConnection(
            ConnectionInfo connectionInfo,
            ConnectionInfo.Force force = ConnectionInfo.Force.None,
            ConnectionWindow conForm = null)
        {
            if (connectionInfo == null)
                return;

            try
            {
                if (!string.IsNullOrEmpty(connectionInfo.EC2InstanceId))
                {
                    try
                    {
                        string host = await ExternalConnectors.AWS.EC2FetchDataService.GetEC2InstanceDataAsync("AWSAPI:" + connectionInfo.EC2InstanceId, connectionInfo.EC2Region);
                        if (!string.IsNullOrEmpty(host))
                            connectionInfo.Hostname = host;
                    }
                    catch
                    {
                    }
                }

                if (string.IsNullOrEmpty(connectionInfo.Hostname))
                {
                    if (!ProtocolFeature.SupportBlankHostname(connectionInfo.Protocol))
                    {
                        Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, Language.ConnectionOpenFailedNoHostname);
                        return;
                    }

                    if (string.IsNullOrEmpty(connectionInfo.Name))
                    {
                        connectionInfo.Name = "localhost";
                    }
                }

                if (!await RunPreConnectionExternalAppAsync(connectionInfo))
                    return;

                // Opening a connection that is already open goes to its tab instead of starting
                // a second session. That is the sensible default and stays the default, but it is
                // also the one piece of this window's behaviour nothing announces - so it can be
                // turned off, under Options - Connections.
                if (!force.HasFlag(ConnectionInfo.Force.DoNotJump) &&
                    !Settings.Default.AlwaysOpenNewSession)
                {
                    if (SwitchToOpenConnection(connectionInfo))
                        return;
                }

                ProtocolFactory protocolFactory = new();

                // A caller that named the window has already answered the question, so it is not
                // asked: dropping a connection onto a tab group said where it goes, and putting a
                // chooser in front of that is asking somebody to repeat themselves.
                string connectionPanel = conForm != null
                                             ? conForm.TabText
                                             : SetConnectionPanel(connectionInfo, force);

                if (string.IsNullOrEmpty(connectionPanel)) return;
                ConnectionWindow connectionForm = SetConnectionForm(conForm, connectionPanel);
                Control connectionContainer = null;

                // Handle connection through SSH tunnel:
                // in case of connection through SSH tunnel, connectionInfo gets cloned, so that modification of its name, hostname and port do not modify the original connection info
                // connectionInfoOriginal points to the original connection info in either case, for where its needed later on.
                ConnectionInfo connectionInfoOriginal = connectionInfo;
                ConnectionInfo connectionInfoSshTunnel = null; // SSH tunnel connection info will be set if SSH tunnel connection is configured, can be found and connected.

                // The protocol carrying the tunnel, kept so it can be closed with the connection
                // that travels over it rather than outliving it.
                ProtocolBase protocolToClose = null;
                if (!string.IsNullOrEmpty(connectionInfoOriginal.SSHTunnelConnectionName))
                {
                    // Find the connection info specified as SSH tunnel in the connections tree
                    connectionInfoSshTunnel = getSSHConnectionInfoByName(Runtime.ConnectionsService.ConnectionTreeModel.RootNodes, connectionInfoOriginal.SSHTunnelConnectionName);
                    if (connectionInfoSshTunnel == null)
                    {
                        Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, string.Format(Language.SshTunnelConfigProblem, connectionInfoOriginal.Name, connectionInfoOriginal.SSHTunnelConnectionName));
                        return;
                    }
                    Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                        $"SSH Tunnel connection '{connectionInfoOriginal.SSHTunnelConnectionName}' configured for '{connectionInfoOriginal.Name}' found. Finding free local port for use as local tunnel port ...");
                    // determine a free local port to use as local tunnel port
                    System.Net.Sockets.TcpListener l = new(System.Net.IPAddress.Loopback, 0);
                    l.Start();
                    int localSshTunnelPort = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
                    l.Stop();
                    Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                        $"{localSshTunnelPort} will be used as local tunnel port. Establishing SSH connection to '{connectionInfoSshTunnel.Hostname}' with additional tunnel options for target connection ...");

                    // clone SSH tunnel connection as tunnel options will be added to it, and those changes shall not be saved to the configuration
                    connectionInfoSshTunnel = connectionInfoSshTunnel.Clone();
                    connectionInfoSshTunnel.SSHOptions += " -L " + localSshTunnelPort + ":" + connectionInfoOriginal.Hostname + ":" + connectionInfoOriginal.Port;

                    // clone target connection info as its hostname will be changed to localhost and port to local tunnel port to establish connection through tunnel, and those changes shall not be saved to the configuration
                    connectionInfo = connectionInfoOriginal.Clone();
                    connectionInfo.Name += " via " + connectionInfoSshTunnel.Name;
                    connectionInfo.Hostname = "localhost";
                    connectionInfo.Port = localSshTunnelPort;

                    // connect the SSH connection to setup the tunnel
                    ProtocolBase protocolSshTunnel = protocolFactory.CreateProtocol(connectionInfoSshTunnel);
                    if (protocolSshTunnel is not ISshTunnelProvider tunnelProvider)
                    {
                        Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                            string.Format(Language.SshTunnelIsNotPutty, connectionInfoOriginal.Name, connectionInfoSshTunnel.Name));
                        return;
                    }

                    SetConnectionFormEventHandlers(protocolSshTunnel, connectionForm);
                    SetConnectionEventHandlers(protocolSshTunnel);
                    connectionContainer = SetConnectionContainer(connectionInfo, connectionForm);
                    BuildConnectionInterfaceController(connectionInfoSshTunnel, protocolSshTunnel, connectionContainer);
                    protocolSshTunnel.InterfaceControl.OriginalInfo = connectionInfoSshTunnel;

                    if (protocolSshTunnel.Initialize() == false)
                    {
                        protocolSshTunnel.Close();
                        Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                            string.Format(Language.SshTunnelNotInitialized, connectionInfoOriginal.Name, connectionInfoSshTunnel.Name));
                        return;
                    }

                    if (protocolSshTunnel.Connect() == false)
                    {
                        protocolSshTunnel.Close();
                        Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                            string.Format(Language.SshTunnelNotConnected, connectionInfoOriginal.Name, connectionInfoSshTunnel.Name));
                        return;
                    }

                    Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                        "Putty started for SSH connection for tunnel. Waiting for local tunnel port to become available ...");

                    // wait until SSH tunnel connection is ready, by checking if local port can be connected to, but max 60 sec.
                    System.Net.Sockets.Socket testsock = new(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    while (stopwatch.ElapsedMilliseconds < 60000)
                    {
                        // confirm that SSH connection is still active
                        // works only if putty is connfigured to always close window on exit
                        // else, if connection attempt fails, window remains open and putty process remains running, and we cannot know that connection is already doomed
                        // in this case the timeout will expire and the log message below will be created
                        // awkward for user as he has already acknowledged the putty popup some seconds again when the below notification comes....
                        if (!tunnelProvider.IsTunnelRunning)
                        {
                            protocolSshTunnel.Close();
                            Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                                string.Format(Language.SshTunnelFailed, connectionInfoOriginal.Name, connectionInfoSshTunnel.Name));
                            return;
                        }

                        try
                        {
                            testsock.Connect(System.Net.IPAddress.Loopback, localSshTunnelPort);
                            testsock.Close();
                            break;
                        }
                        catch
                        {
                            await System.Threading.Tasks.Task.Delay(1000);
                        }
                    }

                    if (stopwatch.ElapsedMilliseconds >= 60000)
                    {
                        protocolSshTunnel.Close();
                        Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                            string.Format(Language.SshTunnelPortNotReadyInTime, connectionInfoOriginal.Name, connectionInfoSshTunnel.Name));
                        return;
                    }

                    Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                        "Local tunnel port is now available. Hiding putty display and setting up target connection via local tunnel port ...");

                    // hide the display of the SSH tunnel connection which has been shown until this time, such that password can be entered if required or errors be seen
                    // it stays invisible in the container however which will be reused for the actual connection and such that if the container is closed the SSH tunnel connection is closed as well
                    protocolSshTunnel.InterfaceControl.Hide();

                    // Being in the container is not on its own enough to end the tunnel with the
                    // connection it carries. Closing the container disposes the hidden control,
                    // and disposing a control does not close a protocol - it only happened to
                    // finish PuTTY off, whose session dies with the window it was drawn in. A
                    // native SSH session survives its control, so the tunnel stayed logged in to
                    // the far side after the connection through it had gone; three tries left
                    // three sessions on the server, and only mRemoteNG exiting cleared them.
                    protocolToClose = protocolSshTunnel;
                }

                ProtocolBase newProtocol = protocolFactory.CreateProtocol(connectionInfo);
                SetConnectionFormEventHandlers(newProtocol, connectionForm);
                SetConnectionEventHandlers(newProtocol);
                // in case of connection through SSH tunnel the container is already defined and must be use, else it needs to be created here
                if (connectionContainer == null) connectionContainer = SetConnectionContainer(connectionInfo, connectionForm);
                BuildConnectionInterfaceController(connectionInfo, newProtocol, connectionContainer);
                // in case of connection through SSH tunnel the connectionInfo was modified but connectionInfoOriginal in all cases retains the original info
                // and is stored in interface control for further use
                newProtocol.InterfaceControl.OriginalInfo = connectionInfoOriginal;
                // SSH tunnel connection is stored in Interface Control to be used in log messages etc
                newProtocol.InterfaceControl.SSHTunnelInfo = connectionInfoSshTunnel;

                newProtocol.Force = force;

                if (protocolToClose != null)
                {
                    ProtocolBase tunnelToClose = protocolToClose;

                    newProtocol.Closed += _ =>
                    {
                        Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                            $"Closing the SSH tunnel that carried '{connectionInfoOriginal.Name}'");
                        tunnelToClose.Close();
                    };
                }

                if (newProtocol.Initialize() == false)
                {
                    newProtocol.Close();
                    return;
                }

                if (newProtocol.Connect() == false)
                {
                    newProtocol.Close();
                    return;
                }

                connectionInfoOriginal.OpenConnections.Add(newProtocol);
                _activeConnections.Add(connectionInfo.ConstantID);
                FrmMain.Default.SelectedConnection = connectionInfo;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace(Language.ConnectionOpenFailed, ex);
            }
        }

        // recursively traverse the tree to find ConnectionInfo of a specific name
        private ConnectionInfo getSSHConnectionInfoByName(IEnumerable<ConnectionInfo> rootnodes, string SSHTunnelConnectionName)
        {
            ConnectionInfo result = null;
            foreach (ConnectionInfo node in rootnodes)
            {
                if (node is ContainerInfo container)
                {
                    result = getSSHConnectionInfoByName(container.Children, SSHTunnelConnectionName);
                }
                else
                {
                    // SSHNative belongs in this test as much as the other two. Left out, the
                    // connection named as the tunnel is reported as "not found in the tree" even
                    // while it sits there in plain sight, and the whole attempt gives up quietly -
                    // the warning goes to the notifications, so what the user sees is a connection
                    // that does nothing at all.
                    if (node.Name == SSHTunnelConnectionName &&
                        node.Protocol is ProtocolType.SSH1 or ProtocolType.SSH2 or ProtocolType.SSHNative)
                        result = node;
                }
                if (result != null) break;
            }
            return result;
        }

        #region Private
        /// <summary>
        /// Runs whatever has to happen before this connection, and says whether to carry on.
        /// </summary>
        /// <remarks>
        /// Two things were wrong with the old one line. It waited on the UI thread, so a tool that
        /// takes a few seconds - bringing up a VPN, say - froze the whole window while it ran, with
        /// nothing on screen to say why. And it ignored what the tool reported: a script that came
        /// back saying the tunnel had not come up was followed by the connection attempt anyway,
        /// which then failed with an error about the host, not about the VPN.
        ///
        /// Waiting happens off the UI thread now, under a wait cursor, and a non-zero exit stops
        /// the connection and says so - naming the tool, so it is clear which step refused.
        /// </remarks>
        private static async Task<bool> RunPreConnectionExternalAppAsync(ConnectionInfo connectionInfo)
        {
            if (connectionInfo.PreExtApp == "") return true;

            Tools.ExternalTool tool = Runtime.ExternalToolsService.GetExtAppByName(connectionInfo.PreExtApp);
            if (tool == null) return true;

            // Not asked to wait: start it and move on, exactly as before.
            if (!tool.WaitForExit)
            {
                tool.Start(connectionInfo);
                return true;
            }

            Form main = FrmMain.Default;
            if (main != null && !main.IsDisposed) main.UseWaitCursor = true;

            try
            {
                int exitCode = await Task.Run(() => tool.StartAndWait(connectionInfo));

                if (exitCode == 0) return true;

                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                    $"'{tool.DisplayName}' ended with {exitCode}, so '{connectionInfo.Name}' was not opened.");

                MessageBox.Show(main,
                                string.Format(Language.PreConnectionToolFailed, tool.DisplayName, exitCode),
                                connectionInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return false;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                    $"The tool to run before '{connectionInfo.Name}' could not be started", ex);
                return false;
            }
            finally
            {
                if (main != null && !main.IsDisposed) main.UseWaitCursor = false;
            }
        }

        private static InterfaceControl FindConnectionContainer(ConnectionInfo connectionInfo)
        {
            if (connectionInfo.OpenConnections.Count <= 0) return null;
            for (int i = 0; i <= Runtime.WindowList.Count - 1; i++)
            {
                // the new structure is ConnectionWindow -> DockPanel -> ActiveDocument -> InterfaceControl
                if (!(Runtime.WindowList[i] is ConnectionWindow connectionWindow)) continue;

                // Found among the controls, not assumed to be the first of them. Controls[0] is
                // whatever is topmost in the z-order, so anything drawn over the dock panel - a
                // label across an empty group, say - made this skip the whole window, and with it
                // every session open in it: the connection looked closed and was opened a second
                // time somewhere else.
                DockPanel cwDp = connectionWindow.Controls.Cast<Control>().OfType<DockPanel>().FirstOrDefault();
                if (cwDp == null) continue;
                // Contents, not Documents: Documents holds only what is docked as a document,
                // so a tab dragged out into a window of its own was not in it. The session was
                // running and on screen, and this reported the connection as closed - a double
                // click on the tree opened another one, and another, once per click.
                foreach (IDockContent dockContent in cwDp.Contents)
                {
                    if (dockContent is not ConnectionTab tab) continue;

                    InterfaceControl ic = InterfaceControl.FindInterfaceControl(tab);
                    if (ic == null) continue;
                    if (ic.Info == connectionInfo || ic.OriginalInfo == connectionInfo)
                        return ic;
                }
            }

            return null;
        }

        /// <summary>
        /// Which tab group this connection opens in, asking only when there is a real choice.
        /// </summary>
        /// <remarks>
        /// The setting used to be all or nothing: never ask, or ask on every single connection -
        /// including when there was one group to choose from and the connection already named it.
        /// A question with one possible answer is not a choice, it is an extra click.
        ///
        /// So: the connection's own group wins when it names one. Otherwise the dialog appears
        /// only if there is more than one group to pick between; with a single group there is
        /// nothing to decide and it opens there.
        /// </remarks>
        private static string SetConnectionPanel(ConnectionInfo connectionInfo, ConnectionInfo.Force force)
        {
            bool asked = force.HasFlag(ConnectionInfo.Force.OverridePanel) ||
                         Properties.OptionsTabsPanelsPage.Default.AlwaysShowPanelSelectionDlg;

            if (connectionInfo.Panel != "" && !asked)
                return connectionInfo.Panel;

            // Force.OverridePanel is somebody asking for the dialog on purpose - from the context
            // menu's "Connect (with options)" - so that one still gets it either way.
            if (!force.HasFlag(ConnectionInfo.Force.OverridePanel) && CountPanels() < 2)
                return connectionInfo.Panel != "" ? connectionInfo.Panel : ConnectionInfo.DefaultPanel;

            FrmChoosePanel frmPnl = new();
            return frmPnl.ShowDialog() == DialogResult.OK
                ? frmPnl.Panel
                : null;
        }

        private static int CountPanels() =>
            Runtime.WindowList?.OfType<ConnectionWindow>().Count() ?? 0;

        private ConnectionWindow SetConnectionForm(ConnectionWindow conForm, string connectionPanel)
        {
            ConnectionWindow connectionForm = conForm ?? Runtime.WindowList.FromString(connectionPanel) as ConnectionWindow;

            if (connectionForm == null)
                // Don't show the panel immediately - it will be shown when first tab is added
                connectionForm = _panelAdder.AddPanel(connectionPanel, showImmediately: false);
            else
                connectionForm.Show(FrmMain.Default.pnlDock);

            connectionForm.Focus();
            return connectionForm;
        }

        private static Control SetConnectionContainer(ConnectionInfo connectionInfo, ConnectionWindow connectionForm)
        {
            Control connectionContainer = connectionForm.AddConnectionTab(connectionInfo);

            if (connectionInfo.Protocol != ProtocolType.IntApp) return connectionContainer;

            Tools.ExternalTool extT = Runtime.ExternalToolsService.GetExtAppByName(connectionInfo.ExtApp);

            if (extT == null) return connectionContainer;

            if (extT.Icon != null)
                ((ConnectionTab)connectionContainer).Icon = extT.Icon;

            return connectionContainer;
        }

        private static void SetConnectionFormEventHandlers(ProtocolBase newProtocol, Form connectionForm)
        {
            newProtocol.Closed += ((ConnectionWindow)connectionForm).Prot_Event_Closed;
        }

        private void SetConnectionEventHandlers(ProtocolBase newProtocol)
        {
            newProtocol.Disconnected += Prot_Event_Disconnected;
            newProtocol.Connected += Prot_Event_Connected;
            newProtocol.Closed += Prot_Event_Closed;
            newProtocol.ErrorOccured += Prot_Event_ErrorOccured;
        }

        private static void BuildConnectionInterfaceController(ConnectionInfo connectionInfo,
                                                               ProtocolBase newProtocol,
                                                               Control connectionContainer)
        {
            newProtocol.InterfaceControl = new InterfaceControl(connectionContainer, newProtocol, connectionInfo);
        }

        #endregion

        #region Event handlers

        private static void Prot_Event_Disconnected(object sender, string disconnectedMessage, int? reasonCode)
        {
            try
            {
                ProtocolBase prot = (ProtocolBase)sender;
                MessageClass msgClass = MessageClass.InformationMsg;

                if (prot.InterfaceControl.Info.Protocol == ProtocolType.RDP)
                {
                    if (reasonCode > 3)
                    {
                        msgClass = MessageClass.WarningMsg;
                    }
                }

                string strHostname = prot.InterfaceControl.OriginalInfo.Hostname;
                if (prot.InterfaceControl.SSHTunnelInfo != null)
                {
                    strHostname += " via SSH Tunnel " + prot.InterfaceControl.SSHTunnelInfo.Name;
                }
                Runtime.MessageCollector.AddMessage(msgClass,
                                                    string.Format(
                                                                  Language.ProtocolEventDisconnected,
                                                                  disconnectedMessage,
                                                                  strHostname,
                                                                  prot.InterfaceControl.Info.Protocol.ToString()));
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace(Language.ProtocolEventDisconnectFailed, ex);
            }
        }

        private void Prot_Event_Closed(object sender)
        {
            try
            {
                ProtocolBase prot = (ProtocolBase)sender;
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, Language.ConnenctionCloseEvent, true);
                string connDetail;
                if (prot.InterfaceControl.OriginalInfo.Hostname == "" && prot.InterfaceControl.Info.Protocol == ProtocolType.IntApp)
                    connDetail = prot.InterfaceControl.Info.ExtApp;
                else if (prot.InterfaceControl.OriginalInfo.Hostname != "")
                    connDetail = prot.InterfaceControl.OriginalInfo.Hostname;
                else
                    connDetail = "UNKNOWN";

                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, string.Format(Language.ConnenctionClosedByUser, connDetail, prot.InterfaceControl.Info.Protocol, Environment.UserName));
                prot.InterfaceControl.OriginalInfo.OpenConnections.Remove(prot);
                if (_activeConnections.Contains(prot.InterfaceControl.Info.ConstantID))
                    _activeConnections.Remove(prot.InterfaceControl.Info.ConstantID);

                if (prot.InterfaceControl.Info.PostExtApp == "") return;
                Tools.ExternalTool extA = Runtime.ExternalToolsService.GetExtAppByName(prot.InterfaceControl.Info.PostExtApp);
                extA?.Start(prot.InterfaceControl.OriginalInfo);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace(Language.ConnenctionCloseEventFailed, ex);
            }
        }

        private static void Prot_Event_Connected(object sender)
        {
            ProtocolBase prot = (ProtocolBase)sender;
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, Language.ConnectionEventConnected,
                                                true);
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                                                string.Format(Language.ConnectionEventConnectedDetail,
                                                              prot.InterfaceControl.OriginalInfo.Hostname,
                                                              prot.InterfaceControl.Info.Protocol, Environment.UserName,
                                                              prot.InterfaceControl.Info.Description,
                                                              prot.InterfaceControl.Info.UserField));
        }

        private static void Prot_Event_ErrorOccured(object sender, string errorMessage, int? errorCode)
        {
            try
            {
                ProtocolBase prot = (ProtocolBase)sender;

                string msg = string.Format(
                                        Language.ConnectionEventErrorOccured,
                                        errorMessage,
                                        prot.InterfaceControl.OriginalInfo.Hostname,
                                        errorCode?.ToString() ?? "-");
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, msg);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace(Language.ConnectionFailed, ex);
            }
        }

        #endregion
    }
}
