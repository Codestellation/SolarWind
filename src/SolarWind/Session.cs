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
        private TaskCompletionSource<(MessageId, Message)> _completionSource;

        public Session()
        {
            _queue = new Queue<(MessageId, Message)>();
            //Keep some sent messages until ACK received to be able to resend them in case of failure
            _sent = new Dictionary<MessageId, Message>();
        }

        public MessageId Enqueue(in Message message)
        {
            lock (_queue)
            {
                _currentMessageId = _currentMessageId.Next();

                if (_completionSource == null)
                {
                    _queue.Enqueue((_currentMessageId, message));
                }
                else
                {
                    _sent.Add(_currentMessageId, message);
                    _completionSource.SetResult((_currentMessageId, message));
                    _completionSource = null;
                }

                return _currentMessageId;
            }
        }

        public void Ack(MessageId messageId)
        {
            lock (_queue)
            {
                _sent.Remove(messageId);
            }
        }

        public bool TryDequeueSync(out MessageId messageId, out Message message)
        {
            if (Monitor.TryEnter(_queue))
            {
                if (_queue.Count > 0)
                {
                    (messageId, message) = _queue.Dequeue();
                    _sent.Add(messageId, message);
                    return true;
                }

                Monitor.Exit(_queue);
            }

            messageId = default;
            message = default;
            return false;
        }

        //TODO: Replace with ValueTask<> with usage of IValueTaskSource (don't have to have pool, it's supposed to be used be the only reader)
        public Task<(MessageId, Message)> DequeueAsync(CancellationToken token)
        {
            lock (_queue)
            {
                if (_queue.Count > 0)
                {
                    (MessageId id, Message message) = _queue.Dequeue();
                    _sent.Add(id, message);
                    return Task.FromResult((id, message));
                }

                _completionSource = new TaskCompletionSource<(MessageId, Message)>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _completionSource.Task;
            }
        }

        public void Cancel() => Volatile.Read(ref _completionSource)?.TrySetCanceled();
    }
}