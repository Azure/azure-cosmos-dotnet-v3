//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Monads;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;

    internal class PartitionKeyRangeCache : IRoutingMapProvider, ICollectionRoutingMapCache
    {
        private const string PageSizeString = "-1";

        private readonly AsyncCache<string, CollectionRoutingMap> routingMapCache;

        private readonly IAuthorizationTokenProvider authorizationTokenProvider;
        private readonly IStoreModel storeModel;
        private readonly CollectionCache collectionCache;

        public PartitionKeyRangeCache(IAuthorizationTokenProvider authorizationTokenProvider, IStoreModel storeModel, CollectionCache collectionCache)
        {
            this.routingMapCache = new AsyncCache<string, CollectionRoutingMap>(
                    EqualityComparer<CollectionRoutingMap>.Default,
                    StringComparer.Ordinal);
            this.authorizationTokenProvider = authorizationTokenProvider;
            this.storeModel = storeModel;
            this.collectionCache = collectionCache;
        }

        public virtual async Task<TryCatch<IReadOnlyList<PartitionKeyRange>>> TryGetOverlappingRangesAsync(
            string collectionRid,
            Range<string> range,
            bool forceRefresh = false)
        {
            Debug.Assert(
                ResourceId.TryParse(collectionRid, out _),
                "Could not parse CollectionRid from ResourceId.");

            TryCatch<CollectionRoutingMap> tryLookupAsync = await this.TryLookupAsync(
                collectionRid,
                previousValue: null,
                request: null,
                CancellationToken.None);

            if (forceRefresh && tryLookupAsync.Succeeded)
            {
                tryLookupAsync = await this.TryLookupAsync(collectionRid, tryLookupAsync.Result, request: null, CancellationToken.None);
            }

            if (tryLookupAsync.Failed)
            {
                return TryCatch<IReadOnlyList<PartitionKeyRange>>.FromException(
                    CosmosExceptionFactory.CreateNotFoundException(
                        message: $"Could not find routing map for collection: {collectionRid} for range: {range}, forceRefresh:{forceRefresh}",
                        innerException: tryLookupAsync.Exception));
            }

            return TryCatch<IReadOnlyList<PartitionKeyRange>>.FromResult(
                tryLookupAsync.Result.GetOverlappingRanges(range));
        }

        public async Task<IReadOnlyList<PartitionKeyRange>> GetOverlappingRangesAsync(string collectionRid, Range<string> range)
        {
            TryCatch<IReadOnlyList<PartitionKeyRange>> tryGetOverlappingRangesAsync = await this.TryGetOverlappingRangesAsync(collectionRid, range, forceRefresh: true);
            tryGetOverlappingRangesAsync.ThrowIfFailed();

            return tryGetOverlappingRangesAsync.Result;
        }

        public async Task<TryCatch<PartitionKeyRange>> TryGetPartitionKeyRangeByIdAsync(
            string collectionResourceId,
            string partitionKeyRangeId,
            bool forceRefresh = false)
        {
            Debug.Assert(
                ResourceId.TryParse(collectionResourceId, out _),
                "Could not parse CollectionRid from ResourceId.");

            TryCatch<CollectionRoutingMap> tryLookupAsync = await this.TryLookupAsync(
                collectionResourceId,
                previousValue: null,
                request: null,
                CancellationToken.None);

            if (forceRefresh && tryLookupAsync.Succeeded)
            {
                tryLookupAsync = await this.TryLookupAsync(collectionResourceId, tryLookupAsync.Result, null, CancellationToken.None);
            }

            if (tryLookupAsync.Failed)
            {
                return TryCatch<PartitionKeyRange>.FromException(
                    CosmosExceptionFactory.CreateNotFoundException(
                        message: $"Could not find routing map for collection: {collectionResourceId} for PartitionKeyRangeId: {partitionKeyRangeId}, forceRefresh:{forceRefresh}",
                        innerException: tryLookupAsync.Exception));
            }

            if (!tryLookupAsync.Result.TryGetRangeByPartitionKeyRangeId(partitionKeyRangeId, out PartitionKeyRange partitionKeyRange))
            {
                return TryCatch<PartitionKeyRange>.FromException(
                    CosmosExceptionFactory.CreateNotFoundException(
                        message: $"Could not find routing map for collection: {collectionResourceId} for PartitionKeyRangeId: {partitionKeyRangeId}, forceRefresh:{forceRefresh}"));
            }

            return TryCatch<PartitionKeyRange>.FromResult(partitionKeyRange);
        }

        public async Task<PartitionKeyRange> GetPartitionKeyRangeByIdAsync(
            string collectionResourceId,
            string partitionKeyRangeId)
        {
            TryCatch<PartitionKeyRange> tryGetPartitionKeyRangeByIdAsync = await this.TryGetPartitionKeyRangeByIdAsync(collectionResourceId, partitionKeyRangeId, forceRefresh: true);
            tryGetPartitionKeyRangeByIdAsync.ThrowIfFailed();

            return tryGetPartitionKeyRangeByIdAsync.Result;
        }

        public virtual async Task<TryCatch<CollectionRoutingMap>> TryLookupAsync(
            string collectionRid,
            CollectionRoutingMap previousValue,
            DocumentServiceRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                CollectionRoutingMap collectionRoutingMap = await this.routingMapCache.GetAsync(
                    collectionRid,
                    previousValue,
                    () => this.GetRoutingMapForCollectionAsync(collectionRid, previousValue, cancellationToken),
                    CancellationToken.None);
                return TryCatch<CollectionRoutingMap>.FromResult(collectionRoutingMap);
            }
            catch (DocumentClientException ex)
            {
                if (previousValue != null)
                {
                    StringBuilder rangesString = new StringBuilder();
                    foreach (PartitionKeyRange range in previousValue.OrderedPartitionKeyRanges)
                    {
                        rangesString.Append(range.ToRange());
                        rangesString.Append(", ");
                    }

                    DefaultTrace.TraceInformation(
                        $"DocumentClientException in TryLookupAsync Collection: {collectionRid}, previousValue: {rangesString} Exception: {ex}");
                }

                if (ex.StatusCode != HttpStatusCode.NotFound)
                {
                    // Not a retryable exception so just rethrow.
                    throw;
                }

                return TryCatch<CollectionRoutingMap>.FromException(ex);
            }
        }

        public async Task<CollectionRoutingMap> LookupAsync(
            string collectionRid,
            CollectionRoutingMap previousValue,
            DocumentServiceRequest request,
            CancellationToken cancellationToken)
        {
            TryCatch<CollectionRoutingMap> tryLookupAsync = await this.TryLookupAsync(collectionRid, previousValue, request, cancellationToken);
            tryLookupAsync.ThrowIfFailed();

            return tryLookupAsync.Result;
        }

        public async Task<TryCatch<PartitionKeyRange>> TryGetRangeByPartitionKeyRangeIdAsync(string collectionRid, string partitionKeyRangeId)
        {
            try
            {
                CollectionRoutingMap routingMap = await this.routingMapCache.GetAsync(
                    collectionRid,
                    null,
                    () => this.GetRoutingMapForCollectionAsync(collectionRid, null, CancellationToken.None),
                    CancellationToken.None);

                if (!routingMap.TryGetRangeByPartitionKeyRangeId(partitionKeyRangeId, out PartitionKeyRange partitionKeyRange))
                {
                    return TryCatch<PartitionKeyRange>.FromException(
                        CosmosExceptionFactory.CreateNotFoundException(
                            message: $"Could not find partiton key range with id: {partitionKeyRangeId} in collection with rid: {collectionRid}"));
                }

                return TryCatch<PartitionKeyRange>.FromResult(partitionKeyRange);
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Not a retryable exception so just rethrow.
                    throw;
                }

                return TryCatch<PartitionKeyRange>.FromException(ex);
            }
        }

        private async Task<CollectionRoutingMap> GetRoutingMapForCollectionAsync(
            string collectionRid,
            CollectionRoutingMap previousRoutingMap,
            CancellationToken cancellationToken)
        {
            List<PartitionKeyRange> ranges = new List<PartitionKeyRange>();
            string changeFeedNextIfNoneMatch = previousRoutingMap?.ChangeFeedNextIfNoneMatch;

            HttpStatusCode lastStatusCode = HttpStatusCode.OK;
            do
            {
                INameValueCollection headers = new DictionaryNameValueCollection();

                headers.Set(HttpConstants.HttpHeaders.PageSize, PageSizeString);
                headers.Set(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);
                if (changeFeedNextIfNoneMatch != null)
                {
                    headers.Set(HttpConstants.HttpHeaders.IfNoneMatch, changeFeedNextIfNoneMatch);
                }

                RetryOptions retryOptions = new RetryOptions();
                using (DocumentServiceResponse response = await BackoffRetryUtility<DocumentServiceResponse>.ExecuteAsync(
                    () => this.ExecutePartitionKeyRangeReadChangeFeedAsync(collectionRid, headers),
                    new ResourceThrottleRetryPolicy(retryOptions.MaxRetryAttemptsOnThrottledRequests, retryOptions.MaxRetryWaitTimeInSeconds),
                    cancellationToken))
                {
                    lastStatusCode = response.StatusCode;
                    changeFeedNextIfNoneMatch = response.Headers[HttpConstants.HttpHeaders.ETag];

                    FeedResource<PartitionKeyRange> feedResource = response.GetResource<FeedResource<PartitionKeyRange>>();
                    if (feedResource != null)
                    {
                        ranges.AddRange(feedResource);
                    }
                }
            }
            while (lastStatusCode != HttpStatusCode.NotModified);

            IEnumerable<Tuple<PartitionKeyRange, ServiceIdentity>> tuples = ranges
                .Select(range => Tuple.Create(range, (ServiceIdentity)null));

            CollectionRoutingMap routingMap;
            if (previousRoutingMap == null)
            {
                // Splits could have happened during change feed query and we might have a mix of gone and new ranges.
                HashSet<string> goneRanges = new HashSet<string>(ranges.SelectMany(range => range.Parents ?? Enumerable.Empty<string>()));
                if (!CollectionRoutingMap.TryCreateCompleteRoutingMap(
                    tuples.Where(tuple => !goneRanges.Contains(tuple.Item1.Id)),
                    string.Empty,
                    changeFeedNextIfNoneMatch,
                    out routingMap))
                {
                    // Range information either doesn't exist or is not complete.
                    throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: GetRoutingMapForCollectionAsync(collectionRid: {collectionRid}), Range information either doesn't exist or is not complete.");
                }
            }
            else
            {
                if (!previousRoutingMap.TryCombine(tuples, changeFeedNextIfNoneMatch, out routingMap))
                {
                    throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: GetRoutingMapForCollectionAsync(collectionRid: {collectionRid}), Range information either doesn't exist or is not complete.");
                }
            }

            return routingMap;
        }

        private async Task<DocumentServiceResponse> ExecutePartitionKeyRangeReadChangeFeedAsync(string collectionRid, INameValueCollection headers)
        {
            using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.ReadFeed,
                collectionRid,
                ResourceType.PartitionKeyRange,
                AuthorizationTokenType.PrimaryMasterKey,
                headers))
            {
                string authorizationToken = null;
                try
                {
                    authorizationToken =
                        this.authorizationTokenProvider.GetUserAuthorizationToken(
                    request.ResourceAddress,
                    PathsHelper.GetResourcePath(request.ResourceType),
                    HttpConstants.HttpMethods.Get,
                    request.Headers,
                    AuthorizationTokenType.PrimaryMasterKey,
                    payload: out _);
                }
                catch (UnauthorizedException)
                {
                }

                request.Headers[HttpConstants.HttpHeaders.Authorization] = authorizationToken ?? throw new NotSupportedException("Resoruce tokens are not supported");

                using (new ActivityScope(Guid.NewGuid()))
                {
                    return await this.storeModel.ProcessMessageAsync(request);
                }
            }
        }
    }
}
