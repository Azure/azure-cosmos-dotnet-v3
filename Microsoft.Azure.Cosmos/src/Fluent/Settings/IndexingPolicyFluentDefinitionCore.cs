//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    internal sealed class IndexingPolicyFluentDefinitionCore : IndexingPolicyFluentDefinition
    {
        private readonly IndexingPolicy indexingPolicy = new IndexingPolicy();
        private readonly CosmosContainerFluentDefinition parent;
        private readonly Action<IndexingPolicy> attachCallback;
        private PathsFluentDefinition includedPathsBuilder;
        private PathsFluentDefinition excludedPathsBuilder;

        public IndexingPolicyFluentDefinitionCore(
            CosmosContainerFluentDefinition parent,
            Action<IndexingPolicy> attachCallback) 
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        public override IndexingPolicyFluentDefinition WithIndexingMode(IndexingMode indexingMode)
        {
            this.indexingPolicy.IndexingMode = indexingMode;
            return this;
        }

        public override IndexingPolicyFluentDefinition WithoutAutomaticIndexing()
        {
            this.indexingPolicy.Automatic = false;
            return this;
        }

        public override PathsFluentDefinition IncludedPaths()
        {
            if (this.includedPathsBuilder == null)
            {
                this.includedPathsBuilder = new PathsFluentDefinitionCore(
                    this, 
                    (paths) => this.WithIncludedPaths(paths));
            }

            return this.includedPathsBuilder;
        }

        public override PathsFluentDefinition ExcludedPaths()
        {
            if (this.excludedPathsBuilder == null)
            {
                this.excludedPathsBuilder = new PathsFluentDefinitionCore(
                    this,
                    (paths) => this.WithExcludedPaths(paths));
            }

            return this.excludedPathsBuilder;
        }

        public override CompositeIndexFluentDefinition WithCompositeIndex()
        {
            return new CompositeIndexFluentDefinitionCore(
                this,
                (compositePaths) => this.WithCompositePaths(compositePaths));
        }

        public override SpatialIndexFluentDefinition WithSpatialIndex()
        {
            return new SpatialIndexFluentDefinitionCore(
                this,
                (spatialIndex) => this.WithSpatialIndex(spatialIndex));
        }

        public override CosmosContainerFluentDefinition Attach()
        {
            this.attachCallback(this.indexingPolicy);
            return this.parent;
        }

        private void WithCompositePaths(Collection<CompositePath> compositePaths)
        {
            this.indexingPolicy.CompositeIndexes.Add(compositePaths);
        }

        private void WithSpatialIndex(SpatialSpec spatialSpec)
        {
            this.indexingPolicy.SpatialIndexes.Add(spatialSpec);
        }

        private void WithIncludedPaths(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                this.indexingPolicy.IncludedPaths.Add(new IncludedPath() { Path = path });
            }
        }

        private void WithExcludedPaths(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                this.indexingPolicy.ExcludedPaths.Add(new ExcludedPath() { Path = path });
            }
        }
    }
}
