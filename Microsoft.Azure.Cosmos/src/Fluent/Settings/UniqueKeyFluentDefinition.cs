//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// <see cref="UniqueKeyPolicy"/> fluent definition.
    /// </summary>
    public abstract class UniqueKeyFluentDefinition : FluentSettings<CosmosContainerFluentDefinition>
    {
        /// <summary>
        /// Adds a path to the current <see cref="UniqueKeyFluentDefinition"/>.
        /// </summary>
        /// <param name="path">Path for the property to add to the current <see cref="UniqueKeyFluentDefinition"/>. Example: /property</param>
        public abstract UniqueKeyFluentDefinition Path(string path);
    }
}
