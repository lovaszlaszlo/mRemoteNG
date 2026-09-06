using System.Runtime.Versioning;
using System.Windows.Forms;
using mRemoteNG.App.Info;
using mRemoteNG.UI.TaskDialog;

namespace mRemoteNG.Tools
{
    /// <summary>
    /// Yes/no questions that answer to the keyboard.
    /// </summary>
    /// <remarks>
    /// A plain MessageBox with Yes and No ignores Escape. Windows maps Escape to a Cancel button,
    /// and there isn't one - so the only ways out were the mouse and Alt+F4, on questions that are
    /// asked in the middle of typing. Nor do its buttons carry mnemonics in every language.
    ///
    /// The program has its own task dialog, and its Yes/No layout sets No as the cancel button and
    /// gives both buttons a mnemonic, so Escape, the close box and the keyboard all work. Asking
    /// through here means that holds for every yes/no question in the program rather than for the
    /// few that happened to be built on it.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static class Confirm
    {
        /// <summary>
        /// Asks a yes/no question. Escape, the close box and No all mean no.
        /// </summary>
        /// <param name="owner">Window the question belongs to.</param>
        /// <param name="question">What is being asked.</param>
        /// <param name="caption">Title bar text; the product name when left out.</param>
        /// <param name="defaultYes">
        /// Whether Yes starts focused. Left alone, No does - the safe answer for a question about
        /// deleting, overwriting, or opening a dozen sessions at once.
        /// </param>
        /// <param name="warning">Shows the warning icon rather than the question mark.</param>
        public static bool Ask(IWin32Window owner,
                               string question,
                               string caption = null,
                               bool defaultYes = false,
                               bool warning = false)
        {
            DialogResult answer = CTaskDialog.ShowTaskDialogBox(
                owner,
                string.IsNullOrEmpty(caption) ? GeneralAppInfo.ProductName : caption,
                question,
                "", "", "", "", "", "",
                ETaskDialogButtons.YesNo,
                warning ? ESysIcons.Warning : ESysIcons.Question,
                ESysIcons.Information,
                defaultYes ? 0 : 1);

            return answer == DialogResult.Yes;
        }
    }
}
