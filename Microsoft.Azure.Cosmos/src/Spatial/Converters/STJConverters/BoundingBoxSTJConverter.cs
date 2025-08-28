// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;
    /// <summary>
    /// Converter used to support System.Text.Json de/serialization of type BoundingBox/>.
    /// </summary>
    internal sealed class BoundingBoxSTJConverter : JsonConverter<BoundingBox>
    {
        public override BoundingBox Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            double[] coordinates = JsonSerializer.Deserialize<double[]>(ref reader, options);
            if (coordinates == null)
            {
                return null;
            }

            if (coordinates.Length % 2 != 0 || coordinates.Length < 4)
            {
                throw new JsonException(RMResources.SpatialBoundingBoxInvalidCoordinates);
            }

            return new BoundingBox(
                new Position(coordinates.Take(coordinates.Length / 2).ToList()),
                new Position(coordinates.Skip(coordinates.Length / 2).ToList()));
        }

        public override void Write(Utf8JsonWriter writer, BoundingBox box, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (double coordinate in box.Min.Coordinates.Concat(box.Max.Coordinates))
            {
                // Check if the number is effectively an integer.
                if (coordinate == Math.Truncate(coordinate))
                {
                    // If so, write it with one decimal place to match Newtonsoft's [x.0] format.
                    writer.WriteRawValue(coordinate.ToString("0.0", CultureInfo.InvariantCulture));
                }
                else
                {
                    writer.WriteRawValue(coordinate.ToString("R", CultureInfo.InvariantCulture));
                }
            }
            writer.WriteEndArray();
        }
    }
}
