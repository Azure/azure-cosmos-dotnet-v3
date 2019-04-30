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
        private readonly CosmosContainerFluentDefinition parent;
        private readonly Action<UniqueKey> attachCallback;

        public UniqueKeyFluentDefinitionCore(
            CosmosContainerFluentDefinition parent,
            Action<UniqueKey> attachCallback) 
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
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

        public override CosmosContainerFluentDefinition Attach()
        {
            this.attachCallback(new UniqueKey()
            {
                Paths = this.paths
            });

            return this.parent;
        }
    }
}
