//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// A custom serializer converter for SQL query spec
    /// </summary>
    internal sealed class TextJsonCosmosSqlQuerySpecConverter : JsonConverter<SqlParameter>
    {
        private readonly CosmosSerializer UserSerializer;

        internal TextJsonCosmosSqlQuerySpecConverter(CosmosSerializer userSerializer)
        {
            this.UserSerializer = userSerializer ?? throw new ArgumentNullException(nameof(userSerializer));
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(SqlParameter) == objectType;
        }

        public override SqlParameter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, SqlParameter sqlParameter, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteStringValue(sqlParameter.Name);
            writer.WritePropertyName("value");

            // Use the user serializer for the parameter values so custom conversions are correctly handled
            using (Stream str = this.UserSerializer.ToStream(sqlParameter.Value))
            {
                using (StreamReader streamReader = new StreamReader(str))
                {
                    string parameterValue = streamReader.ReadToEnd();
                    writer.WriteStringValue(parameterValue);
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
            // If both serializers are the same no need for the custom converter
            if (object.ReferenceEquals(cosmosSerializer, propertiesSerializer))
            {
                return propertiesSerializer;
            }

            JsonSerializerOptions settings = new JsonSerializerOptions();

            settings.Converters.Add(new TextJsonCosmosSqlQuerySpecConverter(cosmosSerializer));

            return new CosmosJsonSerializerWrapper(new CosmosTextJsonSerializer(settings));
        }
    }
}
