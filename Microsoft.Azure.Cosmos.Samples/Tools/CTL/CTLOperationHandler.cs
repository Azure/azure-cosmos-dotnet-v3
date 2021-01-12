//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Synchronizes parallel execution of operations and emits events.
    /// </summary>
    internal static class CTLOperationHandler<T>
    {
        /// <summary>
        /// Waits until the synchronization semaphore is available, creates a new operation and handles resolution.
        /// </summary>
        /// <param name="semaphoreSlim">Synchronization semaphore that defines maximum degree of parallelism.</param>
        /// <param name="diagnosticsLoggingThreshold">Latency threshold above which <paramref name="logDiagnostics"/> will be called.</param>
        /// <param name="stopwatch">Shared stopwatch instance to track ellapsed time and measure latency.</param>
        /// <param name="resultProducer">Producer to generate operation calls as a producer-consumer.</param>
        /// <param name="onSuccess">Event handler for operation success.</param>
        /// <param name="onFailure"></param>
        /// <param name="trackLatency">Event handler for tracking operation latency</param>
        /// <param name="logDiagnostics">Event handler for tracking diagnostics when latency goes above the threshold.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task PerformOperationAsync(
            SemaphoreSlim semaphoreSlim,
            long diagnosticsLoggingThreshold,
            Stopwatch stopwatch,
            ICTLResultProducer<T> resultProducer,
            Action onSuccess,
            Action<Exception> onFailure,
            Action<long> trackLatency,
            Action<T> logDiagnostics,
            CancellationToken cancellationToken)
        {
            while (resultProducer.HasMoreResults)
            {
                await semaphoreSlim.WaitAsync(cancellationToken);
                long startTime = stopwatch.ElapsedMilliseconds;
                await resultProducer.GetNextAsync().ContinueWith(task =>
                {
                    semaphoreSlim.Release();
                    long latency = stopwatch.ElapsedMilliseconds - startTime;
                    trackLatency(latency);
                    if (task.IsCompletedSuccessfully)
                    {
                        if (latency > diagnosticsLoggingThreshold)
                        {
                            logDiagnostics(task.Result);
                        }

                        onSuccess();
                    }
                    else
                    {
                        onFailure(task.Exception);
                    }
                });
            }
        }
    }
}
