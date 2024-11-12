//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;

    /// <summary>
    /// Utility class for performing different operation on a given serializer.
    /// </summary>
    internal static class CosmosSerializerUtils
    {
        /// <summary>
        /// Checks the first byte of the provided stream to determine if it matches the desired JSON serialization format.
        /// </summary>
        /// <param name="messageContent">The stream containing the message content to be checked.</param>
        /// <param name="desiredFormat">The desired JSON serialization format to check against.</param>
        /// <param name="content">The output byte array containing the stream content if the first byte matches the desired format.</param>
        /// <returns>Returns a boolean flag indicating if the first byte of the stream matches the desired format.</returns>
        internal static bool CheckFirstBufferByte(
            Stream messageContent,
            JsonSerializationFormat desiredFormat,
            out byte[] content)
        {
            content = default;

            if (messageContent == null || !messageContent.CanRead)
            {
                return false;
            }

            // Use a buffer to read the first byte
            byte[] buffer = new byte[1];
            int readCount = messageContent.Read(buffer, 0, 1);

            // Reset the stream position if it supports seeking
            if (messageContent.CanSeek)
            {
                messageContent.Position = 0;
            }

            // Check if the first byte matches the desired format
            if (readCount > 0
                && (IsBinaryFormat(buffer[0], desiredFormat) || IsTextFormat(buffer[0], desiredFormat)))
            {
                // If the first byte matches with the desired format then
                // copy the stream content to a byte array.
                using (MemoryStream stream = new())
                {
                    messageContent.CopyTo(stream);
                    content = stream.ToArray();
                }

                // Reset the stream position again after copying
                if (messageContent.CanSeek)
                {
                    messageContent.Position = 0;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines if the first byte of a stream matches the binary JSON serialization format.
        /// </summary>
        /// <param name="firstByte">The first byte of the stream to check.</param>
        /// <param name="desiredFormat">The desired JSON serialization format.</param>
        /// <returns>Returns true if the first byte matches the binary format, otherwise false.</returns>
        internal static bool IsBinaryFormat(
            int firstByte,
            JsonSerializationFormat desiredFormat)
        {
            return desiredFormat == JsonSerializationFormat.Binary && firstByte == (int)JsonSerializationFormat.Binary;
        }

        /// <summary>
        /// Determines if the first byte of a stream matches the text JSON serialization format.
        /// </summary>
        /// <param name="firstByte">The first byte of the stream to check.</param>
        /// <param name="desiredFormat">The desired JSON serialization format.</param>
        /// <returns>Returns true if the first byte matches the text format, otherwise false.</returns>
        internal static bool IsTextFormat(
            int firstByte,
            JsonSerializationFormat desiredFormat)
        {
            return desiredFormat == JsonSerializationFormat.Text && firstByte < (int)JsonSerializationFormat.Binary;
        }

        /// <summary>
        /// Converts the given input object to a binary stream using the specified JSON serializer.
        /// </summary>
        /// <typeparam name="T">The type of the input object.</typeparam>
        /// <param name="input">The input object to be serialized.</param>
        /// <param name="serializer">The JSON serializer to use for serialization.</param>
        /// <returns>Returns a stream containing the binary serialized data of the input object.</returns>
        internal static Stream ConvertInputToBinaryStream<T>(
            T input,
            Newtonsoft.Json.JsonSerializer serializer)
        {
            MemoryStream streamPayload = new();
            using (CosmosDBToNewtonsoftWriter writer = new(JsonSerializationFormat.Binary))
            {
                writer.Formatting = Newtonsoft.Json.Formatting.None;
                serializer.Serialize(writer, input);
                byte[] binBytes = writer.GetResult().ToArray();
                streamPayload.Write(binBytes, 0, binBytes.Length);
            }

            streamPayload.Position = 0;
            return streamPayload;
        }

        /// <summary>
        /// Converts the given input object to a binary stream using the specified JSON serializer.
        /// </summary>
        /// <typeparam name="T">The type of the input object.</typeparam>
        /// <param name="input">The input object to be serialized.</param>
        /// <param name="serializer">The JSON serializer to use for serialization.</param>
        /// <returns>Returns a stream containing the binary serialized data of the input object.</returns>
        internal static Stream ConvertInputToNonSeekableBinaryStream<T>(
            T input,
            Newtonsoft.Json.JsonSerializer serializer)
        {
            using (CosmosDBToNewtonsoftWriter writer = new(JsonSerializationFormat.Binary))
            {
                writer.Formatting = Newtonsoft.Json.Formatting.None;
                serializer.Serialize(writer, input);
                byte[] binBytes = writer.GetResult().ToArray();

                CosmosBufferedStreamWrapperTests.NonSeekableMemoryStream streamPayload = new(binBytes);

                if (streamPayload.CanSeek)
                {
                    streamPayload.Position = 0;
                }

                return streamPayload;
            }
        }
        /// <summary>
        /// Converts the given input object to a binary stream using the specified JSON serializer.
        /// </summary>
        /// <typeparam name="T">The type of the input object.</typeparam>
        /// <param name="input">The input object to be serialized.</param>
        /// <param name="serializer">The JSON serializer to use for serialization.</param>
        /// <returns>Returns a stream containing the binary serialized data of the input object.</returns>
        internal static Stream ConvertInputToTextStream<T>(
            T input,
            Newtonsoft.Json.JsonSerializer serializer)
        {
            MemoryStream streamPayload = new();
            using (StreamWriter streamWriter = new(streamPayload, encoding: new UTF8Encoding(false, true), bufferSize: 1024, leaveOpen: true))
            {
                using (Newtonsoft.Json.JsonWriter writer = new Newtonsoft.Json.JsonTextWriter(streamWriter))
                {
                    writer.Formatting = Newtonsoft.Json.Formatting.None;
                    serializer.Serialize(writer, input);
                    writer.Flush();
                    streamWriter.Flush();
                }
            }

            streamPayload.Position = 0;
            return streamPayload;
        }
    }
}