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
    internal class VectorIndexDefinition<T>
    {
        private readonly VectorIndexPath vectorIndexPath = new VectorIndexPath();
        private readonly T parent;
        private readonly Action<VectorIndexPath> attachCallback;

        internal VectorIndexDefinition(
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
