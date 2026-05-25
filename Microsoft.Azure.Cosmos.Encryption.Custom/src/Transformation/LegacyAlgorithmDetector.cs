// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// Lightweight detector that classifies a Cosmos document by its client-encryption algorithm
    /// without allocating a Newtonsoft <c>JObject</c> or a System.Text.Json <c>EncryptionPropertiesWrapper</c>.
    /// Used by <see cref="EncryptionProcessor"/> when the caller has opted into
    /// <see cref="JsonProcessor.Stream"/> to decide whether the document can be routed straight to
    /// <c>MdeEncryptionProcessor.DecryptAsync</c> or must fall back to the legacy Newtonsoft path.
    /// </summary>
    /// <remarks>
    /// The detector scans the top-level object for the <c>_ei</c> (encrypted info) property, then
    /// scans <c>_ei</c>'s direct children for the <c>_ea</c> (encryption algorithm) string and compares it
    /// against the well-known legacy value. Any parsing error or unexpected shape collapses to
    /// <see cref="DetectionResult.Unknown"/> so callers can safely fall through to the existing
    /// robust path.
    /// </remarks>
    internal static class LegacyAlgorithmDetector
    {
        private static readonly byte[] EncryptedInfoPropertyName = System.Text.Encoding.UTF8.GetBytes(Constants.EncryptedInfo);

        private static readonly byte[] EncryptionAlgorithmPropertyName = System.Text.Encoding.UTF8.GetBytes(Constants.EncryptionAlgorithm);

#pragma warning disable CS0618 // Type or member is obsolete
        private static readonly byte[] LegacyAlgorithmName = System.Text.Encoding.UTF8.GetBytes(CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized);
#pragma warning restore CS0618 // Type or member is obsolete

        private static readonly byte[] MdeAlgorithmName = System.Text.Encoding.UTF8.GetBytes(CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);

        internal enum DetectionResult
        {
            /// <summary>The document has no top-level <c>_ei</c> property; it is not a client-encrypted payload.</summary>
            NotEncrypted,

            /// <summary>The document is encrypted with the MDE algorithm (<c>MdeAeadAes256CbcHmac256Randomized</c>).</summary>
            MdeAlgorithm,

            /// <summary>The document is encrypted with the obsolete <c>AEAes256CbcHmacSha256Randomized</c> algorithm.</summary>
            LegacyAlgorithm,

            /// <summary>
            /// The document could not be classified (malformed JSON, non-object root, missing <c>_ea</c>, etc.).
            /// Callers should fall back to the robust path.
            /// </summary>
            Unknown,
        }

        /// <summary>
        /// Scans <paramref name="documentBytes"/> and returns the detected algorithm class.
        /// </summary>
        /// <param name="documentBytes">UTF-8 bytes of the complete JSON document.</param>
        internal static DetectionResult Detect(ReadOnlySpan<byte> documentBytes)
        {
            try
            {
                Utf8JsonReader reader = new (documentBytes, isFinalBlock: true, state: default);

                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                {
                    return DetectionResult.Unknown;
                }

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return DetectionResult.NotEncrypted;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        return DetectionResult.Unknown;
                    }

                    bool isEncryptedInfo = reader.ValueTextEquals(EncryptedInfoPropertyName);

                    if (!reader.Read())
                    {
                        return DetectionResult.Unknown;
                    }

                    if (!isEncryptedInfo)
                    {
                        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                        {
                            reader.Skip();
                        }

                        continue;
                    }

                    if (reader.TokenType != JsonTokenType.StartObject)
                    {
                        return DetectionResult.Unknown;
                    }

                    return DetectAlgorithmInEncryptedInfo(ref reader);
                }

                return DetectionResult.Unknown;
            }
            catch (JsonException)
            {
                return DetectionResult.Unknown;
            }
        }

        private static DetectionResult DetectAlgorithmInEncryptedInfo(ref Utf8JsonReader reader)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return DetectionResult.Unknown;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    return DetectionResult.Unknown;
                }

                bool isEncryptionAlgorithm = reader.ValueTextEquals(EncryptionAlgorithmPropertyName);

                if (!reader.Read())
                {
                    return DetectionResult.Unknown;
                }

                if (isEncryptionAlgorithm)
                {
                    if (reader.TokenType != JsonTokenType.String)
                    {
                        return DetectionResult.Unknown;
                    }

                    if (reader.ValueTextEquals(LegacyAlgorithmName))
                    {
                        return DetectionResult.LegacyAlgorithm;
                    }

                    // Only the well-known MDE algorithm string is routed straight to
                    // MdeEncryptionProcessor. Empty strings, unknown identifiers, and case
                    // variants fall through to the JObject path so behaviour stays byte-for-byte
                    // identical to non-opt-in callers (which tolerate these shapes via the
                    // permissive Newtonsoft deserializer).
                    if (reader.ValueTextEquals(MdeAlgorithmName))
                    {
                        return DetectionResult.MdeAlgorithm;
                    }

                    return DetectionResult.Unknown;
                }

                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                {
                    reader.Skip();
                }
            }

            return DetectionResult.Unknown;
        }
    }
}
#endif
