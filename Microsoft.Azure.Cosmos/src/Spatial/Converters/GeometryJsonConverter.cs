//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters
{
    using System;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Converter for <see cref="Geometry" /> class.
    /// </summary>
    internal sealed class GeometryJsonConverter : JsonConverter
    {
        /// <summary>
        /// Gets a value indicating whether this <see cref="JsonConverter"/> can write JSON.
        /// </summary>
        /// <value><c>true</c> if this <see cref="JsonConverter"/> can write JSON; otherwise, <c>false</c>.</value>
        public override bool CanWrite => false;

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">
        /// The <see cref="T:Newtonsoft.Json.JsonWriter" /> to write to.
        /// </param>
        /// <param name="value">The existingValue.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing existingValue of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>
        /// Deserialized object.
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
                return null;
            }

            if (token.Type != JTokenType.Object)
            {
                throw new JsonSerializationException(RMResources.SpatialInvalidGeometryType);
            }

            JToken typeToken = token["type"];
            if (typeToken == null 
                || typeToken.Type != JTokenType.String)
            {
                throw new JsonSerializationException(RMResources.SpatialInvalidGeometryType);
            }

            Geometry result;
            switch (typeToken.Value<string>())
            {
                case "Point":
                    result = new Point();
                    break;

                case "MultiPoint":
                    result = new MultiPoint();
                    break;

                case "LineString":
                    result = new LineString();
                    break;

                case "MultiLineString":
                    result = new MultiLineString();
                    break;

                case "Polygon":
                    result = new Polygon();
                    break;

                case "MultiPolygon":
                    result = new MultiPolygon();
                    break;

                case "GeometryCollection":
                    result = new GeometryCollection();
                    break;

                default:
                    throw new JsonSerializationException(RMResources.SpatialInvalidGeometryType);
            }

            serializer.Populate(token.CreateReader(), result);
            return result;
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
            return typeof(Geometry) == objectType;
        }
    }
}
