using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace ReconArt.Synchronization
{
    /// <summary>
    /// Limits the number of threads that can access an object-scoped resource concurrently.
    /// </summary>
    public sealed class ObjectLock : IDisposable
    {
#if NETCOREAPP1_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        private static readonly ReaderWriterLockSlim s_syncLock = new ReaderWriterLockSlim();
#else
        private static readonly ReaderWriterLock s_syncLock = new ReaderWriterLock();
#endif
        private static readonly ConcurrentDictionary<(Type, string), Lock> s_locks = new ConcurrentDictionary<(Type, string), Lock>();
        private static bool _backgroundWorkerStarted;
        private readonly (Type, string) _key;
        private bool _isDisposed;

        private class Lock
        {
            internal int Instances;
            internal SemaphoreSlim Semaphore;

            internal Lock()
            {
                Semaphore = new SemaphoreSlim(1, 1);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectLock"/> class.
        /// </summary>
        /// <param name="objectType">Type of the object to associate with this lock.</param>
        /// <param name="resourceIdentifier">Unique identifier for the resource being locked.</param>
        /// 
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="objectType"/> is null.</exception>
        internal ObjectLock([DisallowNull] Type objectType, [DisallowNull] string resourceIdentifier)
        {
            if (objectType is null)
            {
                throw new ArgumentNullException(nameof(objectType));
            }

            if (string.IsNullOrWhiteSpace(resourceIdentifier))
            {
                if (resourceIdentifier is null)
                {
                    throw new ArgumentNullException(nameof(resourceIdentifier));
                }

                throw new ArgumentException("Argument cannot be an empty string, or string consisting of only whitespace characters.", nameof(resourceIdentifier));
            }

            InitializeLock(_key = (objectType, resourceIdentifier));
            StartBackgroundWorker();
        }

        /// <summary>
        /// Asynchronously waits to enter the lock.
        /// </summary>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> to observe while waiting for the lock.</param>
        /// <returns>A task that represents the asynchronous wait operation.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the current instance has already been disposed.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> was canceled.</exception>
        public Task WaitAsync(CancellationToken cancellationToken = default) =>
            WaitAsync(Timeout.InfiniteTimeSpan, cancellationToken);

        /// <summary>
        /// Asynchronously waits to enter the lock, with a specified timeout and cancellation token.
        /// </summary>
        /// <param name="millisecondsTimeout">Amount of time to wait for the lock.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> to observe while waiting for the lock.</param>
        /// <returns>A task that will complete with a result of true if the lock was entered within the specified timeout; otherwise, false.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the current instance has already been disposed.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> was canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="millisecondsTimeout"/> is a negative number other than -1 milliseconds, which represents an infinite time-out,
        /// or it is greater than <see cref="Int32.MaxValue"/>.</exception>
        public Task<bool> WaitAsync(int millisecondsTimeout, CancellationToken cancellationToken = default) =>
            WaitAsync(millisecondsTimeout == Timeout.Infinite ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(millisecondsTimeout), cancellationToken);

        /// <summary>
        /// Asynchronously waits to enter the lock, with a specified timeout and cancellation token.
        /// </summary>
        /// <param name="timeout">Amount of time to wait for the lock.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> to observe while waiting for the lock.</param>
        /// <returns>A task that will complete with a result of true if the lock was entered within the specified timeout; otherwise, false.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the current instance has already been disposed.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> was canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeout"/> is a negative number other than -1 milliseconds, which represents an infinite time-out,
        /// or it is greater than <see cref="Int32.MaxValue"/>.</exception>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            long totalMilliseconds = (long)timeout.TotalMilliseconds;
            if (totalMilliseconds < -1 || totalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeout), timeout, "The timeout must represent a value between -1 and Int32.MaxValue, inclusive.");
            }

            ThrowIfDisposed();
            return s_locks[_key].Semaphore.WaitAsync(timeout, cancellationToken);
        }

        /// <summary>
        /// Blocks the current thread until it can enter the lock, while observing a <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> to observe.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the current instance has already been disposed.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> was canceled.</exception>
        public void Wait(CancellationToken cancellationToken = default) =>
            Wait(Timeout.InfiniteTimeSpan, cancellationToken);

        /// <summary>
        /// Blocks the current thread until it can enter the lock, using a <see cref="TimeSpan"/> to measure the time interval.
        /// </summary>
        /// <param name="millisecondsTimeout">Amount of time to wait for the lock.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> to observe.</param>
        /// <returns>true if the current thread successfully entered the lock; otherwise, false.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the current instance has already been disposed.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> was canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="millisecondsTimeout"/> is a negative number other than -1 milliseconds, which represents an infinite time-out,
        /// or it is greater than <see cref="Int32.MaxValue"/>.</exception>
        public bool Wait(int millisecondsTimeout, CancellationToken cancellationToken = default) =>
            Wait(millisecondsTimeout == Timeout.Infinite ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(millisecondsTimeout), cancellationToken);

        /// <summary>
        /// Blocks the current thread until it can enter the lock, using a <see cref="TimeSpan"/> to measure the time interval, while observing a <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="timeout">Amount of time to wait for the lock.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> to observe.</param>
        /// <returns>true if the current thread successfully entered the lock; otherwise, false.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the current instance has already been disposed.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the <paramref name="cancellationToken"/> was canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeout"/> is a negative number other than -1 milliseconds, which represents an infinite time-out,
        /// or it is greater than <see cref="Int32.MaxValue"/>.</exception>
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            long totalMilliseconds = (long)timeout.TotalMilliseconds;
            if (totalMilliseconds < -1 || totalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeout), timeout, "The timeout must represent a value between -1 and Int32.MaxValue, inclusive.");
            }

            ThrowIfDisposed();
            return s_locks[_key].Semaphore.Wait(timeout, cancellationToken);
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the current instance has already been disposed.</exception>
        /// <exception cref="SemaphoreFullException">Thrown when the semaphore is already at its maximum count.</exception>
        public void Release()
        {
            ThrowIfDisposed();
            s_locks[_key].Semaphore.Release();
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="ObjectLock"/> class.
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                Interlocked.Decrement(ref s_locks[_key].Instances);

                _isDisposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(ObjectLock));
            }
        }

        private static void InitializeLock((Type, string) key)
        {
#if NETCOREAPP1_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            s_syncLock.EnterReadLock();
#else
            s_syncLock.AcquireReaderLock(Timeout.Infinite);
#endif
            try
            {
                Interlocked.Increment(ref s_locks.GetOrAdd(key, key => new Lock()).Instances);
            }
            finally
            {
#if NETCOREAPP1_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                s_syncLock.ExitReadLock();
#else
                s_syncLock.ReleaseReaderLock();
#endif
            }
        }

        private static void StartBackgroundWorker()
        {
            if (!Volatile.Read(ref _backgroundWorkerStarted))
            {
                bool lockTaken = false;
                try
                {
                    Monitor.TryEnter(s_syncLock, ref lockTaken);
                    if (lockTaken && !Volatile.Read(ref _backgroundWorkerStarted))
                    {
                        _ = Task.Run(TrimLocksAsync);
                        Volatile.Write(ref _backgroundWorkerStarted, true);
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(s_syncLock);
                    }
                }
            }
        }

        private static async Task TrimLocksAsync()
        {
            try
            {
                DateTime lastTrim = DateTime.UtcNow;
                while (true)
                {
                    // Wait 30 seconds before checking if conditions for trimming are met.
                    await Task.Delay(30 * 1000).ConfigureAwait(false);

                    // Trim locks if there are more than 50,000 locks, or it has been more than 24 hours since the last trim.
                    if (s_locks.Count >= 50000 || lastTrim.AddHours(24) <= DateTime.UtcNow)
                    {
#if NETCOREAPP1_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                        s_syncLock.EnterUpgradeableReadLock();
#else
                        s_syncLock.AcquireReaderLock(Timeout.Infinite);
#endif
                        try
                        {
                            foreach (KeyValuePair<(Type, string), Lock> lockEntry in s_locks)
                            {
                                if (Volatile.Read(ref lockEntry.Value.Instances) == 0)
                                {
#if NETCOREAPP1_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                                    s_syncLock.EnterWriteLock();
#else
                                    LockCookie lc = s_syncLock.UpgradeToWriterLock(Timeout.Infinite);
#endif
                                    try
                                    {
                                        // Because we use a readable lock for getting **OR ADDING** a HashLock, it's possible the instances value had changed.
                                        if (Volatile.Read(ref lockEntry.Value.Instances) == 0)
                                        {
                                            if (s_locks.TryRemove(lockEntry.Key, out Lock? removedEntry))
                                            {
                                                removedEntry.Semaphore.Dispose();
                                            }
                                        }
                                    }
                                    finally
                                    {
#if NETCOREAPP1_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                                        s_syncLock.ExitWriteLock();
#else
                                        s_syncLock.DowngradeFromWriterLock(ref lc);
#endif
                                    }
                                }
                            }
                        }
                        finally
                        {
#if NETCOREAPP1_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                            s_syncLock.ExitUpgradeableReadLock();
#else
                            s_syncLock.ReleaseReaderLock();
#endif
                        }

                        lastTrim = DateTime.UtcNow;
                    }
                }
            }
            finally
            {
                Volatile.Write(ref _backgroundWorkerStarted, false);
            }
        }
    }
}
