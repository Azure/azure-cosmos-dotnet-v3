//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System.Collections.Generic;
    using System.Text.Json;

    public static class SystemTextJsonUtils
    {
        public static T ToObject<T>(this JsonElement element)
        {
            string json = element.GetRawText();
            return JsonSerializer.Deserialize<T>(json);
        }

        public static T ToObject<T>(this JsonDocument document)
        {
            string json = document.RootElement.GetRawText();
            return JsonSerializer.Deserialize<T>(json);
        }

        public static string ParseStringProperty(Dictionary<string, object> document, string propertyName)
        {
            if (document[propertyName] is JsonElement)
            {
                return ((JsonElement)document[propertyName]).GetString();
            }

            return (string)document[propertyName];
        }

        public static int ParseIntProperty(Dictionary<string, object> document, string propertyName)
        {
            if (document[propertyName] is JsonElement)
            {
                return ((JsonElement)document[propertyName]).GetInt32();
            }

            return (int)document[propertyName];
        }

        public static double ParseDoubleProperty(Dictionary<string, object> document, string propertyName)
        {
            if (document[propertyName] is JsonElement)
            {
                return ((JsonElement)document[propertyName]).GetDouble();
            }

            return (double)document[propertyName];
        }

        public static JsonElement ToJsonElement(Dictionary<string, object> document)
        {
            JsonDocument jsonDocument = JsonDocument.Parse(JsonSerializer.Serialize(document));
            return jsonDocument.RootElement;
        }
    }
}
