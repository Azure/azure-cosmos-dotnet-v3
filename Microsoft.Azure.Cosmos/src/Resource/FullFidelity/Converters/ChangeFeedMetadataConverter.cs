//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.FullFidelity.Converters
{
    using System;
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
        private readonly static DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

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
                    metadata.ConflictResolutionTimestamp = ChangeFeedMetadataConverter.ToDateTimeFromUnixTimeInSeconds(property.Value.GetInt64());
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

            writer.WriteNumber(ChangeFeedMetadataFields.ConflictResolutionTimestamp, ChangeFeedMetadataConverter.ToUnixTimeInSecondsFromDateTime(value.ConflictResolutionTimestamp));
            writer.WriteBoolean(ChangeFeedMetadataFields.TimeToLiveExpired, value.IsTimeToLiveExpired);
            writer.WriteNumber(ChangeFeedMetadataFields.Lsn, value.Lsn);
            writer.WriteString(ChangeFeedMetadataFields.OperationType, value.OperationType.ToString());
            writer.WriteNumber(ChangeFeedMetadataFields.PreviousImageLSN, value.PreviousLsn);

            writer.WriteEndObject();
        }

        private static long ToUnixTimeInSecondsFromDateTime(DateTime date)
        {
            return (long)(date - ChangeFeedMetadataConverter.UnixEpoch).TotalSeconds;
        }

        private static DateTime ToDateTimeFromUnixTimeInSeconds(long unixTimeInSeconds)
        {
            return ChangeFeedMetadataConverter.UnixEpoch.AddSeconds(unixTimeInSeconds);
        }
    }
}
