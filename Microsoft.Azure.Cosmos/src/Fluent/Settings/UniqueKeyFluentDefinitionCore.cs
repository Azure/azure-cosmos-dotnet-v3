//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.ObjectModel;

    internal sealed class UniqueKeyFluentDefinitionCore : UniqueKeyFluentDefinition
    {
        private readonly Collection<string> paths = new Collection<string>();
        private readonly CosmosContainerFluentDefinitionCore parent;

        public UniqueKeyFluentDefinitionCore(CosmosContainerFluentDefinitionCore parent) 
        {
            this.parent = parent;
        }

        public override UniqueKeyFluentDefinition Path(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.paths.Add(path);
            return this;
        }

        public override CosmosContainerFluentDefinitionForCreate Attach()
        {
            this.parent.WithUniqueKey(
                new UniqueKey()
                {
                    Paths = this.paths
                });

            return this.parent;
        }
    }
}
