//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class ETagConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(ETag).IsAssignableFrom(objectType)
                || typeof(ETag?).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (objectType != typeof(ETag)
                && objectType != typeof(ETag?))
            {
                return null;
            }

            JToken etagToken = JToken.Load(reader);

            if (etagToken.Type == JTokenType.Null)
            {
                return null;
            }

            if (etagToken.Type != JTokenType.String)
            {
                throw new JsonSerializationException(
                    string.Format(CultureInfo.CurrentCulture, RMResources.JsonInvalidStringCharacter));
            }

            return new ETag(etagToken.Value<string>());
        }

        public override bool CanWrite => true;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            ETag valueAsType = (ETag)value;
            if (valueAsType == null)
            {
                return;
            }

            writer.WriteValue(valueAsType.ToString());
        }
    }
}