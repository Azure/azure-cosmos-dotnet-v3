//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal sealed class TextJsonDocumentConverter : JsonConverter<Microsoft.Azure.Documents.Document>
    {
        public override Microsoft.Azure.Documents.Document Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Microsoft.Azure.Documents.Document>(json.RootElement.GetRawText());
        }

        public override void Write(
            Utf8JsonWriter writer,
            Microsoft.Azure.Documents.Document value,
            JsonSerializerOptions options)
        {
            string text = Newtonsoft.Json.JsonConvert.SerializeObject(value);
            JsonSerializer.Serialize(writer, JsonSerializer.Deserialize<Dictionary<string, object>>(text), options);
        }
    }

    internal sealed class TextJsonJTokenConverter : JsonConverter<Newtonsoft.Json.Linq.JToken>
    {
        public override Newtonsoft.Json.Linq.JToken Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JToken>(json.RootElement.GetRawText());
        }

        public override void Write(
            Utf8JsonWriter writer,
            Newtonsoft.Json.Linq.JToken value,
            JsonSerializerOptions options)
        {
            string text = Newtonsoft.Json.JsonConvert.SerializeObject(value);
            JsonSerializer.Serialize(writer, JsonSerializer.Deserialize<Dictionary<string, object>>(text), options);
        }
    }
}
