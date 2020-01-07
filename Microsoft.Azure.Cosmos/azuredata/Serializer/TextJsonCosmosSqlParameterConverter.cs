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
    /// A custom serializer converter for SQL parameter
    /// </summary>
    internal sealed class TextJsonCosmosSqlParameterConverter : JsonConverter<SqlParameter>
    {
        private readonly CosmosSerializer parameterSerializer;

        internal TextJsonCosmosSqlParameterConverter()
        {
        }

        internal TextJsonCosmosSqlParameterConverter(CosmosSerializer parameterSerializer)
        {
            this.parameterSerializer = parameterSerializer ?? throw new ArgumentNullException(nameof(parameterSerializer));
        }

        public override SqlParameter Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(
            Utf8JsonWriter writer,
            SqlParameter sqlParameter,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(JsonEncodedStrings.Name, sqlParameter.Name);

            writer.WritePropertyName(JsonEncodedStrings.Value);
            if (this.parameterSerializer != null)
            {
                // Use the user serializer for the parameter values so custom conversions are correctly handled
                using (Stream str = this.parameterSerializer.ToStream(sqlParameter.Value))
                {
                    // Workaround to do a WriteRaw
                    using (JsonDocument jsonDocument = JsonDocument.Parse(str))
                    {
                        jsonDocument.WriteTo(writer);
                    }
                }
            }
            else
            {
                JsonSerializer.Serialize(writer, sqlParameter.Value);
            }

            writer.WriteEndObject();
        }
    }
}
