//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Fluent definition to specify included paths.
    /// </summary>
    public class IncludedPathsFluentDefinition<T>
    {
        private readonly List<IncludedPath> paths = new List<IncludedPath>();
        private readonly T parent;
        private readonly Action<IEnumerable<IncludedPath>> attachCallback;

        internal IncludedPathsFluentDefinition(
            T parent,
            Action<IEnumerable<IncludedPath>> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Adds a path to the current <see cref="IncludedPathsFluentDefinition{T}"/>.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /path/*</param>
        /// <returns>An instance of the current <see cref="IncludedPathsFluentDefinition{T}"/>.</returns>
        public virtual IncludedPathsFluentDefinition<T> Path(string path)
        {
            this.paths.Add(new IncludedPath() { Path = path });
            return this;
        }

        /// <summary>
        /// Adds a path to the current <see cref="IncludedPathsFluentDefinition{T}"/>.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /path/*</param>
        /// <returns>An instance of the current <see cref="IncludedPathsFluentDefinition{T}"/>.</returns>
        public virtual IncludedPathIndexFluentDefinition<IncludedPathsFluentDefinition<T>> PathWithIndexes(string path)
        {
            return new IncludedPathIndexFluentDefinition<IncludedPathsFluentDefinition<T>>(this, path, this.AddPathWithIndexes);
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public virtual T Attach()
        {
            this.attachCallback(this.paths);
            return this.parent;
        }

        private void AddPathWithIndexes(IncludedPath includedPath)
        {
            this.paths.Add(includedPath);
        }
    }
}
