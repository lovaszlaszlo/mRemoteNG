using System;
using System.Runtime.Versioning;
using mRemoteNG.App.Info;

namespace mRemoteNG.UI.Forms
{
    [SupportedOSPlatform("windows")]
    /// <summary>
    /// Interaction logic for FrmSplashScreenNew.xaml
    /// </summary>
    public partial class FrmSplashScreenNew
    {
        static FrmSplashScreenNew instance = null;
        public FrmSplashScreenNew()
        {
            InitializeComponent();
            LoadFont();
            lblLogoPartD.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
            lblLogoPartD.Content = $@"v. {GeneralAppInfo.ApplicationVersion} - 'DBSystem Kft.'";
        }
        public static FrmSplashScreenNew GetInstance()
        {
            //instance == null
            instance ??= new FrmSplashScreenNew();
            return instance;
        }

        /// <summary>
        /// Shows this window as the About box.
        /// </summary>
        /// <remarks>
        /// The About box used to be a docked tab among the connections - a page that had to be
        /// closed like a session, in the space where the work is. It says the same three things
        /// this window already says at startup, so it is this window now, with a way out: Escape,
        /// the close button, or clicking anywhere on it.
        ///
        /// A new instance every time: a WPF window that has been closed cannot be shown again, and
        /// the startup one is closed by then.
        /// </remarks>
        public static void ShowAbout(System.Windows.Forms.IWin32Window owner)
        {
            FrmSplashScreenNew about = new()
            {
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false,
                Topmost = true
            };

            about.btnClose.Visibility = System.Windows.Visibility.Visible;
            about.KeyDown += about.CloseOnEscape;
            about.MouseLeftButtonDown += (_, _) => about.Close();

            // Focusable, or the key never arrives: the splash is normally a picture nobody types
            // into.
            about.Focusable = true;
            about.Loaded += (_, _) => about.Focus();

            about.ShowDialog();
        }

        private void CloseOnEscape(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key is System.Windows.Input.Key.Escape or System.Windows.Input.Key.Enter)
                Close();
        }

        private void btnClose_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }

        void LoadFont()
        {
            lblLogoPartA.FontFamily = new System.Windows.Media.FontFamily(new Uri("pack://application:,,,/"), "./UI/Font/#HandelGotDBol");
            lblLogoPartB.FontFamily = new System.Windows.Media.FontFamily(new Uri("pack://application:,,,/"), "./UI/Font/#HandelGotDBol");
        }
    }
}
