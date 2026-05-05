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

    internal partial class StreamProcessor
    {
        private const int MaxBufferSize = 64 * 1024 * 1024;

        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();

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

            using PooledMemoryStream tempOutputStream = new ();
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
                buffer = JsonFeedStreamHelper.HandleLeftOver(buffer, dataLength, leftOver, result.BytesConsumed, MaxBufferSize);

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

        private static async Task OverwriteStreamAsync(Stream stream, Stream source, CancellationToken cancellationToken)
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
            using MemoryStream objectInput = new (objectBytes, 0, length, writable: false);
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

            if (properties.EncryptionFormatVersion != EncryptionFormatVersion.Mde)
            {
                throw new NotSupportedException($"Unknown encryption format version: {properties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            int encryptedPathCount = properties.EncryptedPaths is ICollection<string> ec ? ec.Count : properties.EncryptedPaths.Count();
            using ArrayPoolManager arrayPoolManager = new (initialRentCapacity: (encryptedPathCount * 2) + 4);

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(properties.DataEncryptionKeyId, properties.EncryptionAlgorithm, cancellationToken);

            List<string> pathsDecrypted = new (encryptedPathCount);
            using Utf8JsonWriter writer = new (outputBufferWriter);

            byte[] buffer = arrayPoolManager.Rent(PooledStreamConfiguration.Current.StreamProcessorBufferSize);

            JsonReaderState state = new (JsonReaderOptions);

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

                bytesConsumed = this.TransformDecryptBuffer(buffer.AsSpan(0, dataSize), encryptionKey, pathsDecrypted, writer, ref state, encryptedPathsTable, arrayPoolManager, isFinalBlock, ref isIgnoredBlock, ref decryptPropertyName);

                leftOver = dataSize - (int)bytesConsumed;

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
        }

        private long TransformDecryptBuffer(
            ReadOnlySpan<byte> buffer,
            DataEncryptionKey encryptionKey,
            List<string> pathsDecrypted,
            Utf8JsonWriter writer,
            ref JsonReaderState state,
            (byte[] nameBytes, string fullPath)[] encryptedPathsTable,
            ArrayPoolManager arrayPoolManager,
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
                            this.TransformDecryptProperty(ref reader, encryptionKey, writer, arrayPoolManager);

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

        private void TransformDecryptProperty(ref Utf8JsonReader reader, DataEncryptionKey encryptionKey, Utf8JsonWriter writer, ArrayPoolManager arrayPoolManager)
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
    }
}
#endif