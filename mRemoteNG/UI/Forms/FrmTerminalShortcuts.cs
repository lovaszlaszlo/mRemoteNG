using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using mRemoteNG.Resources.Language;
using mRemoteNG.Themes;

namespace mRemoteNG.UI.Forms
{
    /// <summary>
    /// Lists what the native SSH terminal answers to.
    /// </summary>
    /// <remarks>
    /// Every one of these was already there and none of it was written down anywhere - the zoom
    /// keys, the session switching, the clipboard. They were found by reading the source, which is
    /// not a way to learn a program. The title says which terminal they belong to, because they are
    /// not the PuTTY based protocols' keys and never were.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class FrmTerminalShortcuts : Form
    {
        private static readonly (string Keys, Func<string> Description)[] Shortcuts =
        {
            ("Ctrl + +", () => Language.TerminalShortcutZoomIn),
            ("Ctrl + -", () => Language.TerminalShortcutZoomOut),
            ("Ctrl + 0", () => Language.TerminalShortcutZoomReset),
            ("Ctrl + " + "⇅", () => Language.TerminalShortcutZoomWheel),
            ("Ctrl + PageDown", () => Language.TerminalShortcutNextSession),
            ("Ctrl + PageUp", () => Language.TerminalShortcutPreviousSession),
            ("Ctrl + 1 ... 9", () => Language.TerminalShortcutJumpSession),
            ("Ctrl + Insert", () => Language.TerminalShortcutCopy),
            ("Shift + Insert", () => Language.TerminalShortcutPaste),
            (null, () => Language.TerminalShortcutSelect),
            (null, () => Language.TerminalShortcutRightClick)
        };

        public FrmTerminalShortcuts()
        {
            BuildLayout();
            ApplyTheme();
        }

        private void BuildLayout()
        {
            Text = Language.TerminalShortcuts;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            TableLayoutPanel layout = new()
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(14)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Label heading = new()
            {
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(3, 0, 3, 12),
                MaximumSize = new Size(420, 0),
                Text = Language.TerminalShortcutsHeading
            };
            layout.Controls.Add(heading, 0, 0);
            layout.SetColumnSpan(heading, 2);

            int row = 1;
            foreach ((string keys, Func<string> description) in Shortcuts)
            {
                Label left = new()
                {
                    AutoSize = true,
                    Font = new Font("Consolas", Font.Size + 0.5f),
                    Margin = new Padding(3, 3, 24, 3),
                    // The mouse-only rows have no key; their description carries the whole story.
                    Text = keys ?? string.Empty
                };

                Label right = new()
                {
                    AutoSize = true,
                    Margin = new Padding(3, 4, 3, 3),
                    Text = description()
                };

                layout.Controls.Add(left, 0, row);
                layout.Controls.Add(right, 1, row);
                row++;
            }

            Button close = new()
            {
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                DialogResult = DialogResult.Cancel,
                Margin = new Padding(3, 16, 3, 3),
                Text = Language._Close
            };

            layout.Controls.Add(close, 1, row);
            Controls.Add(layout);

            AcceptButton = close;
            CancelButton = close;
        }

        private void ApplyTheme()
        {
            ThemeManager themeManager = ThemeManager.getInstance();
            themeManager.ApplyThemeToTitleBar(this);

            if (!themeManager.ActiveAndExtended) return;

            BackColor = themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
            ForeColor = themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");
        }
    }
}
