//-----------------------------------------------------------------------
// <copyright file="CosmosDefaultItemQueryExecutionContext.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// Gateway document query execution context for single partition queries where the service interop is not available
    /// </summary>
    /// <remarks>
    /// For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop for parsing the query, 
    /// so forcing the request through Gateway. We are also now by-passing this for 32-bit host process in NETFX on Windows
    /// as the ServiceInterop dll is only available in 64-bit.
    /// </remarks>
    internal sealed class CosmosGatewayQueryExecutionContext : IDocumentQueryExecutionContext
    {
        // For a single partition collection the only partition is 0
        private const string SinglePartitionKeyId = "0";

        /// <summary>
        /// Whether or not a continuation is expected.
        /// </summary>
        private readonly SchedulingStopwatch fetchSchedulingMetrics;
        private readonly FetchExecutionRangeAccumulator fetchExecutionRangeAccumulator;
        private readonly PartitionRoutingHelper partitionRoutingHelper;
        private readonly CosmosQueryContext queryContext;

        private long retries;
        private FeedResponse<CosmosElement> lastPage;
        private string ContinuationToken => this.lastPage == null ? this.queryContext.QueryRequestOptions.RequestContinuation : this.lastPage.ResponseContinuation;
        

        public CosmosGatewayQueryExecutionContext(
            CosmosQueryContext cosmosQueryContext)
        {
            if(cosmosQueryContext == null)
            {
                throw new ArgumentNullException(nameof(cosmosQueryContext));
            }

            this.queryContext = cosmosQueryContext;
            this.fetchSchedulingMetrics = new SchedulingStopwatch();
            this.fetchSchedulingMetrics.Ready();
            this.fetchExecutionRangeAccumulator = new FetchExecutionRangeAccumulator();
            this.retries = -1;
            this.partitionRoutingHelper = new PartitionRoutingHelper();
        }

        public bool IsDone => this.lastPage != null && string.IsNullOrEmpty(this.lastPage.ResponseContinuation);

        public async Task<FeedResponse<CosmosElement>> ExecuteNextAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.IsDone)
            {
                throw new InvalidOperationException(RMResources.DocumentQueryExecutionContextIsDone);
            }

            this.lastPage = await this.ExecuteInternalAsync(cancellationToken);
            return this.lastPage;
        }

        private async Task<FeedResponse<CosmosElement>> ExecuteInternalAsync(CancellationToken token)
        {
            CollectionCache collectionCache = await this.queryContext.QueryClient.GetCollectionCacheAsync();
            PartitionKeyRangeCache partitionKeyRangeCache = await this.queryContext.QueryClient.GetPartitionKeyRangeCache();
            IDocumentClientRetryPolicy retryPolicyInstance = this.queryContext.QueryClient.GetRetryPolicy();
            retryPolicyInstance = new InvalidPartitionExceptionRetryPolicy(collectionCache, retryPolicyInstance);
            if (this.queryContext.ResourceTypeEnum.IsPartitioned())
            {
                retryPolicyInstance = new PartitionKeyRangeGoneRetryPolicy(
                    collectionCache,
                    partitionKeyRangeCache,
                    PathsHelper.GetCollectionPath(this.queryContext.ResourceLink.OriginalString),
                    retryPolicyInstance);
            }

            return await BackoffRetryUtility<FeedResponse<CosmosElement>>.ExecuteAsync(
                async () =>
                {
                    this.fetchExecutionRangeAccumulator.BeginFetchRange();
                    ++this.retries;
                    FeedResponse<CosmosElement> response = await this.ExecuteOnceAsync(retryPolicyInstance, token);
                    if (!string.IsNullOrEmpty(response.ResponseHeaders[HttpConstants.HttpHeaders.QueryMetrics]))
                    {
                        this.fetchExecutionRangeAccumulator.EndFetchRange(
                            CosmosGatewayQueryExecutionContext.SinglePartitionKeyId,
                            response.ActivityId,
                            response.Count,
                            this.retries);
                        response = new FeedResponse<CosmosElement>(
                            response,
                            response.Count,
                            response.Headers,
                            response.UseETagAsContinuation,
                            new Dictionary<string, QueryMetrics>
                            {
                                {
                                    SinglePartitionKeyId,
                                    QueryMetrics.CreateFromDelimitedStringAndClientSideMetrics(
                                        response.ResponseHeaders[HttpConstants.HttpHeaders.QueryMetrics],
                                        new ClientSideMetrics(
                                            this.retries,
                                            response.RequestCharge,
                                            this.fetchExecutionRangeAccumulator.GetExecutionRanges(),
                                            string.IsNullOrEmpty(response.ResponseContinuation) ? new List<Tuple<string, SchedulingTimeSpan>>()
                                            {
                                                new Tuple<string, SchedulingTimeSpan>(SinglePartitionKeyId, this.fetchSchedulingMetrics.Elapsed)
                                            } : new List<Tuple<string, SchedulingTimeSpan>>()))
                                }
                            },
                            response.RequestStatistics,
                            response.DisallowContinuationTokenMessage,
                            response.ResponseLengthBytes);
                    }

                    this.retries = -1;
                    return response;
                },
                retryPolicyInstance,
                token);
        }

        private async Task<FeedResponse<CosmosElement>> ExecuteOnceAsync(IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            if(this.queryContext.QueryRequestOptions.PartitionKey != null || !this.queryContext.ResourceTypeEnum.IsPartitioned())
            {
                return await this.queryContext.ExecuteQueryAsync(
                    this.queryContext.SqlQuerySpec,
                    cancellationToken,
                    requestEnricher: (cosmosRequestMessage) =>
                    {
                        cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.IsContinuationExpected, bool.FalseString);
                        CosmosQueryRequestOptions.FillContinuationToken(cosmosRequestMessage, this.ContinuationToken);
                    });
            }

            // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop for parsing the query, 
            // so forcing the request through Gateway. We are also now by-passing this for 32-bit host process in NETFX on Windows
            // as the ServiceInterop dll is only available in 64-bit.
            return await this.queryContext.ExecuteQueryAsync(
                this.queryContext.SqlQuerySpec,
                cancellationToken,
                requestEnricher: (cosmosRequestMessage) =>
                {
                    cosmosRequestMessage.UseGatewayMode = true;
                    cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.IsContinuationExpected, this.queryContext.IsContinuationExpected.ToString());
                    CosmosQueryRequestOptions.FillContinuationToken(cosmosRequestMessage, this.ContinuationToken);
                });
        }

        public void Dispose()
        {
        }
    }
}
