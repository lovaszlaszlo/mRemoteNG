using System.Windows.Forms;
using mRemoteNG.Themes;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;

namespace mRemoteNG.UI.Forms
{
    [SupportedOSPlatform("windows")]
    public sealed partial class FrmInputBox : Form
    {
        internal string returnValue;

        public FrmInputBox(string title, string promptText, string value)
        {
            InitializeComponent();

            Text = title;
            label.Text = promptText;
            textBox.Text = value;
            ApplyLanguage();
            ApplyTheme();

            // None of this was set. The dialog opened with the caret nowhere, Enter did nothing
            // because there was no accept button, and Escape did nothing because there was no
            // cancel button - so renaming a tab group meant clicking into the field and then
            // clicking OK, and anyone who typed and pressed Enter watched nothing happen.
            AcceptButton = _Ok;
            CancelButton = buttonCancel;
            ActiveControl = textBox;
        }

        protected override void OnShown(System.EventArgs e)
        {
            base.OnShown(e);

            // Selected as well as focused: a rename starts by replacing what is there.
            textBox.Focus();
            textBox.SelectAll();
        }

        private void ApplyLanguage()
        {
            _Ok.Text = Language._Ok;
            buttonCancel.Text = Language._Cancel;
        }

        // Apply the dark/light title bar before the window is shown to avoid a white flash.
        protected override void OnHandleCreated(System.EventArgs e)
        {
            base.OnHandleCreated(e);
            ThemeManager.getInstance().ApplyThemeToTitleBar(this);
        }

        private void ApplyTheme()
        {
            ThemeManager _themeManager = ThemeManager.getInstance();
            _themeManager.ApplyThemeToTitleBar(this);
            if (!_themeManager.ActiveAndExtended) return;
            BackColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
            ForeColor = _themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");
        }

        private void _Ok_Click(object sender, System.EventArgs e)
        {
            DialogResult = DialogResult.OK;
            returnValue = textBox.Text;
            Close();
        }

        private void buttonCancel_Click(object sender, System.EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}