//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Retry policy that reacts to the backend signalling (via a <see cref="HttpStatusCode.BadRequest"/> with
    /// sub-status <see cref="ContainerPropertiesExtensions.AddIdToLastPartitionKeyPathSubStatusCode"/>) that the
    /// item's "id" must be appended as the last partition key component. On such a response the policy marks the
    /// container as one whose last partition key path is "/id" so that, on retry, the append-id code path adds the
    /// item's "id" to the partition key. Mirrors <see cref="PartitionKeyMismatchRetryPolicy"/>, which refreshes the
    /// collection cache as its retry side-effect.
    /// </summary>
    internal sealed class AppendIdToPartitionKeyRetryPolicy : IDocumentClientRetryPolicy
    {
        private const int MaxRetries = 1;

        private readonly ContainerInternal container;

        public AppendIdToPartitionKeyRetryPolicy(ContainerInternal container)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            switch (exception)
            {
                case CosmosException cosmosException:
                    return this.ShouldRetryInternalAsync(
                        cosmosException.StatusCode,
                        (int)cosmosException.Headers.SubStatusCode,
                        cancellationToken);

                case DocumentClientException documentClientException:
                    return this.ShouldRetryInternalAsync(
                        documentClientException.StatusCode,
                        (int)documentClientException.GetSubStatus(),
                        cancellationToken);

                default:
                    return Task.FromResult(ShouldRetryResult.NoRetry());
            }
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            return this.ShouldRetryInternalAsync(
                cosmosResponseMessage?.StatusCode,
                cosmosResponseMessage == null ? (int?)null : (int)cosmosResponseMessage.Headers.SubStatusCode,
                cancellationToken);
        }

        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
            // No-op: the partition key transformation happens above the request layer, before the
            // DocumentServiceRequest is built, so there is nothing to mutate on the request here.
        }

        public static async Task<T> ExecuteWithRetryAsync<T>(
            ContainerInternal cosmosContainerCore,
            Func<int, Task<T>> action,
            bool canRetryAction,
            CancellationToken cancellationToken)
        {
            IDocumentClientRetryPolicy appendIdRetryPolicy = null;
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    T result = await action(attempt);
                    if (!canRetryAction
                        || attempt == MaxRetries
                        || result is not ResponseMessage responseMessage
                        || responseMessage.IsSuccessStatusCode)
                    {
                        return result;
                    }

                    appendIdRetryPolicy ??= new AppendIdToPartitionKeyRetryPolicy(cosmosContainerCore);
                    if (!(await appendIdRetryPolicy.ShouldRetryAsync(responseMessage, cancellationToken)).ShouldRetry)
                    {
                        return result;
                    }

                    responseMessage.Dispose();
                }
                catch (Exception exception) when (canRetryAction && attempt < MaxRetries)
                {
                    appendIdRetryPolicy ??= new AppendIdToPartitionKeyRetryPolicy(cosmosContainerCore);
                    if (!(await appendIdRetryPolicy.ShouldRetryAsync(exception, cancellationToken)).ShouldRetry)
                    {
                        throw;
                    }
                }
            }

            throw new InvalidOperationException("The append-id retry loop completed without returning a result.");
        }

        private async Task<ShouldRetryResult> ShouldRetryInternalAsync(
            HttpStatusCode? statusCode,
            int? subStatusCode,
            CancellationToken cancellationToken)
        {
            if (statusCode == HttpStatusCode.BadRequest
                && subStatusCode == ContainerPropertiesExtensions.AddIdToLastPartitionKeyPathSubStatusCode)
            {
                if (!await this.container.TryMarkLastPartitionKeyPathIsIdAsync(cancellationToken))
                {
                    return ShouldRetryResult.NoRetry();
                }

                return ShouldRetryResult.RetryAfter(TimeSpan.Zero);
            }

            return ShouldRetryResult.NoRetry();
        }
    }
}
