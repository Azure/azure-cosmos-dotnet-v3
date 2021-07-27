//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Concurrent;
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

    internal sealed class SystemUsageMonitor : SystemUsageMonitorBase
    {
        private readonly int pollDelayInMilliSeconds;
        private readonly Stopwatch watch = new Stopwatch();
        private readonly IDictionary<string, CpuAndMemoryUsageRecorder> recorders = new Dictionary<string, CpuAndMemoryUsageRecorder>();

        private CancellationTokenSource cancellation;

        private Task periodicTask { set; get; }
        private bool disposed { set; get; }

        internal override int PollDelayInMs => pollDelayInMilliSeconds;
        public override bool IsRunning() => (this.periodicTask.Status == TaskStatus.Running);

        internal override bool TryGetBackgroundTaskException(out AggregateException aggregateException)
        {
            aggregateException = this.periodicTask?.Exception;
            return aggregateException != null;
        }

        internal SystemUsageMonitor(IReadOnlyList<CpuAndMemoryUsageRecorder> recorders) 
        {
            if (recorders.Count == 0)
            {
                throw new ArgumentException("No Recorders are configured so nothing to process");
            }

            int pollDelay = 0;
            foreach (CpuAndMemoryUsageRecorder recorder in recorders)
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

        public override void Start()
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
        public override void Stop()
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

        public override CpuAndMemoryUsageRecorder GetRecorder(string recorderKey)
        {
            this.ThrowIfDisposed();

            if (this.periodicTask == null)
            {
                DefaultTrace.TraceError(nameof(SystemUsageMonitor) + " is not started");
                throw new InvalidOperationException(nameof(SystemUsageMonitor) + " was not started");
            }
            
            return this.recorders.TryGetValue(recorderKey, out CpuAndMemoryUsageRecorder recorder) ? 
                recorder : 
                throw  new ArgumentException("Recorder Identifier not present i.e. " + recorderKey); 
        }

        public override void Dispose()
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
        
        private void RefreshLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!this.watch.IsRunning)
                {
                    this.watch.Start();
                }

                foreach(CpuAndMemoryUsageRecorder recorder in this.recorders.Values)
                {
                    recorder.RecordUsage(systemUtilizationReader, this.watch);
                }

                Task.Delay(this.pollDelayInMilliSeconds).Wait();
            }
        }
    }
}