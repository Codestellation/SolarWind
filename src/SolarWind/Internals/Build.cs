using System.Net.Sockets;

namespace Codestellation.SolarWind.Internals
{
    /// <summary>
    /// Contains some builder methods to simplify socket creation
    /// </summary>
    public static class Build
    {
        /// <summary>
        /// Creates an instance of <see cref="Socket" /> class initialized for usage in TCP/IP v4 networks
        /// </summary>
        /// <returns></returns>
        public static Socket TcpIPv4() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
            ReceiveTimeout = 1000,
            SendTimeout = 1000
        };
    }
}