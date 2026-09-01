//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Describes metadata for a secondary index.
    /// </summary>
    internal interface ISecondaryIndexMetadata
    {
        /// <summary>Gets the resource identifier of the secondary index.</summary>
        string Rid { get; }

        /// <summary>Gets the resource identifier of the source collection.</summary>
        string SourceCollectionRid { get; }

        /// <summary>Gets the secondary index partition key definition.</summary>
        PartitionKeyDefinition PartitionKey { get; }

        /// <summary>Gets the secondary index indexing policy.</summary>
        Cosmos.IndexingPolicy IndexingPolicy { get; }

        /// <summary>Gets the mapping from source paths to projected secondary index paths.</summary>
        IReadOnlyDictionary<string, string> IncludedProperties { get; }

        /// <summary>Gets the consistency level of the secondary index.</summary>
        Cosmos.ConsistencyLevel Consistency { get; }
    }
}
