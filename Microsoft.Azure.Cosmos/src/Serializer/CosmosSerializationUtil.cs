//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Scripts;
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
