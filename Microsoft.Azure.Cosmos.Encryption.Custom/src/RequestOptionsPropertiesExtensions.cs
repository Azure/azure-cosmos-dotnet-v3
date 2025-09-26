//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Centralizes handling of the JsonProcessor override communicated via <see cref="RequestOptions.Properties"/>.
    /// This isolates the property bag key and parsing / normalization logic so that EncryptionProcessor remains focused
    /// on encryption/decryption workflows.
    /// </summary>
    internal static class RequestOptionsPropertiesExtensions
    {
        internal const string JsonProcessorPropertyBagKey = "encryption-json-processor";

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
