//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Composite Index fluent definition.
    /// </summary>
    /// <seealso cref="CompositePath"/>
    public class CompositeIndexFluentDefinition<T>
    {
        private readonly Collection<CompositePath> compositePaths = new Collection<CompositePath>();
        private readonly T parent;
        private readonly Action<Collection<CompositePath>> attachCallback;

        internal CompositeIndexFluentDefinition(
            T parent,
            Action<Collection<CompositePath>> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Add a path to the current <see cref="CompositePath"/> definition.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        public virtual CompositeIndexFluentDefinition<T> Path(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.compositePaths.Add(new CompositePath() { Path = path });
            return this;
        }

        /// <summary>
        /// Add a path to the current <see cref="CompositePath"/> definition with a particular <see cref="CompositePathSortOrder"/>.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <param name="sortOrder"><see cref="CompositePathSortOrder"/> to apply on the path.</param>
        /// <returns></returns>
        public virtual CompositeIndexFluentDefinition<T> Path(
            string path,
            CompositePathSortOrder sortOrder)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.compositePaths.Add(new CompositePath() { Path = path, Order = sortOrder });
            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        public virtual T Attach()
        {
            this.attachCallback(this.compositePaths);
            return this.parent;
        }
    }
}
