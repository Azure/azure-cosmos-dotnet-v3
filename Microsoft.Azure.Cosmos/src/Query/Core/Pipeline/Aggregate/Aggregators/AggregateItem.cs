//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal readonly struct AggregateItem
    {
        private const string ItemName1 = "item";
        private const string ItemName2 = "item2";

        private readonly CosmosObject cosmosObject;

        public AggregateItem(CosmosElement cosmosElement)
        {
            CosmosObject cosmosObject = cosmosElement as CosmosObject;

            if (cosmosObject == null)
            {
                // In case of Aggregate query with VALUE query plan, the top level is an array of one item
                CosmosArray cosmosArray = cosmosElement as CosmosArray;
                if (cosmosArray.Count == 1)
                {
                    cosmosObject = cosmosArray[0] as CosmosObject;
                }
            }

            // If the object is still null, then we have an invalid aggregate item
            this.cosmosObject = cosmosObject ?? throw new ArgumentException($"Unsupported aggregate item. Expected CosmosObject"); 
        }

        public CosmosElement Item
        {
            get
            {
                if (!this.cosmosObject.TryGetValue(ItemName2, out CosmosElement cosmosElement))
                {
                    if (!this.cosmosObject.TryGetValue(ItemName1, out cosmosElement))
                    {
                        cosmosElement = CosmosUndefined.Create();
                    }
                }

                return cosmosElement;
            }
        }
    }
}
