//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Fluent definition to specify paths.
    /// </summary>
    public class PathsDefinition<T>
    {
        private readonly List<string> paths = new List<string>();
        private readonly T parent;
        private readonly Action<IEnumerable<string>> attachCallback;

        internal PathsDefinition(
            T parent,
            Action<IEnumerable<string>> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Adds a path to the current <see cref="PathsDefinition{T}"/>.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /path/*</param>
        /// <returns>An instance of the current <see cref="PathsDefinition{T}"/>.</returns>
        public PathsDefinition<T> Path(string path)
        {
            this.paths.Add(path);
            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public T Attach()
        {
            this.attachCallback(this.paths);
            return this.parent;
        }
    }
}
