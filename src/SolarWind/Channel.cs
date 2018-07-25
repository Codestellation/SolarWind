using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading.Tasks;
using Pipelines.Sockets.Unofficial;

namespace Codestellation.SolarWind
{
    public delegate void SolarWindCallback(Message message);

    public class Channel
    {
        private readonly SolarWindHubOptions _options;
        private readonly SocketConnection _connection;
        private bool _disposed;

        private Task _reader;
        private Task _writer;
        private readonly BlockingCollection<Message> _outgoings;
        private readonly StreamConnection.AsyncPipeStream _pipeStream;


        public static Channel Server(Socket socket, SolarWindHubOptions options)
        {
            SocketConnection.SetRecommendedServerOptions(socket);
            return new Channel(socket, options);
        }

        public static Channel Client(Socket socket, SolarWindHubOptions options)
        {
            SocketConnection.SetRecommendedClientOptions(socket);
            return new Channel(socket, options);
        }

        private Channel(Socket socket, SolarWindHubOptions options)
        {
            _options = options;
            var pipeOptions = new PipeOptions();
            _connection = SocketConnection.Create(socket, pipeOptions);
            _pipeStream = StreamConnection.GetDuplex(_connection);
            _outgoings = new BlockingCollection<Message>();
            _reader = StartReadingTask();
            _writer = StartWritingTask();
        }

        public void Post(Message message) => _outgoings.Add(message);

        private async Task StartReadingTask()
        {
            while (!_disposed)
            {
                ReadResult result = await _connection.Input.ReadAsync();
                if (result.IsCompleted)
                {
                    //Do callback
                }
            }
        }

        private unsafe Task StartWritingTask() => Task.Run(() =>
        {
            foreach (Message message in _outgoings.GetConsumingEnumerable())
            {
                const int prefixSize = sizeof(long);

                Span<byte> span = _connection.Output.GetSpan(prefixSize);
                fixed (byte* p = span)
                {
                    *((long*)p) = message.MessageTypeId.Id;
                }

                _connection.Output.Advance(prefixSize);
                _options.Serializer.Serialize(message.Payload, _pipeStream);
            }
        });
    }
}