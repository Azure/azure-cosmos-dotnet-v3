//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    // This class is thread safe.
    internal sealed class CpuMonitor : IDisposable
    {
        internal const int DefaultRefreshIntervalInSeconds = 10;
        private const int HistoryLength = 6;
        private static TimeSpan refreshInterval =
            TimeSpan.FromSeconds(DefaultRefreshIntervalInSeconds);

        private bool disposed = false;

        private readonly ReaderWriterLockSlim rwLock =
            new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private CancellationTokenSource cancellation;  // Guarded by rwLock.

        // CpuMonitor users get a copy of the internal buffer to avoid racing
        // against changes.
        private CpuLoadHistory currentReading;  // Guarded by rwLock.

        private Task periodicTask;  // Guarded by rwLock.

        // Allows tests to override the default refresh interval
        internal static void OverrideRefreshInterval(TimeSpan newRefreshInterval)
        {
            CpuMonitor.refreshInterval = newRefreshInterval;
        }

        public void Start()
        {
            this.ThrowIfDisposed();
            this.rwLock.EnterWriteLock();
            try
            {
                this.ThrowIfDisposed();
                if (this.periodicTask != null)
                {
                    throw new InvalidOperationException("CpuMonitor already started");
                }
                this.cancellation = new CancellationTokenSource();
                CancellationToken cancellationToken = this.cancellation.Token;
                this.periodicTask = Task.Factory.StartNew(
                    () => this.RefreshLoopAsync(cancellationToken),
                    cancellationToken,
                    TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default).Unwrap();
                Task ignored0 = this.periodicTask.ContinueWith(
                    t =>
                    {
                        DefaultTrace.TraceError(
                            "The CPU monitor refresh task failed. Exception: {0}",
                            t.Exception);
                    },
                    TaskContinuationOptions.OnlyOnFaulted);
                Task ignored1 = this.periodicTask.ContinueWith(t =>
                {
                    DefaultTrace.TraceInformation(
                        "The CPU monitor refresh task stopped. Status: {0}",
                        t.Status);
                },
                TaskContinuationOptions.NotOnFaulted);
            }
            finally
            {
                this.rwLock.ExitWriteLock();
            }
            DefaultTrace.TraceInformation("CpuMonitor started");
        }

        private void StopCoreUnderWriteLock(ref CancellationTokenSource cancel, ref Task backgroundTask)
        {
            if (this.periodicTask == null)
            {
                throw new InvalidOperationException("CpuMonitor not started or has been stopped or disposed already.");
            }

            cancel = this.cancellation;
            backgroundTask = this.periodicTask;

            this.cancellation = null;
            this.currentReading = null;
            this.periodicTask = null;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Synchronously waiting on tasks or awaiters may cause deadlocks. Use await or JoinableTaskFactory.Run instead.", Justification = "Cannot make static helper method async - will be fixed later")]
        private static void StopCoreAfterReleasingWriteLock(CancellationTokenSource cancel, Task backgroundTask)
        {
            cancel.Cancel();
            try
            {
                backgroundTask.Wait();
            }
            catch (AggregateException)
            { }
            cancel.Dispose();
        }

        public void Stop()
        {
            this.ThrowIfDisposed();
            CancellationTokenSource cancel = null;
            Task backgroundTask = null;
            this.rwLock.EnterWriteLock();
            try
            {
                this.ThrowIfDisposed();
                this.StopCoreUnderWriteLock(ref cancel, ref backgroundTask);
            }
            finally
            {
                this.rwLock.ExitWriteLock();
            }

            StopCoreAfterReleasingWriteLock(cancel, backgroundTask);

            DefaultTrace.TraceInformation("CpuMonitor stopped");
        }

        // Returns a read-only collection of CPU load measurements, or null if
        // no results are available yet.
        public CpuLoadHistory GetCpuLoad()
        {
            this.ThrowIfDisposed();
            this.rwLock.EnterReadLock();
            try
            {
                if (this.periodicTask == null)
                {
                    throw new InvalidOperationException("CpuMonitor was not started or has been stopped or disposed already.");
                }
                return this.currentReading;
            }
            finally
            {
                this.rwLock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            Interlocked.MemoryBarrier();
            if (this.disposed)
            {
                return;
            }

            CancellationTokenSource cancel = null;
            Task backgroundTask = null;
            this.rwLock.EnterWriteLock();
            try
            {
                Interlocked.MemoryBarrier();
                if (this.disposed)
                {
                    return;
                }

                if (this.periodicTask != null)
                {
                    this.StopCoreUnderWriteLock(ref cancel, ref backgroundTask);
                }
            }
            finally
            {
                this.MarkDisposed();
                this.rwLock.ExitWriteLock();
            }

            if (backgroundTask != null)
            {
                StopCoreAfterReleasingWriteLock(cancel, backgroundTask);
            }

            this.rwLock.Dispose();
        }

        private void MarkDisposed()
        {
            this.disposed = true;
            Interlocked.MemoryBarrier();
        }

        private void ThrowIfDisposed()
        {
            Interlocked.MemoryBarrier();
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(CpuMonitor));
            }
        }

        private async Task RefreshLoopAsync(CancellationToken cancellationToken)
        {
            Trace.CorrelationManager.ActivityId = Guid.NewGuid();

            SystemUtilizationReaderBase cpuReader = SystemUtilizationReaderBase.SingletonInstance;

            CpuLoad[] buffer = new CpuLoad[CpuMonitor.HistoryLength];
            int clockHand = 0;

            TaskCompletionSource<object> cancellationCompletion =
                new TaskCompletionSource<object>();
            cancellationToken.Register(() => { cancellationCompletion.SetCanceled(); });

            Task[] refreshTasks = new Task[2];
            refreshTasks[1] = cancellationCompletion.Task;

            // Increasing nextExpiration in fixed increments helps maintain
            // timer cadence regardless of scheduling delays.
            DateTime nextExpiration = DateTime.UtcNow;
            while (!cancellationToken.IsCancellationRequested)
            {
                DateTime now = DateTime.UtcNow;
                float currentUtilization = cpuReader.GetSystemWideCpuUsage();

                if (!float.IsNaN(currentUtilization))
                {
                    List<CpuLoad> cpuLoadHistory = new List<CpuLoad>(buffer.Length);
                    CpuLoadHistory newReading = new CpuLoadHistory(
                        new ReadOnlyCollection<CpuLoad>(cpuLoadHistory),
                        CpuMonitor.refreshInterval);

                    buffer[clockHand] = new CpuLoad(now, currentUtilization);
                    clockHand = (clockHand + 1) % buffer.Length;

                    for (int i = 0; i < buffer.Length; i++)
                    {
                        int index = (clockHand + i) % buffer.Length;
                        if (buffer[index].Timestamp != DateTime.MinValue)
                        {
                            cpuLoadHistory.Add(buffer[index]);
                        }
                    }

                    this.rwLock.EnterWriteLock();
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        this.currentReading = newReading;
                    }
                    finally
                    {
                        this.rwLock.ExitWriteLock();
                    }
                }

                // Skip missed beats.
                now = DateTime.UtcNow;
                while (nextExpiration <= now)
                {
                    nextExpiration += CpuMonitor.refreshInterval;
                }
                TimeSpan delay = nextExpiration - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    refreshTasks[0] = Task.Delay(delay);
                    Task completedTask = await Task.WhenAny(refreshTasks);
                    if (object.ReferenceEquals(completedTask, refreshTasks[1]))
                    {
                        // Cancelled.
                        break;
                    }
                }
            }
        }
    }
}