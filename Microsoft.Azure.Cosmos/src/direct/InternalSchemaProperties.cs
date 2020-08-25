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
    /// Represents internal schema properties of schema policy for a collection in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>
    internal sealed class InternalSchemaProperties : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Gets or sets a value that indicates whether collection schema represented by the schema policy
        /// is only used for analytics purposes.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.UseSchemaForAnalyticsOnly)]
        internal bool UseSchemaForAnalyticsOnly
        {
            get
            {
                return base.GetValue<bool>(Constants.Properties.UseSchemaForAnalyticsOnly);
            }
            set
            {
                base.SetValue(Constants.Properties.UseSchemaForAnalyticsOnly, value);
            }
        }

        /// <summary>
        /// Performs a deep copy of the internal schema properties.
        /// </summary>
        /// <returns>
        /// A clone of the internal schema properties.
        /// </returns>
        public object Clone()
        {
            InternalSchemaProperties cloned = new InternalSchemaProperties()
            {
                UseSchemaForAnalyticsOnly = this.UseSchemaForAnalyticsOnly
            };

            return cloned;
        }
    }
}
