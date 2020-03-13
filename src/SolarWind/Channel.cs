using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;
using Codestellation.SolarWind.Threading;
using Microsoft.Extensions.Logging;

namespace Codestellation.SolarWind
{
    /// <summary>
    /// Channel is an abstraction over a connection between two hosts. It provides duplex asynchronous messages stream.
    /// </summary>
    public class Channel
    {
        private volatile Connection _connection;
        private readonly ChannelOptions _options;

        private volatile int _disposed;
        private const int Disposed = 1;
        private const int NotDisposed = 0;

        private volatile CancellationTokenSource _cancellationSource;
        private readonly Session _session;
        private SolarWindCallback _callback;
        private readonly ILogger<Channel> _logger;
        private readonly Message[] _batch;
        private int _batchLength;
        private long _lastReceived;
        private long _lastSent;

        private readonly SemaphoreSlim _readLock;
        private readonly SemaphoreSlim _writeLock;

        public HubId RemoteHubId { get; internal set; }

        internal ChannelId ChannelId { get; set; }
        internal Uri RemoteUri { get; set; }

        public bool Connected => _connection?.Connected ?? false;

        public event Action<Channel> OnKeepAliveTimeout;

        public Channel(ChannelOptions options, ILoggerFactory factory)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _batch = new Message[100];
            _logger = factory.CreateLogger<Channel>();
            _session = new Session(options.Serializer, OnIncomingMessage, factory.CreateLogger<Session>());
            OnKeepAliveTimeout += channel => { };

            _lastReceived = _lastSent = DateTime.Now.Ticks;
            _readLock = new SemaphoreSlim(1,1);
            _writeLock = new SemaphoreSlim(1,1);

            Task.Run(CheckKeepAlive).ContinueWith(LogAndFail, TaskContinuationOptions.OnlyOnFaulted);
        }

        internal void OnReconnect(Connection connection)
        {
            EnsureNotDisposed();

            _logger.LogInformation($"Reconnected to {RemoteHubId}");
            Stop(false);

            var cancellation = new CancellationTokenSource();

            if (Interlocked.CompareExchange(ref _cancellationSource, cancellation, null) != null)
            {
                //Possible a bug here so not more than one connection must exist between hubs
                connection.Dispose();
                return;
            }

            _cancellationSource = cancellation;
            _connection = connection;

            Task
                .Factory
                .StartNew(() => StartReadingTask(cancellation.Token), cancellation.Token, TaskCreationOptions.None, IOTaskScheduler.Instance)
                .ContinueWith(LogAndFail, TaskContinuationOptions.OnlyOnFaulted);

            Task
                .Factory
                .StartNew(() => StartWritingTask(cancellation.Token), cancellation.Token, TaskCreationOptions.None, IOTaskScheduler.Instance)
                .ContinueWith(LogAndFail, TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Puts a message into send queue and returns assigned message identifier.
        /// </summary>
        /// <param name="data">The message</param>
        /// <param name="replyTo">An id of the message which replies to</param>
        /// <returns>An assigned id to the outgoing message</returns>
        public MessageId Post(object data, MessageId replyTo = default)
        {
            EnsureNotDisposed();
            return _session.EnqueueOutgoing(data, replyTo);
        }

        /// <summary>
        /// Sets callback for the channel. Previous callback will be replaced with the supplied one.
        /// </summary>
        /// <param name="callback"></param>
        public void SetCallback(SolarWindCallback callback)
        {
            EnsureNotDisposed();
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        private async Task StartReadingTask(CancellationToken cancellation)
        {
            bool lockTaken = false;
            try
            {
                lockTaken = await _readLock.WaitAsync(Timeout.Infinite, cancellation).ConfigureAwait(ContinueOn.IOScheduler);

                _logger.LogInformation($"Starting receiving from {RemoteHubId.Id}");
                while (!cancellation.IsCancellationRequested)
                {
                    await Receive(cancellation).ConfigureAwait(ContinueOn.IOScheduler);
                }
            }
            finally
            {
                if (lockTaken)
                {
                    _readLock.Release();
                }
            }
        }

        private async ValueTask Receive(CancellationToken token)
        {
            PooledMemoryStream buffer = MemoryStreamPool.Instance.Get();
            try
            {
                await _connection.ReceiveAsync(buffer, WireHeader.Size, token).ConfigureAwait(ContinueOn.IOScheduler);
                buffer.Position = 0;
                WireHeader wireHeader = WireHeader.ReadFrom(buffer);
                buffer.Reset();

                await _connection.ReceiveAsync(buffer, wireHeader.PayloadSize.Value, token).ConfigureAwait(ContinueOn.IOScheduler);
                buffer.Position = 0;
                var message = new Message(wireHeader.MessageHeader, buffer);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"Received {message.Header.ToString()}");
                }
                _session.EnqueueIncoming(message);
                _lastReceived = DateTime.Now.Ticks;
            }
            //Note: do not handle other exception types. These would mean something went completely wrong and we'd better know it asap
            catch (IOException ex)
            {
                MemoryStreamPool.Instance.Return(buffer);
                _logger.LogInformation(ex, "Receive Error.");
                Stop(true);
            }
            catch (OperationCanceledException)
            {
                MemoryStreamPool.Instance.Return(buffer);
            }
        }

        private void OnIncomingMessage(in MessageHeader header, object data)
        {
            try
            {
                (_callback ?? _options.Callback).Invoke(this, header, data);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, $"Error during callback. {header.ToString()}");
                }
            }
        }

        private async Task StartWritingTask(CancellationToken cancellation)
        {
            bool lockTaken = false;
            try
            {
                lockTaken = await _writeLock.WaitAsync(Timeout.Infinite, cancellation).ConfigureAwait(false);

                _logger.LogInformation($"Starting writing to {RemoteHubId.Id}");

                while (!cancellation.IsCancellationRequested)
                {
                    await TrySend(cancellation).ConfigureAwait(ContinueOn.IOScheduler);
                }
            }
            finally
            {
                if (lockTaken)
                {
                    _writeLock.Release();
                }
            }
        }

        private async Task TrySend(CancellationToken cancellation)
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"Dequeuing batch to send to {RemoteHubId.ToString()}");
                }

                while (!cancellation.IsCancellationRequested
                       && _batchLength == 0 //Batch was not send due to exception. will try to resend it
                       && (_batchLength = _session.TryDequeueBatch(_batch)) == 0)
                {
                    await _session.AwaitOutgoing(cancellation).ConfigureAwait(ContinueOn.IOScheduler);
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"Dequeued {_batchLength} messages to {RemoteHubId.ToString()}");
                }


                await TrySendBatch(cancellation).ConfigureAwait(false);

                Array.ForEach(_batch, m => m.Dispose());
                Array.Clear(_batch, 0, _batch.Length); //Allow GC to collect streams
                _batchLength = 0;
                _lastSent = DateTime.Now.Ticks;
            }
            //Note: do not handle other exception types. These would mean something went completely wrong and we'd better know it asap
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Send error");
                Stop(true);
            }
            catch (OperationCanceledException)
            {
                //Return buffers in case of work finished.
                Array.ForEach(_batch, m => m.Dispose());
                Array.Clear(_batch, 0, _batch.Length);
                _logger.LogInformation("Send worker stopped");
            }
        }

        private async Task TrySendBatch(CancellationToken cancellation)
        {
            for (var i = 0; i < _batchLength; i++)
            {
                //_logger.LogDebug($"Writing message {message.Header.ToString()}");
                await _connection
                    .WriteAsync(_batch[i], cancellation)
                    .ConfigureAwait(ContinueOn.IOScheduler);
            }

            await _connection
                .FlushAsync(cancellation)
                .ConfigureAwait(ContinueOn.IOScheduler);
        }

        //It's made internal to avoid occasional calls from user's code.

        internal void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, Disposed, NotDisposed) == NotDisposed)
            {
                Stop(false);
                _session.Dispose();
            }
        }

        private void Stop(bool reconnect)
        {
            CancellationTokenSource source = Interlocked.Exchange(ref _cancellationSource, null);

            if (source == null)
            {
                return;
            }

            source.Cancel();
            _connection.Dispose();

            if (reconnect)
            {
                _connection.Reconnect();
            }
        }

        private void LogAndFail(Task task)
        {
            _logger.LogCritical(task.Exception, "Task failed:");
            Environment.FailFast($"Task failed: {task.Exception}", task.Exception);
        }

        private async void CheckKeepAlive()
        {
            if (_options.KeepAliveTimeout <= TimeSpan.Zero)
            {
                return;
            }

            while (_disposed != Disposed)
            {
                await Task.Delay(_options.KeepAliveTimeout).ConfigureAwait(false);

                var current = DateTime.Now.Ticks;;
                long timeoutTicks = _options.KeepAliveTimeout.Ticks;

                var lastReceived = current - _lastReceived;
                var lastSent = current - _lastSent;
                if (_disposed != Disposed
                    && timeoutTicks < lastReceived
                    && timeoutTicks < lastSent)
                {
                    OnKeepAliveTimeout(this);
                }
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed == Disposed)
            {
                throw new ObjectDisposedException(ChannelId.ToString());
            }
        }
    }
}