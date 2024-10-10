//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// <para>
    /// For cross partition order by queries a query like "SELECT c.id, c.field_0 ORDER BY r.field_7 gets rewritten as:
    /// SELECT r._rid, [{"item": r.field_7}] AS orderByItems, {"id": r.id, "field_0": r.field_0} AS payload
    /// FROM r
    /// WHERE({ document db - formattable order by query - filter})
    /// ORDER BY r.field_7
    /// </para>
    /// <para>
    /// This is so that the client can parse out the _rid, orderByItems from the actual data / payload,
    /// without scanning the entire document.
    /// </para>
    /// <para>
    /// This struct is used to strongly bind the results of that rewritten query.
    /// </para>
    /// </summary>
    internal readonly struct OrderByQueryResult
    {
        private readonly CosmosObject cosmosObject;

        private readonly IReadOnlyList<OrderByItem> orderByItems;

        public CosmosObject RawPayload => this.cosmosObject;

        public OrderByQueryResult(CosmosElement cosmosElement)
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
            this.orderByItems = GetOrderByItems(cosmosObject);
        }

        /// <summary>
        /// Gets the rid of the document.
        /// </summary>
        public string Rid
        {
            get
            {
                // cassandra row uses __sys_rid as opposed to _rid
                if (!this.cosmosObject.TryGetValue(FieldNames.Rid, out CosmosElement cosmosElement))
                {
                    if (!this.cosmosObject.TryGetValue(FieldNames.CassandraRid, out cosmosElement))
                    {
                        throw new InvalidOperationException($"Underlying object does not have an '{FieldNames.Rid}' or '{FieldNames.CassandraRid}' field.");
                    }
                }

                if (!(cosmosElement is CosmosString cosmosString))
                {
                    throw new InvalidOperationException($"'{FieldNames.Rid}' or ' {FieldNames.CassandraRid} ' field.g.");
                }

                return cosmosString.Value;
            }
        }

        /// <summary>
        /// Gets the order by items from the document.
        /// </summary>
        public IReadOnlyList<OrderByItem> OrderByItems => this.orderByItems;

        /// <summary>
        /// Gets the actual document.
        /// </summary>
        public CosmosElement Payload
        {
            get
            {
                if (!this.cosmosObject.TryGetValue(FieldNames.Payload, out CosmosElement cosmosElement))
                {
                    return CosmosUndefined.Create();
                }

                return cosmosElement;
            }
        }

        private static IReadOnlyList<OrderByItem> GetOrderByItems(CosmosObject cosmosObject)
        {
            if (!cosmosObject.TryGetValue(FieldNames.OrderByItems, out CosmosElement cosmosElement))
            {
                throw new InvalidOperationException($"Underlying object does not have an 'orderByItems' field.");
            }

            if (!(cosmosElement is CosmosArray cosmosArray))
            {
                throw new InvalidOperationException($"orderByItems field was not an array.");
            }

            List<OrderByItem> orderByItems = new List<OrderByItem>(cosmosArray.Count);
            foreach (CosmosElement orderByItem in cosmosArray)
            {
                orderByItems.Add(new OrderByItem(orderByItem));
            }

            return orderByItems;
        }

        private static class FieldNames
        {
            public const string OrderByItems = "orderByItems";

            public const string Payload = "payload";

            public const string Rid = "_rid";

            public const string CassandraRid = "__sys_rid";
        }
    }
}
