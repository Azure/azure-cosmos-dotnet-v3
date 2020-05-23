//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal class SerialOperationExecutor : IExecutor
    {
        private readonly IBenchmarkOperatrion operation;
        private readonly string executorId;

        public SerialOperationExecutor(
            string executorId,
            IBenchmarkOperatrion benchmarkOperation)
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
                Action completionCallback)
        {
            Trace.TraceInformation($"Executor {this.executorId} started");

            try
            {
                int currentIterationCount = 0;
                do
                {
                    OperationResult? operationResult = null;

                    this.operation.Prepare();

                    using (TelemetrySpan telemetrySpan = TelemetrySpan.StartNew(
                                () => operationResult.Value,
                                disableTelemetry: isWarmup))
                    {
                        try
                        {
                            operationResult = await this.operation.ExecuteOnceAsync();

                            // Success case
                            this.SuccessOperationCount++;
                            this.TotalRuCharges += operationResult.Value.RuCharges;
                        }
                        catch (Exception ex)
                        {
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
                                // TODO: Populate account, databse, collection context into ComsosDiagnostics
                                RuCharges = opCharge,
                                lazyDiagnostics = () => ex.ToString(),
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
