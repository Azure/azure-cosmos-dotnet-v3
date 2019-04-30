//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// <see cref="IndexingPolicy"/> fluent definition.
    /// </summary>
    public abstract class IndexingPolicyFluentDefinition : FluentSettings<CosmosContainerFluentDefinition>
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
    }
}
