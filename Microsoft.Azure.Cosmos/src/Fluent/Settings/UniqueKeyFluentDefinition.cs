//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// <see cref="UniqueKeyPolicy"/> fluent definition.
    /// </summary>
    public class UniqueKeyFluentDefinition
    {
        private readonly Collection<string> paths = new Collection<string>();
        private readonly CosmosContainerFluentDefinitionForCreate parent;
        private readonly Action<UniqueKey> attachCallback;

        internal UniqueKeyFluentDefinition(
            CosmosContainerFluentDefinitionForCreate parent,
            Action<UniqueKey> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Adds a path to the current <see cref="UniqueKeyFluentDefinition"/>.
        /// </summary>
        /// <param name="path">Path for the property to add to the current <see cref="UniqueKeyFluentDefinition"/>. Example: /property</param>
        public virtual UniqueKeyFluentDefinition Path(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.paths.Add(path);
            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        public virtual CosmosContainerFluentDefinitionForCreate Attach()
        {
            this.attachCallback(new UniqueKey()
            {
                Paths = this.paths
            });

            return this.parent;
        }
    }
}
