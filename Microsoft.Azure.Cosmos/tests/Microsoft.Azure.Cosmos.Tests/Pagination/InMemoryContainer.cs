//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

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

            int partitionKeyRangeId;
            if (partitionKeyRange.Id == null)
            {
                PartitionKeyHash? start = partitionKeyRange.MinInclusive == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(partitionKeyRange.MinInclusive);
                PartitionKeyHash? end = partitionKeyRange.MaxExclusive == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(partitionKeyRange.MaxExclusive);
                PartitionKeyHashRange hashRange = new PartitionKeyHashRange(start, end);
                IEnumerable<KeyValuePair<int, PartitionKeyHashRange>> kvps = this.partitionKeyRangeIdToHashRange.Where(kvp => kvp.Value.Equals(hashRange));
                if (!kvps.Any())
                {
                    return TryCatch<List<PartitionKeyRange>>.FromException(
                        new KeyNotFoundException(
                            $"PartitionKeyRangeId: {hashRange} does not exist."));
                }

                partitionKeyRangeId = kvps.First().Key;
            }
            else
            {
                if (!int.TryParse(partitionKeyRange.Id, out partitionKeyRangeId))
                {
                    return TryCatch<List<PartitionKeyRange>>.FromException(
                        new FormatException(
                            $"PartitionKeyRangeId: {partitionKeyRange.Id} is not an integer."));
                }
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

            Record recordAdded = records.Add(payload);

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
                bool partitionKeyMatches = CosmosElementEqualityComparer.Value.Equals(
                    candidatePartitionKey,
                    partitionKey);

                if (identifierMatches && partitionKeyMatches)
                {
                    return Task.FromResult(TryCatch<Record>.FromResult(candidate));
                }
            }

            return CreateNotFoundException(partitionKey, identifier);
        }

        public Task<TryCatch<DocumentContainerPage>> MonadicReadFeedAsync(
            int partitionKeyRangeId,
            long resourceIdentifer,
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
                .Where(record => record.ResourceIdentifier > resourceIdentifer)
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

        public async Task<TryCatch<QueryPage>> MonadicQueryAsync(
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

            if (!sqlQuerySpec.QueryText.Equals("SELECT * FROM c", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("InMemoryCollection only supports SELECT * FROM c queries");
            }

            long resourceIdentifier = continuationToken != null ? long.Parse(continuationToken) : 0;

            // For now just do a read feed
            TryCatch<DocumentContainerPage> tryGetPage = await this.MonadicReadFeedAsync(
                partitionKeyRangeId,
                resourceIdentifier,
                pageSize,
                cancellationToken);

            if (tryGetPage.Failed)
            {
                return TryCatch<QueryPage>.FromException(tryGetPage.Exception);
            }

            DocumentContainerPage page = tryGetPage.Result;
            List<CosmosElement> documents = new List<CosmosElement>(page.Records.Count);
            foreach (Record record in page.Records)
            {
                Dictionary<string, CosmosElement> keyValuePairs = new Dictionary<string, CosmosElement>
                {
                    ["_rid"] = CosmosNumber64.Create(record.ResourceIdentifier),
                    ["_ts"] = CosmosNumber64.Create(record.Timestamp),
                    ["id"] = CosmosString.Create(record.Identifier)
                };

                foreach (KeyValuePair<string, CosmosElement> property in record.Payload)
                {
                    keyValuePairs[property.Key] = property.Value;
                }

                documents.Add(CosmosObject.Create(keyValuePairs));
            }

            QueryPage queryPage = new QueryPage(
                documents: documents,
                requestCharge: 42,
                activityId: Guid.NewGuid().ToString(),
                responseLengthInBytes: 1337,
                cosmosQueryExecutionInfo: default,
                disallowContinuationTokenMessage: default,
                state: page.State != null ? new QueryState(CosmosString.Create(page.State.ResourceIdentifer.ToString())) : null);

            return TryCatch<QueryPage>.FromResult(queryPage);
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

            public Record Add(CosmosObject payload)
            {
                long previousResourceId;
                if (this.Count == 0)
                {
                    previousResourceId = 0;
                }
                else
                {
                    previousResourceId = this.storage[this.storage.Count - 1].ResourceIdentifier;
                }

                Record record = Record.Create(previousResourceId, payload);
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
