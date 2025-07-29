//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using static Microsoft.Azure.Documents.Rntbd.TransportClient;

    /// <summary>
    /// Represents a schema in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks> 
    /// A schema is a structured JSON document.
    /// </remarks>
    internal sealed class Schema : Resource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Document"/> class in the Azure Cosmos DB service.
        /// </summary>
        public Schema()
        {
        }

        /// <summary>
        /// Gets the resource link for the schema from the Azure Cosmos DB service.
        /// </summary>
        public string ResourceLink
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.ResourceLink);
            }
        }

        //Helper to materialize Document from any .NET object.
        internal static Schema FromObject(object schema)
        {
            if (schema != null)
            {
                if (typeof(Schema).IsAssignableFrom(schema.GetType()))
                {
                    return (Schema)schema;
                }
                else
                {
                    JsonObject serializedPropertyBag = JsonSerializer.SerializeToNode(schema, schema.GetType()) as JsonObject;
                    Schema typeSchema = new Schema();
                    typeSchema.propertyBag = serializedPropertyBag;
                    return typeSchema;
                }
            }
            return null;
        }

        //Property Getter/Setter for Expandable User property.
        private object GetProperty(
            string propertyName, Type returnType)
        {
            if (this.propertyBag != null && this.propertyBag.TryGetPropertyValue(propertyName, out JsonNode token) && token != null)
            {
                return this.SerializerSettings == null
                    ? token.Deserialize(returnType)
                    : token.Deserialize(returnType, this.SerializerSettings);
            }

            throw new DocumentClientException(
                string.Format(CultureInfo.CurrentUICulture,
                RMResources.PropertyNotFound,
                propertyName), null, null);
        }

        private object SetProperty(string propertyName, object value)
        {
            if (this.propertyBag == null)
            {
                this.propertyBag = new JsonObject();
            }
            if (value != null)
            {
                this.propertyBag[propertyName] = JsonSerializer.SerializeToNode(value, value.GetType(), this.SerializerSettings);
            }
            else
            {
                this.propertyBag.Remove(propertyName);
            }
            return value;
        }

        private T AsType<T>() //To convert Schema to any type.
        {
            if (typeof(T) == typeof(Schema) || typeof(T) == typeof(object))
            {
                return (T)(object)this;
            }

            if (this.propertyBag == null)
            {
                return default(T);
            }

            return this.SerializerSettings == null
                ? this.propertyBag.Deserialize<T>()
                : this.propertyBag.Deserialize<T>(this.SerializerSettings);
        }
    }
}
