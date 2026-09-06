using mRemoteNG.App;
using mRemoteNG.Connection.Protocol;
using System;
using System.Drawing;
using System.Windows.Forms;
using mRemoteNG.UI.Tabs;
using WeifenLuo.WinFormsUI.Docking;
using System.Runtime.Versioning;

namespace mRemoteNG.Connection
{
    [SupportedOSPlatform("windows")]
    public sealed partial class InterfaceControl
    {
        public ProtocolBase Protocol { get; set; }
        public ConnectionInfo Info { get; set; }
        // in case the connection is through a SSH tunnel the Info is a copy of original info with hostname and port number overwritten with localhost and local tunnel port
        // and the original Info is saved in the following variable
        public ConnectionInfo OriginalInfo { get; set; }
        // in case the connection is through a SSH tunnel the Info of the SSHTunnelConnection is also saved for reference in log messages etc.
        public ConnectionInfo SSHTunnelInfo { get; set; }


        public InterfaceControl(Control parent, ProtocolBase protocol, ConnectionInfo info)
        {
            try
            {
                Protocol = protocol;
                Info = info;
                Parent = parent;
                // Docked, not sized once and anchored. Measured on 2026-09-06 with a session
                // going fullscreen: the tab around this grew to 3198x1770 and this stayed at
                // 2656x1456, the size it had before - so the session filled four fifths of the
                // screen and the rest was the host panel's black. Whatever the anchors were
                // meant to do, they were not tracking the parent; filling it needs no bookkeeping
                // and cannot fall behind.
                Dock = DockStyle.Fill;
                InitializeComponent();
                
                // Enable custom painting for border
                this.Paint += InterfaceControl_Paint;
                
                // Set padding to prevent content from covering the frame border
                UpdatePaddingForFrameColor();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(Messages.MessageClass.ErrorMsg,
                                                    "Couldn\'t create new InterfaceControl" + Environment.NewLine +
                                                    ex.Message);
            }
        }

        /// <summary>
        /// Sent to a parent window when a mouse button goes down in one of its child windows.
        /// </summary>
        private const int WM_PARENTNOTIFY = 0x0210;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;

        /// <summary>
        /// Closes a menu left open elsewhere in the application when this session is clicked into.
        /// </summary>
        /// <remarks>
        /// A session is drawn by something that is not an ordinary WinForms control - an ActiveX
        /// client for RDP, a WebView2 for the native SSH terminal - and a click that lands in one
        /// of those is not the sort of event a ContextMenuStrip closes itself on. Right-clicking
        /// the connection tree and then working in the session left the menu sitting on top of it.
        ///
        /// Both paths are here because neither covers the other reliably: OnEnter is the WinForms
        /// account of the focus arriving, WM_PARENTNOTIFY the Win32 account of the click, and
        /// which one a given host produces depends on the control.
        /// </remarks>
        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            CloseMenusElsewhere();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_PARENTNOTIFY)
            {
                int notification = m.WParam.ToInt32() & 0xFFFF;

                if (notification == WM_LBUTTONDOWN || notification == WM_RBUTTONDOWN ||
                    notification == WM_MBUTTONDOWN)
                    CloseMenusElsewhere();
            }

            base.WndProc(ref m);
        }

        private static void CloseMenusElsewhere()
        {
            try
            {
                AppWindows.TreeFormIfBuilt?.ConnectionTree?.CloseContextMenu();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddMessage(Messages.MessageClass.WarningMsg,
                                                    "Couldn't close the connection tree's menu" +
                                                    Environment.NewLine + ex.Message, true);
            }
        }

        private void InterfaceControl_Paint(object sender, PaintEventArgs e)
        {
            // Draw colored border based on ConnectionFrameColor property
            if (Info?.ConnectionFrameColor != null && Info.ConnectionFrameColor != ConnectionFrameColor.None)
            {
                Color frameColor = GetFrameColor(Info.ConnectionFrameColor);
                int borderWidth = 4; // 4 pixel border for visibility
                
                using (Pen pen = new Pen(frameColor, borderWidth))
                {
                    // Draw border inside the control bounds
                    Rectangle rect = new Rectangle(
                        borderWidth / 2,
                        borderWidth / 2,
                        this.Width - borderWidth,
                        this.Height - borderWidth
                    );
                    e.Graphics.DrawRectangle(pen, rect);
                }
            }
        }

        private void UpdatePaddingForFrameColor()
        {
            // Add padding to prevent content from covering the frame border
            if (Info?.ConnectionFrameColor != null && Info.ConnectionFrameColor != ConnectionFrameColor.None)
            {
                int borderWidth = 4; // Must match the border width in InterfaceControl_Paint
                // Add 2px margin so the border is fully visible and not covered by child controls
                int padding = borderWidth / 2 + 2;
                this.Padding = new Padding(padding);
            }
            else
            {
                this.Padding = new Padding(0);
            }
        }

        private Color GetFrameColor(ConnectionFrameColor frameColor)
        {
            return frameColor switch
            {
                ConnectionFrameColor.Red => Color.FromArgb(220, 53, 69),      // Bootstrap danger red
                ConnectionFrameColor.Yellow => Color.FromArgb(255, 193, 7),   // Warning yellow
                ConnectionFrameColor.Green => Color.FromArgb(40, 167, 69),    // Success green
                ConnectionFrameColor.Blue => Color.FromArgb(0, 123, 255),     // Primary blue
                ConnectionFrameColor.Purple => Color.FromArgb(111, 66, 193),  // Purple
                _ => Color.Transparent
            };
        }

        public static InterfaceControl FindInterfaceControl(DockPanel DockPnl)
        {
            // instead of repeating the code, call the routine using ConnectionTab if called by DockPanel
            if (DockPnl.ActiveDocument is ConnectionTab ct)
                return FindInterfaceControl(ct);
            return null;
        }

        public static InterfaceControl FindInterfaceControl(ConnectionTab tab)
        {
            if (tab.Controls.Count < 1) return null;
            // if the tab has more than one controls and the second is an InterfaceControl than it must be a connection through SSH tunnel
            // and the first Control is the SSH tunnel connection and thus the second control must be returned.
            if (tab.Controls.Count > 1)
            {
                if (tab.Controls[1] is InterfaceControl ic1)
                    return ic1;
            }
            if (tab.Controls[0] is InterfaceControl ic0)
                return ic0;

            return null;
        }
    }
}