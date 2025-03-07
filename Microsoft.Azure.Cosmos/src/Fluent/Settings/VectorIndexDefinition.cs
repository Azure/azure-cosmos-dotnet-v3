//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;

    /// <summary>
    /// Vector index fluent definition.
    /// </summary>
    /// <seealso cref="VectorIndexPath"/>
    public class VectorIndexDefinition<T>
    {
        private readonly VectorIndexPath vectorIndexPath = new ();
        private readonly T parent;
        private readonly Action<VectorIndexPath> attachCallback;

        /// <summary>
        /// Initializes a new instance of the <see cref="VectorIndexDefinition{T}"/> class.
        /// </summary>
        /// <param name="parent">The original instance of <see cref="ContainerBuilder"/>.</param>
        /// <param name="attachCallback">A callback delegate to be used at a later point of time.</param>
        public VectorIndexDefinition(
            T parent,
            Action<VectorIndexPath> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Add a path to the current <see cref="VectorIndexPath"/> definition with a particular set of <see cref="VectorIndexType"/>s.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <param name="indexType">Set of <see cref="VectorIndexType"/> to apply to the path.</param>
        /// <returns>An instance of the current <see cref="VectorIndexDefinition{T}"/>.</returns>
        public VectorIndexDefinition<T> Path(
            string path,
            VectorIndexType indexType)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.vectorIndexPath.Path = path;
            this.vectorIndexPath.Type = indexType;

            return this;
        }

        /// <summary>
        /// Configures the quantization byte size for the current <see cref="VectorIndexPath"/> definition.
        /// </summary>
        /// <param name="quantizationByteSize">
        /// The number of bytes used in product quantization of the vectors. This is an optional parameter and applies to index
        /// types DiskANN and quantizedFlat. Note that, the allowed range for this parameter is between 1 and 3.
        /// </param>
        /// <returns>An instance of the current <see cref="VectorIndexDefinition{T}"/>.</returns>
#if PREVIEW
        public
#else
        internal
#endif
        VectorIndexDefinition<T> WithQuantizationByteSize(
            int quantizationByteSize)
        {
            this.vectorIndexPath.QuantizationByteSize = quantizationByteSize;
            return this;
        }

        /// <summary>
        /// Configures the indexing search list size for the current <see cref="VectorIndexPath"/> definition.
        /// </summary>
        /// <param name="indexingSearchListSize">
        /// This represents the size of the candidate list of approximate neighbors stored while building the DiskANN index as part of the optimization processes.
        /// This is an optional parameter and applies to index type DiskANN only. The allowed range for this parameter is between 25 and 500.
        /// </param>
        /// <returns>An instance of the current <see cref="VectorIndexDefinition{T}"/>.</returns>
#if PREVIEW
        public
#else
        internal
#endif
        VectorIndexDefinition<T> WithIndexingSearchListSize(
            int indexingSearchListSize)
        {
            this.vectorIndexPath.IndexingSearchListSize = indexingSearchListSize;
            return this;
        }

        /// <summary>
        /// Configures the vector index shard key for the current <see cref="VectorIndexPath"/> definition.
        /// </summary>
        /// <param name="vectorIndexShardKey">
        /// A string array containing the shard keys used for partitioning the vector indexes. This is an optional parameter and
        /// applies to index types DiskANN and quantizedFlat.
        /// </param>
        /// <returns>An instance of the current <see cref="VectorIndexDefinition{T}"/>.</returns>
        internal VectorIndexDefinition<T> WithVectorIndexShardKey(
            string[] vectorIndexShardKey)
        {
            this.vectorIndexPath.VectorIndexShardKey = vectorIndexShardKey ?? throw new ArgumentNullException(nameof(vectorIndexShardKey));
            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public T Attach()
        {
            this.attachCallback(this.vectorIndexPath);
            return this.parent;
        }
    }
}
