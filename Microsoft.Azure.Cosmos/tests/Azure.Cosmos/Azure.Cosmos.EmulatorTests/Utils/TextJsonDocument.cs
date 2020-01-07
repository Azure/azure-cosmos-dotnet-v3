//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;

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

    internal sealed class TextJsonJObjectConverter : JsonConverter<Newtonsoft.Json.Linq.JObject>
    {
        public override Newtonsoft.Json.Linq.JObject Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(json.RootElement.GetRawText());
        }

        public override void Write(
            Utf8JsonWriter writer,
            Newtonsoft.Json.Linq.JObject value,
            JsonSerializerOptions options)
        {
            string text = Newtonsoft.Json.JsonConvert.SerializeObject(value);
            JsonSerializer.Serialize(writer, JsonSerializer.Deserialize<Dictionary<string, object>>(text), options);
        }
    }

    internal sealed class TextJsonJTokenListConverter : JsonConverter<List<object>>
    {
        public override List<object> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<List<object>>(json.RootElement.GetRawText());
        }

        public override void Write(
            Utf8JsonWriter writer,
            List<object> value,
            JsonSerializerOptions options)
        {
            string text = Newtonsoft.Json.JsonConvert.SerializeObject(value);
            JsonSerializer.Serialize(writer, JsonSerializer.Deserialize<Dictionary<string, object>>(text), options);
        }
    }

    internal sealed class TextJsonCosmosElementListConverter : JsonConverter<CosmosFeedResponseUtil<CosmosObject>>
    {
        public override CosmosFeedResponseUtil<CosmosObject> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<CosmosFeedResponseUtil<CosmosObject>>(json.RootElement.GetRawText());
        }

        public override void Write(
            Utf8JsonWriter writer,
            CosmosFeedResponseUtil<CosmosObject> value,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }

    internal sealed class TextJsonCosmosElementConverter : JsonConverter<CosmosElement>
    {
        public override CosmosElement Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<CosmosElement>(json.RootElement.GetRawText());
        }

        public override void Write(
            Utf8JsonWriter writer,
            CosmosElement value,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
