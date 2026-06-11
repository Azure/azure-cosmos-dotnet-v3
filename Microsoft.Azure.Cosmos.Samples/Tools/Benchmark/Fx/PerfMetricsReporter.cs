//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Process-wide coordinator that accumulates per-operation latency/RU/error samples, captures
    /// per-window .NET runtime metrics, and flushes dashboard-schema <see cref="PerfResultsRecord"/>
    /// rows to the configured <see cref="IMetricsSink"/> at the metric reporting interval.
    /// </summary>
    /// <remarks>
    /// Exposed as an ambient singleton (<see cref="Current"/>) so the concurrent
    /// <see cref="SerialOperationExecutor"/> instances can record without threading the reporter
    /// through the execution-strategy API. When no sink is configured the reporter is never created
    /// and <see cref="Current"/> stays null, leaving the existing count-based behavior untouched.
    /// </remarks>
    internal sealed class PerfMetricsReporter
    {
        private readonly OperationWindowAggregator aggregator = new OperationWindowAggregator();
        private readonly RuntimeMetricsCollector runtimeCollector = new RuntimeMetricsCollector();
        private readonly IMetricsSink sink;
        private readonly PerfRunContext runContext;
        private readonly int reportingIntervalInSec;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Stopwatch windowStopwatch = new Stopwatch();

        private Task flushLoop;

        public PerfMetricsReporter(IMetricsSink sink, PerfRunContext runContext, int reportingIntervalInSec)
        {
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            this.runContext = runContext ?? throw new ArgumentNullException(nameof(runContext));
            this.reportingIntervalInSec = Math.Max(1, reportingIntervalInSec);
        }

        /// <summary>
        /// The ambient reporter for the current run, or null when no metrics sink is configured.
        /// </summary>
        public static PerfMetricsReporter Current { get; private set; }

        public void RecordSuccess(double latencyMs, double ruCharge)
        {
            this.aggregator.RecordSuccess(latencyMs, ruCharge);
        }

        public void RecordFailure(double latencyMs, double ruCharge, int statusCode, string errorMessage)
        {
            this.aggregator.RecordFailure(latencyMs, ruCharge, statusCode, errorMessage);
        }

        /// <summary>
        /// Starts the background window flush loop and publishes this instance as <see cref="Current"/>.
        /// </summary>
        public void Start()
        {
            Current = this;
            this.windowStopwatch.Restart();
            this.flushLoop = Task.Run(this.FlushLoopAsync);
        }

        /// <summary>
        /// Stops the flush loop, emits a final window, and flushes the sink.
        /// </summary>
        public async Task StopAndFlushAsync()
        {
            Current = null;
            this.cancellation.Cancel();

            if (this.flushLoop != null)
            {
                try
                {
                    await this.flushLoop;
                }
                catch (OperationCanceledException)
                {
                    // Expected on shutdown.
                }
            }

            await this.FlushWindowAsync();
            await this.sink.FlushAsync();
        }

        private async Task FlushLoopAsync()
        {
            while (!this.cancellation.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(this.reportingIntervalInSec), this.cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await this.FlushWindowAsync();
            }
        }

        private async Task FlushWindowAsync()
        {
            double windowSeconds = this.windowStopwatch.Elapsed.TotalSeconds;
            this.windowStopwatch.Restart();
            if (windowSeconds <= 0)
            {
                windowSeconds = this.reportingIntervalInSec;
            }

            OperationWindowSnapshot snapshot = this.aggregator.SnapshotAndReset(windowSeconds);

            // Capture runtime metrics every window so GC/CPU deltas stay window-aligned, even when
            // an empty window is skipped below.
            RuntimeMetricsSnapshot runtime = this.runtimeCollector.Capture();

            // Skip emitting empty windows (no successes and no errors) to avoid noise rows.
            if (snapshot.Count == 0 && snapshot.Errors == 0)
            {
                return;
            }

            PerfResultsRecord record = PerfResultsRecord.Build(
                timestampUtc: DateTime.UtcNow,
                runContext: this.runContext,
                window: snapshot,
                runtime: runtime);

            await this.sink.EmitAsync(new List<PerfResultsRecord> { record });
        }
    }
}
