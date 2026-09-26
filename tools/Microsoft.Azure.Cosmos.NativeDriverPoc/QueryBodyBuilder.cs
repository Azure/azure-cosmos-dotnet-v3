// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeDriverPoc
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Serializes a SQL query + parameter set into the JSON body the
    /// Cosmos REST surface expects for query operations.
    /// </summary>
    /// <remarks>
    /// The wire shape per Cosmos REST is:
    /// <code>
    /// {
    ///   "query": "SELECT * FROM c WHERE c.tag = @tag",
    ///   "parameters": [
    ///     { "name": "@tag", "value": "abc" }
    ///   ]
    /// }
    /// </code>
    /// The body is passed as the borrowed bytes-view on
    /// <c>CosmosOperationRequest.Body</c>. The driver deep-copies before
    /// the submit call returns, so the caller may reuse / drop the buffer
    /// immediately afterwards.
    /// </remarks>
    internal static class QueryBodyBuilder
    {
        public static byte[] Build(
            string queryText,
            IReadOnlyList<(string Name, object? Value)>? parameters = null)
        {
            if (string.IsNullOrEmpty(queryText))
            {
                throw new ArgumentException("Query text must be non-empty.", nameof(queryText));
            }

            using var ms = new MemoryStream(256);
            using (var w = new Utf8JsonWriter(ms))
            {
                w.WriteStartObject();
                w.WriteString("query", queryText);
                w.WritePropertyName("parameters");
                w.WriteStartArray();
                if (parameters is not null)
                {
                    foreach ((string name, object? value) in parameters)
                    {
                        if (string.IsNullOrEmpty(name))
                        {
                            throw new ArgumentException(
                                "Query parameter names must be non-empty.", nameof(parameters));
                        }
                        w.WriteStartObject();
                        w.WriteString("name", name);
                        w.WritePropertyName("value");
                        WriteJsonValue(w, value);
                        w.WriteEndObject();
                    }
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return ms.ToArray();
        }

        private static void WriteJsonValue(Utf8JsonWriter w, object? value)
        {
            switch (value)
            {
                case null:
                    w.WriteNullValue();
                    break;
                case string s:
                    w.WriteStringValue(s);
                    break;
                case bool b:
                    w.WriteBooleanValue(b);
                    break;
                case int i:
                    w.WriteNumberValue(i);
                    break;
                case long l:
                    w.WriteNumberValue(l);
                    break;
                case double d:
                    w.WriteNumberValue(d);
                    break;
                case float f:
                    w.WriteNumberValue(f);
                    break;
                case decimal m:
                    w.WriteNumberValue(m);
                    break;
                case Guid g:
                    w.WriteStringValue(g);
                    break;
                case DateTime dt:
                    w.WriteStringValue(dt);
                    break;
                default:
                    // Last-resort fallback: ToString into a JSON string.
                    // Production code would use JsonSerializer with a typed
                    // contract; for the POC this is sufficient.
                    w.WriteStringValue(value.ToString());
                    break;
            }
        }
    }
}
