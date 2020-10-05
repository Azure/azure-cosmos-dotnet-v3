//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices.WindowsRuntime;
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
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using ResourceIdentifier = Cosmos.Pagination.ResourceIdentifier;

    // Collection useful for mocking requests and repartitioning (splits / merge).
    internal sealed class InMemoryContainer : IMonadicDocumentContainer
    {
        private static readonly PartitionKeyRange FullRange = new PartitionKeyRange()
        {
            MinInclusive = Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
            MaxExclusive = Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
        };

        private readonly PartitionKeyDefinition partitionKeyDefinition;
        private readonly Dictionary<int, (int, int)> parentToChildMapping;
        private readonly ExecuteQueryBasedOnFeedRangeVisitor executeQueryBasedOnFeedRangeVisitor;

        private PartitionKeyHashRangeDictionary<Records> partitionedRecords;
        private PartitionKeyHashRangeDictionary<List<Change>> partitionedChanges;
        private Dictionary<int, PartitionKeyHashRange> partitionKeyRangeIdToHashRange;

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
            this.parentToChildMapping = new Dictionary<int, (int, int)>();
            this.executeQueryBasedOnFeedRangeVisitor = new ExecuteQueryBasedOnFeedRangeVisitor(this);
        }

        public Task<TryCatch<List<PartitionKeyRange>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken) => this.MonadicGetChildRangeAsync(
                FullRange,
                cancellationToken);

        public async Task<TryCatch<List<PartitionKeyRange>>> MonadicGetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PartitionKeyRange CreateRangeFromId(int id)
            {
                PartitionKeyHashRange hashRange = this.partitionKeyRangeIdToHashRange[id];
                return new PartitionKeyRange()
                {
                    Id = id.ToString(),
                    MinInclusive = hashRange.StartInclusive.HasValue ? hashRange.StartInclusive.Value.ToString() : string.Empty,
                    MaxExclusive = hashRange.EndExclusive.HasValue ? hashRange.EndExclusive.Value.ToString() : string.Empty,
                };
            }

            bool isFullRange = (partitionKeyRange.MinInclusive == Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey) &&
                (partitionKeyRange.MaxExclusive == Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey);
            if (isFullRange)
            {
                List<PartitionKeyRange> ranges = new List<PartitionKeyRange>();
                foreach (int id in this.partitionKeyRangeIdToHashRange.Keys)
                {
                    ranges.Add(CreateRangeFromId(id));
                }

                return TryCatch<List<PartitionKeyRange>>.FromResult(ranges);
            }


            if (partitionKeyRange.Id == null)
            {
                // look for overlapping epk ranges.
                PartitionKeyHash? start = partitionKeyRange.MinInclusive == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(partitionKeyRange.MinInclusive);
                PartitionKeyHash? end = partitionKeyRange.MaxExclusive == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(partitionKeyRange.MaxExclusive);
                PartitionKeyHashRange hashRange = new PartitionKeyHashRange(start, end);
                List<PartitionKeyRange> overlappedIds = this.partitionKeyRangeIdToHashRange
                    .Where(kvp => hashRange.Contains(kvp.Value))
                    .Select(kvp => CreateRangeFromId(kvp.Key))
                    .ToList();
                if (overlappedIds.Count == 0)
                {
                    return TryCatch<List<PartitionKeyRange>>.FromException(
                        new KeyNotFoundException(
                            $"PartitionKeyRangeId: {hashRange} does not exist."));
                }

                return TryCatch<List<PartitionKeyRange>>.FromResult(overlappedIds);
            }

            if (!int.TryParse(partitionKeyRange.Id, out int partitionKeyRangeId))
            {
                return TryCatch<List<PartitionKeyRange>>.FromException(
                    new FormatException(
                        $"PartitionKeyRangeId: {partitionKeyRange.Id} is not an integer."));
            }

            if (!this.parentToChildMapping.TryGetValue(partitionKeyRangeId, out (int left, int right) children))
            {
                // This range has no children (base case)
                if (!this.partitionKeyRangeIdToHashRange.TryGetValue(partitionKeyRangeId, out PartitionKeyHashRange hashRange))
                {
                    return TryCatch<List<PartitionKeyRange>>.FromException(
                        new KeyNotFoundException(
                            $"PartitionKeyRangeId: {partitionKeyRangeId} does not exist."));
                }

                List<PartitionKeyRange> singleRange = new List<PartitionKeyRange>()
                {
                    CreateRangeFromId(partitionKeyRangeId),
                };

                return TryCatch<List<PartitionKeyRange>>.FromResult(singleRange);
            }

            // Recurse on the left and right child.
            PartitionKeyRange left = new PartitionKeyRange()
            {
                Id = children.left.ToString(),
            };

            PartitionKeyRange right = new PartitionKeyRange()
            {
                Id = children.right.ToString(),
            };

            TryCatch<List<PartitionKeyRange>> tryGetLeftRanges = await this.MonadicGetChildRangeAsync(left, cancellationToken);
            if (tryGetLeftRanges.Failed)
            {
                return tryGetLeftRanges;
            }

            TryCatch<List<PartitionKeyRange>> tryGetRightRanges = await this.MonadicGetChildRangeAsync(right, cancellationToken);
            if (tryGetRightRanges.Failed)
            {
                return tryGetRightRanges;
            }

            List<PartitionKeyRange> overlappingRanges = tryGetLeftRanges.Result.Concat(tryGetRightRanges.Result).ToList();
            return TryCatch<List<PartitionKeyRange>>.FromResult(overlappingRanges);
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

        public Task<TryCatch<DocumentContainerPage>> MonadicReadFeedAsync(
            int partitionKeyRangeId,
            ResourceId resourceIdentifer,
            int pageSize,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.partitionKeyRangeIdToHashRange.TryGetValue(
                partitionKeyRangeId,
                out PartitionKeyHashRange range))
            {
                return Task.FromResult(
                    TryCatch<DocumentContainerPage>.FromException(
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

            List<Record> page = records
                .Where(record => record.ResourceIdentifier.Document > resourceIdentifer.Document)
                .Take(pageSize)
                .ToList();

            if (page.Count == 0)
            {
                return Task.FromResult(
                    TryCatch<DocumentContainerPage>.FromResult(
                        new DocumentContainerPage(
                            records: page,
                            state: default)));
            }

            return Task.FromResult(
                TryCatch<DocumentContainerPage>.FromResult(
                    new DocumentContainerPage(
                        records: page,
                        state: new DocumentContainerState(page.Last().ResourceIdentifier))));
        }

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            Cosmos.PartitionKey partitionKey,
            int pageSize,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            int partitionKeyRangeId,
            int pageSize,
            CancellationToken cancellationToken)
        {
            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpec));
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

        public Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken) => feedRange.AcceptAsync(
                this.executeQueryBasedOnFeedRangeVisitor,
                new ExecuteQueryBasedOnFeedRangeVisitor.Arguments(
                    sqlQuerySpec,
                    continuationToken,
                    pageSize),
                cancellationToken);

        public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
            ChangeFeedState state,
            FeedRangeInternal feedRange,
            int pageSize,
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

            if (!(feedRange is FeedRangePartitionKeyRange feedRangePartitionKeyRange))
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            int partitionKeyRangeId = int.Parse(feedRangePartitionKeyRange.PartitionKeyRangeId);

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
                return Task.FromResult(
                    TryCatch<ChangeFeedPage>.FromException(
                        new CosmosException(
                            message:
                                $"I see no changes, all I see is racist faces" +
                                $"Misplaced hate makes disgrace to races" +
                                $"We under, I wonder what it takes to make this" +
                                $"One better place, let's erase the wasted" +
                                $"Take the evil out the people, they'll be actin' right" +
                                $"'Cause both black and white are smokin' crack tonight" +
                                $"And the only time we chill is when we kill each other(Kill each other)" +
                                $"It takes skill to be real, time to heal each other" +
                                $"And although it seems heaven - sent" +
                                $"We ain't ready to see a black president, uh (Oh-ooh)" +
                                $"It ain't a secret, don't conceal the fact" +
                                $"The penitentiary's packed and it's filled with blacks" +
                                $"But some things will never change(Never change)" +
                                $"Try to show another way, but you stayin' in the dope game (Ooh)" +
                                $"Now tell me, what's a mother to do?" +
                                $"Bein' real don't appeal to the brother in you(Yeah)" +
                                $"You gotta operate the easy way" +
                                $"'I made a G today,' but you made it in a sleazy way" +
                                $"Sellin' crack to the kids (Oh-oh), 'I gotta get paid' (Oh)" +
                                $"Well hey, well that's the way it is" +
                                $"Source: https://genius.com/2pac-changes-lyrics",
                            statusCode: System.Net.HttpStatusCode.NotModified,
                            subStatusCode: 0,
                            headers: new Headers
                            {
                                ContinuationToken = DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture)
                            },
                            activityId: Guid.NewGuid().ToString(),
                            requestCharge: 42,
                            retryAfter: default,
                            diagnosticsContext: default,
                            error: default,
                            innerException: default,
                            stackTrace: default)));
            }

            ChangeFeedState responseState = new ChangeFeedStateTime(filteredChanges.Last().Time.AddTicks(1).ToUniversalTime());

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
                    new ChangeFeedPage(
                        responseStream,
                        requestCharge: 42,
                        activityId: Guid.NewGuid().ToString(),
                        responseState)));
        }

        public Task<TryCatch> MonadicSplitAsync(
            int partitionKeyRangeId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                DateTime time = DateTime.Parse(changeFeedStateContinuation.ContinuationToken);
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
