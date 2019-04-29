//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// <see cref="UniqueKeyPolicy"/> fluent definition.
    /// </summary>
    public class UniqueKeyFluentDefinition : FluentSettings<CosmosContainerFluentDefinition>
    {
        private Collection<string> Paths { get; set; } = new Collection<string>();

        internal UniqueKeyFluentDefinition(CosmosContainerFluentDefinition root) : base(root)
        {
        }

        /// <summary>
        /// Adds a path to the current <see cref="UniqueKeyFluentDefinition"/>.
        /// </summary>
        /// <param name="path">Path for the property to add to the current <see cref="UniqueKeyFluentDefinition"/>. Example: /property</param>
        public virtual UniqueKeyFluentDefinition Path(string path)
        {
            this.Paths.Add(path);
            return this;
        }
    }
}
