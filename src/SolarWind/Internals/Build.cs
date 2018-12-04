using System.Net;
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
        public static Socket TcpIPv4()
            => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

        /// <summary>
        /// Created a socket for the address family of the specified endpoint
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Socket BuildTcpSocket(this IPEndPoint self)
            => new Socket(self.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
    }
}