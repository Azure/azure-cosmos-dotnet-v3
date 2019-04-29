//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System.Collections.Generic;

    /// <summary>
    /// <see cref="IndexingPolicy"/> fluent definition.
    /// </summary>
    public class IndexingPolicyFluentDefinition : FluentSettings<CosmosContainerFluentDefinition>
    {
        private readonly List<CompositeIndexFluentDefinition> compositeIndexBuilders = new List<CompositeIndexFluentDefinition>();
        private PathsFluentDefinition includedPathsBuilder;
        private PathsFluentDefinition excludedPathsBuilder;
        private IndexingMode indexingMode;

        internal IndexingPolicyFluentDefinition(CosmosContainerFluentDefinition containerSettings) : base(containerSettings)
        {
        }

        /// <summary>
        /// Defines the <see cref="CosmosContainer"/> <see cref="IndexingMode"/>.
        /// </summary>
        /// <param name="indexingMode">An <see cref="IndexingMode"/></param>
        /// <remarks>
        /// If multiple calls are made to this method within the same <see cref="IndexingPolicyFluentDefinition"/>, the last one will apply.
        /// </remarks>
        public virtual IndexingPolicyFluentDefinition WithIndexingMode(IndexingMode indexingMode)
        {
            this.indexingMode = indexingMode;
            return this;
        }

        /// <summary>
        /// Defines the <see cref="CosmosContainer"/>'s <see cref="IndexingPolicy.IncludedPaths"/>.
        /// </summary>
        public virtual PathsFluentDefinition IncludedPaths()
        {
            if (this.includedPathsBuilder == null)
            {
                this.includedPathsBuilder = new PathsFluentDefinition(this);
            }

            return this.includedPathsBuilder;
        }

        /// <summary>
        /// Defines the <see cref="CosmosContainer"/> <see cref="IndexingPolicy.ExcludedPaths"/>.
        /// </summary>
        public virtual PathsFluentDefinition ExcludedPaths()
        {
            if (this.excludedPathsBuilder == null)
            {
                this.excludedPathsBuilder = new PathsFluentDefinition(this);
            }

            return this.excludedPathsBuilder;
        }

        /// <summary>
        /// Defines a Composite Index in the current <see cref="CosmosContainer"/> definition.
        /// </summary>
        public virtual CompositeIndexFluentDefinition WithCompositeIndex()
        {
            CompositeIndexFluentDefinition newBuilder = new CompositeIndexFluentDefinition(this);
            this.compositeIndexBuilders.Add(newBuilder);
            return newBuilder;
        }
    }
}
