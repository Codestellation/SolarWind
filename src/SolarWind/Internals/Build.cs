using System.Net;
using System.Net.Sockets;

namespace Codestellation.SolarWind.Internals
{
    public static class Build
    {
        public static Socket TcpIPv4() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public static Socket UdpServer(IPEndPoint endpoint = null)
        {
            var result = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            result.Bind(endpoint ?? new IPEndPoint(IPAddress.Loopback, 0));
            return result;
        }

        public static Socket UdpClientFor(EndPoint endpoint)
        {
            var result = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            result.Connect(endpoint);
            return result;
        }

        public static Socket UdpClientFor(Socket server) => UdpClientFor(server.LocalEndPoint);
    }
}