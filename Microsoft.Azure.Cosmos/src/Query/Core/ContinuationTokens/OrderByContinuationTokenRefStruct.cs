// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy;

    internal ref struct OrderByContinuationTokenRefStruct
    {
        private const string CompositeTokenPropertyName = "compositeToken";
        private const string OrderByItemsPropertyName = "orderByItems";
        private const string RidPropertyName = "rid";
        private const string SkipCountPropertyName = "skipCount";
        private const string FilterPropertyName = "filter";
        private const string ItemPropertyName = "item";

        public OrderByContinuationTokenRefStruct(
            CompositeContinuationTokenRefStruct compositeContinuationTokenRefStruct,
            IReadOnlyList<OrderByItem> orderByItems,
            string rid,
            int skipCount,
            string filter)
        {
            this.CompositeContinuationToken = compositeContinuationTokenRefStruct;
            this.OrderByItems = orderByItems;
            this.Rid = rid;
            this.SkipCount = skipCount;
            this.Filter = filter;
        }

        public CompositeContinuationTokenRefStruct CompositeContinuationToken { get; }
        public IReadOnlyList<OrderByItem> OrderByItems { get; }
        public string Rid { get; }
        public int SkipCount { get; }
        public string Filter { get; }

        public void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            jsonWriter.WriteObjectStart();

            jsonWriter.WriteFieldName(OrderByContinuationTokenRefStruct.CompositeTokenPropertyName);
            this.CompositeContinuationToken.WriteTo(jsonWriter);

            jsonWriter.WriteFieldName(OrderByContinuationTokenRefStruct.OrderByItemsPropertyName);
            jsonWriter.WriteArrayStart();

            foreach (OrderByItem orderByItem in this.OrderByItems)
            {
                jsonWriter.WriteObjectStart();

                if (orderByItem.Item != null)
                {
                    jsonWriter.WriteFieldName(OrderByContinuationTokenRefStruct.ItemPropertyName);
                    orderByItem.Item.WriteTo(jsonWriter);
                }

                jsonWriter.WriteObjectEnd();
            }

            jsonWriter.WriteArrayEnd();

            jsonWriter.WriteFieldName(OrderByContinuationTokenRefStruct.RidPropertyName);
            jsonWriter.WriteStringValue(this.Rid);

            jsonWriter.WriteFieldName(OrderByContinuationTokenRefStruct.SkipCountPropertyName);
            jsonWriter.WriteNumberValue(this.SkipCount);

            jsonWriter.WriteFieldName(OrderByContinuationTokenRefStruct.FilterPropertyName);
            if (this.Filter != null)
            {
                jsonWriter.WriteStringValue(this.Filter);
            }
            else
            {
                jsonWriter.WriteNullValue();
            }

            jsonWriter.WriteObjectEnd();
        }
    }
}
