//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Text.Json;

    internal static class Utf8JsonWriterExtension
    {
        public static void WriteRawValue(this Utf8JsonWriter writer, string value)
        {
            using (JsonDocument document = JsonDocument.Parse(value))
            {
                document.RootElement.WriteTo(writer);
            }
        }
    }
}
