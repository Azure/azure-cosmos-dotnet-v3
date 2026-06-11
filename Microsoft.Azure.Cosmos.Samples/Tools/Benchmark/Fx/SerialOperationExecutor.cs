//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using static CosmosBenchmark.TelemetrySpan;

    internal class SerialOperationExecutor : IExecutor
    {
        private readonly IBenchmarkOperation operation;
        private readonly string executorId;

        public SerialOperationExecutor(
            string executorId,
            IBenchmarkOperation benchmarkOperation)
        {
            this.executorId = executorId;
            this.operation = benchmarkOperation;

            this.SuccessOperationCount = 0;
            this.FailedOperationCount = 0;
        }

        public int SuccessOperationCount { get; private set; }
        public int FailedOperationCount { get; private set; }

        public double TotalRuCharges { get; private set; }

        public async Task ExecuteAsync(
                int iterationCount,
                bool isWarmup,
                bool traceFailures,
                Action completionCallback,
                BenchmarkConfig benchmarkConfig,
                DateTime? executionDeadline = null)
        {
            Trace.TraceInformation($"Executor {this.executorId} started");

            // When a perf metrics sink is configured, record per-operation latency / RU / error
            // samples into the ambient reporter so per-window dashboard rows can be emitted.
            PerfMetricsReporter reporter = isWarmup ? null : PerfMetricsReporter.Current;
            Stopwatch perfStopwatch = reporter != null ? new Stopwatch() : null;

            try
            {
                int currentIterationCount = 0;
                do
                {
                    OperationResult? operationResult = null;

                    await this.operation.PrepareAsync();

                    using (ITelemetrySpan telemetrySpan = TelemetrySpan.StartNew(
                                benchmarkConfig,
                                () => operationResult.Value,
                                disableTelemetry: isWarmup))
                    {
                        perfStopwatch?.Restart();
                        try
                        {
                            operationResult = await this.operation.ExecuteOnceAsync();
                            telemetrySpan.MarkSuccess();

                            // Success case
                            this.SuccessOperationCount++;
                            this.TotalRuCharges += operationResult.Value.RuCharges;

                            reporter?.RecordSuccess(
                                perfStopwatch.Elapsed.TotalMilliseconds,
                                operationResult.Value.RuCharges);

                            if (!isWarmup)
                            {
                                CosmosDiagnosticsLogger.Log(operationResult.Value.CosmosDiagnostics);
                            }
                        }
                        catch (Exception ex)
                        {
                            telemetrySpan.MarkFailed();
                            if (traceFailures)
                            {
                                Trace.TraceInformation(ex.ToString());
                            }

                            // failure case
                            this.FailedOperationCount++;

                            // Special case of cosmos exception
                            double opCharge = 0;
                            int statusCode = 0;
                            if (ex is CosmosException cosmosException)
                            {
                                opCharge = cosmosException.RequestCharge;
                                statusCode = (int)cosmosException.StatusCode;
                                this.TotalRuCharges += opCharge;
                            }

                            reporter?.RecordFailure(
                                perfStopwatch.Elapsed.TotalMilliseconds,
                                opCharge,
                                statusCode,
                                ex.Message);

                            operationResult = new OperationResult()
                            {
                                // TODO: Populate account, database, collection context into ComsosDiagnostics
                                RuCharges = opCharge,
                                LazyDiagnostics = () => ex.ToString(),
                            };
                        }
                    }

                    currentIterationCount++;
                } while (ShouldContinue(currentIterationCount, iterationCount, executionDeadline));

                Trace.TraceInformation($"Executor {this.executorId} completed");
            }
            catch (Exception e)
            {
                Utility.TraceError("Error:", e);
                
            }
            finally
            {
                completionCallback();
            }
        }

        /// <summary>
        /// Determines whether the executor should run another iteration. In continuous
        /// (duration) mode it loops until the deadline; otherwise it honors the iteration count.
        /// </summary>
        private static bool ShouldContinue(int currentIterationCount, int iterationCount, DateTime? executionDeadline)
        {
            if (executionDeadline.HasValue)
            {
                return DateTime.UtcNow < executionDeadline.Value;
            }

            return currentIterationCount < iterationCount;
        }
    }
}
