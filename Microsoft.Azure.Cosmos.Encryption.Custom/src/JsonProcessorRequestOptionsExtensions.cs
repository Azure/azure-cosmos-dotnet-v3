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
        /// Resolves and validates the JsonProcessor selection for the given encryption options.
        /// Applies any override from RequestOptions.Properties and validates compatibility with the encryption algorithm.
        /// </summary>
        /// <param name="requestOptions">The request options that may contain a JsonProcessor override.</param>
        /// <param name="encryptionOptions">The encryption options to configure.</param>
        /// <exception cref="NotSupportedException">Thrown when an unsupported combination of algorithm and processor is detected.</exception>
        internal static void ResolveJsonProcessorSelection(this RequestOptions requestOptions, EncryptionOptions encryptionOptions)
        {
#pragma warning disable CS0618 // legacy algorithm still supported
            if (encryptionOptions == null)
            {
                return;
            }

            bool hasOverride = TryReadJsonProcessorOverride(requestOptions, out JsonProcessor overrideProcessor);
            if (hasOverride)
            {
                encryptionOptions.JsonProcessor = overrideProcessor;
            }

            // Normalize unsupported combinations
            if (encryptionOptions.EncryptionAlgorithm == CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized &&
                encryptionOptions.JsonProcessor != JsonProcessor.Newtonsoft)
            {
                throw new NotSupportedException("JsonProcessor.Stream is not supported for AE AES encryption algorithm.");
            }

            SynchronizeJsonProcessorProperty(requestOptions, encryptionOptions.JsonProcessor, hasOverride);
#pragma warning restore CS0618
        }

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
            if (requestOptions?.Properties != null &&
                requestOptions.Properties.TryGetValue(JsonProcessorPropertyBagKey, out object value) && value != null)
            {
                if (value is JsonProcessor enumVal)
                {
                    jsonProcessor = enumVal;
                    return true;
                }
                else if (value is string s && Enum.TryParse<JsonProcessor>(s, true, out JsonProcessor parsed))
                {
                    jsonProcessor = parsed;
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

        private static void SynchronizeJsonProcessorProperty(this RequestOptions requestOptions, JsonProcessor selectedProcessor, bool hasOverride)
        {
            if (requestOptions == null)
            {
                return;
            }

            if (!hasOverride && selectedProcessor == JsonProcessor.Newtonsoft)
            {
                return;
            }

            Dictionary<string, object> properties;
            if (requestOptions.Properties != null)
            {
                properties = new Dictionary<string, object>(capacity: requestOptions.Properties.Count);
                foreach (KeyValuePair<string, object> kvp in requestOptions.Properties)
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                properties = new Dictionary<string, object>();
            }

            properties[JsonProcessorPropertyBagKey] = selectedProcessor;
            requestOptions.Properties = properties;
        }
    }
}
