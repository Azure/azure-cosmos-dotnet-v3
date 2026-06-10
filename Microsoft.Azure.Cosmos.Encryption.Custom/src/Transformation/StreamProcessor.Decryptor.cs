// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
    using System.Buffers.Text;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography.Serializers;

    internal partial class StreamProcessor
    {
        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();

        private static readonly JsonReaderOptions JsonReaderOptions = new () { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        internal MdeEncryptor Encryptor { get; set; } = new MdeEncryptor();

        internal async Task<DecryptionContext> DecryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            Encryptor encryptor,
            EncryptionProperties properties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            using RentArrayBufferWriter bufferWriter = new (PooledStreamConfiguration.Current.StreamInitialCapacity);
            DecryptionContext context = await this.DecryptStreamAsync(inputStream, bufferWriter, encryptor, properties, diagnosticsContext, cancellationToken);
            await bufferWriter.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
            outputStream.Position = 0;
            return context;
        }

        internal async Task<DecryptionContext> DecryptStreamAsync(
            Stream inputStream,
            IBufferWriter<byte> outputBufferWriter,
            Encryptor encryptor,
            EncryptionProperties properties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            ArgumentNullException.ThrowIfNull(outputBufferWriter);
            ArgumentNullException.ThrowIfNull(encryptor);
            ArgumentNullException.ThrowIfNull(properties);
            _ = diagnosticsContext;

            if (properties.EncryptionFormatVersion == EncryptionFormatVersion.AeAes)
            {
                throw new NotSupportedException($"Documents encrypted with the legacy encryption algorithm (encryption format version: {EncryptionFormatVersion.AeAes}) are not supported by the Stream JSON processor. Use the Newtonsoft JSON processor to decrypt this document.");
            }

            if (properties.EncryptionFormatVersion != EncryptionFormatVersion.Mde)
            {
                throw new NotSupportedException($"Unknown encryption format version: {properties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            if (!encryptor.ProvidesDataEncryptionKeyAccess())
            {
                throw new NotSupportedException($"JsonProcessor.Stream requires an {nameof(Custom.Encryptor)} implementation that overrides {nameof(Custom.Encryptor.GetEncryptionKeyAsync)}. Use the Newtonsoft JSON processor with this {nameof(Custom.Encryptor)}.");
            }

            int encryptedPathCount = properties.EncryptedPaths is ICollection<string> ec ? ec.Count : properties.EncryptedPaths.Count();
            using ArrayPoolManager arrayPoolManager = new (initialRentCapacity: (encryptedPathCount * 2) + 4);

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(properties.DataEncryptionKeyId, properties.EncryptionAlgorithm, cancellationToken);

            List<string> pathsDecrypted = new (encryptedPathCount);
            using Utf8JsonWriter writer = new (outputBufferWriter);

            byte[] buffer = arrayPoolManager.Rent(PooledStreamConfiguration.Current.StreamProcessorBufferSize);

            JsonReaderState state = new (StreamProcessor.JsonReaderOptions);

            // Pre-encode the encrypted-paths as UTF-8 byte sequences so that we can match them
            // with Utf8JsonReader.ValueTextEquals (which correctly handles JSON escape
            // sequences) without materializing a new string per property name read. The _ei
            // metadata property name is pre-encoded once per StreamProcessor instance in the
            // encryptionPropertiesNameBytes field (declared in StreamProcessor.Encryptor.cs).
            (byte[] nameBytes, string fullPath)[] encryptedPathsTable = BuildEncryptedPathsTable(properties.EncryptedPaths);

            int leftOver = 0;

            bool isFinalBlock = false;
            bool isIgnoredBlock = false;

            string decryptPropertyName = null;

            while (!isFinalBlock)
            {
                int dataLength = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                int dataSize = dataLength + leftOver;
                isFinalBlock = dataLength == 0;
                long bytesConsumed = 0;

                // processing itself here
                bytesConsumed = TransformDecryptBuffer(buffer.AsSpan(0, dataSize));

                leftOver = dataSize - (int)bytesConsumed;

                // we need to scale out buffer
                if (leftOver == dataSize)
                {
                    byte[] newBuffer = arrayPoolManager.Rent(buffer.Length * 2);
                    buffer.AsSpan().CopyTo(newBuffer);
                    buffer = newBuffer;
                }
                else if (leftOver != 0)
                {
                    buffer.AsSpan(dataSize - leftOver, leftOver).CopyTo(buffer);
                }
            }

            writer.Flush();

            return EncryptionProcessor.CreateDecryptionContext(pathsDecrypted, properties.DataEncryptionKeyId);

            long TransformDecryptBuffer(ReadOnlySpan<byte> buffer)
            {
                Utf8JsonReader reader = new (buffer, isFinalBlock, state);

                while (reader.Read())
                {
                    JsonTokenType tokenType = reader.TokenType;

                    if (isIgnoredBlock && reader.CurrentDepth == 1 && tokenType == JsonTokenType.EndObject)
                    {
                        isIgnoredBlock = false;
                        continue;
                    }
                    else if (isIgnoredBlock)
                    {
                        continue;
                    }

                    switch (tokenType)
                    {
                        case JsonTokenType.String:
                            if (decryptPropertyName == null)
                            {
                                WriteStringValueVerbatim(writer, ref reader, arrayPoolManager);
                            }
                            else
                            {
                                TransformDecryptProperty(ref reader);

                                pathsDecrypted.Add(decryptPropertyName);
                            }

                            decryptPropertyName = null;
                            break;
                        case JsonTokenType.Number:
                            decryptPropertyName = null;
                            writer.WriteRawValue(reader.ValueSpan);
                            break;
                        case JsonTokenType.None: // Unreachable: pre-first-Read state
                            decryptPropertyName = null;
                            break;
                        case JsonTokenType.StartObject:
                            decryptPropertyName = null;
                            writer.WriteStartObject();
                            break;
                        case JsonTokenType.EndObject:
                            decryptPropertyName = null;
                            writer.WriteEndObject();
                            break;
                        case JsonTokenType.StartArray:
                            decryptPropertyName = null;
                            writer.WriteStartArray();
                            break;
                        case JsonTokenType.EndArray:
                            decryptPropertyName = null;
                            writer.WriteEndArray();
                            break;
                        case JsonTokenType.PropertyName:
                            // Only top-level (root object) property names participate in
                            // encrypted-path matching and the _ei skip, mirroring the JObject
                            // decryptor which only ever touches root-level properties. A nested
                            // property sharing an encrypted path's name (or named _ei) is plain
                            // user data and must pass through untouched.
                            if (reader.CurrentDepth == 1)
                            {
                                string matchedPath = null;
                                for (int i = 0; i < encryptedPathsTable.Length; i++)
                                {
                                    if (reader.ValueTextEquals(encryptedPathsTable[i].nameBytes))
                                    {
                                        matchedPath = encryptedPathsTable[i].fullPath;
                                        break;
                                    }
                                }

                                if (matchedPath != null)
                                {
                                    decryptPropertyName = matchedPath;
                                }
                                else if (reader.ValueTextEquals(this.encryptionPropertiesNameBytes))
                                {
                                    if (!reader.TrySkip())
                                    {
                                        isIgnoredBlock = true;
                                    }

                                    break;
                                }
                            }

                            WritePropertyNameVerbatim(writer, ref reader, arrayPoolManager);
                            break;
                        case JsonTokenType.Comment: // Skipped via reader options
                            break;
                        case JsonTokenType.True:
                            decryptPropertyName = null;
                            writer.WriteBooleanValue(true);
                            break;
                        case JsonTokenType.False:
                            decryptPropertyName = null;
                            writer.WriteBooleanValue(false);
                            break;
                        case JsonTokenType.Null:
                            decryptPropertyName = null;
                            writer.WriteNullValue();
                            break;
                    }
                }

                state = reader.CurrentState;
                return reader.BytesConsumed;
            }

            static void WriteDoubleValueNewtonsoftStyle(Utf8JsonWriter writer, double value)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    // Non-finite doubles are not valid JSON; let the writer surface the failure
                    // rather than emitting an invalid literal. Reachable only via forged ciphertext.
                    writer.WriteNumberValue(value);
                    return;
                }

                // Match the textual form Newtonsoft.Json (JToken/JsonConvert) produces so a
                // re-encrypt of the decrypted document classifies the value identically
                // (TypeMarker.Double): integral doubles get an explicit ".0" suffix.
                string text = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                if (text.IndexOf('.') < 0 && text.IndexOf('E') < 0 && text.IndexOf('e') < 0)
                {
                    text += ".0";
                }

                writer.WriteRawValue(text, skipInputValidation: true);
            }

            void TransformDecryptProperty(ref Utf8JsonReader reader)
            {
                byte[] cipherTextWithTypeMarker = arrayPoolManager.Rent(reader.ValueSpan.Length);

                // necessary for proper un-escaping
                int initialLength = reader.CopyString(cipherTextWithTypeMarker);

                OperationStatus status = Base64.DecodeFromUtf8InPlace(cipherTextWithTypeMarker.AsSpan(0, initialLength), out int cipherTextLength);
                if (status != OperationStatus.Done)
                {
                    throw new InvalidOperationException($"Base64 decoding failed: {status}");
                }

                (byte[] bytes, int processedBytes) = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextLength, arrayPoolManager);

                ReadOnlySpan<byte> bytesToWrite = bytes.AsSpan(0, processedBytes);
                switch ((TypeMarker)cipherTextWithTypeMarker[0])
                {
                    case TypeMarker.String:
                        writer.WriteStringValue(bytesToWrite);
                        break;
                    case TypeMarker.Long:
                        writer.WriteNumberValue(SqlLongSerializer.Deserialize(bytesToWrite));
                        break;
                    case TypeMarker.Double:
                        WriteDoubleValueNewtonsoftStyle(writer, SqlDoubleSerializer.Deserialize(bytesToWrite));
                        break;
                    case TypeMarker.Boolean:
                        writer.WriteBooleanValue(SqlBoolSerializer.Deserialize(bytesToWrite));
                        break;
                    case TypeMarker.Null: // Produced only if ciphertext was forged or future versions choose to encrypt nulls; current encryptor skips nulls.
                        writer.WriteNullValue();
                        break;
                    default:
                        writer.WriteRawValue(bytesToWrite, true);
                        break;
                }
            }
        }

        /// <summary>
        /// Writes a pass-through property name preserving its semantic value.
        /// <see cref="Utf8JsonReader.ValueSpan"/> holds the raw (still escaped) token text,
        /// which the writer would escape again; when the token contains escape sequences the
        /// unescaped bytes are obtained via <see cref="Utf8JsonReader.CopyString(System.Span{byte})"/> first.
        /// </summary>
        private static void WritePropertyNameVerbatim(Utf8JsonWriter writer, ref Utf8JsonReader reader, ArrayPoolManager arrayPoolManager)
        {
            if (!reader.ValueIsEscaped)
            {
                writer.WritePropertyName(reader.ValueSpan);
                return;
            }

            byte[] buffer = arrayPoolManager.Rent(reader.ValueSpan.Length);
            int length = reader.CopyString(buffer);
            writer.WritePropertyName(buffer.AsSpan(0, length));
        }

        /// <summary>
        /// Writes a pass-through string value preserving its semantic value.
        /// See <see cref="WritePropertyNameVerbatim"/> for the escaping rationale.
        /// </summary>
        private static void WriteStringValueVerbatim(Utf8JsonWriter writer, ref Utf8JsonReader reader, ArrayPoolManager arrayPoolManager)
        {
            if (!reader.ValueIsEscaped)
            {
                writer.WriteStringValue(reader.ValueSpan);
                return;
            }

            byte[] buffer = arrayPoolManager.Rent(reader.ValueSpan.Length);
            int length = reader.CopyString(buffer);
            writer.WriteStringValue(buffer.AsSpan(0, length));
        }
    }
}
#endif