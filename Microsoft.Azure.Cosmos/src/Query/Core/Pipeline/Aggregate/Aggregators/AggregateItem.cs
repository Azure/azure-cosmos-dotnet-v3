//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate.Aggregators
{
    using System;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal readonly struct AggregateItem
    {
        private const string ItemName1 = "item";
        private const string ItemName2 = "item2";

        private readonly CosmosObject cosmosObject;

        public AggregateItem(CosmosElement cosmosElement)
        {
            // If the query is not a select value query then the top level is a an object
            CosmosObject cosmosObject = cosmosElement as CosmosObject;

            if (cosmosObject == null)
            {
                // In case of Aggregate query with VALUE query plan, the top level is an array of one item after it is rewritten
                // For example, if the query is
                // SELECT VALUE {"age": c.age}
                // FROM c
                // GROUP BY c.age
                // Fhe rewritten query is 
                // SELECT [{"item": c.age}] AS groupByItems, {"age": c.age} AS payload
                // FROM c
                // GROUP BY c.age

                // In this case, the top level is an array of one item [{"item": c.age}]
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
