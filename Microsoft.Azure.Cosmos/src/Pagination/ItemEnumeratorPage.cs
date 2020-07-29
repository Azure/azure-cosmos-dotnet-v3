// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class ItemEnumeratorPage<TState> : Page<TState>
        where TState : State
    {
        public ItemEnumeratorPage(IReadOnlyList<CosmosElement> cosmosElements)
        {
            this.Items = new Queue<CosmosElement>(cosmosElements);
        }

        public Queue<CosmosElement> Items { get; }
    }
}
