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
        public CosmosContainerSettings IncludeUniqueKey(params string[] uniquePaths)
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
            foreach(string path in uniquePaths)
            {
                requestedUniqueueKey.Paths.Add(path);
            }

            this.UniqueKeyPolicy.UniqueKeys.Add(requestedUniqueueKey);
            return this;
        }

        /// <summary>
        /// Specifies the indexing mode to be used.
        /// </summary>
        /// <seealso cref="IndexingMode"/>
        public CosmosContainerSettings WithIndexingMode(IndexingMode indexingMode)
        {
            this.IndexingPolicy.IndexingMode = indexingMode;
            return this;
        }

        /// <summary>
        /// Set the default TTL for the container
        /// </summary>
        /// <seealso cref="CosmosContainerSettings.DefaultTimeToLive"/>
        public CosmosContainerSettings WithDefaultTimeToLive(TimeSpan defaultTtl)
        {
            this.DefaultTimeToLive = defaultTtl;
            return this;
        }

        /// <summary>
        /// Specifies the path of item to be indexed
        /// </summary>
        /// <seealso cref="IndexingPolicy.IncludedPaths"/>
        public CosmosContainerSettings IncludIndexPath(string path)
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
        public CosmosContainerSettings ExcludeIndexPath(string path)
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
        /// <seealso cref="CompositePath"/>
        public CosmosContainerSettings IncludeCompositeIndex(params string[] compositeIndexPaths)
        {
            if (compositeIndexPaths == null)
            {
                throw new ArgumentNullException(nameof(compositeIndexPaths));
            }

            if (compositeIndexPaths.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(compositeIndexPaths));
            }

            CompositePath[] fullCompositePaths = new CompositePath[compositeIndexPaths.Length];
            for (int i = 0; i < compositeIndexPaths.Length; i++)
            {
                fullCompositePaths[i] = new CompositePath()
                {
                    Path = compositeIndexPaths[i],
                    Order = CompositePathSortOrder.Ascending,
                };
            }

            return this.IncludeCompositeIndex(fullCompositePaths);
        }

        /// <summary>
        /// Includes the given additional composite index
        /// </summary>
        /// <seealso cref="IndexingPolicy.CompositeIndexes"/>
        /// <seealso cref="CompositePath"/>
        public CosmosContainerSettings IncludeCompositeIndex(params CompositePath[] compositeIndexPaths)
        {
            if (compositeIndexPaths == null)
            {
                throw new ArgumentNullException(nameof(compositeIndexPaths));
            }

            if (compositeIndexPaths.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(compositeIndexPaths));
            }

            Collection<CompositePath> compositePathsCollection = new Collection<CompositePath>();
            foreach (CompositePath path in compositeIndexPaths)
            {
                compositePathsCollection.Add(path);
            }

            this.IndexingPolicy.CompositeIndexes.Add(compositePathsCollection);
            return this;
        }

        /// <summary>
        /// Include spatial index 
        /// </summary>
        /// <seealso cref="SpatialSpec"/>
        /// <seealso cref="SpatialType"/>
        public CosmosContainerSettings IncludeSpatialIndex(string path, params SpatialType[] spatialTypes)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            SpatialSpec spatialSpec = new SpatialSpec();
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
