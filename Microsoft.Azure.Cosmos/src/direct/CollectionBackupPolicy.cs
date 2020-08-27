//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;

    /// <summary>
    /// Represents the collection backup policy for a collection in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>
    internal sealed class CollectionBackupPolicy : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CollectionBackupPolicy"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// CollectionBackupType is set to Invalid.
        /// </remarks>
        public CollectionBackupPolicy()
        {
            this.CollectionBackupType = CollectionBackupType.Invalid;
        }

        /// <summary>
        /// Gets or sets the CollectionBackupType (Invalid or Continuous) in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.CollectionBackupType"/> enumeration.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.CollectionBackupType)]
        //[JsonConverter(typeof(StringEnumConverter))]
        public CollectionBackupType CollectionBackupType
        {
            get
            {
                int result = base.GetValue<int>(Constants.Properties.CollectionBackupType);
                return (CollectionBackupType)result;
            }
            set
            {
                base.SetValue(Constants.Properties.CollectionBackupType, (int)value);
            }
        }

        /// <summary>
        /// Performs a deep copy of the collection backup policy.
        /// </summary>
        /// <returns>
        /// A clone of the collection backup policy.
        /// </returns>
        public object Clone()
        {
            CollectionBackupPolicy cloned = new CollectionBackupPolicy()
            {
                CollectionBackupType = this.CollectionBackupType,
            };

            return cloned;
        }
    }
}
