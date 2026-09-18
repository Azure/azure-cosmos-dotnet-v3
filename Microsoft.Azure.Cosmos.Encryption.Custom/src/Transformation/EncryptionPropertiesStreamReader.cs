//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Locates the <c>_ei</c> encryption-metadata subtree on a JSON stream and deserializes
    /// it into an <see cref="EncryptionProperties"/> without parsing the rest of the
    /// document. Reads in small pooled chunks via <see cref="Utf8JsonReader"/> with
    /// <c>isFinalBlock: false</c>, growing the buffer only when a single value exceeds it.
    /// </summary>
    internal static class EncryptionPropertiesStreamReader
    {
        private const int InitialBufferSize = 4096;

        private static readonly byte[] EncryptedInfoNameBytes = Encoding.UTF8.GetBytes(Constants.EncryptedInfo);

        private static readonly JsonReaderOptions JsonReaderOptions = new ()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>
        /// Returns the parsed <see cref="EncryptionProperties"/> from the <c>_ei</c> subtree,
        /// or <see langword="null"/> if the root object has no <c>_ei</c> property. Requires
        /// a seekable stream and leaves <see cref="Stream.Position"/> at 0 on return.
        /// </summary>
        public static async ValueTask<EncryptionProperties> ReadAsync(
            Stream input,
            JsonSerializerOptions serializerOptions,
            CancellationToken cancellationToken)
        {
            EncryptionMetadataReadResult result = await ReadResultAsync(
                input,
                serializerOptions,
                cancellationToken,
                JsonFeedStreamHelper.MaximumBufferSize).ConfigureAwait(false);
            return result.Properties;
        }

        internal static async ValueTask<EncryptionProperties> ReadAsync(
            Stream input,
            JsonSerializerOptions serializerOptions,
            CancellationToken cancellationToken,
            int maxBufferSize)
        {
            EncryptionMetadataReadResult result = await ReadResultAsync(
                input,
                serializerOptions,
                cancellationToken,
                maxBufferSize).ConfigureAwait(false);
            return result.Properties;
        }

        internal static ValueTask<EncryptionMetadataReadResult> ReadResultAsync(
            Stream input,
            JsonSerializerOptions serializerOptions,
            CancellationToken cancellationToken)
            => ReadResultAsync(
                input,
                serializerOptions,
                cancellationToken,
                JsonFeedStreamHelper.MaximumBufferSize);

        private static async ValueTask<EncryptionMetadataReadResult> ReadResultAsync(
            Stream input,
            JsonSerializerOptions serializerOptions,
            CancellationToken cancellationToken,
            int maxBufferSize)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (!input.CanSeek)
            {
                throw new ArgumentException(
                    "The encryption-properties reader requires a seekable stream so the document can be re-read from the start after scanning _ei.",
                    nameof(input));
            }

            StreamPositionHelper.ResetToStart(input, nameof(input));

            byte[] buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
            try
            {
                int leftOver = 0;
                bool isFinalBlock = false;
                JsonReaderState readerState = new (JsonReaderOptions);
                MetadataCandidate metadataCandidate = default;
                bool rootObjectStarted = false;
                bool rootObjectEnded = false;

                while (!isFinalBlock)
                {
                    int read = await input.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken).ConfigureAwait(false);
                    int dataSize = leftOver + read;
                    isFinalBlock = read == 0;

                    // ScanChunk holds a ref-struct Utf8JsonReader and cannot span an await.
                    ChunkOutcome outcome = ScanChunk(
                        buffer.AsSpan(0, dataSize),
                        isFinalBlock,
                        readerState,
                        ref metadataCandidate,
                        ref rootObjectStarted,
                        ref rootObjectEnded);
                    readerState = outcome.NextState;

                    if (outcome.Status == ScanResult.RootEnded)
                    {
                        StreamPositionHelper.ResetToStart(input, nameof(input));
                        return metadataCandidate.Deserialize(serializerOptions);
                    }

                    leftOver = dataSize - (int)outcome.BytesConsumed;

                    buffer = JsonFeedStreamHelper.HandleLeftOver(
                        buffer,
                        dataSize,
                        leftOver,
                        checked((int)outcome.BytesConsumed),
                        maxBufferSize);
                }

                throw new InvalidOperationException(NewtonsoftJsonObjectReader.InvalidBodyMessage);
            }
            catch (JsonException exception)
            {
                StreamPositionHelper.TryResetToStart(input);
                throw new InvalidOperationException(
                    NewtonsoftJsonObjectReader.InvalidBodyMessage,
                    exception);
            }
            catch
            {
                StreamPositionHelper.TryResetToStart(input);
                throw;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }

        private enum ScanResult
        {
            NeedMore,
            RootEnded,
        }

        private readonly struct ChunkOutcome
        {
            public ChunkOutcome(ScanResult status, long bytesConsumed, JsonReaderState nextState)
            {
                this.Status = status;
                this.BytesConsumed = bytesConsumed;
                this.NextState = nextState;
            }

            public ScanResult Status { get; }

            public long BytesConsumed { get; }

            public JsonReaderState NextState { get; }
        }

        private struct MetadataCandidate
        {
            private byte[] json;
            private bool seen;
            private bool invalid;

            public void SetNull()
            {
                this.json = null;
                this.seen = true;
                this.invalid = false;
            }

            public void SetJson(ReadOnlySpan<byte> value)
            {
                this.json = value.ToArray();
                this.seen = true;
                this.invalid = false;
            }

            public void SetInvalid()
            {
                this.invalid = true;
                this.seen = true;
            }

            public EncryptionMetadataReadResult Deserialize(JsonSerializerOptions serializerOptions)
            {
                if (!this.seen)
                {
                    return new EncryptionMetadataReadResult(
                        EncryptionMetadataDisposition.None,
                        properties: null);
                }

                if (this.invalid)
                {
                    return new EncryptionMetadataReadResult(
                        EncryptionMetadataDisposition.Invalid,
                        properties: null);
                }

                if (this.json == null)
                {
                    return new EncryptionMetadataReadResult(
                        EncryptionMetadataDisposition.Plaintext,
                        properties: null);
                }

                using JsonDocument document = JsonDocument.Parse(this.json);
                EncryptionMetadataDisposition disposition =
                    EncryptionMetadataClassifier.Classify(document.RootElement);
                EncryptionProperties properties =
                    disposition == EncryptionMetadataDisposition.Mde ||
                    disposition == EncryptionMetadataDisposition.Legacy ||
                    disposition == EncryptionMetadataDisposition.Unsupported
                        ? JsonSerializer.Deserialize<EncryptionProperties>(this.json, serializerOptions)
                        : null;
                return new EncryptionMetadataReadResult(disposition, properties);
            }
        }

        private static ChunkOutcome ScanChunk(
            ReadOnlySpan<byte> buffer,
            bool isFinalBlock,
            JsonReaderState readerState,
            ref MetadataCandidate metadataCandidate,
            ref bool rootObjectStarted,
            ref bool rootObjectEnded)
        {
            Utf8JsonReader reader = new (buffer, isFinalBlock, readerState);

            // Snapshot of the reader position at the last inter-property boundary. When a
            // chunk ends mid-property we return this point so the next scan re-sees the
            // PropertyName token instead of getting stranded inside a half-consumed value.
            long safeConsumed = reader.BytesConsumed;
            JsonReaderState safeState = reader.CurrentState;

            while (reader.Read())
            {
                if (!rootObjectStarted)
                {
                    if (reader.TokenType != JsonTokenType.StartObject)
                    {
                        throw new InvalidOperationException(
                            NewtonsoftJsonObjectReader.InvalidBodyMessage);
                    }

                    rootObjectStarted = true;
                }

                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1)
                {
                    if (reader.ValueTextEquals(EncryptedInfoNameBytes))
                    {
                        if (!reader.Read())
                        {
                            return new ChunkOutcome(ScanResult.NeedMore, safeConsumed, safeState);
                        }

                        if (reader.TokenType == JsonTokenType.Null)
                        {
                            metadataCandidate.SetNull();
                        }
                        else if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            long objectStart = reader.TokenStartIndex;
                            if (!reader.TrySkip())
                            {
                                return new ChunkOutcome(ScanResult.NeedMore, safeConsumed, safeState);
                            }

                            long objectEnd = reader.BytesConsumed;
                            metadataCandidate.SetJson(buffer.Slice((int)objectStart, (int)(objectEnd - objectStart)));
                        }
                        else
                        {
                            if (!reader.TrySkip())
                            {
                                return new ChunkOutcome(ScanResult.NeedMore, safeConsumed, safeState);
                            }

                            metadataCandidate.SetInvalid();
                        }
                    }

                    if (!reader.TrySkip())
                    {
                        return new ChunkOutcome(ScanResult.NeedMore, safeConsumed, safeState);
                    }
                }
                else if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 0)
                {
                    rootObjectEnded = true;
                }

                safeConsumed = reader.BytesConsumed;
                safeState = reader.CurrentState;
            }

            return new ChunkOutcome(
                isFinalBlock && rootObjectEnded ? ScanResult.RootEnded : ScanResult.NeedMore,
                safeConsumed,
                safeState);
        }

        internal readonly struct EncryptionMetadataReadResult
        {
            public EncryptionMetadataReadResult(
                EncryptionMetadataDisposition disposition,
                EncryptionProperties properties)
            {
                this.Disposition = disposition;
                this.Properties = properties;
            }

            public EncryptionMetadataDisposition Disposition { get; }

            public EncryptionProperties Properties { get; }
        }
    }
}
#endif
