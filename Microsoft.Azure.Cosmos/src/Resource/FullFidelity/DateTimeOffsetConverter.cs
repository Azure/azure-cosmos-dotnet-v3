//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// DateTimeOffsetConverter
    /// </summary>
    public class DateTimeOffsetConverter : JsonConverter
    {
        /// <summary>
        /// Convert.
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns>bool</returns>
        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// ReadJson.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <param name="existingValue"></param>
        /// <param name="serializer"></param>
        /// <returns>object</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return DateTimeOffset.FromUnixTimeSeconds((long)reader.Value);
        }

        /// <summary>
        /// WriteJson.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="serializer"></param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanWrite is false. This is readonly.");
        }

        /// <summary>
        /// CanWrite.
        /// </summary>
        public override bool CanWrite => false;
    }
}
