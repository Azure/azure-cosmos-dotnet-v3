//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Json.Serialization.Metadata;

#nullable enable
    internal static partial class CosmosFeedResponseSerializer
    {
        /// <summary>
        /// The service returns feed responses in an envelope. This removes the envelope
        /// and serializes all the items into a list
        /// </summary>
        /// <param name="typeInfo">The type information for the items in the feed response</param>
        /// <param name="serializerCore">The cosmos serializer</param>
        /// <param name="streamWithServiceEnvelope">A stream with the service envelope like: { "ContainerRid":"Test", "Documents":[{ "id":"MyItem"}], "count":1}</param>
        /// <returns>A read only list of the serialized items</returns>
        internal static IReadOnlyCollection<T> FromFeedResponseStream<T>(
            JsonTypeInfo<T[]> typeInfo,
            CosmosSerializerCore serializerCore,
            Stream streamWithServiceEnvelope)
        {
            if (streamWithServiceEnvelope == null)
            {
                return new List<T>();
            }

            using (streamWithServiceEnvelope)
            using (MemoryStream stream = GetStreamWithoutServiceEnvelope(
                            streamWithServiceEnvelope))
            {
                return serializerCore.FromFeedStream<T>(stream, typeInfo);
            }
        }
    }
}
