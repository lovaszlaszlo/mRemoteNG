
using mRemoteNG.Themes;
using mRemoteNG.UI.Controls;

namespace mRemoteNG.UI.Window
{
	public partial class PortScanWindow : BaseWindow
	{
        #region  Windows Form Designer generated code
				
		internal Controls.MrngLabel lblEndIP;
		internal Controls.MrngLabel lblStartIP;
		internal MrngIpTextBox ipEnd;
		internal Controls.MrngListView olvHosts;
		internal BrightIdeasSoftware.OLVColumn clmHost;
		internal BrightIdeasSoftware.OLVColumn clmIp;
		internal BrightIdeasSoftware.OLVColumn clmSSH;
		internal BrightIdeasSoftware.OLVColumn clmTelnet;
		internal BrightIdeasSoftware.OLVColumn clmHTTP;
		internal BrightIdeasSoftware.OLVColumn clmHTTPS;
		internal BrightIdeasSoftware.OLVColumn clmRlogin;
		internal BrightIdeasSoftware.OLVColumn clmRDP;
		internal BrightIdeasSoftware.OLVColumn clmVNC;
		internal BrightIdeasSoftware.OLVColumn clmOpenPorts;
		internal BrightIdeasSoftware.OLVColumn clmClosedPorts;
		internal Controls.MrngProgressBar prgBar;
		internal Controls.MrngLabel lblOnlyImport;
		internal MrngComboBox cbProtocol;
		internal Controls.MrngNumericUpDown portEnd;
		internal Controls.MrngNumericUpDown portStart;
		internal MrngButton btnImport;
		internal MrngIpTextBox ipStart;
				
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PortScanWindow));
            this.ipStart = new mRemoteNG.UI.Controls.MrngIpTextBox();
            this.ipEnd = new mRemoteNG.UI.Controls.MrngIpTextBox();
            this.lblStartIP = new mRemoteNG.UI.Controls.MrngLabel();
            this.lblEndIP = new mRemoteNG.UI.Controls.MrngLabel();
            this.olvHosts = new mRemoteNG.UI.Controls.MrngListView();
            this.resultsMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.importHTTPToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importHTTPSToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importRDPToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importRloginToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importSSH2ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importTelnetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.importVNCToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.btnImport = new MrngButton();
            this.cbProtocol = new MrngComboBox();
            this.lblOnlyImport = new mRemoteNG.UI.Controls.MrngLabel();
            this.clmHost = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.clmIp = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.clmSSH = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.clmTelnet = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.clmHTTP = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.clmHTTPS = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.clmRlogin = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.clmRDP = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.clmVNC = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.clmOpenPorts = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.clmClosedPorts = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.prgBar = new mRemoteNG.UI.Controls.MrngProgressBar();
            this.numericSelectorTimeout = new mRemoteNG.UI.Controls.MrngNumericUpDown();
            this.lblTimeout = new System.Windows.Forms.Label();
            this.portEnd = new mRemoteNG.UI.Controls.MrngNumericUpDown();
            this.portStart = new mRemoteNG.UI.Controls.MrngNumericUpDown();
            this.pnlIp = new System.Windows.Forms.TableLayoutPanel();
            this.btnScan = new MrngButton();
            this.ngCheckFirstPort = new MrngCheckBox();
            this.ngCheckLastPort = new MrngCheckBox();
            this.lblScanScope = new mRemoteNG.UI.Controls.MrngLabel();
            this.chkOnlyResponding = new MrngCheckBox();
            this.chkOnlyNew = new MrngCheckBox();
            this.pnlFilter = new System.Windows.Forms.TableLayoutPanel();
            this.lblFilter = new mRemoteNG.UI.Controls.MrngLabel();
            this.lblProtocolFilter = new mRemoteNG.UI.Controls.MrngLabel();
            this.chkProtoAll = new MrngCheckBox();
            this.chkProtoSsh = new MrngCheckBox();
            this.chkProtoTelnet = new MrngCheckBox();
            this.chkProtoHttp = new MrngCheckBox();
            this.chkProtoHttps = new MrngCheckBox();
            this.chkProtoRlogin = new MrngCheckBox();
            this.chkProtoRdp = new MrngCheckBox();
            this.chkProtoVnc = new MrngCheckBox();
            this.lblDestination = new mRemoteNG.UI.Controls.MrngLabel();
            this.cbDestination = new mRemoteNG.UI.Controls.MrngComboBox();
            this.pnlImport = new System.Windows.Forms.TableLayoutPanel();
            this.pnlMain = new System.Windows.Forms.TableLayoutPanel();
            this.portScanToolTip = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.olvHosts)).BeginInit();
            this.resultsMenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericSelectorTimeout)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.portEnd)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.portStart)).BeginInit();
            this.pnlIp.SuspendLayout();
            this.pnlFilter.SuspendLayout();
            this.pnlImport.SuspendLayout();
            this.pnlMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // ipStart
            // 
            this.ipStart.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ipStart.Location = new System.Drawing.Point(133, 3);
            this.ipStart.Name = "ipStart";
            this.ipStart.Size = new System.Drawing.Size(124, 18);
            this.ipStart.TabIndex = 1;
            this.ipStart.ToolTipText = "";
            // 
            // ipEnd
            // 
            this.ipEnd.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ipEnd.Location = new System.Drawing.Point(133, 27);
            this.ipEnd.Name = "ipEnd";
            this.ipEnd.Size = new System.Drawing.Size(124, 18);
            this.ipEnd.TabIndex = 2;
            this.ipEnd.ToolTipText = "";
            // 
            // lblStartIP
            // 
            this.lblStartIP.AutoSize = true;
            this.lblStartIP.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStartIP.Location = new System.Drawing.Point(3, 0);
            this.lblStartIP.Name = "lblStartIP";
            this.lblStartIP.Size = new System.Drawing.Size(124, 24);
            this.lblStartIP.TabIndex = 0;
            this.lblStartIP.Text = "First IP";
            this.lblStartIP.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblEndIP
            // 
            this.lblEndIP.AutoSize = true;
            this.lblEndIP.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEndIP.Location = new System.Drawing.Point(3, 24);
            this.lblEndIP.Name = "lblEndIP";
            this.lblEndIP.Size = new System.Drawing.Size(124, 24);
            this.lblEndIP.TabIndex = 5;
            this.lblEndIP.Text = "Last IP";
            this.lblEndIP.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // olvHosts
            // 
            this.olvHosts.CellEditUseWholeCell = false;
            this.olvHosts.ContextMenuStrip = this.resultsMenuStrip;
            this.olvHosts.Cursor = System.Windows.Forms.Cursors.Default;
            this.olvHosts.DecorateLines = true;
            this.olvHosts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.olvHosts.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.olvHosts.FullRowSelect = true;
            this.olvHosts.GridLines = true;
            this.olvHosts.HideSelection = false;
            this.olvHosts.Location = new System.Drawing.Point(3, 168);
            this.olvHosts.Name = "olvHosts";
            this.olvHosts.ShowGroups = false;
            this.olvHosts.Size = new System.Drawing.Size(878, 230);
            this.olvHosts.TabIndex = 26;
            this.olvHosts.UseCompatibleStateImageBehavior = false;
            this.olvHosts.View = System.Windows.Forms.View.Details;
            // 
            // resultsMenuStrip
            // 
            this.resultsMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.importHTTPToolStripMenuItem,
            this.importHTTPSToolStripMenuItem,
            this.importRDPToolStripMenuItem,
            this.importRloginToolStripMenuItem,
            this.importSSH2ToolStripMenuItem,
            this.importTelnetToolStripMenuItem,
            this.importVNCToolStripMenuItem});
            this.resultsMenuStrip.Name = "resultsMenuStrip";
            this.resultsMenuStrip.Size = new System.Drawing.Size(148, 158);
            // 
            // importHTTPToolStripMenuItem
            // 
            this.importHTTPToolStripMenuItem.Name = "importHTTPToolStripMenuItem";
            this.importHTTPToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.importHTTPToolStripMenuItem.Text = "Import HTTP";
            this.importHTTPToolStripMenuItem.Click += new System.EventHandler(this.importHTTPToolStripMenuItem_Click);
            // 
            // importHTTPSToolStripMenuItem
            // 
            this.importHTTPSToolStripMenuItem.Name = "importHTTPSToolStripMenuItem";
            this.importHTTPSToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.importHTTPSToolStripMenuItem.Text = "Import HTTPS";
            this.importHTTPSToolStripMenuItem.Click += new System.EventHandler(this.importHTTPSToolStripMenuItem_Click);
            // 
            // importRDPToolStripMenuItem
            // 
            this.importRDPToolStripMenuItem.Name = "importRDPToolStripMenuItem";
            this.importRDPToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.importRDPToolStripMenuItem.Text = "Import RDP";
            this.importRDPToolStripMenuItem.Click += new System.EventHandler(this.importRDPToolStripMenuItem_Click);
            // 
            // importRloginToolStripMenuItem
            // 
            this.importRloginToolStripMenuItem.Name = "importRloginToolStripMenuItem";
            this.importRloginToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.importRloginToolStripMenuItem.Text = "Import Rlogin";
            this.importRloginToolStripMenuItem.Click += new System.EventHandler(this.importRloginToolStripMenuItem_Click);
            // 
            // importSSH2ToolStripMenuItem
            // 
            this.importSSH2ToolStripMenuItem.Name = "importSSH2ToolStripMenuItem";
            this.importSSH2ToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.importSSH2ToolStripMenuItem.Text = "Import SSH2";
            this.importSSH2ToolStripMenuItem.Click += new System.EventHandler(this.importSSH2ToolStripMenuItem_Click);
            // 
            // importTelnetToolStripMenuItem
            // 
            this.importTelnetToolStripMenuItem.Name = "importTelnetToolStripMenuItem";
            this.importTelnetToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.importTelnetToolStripMenuItem.Text = "Import Telnet";
            this.importTelnetToolStripMenuItem.Click += new System.EventHandler(this.importTelnetToolStripMenuItem_Click);
            // 
            // importVNCToolStripMenuItem
            // 
            this.importVNCToolStripMenuItem.Name = "importVNCToolStripMenuItem";
            this.importVNCToolStripMenuItem.Size = new System.Drawing.Size(147, 22);
            this.importVNCToolStripMenuItem.Text = "Import VNC";
            this.importVNCToolStripMenuItem.Click += new System.EventHandler(this.importVNCToolStripMenuItem_Click);
            // 
            // btnImport
            // 
            this.btnImport._mice = MrngButton.MouseState.OUT;
            this.btnImport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnImport.Location = new System.Drawing.Point(765, 27);
            this.btnImport.Name = "btnImport";
            this.btnImport.Size = new System.Drawing.Size(110, 24);
            this.btnImport.TabIndex = 8;
            this.btnImport.Text = "&Import";
            this.btnImport.UseVisualStyleBackColor = true;
            this.btnImport.Click += new System.EventHandler(this.btnImport_Click);
            // 
            // cbProtocol
            // 
            this.cbProtocol._mice = MrngComboBox.MouseState.HOVER;
            this.cbProtocol.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbProtocol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbProtocol.FormattingEnabled = true;
            this.cbProtocol.Items.AddRange(new object[] {
            "SSH2",
            "Telnet",
            "HTTP",
            "HTTPS",
            "Rlogin",
            "RDP",
            "VNC"});
            this.cbProtocol.Location = new System.Drawing.Point(3, 27);
            this.cbProtocol.Name = "cbProtocol";
            this.cbProtocol.Size = new System.Drawing.Size(144, 21);
            this.cbProtocol.TabIndex = 7;
            // 
            // lblOnlyImport
            // 
            this.lblOnlyImport.AutoSize = true;
            this.lblOnlyImport.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblOnlyImport.Location = new System.Drawing.Point(3, 0);
            this.lblOnlyImport.Name = "lblOnlyImport";
            this.lblOnlyImport.Size = new System.Drawing.Size(144, 24);
            this.lblOnlyImport.TabIndex = 1;
            this.lblOnlyImport.Text = "Protocol to import";
            this.lblOnlyImport.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // clmHost
            // 
            this.clmHost.AspectName = "HostNameOnly";
            this.clmHost.Text = "Hostname";
            this.clmHost.Width = 160;
            //
            // clmIp
            //
            this.clmIp.AspectName = "HostIp";
            this.clmIp.Text = "IP";
            this.clmIp.Width = 110;
            // 
            // clmSSH
            // 
            this.clmSSH.AspectName = "SshName";
            this.clmSSH.Text = "SSH";
            this.clmSSH.Width = 50;
            this.clmSSH.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // clmTelnet
            // 
            this.clmTelnet.AspectName = "TelnetName";
            this.clmTelnet.Text = "Telnet";
            this.clmTelnet.Width = 50;
            this.clmTelnet.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // clmHTTP
            // 
            this.clmHTTP.AspectName = "HttpName";
            this.clmHTTP.Text = "HTTP";
            this.clmHTTP.Width = 50;
            this.clmHTTP.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // clmHTTPS
            // 
            this.clmHTTPS.AspectName = "HttpsName";
            this.clmHTTPS.Text = "HTTPS";
            this.clmHTTPS.Width = 50;
            this.clmHTTPS.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // clmRlogin
            // 
            this.clmRlogin.AspectName = "RloginName";
            this.clmRlogin.Text = "Rlogin";
            this.clmRlogin.Width = 50;
            this.clmRlogin.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // clmRDP
            // 
            this.clmRDP.AspectName = "RdpName";
            this.clmRDP.Text = "RDP";
            this.clmRDP.Width = 50;
            this.clmRDP.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // clmVNC
            // 
            this.clmVNC.AspectName = "VncName";
            this.clmVNC.Text = "VNC";
            this.clmVNC.Width = 50;
            this.clmVNC.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // clmOpenPorts
            // 
            this.clmOpenPorts.AspectName = "OpenPortsName";
            this.clmOpenPorts.FillsFreeSpace = true;
            this.clmOpenPorts.Text = "Open Ports";
            this.clmOpenPorts.Width = 150;
            // 
            // clmClosedPorts
            // 
            this.clmClosedPorts.AspectName = "ClosedPortsName";
            this.clmClosedPorts.FillsFreeSpace = true;
            this.clmClosedPorts.Text = "Closed Ports";
            this.clmClosedPorts.Width = 150;
            // 
            // prgBar
            // 
            this.prgBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.prgBar.Location = new System.Drawing.Point(3, 138);
            this.prgBar.Name = "prgBar";
            this.prgBar.Size = new System.Drawing.Size(878, 24);
            this.prgBar.Step = 1;
            this.prgBar.TabIndex = 28;
            // 
            // numericSelectorTimeout
            // 
            this.numericSelectorTimeout.Location = new System.Drawing.Point(133, 99);
            this.numericSelectorTimeout.Maximum = new decimal(new int[] {
            2147482,
            0,
            0,
            0});
            this.numericSelectorTimeout.Name = "numericSelectorTimeout";
            this.numericSelectorTimeout.Size = new System.Drawing.Size(67, 22);
            this.numericSelectorTimeout.TabIndex = 5;
            this.numericSelectorTimeout.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            // 
            // lblTimeout
            // 
            this.lblTimeout.AutoSize = true;
            this.lblTimeout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTimeout.Location = new System.Drawing.Point(3, 96);
            this.lblTimeout.Name = "lblTimeout";
            this.lblTimeout.Size = new System.Drawing.Size(124, 33);
            this.lblTimeout.TabIndex = 16;
            this.lblTimeout.Text = "Timeout [seconds]";
            this.lblTimeout.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // portEnd
            // 
            this.portEnd.Enabled = false;
            this.portEnd.Location = new System.Drawing.Point(133, 75);
            this.portEnd.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.portEnd.Name = "portEnd";
            this.portEnd.Size = new System.Drawing.Size(67, 22);
            this.portEnd.TabIndex = 4;
            this.portEnd.Value = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.portEnd.Enter += new System.EventHandler(this.portEnd_Enter);
            // 
            // portStart
            // 
            this.portStart.Enabled = false;
            this.portStart.Location = new System.Drawing.Point(133, 51);
            this.portStart.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.portStart.Name = "portStart";
            this.portStart.Size = new System.Drawing.Size(67, 22);
            this.portStart.TabIndex = 3;
            this.portStart.Enter += new System.EventHandler(this.portStart_Enter);
            // 
            // pnlIp
            // 
            this.pnlIp.AutoSize = true;
            this.pnlIp.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlIp.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlIp.ColumnCount = 3;
            this.pnlIp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            // Was a fixed 130 pixels, which is wider than the IP control's designed 124 - until the
            // display scales. At 125% the control needs about 155 and the column stays at 130, so
            // the fourth octet was cut off the edge of the cell: not reachable by mouse, and typing
            // into a box nobody can see. Sized to its content instead.
            this.pnlIp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlIp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.pnlIp.Controls.Add(this.lblStartIP, 0, 0);
            this.pnlIp.Controls.Add(this.ipEnd, 1, 1);
            this.pnlIp.Controls.Add(this.ipStart, 1, 0);
            this.pnlIp.Controls.Add(this.lblEndIP, 0, 1);
            this.pnlIp.Controls.Add(this.portStart, 1, 2);
            this.pnlIp.Controls.Add(this.portEnd, 1, 3);
            this.pnlIp.Controls.Add(this.lblTimeout, 0, 4);
            this.pnlIp.Controls.Add(this.numericSelectorTimeout, 1, 4);
            this.pnlIp.Controls.Add(this.btnScan, 2, 4);
            this.pnlIp.Controls.Add(this.ngCheckFirstPort, 0, 2);
            this.pnlIp.Controls.Add(this.ngCheckLastPort, 0, 3);
            // The two result filters used to sit here, in the block that says what to scan -
            // they say what to show of what came back, which is a different question and now has
            // its own block. The scan scope text takes the whole free column instead.
            this.pnlIp.Controls.Add(this.lblScanScope, 2, 0);
            this.pnlIp.SetRowSpan(this.lblScanScope, 4);
            this.pnlIp.Location = new System.Drawing.Point(3, 3);
            this.pnlIp.Name = "pnlIp";
            this.pnlIp.RowCount = 5;
            // Rows sized to their contents, not to a fixed 24 pixels. The controls grow with the
            // display scaling and the row did not, so at 125% the bottom of the address fields was
            // cut off - the same fault as the fixed column width next to it.
            this.pnlIp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlIp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlIp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlIp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlIp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlIp.Size = new System.Drawing.Size(878, 129);
            this.pnlIp.TabIndex = 103;
            // 
            // btnScan
            // 
            this.btnScan._mice = MrngButton.MouseState.OUT;
            // Left, not right. Its column takes whatever space is left over, so anchoring right
            // put the button against the far edge of the window - a screen's width away from the
            // fields it acts on, and further the wider the monitor. It sits by the timeout now.
            this.btnScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
            this.btnScan.Margin = new System.Windows.Forms.Padding(16, 3, 3, 3);
            this.btnScan.Image = global::mRemoteNG.Properties.Resources.Search_16x;
            this.btnScan.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // lblScanScope
            //
            this.chkOnlyResponding.AutoSize = true;
            this.chkOnlyResponding.Checked = true;
            this.chkOnlyResponding.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkOnlyResponding.Name = "chkOnlyResponding";
            this.chkOnlyResponding.TabIndex = 98;
            this.chkOnlyResponding.UseVisualStyleBackColor = true;
            this.chkOnlyResponding.CheckedChanged += new System.EventHandler(this.ChkOnlyResponding_CheckedChanged);
            //
            // chkOnlyNew
            //
            this.chkOnlyNew.AutoSize = true;
            this.chkOnlyNew.Name = "chkOnlyNew";
            this.chkOnlyNew.TabIndex = 97;
            this.chkOnlyNew.UseVisualStyleBackColor = true;
            this.chkOnlyNew.CheckedChanged += new System.EventHandler(this.ChkOnlyResponding_CheckedChanged);
            //
            this.lblScanScope.AutoSize = false;
            this.lblScanScope.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblScanScope.Name = "lblScanScope";
            this.lblScanScope.TabIndex = 99;
            this.lblScanScope.Text = "";
            this.lblScanScope.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            this.btnScan.Location = new System.Drawing.Point(765, 99);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new System.Drawing.Size(110, 24);
            this.btnScan.TabIndex = 6;
            this.btnScan.Text = "&Scan";
            this.btnScan.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.btnScan.UseVisualStyleBackColor = true;
            this.btnScan.Click += new System.EventHandler(this.btnScan_Click);
            // 
            // ngCheckFirstPort
            // 
            this.ngCheckFirstPort._mice = MrngCheckBox.MouseState.OUT;
            this.ngCheckFirstPort.AutoSize = true;
            this.ngCheckFirstPort.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ngCheckFirstPort.Location = new System.Drawing.Point(3, 51);
            this.ngCheckFirstPort.Name = "ngCheckFirstPort";
            this.ngCheckFirstPort.Size = new System.Drawing.Size(72, 17);
            this.ngCheckFirstPort.TabIndex = 17;
            this.ngCheckFirstPort.Text = "First Port";
            this.ngCheckFirstPort.UseVisualStyleBackColor = true;
            this.ngCheckFirstPort.CheckedChanged += new System.EventHandler(this.NgCheckFirstPort_CheckedChanged);
            // 
            // ngCheckLastPort
            // 
            this.ngCheckLastPort._mice = MrngCheckBox.MouseState.OUT;
            this.ngCheckLastPort.AutoSize = true;
            this.ngCheckLastPort.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ngCheckLastPort.Location = new System.Drawing.Point(3, 75);
            this.ngCheckLastPort.Name = "ngCheckLastPort";
            this.ngCheckLastPort.Size = new System.Drawing.Size(70, 17);
            this.ngCheckLastPort.TabIndex = 18;
            this.ngCheckLastPort.Text = "Last Port";
            this.ngCheckLastPort.UseVisualStyleBackColor = true;
            this.ngCheckLastPort.CheckedChanged += new System.EventHandler(this.NgCheckLastPort_CheckedChanged);
            // 
            // pnlImport
            // 
            this.pnlImport.ColumnCount = 2;
            this.pnlImport.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.pnlImport.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.pnlImport.Controls.Add(this.lblOnlyImport, 0, 0);
            this.pnlImport.Controls.Add(this.cbProtocol, 0, 1);
            this.pnlImport.Controls.Add(this.btnImport, 1, 1);
            this.pnlImport.Controls.Add(this.lblDestination, 0, 2);
            this.pnlImport.Controls.Add(this.cbDestination, 1, 2);
            //
            // lblDestination
            //
            this.lblDestination.AutoSize = true;
            this.lblDestination.Name = "lblDestination";
            this.lblDestination.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // cbDestination
            //
            this.cbDestination.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbDestination.Name = "cbDestination";
            this.cbDestination.Width = 420;
            this.pnlImport.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlImport.Location = new System.Drawing.Point(3, 404);
            this.pnlImport.Name = "pnlImport";
            // Three rows now, sized to their contents: the destination folder is chosen here
            // rather than being read invisibly from whatever happens to be selected in the tree.
            this.pnlImport.RowCount = 3;
            this.pnlImport.AutoSize = true;
            this.pnlImport.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlImport.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlImport.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlImport.Size = new System.Drawing.Size(878, 54);
            this.pnlImport.TabIndex = 104;
            // 
            //
            // pnlFilter
            //
            this.pnlFilter.AutoSize = true;
            this.pnlFilter.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            // Ten columns, with the controls in them directly. They were in two wrapping
            // FlowLayoutPanels before, which left a hand's width of blank space between this block
            // and the grid: a TableLayoutPanel measures a child by proposing zero width, a
            // FlowLayoutPanel with WrapContents answers that by putting every control on a line of
            // its own, and the row kept that height even though the panel drew itself on one line
            // once it had real width to work with.
            //
            // The labels share column 0, so both rows start at the same place. The two long
            // checkboxes on the first row span the narrow protocol columns beneath them rather
            // than widening any one of them, and the last column takes up the slack.
            this.pnlFilter.ColumnCount = 10;
            this.pnlFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.pnlFilter.Controls.Add(this.lblFilter, 0, 0);
            this.pnlFilter.Controls.Add(this.chkOnlyResponding, 1, 0);
            this.pnlFilter.SetColumnSpan(this.chkOnlyResponding, 4);
            this.pnlFilter.Controls.Add(this.chkOnlyNew, 5, 0);
            this.pnlFilter.SetColumnSpan(this.chkOnlyNew, 5);
            this.pnlFilter.Controls.Add(this.lblProtocolFilter, 0, 1);
            this.pnlFilter.Controls.Add(this.chkProtoAll, 1, 1);
            this.pnlFilter.Controls.Add(this.chkProtoSsh, 2, 1);
            this.pnlFilter.Controls.Add(this.chkProtoTelnet, 3, 1);
            this.pnlFilter.Controls.Add(this.chkProtoHttp, 4, 1);
            this.pnlFilter.Controls.Add(this.chkProtoHttps, 5, 1);
            this.pnlFilter.Controls.Add(this.chkProtoRlogin, 6, 1);
            this.pnlFilter.Controls.Add(this.chkProtoRdp, 7, 1);
            this.pnlFilter.Controls.Add(this.chkProtoVnc, 8, 1);
            this.pnlFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlFilter.Name = "pnlFilter";
            this.pnlFilter.RowCount = 2;
            this.pnlFilter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlFilter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlFilter.TabIndex = 106;
            //
            // lblFilter
            //
            this.lblFilter.AutoSize = true;
            this.lblFilter.Margin = new System.Windows.Forms.Padding(3, 4, 8, 3);
            this.lblFilter.Name = "lblFilter";
            //
            // lblProtocolFilter
            //
            this.lblProtocolFilter.AutoSize = true;
            this.lblProtocolFilter.Margin = new System.Windows.Forms.Padding(3, 4, 8, 3);
            this.lblProtocolFilter.Name = "lblProtocolFilter";
            //
            // chkProtoAll
            //
            this.chkProtoAll.AutoSize = true;
            this.chkProtoAll.Checked = true;
            this.chkProtoAll.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkProtoAll.Margin = new System.Windows.Forms.Padding(3, 3, 16, 3);
            this.chkProtoAll.Name = "chkProtoAll";
            this.chkProtoAll.UseVisualStyleBackColor = true;
            this.chkProtoAll.CheckedChanged += new System.EventHandler(this.ChkProtoAll_CheckedChanged);
            //
            // chkProtoSsh
            //
            this.chkProtoSsh.AutoSize = true;
            this.chkProtoSsh.Checked = true;
            this.chkProtoSsh.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkProtoSsh.Margin = new System.Windows.Forms.Padding(3, 3, 12, 3);
            this.chkProtoSsh.Name = "chkProtoSsh";
            this.chkProtoSsh.Text = "SSH";
            this.chkProtoSsh.UseVisualStyleBackColor = true;
            this.chkProtoSsh.CheckedChanged += new System.EventHandler(this.ProtocolFilter_CheckedChanged);
            //
            // chkProtoTelnet
            //
            this.chkProtoTelnet.AutoSize = true;
            this.chkProtoTelnet.Checked = true;
            this.chkProtoTelnet.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkProtoTelnet.Margin = new System.Windows.Forms.Padding(3, 3, 12, 3);
            this.chkProtoTelnet.Name = "chkProtoTelnet";
            this.chkProtoTelnet.Text = "Telnet";
            this.chkProtoTelnet.UseVisualStyleBackColor = true;
            this.chkProtoTelnet.CheckedChanged += new System.EventHandler(this.ProtocolFilter_CheckedChanged);
            //
            // chkProtoHttp
            //
            this.chkProtoHttp.AutoSize = true;
            this.chkProtoHttp.Checked = true;
            this.chkProtoHttp.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkProtoHttp.Margin = new System.Windows.Forms.Padding(3, 3, 12, 3);
            this.chkProtoHttp.Name = "chkProtoHttp";
            this.chkProtoHttp.Text = "HTTP";
            this.chkProtoHttp.UseVisualStyleBackColor = true;
            this.chkProtoHttp.CheckedChanged += new System.EventHandler(this.ProtocolFilter_CheckedChanged);
            //
            // chkProtoHttps
            //
            this.chkProtoHttps.AutoSize = true;
            this.chkProtoHttps.Checked = true;
            this.chkProtoHttps.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkProtoHttps.Margin = new System.Windows.Forms.Padding(3, 3, 12, 3);
            this.chkProtoHttps.Name = "chkProtoHttps";
            this.chkProtoHttps.Text = "HTTPS";
            this.chkProtoHttps.UseVisualStyleBackColor = true;
            this.chkProtoHttps.CheckedChanged += new System.EventHandler(this.ProtocolFilter_CheckedChanged);
            //
            // chkProtoRlogin
            //
            this.chkProtoRlogin.AutoSize = true;
            this.chkProtoRlogin.Checked = true;
            this.chkProtoRlogin.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkProtoRlogin.Margin = new System.Windows.Forms.Padding(3, 3, 12, 3);
            this.chkProtoRlogin.Name = "chkProtoRlogin";
            this.chkProtoRlogin.Text = "Rlogin";
            this.chkProtoRlogin.UseVisualStyleBackColor = true;
            this.chkProtoRlogin.CheckedChanged += new System.EventHandler(this.ProtocolFilter_CheckedChanged);
            //
            // chkProtoRdp
            //
            this.chkProtoRdp.AutoSize = true;
            this.chkProtoRdp.Checked = true;
            this.chkProtoRdp.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkProtoRdp.Margin = new System.Windows.Forms.Padding(3, 3, 12, 3);
            this.chkProtoRdp.Name = "chkProtoRdp";
            this.chkProtoRdp.Text = "RDP";
            this.chkProtoRdp.UseVisualStyleBackColor = true;
            this.chkProtoRdp.CheckedChanged += new System.EventHandler(this.ProtocolFilter_CheckedChanged);
            //
            // chkProtoVnc
            //
            this.chkProtoVnc.AutoSize = true;
            this.chkProtoVnc.Checked = true;
            this.chkProtoVnc.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkProtoVnc.Margin = new System.Windows.Forms.Padding(3, 3, 12, 3);
            this.chkProtoVnc.Name = "chkProtoVnc";
            this.chkProtoVnc.Text = "VNC";
            this.chkProtoVnc.UseVisualStyleBackColor = true;
            this.chkProtoVnc.CheckedChanged += new System.EventHandler(this.ProtocolFilter_CheckedChanged);
            //
            // pnlMain
            // 
            this.pnlMain.ColumnCount = 1;
            this.pnlMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.pnlMain.Controls.Add(this.pnlIp, 0, 0);
            this.pnlMain.Controls.Add(this.pnlFilter, 0, 1);
            this.pnlMain.Controls.Add(this.prgBar, 0, 2);
            this.pnlMain.Controls.Add(this.olvHosts, 0, 3);
            this.pnlMain.Controls.Add(this.pnlImport, 0, 4);
            this.pnlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMain.Location = new System.Drawing.Point(0, 0);
            this.pnlMain.Name = "pnlMain";
            this.pnlMain.RowCount = 5;
            this.pnlMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.pnlMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.pnlMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.pnlMain.Size = new System.Drawing.Size(884, 461);
            this.pnlMain.TabIndex = 105;
            // 
            // PortScanWindow
            // 
            this.AcceptButton = this.btnImport;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(884, 461);
            this.Controls.Add(this.pnlMain);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "PortScanWindow";
            this.TabText = "Port Scan";
            this.Text = "Port Scan";
            this.Load += new System.EventHandler(this.PortScan_Load);
            ((System.ComponentModel.ISupportInitialize)(this.olvHosts)).EndInit();
            this.resultsMenuStrip.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numericSelectorTimeout)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.portEnd)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.portStart)).EndInit();
            this.pnlIp.ResumeLayout(false);
            this.pnlIp.PerformLayout();
            this.pnlFilter.ResumeLayout(false);
            this.pnlFilter.PerformLayout();
            this.pnlImport.ResumeLayout(false);
            this.pnlImport.PerformLayout();
            this.pnlMain.ResumeLayout(false);
            this.ResumeLayout(false);

		}
        #endregion

        private System.Windows.Forms.ContextMenuStrip resultsMenuStrip;
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.ToolStripMenuItem importHTTPToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importHTTPSToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importRDPToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importRloginToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importSSH2ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importTelnetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem importVNCToolStripMenuItem;
		private System.Windows.Forms.Label lblTimeout;
		private Controls.MrngNumericUpDown numericSelectorTimeout;
        private System.Windows.Forms.TableLayoutPanel pnlIp;
        private System.Windows.Forms.TableLayoutPanel pnlImport;
        internal MrngButton btnScan;
        private System.Windows.Forms.TableLayoutPanel pnlMain;
        private MrngCheckBox ngCheckFirstPort;
        private MrngCheckBox ngCheckLastPort;
        private mRemoteNG.UI.Controls.MrngLabel lblScanScope;
        private MrngCheckBox chkOnlyResponding;
        private MrngCheckBox chkOnlyNew;
        private System.Windows.Forms.TableLayoutPanel pnlFilter;
        private mRemoteNG.UI.Controls.MrngLabel lblFilter;
        private mRemoteNG.UI.Controls.MrngLabel lblProtocolFilter;
        private MrngCheckBox chkProtoAll;
        private MrngCheckBox chkProtoSsh;
        private MrngCheckBox chkProtoTelnet;
        private MrngCheckBox chkProtoHttp;
        private MrngCheckBox chkProtoHttps;
        private MrngCheckBox chkProtoRlogin;
        private MrngCheckBox chkProtoRdp;
        private MrngCheckBox chkProtoVnc;
        private mRemoteNG.UI.Controls.MrngLabel lblDestination;
        private mRemoteNG.UI.Controls.MrngComboBox cbDestination;
        private System.Windows.Forms.ToolTip portScanToolTip;
    }
}
