using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Codestellation.SolarWind
{
    public class Poller : IDisposable
    {
        private readonly ConcurrentDictionary<ChannelId, Channel> _channels;
        private readonly Thread _pollerThread;
        private bool _disposed;

        private readonly List<Socket> _readList;
        private readonly List<Socket> _errorList;
        private readonly Dictionary<Socket, Connection> _connectionIndex;
        private readonly ManualResetEventSlim _threadExited;

        public Poller(ConcurrentDictionary<ChannelId, Channel> channels)
        {
            _channels = channels;
            _readList = new List<Socket>();
            _errorList = new List<Socket>();

            _connectionIndex = new Dictionary<Socket, Connection>();
            _threadExited = new ManualResetEventSlim();

            _pollerThread = new Thread(PollChannelSockets);
            _pollerThread.Start();
        }

        private void PollChannelSockets(object obj)
        {
            while (!_disposed)
            {
                ProcessPolling();
            }

            _threadExited.Set();
        }

        private void ProcessPolling()
        {
            FillCollections();
            if (_disposed)
            {
                return;
            }

            if (_disposed)
            {
                return;
            }

            Poll();
            InvokeReady();
        }

        private void FillCollections()
        {
            do
            {
                _readList.Clear();
                _errorList.Clear();
                _connectionIndex.Clear();

                foreach (Channel channel in _channels.Values)
                {
                    Connection connection = channel.Connection;
                    if (connection == null)
                    {
                        continue;
                    }

                    if (connection.TryGetSocket(out Socket socket))
                    {
                        _readList.Add(socket);
                        _errorList.Add(socket);
                        _connectionIndex.Add(socket, channel.Connection);
                    }
                }
            } while (!_disposed && _readList.Count == 0);
        }

        private void Poll() => Socket.Select(_readList, Array.Empty<Socket>(), _errorList, 50_000);

        private void InvokeReady()
        {
            foreach (Socket socket in _readList)
            {
                _connectionIndex[socket].NotifyReady();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _threadExited.Wait();
        }
    }
}