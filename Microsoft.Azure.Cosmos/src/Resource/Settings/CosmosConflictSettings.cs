//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents a conflict in the Azure Cosmos DB service.
    /// </summary>
    public class CosmosConflictSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:CosmosConflictSettings"/> class for the Azure Cosmos DB service.
        /// </summary>
        public CosmosConflictSettings()
        {
        }

        /// <summary>
        /// Gets or sets the Id of the resource in the Azure Cosmos DB service.
        /// </summary>
        /// <value>The Id associated with the resource.</value>
        /// <remarks>
        /// <para>
        /// Every resource within an Azure Cosmos DB database account needs to have a unique identifier. 
        /// </para>
        /// <para>
        /// The following characters are restricted and cannot be used in the Id property:
        ///  '/', '\\', '?', '#'
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = Documents.Constants.Properties.Id)]
        public virtual string Id { get; set; }

        /// <summary>
        /// Gets or sets the operation that resulted in the conflict in the Azure Cosmos DB service.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Documents.Constants.Properties.OperationType)]
        public virtual OperationKind OperationKind { get; set; }

        /// <summary>
        /// Gets or sets the type of the conflicting resource in the Azure Cosmos DB service.
        /// </summary>
        [JsonConverter(typeof(ConflictResourceTypeJsonConverter))]
        [JsonProperty(PropertyName = Documents.Constants.Properties.ResourceType)]
        public virtual Type ResourceType { get; set; }

        private class ConflictResourceTypeJsonConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                string resourceType = null;
                Type valueAsType = (Type)value;
                if (valueAsType == typeof(Newtonsoft.Json.Linq.JObject))
                {
                    resourceType = Documents.Constants.Properties.ResourceTypeDocument;
                }
                else if (valueAsType == typeof(CosmosStoredProcedure))
                {
                    resourceType = Documents.Constants.Properties.ResourceTypeStoredProcedure;
                }
                else if (valueAsType == typeof(CosmosTrigger))
                {
                    resourceType = Documents.Constants.Properties.ResourceTypeTrigger;
                }
                else if (valueAsType == typeof(CosmosUserDefinedFunction))
                {
                    resourceType = Documents.Constants.Properties.ResourceTypeUserDefinedFunction;
                }
                else
                {
                    throw new ArgumentException(String.Format(CultureInfo.CurrentUICulture, "Unsupported resource type {0}", value.ToString()));
                }

                writer.WriteValue(resourceType);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.Value == null)
                {
                    return null;
                }

                string resourceType = reader.Value.ToString();

                if (string.Equals(Documents.Constants.Properties.ResourceTypeDocument, resourceType, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(Newtonsoft.Json.Linq.JObject);
                }
                else if (string.Equals(Documents.Constants.Properties.ResourceTypeStoredProcedure, resourceType, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(CosmosStoredProcedure);
                }
                else if (string.Equals(Documents.Constants.Properties.ResourceTypeTrigger, resourceType, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(CosmosTrigger);
                }
                else if (string.Equals(Documents.Constants.Properties.ResourceTypeUserDefinedFunction, resourceType, StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(CosmosUserDefinedFunction);
                }
                else
                {
                    return null;
                }
            }

            public override bool CanRead => true;

            public override bool CanConvert(Type objectType) => true;
        }
    }
}
