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
    public class IndexingPolicyDefinition<T>
    {
        private readonly IndexingPolicy indexingPolicy = new IndexingPolicy();
        private readonly T parent;
        private readonly Action<IndexingPolicy> attachCallback;
        private PathsDefinition<IndexingPolicyDefinition<T>> includedPathsBuilder;
        private PathsDefinition<IndexingPolicyDefinition<T>> excludedPathsBuilder;

        /// <summary>
        /// Creates an instance for unit-testing
        /// </summary>
        public IndexingPolicyDefinition()
        {
        }

        internal IndexingPolicyDefinition(
            T parent,
            Action<IndexingPolicy> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Defines the <see cref="Container"/>'s <see cref="Cosmos.IndexingMode"/>.
        /// </summary>
        /// <param name="indexingMode">An <see cref="Cosmos.IndexingMode"/></param>
        /// <returns>An instance of <see cref="IndexingPolicyDefinition{T}"/>.</returns>
        /// <remarks>
        /// If multiple calls are made to this method within the same <see cref="IndexingPolicyDefinition{T}"/>, the last one will apply.
        /// </remarks>
        public IndexingPolicyDefinition<T> WithIndexingMode(IndexingMode indexingMode)
        {
            this.indexingPolicy.IndexingMode = indexingMode;
            return this;
        }

        /// <summary>
        /// Defines the <see cref="Container"/>'s automatic indexing.
        /// </summary>
        /// <param name="enabled">Defines whether Automatic Indexing is enabled or not.</param>
        /// <returns>An instance of <see cref="IndexingPolicyDefinition{T}"/>.</returns>
        public IndexingPolicyDefinition<T> WithAutomaticIndexing(bool enabled)
        {
            this.indexingPolicy.Automatic = enabled;
            return this;
        }

        /// <summary>
        /// Defines the <see cref="Container"/>'s <see cref="IndexingPolicy.IncludedPaths"/>.
        /// </summary>
        /// <returns>An instance of <see cref="PathsDefinition{T}"/>.</returns>
        public PathsDefinition<IndexingPolicyDefinition<T>> WithIncludedPaths()
        {
            if (this.includedPathsBuilder == null)
            {
                this.includedPathsBuilder = new PathsDefinition<IndexingPolicyDefinition<T>>(
                    this,
                    (paths) => this.AddIncludedPaths(paths));
            }

            return this.includedPathsBuilder;
        }

        /// <summary>
        /// Defines the <see cref="Container"/>'s <see cref="IndexingPolicy.ExcludedPaths"/>.
        /// </summary>
        /// <returns>An instance of <see cref="PathsDefinition{T}"/>.</returns>
        public PathsDefinition<IndexingPolicyDefinition<T>> WithExcludedPaths()
        {
            if (this.excludedPathsBuilder == null)
            {
                this.excludedPathsBuilder = new PathsDefinition<IndexingPolicyDefinition<T>>(
                    this,
                    (paths) => this.AddExcludedPaths(paths));
            }

            return this.excludedPathsBuilder;
        }

        /// <summary>
        /// Defines a Composite Index in the current <see cref="Container"/>'s definition.
        /// </summary>
        /// <returns>An instance of <see cref="CompositeIndexDefinition{T}"/>.</returns>
        public CompositeIndexDefinition<IndexingPolicyDefinition<T>> WithCompositeIndex()
        {
            return new CompositeIndexDefinition<IndexingPolicyDefinition<T>>(
                this,
                (compositePaths) => this.AddCompositePaths(compositePaths));
        }

        /// <summary>
        /// Defines a <see cref="Cosmos.SpatialIndex"/> in the current <see cref="Container"/>'s definition.
        /// </summary>
        /// <returns>An instance of <see cref="SpatialIndexDefinition{T}"/>.</returns>
        public SpatialIndexDefinition<IndexingPolicyDefinition<T>> WithSpatialIndex()
        {
            return new SpatialIndexDefinition<IndexingPolicyDefinition<T>>(
                this,
                (spatialIndex) => this.AddSpatialPath(spatialIndex));
        }

        /// <summary>
        /// Defines a <see cref="VectorIndexPath"/> in the current <see cref="Container"/>'s definition.
        /// </summary>
        /// <returns>An instance of <see cref="VectorIndexDefinition{T}"/>.</returns>
        public VectorIndexDefinition<IndexingPolicyDefinition<T>> WithVectorIndex()
        {
            return new VectorIndexDefinition<IndexingPolicyDefinition<T>>(
                this,
                (vectorIndex) => this.AddVectorIndexPath(vectorIndex));
        }

        /// <summary>
        /// Defines a <see cref="FullTextIndexPath"/> in the current <see cref="Container"/>'s definition.
        /// </summary>
        /// <returns>An instance of <see cref="FullTextIndexDefinition{T}"/>.</returns>
#if PREVIEW
        public
#else
        internal
#endif
        FullTextIndexDefinition<IndexingPolicyDefinition<T>> WithFullTextIndex()
        {
            return new FullTextIndexDefinition<IndexingPolicyDefinition<T>>(
                this,
                (fullTextIndex) => this.AddFullTextndexPath(fullTextIndex));
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public T Attach()
        {
            this.attachCallback(this.indexingPolicy);
            return this.parent;
        }

        private void AddCompositePaths(Collection<CompositePath> compositePaths)
        {
            this.indexingPolicy.CompositeIndexes.Add(compositePaths);
        }

        private void AddSpatialPath(SpatialPath spatialSpec)
        {
            this.indexingPolicy.SpatialIndexes.Add(spatialSpec);
        }

        private void AddVectorIndexPath(VectorIndexPath vectorIndexPath)
        {
            this.indexingPolicy.VectorIndexes.Add(vectorIndexPath);
        }

        private void AddFullTextndexPath(FullTextIndexPath fullTextIndexPath)
        {
            this.indexingPolicy.FullTextIndexes.Add(fullTextIndexPath);
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
