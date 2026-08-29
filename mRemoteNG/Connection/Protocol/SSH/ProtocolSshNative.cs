using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
using mRemoteNG.App;
using mRemoteNG.Messages;
using mRemoteNG.Resources.Language;
using mRemoteNG.Tools;
using Renci.SshNet;

namespace mRemoteNG.Connection.Protocol.SSH
{
    /// <summary>
    /// SSH that runs inside mRemoteNG instead of embedding a PuTTY window: xterm.js rendered in a
    /// WebView2 control, driven by an SSH.NET shell stream.
    /// </summary>
    /// <remarks>
    /// The point of this protocol is that its control is an ordinary WinForms control, the way the
    /// RDP client is. The PuTTY window is a window of another process that keeps its own top level
    /// window style, which is what makes Alt+Tab, tab switching and clicking the console behave
    /// oddly - none of that can happen here.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public class ProtocolSshNative : ProtocolBase
    {
        private const string VirtualHost = "mremoteng.terminal";
        private const int DefaultColumns = 80;
        private const int DefaultRows = 24;

        private readonly ConnectionInfo _connectionInfo;
        private Panel _host;
        private WebView2 _webView;
        private SshClient _sshClient;
        private ShellStream _shellStream;
        private CancellationTokenSource _readCancellation;
        private uint _columns = DefaultColumns;
        private uint _rows = DefaultRows;

        /// <summary>
        /// 1 once the loss of the link has been acted on, so that the read loop finishing, the
        /// keepalive failing and the machine waking up do not each act on the same death.
        /// </summary>
        private int _linkLost;

        /// <summary>
        /// Asks the client whether it is still connected, because it will not always say so
        /// unprompted.
        /// </summary>
        /// <remarks>
        /// On 2026-08-28 a session sat in a tab all day after the network dropped: the client knew
        /// perfectly well it was disconnected - every keystroke came back "Client not connected" -
        /// but no ErrorOccurred ever reached us and the read loop stayed blocked in ReadAsync, so
        /// nothing was ever told. Neither of the other two paths covers this: the resume check
        /// needs a standby that never happened, and the keepalive only helps if SSH.NET raises the
        /// failure rather than swallowing it.
        /// </remarks>
        private System.Threading.Timer _livenessTimer;

        private static readonly TimeSpan LivenessInterval = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The tunnels asked for in the connection's SSH options, held so they can be stopped with
        /// the session that carries them.
        /// </summary>
        private readonly List<ForwardedPort> _forwardedPorts = new();

        public ProtocolSshNative(ConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo;
        }

        public override bool Initialize()
        {
            try
            {
                _webView = new WebView2 { Dock = DockStyle.Fill };

                // The WebView2 sits in a plain panel rather than being the connection control
                // itself, so that ShowFallbackMessage has somewhere to put its label when the
                // page never comes up - which is exactly when there is something to say.
                _host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
                _host.Controls.Add(_webView);
                Control = _host;

                tmrReconnect.Elapsed += OnReconnectTimerElapsed;

                // Hibernating breaks the TCP connection under the session, and nothing on the wire
                // says so: the next read simply never returns. Waking up is the moment to go and
                // look, rather than waiting for a keepalive to time out.
                SystemEvents.PowerModeChanged += OnPowerModeChanged;

                return base.Initialize();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace("Could not create the SSH terminal control", ex);
                return false;
            }
        }

        public override bool Connect()
        {
            _ = StartAsync();
            return true;
        }

        private async Task StartAsync()
        {
            try
            {
                await InitializeWebViewAsync();
                await Task.Run(ConnectSsh);
            }
            catch (Exception ex)
            {
                // Deliberately leaves the tab open, unlike a session that ended normally: the
                // reason it could not connect is worth reading, and it is written into the
                // terminal itself.
                Runtime.MessageCollector.AddExceptionMessage($"SSH connection to '{_connectionInfo.Hostname}' failed", ex);
                WriteStatus($"[31m{ex.Message}[0m");

                // WriteStatus needs a loaded page to write into. When the failure was the page
                // itself - most often a machine with no WebView2 runtime - it reaches nothing and
                // the tab just sits there empty, which is what this fallback is for.
                ShowFallbackMessage(ex.Message);

                Event_Disconnected(this, ex.Message, null);
            }
        }

        private async Task InitializeWebViewAsync()
        {
            // Keep the browser profile out of the install directory, which is not writable.
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "mRemoteNG", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            // Asked before CreateAsync only so the failure can say what to do about it. The
            // runtime ships with Windows 11 and with Edge, so it is present on a developer
            // machine and easy to forget - but it is a separate install, and a machine without it
            // gets a control that renders nothing at all.
            try
            {
                CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch (WebView2RuntimeNotFoundException ex)
            {
                throw new InvalidOperationException(
                    "The WebView2 runtime is not installed on this machine, and the SSH (native) " +
                    "terminal is drawn by it. Install the 'Evergreen Standalone Installer' from " +
                    "https://developer.microsoft.com/microsoft-edge/webview2/ - or use the SSH " +
                    "(PuTTY) protocol, which does not need it.", ex);
            }

            CoreWebView2Environment environment =
                await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await _webView.EnsureCoreWebView2Async(environment);

            CoreWebView2 core = _webView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            // Serve the terminal page from disk under a fixed host name, so xterm.js is loaded
            // locally and no request ever leaves the machine.
            string terminalFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Terminal");
            core.SetVirtualHostNameToFolderMapping(VirtualHost, terminalFolder,
                                                   CoreWebView2HostResourceAccessKind.Allow);

            core.WebMessageReceived += OnWebMessageReceived;

            TaskCompletionSource<bool> navigated = new();
            void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                core.NavigationCompleted -= OnNavigationCompleted;
                navigated.TrySetResult(e.IsSuccess);
            }

            core.NavigationCompleted += OnNavigationCompleted;
            core.Navigate($"https://{VirtualHost}/terminal.html");
            await navigated.Task;

            ApplyPuttySessionAppearance();
        }

        /// <summary>
        /// Takes the font and colours from the PuTTY session the connection names, so it looks the
        /// same whether it is opened with this protocol or the PuTTY based one.
        /// </summary>
        private void ApplyPuttySessionAppearance()
        {
            PuttySessionAppearance appearance = PuttySessionAppearance.Load(_connectionInfo.PuttySession);
            if (appearance == null) return;

            PostToPage(new
            {
                type = "appearance",
                fontFamily = appearance.FontFamily,
                fontSize = appearance.FontSize,
                bold = appearance.Bold,
                scrollback = appearance.Scrollback,
                theme = appearance.Theme
            });
        }

        private void ConnectSsh()
        {
            Renci.SshNet.ConnectionInfo sshConnectionInfo = BuildSshConnectionInfo();

            _sshClient = new SshClient(sshConnectionInfo);
            _sshClient.HostKeyReceived += OnHostKeyReceived;

            // Without this nothing is ever written to an idle session, so a link that has died
            // quietly - a hibernated laptop, a dropped VPN - is not noticed until someone types
            // into it. The keepalive turns that into an ErrorOccurred within the interval.
            _sshClient.KeepAliveInterval = TimeSpan.FromSeconds(30);
            _sshClient.ErrorOccurred += OnSshErrorOccurred;

            _sshClient.Connect();

            StartForwardedPorts();

            _shellStream = _sshClient.CreateShellStream("xterm-256color", _columns, _rows, 0, 0, 8192);

            _readCancellation = new CancellationTokenSource();
            _ = Task.Run(() => PumpOutputAsync(_readCancellation.Token));

            // Armed only now: a session that is up is one that can be lost again.
            Interlocked.Exchange(ref _linkLost, 0);

            _livenessTimer = new System.Threading.Timer(_ => CheckLiveness(), null,
                                                        LivenessInterval, LivenessInterval);

            Event_Connected(this);

            // The terminal is only worth typing into now, and the tab was built while the page was
            // still loading, so nothing has given it the keyboard yet.
            Focus();
        }

        private Renci.SshNet.ConnectionInfo BuildSshConnectionInfo()
        {
            string host = _connectionInfo.Hostname;
            int port = _connectionInfo.Port > 0 ? _connectionInfo.Port : 22;
            string username = string.IsNullOrEmpty(_connectionInfo.Username)
                ? Environment.UserName
                : _connectionInfo.Username;
            string password = _connectionInfo.Password ?? string.Empty;

            List<AuthenticationMethod> methods = new();

            // A key wins over the password when one is configured, but keep the password as a
            // fallback: servers commonly accept either, and the key may be the wrong one.
            string keyFile = ResolvePrivateKeyFile();
            if (keyFile != null)
            {
                // The connection password doubles as the passphrase - an encrypted key needs one,
                // and there is nowhere else to put it without changing the connection file format.
                PrivateKeyFile key = string.IsNullOrEmpty(password)
                    ? new PrivateKeyFile(keyFile)
                    : new PrivateKeyFile(keyFile, password);

                methods.Add(new PrivateKeyAuthenticationMethod(username, key));
            }

            if (!string.IsNullOrEmpty(password))
                methods.Add(new PasswordAuthenticationMethod(username, password));

            // Servers that ask for the password over keyboard-interactive rather than the password
            // method - common with PAM - would otherwise refuse a perfectly good password.
            if (!string.IsNullOrEmpty(password))
            {
                KeyboardInteractiveAuthenticationMethod interactive = new(username);
                interactive.AuthenticationPrompt += (_, e) =>
                {
                    foreach (Renci.SshNet.Common.AuthenticationPrompt prompt in e.Prompts)
                        prompt.Response = password;
                };
                methods.Add(interactive);
            }

            if (methods.Count == 0)
                throw new InvalidOperationException(
                    "No password and no private key are configured for this connection.");

            return new Renci.SshNet.ConnectionInfo(host, port, username, methods.ToArray());
        }

        /// <summary>
        /// Private key to authenticate with, or null when there is none.
        /// </summary>
        /// <remarks>
        /// Read from the existing SSH options property rather than a property of its own, so the
        /// connection file format is untouched: either "-i &lt;path&gt;" as OpenSSH spells it, or a
        /// bare path. When nothing is configured, the usual keys under %USERPROFILE%\.ssh are tried.
        /// </remarks>
        private string ResolvePrivateKeyFile()
        {
            string configured = Options.KeyFile;

            if (!string.IsNullOrEmpty(configured))
            {
                if (File.Exists(configured)) return configured;

                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                    $"The private key configured for '{_connectionInfo.Name}' was not found: {configured}");
            }

            string sshFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");

            foreach (string name in new[] { "id_ed25519", "id_ecdsa", "id_rsa" })
            {
                string path = Path.Combine(sshFolder, name);
                if (File.Exists(path)) return path;
            }

            return null;
        }

        /// <summary>
        /// The connection's SSH options field, parsed. Re-read on every connect so a reconnect
        /// picks up whatever the field says now.
        /// </summary>
        private SshCommandLineOptions Options =>
            SshCommandLineOptions.Parse(_connectionInfo.SSHOptions);

        /// <summary>
        /// Opens the tunnels asked for in the SSH options, and says plainly what it could not.
        /// </summary>
        /// <remarks>
        /// PuTTY gets this field as a real command line, so anything typed there works for a
        /// PuTTY based connection. Here it is interpreted, and only -i, -L, -R and -D are
        /// understood - so anything else is written into the terminal rather than dropped. A
        /// tunnel that silently never opened is the kind of thing that costs an afternoon.
        ///
        /// A forward that will not start does not fail the session: the shell behind it is still
        /// worth having, and the reason is on screen.
        /// </remarks>
        private void StartForwardedPorts()
        {
            SshCommandLineOptions options = Options;

            foreach (SshCommandLineOptions.PortForward forward in options.Forwards)
            {
                try
                {
                    ForwardedPort port = forward.Kind switch
                    {
                        SshCommandLineOptions.ForwardKind.Local =>
                            new ForwardedPortLocal(forward.BoundHost, forward.BoundPort, forward.Host, forward.Port),
                        SshCommandLineOptions.ForwardKind.Remote =>
                            new ForwardedPortRemote(forward.BoundHost, forward.BoundPort, forward.Host, forward.Port),
                        _ => new ForwardedPortDynamic(forward.BoundHost, forward.BoundPort)
                    };

                    // Raised while the tunnel is running, long after this method returned - a
                    // refused connection through it, for instance. Worth the log, not the session.
                    port.Exception += (_, e) =>
                        Runtime.MessageCollector.AddExceptionMessage(
                            $"SSH tunnel {forward} reported a problem", e.Exception,
                            MessageClass.WarningMsg, false);

                    _sshClient.AddForwardedPort(port);
                    port.Start();
                    _forwardedPorts.Add(port);

                    Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                        $"SSH tunnel {forward} is open for '{_connectionInfo.Name}'", true);
                }
                catch (Exception ex)
                {
                    Runtime.MessageCollector.AddExceptionMessage($"Could not open the SSH tunnel {forward}", ex,
                                                                 MessageClass.WarningMsg, false);
                    WriteStatus($"[31m-- {forward} could not be opened: {ex.Message} --[0m");
                }
            }

            if (options.Unrecognised.Count == 0) return;

            string ignored = string.Join(" ", options.Unrecognised);

            Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                $"Ignored in the SSH options of '{_connectionInfo.Name}': {ignored}. " +
                "The native SSH protocol understands -i, -L, -R and -D; the SSH (PuTTY) protocol " +
                "passes the whole field to PuTTY.", true);

            WriteStatus($"[33m-- ignored in the SSH options: {ignored} " +
                        "(this protocol understands -i, -L, -R and -D) --[0m");
        }

        /// <summary>
        /// Closes the tunnels, on a deadline, and says what happened either way.
        /// </summary>
        /// <remarks>
        /// On 2026-08-29 a forward outlived its session: the local port stayed bound until
        /// mRemoteNG itself exited, and the log said nothing at all - the teardown either never
        /// ran or sat inside it, and there was no way to tell which. Hence the messages.
        ///
        /// Stopping a forward whose transport has already gone can wait on a channel that will
        /// never answer, and this runs on the path that closes the tab, so it gets a deadline
        /// rather than the chance to wedge it.
        ///
        /// The list is taken and emptied first, so that whatever becomes of a port, no later
        /// caller down the close path tries it a second time.
        /// </remarks>
        private void StopForwardedPorts()
        {
            if (_forwardedPorts.Count == 0) return;

            List<ForwardedPort> ports = new(_forwardedPorts);
            _forwardedPorts.Clear();

            foreach (ForwardedPort port in ports)
            {
                // Dispose rather than Stop: it stops the port itself, and it is the call that
                // actually lets go of the listening socket.
                Task closing = Task.Run(() => port.Dispose());

                try
                {
                    if (closing.Wait(TimeSpan.FromSeconds(3)))
                    {
                        Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                            $"SSH tunnel closed for '{_connectionInfo.Name}'", true);
                        continue;
                    }

                    // Left to run rather than waited on any longer: the socket goes with the
                    // process at worst, and a hung teardown must not take the tab with it.
                    Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                        $"An SSH tunnel of '{_connectionInfo.Name}' did not close within three " +
                        "seconds; its local port stays bound until mRemoteNG exits.", true);
                }
                catch (Exception ex)
                {
                    Runtime.MessageCollector.AddExceptionMessage(
                        $"Could not close an SSH tunnel of '{_connectionInfo.Name}'", ex,
                        MessageClass.WarningMsg, false);
                }
            }
        }

        private void OnHostKeyReceived(object sender, Renci.SshNet.Common.HostKeyEventArgs e)
        {
            string host = _connectionInfo.Hostname;
            int port = _connectionInfo.Port > 0 ? _connectionInfo.Port : 22;
            string fingerprint = e.FingerPrintSHA256;

            SshKnownHosts.Verdict verdict = SshKnownHosts.Check(host, port, fingerprint, out string stored);

            if (verdict == SshKnownHosts.Verdict.Known)
            {
                e.CanTrust = true;
                return;
            }

            // The read happens on a background thread, so ask on the UI thread and wait for it.
            bool accepted = AskAboutHostKey(verdict, host, port, e.HostKeyName, fingerprint, stored);

            e.CanTrust = accepted;
            if (accepted) SshKnownHosts.Remember(host, port, fingerprint);
        }

        private bool AskAboutHostKey(SshKnownHosts.Verdict verdict, string host, int port,
                                     string keyType, string fingerprint, string storedFingerprint)
        {
            string caption = verdict == SshKnownHosts.Verdict.Changed
                ? "SSH host key CHANGED"
                : "Unknown SSH host key";

            string text = verdict == SshKnownHosts.Verdict.Changed
                ? $"The host key of {host}:{port} is not the one accepted earlier.\r\n\r\n" +
                  $"Key type: {keyType}\r\n" +
                  $"Now:      SHA256:{fingerprint}\r\n" +
                  $"Earlier:  SHA256:{storedFingerprint}\r\n\r\n" +
                  "This happens after a server is rebuilt - but it is also what an intercepted " +
                  "connection looks like. Only continue if you know why the key changed.\r\n\r\n" +
                  "Trust this key from now on?"
                : $"{host}:{port} has not been connected to before.\r\n\r\n" +
                  $"Key type: {keyType}\r\n" +
                  $"SHA256:{fingerprint}\r\n\r\n" +
                  "Trust this key from now on?";

            MessageBoxIcon icon = verdict == SshKnownHosts.Verdict.Changed
                ? MessageBoxIcon.Warning
                : MessageBoxIcon.Question;

            Control uiThread = _webView;
            if (uiThread == null || uiThread.IsDisposed) return false;

            if (uiThread.InvokeRequired)
                return (bool)uiThread.Invoke(new Func<bool>(() => Ask(text, caption, icon)));

            return Ask(text, caption, icon);
        }

        private static bool Ask(string text, string caption, MessageBoxIcon icon) =>
            MessageBox.Show(text, caption, MessageBoxButtons.YesNo, icon, MessageBoxDefaultButton.Button2)
            == DialogResult.Yes;

        private async Task PumpOutputAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[8192];

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    int read = await _shellStream.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read <= 0) break;

                    string payload = Convert.ToBase64String(buffer, 0, read);
                    PostToPage(new { type = "output", data = payload });
                }
            }
            catch (OperationCanceledException)
            {
                // closing down
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("SSH terminal read loop stopped", ex,
                                                             MessageClass.WarningMsg, false);
            }

            if (cancellationToken.IsCancellationRequested) return;

            // Logging out is not a link that failed, and it must not be offered a reconnect: the
            // transport is still up underneath, it is the shell on top of it that finished. The
            // two look identical from here except for exactly that.
            bool loggedOut = _sshClient?.IsConnected == true;

            HandleLinkLost(loggedOut ? "the session ended" : "the connection was lost",
                           mayReconnect: !loggedOut);
        }

        /// <summary>
        /// Acts once on a session that is no longer there, whether it was logged out of, timed out
        /// on a keepalive, or found dead after the machine woke up.
        /// </summary>
        /// <remarks>
        /// An SSH session cannot survive its TCP connection the way an RDP session survives one -
        /// the shell on the far side is gone with everything that was running in it, so a
        /// reconnect here means a new shell, not the old one continued. PuTTY behaves the same
        /// way; only a multiplexer on the server (tmux, screen) actually keeps the work.
        /// </remarks>
        private void HandleLinkLost(string reason, bool mayReconnect = true)
        {
            // The read loop ending, the keepalive failing and the check after a resume can all be
            // describing the same death, and they race.
            if (Interlocked.Exchange(ref _linkLost, 1) == 1) return;

            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                $"The SSH session to '{_connectionInfo.Hostname}' is gone: {reason}", true);

            // Done here rather than left to the close path. A protocol that raises Event_Closed
            // itself is not reliably asked to Close afterwards, and a tunnel that outlives its
            // session keeps a local port bound with nothing behind it - which is exactly what
            // happened on 2026-08-29.
            StopForwardedPorts();

            if (!mayReconnect || !Properties.OptionsAdvancedPage.Default.ReconnectOnDisconnect)
            {
                // Written for the case where the tab outlives the session; normally it is gone
                // before this can be read. Tearing the tab down is what the PuTTY protocol does
                // when its process exits - a tab holding a dead console is nothing but clutter.
                WriteStatus($"[2m-- {reason} --[0m");
                Event_Closed(this);
                return;
            }

            WriteStatus($"[33m-- {reason}; waiting for {_connectionInfo.Hostname} to answer again --[0m");
            Event_Disconnected(this, reason, null);
            ShowReconnectGroup();
        }

        /// <summary>
        /// Puts the same "waiting for the server" panel over the terminal that a dropped RDP
        /// session gets, and starts polling the port behind it.
        /// </summary>
        private void ShowReconnectGroup()
        {
            if (_host == null || _host.IsDisposed) return;

            if (_host.InvokeRequired)
            {
                _host.BeginInvoke(new Action(ShowReconnectGroup));
                return;
            }

            ReconnectGroup = new ReconnectGroup();
            ReconnectGroup.CloseClicked += Event_ReconnectGroupCloseClicked;
            ReconnectGroup.Left = _host.Width / 2 - ReconnectGroup.Width / 2;
            ReconnectGroup.Top = _host.Height / 2 - ReconnectGroup.Height / 2;
            ReconnectGroup.Parent = _host;
            ReconnectGroup.Show();
            ReconnectGroup.BringToFront();

            tmrReconnect.Enabled = true;
        }

        private void OnReconnectTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (ReconnectGroup == null) return;

                int port = _connectionInfo.Port > 0 ? _connectionInfo.Port : 22;
                bool serverReady = PortScanner.IsPortOpen(_connectionInfo.Hostname, port.ToString());

                ReconnectGroup.ServerReady = serverReady;

                if (!ReconnectGroup.ReconnectWhenReady || !serverReady) return;

                tmrReconnect.Enabled = false;
                Reconnect();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                    string.Format(Language.AutomaticReconnectError, _connectionInfo.Hostname),
                    ex, MessageClass.WarningMsg, false);
            }
        }

        /// <summary>
        /// Drops what is left of the dead session and opens a new one into the same terminal, so
        /// the scrollback of the old one is still there to read.
        /// </summary>
        private void Reconnect()
        {
            try
            {
                DisposeSshSession();

                WriteStatus("[2m-- reconnecting --[0m");
                ConnectSsh();

                DisposeReconnectGroup();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                    string.Format(Language.AutomaticReconnectError, _connectionInfo.Hostname),
                    ex, MessageClass.WarningMsg, false);

                WriteStatus($"[31m{ex.Message}[0m");

                // Back to waiting rather than giving up: the port answered but the session did not
                // come up, and a server that is still finishing its boot is the ordinary reason.
                Interlocked.Exchange(ref _linkLost, 1);
                tmrReconnect.Enabled = true;
            }
        }

        private void DisposeReconnectGroup()
        {
            // DisposeReconnectGroup marshals itself onto the UI thread, so the timer thread can
            // call this directly.
            ReconnectGroup?.DisposeReconnectGroup();
            ReconnectGroup = null;
        }

        private void CheckLiveness()
        {
            try
            {
                SshClient client = _sshClient;
                if (client == null || client.IsConnected) return;

                HandleLinkLost("the connection was lost");
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Could not check whether the SSH session is still up", ex,
                                                             MessageClass.WarningMsg, false);
            }
        }

        private void OnSshErrorOccurred(object sender, Renci.SshNet.Common.ExceptionEventArgs e)
        {
            // Only the link dying is interesting here; anything the session itself runs into is
            // already reported by the read loop.
            if (_sshClient == null || _sshClient.IsConnected) return;

            HandleLinkLost(e.Exception?.Message ?? "the connection was lost");
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume) return;

            // Off the caller's thread: this arrives on a system thread that Windows wants back
            // promptly, and IsConnected can sit on a socket for a moment.
            Task.Run(() =>
            {
                try
                {
                    if (_sshClient == null || _sshClient.IsConnected) return;

                    HandleLinkLost("the connection did not survive standby");
                }
                catch (Exception ex)
                {
                    Runtime.MessageCollector.AddExceptionMessage(
                        "Could not check the SSH session after the machine woke up", ex,
                        MessageClass.WarningMsg, false);
                }
            });
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            JsonDocument document;

            // Only the parsing is a "malformed message". What the message then asks for can fail
            // for its own reasons, and saying that the page sent nonsense when the session simply
            // died sends the next person looking in the wrong place entirely.
            try
            {
                document = JsonDocument.Parse(e.TryGetWebMessageAsString());
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Malformed message from the SSH terminal page", ex,
                                                             MessageClass.WarningMsg, false);
                return;
            }

            using (document)
            {
                JsonElement message = document.RootElement;

                switch (message.GetProperty("type").GetString())
                {
                    case "ready":
                        break;

                    case "active":
                        DismissMenusElsewhere();
                        break;

                    case "input":
                        DismissMenusElsewhere();
                        WriteToShell(message.GetProperty("data").GetString());
                        break;

                    case "paste":
                        WriteToShell(ReadClipboard());
                        break;

                    case "copy":
                        WriteClipboard(message.GetProperty("data").GetString());
                        break;

                    case "resize":
                        _columns = (uint)message.GetProperty("cols").GetInt32();
                        _rows = (uint)message.GetProperty("rows").GetInt32();
                        TellShellTheSize();
                        break;
                }
            }
        }

        /// <summary>
        /// Tells the connection tree that its context menu is in the way.
        /// </summary>
        /// <remarks>
        /// Right-clicking the tree and then typing in here left the menu sitting in front of the
        /// terminal: this page's input goes through the browser process, so the menu never sees
        /// the click or the keystroke that would normally close it.
        /// </remarks>
        private static void DismissMenusElsewhere()
        {
            try
            {
                AppWindows.TreeFormIfBuilt?.ConnectionTree?.CloseContextMenu();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Could not close the connection tree's menu", ex,
                                                             MessageClass.WarningMsg, false);
            }
        }

        /// <summary>
        /// The clipboard for the terminal's two mouse habits: a right click pastes, and a
        /// selection is copied as soon as it is made - the way PuTTY has always behaved.
        /// </summary>
        /// <remarks>
        /// Done here rather than in the page because the page would have to ask the browser for
        /// clipboard permission, and a terminal is a poor place to be asked. Here it is a plain
        /// WinForms call on the UI thread that WebMessageReceived already runs on, which is also
        /// what Clipboard needs, being STA only.
        ///
        /// Both directions swallow their failures. The clipboard is shared, another process can
        /// hold it open for a moment, and neither a lost copy nor an empty paste is worth putting
        /// an error in front of someone mid-session.
        /// </remarks>
        private static string ReadClipboard()
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Could not read the clipboard", ex,
                                                             MessageClass.WarningMsg, false);
                return string.Empty;
            }
        }

        private static void WriteClipboard(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                // The retrying overload rather than SetText: selecting text is a thing people do
                // constantly, and a clipboard another process happens to hold for a moment should
                // cost a short wait, not the copy.
                Clipboard.SetDataObject(text, true, 5, 100);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Could not put the selection on the clipboard", ex,
                                                             MessageClass.WarningMsg, false);
            }
        }

        private void WriteToShell(string text)
        {
            if (string.IsNullOrEmpty(text) || _shellStream == null) return;

            byte[] bytes = Encoding.UTF8.GetBytes(text);

            try
            {
                _shellStream.Write(bytes, 0, bytes.Length);
                _shellStream.Flush();
            }
            catch (Exception ex)
            {
                // Typing into a session whose link is gone is the plainest evidence there is that
                // it is gone, and it arrives at the one moment the user is certainly watching.
                HandleLinkLost(ex.Message);
            }
        }

        /// <summary>
        /// Passes the new window size to the far side, and shrugs if it cannot.
        /// </summary>
        /// <remarks>
        /// Not a reason to declare the session dead: this fires while panels are being dragged
        /// about, and the liveness check is the thing that decides that question.
        /// </remarks>
        private void TellShellTheSize()
        {
            try
            {
                _shellStream?.ChangeWindowSize(_columns, _rows, 0, 0);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Could not tell the SSH session its new size", ex,
                                                             MessageClass.WarningMsg, false);
            }
        }

        /// <summary>
        /// Puts <paramref name="text"/> on the tab as a plain label, for the case where the
        /// terminal page is not there to write it into.
        /// </summary>
        /// <remarks>
        /// Does nothing once the page is up: there the message belongs in the terminal, in the
        /// scrollback with everything else. This only covers the tab that would otherwise be
        /// blank.
        /// </remarks>
        private void ShowFallbackMessage(string text)
        {
            if (_host == null || _host.IsDisposed) return;

            if (_host.InvokeRequired)
            {
                _host.BeginInvoke(new Action(() => ShowFallbackMessage(text)));
                return;
            }

            if (_webView?.CoreWebView2 != null) return;

            if (_webView != null)
                _webView.Visible = false;

            Label message = new()
            {
                Dock = DockStyle.Fill,
                Text = text,
                ForeColor = Color.White,
                BackColor = Color.Black,
                Padding = new Padding(12),
                AutoSize = false
            };

            _host.Controls.Add(message);
            message.BringToFront();
        }

        private void PostToPage(object message)
        {
            if (_webView == null || _webView.IsDisposed) return;

            string json = JsonSerializer.Serialize(message);

            if (_webView.InvokeRequired)
                _webView.BeginInvoke(new Action(() => PostToPageCore(json)));
            else
                PostToPageCore(json);
        }

        private void PostToPageCore(string json)
        {
            try
            {
                _webView.CoreWebView2?.PostWebMessageAsString(json);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Could not deliver output to the SSH terminal page", ex,
                                                             MessageClass.WarningMsg, false);
            }
        }

        /// <summary>
        /// Writes a line into the terminal itself, which is where the user is already looking when
        /// something goes wrong.
        /// </summary>
        /// <remarks>
        /// Not gated on the page having reported itself ready: the caller has already waited for
        /// navigation to complete, and a connection that fails immediately would otherwise lose
        /// its error message to that race.
        /// </remarks>
        private void WriteStatus(string text) => PostToPage(new { type = "status", text });

        /// <summary>
        /// Focusing the control is not enough on its own: the keyboard belongs to a hidden textarea
        /// inside the page, so the page has to be told to take it as well.
        /// </summary>
        public override void Focus()
        {
            try
            {
                if (_webView == null || _webView.IsDisposed) return;

                if (_webView.InvokeRequired)
                {
                    _webView.BeginInvoke(new Action(Focus));
                    return;
                }

                _webView.Focus();
                PostToPage(new { type = "focus" });
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace("Could not focus the SSH terminal", ex);
            }
        }

        public override void Disconnect()
        {
            Close();
        }

        public override void Close()
        {
            // Logged because its absence was the whole difficulty on 2026-08-29: a tunnel was
            // still bound after its tab had gone, and nothing said whether this had even run.
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                $"Closing the SSH session of '{_connectionInfo.Name}'", true);

            // Before the session goes: the resume check must not find the client half torn down
            // and call this a lost link, and the poll must not reconnect a tab that is closing.
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            tmrReconnect.Enabled = false;
            tmrReconnect.Elapsed -= OnReconnectTimerElapsed;
            Interlocked.Exchange(ref _linkLost, 1);

            // Closing the tab from the reconnect panel's own Close button lands here too.
            DisposeReconnectGroup();

            DisposeSshSession();
            base.Close();
        }

        /// <summary>
        /// Ends the SSH session and lets go of it, leaving the terminal page alone - a reconnect
        /// opens a new session into the same page, so this cannot take the page with it.
        /// </summary>
        private void DisposeSshSession()
        {
            try
            {
                _livenessTimer?.Dispose();
                StopForwardedPorts();
                _readCancellation?.Cancel();
                _shellStream?.Dispose();

                if (_sshClient != null)
                {
                    _sshClient.ErrorOccurred -= OnSshErrorOccurred;
                    if (_sshClient.IsConnected) _sshClient.Disconnect();
                    _sshClient.Dispose();
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Could not close the SSH session cleanly", ex,
                                                             MessageClass.WarningMsg, false);
            }
            finally
            {
                _livenessTimer = null;
                _readCancellation = null;
                _shellStream = null;
                _sshClient = null;
            }
        }
    }
}
