using System;
using System.Linq;
using System.Net;

namespace Codestellation.SolarWind.Internals
{
    public static class UriExtensions
    {
        public const string Tcp = "tcp";
        public const string Tls = "tls";

        public static bool UseTls(this Uri uri) => string.Equals(uri.Scheme, Tls, StringComparison.OrdinalIgnoreCase);

        public static IPEndPoint[] ResolveLocalEndpoint(this Uri localUri)
        {
            Uri uri = HandleWildcard(localUri);

            return uri.ResolveRemoteEndpoint();
        }

        public static IPEndPoint[] ResolveRemoteEndpoint(this Uri remoteUri)
        {
            if (IPAddress.TryParse(remoteUri.Host, out IPAddress ipAddress))
            {
                return new[] {new IPEndPoint(ipAddress, remoteUri.Port)};
            }


            IPAddress[] ips = Dns.GetHostAddresses(remoteUri.Host);
            if (ips == null || ips.Length == 0)
            {
                throw new ArgumentException($"Could not resolve address {remoteUri}. Please notice IPv6 is not supported currently");
            }

            return ips
                .Select(x => new IPEndPoint(x, remoteUri.Port))
                .ToArray();
        }

        private static Uri HandleWildcard(Uri localUri) =>
            localUri.Host == "*"
                ? new Uri(localUri.OriginalString.Replace("*", "0.0.0.0"))
                : localUri;
    }
}