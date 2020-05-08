//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// <see cref="JsonConverter"/> for <see cref="Crs" /> class and all its implementations.
    /// </summary>
    internal sealed class CrsJsonConverter : JsonConverter
    {
        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Crs crs = (Crs)value;
            switch (crs.Type)
            {
                case CrsType.Linked:
                    LinkedCrs linkedCrs = (LinkedCrs)crs;
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("link");
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WritePropertyName("href");
                    writer.WriteValue(linkedCrs.Href);
                    if (linkedCrs.HrefType != null)
                    {
                        writer.WritePropertyName("type");
                        writer.WriteValue(linkedCrs.HrefType);
                    }

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    break;

                case CrsType.Named:
                    NamedCrs namedCrs = (NamedCrs)crs;
                    writer.WriteStartObject();
                    writer.WritePropertyName("type");
                    writer.WriteValue("name");
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WritePropertyName("name");
                    writer.WriteValue(namedCrs.Name);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    break;

                case CrsType.Unspecified:
                    writer.WriteNull();
                    break;
            }
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing value of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>
        /// The object value.
        /// </returns>
        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Null)
            {
                return Crs.Unspecified;
            }

            if (token.Type != JTokenType.Object)
            {
                throw new JsonSerializationException(RMResources.SpatialFailedToDeserializeCrs);
            }

            JToken properties = token["properties"];
            if (properties == null || properties.Type != JTokenType.Object)
            {
                throw new JsonSerializationException(RMResources.SpatialFailedToDeserializeCrs);
            }

            JToken crsType = token["type"];
            if (crsType == null || crsType.Type != JTokenType.String)
            {
                throw new JsonSerializationException(RMResources.SpatialFailedToDeserializeCrs);
            }

            switch (crsType.Value<string>())
            {
                case "name":
                    JToken crsName = properties["name"];
                    if (crsName == null || crsName.Type != JTokenType.String)
                    {
                        throw new JsonSerializationException(RMResources.SpatialFailedToDeserializeCrs);
                    }

                    return new NamedCrs(crsName.Value<string>());

                case "link":
                    JToken crsHref = properties["href"];
                    JToken crsHrefType = properties["type"];

                    if (crsHref == null || crsHref.Type != JTokenType.String || (crsHrefType != null && crsHref.Type != JTokenType.String))
                    {
                        throw new JsonSerializationException(RMResources.SpatialFailedToDeserializeCrs);
                    }

                    return new LinkedCrs(crsHref.Value<string>(), crsHrefType.Value<string>());

                default:
                    throw new JsonSerializationException(RMResources.SpatialFailedToDeserializeCrs);
            }
        }

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>
        /// <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type objectType)
        {
            return typeof(Crs).IsAssignableFrom(objectType);
        }
    }
}
