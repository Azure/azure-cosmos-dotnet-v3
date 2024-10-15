// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.SystemTextJson
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class JsonBytesConverter : JsonConverter<JsonBytes>
    {
        public override JsonBytes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, JsonBytes value, JsonSerializerOptions options)
        {
            writer.WriteBase64StringValue(value.Bytes.AsSpan(value.Offset, value.Length));
        }
    }
}
#endif