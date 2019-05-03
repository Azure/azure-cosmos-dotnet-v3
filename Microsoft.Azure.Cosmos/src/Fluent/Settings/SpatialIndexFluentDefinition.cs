//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;

    /// <summary>
    /// Spatial index fluent definition.
    /// </summary>
    /// <seealso cref="SpatialSpec"/>
    public class SpatialIndexFluentDefinition<T>
    {
        private readonly SpatialSpec spatialSpec = new SpatialSpec();
        private readonly T parent;
        private readonly Action<SpatialSpec> attachCallback;

        internal SpatialIndexFluentDefinition(
            T parent,
            Action<SpatialSpec> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Adds a path to the current <see cref="SpatialSpec"/> definition.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <returns></returns>
        public virtual SpatialIndexFluentDefinition<T> Path(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.spatialSpec.Path = path;
            return this;
        }

        /// <summary>
        /// Add a path to the current <see cref="SpatialSpec"/> definition with a particular set of <see cref="SpatialType"/>s.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <param name="spatialTypes">Set of <see cref="SpatialType"/> to apply to the path.</param>
        /// <returns></returns>
        public virtual SpatialIndexFluentDefinition<T> Path(
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
        public virtual T Attach()
        {
            this.attachCallback(this.spatialSpec);
            return this.parent;
        }
    }
}
