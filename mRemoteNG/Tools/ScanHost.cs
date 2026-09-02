using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Runtime.Versioning;
using mRemoteNG.App;
using mRemoteNG.Connection.Protocol.Http;
using mRemoteNG.Connection.Protocol.RDP;
using mRemoteNG.Connection.Protocol.Rlogin;
using mRemoteNG.Connection.Protocol.SSH;
using mRemoteNG.Connection.Protocol.Telnet;
using mRemoteNG.Connection.Protocol.VNC;
using mRemoteNG.Messages;
using mRemoteNG.Resources.Language;


namespace mRemoteNG.Tools
{
    [SupportedOSPlatform("windows")]
    public class ScanHost(string host)
    {
        #region Properties

        public static int SshPort { get; set; } = (int)ProtocolSSH1.Defaults.Port;
        public static int TelnetPort { get; set; } = (int)ProtocolTelnet.Defaults.Port;
        public static int HttpPort { get; set; } = (int)ProtocolHTTP.Defaults.Port;
        public static int HttpsPort { get; set; } = (int)ProtocolHTTPS.Defaults.Port;
        public static int RloginPort { get; set; } = (int)ProtocolRlogin.Defaults.Port;
        public static int RdpPort { get; set; } = (int)RdpProtocol.Defaults.Port;
        public static int VncPort { get; set; } = (int)ProtocolVNC.Defaults.Port;
        public ArrayList OpenPorts { get; set; } = [];
        public ArrayList ClosedPorts { get; set; } = [];
        public bool Rdp { get; set; }
        public bool Vnc { get; set; }
        public bool Ssh { get; set; }
        public bool Telnet { get; set; }
        public bool Rlogin { get; set; }
        public bool Http { get; set; }
        public bool Https { get; set; }
        public string HostIp { get; set; } = host;
        public string HostName { get; set; } = "";

        public string HostNameWithoutDomain
        {
            get
            {
                if (string.IsNullOrEmpty(HostName) || HostName == HostIp)
                {
                    return HostIp;
                }

                return HostName.Split('.')[0];
            }
        }

        #endregion
        #region Methods

        public override string ToString()
        {
            try
            {
                return "SSH: " + Convert.ToString(Ssh) + " Telnet: " + Convert.ToString(Telnet) + " HTTP: " +
                       Convert.ToString(Http) + " HTTPS: " + Convert.ToString(Https) + " Rlogin: " +
                       Convert.ToString(Rlogin) + " RDP: " + Convert.ToString(Rdp) + " VNC: " + Convert.ToString(Vnc);
            }
            catch (Exception)
            {
                Runtime.MessageCollector.AddMessage(MessageClass.WarningMsg, "ToString failed (Tools.PortScan)", true);
                return "";
            }
        }

        /// <summary>
        /// The address as a number, for sorting.
        /// </summary>
        /// <remarks>
        /// Sorted as text, 192.168.1.106 comes before 192.168.1.2 and 192.168.1.35 - the grid
        /// reads character by character and 1 sorts before 3. Anything that will not parse as IPv4
        /// sorts to the end rather than to the front, so a stray entry cannot hide at the top of
        /// the list.
        /// </remarks>
        public long IpSortKey
        {
            get
            {
                if (!IPAddress.TryParse(HostIp, out IPAddress address) ||
                    address.AddressFamily != AddressFamily.InterNetwork)
                    return long.MaxValue;

                byte[] octets = address.GetAddressBytes();
                return ((long)octets[0] << 24) | ((long)octets[1] << 16) |
                       ((long)octets[2] << 8) | octets[3];
            }
        }

        /// <summary>
        /// The name on its own, blank when there is none.
        /// </summary>
        /// <remarks>
        /// The grid used to show name-or-address in one column, so on everything that resolved -
        /// which is most of a working network - the address was simply not there. Knowing a host
        /// is called WIN11-WEB does not tell you which machine answered, and that is usually the
        /// thing being looked up. Two columns, both always filled in.
        /// </remarks>
        public string HostNameOnly =>
            string.IsNullOrEmpty(HostName) || HostName == HostIp ? string.Empty : HostName;

        //Adpating to objectlistview instaed of listview
        public string HostIPorName
        {
            get
            {
                if (string.IsNullOrEmpty(HostName))
                    return HostIp;
                else
                    return HostName;
            }
        }

        public string RdpName => BoolToYesNo(Rdp);

        public string VncName => BoolToYesNo(Vnc);

        // Was BoolToYesNo(Rdp) - the SSH column showed whether port 3389 was open, not 22.
        // A copy-paste that made the one column most people scan for report another protocol.
        public string SshName => BoolToYesNo(Ssh);

        public string TelnetName => BoolToYesNo(Telnet);

        public string RloginName => BoolToYesNo(Rlogin);

        public string HttpName => BoolToYesNo(Http);

        public string HttpsName => BoolToYesNo(Https);

        // Both lists used to be built by appending "port, " in a loop, so every cell in the
        // grid ended on a comma with nothing after it.
        public string OpenPortsName => Join(OpenPorts);

        public string ClosedPortsName => Join(ClosedPorts);

        private static string Join(ArrayList ports) =>
            string.Join(", ", ports.Cast<int>());


        /// <summary>
        /// A tick for an open port, and nothing at all for a closed one.
        /// </summary>
        /// <remarks>
        /// It used to write "Yes" or "No" in every cell of seven columns. A scan of a /24 fills
        /// the grid with hundreds of rows of the word "No", and the handful of hosts that answered
        /// are lost in it. A mark that is only present when there is something to report lets the
        /// eye find them without reading.
        /// </remarks>
        private static string BoolToYesNo(bool value)
        {
            return value ? "\u2714" : string.Empty;
        }

        public void SetAllProtocols(bool value)
        {
            Vnc = value;
            Telnet = value;
            Ssh = value;
            Rlogin = value;
            Rdp = value;
            Https = value;
            Http = value;
        }

        #endregion
    }
}