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
            List<Range<string>> effectivePartitionKeyRanges,
            PartitionKeyDefinition partitionKeyDefinition,
            Cosmos.GeospatialType geospatialType)
        {
            this.ResourceId = resourceId;
            this.EffectivePartitionKeyRanges = effectivePartitionKeyRanges;
            this.PartitionKeyDefinition = partitionKeyDefinition;
            this.GeospatialType = geospatialType;
        }

        public string ResourceId { get; }
        public IReadOnlyList<Range<string>> EffectivePartitionKeyRanges { get; }
        public PartitionKeyDefinition PartitionKeyDefinition { get; }
        public Cosmos.GeospatialType GeospatialType { get; }
    }
}