using System;
using System.Threading;

namespace Codestellation.SolarWind.Internals
{
    public struct SemaphoreSlimLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly bool _lockTaken;

        private SemaphoreSlimLock(SemaphoreSlim semaphore, bool lockTaken)
        {
            _semaphore = semaphore ?? throw new ArgumentNullException(nameof(semaphore));
            _lockTaken = lockTaken;
        }

        public void Dispose()
        {
            if (_lockTaken)
            {
                _semaphore.Release();
            }
        }

        public static SemaphoreSlimLock Lock(SemaphoreSlim semaphore)
        {
            semaphore.Wait();
            return new SemaphoreSlimLock(semaphore, true);
        }

        public static SemaphoreSlimLock Lock(SemaphoreSlim semaphore, int msTimeout) => new SemaphoreSlimLock(semaphore, semaphore.Wait(msTimeout));
    }
}