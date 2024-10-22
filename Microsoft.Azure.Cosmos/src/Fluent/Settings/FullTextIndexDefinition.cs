//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;

    /// <summary>
    /// Full text index fluent definition.
    /// </summary>
    /// <seealso cref="FullTextIndexPath"/>
#if PREVIEW
    public
#else
    internal
#endif
    class FullTextIndexDefinition<T>
    {
        private readonly FullTextIndexPath fullTextIndexPath = new ();
        private readonly T parent;
        private readonly Action<FullTextIndexPath> attachCallback;

        /// <summary>
        /// Initializes a new instance of the <see cref="FullTextIndexDefinition{T}"/> class.
        /// </summary>
        /// <param name="parent">The original instance of <see cref="ContainerBuilder"/>.</param>
        /// <param name="attachCallback">A callback delegate to be used at a later point of time.</param>
        public FullTextIndexDefinition(
            T parent,
            Action<FullTextIndexPath> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Add a path to the current <see cref="FullTextIndexPath"/> definition.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        /// <returns>An instance of the current <see cref="FullTextIndexDefinition{T}"/>.</returns>
        public FullTextIndexDefinition<T> Path(
            string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.fullTextIndexPath.Path = path;

            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public T Attach()
        {
            this.attachCallback(this.fullTextIndexPath);
            return this.parent;
        }
    }
}
