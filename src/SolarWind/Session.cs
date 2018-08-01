using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Codestellation.SolarWind
{
    public class Session
    {
        private readonly Queue<(MessageId, Message)> _queue;
        private readonly Dictionary<MessageId, Message> _sent;

        private MessageId _currentMessageId;
        private readonly SemaphoreSlim _asyncLock;
        private readonly SemaphoreSlim _awaiter;

        public Session()
        {
            _queue = new Queue<(MessageId, Message)>();
            //Keep some sent messages until ACK received to be able to resend them in case of failure
            _sent = new Dictionary<MessageId, Message>();
            _asyncLock = new SemaphoreSlim(1);
            _awaiter = new SemaphoreSlim(0);
        }

        public MessageId Enqueue(in Message message)
        {
            _asyncLock.Wait();
            try
            {
                _currentMessageId = _currentMessageId.Next();
                _queue.Enqueue((_currentMessageId, message));
                _awaiter.Release();
            }
            finally
            {
                _asyncLock.Release();
            }

            return _currentMessageId;
        }

        public void Ack(MessageId messageId)
        {
            _asyncLock.Wait();
            _sent.Remove(messageId);
            _asyncLock.Release();
        }

        public ValueTask<(MessageId, Message)> Dequeue(CancellationToken cancellation) =>
            !_awaiter.Wait(0) ? AwaitNewMessages(cancellation) : DoDequeue(cancellation);

        private async ValueTask<(MessageId, Message)> AwaitNewMessages(CancellationToken cancellation)
        {
            await _awaiter.WaitAsync(cancellation).ConfigureAwait(false);
            return await DoDequeue(cancellation);
        }

        private ValueTask<(MessageId, Message)> DoDequeue(CancellationToken cancellation)
        {
            if (!_asyncLock.Wait(0))
            {
                return AwaitLock(cancellation);
            }

            (MessageId messageId, Message message) = _queue.Dequeue();
            _sent.Add(messageId, message);
            _asyncLock.Release();
            return new ValueTask<(MessageId, Message)>((messageId, message));
        }

        private async ValueTask<(MessageId, Message)> AwaitLock(CancellationToken cancellation)
        {
            await _asyncLock.WaitAsync(cancellation).ConfigureAwait(false);
            (MessageId messageId, Message message) = _queue.Dequeue();
            _sent.Add(messageId, message);
            _asyncLock.Release();
            return (messageId, message);
        }
    }
}