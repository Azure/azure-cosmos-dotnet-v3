//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using ResourceIdentifier = Cosmos.Pagination.ResourceIdentifier;

    // Collection useful for mocking requests and repartitioning (splits / merge).
    internal sealed class InMemoryContainer : IMonadicDocumentContainer
    {
        private readonly PartitionKeyDefinition partitionKeyDefinition;
        private readonly Dictionary<int, (int, int)> parentToChildMapping;

        private PartitionKeyHashRangeDictionary<Records> partitionedRecords;
        private PartitionKeyHashRangeDictionary<List<Change>> partitionedChanges;
        private Dictionary<int, PartitionKeyHashRange> partitionKeyRangeIdToHashRange;
        private Dictionary<int, PartitionKeyHashRange> cachedPartitionKeyRangeIdToHashRange;

        public InMemoryContainer(
            PartitionKeyDefinition partitionKeyDefinition)
        {
            this.partitionKeyDefinition = partitionKeyDefinition ?? throw new ArgumentNullException(nameof(partitionKeyDefinition));
            PartitionKeyHashRange fullRange = new PartitionKeyHashRange(startInclusive: null, endExclusive: null);
            PartitionKeyHashRanges partitionKeyHashRanges = PartitionKeyHashRanges.Create(new PartitionKeyHashRange[] { fullRange });
            this.partitionedRecords = new PartitionKeyHashRangeDictionary<Records>(partitionKeyHashRanges);
            this.partitionedRecords[fullRange] = new Records();
            this.partitionedChanges = new PartitionKeyHashRangeDictionary<List<Change>>(partitionKeyHashRanges);
            this.partitionedChanges[fullRange] = new List<Change>();
            this.partitionKeyRangeIdToHashRange = new Dictionary<int, PartitionKeyHashRange>()
            {
                { 0, fullRange }
            };
            this.cachedPartitionKeyRangeIdToHashRange = new Dictionary<int, PartitionKeyHashRange>()
            {
                { 0, fullRange }
            };
            this.parentToChildMapping = new Dictionary<int, (int, int)>();
        }

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(
            ITrace trace,
            CancellationToken cancellationToken) => this.MonadicGetChildRangeAsync(
                FeedRangeEpk.FullRange,
                trace,
                cancellationToken);

        public async Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
            FeedRangeInternal feedRange,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (feedRange == null)
            {
                throw new ArgumentNullException(nameof(feedRange));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            using (ITrace getChildRangesTrace = trace.StartChild(name: "Get Child Ranges", TraceComponent.Routing, TraceLevel.Info))
            {
                FeedRangeEpk CreateRangeFromId(int id)
                {
                    PartitionKeyHashRange hashRange = this.cachedPartitionKeyRangeIdToHashRange[id];
                    return new FeedRangeEpk(
                        new Documents.Routing.Range<string>(
                            min: hashRange.StartInclusive.HasValue ? hashRange.StartInclusive.Value.ToString() : string.Empty,
                            max: hashRange.EndExclusive.HasValue ? hashRange.EndExclusive.Value.ToString() : string.Empty,
                            isMinInclusive: true,
                            isMaxInclusive: false));
                }

                if (feedRange is FeedRangePartitionKey)
                {
                    throw new ArgumentException("Can not get the child of a logical partition key");
                }

            if (feedRange.Equals(FeedRangeEpk.FullRange))
            {
                List<FeedRangeEpk> ranges = new List<FeedRangeEpk>();
                foreach (int id in this.cachedPartitionKeyRangeIdToHashRange.Keys)
                {
                    ranges.Add(CreateRangeFromId(id));
                }

                    return TryCatch<List<FeedRangeEpk>>.FromResult(ranges);
                }

            if (feedRange is FeedRangeEpk feedRangeEpk)
            {
                // look for overlapping epk ranges.
                List<FeedRangeEpk> overlappedIds;
                if (feedRangeEpk.Range.Min.Equals(FeedRangeEpk.FullRange.Range.Min) && feedRangeEpk.Range.Max.Equals(FeedRangeEpk.FullRange.Range.Max))
                {
                    overlappedIds = this.cachedPartitionKeyRangeIdToHashRange.Select(kvp => CreateRangeFromId(kvp.Key)).ToList();
                }
                else
                {
                    PartitionKeyHashRange hashRange = FeedRangeEpkToHashRange(feedRangeEpk);
                    overlappedIds = this.cachedPartitionKeyRangeIdToHashRange
                        .Where(kvp => hashRange.Contains(kvp.Value))
                        .Select(kvp => CreateRangeFromId(kvp.Key))
                        .ToList();
                }

                    if (overlappedIds.Count == 0)
                    {
                        return TryCatch<List<FeedRangeEpk>>.FromException(
                            new KeyNotFoundException(
                                $"PartitionKeyRangeId: {feedRangeEpk} does not exist."));
                    }

                    return TryCatch<List<FeedRangeEpk>>.FromResult(overlappedIds);
                }

                if (!(feedRange is FeedRangePartitionKeyRange feedRangePartitionKeyRange))
                {
                    throw new InvalidOperationException("Expected feed range to be a partition key range at this point.");
                }

                if (!int.TryParse(feedRangePartitionKeyRange.PartitionKeyRangeId, out int partitionKeyRangeId))
                {
                    return TryCatch<List<FeedRangeEpk>>.FromException(
                        new FormatException(
                            $"PartitionKeyRangeId: {feedRangePartitionKeyRange.PartitionKeyRangeId} is not an integer."));
                }

            if (!this.parentToChildMapping.TryGetValue(partitionKeyRangeId, out (int left, int right) children))
            {
                // This range has no children (base case)
                if (!this.cachedPartitionKeyRangeIdToHashRange.TryGetValue(partitionKeyRangeId, out PartitionKeyHashRange hashRange))
                {
                    return TryCatch<List<FeedRangeEpk>>.FromException(
                        new KeyNotFoundException(
                            $"PartitionKeyRangeId: {partitionKeyRangeId} does not exist."));
                }

                    List<FeedRangeEpk> singleRange = new List<FeedRangeEpk>()
                {
                    CreateRangeFromId(partitionKeyRangeId),
                };

                    return TryCatch<List<FeedRangeEpk>>.FromResult(singleRange);
                }

                // Recurse on the left and right child.
                FeedRangeInternal left = new FeedRangePartitionKeyRange(children.left.ToString());
                FeedRangeInternal right = new FeedRangePartitionKeyRange(children.right.ToString());

                TryCatch<List<FeedRangeEpk>> tryGetLeftRanges = await this.MonadicGetChildRangeAsync(left, trace, cancellationToken);
                if (tryGetLeftRanges.Failed)
                {
                    return tryGetLeftRanges;
                }

                TryCatch<List<FeedRangeEpk>> tryGetRightRanges = await this.MonadicGetChildRangeAsync(right, trace, cancellationToken);
                if (tryGetRightRanges.Failed)
                {
                    return tryGetRightRanges;
                }

                List<FeedRangeEpk> overlappingRanges = tryGetLeftRanges.Result.Concat(tryGetRightRanges.Result).ToList();
                return TryCatch<List<FeedRangeEpk>>.FromResult(overlappingRanges);
            }
        }

        public Task<TryCatch> MonadicRefreshProviderAsync(
            ITrace trace, 
            CancellationToken cancellationToken)
        {
            using (ITrace refreshProviderTrace = trace.StartChild("Refreshing FeedRangeProvider", TraceComponent.Routing, TraceLevel.Info))
            {
                this.cachedPartitionKeyRangeIdToHashRange = new Dictionary<int, PartitionKeyHashRange>(this.partitionKeyRangeIdToHashRange);
                return Task.FromResult(TryCatch.FromResult());
            }
        }

        public Task<TryCatch<Record>> MonadicCreateItemAsync(
            CosmosObject payload,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            PartitionKeyHash partitionKeyHash = GetHashFromPayload(payload, this.partitionKeyDefinition);
            if (!this.partitionedRecords.TryGetValue(partitionKeyHash, out Records records))
            {
                records = new Records();
                this.partitionedRecords[partitionKeyHash] = records;
            }

            int? pkrangeid = null;
            foreach (KeyValuePair<int, PartitionKeyHashRange> kvp in this.partitionKeyRangeIdToHashRange)
            {
                if (kvp.Value.Contains(partitionKeyHash))
                {
                    pkrangeid = kvp.Key;
                }
            }

            if (!pkrangeid.HasValue)
            {
                throw new InvalidOperationException();
            }

            Record recordAdded = records.Add(pkrangeid.Value, payload);

            if (!this.partitionedChanges.TryGetValue(partitionKeyHash, out List<Change> changes))
            {
                changes = new List<Change>();
                this.partitionedChanges[partitionKeyHash] = changes;
            }

            changes.Add(new Change(new DateTime(recordAdded.Timestamp), recordAdded));

            return Task.FromResult(TryCatch<Record>.FromResult(recordAdded));
        }

        public Task<TryCatch<Record>> MonadicReadItemAsync(
            CosmosElement partitionKey,
            string identifier,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            static Task<TryCatch<Record>> CreateNotFoundException(CosmosElement partitionKey, string identifer)
            {
                return Task.FromResult(
                    TryCatch<Record>.FromException(
                        new CosmosException(
                            message: $"Document with partitionKey: {partitionKey?.ToString() ?? "UNDEFINED"} and id: {identifer} not found.",
                            statusCode: System.Net.HttpStatusCode.NotFound,
                            subStatusCode: default,
                            activityId: Guid.NewGuid().ToString(),
                            requestCharge: 42)));
            }

            PartitionKeyHash partitionKeyHash = GetHashFromPartitionKey(
                partitionKey,
                this.partitionKeyDefinition);

            if (!this.partitionedRecords.TryGetValue(partitionKeyHash, out Records records))
            {
                return CreateNotFoundException(partitionKey, identifier);
            }

            foreach (Record candidate in records)
            {
                bool identifierMatches = candidate.Identifier == identifier;

                CosmosElement candidatePartitionKey = GetPartitionKeyFromPayload(
                    candidate.Payload,
                    this.partitionKeyDefinition);

                bool partitionKeyMatches;
                if (candidatePartitionKey is null && partitionKey is null)
                {
                    partitionKeyMatches = true;
                }
                else if ((candidatePartitionKey != null) && (partitionKey != null))
                {
                    partitionKeyMatches = candidatePartitionKey.Equals(partitionKey);
                }
                else
                {
                    partitionKeyMatches = false;
                }

                if (identifierMatches && partitionKeyMatches)
                {
                    return Task.FromResult(TryCatch<Record>.FromResult(candidate));
                }
            }

            return CreateNotFoundException(partitionKey, identifier);
        }

        public Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(
            ReadFeedState readFeedState,
            FeedRangeInternal feedRange,
            QueryRequestOptions queryRequestOptions,
            int pageSize,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (ITrace readFeed = trace.StartChild("Read Feed Transport", TraceComponent.Transport, TraceLevel.Info))
            {
                TryCatch<int> monadicPartitionKeyRangeId = this.MonadicGetPartitionKeyRangeIdFromFeedRange(feedRange);
                if (monadicPartitionKeyRangeId.Failed)
                {
                    return Task.FromResult(TryCatch<ReadFeedPage>.FromException(monadicPartitionKeyRangeId.Exception));
                }

                int partitionKeyRangeId = monadicPartitionKeyRangeId.Result;

                if (!this.partitionKeyRangeIdToHashRange.TryGetValue(
                    partitionKeyRangeId,
                    out PartitionKeyHashRange range))
                {
                    return Task.FromResult(
                        TryCatch<ReadFeedPage>.FromException(
                            new CosmosException(
                            message: $"PartitionKeyRangeId {partitionKeyRangeId} is gone",
                            statusCode: System.Net.HttpStatusCode.Gone,
                            subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                            activityId: Guid.NewGuid().ToString(),
                            requestCharge: 42)));
                }

                if (!this.partitionedRecords.TryGetValue(range, out Records records))
                {
                    throw new InvalidOperationException("failed to find the range.");
                }

                ulong documentIndex = (readFeedState == null) || readFeedState is ReadFeedBeginningState ? 0 : (ulong)Number64.ToLong(((CosmosNumber64)((ReadFeedContinuationState)readFeedState).ContinuationToken).Value);
                List<Record> page = records
                    .Where(record => record.ResourceIdentifier.Document > documentIndex)
                    .Take(pageSize)
                    .ToList();

                List<CosmosObject> documents = new List<CosmosObject>();
                foreach (Record record in page)
                {
                    CosmosObject document = ConvertRecordToCosmosElement(record);
                    documents.Add(CosmosObject.Create(document));
                }

                documents = FilterDocumentsWithFeedRange(documents, feedRange, this.partitionKeyDefinition);

                ReadFeedState continuationState = documents.Count == 0 ? null : ReadFeedState.Continuation(CosmosNumber64.Create(page.Last().ResourceIdentifier.Document));
                CosmosArray cosmosDocuments = CosmosArray.Create(documents);
                CosmosNumber cosmosCount = CosmosNumber64.Create(cosmosDocuments.Count);
                CosmosString cosmosRid = CosmosString.Create("AYIMAMmFOw8YAAAAAAAAAA==");

                Dictionary<string, CosmosElement> responseDictionary = new Dictionary<string, CosmosElement>()
                {
                    { "Documents", cosmosDocuments },
                    { "_count", cosmosCount },
                    { "_rid", cosmosRid },
                };
                CosmosObject cosmosResponse = CosmosObject.Create(responseDictionary);
                IJsonWriter jsonWriter = Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Text);
                cosmosResponse.WriteTo(jsonWriter);
                byte[] result = jsonWriter.GetResult().ToArray();
                MemoryStream responseStream = new MemoryStream(result);

                ReadFeedPage readFeedPage = new ReadFeedPage(
                    responseStream,
                    requestCharge: 42,
                    activityId: Guid.NewGuid().ToString(),
                    CosmosDiagnosticsContext.Create(default),
                    continuationState);

                return Task.FromResult(TryCatch<ReadFeedPage>.FromResult(readFeedPage));
            }
        }

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpec));
            }

            using (ITrace childTrace = trace.StartChild("Query Transport", TraceComponent.Transport, TraceLevel.Info))
            {
                TryCatch<int> monadicPartitionKeyRangeId = this.MonadicGetPartitionKeyRangeIdFromFeedRange(feedRange);
                if (monadicPartitionKeyRangeId.Failed)
                {
                    return Task.FromResult(TryCatch<QueryPage>.FromException(monadicPartitionKeyRangeId.Exception));
                }

                int partitionKeyRangeId = monadicPartitionKeyRangeId.Result;

                if (!this.partitionKeyRangeIdToHashRange.TryGetValue(
                    partitionKeyRangeId,
                    out PartitionKeyHashRange range))
                {
                    return Task.FromResult(TryCatch<QueryPage>.FromException(
                        new CosmosException(
                            message: $"PartitionKeyRangeId {partitionKeyRangeId} is gone",
                            statusCode: System.Net.HttpStatusCode.Gone,
                            subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                            activityId: Guid.NewGuid().ToString(),
                            requestCharge: 42)));
                }

                if (!this.partitionedRecords.TryGetValue(range, out Records records))
                {
                    throw new InvalidOperationException("failed to find the range.");
                }

                List<CosmosObject> documents = new List<CosmosObject>();
                foreach (Record record in records)
                {
                    CosmosObject document = ConvertRecordToCosmosElement(record);
                    documents.Add(CosmosObject.Create(document));
                }

                documents = FilterDocumentsWithFeedRange(documents, feedRange, this.partitionKeyDefinition);

                TryCatch<SqlQuery> monadicParse = SqlQueryParser.Monadic.Parse(sqlQuerySpec.QueryText);
                if (monadicParse.Failed)
                {
                    return Task.FromResult(TryCatch<QueryPage>.FromException(monadicParse.Exception));
                }

                SqlQuery sqlQuery = monadicParse.Result;
                IEnumerable<CosmosElement> queryResults = SqlInterpreter.ExecuteQuery(documents, sqlQuery);
                IEnumerable<CosmosElement> queryPageResults = queryResults;

                // Filter for the continuation token
                string continuationResourceId;
                int continuationSkipCount;

                if (continuationToken != null)
                {
                    CosmosObject parsedContinuationToken = CosmosObject.Parse(continuationToken);
                    continuationResourceId = ((CosmosString)parsedContinuationToken["resourceId"]).Value;
                    continuationSkipCount = (int)Number64.ToLong(((CosmosNumber64)parsedContinuationToken["skipCount"]).Value);

                    ResourceIdentifier continuationParsedResourceId = ResourceIdentifier.Parse(continuationResourceId);
                    queryPageResults = queryPageResults.Where((Func<CosmosElement, bool>)(c =>
                    {
                        ResourceId documentResourceId = ResourceId.Parse(((CosmosString)((CosmosObject)c)["_rid"]).Value);
                        return documentResourceId.Document >= continuationParsedResourceId.Document;
                    }));

                    for (int i = 0; i < continuationSkipCount; i++)
                    {
                        if (queryPageResults.FirstOrDefault() is CosmosObject firstDocument)
                        {
                            string currentResourceId = ((CosmosString)firstDocument["_rid"]).Value;
                            if (currentResourceId == continuationResourceId)
                            {
                                queryPageResults = queryPageResults.Skip(1);
                            }
                        }
                    }
                }
                else
                {
                    continuationResourceId = null;
                    continuationSkipCount = 0;
                }

                queryPageResults = queryPageResults.Take(pageSize);
                List<CosmosElement> queryPageResultList = queryPageResults.ToList();
                QueryState queryState;
                if (queryPageResultList.LastOrDefault() is CosmosObject lastDocument)
                {
                    string currentResourceId = ((CosmosString)lastDocument["_rid"]).Value;
                    int currentSkipCount = queryPageResultList
                        .Where(document => ((CosmosString)((CosmosObject)document)["_rid"]).Value == currentResourceId)
                        .Count();
                    if (currentResourceId == continuationResourceId)
                    {
                        currentSkipCount += continuationSkipCount;
                    }

                    CosmosObject queryStateValue = CosmosObject.Create(new Dictionary<string, CosmosElement>()
                {
                    { "resourceId", CosmosString.Create(currentResourceId) },
                    { "skipCount", CosmosNumber64.Create(currentSkipCount) },
                });

                    queryState = new QueryState(CosmosString.Create(queryStateValue.ToString()));
                }
                else
                {
                    queryState = default;
                }

                return Task.FromResult(
                    TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            queryPageResultList,
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            responseLengthInBytes: 1337,
                            cosmosQueryExecutionInfo: default,
                            disallowContinuationTokenMessage: default,
                            state: queryState)));
            }
        }

        public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
            ChangeFeedState state,
            FeedRangeInternal feedRange,
            int pageSize,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (feedRange == null)
            {
                throw new ArgumentNullException(nameof(feedRange));
            }

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            using (ITrace childTrace = trace.StartChild("Change Feed Transport", TraceComponent.Transport, TraceLevel.Info))
            {
                TryCatch<int> monadicPartitionKeyRangeId = this.MonadicGetPartitionKeyRangeIdFromFeedRange(feedRange);
                if (monadicPartitionKeyRangeId.Failed)
                {
                    return Task.FromResult(TryCatch<ChangeFeedPage>.FromException(monadicPartitionKeyRangeId.Exception));
                }

                int partitionKeyRangeId = monadicPartitionKeyRangeId.Result;

                if (!this.partitionKeyRangeIdToHashRange.TryGetValue(
                    partitionKeyRangeId,
                    out PartitionKeyHashRange range))
                {
                    return Task.FromResult(TryCatch<ChangeFeedPage>.FromException(
                        new CosmosException(
                            message: $"PartitionKeyRangeId {partitionKeyRangeId} is gone",
                            statusCode: System.Net.HttpStatusCode.Gone,
                            subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                            activityId: Guid.NewGuid().ToString(),
                            requestCharge: 42)));
                }

                if (!this.partitionedChanges.TryGetValue(range, out List<Change> changes))
                {
                    throw new InvalidOperationException("failed to find the range.");
                }

                List<Change> filteredChanges = changes
                    .Where(change => state.Accept(ChangeFeedPredicate.Singleton, change))
                    .Take(pageSize)
                    .ToList();

                if (filteredChanges.Count == 0)
                {
                    ChangeFeedState notModifiedResponseState = new ChangeFeedStateTime(DateTime.UtcNow);
                    return Task.FromResult(
                    TryCatch<ChangeFeedPage>.FromResult(
                        new ChangeFeedNotModifiedPage(
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            notModifiedResponseState)));
                }

                ChangeFeedState responseState = new ChangeFeedStateTime(filteredChanges.Last().Time.AddTicks(1).ToUniversalTime());

                List<CosmosObject> documents = new List<CosmosObject>();
                foreach (Change change in filteredChanges)
                {
                    CosmosObject document = ConvertRecordToCosmosElement(change.Record);
                    documents.Add(CosmosObject.Create(document));
                }

                documents = FilterDocumentsWithFeedRange(documents, feedRange, this.partitionKeyDefinition);

                CosmosArray cosmosDocuments = CosmosArray.Create(documents);
                CosmosNumber cosmosCount = CosmosNumber64.Create(cosmosDocuments.Count);
                CosmosString cosmosRid = CosmosString.Create("AYIMAMmFOw8YAAAAAAAAAA==");

                Dictionary<string, CosmosElement> responseDictionary = new Dictionary<string, CosmosElement>()
                {
                    { "Documents", cosmosDocuments },
                    { "_count", cosmosCount },
                    { "_rid", cosmosRid },
                };
                CosmosObject cosmosResponse = CosmosObject.Create(responseDictionary);
                IJsonWriter jsonWriter = Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Text);
                cosmosResponse.WriteTo(jsonWriter);
                byte[] result = jsonWriter.GetResult().ToArray();
                MemoryStream responseStream = new MemoryStream(result);

                return Task.FromResult(
                    TryCatch<ChangeFeedPage>.FromResult(
                        new ChangeFeedSuccessPage(
                            responseStream,
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            responseState)));
            }
        }

        public Task<TryCatch> MonadicSplitAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (feedRange is FeedRangePartitionKey)
            {
                throw new NotSupportedException("Can not split a logical partition");
            }

            TryCatch<int> monadicPartitionKeyRangeId = this.MonadicGetPartitionKeyRangeIdFromFeedRange(feedRange);
            if (monadicPartitionKeyRangeId.Failed)
            {
                return Task.FromResult(TryCatch.FromException(monadicPartitionKeyRangeId.Exception));
            }

            int partitionKeyRangeId = monadicPartitionKeyRangeId.Result;

            // Get the current range and records
            if (!this.partitionKeyRangeIdToHashRange.TryGetValue(
                partitionKeyRangeId,
                out PartitionKeyHashRange parentRange))
            {
                return Task.FromResult(
                    TryCatch.FromException(
                        new CosmosException(
                        message: $"PartitionKeyRangeId {partitionKeyRangeId} is gone",
                        statusCode: System.Net.HttpStatusCode.Gone,
                        subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                        activityId: Guid.NewGuid().ToString(),
                        requestCharge: 42)));
            }

            if (!this.partitionedRecords.TryGetValue(parentRange, out Records parentRecords))
            {
                throw new InvalidOperationException("failed to find the range.");
            }

            if (!this.partitionedChanges.TryGetValue(parentRange, out List<Change> parentChanges))
            {
                throw new InvalidOperationException("failed to find the range.");
            }

            int maxPartitionKeyRangeId = this.partitionKeyRangeIdToHashRange.Keys.Max();

            // Split the range space
            PartitionKeyHashRanges partitionKeyHashRanges = PartitionKeyHashRangeSplitterAndMerger.SplitRange(
                parentRange,
                rangeCount: 2);

            // Update the partition routing map
            this.parentToChildMapping[partitionKeyRangeId] = (maxPartitionKeyRangeId + 1, maxPartitionKeyRangeId + 2);
            Dictionary<int, PartitionKeyHashRange> newPartitionKeyRangeIdToHashRange = new Dictionary<int, PartitionKeyHashRange>()
            {
                { maxPartitionKeyRangeId + 1, partitionKeyHashRanges.First() },
                { maxPartitionKeyRangeId + 2, partitionKeyHashRanges.Last() },
            };

            foreach (KeyValuePair<int, PartitionKeyHashRange> kvp in this.partitionKeyRangeIdToHashRange)
            {
                int oldRangeId = kvp.Key;
                PartitionKeyHashRange oldRange = kvp.Value;
                if (!oldRange.Equals(parentRange))
                {
                    newPartitionKeyRangeIdToHashRange[oldRangeId] = oldRange;
                }
            }

            // Copy over the partitioned records (minus the parent range)
            PartitionKeyHashRangeDictionary<Records> newPartitionedRecords = new PartitionKeyHashRangeDictionary<Records>(
                PartitionKeyHashRanges.Create(newPartitionKeyRangeIdToHashRange.Values));

            newPartitionedRecords[partitionKeyHashRanges.First()] = new Records();
            newPartitionedRecords[partitionKeyHashRanges.Last()] = new Records();

            foreach (PartitionKeyHashRange range in this.partitionKeyRangeIdToHashRange.Values)
            {
                if (!range.Equals(parentRange))
                {
                    newPartitionedRecords[range] = this.partitionedRecords[range];
                }
            }

            PartitionKeyHashRangeDictionary<List<Change>> newPartitionedChanges = new PartitionKeyHashRangeDictionary<List<Change>>(
                PartitionKeyHashRanges.Create(newPartitionKeyRangeIdToHashRange.Values));

            newPartitionedChanges[partitionKeyHashRanges.First()] = new List<Change>();
            newPartitionedChanges[partitionKeyHashRanges.Last()] = new List<Change>();

            foreach (PartitionKeyHashRange range in this.partitionKeyRangeIdToHashRange.Values)
            {
                if (!range.Equals(parentRange))
                {
                    newPartitionedChanges[range] = this.partitionedChanges[range];
                }
            }

            this.partitionedRecords = newPartitionedRecords;
            this.partitionedChanges = newPartitionedChanges;
            this.partitionKeyRangeIdToHashRange = newPartitionKeyRangeIdToHashRange;

            // Rehash the records in the parent range
            foreach (Record record in parentRecords)
            {
                PartitionKeyHash partitionKeyHash = GetHashFromPayload(record.Payload, this.partitionKeyDefinition);
                if (!this.partitionedRecords.TryGetValue(partitionKeyHash, out Records records))
                {
                    records = new Records();
                    this.partitionedRecords[partitionKeyHash] = records;
                }

                records.Add(record);
            }

            // Rehash the changes in the parent range 
            foreach (Change change in parentChanges)
            {
                PartitionKeyHash partitionKeyHash = GetHashFromPayload(change.Record.Payload, this.partitionKeyDefinition);
                if (!this.partitionedChanges.TryGetValue(partitionKeyHash, out List<Change> changes))
                {
                    changes = new List<Change>();
                    this.partitionedChanges[partitionKeyHash] = changes;
                }

                changes.Add(change);
            }

            return Task.FromResult(TryCatch.FromResult());
        }

        public IEnumerable<int> PartitionKeyRangeIds => this.partitionKeyRangeIdToHashRange.Keys;

        private TryCatch<int> MonadicGetPkRangeIdFromEpk(FeedRangeEpk feedRangeEpk)
        {
            List<int> matchIds;
            if (feedRangeEpk.Range.Min.Equals(FeedRangeEpk.FullRange.Range.Min) && feedRangeEpk.Range.Max.Equals(FeedRangeEpk.FullRange.Range.Max))
            {
                matchIds = this.PartitionKeyRangeIds.ToList();
            }
            else
            {
                PartitionKeyHashRange hashRange = FeedRangeEpkToHashRange(feedRangeEpk);
                matchIds = this.partitionKeyRangeIdToHashRange
                    .Where(kvp => kvp.Value.Contains(hashRange) || hashRange.Contains(kvp.Value))
                    .Select(kvp => kvp.Key)
                    .ToList();
            }

            if (matchIds.Count != 1)
            {
                // Simulate a split exception, since we don't have a partition key range id to route to.
                CosmosException goneException = new CosmosException(
                    message: $"Epk Range: {feedRangeEpk.Range} is gone.",
                    statusCode: System.Net.HttpStatusCode.Gone,
                    subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                    activityId: Guid.NewGuid().ToString(),
                    requestCharge: default);

                return TryCatch<int>.FromException(goneException);
            }

            return TryCatch<int>.FromResult(matchIds[0]);
        }

        private static PartitionKeyHash GetHashFromPayload(
            CosmosObject payload,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            CosmosElement partitionKey = GetPartitionKeyFromPayload(payload, partitionKeyDefinition);
            return GetHashFromPartitionKey(partitionKey, partitionKeyDefinition);
        }

        private static PartitionKeyHash GetHashFromObjectModel(
            Cosmos.PartitionKey payload,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            CosmosElement partitionKey = GetPartitionKeyFromObjectModel(payload);
            return GetHashFromPartitionKey(partitionKey, partitionKeyDefinition);
        }

        private static CosmosElement GetPartitionKeyFromPayload(CosmosObject payload, PartitionKeyDefinition partitionKeyDefinition)
        {
            // Restrict the partition key definition for now to keep things simple
            if (partitionKeyDefinition.Kind != PartitionKind.Hash)
            {
                throw new ArgumentOutOfRangeException("Can only support hash partitioning");
            }

            if (partitionKeyDefinition.Version != Documents.PartitionKeyDefinitionVersion.V2)
            {
                throw new ArgumentOutOfRangeException("Can only support hash v2");
            }

            if (partitionKeyDefinition.Paths.Count != 1)
            {
                throw new ArgumentOutOfRangeException("Can only support a single partition key path.");
            }

            IEnumerable<string> tokens = partitionKeyDefinition.Paths[0].Split("/").Skip(1);

            CosmosElement partitionKey = payload;
            foreach (string token in tokens)
            {
                if (partitionKey != default)
                {
                    if (!payload.TryGetValue(token, out partitionKey))
                    {
                        partitionKey = default;
                    }
                }
            }

            return partitionKey;
        }

        private static CosmosElement GetPartitionKeyFromObjectModel(Cosmos.PartitionKey payload)
        {
            CosmosArray partitionKeyPayload = CosmosArray.Parse(payload.ToJsonString());
            if (partitionKeyPayload.Count != 1)
            {
                throw new ArgumentOutOfRangeException("Can only support a single partition key path.");
            }

            return partitionKeyPayload[0];
        }

        private static PartitionKeyHash GetHashFromPartitionKey(CosmosElement partitionKey, PartitionKeyDefinition partitionKeyDefinition)
        {
            // Restrict the partition key definition for now to keep things simple
            if (partitionKeyDefinition.Kind != PartitionKind.Hash)
            {
                throw new ArgumentOutOfRangeException("Can only support hash partitioning");
            }

            if (partitionKeyDefinition.Version != Documents.PartitionKeyDefinitionVersion.V2)
            {
                throw new ArgumentOutOfRangeException("Can only support hash v2");
            }

            if (partitionKeyDefinition.Paths.Count != 1)
            {
                throw new ArgumentOutOfRangeException("Can only support a single partition key path.");
            }

            PartitionKeyHash partitionKeyHash = partitionKey switch
            {
                null => PartitionKeyHash.V2.HashUndefined(),
                CosmosString stringPartitionKey => PartitionKeyHash.V2.Hash(stringPartitionKey.Value),
                CosmosNumber numberPartitionKey => PartitionKeyHash.V2.Hash(Number64.ToDouble(numberPartitionKey.Value)),
                CosmosBoolean cosmosBoolean => PartitionKeyHash.V2.Hash(cosmosBoolean.Value),
                CosmosNull _ => PartitionKeyHash.V2.HashNull(),
                _ => throw new ArgumentOutOfRangeException(),
            };
            return partitionKeyHash;
        }

        private static CosmosObject ConvertRecordToCosmosElement(Record record)
        {
            Dictionary<string, CosmosElement> keyValuePairs = new Dictionary<string, CosmosElement>
            {
                ["_rid"] = CosmosString.Create(record.ResourceIdentifier.ToString()),
                ["_ts"] = CosmosNumber64.Create(record.Timestamp),
                ["id"] = CosmosString.Create(record.Identifier)
            };

            foreach (KeyValuePair<string, CosmosElement> property in record.Payload)
            {
                keyValuePairs[property.Key] = property.Value;
            }

            return CosmosObject.Create(keyValuePairs);
        }

        private static List<CosmosObject> FilterDocumentsWithFeedRange(
            IReadOnlyList<CosmosObject> documents,
            FeedRange feedRange,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            List<CosmosObject> filteredDocuments;
            if (feedRange is FeedRangePartitionKey feedRangePartitionKey)
            {
                CosmosElement partitionKey = GetPartitionKeyFromObjectModel(feedRangePartitionKey.PartitionKey);
                filteredDocuments = documents.Where(
                    predicate: (document) =>
                    {
                        CosmosElement partitionKeyFromDocument = GetPartitionKeyFromPayload(document, partitionKeyDefinition);
                        return partitionKey.Equals(partitionKeyFromDocument);
                    }).ToList();
            }
            else if (feedRange is FeedRangeEpk feedRangeEpk)
            {
                PartitionKeyHashRange hashRange = FeedRangeEpkToHashRange(feedRangeEpk);
                filteredDocuments = documents.Where(
                    predicate: (document) =>
                    {
                        PartitionKeyHash hash = GetHashFromPayload(document, partitionKeyDefinition);
                        return hashRange.Contains(hash);
                    }).ToList();
            }
            else if (feedRange is FeedRangePartitionKeyRange)
            {
                // No need to filter 
                filteredDocuments = documents.ToList();
            }
            else
            {
                throw new NotImplementedException();
            }

            return filteredDocuments;
        }

        private TryCatch<int> MonadicGetPartitionKeyRangeIdFromFeedRange(FeedRange feedRange)
        {
            int partitionKeyRangeId;
            if (feedRange is FeedRangeEpk feedRangeEpk)
            {
                // Check to see if it lines up exactly with one physical partition
                TryCatch<int> monadicGetPkRangeIdFromEpkRange = this.MonadicGetPkRangeIdFromEpk(feedRangeEpk);
                if (monadicGetPkRangeIdFromEpkRange.Failed)
                {
                    return monadicGetPkRangeIdFromEpkRange;
                }

                partitionKeyRangeId = monadicGetPkRangeIdFromEpkRange.Result;
            }
            else if (feedRange is FeedRangePartitionKeyRange feedRangePartitionKeyRange)
            {
                partitionKeyRangeId = int.Parse(feedRangePartitionKeyRange.PartitionKeyRangeId);
            }
            else if (feedRange is FeedRangePartitionKey feedRangePartitionKey)
            {
                PartitionKeyHash partitionKeyHash = GetHashFromObjectModel(feedRangePartitionKey.PartitionKey, this.partitionKeyDefinition);

                int? foundValue = null;
                foreach (KeyValuePair<int, PartitionKeyHashRange> kvp in this.partitionKeyRangeIdToHashRange)
                {
                    if (kvp.Value.Contains(partitionKeyHash))
                    {
                        foundValue = kvp.Key;
                    }
                }

                if (!foundValue.HasValue)
                {
                    throw new InvalidOperationException("Failed to find value");
                }

                partitionKeyRangeId = foundValue.Value;
            }
            else
            {
                throw new NotImplementedException("Unknown feed range type");
            }

            return TryCatch<int>.FromResult(partitionKeyRangeId);
        }

        private static PartitionKeyHashRange FeedRangeEpkToHashRange(FeedRangeEpk feedRangeEpk)
        {
            PartitionKeyHash? start = feedRangeEpk.Range.Min == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(feedRangeEpk.Range.Min);
            PartitionKeyHash? end = feedRangeEpk.Range.Max == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(feedRangeEpk.Range.Max);
            PartitionKeyHashRange hashRange = new PartitionKeyHashRange(start, end);
            return hashRange;
        }

        public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
        {
            return Task.FromResult(TryCatch<string>.FromResult("AYIMAMmFOw8YAAAAAAAAAA=="));
        }

        private sealed class Records : IReadOnlyList<Record>
        {
            private readonly List<Record> storage;

            public Records()
            {
                this.storage = new List<Record>();
            }

            public Record this[int index] => this.storage[index];

            public int Count => this.storage.Count;

            public IEnumerator<Record> GetEnumerator() => this.storage.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => this.storage.GetEnumerator();

            public Record Add(int pkrangeid, CosmosObject payload)
            {
                // using pkrangeid for database since resource id doesnt serialize both document and pkrangeid.
                ResourceId currentResourceId;
                if (this.Count == 0)
                {
                    currentResourceId = ResourceId.Parse("AYIMAMmFOw8YAAAAAAAAAA==");

                    PropertyInfo documentProp = currentResourceId
                        .GetType()
                        .GetProperty("Document", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    documentProp.SetValue(currentResourceId, (ulong)1);

                    PropertyInfo databaseProp = currentResourceId
                        .GetType()
                        .GetProperty("Database", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    databaseProp.SetValue(currentResourceId, (uint)pkrangeid + 1);
                }
                else
                {
                    currentResourceId = this.storage[this.storage.Count - 1].ResourceIdentifier;
                }

                ResourceId nextResourceId = ResourceId.Parse("AYIMAMmFOw8YAAAAAAAAAA==");
                {
                    PropertyInfo documentProp = nextResourceId
                        .GetType()
                        .GetProperty("Document", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    documentProp.SetValue(nextResourceId, (ulong)(currentResourceId.Document + 1));

                    PropertyInfo databaseProp = nextResourceId
                        .GetType()
                        .GetProperty("Database", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    databaseProp.SetValue(nextResourceId, (uint)pkrangeid + 1);
                }

                Record record = new Record(nextResourceId, DateTime.UtcNow.Ticks, Guid.NewGuid().ToString(), payload);
                this.storage.Add(record);
                return record;
            }

            public Record Add(Record record)
            {
                this.storage.Add(record);
                return record;
            }
        }

        private readonly struct Change
        {
            public Change(DateTime time, Record record)
            {
                this.Time = time;
                this.Record = record ?? throw new ArgumentNullException(nameof(record));
            }

            public DateTime Time { get; }
            public Record Record { get; }
        }

        private sealed class ChangeFeedPredicate : IChangeFeedStateVisitor<Change, bool>
        {
            public static readonly ChangeFeedPredicate Singleton = new ChangeFeedPredicate();

            private ChangeFeedPredicate()
            {
            }

            public bool Visit(ChangeFeedStateBeginning changeFeedStateBeginning, Change input) => true;

            public bool Visit(ChangeFeedStateTime changeFeedStateTime, Change input) => input.Time >= changeFeedStateTime.StartTime;

            public bool Visit(ChangeFeedStateContinuation changeFeedStateContinuation, Change input)
            {
                DateTime time = DateTime.Parse(((CosmosString)changeFeedStateContinuation.ContinuationToken).Value);
                time = time.ToUniversalTime();
                ChangeFeedStateTime startTime = new ChangeFeedStateTime(time);
                return this.Visit(startTime, input);
            }

            public bool Visit(ChangeFeedStateNow changeFeedStateNow, Change input)
            {
                DateTime now = DateTime.UtcNow;
                ChangeFeedStateTime startTime = new ChangeFeedStateTime(now);
                return this.Visit(startTime, input);
            }
        }
    }
}
