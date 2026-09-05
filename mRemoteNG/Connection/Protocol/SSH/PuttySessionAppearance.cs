using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using mRemoteNG.App;
using mRemoteNG.Messages;

namespace mRemoteNG.Connection.Protocol.SSH
{
    /// <summary>
    /// Font and colours of a saved PuTTY session, in the shape xterm.js wants them.
    /// </summary>
    /// <remarks>
    /// The native terminal reads the same PuTTY session as the PuTTY based protocols so a
    /// connection looks the same whichever one opens it. Appearance is the only thing the saved
    /// sessions here are used for, and reading them beats asking the user to configure it twice.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public class PuttySessionAppearance
    {
        private const string SessionsKey = @"Software\SimonTatham\PuTTY\Sessions";

        // PuTTY stores its palette as Colour0..Colour21 in a fixed order.
        private const int DefaultForeground = 0;
        private const int DefaultBackground = 2;
        private const int CursorText = 4;
        private const int CursorColour = 5;
        private const int FirstAnsiColour = 6;

        /// <summary>Names of Colour6..Colour21 as xterm.js knows them, in PuTTY's order.</summary>
        private static readonly string[] AnsiNames =
        {
            "black", "brightBlack", "red", "brightRed", "green", "brightGreen",
            "yellow", "brightYellow", "blue", "brightBlue", "magenta", "brightMagenta",
            "cyan", "brightCyan", "white", "brightWhite"
        };

        public string FontFamily { get; private init; }
        public int FontSize { get; private init; }

        /// <summary>The size as PuTTY stores it, before the conversion to pixels.</summary>
        public int FontSizePoints { get; private init; }
        public bool Bold { get; private init; }
        public int Scrollback { get; private init; }
        public Dictionary<string, string> Theme { get; private init; }

        /// <summary>
        /// Reads a session, or returns null when there is no such session to read.
        /// </summary>
        public static PuttySessionAppearance Load(string sessionName)
        {
            if (string.IsNullOrWhiteSpace(sessionName)) return null;

            try
            {
                // PuTTY escapes its session names for the registry; %20 for a space is the one
                // that actually turns up.
                string escaped = sessionName.Replace(" ", "%20");

                using RegistryKey key = Registry.CurrentUser.OpenSubKey($@"{SessionsKey}\{escaped}")
                                        ?? Registry.CurrentUser.OpenSubKey($@"{SessionsKey}\{sessionName}");
                if (key == null) return null;

                Dictionary<string, string> theme = new();
                AddColour(theme, key, DefaultForeground, "foreground");
                AddColour(theme, key, DefaultBackground, "background");
                AddColour(theme, key, CursorColour, "cursor");
                AddColour(theme, key, CursorText, "cursorAccent");

                for (int i = 0; i < AnsiNames.Length; i++)
                    AddColour(theme, key, FirstAnsiColour + i, AnsiNames[i]);

                return new PuttySessionAppearance
                {
                    FontFamily = key.GetValue("Font") as string,
                    // PuTTY keeps the font height in points, xterm.js wants CSS pixels.
                    FontSize = PointsToPixels(key.GetValue("FontHeight")),
                    FontSizePoints = Points(key.GetValue("FontHeight")),
                    Bold = Convert.ToInt32(key.GetValue("FontIsBold", 0), CultureInfo.InvariantCulture) != 0,
                    Scrollback = Convert.ToInt32(key.GetValue("ScrollbackLines", 2000), CultureInfo.InvariantCulture),
                    Theme = theme
                };
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                    $"Could not read the PuTTY session '{sessionName}'", ex, MessageClass.WarningMsg, false);
                return null;
            }
        }

        private static int PointsToPixels(object fontHeight) =>
            (int)Math.Round(Points(fontHeight) * 96.0 / 72.0);

        private static int Points(object fontHeight)
        {
            int points = Convert.ToInt32(fontHeight ?? 10, CultureInfo.InvariantCulture);
            return points <= 0 ? 10 : points;
        }

        private static void AddColour(Dictionary<string, string> theme, RegistryKey key, int index, string name)
        {
            // Stored as "r,g,b" decimal.
            if (key.GetValue($"Colour{index}") is not string value) return;

            string[] parts = value.Split(',');
            if (parts.Length != 3) return;

            if (!byte.TryParse(parts[0], out byte r) ||
                !byte.TryParse(parts[1], out byte g) ||
                !byte.TryParse(parts[2], out byte b)) return;

            theme[name] = $"#{r:x2}{g:x2}{b:x2}";
        }
    }
}
