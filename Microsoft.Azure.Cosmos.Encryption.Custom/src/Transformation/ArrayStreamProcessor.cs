// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom.RecyclableMemoryStreamMirror;

    internal class ArrayStreamProcessor
    {
        internal int InitialBufferSize { get; set; } = 16384;

        private static readonly JsonReaderOptions JsonReaderOptions = new () { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        private static readonly ReadOnlyMemory<byte> DocumentsPropertyUtf8Bytes;

        static ArrayStreamProcessor()
        {
            DocumentsPropertyUtf8Bytes = new Memory<byte>(Encoding.UTF8.GetBytes(Constants.DocumentsResourcePropertyName));
        }

        internal async Task DeserializeAndDecryptCollectionAsync(
            Stream input,
            Stream output,
            Encryptor encryptor,
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
            using Utf8JsonWriter writer = new (output);

            byte[] buffer = arrayPoolManager.Rent(this.InitialBufferSize);

            Utf8JsonWriter chunkWriter = null;

            int leftOver = 0;
            bool isFinalBlock = false;
            bool isDocumentsArray = false;
            RecyclableMemoryStream bufferWriter = null;
            bool isDocumentsProperty = false;

            RecyclableMemoryStreamManager recyclableMemoryStreamManager = new ();

            JsonReaderState state = new (ArrayStreamProcessor.JsonReaderOptions);

            while (!isFinalBlock)
            {
                int dataLength = await readStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                int dataSize = dataLength + leftOver;
                isFinalBlock = dataSize == 0;
                long bytesConsumed = 0;

                bytesConsumed = this.TransformBuffer(
                    buffer.AsSpan(0, dataSize),
                    isFinalBlock,
                    writer,
                    ref bufferWriter,
                    ref chunkWriter,
                    ref state,
                    ref isDocumentsProperty,
                    ref isDocumentsArray,
                    arrayPoolManager,
                    encryptor,
                    manager,
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
            output.Position = 0;
        }

        private long TransformBuffer(Span<byte> buffer, bool isFinalBlock, Utf8JsonWriter writer, ref RecyclableMemoryStream bufferWriter, ref Utf8JsonWriter chunkWriter, ref JsonReaderState state, ref bool isDocumentsProperty, ref bool isDocumentsArray, ArrayPoolManager arrayPoolManager, Encryptor encryptor, StreamManager streamManager, RecyclableMemoryStreamManager manager)
        {
            Utf8JsonReader reader = new Utf8JsonReader(buffer, isFinalBlock, state);

            while (reader.Read())
            {
                Utf8JsonWriter currentWriter = chunkWriter ?? writer;

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
                            chunkWriter.WriteStartObject();
                        }
                        else
                        {
                            currentWriter.WriteStartObject();
                        }

                        break;
                    case JsonTokenType.EndObject:
                        currentWriter.WriteEndObject();
                        if (reader.CurrentDepth == 2 && chunkWriter != null)
                        {
                            currentWriter.Flush();
                            Stream transformStream = streamManager.CreateStream();
                            bufferWriter.Position = 0;
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits - we cannot make this call async
                            _ = EncryptionProcessor.DecryptAsync(bufferWriter, transformStream, encryptor, new CosmosDiagnosticsContext(), JsonProcessor.Stream, CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

                            byte[] copyBuffer = arrayPoolManager.Rent(16384);
                            Span<byte> copySpan = copyBuffer.AsSpan();
                            int readBytes = 16384;
                            while (readBytes == 16384)
                            {
                                readBytes = transformStream.Read(copySpan);

                                if (readBytes > 0)
                                {
                                    writer.WriteRawValue(copySpan[..readBytes], false);
                                }
                            }

                            transformStream.Dispose();
                            chunkWriter.Dispose();
                            bufferWriter.Dispose();
                            chunkWriter = null;
                        }

                        break;
                    case JsonTokenType.StartArray:
                        if (isDocumentsProperty && reader.CurrentDepth == 1)
                        {
                            isDocumentsArray = true;
                        }

                        currentWriter.WriteStartArray();
                        break;

                    case JsonTokenType.EndArray:
                        currentWriter.WriteEndArray();
                        if (isDocumentsArray && reader.CurrentDepth == 1)
                        {
                            isDocumentsArray = false;
                            isDocumentsProperty = false;
                        }

                        break;

                    case JsonTokenType.PropertyName:
                        if (chunkWriter == null && reader.ValueTextEquals(DocumentsPropertyUtf8Bytes.Span))
                        {
                            isDocumentsProperty = true;
                        }

                        currentWriter.WritePropertyName(reader.ValueSpan);
                        break;
                    case JsonTokenType.String:
                        if (!reader.ValueIsEscaped)
                        {
                            currentWriter.WriteStringValue(reader.ValueSpan);
                        }
                        else
                        {
                            byte[] temp = arrayPoolManager.Rent(reader.ValueSpan.Length);
                            int tempBytes = reader.CopyString(temp);
                            currentWriter.WriteStringValue(temp.AsSpan(0, tempBytes));
                        }

                        break;
                    default:
                        currentWriter.WriteRawValue(reader.ValueSpan, true);
                        break;
                }
            }

            state = reader.CurrentState;
            return reader.BytesConsumed;
        }
    }
}
#endif