//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics.Timer;

    /// <summary>
    /// Synchronizes parallel execution of operations and emits events.
    /// </summary>
    internal static class CTLOperationHandler<T>
    {
        /// <summary>
        /// Waits until the synchronization semaphore is available, creates a new operation and handles resolution.
        /// </summary>
        /// <param name="diagnosticsLoggingThreshold">Latency threshold above which <paramref name="logDiagnostics"/> will be called.</param>
        /// <param name="createTimerContext">Creates a <see cref="TimerContext"/> to measure operation latency.</param>
        /// <param name="resultProducer">Producer to generate operation calls as a producer-consumer.</param>
        /// <param name="onSuccess">Event handler for operation success.</param>
        /// <param name="onFailure"></param>
        /// <param name="logDiagnostics">Event handler for tracking diagnostics when latency goes above the threshold.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task PerformOperationAsync(
            long diagnosticsLoggingThreshold,
            Func<TimerContext> createTimerContext,
            ICTLResultProducer<T> resultProducer,
            Action onSuccess,
            Action<Exception> onFailure,
            Action<T> logDiagnostics,
            CancellationToken cancellationToken)
        {
            while (resultProducer.HasMoreResults)
            {
                using (TimerContext timerContext = createTimerContext())
                {
                    await resultProducer.GetNextAsync().ContinueWith(task =>
                    {
                        long latency = (long)timerContext.Elapsed.TotalMilliseconds;
                        if (task.IsCompletedSuccessfully)
                        {
                            if (latency > diagnosticsLoggingThreshold)
                            {
                                logDiagnostics(task.Result);
                            }

                            if (!resultProducer.HasMoreResults)
                            {
                                onSuccess();
                            }
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
}
