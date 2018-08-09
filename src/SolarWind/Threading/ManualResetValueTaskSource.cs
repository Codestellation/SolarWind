// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The code is fairly stolen from corefx repo and received some patches.
// See https://github.com/dotnet/corefx/blob/df43abbed58fa534a36ad1840ff597efc7b00f85/src/Common/tests/System/Threading/Tasks/Sources/ManualResetValueTaskSource.cs

using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

// I'd rather know why this was place in such a namespace? Is there any black magic involved?
namespace System.Runtime.CompilerServices
{
    public interface IStrongBox<T>
    {
        ref T Value { get; }
    }
}

namespace Codestellation.SolarWind.Threading
{
    /// <summary>
    ///     <remarks>This class must be used with an instance per awaiter. So if it's expected to have multiple awaiters simultaneously - use object pool or create new instances</remarks>
    /// </summary>
    public sealed class AutoResetValueTaskSource<T> : IStrongBox<AutoResetValueTaskSourceLogic<T>>, IValueTaskSource<T>, IValueTaskSource
    {
        private AutoResetValueTaskSourceLogic<T> _logic; // mutable struct; do not make this readonly
        private readonly Action _cancellationCallback;

        public AutoResetValueTaskSource()
        {
            _logic = new AutoResetValueTaskSourceLogic<T>(this);
            _cancellationCallback = SetCanceled;
        }

        public short Version => _logic.Version;

        public void SetResult(in T result)
        {
            //If monitor is entered by someone else - we are late, so simply drop results
            if (Monitor.TryEnter(_cancellationCallback))
            {
                _logic.SetResult(result);
                Monitor.Exit(_cancellationCallback);
            }
        }

        public void SetException(Exception error)
        {
            //If monitor is entered by someone else - we are late, so simply drop results
            if (Monitor.TryEnter(_cancellationCallback))
            {
                _logic.SetException(error);
                Monitor.Exit(_cancellationCallback);
            }
        }

        public void SetCanceled() => SetException(new TaskCanceledException());

        public T GetResult(short token) => _logic.GetResult(token);

        void IValueTaskSource.GetResult(short token) => _logic.GetResult(token);

        public ValueTaskSourceStatus GetStatus(short token) => _logic.GetStatus(token);

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags) => _logic.OnCompleted(continuation, state, token, flags);

        ref AutoResetValueTaskSourceLogic<T> IStrongBox<AutoResetValueTaskSourceLogic<T>>.Value => ref _logic;

        public bool IsBeingAwaited => _logic.IsBeingAwaited;

        public ValueTask<T> AwaitValue(CancellationToken cancellation)
        {
            CancellationTokenRegistration? registration = cancellation == CancellationToken.None
                ? (CancellationTokenRegistration?)null
                : cancellation.Register(_cancellationCallback);
            return _logic.AwaitValue(this, registration);
        }

        public ValueTask AwaitVoid(CancellationToken cancellation)
        {
            CancellationTokenRegistration? registration = cancellation == CancellationToken.None
                ? (CancellationTokenRegistration?)null
                : cancellation.Register(_cancellationCallback);
            return _logic.AwaitVoid(this, registration);
        }
    }

    public struct AutoResetValueTaskSourceLogic<TResult>
    {
        private static readonly Action<object> s_sentinel = s => throw new InvalidOperationException();

        private readonly IStrongBox<AutoResetValueTaskSourceLogic<TResult>> _parent;
        private Action<object> _continuation;
        private object _continuationState;
        private object _capturedContext;
        private ExecutionContext _executionContext;
        private bool _completed;
        private TResult _result;
        private ExceptionDispatchInfo _error;
        private short _version;
        private bool _isBeingAwaited;
        private CancellationTokenRegistration? _registration;

        public AutoResetValueTaskSourceLogic(IStrongBox<AutoResetValueTaskSourceLogic<TResult>> parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _continuation = null;
            _continuationState = null;
            _capturedContext = null;
            _executionContext = null;
            _completed = false;
            _result = default;
            _error = null;
            _version = 0;
            _isBeingAwaited = false;
            _registration = null;
        }

        public short Version => _version;
        public bool IsBeingAwaited => _isBeingAwaited;

        private void ValidateToken(short token)
        {
            if (token != _version)
            {
                throw new InvalidOperationException();
            }
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            ValidateToken(token);

            return
                !_completed ? ValueTaskSourceStatus.Pending :
                _error == null ? ValueTaskSourceStatus.Succeeded :
                _error.SourceException is OperationCanceledException ? ValueTaskSourceStatus.Canceled :
                ValueTaskSourceStatus.Faulted;
        }

        public TResult GetResult(short token)
        {
            ValidateToken(token);

            if (!_completed)
            {
                throw new InvalidOperationException();
            }

            TResult result = _result;
            ExceptionDispatchInfo error = _error;
            Reset();

            error?.Throw();
            return result;
        }

        private void Reset()
        {
            _version++;

            _registration?.Dispose();

            _completed = false;
            _continuation = null;
            _continuationState = null;
            _result = default;
            _error = null;
            _executionContext = null;
            _capturedContext = null;
            _isBeingAwaited = false;
            _registration = null;
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }

            ValidateToken(token);

            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            {
                _executionContext = ExecutionContext.Capture();
            }

            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                SynchronizationContext sc = SynchronizationContext.Current;
                if (sc != null && sc.GetType() != typeof(SynchronizationContext))
                {
                    _capturedContext = sc;
                }
                else
                {
                    TaskScheduler ts = TaskScheduler.Current;
                    if (ts != TaskScheduler.Default)
                    {
                        _capturedContext = ts;
                    }
                }
            }

            _continuationState = state;
            if (Interlocked.CompareExchange(ref _continuation, continuation, null) != null)
            {
                _executionContext = null;

                object cc = _capturedContext;
                _capturedContext = null;

                switch (cc)
                {
                    case null:
                        Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                        break;

                    case SynchronizationContext sc:
                        sc.Post(s =>
                        {
                            var tuple = (Tuple<Action<object>, object>)s;
                            tuple.Item1(tuple.Item2);
                        }, Tuple.Create(continuation, state));
                        break;

                    case TaskScheduler ts:
                        Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                        break;
                }
            }
        }

        public void SetResult(in TResult result)
        {
            _result = result;
            SignalCompletion();
        }

        public void SetException(Exception error)
        {
            _error = ExceptionDispatchInfo.Capture(error);
            SignalCompletion();
        }

        private void SignalCompletion()
        {
            if (_completed)
            {
                throw new InvalidOperationException();
            }

            _completed = true;

            if (Interlocked.CompareExchange(ref _continuation, s_sentinel, null) != null)
            {
                if (_executionContext != null)
                {
                    ExecutionContext.Run(
                        _executionContext,
                        s => ((IStrongBox<AutoResetValueTaskSourceLogic<TResult>>)s).Value.InvokeContinuation(),
                        _parent ?? throw new InvalidOperationException());
                }
                else
                {
                    InvokeContinuation();
                }
            }
        }

        private void InvokeContinuation()
        {
            object cc = _capturedContext;
            _capturedContext = null;

            switch (cc)
            {
                case null:
                    _continuation(_continuationState);
                    break;

                case SynchronizationContext sc:
                    sc.Post(s =>
                        {
                            ref AutoResetValueTaskSourceLogic<TResult> logicRef = ref ((IStrongBox<AutoResetValueTaskSourceLogic<TResult>>)s).Value;
                            logicRef._continuation(logicRef._continuationState);
                        }, _parent ?? throw new InvalidOperationException());
                    break;

                case TaskScheduler ts:
                    Task.Factory.StartNew(_continuation, _continuationState, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                    break;
            }
        }

        public ValueTask<T> AwaitValue<T>(IValueTaskSource<T> source, CancellationTokenRegistration? registration)
        {
            _isBeingAwaited = true;
            _registration = registration;
            return new ValueTask<T>(source, _version);
        }

        public ValueTask AwaitVoid(IValueTaskSource source, CancellationTokenRegistration? registration)
        {
            _isBeingAwaited = true;
            _registration = registration;
            return new ValueTask(source, _version);
        }
    }
}