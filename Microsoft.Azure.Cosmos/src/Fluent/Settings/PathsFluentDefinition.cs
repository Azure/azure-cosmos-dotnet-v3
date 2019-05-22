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
    public class PathsFluentDefinition<T>
    {
        private readonly List<string> paths = new List<string>();
        private readonly T parent;
        private readonly Action<IEnumerable<string>> attachCallback;

        internal PathsFluentDefinition(
            T parent,
            Action<IEnumerable<string>> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Adds a path to the current <see cref="PathsFluentDefinition{T}"/>.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /path/*</param>
        /// <returns>An instance of the current <see cref="PathsFluentDefinition{T}"/>.</returns>
        public virtual PathsFluentDefinition<T> Path(string path)
        {
            this.paths.Add(path);
            return this;
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
    }
}
