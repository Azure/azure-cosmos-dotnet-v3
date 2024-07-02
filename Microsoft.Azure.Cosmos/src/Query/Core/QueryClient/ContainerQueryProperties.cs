//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryClient
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    internal readonly struct ContainerQueryProperties
    {
        public ContainerQueryProperties(
            string resourceId,
            IReadOnlyList<Range<string>> effectivePartitionKeyRanges,
            PartitionKeyDefinition partitionKeyDefinition,
            Cosmos.VectorEmbeddingPolicy vectorEmbeddingPolicy,
            Cosmos.GeospatialType geospatialType)
        {
            this.ResourceId = resourceId;
            this.EffectiveRangesForPartitionKey = effectivePartitionKeyRanges;
            this.PartitionKeyDefinition = partitionKeyDefinition;
            this.VectorEmbeddingPolicy = vectorEmbeddingPolicy;
            this.GeospatialType = geospatialType;
        }

        public string ResourceId { get; }

        //A PartitionKey has one range when it is a full PartitionKey value.
        //It can span many  it is a prefix PartitionKey for a sub-partitioned container.
        public IReadOnlyList<Range<string>> EffectiveRangesForPartitionKey { get; }

        public PartitionKeyDefinition PartitionKeyDefinition { get; }

        public Cosmos.VectorEmbeddingPolicy VectorEmbeddingPolicy { get; }

        public Cosmos.GeospatialType GeospatialType { get; }
    }
}