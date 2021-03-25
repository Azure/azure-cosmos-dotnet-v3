//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    internal static class SpeculativeRetryStore
    {
        public static Task<DocumentServiceResponse> ProcessMessageAsync(
            IStoreModel storeModel,
            TimeSpan? speculativeRetryAfter,
            DocumentServiceRequest request,
            CancellationToken cancellationToken)
        {
            if (!request.IsReadOnlyRequest)
            {
                return storeModel.ProcessMessageAsync(request, cancellationToken);
            }

            if (!speculativeRetryAfter.HasValue)
            {
                return storeModel.ProcessMessageAsync(request, cancellationToken);
            }

            if (speculativeRetryAfter.Value == TimeSpan.Zero)
            {
                return AlwaysSpeculativeRetryAsync(
                    storeModel,
                    request,
                    cancellationToken);
            }

            return SpeculativeRetryWithLatencyAsync(
                storeModel,
                speculativeRetryAfter.Value,
                request,
                cancellationToken);
        }

        private static async Task<DocumentServiceResponse> SpeculativeRetryWithLatencyAsync(
            IStoreModel storeModel,
            TimeSpan speculativeRetryAfter,
            DocumentServiceRequest request,
            CancellationToken cancellationToken)
        {
            Task<DocumentServiceResponse> firstRequestTask = storeModel.ProcessMessageAsync(request, cancellationToken);
            Task timer = Task.Delay(speculativeRetryAfter);

            await Task.WhenAny(firstRequestTask, timer);
            if (firstRequestTask.IsCompleted)
            {
                return await firstRequestTask;
            }

            return await CloneAndAwaitSingleResponseAsync(storeModel, request, firstRequestTask, cancellationToken);
        }

        private static async Task<DocumentServiceResponse> CloneAndAwaitSingleResponseAsync(
            IStoreModel storeModel,
            DocumentServiceRequest request,
            Task<DocumentServiceResponse> firstRequestTask,
            CancellationToken cancellationToken)
        {
            DocumentServiceRequest requestClone = request.Clone();
            requestClone.Body = request.CloneableBody;
            Task<DocumentServiceResponse> secondRequestTask = storeModel.ProcessMessageAsync(requestClone, cancellationToken);
            Task<DocumentServiceResponse> resultTask = await Task.WhenAny(firstRequestTask, secondRequestTask);
            Task<DocumentServiceResponse> slowTask = firstRequestTask.IsCompleted ? secondRequestTask : firstRequestTask;

            // Clean up the slow task
            _ = slowTask.ContinueWith(t => DefaultTrace.TraceWarning("initializeTask failed {0}", t.Exception), TaskContinuationOptions.OnlyOnFaulted);
            _ = slowTask.ContinueWith(t => t.Dispose(), TaskContinuationOptions.OnlyOnRanToCompletion);
            return await resultTask;
        }

        private static Task<DocumentServiceResponse> AlwaysSpeculativeRetryAsync(
            IStoreModel storeModel,
            DocumentServiceRequest request,
            CancellationToken cancellationToken)
        {
            Task<DocumentServiceResponse> task1 = storeModel.ProcessMessageAsync(request, cancellationToken);
            return CloneAndAwaitSingleResponseAsync(
                storeModel,
                request,
                task1,
                cancellationToken);
        }
    }
}
