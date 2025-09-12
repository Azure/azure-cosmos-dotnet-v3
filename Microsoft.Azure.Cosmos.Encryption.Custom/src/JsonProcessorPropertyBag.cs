//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
    using System;

    /// <summary>
    /// Centralizes handling of the JsonProcessor override communicated via <see cref="RequestOptions.Properties"/>.
    /// This isolates the property bag key and parsing / normalization logic so that EncryptionProcessor remains focused
    /// on encryption/decryption workflows.
    /// </summary>
    internal static class JsonProcessorPropertyBag
    {
        internal const string JsonProcessorPropertyBagKey = "encryption-json-processor";

        internal static void DetermineAndNormalizeJsonProcessor(EncryptionOptions encryptionOptions, RequestOptions requestOptions)
        {
#pragma warning disable CS0618 // legacy algorithm still supported
            if (encryptionOptions == null)
            {
                return;
            }

            if (TryGetJsonProcessorOverride(requestOptions, out JsonProcessor overrideProcessor))
            {
                encryptionOptions.JsonProcessor = overrideProcessor;
            }

            // Normalize unsupported combinations
            if (encryptionOptions.EncryptionAlgorithm == CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized &&
                encryptionOptions.JsonProcessor != JsonProcessor.Newtonsoft)
            {
                throw new NotSupportedException("JsonProcessor.Stream is not supported for AE AES encryption algorithm.");
            }
#pragma warning restore CS0618
        }

        internal static bool TryGetJsonProcessorOverride(RequestOptions requestOptions, out JsonProcessor jsonProcessor)
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
    }
#endif
}
