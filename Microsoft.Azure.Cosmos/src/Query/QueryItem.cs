//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using Microsoft.Azure.Cosmos.Internal;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class QueryItem
    {
        private static readonly JsonSerializerSettings NoDateParseHandlingJsonSerializerSettings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };

        private bool isItemDeserialized;
        private object item;

        [JsonProperty("item")]
        private JRaw RawItem
        {
            get;
            set;
        }

        public object GetItem()
        {
            if (!this.isItemDeserialized)
            {
                if (this.RawItem == null)
                {
                    this.item = Undefined.Value;
                }
                else
                {
                    this.item = JsonConvert.DeserializeObject((string)this.RawItem.Value, NoDateParseHandlingJsonSerializerSettings);
                }

                this.isItemDeserialized = true;
            }

            return this.item;
        }
    }
}
