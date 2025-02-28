//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the collection tiering policy for a collection in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>
    internal sealed class CollectionTieringPolicy : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Gets or sets the CollectionTieringPolicy (Timestamp) in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.CollectionTieringType)]
        public string CollectionTieringType
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.CollectionTieringType);
            }
            set
            {
                this.SetValue(Constants.Properties.CollectionTieringType, value);
            }
        }

        /// <summary>
        /// Gets the CollectionTieringThreshold in days for documents in a collection from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// A valid value must be a nonzero positive integer.
        /// The unit of measurement is days.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.CollectionTieringThreshold, NullValueHandling = NullValueHandling.Ignore)]
        internal int? CollectionTieringThreshold
        {
            get
            {
                return base.GetValue<int>(Constants.Properties.CollectionTieringThreshold);
            }
            set
            {
                base.SetValue(Constants.Properties.CollectionTieringThreshold, value);
            }
        }

        /// <summary>
        /// Performs a deep copy of the collection tiering policy.
        /// </summary>
        /// <returns>
        /// A clone of the collection tiering policy.
        /// </returns>
        public object Clone()
        {
            CollectionTieringPolicy cloned = new CollectionTieringPolicy()
            {
                CollectionTieringType = this.CollectionTieringType,
                CollectionTieringThreshold = this.CollectionTieringThreshold,
            };

            return cloned;
        }
    }
}
