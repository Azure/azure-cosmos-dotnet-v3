//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Documents.RntbdConstants;

    internal class PartitionRoutingHelper
    {
        public static IReadOnlyList<Range<string>> GetProvidedPartitionKeyRanges(
            string querySpecJsonString,
            bool enableCrossPartitionQuery,
            bool parallelizeCrossPartitionQuery,
            bool isContinuationExpected,
            bool hasLogicalPartitionKey,
            bool allowDCount,
            bool allowNonValueAggregates,
            bool useSystemPrefix,
            PartitionKeyDefinition partitionKeyDefinition,
            Cosmos.VectorEmbeddingPolicy vectorEmbeddingPolicy,
            QueryPartitionProvider queryPartitionProvider,
            string clientApiVersion,
            Cosmos.GeospatialType geospatialType,
            out QueryInfo queryInfo)
        {
            if (querySpecJsonString == null)
            {
                throw new ArgumentNullException(nameof(querySpecJsonString));
            }

            if (partitionKeyDefinition == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyDefinition));
            }

            if (queryPartitionProvider == null)
            {
                throw new ArgumentNullException(nameof(queryPartitionProvider));
            }

            TryCatch<PartitionedQueryExecutionInfo> tryGetPartitionQueryExecutionInfo = queryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                querySpecJsonString: querySpecJsonString,
                partitionKeyDefinition: partitionKeyDefinition,
                vectorEmbeddingPolicy: vectorEmbeddingPolicy,
                requireFormattableOrderByQuery: VersionUtility.IsLaterThan(clientApiVersion, HttpConstants.VersionDates.v2016_11_14),
                isContinuationExpected: isContinuationExpected,
                allowNonValueAggregateQuery: allowNonValueAggregates,
                hasLogicalPartitionKey: hasLogicalPartitionKey,
                allowDCount: allowDCount,
                useSystemPrefix: useSystemPrefix,
                geospatialType: geospatialType);
            if (!tryGetPartitionQueryExecutionInfo.Succeeded)
            {
                throw new BadRequestException(tryGetPartitionQueryExecutionInfo.Exception);
            }

            PartitionedQueryExecutionInfo queryExecutionInfo = tryGetPartitionQueryExecutionInfo.Result;
            if (queryExecutionInfo?.QueryRanges == null ||
                queryExecutionInfo.QueryInfo == null ||
                queryExecutionInfo.QueryRanges.Any(range => range.Min == null || range.Max == null))
            {
                DefaultTrace.TraceInformation("QueryPartitionProvider returned bad query info");
            }

            bool isSinglePartitionQuery = queryExecutionInfo.QueryRanges.Count == 1 && queryExecutionInfo.QueryRanges[0].IsSingleValue;

            bool queryFansOutToMultiplePartitions = partitionKeyDefinition.Paths.Count > 0 && !isSinglePartitionQuery;
            if (queryFansOutToMultiplePartitions)
            {
                if (!enableCrossPartitionQuery)
                {
                    BadRequestException exception = new BadRequestException(RMResources.CrossPartitionQueryDisabled);
                    exception.Error.AdditionalErrorInfo = JsonConvert.SerializeObject(queryExecutionInfo);
                    throw exception;
                }
                else
                {
                    bool queryNotServiceableByGateway = parallelizeCrossPartitionQuery ||
                        queryExecutionInfo.QueryInfo.HasTop ||
                        queryExecutionInfo.QueryInfo.HasOrderBy ||
                        queryExecutionInfo.QueryInfo.HasAggregates ||
                        queryExecutionInfo.QueryInfo.HasDistinct ||
                        queryExecutionInfo.QueryInfo.HasOffset ||
                        queryExecutionInfo.QueryInfo.HasLimit ||
                        queryExecutionInfo.QueryInfo.HasGroupBy;

                    if (queryNotServiceableByGateway)
                    {
                        if (!IsSupportedPartitionedQueryExecutionInfo(queryExecutionInfo, clientApiVersion))
                        {
                            BadRequestException exception = new BadRequestException(RMResources.UnsupportedCrossPartitionQuery);
                            exception.Error.AdditionalErrorInfo = JsonConvert.SerializeObject(queryExecutionInfo);
                            throw exception;
                        }
                        else if (queryExecutionInfo.QueryInfo.HasAggregates && !IsAggregateSupportedApiVersion(clientApiVersion))
                        {
                            BadRequestException exception = new BadRequestException(RMResources.UnsupportedCrossPartitionQueryWithAggregate);
                            exception.Error.AdditionalErrorInfo = JsonConvert.SerializeObject(queryExecutionInfo);
                            throw exception;
                        }
                        else
                        {
                            DocumentClientException exception = new DocumentClientException(
                                RMResources.UnsupportedCrossPartitionQuery,
                                HttpStatusCode.BadRequest,
                                SubStatusCodes.CrossPartitionQueryNotServable);

                            exception.Error.AdditionalErrorInfo = JsonConvert.SerializeObject(queryExecutionInfo);
                            throw exception;
                        }
                    }
                }
            }
            else
            {
                if (queryExecutionInfo.QueryInfo.HasAggregates && !isContinuationExpected)
                {
                    // For single partition query with aggregate functions and no continuation expected, 
                    // we would try to accumulate the results for them on the SDK, if supported.

                    if (IsAggregateSupportedApiVersion(clientApiVersion))
                    {
                        DocumentClientException exception = new DocumentClientException(
                            RMResources.UnsupportedQueryWithFullResultAggregate,
                            HttpStatusCode.BadRequest,
                            SubStatusCodes.CrossPartitionQueryNotServable);

                        exception.Error.AdditionalErrorInfo = JsonConvert.SerializeObject(queryExecutionInfo);
                        throw exception;
                    }
                    else
                    {
                        throw new BadRequestException(RMResources.UnsupportedQueryWithFullResultAggregate);
                    }
                }
                else if (queryExecutionInfo.QueryInfo.HasDistinct)
                {
                    // If the query has DISTINCT then we have to reject it since the backend only returns
                    // elements that are DISTINCT within a page and we need the client to do post distinct processing
                    DocumentClientException exception = new DocumentClientException(
                        RMResources.UnsupportedCrossPartitionQuery,
                        HttpStatusCode.BadRequest,
                        SubStatusCodes.CrossPartitionQueryNotServable);

                    exception.Error.AdditionalErrorInfo = JsonConvert.SerializeObject(queryExecutionInfo);
                    throw exception;
                }
                else if (queryExecutionInfo.QueryInfo.HasGroupBy)
                {
                    // If the query has GROUP BY then we have to reject it since the backend only returns
                    // elements that are grouped within a page and we need the client to merge the groupings
                    DocumentClientException exception = new DocumentClientException(
                        RMResources.UnsupportedCrossPartitionQuery,
                        HttpStatusCode.BadRequest,
                        SubStatusCodes.CrossPartitionQueryNotServable);

                    exception.Error.AdditionalErrorInfo = JsonConvert.SerializeObject(queryExecutionInfo);
                    throw exception;
                }
            }

            queryInfo = queryExecutionInfo.QueryInfo;
            return queryExecutionInfo.QueryRanges;
        }

        /// <summary>
        /// Gets <see cref="PartitionKeyRange"/> instance which corresponds to <paramref name="rangeFromContinuationToken"/>
        /// </summary>
        /// <param name="providedPartitionKeyRanges"></param>
        /// <param name="routingMapProvider"></param>
        /// <param name="collectionRid"></param>
        /// <param name="rangeFromContinuationToken"></param>
        /// <param name="suppliedTokens"></param>
        /// <param name="trace"></param>
        /// <param name="direction"></param>
        /// <returns>null if collection with specified <paramref name="collectionRid"/> doesn't exist, which potentially means
        /// that collection was resolved to outdated Rid by name. Also null can be returned if <paramref name="rangeFromContinuationToken"/>
        /// is not found - this means it was split.
        /// </returns>
        public virtual async Task<ResolvedRangeInfo> TryGetTargetRangeFromContinuationTokenRangeAsync(
            IReadOnlyList<Range<string>> providedPartitionKeyRanges,
            IRoutingMapProvider routingMapProvider,
            string collectionRid,
            Range<string> rangeFromContinuationToken,
            List<CompositeContinuationToken> suppliedTokens,
            ITrace trace,
            RntdbEnumerationDirection direction = RntdbEnumerationDirection.Forward)
        {
            // For queries such as "SELECT * FROM root WHERE false", 
            // we will have empty ranges and just forward the request to the first partition
            if (providedPartitionKeyRanges.Count == 0)
            {
                return new ResolvedRangeInfo(
                    await routingMapProvider.TryGetRangeByEffectivePartitionKeyAsync(
                        collectionRid,
                        PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        trace),
                    suppliedTokens);
            }

            // Initially currentRange will be empty
            if (rangeFromContinuationToken.IsEmpty)
            {
                if (direction == RntdbEnumerationDirection.Reverse)
                {
                    IReadOnlyList<PartitionKeyRange> partitionKeyRanges = await routingMapProvider.TryGetOverlappingRangesAsync(
                        collectionRid, 
                        providedPartitionKeyRanges.Single(),
                        trace);
                    PartitionKeyRange lastPartitionKeyRange = partitionKeyRanges[partitionKeyRanges.Count - 1];

                    return new ResolvedRangeInfo(lastPartitionKeyRange, suppliedTokens);
                }

                Range<string> minimumRange = PartitionRoutingHelper.Min(
                providedPartitionKeyRanges,
                Range<string>.MinComparer.Instance);

                return new ResolvedRangeInfo(
                    await routingMapProvider.TryGetRangeByEffectivePartitionKeyAsync(collectionRid, minimumRange.Min, trace),
                    suppliedTokens);
            }

            PartitionKeyRange targetPartitionKeyRange = await routingMapProvider.TryGetRangeByEffectivePartitionKeyAsync(collectionRid, rangeFromContinuationToken.Min, trace);

            if (targetPartitionKeyRange == null)
            {
                return new ResolvedRangeInfo(null, suppliedTokens);
            }

            if (!rangeFromContinuationToken.Equals(targetPartitionKeyRange.ToRange()))
            {
                // Cannot find target range. Either collection was resolved incorrectly or the range was split
                IReadOnlyList<PartitionKeyRange> replacedRanges = await routingMapProvider.TryGetOverlappingRangesAsync(
                        collectionResourceId: collectionRid,
                        range: rangeFromContinuationToken,
                        trace: trace,
                        forceRefresh: true);

                if (replacedRanges == null || replacedRanges.Count < 1)
                {
                    return new ResolvedRangeInfo(null, null);
                }
                else
                {
                    if (!(replacedRanges[0].MinInclusive.Equals(rangeFromContinuationToken.Min) && replacedRanges[replacedRanges.Count - 1].MaxExclusive.Equals(rangeFromContinuationToken.Max)))
                    {
                        return new ResolvedRangeInfo(null, null);
                    }
                }

                if (direction == RntdbEnumerationDirection.Reverse)
                {
                    replacedRanges = new ReadOnlyCollection<PartitionKeyRange>(replacedRanges.Reverse().ToList());
                }

                List<CompositeContinuationToken> continuationTokensToBePersisted = null;

                if (suppliedTokens != null && suppliedTokens.Count > 0)
                {
                    continuationTokensToBePersisted = new List<CompositeContinuationToken>(replacedRanges.Count + suppliedTokens.Count - 1);

                    foreach (PartitionKeyRange partitionKeyRange in replacedRanges)
                    {
                        CompositeContinuationToken token = (CompositeContinuationToken)suppliedTokens[0].ShallowCopy();
                        token.Range = partitionKeyRange.ToRange();
                        continuationTokensToBePersisted.Add(token);
                    }

                    continuationTokensToBePersisted.AddRange(suppliedTokens.Skip(1));
                }

                return new ResolvedRangeInfo(replacedRanges[0], continuationTokensToBePersisted);
            }

            return new ResolvedRangeInfo(targetPartitionKeyRange, suppliedTokens);
        }

        public static async Task<List<PartitionKeyRange>> GetReplacementRangesAsync(PartitionKeyRange targetRange, 
                                                            IRoutingMapProvider routingMapProvider, 
                                                            string collectionRid,
                                                            ITrace trace)
        {
            return (await routingMapProvider.TryGetOverlappingRangesAsync(
                collectionRid, 
                targetRange.ToRange(), 
                trace, 
                forceRefresh: true)).ToList();
        }

        /// <summary>
        /// TryAddPartitionKeyRangeToContinuationTokenAsync
        /// </summary>
        /// <returns><c>false</c> if collectionRid is likely wrong because range was not found. Cache needs to be refreshed probably.</returns>
        public virtual async Task<bool> TryAddPartitionKeyRangeToContinuationTokenAsync(
            INameValueCollection backendResponseHeaders,
            IReadOnlyList<Range<string>> providedPartitionKeyRanges,
            IRoutingMapProvider routingMapProvider,
            string collectionRid,
            ResolvedRangeInfo resolvedRangeInfo,
            ITrace trace,
            RntdbEnumerationDirection direction = RntdbEnumerationDirection.Forward)
        {
            Debug.Assert(resolvedRangeInfo.ResolvedRange != null, "ResolvedRange can't be null");

            PartitionKeyRange currentRange = resolvedRangeInfo.ResolvedRange;

            // IF : Split happened, or already had multiple target ranges in the continuation
            if (resolvedRangeInfo.ContinuationTokens != null && resolvedRangeInfo.ContinuationTokens.Count > 1)
            {
                if (!string.IsNullOrEmpty(backendResponseHeaders[HttpConstants.HttpHeaders.Continuation]))
                {
                    resolvedRangeInfo.ContinuationTokens[0].Token = backendResponseHeaders[HttpConstants.HttpHeaders.Continuation];
                }
                else
                {
                    resolvedRangeInfo.ContinuationTokens.RemoveAt(0);
                }

                backendResponseHeaders[HttpConstants.HttpHeaders.Continuation] = JsonConvert.SerializeObject(resolvedRangeInfo.ContinuationTokens);
            }
            else
            {
                //// ELSE: Single target Range was provided, and no split happened

                PartitionKeyRange rangeToUse = currentRange;

                // We only need to get the next range if we have to
                if (string.IsNullOrEmpty(backendResponseHeaders[HttpConstants.HttpHeaders.Continuation]))
                {
                    if (direction == RntdbEnumerationDirection.Reverse)
                    {
                        rangeToUse = PartitionRoutingHelper.MinBefore(
                            (await routingMapProvider.TryGetOverlappingRangesAsync(
                                collectionRid, 
                                providedPartitionKeyRanges.Single(),
                                trace)).ToList(),
                            currentRange);
                    }
                    else
                    {
                        Range<string> nextProvidedRange = PartitionRoutingHelper.MinAfter(
                        providedPartitionKeyRanges,
                        currentRange.ToRange(),
                        Range<string>.MaxComparer.Instance);

                        if (nextProvidedRange == null)
                        {
                            return true;
                        }

                        string max = string.CompareOrdinal(nextProvidedRange.Min, currentRange.MaxExclusive) > 0
                             ? nextProvidedRange.Min
                             : currentRange.MaxExclusive;

                        if (string.CompareOrdinal(max, PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey) == 0)
                        {
                            return true;
                        }

                        PartitionKeyRange nextRange = await routingMapProvider.TryGetRangeByEffectivePartitionKeyAsync(collectionRid, max, trace);
                        if (nextRange == null)
                        {
                            return false;
                        }

                        rangeToUse = nextRange;
                    }
                }

                if (rangeToUse != null)
                {
                    backendResponseHeaders[HttpConstants.HttpHeaders.Continuation] = PartitionRoutingHelper.AddPartitionKeyRangeToContinuationToken(
                        backendResponseHeaders[HttpConstants.HttpHeaders.Continuation],
                        rangeToUse);
                }
            }

            return true;
        }

        public virtual Range<string> ExtractPartitionKeyRangeFromContinuationToken(INameValueCollection headers, out List<CompositeContinuationToken> compositeContinuationTokens)
        {
            if (headers == null)
            {
                throw new ArgumentNullException("headers");
            }

            compositeContinuationTokens = null;

            Range<string> range = Range<string>.GetEmptyRange(PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey);

            if (string.IsNullOrEmpty(headers[HttpConstants.HttpHeaders.Continuation]))
            {
                return range;
            }

            string providedContinuation = headers[HttpConstants.HttpHeaders.Continuation];
            CompositeContinuationToken initialContinuationToken = null;

            if (!string.IsNullOrEmpty(providedContinuation))
            {
                try
                {
                    if (providedContinuation.Trim().StartsWith("[", StringComparison.Ordinal))
                    {
                        compositeContinuationTokens = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(providedContinuation);

                        if (compositeContinuationTokens != null && compositeContinuationTokens.Count > 0)
                        {
                            headers[HttpConstants.HttpHeaders.Continuation] = compositeContinuationTokens[0].Token;
                            initialContinuationToken = compositeContinuationTokens[0];
                        }
                        else
                        {
                            headers.Remove(HttpConstants.HttpHeaders.Continuation);
                        }
                    }
                    else
                    {
                        // TODO: Remove the else logic after the gateway deployment is complete
                        initialContinuationToken = JsonConvert.DeserializeObject<CompositeContinuationToken>(providedContinuation);
                        if (initialContinuationToken != null)
                        {
                            compositeContinuationTokens = new List<CompositeContinuationToken> { initialContinuationToken };
                        }
                        else
                        {
                            throw new BadRequestException(RMResources.InvalidContinuationToken);
                        }
                    }

                    if (initialContinuationToken?.Range != null)
                    {
                        range = initialContinuationToken.Range;
                    }

                    if (initialContinuationToken != null && !string.IsNullOrEmpty(initialContinuationToken.Token))
                    {
                        headers[HttpConstants.HttpHeaders.Continuation] = initialContinuationToken.Token;
                    }
                    else
                    {
                        headers.Remove(HttpConstants.HttpHeaders.Continuation);
                    }
                }
                catch (JsonException ex)
                {
                    DefaultTrace.TraceWarning(
                        string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} Invalid JSON in the continuation token {1}",
                        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                        providedContinuation));
                    throw new BadRequestException(RMResources.InvalidContinuationToken, ex);
                }
            }
            else
            {
                headers.Remove(HttpConstants.HttpHeaders.Continuation);
            }

            return range;
        }

        private static string AddPartitionKeyRangeToContinuationToken(string continuationToken, PartitionKeyRange partitionKeyRange)
        {
            return JsonConvert.SerializeObject(new CompositeContinuationToken
            {
                Token = continuationToken,
                Range = partitionKeyRange.ToRange(),
            });
        }

        private static bool IsSupportedPartitionedQueryExecutionInfo(
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfoueryInfo,
            string clientApiVersion)
        {
            if (VersionUtility.IsLaterThan(clientApiVersion, HttpConstants.VersionDates.v2016_07_11))
            {
                return partitionedQueryExecutionInfoueryInfo.Version <= Constants.PartitionedQueryExecutionInfo.CurrentVersion;
            }

            return false;
        }

        private static bool IsAggregateSupportedApiVersion(string clientApiVersion)
        {
            return VersionUtility.IsLaterThan(clientApiVersion, HttpConstants.VersionDates.v2016_11_14);
        }

        private static T Min<T>(IReadOnlyList<T> values, IComparer<T> comparer)
        {
            if (values.Count == 0)
            {
                throw new ArgumentException(nameof(values));
            }

            T min = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                if (comparer.Compare(values[i], min) < 0)
                {
                    min = values[i];
                }
            }

            return min;
        }

        private static T MinAfter<T>(IReadOnlyList<T> values, T minValue, IComparer<T> comparer)
            where T : class
        {
            if (values.Count == 0)
            {
                throw new ArgumentException(nameof(values));
            }

            T min = null;
            foreach (T value in values)
            {
                if (comparer.Compare(value, minValue) > 0 && (min == null || comparer.Compare(value, min) < 0))
                {
                    min = value;
                }
            }

            return min;
        }

        private static PartitionKeyRange MinBefore(IReadOnlyList<PartitionKeyRange> values, PartitionKeyRange minValue)
        {
            if (values.Count == 0)
            {
                throw new ArgumentException(nameof(values));
            }

            IComparer<Range<string>> comparer = Range<string>.MinComparer.Instance;
            PartitionKeyRange min = null;
            foreach (PartitionKeyRange value in values)
            {
                if (comparer.Compare(value.ToRange(), minValue.ToRange()) < 0 && (min == null || comparer.Compare(value.ToRange(), min.ToRange()) > 0))
                {
                    min = value;
                }
            }

            return min;
        }

        public readonly struct ResolvedRangeInfo
        {
            public readonly PartitionKeyRange ResolvedRange;
            public readonly List<CompositeContinuationToken> ContinuationTokens;

            public ResolvedRangeInfo(PartitionKeyRange range, List<CompositeContinuationToken> continuationTokens)
            {
                this.ResolvedRange = range;
                this.ContinuationTokens = continuationTokens;
            }
        }
    }
}
