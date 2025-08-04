//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal sealed class CosmosIndexJsonConverter : JsonConverter<Index>
    {
        public override Index Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty(Documents.Constants.Properties.IndexKind, out var indexKindStr))
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, Documents.RMResources.InvalidIndexSpecFormat));

            IndexKind indexKind = IndexKind.Hash;
            if (Enum.TryParse<IndexKind>(indexKindStr.GetString(), out indexKind))
            {
                switch (indexKind)
                {
                    case IndexKind.Hash:
                        return JsonSerializer.Deserialize<HashIndex>(root.GetRawText(), CosmosSerializerContext.Default.HashIndex);
                    case IndexKind.Range:
                        return JsonSerializer.Deserialize<RangeIndex>(root.GetRawText(), CosmosSerializerContext.Default.RangeIndex); ;
                    case IndexKind.Spatial:
                        return JsonSerializer.Deserialize<SpatialIndex>(root.GetRawText(), CosmosSerializerContext.Default.SpatialIndex); ;
                    default:
                        throw new JsonException(
                            string.Format(CultureInfo.CurrentCulture, Documents.RMResources.InvalidIndexKindValue, indexKind));
                }
            }
            else
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, Documents.RMResources.InvalidIndexKindValue, indexKindStr.GetString()));
            }
        }

        public override void Write(Utf8JsonWriter writer, Index value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}