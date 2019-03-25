//-----------------------------------------------------------------------
// <copyright file="QueryItem.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Used to lazily bind a item from a query.
    /// </summary>
    internal sealed class QueryItem
    {
        private static readonly JsonSerializerSettings NoDateParseHandlingJsonSerializerSettings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None
        };
        /// <summary>
        /// whether or not the item has been deserizalized yet.
        /// </summary>
        private bool isItemDeserialized;

        /// <summary>
        /// The actual item.
        /// </summary>
        private object item;

        /// <summary>
        /// The raw value of the item.
        /// </summary>
        [JsonProperty("item")]
        private JRaw RawItem
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the item and deserializes it if it hasn't already been.
        /// </summary>
        /// <remarks>This can be replaced with Lazy of T</remarks>
        /// <returns>The item.</returns>
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
