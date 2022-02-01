//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Azure.Monitor.OpenTelemetry.Exporter;
    using Microsoft.Azure.Cosmos;
    using OpenTelemetry;
    using OpenTelemetry.Trace;

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
                BenchmarkConfig benchmarkConfig,
                Action completionCallback)
        {
            Trace.TraceInformation($"Executor {this.executorId} started");

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
                            TracerProvider openTelemetry = null;
                            if (benchmarkConfig.EnableOpenTelemetry)
                            {
                                AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
                                TracerProviderBuilder traceBuilder = Sdk.CreateTracerProviderBuilder()
                                                        .AddSource("Azure.*"); // Collect all traces from Azure SDKs
                                if(benchmarkConfig.AppInsightConnectionString != null)
                                {
                                    traceBuilder
                                        .AddAzureMonitorTraceExporter(
                                                options => 
                                                options.ConnectionString = benchmarkConfig.AppInsightConnectionString); // Export traces to Azure Monitor
                                }
                                openTelemetry = traceBuilder.Build();
                            }
                            using (openTelemetry)
                            {
                                operationResult = await this.operation.ExecuteOnceAsync();
                            }

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
                            if (benchmarkConfig.TraceFailures)
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
