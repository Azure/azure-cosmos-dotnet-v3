//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Provides extension methods for <see cref="RequestOptions"/> to configure JSON processor selection for encryption operations.
    /// Centralizes handling of the JsonProcessor override communicated via <see cref="RequestOptions.Properties"/>.
    /// </summary>
    internal static class JsonProcessorRequestOptionsExtensions
    {
        /// <summary>
        /// The property bag key used to store the JsonProcessor override in RequestOptions.Properties.
        /// </summary>
        internal const string JsonProcessorPropertyBagKey = "encryption-json-processor";

        /// <summary>
        /// Attempts to read a JsonProcessor override from the RequestOptions.Properties dictionary.
        /// Supports both JsonProcessor enum values and string representations (case-insensitive).
        /// </summary>
        /// <param name="requestOptions">The request options to read from.</param>
        /// <param name="jsonProcessor">When this method returns, contains the JsonProcessor value if found; otherwise, JsonProcessor.Newtonsoft.</param>
        /// <returns><c>true</c> if a valid JsonProcessor override was found; otherwise, <c>false</c>.</returns>
        internal static bool TryReadJsonProcessorOverride(this RequestOptions requestOptions, out JsonProcessor jsonProcessor)
        {
            jsonProcessor = JsonProcessor.Newtonsoft;
            JsonProcessor? typedOverride = requestOptions switch
            {
                EncryptionItemRequestOptions itemOptions => itemOptions.JsonProcessorOverride,
                EncryptionTransactionalBatchItemRequestOptions batchItemOptions => batchItemOptions.JsonProcessorOverride,
                _ => null,
            };
            if (typedOverride.HasValue)
            {
                jsonProcessor = typedOverride.Value;
                return true;
            }

            if (requestOptions?.Properties != null &&
                requestOptions.Properties.TryGetValue(JsonProcessorPropertyBagKey, out object value) && value != null)
            {
                if (value is JsonProcessor enumVal)
                {
                    if (Enum.IsDefined(typeof(JsonProcessor), enumVal))
                    {
                        jsonProcessor = enumVal;
                        StoreInternalOverride(requestOptions, jsonProcessor);
                        return true;
                    }
                }
                else if (value is string s
                    && Enum.TryParse(s, true, out JsonProcessor parsed)
                    && Enum.IsDefined(typeof(JsonProcessor), parsed))
                {
                    jsonProcessor = parsed;
                    StoreInternalOverride(requestOptions, jsonProcessor);
                    return true;
                }
            }

            return false;
        }

        internal static JsonProcessor GetJsonProcessor(this RequestOptions requestOptions, JsonProcessor defaultJsonProcessor = JsonProcessor.Newtonsoft)
        {
            if (requestOptions.TryReadJsonProcessorOverride(out JsonProcessor jsonProcessor))
            {
                return jsonProcessor;
            }

            return defaultJsonProcessor;
        }

        private static void StoreInternalOverride(RequestOptions requestOptions, JsonProcessor jsonProcessor)
        {
            switch (requestOptions)
            {
                case EncryptionItemRequestOptions itemOptions:
                    itemOptions.JsonProcessorOverride = jsonProcessor;
                    break;
                case EncryptionTransactionalBatchItemRequestOptions batchItemOptions:
                    batchItemOptions.JsonProcessorOverride = jsonProcessor;
                    break;
                default:
                    return;
            }

            Dictionary<string, object> properties = new ();
            foreach (KeyValuePair<string, object> property in requestOptions.Properties)
            {
                properties[property.Key] = property.Value;
            }

            properties.Remove(JsonProcessorPropertyBagKey);
            requestOptions.Properties = properties.Count == 0 ? null : properties;
        }
    }
}
