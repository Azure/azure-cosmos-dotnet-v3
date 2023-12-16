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
            if (cosmosElement == null)
            {
                throw new ArgumentNullException($"{nameof(cosmosElement)} must not be null.");
            }

            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                // In case of Aggregate query with VALUE query plan, the top level is an array of one item
                cosmosObject = cosmosElement is CosmosArray cosmosArray && cosmosArray[0] is CosmosObject cosmosObjectFromCosmosArray
                    ? cosmosObjectFromCosmosArray
                    : throw new ArgumentException($"{nameof(cosmosElement)} must not be an object.");
            }

            this.cosmosObject = cosmosObject;
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
