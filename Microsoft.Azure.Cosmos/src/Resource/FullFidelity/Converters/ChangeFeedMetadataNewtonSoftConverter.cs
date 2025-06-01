// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.FullFidelity.Converters
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Spatial;
    using Newtonsoft.Json;

    internal class ChangeFeedMetadataNewtonSoftConverter : JsonConverter
    {
        private readonly static DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="JsonWriter"/> to write to.</param>
        /// <param name="value">The object value to write.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is ChangeFeedMetadata metadata)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(ChangeFeedMetadataFields.ConflictResolutionTimestamp);
                serializer.Serialize(writer, ChangeFeedMetadataNewtonSoftConverter.ToUnixTimeInSecondsFromDateTime(metadata.ConflictResolutionTimestamp));

                writer.WritePropertyName(ChangeFeedMetadataFields.Lsn);
                writer.WriteValue(metadata.Lsn);

                writer.WritePropertyName(ChangeFeedMetadataFields.OperationType);
                serializer.Serialize(writer, metadata.OperationType);

                writer.WritePropertyName(ChangeFeedMetadataFields.PreviousImageLSN);
                writer.WriteValue(metadata.PreviousLsn);

                writer.WritePropertyName(ChangeFeedMetadataFields.TimeToLiveExpired);
                writer.WriteValue(metadata.IsTimeToLiveExpired);

                writer.WritePropertyName(ChangeFeedMetadataFields.Id);
                writer.WriteValue(metadata.Id);
                if (metadata.PartitionKey != null)
                {
                    writer.WritePropertyName(ChangeFeedMetadataFields.PartitionKey);
                    writer.WriteStartObject(); 

                    foreach ((string key, object objectValue) in metadata.PartitionKey)
                    {
                        writer.WritePropertyName(key);

                        if (objectValue == null)
                        {
                            writer.WriteNull();
                        }
                        else
                        {
                            switch (objectValue)
                            {
                                case string stringValue:
                                    writer.WriteValue(stringValue);
                                    break;

                                case long longValue:
                                    writer.WriteValue(longValue);
                                    break;

                                case double doubleValue:
                                    writer.WriteValue(doubleValue);
                                    break;

                                case bool boolValue:
                                    writer.WriteValue(boolValue);
                                    break;

                                default:
                                    throw new JsonSerializationException($"Unexpected value type: {objectValue.GetType()} for PartitionKey property.");
                            }
                        }
                    }

                    writer.WriteEndObject(); // End PartitionKey object
                }

                writer.WriteEndObject();
            }
            else
            {
                throw new JsonSerializationException($"Unexpected value when converting {nameof(ChangeFeedMetadata)}.");
            }
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="JsonReader"/> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing value of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>The deserialized object.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            ChangeFeedMetadata metadata = new ChangeFeedMetadata();
            List<(string, object)> partitionKey = null;

            reader.Read(); // StartObject

            while (reader.TokenType == JsonToken.PropertyName)
            {
                string propertyName = reader.Value.ToString();
                reader.Read(); // Move to property value

                switch (propertyName)
                {
                    case ChangeFeedMetadataFields.ConflictResolutionTimestamp:
                        metadata.ConflictResolutionTimestamp = ChangeFeedMetadataNewtonSoftConverter.ToDateTimeFromUnixTimeInSeconds(Convert.ToInt64(reader.Value));
                        break;

                    case ChangeFeedMetadataFields.Lsn:
                        metadata.Lsn = reader.Value != null ? Convert.ToInt64(reader.Value) : 0;
                        break;

                    case ChangeFeedMetadataFields.OperationType:
                        metadata.OperationType = serializer.Deserialize<ChangeFeedOperationType>(reader);
                        break;

                    case ChangeFeedMetadataFields.PreviousImageLSN:
                        metadata.PreviousLsn = reader.Value != null ? Convert.ToInt64(reader.Value) : 0;
                        break;

                    case ChangeFeedMetadataFields.TimeToLiveExpired:
                        metadata.IsTimeToLiveExpired = reader.Value != null && Convert.ToBoolean(reader.Value);
                        break;

                    case ChangeFeedMetadataFields.Id:
                        metadata.Id = reader.Value?.ToString();
                        break;

                    case ChangeFeedMetadataFields.PartitionKey:
                        if (reader.TokenType == JsonToken.StartObject)
                        {
                            partitionKey ??= new List<(string, object)>();
                            reader.Read(); // Move to the first property in the object
                            while (reader.TokenType == JsonToken.PropertyName)
                            {
                                string key = reader.Value.ToString();
                                reader.Read(); // Move to the value of the property

                                object value = reader.TokenType switch
                                {
                                    JsonToken.String => reader.Value.ToString(),
                                    JsonToken.Integer => Convert.ToInt64(reader.Value),
                                    JsonToken.Float => Convert.ToDouble(reader.Value),
                                    JsonToken.Boolean => Convert.ToBoolean(reader.Value),
                                    JsonToken.Null => null,
                                    _ => throw new JsonSerializationException($"Unexpected token type: {reader.TokenType} for PartitionKey property.")
                                };

                                partitionKey.Add((key, value));
                                reader.Read(); // Move to the next property or EndObject
                            }
                        }
                        break;

                    default:
                        reader.Skip();
                        break;
                }

                reader.Read(); // Move to next property or EndObject
            }

            metadata.PartitionKey = partitionKey;
            return metadata;
        }
        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns><c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.</returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ChangeFeedMetadata);
        }

        private static long ToUnixTimeInSecondsFromDateTime(DateTime date)
        {
            return (long)(date - ChangeFeedMetadataNewtonSoftConverter.UnixEpoch).TotalSeconds;
        }

        private static DateTime ToDateTimeFromUnixTimeInSeconds(long unixTimeInSeconds)
        {
            return ChangeFeedMetadataNewtonSoftConverter.UnixEpoch.AddSeconds(unixTimeInSeconds);
        }
    }
}
