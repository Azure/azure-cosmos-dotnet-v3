//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// A custom serializer converter for SQL query spec
    /// </summary>
    internal sealed class TextJsonCosmosSqlQuerySpecConverter : JsonConverter<SqlQuerySpec>
    {
        private readonly TextJsonCosmosSqlParameterConverter sqlParameterConverter;

        internal TextJsonCosmosSqlQuerySpecConverter()
        {
            this.sqlParameterConverter = new TextJsonCosmosSqlParameterConverter();
        }

        internal TextJsonCosmosSqlQuerySpecConverter(CosmosSerializer serializer)
        {
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            this.sqlParameterConverter = new TextJsonCosmosSqlParameterConverter(serializer);
        }

        public override SqlQuerySpec Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(
            Utf8JsonWriter writer,
            SqlQuerySpec sqlQuerySpec,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(JsonEncodedStrings.Query, sqlQuerySpec.QueryText);

            writer.WritePropertyName(JsonEncodedStrings.Parameters);
            writer.WriteStartArray();
            foreach (SqlParameter sqlParameter in sqlQuerySpec.Parameters)
            {
                this.sqlParameterConverter.Write(writer, sqlParameter, options);
            }

            writer.WriteEndArray();

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

            return CosmosSerializer.ForObjectSerializer(new Azure.Core.JsonObjectSerializer(settings));
        }
    }
}
