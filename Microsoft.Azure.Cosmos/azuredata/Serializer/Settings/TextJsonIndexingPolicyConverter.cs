//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;

    internal class TextJsonIndexingPolicyConverter : JsonConverter<IndexingPolicy>
    {
        public override IndexingPolicy Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.JsonUnexpectedToken));
            }

            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            return TextJsonIndexingPolicyConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            IndexingPolicy policy,
            JsonSerializerOptions options)
        {
            TextJsonIndexingPolicyConverter.WritePropertyValue(writer, policy, options);
        }

        public static void WritePropertyValue(
            Utf8JsonWriter writer,
            IndexingPolicy policy,
            JsonSerializerOptions options)
        {
            if (policy == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WriteBoolean(Constants.Properties.Automatic, policy.Automatic);

            writer.WriteString(Constants.Properties.IndexingMode, policy.IndexingMode.ToString());

            writer.WritePropertyName(Constants.Properties.IncludedPaths);
            writer.WriteStartArray();
            foreach (IncludedPath includedPath in policy.IncludedPaths)
            {
                TextJsonIncludedPathConverter.WritePropertyValues(writer, includedPath, options);
            }
            writer.WriteEndArray();

            writer.WritePropertyName(Constants.Properties.ExcludedPaths);
            writer.WriteStartArray();
            foreach (ExcludedPath excludedPath in policy.ExcludedPaths)
            {
                TextJsonExcludedPathConverter.WritePropertyValues(writer, excludedPath, options);
            }
            writer.WriteEndArray();

            writer.WritePropertyName(Constants.Properties.CompositeIndexes);
            writer.WriteStartArray();
            foreach (Collection<CompositePath> compositePaths in policy.CompositeIndexes)
            {
                writer.WriteStartArray();
                foreach (CompositePath compositePath in compositePaths)
                {
                    TextJsonCompositePathConverter.WritePropertyValues(writer, compositePath, options);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WritePropertyName(Constants.Properties.SpatialIndexes);
            writer.WriteStartArray();
            foreach (SpatialPath spatialPath in policy.SpatialIndexes)
            {
                TextJsonSpatialPathConverter.WritePropertyValues(writer, spatialPath, options);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        public static IndexingPolicy ReadProperty(JsonElement root)
        {
            IndexingPolicy policy = new IndexingPolicy();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonIndexingPolicyConverter.ReadPropertyValue(policy, property);
            }

            return policy;
        }

        private static void ReadPropertyValue(
            IndexingPolicy policy,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.Automatic))
            {
                policy.Automatic = property.Value.GetBoolean();
            }
            else if (property.NameEquals(Constants.Properties.IndexingMode))
            {
                if (Enum.TryParse(property.Value.GetString(), out IndexingMode indexingMode))
                {
                    policy.IndexingMode = indexingMode;
                }
            }
            else if (property.NameEquals(Constants.Properties.IncludedPaths))
            {
                policy.IncludedPaths = new Collection<IncludedPath>();
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    policy.IncludedPaths.Add(TextJsonIncludedPathConverter.ReadProperty(item));
                }
            }
            else if (property.NameEquals(Constants.Properties.ExcludedPaths))
            {
                policy.ExcludedPaths = new Collection<ExcludedPath>();
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    policy.ExcludedPaths.Add(TextJsonExcludedPathConverter.ReadProperty(item));
                }
            }
            else if (property.NameEquals(Constants.Properties.CompositeIndexes))
            {
                policy.CompositeIndexes = new Collection<Collection<CompositePath>>();
                foreach (JsonElement array in property.Value.EnumerateArray())
                {
                    Collection<CompositePath> itemArray = new Collection<CompositePath>();
                    foreach (JsonElement item in array.EnumerateArray())
                    {
                        itemArray.Add(TextJsonCompositePathConverter.ReadProperty(item));
                    }

                    policy.CompositeIndexes.Add(itemArray);
                }
            }
            else if (property.NameEquals(Constants.Properties.SpatialIndexes))
            {
                policy.SpatialIndexes = new Collection<SpatialPath>();
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    policy.SpatialIndexes.Add(TextJsonSpatialPathConverter.ReadProperty(item));
                }
            }
        }
    }
}
