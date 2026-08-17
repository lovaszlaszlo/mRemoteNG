using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using mRemoteNG.App;
using mRemoteNG.Messages;
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
            _sshClient.Connect();

            _shellStream = _sshClient.CreateShellStream("xterm-256color", _columns, _rows, 0, 0, 8192);

            _readCancellation = new CancellationTokenSource();
            _ = Task.Run(() => PumpOutputAsync(_readCancellation.Token));

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
        /// Read from the existing SSH options property rather than a new one, so the connection
        /// file format is untouched: either "-i &lt;path&gt;" as OpenSSH spells it, or a bare path.
        /// When nothing is configured, the usual keys under %USERPROFILE%\.ssh are tried.
        /// </remarks>
        private string ResolvePrivateKeyFile()
        {
            string options = _connectionInfo.SSHOptions?.Trim();

            if (!string.IsNullOrEmpty(options))
            {
                string candidate = options;

                int flag = options.IndexOf("-i", StringComparison.OrdinalIgnoreCase);
                if (flag >= 0)
                    candidate = options[(flag + 2)..].Trim().Trim('"');

                if (File.Exists(candidate)) return candidate;

                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg,
                    $"The private key configured for '{_connectionInfo.Name}' was not found: {candidate}");
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

            // Written for the case where the tab outlives the session; normally it is gone before
            // this can be read.
            WriteStatus("[2m-- the session ended --[0m");

            // Tear the tab down, the way the PuTTY protocol does when its process exits. A tab
            // holding a dead console is nothing but clutter.
            Event_Closed(this);
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(e.TryGetWebMessageAsString());
                JsonElement message = document.RootElement;

                switch (message.GetProperty("type").GetString())
                {
                    case "ready":
                        break;

                    case "input":
                        WriteToShell(message.GetProperty("data").GetString());
                        break;

                    case "resize":
                        _columns = (uint)message.GetProperty("cols").GetInt32();
                        _rows = (uint)message.GetProperty("rows").GetInt32();
                        _shellStream?.ChangeWindowSize(_columns, _rows, 0, 0);
                        break;
                }
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("Malformed message from the SSH terminal page", ex,
                                                             MessageClass.WarningMsg, false);
            }
        }

        private void WriteToShell(string text)
        {
            if (string.IsNullOrEmpty(text) || _shellStream == null) return;

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            _shellStream.Write(bytes, 0, bytes.Length);
            _shellStream.Flush();
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
            try
            {
                _readCancellation?.Cancel();
                _shellStream?.Dispose();

                if (_sshClient != null)
                {
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
                _shellStream = null;
                _sshClient = null;
                base.Close();
            }
        }
    }
}
