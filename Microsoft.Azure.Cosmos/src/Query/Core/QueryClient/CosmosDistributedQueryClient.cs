//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryClient
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

    internal sealed class CosmosDistributedQueryClient : ICosmosDistributedQueryClient
    {
        private readonly CosmosClientContext cosmosClientContext;

        private readonly string containerLink;

        private readonly Guid correlatedActivityId;

        private DocumentClient DocumentClient => this.cosmosClientContext.DocumentClient;

        private IDocumentClientRetryPolicy RetryPolicy => this.cosmosClientContext.DocumentClient.ResetSessionTokenRetryPolicy.GetRequestPolicy();

        public CosmosDistributedQueryClient(
            CosmosClientContext cosmosClientContext,
            string containerLink,
            Guid correlatedActivityId)
        {
            this.cosmosClientContext = cosmosClientContext ?? throw new ArgumentNullException(nameof(cosmosClientContext));
            this.containerLink = containerLink ?? throw new ArgumentNullException(nameof(containerLink));
            this.correlatedActivityId = correlatedActivityId;
        }

        public async Task<TryCatch<QueryPage>> MonadicQueryAsync(
            Cosmos.PartitionKey? partitionKey,
            FeedRangeInternal feedRange,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            QueryExecutionOptions queryPaginationOptions,
            Tracing.ITrace trace,
            CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            try
            {
                await this.DocumentClient.EnsureValidClientAsync(trace);

                DocumentServiceRequest request = this.CreateRequest(
                    sqlQuerySpec,
                    partitionKey,
                    feedRange,
                    continuationToken,
                    queryPaginationOptions,
                    trace);

                DocumentServiceResponse response = await this.DocumentClient.ExecuteQueryAsync(
                    request,
                    this.RetryPolicy,
                    cancellationToken);

                return CreatePage(response, trace);
            }
            catch (DocumentClientException exception)
            {
                CosmosException cosmosException = CosmosExceptionFactory.Create(exception, trace);
                return TryCatch<QueryPage>.FromException(cosmosException);
            }
        }

        private DocumentServiceRequest CreateRequest(
            SqlQuerySpec sqlQuerySpec,
            Cosmos.PartitionKey? partitionKey,
            FeedRangeInternal feedRange,
            string continuationToken,
            QueryExecutionOptions queryPaginationOptions,
            Tracing.ITrace trace)
        {
            Stream serializedQuerySpec = this.cosmosClientContext.SerializerCore.ToStreamSqlQuerySpec(sqlQuerySpec, ResourceType.Document);

            StoreRequestHeaders headers = new StoreRequestHeaders();
            headers.ContentType = RuntimeConstants.MediaTypes.QueryJson;
            headers.Add(HttpConstants.HttpHeaders.IsContinuationExpected, bool.TrueString);
            headers.Add(HttpConstants.HttpHeaders.EnableCrossPartitionQuery, bool.TrueString);
            headers.ConsistencyLevel = this.cosmosClientContext.ClientOptions.ConsistencyLevel.ToString();
            headers.Continuation = continuationToken?.ToString();
            headers.PageSize = queryPaginationOptions.PageSizeLimit?.ToString() ?? int.MaxValue.ToString();
            headers.PartitionKey = partitionKey?.ToString();
            headers.SupportedSerializationFormats = (SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.JsonText).ToString();
            headers.ActivityId = this.correlatedActivityId.ToString();
            headers.Add(HttpConstants.HttpHeaders.CorrelatedActivityId, this.correlatedActivityId.ToString());

            feedRange.Accept(FeedRangeVisitor.Instance, headers);

            DocumentServiceRequest request = new DocumentServiceRequest(
                operationType: OperationType.Query,
                resourceType: ResourceType.Document,
                path: this.containerLink,
                body: serializedQuerySpec,
                authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey,
                headers: headers.INameValueCollection)
            {
                UseStatusCodeForFailures = true,
                UseStatusCodeFor429 = true,
                UseGatewayMode = true,
            };

            ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace);
            request.RequestContext.ClientRequestStatistics = clientSideRequestStatisticsTraceDatum;

            return request;
        }

        private static TryCatch<QueryPage> CreatePage(DocumentServiceResponse response, Tracing.ITrace trace)
        {
            string queryMetricsText = response.Headers[HttpConstants.HttpHeaders.QueryMetrics];
            if (queryMetricsText != null)
            {
                QueryMetricsTraceDatum datum = new QueryMetricsTraceDatum(
                    new Lazy<QueryMetrics>(() => new QueryMetrics(
                        queryMetricsText,
                        IndexUtilizationInfo.Empty, 
                        ClientSideMetrics.Empty)));
                trace.AddDatum("Query Metrics", datum);
            }

            Headers headers = new Headers(response.Headers);
            if (!response.StatusCode.IsSuccess())
            {
                CosmosException cosmosException = CosmosExceptionFactory.Create(
                    response,
                    headers,
                    trace);
                return TryCatch<QueryPage>.FromException(cosmosException);
            }

            return CosmosQueryClientCore.CreateQueryPage(
                headers,
                response.ResponseBody,
                ResourceType.Document);
        }

        private sealed class FeedRangeVisitor : IFeedRangeVisitor<StoreRequestHeaders>
        {
            public static FeedRangeVisitor Instance { get; } = new FeedRangeVisitor();

            private FeedRangeVisitor()
            {
            }

            public void Visit(FeedRangePartitionKey feedRange, StoreRequestHeaders headers)
            {
                headers.PartitionKey = feedRange.PartitionKey.ToString();
            }

            public void Visit(FeedRangePartitionKeyRange feedRange, StoreRequestHeaders headers)
            {
                headers.PartitionKeyRangeId = feedRange.PartitionKeyRangeId;
            }

            public void Visit(FeedRangeEpk feedRange, StoreRequestHeaders headers)
            {
                if (!FeedRangeEpk.FullRange.Equals(feedRange))
                {
                    headers.StartEpk = feedRange.Range.Min;
                    headers.EndEpk = feedRange.Range.Max;
                }
            }
        }
    }
}