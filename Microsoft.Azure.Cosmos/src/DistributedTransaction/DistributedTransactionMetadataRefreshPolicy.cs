// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class DistributedTransactionMetadataRefreshPolicy
    {
        private readonly IReadOnlyList<DistributedTransactionOperation> operations;
        private readonly CosmosClientContext clientContext;
        private bool metadataRefreshed;

        public DistributedTransactionMetadataRefreshPolicy(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
        }

        public async Task<bool> ShouldReturnOriginalResponseAsync(
            DistributedTransactionResponse response,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (this.metadataRefreshed)
            {
                return false;
            }

            bool isNameCacheStale = IsNameCacheStale(response.StatusCode, response.SubStatusCode);
            HashSet<string> staleCollectionPaths = null;
            foreach (DistributedTransactionOperationResult result in response)
            {
                if (!IsNameCacheStale(result.StatusCode, result.SubStatusCode))
                {
                    continue;
                }

                isNameCacheStale = true;
                // Synthesized envelope-error results do not identify a stale participant.
                if (result.HasIndex && result.Index >= 0 && result.Index < this.operations.Count)
                {
                    DistributedTransactionOperation operation = this.operations[result.Index];
                    staleCollectionPaths ??= new HashSet<string>(StringComparer.Ordinal);
                    staleCollectionPaths.Add(DistributedTransactionConstants.GetCollectionFullName(
                        operation.Database, operation.Container));
                }
            }

            if (!isNameCacheStale)
            {
                return false;
            }

            // An unchanged RID means the cache is already fresh; another refresh only adds load.
            this.metadataRefreshed = true;
            using (ITrace refreshTrace = trace.StartChild(
                "Refresh Distributed Transaction Metadata", TraceComponent.Batch, TraceLevel.Info))
            {
                bool returnOriginalResponse;
                try
                {
                    returnOriginalResponse = await DistributedTransactionCommitterUtils.RefreshCollectionRidsAsync(
                        this.operations,
                        this.clientContext,
                        refreshTrace,
                        staleCollectionPaths,
                        cancellationToken);
                }
                catch (Exception exception) when (exception is CosmosException || exception is HttpRequestException)
                {
                    refreshTrace.AddDatum("Exception Message", exception.Message);
                    CosmosException cosmosException = exception as CosmosException;
                    DefaultTrace.TraceWarning(
                        "Distributed transaction metadata refresh failed after a stale-cache response " +
                        "(ExceptionType={0}, StatusCode={1}, SubStatusCode={2}). Returning the original transaction failure.",
                        exception.GetType().Name,
                        cosmosException?.StatusCode,
                        cosmosException?.SubStatusCode);
                    returnOriginalResponse = true;
                }

                cancellationToken.ThrowIfCancellationRequested();
                return returnOriginalResponse;
            }
        }

        private static bool IsNameCacheStale(HttpStatusCode statusCode, SubStatusCodes subStatusCode)
        {
            return statusCode == HttpStatusCode.Gone && subStatusCode == SubStatusCodes.NameCacheIsStale;
        }
    }
}
