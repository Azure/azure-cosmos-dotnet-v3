//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Documents.Rntbd.SystemUsageMonitor;
#if NETSTANDARD15 || NETSTANDARD16
    using Trace = Microsoft.Azure.Documents.Trace;
#endif
    internal sealed class SystemUsageMonitor : IDisposable
    {
        private readonly SystemUtilizationReaderBase systemUtilizationReader = SystemUtilizationReaderBase.SingletonInstance;
        private readonly IDictionary<string, SystemUsageRecorder> recorders = new Dictionary<string, SystemUsageRecorder>();
        private readonly Stopwatch watch = new Stopwatch();

        private int pollDelayInMilliSeconds;
        private CancellationTokenSource cancellation;

        private Task periodicTask { set; get; }
        private bool disposed { set; get; }

        internal int PollDelayInMs => pollDelayInMilliSeconds;
        public bool IsRunning() => (this.periodicTask.Status == TaskStatus.Running);

        internal bool TryGetBackgroundTaskException(out AggregateException aggregateException)
        {
            aggregateException = this.periodicTask?.Exception;
            return aggregateException != null;
        }

        public static SystemUsageMonitor CreateAndStart(IReadOnlyList<SystemUsageRecorder> usageRecorders)
        {
            SystemUsageMonitor monitor = new SystemUsageMonitor(usageRecorders);
            monitor.Start();

            return monitor;
        }

        private SystemUsageMonitor(IReadOnlyList<SystemUsageRecorder> recorders)
        {
            if (recorders.Count == 0)
            {
                throw new ArgumentException("No Recorders are configured so nothing to process");
            }

            int pollDelay = 0;
            foreach (SystemUsageRecorder recorder in recorders)
            {
                this.recorders.Add(recorder.identifier, recorder);
                pollDelay = this.GCD((int)recorder.refreshInterval.TotalMilliseconds, pollDelay);
            }

            this.pollDelayInMilliSeconds = pollDelay;
        }

        /// <summary>
        /// This Function gives GCD of given 2 numbers.
        /// </summary>
        /// <param name="timeInterval1"></param>
        /// <param name="timeInterval2"></param>
        /// <returns></returns>
        private int GCD(int timeInterval1, int timeInterval2)
        {
            return timeInterval2 == 0 ?
                timeInterval1 :
                this.GCD(timeInterval2, timeInterval1 % timeInterval2);
        }

        private void Start()
        {
            this.ThrowIfDisposed();
            if (this.periodicTask != null)
            {
                throw new InvalidOperationException(nameof(SystemUsageMonitor) + " already started");
            }

            this.cancellation = new CancellationTokenSource();
            this.periodicTask = Task.Run(
                () => this.RefreshLoopAsync(this.cancellation.Token),
                this.cancellation.Token);

            this.periodicTask.ContinueWith(
                t =>
                {
                    DefaultTrace.TraceError(
                        "The CPU and Memory usage monitoring refresh task failed. Exception: {0}",
                        t.Exception);
                },
                TaskContinuationOptions.OnlyOnFaulted);

            this.periodicTask.ContinueWith(
                t =>
                {
                    DefaultTrace.TraceWarning(
                        "The CPU and Memory usage monitoring refresh task stopped. Status: {0}",
                        t.Status);
                },
                TaskContinuationOptions.NotOnFaulted);

            DefaultTrace.TraceInformation(nameof(SystemUsageMonitor) + " started");
        }

        /// <summary>
        /// Stop the Monitoring
        /// </summary>
        public void Stop()
        {
            this.ThrowIfDisposed();

            if (this.periodicTask == null)
            {
                throw new InvalidOperationException(nameof(SystemUsageMonitor) + " not running");
            }

            CancellationTokenSource cancel = this.cancellation;
            Task backgroundTask = this.periodicTask;

            this.watch.Stop();

            this.cancellation = null;
            this.periodicTask = null;

            cancel.Cancel();
            try
            {
                backgroundTask.Wait();
            }
            catch (AggregateException)
            { }
            cancel.Dispose();
            DefaultTrace.TraceInformation(nameof(SystemUsageMonitor) + " stopped");
        }

        public SystemUsageRecorder GetRecorder(string recorderKey)
        {
            this.ThrowIfDisposed();

            if (this.periodicTask == null)
            {
                DefaultTrace.TraceError(nameof(SystemUsageMonitor) + " is not started");
                throw new InvalidOperationException(nameof(SystemUsageMonitor) + " was not started");
            }

            return this.recorders.TryGetValue(recorderKey, out SystemUsageRecorder recorder) ?
                recorder :
                throw new ArgumentException("Recorder Identifier not present i.e. " + recorderKey);
        }

        public void Dispose()
        {
            this.ThrowIfDisposed();
            if (this.periodicTask != null)
            {
                this.Stop();
            }

            this.disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(SystemUsageMonitor));
            }

        }

        /// <summary>
        /// Keep Running and keep record the System Usage.
        /// </summary>
        /// <param name="cancellationToken"></param>
        private void RefreshLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!this.watch.IsRunning)
                {
                    this.watch.Start();
                }

                // reset value on each iteration
                Nullable<SystemUsageLoad> usageData = null;

                foreach (SystemUsageRecorder recorder in this.recorders.Values)
                {
                    if(recorder.IsEligibleForRecording(this.watch))
                    {
                        // Getting CPU and Memory Usage from utilization reader if its not there only first time, use this value for all the recorders
                        if (usageData == null)
                        {
                            DateTime now = DateTime.UtcNow;
                            usageData = new SystemUsageLoad(
                                timestamp: now, 
                                threadInfo: ThreadInformation.Get(), 
                                cpuUsage: systemUtilizationReader.GetSystemWideCpuUsage(), 
                                memoryAvailable: systemUtilizationReader.GetSystemWideMemoryAvailabilty(),
                                numberOfOpenTcpConnection: Connection.NumberOfOpenTcpConnections);
                        }

                        // record the above calculated usage if eligible
                        recorder.RecordUsage(usageData.Value, this.watch);
                    }
                }

                Task.Delay(this.pollDelayInMilliSeconds).Wait();
            }
        }

    }
}