using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Codestellation.SolarWind
{
    public static class UriExtensions
    {
        public static IPEndPoint ResolveLocalEndpoint(this Uri localUri)
        {
            Uri uri = HandleWildcard(localUri);

            return uri.ResolveRemoteEndpoint();
        }

        public static IPEndPoint ResolveRemoteEndpoint(this Uri remoteUri)
        {
            if (!IPAddress.TryParse(remoteUri.Host, out IPAddress ipAddress))
            {
                ipAddress = Dns
                    .GetHostAddresses(remoteUri.Host)
                    .SingleOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
            }

            if (ipAddress == null)
            {
                throw new ArgumentException($"Could not resolve address {remoteUri}. Please notice IPv6 is not supported currently");
            }

            return new IPEndPoint(ipAddress, remoteUri.Port);
        }

        private static Uri HandleWildcard(Uri localUri) =>
            localUri.Host == "*"
                ? new Uri(localUri.OriginalString.Replace("*", "0.0.0.0"))
                : localUri;
    }
}