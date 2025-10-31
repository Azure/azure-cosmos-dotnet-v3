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
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

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
                long unixTimeInSeconds = (long)(metadata.ConflictResolutionTimestamp - UnixEpoch).TotalSeconds;
                writer.WriteValue(unixTimeInSeconds);

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
                    // Dictionary<string, object> is handled by default Newtonsoft.Json serialization
                    writer.WritePropertyName(ChangeFeedMetadataFields.PartitionKey);
                    serializer.Serialize(writer, metadata.PartitionKey);
                }

                writer.WriteEndObject();
            }
            else
            {
                throw new JsonSerializationException($"Unexpected value '{value}' of type '{value?.GetType()}' when converting {nameof(ChangeFeedMetadata)}.");
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

            reader.Read(); // StartObject

            while (reader.TokenType == JsonToken.PropertyName)
            {
                string propertyName = reader.Value.ToString();
                reader.Read(); // Move to property value

                switch (propertyName)
                {
                    case ChangeFeedMetadataFields.ConflictResolutionTimestamp:
                        // Read the Unix timestamp and convert to DateTime
                        if (reader.Value != null)
                        {
                            long unixTimeInSeconds = Convert.ToInt64(reader.Value);
                            metadata.ConflictResolutionTimestamp = UnixEpoch.AddSeconds(unixTimeInSeconds);
                        }
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
                        // Dictionary<string, object> is handled by default Newtonsoft.Json deserialization
                        metadata.PartitionKey = serializer.Deserialize<Dictionary<string, object>>(reader);
                        break;

                    default:
                        reader.Skip();
                        break;
                }

                reader.Read(); // Move to next property or EndObject
            }
            
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
    }
}
