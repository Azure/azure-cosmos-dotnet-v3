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
    using Microsoft.IO;

    internal partial class StreamProcessor
    {
        private const string EncryptionPropertiesPath = "/" + Constants.EncryptedInfo;
        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();

        private static readonly JsonReaderOptions JsonReaderOptions = new () { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        private static readonly JsonSerializerOptions JsonSerializerOptions = new () { AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };
        private static readonly RecyclableMemoryStreamManager RecyclableMemoryStreamManager = new ();

        internal static int InitialBufferSize { get; set; } = 16384;

        internal MdeEncryptor Encryptor { get; set; } = new MdeEncryptor();

        internal async Task<DecryptionContext> DecryptJsonArrayStreamInPlaceAsync(
            Stream stream,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(encryptor);
            ArgumentNullException.ThrowIfNull(diagnosticsContext);

            if (!stream.CanRead || !stream.CanWrite || !stream.CanSeek)
            {
                throw new ArgumentException("Stream must be readable, writable, and seekable for in-place decryption.", nameof(stream));
            }

            using RecyclableMemoryStream tempOutputStream = RecyclableMemoryStreamManager.GetStream();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);

            try
            {
                stream.Position = 0;

                JsonReaderState readerState = new (JsonReaderOptions);
                JsonArrayTraversalState traversalState = default;
                Dictionary<string, HashSet<string>> aggregatedPaths = null;

                using RentArrayBufferWriter objectBuffer = new (InitialBufferSize);
                using RentArrayBufferWriter decryptedObjectBuffer = new (InitialBufferSize);

                bool isFinalBlock = false;
                int leftOver = 0;

                while (!isFinalBlock)
                {
                    int bytesRead = await stream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken).ConfigureAwait(false);
                    int dataLength = leftOver + bytesRead;
                    isFinalBlock = bytesRead == 0;

                    ProcessResult result = ProcessJsonArrayChunk(
                        buffer.AsSpan(0, dataLength),
                        isFinalBlock,
                        tempOutputStream,
                        objectBuffer,
                        ref readerState,
                        ref traversalState);

                    leftOver = dataLength - result.BytesConsumed;
                    buffer = HandleLeftOver(buffer, dataLength, leftOver, result.BytesConsumed);

                    if (isFinalBlock && leftOver > 0)
                    {
                        isFinalBlock = false;
                    }

                    if (result.ObjectCompleted)
                    {
                        aggregatedPaths = await this.ProcessCapturedObjectAsync(
                            objectBuffer,
                            decryptedObjectBuffer,
                            tempOutputStream,
                            encryptor,
                            diagnosticsContext,
                            cancellationToken,
                            aggregatedPaths).ConfigureAwait(false);
                    }
                }

                ValidateJsonArrayCompletion(traversalState);

                stream.Position = 0;
                stream.SetLength(0);
                tempOutputStream.Position = 0;
                await tempOutputStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
                stream.Position = 0;

                return CreateAggregatedContext(aggregatedPaths);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }

        private static ProcessResult ProcessJsonArrayChunk(
            ReadOnlySpan<byte> dataSegment,
            bool finalBlock,
            Stream outputStream,
            RentArrayBufferWriter objectBuffer,
            ref JsonReaderState readerState,
            ref JsonArrayTraversalState traversalState)
        {
            Utf8JsonReader reader = new (dataSegment, finalBlock, readerState);
            int lastConsumed = 0;

            while (reader.Read())
            {
                int tokenStart = checked((int)reader.TokenStartIndex);
                int tokenEnd = checked((int)reader.BytesConsumed);

                WritePendingSegment(
                    dataSegment,
                    ref lastConsumed,
                    tokenStart,
                    traversalState.CapturingObject,
                    objectBuffer,
                    outputStream);

                JsonTokenType tokenType = reader.TokenType;

                EnsureRootArrayStarted(ref traversalState, tokenType);
                HandleStartObject(ref traversalState, tokenType, reader.CurrentDepth);

                WriteJsonSegment(
                    traversalState.CapturingObject,
                    objectBuffer,
                    outputStream,
                    dataSegment[tokenStart..tokenEnd]);

                if (TryCompleteObject(ref traversalState, tokenType, ref reader, ref readerState))
                {
                    return new ProcessResult(checked((int)reader.BytesConsumed), objectCompleted: true);
                }

                UpdateArrayCompletion(ref traversalState, tokenType, reader.CurrentDepth);

                lastConsumed = tokenEnd;
            }

            readerState = reader.CurrentState;

            return new ProcessResult(checked((int)reader.BytesConsumed), objectCompleted: false);
        }

        private static void WritePendingSegment(
            ReadOnlySpan<byte> dataSegment,
            ref int lastConsumed,
            int tokenStart,
            bool capturingObject,
            RentArrayBufferWriter objectBuffer,
            Stream outputStream)
        {
            if (tokenStart <= lastConsumed)
            {
                return;
            }

            WriteJsonSegment(
                capturingObject,
                objectBuffer,
                outputStream,
                dataSegment[lastConsumed..tokenStart]);
            lastConsumed = tokenStart;
        }

        private static void EnsureRootArrayStarted(ref JsonArrayTraversalState traversalState, JsonTokenType tokenType)
        {
            if (traversalState.RootArrayStarted)
            {
                return;
            }

            if (tokenType == JsonTokenType.StartArray)
            {
                traversalState.RootArrayStarted = true;

                return;
            }

            if (tokenType != JsonTokenType.Comment && tokenType != JsonTokenType.None)
            {
                throw new InvalidOperationException("The JSON payload must be an array of objects.");
            }
        }

        private static void HandleStartObject(ref JsonArrayTraversalState traversalState, JsonTokenType tokenType, int readerDepth)
        {
            if (tokenType != JsonTokenType.StartObject)
            {
                return;
            }

            if (!traversalState.CapturingObject && traversalState.RootArrayStarted && !traversalState.RootArrayCompleted && readerDepth == 1)
            {
                traversalState.CapturingObject = true;
                traversalState.CapturedObjectDepth = 1;
                return;
            }

            if (traversalState.CapturingObject)
            {
                traversalState.CapturedObjectDepth++;
            }
        }

        private static bool TryCompleteObject(
            ref JsonArrayTraversalState traversalState,
            JsonTokenType tokenType,
            ref Utf8JsonReader reader,
            ref JsonReaderState readerState)
        {
            if (!traversalState.CapturingObject || tokenType != JsonTokenType.EndObject)
            {
                return false;
            }

            traversalState.CapturedObjectDepth--;
            if (traversalState.CapturedObjectDepth > 0)
            {
                return false;
            }

            traversalState.CapturingObject = false;
            readerState = reader.CurrentState;

            return true;
        }

        private static void UpdateArrayCompletion(ref JsonArrayTraversalState traversalState, JsonTokenType tokenType, int readerDepth)
        {
            if (traversalState.RootArrayStarted && !traversalState.RootArrayCompleted && tokenType == JsonTokenType.EndArray && readerDepth == 0)
            {
                traversalState.RootArrayCompleted = true;
            }
        }

        private async Task<Dictionary<string, HashSet<string>>> ProcessCapturedObjectAsync(
            RentArrayBufferWriter objectBuffer,
            RentArrayBufferWriter decryptedObjectBuffer,
            Stream outputStream,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken,
            Dictionary<string, HashSet<string>> aggregatedPaths)
        {
            (byte[] objectBytes, int length) = objectBuffer.WrittenBuffer;

            EncryptionProperties encryptionProperties = TryExtractEncryptionProperties(objectBytes, length);
            if (encryptionProperties == null)
            {
                WriteBufferedObject(objectBytes, length, objectBuffer, outputStream);
                return aggregatedPaths;
            }

            DecryptionContext context = await this.DecryptEncryptedObjectAsync(
                objectBytes,
                length,
                objectBuffer,
                decryptedObjectBuffer,
                outputStream,
                encryptor,
                encryptionProperties,
                diagnosticsContext,
                cancellationToken).ConfigureAwait(false);

            UpdateAggregatedPaths(context, ref aggregatedPaths);
            return aggregatedPaths;
        }

        private static byte[] HandleLeftOver(byte[] buffer, int dataLength, int leftOver, int bytesConsumed)
        {
            if (leftOver == dataLength)
            {
                byte[] newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                buffer.AsSpan(0, dataLength).CopyTo(newBuffer);
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                return newBuffer;
            }

            if (leftOver != 0)
            {
                buffer.AsSpan(bytesConsumed, leftOver).CopyTo(buffer);
            }

            return buffer;
        }

        private static void ValidateJsonArrayCompletion(JsonArrayTraversalState traversalState)
        {
            if (traversalState.CapturingObject)
            {
                throw new InvalidOperationException("Input stream ended while reading a JSON object.");
            }

            if (!traversalState.RootArrayStarted || !traversalState.RootArrayCompleted)
            {
                throw new InvalidOperationException("Input stream does not contain a complete JSON array.");
            }
        }

        private static DecryptionContext CreateAggregatedContext(Dictionary<string, HashSet<string>> aggregatedPaths)
        {
            if (aggregatedPaths == null || aggregatedPaths.Count == 0)
            {
                return null;
            }

            List<DecryptionInfo> infos = new (aggregatedPaths.Count);
            foreach ((string dekId, HashSet<string> paths) in aggregatedPaths)
            {
                infos.Add(new DecryptionInfo(new List<string>(paths), dekId));
            }

            return new DecryptionContext(infos);
        }

        private async Task<DecryptionContext> DecryptEncryptedObjectAsync(
            byte[] objectBytes,
            int length,
            RentArrayBufferWriter objectBuffer,
            RentArrayBufferWriter decryptedObjectBuffer,
            Stream outputStream,
            Encryptor encryptor,
            EncryptionProperties encryptionProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            using MemoryStream objectInput = RecyclableMemoryStreamManager.GetStream("StreamProcessor.DecryptEncryptedObjectAsync", objectBytes, 0, length);
            decryptedObjectBuffer.Clear();

            DecryptionContext context = await this.DecryptStreamAsync(
                objectInput,
                decryptedObjectBuffer,
                encryptor,
                encryptionProperties,
                diagnosticsContext,
                cancellationToken).ConfigureAwait(false);

            WriteDecryptedPayload(decryptedObjectBuffer, outputStream);
            decryptedObjectBuffer.Clear();
            objectBuffer.Clear();

            return context;
        }

        private static void WriteBufferedObject(byte[] objectBytes, int length, RentArrayBufferWriter objectBuffer, Stream outputStream)
        {
            outputStream.Write(objectBytes, 0, length);
            objectBuffer.Clear();
        }

        private static void WriteDecryptedPayload(RentArrayBufferWriter decryptedObjectBuffer, Stream outputStream)
        {
            ReadOnlySpan<byte> decryptedSpan = decryptedObjectBuffer.WrittenSpan;
            if (!decryptedSpan.IsEmpty)
            {
                outputStream.Write(decryptedSpan);
            }
        }

        private static void UpdateAggregatedPaths(DecryptionContext context, ref Dictionary<string, HashSet<string>> aggregatedPaths)
        {
            if (context == null)
            {
                return;
            }

            aggregatedPaths ??= new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (DecryptionInfo info in context.DecryptionInfoList)
            {
                if (!aggregatedPaths.TryGetValue(info.DataEncryptionKeyId, out HashSet<string> paths))
                {
                    paths = new HashSet<string>(StringComparer.Ordinal);
                    aggregatedPaths[info.DataEncryptionKeyId] = paths;
                }

                foreach (string path in info.PathsDecrypted)
                {
                    paths.Add(path);
                }
            }
        }

        private readonly struct ProcessResult
        {
            internal ProcessResult(int bytesConsumed, bool objectCompleted)
            {
                this.BytesConsumed = bytesConsumed;
                this.ObjectCompleted = objectCompleted;
            }

            internal int BytesConsumed { get; }

            internal bool ObjectCompleted { get; }
        }

        private struct JsonArrayTraversalState
        {
            internal bool RootArrayStarted;
            internal bool RootArrayCompleted;
            internal bool CapturingObject;
            internal int CapturedObjectDepth;
        }

        private static void WriteJsonSegment(
            bool capturingObject,
            RentArrayBufferWriter objectBuffer,
            Stream outputStream,
            ReadOnlySpan<byte> segment)
        {
            if (segment.IsEmpty)
            {
                return;
            }

            if (capturingObject)
            {
                Span<byte> destination = objectBuffer.GetSpan(segment.Length);
                segment.CopyTo(destination);
                objectBuffer.Advance(segment.Length);
            }
            else
            {
                outputStream.Write(segment);
            }
        }

        private static EncryptionProperties TryExtractEncryptionProperties(byte[] buffer, int length)
        {
            try
            {
                EncryptionPropertiesWrapper wrapper = JsonSerializer.Deserialize<EncryptionPropertiesWrapper>(
                    new ReadOnlySpan<byte>(buffer, 0, length),
                    JsonSerializerOptions);
                return wrapper?.EncryptionProperties;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        internal async Task<DecryptionContext> DecryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            Encryptor encryptor,
            EncryptionProperties properties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(outputStream);

            using Utf8JsonWriter writer = new (outputStream);
            DecryptionContext context = await this.DecryptStreamCoreAsync(
                inputStream,
                writer,
                encryptor,
                properties,
                diagnosticsContext,
                cancellationToken).ConfigureAwait(false);

            if (outputStream.CanSeek)
            {
                outputStream.Position = 0;
            }

            return context;
        }

        internal async Task<DecryptionContext> DecryptStreamAsync(
            Stream inputStream,
            IBufferWriter<byte> outputBuffer,
            Encryptor encryptor,
            EncryptionProperties properties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(outputBuffer);

            using Utf8JsonWriter writer = new (outputBuffer);
            return await this.DecryptStreamCoreAsync(
                inputStream,
                writer,
                encryptor,
                properties,
                diagnosticsContext,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<DecryptionContext> DecryptStreamCoreAsync(
            Stream inputStream,
            Utf8JsonWriter writer,
            Encryptor encryptor,
            EncryptionProperties properties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            _ = diagnosticsContext;

            if (properties.EncryptionFormatVersion != EncryptionFormatVersion.Mde && properties.EncryptionFormatVersion != EncryptionFormatVersion.MdeWithCompression)
            {
                throw new NotSupportedException($"Unknown encryption format version: {properties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            bool containsCompressed = properties.CompressedEncryptedPaths?.Count > 0;

            if (properties.CompressionAlgorithm != CompressionOptions.CompressionAlgorithm.Brotli && containsCompressed)
            {
                throw new NotSupportedException($"Unknown compression algorithm {properties.CompressionAlgorithm}");
            }

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(properties.DataEncryptionKeyId, properties.EncryptionAlgorithm, cancellationToken);

            List<string> pathsDecrypted = new (properties.EncryptedPaths.Count());
            byte[] buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);

            try
            {
                JsonReaderState state = new (JsonReaderOptions);

                HashSet<string> encryptedPaths = properties.EncryptedPaths as HashSet<string> ?? new (properties.EncryptedPaths, StringComparer.Ordinal);

                int leftOver = 0;

                bool isFinalBlock = false;
                bool isIgnoredBlock = false;

                string decryptPropertyName = null;

                while (!isFinalBlock)
                {
                    int dataLength = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                    int dataSize = dataLength + leftOver;
                    isFinalBlock = dataSize == 0;
                    long bytesConsumed = 0;

                    bytesConsumed = this.TransformDecryptBuffer(buffer.AsSpan(0, dataSize), containsCompressed, encryptionKey, pathsDecrypted, writer, ref state, encryptedPaths, isFinalBlock, ref isIgnoredBlock, ref decryptPropertyName, properties);

                    leftOver = dataSize - (int)bytesConsumed;

                    if (leftOver == dataSize)
                    {
                        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                        buffer.AsSpan().CopyTo(newBuffer);
                        ArrayPool<byte>.Shared.Return(buffer, true);
                        buffer = newBuffer;
                    }
                    else if (leftOver != 0)
                    {
                        buffer.AsSpan(dataSize - leftOver, leftOver).CopyTo(buffer);
                    }
                }

                writer.Flush();

                return EncryptionProcessor.CreateDecryptionContext(pathsDecrypted, properties.DataEncryptionKeyId);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, true);
            }
        }

        private long TransformDecryptBuffer(
            ReadOnlySpan<byte> buffer,
            bool containsCompressed,
            DataEncryptionKey encryptionKey,
            List<string> pathsDecrypted,
            Utf8JsonWriter writer,
            ref JsonReaderState state,
            HashSet<string> encryptedPaths,
            bool isFinalBlock,
            ref bool isIgnoredBlock,
            ref string decryptPropertyName,
            EncryptionProperties encryptionProperties)
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
                            writer.WriteStringValue(reader.ValueSpan);
                        }
                        else
                        {
                            this.TransformDecryptProperty(ref reader, encryptionProperties, containsCompressed, encryptionKey, writer, decryptPropertyName);

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
                        string propertyName = "/" + reader.GetString();
                        if (encryptedPaths.Contains(propertyName))
                        {
                            decryptPropertyName = propertyName;
                        }
                        else if (propertyName == EncryptionPropertiesPath)
                        {
                            if (!reader.TrySkip())
                            {
                                isIgnoredBlock = true;
                            }

                            break;
                        }

                        writer.WritePropertyName(reader.ValueSpan);
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

        private void TransformDecryptProperty(ref Utf8JsonReader reader, EncryptionProperties properties, bool containsCompressed, DataEncryptionKey encryptionKey, Utf8JsonWriter writer, string decryptPropertyName)
        {
            byte[] cipherTextWithTypeMarker = ArrayPool<byte>.Shared.Rent(reader.ValueSpan.Length);

            try
            {
                // necessary for proper un-escaping
                int initialLength = reader.CopyString(cipherTextWithTypeMarker);

                OperationStatus status = Base64.DecodeFromUtf8InPlace(cipherTextWithTypeMarker.AsSpan(0, initialLength), out int cipherTextLength);
                if (status != OperationStatus.Done)
                {
                    throw new InvalidOperationException($"Base64 decoding failed: {status}");
                }

                int expectedLength = 1 + this.Encryptor.GetDecryptedByteCount(encryptionKey, cipherTextLength);
                byte[] plainText = ArrayPool<byte>.Shared.Rent(expectedLength);
                byte[] decompressedPlaintext = null;
                BrotliCompressor decompressor = null;
                try
                {
                    int processedBytes = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextLength, plainText);

                    if (containsCompressed && properties.CompressedEncryptedPaths.TryGetValue(decryptPropertyName, out int decompressedSize))
                    {
                        decompressor ??= new BrotliCompressor();
                        decompressedPlaintext = ArrayPool<byte>.Shared.Rent(decompressedSize);
                        processedBytes = decompressor.Decompress(plainText, processedBytes, decompressedPlaintext);
                    }

                    ReadOnlySpan<byte> bytesToWrite = (decompressedPlaintext ?? plainText).AsSpan(0, processedBytes);
                    switch ((TypeMarker)cipherTextWithTypeMarker[0])
                    {
                        case TypeMarker.String:
                            writer.WriteStringValue(bytesToWrite);
                            break;
                        case TypeMarker.Long:
                            writer.WriteNumberValue(SqlLongSerializer.Deserialize(bytesToWrite));
                            break;
                        case TypeMarker.Double:
                            writer.WriteNumberValue(SqlDoubleSerializer.Deserialize(bytesToWrite));
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
                finally
                {
                    if (decompressedPlaintext != null)
                    {
                        ArrayPool<byte>.Shared.Return(decompressedPlaintext, true);
                    }

                    decompressor?.Dispose();

                    ArrayPool<byte>.Shared.Return(plainText, true);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(cipherTextWithTypeMarker, true);
            }
        }
    }
}
#endif