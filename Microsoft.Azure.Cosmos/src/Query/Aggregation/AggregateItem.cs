//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class AggregateItem
    {
        private static readonly JsonSerializerSettings NoDateParseHandlingJsonSerializerSettings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };

        private readonly Lazy<object> item;

        [JsonProperty("item")]
        private JRaw RawItem
        {
            get;
            set;
        }

        [JsonProperty("item2")]
        private JRaw RawItem2
        {
            get;
            set;
        }

        public AggregateItem(JRaw rawItem, JRaw rawItem2)
        {
            this.RawItem = rawItem;
            this.RawItem2 = rawItem2;
            this.item = new Lazy<object>(this.InitLazy);
        }

        private object InitLazy()
        {
            object item;
            if (this.RawItem == null)
            {
                item = Undefined.Value;
            }
            else
            {
                item = JsonConvert.DeserializeObject((string)this.RawItem.Value, NoDateParseHandlingJsonSerializerSettings);
            }

            // If there is an item2, then take that over item1 (since it's more precise).
            if (this.RawItem2 != null)
            {
                item = JsonConvert.DeserializeObject((string)this.RawItem2.Value, NoDateParseHandlingJsonSerializerSettings);
            }

            return item;
        }

        public object GetItem()
        {
            return this.item.Value;
        }
    }
}
