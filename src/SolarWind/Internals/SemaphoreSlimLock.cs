using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Codestellation.SolarWind.Internals
{
    public readonly struct SemaphoreSlimLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly bool _lockTaken;

        private SemaphoreSlimLock(SemaphoreSlim semaphore, bool lockTaken)
        {
            _semaphore = semaphore;
            _lockTaken = lockTaken;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_lockTaken)
            {
                _semaphore.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SemaphoreSlimLock Lock(SemaphoreSlim semaphore)
        {
            semaphore.Wait();
            return new SemaphoreSlimLock(semaphore, true);
        }

        public static SemaphoreSlimLock Lock(SemaphoreSlim semaphore, int msTimeout) => new SemaphoreSlimLock(semaphore, semaphore.Wait(msTimeout));
    }
}