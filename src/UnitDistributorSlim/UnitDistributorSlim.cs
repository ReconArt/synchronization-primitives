using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using ReconArt.Collections;

namespace ReconArt.Synchronization
{
    /// <summary>
    /// Allows multi-threaded access to a shared resource of limited supply by distributing that supply fairly. Supports priority-based distribution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="UnitDistributorSlim"/> class provides the means to acquiring a set amount of units for a specific, limited resource.
    /// </para>
    /// <para>
    /// The slim variant is optimized for working only with 6 priorities: from 0 to 5.
    /// </para>
    /// </remarks>
    public sealed class UnitDistributorSlim
    {
        private readonly object _syncLock = new();
        private readonly PriorityQueue<UnitRequest> _waitingLine = new();
        private readonly uint _maxSupply;
        private volatile uint _currentSupply;

        private readonly struct UnitRequest
        {
            internal UnitRequest(in uint units, in uint priority)
            {
                Units = units;
                Priority = priority;
                Source = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public uint Units { get; }

            public uint Priority { get; }

            public Task<bool> Awaiter => Source.Task;

            internal TaskCompletionSource<bool> Source { get; }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="UnitDistributorSlim"/> class, specifying
        /// the total supply that can be taken out.
        /// </summary>
        /// <param name="supply">Units the <see cref="UnitDistributorSlim"/> will distribute.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="supply"/> is 0.</exception>
        public UnitDistributorSlim(uint supply)
        {
            if (supply == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(supply), supply, "Value cannot be 0.");
            }

            _currentSupply = supply;
            _maxSupply = supply;
        }

        /// <summary>
        /// Gets the available units that can be given out by the <see cref="UnitDistributorSlim"/>.
        /// </summary>
        /// <remarks>
        /// If there are waiters, the value shown here will only be available to a request if it has higher
        /// priority than all of the current waiters in the queue.
        /// </remarks>
        public uint AvailableSupply => _currentSupply;

        /// <summary>
        /// Attempts to take the specified amount of <paramref name="units"/> from the <see cref="UnitDistributorSlim"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If there are available units to give, the method immediately returns. <br/>
        /// Otherwise, the method will asynchronously wait for enough units to be made available, by stepping into a queue of other waiters.
        /// </para>
        /// <para>
        /// Order in the queue is preserved, but priority takes precedence. <br/>
        /// As such, it's possible for this method to never return in cases where requests with higher priority
        /// continue to flood the <see cref="UnitDistributorSlim"/>, as they will be handled first.
        /// </para>
        /// </remarks>
        /// <param name="units">Units to take out.</param>
        /// <param name="priority">A number between 0 and 5 denoting priority. Lower is better.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="units"/> is 0 or larger than the units shared in <see cref="UnitDistributorSlim"/>
        /// -or- <paramref name="priority"/> is not a number between 0 and 5.</exception>
        /// <returns>A task that will complete when the units have been taken.</returns>
        public Task TryTakeAsync(uint units, uint priority, CancellationToken cancellationToken = default) =>
            TryTakeAsync(units, priority, Timeout.Infinite, cancellationToken);

        /// <summary>
        /// Attempts to take the specified amount of <paramref name="units"/> from the <see cref="UnitDistributorSlim"/> in the specified time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If there are available units to give, the method immediately returns. <br/>
        /// Otherwise, the method will asynchronously wait for enough units to be made available, by stepping into a queue of other waiters.
        /// </para>
        /// <para>
        /// Order in the queue is preserved, but priority takes precedence. <br/>
        /// As such, it's possible for this method to never return in cases where requests with higher priority
        /// continue to flood the <see cref="UnitDistributorSlim"/>, as they will be handled first.
        /// </para>
        /// </remarks>
        /// <param name="units">Units to take out.</param>
        /// <param name="priority">A number between 0 and 5 denoting priority. Lower is better.</param>
        /// <param name="timeout">
        /// A <see cref="System.TimeSpan"/> that represents the number of milliseconds
        /// to wait, or a <see cref="System.TimeSpan"/> that represents -1 milliseconds to wait indefinitely.
        /// </param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="units"/> is 0 or larger than the units shared in <see cref="UnitDistributorSlim"/>
        /// -or- <paramref name="timeout"/> is larger than <see cref="int.MaxValue"/> -or- <paramref name="priority"/> is not a number between 0 and 5.</exception>
        /// <returns>A task that will complete with a result of <see langword="true"/> if the current thread successfully
        ///  acquired the units it requested from the <see cref="UnitDistributorSlim"/>, otherwise with a result of <see langword="false"/>.</returns>
        public Task<bool> TryTakeAsync(uint units, uint priority, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            // Validate the timeout
            long totalMilliseconds = (long)timeout.TotalMilliseconds;
            if (totalMilliseconds < -1 || totalMilliseconds > int.MaxValue)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(timeout), timeout, $"Value cannot be larger than {int.MaxValue}");
            }

            return TryTakeAsync(units, priority, (int)timeout.TotalMilliseconds, cancellationToken);
        }

        /// <summary>
        /// Attempts to take the specified amount of <paramref name="units"/> from the <see cref="UnitDistributorSlim"/> in the specified time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If there are available units to give, the method immediately returns. <br/>
        /// Otherwise, the method will asynchronously wait for enough units to be made available, by stepping into a queue of other waiters.
        /// </para>
        /// <para>
        /// Order in the queue is preserved, but priority takes precedence. <br/>
        /// As such, it's possible for this method to never return in cases where requests with higher priority
        /// continue to flood the <see cref="UnitDistributorSlim"/>, as they will be handled first.
        /// </para>
        /// </remarks>
        /// <param name="units">Units to take out.</param>
        /// <param name="priority">A number between 0 and 5 denoting priority. Lower is better.</param>
        /// <param name="millisecondsTimeout">
        /// The number of milliseconds to wait, or <see cref="Timeout.Infinite"/>(-1) to wait indefinitely.
        /// </param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="units"/> is 0 or larger than the units shared in <see cref="UnitDistributorSlim"/>
        /// -or- <paramref name="millisecondsTimeout"/> is less than -1 -or- <paramref name="priority"/> is not a number between 0 and 5.</exception>
        /// <returns>A task that will complete with a result of <see langword="true"/> if the current thread successfully
        ///  acquired the units it requested from the <see cref="UnitDistributorSlim"/>, otherwise with a result of <see langword="false"/>.</returns>
        public Task<bool> TryTakeAsync(uint units, uint priority, int millisecondsTimeout, CancellationToken cancellationToken = default)
        {
            // Validate input
            if (units == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(units), units, "Value cannot be 0.");
            }

            if (units > _maxSupply)
            {
                throw new ArgumentOutOfRangeException(nameof(units), units,
                    $"Value cannot exceed the maximum allocated supply of {_maxSupply}.");
            }

            if (priority < 0 || priority > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(priority), priority, "Value must be a number between 0 and 5 (inclusive).");
            }

            if (millisecondsTimeout < -1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(millisecondsTimeout), millisecondsTimeout, "Value cannot be less than -1.");
            }

            // Bail early for cancellation
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<bool>(cancellationToken);

            lock (_syncLock)
            {
                // If there is supply available, allow this waiter to succeed
                // if there are waiters on the line, only allow this if its priority is the largest (the lower the better)
                if (_currentSupply >= units && (_waitingLine.Count == 0 || _waitingLine.Peek().Priority > priority))
                {
                    _currentSupply -= units;
                    return Task.FromResult(true);
                }
                else if (millisecondsTimeout == 0)
                {
                    // No supply, if timeout is zero fail fast
                    return Task.FromResult(false);
                }
                // If there isn't, create and return a task to the caller.
                // The task will be completed either when enough units havve been
                // released or when the timeout expired or cancellation was requested.
                else
                {
                    return GetEntryTaskAsync(units, priority, millisecondsTimeout, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Gives back the specified amount of units to the <see cref="UnitDistributorSlim"/>.
        /// </summary>
        /// <returns>The previous <see cref="AvailableSupply"/>.</returns>
        /// <param name="units">The units to return.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="units"/> is less than 1.</exception>
        /// <exception cref="UnitOverflowException">Units returned exceeded the maximum allocated supply of the <see cref="UnitDistributorSlim"/>.</exception>
        public uint Return(uint units)
        {
            // Validate input
            if (units == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(units), units, "Value cannot be less than 1.");
            }

            uint returnSupply;
            lock (_syncLock)
            {
                // Read the current supply into a local variable to avoid unnecessary volatile accesses inside the lock.
                uint currentSupply = _currentSupply;
                returnSupply = currentSupply;

                // If the units to return would exceed the maximum allocated supply, throw UnitOverflowException.
                if (_maxSupply < units + currentSupply)
                {
                    throw new UnitOverflowException();
                }

                // Increase the current supply by the amount specified
                currentSupply += units;
                UnitRequest request;

                // Check if we have waiters that can be signaled
                while (_waitingLine.Count > 0)
                {
                    request = _waitingLine.Peek();
                    if (request.Units <= currentSupply)
                    {
                        currentSupply -= request.Units;
                        _ = _waitingLine.Dequeue();

                        // Signal that units have been given
                        request.Source.TrySetResult(true);
                    }
                    else
                    {
                        // The next element in line needs more units than what we currently have,
                        // break to prevent starvation and preserve order
                        break;
                    }
                }

                _currentSupply = currentSupply;
            }

            // And return the supply
            return returnSupply;
        }

        #region Private_Methods

        private Task<bool> GetEntryTaskAsync(in uint units, in uint priority, in int millisecondsTimeout, in CancellationToken cancellationToken)
        {
            Debug.Assert(_currentSupply >= 0, $"{nameof(_currentSupply)} should never be negative");
            LinkedListNode<UnitRequest> node = CreateAndAddUnitRequest(units, priority);
            return (millisecondsTimeout == Timeout.Infinite && !cancellationToken.CanBeCanceled) ?
                node.Value.Awaiter :
                WaitUntilCountOrTimeoutAsync(node, millisecondsTimeout, cancellationToken);
        }

        private async Task<bool> WaitUntilCountOrTimeoutAsync(LinkedListNode<UnitRequest> node, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            Task<bool> awaiter = node.Value.Awaiter;
            Debug.Assert(awaiter != null, "Waiter should have been constructed");
            Debug.Assert(Monitor.IsEntered(_syncLock), "Requires the lock be held");

            await ((Task)awaiter.WaitAsync(TimeSpan.FromMilliseconds(millisecondsTimeout), cancellationToken))
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            if (cancellationToken.IsCancellationRequested)
            {
                // If we might be running as part of a cancellation callback, force the completion to be asynchronous
                // so as to maintain semantics similar to when no token is passed (neither Release nor Cancel would invoke
                // continuations off of this task).
                await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            }

            if (awaiter.IsCompleted)
            {
                return true; // successfully acquired
            }

            // The wait has timed out or been canceled.

            // If the await completed synchronously, we still hold the lock.  If it didn't,
            // we no longer hold the lock.  As such, acquire it.
            lock (_syncLock)
            {
                // Remove the request from the list. If we're successful in doing so,
                // we know that no one else has tried to complete this waiter yet,
                // so we can safely cancel or timeout.
                if (_waitingLine.TryRemove(node, node.Value.Priority))
                {
                    cancellationToken.ThrowIfCancellationRequested(); // cancellation occurred
                    return false; // timeout occurred
                }
            }

            // The waiter had already been removed, which means it's already completed or is about to
            // complete, so let it, and don't return until it does.
            return await awaiter.ConfigureAwait(false);
        }

        private LinkedListNode<UnitRequest> CreateAndAddUnitRequest(uint units, uint priority)
        {
            Debug.Assert(Monitor.IsEntered(_syncLock), "Requires the lock be held");

            // Create the unit request
            var request = new UnitRequest(units, priority);

            // Add it to the waiting line
            LinkedListNode<UnitRequest> node = _waitingLine.Enqueue(request, priority);

            // Hand back the node with which we'll be able to remove the request
            return node;
        }

#endregion
    }
}
