//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote.OrderBy
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Used to represent an order by item for a cross partition ORDER BY query.
    /// </summary>
    /// <example>{"item": 5}</example>
    internal readonly struct OrderByItem
    {
        private const string ItemName = "item";

        private readonly CosmosObject cosmosObject;

        public OrderByItem(CosmosElement cosmosElement)
        {
            if (cosmosElement == null)
            {
                throw new ArgumentNullException($"{nameof(cosmosElement)} must not be null.");
            }

            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                throw new ArgumentException($"{nameof(cosmosElement)} must be an object.");
            }

            this.cosmosObject = cosmosObject;
        }

        public CosmosElement Item
        {
            get
            {
                if (!this.cosmosObject.TryGetValue(ItemName, out CosmosElement cosmosElement))
                {
                    cosmosElement = null;
                }

                return cosmosElement;
            }
        }

        public static CosmosElement ToCosmosElement(OrderByItem orderByItem)
        {
            return orderByItem.cosmosObject;
        }

        public static OrderByItem FromCosmosElement(CosmosElement cosmosElement)
        {
            return new OrderByItem(cosmosElement);
        }
    }
}
