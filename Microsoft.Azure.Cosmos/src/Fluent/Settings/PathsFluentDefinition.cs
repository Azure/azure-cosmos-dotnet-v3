//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// Fluent definition to specify paths.
    /// </summary>
    public abstract class PathsFluentDefinition
    {
        /// <summary>
        /// Adds a path to the current <see cref="PathsFluentDefinition"/>.
        /// </summary>
        /// <param name="path">Property path for the current definition. Example: /path/*</param>
        public abstract PathsFluentDefinition Path(string path);

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        public abstract IndexingPolicyFluentDefinition Attach();
    }
}
