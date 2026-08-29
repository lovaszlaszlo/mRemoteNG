using mRemoteNG.App;
using System;
using System.IO;
using System.Threading;
using mRemoteNG.Tools;
using WeifenLuo.WinFormsUI.Docking;
using System.Windows.Forms;
using mRemoteNG.Messages;
using mRemoteNG.UI.Controls;
using mRemoteNG.UI.Forms;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;

namespace mRemoteNG.UI.Window
{
    [SupportedOSPlatform("windows")]
    public class SSHTransferWindow : BaseWindow
    {
        #region Form Init

        private MrngProgressBar pbStatus;
        private MrngButton btnTransfer;
        private MrngButton btnCancel;
        private MrngLabel lblStatus;
        private MrngTextBox txtUser;
        private MrngTextBox txtPassword;
        private MrngTextBox txtHost;
        private MrngTextBox txtPort;
        private MrngLabel lblHost;
        private MrngLabel lblPort;
        private MrngLabel lblUser;
        private MrngLabel lblPassword;
        private MrngLabel lblProtocol;
        private MrngRadioButton radProtSCP;
        private MrngRadioButton radProtSFTP;
        private MrngGroupBox grpConnection;
        private MrngButton btnBrowse;
        private MrngLabel lblRemoteFile;
        private MrngTextBox txtRemoteFile;
        private MrngTextBox txtLocalFile;
        private MrngLabel lblLocalFile;
        private MrngGroupBox grpFiles;

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources =
                new(typeof(SSHTransferWindow));
            grpFiles = new MrngGroupBox();
            lblLocalFile = new MrngLabel();
            txtLocalFile = new MrngTextBox();
            btnTransfer = new MrngButton();
            btnCancel = new MrngButton();
            lblStatus = new MrngLabel();
            txtRemoteFile = new MrngTextBox();
            lblRemoteFile = new MrngLabel();
            btnBrowse = new MrngButton();
            grpConnection = new MrngGroupBox();
            radProtSFTP = new MrngRadioButton();
            radProtSCP = new MrngRadioButton();
            lblProtocol = new MrngLabel();
            lblPassword = new MrngLabel();
            lblUser = new MrngLabel();
            lblPort = new MrngLabel();
            lblHost = new MrngLabel();
            txtPort = new MrngTextBox();
            txtHost = new MrngTextBox();
            txtPassword = new MrngTextBox();
            txtUser = new MrngTextBox();
            pbStatus = new MrngProgressBar();
            grpFiles.SuspendLayout();
            grpConnection.SuspendLayout();
            SuspendLayout();
            // 
            // grpFiles
            // 
            grpFiles.Controls.Add(lblLocalFile);
            grpFiles.Controls.Add(txtLocalFile);
            grpFiles.Controls.Add(btnTransfer);
            grpFiles.Controls.Add(btnCancel);
            grpFiles.Controls.Add(txtRemoteFile);
            grpFiles.Controls.Add(lblRemoteFile);
            grpFiles.Controls.Add(btnBrowse);
            grpFiles.FlatStyle = FlatStyle.Flat;
            grpFiles.Location = new System.Drawing.Point(12, 172);
            grpFiles.Name = "grpFiles";
            grpFiles.Size = new System.Drawing.Size(668, 175);
            grpFiles.TabIndex = 2000;
            grpFiles.TabStop = false;
            grpFiles.Text = "Files";
            // 
            // lblLocalFile
            // 
            lblLocalFile.AutoSize = true;
            lblLocalFile.Location = new System.Drawing.Point(6, 30);
            lblLocalFile.Name = "lblLocalFile";
            lblLocalFile.Size = new System.Drawing.Size(55, 13);
            lblLocalFile.TabIndex = 10;
            lblLocalFile.Text = "Local file:";
            // 
            // txtLocalFile
            // 
            txtLocalFile.BorderStyle = BorderStyle.FixedSingle;
            txtLocalFile.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular,
                                                             System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            txtLocalFile.Location = new System.Drawing.Point(105, 28);
            txtLocalFile.Name = "txtLocalFile";
            txtLocalFile.Size = new System.Drawing.Size(455, 22);
            txtLocalFile.TabIndex = 20;
            // 
            // btnTransfer
            // 
            btnTransfer._mice = MrngButton.MouseState.HOVER;
            btnTransfer.FlatStyle = FlatStyle.Flat;
            btnTransfer.Image = Properties.Resources.SyncArrow_16x;
            btnTransfer.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            btnTransfer.Location = new System.Drawing.Point(562, 145);
            btnTransfer.Name = "btnTransfer";
            btnTransfer.Size = new System.Drawing.Size(100, 24);
            btnTransfer.TabIndex = 10000;
            btnTransfer.Text = "Transfer";
            btnTransfer.UseVisualStyleBackColor = true;
            btnTransfer.Click += new EventHandler(btnTransfer_Click);
            //
            // btnCancel
            //
            btnCancel._mice = MrngButton.MouseState.HOVER;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Location = new System.Drawing.Point(456, 145);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(100, 24);
            btnCancel.TabIndex = 10001;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += new EventHandler(btnCancel_Click);
            // 
            // txtRemoteFile
            // 
            txtRemoteFile.BorderStyle = BorderStyle.FixedSingle;
            txtRemoteFile.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular,
                                                              System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            txtRemoteFile.Location = new System.Drawing.Point(105, 60);
            txtRemoteFile.Name = "txtRemoteFile";
            txtRemoteFile.Size = new System.Drawing.Size(542, 22);
            txtRemoteFile.TabIndex = 50;
            // 
            // lblRemoteFile
            // 
            lblRemoteFile.AutoSize = true;
            lblRemoteFile.Location = new System.Drawing.Point(6, 67);
            lblRemoteFile.Name = "lblRemoteFile";
            lblRemoteFile.Size = new System.Drawing.Size(68, 13);
            lblRemoteFile.TabIndex = 40;
            lblRemoteFile.Text = "Remote file:";
            // 
            // btnBrowse
            // 
            btnBrowse._mice = MrngButton.MouseState.HOVER;
            btnBrowse.FlatStyle = FlatStyle.Flat;
            btnBrowse.Location = new System.Drawing.Point(566, 28);
            btnBrowse.Name = "btnBrowse";
            btnBrowse.Size = new System.Drawing.Size(81, 22);
            btnBrowse.TabIndex = 30;
            btnBrowse.Text = "Browse";
            btnBrowse.UseVisualStyleBackColor = true;
            btnBrowse.Click += new EventHandler(btnBrowse_Click);
            // 
            // grpConnection
            // 
            grpConnection.Controls.Add(radProtSFTP);
            grpConnection.Controls.Add(radProtSCP);
            grpConnection.Controls.Add(lblProtocol);
            grpConnection.Controls.Add(lblPassword);
            grpConnection.Controls.Add(lblUser);
            grpConnection.Controls.Add(lblPort);
            grpConnection.Controls.Add(lblHost);
            grpConnection.Controls.Add(txtPort);
            grpConnection.Controls.Add(txtHost);
            grpConnection.Controls.Add(txtPassword);
            grpConnection.Controls.Add(txtUser);
            grpConnection.FlatStyle = FlatStyle.Flat;
            grpConnection.Location = new System.Drawing.Point(12, 12);
            grpConnection.Name = "grpConnection";
            grpConnection.Size = new System.Drawing.Size(668, 154);
            grpConnection.TabIndex = 1000;
            grpConnection.TabStop = false;
            grpConnection.Text = "Connection";
            // 
            // radProtSFTP
            // 
            radProtSFTP.AutoSize = true;
            radProtSFTP.FlatStyle = FlatStyle.Flat;
            radProtSFTP.Location = new System.Drawing.Point(164, 113);
            radProtSFTP.Name = "radProtSFTP";
            radProtSFTP.Size = new System.Drawing.Size(47, 17);
            radProtSFTP.TabIndex = 90;
            radProtSFTP.Text = "SFTP";
            radProtSFTP.UseVisualStyleBackColor = true;
            // 
            // radProtSCP
            // 
            radProtSCP.AutoSize = true;
            radProtSCP.Checked = true;
            radProtSCP.FlatStyle = FlatStyle.Flat;
            radProtSCP.Location = new System.Drawing.Point(105, 113);
            radProtSCP.Name = "radProtSCP";
            radProtSCP.Size = new System.Drawing.Size(43, 17);
            radProtSCP.TabIndex = 80;
            radProtSCP.TabStop = true;
            radProtSCP.Text = "SCP";
            radProtSCP.UseVisualStyleBackColor = true;
            // 
            // lblProtocol
            // 
            lblProtocol.AutoSize = true;
            lblProtocol.Location = new System.Drawing.Point(6, 117);
            lblProtocol.Name = "lblProtocol";
            lblProtocol.Size = new System.Drawing.Size(53, 13);
            lblProtocol.TabIndex = 90;
            lblProtocol.Text = "Protocol:";
            // 
            // lblPassword
            // 
            lblPassword.AutoSize = true;
            lblPassword.Location = new System.Drawing.Point(6, 88);
            lblPassword.Name = "lblPassword";
            lblPassword.Size = new System.Drawing.Size(59, 13);
            lblPassword.TabIndex = 70;
            lblPassword.Text = "Password:";
            // 
            // lblUser
            // 
            lblUser.AutoSize = true;
            lblUser.Location = new System.Drawing.Point(6, 58);
            lblUser.Name = "lblUser";
            lblUser.Size = new System.Drawing.Size(33, 13);
            lblUser.TabIndex = 50;
            lblUser.Text = "User:";
            // 
            // lblPort
            // 
            lblPort.AutoSize = true;
            lblPort.Location = new System.Drawing.Point(228, 115);
            lblPort.Name = "lblPort";
            lblPort.Size = new System.Drawing.Size(31, 13);
            lblPort.TabIndex = 30;
            lblPort.Text = "Port:";
            // 
            // lblHost
            // 
            lblHost.AutoSize = true;
            lblHost.Location = new System.Drawing.Point(6, 27);
            lblHost.Name = "lblHost";
            lblHost.Size = new System.Drawing.Size(34, 13);
            lblHost.TabIndex = 10;
            lblHost.Text = "Host:";
            // 
            // txtPort
            // 
            txtPort.BorderStyle = BorderStyle.FixedSingle;
            txtPort.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular,
                                                        System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            txtPort.Location = new System.Drawing.Point(271, 110);
            txtPort.Name = "txtPort";
            txtPort.Size = new System.Drawing.Size(30, 22);
            txtPort.TabIndex = 100;
            txtPort.Text = "22";
            txtPort.TextAlign = HorizontalAlignment.Center;
            // 
            // txtHost
            // 
            txtHost.BorderStyle = BorderStyle.FixedSingle;
            txtHost.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular,
                                                        System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            txtHost.Location = new System.Drawing.Point(105, 19);
            txtHost.Name = "txtHost";
            txtHost.Size = new System.Drawing.Size(471, 22);
            txtHost.TabIndex = 20;
            // 
            // txtPassword
            // 
            txtPassword.BorderStyle = BorderStyle.FixedSingle;
            txtPassword.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular,
                                                            System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            txtPassword.Location = new System.Drawing.Point(105, 81);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new System.Drawing.Size(471, 22);
            txtPassword.TabIndex = 60;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // txtUser
            // 
            txtUser.BorderStyle = BorderStyle.FixedSingle;
            txtUser.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular,
                                                        System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            txtUser.Location = new System.Drawing.Point(105, 51);
            txtUser.Name = "txtUser";
            txtUser.Size = new System.Drawing.Size(471, 22);
            txtUser.TabIndex = 40;
            // 
            // pbStatus
            // 
            pbStatus.Location = new System.Drawing.Point(12, 353);
            pbStatus.Name = "pbStatus";
            pbStatus.Size = new System.Drawing.Size(668, 23);
            pbStatus.Style = ProgressBarStyle.Continuous;
            pbStatus.TabIndex = 3000;
            //
            // lblStatus
            //
            lblStatus.AutoSize = false;
            lblStatus.Location = new System.Drawing.Point(12, 380);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new System.Drawing.Size(668, 20);
            lblStatus.TabIndex = 3001;
            lblStatus.Text = "";
            // 
            // SSHTransferWindow
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new System.Drawing.Size(692, 423);
            Controls.Add(grpFiles);
            Controls.Add(grpConnection);
            Controls.Add(pbStatus);
            Controls.Add(lblStatus);
            Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular,
                                                System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            Name = "SSHTransferWindow";
            TabText = "SSH File Transfer";
            Text = "SSH File Transfer";
            Load += new EventHandler(SSHTransfer_Load);
            grpFiles.ResumeLayout(false);
            grpFiles.PerformLayout();
            grpConnection.ResumeLayout(false);
            grpConnection.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        #region Private Properties

        private readonly OpenFileDialog oDlg;

        #endregion

        #region Public Properties

        public string Hostname
        {
            get => txtHost.Text;
            set => txtHost.Text = value;
        }

        public string Port
        {
            get => txtPort.Text;
            set => txtPort.Text = value;
        }

        public string Username
        {
            get => txtUser.Text;
            set => txtUser.Text = value;
        }

        public string Password
        {
            get => txtPassword.Text;
            set => txtPassword.Text = value;
        }

        #endregion

        #region Form Stuff

        private void SSHTransfer_Load(object sender, EventArgs e)
        {
            ApplyTheme();
            ApplyLanguage();
            Icon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.SyncArrow_16x);
            DisplayProperties display = new();
            btnTransfer.Image = display.ScaleImage(btnTransfer.Image);
        }

        private void ApplyLanguage()
        {
            grpFiles.Text = Language.Files;
            lblLocalFile.Text = Language.LocalFile + ":";
            lblRemoteFile.Text = Language.RemoteFile + ":";
            btnBrowse.Text = Language._Browse;
            grpConnection.Text = Language.Connection;
            lblProtocol.Text = Language.Protocol;
            lblPassword.Text = Language.Password;
            lblUser.Text = Language.User + ":";
            lblPort.Text = Language.Port;
            lblHost.Text = Language.Host + ":";
            btnTransfer.Text = Language.Transfer;
            btnCancel.Text = Language._Cancel;
            TabText = Language.Transfer;
            Text = Language.Transfer;
        }

        #endregion

        #region Private Methods

        private SecureTransfer st;

        /// <summary>
        /// Whether a transfer is on the wire, which is what decides between the two things Cancel
        /// can mean here.
        /// </summary>
        private volatile bool _transferring;

        /// <summary>
        /// Cancel stops the transfer if one is running, and otherwise closes the window.
        /// </summary>
        /// <remarks>
        /// Both readings of the word are what someone means by it at the moment they press it:
        /// there is nothing to cancel but the window when nothing is being sent. Closing it was
        /// the harder half to arrange - until the panel tab was fixed there was no close button
        /// anywhere, and this form has never had one of its own.
        /// </remarks>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (_transferring)
            {
                AbortTransfer();
                return;
            }

            Close();
        }

        /// <summary>
        /// Stops a transfer in progress by taking the connection out from under it.
        /// </summary>
        /// <remarks>
        /// There is nothing gentler available: SecureTransfer.Upload has no cancellation of its
        /// own, and for SCP the call does not return until the file has gone. Disconnecting makes
        /// it throw, which the background thread already catches and tidies up after.
        ///
        /// The remote file is left as it stands - a part of it will have arrived, and the far side
        /// is where that has to be dealt with. Saying so is better than implying the transfer was
        /// undone.
        /// </remarks>
        private void AbortTransfer()
        {
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                "SSH file transfer cancelled; the partly written remote file is left as it is.",
                true);

            try
            {
                st?.Disconnect();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                    "Could not close the SSH file transfer connection cleanly", ex,
                    MessageClass.WarningMsg, false);
            }

            _transferring = false;
            ReportStatus(Language.TransferStatusCancelled);
            EnableButtons();
        }

        private void StartTransfer(SecureTransfer.SSHTransferProtocol Protocol)
        {
            if (AllFieldsSet() == false)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.ErrorMsg, Language.PleaseFillAllFields);
                ReportStatus(Language.PleaseFillAllFields);
                return;
            }

            // Cleared here rather than at the end of the last run, so the previous result stays
            // readable until a new transfer is actually asked for.
            maxVal = 1;
            curVal = 0;
            SetStatus();
            ReportStatus(Language.TransferStatusConnecting);

            if (File.Exists(txtLocalFile.Text) == false)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, Language.LocalFileDoesNotExist);
                return;
            }

            try
            {
                st = new SecureTransfer(txtHost.Text, txtUser.Text, txtPassword.Text, int.Parse(txtPort.Text), Protocol,
                                        txtLocalFile.Text, txtRemoteFile.Text);

                // Connect creates the protocol objects and makes the initial connection.
                st.Connect();

                switch (Protocol)
                {
                    case SecureTransfer.SSHTransferProtocol.SCP:
                        st.ScpClt.Uploading += ScpClt_Uploading;
                        break;
                    case SecureTransfer.SSHTransferProtocol.SFTP:
                        st.asyncCallback = AsyncCallback;
                        break;
                }

                _transferring = true;

                Thread t = new(StartTransferBG);
                t.SetApartmentState(ApartmentState.STA);
                t.IsBackground = true;
                t.Start();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace(Language.SshTransferFailed, ex);
                ReportStatus(string.Format(Language.TransferStatusFailed, ex.Message));
                _transferring = false;
                st?.Disconnect();
                st?.Dispose();
                EnableButtons();
            }
        }

        private void AsyncCallback(IAsyncResult ar)
        {
            Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg, $"SFTP AsyncCallback completed.", true);
        }

        private void ScpClt_Uploading(object sender, Renci.SshNet.Common.ScpUploadEventArgs e)
        {
            // If the file size is over 2 gigs, convert to kb. This means we'll support a 2TB file.
            int max = e.Size > int.MaxValue ? Convert.ToInt32(e.Size / 1024) : Convert.ToInt32(e.Size);

            // yes, compare to size since that's the total/original file size
            int cur = e.Size > int.MaxValue ? Convert.ToInt32(e.Uploaded / 1024) : Convert.ToInt32(e.Uploaded);

            SshTransfer_Progress(cur, max);
        }

        private void StartTransferBG()
        {
            try
            {
                DisableButtons();
                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                                                    $"Transfer of {Path.GetFileName(st.SrcFile)} started.", true);
                st.Upload();

                // SftpClient is Asynchronous, so we need to wait here after the upload and handle the status directly since no status events are raised.
                if (st.Protocol == SecureTransfer.SSHTransferProtocol.SFTP)
                {
                    FileInfo fi = new(st.SrcFile);
                    while (!st.asyncResult.IsCompleted)
                    {
                        int max = fi.Length > int.MaxValue
                            ? Convert.ToInt32(fi.Length / 1024)
                            : Convert.ToInt32(fi.Length);

                        int cur = fi.Length > int.MaxValue
                            ? Convert.ToInt32(st.asyncResult.UploadedBytes / 1024)
                            : Convert.ToInt32(st.asyncResult.UploadedBytes);
                        SshTransfer_Progress(cur, max);
                        Thread.Sleep(50);
                    }
                }

                Runtime.MessageCollector.AddMessage(MessageClass.InformationMsg,
                                                    $"Transfer of {Path.GetFileName(st.SrcFile)} completed.", true);
                ReportStatus(string.Format(Language.TransferStatusCompleted,
                                           Path.GetFileName(st.SrcFile), st.DstFile));
                st.Disconnect();
                st.Dispose();
                _transferring = false;
                EnableButtons();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionStackTrace(Language.SshBackgroundTransferFailed, ex,
                                                                MessageClass.ErrorMsg, false);

                // Only shown when the user did not cause it: after Cancel the connection is pulled
                // on purpose, and the exception that follows is the mechanism, not news.
                if (_transferring)
                    ReportStatus(string.Format(Language.TransferStatusFailed, ex.Message));
                st?.Disconnect();
                st?.Dispose();

                // The buttons were left disabled here, so a transfer that failed - or one just
                // cancelled - locked the form until it was closed and opened again.
                _transferring = false;
                EnableButtons();
            }
        }

        private bool AllFieldsSet()
        {
            if (txtHost.Text != "" && txtPort.Text != "" && txtUser.Text != "" && txtLocalFile.Text != "" &&
                txtRemoteFile.Text != "")
            {
                if (txtPassword.Text == "")
                {
                    if (MessageBox.Show(FrmMain.Default, Language.EmptyPasswordContinue, @"Question?",
                                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                    {
                        return false;
                    }
                }

                if (txtRemoteFile.Text.EndsWith("/") || txtRemoteFile.Text.EndsWith("\\"))
                {
                    txtRemoteFile.Text +=
                        txtLocalFile.Text.Substring(txtLocalFile.Text.LastIndexOf("\\", StringComparison.Ordinal) + 1);
                }

                return true;
            }
            else
            {
                return false;
            }
        }


        private int maxVal;
        private int curVal;

        private delegate void SetStatusCB();

        private delegate void ReportStatusCB(string text);

        /// <summary>
        /// Says in words what the transfer is doing.
        /// </summary>
        /// <remarks>
        /// The window used to report a transfer with nothing but a progress bar, which on a small
        /// file fills and empties faster than the eye catches. A file went across twice in under a
        /// second each and the window looked exactly as it had before pressing the button - the
        /// only place anything was said was the log. A transfer either happened or it did not, and
        /// the window that was asked to do it is where that belongs.
        /// </remarks>
        private void ReportStatus(string text)
        {
            if (lblStatus.InvokeRequired)
            {
                ReportStatusCB d = ReportStatus;
                lblStatus.Invoke(d, text);
                return;
            }

            lblStatus.Text = text;
        }

        private void SetStatus()
        {
            if (pbStatus.InvokeRequired)
            {
                SetStatusCB d = SetStatus;
                pbStatus.Invoke(d);
            }
            else
            {
                pbStatus.Maximum = maxVal;
                pbStatus.Value = curVal;
            }
        }

        private delegate void EnableButtonsCB();

        private void EnableButtons()
        {
            if (btnTransfer.InvokeRequired)
            {
                EnableButtonsCB d = EnableButtons;
                btnTransfer.Invoke(d);
            }
            else
            {
                btnTransfer.Enabled = true;
            }
        }

        private delegate void DisableButtonsCB();

        private void DisableButtons()
        {
            if (btnTransfer.InvokeRequired)
            {
                DisableButtonsCB d = DisableButtons;
                btnTransfer.Invoke(d);
            }
            else
            {
                btnTransfer.Enabled = false;
            }
        }

        private void SshTransfer_Progress(int transferredBytes, int totalBytes)
        {
            maxVal = totalBytes;
            curVal = transferredBytes;

            SetStatus();
            ReportStatus(string.Format(Language.TransferStatusTransferring,
                                       totalBytes > 0 ? transferredBytes * 100L / totalBytes : 0));
        }

        #endregion

        #region Public Methods

        public SSHTransferWindow()
        {
            WindowType = WindowType.SSHTransfer;
            DockPnl = new DockContent();
            InitializeComponent();

            oDlg = new OpenFileDialog
            {
                Filter = @"All Files (*.*)|*.*",
                CheckFileExists = true
            };
        }

        #endregion

        #region Form Stuff

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            if (oDlg.ShowDialog() != DialogResult.OK) return;
            if (oDlg.FileName != "")
            {
                txtLocalFile.Text = oDlg.FileName;
            }
        }

        private void btnTransfer_Click(object sender, EventArgs e)
        {
            if (radProtSCP.Checked)
            {
                StartTransfer(SecureTransfer.SSHTransferProtocol.SCP);
            }
            else if (radProtSFTP.Checked)
            {
                StartTransfer(SecureTransfer.SSHTransferProtocol.SFTP);
            }
        }

        #endregion
    }
}