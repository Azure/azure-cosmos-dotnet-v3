//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Newtonsoft.Json.Linq;

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
            if(schema != null)
            {
                if(typeof(Schema).IsAssignableFrom(schema.GetType()))
                {
                    return (Schema)schema;
                }
                else
                {
                    // FromObject: for dynamics, it only go through JsonProperty attribute decorated properties.
                    //             for poco, it will go through all public properties
                    JObject serializedPropertyBag = JObject.FromObject(schema);
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
            if(this.propertyBag != null)
            {
                JToken token = this.propertyBag[propertyName];
                if(token != null)
                {
                    return token.ToObject(returnType);
                }
            }

            //Any property not in JSON throw exception rather than returning null.
            throw new DocumentClientException(
                string.Format(CultureInfo.CurrentUICulture,
                RMResources.PropertyNotFound,
                propertyName), null, null);
        }

        private object SetProperty(string propertyName, object value)
        {
            if(value != null)
            {
                if(this.propertyBag == null)
                {
                    this.propertyBag = new JObject();
                }
                this.propertyBag[propertyName] = JToken.FromObject(value);
            }
            else
            {
                if(this.propertyBag != null)
                {
                    this.propertyBag.Remove(propertyName);
                }
            }
            return value;
        }

        private T AsType<T>() //To convert Schema to any type.
        {
            if(typeof(T) == typeof(Schema) || typeof(T) == typeof(object))
            {
                return (T)(object)this;
            }

            if(this.propertyBag == null)
            {
                return default(T);
            }

            //Materialize the type.
            T result = (T)this.propertyBag.ToObject<T>();

            return result;
        }
    }
}
