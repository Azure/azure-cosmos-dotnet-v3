//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Indexing Policy fluent definition.
    /// </summary>
    /// <seealso cref="IndexingPolicy"/>
    public class IndexingPolicyFluentDefinition<T>
    {
        private readonly IndexingPolicy indexingPolicy = new IndexingPolicy();
        private readonly T parent;
        private readonly Action<IndexingPolicy> attachCallback;
        private PathsFluentDefinition<IndexingPolicyFluentDefinition<T>> includedPathsBuilder;
        private PathsFluentDefinition<IndexingPolicyFluentDefinition<T>> excludedPathsBuilder;

        /// <summary>
        /// Creates an instance for unit-testing
        /// </summary>
        public IndexingPolicyFluentDefinition() { }

        internal IndexingPolicyFluentDefinition(
            T parent,
            Action<IndexingPolicy> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Defines the <see cref="CosmosContainer"/>'s <see cref="Cosmos.IndexingMode"/>.
        /// </summary>
        /// <param name="indexingMode">An <see cref="Cosmos.IndexingMode"/></param>
        /// <remarks>
        /// If multiple calls are made to this method within the same <see cref="IndexingPolicyFluentDefinition{T}"/>, the last one will apply.
        /// </remarks>
        public virtual IndexingPolicyFluentDefinition<T> IndexingMode(IndexingMode indexingMode)
        {
            this.indexingPolicy.IndexingMode = indexingMode;
            return this;
        }

        /// <summary>
        /// Defines the <see cref="CosmosContainer"/>'s automatic indexing.
        /// </summary>
        public virtual IndexingPolicyFluentDefinition<T> AutomaticIndexing(bool enabled)
        {
            this.indexingPolicy.Automatic = enabled;
            return this;
        }

        /// <summary>
        /// Defines the <see cref="CosmosContainer"/>'s <see cref="IndexingPolicy.IncludedPaths"/>.
        /// </summary>
        public virtual PathsFluentDefinition<IndexingPolicyFluentDefinition<T>> IncludedPaths()
        {
            if (this.includedPathsBuilder == null)
            {
                this.includedPathsBuilder = new PathsFluentDefinition<IndexingPolicyFluentDefinition<T>>(
                    this,
                    (paths) => this.AddIncludedPaths(paths));
            }

            return this.includedPathsBuilder;
        }

        /// <summary>
        /// Defines the <see cref="CosmosContainer"/>'s <see cref="IndexingPolicy.ExcludedPaths"/>.
        /// </summary>
        public virtual PathsFluentDefinition<IndexingPolicyFluentDefinition<T>> ExcludedPaths()
        {
            if (this.excludedPathsBuilder == null)
            {
                this.excludedPathsBuilder = new PathsFluentDefinition<IndexingPolicyFluentDefinition<T>>(
                    this,
                    (paths) => this.AddExcludedPaths(paths));
            }

            return this.excludedPathsBuilder;
        }

        /// <summary>
        /// Defines a Composite Index in the current <see cref="CosmosContainer"/>'s definition.
        /// </summary>
        public virtual CompositeIndexFluentDefinition<IndexingPolicyFluentDefinition<T>> CompositeIndex()
        {
            return new CompositeIndexFluentDefinition<IndexingPolicyFluentDefinition<T>>(
                this,
                (compositePaths) => this.AddCompositePaths(compositePaths));
        }

        /// <summary>
        /// Defines a <see cref="Cosmos.SpatialIndex"/> in the current <see cref="CosmosContainer"/>'s definition.
        /// </summary>
        /// <returns></returns>
        public virtual SpatialIndexFluentDefinition<IndexingPolicyFluentDefinition<T>> SpatialIndex()
        {
            return new SpatialIndexFluentDefinition<IndexingPolicyFluentDefinition<T>>(
                this,
                (spatialIndex) => this.AddSpatialIndex(spatialIndex));
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        public virtual T Attach()
        {
            this.attachCallback(this.indexingPolicy);
            return this.parent;
        }

        private void AddCompositePaths(Collection<CompositePath> compositePaths)
        {
            this.indexingPolicy.CompositeIndexes.Add(compositePaths);
        }

        private void AddSpatialIndex(SpatialSpec spatialSpec)
        {
            this.indexingPolicy.SpatialIndexes.Add(spatialSpec);
        }

        private void AddIncludedPaths(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                this.indexingPolicy.IncludedPaths.Add(new IncludedPath() { Path = path });
            }
        }

        private void AddExcludedPaths(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                this.indexingPolicy.ExcludedPaths.Add(new ExcludedPath() { Path = path });
            }
        }
    }
}
