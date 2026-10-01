//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
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
            JToken encryptedData = metadata[Constants.EncryptedData];
            JToken encryptedPaths = metadata[Constants.EncryptedPaths];

            return Classify(
                GetString(algorithm, out string algorithmValue),
                algorithmValue,
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
            JsonElement encryptedData = default;
            JsonElement encryptedPaths = default;
            bool hasAlgorithm = encryptionMetadata.TryGetProperty(Constants.EncryptionAlgorithm, out algorithm);
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
            EncryptedDataState encryptedDataState = !hasEncryptedData
                ? EncryptedDataState.Missing
                : encryptedData.ValueKind == JsonValueKind.Null
                    ? EncryptedDataState.Null
                    : encryptedData.ValueKind == JsonValueKind.String
                        ? EncryptedDataState.Value
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

            if (string.Equals(
                algorithm,
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                StringComparison.Ordinal))
            {
                return (encryptedDataState == EncryptedDataState.Missing ||
                        encryptedDataState == EncryptedDataState.Null) &&
                    (pathsState == PathsState.Empty || pathsState == PathsState.NonEmpty)
                    ? EncryptionMetadataDisposition.Mde
                    : EncryptionMetadataDisposition.Invalid;
            }

#pragma warning disable CS0618
            if (string.Equals(
                algorithm,
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                StringComparison.Ordinal))
#pragma warning restore CS0618
            {
                return EncryptionMetadataDisposition.Legacy;
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

        private static EncryptedDataState GetEncryptedDataState(JToken value)
        {
            if (value == null)
            {
                return EncryptedDataState.Missing;
            }

            return value.Type switch
            {
                JTokenType.Null => EncryptedDataState.Null,
                JTokenType.String => EncryptedDataState.Value,
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
            Value,
            Invalid,
        }
    }
}
