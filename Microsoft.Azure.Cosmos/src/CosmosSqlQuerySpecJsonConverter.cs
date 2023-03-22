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
            return typeof(SqlParameter) == objectType
                || typeof(SqlQueryResumeInfo.ResumeValue).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is SqlParameter sqlParameter)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("name");
                serializer.Serialize(writer, sqlParameter.Name);
                writer.WritePropertyName("value");

                // if the SqlParameter has stream value we dont pass it through the custom serializer.
                if (sqlParameter.Value is SerializedParameterValue serializedEncryptedData)
                {
                    writer.WriteRawValue(serializedEncryptedData.rawSerializedJsonValue);
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
            else if (value is SqlQueryResumeInfo.ResumeValue resumeValue)
            {
                this.WriteResumeValue(writer, resumeValue, serializer);
            }
        }

        private void WriteResumeValue(JsonWriter writer, SqlQueryResumeInfo.ResumeValue value, JsonSerializer serializer)
        {
            switch (value)
            {
                case SqlQueryResumeInfo.UndefinedResumeValue:
                    writer.WriteStartArray();
                    writer.WriteEndArray();
                    break;

                case SqlQueryResumeInfo.NullResumeValue:
                    writer.WriteNull();
                    break;

                case SqlQueryResumeInfo.BooleanResumeValue booleanValue:
                    serializer.Serialize(writer, booleanValue.Value);
                    break;

                case SqlQueryResumeInfo.NumberResumeValue numberValue:
                    serializer.Serialize(writer, numberValue.Value);
                    break;

                case SqlQueryResumeInfo.StringResumeValue stringValue:
                    serializer.Serialize(writer, stringValue.Value.ToString());
                    break;

                case SqlQueryResumeInfo.ArrayResumeValue arrayValue:
                    writer.WriteStartObject();
                    writer.WritePropertyName(SqlQueryResumeInfo.ResumeValue.PropertyNames.Type);
                    writer.WriteValue(SqlQueryResumeInfo.ResumeValue.PropertyNames.ArrayType);
                    writer.WritePropertyName(SqlQueryResumeInfo.ResumeValue.PropertyNames.Low);
                    writer.WriteValue(arrayValue.HashValue.GetLow());
                    writer.WritePropertyName(SqlQueryResumeInfo.ResumeValue.PropertyNames.High);
                    writer.WriteValue(arrayValue.HashValue.GetHigh());
                    writer.WriteEndObject();
                    break;

                case SqlQueryResumeInfo.ObjectResumeValue objectValue:
                    writer.WriteStartObject();
                    writer.WritePropertyName(SqlQueryResumeInfo.ResumeValue.PropertyNames.Type);
                    writer.WriteValue(SqlQueryResumeInfo.ResumeValue.PropertyNames.ObjectType);
                    writer.WritePropertyName(SqlQueryResumeInfo.ResumeValue.PropertyNames.Low);
                    writer.WriteValue(objectValue.HashValue.GetLow());
                    writer.WritePropertyName(SqlQueryResumeInfo.ResumeValue.PropertyNames.High);
                    writer.WriteValue(objectValue.HashValue.GetHigh());
                    writer.WriteEndObject();
                    break;

                default:
                    throw new ArgumentException($"Invalid {nameof(SqlQueryResumeInfo.ResumeValue)} type.");
            }
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
                Converters = new List<JsonConverter>() { new CosmosSqlQuerySpecJsonConverter(cosmosSerializer ?? propertiesSerializer) },
                MaxDepth = 64, // https://github.com/advisories/GHSA-5crp-9r3c-p9vr
            };

            return new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer(settings));
        }
    }
}
