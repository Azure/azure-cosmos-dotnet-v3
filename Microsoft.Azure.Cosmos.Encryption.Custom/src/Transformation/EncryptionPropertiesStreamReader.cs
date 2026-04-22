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
    /// Locates the <c>_ei</c> encryption metadata subtree inside a JSON document on a
    /// <see cref="Stream"/> and deserializes it into an <see cref="EncryptionProperties"/>
    /// without parsing the rest of the document or materializing the wrapper type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The previous implementation called <c>JsonSerializer.DeserializeAsync&lt;EncryptionPropertiesWrapper&gt;</c>,
    /// which scans the full document and, because <c>Skip()</c> over each unknown root
    /// property requires the complete value to be in-buffer, forces an
    /// <c>ArrayPool&lt;byte&gt;.Shared</c> buffer to grow through <c>16K &#x2192; 32K &#x2192;
    /// 64K &#x2192; 128K</c> on medium documents — a chain whose final rent lands on the
    /// Large Object Heap and rarely comes from a warm pool bucket.
    /// </para>
    /// <para>
    /// The hand-rolled scanner here uses <see cref="Utf8JsonReader"/> with
    /// <c>isFinalBlock: false</c> chunk reads against a small (4 KB by default) pooled buffer.
    /// For non-<c>_ei</c> root properties it calls <see cref="Utf8JsonReader.TrySkip"/>, which
    /// in non-final-block mode respects the buffer boundary and only succeeds when the value is
    /// fully buffered. When <c>TrySkip</c> returns false the scanner reads more bytes (growing
    /// the buffer only for single values that exceed its current size) and retries. When the
    /// <c>_ei</c> property is encountered, the scanner skips past its value to verify the
    /// subtree is fully present, then materializes <see cref="EncryptionProperties"/> by
    /// deserializing from the already-buffered span. The wrapper type is not used.
    /// </para>
    /// </remarks>
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
        /// Streams <paramref name="input"/> and returns the parsed
        /// <see cref="EncryptionProperties"/> from the <c>_ei</c> subtree, or <see langword="null"/>
        /// if the root object does not contain an <c>_ei</c> property.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Leaves <paramref name="input"/>'s <see cref="Stream.Position"/> at 0 on return.
        /// </para>
        /// <para>
        /// Requires <see cref="Stream.CanSeek"/>. The stream is read forward to locate
        /// <c>_ei</c> and is then rewound so the downstream decrypt loop can re-read the
        /// full document from the start. A non-seekable stream cannot support that
        /// contract, so the method fails fast with <see cref="ArgumentException"/> rather
        /// than silently producing partial output.
        /// </para>
        /// </remarks>
        public static async ValueTask<EncryptionProperties> ReadAsync(
            Stream input,
            JsonSerializerOptions serializerOptions,
            CancellationToken cancellationToken)
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

            input.Position = 0;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
            try
            {
                int leftOver = 0;
                bool isFinalBlock = false;
                JsonReaderState readerState = new (JsonReaderOptions);

                while (!isFinalBlock)
                {
                    int read = await input.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken).ConfigureAwait(false);
                    int dataSize = leftOver + read;
                    isFinalBlock = read == 0;

                    // ScanChunk contains the ref-struct Utf8JsonReader and therefore cannot
                    // span an await. It returns a discriminated result plus the reader's
                    // post-scan state for the next iteration.
                    ChunkOutcome outcome = ScanChunk(buffer.AsSpan(0, dataSize), isFinalBlock, readerState, serializerOptions);
                    readerState = outcome.NextState;

                    if (outcome.Status == ScanResult.Found)
                    {
                        input.Position = 0;
                        return outcome.Properties;
                    }

                    if (outcome.Status == ScanResult.RootEnded)
                    {
                        input.Position = 0;
                        return null;
                    }

                    // ScanResult.NeedMore: carry the unconsumed tail and read more.
                    leftOver = dataSize - (int)outcome.BytesConsumed;

                    // Only grow the buffer when it is actually full AND the scan made no
                    // forward progress. A small chunk (e.g. from a trickling network stream)
                    // that failed to complete a token is not a signal that the buffer is too
                    // small; it is a signal that more bytes are needed. Growing on "zero
                    // progress" alone would cause pathological exponential growth under
                    // partial-read transports.
                    if (leftOver == dataSize && dataSize == buffer.Length && !isFinalBlock)
                    {
                        int newSize = buffer.Length * 2;
                        byte[] bigger = ArrayPool<byte>.Shared.Rent(newSize);
                        buffer.AsSpan(0, leftOver).CopyTo(bigger);
                        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                        buffer = bigger;
                    }
                    else if (leftOver != 0 && leftOver != dataSize)
                    {
                        buffer.AsSpan(dataSize - leftOver, leftOver).CopyTo(buffer);
                    }

                    // If leftOver == dataSize && dataSize < buffer.Length, no copy is needed:
                    // the unconsumed bytes are already at the front of the buffer and the
                    // next ReadAsync will simply append more bytes to them.
                }

                // Reached when the outer loop exits with isFinalBlock:true without having
                // seen either the _ei property or a root-level EndObject. This happens for
                // well-formed JSON whose root is a non-object value (array, number, string,
                // literal) — the scanner only recognises _ei at depth 1 inside a root
                // object, so it falls through here with no properties to return. Reset the
                // stream position for parity with the Found / RootEnded paths so the caller
                // can re-read the input if desired.
                input.Position = 0;
                return null;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
        }

        private enum ScanResult
        {
            NeedMore,
            Found,
            RootEnded,
        }

        private readonly struct ChunkOutcome
        {
            public ChunkOutcome(ScanResult status, long bytesConsumed, JsonReaderState nextState, EncryptionProperties properties)
            {
                this.Status = status;
                this.BytesConsumed = bytesConsumed;
                this.NextState = nextState;
                this.Properties = properties;
            }

            public ScanResult Status { get; }

            public long BytesConsumed { get; }

            public JsonReaderState NextState { get; }

            public EncryptionProperties Properties { get; }
        }

        private static ChunkOutcome ScanChunk(
            ReadOnlySpan<byte> buffer,
            bool isFinalBlock,
            JsonReaderState readerState,
            JsonSerializerOptions serializerOptions)
        {
            Utf8JsonReader reader = new (buffer, isFinalBlock, readerState);

            // Track the last reader position that represents a clean "between root-level
            // properties" state. When a buffer boundary truncates a property value (non-_ei
            // via TrySkip, or _ei itself via Read/TrySkip), we return this safe point so the
            // outer loop can grow/refill the buffer and the next scan restarts from a
            // position where both Utf8JsonReader and our logic are in sync (notably: the
            // _ei property name, if any, is re-seen on retry rather than stranding us inside
            // a half-consumed subtree).
            long safeConsumed = reader.BytesConsumed;
            JsonReaderState safeState = reader.CurrentState;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1)
                {
                    if (reader.ValueTextEquals(EncryptedInfoNameBytes))
                    {
                        if (!reader.Read())
                        {
                            return new ChunkOutcome(ScanResult.NeedMore, safeConsumed, safeState, null);
                        }

                        if (reader.TokenType == JsonTokenType.Null)
                        {
                            return new ChunkOutcome(ScanResult.Found, reader.BytesConsumed, reader.CurrentState, null);
                        }

                        if (reader.TokenType != JsonTokenType.StartObject)
                        {
                            throw new InvalidOperationException("Encryption properties metadata was malformed (_ei value was not a JSON object).");
                        }

                        long objectStart = reader.TokenStartIndex;
                        if (!reader.TrySkip())
                        {
                            return new ChunkOutcome(ScanResult.NeedMore, safeConsumed, safeState, null);
                        }

                        long objectEnd = reader.BytesConsumed;

                        // Materialize EncryptionProperties from the confirmed-complete subtree.
                        // isFinalBlock: true here because the slice is a standalone JSON value.
                        Utf8JsonReader subReader = new (buffer.Slice((int)objectStart, (int)(objectEnd - objectStart)), isFinalBlock: true, state: default);
                        EncryptionProperties ep = JsonSerializer.Deserialize<EncryptionProperties>(ref subReader, serializerOptions);
                        return new ChunkOutcome(ScanResult.Found, reader.BytesConsumed, reader.CurrentState, ep);
                    }

                    if (!reader.TrySkip())
                    {
                        return new ChunkOutcome(ScanResult.NeedMore, safeConsumed, safeState, null);
                    }
                }
                else if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 0)
                {
                    return new ChunkOutcome(ScanResult.RootEnded, reader.BytesConsumed, reader.CurrentState, null);
                }

                // Successfully consumed a full token (or a non-_ei property name plus its
                // skipped value). Advance the safe point only here, so any subsequent
                // NeedMore still rewinds to a root-between-properties boundary.
                safeConsumed = reader.BytesConsumed;
                safeState = reader.CurrentState;
            }

            return new ChunkOutcome(ScanResult.NeedMore, safeConsumed, safeState, null);
        }
    }
}
#endif
