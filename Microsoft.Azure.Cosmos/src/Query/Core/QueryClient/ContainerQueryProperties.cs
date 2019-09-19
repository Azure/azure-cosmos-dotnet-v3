//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using Microsoft.Azure.Documents;

    internal struct ContainerQueryProperties
    {
        internal ContainerQueryProperties(
            string resourceId,
            string effectivePartitionKeyString,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            this.ResourceId = resourceId;
            this.EffectivePartitionKeyString = effectivePartitionKeyString;
            this.PartitionKeyDefinition = partitionKeyDefinition;
        }

        internal string ResourceId { get; }
        internal string EffectivePartitionKeyString { get; }
        internal PartitionKeyDefinition PartitionKeyDefinition { get; }
    }
}