using System.ComponentModel;
using System.Windows.Forms;
using mRemoteNG.Themes;
using System;
using System.Collections;
using System.Linq;
using mRemoteNG.App;
using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Connection.Protocol.SSH;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;

namespace mRemoteNG.UI.Controls
{
    [SupportedOSPlatform("windows")]
    public partial class MultiSshToolStrip : ToolStrip
    {
        private IContainer components;
        private ToolStripLabel lblMultiSsh;
        private ToolStripTextBox txtMultiSsh;
        private int previousCommandIndex = 0;
        private readonly ArrayList processHandlers = [];
        private readonly ArrayList quickConnectConnections = [];
        private readonly ArrayList previousCommands = [];
        private readonly ThemeManager _themeManager;

        private int CommandHistoryLength { get; set; } = 100;

        public MultiSshToolStrip()
        {
            InitializeComponent();
            _themeManager = ThemeManager.getInstance();
            _themeManager.ThemeChanged += ApplyTheme;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (!_themeManager.ActiveAndExtended) return;
            txtMultiSsh.BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Background");
            txtMultiSsh.ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("TextBox_Foreground");
        }

        /// <summary>
        /// The open sessions this toolbar can type into.
        /// </summary>
        /// <remarks>
        /// PuTTY only, until now: it collected PuttyBase sessions and posted Windows messages to
        /// their windows. Every connection on this fork uses the native SSH protocol, which has no
        /// such window, so the toolbar accepted a command, said nothing, and sent it nowhere.
        /// </remarks>
        private ArrayList ProcessOpenConnections(ConnectionInfo connection)
        {
            ArrayList handlers = new();

            foreach (ProtocolBase _base in connection.OpenConnections)
            {
                if (_base is PuttyBase or ProtocolSshNative)
                    handlers.Add(_base);
            }

            return handlers;
        }

        private void SendAllKeystrokes(int keyType, int keyData)
        {
            if (processHandlers.Count == 0) return;

            foreach (object handler in processHandlers)
            {
                // Only PuTTY is driven a keystroke at a time; the native sessions get the whole
                // line at once, from SendCommandToNativeSessions.
                if (handler is PuttyBase proc)
                    NativeMethods.PostMessage(proc.PuttyHandle, keyType, new IntPtr(keyData), new IntPtr(0));
            }
        }

        /// <summary>
        /// Gives the native sessions the finished line, rather than one character at a time.
        /// </summary>
        private void SendCommandToNativeSessions(string command)
        {
            foreach (object handler in processHandlers)
            {
                if (handler is ProtocolSshNative ssh)
                    ssh.SendCommand(command);
            }
        }

        #region Key Event Handler

        private void RefreshActiveConnections(object sender, EventArgs e)
        {
            processHandlers.Clear();
            foreach (ConnectionInfo connection in quickConnectConnections)
            {
                processHandlers.AddRange(ProcessOpenConnections(connection));
            }

            System.Collections.Generic.IEnumerable<ConnectionInfo> connectionTreeConnections = Runtime.ConnectionsService.ConnectionTreeModel.GetRecursiveChildList().Where(item => item.OpenConnections.Count > 0);

            foreach (ConnectionInfo connection in connectionTreeConnections)
            {
                processHandlers.AddRange(ProcessOpenConnections(connection));
            }
        }

        private void ProcessKeyPress(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                try
                {
                    switch (e.KeyCode)
                    {
                        case Keys.Up when previousCommandIndex - 1 >= 0:
                            previousCommandIndex -= 1;
                            break;
                        case Keys.Down when previousCommandIndex + 1 < previousCommands.Count:
                            previousCommandIndex += 1;
                            break;
                        default:
                            return;
                    }
                }
                catch { }

                txtMultiSsh.Text = previousCommands[previousCommandIndex].ToString();
                txtMultiSsh.SelectAll();
            }

            if (e.Control && e.KeyCode != Keys.V && e.Alt == false)
            {
                SendAllKeystrokes(NativeMethods.WM_KEYDOWN, e.KeyValue);
            }

            if (e.KeyCode == Keys.Enter)
            {
                foreach (char chr1 in txtMultiSsh.Text)
                {
                    SendAllKeystrokes(NativeMethods.WM_CHAR, Convert.ToByte(chr1));
                }

                SendAllKeystrokes(NativeMethods.WM_KEYDOWN, 13); // Enter = char13
                SendCommandToNativeSessions(txtMultiSsh.Text);
            }
        }

        private void ProcessKeyRelease(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            if (string.IsNullOrWhiteSpace(txtMultiSsh.Text)) return;

            previousCommands.Add(txtMultiSsh.Text.Trim());

            if (previousCommands.Count >= CommandHistoryLength) previousCommands.RemoveAt(0);

            previousCommandIndex = previousCommands.Count - 1;
            txtMultiSsh.Clear();
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if(components != null)
                    components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblMultiSsh = new ToolStripLabel();
            this.txtMultiSsh = new ToolStripTextBox();
            this.SuspendLayout();
            // 
            // lblMultiSSH
            // 
            this.lblMultiSsh.Name = "_lblMultiSsh";
            this.lblMultiSsh.Size = new System.Drawing.Size(77, 22);
            this.lblMultiSsh.Text = Language.MultiSsh;
            // 
            // txtMultiSsh
            // 
            this.txtMultiSsh.Name = "_txtMultiSsh";
            this.txtMultiSsh.Size = new System.Drawing.Size(new DisplayProperties().ScaleWidth(300), 25);
            this.txtMultiSsh.ToolTipText = Language.MultiSshToolTip;
            this.txtMultiSsh.Enter += RefreshActiveConnections;
            this.txtMultiSsh.KeyDown += ProcessKeyPress;
            this.txtMultiSsh.KeyUp += ProcessKeyRelease;

            this.Items.AddRange(new ToolStripItem[]
            {
                lblMultiSsh,
                txtMultiSsh
            });
            this.ResumeLayout(false);
        }

        #endregion

    }
}