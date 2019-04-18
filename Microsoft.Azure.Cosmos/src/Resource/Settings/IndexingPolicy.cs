//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Represents the indexing policy configuration for a collection in the Azure Cosmos DB service.
    /// </summary> 
    /// <remarks>
    /// Indexing policies can used to configure which properties (JSON paths) are included/excluded, whether the index is updated consistently
    /// or offline (lazy), automatic vs. opt-in per-document, as well as the precision and type of index per path.
    /// <para>
    /// Refer to <see>http://azure.microsoft.com/documentation/articles/documentdb-indexing-policies/</see> for additional information on how to specify
    /// indexing policies.
    /// </para>
    /// </remarks>
    /// <seealso cref="CosmosContainerSettings"/>
    public sealed class IndexingPolicy 
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IndexingPolicy"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Indexing mode is set to consistent.
        /// </remarks>
        public IndexingPolicy()
        {
            this.Automatic = true;
            this.IndexingMode = IndexingMode.Consistent;
        }

        /// <summary>
        /// Gets or sets a value that indicates whether automatic indexing is enabled for a collection in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// In automatic indexing, documents can be explicitly excluded from indexing using <see cref="T:Microsoft.Azure.Documents.Client.RequestOptions"/>.  
        /// In manual indexing, documents can be explicitly included.
        /// </remarks>
        /// <value>
        /// True, if automatic indexing is enabled; otherwise, false.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.Automatic)]
        public bool Automatic { get; set; }

        /// <summary>
        /// Gets or sets the indexing mode (consistent or lazy) in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.IndexingMode"/> enumeration.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.IndexingMode)]
        [JsonConverter(typeof(StringEnumConverter))]
        public IndexingMode IndexingMode { get; set; }

        /// <summary>
        /// Gets or sets the collection containing <see cref="IncludedPath"/> objects in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The collection containing <see cref="IncludedPath"/> objects.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.IncludedPaths)]
        public Collection<IncludedPath> IncludedPaths { get; set; } = new Collection<IncludedPath>();

        /// <summary>
        /// Gets or sets the collection containing <see cref="ExcludedPath"/> objects in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The collection containing <see cref="ExcludedPath"/> objects.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.ExcludedPaths)]
        public Collection<ExcludedPath> ExcludedPaths { get; set; } = new Collection<ExcludedPath>();

        /// <summary>
        /// Gets or sets the composite indexes for additional indexes
        /// </summary>
        /// <example>
        /// <![CDATA[
        ///   "composite": [
        ///      [
        ///         {
        ///            "path": "/joining_year",
        ///            "order": "ascending"
        ///         },
        ///         {
        ///            "path": "/level",
        ///            "order": "descending"
        ///         }
        ///      ],
        ///      [
        ///         {
        ///            "path": "/country"
        ///         },
        ///         {
        ///            "path": "/city"
        ///         }
        ///      ]
        ///   ]
        /// ]]>
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.CompositeIndexes)]
        public Collection<Collection<CompositePath>> CompositeIndexes { get; set; } = new Collection<Collection<CompositePath>>();

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.SpatialIndexes)]
        public Collection<SpatialSpec> SpatialIndexes { get; set; } = new Collection<SpatialSpec>();
    }
}
