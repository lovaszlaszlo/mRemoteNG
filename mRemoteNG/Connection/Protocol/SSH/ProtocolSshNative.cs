using System;
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
        private WebView2 _webView;
        private SshClient _sshClient;
        private ShellStream _shellStream;
        private CancellationTokenSource _readCancellation;
        private uint _columns = DefaultColumns;
        private uint _rows = DefaultRows;
        private bool _pageReady;

        public ProtocolSshNative(ConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo;
        }

        public override bool Initialize()
        {
            try
            {
                _webView = new WebView2 { Dock = DockStyle.Fill };
                Control = _webView;
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
                Runtime.MessageCollector.AddExceptionMessage($"SSH connection to '{_connectionInfo.Hostname}' failed", ex);
                WriteStatus($"[31m{ex.Message}[0m");
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
        }

        private void ConnectSsh()
        {
            Renci.SshNet.ConnectionInfo sshConnectionInfo = BuildSshConnectionInfo();

            _sshClient = new SshClient(sshConnectionInfo);
            _sshClient.Connect();

            _shellStream = _sshClient.CreateShellStream("xterm-256color", _columns, _rows, 0, 0, 8192);

            _readCancellation = new CancellationTokenSource();
            _ = Task.Run(() => PumpOutputAsync(_readCancellation.Token));

            Event_Connected(this);
        }

        private Renci.SshNet.ConnectionInfo BuildSshConnectionInfo()
        {
            string host = _connectionInfo.Hostname;
            int port = _connectionInfo.Port > 0 ? _connectionInfo.Port : 22;
            string username = string.IsNullOrEmpty(_connectionInfo.Username)
                ? Environment.UserName
                : _connectionInfo.Username;

            // Prototype: password authentication only. Key files, agent forwarding and the
            // external credential providers are still to be wired up.
            return new Renci.SshNet.ConnectionInfo(host, port, username,
                new PasswordAuthenticationMethod(username, _connectionInfo.Password ?? string.Empty));
        }

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

            if (!cancellationToken.IsCancellationRequested)
                Event_Disconnected(this, "The SSH session ended", null);
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
                        _pageReady = true;
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

        private void WriteStatus(string text)
        {
            if (_pageReady) PostToPage(new { type = "status", text });
        }

        public override void Focus()
        {
            try
            {
                _webView?.Focus();
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
