//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // Hack to keep using Document on the tests but support System.Text.Json serialization
    [JsonConverter(typeof(TextJsonDocumentConverter))]
    internal class Document : Microsoft.Azure.Documents.Document
    {
    }

    internal sealed class TextJsonDocumentConverter : JsonConverter<Document>
    {
        public override Document Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            //string text = JsonSerializer.Deserialize(ref reader);
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Document>(json.RootElement.GetRawText());
        }

        public override void Write(
            Utf8JsonWriter writer,
            Document value,
            JsonSerializerOptions options)
        {
            string text = Newtonsoft.Json.JsonConvert.SerializeObject(value);
            JsonSerializer.Serialize(writer, JsonSerializer.Deserialize<Dictionary<string, object>>(text), options);
        }
    }
}
