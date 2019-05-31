//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Fluent definition to specify included paths.
    /// </summary>
    public class IncludedPathIndexFluentDefinition<T>
    {
        private readonly Collection<Index> indexes = new Collection<Index>();
        private readonly T parent;
        private readonly string path;
        private readonly Action<IncludedPath> attachCallback;

        internal IncludedPathIndexFluentDefinition(
            T parent,
            string path,
            Action<IncludedPath> attachCallback)
        {
            this.parent = parent;
            this.path = path;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Adds a range index to the current <see cref="IncludedPathIndexFluentDefinition{T}"/>.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <returns>An instance of the current <see cref="IncludedPathIndexFluentDefinition{T}"/>.</returns>
        public virtual IncludedPathIndexFluentDefinition<T> RangeIndex(DataType dataType)
        {
            this.indexes.Add(Index.Range(dataType));
            return this;
        }

        /// <summary>
        /// Adds a hash index to the current <see cref="IncludedPathIndexFluentDefinition{T}"/>.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <returns>An instance of the current <see cref="IncludedPathIndexFluentDefinition{T}"/>.</returns>
        public virtual IncludedPathIndexFluentDefinition<T> HashIndex(DataType dataType)
        {
            this.indexes.Add(Index.Hash(dataType));
            return this;
        }

        /// <summary>
        /// Adds a hash index to the current <see cref="IncludedPathIndexFluentDefinition{T}"/>.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <param name="precision">Specifies the precision to be used for the data type associated with this index.</param>
        /// <returns>An instance of the current <see cref="IncludedPathIndexFluentDefinition{T}"/>.</returns>
        public virtual IncludedPathIndexFluentDefinition<T> HashIndex(
            DataType dataType,
            short precision)
        {
            this.indexes.Add(Index.Hash(dataType, precision));
            return this;
        }

        /// <summary>
        /// Adds a spatial index to the current <see cref="IncludedPathIndexFluentDefinition{T}"/>.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <returns>An instance of the current <see cref="IncludedPathIndexFluentDefinition{T}"/>.</returns>
        public virtual IncludedPathIndexFluentDefinition<T> SpatialIndex(DataType dataType)
        {
            this.indexes.Add(Index.Spatial(dataType));
            return this;
        }

        /// <summary>
        /// Adds a range index to the current <see cref="IncludedPathIndexFluentDefinition{T}"/>.
        /// </summary>
        /// <param name="dataType">Specifies the target data type for the index path specification.</param>
        /// <param name="precision">Specifies the precision to be used for the data type associated with this index.</param>
        /// <returns>An instance of the current <see cref="IncludedPathIndexFluentDefinition{T}"/>.</returns>
        public virtual IncludedPathIndexFluentDefinition<T> RangeIndex(
            DataType dataType,
            short precision)
        {
            this.indexes.Add(Index.Range(dataType, precision));
            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public virtual T Attach()
        {
            this.attachCallback(new IncludedPath()
            {
                Path = this.path,
                Indexes = this.indexes
            });
            return this.parent;
        }
    }
}
