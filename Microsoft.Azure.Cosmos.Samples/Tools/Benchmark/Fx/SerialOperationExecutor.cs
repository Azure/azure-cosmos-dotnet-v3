//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Logging;
    using App.Metrics.Timer;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Logging;

    internal class SerialOperationExecutor : IExecutor
    {
        private readonly IBenchmarkOperation operation;
        private readonly string executorId;

        // TODO: Move to config.
        private const string LoggingContextIdentifier = "CosmosDBBenchmarkLoggingContext";

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
                ILogger logger,
                IMetrics metrics)
        {
            logger.LogInformation($"Executor {this.executorId} started");

            logger.LogInformation("Initializing counters and metrics.");
            CounterOptions readSuccessMeter = new CounterOptions { Name = "#Read Successful Operations", Context = LoggingContextIdentifier };
            CounterOptions readFailureMeter = new CounterOptions { Name = "#Read Unsuccessful Operations", Context = LoggingContextIdentifier };
            CounterOptions writeSuccessMeter = new CounterOptions { Name = "#Write Successful Operations", Context = LoggingContextIdentifier };
            CounterOptions writeFailureMeter = new CounterOptions { Name = "#Write Unsuccessful Operations", Context = LoggingContextIdentifier };
            CounterOptions querySuccessMeter = new CounterOptions { Name = "#Query Successful Operations", Context = LoggingContextIdentifier };
            CounterOptions queryFailureMeter = new CounterOptions { Name = "#Query Unsuccessful Operations", Context = LoggingContextIdentifier };

            TimerOptions readLatencyTimer = new()
            {
                Name = "Read latency",
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Milliseconds,
                RateUnit = TimeUnit.Seconds,
                Context = LoggingContextIdentifier,

                // TODO: Pass config.
                Reservoir = () => ReservoirProvider.GetReservoir(new BenchmarkConfig())
            };

            try
            {
                int currentIterationCount = 0;
                do
                {
                    OperationResult? operationResult = null;

                    await this.operation.PrepareAsync();

                    using (IDisposable telemetrySpan = TelemetrySpan.StartNew(
                                () => operationResult.Value,
                                disableTelemetry: isWarmup))
                    {

                        try
                        {
                            using (TimerContext timerContext = metrics.Measure.Timer.Time(readLatencyTimer))
                            {
                                operationResult = await this.operation.ExecuteOnceAsync();
                            }

                            // TODO: Move to operation implementation.
                            // if (this.operation.GetType() == typeof(ReadTExistsV3BenchmarkOperation))
                            metrics.Measure.Counter.Increment(readSuccessMeter);

                            // Success case
                            this.SuccessOperationCount++;
                            this.TotalRuCharges += operationResult.Value.RuCharges;

                            if (!isWarmup)
                            {
                                CosmosDiagnosticsLogger.Log(operationResult.Value.CosmosDiagnostics);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (traceFailures)
                            {
                                Console.WriteLine(ex.ToString());
                            }

                            // failure case
                            this.FailedOperationCount++;

                            // Special case of cosmos exception
                            double opCharge = 0;
                            if (ex is CosmosException cosmosException)
                            {
                                opCharge = cosmosException.RequestCharge;
                                this.TotalRuCharges += opCharge;
                            }

                            operationResult = new OperationResult()
                            {
                                // TODO: Populate account, database, collection context into ComsosDiagnostics
                                RuCharges = opCharge,
                                LazyDiagnostics = () => ex.ToString(),
                            };
                        }
                    }

                    currentIterationCount++;
                } while (currentIterationCount < iterationCount);

                Trace.TraceInformation($"Executor {this.executorId} completed");
            }
            finally
            {
                completionCallback();
            }
        }
    }
}
