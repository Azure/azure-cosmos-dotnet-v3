//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Fluent;

    internal partial class CosmosContainersCore
    {
        public override CosmosContainerFluentDefinitionForCreate Create(
            string name,
            string partitionKeyPath)
        {
            if (string.IsNullOrEmpty(partitionKeyPath))
            {
                throw new ArgumentNullException(nameof(partitionKeyPath));
            }

            return new CosmosContainerFluentDefinitionForCreate(this, name, partitionKeyPath);
        }
    }
}
