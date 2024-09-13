﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    internal abstract class DocumentQueryExecutionContextBase : IDocumentQueryExecutionContext
    {
        public static readonly string DefaultSupportedSerializationFormats = string.Join(",", SupportedSerializationFormats.JsonText, SupportedSerializationFormats.CosmosBinary);
        
        public readonly struct InitParams
        {
            public IDocumentQueryClient Client { get; }
            public ResourceType ResourceTypeEnum { get; }
            public Type ResourceType { get; }
            public Expression Expression { get; }
            public FeedOptions FeedOptions { get; }
            public string ResourceLink { get; }
            public bool GetLazyFeedResponse { get; }
            public Guid CorrelatedActivityId { get; }

            public InitParams(
                IDocumentQueryClient client,
                ResourceType resourceTypeEnum,
                Type resourceType,
                Expression expression,
                FeedOptions feedOptions,
                string resourceLink,
                bool getLazyFeedResponse,
                Guid correlatedActivityId)
            {
                if (client == null)
                {
                    throw new ArgumentNullException($"{nameof(client)} can not be null.");
                }

                if (resourceType == null)
                {
                    throw new ArgumentNullException($"{nameof(resourceType)} can not be null.");
                }

                if (expression == null)
                {
                    throw new ArgumentNullException($"{nameof(expression)} can not be null.");
                }

                if (feedOptions == null)
                {
                    throw new ArgumentNullException($"{nameof(feedOptions)} can not be null.");
                }

                if (correlatedActivityId == Guid.Empty)
                {
                    throw new ArgumentException($"{nameof(correlatedActivityId)} can not be empty.");
                }

                this.Client = client;
                this.ResourceTypeEnum = resourceTypeEnum;
                this.ResourceType = resourceType;
                this.Expression = expression;
                this.FeedOptions = feedOptions;
                this.ResourceLink = resourceLink;
                this.GetLazyFeedResponse = getLazyFeedResponse;
                this.CorrelatedActivityId = correlatedActivityId;
            }
        }

        public static readonly DocumentFeedResponse<dynamic> EmptyFeedResponse = new DocumentFeedResponse<dynamic>(
            Enumerable.Empty<dynamic>(),
            Enumerable.Empty<dynamic>().Count(),
            new RequestNameValueCollection());
        protected SqlQuerySpec querySpec;
        private readonly Expression expression;
        private readonly FeedOptions feedOptions;
        private readonly bool getLazyFeedResponse;
        private bool isExpressionEvaluated;
        private DocumentFeedResponse<CosmosElement> lastPage;

        protected DocumentQueryExecutionContextBase(
           InitParams initParams)
        {
            this.Client = initParams.Client;
            this.ResourceTypeEnum = initParams.ResourceTypeEnum;
            this.ResourceType = initParams.ResourceType;
            this.expression = initParams.Expression;
            this.feedOptions = initParams.FeedOptions;
            this.ResourceLink = initParams.ResourceLink;
            this.getLazyFeedResponse = initParams.GetLazyFeedResponse;
            this.CorrelatedActivityId = initParams.CorrelatedActivityId;
            this.isExpressionEvaluated = false;
        }

        public bool ShouldExecuteQueryRequest => this.QuerySpec != null;

        public IDocumentQueryClient Client { get; }

        public Type ResourceType { get; }

        public ResourceType ResourceTypeEnum { get; }

        public string ResourceLink { get; }

        public int? MaxItemCount => this.feedOptions.MaxItemCount;

        protected SqlQuerySpec QuerySpec
        {
            get
            {
                if (!this.isExpressionEvaluated)
                {
                    LinqQueryOperation linqQuery = DocumentQueryEvaluator.Evaluate(this.expression);

                    if (linqQuery.ScalarOperationKind != ScalarOperationKind.None)
                    {
                        throw new NotSupportedException($"This operation does not support the supplied LINQ expression since it involves client side operation : {linqQuery.ScalarOperationKind}");
                    }

                    this.querySpec = linqQuery.SqlQuerySpec;
                    this.isExpressionEvaluated = true;
                }

                return this.querySpec;
            }
        }

        protected PartitionKeyInternal PartitionKeyInternal => this.feedOptions.PartitionKey == null ? null : this.feedOptions.PartitionKey.InternalKey;

        protected int MaxBufferedItemCount => this.feedOptions.MaxBufferedItemCount;

        protected int MaxDegreeOfParallelism => this.feedOptions.MaxDegreeOfParallelism;

        protected string PartitionKeyRangeId => this.feedOptions.PartitionKeyRangeId;

        protected virtual string ContinuationToken => this.lastPage == null ? this.feedOptions.RequestContinuationToken : this.lastPage.ResponseContinuation;

        public virtual bool IsDone => this.lastPage != null && string.IsNullOrEmpty(this.lastPage.ResponseContinuation);

        public Guid CorrelatedActivityId { get; }

        public async Task<PartitionedQueryExecutionInfo> GetPartitionedQueryExecutionInfoAsync(
            PartitionKeyDefinition partitionKeyDefinition,
            Cosmos.VectorEmbeddingPolicy vectorEmbeddingPolicy,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            bool allowDCount,
            Cosmos.GeospatialType geospatialType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // $ISSUE-felixfan-2016-07-13: We should probably get PartitionedQueryExecutionInfo from Gateway in GatewayMode

            QueryPartitionProvider queryPartitionProvider = await this.Client.GetQueryPartitionProviderAsync();
            TryCatch<PartitionedQueryExecutionInfo> tryGetPartitionedQueryExecutionInfo = queryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                querySpecJsonString: JsonConvert.SerializeObject(this.QuerySpec),
                partitionKeyDefinition: partitionKeyDefinition,
                vectorEmbeddingPolicy: vectorEmbeddingPolicy,
                requireFormattableOrderByQuery: requireFormattableOrderByQuery,
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                hasLogicalPartitionKey: hasLogicalPartitionKey,
                allowDCount: allowDCount,
                geospatialType: geospatialType,
                useSystemPrefix: false);
            if (!tryGetPartitionedQueryExecutionInfo.Succeeded)
            {
                throw new BadRequestException(tryGetPartitionedQueryExecutionInfo.Exception);
            }

            return tryGetPartitionedQueryExecutionInfo.Result;
        }

        public virtual async Task<DocumentFeedResponse<CosmosElement>> ExecuteNextFeedResponseAsync(CancellationToken cancellationToken)
        {
            if (this.IsDone)
            {
                throw new InvalidOperationException(RMResources.DocumentQueryExecutionContextIsDone);
            }

            this.lastPage = await this.ExecuteInternalAsync(cancellationToken);
            return this.lastPage;
        }

        public FeedOptions GetFeedOptions(string continuationToken)
        {
            FeedOptions options = new FeedOptions(this.feedOptions);
            options.RequestContinuationToken = continuationToken;
            return options;
        }

        public async Task<INameValueCollection> CreateCommonHeadersAsync(FeedOptions feedOptions)
        {
            INameValueCollection requestHeaders = new RequestNameValueCollection();

            Cosmos.ConsistencyLevel defaultConsistencyLevel = (Cosmos.ConsistencyLevel)await this.Client.GetDefaultConsistencyLevelAsync();
            Cosmos.ConsistencyLevel? desiredConsistencyLevel = (Cosmos.ConsistencyLevel?)await this.Client.GetDesiredConsistencyLevelAsync();
            if (!string.IsNullOrEmpty(feedOptions.SessionToken) && !ReplicatedResourceClient.IsReadingFromMaster(this.ResourceTypeEnum, OperationType.ReadFeed))
            {
                if (defaultConsistencyLevel == Cosmos.ConsistencyLevel.Session || (desiredConsistencyLevel.HasValue && desiredConsistencyLevel.Value == Cosmos.ConsistencyLevel.Session))
                {
                    // Query across partitions is not supported today. Master resources (for e.g., database) 
                    // can span across partitions, whereas server resources (viz: collection, document and attachment)
                    // don't span across partitions. Hence, session token returned by one partition should not be used 
                    // when quering resources from another partition. 
                    // Since master resources can span across partitions, don't send session token to the backend.
                    // As master resources are sync replicated, we should always get consistent query result for master resources,
                    // irrespective of the chosen replica.
                    // For server resources, which don't span partitions, specify the session token 
                    // for correct replica to be chosen for servicing the query result.
                    requestHeaders[HttpConstants.HttpHeaders.SessionToken] = feedOptions.SessionToken;
                }
            }

            requestHeaders[HttpConstants.HttpHeaders.Continuation] = feedOptions.RequestContinuationToken;
            requestHeaders[HttpConstants.HttpHeaders.IsQuery] = bool.TrueString;

            // Flow the pageSize only when we are not doing client eval
            if (feedOptions.MaxItemCount.HasValue)
            {
                requestHeaders[HttpConstants.HttpHeaders.PageSize] = feedOptions.MaxItemCount.ToString();
            }

            requestHeaders[HttpConstants.HttpHeaders.EnableCrossPartitionQuery] = feedOptions.EnableCrossPartitionQuery.ToString();

            if (feedOptions.MaxDegreeOfParallelism != 0)
            {
                requestHeaders[HttpConstants.HttpHeaders.ParallelizeCrossPartitionQuery] = bool.TrueString;
            }

            if (this.feedOptions.EnableScanInQuery != null)
            {
                requestHeaders[HttpConstants.HttpHeaders.EnableScanInQuery] = this.feedOptions.EnableScanInQuery.ToString();
            }

            if (this.feedOptions.EmitVerboseTracesInQuery != null)
            {
                requestHeaders[HttpConstants.HttpHeaders.EmitVerboseTracesInQuery] = this.feedOptions.EmitVerboseTracesInQuery.ToString();
            }

            if (this.feedOptions.EnableLowPrecisionOrderBy != null)
            {
                requestHeaders[HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy] = this.feedOptions.EnableLowPrecisionOrderBy.ToString();
            }

            if (!string.IsNullOrEmpty(this.feedOptions.FilterBySchemaResourceId))
            {
                requestHeaders[HttpConstants.HttpHeaders.FilterBySchemaResourceId] = this.feedOptions.FilterBySchemaResourceId;
            }

            if (this.feedOptions.ResponseContinuationTokenLimitInKb != null)
            {
                requestHeaders[HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB] = this.feedOptions.ResponseContinuationTokenLimitInKb.ToString();
            }

            if (this.feedOptions.ConsistencyLevel.HasValue)
            {
                await this.Client.EnsureValidOverwriteAsync(
                    (Documents.ConsistencyLevel)feedOptions.ConsistencyLevel.Value,
                    OperationType.ReadFeed,
                    this.ResourceTypeEnum);

                requestHeaders.Set(HttpConstants.HttpHeaders.ConsistencyLevel, this.feedOptions.ConsistencyLevel.Value.ToString());
            }
            else if (desiredConsistencyLevel.HasValue)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.ConsistencyLevel, desiredConsistencyLevel.Value.ToString());
            }

            if (this.feedOptions.EnumerationDirection.HasValue)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.EnumerationDirection, this.feedOptions.EnumerationDirection.Value.ToString());
            }

            if (this.feedOptions.ReadFeedKeyType.HasValue)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.ReadFeedKeyType, this.feedOptions.ReadFeedKeyType.Value.ToString());
            }

            if (this.feedOptions.StartId != null)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.StartId, this.feedOptions.StartId);
            }

            if (this.feedOptions.EndId != null)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.EndId, this.feedOptions.EndId);
            }

            if (this.feedOptions.StartEpk != null)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.StartEpk, this.feedOptions.StartEpk);
            }

            if (this.feedOptions.EndEpk != null)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.EndEpk, this.feedOptions.EndEpk);
            }

            if (this.feedOptions.PopulateQueryMetrics)
            {
                requestHeaders[HttpConstants.HttpHeaders.PopulateQueryMetrics] = bool.TrueString;
            }

            if (this.feedOptions.ForceQueryScan)
            {
                requestHeaders[HttpConstants.HttpHeaders.ForceQueryScan] = bool.TrueString;
            }

            if (this.feedOptions.MergeStaticId != null)
            {
                requestHeaders.Set(HttpConstants.HttpHeaders.MergeStaticId, this.feedOptions.MergeStaticId);
            }

            requestHeaders[HttpConstants.HttpHeaders.SupportedSerializationFormats] = this.feedOptions.SupportedSerializationFormats?.ToString() ?? DefaultSupportedSerializationFormats;

            return requestHeaders;
        }

        public DocumentServiceRequest CreateDocumentServiceRequest(INameValueCollection requestHeaders, SqlQuerySpec querySpec, PartitionKeyInternal partitionKey)
        {
            DocumentServiceRequest request = this.CreateDocumentServiceRequest(requestHeaders, querySpec);
            this.PopulatePartitionKeyInfo(request, partitionKey);
            request.Properties = this.feedOptions.Properties;
            return request;
        }

        public DocumentServiceRequest CreateDocumentServiceRequest(INameValueCollection requestHeaders, SqlQuerySpec querySpec, PartitionKeyRange targetRange, string collectionRid)
        {
            DocumentServiceRequest request = this.CreateDocumentServiceRequest(requestHeaders, querySpec);

            this.PopulatePartitionKeyRangeInfo(request, targetRange, collectionRid);
            request.Properties = this.feedOptions.Properties;
            return request;
        }

        public async Task<DocumentFeedResponse<CosmosElement>> ExecuteRequestLazyAsync(
    DocumentServiceRequest request,
    IDocumentClientRetryPolicy retryPolicyInstance,
    CancellationToken cancellationToken)
        {
            DocumentServiceResponse documentServiceResponse = await this.ExecuteQueryRequestInternalAsync(
                request,
                retryPolicyInstance,
                cancellationToken);

            return this.GetFeedResponse(request, documentServiceResponse);
        }

        public async Task<DocumentFeedResponse<CosmosElement>> ExecuteRequestAsync(
            DocumentServiceRequest request,
            IDocumentClientRetryPolicy retryPolicyInstance,
            CancellationToken cancellationToken)
        {
            return await (this.ShouldExecuteQueryRequest ?
                this.ExecuteQueryRequestAsync(request, retryPolicyInstance, cancellationToken) :
                this.ExecuteReadFeedRequestAsync(request, retryPolicyInstance, cancellationToken));
        }

        public async Task<DocumentFeedResponse<T>> ExecuteRequestAsync<T>(
            DocumentServiceRequest request,
            IDocumentClientRetryPolicy retryPolicyInstance,
            CancellationToken cancellationToken)
        {
            return await (this.ShouldExecuteQueryRequest ?
                this.ExecuteQueryRequestAsync<T>(request, retryPolicyInstance, cancellationToken) :
                this.ExecuteReadFeedRequestAsync<T>(request, retryPolicyInstance, cancellationToken));
        }

        public async Task<DocumentFeedResponse<CosmosElement>> ExecuteQueryRequestAsync(
            DocumentServiceRequest request,
            IDocumentClientRetryPolicy retryPolicyInstance,
            CancellationToken cancellationToken)
        {
            return this.GetFeedResponse(request, await this.ExecuteQueryRequestInternalAsync(request, retryPolicyInstance, cancellationToken));
        }

        public async Task<DocumentFeedResponse<T>> ExecuteQueryRequestAsync<T>(
            DocumentServiceRequest request,
            IDocumentClientRetryPolicy retryPolicyInstance,
            CancellationToken cancellationToken)
        {
            return this.GetFeedResponse<T>(await this.ExecuteQueryRequestInternalAsync(request, retryPolicyInstance, cancellationToken));
        }

        public async Task<DocumentFeedResponse<CosmosElement>> ExecuteReadFeedRequestAsync(
            DocumentServiceRequest request,
            IDocumentClientRetryPolicy retryPolicyInstance,
            CancellationToken cancellationToken)
        {
            return this.GetFeedResponse(request, await this.Client.ReadFeedAsync(request, retryPolicyInstance, cancellationToken));
        }

        public async Task<DocumentFeedResponse<T>> ExecuteReadFeedRequestAsync<T>(
            DocumentServiceRequest request,
            IDocumentClientRetryPolicy retryPolicyInstance,
            CancellationToken cancellationToken)
        {
            return this.GetFeedResponse<T>(await this.Client.ReadFeedAsync(request, retryPolicyInstance, cancellationToken));
        }

        public void PopulatePartitionKeyRangeInfo(DocumentServiceRequest request, PartitionKeyRange range, string collectionRid)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (range == null)
            {
                throw new ArgumentNullException("range");
            }

            if (this.ResourceTypeEnum.IsPartitioned())
            {
                request.RouteTo(new PartitionKeyRangeIdentity(collectionRid, range.Id));
            }
        }

        public async Task<PartitionKeyRange> GetTargetPartitionKeyRangeByIdAsync(string collectionResourceId, string partitionKeyRangeId)
        {
            IRoutingMapProvider routingMapProvider = await this.Client.GetRoutingMapProviderAsync();

            PartitionKeyRange range = await routingMapProvider.TryGetPartitionKeyRangeByIdAsync(collectionResourceId, partitionKeyRangeId, NoOpTrace.Singleton);
            if (range == null && PathsHelper.IsNameBased(this.ResourceLink))
            {
                // Refresh the cache and don't try to reresolve collection as it is not clear what already
                // happened based on previously resolved collection rid.
                // Return NotFoundException this time. Next query will succeed.
                // This can only happen if collection is deleted/created with same name and client was not restarted
                // inbetween.
                CollectionCache collectionCache = await this.Client.GetCollectionCacheAsync();
                collectionCache.Refresh(this.ResourceLink);
            }

            if (range == null)
            {
                throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: GetTargetPartitionKeyRangeById(collectionResourceId:{collectionResourceId}, partitionKeyRangeId: {partitionKeyRangeId}) failed due to stale cache");
            }

            return range;
        }

        internal Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkStringAsync(string collectionResourceId, string effectivePartitionKeyString)
        {
            return this.GetTargetPartitionKeyRangesAsync(collectionResourceId,
                new List<Range<string>>
                {
                    Range<string>.GetPointRange(effectivePartitionKeyString)
                });
        }

        internal async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(string collectionResourceId, List<Range<string>> providedRanges)
        {
            if (string.IsNullOrEmpty(nameof(collectionResourceId)))
            {
                throw new ArgumentNullException();
            }

            if (providedRanges == null || !providedRanges.Any())
            {
                throw new ArgumentNullException(nameof(providedRanges));
            }

            IRoutingMapProvider routingMapProvider = await this.Client.GetRoutingMapProviderAsync();

            List<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(collectionResourceId, providedRanges, NoOpTrace.Singleton);
            if (ranges == null && PathsHelper.IsNameBased(this.ResourceLink))
            {
                // Refresh the cache and don't try to re-resolve collection as it is not clear what already
                // happened based on previously resolved collection rid.
                // Return NotFoundException this time. Next query will succeed.
                // This can only happen if collection is deleted/created with same name and client was not restarted
                // in between.
                CollectionCache collectionCache = await this.Client.GetCollectionCacheAsync();
                collectionCache.Refresh(this.ResourceLink);
            }

            if (ranges == null)
            {
                throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: GetTargetPartitionKeyRanges(collectionResourceId:{collectionResourceId}, providedRanges: {string.Join(",", providedRanges)} failed due to stale cache");
            }

            return ranges;
        }

        public abstract void Dispose();

        protected abstract Task<DocumentFeedResponse<CosmosElement>> ExecuteInternalAsync(CancellationToken cancellationToken);

        protected async Task<List<PartitionKeyRange>> GetReplacementRangesAsync(PartitionKeyRange targetRange, string collectionRid)
        {
            IRoutingMapProvider routingMapProvider = await this.Client.GetRoutingMapProviderAsync();
            List<PartitionKeyRange> replacementRanges = (await routingMapProvider.TryGetOverlappingRangesAsync(collectionRid, targetRange.ToRange(), NoOpTrace.Singleton, forceRefresh: true)).ToList();
            string replaceMinInclusive = replacementRanges.First().MinInclusive;
            string replaceMaxExclusive = replacementRanges.Last().MaxExclusive;
            if (!replaceMinInclusive.Equals(targetRange.MinInclusive, StringComparison.Ordinal) || !replaceMaxExclusive.Equals(targetRange.MaxExclusive, StringComparison.Ordinal))
            {
                throw new InternalServerErrorException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Target range and Replacement range has mismatched min/max. Target range: [{0}, {1}). Replacement range: [{2}, {3}).",
                    targetRange.MinInclusive,
                    targetRange.MaxExclusive,
                    replaceMinInclusive,
                    replaceMaxExclusive));
            }

            return replacementRanges;
        }

        protected bool NeedPartitionKeyRangeCacheRefresh(DocumentClientException ex)
        {
            return ex.StatusCode == (HttpStatusCode)StatusCodes.Gone && ex.GetSubStatus() == SubStatusCodes.PartitionKeyRangeGone;
        }

        private async Task<DocumentServiceResponse> ExecuteQueryRequestInternalAsync(
            DocumentServiceRequest request,
            IDocumentClientRetryPolicy retryPolicyInstance,
            CancellationToken cancellationToken)
        {
            try
            {
                return await this.Client.ExecuteQueryAsync(request, retryPolicyInstance, cancellationToken);
            }
            finally
            {
                request.Body.Position = 0;
            }
        }

        private DocumentServiceRequest CreateDocumentServiceRequest(INameValueCollection requestHeaders, SqlQuerySpec querySpec)
        {
            DocumentServiceRequest request = querySpec != null ?
                this.CreateQueryDocumentServiceRequest(requestHeaders, querySpec) :
                this.CreateReadFeedDocumentServiceRequest(requestHeaders);

            if (this.feedOptions.JsonSerializerSettings != null)
            {
                request.SerializerSettings = this.feedOptions.JsonSerializerSettings;
            }

            return request;
        }

        private DocumentServiceRequest CreateQueryDocumentServiceRequest(INameValueCollection requestHeaders, SqlQuerySpec querySpec)
        {
            DocumentServiceRequest executeQueryRequest;

            string queryText;
            switch (this.Client.QueryCompatibilityMode)
            {
                case QueryCompatibilityMode.SqlQuery:
                    if (querySpec.Parameters != null && querySpec.Parameters.Count > 0)
                    {
                        throw new ArgumentException(
                            string.Format(CultureInfo.InvariantCulture, "Unsupported argument in query compatibility mode '{0}'", this.Client.QueryCompatibilityMode),
                            "querySpec.Parameters");
                    }

                    executeQueryRequest = DocumentServiceRequest.Create(
                        OperationType.SqlQuery,
                        this.ResourceTypeEnum,
                        this.ResourceLink,
                        AuthorizationTokenType.PrimaryMasterKey,
                        requestHeaders);

                    executeQueryRequest.Headers[HttpConstants.HttpHeaders.ContentType] = RuntimeConstants.MediaTypes.SQL;
                    queryText = querySpec.QueryText;
                    break;

                case QueryCompatibilityMode.Default:
                case QueryCompatibilityMode.Query:
                default:
                    executeQueryRequest = DocumentServiceRequest.Create(
                        OperationType.Query,
                        this.ResourceTypeEnum,
                        this.ResourceLink,
                        AuthorizationTokenType.PrimaryMasterKey,
                        requestHeaders);

                    executeQueryRequest.Headers[HttpConstants.HttpHeaders.ContentType] = RuntimeConstants.MediaTypes.QueryJson;
                    queryText = JsonConvert.SerializeObject(querySpec);
                    break;
            }

            executeQueryRequest.Body = new MemoryStream(Encoding.UTF8.GetBytes(queryText));
            return executeQueryRequest;
        }

        private DocumentServiceRequest CreateReadFeedDocumentServiceRequest(INameValueCollection requestHeaders)
        {
            if (this.ResourceTypeEnum == Microsoft.Azure.Documents.ResourceType.Database
                || this.ResourceTypeEnum == Microsoft.Azure.Documents.ResourceType.Offer
                || this.ResourceTypeEnum == Microsoft.Azure.Documents.ResourceType.Snapshot)
            {
                return DocumentServiceRequest.Create(
                    OperationType.ReadFeed,
                    null,
                    this.ResourceTypeEnum,
                    AuthorizationTokenType.PrimaryMasterKey,
                    requestHeaders);
            }
            else
            {
                return DocumentServiceRequest.Create(
                   OperationType.ReadFeed,
                   this.ResourceTypeEnum,
                   this.ResourceLink,
                   AuthorizationTokenType.PrimaryMasterKey,
                   requestHeaders);
            }
        }

        private void PopulatePartitionKeyInfo(DocumentServiceRequest request, PartitionKeyInternal partitionKey)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (this.ResourceTypeEnum.IsPartitioned())
            {
                if (partitionKey != null)
                {
                    request.Headers[HttpConstants.HttpHeaders.PartitionKey] = partitionKey.ToJsonString();
                }
            }
        }

        private DocumentFeedResponse<T> GetFeedResponse<T>(DocumentServiceResponse response)
        {
            long responseLengthBytes = response.ResponseBody.CanSeek ? response.ResponseBody.Length : 0;
            IEnumerable<T> responseFeed = response.GetQueryResponse<T>(this.ResourceType, this.getLazyFeedResponse, out int itemCount);

            return new DocumentFeedResponse<T>(responseFeed, itemCount, response.Headers, response.RequestStats, responseLengthBytes);
        }

        private DocumentFeedResponse<CosmosElement> GetFeedResponse(
                   DocumentServiceRequest documentServiceRequest,
                   DocumentServiceResponse documentServiceResponse)
        {
            // Execute the callback an each element of the page
            // For example just could get a response like this
            // {
            //    "_rid": "qHVdAImeKAQ=",
            //    "Documents": [{
            //        "id": "03230",
            //        "_rid": "qHVdAImeKAQBAAAAAAAAAA==",
            //        "_self": "dbs\/qHVdAA==\/colls\/qHVdAImeKAQ=\/docs\/qHVdAImeKAQBAAAAAAAAAA==\/",
            //        "_etag": "\"410000b0-0000-0000-0000-597916b00000\"",
            //        "_attachments": "attachments\/",
            //        "_ts": 1501107886
            //    }],
            //    "_count": 1
            // }
            // And you should execute the callback on each document in "Documents".
            MemoryStream memoryStream = new MemoryStream();
            documentServiceResponse.ResponseBody.CopyTo(memoryStream);
            long responseLengthBytes = memoryStream.Length;

            ReadOnlyMemory<byte> content;
            if (memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                content = buffer;
            }
            else
            {
                content = memoryStream.ToArray();
            }
            IJsonNavigator jsonNavigator = JsonNavigator.Create(content);

            string resourceName = this.GetRootNodeName(documentServiceRequest.ResourceType);

            if (!jsonNavigator.TryGetObjectProperty(
                jsonNavigator.GetRootNode(),
                resourceName,
                out ObjectProperty objectProperty))
            {
                throw new InvalidOperationException($"Response Body Contract was violated. QueryResponse did not have property: {resourceName}");
            }

            IJsonNavigatorNode cosmosElements = objectProperty.ValueNode;
            if (!(CosmosElement.Dispatch(
                jsonNavigator,
                cosmosElements) is CosmosArray cosmosArray))
            {
                throw new InvalidOperationException($"QueryResponse did not have an array of : {resourceName}");
            }

            int itemCount = cosmosArray.Count;
            return new DocumentFeedResponse<CosmosElement>(
                cosmosArray,
                itemCount,
                documentServiceResponse.Headers,
                documentServiceResponse.RequestStats,
                responseLengthBytes);
        }

        private string GetRootNodeName(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case Documents.ResourceType.Collection:
                    return "DocumentCollections";
                default:
                    return resourceType.ToResourceTypeString() + "s";
            }
        }
    }
}