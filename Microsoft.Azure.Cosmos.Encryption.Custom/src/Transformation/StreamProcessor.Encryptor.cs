// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal partial class StreamProcessor
    {
        private readonly byte[] encryptionPropertiesNameBytes = Encoding.UTF8.GetBytes(Constants.EncryptedInfo);

        internal async Task EncryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CancellationToken cancellationToken)
        {
            List<string> pathsEncrypted = new (encryptionOptions.PathsToEncrypt is ICollection<string> c ? c.Count : 0);

            using ArrayPoolManager arrayPoolManager = new ();

            MdeCryptoOperationAdapter cryptoOperationAdapter = await MdeCryptoOperationAdapter.CreateAsync(
                encryptor,
                encryptionOptions.DataEncryptionKeyId,
                encryptionOptions.EncryptionAlgorithm,
                this.Encryptor,
                cancellationToken).ConfigureAwait(false);
            if (cryptoOperationAdapter.UsesPublicEncryptor)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            using PooledMemoryStream publicFallbackOutput = cryptoOperationAdapter.UsesPublicEncryptor
                ? new PooledMemoryStream()
                : null;
            Stream encryptionOutput = publicFallbackOutput ?? outputStream;

            // Pre-encode the paths-to-encrypt as UTF-8 byte sequences so that we can match
            // against Utf8JsonReader tokens with ValueTextEquals (which correctly handles
            // JSON escape sequences), without allocating a new string per property name.
            // The leading '/' is stripped here since ValueTextEquals compares against the
            // decoded property-name bytes, while the original slash-prefixed path string is
            // preserved for the pathsEncrypted output list.
            (byte[] nameBytes, string fullPath)[] encryptedPathsTable = BuildEncryptedPathsTable(encryptionOptions.PathsToEncrypt);

            using Utf8JsonWriter writer = new (encryptionOutput);

            byte[] buffer = arrayPoolManager.Rent(PooledStreamConfiguration.Current.StreamProcessorBufferSize);

            JsonReaderState state = new (StreamProcessor.JsonReaderOptions);

            int leftOver = 0;

            bool isFinalBlock = false;

            Utf8JsonWriter encryptionPayloadWriter = null;
            string encryptPropertyName = null;
            RentArrayBufferWriter bufferWriter = null;
            bool firstTokenValidated = false;
            Task<MdeCryptoResult> pendingCryptoOperation = null;

            try
            {
                while (!isFinalBlock)
                {
                    int dataLength = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                    int dataSize = dataLength + leftOver;
                    isFinalBlock = dataLength == 0;

                    while (true)
                    {
                        pendingCryptoOperation = null;
                        long bytesConsumed = TransformEncryptBuffer(buffer.AsSpan(0, dataSize));
                        int remaining = dataSize - (int)bytesConsumed;

                        if (pendingCryptoOperation != null)
                        {
                            MdeCryptoResult result = await pendingCryptoOperation.ConfigureAwait(false);
                            WriteEncryptedValue(result);

                            if (remaining == 0)
                            {
                                leftOver = 0;
                                break;
                            }

                            buffer.AsSpan((int)bytesConsumed, remaining).CopyTo(buffer);
                            dataSize = remaining;
                            continue;
                        }

                        leftOver = remaining;
                        buffer = HandleReadBuffer(
                            buffer,
                            dataSize,
                            leftOver,
                            isFinalBlock,
                            arrayPoolManager,
                            JsonFeedStreamHelper.MaximumBufferSize);
                        break;
                    }
                }

                await inputStream.DisposeAsync();
            }
            finally
            {
                if (encryptionPayloadWriter != null)
                {
                    await encryptionPayloadWriter.DisposeAsync();
                }

#pragma warning disable VSTHRD103 // Call async methods when in an async method
                bufferWriter?.Dispose();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
            }

            EncryptionProperties encryptionProperties = new (
                encryptionFormatVersion: EncryptionFormatVersion.Mde,
                encryptionOptions.EncryptionAlgorithm,
                encryptionOptions.DataEncryptionKeyId,
                encryptedData: null,
                pathsEncrypted);

            writer.WritePropertyName(this.encryptionPropertiesNameBytes);
            JsonSerializer.Serialize(writer, encryptionProperties);
            writer.WriteEndObject();

            writer.Flush();

            if (publicFallbackOutput != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (publicFallbackOutput.TryGetBuffer(out ArraySegment<byte> encryptedDocument) &&
                    encryptedDocument.Count > 0)
                {
                    await outputStream.WriteAsync(
                        encryptedDocument.Array.AsMemory(
                            encryptedDocument.Offset,
                            encryptedDocument.Count),
                        CancellationToken.None).ConfigureAwait(false);
                }
            }

            outputStream.Position = 0;

            long TransformEncryptBuffer(ReadOnlySpan<byte> buffer)
            {
                Utf8JsonReader reader = new (buffer, isFinalBlock, state);

                while (reader.Read())
                {
                    JsonTokenType tokenType = reader.TokenType;

                    if (!firstTokenValidated)
                    {
                        // The first non-None token must be StartObject for streaming encryption.
                        if (tokenType == JsonTokenType.StartObject)
                        {
                            firstTokenValidated = true;
                        }
                        else if (tokenType == JsonTokenType.Comment || tokenType == JsonTokenType.None)
                        {
                            continue; // skip and keep waiting for first structural token
                        }
                        else
                        {
                            throw new NotSupportedException("Streaming encryption requires a JSON object root. Root arrays or primitive values are not supported.");
                        }
                    }

                    Utf8JsonWriter currentWriter = encryptionPayloadWriter ?? writer;

                    switch (tokenType)
                    {
                        case JsonTokenType.None: // Unreachable after first Read()
                            break;
                        case JsonTokenType.StartObject:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                bufferWriter = new RentArrayBufferWriter();
                                encryptionPayloadWriter = new Utf8JsonWriter(bufferWriter);
                                encryptionPayloadWriter.WriteStartObject();
                            }
                            else
                            {
                                currentWriter.WriteStartObject();
                            }

                            break;
                        case JsonTokenType.EndObject:
                            if (reader.CurrentDepth == 0)
                            {
                                continue;
                            }

                            currentWriter.WriteEndObject();
                            if (reader.CurrentDepth == 1 && encryptionPayloadWriter != null)
                            {
                                currentWriter.Flush();
                                (byte[] bytes, int length) = bufferWriter.WrittenBuffer;
                                bool encryptionCompleted = TryTransformEncryptPayload(bytes, length, TypeMarker.Object, out MdeCryptoResult result);
                                encryptionPayloadWriter = null;
                                bufferWriter?.Dispose();
                                bufferWriter = null;
                                if (!encryptionCompleted)
                                {
                                    state = reader.CurrentState;
                                    return reader.BytesConsumed;
                                }

                                WriteEncryptedValue(result);
                            }

                            break;
                        case JsonTokenType.StartArray:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                bufferWriter = new RentArrayBufferWriter();
                                encryptionPayloadWriter = new Utf8JsonWriter(bufferWriter);
                                encryptionPayloadWriter.WriteStartArray();
                            }
                            else
                            {
                                currentWriter.WriteStartArray();
                            }

                            break;
                        case JsonTokenType.EndArray:
                            currentWriter.WriteEndArray();
                            if (reader.CurrentDepth == 1 && encryptionPayloadWriter != null)
                            {
                                currentWriter.Flush();
                                (byte[] bytes, int length) = bufferWriter.WrittenBuffer;
                                bool encryptionCompleted = TryTransformEncryptPayload(bytes, length, TypeMarker.Array, out MdeCryptoResult result);
                                encryptionPayloadWriter = null;
                                bufferWriter?.Dispose();
                                bufferWriter = null;
                                if (!encryptionCompleted)
                                {
                                    state = reader.CurrentState;
                                    return reader.BytesConsumed;
                                }

                                WriteEncryptedValue(result);
                            }

                            break;
                        case JsonTokenType.PropertyName:
                            if (reader.CurrentDepth == 1)
                            {
                                // Reject a pre-existing top-level _ei up front. The Newtonsoft
                                // default rejects the same case incidentally (JObject.Add throws
                                // ArgumentException on the duplicate key); we throw a deliberate
                                // InvalidOperationException. Only the reject behavior is contractual
                                // across processors, not the exception type.
                                if (reader.ValueTextEquals(this.encryptionPropertiesNameBytes))
                                {
                                    throw new InvalidOperationException($"The input document already contains a top-level '{Constants.EncryptedInfo}' property, which is reserved for encryption metadata. Encrypting a document that already contains this property is not supported (it would produce a duplicate '{Constants.EncryptedInfo}').");
                                }

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
                                    encryptPropertyName = matchedPath;
                                }
                            }

                            WritePropertyNameVerbatim(currentWriter, ref reader, arrayPoolManager);
                            break;
                        case JsonTokenType.Comment: // Skipped via reader options
                            currentWriter.WriteCommentValue(reader.ValueSpan);
                            break;
                        case JsonTokenType.String:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                byte[] bytes = arrayPoolManager.Rent(reader.ValueSpan.Length);
                                int length = reader.CopyString(bytes);
                                if (!TryTransformEncryptPayload(bytes, length, TypeMarker.String, out MdeCryptoResult result))
                                {
                                    state = reader.CurrentState;
                                    return reader.BytesConsumed;
                                }

                                WriteEncryptedValue(result);
                            }
                            else
                            {
                                WriteStringValueVerbatim(currentWriter, ref reader, arrayPoolManager);
                            }

                            break;
                        case JsonTokenType.Number:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                (TypeMarker typeMarker, byte[] bytes, int length) = SerializeNumber(reader.ValueSpan, arrayPoolManager);
                                if (!TryTransformEncryptPayload(bytes, length, typeMarker, out MdeCryptoResult result))
                                {
                                    state = reader.CurrentState;
                                    return reader.BytesConsumed;
                                }

                                WriteEncryptedValue(result);
                            }
                            else
                            {
                                currentWriter.WriteRawValue(reader.ValueSpan, true);
                            }

                            break;
                        case JsonTokenType.True:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                (byte[] bytes, int length) = Serialize(true, arrayPoolManager);
                                if (!TryTransformEncryptPayload(bytes, length, TypeMarker.Boolean, out MdeCryptoResult result))
                                {
                                    state = reader.CurrentState;
                                    return reader.BytesConsumed;
                                }

                                WriteEncryptedValue(result);
                            }
                            else
                            {
                                currentWriter.WriteBooleanValue(true);
                            }

                            break;
                        case JsonTokenType.False:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                (byte[] bytes, int length) = Serialize(false, arrayPoolManager);
                                if (!TryTransformEncryptPayload(bytes, length, TypeMarker.Boolean, out MdeCryptoResult result))
                                {
                                    state = reader.CurrentState;
                                    return reader.BytesConsumed;
                                }

                                WriteEncryptedValue(result);
                            }
                            else
                            {
                                currentWriter.WriteBooleanValue(false);
                            }

                            break;
                        case JsonTokenType.Null:
                            currentWriter.WriteNullValue();

                            // Only clear the pending encrypt target when we are NOT buffering an
                            // encryption payload. A null nested inside an encrypted object/array
                            // must not wipe the path being captured, otherwise the payload's _ep
                            // entry is lost and the value becomes undecryptable.
                            if (encryptionPayloadWriter == null)
                            {
                                encryptPropertyName = null;
                            }

                            break;
                    }
                }

                state = reader.CurrentState;
                return reader.BytesConsumed;
            }

            bool TryTransformEncryptPayload(
                byte[] payload,
                int payloadSize,
                TypeMarker typeMarker,
                out MdeCryptoResult result)
            {
                return cryptoOperationAdapter.TryEncrypt(
                    typeMarker,
                    payload,
                    payloadSize,
                    arrayPoolManager,
                    out result,
                    out pendingCryptoOperation);
            }

            void WriteEncryptedValue(MdeCryptoResult result)
            {
                writer.WriteBase64StringValue(result.Buffer.AsSpan(0, result.Length));
                pathsEncrypted.Add(encryptPropertyName);
                encryptPropertyName = null;
            }
        }

        private static (byte[] nameBytes, string fullPath)[] BuildEncryptedPathsTable(IEnumerable<string> pathsToEncrypt)
        {
            List<(byte[] nameBytes, string fullPath)> table = pathsToEncrypt is ICollection<string> c
                ? new List<(byte[], string)>(c.Count)
                : new List<(byte[], string)>();
            foreach (string path in pathsToEncrypt)
            {
                if (string.IsNullOrEmpty(path) || path[0] != '/' || path.Length < 2)
                {
                    // Paths are already validated by EncryptionOptions; skip defensively.
                    continue;
                }

                // Strip the leading '/'. The property name bytes are what the JSON reader
                // token surfaces (without the JSON Pointer prefix). The original slash-
                // prefixed string is preserved for the output pathsEncrypted list so the
                // serialized _ei metadata remains byte-identical to the previous
                // implementation.
                ReadOnlySpan<char> nameChars = path.AsSpan(1);
                byte[] nameBytes = new byte[Encoding.UTF8.GetByteCount(nameChars)];
                Encoding.UTF8.GetBytes(nameChars, nameBytes);
                table.Add((nameBytes, path));
            }

            return table.ToArray();
        }

        private static (byte[] buffer, int length) Serialize(bool value, ArrayPoolManager arrayPoolManager)
        {
            int byteCount = StreamProcessor.SqlBoolSerializer.GetSerializedMaxByteCount();
            byte[] buffer = arrayPoolManager.Rent(byteCount);
            int length = StreamProcessor.SqlBoolSerializer.Serialize(value, buffer);

            return (buffer, length);
        }

        private static (TypeMarker typeMarker, byte[] buffer, int length) SerializeNumber(ReadOnlySpan<byte> utf8bytes, ArrayPoolManager arrayPoolManager)
        {
            if (System.Buffers.Text.Utf8Parser.TryParse(utf8bytes, out long longValue, out int consumedLong) && consumedLong == utf8bytes.Length)
            {
                return Serialize(longValue, arrayPoolManager);
            }

            // An integer literal (no '.', 'e' or 'E') that did not fit in Int64 above would be
            // silently coerced to a lossy double below. Reject it (fail-closed) so a value that
            // cannot be round-tripped is never persisted. This matches the Newtonsoft processor,
            // which also rejects out-of-range integers (its ToObject<long>() throws OverflowException).
            // Only the reject behavior is contractual across processors, not the exception type.
            if (utf8bytes.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0)
            {
                throw new InvalidOperationException("Unsupported Number type: integer literal is outside the supported Int64 range.");
            }

            if (System.Buffers.Text.Utf8Parser.TryParse(utf8bytes, out double doubleValue, out int consumedDouble) && consumedDouble == utf8bytes.Length)
            {
                // Reject non-finite numbers to keep JSON contract compatibility
                if (double.IsFinite(doubleValue))
                {
                    return Serialize(doubleValue, arrayPoolManager);
                }
            }

            throw new InvalidOperationException("Unsupported Number type");
        }

        private static (TypeMarker typeMarker, byte[] buffer, int length) Serialize(long value, ArrayPoolManager arrayPoolManager)
        {
            int byteCount = StreamProcessor.SqlLongSerializer.GetSerializedMaxByteCount();
            byte[] buffer = arrayPoolManager.Rent(byteCount);
            int length = StreamProcessor.SqlLongSerializer.Serialize(value, buffer);

            return (TypeMarker.Long, buffer, length);
        }

        private static (TypeMarker typeMarker, byte[] buffer, int length) Serialize(double value, ArrayPoolManager arrayPoolManager)
        {
            int byteCount = StreamProcessor.SqlDoubleSerializer.GetSerializedMaxByteCount();
            byte[] buffer = arrayPoolManager.Rent(byteCount);
            int length = StreamProcessor.SqlDoubleSerializer.Serialize(value, buffer);

            return (TypeMarker.Double, buffer, length);
        }
    }
}
#endif