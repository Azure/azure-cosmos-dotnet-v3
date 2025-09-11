// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    // Streaming encryptor: selective path encryption, fully streaming (Utf8JsonReader -> Utf8JsonWriter), emits _ei metadata at root end.
    internal partial class StreamProcessor
    {
        private static readonly byte[] EncryptionPropertiesNameBytes = Encoding.UTF8.GetBytes(Constants.EncryptedInfo);

        internal static void WriteEncryptionInfo(
            Utf8JsonWriter writer,
            int formatVersion,
            string encryptionAlgorithm,
            string dataEncryptionKeyId,
            IReadOnlyCollection<string> encryptedPaths,
            byte[] encryptedData)
        {
            EncryptionProperties props = new EncryptionProperties(
                encryptionFormatVersion: formatVersion,
                encryptionAlgorithm: encryptionAlgorithm,
                dataEncryptionKeyId: dataEncryptionKeyId,
                encryptedData: encryptedData,
                encryptedPaths: encryptedPaths);

            // Write property name then raw JSON value for _ei.
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
            };

            byte[] json = JsonSerializer.SerializeToUtf8Bytes(props, options);
            writer.WritePropertyName(EncryptionPropertiesNameBytes);
            writer.WriteRawValue(json, skipInputValidation: true);
        }

        internal async Task EncryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            ArgumentNullException.ThrowIfNull(outputStream);
            ArgumentNullException.ThrowIfNull(encryptor);
            ArgumentNullException.ThrowIfNull(encryptionOptions);
            if (!inputStream.CanRead)
            {
                throw new ArgumentException("Input stream must be readable.", nameof(inputStream));
            }

            if (!outputStream.CanWrite)
            {
                throw new ArgumentException("Output stream must be writable.", nameof(outputStream));
            }

            long bytesRead = 0;
            long bytesWritten = 0;
            long startTimestamp = Stopwatch.GetTimestamp();

            int pathsCapacity = 0;
            if (encryptionOptions.PathsToEncrypt is ICollection<string> coll)
            {
                pathsCapacity = coll.Count;
            }

            using ArrayPoolManager arrayPoolManager = new ArrayPoolManager();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(
                encryptionOptions.DataEncryptionKeyId,
                encryptionOptions.EncryptionAlgorithm,
                cancellationToken).ConfigureAwait(false);

            CandidatePaths candidatePaths = CandidatePaths.Build(encryptionOptions.PathsToEncrypt);

            // Always use MDE format version
            int formatVersion = EncryptionFormatVersion.Mde;

            using Utf8JsonWriter writer = new Utf8JsonWriter(outputStream, StreamProcessor.JsonWriterOptions);

            // Streaming chunk buffer (grown only when a single token spans it below).
            byte[] buffer = arrayPoolManager.Rent(this.initialBufferSize);
            int leftOver = 0;
            JsonReaderState readerState = new JsonReaderState(StreamProcessor.JsonReaderOptions);

            EncryptionPipelineState pipelineState = new EncryptionPipelineState(
                writer,
                encryptionKey,
                encryptionOptions,
                candidatePaths,
                formatVersion,
                pathsCapacity,
                arrayPoolManager,
                readerState);

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int read = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken).ConfigureAwait(false);
                    int dataSize = read + leftOver;
                    bytesRead += read;

                    bool isFinalBlock = read == 0;
                    long consumed = ProcessBufferChunk(ref pipelineState, buffer.AsSpan(0, dataSize), isFinalBlock);

                    leftOver = dataSize - (int)consumed;

                    if (isFinalBlock)
                    {
                        break;
                    }

                    // Grow buffer if a token spans the whole buffer; enforce max size.
                    if (dataSize > 0 && leftOver == dataSize)
                    {
                        // Entire buffer is a partial token: grow (capped) so the reader can finish that token.
                        int target = Math.Max(buffer.Length * 2, leftOver + BufferGrowthMinIncrement);
                        int capped = Math.Min(MaxBufferSizeBytes, target);
                        if (buffer.Length >= capped)
                        {
                            throw new InvalidOperationException($"JSON token exceeds maximum supported size of {MaxBufferSizeBytes} bytes.");
                        }

                        byte[] oldBuffer = buffer;
                        byte[] newBuffer = arrayPoolManager.Rent(capped);
                        oldBuffer.AsSpan(0, dataSize).CopyTo(newBuffer);
                        buffer = newBuffer;
                        arrayPoolManager.Return(oldBuffer);
                    }
                    else if (leftOver != 0)
                    {
                        buffer.AsSpan(dataSize - leftOver, leftOver).CopyTo(buffer);
                    }
                }

                writer.Flush();
                bytesWritten = writer.BytesCommitted;

                if (outputStream.CanSeek)
                {
                    outputStream.Position = 0;
                }
            }
            finally
            {
                arrayPoolManager.Return(buffer);
                pipelineState.Dispose();
            }

            diagnosticsContext?.SetMetric("encrypt.bytesRead", bytesRead);
            diagnosticsContext?.SetMetric("encrypt.bytesWritten", bytesWritten);
            diagnosticsContext?.SetMetric("encrypt.propertiesEncrypted", pipelineState.PropertiesEncrypted);

#if NET8_0_OR_GREATER
            long elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
#else
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            long elapsedMs = (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
#endif
            diagnosticsContext?.SetMetric("encrypt.elapsedMs", elapsedMs);
        }

        private static long ProcessBufferChunk(ref EncryptionPipelineState s, ReadOnlySpan<byte> span, bool isFinalBlock)
        {
            // Resume incremental reader with preserved state; bytes consumed returned so caller can keep leftovers.
            Utf8JsonReader reader = new Utf8JsonReader(span, isFinalBlock, s.ReaderState);
            while (reader.Read())
            {
                DispatchToken(ref s, ref reader);
            }

            s.ReaderState = reader.CurrentState;
            return reader.BytesConsumed;
        }

        private static Utf8JsonWriter TargetWriter(EncryptionPipelineState s)
        {
            return s.CurrentPayloadWriter ?? s.Writer;
        }

        private static void DispatchToken(ref EncryptionPipelineState s, ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    HandleStartObject(ref s);
                    break;
                case JsonTokenType.EndObject:
                    HandleEndObject(ref s, ref reader);
                    break;
                case JsonTokenType.StartArray:
                    HandleStartArray(ref s);
                    break;
                case JsonTokenType.EndArray:
                    HandleEndArray(ref s);
                    break;
                case JsonTokenType.PropertyName:
                    HandlePropertyName(ref s, ref reader);
                    break;
                case JsonTokenType.String:
                    HandleString(ref s, ref reader);
                    break;
                case JsonTokenType.Number:
                    HandleNumber(ref s, ref reader);
                    break;
                case JsonTokenType.True:
                    HandleBool(ref s, true);
                    break;
                case JsonTokenType.False:
                    HandleBool(ref s, false);
                    break;
                case JsonTokenType.Null:
                    HandleNull(ref s);
                    break;
            }
        }

        private static void BeginBufferingContainer(ref EncryptionPipelineState s, bool isArray, string path)
        {
            // Buffer full container (object/array) so we can encrypt it as a single payload instead of per element.
            s.PayloadBuffer.Clear();
            s.PayloadWriter.Reset(s.PayloadBuffer);
            if (isArray)
            {
                s.PayloadWriter.WriteStartArray();
            }
            else
            {
                s.PayloadWriter.WriteStartObject();
            }

            s.CurrentPayloadWriter = s.PayloadWriter;
            s.BufferingPath = path;
            s.BufferedDepth = 1;
        }

        private static void EndBufferingContainer(ref EncryptionPipelineState s, TypeMarker marker)
        {
            s.CurrentPayloadWriter.Flush();
            (byte[] bytes, int len) = s.PayloadBuffer.WrittenBuffer;
            (byte[] encBytes, int encLen) = EncryptPayload(ref s, bytes, len, marker, s.BufferingPath);
            s.Writer.WriteBase64StringValue(encBytes.AsSpan(0, encLen));
            s.Pool.Return(encBytes);
            s.PropertiesEncrypted++;
            s.PendingEncryptedPath = null;
            s.BufferingPath = null;
            s.CurrentPayloadWriter = null;
            s.BufferedDepth = 0;
            s.PayloadBuffer.Clear();
        }

        private static void EncryptAndWritePrimitive(ref EncryptionPipelineState s, TypeMarker marker, byte[] buffer, int length, string path)
        {
            (byte[] encBytes, int encLen) = EncryptPayload(ref s, buffer, length, marker, path);
            TargetWriter(s).WriteBase64StringValue(encBytes.AsSpan(0, encLen));
            s.Pool.Return(encBytes);
        }

        private static (byte[] buffer, int length) EncryptPayload(ref EncryptionPipelineState s, byte[] payload, int payloadSize, TypeMarker typeMarker, string path)
        {
            byte[] processedBytes = payload;
            int processedLength = payloadSize;
            (byte[] encryptedBytes, int encryptedCount) = s.Encryptor.Encrypt(s.DataEncryptionKey, typeMarker, processedBytes, processedLength, s.Pool);

            s.PathsEncrypted.Add(path);
            return (encryptedBytes, encryptedCount);
        }

        private static int CopySequenceToScratch(ReadOnlySequence<byte> sequence, ref byte[] scratch, int minCapacity, ArrayPoolManager pool)
        {
            int length = (int)sequence.Length;
            EnsureScratchCapacity(ref scratch, Math.Max(length, minCapacity), pool);
            sequence.CopyTo(scratch.AsSpan(0, length));
            return length;
        }

        private static void EnsureScratchCapacity(ref byte[] scratch, int needed, ArrayPoolManager pool)
        {
            if (scratch == null || scratch.Length < needed)
            {
                byte[] newBuf = pool.Rent(needed);
                if (scratch != null)
                {
                    pool.Return(scratch);
                }

                scratch = newBuf;
            }
        }

        private static void HandleStartObject(ref EncryptionPipelineState s)
        {
            if (s.CurrentPayloadWriter != null)
            {
                s.CurrentPayloadWriter.WriteStartObject();
                s.BufferedDepth++;
            }
            else if (s.PendingEncryptedPath != null)
            {
                BeginBufferingContainer(ref s, isArray: false, s.PendingEncryptedPath);
            }
            else
            {
                s.Writer.WriteStartObject();
            }
        }

        private static void HandleEndObject(ref EncryptionPipelineState s, ref Utf8JsonReader reader)
        {
            if (s.CurrentPayloadWriter != null)
            {
                s.CurrentPayloadWriter.WriteEndObject();
                s.BufferedDepth--;
                if (s.BufferedDepth == 0)
                {
                    EndBufferingContainer(ref s, TypeMarker.Object);
                }
            }
            else
            {
                if (reader.CurrentDepth == 0)
                {
                    // Insert encryption metadata before closing root object (so it remains part of the document body).
                    WriteEncryptionInfo(
                        s.Writer,
                        s.FormatVersion,
                        s.EncryptionAlgorithmName,
                        s.DataEncryptionKeyId,
                        s.PathsEncrypted,
                        null);
                }

                s.Writer.WriteEndObject();
            }
        }

        private static void HandleStartArray(ref EncryptionPipelineState s)
        {
            if (s.CurrentPayloadWriter != null)
            {
                s.CurrentPayloadWriter.WriteStartArray();
                s.BufferedDepth++;
            }
            else if (s.PendingEncryptedPath != null)
            {
                BeginBufferingContainer(ref s, isArray: true, s.PendingEncryptedPath);
            }
            else
            {
                s.Writer.WriteStartArray();
            }
        }

        private static void HandleEndArray(ref EncryptionPipelineState s)
        {
            if (s.CurrentPayloadWriter != null)
            {
                s.CurrentPayloadWriter.WriteEndArray();
                s.BufferedDepth--;
                if (s.BufferedDepth == 0)
                {
                    EndBufferingContainer(ref s, TypeMarker.Array);
                }
            }
            else
            {
                s.Writer.WriteEndArray();
            }
        }

        private static void HandlePropertyName(ref EncryptionPipelineState s, ref Utf8JsonReader reader)
        {
            if (reader.CurrentDepth == 1)
            {
                // Only top-level properties are candidates for encryption path matches.
                int nameLen = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                s.PendingEncryptedPath = s.CandidatePaths.TryMatch(ref reader, nameLen, out string path) ? path : null;
            }

            if (!reader.HasValueSequence)
            {
                TargetWriter(s).WritePropertyName(reader.ValueSpan);
            }
            else
            {
                int estimatedLength = (int)reader.ValueSequence.Length;
                byte[] scratch = s.Scratch;
                EnsureScratchCapacity(ref scratch, Math.Max(estimatedLength, EncryptionPipelineState.ScratchMinForStrings), s.Pool);
                int copied = reader.CopyString(scratch);
                s.Scratch = scratch;
                TargetWriter(s).WritePropertyName(scratch.AsSpan(0, copied));
            }
        }

        private static void HandleString(ref EncryptionPipelineState s, ref Utf8JsonReader reader)
        {
            if (s.PendingEncryptedPath != null && s.CurrentPayloadWriter == null)
            {
                int estimatedLength = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                byte[] scratch = s.Scratch;
                EnsureScratchCapacity(ref scratch, Math.Max(estimatedLength, EncryptionPipelineState.ScratchMinForStrings), s.Pool);
                int len = reader.CopyString(scratch);
                s.Scratch = scratch;
                EncryptAndWritePrimitive(ref s, TypeMarker.String, scratch, len, s.PendingEncryptedPath);
                s.PendingEncryptedPath = null;
                s.PropertiesEncrypted++;
            }
            else
            {
                if (!reader.HasValueSequence && !reader.ValueIsEscaped)
                {
                    TargetWriter(s).WriteStringValue(reader.ValueSpan);
                }
                else
                {
                    int estimatedLength = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                    byte[] scratch = s.Scratch;
                    EnsureScratchCapacity(ref scratch, Math.Max(estimatedLength, EncryptionPipelineState.ScratchMinForStrings), s.Pool);
                    int copied = reader.CopyString(scratch);
                    s.Scratch = scratch;
                    TargetWriter(s).WriteStringValue(scratch.AsSpan(0, copied));
                }
            }
        }

        private static void HandleNumber(ref EncryptionPipelineState s, ref Utf8JsonReader reader)
        {
            if (s.PendingEncryptedPath != null && s.CurrentPayloadWriter == null)
            {
                ReadOnlySpan<byte> numberSpan;
                if (!reader.HasValueSequence)
                {
                    numberSpan = reader.ValueSpan;
                }
                else
                {
                    byte[] scratch = s.Scratch;
                    int len = CopySequenceToScratch(reader.ValueSequence, ref scratch, EncryptionPipelineState.ScratchMinForNumbers, s.Pool);
                    s.Scratch = scratch;
                    numberSpan = scratch.AsSpan(0, len);
                }

                (TypeMarker marker, byte[] bytes, int length) = SerializeNumber(numberSpan, s.Pool);
                (byte[] encBytes, int encLen) = EncryptPayload(ref s, bytes, length, marker, s.PendingEncryptedPath);
                s.Pool.Return(bytes);
                TargetWriter(s).WriteBase64StringValue(encBytes.AsSpan(0, encLen));
                s.Pool.Return(encBytes);
                s.PendingEncryptedPath = null;
                s.PropertiesEncrypted++;
            }
            else
            {
                if (!reader.HasValueSequence)
                {
                    TargetWriter(s).WriteRawValue(reader.ValueSpan, true);
                }
                else
                {
                    byte[] scratch = s.Scratch;
                    int len = CopySequenceToScratch(reader.ValueSequence, ref scratch, EncryptionPipelineState.ScratchMinForNumbers, s.Pool);
                    s.Scratch = scratch;
                    TargetWriter(s).WriteRawValue(scratch.AsSpan(0, len), true);
                }
            }
        }

        private static void HandleBool(ref EncryptionPipelineState s, bool value)
        {
            if (s.PendingEncryptedPath != null && s.CurrentPayloadWriter == null)
            {
                (byte[] bytes, int length) = Serialize(value, s.Pool);
                (byte[] encBytes, int encLen) = EncryptPayload(ref s, bytes, length, TypeMarker.Boolean, s.PendingEncryptedPath);
                s.Pool.Return(bytes);
                TargetWriter(s).WriteBase64StringValue(encBytes.AsSpan(0, encLen));
                s.Pool.Return(encBytes);
                s.PendingEncryptedPath = null;
                s.PropertiesEncrypted++;
            }
            else
            {
                TargetWriter(s).WriteBooleanValue(value);
            }
        }

        private static void HandleNull(ref EncryptionPipelineState s)
        {
            TargetWriter(s).WriteNullValue();
            if (s.CurrentPayloadWriter == null)
            {
                s.PendingEncryptedPath = null;
            }
        }

        private static (byte[] buffer, int length) Serialize(bool value, ArrayPoolManager arrayPoolManager)
        {
            int byteCount = StreamProcessor.SqlBoolSerializer.GetSerializedMaxByteCount();
            byte[] buffer = arrayPoolManager.Rent(byteCount);
            int length = StreamProcessor.SqlBoolSerializer.Serialize(value, buffer);
            return (buffer, length);
        }

        private static (TypeMarker typeMarker, byte[] buffer, int length) SerializeNumber(ReadOnlySpan<byte> utf8Bytes, ArrayPoolManager arrayPoolManager)
        {
            if (System.Buffers.Text.Utf8Parser.TryParse(utf8Bytes, out long longValue, out int consumedLong) && consumedLong == utf8Bytes.Length)
            {
                return Serialize(longValue, arrayPoolManager);
            }

            if (System.Buffers.Text.Utf8Parser.TryParse(utf8Bytes, out double doubleValue, out int consumedDouble) && consumedDouble == utf8Bytes.Length)
            {
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

        private sealed class EncryptionPipelineState : IDisposable
        {
            internal const int ScratchMinForStrings = 64;
            internal const int ScratchMinForNumbers = 32;

            // Scratch minimums chosen to reduce immediate re-rent for common small tokens.
            internal Utf8JsonWriter Writer { get; }

            internal MdeEncryptor Encryptor { get; } = new MdeEncryptor();

            internal DataEncryptionKey DataEncryptionKey { get; }

            internal string EncryptionAlgorithmName { get; }

            internal string DataEncryptionKeyId { get; }

            internal CandidatePaths CandidatePaths { get; }

            internal int FormatVersion { get; }

            internal ArrayPoolManager Pool { get; }

            internal List<string> PathsEncrypted { get; }

            internal RentArrayBufferWriter PayloadBuffer { get; }

            internal Utf8JsonWriter PayloadWriter { get; }

            internal byte[] Scratch { get; set; }

            internal JsonReaderState ReaderState { get; set; }

            internal Utf8JsonWriter CurrentPayloadWriter { get; set; }

            internal string PendingEncryptedPath { get; set; }

            internal string BufferingPath { get; set; }

            internal int BufferedDepth { get; set; }

            internal long PropertiesEncrypted { get; set; }

            internal EncryptionPipelineState(
                Utf8JsonWriter writer,
                DataEncryptionKey key,
                EncryptionOptions options,
                CandidatePaths candidatePaths,
                int formatVersion,
                int pathsCapacity,
                ArrayPoolManager pool,
                JsonReaderState initialReaderState)
            {
                this.Writer = writer ?? throw new ArgumentNullException(nameof(writer));
                this.DataEncryptionKey = key ?? throw new ArgumentNullException(nameof(key));
                this.EncryptionAlgorithmName = options?.EncryptionAlgorithm ?? throw new ArgumentNullException(nameof(options));
                this.DataEncryptionKeyId = options.DataEncryptionKeyId;
                this.CandidatePaths = candidatePaths ?? throw new ArgumentNullException(nameof(candidatePaths));
                this.FormatVersion = formatVersion;
                this.Pool = pool ?? throw new ArgumentNullException(nameof(pool));
                this.PathsEncrypted = pathsCapacity > 0 ? new List<string>(pathsCapacity) : new List<string>();
                this.PayloadBuffer = new RentArrayBufferWriter();
                this.PayloadWriter = new Utf8JsonWriter(this.PayloadBuffer);
                this.ReaderState = initialReaderState;
            }

            public void Dispose()
            {
                this.PayloadWriter?.Dispose();
                this.PayloadBuffer?.Dispose();
                if (this.Scratch != null)
                {
                    this.Pool.Return(this.Scratch);
                    this.Scratch = null;
                }
            }
        }
    }
}
#endif