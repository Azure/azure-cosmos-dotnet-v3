//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Newtonsoft.Json;

    /// <summary>
    /// A custom serializer converter for SQL query spec
    /// </summary>
    internal sealed class CosmosSqlQuerySpecJsonConverter : JsonConverter
    {
        private readonly CosmosSerializer UserSerializer;

        internal CosmosSqlQuerySpecJsonConverter(CosmosSerializer userSerializer)
        {
            this.UserSerializer = userSerializer ?? throw new ArgumentNullException(nameof(userSerializer));
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(SqlParameter) == objectType;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            SqlParameter sqlParameter = (SqlParameter)value;

            writer.WriteStartObject();
            writer.WritePropertyName("name");
            serializer.Serialize(writer, sqlParameter.Name);
            writer.WritePropertyName("value");

            // if the SqlParameter has stream value we dont pass it through the custom serializer.
            if (sqlParameter.Value is SerializedParameterValue serializedEncryptedData)
            {
                using (StreamReader streamReader = new StreamReader(serializedEncryptedData.valueStream))
                {
                    string parameterValue = streamReader.ReadToEnd();
                    writer.WriteRawValue(parameterValue);
                }
            }
            else
            {
                // Use the user serializer for the parameter values so custom conversions are correctly handled
                using (Stream str = this.UserSerializer.ToStream(sqlParameter.Value))
                {
                    using (StreamReader streamReader = new StreamReader(str))
                    {
                        string parameterValue = streamReader.ReadToEnd();
                        writer.WriteRawValue(parameterValue);
                    }
                }
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Only create a custom SQL query spec serializer if there is a customer serializer else
        /// use the default properties serializer
        /// </summary>
        internal static CosmosSerializer CreateSqlQuerySpecSerializer(
            CosmosSerializer cosmosSerializer,
            CosmosSerializer propertiesSerializer)
        {
            if (propertiesSerializer is CosmosJsonSerializerWrapper cosmosJsonSerializerWrapper)
            {
                propertiesSerializer = cosmosJsonSerializerWrapper.InternalJsonSerializer;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                Converters = new List<JsonConverter>() { new CosmosSqlQuerySpecJsonConverter(cosmosSerializer ?? propertiesSerializer) }
            };

            return new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer(settings));
        }
    }
}
