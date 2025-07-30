//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This is an interface to allow a custom serializer to be used by the CosmosClient
    /// </summary>
    internal partial class CosmosSerializerCore
    {
        internal T[] FromFeedStream<T>(Stream stream, JsonTypeInfo<T[]> typeInfo)
        {
            return JsonSerializer.Deserialize<T[]>(stream, typeInfo);
        }
    }
}