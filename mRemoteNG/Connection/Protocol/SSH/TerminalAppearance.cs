using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text.Json;
using mRemoteNG.App;
using mRemoteNG.Messages;
using mRemoteNG.Properties;

namespace mRemoteNG.Connection.Protocol.SSH
{
    /// <summary>
    /// Font and colours of the native terminal, kept by this program.
    /// </summary>
    /// <remarks>
    /// These used to be read out of a saved PuTTY session named on the connection, which meant the
    /// look of the native terminal was configured in another program, through a field whose name
    /// gave no hint of it. Nothing said so anywhere, so the only way to find out was to read the
    /// source - and installing PuTTY was a prerequisite for choosing a font.
    ///
    /// The settings live here now. A PuTTY session can still be read, but only once and on demand,
    /// as an import: the point is to get the old look across, not to keep depending on it.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class TerminalAppearance
    {
        /// <summary>
        /// Every colour xterm.js takes, in the order worth showing them.
        /// </summary>
        public static readonly string[] ColourNames =
        {
            "foreground", "background", "cursor", "cursorAccent",
            "black", "red", "green", "yellow", "blue", "magenta", "cyan", "white",
            "brightBlack", "brightRed", "brightGreen", "brightYellow",
            "brightBlue", "brightMagenta", "brightCyan", "brightWhite"
        };

        public string FontFamily { get; init; } = "Consolas";

        /// <summary>Size in points, as font dialogs and PuTTY both express it.</summary>
        public int FontSizePoints { get; init; } = 10;

        public bool Bold { get; init; }

        public int Scrollback { get; init; } = 2000;

        public Dictionary<string, string> Theme { get; init; } = new();

        /// <summary>What xterm.js wants: the size in pixels.</summary>
        public int FontSizePixels => (int)Math.Round(FontSizePoints * 96.0 / 72.0);

        /// <summary>
        /// Whether the terminal has been given a look of its own yet.
        /// </summary>
        /// <remarks>
        /// Until it has, the connection's PuTTY session is still read, so upgrading does not change
        /// how anybody's terminals look without them asking for it. Importing a session once, or
        /// touching any setting on the Terminal page, ends that.
        /// </remarks>
        public static bool IsConfigured => !string.IsNullOrWhiteSpace(Settings.Default.TerminalFontFamily);

        public static TerminalAppearance Load()
        {
            return new TerminalAppearance
            {
                FontFamily = string.IsNullOrWhiteSpace(Settings.Default.TerminalFontFamily)
                                 ? "Consolas"
                                 : Settings.Default.TerminalFontFamily,
                FontSizePoints = Settings.Default.TerminalFontSize > 0 ? Settings.Default.TerminalFontSize : 10,
                Bold = Settings.Default.TerminalFontBold,
                Scrollback = Settings.Default.TerminalScrollback > 0 ? Settings.Default.TerminalScrollback : 2000,
                Theme = ReadTheme()
            };
        }

        public void Save()
        {
            Settings.Default.TerminalFontFamily = FontFamily;
            Settings.Default.TerminalFontSize = FontSizePoints;
            Settings.Default.TerminalFontBold = Bold;
            Settings.Default.TerminalScrollback = Scrollback;
            Settings.Default.TerminalTheme = JsonSerializer.Serialize(Theme);
        }

        /// <summary>
        /// The same values, taken from a saved PuTTY session.
        /// </summary>
        public static TerminalAppearance FromPuttySession(string sessionName)
        {
            PuttySessionAppearance putty = PuttySessionAppearance.Load(sessionName);
            if (putty == null) return null;

            return new TerminalAppearance
            {
                FontFamily = string.IsNullOrWhiteSpace(putty.FontFamily) ? "Consolas" : putty.FontFamily,
                FontSizePoints = putty.FontSizePoints,
                Bold = putty.Bold,
                Scrollback = putty.Scrollback,
                Theme = putty.Theme ?? new Dictionary<string, string>()
            };
        }

        private static Dictionary<string, string> ReadTheme()
        {
            string stored = Settings.Default.TerminalTheme;
            if (string.IsNullOrWhiteSpace(stored)) return new Dictionary<string, string>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(stored)
                       ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                // A theme that will not parse is not worth failing a connection over - the terminal
                // simply opens in xterm.js's own colours.
                Runtime.MessageCollector.AddExceptionMessage("The stored terminal theme could not be read.", ex);
                return new Dictionary<string, string>();
            }
        }
    }
}
