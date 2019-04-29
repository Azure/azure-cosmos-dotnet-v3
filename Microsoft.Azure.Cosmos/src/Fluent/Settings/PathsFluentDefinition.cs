//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// Fluent definition to specify paths.
    /// </summary>
    public abstract class PathsFluentDefinition : FluentSettings<IndexingPolicyFluentDefinition>
    {
        /// <summary>
        /// Adds a path to the current <see cref="PathsFluentDefinition"/>.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /property</param>
        public abstract PathsFluentDefinition Path(string path);
    }
}
