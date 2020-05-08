//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial
{
    using System.Text.Json;

    /// <summary>
    /// Pre-encode all strings used on serialization to improve performance.
    /// </summary>
    internal static class JsonEncodedStrings
    {
        public readonly static JsonEncodedText Name = JsonEncodedText.Encode("name");
        public readonly static JsonEncodedText Type = JsonEncodedText.Encode("type");
        public readonly static JsonEncodedText Properties = JsonEncodedText.Encode("properties");
        public readonly static JsonEncodedText Href = JsonEncodedText.Encode("href");
        public readonly static JsonEncodedText Crs = JsonEncodedText.Encode("crs");
        public readonly static JsonEncodedText BoundingBox = JsonEncodedText.Encode("bbox");
        public readonly static JsonEncodedText Coordinates = JsonEncodedText.Encode("coordinates");
        public readonly static JsonEncodedText Geometries = JsonEncodedText.Encode("geometries");
        public readonly static JsonEncodedText Valid = JsonEncodedText.Encode("valid");
        public readonly static JsonEncodedText Reason = JsonEncodedText.Encode("reason");
    }
}
