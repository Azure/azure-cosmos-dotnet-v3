//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Globalization;
    using Newtonsoft.Json.Linq;
#if NET8_0_OR_GREATER
    using System.Text.Json;
#endif

    internal enum EncryptionMetadataDisposition
    {
        None,
        Plaintext,
        Mde,
        Legacy,
        Unsupported,
        Invalid,
    }

    internal static class EncryptionMetadataClassifier
    {
        internal const string InvalidMetadataMessage = "The document contains invalid encryption metadata.";

        public static EncryptionMetadataDisposition Classify(JToken encryptionMetadata)
        {
            if (encryptionMetadata == null)
            {
                return EncryptionMetadataDisposition.None;
            }

            if (encryptionMetadata.Type == JTokenType.Null)
            {
                return EncryptionMetadataDisposition.Plaintext;
            }

            if (encryptionMetadata is not JObject metadata)
            {
                return EncryptionMetadataDisposition.Invalid;
            }

            JToken algorithm = metadata[Constants.EncryptionAlgorithm];
            JToken version = metadata[Constants.EncryptionFormatVersion];
            JToken dekId = metadata[Constants.EncryptionDekId];
            JToken encryptedData = metadata[Constants.EncryptedData];
            JToken encryptedPaths = metadata[Constants.EncryptedPaths];

            return Classify(
                GetString(algorithm, out string algorithmValue),
                algorithmValue,
                GetVersionState(version),
                GetString(dekId, out string dekIdValue),
                dekIdValue,
                GetEncryptedDataState(encryptedData),
                GetPathsState(encryptedPaths));
        }

#if NET8_0_OR_GREATER
        public static EncryptionMetadataDisposition Classify(JsonElement encryptionMetadata)
        {
            if (encryptionMetadata.ValueKind == JsonValueKind.Null)
            {
                return EncryptionMetadataDisposition.Plaintext;
            }

            if (encryptionMetadata.ValueKind != JsonValueKind.Object)
            {
                return EncryptionMetadataDisposition.Invalid;
            }

            JsonElement algorithm = default;
            JsonElement version = default;
            JsonElement dekId = default;
            JsonElement encryptedData = default;
            JsonElement encryptedPaths = default;
            bool hasAlgorithm = encryptionMetadata.TryGetProperty(Constants.EncryptionAlgorithm, out algorithm);
            bool hasVersion = encryptionMetadata.TryGetProperty(Constants.EncryptionFormatVersion, out version);
            bool hasDekId = encryptionMetadata.TryGetProperty(Constants.EncryptionDekId, out dekId);
            bool hasEncryptedData = encryptionMetadata.TryGetProperty(Constants.EncryptedData, out encryptedData);
            bool hasEncryptedPaths = encryptionMetadata.TryGetProperty(Constants.EncryptedPaths, out encryptedPaths);

            ValueState algorithmState = !hasAlgorithm
                ? ValueState.Missing
                : algorithm.ValueKind == JsonValueKind.Null
                    ? ValueState.Null
                    : algorithm.ValueKind == JsonValueKind.String
                        ? ValueState.Value
                        : ValueState.Invalid;
            string algorithmValue = algorithmState == ValueState.Value ? algorithm.GetString() : null;
            ValueState dekIdState = !hasDekId
                ? ValueState.Missing
                : dekId.ValueKind == JsonValueKind.Null
                    ? ValueState.Null
                    : dekId.ValueKind == JsonValueKind.String
                        ? ValueState.Value
                        : ValueState.Invalid;
            string dekIdValue = dekIdState == ValueState.Value ? dekId.GetString() : null;
            EncryptedDataState encryptedDataState = !hasEncryptedData
                ? EncryptedDataState.Missing
                : encryptedData.ValueKind == JsonValueKind.Null
                    ? EncryptedDataState.Null
                    : encryptedData.ValueKind == JsonValueKind.String
                        ? encryptedData.GetString().Length == 0
                            ? EncryptedDataState.Empty
                            : EncryptedDataState.Value
                        : EncryptedDataState.Invalid;
            PathsState pathsState;
            if (!hasEncryptedPaths)
            {
                pathsState = PathsState.Missing;
            }
            else if (encryptedPaths.ValueKind == JsonValueKind.Null)
            {
                pathsState = PathsState.Null;
            }
            else
            {
                pathsState = encryptedPaths.ValueKind == JsonValueKind.Array
                    ? GetPathsState(encryptedPaths)
                    : PathsState.Invalid;
            }

            return Classify(
                algorithmState,
                algorithmValue,
                hasVersion ? GetVersionState(version) : ValueState.Missing,
                dekIdState,
                dekIdValue,
                encryptedDataState,
                pathsState);
        }
#endif

        public static void ThrowIfInvalid(EncryptionMetadataDisposition disposition)
        {
            if (disposition == EncryptionMetadataDisposition.Invalid)
            {
                throw new InvalidOperationException(InvalidMetadataMessage);
            }
        }

        private static EncryptionMetadataDisposition Classify(
            ValueState algorithmState,
            string algorithm,
            ValueState versionState,
            ValueState dekIdState,
            string dekId,
            EncryptedDataState encryptedDataState,
            PathsState pathsState)
        {
            if (encryptedDataState == EncryptedDataState.Invalid ||
                pathsState == PathsState.Invalid)
            {
                return EncryptionMetadataDisposition.Invalid;
            }

            if (algorithmState == ValueState.Missing || algorithmState == ValueState.Null)
            {
                return (encryptedDataState == EncryptedDataState.Missing ||
                        encryptedDataState == EncryptedDataState.Null) &&
                    (pathsState == PathsState.Missing || pathsState == PathsState.Empty)
                    ? EncryptionMetadataDisposition.Plaintext
                    : EncryptionMetadataDisposition.Invalid;
            }

            if (algorithmState != ValueState.Value || string.IsNullOrEmpty(algorithm))
            {
                return EncryptionMetadataDisposition.Invalid;
            }

            bool isMde = string.Equals(
                algorithm,
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                StringComparison.Ordinal);
#pragma warning disable CS0618
            bool isLegacy = string.Equals(
                algorithm,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                StringComparison.Ordinal);
#pragma warning restore CS0618
            if ((isMde || isLegacy) &&
                (versionState != ValueState.Value ||
                 dekIdState != ValueState.Value ||
                 string.IsNullOrWhiteSpace(dekId)))
            {
                return EncryptionMetadataDisposition.Invalid;
            }

            if (isMde)
            {
                return (encryptedDataState == EncryptedDataState.Missing ||
                        encryptedDataState == EncryptedDataState.Null) &&
                    (pathsState == PathsState.Empty || pathsState == PathsState.NonEmpty)
                    ? EncryptionMetadataDisposition.Mde
                    : EncryptionMetadataDisposition.Invalid;
            }

            if (isLegacy)
            {
                // Legacy owns the _ed envelope field, not the encryptor's ciphertext byte format.
                return encryptedDataState == EncryptedDataState.Value
                    ? EncryptionMetadataDisposition.Legacy
                    : EncryptionMetadataDisposition.Invalid;
            }

            return EncryptionMetadataDisposition.Unsupported;
        }

        private static ValueState GetString(JToken value, out string result)
        {
            result = null;
            if (value == null)
            {
                return ValueState.Missing;
            }

            if (value.Type == JTokenType.Null)
            {
                return ValueState.Null;
            }

            if (value.Type != JTokenType.String)
            {
                return ValueState.Invalid;
            }

            result = value.Value<string>();
            return ValueState.Value;
        }

        private static ValueState GetVersionState(JToken value)
        {
            if (value == null)
            {
                return ValueState.Missing;
            }

            if (value.Type == JTokenType.Null)
            {
                return ValueState.Null;
            }

            return (value.Type == JTokenType.Integer || value.Type == JTokenType.String) &&
                int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? ValueState.Value
                : ValueState.Invalid;
        }

        private static EncryptedDataState GetEncryptedDataState(JToken value)
        {
            if (value == null)
            {
                return EncryptedDataState.Missing;
            }

            return value.Type switch
            {
                JTokenType.Null => EncryptedDataState.Null,
                JTokenType.String => value.Value<string>().Length == 0
                    ? EncryptedDataState.Empty
                    : EncryptedDataState.Value,
                _ => EncryptedDataState.Invalid,
            };
        }

        private static PathsState GetPathsState(JToken value)
        {
            if (value == null)
            {
                return PathsState.Missing;
            }

            if (value.Type == JTokenType.Null)
            {
                return PathsState.Null;
            }

            if (value is not JArray paths)
            {
                return PathsState.Invalid;
            }

            foreach (JToken path in paths)
            {
                if (path.Type != JTokenType.String || string.IsNullOrEmpty(path.Value<string>()))
                {
                    return PathsState.Invalid;
                }
            }

            return paths.Count == 0 ? PathsState.Empty : PathsState.NonEmpty;
        }

#if NET8_0_OR_GREATER
        private static ValueState GetVersionState(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Null)
            {
                return ValueState.Null;
            }

            return (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _)) ||
                (value.ValueKind == JsonValueKind.String &&
                 int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                ? ValueState.Value
                : ValueState.Invalid;
        }

        private static PathsState GetPathsState(JsonElement paths)
        {
            int count = 0;
            foreach (JsonElement path in paths.EnumerateArray())
            {
                if (path.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(path.GetString()))
                {
                    return PathsState.Invalid;
                }

                count++;
            }

            return count == 0 ? PathsState.Empty : PathsState.NonEmpty;
        }
#endif

        private enum ValueState
        {
            Missing,
            Null,
            Value,
            Invalid,
        }

        private enum PathsState
        {
            Missing,
            Null,
            Empty,
            NonEmpty,
            Invalid,
        }

        private enum EncryptedDataState
        {
            Missing,
            Null,
            Empty,
            Value,
            Invalid,
        }
    }
}
