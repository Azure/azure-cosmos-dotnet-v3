//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// Indexing Policy fluent definition.
    /// </summary>
    /// <seealso cref="IndexingPolicy"/>
    public abstract class IndexingPolicyFluentDefinition
    {
        /// <summary>
        /// Defines the <see cref="CosmosContainer"/>'s <see cref="IndexingMode"/>.
        /// </summary>
        /// <param name="indexingMode">An <see cref="IndexingMode"/></param>
        /// <remarks>
        /// If multiple calls are made to this method within the same <see cref="IndexingPolicyFluentDefinition"/>, the last one will apply.
        /// </remarks>
        public abstract IndexingPolicyFluentDefinition WithIndexingMode(IndexingMode indexingMode);

        /// <summary>
        /// Turns off the <see cref="CosmosContainer"/>'s automatic indexing.
        /// </summary>
        public abstract IndexingPolicyFluentDefinition WithoutAutomaticIndexing();

        /// <summary>
        /// Defines the <see cref="CosmosContainer"/>'s <see cref="IndexingPolicy.IncludedPaths"/>.
        /// </summary>
        public abstract PathsFluentDefinition IncludedPaths();

        /// <summary>
        /// Defines the <see cref="CosmosContainer"/>'s <see cref="IndexingPolicy.ExcludedPaths"/>.
        /// </summary>
        public abstract PathsFluentDefinition ExcludedPaths();

        /// <summary>
        /// Defines a Composite Index in the current <see cref="CosmosContainer"/>'s definition.
        /// </summary>
        public abstract CompositeIndexFluentDefinition WithCompositeIndex();

        /// <summary>
        /// Defines a <see cref="SpatialIndex"/> in the current <see cref="CosmosContainer"/>'s definition.
        /// </summary>
        /// <returns></returns>
        public abstract SpatialIndexFluentDefinition WithSpatialIndex();

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        public abstract CosmosContainerFluentDefinition Attach();
    }
}
