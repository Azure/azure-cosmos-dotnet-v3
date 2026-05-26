//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
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
        /// <remarks>
        /// When callers set this key to <c>JsonProcessor.Stream</c> (NET8+ only) or the string
        /// <c>"Stream"</c> they route decrypt operations through the <c>SystemTextJsonStreamAdapter</c>.
        ///
        /// <para><b>Documented observable difference</b>: for malformed inputs the streaming adapter
        /// throws <see cref="System.Text.Json.JsonException"/> where the default
        /// <see cref="JsonProcessor.Newtonsoft"/> adapter would have thrown
        /// <c>Newtonsoft.Json.JsonException</c> or <see cref="System.FormatException"/>. Both paths
        /// reject the same set of inputs; only the exception type differs because the two adapters
        /// fail at different layers. See <c>JsonProcessor.Stream</c> remarks for the full
        /// rationale.</para>
        /// </remarks>
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
            if (requestOptions?.Properties != null &&
                requestOptions.Properties.TryGetValue(JsonProcessorPropertyBagKey, out object value) && value != null)
            {
                if (value is JsonProcessor enumVal)
                {
                    jsonProcessor = enumVal;
                    return true;
                }
                else if (value is string s && Enum.TryParse(s, true, out JsonProcessor parsed))
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
    }
}
