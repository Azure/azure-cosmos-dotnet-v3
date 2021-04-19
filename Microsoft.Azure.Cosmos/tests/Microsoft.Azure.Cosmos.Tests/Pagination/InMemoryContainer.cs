//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
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
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
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
                    List<FeedRangeEpk> overlappingRanges;
                    if (feedRangeEpk.Range.Min.Equals(FeedRangeEpk.FullRange.Range.Min) && feedRangeEpk.Range.Max.Equals(FeedRangeEpk.FullRange.Range.Max))
                    {
                        overlappingRanges = this.cachedPartitionKeyRangeIdToHashRange.Select(kvp => CreateRangeFromId(kvp.Key)).ToList();
                    }
                    else
                    {
                        overlappingRanges = new List<FeedRangeEpk>();
                        PartitionKeyHashRange userRange = FeedRangeEpkToHashRange(feedRangeEpk);
                        foreach (PartitionKeyHashRange systemRange in this.cachedPartitionKeyRangeIdToHashRange.Values)
                        {
                            if (userRange.TryGetOverlappingRange(systemRange, out PartitionKeyHashRange overlappingRange))
                            {
                                overlappingRanges.Add(HashRangeToFeedRangeEpk(overlappingRange));
                            }
                        }
                    }

                    if (overlappingRanges.Count == 0)
                    {
                        return TryCatch<List<FeedRangeEpk>>.FromException(
                            new KeyNotFoundException(
                                $"PartitionKeyRangeId: {feedRangeEpk} does not exist."));
                    }

                    return TryCatch<List<FeedRangeEpk>>.FromResult(overlappingRanges);
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

                List<FeedRangeEpk> recursiveOverlappingRanges = tryGetLeftRanges.Result.Concat(tryGetRightRanges.Result).ToList();
                return TryCatch<List<FeedRangeEpk>>.FromResult(recursiveOverlappingRanges);
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

            ulong maxLogicalSequenceNumber = changes.Count == 0 ? 0 : changes.Select(change => change.LogicalSequenceNumber).Max();

            Change change = new Change(
                recordAdded,
                partitionKeyRangeId: (ulong)pkrangeid.Value,
                logicalSequenceNumber: maxLogicalSequenceNumber + 1);

            changes.Add(change);
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
            FeedRangeState<ReadFeedState> feedRangeState,
            ReadFeedPaginationOptions readFeedPaginationOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            readFeedPaginationOptions ??= ReadFeedPaginationOptions.Default;

            using (ITrace readFeed = trace.StartChild("Read Feed Transport", TraceComponent.Transport, TraceLevel.Info))
            {
                TryCatch<int> monadicPartitionKeyRangeId = this.MonadicGetPartitionKeyRangeIdFromFeedRange(feedRangeState.FeedRange);
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

                (ulong pkrangeId, ulong documentIndex) rangeIdAndIndex;
                if (feedRangeState.State is ReadFeedBeginningState)
                {
                    rangeIdAndIndex = (0, 0);
                }
                else if (feedRangeState.State is ReadFeedContinuationState readFeedContinuationState)
                {
                    ResourceIdentifier resourceIdentifier = ResourceIdentifier.Parse(((CosmosString)readFeedContinuationState.ContinuationToken).Value);
                    rangeIdAndIndex = (resourceIdentifier.Database, resourceIdentifier.Document);
                }
                else
                {
                    throw new InvalidOperationException("Unknown read feed state");
                }

                List<Record> page = records
                    .Where((record) =>
                    {
                        if (!IsRecordWithinFeedRange(record, feedRangeState.FeedRange, this.partitionKeyDefinition))
                        {
                            return false;
                        }

                        // We do a filter on a composite index here 
                        int pkRangeIdCompare = record.ResourceIdentifier.Database.CompareTo((uint)rangeIdAndIndex.pkrangeId);
                        if (pkRangeIdCompare < 0)
                        {
                            return false;
                        }
                        else if (pkRangeIdCompare > 0)
                        {
                            return true;
                        }
                        else // pkRangeIdCompare == 0
                        {
                            return record.ResourceIdentifier.Document > rangeIdAndIndex.documentIndex;
                        }
                    })
                    .Take(readFeedPaginationOptions.PageSizeLimit.GetValueOrDefault(int.MaxValue))
                    .ToList();

                List<CosmosObject> documents = new List<CosmosObject>();
                foreach (Record record in page)
                {
                    CosmosObject document = ConvertRecordToCosmosElement(record);
                    documents.Add(CosmosObject.Create(document));
                }

                ReadFeedState continuationState;
                if (documents.Count == 0)
                {
                    continuationState = null;
                }
                else
                {
                    ResourceId resourceIdentifier = page.Last().ResourceIdentifier;
                    CosmosString continuationToken = CosmosString.Create(resourceIdentifier.ToString());
                    continuationState = ReadFeedState.Continuation(continuationToken);
                }

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
                    additionalHeaders: new Dictionary<string, string>()
                    {
                        { "test-header", "test-value" }
                    },
                    continuationState);

                return Task.FromResult(TryCatch<ReadFeedPage>.FromResult(readFeedPage));
            }
        }

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            FeedRangeState<QueryState> feedRangeState,
            QueryPaginationOptions queryPaginationOptions,
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
                TryCatch<int> monadicPartitionKeyRangeId = this.MonadicGetPartitionKeyRangeIdFromFeedRange(feedRangeState.FeedRange);
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
                foreach (Record record in records.Where(r => IsRecordWithinFeedRange(r, feedRangeState.FeedRange, this.partitionKeyDefinition)))
                {
                    CosmosObject document = ConvertRecordToCosmosElement(record);
                    documents.Add(CosmosObject.Create(document));
                }

                TryCatch<SqlQuery> monadicParse = SqlQueryParser.Monadic.Parse(sqlQuerySpec.QueryText);
                if (monadicParse.Failed)
                {
                    return Task.FromResult(TryCatch<QueryPage>.FromException(monadicParse.Exception));
                }

                SqlQuery sqlQuery = monadicParse.Result;
                if ((sqlQuery.OrderByClause != null) && (feedRangeState.State != null))
                {
                    // This is a hack.
                    // If the query is an ORDER BY query then we need to seek to the resume term.
                    // Since I don't want to port over the proper logic from the backend I will just inject a filter.
                    // For now I am only handling the single order by item case
                    if (sqlQuery.OrderByClause.OrderByItems.Length != 1)
                    {
                        throw new NotImplementedException("Can only support a single order by column");
                    }

                    SqlOrderByItem orderByItem = sqlQuery.OrderByClause.OrderByItems[0];
                    CosmosObject parsedContinuationToken = CosmosObject.Parse(((CosmosString)feedRangeState.State.Value).Value);
                    SqlBinaryScalarExpression resumeFilter = SqlBinaryScalarExpression.Create(
                        orderByItem.IsDescending ? SqlBinaryScalarOperatorKind.LessThan : SqlBinaryScalarOperatorKind.GreaterThan,
                        orderByItem.Expression,
                        parsedContinuationToken["orderByItem"].Accept(CosmosElementToSqlScalarExpressionVisitor.Singleton));

                    SqlWhereClause modifiedWhereClause = sqlQuery.WhereClause.FilterExpression == null
                        ? SqlWhereClause.Create(resumeFilter)
                        : SqlWhereClause.Create(
                            SqlBinaryScalarExpression.Create(
                                SqlBinaryScalarOperatorKind.And,
                                sqlQuery.WhereClause.FilterExpression,
                                resumeFilter));

                    sqlQuery = SqlQuery.Create(
                        sqlQuery.SelectClause,
                        sqlQuery.FromClause,
                        modifiedWhereClause,
                        sqlQuery.GroupByClause,
                        sqlQuery.OrderByClause,
                        sqlQuery.OffsetLimitClause);

                    // We still need to handle duplicate values and break the tie with the rid
                    // But since all the values are unique for our testing purposes we can ignore this for now.
                }
                IEnumerable<CosmosElement> queryResults = SqlInterpreter.ExecuteQuery(documents, sqlQuery);
                IEnumerable<CosmosElement> queryPageResults = queryResults;

                // Filter for the continuation token
                string continuationResourceId;
                int continuationSkipCount;

                if ((sqlQuery.OrderByClause == null) && (feedRangeState.State != null))
                {
                    CosmosObject parsedContinuationToken = CosmosObject.Parse(((CosmosString)feedRangeState.State.Value).Value);
                    continuationResourceId = ((CosmosString)parsedContinuationToken["resourceId"]).Value;
                    continuationSkipCount = (int)Number64.ToLong(((CosmosNumber64)parsedContinuationToken["skipCount"]).Value);

                    ResourceIdentifier continuationParsedResourceId = ResourceIdentifier.Parse(continuationResourceId);
                    queryPageResults = queryPageResults.Where(c =>
                    {
                        ResourceId documentResourceId = ResourceId.Parse(((CosmosString)((CosmosObject)c)["_rid"]).Value);
                        // Perform a composite filter on pkrange id and document index 
                        int pkRangeIdCompare = documentResourceId.Database.CompareTo(continuationParsedResourceId.Database);
                        if (pkRangeIdCompare < 0)
                        {
                            return false;
                        }
                        else if (pkRangeIdCompare > 0)
                        {
                            return true;
                        }
                        else // pkRangeIdCompare == 0
                        {
                            int documentCompare = documentResourceId.Document.CompareTo(continuationParsedResourceId.Document);

                            // If we have a skip count, then we can't skip over the rid we last saw, since
                            // there are documents with the same rid that we need to skip over.
                            return continuationSkipCount == 0 ? documentCompare > 0 : documentCompare >= 0;
                        }
                    });

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

                queryPageResults = queryPageResults.Take((queryPaginationOptions ?? QueryPaginationOptions.Default).PageSizeLimit.GetValueOrDefault(int.MaxValue));
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

                    Dictionary<string, CosmosElement> queryStateDictionary = new Dictionary<string, CosmosElement>()
                    {
                        { "resourceId", CosmosString.Create(currentResourceId) },
                        { "skipCount", CosmosNumber64.Create(currentSkipCount) },
                    };

                    if (sqlQuery.OrderByClause != null)
                    {
                        SqlOrderByItem orderByItem = sqlQuery.OrderByClause.OrderByItems[0];
                        string propertyName = ((SqlPropertyRefScalarExpression)orderByItem.Expression).Identifier.Value;
                        queryStateDictionary["orderByItem"] = ((CosmosObject)lastDocument["payload"])[propertyName];
                    }

                    CosmosObject queryStateValue = CosmosObject.Create(queryStateDictionary);

                    queryState = new QueryState(CosmosString.Create(queryStateValue.ToString()));
                }
                else
                {
                    queryState = default;
                }

                ImmutableDictionary<string, string>.Builder additionalHeaders = ImmutableDictionary.CreateBuilder<string, string>();
                additionalHeaders.Add("x-ms-documentdb-partitionkeyrangeid", "0");
                additionalHeaders.Add("x-ms-test-header", "true");

                return Task.FromResult(
                    TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            queryPageResultList,
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            responseLengthInBytes: 1337,
                            cosmosQueryExecutionInfo: default,
                            disallowContinuationTokenMessage: default,
                            additionalHeaders: additionalHeaders.ToImmutable(),
                            state: queryState)));
            }
        }

        public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
            FeedRangeState<ChangeFeedState> feedRangeState,
            ChangeFeedPaginationOptions changeFeedPaginationOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (ITrace childTrace = trace.StartChild("Change Feed Transport", TraceComponent.Transport, TraceLevel.Info))
            {
                TryCatch<int> monadicPartitionKeyRangeId = this.MonadicGetPartitionKeyRangeIdFromFeedRange(feedRangeState.FeedRange);
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
                    .Where(change => IsRecordWithinFeedRange(change.Record, feedRangeState.FeedRange, this.partitionKeyDefinition))
                    .Where(change => feedRangeState.State.Accept(ChangeFeedPredicate.Singleton, change))
                    .Take((changeFeedPaginationOptions ?? ChangeFeedPaginationOptions.Default).PageSizeLimit.GetValueOrDefault(int.MaxValue))
                    .ToList();

                if (filteredChanges.Count == 0)
                {
                    ChangeFeedState notModifiedResponseState = new ChangeFeedStateTime(DateTime.UtcNow);
                    return Task.FromResult(
                    TryCatch<ChangeFeedPage>.FromResult(
                        new ChangeFeedNotModifiedPage(
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            additionalHeaders: default,
                            notModifiedResponseState)));
                }

                Change lastChange = filteredChanges.Last();
                CosmosObject continuationToken = CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { "PkRangeId", CosmosNumber64.Create(lastChange.PartitionKeyRangeId) },
                        { "LSN", CosmosNumber64.Create(lastChange.LogicalSequenceNumber) }
                    });

                ChangeFeedState responseState = ChangeFeedState.Continuation(continuationToken);

                List<CosmosObject> documents = new List<CosmosObject>();
                foreach (Change change in filteredChanges)
                {
                    CosmosObject document = ConvertRecordToCosmosElement(change.Record);
                    documents.Add(CosmosObject.Create(document));
                }

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
                            additionalHeaders: default,
                            responseState)));
            }
        }

        public Task<TryCatch> MonadicSplitAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (feedRange == null)
            {
                throw new ArgumentNullException(nameof(feedRange));
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

            // Split the range space
            PartitionKeyHashRanges partitionKeyHashRanges = PartitionKeyHashRangeSplitterAndMerger.SplitRange(
                parentRange,
                rangeCount: 2);

            // Update the partition routing map
            int maxPartitionKeyRangeId = this.partitionKeyRangeIdToHashRange.Keys.Max();
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

        public Task<TryCatch> MonadicMergeAsync(
            FeedRangeInternal feedRange1,
            FeedRangeInternal feedRange2,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (feedRange1 == null)
            {
                throw new ArgumentNullException(nameof(feedRange1));
            }

            if (feedRange2 == null)
            {
                throw new ArgumentNullException(nameof(feedRange2));
            }

            TryCatch<int> monadicPartitionKeyRangeId1 = this.MonadicGetPartitionKeyRangeIdFromFeedRange(feedRange1);
            if (monadicPartitionKeyRangeId1.Failed)
            {
                return Task.FromResult(TryCatch.FromException(monadicPartitionKeyRangeId1.Exception));
            }

            int sourceRangeId1 = monadicPartitionKeyRangeId1.Result;

            TryCatch<int> monadicPartitionKeyRangeId2 = this.MonadicGetPartitionKeyRangeIdFromFeedRange(feedRange2);
            if (monadicPartitionKeyRangeId2.Failed)
            {
                return Task.FromResult(TryCatch.FromException(monadicPartitionKeyRangeId2.Exception));
            }

            int sourceRangeId2 = monadicPartitionKeyRangeId2.Result;

            // Get the range and records
            if (!this.partitionKeyRangeIdToHashRange.TryGetValue(
                sourceRangeId1,
                out PartitionKeyHashRange sourceHashRange1))
            {
                return Task.FromResult(
                    TryCatch.FromException(
                        new CosmosException(
                        message: $"PartitionKeyRangeId {sourceRangeId1} is gone",
                        statusCode: System.Net.HttpStatusCode.Gone,
                        subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                        activityId: Guid.NewGuid().ToString(),
                        requestCharge: 42)));
            }

            if (!this.partitionedRecords.TryGetValue(sourceHashRange1, out Records sourceRecords1))
            {
                throw new InvalidOperationException("failed to find the range.");
            }

            if (!this.partitionedChanges.TryGetValue(sourceHashRange1, out List<Change> sourceChanges1))
            {
                throw new InvalidOperationException("failed to find the range.");
            }

            if (!this.partitionKeyRangeIdToHashRange.TryGetValue(
                sourceRangeId2,
                out PartitionKeyHashRange sourceHashRange2))
            {
                return Task.FromResult(
                    TryCatch.FromException(
                        new CosmosException(
                        message: $"PartitionKeyRangeId {sourceRangeId2} is gone",
                        statusCode: System.Net.HttpStatusCode.Gone,
                        subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                        activityId: Guid.NewGuid().ToString(),
                        requestCharge: 42)));
            }

            if (!this.partitionedRecords.TryGetValue(sourceHashRange2, out Records sourceRecords2))
            {
                throw new InvalidOperationException("failed to find the range.");
            }

            if (!this.partitionedChanges.TryGetValue(sourceHashRange2, out List<Change> sourceChanges2))
            {
                throw new InvalidOperationException("failed to find the range.");
            }

            // Merge the range space
            TryCatch<PartitionKeyHashRanges> monadicRanges = PartitionKeyHashRanges.Monadic.Create(new List<PartitionKeyHashRange>()
            {
                sourceHashRange1,
                sourceHashRange2
            });

            if (monadicRanges.Failed)
            {
                return Task.FromResult(TryCatch.FromException(monadicRanges.Exception));
            }

            PartitionKeyHashRange mergedHashRange = PartitionKeyHashRangeSplitterAndMerger.MergeRanges(
                monadicRanges.Result);

            // Update the partition routing map 
            int maxPartitionKeyRangeId = this.partitionKeyRangeIdToHashRange.Keys.Max();
            Dictionary<int, PartitionKeyHashRange> newPartitionKeyRangeIdToHashRange = new Dictionary<int, PartitionKeyHashRange>()
            {
                { maxPartitionKeyRangeId + 1, mergedHashRange },
            };

            foreach (KeyValuePair<int, PartitionKeyHashRange> kvp in this.partitionKeyRangeIdToHashRange)
            {
                int oldRangeId = kvp.Key;
                PartitionKeyHashRange oldRange = kvp.Value;
                if (!(oldRange.Equals(sourceHashRange1) || oldRange.Equals(sourceHashRange2)))
                {
                    newPartitionKeyRangeIdToHashRange[oldRangeId] = oldRange;
                }
            }

            // Copy over the partitioned records (minus the source ranges)
            PartitionKeyHashRangeDictionary<Records> newPartitionedRecords = new PartitionKeyHashRangeDictionary<Records>(
                PartitionKeyHashRanges.Create(newPartitionKeyRangeIdToHashRange.Values));

            newPartitionedRecords[mergedHashRange] = new Records();

            foreach (PartitionKeyHashRange range in this.partitionKeyRangeIdToHashRange.Values)
            {
                if (!(range.Equals(sourceHashRange1) || range.Equals(sourceHashRange2)))
                {
                    newPartitionedRecords[range] = this.partitionedRecords[range];
                }
            }

            PartitionKeyHashRangeDictionary<List<Change>> newPartitionedChanges = new PartitionKeyHashRangeDictionary<List<Change>>(
                PartitionKeyHashRanges.Create(newPartitionKeyRangeIdToHashRange.Values));

            newPartitionedChanges[mergedHashRange] = new List<Change>();

            foreach (PartitionKeyHashRange range in this.partitionKeyRangeIdToHashRange.Values)
            {
                if (!(range.Equals(sourceHashRange1) || range.Equals(sourceHashRange2)))
                {
                    newPartitionedChanges[range] = this.partitionedChanges[range];
                }
            }

            this.partitionedRecords = newPartitionedRecords;
            this.partitionedChanges = newPartitionedChanges;
            this.partitionKeyRangeIdToHashRange = newPartitionKeyRangeIdToHashRange;

            // Rehash the records in the source ranges
            List<Record> combinedOrderedRecords = new List<Record>();
            foreach (Records sourceRecords in new Records[] { sourceRecords1, sourceRecords2 })
            {
                combinedOrderedRecords.AddRange(sourceRecords);
            }

            combinedOrderedRecords = combinedOrderedRecords
                .OrderBy(record => record.ResourceIdentifier.Database)
                .ThenBy(record => record.ResourceIdentifier.Document)
                .ToList();

            foreach (Record record in combinedOrderedRecords)
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
            List<Change> combinedOrderedChanges = new List<Change>();
            foreach (List<Change> sourceChanges in new List<Change>[] { sourceChanges1, sourceChanges2 })
            {
                combinedOrderedChanges.AddRange(sourceChanges);
            }

            combinedOrderedChanges = combinedOrderedChanges
                .OrderBy(change => change.PartitionKeyRangeId)
                .ThenBy(change => change.LogicalSequenceNumber)
                .ToList();

            foreach (Change change in combinedOrderedChanges)
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
                ["_ts"] = CosmosNumber64.Create(record.Timestamp.Ticks),
                ["id"] = CosmosString.Create(record.Identifier)
            };

            foreach (KeyValuePair<string, CosmosElement> property in record.Payload)
            {
                keyValuePairs[property.Key] = property.Value;
            }

            return CosmosObject.Create(keyValuePairs);
        }

        private static bool IsRecordWithinFeedRange(
            Record record,
            FeedRange feedRange,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            if (feedRange is FeedRangePartitionKey feedRangePartitionKey)
            {
                CosmosElement partitionKey = GetPartitionKeyFromObjectModel(feedRangePartitionKey.PartitionKey);
                CosmosElement partitionKeyFromRecord = GetPartitionKeyFromPayload(record.Payload, partitionKeyDefinition);
                return partitionKey.Equals(partitionKeyFromRecord);
            }
            else if (feedRange is FeedRangeEpk feedRangeEpk)
            {
                PartitionKeyHashRange hashRange = FeedRangeEpkToHashRange(feedRangeEpk);
                PartitionKeyHash hash = GetHashFromPayload(record.Payload, partitionKeyDefinition);
                return hashRange.Contains(hash);
            }
            else if (feedRange is FeedRangePartitionKeyRange)
            {
                return true;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private TryCatch<int> MonadicGetPartitionKeyRangeIdFromFeedRange(FeedRange feedRange)
        {
            int partitionKeyRangeId;
            if (feedRange is FeedRangeEpk feedRangeEpk)
            {
                // Check to see if any of the system ranges contain the user range.
                List<int> matchIds;
                if (feedRangeEpk.Range.Min.Equals(FeedRangeEpk.FullRange.Range.Min) && feedRangeEpk.Range.Max.Equals(FeedRangeEpk.FullRange.Range.Max))
                {
                    matchIds = this.PartitionKeyRangeIds.ToList();
                }
                else
                {
                    PartitionKeyHashRange hashRange = FeedRangeEpkToHashRange(feedRangeEpk);
                    matchIds = this.partitionKeyRangeIdToHashRange
                        .Where(kvp => kvp.Value.Contains(hashRange))
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

                partitionKeyRangeId = matchIds[0];
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

        private static FeedRangeEpk HashRangeToFeedRangeEpk(PartitionKeyHashRange hashRange)
        {
            return new FeedRangeEpk(
                new Documents.Routing.Range<string>(
                    min: hashRange.StartInclusive.HasValue ? hashRange.StartInclusive.ToString() : string.Empty,
                    max: hashRange.EndExclusive.HasValue ? hashRange.EndExclusive.ToString() : string.Empty,
                    isMinInclusive: true,
                    isMaxInclusive: false));
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

                Record record = new Record(nextResourceId, DateTime.UtcNow, Guid.NewGuid().ToString(), payload);
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
            public Change(Record record, ulong partitionKeyRangeId, ulong logicalSequenceNumber)
            {
                this.Record = record ?? throw new ArgumentNullException(nameof(record));
                this.PartitionKeyRangeId = partitionKeyRangeId;
                this.LogicalSequenceNumber = logicalSequenceNumber;
            }

            public Record Record { get; }
            public ulong PartitionKeyRangeId { get; }
            public ulong LogicalSequenceNumber { get; }
        }

        private sealed class ChangeFeedPredicate : IChangeFeedStateVisitor<Change, bool>
        {
            public static readonly ChangeFeedPredicate Singleton = new ChangeFeedPredicate();

            private ChangeFeedPredicate()
            {
            }

            public bool Visit(ChangeFeedStateBeginning changeFeedStateBeginning, Change input) => true;

            public bool Visit(ChangeFeedStateTime changeFeedStateTime, Change input) => input.Record.Timestamp >= changeFeedStateTime.StartTime;

            public bool Visit(ChangeFeedStateContinuation changeFeedStateContinuation, Change input)
            {
                CosmosObject continuation = (CosmosObject)changeFeedStateContinuation.ContinuationToken;

                if (!continuation.TryGetValue("PkRangeId", out CosmosNumber pkRangeIdCosmosElement))
                {
                    throw new InvalidOperationException("failed to get pkrange id");
                }

                ulong pkRangeId = (ulong)Number64.ToLong(pkRangeIdCosmosElement.Value);

                if (!continuation.TryGetValue("LSN", out CosmosNumber lsnCosmosElement))
                {
                    throw new InvalidOperationException("failed to get lsn");
                }

                ulong lsn = (ulong)Number64.ToLong(lsnCosmosElement.Value);

                int pkRangeIdCompare = input.PartitionKeyRangeId.CompareTo(pkRangeId);
                if (pkRangeIdCompare < 0)
                {
                    return false;
                }
                else if (pkRangeIdCompare > 0)
                {
                    return true;
                }
                else
                {
                    return input.LogicalSequenceNumber > lsn;
                }
            }

            public bool Visit(ChangeFeedStateNow changeFeedStateNow, Change input)
            {
                DateTime now = DateTime.UtcNow;
                ChangeFeedStateTime startTime = new ChangeFeedStateTime(now);
                return this.Visit(startTime, input);
            }
        }

        private sealed class CosmosElementToSqlScalarExpressionVisitor : ICosmosElementVisitor<SqlScalarExpression>
        {
            public static readonly CosmosElementToSqlScalarExpressionVisitor Singleton = new CosmosElementToSqlScalarExpressionVisitor();

            private CosmosElementToSqlScalarExpressionVisitor()
            {
                // Private constructor, since this class is a singleton.
            }

            public SqlScalarExpression Visit(CosmosArray cosmosArray)
            {
                List<SqlScalarExpression> items = new List<SqlScalarExpression>();
                foreach (CosmosElement item in cosmosArray)
                {
                    items.Add(item.Accept(this));
                }

                return SqlArrayCreateScalarExpression.Create(items.ToImmutableArray());
            }

            public SqlScalarExpression Visit(CosmosBinary cosmosBinary)
            {
                // Can not convert binary to scalar expression without knowing the API type.
                throw new NotImplementedException();
            }

            public SqlScalarExpression Visit(CosmosBoolean cosmosBoolean)
            {
                return SqlLiteralScalarExpression.Create(SqlBooleanLiteral.Create(cosmosBoolean.Value));
            }

            public SqlScalarExpression Visit(CosmosGuid cosmosGuid)
            {
                // Can not convert guid to scalar expression without knowing the API type.
                throw new NotImplementedException();
            }

            public SqlScalarExpression Visit(CosmosNull cosmosNull)
            {
                return SqlLiteralScalarExpression.Create(SqlNullLiteral.Create());
            }

            public SqlScalarExpression Visit(CosmosNumber cosmosNumber)
            {
                if (!(cosmosNumber is CosmosNumber64 cosmosNumber64))
                {
                    throw new ArgumentException($"Unknown {nameof(CosmosNumber)} type: {cosmosNumber.GetType()}.");
                }

                return SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(cosmosNumber64.GetValue()));
            }

            public SqlScalarExpression Visit(CosmosObject cosmosObject)
            {
                List<SqlObjectProperty> properties = new List<SqlObjectProperty>();
                foreach (KeyValuePair<string, CosmosElement> prop in cosmosObject)
                {
                    SqlPropertyName name = SqlPropertyName.Create(prop.Key);
                    CosmosElement value = prop.Value;
                    SqlScalarExpression expression = value.Accept(this);
                    SqlObjectProperty property = SqlObjectProperty.Create(name, expression);
                    properties.Add(property);
                }

                return SqlObjectCreateScalarExpression.Create(properties.ToImmutableArray());
            }

            public SqlScalarExpression Visit(CosmosString cosmosString)
            {
                return SqlLiteralScalarExpression.Create(SqlStringLiteral.Create(cosmosString.Value));
            }
        }
    }
}
