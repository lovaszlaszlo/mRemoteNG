using System;
using System.Windows.Forms;
using mRemoteNG.Resources.Language;

namespace mRemoteNG.UI.Controls
{
    public class MrngSearchBox : MrngTextBox
    {
        private readonly PictureBox _pbClear = new();
        private readonly ToolTip _btClearToolTip = new();

        /// <summary>
        /// Set while the focus has just arrived, so the first click selects rather than places the
        /// caret.
        /// </summary>
        private bool _selectOnFirstClick;

        public MrngSearchBox()
        {
            // A real placeholder, drawn by the control and never part of its contents. The prompt
            // used to be actual text that was cleared when the box took the focus - so a click
            // put the caret after the word "Search" and it had to be deleted by hand, and every
            // reader of Text had to know that one particular value meant "empty".
            PlaceholderText = Prompt;

            TextChanged += (_, _) => _pbClear.Visible = TextLength > 0;

            // Clicking a box that already has something in it selects all of it, so typing
            // replaces the search and End carries on from the end of it. GotFocus alone is not
            // enough: a click sets the caret after the event, undoing the selection.
            Enter += (_, _) => _selectOnFirstClick = true;
            MouseUp += (_, _) =>
            {
                if (!_selectOnFirstClick) return;

                _selectOnFirstClick = false;
                if (TextLength > 0) SelectAll();
            };
            Leave += (_, _) => _selectOnFirstClick = false;

            AddClearButton();
            ApplyLanguage();
        }

        /// <summary>
        /// What the empty box says: what it is for, and the key that gets here.
        /// </summary>
        /// <remarks>
        /// Ctrl+F on the connection tree jumps into this box and selects what is in it. It worked
        /// all along and was written down nowhere, so the placeholder is where it says so - the
        /// one line a person reads before using the box.
        /// </remarks>
        internal static string Prompt => Language.SearchPrompt + " (Ctrl+F)";

        private void ApplyLanguage()
        {
            PlaceholderText = Prompt;
            _btClearToolTip.SetToolTip(_pbClear, Language.ClearSearchString);
        }

        private void AddClearButton()
        {
            _pbClear.Image = Properties.Resources.Close_16x;
            _pbClear.Width = 20;
            _pbClear.Dock = DockStyle.Right;
            _pbClear.Cursor = Cursors.Default;
            _pbClear.Visible = false;
            _pbClear.Click += PbClear_Click;
            Controls.Add(_pbClear);
        }

        private void PbClear_Click(object sender, EventArgs e)
        {
            Text = string.Empty;
            Focus();
        }
    }
}
