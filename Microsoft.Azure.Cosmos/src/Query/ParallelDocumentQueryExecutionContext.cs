//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections.Generic;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;

    internal sealed class ParallelDocumentQueryExecutionContext : ParallelDocumentQueryExecutionContextBase<object>
    {
        private readonly string collectionRid;
        private readonly IDictionary<string, int> documentProducerPositionCache;
        private readonly Func<DocumentProducer<object>, int> taskPriorityFunc;
        private int currentDocumentProducerIndex;

        private ParallelDocumentQueryExecutionContext(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            string resourceLink,
            string rewrittenQuery,
            bool isContinuationExpected,
            bool getLazyFeedResponse,
            string collectionRid,
            Guid correlatedActivityId) :
            base(
            client,
            resourceTypeEnum,
            resourceType,
            expression,
            feedOptions,
            resourceLink,
            rewrittenQuery,
            correlatedActivityId,
            isContinuationExpected,
            getLazyFeedResponse,
            isDynamicPageSizeAllowed: false)
        {
            this.collectionRid = collectionRid;
            this.documentProducerPositionCache = new Dictionary<string, int>();
            this.taskPriorityFunc = (producer) => this.documentProducerPositionCache[producer.TargetRange.MinInclusive];
        }

        public override bool IsDone
        {
            get
            {
                return this.TaskScheduler.IsStopped ||
                    this.currentDocumentProducerIndex >= base.DocumentProducers.Count;
            }
        }

        protected override string ContinuationToken
        {
            get
            {
                if (!this.IsContinuationExpected)
                {
                    return this.DefaultContinuationToken;
                }

                if (this.IsDone)
                {
                    return null;
                }

                return (base.CurrentContinuationTokens.Count > 0) ?
                    JsonConvert.SerializeObject(
                    base.CurrentContinuationTokens.Select(kvp => new CompositeContinuationToken { Token = kvp.Value, Range = kvp.Key.TargetRange.ToRange() }),
                    DefaultJsonSerializationSettings.Value) : null;
            }
        }

        private DocumentProducer<object> CurrentDocumentProducer
        {
            get
            {
                if (this.currentDocumentProducerIndex < base.DocumentProducers.Count)
                {
                    return base.DocumentProducers[this.currentDocumentProducerIndex];
                }

                return null;
            }
        }

        private Guid ActivityId
        {
            get
            {

                if (this.currentDocumentProducerIndex >= base.DocumentProducers.Count)
                {
                    throw new InvalidOperationException("There is no active document producer");
                }

                return this.CurrentDocumentProducer.ActivityId;
            }
        }

        public static async Task<ParallelDocumentQueryExecutionContext> CreateAsync(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            string resourceLink,
            string collectionRid,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<PartitionKeyRange> targetRanges,
            int initialPageSize,
            bool isContinuationExpected,
            bool getLazyFeedResponse,
            string requestContinuation,
            CancellationToken token,
            Guid correlatedActivityId)
        {
            Debug.Assert(
                !partitionedQueryExecutionInfo.QueryInfo.HasOrderBy,
                "Parallel~Context must not have order by query info.");

            ParallelDocumentQueryExecutionContext context = new ParallelDocumentQueryExecutionContext(
                client,
                resourceTypeEnum,
                resourceType,
                expression,
                feedOptions,
                resourceLink,
                partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery,
                isContinuationExpected,
                getLazyFeedResponse,
                collectionRid,
                correlatedActivityId);

            await context.InitializeAsync(
                collectionRid,
                partitionedQueryExecutionInfo.QueryRanges,
                targetRanges,
                initialPageSize,
                requestContinuation,
                token);

            return context;
        }

        public override async Task<FeedResponse<object>> DrainAsync(int maxElements, CancellationToken token)
        {
            List<object> result = new List<object>();
            List<Uri> replicaUris = new List<Uri>();
            ClientSideRequestStatistics requestStats = new ClientSideRequestStatistics();

            while (!this.IsDone)
            {
                DocumentProducer<object> currentDocumentProducer = base.DocumentProducers[this.currentDocumentProducerIndex];

                if (currentDocumentProducer.IsAtContinuationBoundary)
                {
                    if (maxElements - result.Count < currentDocumentProducer.ItemsTillNextContinuationBoundary)
                    {
                        break;
                    }

                    base.CurrentContinuationTokens[currentDocumentProducer] = currentDocumentProducer.ResponseContinuation;

                    result.Add(currentDocumentProducer.Current);

                    if(currentDocumentProducer.RequestStatistics != null)
                    {
                        replicaUris.AddRange(currentDocumentProducer.RequestStatistics.ContactedReplicas);
                    }

                    while (currentDocumentProducer.ItemsTillNextContinuationBoundary > 1)
                    {
                        bool hasMoreResults = await currentDocumentProducer.MoveNextAsync(token);
                        Debug.Assert(hasMoreResults, "Expect hasMoreResults be true.");
                        result.Add(currentDocumentProducer.Current);
                    }
                }

                if (!await this.TryMoveNextProducerAsync(currentDocumentProducer, token))
                {
                    ++this.currentDocumentProducerIndex;

                    if (this.currentDocumentProducerIndex < base.DocumentProducers.Count
                        && !base.CurrentContinuationTokens.ContainsKey(base.DocumentProducers[this.currentDocumentProducerIndex]))
                    {
                        base.CurrentContinuationTokens[base.DocumentProducers[this.currentDocumentProducerIndex]] = null;
                    }

                    if (result.Count >= maxElements)
                    {
                        break;
                    }

                    continue;
                }

                if (maxElements >= int.MaxValue && result.Count > this.ActualMaxBufferedItemCount)
                {
                    break;
                }
            }

            this.ReduceTotalBufferedItems(result.Count);
            requestStats.ContactedReplicas.AddRange(replicaUris);

            return new FeedResponse<object>(result, result.Count, this.ResponseHeaders, requestStats, this.GetAndResetResponseLengthBytes());
        }

        private async Task InitializeAsync(
            string collectionRid,
            List<Range<string>> queryRanges,
            List<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            string requestContinuation,
            CancellationToken token)
        {
            this.InitializationSchedulingMetrics.Start();
            try
            {
                bool isContinuationNull = string.IsNullOrEmpty(requestContinuation);
                IReadOnlyList<PartitionKeyRange> filteredPartitionKeyRanges;
                Dictionary<string, CompositeContinuationToken> targetIndicesForFullContinuation = null;

                if (isContinuationNull)
                {
                    filteredPartitionKeyRanges = partitionKeyRanges;
                }
                else
                {
                    CompositeContinuationToken[] suppliedCompositeContinuationTokens = null;

                    try
                    {
                        suppliedCompositeContinuationTokens = JsonConvert.DeserializeObject<CompositeContinuationToken[]>(requestContinuation, DefaultJsonSerializationSettings.Value);

                        foreach (CompositeContinuationToken suppliedContinuationToken in suppliedCompositeContinuationTokens)
                        {
                            if (suppliedContinuationToken.Range == null || suppliedContinuationToken.Range.IsEmpty)
                            {
                                DefaultTrace.TraceWarning(
                                    string.Format(
                                    CultureInfo.InvariantCulture,
                                        "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | Invalid Range in the continuation token {3} for OrderBy~Context.",
                                        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                                        this.CorrelatedActivityId,
                                        this.DocumentProducers.Count != 0 ? this.ActivityId.ToString() : "No Activity ID yet.",
                                        requestContinuation));
                                throw new BadRequestException(RMResources.InvalidContinuationToken);
                            }
                        }

                        if (suppliedCompositeContinuationTokens.Length == 0)
                        {
                            DefaultTrace.TraceWarning(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | Invalid format for continuation token {3} for OrderBy~Context.",
                                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                                    this.CorrelatedActivityId,
                                    this.DocumentProducers.Count != 0 ? this.ActivityId.ToString() : "No Activity ID yet.",
                                    requestContinuation));
                            throw new BadRequestException(RMResources.InvalidContinuationToken);
                        }
                    }
                    catch (JsonException ex)
                    {
                        DefaultTrace.TraceWarning(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | Invalid JSON in continuation token {3} for Parallel~Context, exception: {4}",
                            DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                            this.CorrelatedActivityId,
                            this.DocumentProducers.Count !=0 ? this.ActivityId.ToString() :  "No Activity ID yet.",
                            requestContinuation,
                            ex.Message));

                        throw new BadRequestException(RMResources.InvalidContinuationToken, ex);
                    }

                    filteredPartitionKeyRanges = this.GetPartitionKeyRangesForContinuation(suppliedCompositeContinuationTokens, partitionKeyRanges, out targetIndicesForFullContinuation);
                }

                base.DocumentProducers.Capacity = filteredPartitionKeyRanges.Count;

                await base.InitializeAsync(
                    collectionRid,
                    queryRanges,
                    this.taskPriorityFunc,
                    filteredPartitionKeyRanges,
                    initialPageSize,
                    this.QuerySpec,
                    (targetIndicesForFullContinuation != null) ? targetIndicesForFullContinuation.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Token) : null,
                    token);

                this.PopulateDocumentProducerPositionCache();

                // Prefetch if necessary, and populate consume queue.
                if (this.ShouldPrefetch)
                {
                    foreach (var producer in base.DocumentProducers)
                    {
                        producer.TryScheduleFetch();
                    }
                }
            }
            finally
            {
                this.InitializationSchedulingMetrics.Stop();
                DefaultTrace.TraceInformation(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, CorrelatedActivityId: {1} | Parallel~Context.InitializeAsync, Scheduling Metrics: [{2}]",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    this.CorrelatedActivityId,
                    this.InitializationSchedulingMetrics));
            }
        }

        private IReadOnlyList<PartitionKeyRange> GetPartitionKeyRangesForContinuation(
            CompositeContinuationToken[] suppliedCompositeContinuationTokens,
            List<PartitionKeyRange> partitionKeyRanges,
            out Dictionary<string, CompositeContinuationToken> targetRangeToContinuationMap)
        {
            targetRangeToContinuationMap = new Dictionary<string, CompositeContinuationToken>();
            int minIndex = this.FindTargetRangeAndExtractContinuationTokens(
                partitionKeyRanges,
                suppliedCompositeContinuationTokens.Select(
                    token => Tuple.Create(token, token.Range)
                    ),
                out targetRangeToContinuationMap);

            return new PartialReadOnlyList<PartitionKeyRange>(
                partitionKeyRanges,
                minIndex,
                partitionKeyRanges.Count - minIndex);
        }

        private Task<bool> TryMoveNextProducerAsync(DocumentProducer<object> producer, CancellationToken cancellationToken)
        {
            return base.TryMoveNextProducerAsync(
                producer,
                (currentProducer) => this.RepairParallelContext(currentProducer),
                cancellationToken);
        }

        private async Task<DocumentProducer<object>> RepairParallelContext(DocumentProducer<object> producer)
        {
            List<PartitionKeyRange> replacementRanges = await base.GetReplacementRanges(producer.TargetRange, this.collectionRid);

            await base.RepairContextAsync(
                this.collectionRid,
                this.currentDocumentProducerIndex,
                this.taskPriorityFunc,
                replacementRanges,
                this.QuerySpec,
                () => this.PopulateDocumentProducerPositionCache(this.currentDocumentProducerIndex));

            return base.DocumentProducers[this.currentDocumentProducerIndex];
        }

        private void PopulateDocumentProducerPositionCache(int startingPosition = 0)
        {
            for (int index = startingPosition; index < this.DocumentProducers.Count; ++index)
            {
                this.documentProducerPositionCache[base.DocumentProducers[index].TargetRange.MinInclusive] = index;
            }
        }
    }
}
