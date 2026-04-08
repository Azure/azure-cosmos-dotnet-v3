// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Schema
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using CosmosContainer = Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// Infers a JSON Schema from a sample of documents in a Cosmos DB container.
    /// </summary>
    public class SchemaInferrer
    {
        private static readonly HashSet<string> SystemProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "_rid", "_self", "_etag", "_attachments", "_ts"
        };

        /// <summary>
        /// Samples documents from a container and produces a merged JSON Schema.
        /// </summary>
        public async Task<InferredSchema> InferSchemaAsync(
            CosmosContainer container,
            int sampleSize = 20,
            CancellationToken cancellationToken = default)
        {
            ContainerProperties containerProps = await container.ReadContainerAsync(cancellationToken: cancellationToken);

            string partitionKeyPath = string.Join(", ", containerProps.PartitionKeyPaths ?? Array.Empty<string>());

            List<JsonElement> samples = new();
            string query = $"SELECT TOP {sampleSize} * FROM c ORDER BY c._ts DESC";

            using FeedIterator<JsonElement> iterator = container.GetItemQueryIterator<JsonElement>(
                new QueryDefinition(query),
                requestOptions: new QueryRequestOptions { MaxItemCount = sampleSize });

            while (iterator.HasMoreResults && samples.Count < sampleSize)
            {
                FeedResponse<JsonElement> response = await iterator.ReadNextAsync(cancellationToken);
                foreach (JsonElement doc in response)
                {
                    samples.Add(doc);
                    if (samples.Count >= sampleSize)
                    {
                        break;
                    }
                }
            }

            if (samples.Count == 0)
            {
                return new InferredSchema
                {
                    PartitionKeyPath = partitionKeyPath,
                    SampleCount = 0,
                    Schema = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>(),
                        ["description"] = "No documents found in container."
                    },
                    UniqueKeyPaths = containerProps.UniqueKeyPolicy?.UniqueKeys?
                        .SelectMany(k => k.Paths)
                        .ToList() ?? new List<string>()
                };
            }

            Dictionary<string, PropertySchema> mergedProperties = new();
            int docCount = samples.Count;

            foreach (JsonElement doc in samples)
            {
                MergeDocument(doc, mergedProperties);
            }

            Dictionary<string, object> schema = BuildSchema(mergedProperties, docCount);

            return new InferredSchema
            {
                PartitionKeyPath = partitionKeyPath,
                SampleCount = docCount,
                Schema = schema,
                UniqueKeyPaths = containerProps.UniqueKeyPolicy?.UniqueKeys?
                    .SelectMany(k => k.Paths)
                    .ToList() ?? new List<string>()
            };
        }

        private static void MergeDocument(JsonElement element, Dictionary<string, PropertySchema> properties)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (JsonProperty prop in element.EnumerateObject())
            {
                if (!properties.TryGetValue(prop.Name, out PropertySchema? schema))
                {
                    schema = new PropertySchema { Name = prop.Name };
                    properties[prop.Name] = schema;
                }

                schema.OccurrenceCount++;
                string jsonType = GetJsonSchemaType(prop.Value);
                schema.Types.Add(jsonType);

                if (SystemProperties.Contains(prop.Name))
                {
                    schema.IsSystemProperty = true;
                }

                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    MergeDocument(prop.Value, schema.NestedProperties);
                }
                else if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                {
                    JsonElement firstItem = prop.Value.EnumerateArray().First();
                    schema.ArrayItemTypes.Add(GetJsonSchemaType(firstItem));

                    if (firstItem.ValueKind == JsonValueKind.Object)
                    {
                        MergeDocument(firstItem, schema.ArrayItemProperties);
                    }
                }
            }
        }

        private static Dictionary<string, object> BuildSchema(
            Dictionary<string, PropertySchema> properties,
            int totalDocs)
        {
            Dictionary<string, object> schema = new()
            {
                ["type"] = "object"
            };

            Dictionary<string, object> props = new();
            List<string> required = new();

            foreach (KeyValuePair<string, PropertySchema> kvp in properties.OrderBy(p => p.Key))
            {
                PropertySchema prop = kvp.Value;
                Dictionary<string, object> propSchema = new();

                List<string> types = prop.Types.Distinct().ToList();
                if (types.Count == 1)
                {
                    propSchema["type"] = types[0];
                }
                else
                {
                    propSchema["type"] = types;
                }

                if (prop.IsSystemProperty)
                {
                    propSchema["x-cosmos-system-property"] = true;
                }

                if (prop.NestedProperties.Count > 0)
                {
                    propSchema["properties"] = BuildNestedProperties(prop.NestedProperties, prop.OccurrenceCount);
                }

                if (prop.ArrayItemTypes.Count > 0)
                {
                    Dictionary<string, object> itemsSchema = new();
                    List<string> itemTypes = prop.ArrayItemTypes.Distinct().ToList();
                    if (itemTypes.Count == 1)
                    {
                        itemsSchema["type"] = itemTypes[0];
                    }
                    else
                    {
                        itemsSchema["type"] = itemTypes;
                    }

                    if (prop.ArrayItemProperties.Count > 0)
                    {
                        itemsSchema["properties"] = BuildNestedProperties(prop.ArrayItemProperties, 1);
                    }

                    propSchema["items"] = itemsSchema;
                }

                if (prop.OccurrenceCount == totalDocs)
                {
                    required.Add(kvp.Key);
                }

                props[kvp.Key] = propSchema;
            }

            schema["properties"] = props;

            if (required.Count > 0)
            {
                schema["required"] = required;
            }

            return schema;
        }

        private static Dictionary<string, object> BuildNestedProperties(
            Dictionary<string, PropertySchema> properties,
            int parentOccurrences)
        {
            Dictionary<string, object> result = new();

            foreach (KeyValuePair<string, PropertySchema> kvp in properties.OrderBy(p => p.Key))
            {
                PropertySchema prop = kvp.Value;
                Dictionary<string, object> propSchema = new();

                List<string> types = prop.Types.Distinct().ToList();
                propSchema["type"] = types.Count == 1 ? (object)types[0] : types;

                if (prop.NestedProperties.Count > 0)
                {
                    propSchema["properties"] = BuildNestedProperties(prop.NestedProperties, prop.OccurrenceCount);
                }

                result[kvp.Key] = propSchema;
            }

            return result;
        }

        private static string GetJsonSchemaType(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => "string",
                JsonValueKind.Number => element.TryGetInt64(out _) ? "integer" : "number",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Array => "array",
                JsonValueKind.Object => "object",
                JsonValueKind.Null => "null",
                _ => "string"
            };
        }

        private class PropertySchema
        {
            public string Name { get; set; } = string.Empty;
            public int OccurrenceCount { get; set; }
            public HashSet<string> Types { get; set; } = new();
            public bool IsSystemProperty { get; set; }
            public Dictionary<string, PropertySchema> NestedProperties { get; set; } = new();
            public HashSet<string> ArrayItemTypes { get; set; } = new();
            public Dictionary<string, PropertySchema> ArrayItemProperties { get; set; } = new();
        }
    }

    /// <summary>
    /// Result of schema inference for a container.
    /// </summary>
    public class InferredSchema
    {
        /// <summary>The container's partition key path.</summary>
        public string PartitionKeyPath { get; set; } = string.Empty;

        /// <summary>Number of documents actually sampled.</summary>
        public int SampleCount { get; set; }

        /// <summary>JSON Schema representation as a dictionary tree.</summary>
        public Dictionary<string, object> Schema { get; set; } = new();

        /// <summary>Unique key paths configured on the container.</summary>
        public List<string> UniqueKeyPaths { get; set; } = new();
    }
}
