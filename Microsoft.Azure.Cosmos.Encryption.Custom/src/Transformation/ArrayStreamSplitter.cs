// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
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
    using Microsoft.Azure.Cosmos.Encryption.Custom.RecyclableMemoryStreamMirror;

    internal class ArrayStreamSplitter
    {
        internal int InitialBufferSize { get; set; } = 16384;

        private static readonly JsonReaderOptions JsonReaderOptions = new () { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        private static readonly ReadOnlyMemory<byte> DocumentsPropertyUtf8Bytes;

        static ArrayStreamSplitter()
        {
            DocumentsPropertyUtf8Bytes = new Memory<byte>(Encoding.UTF8.GetBytes(Constants.DocumentsResourcePropertyName));
        }

        internal async Task<List<Stream>> SplitCollectionAsync(
            Stream input,
            StreamManager manager,
            CancellationToken cancellationToken)
        {
            Stream readStream = input;
            if (!input.CanSeek)
            {
                Stream temp = manager.CreateStream();
                await input.CopyToAsync(temp, cancellationToken);
                temp.Position = 0;
                readStream = temp;
            }

            using ArrayPoolManager arrayPoolManager = new ();

            byte[] buffer = arrayPoolManager.Rent(this.InitialBufferSize);

            Utf8JsonWriter chunkWriter = null;

            int leftOver = 0;
            bool isFinalBlock = false;
            bool isDocumentsArray = false;
            RecyclableMemoryStream bufferWriter = null;
            bool isDocumentsProperty = false;

            RecyclableMemoryStreamManager recyclableMemoryStreamManager = new ();

            JsonReaderState state = new (ArrayStreamSplitter.JsonReaderOptions);
            List<Stream> outputList = new List<Stream>();

            while (!isFinalBlock)
            {
                int dataLength = await readStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                int dataSize = dataLength + leftOver;
                isFinalBlock = dataSize == 0;
                long bytesConsumed = 0;

                bytesConsumed = this.TransformBuffer(
                    buffer.AsSpan(0, dataSize),
                    outputList,
                    isFinalBlock,
                    ref bufferWriter,
                    ref chunkWriter,
                    ref state,
                    ref isDocumentsProperty,
                    ref isDocumentsArray,
                    recyclableMemoryStreamManager);

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

            await readStream.DisposeAsync();

            return outputList;
        }

        private long TransformBuffer(Span<byte> buffer, List<Stream> outputList, bool isFinalBlock, ref RecyclableMemoryStream bufferWriter, ref Utf8JsonWriter chunkWriter, ref JsonReaderState state, ref bool isDocumentsProperty, ref bool isDocumentsArray, RecyclableMemoryStreamManager manager)
        {
            Utf8JsonReader reader = new Utf8JsonReader(buffer, isFinalBlock, state);

            while (reader.Read())
            {
                Utf8JsonWriter currentWriter = chunkWriter;

                JsonTokenType tokenType = reader.TokenType;

                switch (tokenType)
                {
                    case JsonTokenType.None:
                        break;
                    case JsonTokenType.StartObject:
                        if (isDocumentsArray && chunkWriter == null)
                        {
                            bufferWriter = new RecyclableMemoryStream(manager);
                            chunkWriter = new Utf8JsonWriter((IBufferWriter<byte>)bufferWriter);
                            chunkWriter?.WriteStartObject();
                        }
                        else
                        {
                            currentWriter?.WriteStartObject();
                        }

                        break;
                    case JsonTokenType.EndObject:
                        currentWriter?.WriteEndObject();
                        if (reader.CurrentDepth == 2 && chunkWriter != null)
                        {
                            currentWriter.Flush();
                            bufferWriter.Position = 0;
                            outputList.Add(bufferWriter);

                            bufferWriter = null;

                            chunkWriter.Dispose();
                            chunkWriter = null;
                        }

                        break;
                    case JsonTokenType.StartArray:
                        if (isDocumentsProperty && reader.CurrentDepth == 1)
                        {
                            isDocumentsArray = true;
                        }

                        currentWriter?.WriteStartArray();
                        break;

                    case JsonTokenType.EndArray:
                        currentWriter?.WriteEndArray();
                        if (isDocumentsArray && reader.CurrentDepth == 1)
                        {
                            isDocumentsArray = false;
                            isDocumentsProperty = false;
                        }

                        break;

                    case JsonTokenType.PropertyName:
                        if (reader.ValueTextEquals(DocumentsPropertyUtf8Bytes.Span))
                        {
                            isDocumentsProperty = true;
                        }

                        currentWriter?.WritePropertyName(reader.ValueSpan);
                        break;
                    default:
                        currentWriter?.WriteRawValue(reader.ValueSpan, true);
                        break;
                }
            }

            state = reader.CurrentState;
            return reader.BytesConsumed;
        }
    }
}
#endif