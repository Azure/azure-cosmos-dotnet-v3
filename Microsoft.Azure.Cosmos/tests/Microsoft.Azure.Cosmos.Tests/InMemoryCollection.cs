//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    // Collection useful for mocking requests and repartitioning (splits / merge).
    internal sealed class InMemoryContainer
    {
        private readonly PartitionKeyHashRangeDictionary<Records> partitionedRecords;
        private readonly PartitionKeyDefinition partitionKeyDefinition;

        public InMemoryContainer(PartitionKeyDefinition partitionKeyDefinition)
        {
            PartitionKeyHashRange fullRange = new PartitionKeyHashRange(startInclusive: null, endExclusive: null);
            PartitionKeyHashRanges partitionKeyHashRanges = PartitionKeyHashRanges.Create(new PartitionKeyHashRange[] { fullRange });
            this.partitionedRecords = new PartitionKeyHashRangeDictionary<Records>(partitionKeyHashRanges);
            this.partitionKeyDefinition = partitionKeyDefinition ?? throw new ArgumentNullException(nameof(partitionKeyDefinition));
        }

        public Record CreateItem(CosmosObject payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            PartitionKeyHash partitionKeyHash = GetHashFromPayload(payload, this.partitionKeyDefinition);
            if (!this.partitionedRecords.TryGetValue(partitionKeyHash, out Records records))
            {
                this.partitionedRecords[partitionKeyHash] = new Records();
            }

            return records.Add(payload);
        }

        public bool TryReadItem(CosmosElement partitionKey, Guid identifier, out Record record)
        {
            PartitionKeyHash partitionKeyHash = GetHashFromPartitionKey(partitionKey, this.partitionKeyDefinition);
            if (!this.partitionedRecords.TryGetValue(partitionKeyHash, out Records records))
            {
                record = default;
                return false;
            }

            foreach (Record candidate in records)
            {
                if (candidate.Identifier == identifier)
                {
                    record = candidate;
                    return true;
                }
            }

            record = default;
            return false;
        }

        private static PartitionKeyHash GetHashFromPayload(CosmosObject payload, PartitionKeyDefinition partitionKeyDefinition)
        {
            // Restrict the partition key definition for now to keep things simple
            if (partitionKeyDefinition.Kind != PartitionKind.Hash)
            {
                throw new ArgumentOutOfRangeException("Can only support hash partitioning");
            }

            if (partitionKeyDefinition.Version != PartitionKeyDefinitionVersion.V2)
            {
                throw new ArgumentOutOfRangeException("Can only support hash v2");
            }

            if (partitionKeyDefinition.Paths.Count != 1)
            {
                throw new ArgumentOutOfRangeException("Can only support a single partition key path.");
            }

            string[] tokens = partitionKeyDefinition.Paths[0].Split("/");

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

            return GetHashFromPartitionKey(partitionKey, partitionKeyDefinition);
        }

        private static PartitionKeyHash GetHashFromPartitionKey(CosmosElement partitionKey, PartitionKeyDefinition partitionKeyDefinition)
        {
            // Restrict the partition key definition for now to keep things simple
            if (partitionKeyDefinition.Kind != PartitionKind.Hash)
            {
                throw new ArgumentOutOfRangeException("Can only support hash partitioning");
            }

            if (partitionKeyDefinition.Version != PartitionKeyDefinitionVersion.V2)
            {
                throw new ArgumentOutOfRangeException("Can only support hash v2");
            }

            if (partitionKeyDefinition.Paths.Count != 1)
            {
                throw new ArgumentOutOfRangeException("Can only support a single partition key path.");
            }

            PartitionKeyHash partitionKeyHash;
            switch (partitionKey)
            {
                case null:
                    partitionKeyHash = PartitionKeyHash.V2.HashUndefined();
                    break;

                case CosmosString stringPartitionKey:
                    partitionKeyHash = PartitionKeyHash.V2.Hash(stringPartitionKey.Value);
                    break;

                case CosmosNumber numberPartitionKey:
                    partitionKeyHash = PartitionKeyHash.V2.Hash(Number64.ToDouble(numberPartitionKey.Value));
                    break;

                case CosmosBoolean cosmosBoolean:
                    partitionKeyHash = PartitionKeyHash.V2.Hash(cosmosBoolean.Value);
                    break;

                case CosmosNull _:
                    partitionKeyHash = PartitionKeyHash.V2.HashNull();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            return partitionKeyHash;
        }

        public sealed class Record
        {
            private Record(long resourceIdentifier, long timestamp, Guid identifier, CosmosObject payload)
            {
                this.ResourceIdentifier = resourceIdentifier < 0 ? throw new ArgumentOutOfRangeException(nameof(resourceIdentifier)) : resourceIdentifier;
                this.Timestamp = timestamp < 0 ? throw new ArgumentOutOfRangeException(nameof(timestamp)) : timestamp;
                this.Identifier = identifier;
                this.Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            }

            public long ResourceIdentifier { get; }

            public long Timestamp { get; }

            public Guid Identifier { get; }

            public CosmosObject Payload { get; }

            public static Record Create(long previousResourceIdentifier, CosmosObject payload)
            {
                return new Record(previousResourceIdentifier + 1, DateTime.UtcNow.Ticks, Guid.NewGuid(), payload);
            }
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
        }
    }
}
