//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary> 
    /// Specifies a path within a JSON document to be included in the Azure Cosmos DB service.
    /// </summary>
    public sealed class IncludedPath
    {
        private Collection<Index> indexes;

        /// <summary>
        /// Initializes a new instance of the <see cref="IncludedPath"/> class for the Azure Cosmos DB service.
        /// </summary>
        public IncludedPath()
        {
        }

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
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the collection of <see cref="Index"/> objects to be applied for this included path in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The collection of the <see cref="Index"/> objects to be applied for this included path.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.Indexes)]
        public Collection<Index> Indexes
        {
            get
            {
                if (this.indexes == null)
                {
                    this.indexes = new Collection<Index>();
                }

                return this.indexes;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, RMResources.PropertyCannotBeNull, "Indexes"));
                }

                this.indexes = value;
            }
        }
    }
}
