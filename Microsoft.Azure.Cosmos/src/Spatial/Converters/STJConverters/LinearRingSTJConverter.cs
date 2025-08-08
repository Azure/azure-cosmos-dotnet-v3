// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;
    /// <summary>
    /// Converter used to support System.Text.Json de/serialization of type LinearRing/>.
    /// </summary>
    internal sealed class LinearRingSTJConverter : JsonConverter<LinearRing>
    {
        public override LinearRing Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }

            IList<Position> positions = JsonSerializer.Deserialize<List<Position>>(ref reader, options);
            return new LinearRing(positions);
        }
        public override void Write(Utf8JsonWriter writer, LinearRing linearRing, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, linearRing.Positions, options);
        }

    }

}
