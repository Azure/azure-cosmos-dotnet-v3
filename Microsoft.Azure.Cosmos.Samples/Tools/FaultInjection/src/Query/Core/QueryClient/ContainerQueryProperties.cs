//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryClient
{
    using Microsoft.Azure.Documents;

    internal readonly struct ContainerQueryProperties
    {
        public ContainerQueryProperties(
            string resourceId,
            string effectivePartitionKeyString,
            PartitionKeyDefinition partitionKeyDefinition,
            Cosmos.GeospatialType geospatialType)
        {
            this.ResourceId = resourceId;
            this.EffectivePartitionKeyString = effectivePartitionKeyString;
            this.PartitionKeyDefinition = partitionKeyDefinition;
            this.GeospatialType = geospatialType;
        }

        public string ResourceId { get; }
        public string EffectivePartitionKeyString { get; }
        public PartitionKeyDefinition PartitionKeyDefinition { get; }
        public Cosmos.GeospatialType GeospatialType { get; }
    }
}