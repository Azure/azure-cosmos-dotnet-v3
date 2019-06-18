//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;

    /// <summary>
    /// Spatial index fluent definition.
    /// </summary>
    /// <seealso cref="SpatialPath"/>
    public class SpatialIndexDefinition<T>
    {
        private readonly SpatialPath spatialSpec = new SpatialPath();
        private readonly T parent;
        private readonly Action<SpatialPath> attachCallback;

        internal SpatialIndexDefinition(
            T parent,
            Action<SpatialPath> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Adds a path to the current <see cref="SpatialPath"/> definition.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <returns>An instance of the current <see cref="SpatialIndexDefinition{T}"/>.</returns>
        public SpatialIndexDefinition<T> Path(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.spatialSpec.Path = path;
            return this;
        }

        /// <summary>
        /// Add a path to the current <see cref="SpatialPath"/> definition with a particular set of <see cref="SpatialType"/>s.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <param name="spatialTypes">Set of <see cref="SpatialType"/> to apply to the path.</param>
        /// <returns>An instance of the current <see cref="SpatialIndexDefinition{T}"/>.</returns>
        public SpatialIndexDefinition<T> Path(
            string path,
            params SpatialType[] spatialTypes)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (spatialTypes == null)
            {
                throw new ArgumentNullException(nameof(spatialTypes));
            }

            this.spatialSpec.Path = path;

            foreach (SpatialType spatialType in spatialTypes)
            {
                this.spatialSpec.SpatialTypes.Add(spatialType);
            }

            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public T Attach()
        {
            this.attachCallback(this.spatialSpec);
            return this.parent;
        }
    }
}
