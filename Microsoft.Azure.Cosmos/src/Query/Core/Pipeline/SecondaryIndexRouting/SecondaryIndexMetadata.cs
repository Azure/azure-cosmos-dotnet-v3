//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting
{
    using System;
    using System.Collections.Generic;
    using PartitionKeyDefinition = Microsoft.Azure.Documents.PartitionKeyDefinition;

    internal sealed class SecondaryIndexMetadata : ISecondaryIndexMetadata
    {
        public SecondaryIndexMetadata(
            string rid,
            string sourceCollectionRid,
            PartitionKeyDefinition partitionKey,
            IndexingPolicy indexingPolicy,
            IReadOnlyDictionary<string, string> includedProperties,
            ConsistencyLevel consistency)
        {
            this.Rid = string.IsNullOrWhiteSpace(rid) ? throw new ArgumentNullException(nameof(rid)) : rid;
            this.SourceCollectionRid = string.IsNullOrWhiteSpace(sourceCollectionRid) ? throw new ArgumentNullException(nameof(sourceCollectionRid)) : sourceCollectionRid;
            this.PartitionKey = partitionKey ?? throw new ArgumentNullException(nameof(partitionKey));
            this.IndexingPolicy = indexingPolicy ?? throw new ArgumentNullException(nameof(indexingPolicy));
            this.IncludedProperties = includedProperties ?? throw new ArgumentNullException(nameof(includedProperties));
            this.Consistency = consistency;
        }

        public string Rid { get; }

        public string SourceCollectionRid { get; }

        public PartitionKeyDefinition PartitionKey { get; }

        public IndexingPolicy IndexingPolicy { get; }

        public IReadOnlyDictionary<string, string> IncludedProperties { get; }

        public ConsistencyLevel Consistency { get; }
    }
}
