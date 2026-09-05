using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;
using mRemoteNG.Config.Putty;
using mRemoteNG.Connection.Protocol.SSH;
using mRemoteNG.Properties;
using mRemoteNG.Resources.Language;
using mRemoteNG.Themes;
using mRemoteNG.UI.Controls;

namespace mRemoteNG.UI.Forms.OptionsPages
{
    /// <summary>
    /// Font and colours of the native SSH terminal.
    /// </summary>
    /// <remarks>
    /// These had no page of their own: the terminal read them from a saved PuTTY session named on
    /// each connection, so choosing a font meant installing PuTTY, and nothing in the program said
    /// so. The PuTTY session can still be read - once, by the button here - which is the difference
    /// between importing a setting and depending on another program for it.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class TerminalPage : OptionsPage
    {
        private ComboBox _fontFamily;
        private NumericUpDown _fontSize;
        private MrngCheckBox _bold;
        private NumericUpDown _scrollback;
        private Label _sample;
        private Label _state;

        private readonly Dictionary<string, Button> _swatches = new();
        private readonly Dictionary<string, string> _theme = new();

        public TerminalPage()
        {
            PageIcon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.Console_16x);
            BuildLayout();
        }

        public override string PageName
        {
            get => Language.Terminal;
            set { }
        }

        #region Layout

        private void BuildLayout()
        {
            Dock = DockStyle.Fill;

            TableLayoutPanel layout = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(6)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 4; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _state = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(3, 3, 3, 10),
                MaximumSize = new Size(560, 0)
            };

            layout.Controls.Add(_state, 0, 0);
            layout.Controls.Add(BuildFontRow(), 0, 1);
            layout.Controls.Add(BuildColours(), 0, 2);
            layout.Controls.Add(BuildSample(), 0, 3);

            Controls.Add(layout);
        }

        private Control BuildFontRow()
        {
            TableLayoutPanel grid = new()
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                ColumnCount = 5,
                RowCount = 2
            };
            for (int i = 0; i < 4; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _fontFamily = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 240,
                Margin = new Padding(3, 3, 16, 3)
            };
            _fontFamily.Items.AddRange(MonospacedFonts().Cast<object>().ToArray());
            _fontFamily.SelectedIndexChanged += (_, _) => Changed();

            _fontSize = new NumericUpDown
            {
                Minimum = 6,
                Maximum = 72,
                Width = 60,
                Margin = new Padding(3, 3, 16, 3)
            };
            _fontSize.ValueChanged += (_, _) => Changed();

            _bold = new MrngCheckBox { AutoSize = true, Margin = new Padding(3, 6, 3, 3) };
            _bold.CheckedChanged += (_, _) => Changed();

            _scrollback = new NumericUpDown
            {
                Minimum = 100,
                Maximum = 1000000,
                Increment = 500,
                Width = 90,
                Margin = new Padding(3, 3, 16, 3)
            };
            _scrollback.ValueChanged += (_, _) => Changed();

            Button import = new()
            {
                AutoSize = true,
                Margin = new Padding(3, 2, 3, 3),
                Text = Language.TerminalImportFromPutty
            };
            import.Click += Import_Click;

            grid.Controls.Add(Caption(Language.TerminalFont), 0, 0);
            grid.Controls.Add(Caption(Language.TerminalFontSize), 1, 0);
            grid.Controls.Add(Caption(string.Empty), 2, 0);
            grid.Controls.Add(Caption(Language.TerminalScrollback), 3, 0);

            grid.Controls.Add(_fontFamily, 0, 1);
            grid.Controls.Add(_fontSize, 1, 1);
            grid.Controls.Add(_bold, 2, 1);
            grid.Controls.Add(_scrollback, 3, 1);
            grid.Controls.Add(import, 4, 1);

            return grid;
        }

        private Control BuildColours()
        {
            TableLayoutPanel grid = new()
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                ColumnCount = 4,
                Margin = new Padding(0, 14, 0, 0)
            };
            for (int i = 0; i < 4; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            int column = 0;
            int row = 0;

            foreach (string name in TerminalAppearance.ColourNames)
            {
                Button swatch = new()
                {
                    Width = 26,
                    Height = 20,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(3, 2, 6, 2),
                    Tag = name
                };
                swatch.FlatAppearance.BorderSize = 1;
                swatch.Click += Swatch_Click;

                Label caption = new()
                {
                    AutoSize = true,
                    Margin = new Padding(0, 5, 18, 2),
                    Text = ColourCaption(name)
                };

                FlowLayoutPanel cell = new()
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Margin = new Padding(0),
                    WrapContents = false
                };
                cell.Controls.Add(swatch);
                cell.Controls.Add(caption);

                grid.Controls.Add(cell, column, row);
                _swatches[name] = swatch;

                if (++column < 4) continue;
                column = 0;
                row++;
            }

            return grid;
        }

        private Control BuildSample()
        {
            _sample = new Label
            {
                AutoSize = false,
                Height = 76,
                Dock = DockStyle.Top,
                Margin = new Padding(3, 14, 3, 3),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "user@host:~$ ls -la"
            };

            return _sample;
        }

        private static Label Caption(string text) => new()
        {
            AutoSize = true,
            Margin = new Padding(3, 3, 3, 1),
            Text = text
        };

        /// <summary>
        /// The colour's own name is the honest caption: these are the slots xterm.js paints with,
        /// and inventing friendlier names for sixteen ANSI colours would only hide which is which.
        /// </summary>
        private static string ColourCaption(string name) => name switch
        {
            "foreground" => Language.TerminalColourForeground,
            "background" => Language.TerminalColourBackground,
            "cursor" => Language.TerminalColourCursor,
            "cursorAccent" => Language.TerminalColourCursorText,
            _ => name
        };

        #endregion

        #region Contents

        private static IEnumerable<string> MonospacedFonts()
        {
            // Every installed family, with the fixed-pitch ones first: a proportional font in a
            // terminal is unusable, but forbidding it outright would be worse than ordering it
            // last - somebody will have a font this test misjudges.
            using InstalledFontCollection installed = new();

            List<string> monospaced = new();
            List<string> rest = new();

            foreach (FontFamily family in installed.Families)
            {
                try
                {
                    using Font probe = new(family, 10);
                    bool fixedPitch = TextRenderer.MeasureText("i", probe).Width ==
                                      TextRenderer.MeasureText("W", probe).Width;

                    (fixedPitch ? monospaced : rest).Add(family.Name);
                }
                catch
                {
                    rest.Add(family.Name);
                }
            }

            monospaced.Sort(StringComparer.CurrentCultureIgnoreCase);
            rest.Sort(StringComparer.CurrentCultureIgnoreCase);

            return monospaced.Concat(rest);
        }

        private void Swatch_Click(object sender, EventArgs e)
        {
            if (sender is not Button swatch || swatch.Tag is not string name) return;

            using ColorDialog picker = new()
            {
                Color = swatch.BackColor,
                FullOpen = true,
                AnyColor = true
            };

            if (picker.ShowDialog(this) != DialogResult.OK) return;

            _theme[name] = $"#{picker.Color.R:x2}{picker.Color.G:x2}{picker.Color.B:x2}";
            swatch.BackColor = picker.Color;

            Changed();
            UpdateSample();
        }

        private void Import_Click(object sender, EventArgs e)
        {
            string[] sessions = PuttySessionsManager.SessionList.Names
                                                    .Where(name => !string.IsNullOrWhiteSpace(name))
                                                    .ToArray();

            if (sessions.Length == 0)
            {
                MessageBox.Show(this, Language.TerminalNoPuttySessions, Language.Terminal,
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using PuttySessionPicker picker = new(sessions);
            if (picker.ShowDialog(this) != DialogResult.OK) return;

            TerminalAppearance imported = TerminalAppearance.FromPuttySession(picker.SelectedSession);
            if (imported == null)
            {
                MessageBox.Show(this, Language.TerminalPuttySessionUnreadable, Language.Terminal,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Show(imported);
            Changed();
        }

        private void Changed()
        {
            HasChanges = true;
            UpdateSample();
            UpdateState();
        }

        private void UpdateState()
        {
            _state.Text = HasChanges || TerminalAppearance.IsConfigured
                              ? Language.TerminalConfigured
                              : Language.TerminalNotConfigured;
        }

        private void UpdateSample()
        {
            if (_sample == null) return;

            try
            {
                _sample.Font = new Font(_fontFamily.Text, (float)_fontSize.Value,
                                        _bold.Checked ? FontStyle.Bold : FontStyle.Regular);
            }
            catch
            {
                // A family that cannot be realised at this size is not worth a crash in a preview.
            }

            _sample.BackColor = Parse(_theme, "background", Color.Black);
            _sample.ForeColor = Parse(_theme, "foreground", Color.Gainsboro);
        }

        private static Color Parse(IReadOnlyDictionary<string, string> theme, string name, Color fallback)
        {
            if (!theme.TryGetValue(name, out string value) || string.IsNullOrWhiteSpace(value))
                return fallback;

            try
            {
                return ColorTranslator.FromHtml(value);
            }
            catch
            {
                return fallback;
            }
        }

        private void Show(TerminalAppearance appearance)
        {
            _fontFamily.SelectedItem = appearance.FontFamily;
            if (_fontFamily.SelectedItem == null && _fontFamily.Items.Count > 0)
            {
                _fontFamily.Items.Insert(0, appearance.FontFamily);
                _fontFamily.SelectedIndex = 0;
            }

            _fontSize.Value = Math.Clamp(appearance.FontSizePoints, _fontSize.Minimum, _fontSize.Maximum);
            _bold.Checked = appearance.Bold;
            _scrollback.Value = Math.Clamp(appearance.Scrollback, _scrollback.Minimum, _scrollback.Maximum);

            _theme.Clear();
            foreach (KeyValuePair<string, string> colour in appearance.Theme)
                _theme[colour.Key] = colour.Value;

            foreach (KeyValuePair<string, Button> swatch in _swatches)
                swatch.Value.BackColor = Parse(_theme, swatch.Key, SystemColors.Control);

            UpdateSample();
        }

        #endregion

        #region OptionsPage

        public override void ApplyLanguage()
        {
            _bold.Text = Language.TerminalBold;
            UpdateState();
        }

        public override void LoadSettings()
        {
            Show(TerminalAppearance.Load());
            HasChanges = false;
            UpdateState();
        }

        public override void SaveSettings()
        {
            new TerminalAppearance
            {
                FontFamily = _fontFamily.Text,
                FontSizePoints = (int)_fontSize.Value,
                Bold = _bold.Checked,
                Scrollback = (int)_scrollback.Value,
                Theme = new Dictionary<string, string>(_theme)
            }.Save();
        }

        public override void RevertSettings()
        {
            LoadSettings();
        }

        #endregion

        /// <summary>
        /// Asks which saved PuTTY session to take the look from.
        /// </summary>
        private sealed class PuttySessionPicker : Form
        {
            private readonly ListBox _sessions;

            public PuttySessionPicker(IEnumerable<string> sessions)
            {
                Text = Language.TerminalImportFromPutty;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MinimizeBox = false;
                MaximizeBox = false;
                ShowInTaskbar = false;
                ClientSize = new Size(320, 260);

                _sessions = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
                _sessions.Items.AddRange(sessions.Cast<object>().ToArray());
                if (_sessions.Items.Count > 0) _sessions.SelectedIndex = 0;
                _sessions.DoubleClick += (_, _) => Accept();

                FlowLayoutPanel buttons = new()
                {
                    Dock = DockStyle.Bottom,
                    FlowDirection = FlowDirection.RightToLeft,
                    AutoSize = true,
                    Padding = new Padding(6)
                };

                Button cancel = new() { AutoSize = true, DialogResult = DialogResult.Cancel, Text = Language._Cancel };
                Button ok = new() { AutoSize = true, Text = Language._Ok };
                ok.Click += (_, _) => Accept();

                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);

                Controls.Add(_sessions);
                Controls.Add(buttons);

                AcceptButton = ok;
                CancelButton = cancel;
            }

            public string SelectedSession => _sessions.SelectedItem as string;

            private void Accept()
            {
                if (SelectedSession == null) return;

                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
