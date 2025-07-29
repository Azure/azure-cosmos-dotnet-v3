//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Converts a DateTime object to and from JSON.
    /// DateTime is represented as the total number of seconds
    /// that have elapsed since January 1, 1970 (midnight UTC/GMT), 
    /// not counting leap seconds (in ISO 8601: 1970-01-01T00:00:00Z).
    /// </summary>
#if COSMOSCLIENT && !COSMOS_GW_AOT
    internal
#else
    public
#endif
    sealed class UnixDateTimeConverter : JsonConverter<DateTime?>
    {
        private static DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Reads the JSON representation of the DateTime object.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="typeToConvert"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="JsonException"></exception>
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out long seconds))
            {
                throw new JsonException("Expected Unix timestamp in seconds.");
            }

            return UnixStartTime.AddSeconds(seconds);
        }

        /// <summary>
        /// Writes the JSON representation of the DateTime object.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            long seconds = (long)(value.Value.ToUniversalTime() - UnixStartTime).TotalSeconds;
            writer.WriteNumberValue(seconds);
        }
    }
}
