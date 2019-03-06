//-----------------------------------------------------------------------
// <copyright file="OrderByItem.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Used to represent an order by item for a cross partition ORDER BY query.
    /// </summary>
    /// <example>{"item": 5}</example>
    internal struct OrderByItem
    {
        private const string ItemName = "item";
        private static readonly JsonSerializerSettings NoDateParseHandlingJsonSerializerSettings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None
        };

        private readonly CosmosObject cosmosObject;

        public OrderByItem(CosmosElement cosmosElement)
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

        public bool IsDefined
        {
            get
            {
                return this.cosmosObject.ContainsKey(ItemName);
            }
        }

        public object Item
        {
            get
            {
                if (!this.cosmosObject.TryGetValue(ItemName, out CosmosElement cosmosElement))
                {
                    throw new InvalidOperationException($"Underlying object does not have an 'item' field.");
                }

                return ToObject(cosmosElement);
            }
        }

        public CosmosElementType Type
        {
            get
            {
                if (!this.cosmosObject.TryGetValue(ItemName, out CosmosElement cosmosElement))
                {
                    throw new InvalidOperationException($"Underlying object does not have an 'item' field.");
                }

                return cosmosElement.Type;
            }
        }

        private static object ToObject(CosmosElement cosmosElement)
        {
            if (cosmosElement == null)
            {
                throw new ArgumentNullException($"{nameof(cosmosElement)} must not be null.");
            }

            object obj;
            switch (cosmosElement.Type)
            {
                case CosmosElementType.String:
                    obj = (cosmosElement as CosmosString).Value;
                    break;

                case CosmosElementType.Number:
                    obj = (cosmosElement as CosmosNumber).GetValueAsDouble();
                    break;

                case CosmosElementType.Boolean:
                    obj = (cosmosElement as CosmosBoolean).Value;
                    break;

                case CosmosElementType.Null:
                    obj = null;
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(CosmosElementType)}: {cosmosElement.Type}");
            }

            return obj;
        }
    }
}
