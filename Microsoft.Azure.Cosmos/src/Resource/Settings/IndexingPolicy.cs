//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

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
        internal const string DefaultPath = "/*";

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
        /// <example>
        /// The example below creates a new container by specifying only to exclude specfic paths for indexing
        /// in ascending order (default)
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerSettings containerSettings =
        ///     new CosmosContainerSettings("TestContainer", "/partitionKey")
        ///        .IncludeIndexPath("/excludepath1")
        ///        .IncludeIndexPath("/excludepath2");
        /// ]]>
        /// </code>
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.IncludedPaths)]
        public Collection<IncludedPath> IncludedPaths { get; set; } = new Collection<IncludedPath>();

        /// <summary>
        /// Gets or sets the collection containing <see cref="ExcludedPath"/> objects in the Azure Cosmos DB service.
        /// </summary>
        /// <example>
        /// The example below creates a new container by specifying to exclude paths from indexing
        /// in ascending order (default)
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerSettings containerSettings =
        ///     new CosmosContainerSettings("TestContainer", "/partitionKey")
        ///        .ExcludeIndexPath("/excludepath1")
        ///        .ExcludeIndexPath("/excludepath2");
        /// ]]>
        /// </code>
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.ExcludedPaths)]
        public Collection<ExcludedPath> ExcludedPaths { get; set; } = new Collection<ExcludedPath>();

        /// <summary>
        /// Gets or sets the additional composite indexes 
        /// </summary>
        /// <example>
        /// The example below creates a new container with composite index on (property1, property2)
        /// in ascending order (default)
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerSettings containerSettings =
        ///     new CosmosContainerSettings("TestContainer", "/partitionKey")
        ///         .IncludeCompositeIndex("/compPath1", "/property2");
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The example below creates a new container with composite index on (property1, property2)
        /// in descending order
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerSettings containerSettings =
        ///     new CosmosContainerSettings("TestContainer", "/partitionKey")
        ///         .IncludeCompositeIndex(
        ///            CompositePathDefinition.Create("/property1", CompositePathSortOrder.Descending),
        ///            CompositePathDefinition.Create("/property2", CompositePathSortOrder.Descending));
        /// ]]>
        /// </code>
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.CompositeIndexes)]
        public Collection<Collection<CompositePath>> CompositeIndexes { get; set; } = new Collection<Collection<CompositePath>>();

        /// <summary>
        /// Collection of spatial index definitions to be used
        /// </summary>
        /// <example>
        /// The example below creates a new container with spatial index on property1
        /// in descending order
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerSettings containerSettings =
        ///     new CosmosContainerSettings("TestContainer", "/partitionKey")
        ///         .IncludeSpatialIndex("/property1", SpatialType.Point);
        /// ]]>
        /// </code>
        /// </example>
        [JsonProperty(PropertyName = Constants.Properties.SpatialIndexes)]
        public Collection<SpatialSpec> SpatialIndexes { get; set; } = new Collection<SpatialSpec>();
    }

    public class IndexingPolicyBuilder
    {
        private List<CompositeIndexBuilder> compositeIndexBuilders = new List<CompositeIndexBuilder>();
        private PathsBuilder includedPathsBuilder;
        private PathsBuilder excludedPathsBuilder;
        private IndexingMode indexingMode;

        private CosmosContainerBuilder containerSettingsBuilder { get; }

        public IndexingPolicyBuilder(CosmosContainerBuilder containerSettings)
        {
            this.containerSettingsBuilder = containerSettings;
        }

        public IndexingPolicyBuilder WithIndexingMode(IndexingMode indexingMode)
        {
            this.indexingMode = indexingMode;
            return this;
        }

        public PathsBuilder IncludedPaths()
        {
            if (this.includedPathsBuilder == null)
            {
                this.includedPathsBuilder = new PathsBuilder(this);
            }
            return this.includedPathsBuilder;
        }

        public PathsBuilder ExcludedPaths()
        {
            if (this.excludedPathsBuilder == null)
            {
                this.excludedPathsBuilder = new PathsBuilder(this);
            }
            return this.excludedPathsBuilder;
        }

        public CompositeIndexBuilder WithCompositeIndex()
        {
            var newBuilder = new CompositeIndexBuilder(this);
            this.compositeIndexBuilders.Add(newBuilder);

            return newBuilder;
        }

        public CosmosContainerBuilder Attach()
        {
            return this.containerSettingsBuilder;
        }
    }

    public class PathsBuilder
    {
        private List<string> includedPaths = new List<string>();
        private IndexingPolicyBuilder indexingPolicyBuilder;

        public PathsBuilder(IndexingPolicyBuilder indexingPolicyBuilder)
        {
            this.indexingPolicyBuilder = indexingPolicyBuilder;
        }

        public PathsBuilder Path(string path)
        {
            this.includedPaths.Add(path);
            return this;
        }

        public IndexingPolicyBuilder Attach()
        {
            return this.indexingPolicyBuilder;
        }
    }

    public class CompositeIndexBuilder
    {
        private Collection<CompositePath> compositePaths = new Collection<CompositePath>();
        private IndexingPolicyBuilder indexingPolicyBuilder;

        public CompositeIndexBuilder(IndexingPolicyBuilder indexingPolicyBuilder)
        {
            this.indexingPolicyBuilder = indexingPolicyBuilder;
        }

        public CompositeIndexBuilder Path(string path)
        {
            this.compositePaths.Add(new CompositePath() { Path = path });
            return this;
        }

        public CompositeIndexBuilder Path(string path, CompositePathSortOrder sortOrder)
        {
            this.compositePaths.Add(new CompositePath() { Path = path, Order = sortOrder });
            return this;
        }

        public IndexingPolicyBuilder Attach()
        {
            return this.indexingPolicyBuilder;
        }
    }

    public class SpatialIndexBuilder
    {
        private SpatialSpec spatialSpec = new SpatialSpec();
        private IndexingPolicyBuilder indexingPolicyBuilder;

        public SpatialIndexBuilder(IndexingPolicyBuilder indexingPolicyBuilder)
        {
            this.indexingPolicyBuilder = indexingPolicyBuilder;
        }

        public SpatialIndexBuilder WithPath(string path)
        {
            this.spatialSpec.Path = path;
            return this;
        }

        public SpatialIndexBuilder WithPath(string path, params SpatialType[] spatialTypes)
        {
            this.spatialSpec.Path = path;

            foreach (var e in spatialTypes)
            {
                this.spatialSpec.SpatialTypes.Add(e);
            }

            return this;
        }

        public IndexingPolicyBuilder Attach()
        {
            return this.indexingPolicyBuilder;
        }
    }
}
