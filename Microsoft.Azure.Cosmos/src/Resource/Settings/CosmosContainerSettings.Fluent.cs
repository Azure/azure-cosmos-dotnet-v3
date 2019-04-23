//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;

    public partial class CosmosContainerSettings
    {
        internal static readonly Collection<Index> DefaultIndexes = new Collection<Index>()
        {
            Index.Range(DataType.String, -1),
            Index.Range(DataType.Number, -1)
        };

        /// <summary>
        /// Includes a unique key on that enforces uniqueness constraint on documents in the collection in the Azure Cosmos DB service.
        /// </summary>
        /// <seealso cref="UniqueKey"/>
        [IgnoreForUnitTest]
        public CosmosContainerSettings WithUniqueKey(params string[] uniquePaths)
        {
            if (uniquePaths == null)
            {
                throw new ArgumentNullException(nameof(uniquePaths));
            }

            if (uniquePaths.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(uniquePaths));
            }

            UniqueKey requestedUniqueueKey = new UniqueKey();
            for(int i =0; i < uniquePaths.Length; i++)
            {
                if (string.IsNullOrEmpty(uniquePaths[i]))
                {
                    throw new ArgumentOutOfRangeException($"{nameof(uniquePaths)} has a null or empty at position {i}");
                }

                requestedUniqueueKey.Paths.Add(uniquePaths[i]);
            }

            this.UniqueKeyPolicy.UniqueKeys.Add(requestedUniqueueKey);
            return this;
        }

        /// <summary>
        /// Specifies the indexing mode to be used.
        /// </summary>
        /// <seealso cref="IndexingMode"/>
        [IgnoreForUnitTest]
        public CosmosContainerSettings WithIndexingMode(IndexingMode indexingMode)
        {
            this.IndexingPolicy.IndexingMode = indexingMode;
            return this;
        }

        /// <summary>
        /// Set the default TTL for the container
        /// </summary>
        /// <seealso cref="CosmosContainerSettings.DefaultTimeToLive"/>
        [IgnoreForUnitTest]
        public CosmosContainerSettings WithDefaultTimeToLive(TimeSpan defaultTtl)
        {
            this.DefaultTimeToLive = defaultTtl;
            return this;
        }

        /// <summary>
        /// Specifies the path of item to be indexed
        /// </summary>
        /// <seealso cref="IndexingPolicy.IncludedPaths"/>
        [IgnoreForUnitTest]
        public CosmosContainerSettings WithIncludeIndexPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            IncludedPath includedPath = new IncludedPath()
            {
                Path = path,
                Indexes = CosmosContainerSettings.DefaultIndexes
            };

            this.IndexingPolicy.IncludedPaths.Add(includedPath);
            return this;
        }

        /// <summary>
        /// Path to be excluded for indexing
        /// </summary>
        /// <seealso cref="IndexingPolicy.ExcludedPaths"/>
        [IgnoreForUnitTest]
        public CosmosContainerSettings WithExcludeIndexPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            ExcludedPath excludedPath = new ExcludedPath()
            {
                Path = path
            };

            this.IndexingPolicy.ExcludedPaths.Add(excludedPath);
            return this;
        }

        /// <summary>
        /// Includes the given additional composite index
        /// </summary>
        /// <seealso cref="IndexingPolicy.CompositeIndexes"/>
        /// <seealso cref="CompositePathDefinition"/>
        [IgnoreForUnitTest]
        public CosmosContainerSettings WithCompositeIndex(params string[] compositeIndexPaths)
        {
            if (compositeIndexPaths == null)
            {
                throw new ArgumentNullException(nameof(compositeIndexPaths));
            }

            if (compositeIndexPaths.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(compositeIndexPaths));
            }

            CompositePathDefinition[] compositeSpecs = new CompositePathDefinition[compositeIndexPaths.Length];
            for (int i = 0; i < compositeIndexPaths.Length; i++)
            {
                if (string.IsNullOrEmpty(compositeIndexPaths[i]))
                {
                    throw new ArgumentOutOfRangeException($"{nameof(compositeIndexPaths)} has null or empty entry at position {i}");
                }

                compositeSpecs[i] = new CompositePathDefinition()
                {
                    Path = compositeIndexPaths[i],
                    Order = CompositePathSortOrder.Ascending,
                };
            }

            return this.WithCompositeIndex(compositeSpecs);
        }

        /// <summary>
        /// Includes the given additional composite index
        /// </summary>
        /// <seealso cref="IndexingPolicy.CompositeIndexes"/>
        /// <seealso cref="CompositePathDefinition"/>
        [IgnoreForUnitTest]
        public CosmosContainerSettings WithCompositeIndex(params CompositePathDefinition[] compositeIndexPaths)
        {
            if (compositeIndexPaths == null)
            {
                throw new ArgumentNullException(nameof(compositeIndexPaths));
            }

            if (compositeIndexPaths.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(compositeIndexPaths));
            }

            Collection<CompositePathDefinition> compositePathsCollection = new Collection<CompositePathDefinition>();
            for (int i=0; i < compositeIndexPaths.Length; i++)
            {
                if (compositeIndexPaths[i] == null || String.IsNullOrEmpty(compositeIndexPaths[i].Path))
                {
                    throw new ArgumentOutOfRangeException($"{nameof(compositeIndexPaths)} has null or empty path at position {i}");
                }

                compositePathsCollection.Add(compositeIndexPaths[i]);
            }

            this.IndexingPolicy.CompositeIndexes.Add(compositePathsCollection);
            return this;
        }

        /// <summary>
        /// Include spatial index 
        /// </summary>
        /// <seealso cref="SpatialIndexDefinition"/>
        /// <seealso cref="SpatialType"/>
        [IgnoreForUnitTest]
        public CosmosContainerSettings WithSpatialIndex(string path, params SpatialType[] spatialTypes)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            SpatialIndexDefinition spatialSpec = new SpatialIndexDefinition();
            spatialSpec.Path = path;

            foreach(SpatialType spatialType in spatialTypes)
            {
                spatialSpec.SpatialTypes.Add(spatialType);
            }

            this.IndexingPolicy.SpatialIndexes.Add(spatialSpec);
            return this;
        }
    }
}
