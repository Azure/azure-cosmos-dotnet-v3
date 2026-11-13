//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Converts a DateTime object to and from JSON.
    /// DateTime is represented as the total number of seconds
    /// that have elapsed since January 1, 1970 (midnight UTC/GMT), 
    /// not counting leap seconds (in ISO 8601: 1970-01-01T00:00:00Z).
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class UnixDateTimeConverter : DateTimeConverterBase
    {
        private static DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Writes the JSON representation of the DateTime object.
        /// </summary>
        /// <param name="writer">The Newtonsoft.Json.JsonWriter to write to.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is DateTime)
            {
                Int64 totalSeconds = (Int64) ((DateTime)value - UnixStartTime).TotalSeconds;
                writer.WriteValue(totalSeconds);
            }
            else
            {
                throw new ArgumentException(RMResources.DateTimeConverterInvalidDateTime, "value");
            }
        }

        /// <summary>
        /// Reads the JSON representation of the DateTime object.
        /// </summary>
        /// <param name="reader">The Newtonsoft.Json.JsonReader to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing value of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>
        /// The DateTime object value.
        /// </returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != Newtonsoft.Json.JsonToken.Integer)
            {
                throw new Exception(RMResources.DateTimeConverterInvalidReaderValue);
            }

            double totalSeconds = 0;

            try
            {
                totalSeconds = Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
            }
            catch
            {
                throw new Exception(RMResources.DateTimeConveterInvalidReaderDoubleValue);
            }

            return UnixStartTime.AddSeconds(totalSeconds);
        }
    }
}
