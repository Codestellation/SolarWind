using System;
using System.Net.Sockets;

namespace Codestellation.SolarWind.Internals
{
    internal static class SocketExtensions
    {
        public static void SafeDispose(this Socket socket)
        {
            if (socket == null)
            {
                return;
            }

            socket.SwallowExceptions(s => s.Shutdown(SocketShutdown.Both));
            socket.SwallowExceptions(s => s.Close());
            socket.SwallowExceptions(s => s.Dispose());
        }

        private static void SwallowExceptions(this Socket self, Action<Socket> action)
        {
            try
            {
                action(self);
            }
            catch
            {
            }
        }
    }
}