//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System.Collections.ObjectModel;

    /// <summary> 
    /// Specifies a path within a JSON document to be included in the Azure Cosmos DB service.
    /// </summary>
    public sealed class IncludedPath
    {
        /// <summary>
        /// Gets or sets the path to be indexed in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The path to be indexed.
        /// </value>
        /// <remarks>
        /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-indexing-policies/#ConfigPolicy for how to specify paths.
        /// Some valid examples: /"prop"/?, /"prop"/**, /"prop"/"subprop"/?, /"prop"/[]/?
        /// </remarks>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the collection of <see cref="Index"/> objects to be applied for this included path in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The collection of the <see cref="Index"/> objects to be applied for this included path.
        /// </value>
        internal Collection<Index> Indexes { get; set; } = new Collection<Index>();
    }
}
