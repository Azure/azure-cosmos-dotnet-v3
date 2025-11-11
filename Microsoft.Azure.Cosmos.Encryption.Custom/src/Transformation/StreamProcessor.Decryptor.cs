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
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Data.Encryption.Cryptography.Serializers;
    using Microsoft.IO;

    internal partial class StreamProcessor
    {
        private const string EncryptionPropertiesPath = "/" + Constants.EncryptedInfo;
        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();
        private static readonly RecyclableMemoryStreamManager RecyclableMemoryStreamManager = new ();

        private static readonly JsonReaderOptions JsonReaderOptions = new () { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        private static readonly JsonSerializerOptions JsonSerializerOptions = new () { AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

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
                throw new NotSupportedException("Stream must support read, write, and seek operations for in-place decryption.");
            }

            using RecyclableMemoryStream tempOutputStream = RecyclableMemoryStreamManager.GetStream(nameof(StreamProcessor));
            byte[] buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);

            try
            {
                stream.Position = 0;

                using RentArrayBufferWriter objectBuffer = new (InitialBufferSize);
                using RentArrayBufferWriter decryptedObjectBuffer = new (InitialBufferSize);

                (Dictionary<string, HashSet<string>> aggregatedPaths, byte[] updatedBuffer) = await this.ProcessJsonArrayStreamAsync(
                    stream,
                    encryptor,
                    diagnosticsContext,
                    tempOutputStream,
                    objectBuffer,
                    decryptedObjectBuffer,
                    cancellationToken,
                    buffer).ConfigureAwait(false);

                buffer = updatedBuffer;

                await OverwriteStreamAsync(stream, tempOutputStream, cancellationToken).ConfigureAwait(false);

                return CreateAggregatedContext(aggregatedPaths);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }

        private async Task<(Dictionary<string, HashSet<string>> AggregatedPaths, byte[] Buffer)> ProcessJsonArrayStreamAsync(
            Stream stream,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            Stream tempOutputStream,
            RentArrayBufferWriter objectBuffer,
            RentArrayBufferWriter decryptedObjectBuffer,
            CancellationToken cancellationToken,
            byte[] buffer)
        {
            JsonReaderState readerState = new (JsonReaderOptions);
            JsonArrayTraversalState traversalState = JsonArrayTraversalState.CreateInitial();
            Dictionary<string, HashSet<string>> aggregatedPaths = null;

            bool isFinalBlock = false;
            int leftOver = 0;

            void WriteEnvelopeSegment(ReadOnlySpan<byte> segment)
            {
                tempOutputStream.Write(segment);
            }

            void WriteObjectSegment(ReadOnlySpan<byte> segment)
            {
                Span<byte> destination = objectBuffer.GetSpan(segment.Length);
                segment.CopyTo(destination);
                objectBuffer.Advance(segment.Length);
            }

            while (!isFinalBlock)
            {
                int bytesRead = await stream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken).ConfigureAwait(false);
                int dataLength = leftOver + bytesRead;
                isFinalBlock = bytesRead == 0;

                ProcessResult result = JsonFeedStreamHelper.ProcessChunk(
                    buffer.AsSpan(0, dataLength),
                    isFinalBlock,
                    ref readerState,
                    ref traversalState,
                    WriteEnvelopeSegment,
                    WriteObjectSegment);

                leftOver = dataLength - result.BytesConsumed;
                buffer = JsonFeedStreamHelper.HandleLeftOver(buffer, dataLength, leftOver, result.BytesConsumed);

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

            return (aggregatedPaths, buffer);
        }

        private static async Task OverwriteStreamAsync(Stream stream, MemoryStream source, CancellationToken cancellationToken)
        {
            stream.Position = 0;
            stream.SetLength(0);
            source.Position = 0;
            await source.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
            stream.Position = 0;
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
            using RecyclableMemoryStream objectInput = RecyclableMemoryStreamManager.GetStream(nameof(StreamProcessor));
            objectInput.Write(objectBytes.AsSpan(0, length));
            objectInput.Position = 0;
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

            if (properties.EncryptionFormatVersion != EncryptionFormatVersion.Mde)
            {
                throw new NotSupportedException($"Unknown encryption format version: {properties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
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

                    bytesConsumed = this.TransformDecryptBuffer(buffer.AsSpan(0, dataSize), encryptionKey, pathsDecrypted, writer, ref state, encryptedPaths, isFinalBlock, ref isIgnoredBlock, ref decryptPropertyName);

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
            DataEncryptionKey encryptionKey,
            List<string> pathsDecrypted,
            Utf8JsonWriter writer,
            ref JsonReaderState state,
            HashSet<string> encryptedPaths,
            bool isFinalBlock,
            ref bool isIgnoredBlock,
            ref string decryptPropertyName)
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
                            this.TransformDecryptProperty(ref reader, encryptionKey, writer);

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

        private void TransformDecryptProperty(ref Utf8JsonReader reader, DataEncryptionKey encryptionKey, Utf8JsonWriter writer)
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
                try
                {
                    int processedBytes = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextLength, plainText);

                    ReadOnlySpan<byte> bytesToWrite = plainText.AsSpan(0, processedBytes);
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