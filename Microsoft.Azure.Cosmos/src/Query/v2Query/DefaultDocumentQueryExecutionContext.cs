//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
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
    /// Default document query execution context for single partition queries or for split proofing general requests.
    /// </summary>
    internal sealed class DefaultDocumentQueryExecutionContext : DocumentQueryExecutionContextBase
    {
        /// <summary>
        /// Whether or not a continuation is expected.
        /// </summary>
        private readonly bool isContinuationExpected;
        private readonly SchedulingStopwatch fetchSchedulingMetrics;
        private readonly FetchExecutionRangeAccumulator fetchExecutionRangeAccumulator;
        private readonly IDictionary<string, IReadOnlyList<Range<string>>> providedRangesCache;
        private readonly PartitionRoutingHelper partitionRoutingHelper;
        private long retries;

        public DefaultDocumentQueryExecutionContext(
            DocumentQueryExecutionContextBase.InitParams constructorParams,
            bool isContinuationExpected)
            : base(constructorParams)
        {
            this.isContinuationExpected = isContinuationExpected;
            this.fetchSchedulingMetrics = new SchedulingStopwatch();
            this.fetchSchedulingMetrics.Ready();
            this.fetchExecutionRangeAccumulator = new FetchExecutionRangeAccumulator();
            this.providedRangesCache = new Dictionary<string, IReadOnlyList<Range<string>>>();
            this.retries = -1;
            this.partitionRoutingHelper = new PartitionRoutingHelper();
        }

        public static Task<DefaultDocumentQueryExecutionContext> CreateAsync(
            DocumentQueryExecutionContextBase.InitParams constructorParams,
            bool isContinuationExpected,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(new DefaultDocumentQueryExecutionContext(
                constructorParams,
                isContinuationExpected));
        }

        public override void Dispose()
        {
        }

        protected override async Task<DocumentFeedResponse<CosmosElement>> ExecuteInternalAsync(CancellationToken token)
        {
            CollectionCache collectionCache = await this.Client.GetCollectionCacheAsync();
            PartitionKeyRangeCache partitionKeyRangeCache = await this.Client.GetPartitionKeyRangeCacheAsync();
            IDocumentClientRetryPolicy retryPolicyInstance = this.Client.ResetSessionTokenRetryPolicy.GetRequestPolicy();
            retryPolicyInstance = new InvalidPartitionExceptionRetryPolicy(retryPolicyInstance);
            if (base.ResourceTypeEnum.IsPartitioned())
            {
                retryPolicyInstance = new PartitionKeyRangeGoneRetryPolicy(
                    collectionCache,
                    partitionKeyRangeCache,
                    PathsHelper.GetCollectionPath(base.ResourceLink),
                    retryPolicyInstance);
            }

            return await BackoffRetryUtility<DocumentFeedResponse<CosmosElement>>.ExecuteAsync(
                async () =>
                {
                    this.fetchExecutionRangeAccumulator.BeginFetchRange();
                    ++this.retries;
                    Tuple<DocumentFeedResponse<CosmosElement>, string> responseAndPartitionIdentifier = await this.ExecuteOnceAsync(retryPolicyInstance, token);
                    DocumentFeedResponse<CosmosElement> response = responseAndPartitionIdentifier.Item1;
                    string partitionIdentifier = responseAndPartitionIdentifier.Item2;
                    if (!string.IsNullOrEmpty(response.ResponseHeaders[HttpConstants.HttpHeaders.QueryMetrics]))
                    {
                        this.fetchExecutionRangeAccumulator.EndFetchRange(
                            partitionIdentifier,
                            response.ActivityId,
                            response.Count,
                            this.retries);
                        response = new DocumentFeedResponse<CosmosElement>(
                            response,
                            response.Count,
                            response.Headers,
                            response.UseETagAsContinuation,
                            new Dictionary<string, QueryMetrics>
                            {
                                {
                                    partitionIdentifier,
                                    QueryMetrics.CreateFromDelimitedStringAndClientSideMetrics(
                                        response.ResponseHeaders[HttpConstants.HttpHeaders.QueryMetrics],
                                        new ClientSideMetrics(
                                            this.retries,
                                            response.RequestCharge,
                                            this.fetchExecutionRangeAccumulator.GetExecutionRanges(),
                                            string.IsNullOrEmpty(response.ResponseContinuation) ?
                                            new List<Tuple<string, SchedulingTimeSpan>>()
                                                {
                                                    new Tuple<string, SchedulingTimeSpan>(partitionIdentifier, this.fetchSchedulingMetrics.Elapsed)
                                                }
                                            : new List<Tuple<string, SchedulingTimeSpan>>()))
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

        private async Task<Tuple<DocumentFeedResponse<CosmosElement>, string>> ExecuteOnceAsync(
            IDocumentClientRetryPolicy retryPolicyInstance,
            CancellationToken cancellationToken)
        {
            // Don't reuse request, as the rest of client SDK doesn't reuse requests between retries.
            // The code leaves some temporary garbage in request (in RequestContext etc.),
            // which shold be erased during retries.
            using (DocumentServiceRequest request = await this.CreateRequestAsync())
            {
                DocumentFeedResponse<CosmosElement> feedRespose;
                string partitionIdentifier;
                // We need to determine how to execute the request:
                if (LogicalPartitionKeyProvided(request))
                {
                    feedRespose = await this.ExecuteRequestAsync(request, retryPolicyInstance, cancellationToken);
                    partitionIdentifier = $"PKId({request.Headers[HttpConstants.HttpHeaders.PartitionKey]})";
                }
                else if (PhysicalPartitionKeyRangeIdProvided(this))
                {
                    CollectionCache collectionCache = await this.Client.GetCollectionCacheAsync();
                    ContainerProperties collection = await collectionCache.ResolveCollectionAsync(request, CancellationToken.None);

                    request.RouteTo(new PartitionKeyRangeIdentity(collection.ResourceId, base.PartitionKeyRangeId));
                    feedRespose = await this.ExecuteRequestAsync(request, retryPolicyInstance, cancellationToken);
                    partitionIdentifier = base.PartitionKeyRangeId;
                }
                else
                {
                    // The query is going to become a full fan out, but we go one partition at a time.
                    if (ServiceInteropAvailable())
                    {
                        // Get the routing map provider
                        CollectionCache collectionCache = await this.Client.GetCollectionCacheAsync();
                        ContainerProperties collection = await collectionCache.ResolveCollectionAsync(request, CancellationToken.None);
                        QueryPartitionProvider queryPartitionProvider = await this.Client.GetQueryPartitionProviderAsync(cancellationToken);
                        IRoutingMapProvider routingMapProvider = await this.Client.GetRoutingMapProviderAsync();

                        // Figure out what partition you are going to based on the range from the continuation token
                        // If token is null then just start at partitionKeyRangeId "0"
                        List<CompositeContinuationToken> suppliedTokens;
                        Range<string> rangeFromContinuationToken =
                            this.partitionRoutingHelper.ExtractPartitionKeyRangeFromContinuationToken(
                                request.Headers,
                                out suppliedTokens);
                        Tuple<PartitionRoutingHelper.ResolvedRangeInfo, IReadOnlyList<Range<string>>> queryRoutingInfo =
                            await this.TryGetTargetPartitionKeyRangeAsync(
                                request,
                                collection,
                                queryPartitionProvider,
                                routingMapProvider,
                                rangeFromContinuationToken,
                                suppliedTokens);

                        if (request.IsNameBased && queryRoutingInfo == null)
                        {
                            request.ForceNameCacheRefresh = true;
                            collection = await collectionCache.ResolveCollectionAsync(request, CancellationToken.None);
                            queryRoutingInfo = await this.TryGetTargetPartitionKeyRangeAsync(
                                request,
                                collection,
                                queryPartitionProvider,
                                routingMapProvider,
                                rangeFromContinuationToken,
                                suppliedTokens);
                        }

                        if (queryRoutingInfo == null)
                        {
                            throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: Was not able to get queryRoutingInfo even after resolve collection async with force name cache refresh to the following collectionRid: {collection.ResourceId} with the supplied tokens: {JsonConvert.SerializeObject(suppliedTokens)}");
                        }

                        request.RouteTo(new PartitionKeyRangeIdentity(collection.ResourceId, queryRoutingInfo.Item1.ResolvedRange.Id));
                        DocumentFeedResponse<CosmosElement> response = await this.ExecuteRequestAsync(request, retryPolicyInstance, cancellationToken);

                        // Form a composite continuation token (range + backend continuation token).
                        // If the backend continuation token was null for the range, 
                        // then use the next adjacent range.
                        // This is how the default execution context serially visits every partition.
                        if (!await this.partitionRoutingHelper.TryAddPartitionKeyRangeToContinuationTokenAsync(
                            response.Headers,
                            providedPartitionKeyRanges: queryRoutingInfo.Item2,
                            routingMapProvider: routingMapProvider,
                            collectionRid: collection.ResourceId,
                            resolvedRangeInfo: queryRoutingInfo.Item1))
                        {
                            // Collection to which this request was resolved doesn't exist.
                            // Retry policy will refresh the cache and return NotFound.
                            throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: Call to TryAddPartitionKeyRangeToContinuationTokenAsync failed to the following collectionRid: {collection.ResourceId} with the supplied tokens: {JsonConvert.SerializeObject(suppliedTokens)}");
                        }

                        feedRespose = response;
                        partitionIdentifier = queryRoutingInfo.Item1.ResolvedRange.Id;
                    }
                    else
                    {
                        // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop for parsing the query, 
                        // so forcing the request through Gateway. We are also now by-passing this for 32-bit host process in NETFX on Windows
                        // as the ServiceInterop dll is only available in 64-bit.

                        request.UseGatewayMode = true;
                        feedRespose = await this.ExecuteRequestAsync(request, retryPolicyInstance, cancellationToken);
                        partitionIdentifier = "Gateway";
                    }
                }

                return new Tuple<DocumentFeedResponse<CosmosElement>, string>(feedRespose, partitionIdentifier);
            }
        }

        private static bool LogicalPartitionKeyProvided(DocumentServiceRequest request)
        {
            return !string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.PartitionKey])
                    || !request.ResourceType.IsPartitioned();
        }

        private static bool PhysicalPartitionKeyRangeIdProvided(DefaultDocumentQueryExecutionContext context)
        {
            return !string.IsNullOrEmpty(context.PartitionKeyRangeId);
        }

        private static bool ServiceInteropAvailable()
        {
            return !CustomTypeExtensions.ByPassQueryParsing();
        }

        private async Task<Tuple<PartitionRoutingHelper.ResolvedRangeInfo, IReadOnlyList<Range<string>>>> TryGetTargetPartitionKeyRangeAsync(
           DocumentServiceRequest request,
           ContainerProperties collection,
           QueryPartitionProvider queryPartitionProvider,
           IRoutingMapProvider routingMapProvider,
           Range<string> rangeFromContinuationToken,
           List<CompositeContinuationToken> suppliedTokens)
        {
            string version = request.Headers[HttpConstants.HttpHeaders.Version];
            version = string.IsNullOrEmpty(version) ? HttpConstants.Versions.CurrentVersion : version;

            bool enableCrossPartitionQuery = false;

            string enableCrossPartitionQueryHeader = request.Headers[HttpConstants.HttpHeaders.EnableCrossPartitionQuery];
            if (enableCrossPartitionQueryHeader != null)
            {
                if (!bool.TryParse(enableCrossPartitionQueryHeader, out enableCrossPartitionQuery))
                {
                    throw new BadRequestException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            RMResources.InvalidHeaderValue,
                            enableCrossPartitionQueryHeader,
                            HttpConstants.HttpHeaders.EnableCrossPartitionQuery));
                }
            }

            IReadOnlyList<Range<string>> providedRanges;
            if (!this.providedRangesCache.TryGetValue(collection.ResourceId, out providedRanges))
            {
                if (this.ShouldExecuteQueryRequest)
                {
                    FeedOptions feedOptions = this.GetFeedOptions(null);
                    PartitionKeyDefinition partitionKeyDefinition;
                    object partitionKeyDefinitionObject;
                    if (feedOptions.Properties != null && feedOptions.Properties.TryGetValue(CosmosQueryExecutionContextFactory.InternalPartitionKeyDefinitionProperty, out partitionKeyDefinitionObject))
                    {
                        if (partitionKeyDefinitionObject is PartitionKeyDefinition definition)
                        {
                            partitionKeyDefinition = definition;
                        }
                        else
                        {
                            throw new ArgumentException(
                                "partitionkeydefinition has invalid type",
                                nameof(partitionKeyDefinitionObject));
                        }
                    }
                    else
                    {
                        partitionKeyDefinition = collection.PartitionKey;
                    }

                    QueryInfo queryInfo;
                    providedRanges = PartitionRoutingHelper.GetProvidedPartitionKeyRanges(
                        (errorMessage) => new BadRequestException(errorMessage),
                        this.QuerySpec,
                        enableCrossPartitionQuery,
                        false,
                        this.isContinuationExpected,
                        false, //haslogicalpartitionkey
                        partitionKeyDefinition,
                        queryPartitionProvider,
                        version,
                        out queryInfo);
                }
                else if (request.Properties != null && request.Properties.TryGetValue(
                    WFConstants.BackendHeaders.EffectivePartitionKeyString,
                    out object effectivePartitionKey))
                {
                    if (effectivePartitionKey is string effectivePartitionKeyString)
                    {
                        providedRanges = new List<Range<string>>()
                        {
                            Range<string>.GetPointRange(effectivePartitionKeyString),
                        };
                    }
                    else
                    {
                        throw new ArgumentException(
                            "EffectivePartitionKey must be a string",
                            WFConstants.BackendHeaders.EffectivePartitionKeyString);
                    }
                }
                else
                {
                    providedRanges = new List<Range<string>>
                    {
                        new Range<string>(
                            PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                            PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                            true,
                            false)
                    };
                }

                this.providedRangesCache[collection.ResourceId] = providedRanges;
            }

            PartitionRoutingHelper.ResolvedRangeInfo resolvedRangeInfo = await this.partitionRoutingHelper.TryGetTargetRangeFromContinuationTokenRangeAsync(
                    providedRanges,
                    routingMapProvider,
                    collection.ResourceId,
                    rangeFromContinuationToken,
                    suppliedTokens);

            if (resolvedRangeInfo.ResolvedRange == null)
            {
                return null;
            }
            else
            {
                return Tuple.Create(resolvedRangeInfo, providedRanges);
            }
        }

        private async Task<DocumentServiceRequest> CreateRequestAsync()
        {
            INameValueCollection requestHeaders = await this.CreateCommonHeadersAsync(
                    this.GetFeedOptions(this.ContinuationToken));

            requestHeaders[HttpConstants.HttpHeaders.IsContinuationExpected] = isContinuationExpected.ToString();

            return this.CreateDocumentServiceRequest(
                requestHeaders,
                this.QuerySpec,
                this.PartitionKeyInternal);
        }
    }
}
