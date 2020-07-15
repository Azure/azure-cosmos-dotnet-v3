//-----------------------------------------------------------------------
// <copyright file="InvalidJsonValueDetector.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
    using Microsoft.Azure.Documents.Tools.QueryOracle.Indexing;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Detects invalid JSON values.
    /// </summary>
    internal static class InvalidJsonValueDetector
    {
        /// <summary>
        /// Detects if a JToken has an invalid JSON value.
        /// </summary>
        /// <param name="json">The JToken to traverse.</param>
        /// <returns>Whether or not the JToken has an invalid value.</returns>
        public static bool HasInvalidJsonValue(JToken json)
        {
            if (json == null)
            {
                return false;
            }

            JsonType jsonType = JsonTypeUtils.JTokenTypeToJsonType(json.Type);
            bool hasInvalidJsonValue;
            switch (jsonType)
            {
                case JsonType.Array:
                    hasInvalidJsonValue = InvalidJsonValueDetector.HasInvalidJsonValue((JArray)json);
                    break;

                case JsonType.Boolean:
                    hasInvalidJsonValue = false;
                    break;

                case JsonType.Null:
                    hasInvalidJsonValue = false;
                    break;

                case JsonType.Number:
                    hasInvalidJsonValue = InvalidJsonValueDetector.HasInvalidJsonValue((double)json);
                    break;

                case JsonType.Object:
                    hasInvalidJsonValue = InvalidJsonValueDetector.HasInvalidJsonValue((JObject)json);
                    break;

                case JsonType.String:
                    hasInvalidJsonValue = false;
                    break;

                default:
                    throw new ArgumentException($"Invalid JSON type: {jsonType}");
            }

            return hasInvalidJsonValue;
        }

        private static bool HasInvalidJsonValue(JArray array)
        {
            if (array == null)
            {
                return false;
            }

            bool hasInvalidValue = false;
            foreach (JToken item in array)
            {
                hasInvalidValue |= InvalidJsonValueDetector.HasInvalidJsonValue(item);
            }

            return hasInvalidValue;
        }

        private static bool HasInvalidJsonValue(JObject obj)
        {
            if (obj == null)
            {
                return false;
            }

            bool hasInvalidValue = false;
            foreach (JProperty property in obj.Properties())
            {
                hasInvalidValue |= InvalidJsonValueDetector.HasInvalidJsonValue(property.Value);
            }

            return hasInvalidValue;
        }

        private static bool HasInvalidJsonValue(double value)
        {
            return
                double.IsInfinity(value) ||
                double.IsNaN(value) ||
                double.IsNegativeInfinity(value) ||
                double.IsPositiveInfinity(value);
        }
    }
}
