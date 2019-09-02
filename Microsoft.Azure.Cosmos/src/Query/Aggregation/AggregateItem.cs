//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal struct AggregateItem
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
                throw new ArgumentException($"{nameof(cosmosElement)} must not be an object.");
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
                        // Undefined
                        cosmosElement = null;
                    }
                }

                return cosmosElement;
            }
        }
    }
}
