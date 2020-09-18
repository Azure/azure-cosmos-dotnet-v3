//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Scripts;
    using Newtonsoft.Json;

    internal sealed class ConflictResourceTypeJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            string resourceType = null;
            Type valueAsType = (Type)value;
            if (valueAsType == typeof(CosmosElements.CosmosElement))
            {
                resourceType = Documents.Constants.Properties.ResourceTypeDocument;
            }
            else if (valueAsType == typeof(StoredProcedureProperties))
            {
                resourceType = Documents.Constants.Properties.ResourceTypeStoredProcedure;
            }
            else if (valueAsType == typeof(TriggerProperties))
            {
                resourceType = Documents.Constants.Properties.ResourceTypeTrigger;
            }
            else if (valueAsType == typeof(UserDefinedFunctionProperties))
            {
                resourceType = Documents.Constants.Properties.ResourceTypeUserDefinedFunction;
            }
            else
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported resource type {0}", value.ToString()));
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
                return typeof(CosmosElements.CosmosElement);
            }
            else if (string.Equals(Documents.Constants.Properties.ResourceTypeStoredProcedure, resourceType, StringComparison.OrdinalIgnoreCase))
            {
                return typeof(StoredProcedureProperties);
            }
            else if (string.Equals(Documents.Constants.Properties.ResourceTypeTrigger, resourceType, StringComparison.OrdinalIgnoreCase))
            {
                return typeof(TriggerProperties);
            }
            else if (string.Equals(Documents.Constants.Properties.ResourceTypeUserDefinedFunction, resourceType, StringComparison.OrdinalIgnoreCase))
            {
                return typeof(UserDefinedFunctionProperties);
            }
            else
            {
                return null;
            }
        }

        public override bool CanRead => true;

        public override bool CanConvert(Type objectType)
        {
            return true;
        }
    }
}
