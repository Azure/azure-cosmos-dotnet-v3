//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Serialization;

    internal static class CosmosSerializationUtil
    {
        private static readonly CamelCaseNamingStrategy camelCaseNamingStrategy = new CamelCaseNamingStrategy();

        internal static string GetStringWithPropertyNamingPolicy(CosmosLinqSerializerOptions options, string name)
        {
            if (options == null)
            {
                return name;
            }

            return GetStringWithPropertyNamingPolicy(options.PropertyNamingPolicy, name);
        }

        internal static string GetStringWithPropertyNamingPolicy(CosmosPropertyNamingPolicy namingPolicy, string name)
        {
            return namingPolicy switch
            {
                CosmosPropertyNamingPolicy.CamelCase => CosmosSerializationUtil.camelCaseNamingStrategy.GetPropertyName(name, false),
                CosmosPropertyNamingPolicy.Default => name,
                _ => throw new NotImplementedException("Unsupported CosmosPropertyNamingPolicy value"),
            };
        }

        /// <summary>
        /// Attempts to serialize the input stream to the specified target JSON serialization format.
        /// </summary>
        /// <param name="targetSerializationFormat">The desired JSON serialization format for the output stream.</param>
        /// <param name="inputStream">The input stream containing the data to be serialized.</param>
        /// <returns>Returns true if the input stream is successfully serialized to the target format, otherwise false.</returns>
        internal static Stream TrySerializeStreamToTargetFormat(
            JsonSerializationFormat targetSerializationFormat,
            CloneableStream inputStream)
        {
            if (inputStream == null)
            {
                return null;
            }

            using (CosmosBufferedStreamWrapper bufferedStream = new (
                inputStream,
                shouldDisposeInnerStream: false))
            {
                JsonSerializationFormat sourceSerializationFormat = bufferedStream.GetJsonSerializationFormat();

                if (sourceSerializationFormat != JsonSerializationFormat.HybridRow
                    && sourceSerializationFormat != targetSerializationFormat)
                {
                    byte[] targetContent = bufferedStream.ReadAll();

                    if (targetContent != null && targetContent.Length > 0)
                    {
                        return CosmosSerializationUtil.ConvertToStreamUsingJsonSerializationFormat(
                            targetContent,
                            targetSerializationFormat);
                    }
                }
            }

            return inputStream;
        }

        /// <summary>
        /// Prefers text for SDK-owned serializers without changing their settings.
        /// Custom serializers still control their output format, which the embedding boundary must normalize.
        /// </summary>
        internal static Stream ToStreamForTextJson<T>(CosmosSerializer serializer, T input)
        {
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            CosmosSerializer unwrapped = serializer;
            while (unwrapped is CosmosJsonSerializerWrapper wrapper)
            {
                unwrapped = wrapper.InternalJsonSerializer;
            }

            return unwrapped is CosmosJsonDotNetSerializer jsonSerializer
                ? jsonSerializer.ToStream(input, binaryEncodingEnabled: false)
                : serializer.ToStream(input);
        }

        /// <summary>
        /// Converts binary encoded JSON content to text JSON, for payloads that are embedded in a text JSON envelope.
        /// The input memory is never modified or disposed.
        /// </summary>
        /// <param name="content">The serialized JSON content, in text or binary format.</param>
        /// <param name="textContent">The text JSON bytes when a conversion was needed; otherwise null.</param>
        /// <returns>True if <paramref name="content"/> was converted; false if it was already in a format that needs no conversion.</returns>
        internal static bool TryConvertBinaryToText(
            ReadOnlyMemory<byte> content,
            out byte[] textContent)
        {
            textContent = null;
            if (content.IsEmpty || content.Span[0] != (byte)JsonSerializationFormat.Binary)
            {
                return false;
            }

            using (Stream converted = CosmosSerializationUtil.ConvertToStreamUsingJsonSerializationFormat(
                content,
                JsonSerializationFormat.Text))
            {
                textContent = ((MemoryStream)converted).ToArray();
                return true;
            }
        }

        /// <summary>
        /// Reads from the current position without disposing the caller-owned stream.
        /// Binary payloads are converted before embedding in a text JSON envelope.
        /// </summary>
        internal static string ReadStreamAsTextJson(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (stream.CanSeek)
            {
                long position = stream.Position;
                int firstByte = stream.ReadByte();
                stream.Position = position;
                if (firstByte != (byte)JsonSerializationFormat.Binary)
                {
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }

            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                if (CosmosSerializationUtil.TryConvertBinaryToText(
                    new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length),
                    out byte[] textContent))
                {
                    return Encoding.UTF8.GetString(textContent);
                }

                buffer.Position = 0;
                using (StreamReader reader = new StreamReader(buffer))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Converts raw bytes to a stream using the specified JSON serialization format.
        /// </summary>
        /// <param name="rawBytes">The raw byte array to be converted.</param>
        /// <param name="format">The desired JSON serialization format.</param>
        /// <returns>Returns a stream containing the formatted JSON data.</returns>
        internal static Stream ConvertToStreamUsingJsonSerializationFormat(
            ReadOnlyMemory<byte> rawBytes,
            JsonSerializationFormat format)
        {
            IJsonWriter writer = JsonWriter.Create(format);
            if (CosmosObject.TryCreateFromBuffer(rawBytes, out CosmosObject cosmosObject))
            {
                cosmosObject.WriteTo(writer);
            }
            else
            {
                IJsonReader desiredReader = JsonReader.Create(rawBytes);
                desiredReader.WriteAll(writer);
            }

            byte[] formattedBytes = writer.GetResult().ToArray();

            return new MemoryStream(formattedBytes, index: 0, count: formattedBytes.Length, writable: true, publiclyVisible: true);
        }
    }
}
