using mRemoteNG.App;
using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Container;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Versioning;

namespace mRemoteNG.Tools
{
    [SupportedOSPlatform("windows")]
    public class SshTunnelTypeConverter : StringConverter
    {
        public static string[] SshTunnels => SshTunnelsFor(null);

        /// <summary>
        /// The connections that can carry a tunnel, for the connection being edited.
        /// </summary>
        /// <param name="excludedName">
        /// The name of the connection the list is being offered to, left out of it. A connection
        /// cannot reach itself through itself, and offering it invites a choice that can only
        /// fail.
        /// </param>
        public static string[] SshTunnelsFor(string excludedName)
        {
            List<string> names = GetSshConnectionNames(Runtime.ConnectionsService.ConnectionTreeModel.RootNodes);

            if (!string.IsNullOrEmpty(excludedName))
                names.RemoveAll(name => name == excludedName);

            // Sorted because the tree's own order is the order they happen to sit in, which is no
            // help at all once there are more than a handful to pick from.
            names.Sort(StringComparer.CurrentCultureIgnoreCase);

            // The blank first entry is how "no tunnel" is chosen.
            names.Insert(0, string.Empty);

            return names.ToArray();
        }

        // recursively traverse the connection tree to find all ConnectionInfo s of type SSH
        private static List<string> GetSshConnectionNames(IEnumerable<ConnectionInfo> rootnodes)
        {
            List<string> result = new();
            foreach (ConnectionInfo node in rootnodes)
                if (node is ContainerInfo container)
                {
                    result.AddRange(GetSshConnectionNames(container.Children));
                }
                else
                {
                    if (node is PuttySessionInfo) continue;

                    // SSHNative belongs here as much as the other two: it forwards ports itself
                    // now, and ConnectionInitiator asks a protocol whether it can provide a tunnel
                    // rather than whether it is PuTTY. Leaving it out emptied this list entirely
                    // for anyone who had moved their SSH connections over - and the list is
                    // exclusive, so an empty one cannot be typed past.
                    if (node.Protocol is ProtocolType.SSH1 or ProtocolType.SSH2 or ProtocolType.SSHNative)
                        result.Add(node.Name);
                }

            return result;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            // The grid hands over the connection being edited, which is how the list knows to
            // leave that one out. With several selected there is no single one to exclude, and
            // the whole list is the honest answer.
            string excludedName = context?.Instance is ConnectionInfo connection ? connection.Name : null;

            return new StandardValuesCollection(SshTunnelsFor(excludedName));
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context)
        {
            return true;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)
        {
            return true;
        }
    }
}