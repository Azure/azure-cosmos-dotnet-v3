//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System.Collections.Generic;

    /// <summary>
    /// Fluent definition to specify paths.
    /// </summary>
    public class PathsFluentDefinition : FluentSettings<IndexingPolicyFluentDefinition>
    {
        private readonly List<string> includedPaths = new List<string>();

        internal PathsFluentDefinition(IndexingPolicyFluentDefinition indexingPolicyBuilder) : base(indexingPolicyBuilder)
        {
        }

        /// <summary>
        /// Adds a path to the current <see cref="PathsFluentDefinition"/>.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        public virtual PathsFluentDefinition Path(string path)
        {
            this.includedPaths.Add(path);
            return this;
        }
    }
}
