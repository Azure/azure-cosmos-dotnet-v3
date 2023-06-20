//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ServiceFramework.Core
{
    using System;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// Encapsulates a system mutex that can be used for inter-process synchronization of operations.
    /// </summary>
    internal sealed class SystemSynchronizationScope : IDisposable
    {
        private readonly Mutex mutex;
        private readonly bool isOwned;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemSynchronizationScope"/> class.
        /// </summary>
        /// <param name="name">Name of the mutex.</param>
        /// <param name="timeout">Time to wait to acquire the mutex.</param>
        public SystemSynchronizationScope(string name, TimeSpan timeout = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name should not be null or empty", nameof(name));
            }

            this.MutexName = name;
            Mutex tempMutex = default;
            try
            {
                tempMutex = new Mutex(initiallyOwned: true, this.MutexName, out bool createdNew);
                if (!createdNew)
                {
                    DefaultTrace.TraceInformation($"{this.TraceId}: Acquiring existing system mutex '{this.MutexName}'");
                    try
                    {
                        timeout = timeout == default ? Timeout.InfiniteTimeSpan : timeout;
                        this.isOwned = tempMutex.WaitOne(timeout);
                        if (!this.isOwned)
                        {
                            throw new TimeoutException($"Timed out waiting for system mutex '{this.MutexName}'");
                        }

                        DefaultTrace.TraceInformation($"{this.TraceId}: Acquired existing system mutex '{this.MutexName}'");
                    }
                    catch (AbandonedMutexException amEx)
                    {
                        DefaultTrace.TraceWarning($"{this.TraceId}: {nameof(AbandonedMutexException)} waiting for mutex '{this.MutexName}': {amEx}");
                        this.isOwned = true;
                    }
                }
                else
                {
                    this.isOwned = true;
                    DefaultTrace.TraceInformation($"{this.TraceId}: Created system mutex '{this.MutexName}'");
                }

                this.mutex = tempMutex;
                tempMutex = default;
            }
            finally
            {
                this.ReleaseAndDisposeMutexSave(tempMutex);
            }
        }

        /// <summary>
        /// Gets the name of the system mutex.
        /// </summary>
        public string MutexName { get; }

        private string TraceId => $"{nameof(SystemSynchronizationScope)}[{Environment.CurrentManagedThreadId}]";

        /// <summary>
        /// Creates a synchronization object based on a mutex of the given name to guarantee that the code within the scope executes synchronously
        /// across processes.
        /// </summary>
        /// <param name="name">Name of the scope mutex.</param>
        /// <param name="timeout">Time to wait to acquire the mutex.</param>
        /// <returns>Object which releases the scope mutex when disposed.</returns>
        public static SystemSynchronizationScope CreateSynchronizationScope(string name, TimeSpan timeout = default)
            => new SystemSynchronizationScope(name, timeout);

        /// <summary>
        /// Executes an operation within a system mutex synchronization scope.
        /// </summary>
        /// <param name="name">Name of the scope mutex.</param>
        /// <param name="function">Operation to be executed.</param>
        /// <param name="timeout">Time to wait to acquire the mutex.</param>
        /// <returns>Result of the <paramref name="function"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="function"/> is <c>null</c>.</exception>
        public static TResult ExecuteWithSynchronization<TResult>(string name, Func<TResult> function, TimeSpan timeout = default)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            using (SystemSynchronizationScope.CreateSynchronizationScope(name, timeout))
            {
                return function.Invoke();
            }
        }

        /// <summary>
        /// Executes an operation within a system mutex synchronization scope.
        /// </summary>
        /// <param name="name">Name of the scope mutex.</param>
        /// <param name="action">Operation to be executed.</param>
        /// <param name="timeout">Time to wait to acquire the mutex.</param>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="action"/> is <c>null</c>.</exception>
        public static void ExecuteWithSynchronization(string name, Action action, TimeSpan timeout = default)
        {
            SystemSynchronizationScope.ExecuteWithSynchronization(name,
                function: () =>
                {
                    action.Invoke();
                    return true;
                },
                timeout);
        }

        public void Dispose()
        {
            this.ReleaseAndDisposeMutexSave(this.mutex);
        }

        private void ReleaseAndDisposeMutexSave(Mutex mutex)
        {
            if (mutex != null)
            {
                try
                {
                    // If we already have the mutex then release it.
                    if (this.isOwned)
                    {
                        DefaultTrace.TraceInformation($"{this.TraceId}: Releasing system mutex '{this.MutexName}'");
                        mutex.ReleaseMutex();
                    }
                }
                catch (AbandonedMutexException amEx)
                {
                    DefaultTrace.TraceWarning($"{this.TraceId}: {nameof(AbandonedMutexException)} waiting for mutex '{this.MutexName}': {amEx}");
                }
                catch (ApplicationException appEx)
                {
                    DefaultTrace.TraceWarning($"{this.TraceId}: Exception releasing system mutex '{this.MutexName}': {appEx}");
                }

                mutex.Dispose();
            }
        }
    }
}
