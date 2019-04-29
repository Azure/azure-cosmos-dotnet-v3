//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    internal sealed class IndexingPolicyFluentDefinitionCore : IndexingPolicyFluentDefinition
    {
        private readonly IndexingPolicy indexingPolicy = new IndexingPolicy();
        private readonly CosmosContainerFluentDefinitionCore parent;
        private PathsFluentDefinition includedPathsBuilder;
        private PathsFluentDefinition excludedPathsBuilder;

        public IndexingPolicyFluentDefinitionCore(CosmosContainerFluentDefinitionCore parent) 
        {
            this.parent = parent;
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
                this.includedPathsBuilder = new PathsFluentDefinitionCore(this, PathsFluentDefinitionType.Included);
            }

            return this.includedPathsBuilder;
        }

        public override PathsFluentDefinition ExcludedPaths()
        {
            if (this.excludedPathsBuilder == null)
            {
                this.excludedPathsBuilder = new PathsFluentDefinitionCore(this, PathsFluentDefinitionType.Excluded);
            }

            return this.excludedPathsBuilder;
        }

        public override CompositeIndexFluentDefinition WithCompositeIndex()
        {
            return new CompositeIndexFluentDefinitionCore(this);
        }

        public override CosmosContainerFluentDefinition Attach()
        {
            this.parent.WithIndexingPolicy(this.indexingPolicy);
            return this.parent;
        }

        public void WithCompositePaths(Collection<CompositePath> compositePaths)
        {
            this.indexingPolicy.CompositeIndexes.Add(compositePaths);
        }

        public void WithSpatialIndex(SpatialSpec spatialSpec)
        {
            this.indexingPolicy.SpatialIndexes.Add(spatialSpec);
        }

        public void WithIncludedPaths(List<string> paths)
        {
            foreach (string path in paths)
            {
                this.indexingPolicy.IncludedPaths.Add(new IncludedPath() { Path = path });
            }
        }

        public void WithExcludedPaths(List<string> paths)
        {
            foreach (string path in paths)
            {
                this.indexingPolicy.ExcludedPaths.Add(new ExcludedPath() { Path = path });
            }
        }
    }
}
