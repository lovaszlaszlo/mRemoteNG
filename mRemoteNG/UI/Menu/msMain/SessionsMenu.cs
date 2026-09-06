using System;
using System.Linq;
using System.Windows.Forms;
using mRemoteNG.UI.Window;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;
using mRemoteNG.UI.Forms;

namespace mRemoteNG.UI.Menu
{
    [SupportedOSPlatform("windows")]
    public class SessionsMenu : ToolStripMenuItem
    {
        private ToolStripMenuItem _mMenSessionsNextSession;
        private ToolStripMenuItem _mMenSessionsPreviousSession;
        private ToolStripSeparator _mMenSessionsSep1;
        private readonly ToolStripMenuItem[] _sessionNumberItems = new ToolStripMenuItem[9];

        public SessionsMenu()
        {
            Initialize();
        }

        private void Initialize()
        {
            _mMenSessionsNextSession = new ToolStripMenuItem();
            _mMenSessionsPreviousSession = new ToolStripMenuItem();
            _mMenSessionsSep1 = new ToolStripSeparator();

            // Initialize session number menu items (Ctrl+1 through Ctrl+9)
            for (int i = 0; i < 9; i++)
            {
                _sessionNumberItems[i] = new ToolStripMenuItem();
            }

            // 
            // mMenSessions
            // 
            DropDownItems.Add(_mMenSessionsNextSession);
            DropDownItems.Add(_mMenSessionsPreviousSession);
            DropDownItems.Add(_mMenSessionsSep1);

            for (int i = 0; i < 9; i++)
            {
                DropDownItems.Add(_sessionNumberItems[i]);
            }

            Name = "mMenSessions";
            Size = new System.Drawing.Size(61, 20);
            Text = Language._Sessions;

            // 
            // mMenSessionsNextSession
            // 
            _mMenSessionsNextSession.Name = "mMenSessionsNextSession";
            // Ctrl+PageDown, not Ctrl+Right. Arrow keys are navigation keys: the control that has
            // the focus is offered them first and takes them, so the menu never saw these two -
            // measured against Ctrl+1..9 on the same window, which arrive and work. And in a shell
            // Ctrl+Left and Ctrl+Right move by word, which is not ours to take away. PageUp and
            // PageDown are what browsers and terminals use for the same job.
            _mMenSessionsNextSession.ShortcutKeys = Keys.Control | Keys.PageDown;
            _mMenSessionsNextSession.Size = new System.Drawing.Size(230, 22);
            _mMenSessionsNextSession.Text = Language.NextSession;
            _mMenSessionsNextSession.Click += mMenSessionsNextSession_Click;

            // 
            // mMenSessionsPreviousSession
            // 
            _mMenSessionsPreviousSession.Name = "mMenSessionsPreviousSession";
            _mMenSessionsPreviousSession.ShortcutKeys = Keys.Control | Keys.PageUp;
            _mMenSessionsPreviousSession.Size = new System.Drawing.Size(230, 22);
            _mMenSessionsPreviousSession.Text = Language.PreviousSession;
            _mMenSessionsPreviousSession.Click += mMenSessionsPreviousSession_Click;

            // 
            // mMenSessionsSep1
            // 
            _mMenSessionsSep1.Name = "mMenSessionsSep1";
            _mMenSessionsSep1.Size = new System.Drawing.Size(227, 6);

            // Initialize session number items (Ctrl+1 through Ctrl+9)
            for (int i = 0; i < 9; i++)
            {
                int sessionNumber = i + 1;
                _sessionNumberItems[i].Name = $"mMenSessionsSession{sessionNumber}";
                _sessionNumberItems[i].ShortcutKeys = Keys.Control | (Keys)((int)Keys.D1 + i);
                _sessionNumberItems[i].Size = new System.Drawing.Size(230, 22);
                _sessionNumberItems[i].Text = string.Format(Language.JumpToSession.ToString(), sessionNumber);
                int capturedIndex = i; // Capture the index for the lambda
                _sessionNumberItems[i].Click += (s, e) => JumpToSessionNumber(capturedIndex);
            }

            // Nothing here is ever disabled. A disabled ToolStripMenuItem does not fire its
            // shortcut either, so greying these out took Ctrl+Left, Ctrl+Right and Ctrl+1..9 with
            // them - and they were disabled from startup, and again every time the front document
            // was not a connection window. With no session to move to they simply do nothing,
            // which is what a navigation shortcut should do.
        }

        public void ApplyLanguage()
        {
            Text = Language._Sessions;
            _mMenSessionsNextSession.Text = Language.NextSession;
            _mMenSessionsPreviousSession.Text = Language.PreviousSession;

            for (int i = 0; i < 9; i++)
            {
                _sessionNumberItems[i].Text = string.Format(Language.JumpToSession.ToString(), i + 1);
            }
        }

        private void mMenSessionsNextSession_Click(object sender, EventArgs e) => NextSession();

        private void mMenSessionsPreviousSession_Click(object sender, EventArgs e) => PreviousSession();

        public void NextSession() => GetActiveConnectionWindow()?.NavigateToNextTab();

        public void PreviousSession() => GetActiveConnectionWindow()?.NavigateToPreviousTab();

        private void JumpToSessionNumber(int index) => JumpToSession(index);

        public void JumpToSession(int index) => GetActiveConnectionWindow()?.NavigateToTab(index);

        /// <summary>
        /// Remembered so the shortcuts keep working while something else is in front.
        /// </summary>
        private ConnectionWindow _lastConnectionWindow;

        private ConnectionWindow GetActiveConnectionWindow()
        {
            // The Options page and the port scan are documents in the same dock panel, so while
            // either of them is in front ActiveDocument is not a connection window at all - and
            // asking only that question made every one of these shortcuts a no-op. The last
            // connection window is the one they should still move between.
            if (FrmMain.Default.pnlDock?.ActiveDocument is ConnectionWindow active)
            {
                _lastConnectionWindow = active;
                return active;
            }

            if (_lastConnectionWindow is { IsDisposed: false })
                return _lastConnectionWindow;

            return FrmMain.Default.pnlDock?.Documents.OfType<ConnectionWindow>().FirstOrDefault();
        }
    }
}
