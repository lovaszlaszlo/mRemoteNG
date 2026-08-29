using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Text;

namespace mRemoteNG.Connection.Protocol.SSH
{
    /// <summary>
    /// Reads the connection's "SSH options" field, which is a command line.
    /// </summary>
    /// <remarks>
    /// For a PuTTY based connection the field really is one: <c>PuttyBase</c> appends it verbatim
    /// to the arguments PuTTY is started with, so anything PuTTY understands works. The native
    /// protocol has no command line to append to, and used to read nothing out of the field but a
    /// private key - everything else typed there vanished without a word.
    ///
    /// Worse than vanishing: the old reader took the whole remainder of the string after
    /// <c>-i</c> as the key path, so <c>-i C:\key -L 5432:localhost:5432</c> found no such file
    /// and lost the key as well as the tunnel, falling back to whatever sat in ~/.ssh.
    ///
    /// So the field is parsed properly here, and whatever is left over is reported rather than
    /// dropped. The reporting matters as much as the parsing: a setting that is quietly ignored is
    /// worse than one that is refused, because it looks like it is working.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    internal sealed class SshCommandLineOptions
    {
        public enum ForwardKind
        {
            /// <summary>-L: a port here, reached through the session.</summary>
            Local,

            /// <summary>-R: a port on the server, reached back through the session.</summary>
            Remote,

            /// <summary>-D: a SOCKS proxy here.</summary>
            Dynamic
        }

        public sealed class PortForward
        {
            public ForwardKind Kind { get; init; }
            public string BoundHost { get; init; }
            public uint BoundPort { get; init; }
            public string Host { get; init; }
            public uint Port { get; init; }

            public override string ToString() =>
                Kind == ForwardKind.Dynamic
                    ? $"-D {BoundHost}:{BoundPort}"
                    : $"{(Kind == ForwardKind.Local ? "-L" : "-R")} {BoundHost}:{BoundPort}:{Host}:{Port}";
        }

        /// <summary>Path given with -i, or a bare path as the whole field. Null when there is none.</summary>
        public string KeyFile { get; private set; }

        public List<PortForward> Forwards { get; } = new();

        /// <summary>
        /// Where to write a transcript of the session, from -sessionlog. Null when none was asked
        /// for. Still carries PuTTY's &amp;-placeholders: they need the host, which is not known
        /// here.
        /// </summary>
        public string SessionLogPath { get; private set; }

        /// <summary>
        /// Everything that was not understood, kept verbatim so it can be shown back to the user.
        /// </summary>
        public List<string> Unrecognised { get; } = new();

        public static SshCommandLineOptions Parse(string options)
        {
            SshCommandLineOptions parsed = new();
            string text = options?.Trim() ?? string.Empty;

            if (text.Length == 0) return parsed;

            // A bare path, which is how this field was often filled in before -i was understood.
            // Checked whole and first, since such a path may well contain spaces.
            string bare = ExpandPath(text.Trim('"'));
            if (File.Exists(bare))
            {
                parsed.KeyFile = bare;
                return parsed;
            }

            List<string> tokens = Tokenise(text);

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];

                // Before -i and the rest, so that the longer flag is not mistaken for a shorter
                // one sharing its first letters.
                if (TryTakeValue(token, "-sessionlog", tokens, ref i, out string logPath))
                {
                    parsed.SessionLogPath = logPath.Trim('"');
                    continue;
                }

                if (TryTakeValue(token, "-i", tokens, ref i, out string keyFile))
                {
                    parsed.KeyFile = ExpandPath(keyFile.Trim('"'));
                    continue;
                }

                if (TryTakeValue(token, "-L", tokens, ref i, out string local))
                {
                    parsed.AddForward(ForwardKind.Local, local, token);
                    continue;
                }

                if (TryTakeValue(token, "-R", tokens, ref i, out string remote))
                {
                    parsed.AddForward(ForwardKind.Remote, remote, token);
                    continue;
                }

                if (TryTakeValue(token, "-D", tokens, ref i, out string dynamicForward))
                {
                    parsed.AddForward(ForwardKind.Dynamic, dynamicForward, token);
                    continue;
                }

                parsed.Unrecognised.Add(token);
            }

            return parsed;
        }

        /// <summary>
        /// Matches a flag and yields its value, which may be glued to the flag (-iC:\key) or be the
        /// token after it (-i C:\key), since both spellings are accepted by ssh and by PuTTY.
        /// </summary>
        private static bool TryTakeValue(string token, string flag, List<string> tokens, ref int index,
                                         out string value)
        {
            value = null;

            if (!token.StartsWith(flag, StringComparison.OrdinalIgnoreCase)) return false;

            if (token.Length > flag.Length)
            {
                value = token[flag.Length..];
                return true;
            }

            if (index + 1 >= tokens.Count) return false;

            value = tokens[++index];
            return true;
        }

        private void AddForward(ForwardKind kind, string spec, string flag)
        {
            // -L and -R: [bind:]port:host:hostport. -D: [bind:]port.
            string[] parts = spec.Split(':');

            bool dynamicForward = kind == ForwardKind.Dynamic;
            bool hasBind = parts.Length == (dynamicForward ? 2 : 4);

            if (parts.Length != (dynamicForward ? 1 : 3) && !hasBind)
            {
                Unrecognised.Add($"{flag} {spec}");
                return;
            }

            // The default bind is the loopback at whichever end listens - here for -L and -D, on
            // the server for -R, which is also what ssh does without GatewayPorts.
            string boundHost = hasBind
                ? parts[0]
                : kind == ForwardKind.Remote ? "localhost" : "127.0.0.1";

            int boundPortIndex = hasBind ? 1 : 0;
            int portIndex = hasBind ? 3 : 2;

            uint port = 0;

            if (!TryPort(parts[boundPortIndex], out uint boundPort) ||
                (!dynamicForward && !TryPort(parts[portIndex], out port)))
            {
                Unrecognised.Add($"{flag} {spec}");
                return;
            }

            Forwards.Add(new PortForward
            {
                Kind = kind,
                BoundHost = boundHost,
                BoundPort = boundPort,
                Host = dynamicForward ? null : parts[hasBind ? 2 : 1],
                Port = port
            });
        }

        /// <summary>
        /// Resolves the shorthands people reasonably expect in a path field: %USERPROFILE% and
        /// friends, and a leading ~.
        /// </summary>
        /// <remarks>
        /// Worth more than the typing it saves - a connection file that names the profile by
        /// variable rather than by C:\Users\someone still works when it is opened on another
        /// machine or by another user.
        ///
        /// $env:USERPROFILE, which is what a PowerShell habit produces, is deliberately NOT among
        /// them: that syntax exists only inside PowerShell and nothing expands it out here. Such a
        /// path is simply not found, and the "private key was not found" warning names it back.
        /// </remarks>
        public static string ExpandPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            string expanded = Environment.ExpandEnvironmentVariables(path);

            if (expanded.StartsWith("~", StringComparison.Ordinal))
                expanded = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    expanded[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            return expanded;
        }

        private static bool TryPort(string text, out uint port)
        {
            port = 0;

            if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value))
                return false;

            if (value == 0 || value > 65535) return false;

            port = value;
            return true;
        }

        /// <summary>
        /// Splits on whitespace, keeping anything inside double quotes together, so a path with
        /// spaces in it survives.
        /// </summary>
        private static List<string> Tokenise(string text)
        {
            List<string> tokens = new();
            StringBuilder current = new();
            bool quoted = false;

            foreach (char c in text)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (!quoted && char.IsWhiteSpace(c))
                {
                    if (current.Length > 0) tokens.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0) tokens.Add(current.ToString());

            return tokens;
        }
    }
}
