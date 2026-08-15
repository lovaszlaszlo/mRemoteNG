using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using mRemoteNG.App;
using mRemoteNG.Messages;

namespace mRemoteNG.Connection.Protocol.SSH
{
    /// <summary>
    /// Remembers which host key was accepted for a server, so a changed key is noticed.
    /// </summary>
    /// <remarks>
    /// Deliberately not the OpenSSH known_hosts format: that stores the key itself and hashes the
    /// host names, which is more than is needed here. One line per server holding the SHA-256
    /// fingerprint is enough to answer the only question that matters - is this the same key as
    /// last time.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static class SshKnownHosts
    {
        public enum Verdict
        {
            /// <summary>Never seen before - the user has to decide.</summary>
            Unknown,

            /// <summary>Same key as the one accepted earlier.</summary>
            Known,

            /// <summary>A different key than the one accepted earlier.</summary>
            Changed
        }

        private static readonly object FileLock = new();

        private static string FilePath => Path.Combine(App.Info.SettingsFileInfo.SettingsPath, "ssh_known_hosts");

        public static Verdict Check(string host, int port, string fingerprint, out string storedFingerprint)
        {
            storedFingerprint = null;
            string key = MakeKey(host, port);

            foreach ((string entryKey, string entryFingerprint) in ReadEntries())
            {
                if (!string.Equals(entryKey, key, StringComparison.OrdinalIgnoreCase)) continue;

                storedFingerprint = entryFingerprint;
                return string.Equals(entryFingerprint, fingerprint, StringComparison.Ordinal)
                    ? Verdict.Known
                    : Verdict.Changed;
            }

            return Verdict.Unknown;
        }

        public static void Remember(string host, int port, string fingerprint)
        {
            string key = MakeKey(host, port);

            lock (FileLock)
            {
                try
                {
                    List<string> lines = ReadEntries()
                        .Where(entry => !string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                        .Select(entry => $"{entry.Key} {entry.Fingerprint}")
                        .ToList();

                    lines.Add($"{key} {fingerprint}");

                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? string.Empty);
                    File.WriteAllLines(FilePath, lines);
                }
                catch (Exception ex)
                {
                    Runtime.MessageCollector.AddExceptionMessage(
                        "Could not record the SSH host key", ex, MessageClass.WarningMsg, false);
                }
            }
        }

        private static IEnumerable<(string Key, string Fingerprint)> ReadEntries()
        {
            string path = FilePath;
            if (!File.Exists(path)) yield break;

            string[] lines;
            lock (FileLock)
            {
                try
                {
                    lines = File.ReadAllLines(path);
                }
                catch (Exception ex)
                {
                    Runtime.MessageCollector.AddExceptionMessage(
                        "Could not read the stored SSH host keys", ex, MessageClass.WarningMsg, false);
                    yield break;
                }
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;

                string[] parts = line.Split(' ', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2) yield return (parts[0], parts[1]);
            }
        }

        private static string MakeKey(string host, int port) => $"{host?.Trim().ToLowerInvariant()}:{port}";
    }
}
