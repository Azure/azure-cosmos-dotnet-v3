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
    public class UniqueKeyDefinition
    {
        private readonly Collection<string> paths = new Collection<string>();
        private readonly ContainerBuilder parent;
        private readonly Action<UniqueKey> attachCallback;

        internal UniqueKeyDefinition(
            ContainerBuilder parent,
            Action<UniqueKey> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Adds a path to the current <see cref="UniqueKeyDefinition"/>.
        /// </summary>
        /// <param name="path">Path for the property to add to the current <see cref="UniqueKeyDefinition"/>. Example: /property</param>
        /// <returns>An instance of the current <see cref="UniqueKeyDefinition"/>.</returns>
        public UniqueKeyDefinition Path(string path)
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
        /// <returns>An instance of the parent.</returns>
        public ContainerBuilder Attach()
        {
            this.attachCallback(new UniqueKey()
            {
                Paths = this.paths
            });

            return this.parent;
        }
    }
}
