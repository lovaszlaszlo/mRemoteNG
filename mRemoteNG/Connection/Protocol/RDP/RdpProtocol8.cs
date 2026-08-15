using System;
using System.Drawing;
using System.Windows.Forms;
using AxMSTSCLib;
using mRemoteNG.App;
using mRemoteNG.Messages;
using MSTSCLib;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace mRemoteNG.Connection.Protocol.RDP
{
    [SupportedOSPlatform("windows")]
    /* RDP v8 requires Windows 7 with:
		* https://support.microsoft.com/en-us/kb/2592687 
		* OR
		* https://support.microsoft.com/en-us/kb/2923545
		* 
		* Windows 8+ support RDP v8 out of the box.
		*/
    public class RdpProtocol8 : RdpProtocol7
    {
        private MsRdpClient8NotSafeForScripting RdpClient8 => (MsRdpClient8NotSafeForScripting)((AxHost)Control).GetOcx();

        protected override RdpVersion RdpProtocolVersion => RDP.RdpVersion.Rdc8;
        protected FormWindowState LastWindowState = FormWindowState.Minimized;

        // Debounce timer to reduce flickering during resize
        private System.Timers.Timer _resizeDebounceTimer;
        private Size _pendingResizeSize;
        private bool _hasPendingResize = false;

        public RdpProtocol8()
        {
            // Initialize debounce timer (300ms delay).
            // Keep this in the constructor because it doesn't root the instance in any
            // external static object – it's safe for the temporary probing instances
            // created by RdpProtocolFactory.
            _resizeDebounceTimer = new System.Timers.Timer(300);
            _resizeDebounceTimer.AutoReset = false;
            _resizeDebounceTimer.Elapsed += ResizeDebounceTimer_Elapsed;
        }

        public override bool Initialize()
        {
            if (!base.Initialize())
                return false;

            if (RdpVersion < Versions.RDC81) return false; // minimum dll version checked, loaded MSTSCLIB dll version is not capable

            // Subscribe to static/external events here (not in the constructor) so that
            // temporary probing instances created by RdpProtocolFactory.RdpVersionSupported()
            // are not rooted and do not accumulate memory leaks or spurious callbacks.
            _frmMain.ResizeEnd += ResizeEnd;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            // https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpextendedsettings-property
            if (connectionInfo.UseRestrictedAdmin)
            {
                SetExtendedProperty("RestrictedLogon", true);
            }
            else if (connectionInfo.UseRCG)
            {
                SetExtendedProperty("DisableCredentialsDelegation", true);
                SetExtendedProperty("RedirectedAuthentication", true);
            }
            
            return true;
        }

        public override bool Fullscreen
        {
            get => base.Fullscreen;
            protected set
            {
                base.Fullscreen = value;
                DoResizeClient();
            }
        }

        protected override void Resize(object sender, EventArgs e)
        {
            if (_frmMain == null) return;

            // Skip resize entirely when minimized or minimizing
            if (_frmMain.WindowState == FormWindowState.Minimized) return;

            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Resize() called - WindowState={_frmMain.WindowState}, LastWindowState={LastWindowState}");

            // Update control size during resize to keep UI synchronized
            // Actual RDP session resize is deferred to ResizeEnd() to prevent flickering
            DoResizeControl();

            // A window state change used to resize the session immediately. Entering fullscreen
            // goes Maximized -> Normal -> Maximized in one go (see FullscreenHandler), so that
            // fired a burst of resolution changes at the server and the session came back without
            // the keyboard. Debounce this the same as everything else, so a burst settles into a
            // single resize.
            if (LastWindowState != _frmMain.WindowState)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize() - Window state changed from {LastWindowState} to {_frmMain.WindowState}, scheduling debounced resize");
                LastWindowState = _frmMain.WindowState;
                ScheduleDebouncedResize();
            }
            else
            {
                // Do not wait for ResizeEnd: that only arrives for a drag of the main window. The
                // panel also changes size when the connection tree splitter is dragged or the dock
                // layout changes, and those never raise it - the session then kept its old
                // resolution and the panel grew scrollbars instead. The debounce timer restarts on
                // every call, so a drag still results in a single resize once it settles.
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize() - Window state unchanged ({_frmMain.WindowState}), scheduling debounced resize");
                ScheduleDebouncedResize();
            }
        }

        protected override void ResizeEnd(object sender, EventArgs e)
        {
            if (_frmMain == null) return;

            // Skip resize when minimized
            if (_frmMain.WindowState == FormWindowState.Minimized) return;

            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"ResizeEnd() called - WindowState={_frmMain.WindowState}");

            // Update window state tracking
            LastWindowState = _frmMain.WindowState;

            // Update control size immediately (no flicker)
            DoResizeControl();

            // Debounce the RDP session resize to reduce flickering
            ScheduleDebouncedResize();
        }

        private void ScheduleDebouncedResize()
        {
            if (InterfaceControl == null) return;

            // Store the pending size
            _pendingResizeSize = InterfaceControl.Size;
            _hasPendingResize = true;

            // Reset the timer (this delays the resize if called repeatedly)
            _resizeDebounceTimer?.Stop();
            _resizeDebounceTimer?.Start();

            Runtime.MessageCollector?.AddMessage(MessageClass.DebugMsg,
                $"Resize debounced - will resize to {_pendingResizeSize.Width}x{_pendingResizeSize.Height} after 300ms");
        }

        private void ResizeDebounceTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!_hasPendingResize) return;

            // Check if controls are still valid (not disposed during shutdown)
            if (Control == null || Control.IsDisposed || InterfaceControl == null || InterfaceControl.IsDisposed)
            {
                _hasPendingResize = false;
                return;
            }

            // Guard against the window handle not yet being created or already destroyed
            if (!InterfaceControl.IsHandleCreated)
            {
                _hasPendingResize = false;
                return;
            }

            _hasPendingResize = false;

            Runtime.MessageCollector?.AddMessage(MessageClass.DebugMsg,
                $"Debounce timer fired - executing delayed resize to {_pendingResizeSize.Width}x{_pendingResizeSize.Height}");

            // Marshal to the UI thread because DoResizeClient() accesses WinForms and COM objects.
            // Wrap in try/catch: even after the guards above, there is a disposal race between
            // this timer thread and the UI thread that can cause ObjectDisposedException or
            // InvalidOperationException from BeginInvoke.
            try
            {
                if (InterfaceControl.InvokeRequired)
                {
                    InterfaceControl.BeginInvoke(new Action(DoResizeClient));
                }
                else
                {
                    DoResizeClient();
                }
            }
            catch (ObjectDisposedException ex)
            {
                Runtime.MessageCollector?.AddMessage(MessageClass.DebugMsg,
                    $"ResizeDebounceTimer_Elapsed: control disposed during BeginInvoke ({ex.GetType().Name})");
            }
            catch (InvalidOperationException ex)
            {
                Runtime.MessageCollector?.AddMessage(MessageClass.DebugMsg,
                    $"ResizeDebounceTimer_Elapsed: control handle unavailable during BeginInvoke ({ex.GetType().Name})");
            }
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            // When display settings change (e.g., outer RDP session reconnects with a different
            // resolution/viewport), schedule a debounced resize so the inner RDP session is
            // updated to match the new panel dimensions once the display has settled.
            // SystemEvents.DisplaySettingsChanged can fire on a non-UI thread, so marshal
            // ScheduleDebouncedResize() back to the UI thread before touching UI state.
            if (!loginComplete) return;
            if (InterfaceControl == null || InterfaceControl.IsDisposed) return;

            if (InterfaceControl.InvokeRequired)
            {
                InterfaceControl.BeginInvoke(new Action(ScheduleDebouncedResize));
            }
            else
            {
                ScheduleDebouncedResize();
            }
        }

        protected override AxHost CreateActiveXRdpClientControl()
        {
            return new AxMsRdpClient8NotSafeForScripting();
        }

        private void DoResizeClient()
        {
            if (!loginComplete)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize skipped for '{connectionInfo.Hostname}': Login not complete");
                return;
            }

            if (!InterfaceControl.Info.AutomaticResize)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize skipped for '{connectionInfo.Hostname}': AutomaticResize is disabled");
                return;
            }

            // SmartSize scales the image client-side, so the remote session must keep the
            // resolution it connected with. FitToWindow and Fullscreen both mean "the session is
            // as big as the space it is shown in", so they follow the panel.
            if (InterfaceControl.Info.Resolution == RDPResolutions.SmartSize)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Resize skipped for '{connectionInfo.Hostname}': SmartSize scales the image instead of resizing the session");
                return;
            }

            Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                $"Resizing RDP connection to host '{connectionInfo.Hostname}'");

            try
            {
                // Never measure Control here: the RDP ActiveX sizes itself to the resolution of the
                // session, so feeding its size back in pins the session to the first value it ever
                // got. Tracing showed exactly that - the panel grew from 1190x622 to 3193x1974
                // while every call kept requesting 3258x1979.
                Size size = Fullscreen
                    ? Screen.FromControl(Control).Bounds.Size
                    : GetAvailableContentSize();

                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Calling UpdateSessionDisplaySettings({size.Width}, {size.Height}) for '{connectionInfo.Hostname}' (Control.Size={Control.Size}, InterfaceControl.Size={InterfaceControl.Size})");

                UpdateSessionDisplaySettings((uint)size.Width, (uint)size.Height);

                Runtime.MessageCollector.AddMessage(MessageClass.DebugMsg,
                    $"Successfully resized RDP session for '{connectionInfo.Hostname}' to {size.Width}x{size.Height}");
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                    string.Format(Language.ChangeConnectionResolutionError, connectionInfo.Hostname),
                    ex, MessageClass.WarningMsg, false);
            }
        }

        /// <summary>
        /// Space the connection actually has on screen: the panel's client area minus the padding
        /// that draws the connection frame.
        /// </summary>
        /// <remarks>
        /// Deliberately measured on InterfaceControl and never on Control. The RDP ActiveX sizes
        /// itself to the resolution of the session, so using its size as the input for the next
        /// resize pins the session to the first value it ever received.
        /// </remarks>
        private Size GetAvailableContentSize()
        {
            if (InterfaceControl == null || InterfaceControl.IsDisposed) return Size.Empty;

            Padding padding = InterfaceControl.Padding;
            Rectangle client = InterfaceControl.ClientRectangle;
            return new Size(client.Width - padding.Horizontal, client.Height - padding.Vertical);
        }

        private bool DoResizeControl()
        {
            if (Control == null || InterfaceControl == null) return false;

            // Check if controls are being disposed during shutdown
            if (Control.IsDisposed || InterfaceControl.IsDisposed) return false;

            // FitToWindow means the session follows the panel, so the control has to follow it too.
            // Clear the scroll range first: while it is set, ClientRectangle is shrunk by the
            // scrollbars and we would keep chasing a size that never fits.
            // FitToWindow anchors the control to the panel at connect time (see
            // RdpProtocol.SetResolution), so WinForms keeps it in step with the panel on its own.
            // Assigning Size here is pointless - the ActiveX wrapper ignores it.
            if (InterfaceControl.Info.Resolution == RDPResolutions.FitToWindow)
                return false;

            Runtime.MessageCollector?.AddMessage(MessageClass.DebugMsg,
                $"DoResizeControl - Before: Control.Size={Control.Size}, InterfaceControl.Size={InterfaceControl.Size}, Control.Dock={Control.Dock}");

            // If control is docked, we need to temporarily undock it, resize it, then redock it
            // because WinForms ignores Size assignments on docked controls
            bool wasDocked = Control.Dock == DockStyle.Fill;

            if (wasDocked)
            {
                Control.Dock = DockStyle.None;
            }

            Control.Location = InterfaceControl.Location;

            if (Control.Size == InterfaceControl.Size || InterfaceControl.Size == Size.Empty)
            {
                // Restore docking if we changed it
                if (wasDocked)
                {
                    Control.Dock = DockStyle.Fill;
                }

                Runtime.MessageCollector?.AddMessage(MessageClass.DebugMsg,
                    $"DoResizeControl - Skipped: Sizes already match or InterfaceControl.Size is empty");
                return false;
            }

            Control.Size = InterfaceControl.Size;

            // Restore docking
            if (wasDocked)
            {
                Control.Dock = DockStyle.Fill;
            }

            Runtime.MessageCollector?.AddMessage(MessageClass.DebugMsg,
                $"DoResizeControl - After: Control.Size={Control.Size}, Control.Dock={Control.Dock}");

            return true;
        }

        protected virtual void UpdateSessionDisplaySettings(uint width, uint height)
        {
            if (RdpClient8 != null)
            {
                RdpClient8.Reconnect(width, height);
            }
        }

        public override void Close()
        {
            // Unsubscribe from external/static events to prevent memory leaks
            _frmMain.ResizeEnd -= ResizeEnd;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

            // Clean up debounce timer
            if (_resizeDebounceTimer != null)
            {
                _resizeDebounceTimer.Stop();
                _resizeDebounceTimer.Elapsed -= ResizeDebounceTimer_Elapsed;
                _resizeDebounceTimer.Dispose();
                _resizeDebounceTimer = null;
            }

            base.Close();
        }

    }
}
