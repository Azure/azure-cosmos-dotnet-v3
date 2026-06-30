//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Decrypts feed / query response bodies under the Stream (System.Text.Json) processor by splicing the
    /// per-document decrypted bytes directly into the response envelope.
    /// </summary>
    /// <remarks>
    /// The Newtonsoft feed path materializes the whole page as a <c>JObject</c>, converts every document back to a
    /// stream to decrypt it, then re-serializes the entire page. This walker instead buffers the raw response once,
    /// locates each <c>Documents</c> array element with a <see cref="Utf8JsonReader"/>, and hands each element's raw
    /// byte slice to the supplied per-document decrypt callback — so the walker never materializes the page (or any
    /// element) as an intermediate Newtonsoft <c>JObject</c>. The envelope (everything outside the <c>Documents</c>
    /// array) is copied through verbatim and element order is preserved. The callback reuses the same MDE streaming
    /// adapter machinery as point reads, so per-document algorithm routing and the
    /// <c>EncryptionProcessor.Decrypt.Mde.Stream</c> diagnostics scope are byte-for-byte identical to the existing path
    /// (legacy AEAD documents, which the Stream processor does not support, still fall back to Newtonsoft decryption).
    /// </remarks>
    internal static class StreamFeedProcessor
    {
        private const int InitialBufferSize = 16384;

        private static readonly byte[] DocumentsPropertyNameUtf8 = Encoding.UTF8.GetBytes(Constants.DocumentsResourcePropertyName);

        private static readonly byte[] CommaUtf8 = { (byte)',' };

        /// <summary>
        /// Decrypts a single feed document supplied as a seekable stream, returning the decrypted stream and the
        /// <see cref="DecryptionContext"/> (or a <see langword="null"/> context when the document carried no
        /// encryption metadata and was passed through unchanged).
        /// </summary>
        internal delegate Task<(Stream DecryptedStream, DecryptionContext Context)> DecryptDocumentAsync(
            Stream documentStream,
            CosmosDiagnosticsContext diagnosticsContext);

        public static async Task<Stream> DecryptResponseAsync(
            Stream content,
            DecryptDocumentAsync decryptDocumentAsync,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            (byte[] body, int length) = await ReadToPooledBufferAsync(content, cancellationToken).ConfigureAwait(false);

            // Match the Newtonsoft path, which disposes the source stream via the serializer once the body is read.
            await content.DisposeCompatAsync().ConfigureAwait(false);

            try
            {
                FeedDocumentsLayout layout = LocateFeedDocuments(body, length);

                MemoryStream output = new (length);
                try
                {
                    // Envelope prefix: everything up to and including the Documents array's opening '['.
                    await output.WriteAsync(body.AsMemory(0, layout.PrefixLength), cancellationToken).ConfigureAwait(false);

                    IReadOnlyList<FeedElement> elements = layout.Elements;
                    for (int index = 0; index < elements.Count; index++)
                    {
                        if (index > 0)
                        {
                            await output.WriteAsync(CommaUtf8, cancellationToken).ConfigureAwait(false);
                        }

                        FeedElement element = elements[index];
                        if (!element.IsObject)
                        {
                            // null / scalar / nested-array element: not an encryptable document, copy verbatim.
                            await output.WriteAsync(body.AsMemory(element.Start, element.Length), cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        await DecryptElementAsync(
                            body,
                            element,
                            output,
                            decryptDocumentAsync,
                            requestOptions,
                            cancellationToken).ConfigureAwait(false);
                    }

                    // Envelope suffix: the Documents array's closing ']' through the end of the body.
                    await output.WriteAsync(body.AsMemory(layout.SuffixStart, length - layout.SuffixStart), cancellationToken).ConfigureAwait(false);

                    output.Position = 0;
                    return output;
                }
                catch
                {
                    await output.DisposeCompatAsync().ConfigureAwait(false);
                    throw;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(body, clearArray: true);
            }
        }

        private static async Task DecryptElementAsync(
            byte[] body,
            FeedElement element,
            Stream output,
            DecryptDocumentAsync decryptDocumentAsync,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);
            MemoryStream documentStream = new (body, element.Start, element.Length, writable: false);
            Stream decryptedStream = null;
            try
            {
                DecryptionContext decryptionContext;
                (decryptedStream, decryptionContext) = await decryptDocumentAsync(documentStream, diagnosticsContext).ConfigureAwait(false);

                if (decryptionContext == null)
                {
                    // Unencrypted document (no _ei, or _ei is not an encryption-metadata object): copy verbatim.
                    await output.WriteAsync(body.AsMemory(element.Start, element.Length), cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (decryptedStream.CanSeek)
                {
                    decryptedStream.Position = 0;
                }

                await decryptedStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (decryptedStream != null && !ReferenceEquals(decryptedStream, documentStream))
                {
                    await decryptedStream.DisposeCompatAsync().ConfigureAwait(false);
                }

                await documentStream.DisposeCompatAsync().ConfigureAwait(false);
            }
        }

        private static async Task<(byte[] Body, int Length)> ReadToPooledBufferAsync(
            Stream content,
            CancellationToken cancellationToken)
        {
            if (content.CanSeek)
            {
                content.Position = 0;
                int length = checked((int)content.Length);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Max(length, 1));
                int offset = 0;
                while (offset < length)
                {
                    int read = await content.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    offset += read;
                }

                return (buffer, offset);
            }
            else
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
                int offset = 0;
                while (true)
                {
                    if (offset == buffer.Length)
                    {
                        byte[] bigger = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                        Array.Copy(buffer, bigger, offset);
                        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                        buffer = bigger;
                    }

                    int read = await content.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    offset += read;
                }

                return (buffer, offset);
            }
        }

        /// <summary>
        /// Locates the top-level <c>Documents</c> array and records the byte ranges of the envelope prefix, the
        /// envelope suffix, and each array element. The whole body is in a single buffer (<c>isFinalBlock: true</c>),
        /// so <see cref="Utf8JsonReader.TrySkip"/> can jump over each element without inspecting its interior tokens.
        /// </summary>
        private static FeedDocumentsLayout LocateFeedDocuments(byte[] body, int length)
        {
            Utf8JsonReader reader = new (body.AsSpan(0, length), isFinalBlock: true, state: default);

            bool documentsFound = false;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName
                    && reader.CurrentDepth == 1
                    && reader.ValueTextEquals(DocumentsPropertyNameUtf8))
                {
                    documentsFound = true;
                    break;
                }
            }

            if (!documentsFound || !reader.Read() || reader.TokenType != JsonTokenType.StartArray)
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed response did not have an array of Documents");
            }

            int prefixLength = (int)reader.BytesConsumed;
            List<FeedElement> elements = new ();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                int elementStart = (int)reader.TokenStartIndex;
                bool isObject = reader.TokenType == JsonTokenType.StartObject;

                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                int elementEnd = (int)reader.BytesConsumed;
                elements.Add(new FeedElement(elementStart, elementEnd - elementStart, isObject));
            }

            if (reader.TokenType != JsonTokenType.EndArray)
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Documents array was not terminated.");
            }

            int suffixStart = (int)reader.TokenStartIndex;
            return new FeedDocumentsLayout(prefixLength, suffixStart, elements);
        }

        private readonly struct FeedElement
        {
            public FeedElement(int start, int length, bool isObject)
            {
                this.Start = start;
                this.Length = length;
                this.IsObject = isObject;
            }

            public int Start { get; }

            public int Length { get; }

            public bool IsObject { get; }
        }

        private readonly struct FeedDocumentsLayout
        {
            public FeedDocumentsLayout(int prefixLength, int suffixStart, List<FeedElement> elements)
            {
                this.PrefixLength = prefixLength;
                this.SuffixStart = suffixStart;
                this.Elements = elements;
            }

            public int PrefixLength { get; }

            public int SuffixStart { get; }

            public IReadOnlyList<FeedElement> Elements { get; }
        }
    }
}
#endif
