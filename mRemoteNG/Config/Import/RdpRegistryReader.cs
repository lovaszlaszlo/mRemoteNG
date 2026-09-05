using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;
using mRemoteNG.App;
using mRemoteNG.Messages;

namespace mRemoteNG.Config.Import
{
    /// <summary>
    /// One machine the Windows Remote Desktop client remembers.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class RdpRegistryEntry
    {
        public string Address { get; init; } = string.Empty;

        public int Port { get; set; } = 3389;

        public string Username { get; set; } = string.Empty;

        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// The address as it is worth showing: with the port only when it is not the usual one.
        /// </summary>
        public string Display => Port == 3389 ? Address : $"{Address}:{Port}";

        public string Account => string.IsNullOrEmpty(Domain) ? Username : $"{Domain}\\{Username}";
    }

    /// <summary>
    /// Reads the machines the Windows Remote Desktop client has connected to.
    /// </summary>
    /// <remarks>
    /// Two keys, holding different halves of the answer. "Servers" has every machine ever connected
    /// to, each with the account last used - but its subkeys are named by address alone, so a
    /// non-standard port is nowhere in it. "Default" holds only the last ten, but as they were
    /// typed, port and all. Merged they give a list worth importing; either on its own is missing
    /// something, and the taskbar's jump list shows neither in full.
    ///
    /// No password is read, because none is there: those live in Credential Manager, encrypted to
    /// the Windows account.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static class RdpRegistryReader
    {
        private const string ClientKey = @"Software\Microsoft\Terminal Server Client";

        public static IReadOnlyList<RdpRegistryEntry> Read()
        {
            Dictionary<string, RdpRegistryEntry> found = new(StringComparer.OrdinalIgnoreCase);

            try
            {
                ReadServers(found);
                ReadRecentlyTyped(found);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("RdpRegistryReader.Read() failed.", ex);
            }

            return found.Values.OrderBy(entry => entry.Address, StringComparer.OrdinalIgnoreCase)
                        .ToList();
        }

        /// <summary>
        /// Every machine ever connected to, with the account last used on it.
        /// </summary>
        private static void ReadServers(IDictionary<string, RdpRegistryEntry> found)
        {
            using RegistryKey servers = Registry.CurrentUser.OpenSubKey(ClientKey + @"\Servers");
            if (servers == null) return;

            foreach (string address in servers.GetSubKeyNames())
            {
                if (string.IsNullOrWhiteSpace(address)) continue;

                using RegistryKey server = servers.OpenSubKey(address);

                RdpRegistryEntry entry = new() { Address = address };
                SetAccount(entry, server?.GetValue("UsernameHint") as string);

                found[address] = entry;
            }
        }

        /// <summary>
        /// The last ten addresses as they were typed - the only place the port survives.
        /// </summary>
        private static void ReadRecentlyTyped(IDictionary<string, RdpRegistryEntry> found)
        {
            using RegistryKey recent = Registry.CurrentUser.OpenSubKey(ClientKey + @"\Default");
            if (recent == null) return;

            foreach (string name in recent.GetValueNames())
            {
                if (!name.StartsWith("MRU", StringComparison.OrdinalIgnoreCase)) continue;
                if (recent.GetValue(name) is not string typed || string.IsNullOrWhiteSpace(typed)) continue;

                Split(typed, out string address, out int port);

                if (found.TryGetValue(address, out RdpRegistryEntry existing))
                    existing.Port = port;
                else
                    found[address] = new RdpRegistryEntry { Address = address, Port = port };
            }
        }

        /// <summary>
        /// Splits "host:3381" into its parts, leaving IPv6 in brackets alone.
        /// </summary>
        private static void Split(string typed, out string address, out int port)
        {
            address = typed.Trim();
            port = 3389;

            int colon = address.LastIndexOf(':');
            if (colon <= 0 || address.IndexOf(':') != colon) return;
            if (!int.TryParse(address[(colon + 1)..], out int parsed)) return;
            if (parsed is < 1 or > 65535) return;

            port = parsed;
            address = address[..colon];
        }

        /// <summary>
        /// Separates DOMAIN\user; a user@domain.tld is left whole, because that is how it is typed
        /// and how the server expects it.
        /// </summary>
        private static void SetAccount(RdpRegistryEntry entry, string usernameHint)
        {
            if (string.IsNullOrWhiteSpace(usernameHint)) return;

            string hint = usernameHint.Trim();
            int slash = hint.IndexOf('\\');

            if (slash > 0)
            {
                entry.Domain = hint[..slash];
                entry.Username = hint[(slash + 1)..];
                return;
            }

            entry.Username = hint;
        }
    }
}
