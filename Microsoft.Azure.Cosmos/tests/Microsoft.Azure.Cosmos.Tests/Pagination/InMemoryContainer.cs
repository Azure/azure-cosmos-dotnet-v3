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
    using Microsoft.Azure.Documents;
    using ResourceIdentifier = Cosmos.Pagination.ResourceIdentifier;

    // Collection useful for mocking requests and repartitioning (splits / merge).
    internal sealed class InMemoryContainer : IMonadicDocumentContainer
    {
        private readonly PartitionKeyDefinition partitionKeyDefinition;
        private readonly Dictionary<int, (int, int)> parentToChildMapping;

        private PartitionKeyHashRangeDictionary<Records> partitionedRecords;
        private Dictionary<int, PartitionKeyHashRange> partitionKeyRangeIdToHashRange;

        public InMemoryContainer(
            PartitionKeyDefinition partitionKeyDefinition)
        {
            this.partitionKeyDefinition = partitionKeyDefinition ?? throw new ArgumentNullException(nameof(partitionKeyDefinition));
            PartitionKeyHashRange fullRange = new PartitionKeyHashRange(startInclusive: null, endExclusive: null);
            PartitionKeyHashRanges partitionKeyHashRanges = PartitionKeyHashRanges.Create(new PartitionKeyHashRange[] { fullRange });
            this.partitionedRecords = new PartitionKeyHashRangeDictionary<Records>(partitionKeyHashRanges);
            this.partitionedRecords[fullRange] = new Records();
            this.partitionKeyRangeIdToHashRange = new Dictionary<int, PartitionKeyHashRange>()
            {
                { 0, fullRange }
            };
            this.parentToChildMapping = new Dictionary<int, (int, int)>();
        }

        public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken) => this.MonadicGetChildRangeAsync(
                FeedRangeEpk.FullRange,
                cancellationToken);

        public async Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FeedRangeEpk CreateRangeFromId(int id)
            {
                PartitionKeyHashRange hashRange = this.partitionKeyRangeIdToHashRange[id];
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
                foreach (int id in this.partitionKeyRangeIdToHashRange.Keys)
                {
                    ranges.Add(CreateRangeFromId(id));
                }

                return TryCatch<List<FeedRangeEpk>>.FromResult(ranges);
            }

            if (feedRange is FeedRangeEpk feedRangeEpk)
            {
                // look for overlapping epk ranges.
                PartitionKeyHash? start = feedRangeEpk.Range.Min == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(feedRangeEpk.Range.Min);
                PartitionKeyHash? end = feedRangeEpk.Range.Max == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(feedRangeEpk.Range.Max);
                PartitionKeyHashRange hashRange = new PartitionKeyHashRange(start, end);
                List<FeedRangeEpk> overlappedIds = this.partitionKeyRangeIdToHashRange
                    .Where(kvp => hashRange.Contains(kvp.Value))
                    .Select(kvp => CreateRangeFromId(kvp.Key))
                    .ToList();
                if (overlappedIds.Count == 0)
                {
                    return TryCatch<List<FeedRangeEpk>>.FromException(
                        new KeyNotFoundException(
                            $"PartitionKeyRangeId: {hashRange} does not exist."));
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
                if (!this.partitionKeyRangeIdToHashRange.TryGetValue(partitionKeyRangeId, out PartitionKeyHashRange hashRange))
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

            TryCatch<List<FeedRangeEpk>> tryGetLeftRanges = await this.MonadicGetChildRangeAsync(left, cancellationToken);
            if (tryGetLeftRanges.Failed)
            {
                return tryGetLeftRanges;
            }

            TryCatch<List<FeedRangeEpk>> tryGetRightRanges = await this.MonadicGetChildRangeAsync(right, cancellationToken);
            if (tryGetRightRanges.Failed)
            {
                return tryGetRightRanges;
            }

            List<FeedRangeEpk> overlappingRanges = tryGetLeftRanges.Result.Concat(tryGetRightRanges.Result).ToList();
            return TryCatch<List<FeedRangeEpk>>.FromResult(overlappingRanges);
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
            int pageSize,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (feedRange is FeedRangePartitionKey)
            {
                throw new NotImplementedException();
            }

            int partitionKeyRangeId;
            if (feedRange is FeedRangeEpk feedRangeEpk)
            {
                // Check to see if it lines up exactly with one physical partition
                TryCatch<int> monadicGetPkRangeIdFromEpkRange = this.MonadicGetPkRangeIdFromEpk(feedRangeEpk);
                if (monadicGetPkRangeIdFromEpkRange.Failed)
                {
                    return Task.FromResult(TryCatch<ReadFeedPage>.FromException(monadicGetPkRangeIdFromEpkRange.Exception));
                }

                partitionKeyRangeId = monadicGetPkRangeIdFromEpkRange.Result;
            }
            else if (feedRange is FeedRangePartitionKeyRange feedRangePartitionKeyRange)
            {
                partitionKeyRangeId = int.Parse(feedRangePartitionKeyRange.PartitionKeyRangeId);
            }
            else
            {
                throw new NotImplementedException();
            }

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

            ulong documentIndex = readFeedState == null ? 0 : ulong.Parse(readFeedState.ContinuationToken);
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

            ReadFeedState continuationState = documents.Count == 0 ? null : new ReadFeedState(page.Last().ResourceIdentifier.Document.ToString());
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

            ReadFeedPage readFeedPage = new ReadFeedPage(responseStream, requestCharge: 42, activityId: Guid.NewGuid().ToString(), continuationState);

            return Task.FromResult(TryCatch<ReadFeedPage>.FromResult(readFeedPage));
        }

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpec));
            }

            if (feedRange is FeedRangePartitionKey)
            {
                throw new NotImplementedException();
            }

            int partitionKeyRangeId;
            if (feedRange is FeedRangeEpk feedRangeEpk)
            {
                // Check to see if it lines up exactly with one physical partition
                TryCatch<int> monadicGetPkRangeIdFromEpkRange = this.MonadicGetPkRangeIdFromEpk(feedRangeEpk);
                if (monadicGetPkRangeIdFromEpkRange.Failed)
                {
                    return Task.FromResult(TryCatch<QueryPage>.FromException(monadicGetPkRangeIdFromEpkRange.Exception));
                }

                partitionKeyRangeId = monadicGetPkRangeIdFromEpkRange.Result;
            }
            else if (feedRange is FeedRangePartitionKeyRange feedRangePartitionKeyRange)
            {
                partitionKeyRangeId = int.Parse(feedRangePartitionKeyRange.PartitionKeyRangeId);
            }
            else
            {
                throw new NotImplementedException();
            }

            TryCatch<SqlQuery> monadicParse = SqlQueryParser.Monadic.Parse(sqlQuerySpec.QueryText);
            if (monadicParse.Failed)
            {
                return Task.FromResult(TryCatch<QueryPage>.FromException(monadicParse.Exception));
            }

            SqlQuery sqlQuery = monadicParse.Result;

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

            IEnumerable<CosmosElement> queryResults = SqlInterpreter.ExecuteQuery(documents, sqlQuery);

            IEnumerable<CosmosElement> queryPageResults = queryResults;

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

        public Task<TryCatch> MonadicSplitAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (feedRange is FeedRangePartitionKey)
            {
                throw new NotSupportedException("Can not split a logical partition");
            }

            int partitionKeyRangeId;
            if (feedRange is FeedRangeEpk feedRangeEpk)
            {
                // Check to see if it lines up exactly with one physical partition
                TryCatch<int> monadicGetPkRangeIdFromEpkRange = this.MonadicGetPkRangeIdFromEpk(feedRangeEpk);
                if (monadicGetPkRangeIdFromEpkRange.Failed)
                {
                    return Task.FromResult(TryCatch.FromException(monadicGetPkRangeIdFromEpkRange.Exception));
                }

                partitionKeyRangeId = monadicGetPkRangeIdFromEpkRange.Result;
            }
            else if (feedRange is FeedRangePartitionKeyRange feedRangePartitionKeyRange)
            {
                partitionKeyRangeId = int.Parse(feedRangePartitionKeyRange.PartitionKeyRangeId);
            }
            else
            {
                throw new NotImplementedException();
            }

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

            this.partitionedRecords = newPartitionedRecords;
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

            return Task.FromResult(TryCatch.FromResult());
        }

        public IEnumerable<int> PartitionKeyRangeIds => this.partitionKeyRangeIdToHashRange.Keys;

        private TryCatch<int> MonadicGetPkRangeIdFromEpk(FeedRangeEpk feedRangeEpk)
        {
            PartitionKeyHash? start = feedRangeEpk.Range.Min == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(feedRangeEpk.Range.Min);
            PartitionKeyHash? end = feedRangeEpk.Range.Max == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(feedRangeEpk.Range.Max);
            PartitionKeyHashRange hashRange = new PartitionKeyHashRange(start, end);
            List<int> matchIds = this.partitionKeyRangeIdToHashRange
                .Where(kvp => kvp.Value.Equals(hashRange))
                .Select(kvp => kvp.Key)
                .ToList();
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
    }
}
