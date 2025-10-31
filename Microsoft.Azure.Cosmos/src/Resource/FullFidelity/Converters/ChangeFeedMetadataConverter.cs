//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.FullFidelity.Converters
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Resource.FullFidelity;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Converter used to support System.Text.Json de/serialization of type ChangeFeedMetadata/>.
    /// </summary>
    internal class ChangeFeedMetadataConverter : JsonConverter<ChangeFeedMetadata>
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public override ChangeFeedMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.JsonUnexpectedToken));
            }

            JsonElement element = JsonDocument.ParseValue(ref reader).RootElement;

            ChangeFeedMetadata metadata = new ();

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.NameEquals(ChangeFeedMetadataFields.Lsn))
                {
                    metadata.Lsn = property.Value.GetInt64();
                }
                else if (property.NameEquals(ChangeFeedMetadataFields.ConflictResolutionTimestamp))
                {
                    // Read the Unix timestamp and convert to DateTime
                    long unixTimeInSeconds = property.Value.GetInt64();
                    metadata.ConflictResolutionTimestamp = UnixEpoch.AddSeconds(unixTimeInSeconds);
                }
                else if (property.NameEquals(ChangeFeedMetadataFields.OperationType))
                {
                    metadata.OperationType = (ChangeFeedOperationType)Enum.Parse(enumType: typeof(ChangeFeedOperationType), value: property.Value.GetString(), ignoreCase: true);
                }
                else if (property.NameEquals(ChangeFeedMetadataFields.TimeToLiveExpired))
                {
                    metadata.IsTimeToLiveExpired = property.Value.GetBoolean();
                }
                else if (property.NameEquals(ChangeFeedMetadataFields.PreviousImageLSN))
                {
                    metadata.PreviousLsn = property.Value.GetInt64();
                }
                else if (property.NameEquals(ChangeFeedMetadataFields.Id))
                {
                    metadata.Id = property.Value.GetString();
                }
                else if (property.NameEquals(ChangeFeedMetadataFields.PartitionKey))
                {
                    // Dictionary<string, object> is handled by default System.Text.Json deserialization
                    metadata.PartitionKey = JsonSerializer.Deserialize<Dictionary<string, object>>(property.Value.GetRawText(), options);
                }
            }
            
            // validate delete operation requirements
            if (metadata.OperationType == ChangeFeedOperationType.Delete)
            {
                if (metadata.Id == null || metadata.PartitionKey == null)
                {
                    throw new JsonException("Delete operations require both 'id' and 'partitionKey' to be present.");
                }
            }

            return metadata;
        }

        public override void Write(Utf8JsonWriter writer, ChangeFeedMetadata value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WritePropertyName(ChangeFeedMetadataFields.ConflictResolutionTimestamp);
            long unixTimeInSeconds = (long)(value.ConflictResolutionTimestamp - UnixEpoch).TotalSeconds;
            writer.WriteNumberValue(unixTimeInSeconds);

            writer.WriteBoolean(ChangeFeedMetadataFields.TimeToLiveExpired, value.IsTimeToLiveExpired);
            writer.WriteNumber(ChangeFeedMetadataFields.Lsn, value.Lsn);
            writer.WriteString(ChangeFeedMetadataFields.OperationType, value.OperationType.ToString());
            writer.WriteNumber(ChangeFeedMetadataFields.PreviousImageLSN, value.PreviousLsn);

            if (value.Id != null)
            {
                writer.WriteString(ChangeFeedMetadataFields.Id, value.Id);
            }

            if (value.PartitionKey != null)
            {
                // Dictionary<string, object> is handled by default System.Text.Json serialization
                writer.WritePropertyName(ChangeFeedMetadataFields.PartitionKey);
                JsonSerializer.Serialize(writer, value.PartitionKey, options);
            }

            writer.WriteEndObject();
        }
    }
}
