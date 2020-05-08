//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// <see cref="JsonConverter" /> for <see cref="BoundingBox" /> class.
    /// </summary>
    internal sealed class BoundingBoxJsonConverter : JsonConverter
    {
        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="value">The existingValue.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            BoundingBox boundingBox = (BoundingBox)value;
            serializer.Serialize(writer, boundingBox.Min.Coordinates.Concat(boundingBox.Max.Coordinates));
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
            double[] coordinates = serializer.Deserialize<double[]>(reader);
            if (coordinates == null)
            {
                return null;
            }

            if (coordinates.Length % 2 != 0 || coordinates.Length < 4)
            {
                throw new JsonSerializationException(RMResources.SpatialBoundingBoxInvalidCoordinates);
            }

            return new BoundingBox(
                new Position(coordinates.Take(coordinates.Length / 2).ToList()),
                new Position(coordinates.Skip(coordinates.Length / 2).ToList()));
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
            return objectType == typeof(BoundingBox);
        }
    }
}
