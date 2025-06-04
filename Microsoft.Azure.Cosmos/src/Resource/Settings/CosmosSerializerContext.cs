//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using Microsoft.Azure.Documents;

    [JsonSerializable(typeof(AccountProperties))]
    [JsonSerializable(typeof(FeedResource_Address))]
    [JsonSerializable(typeof(FeedResource_PartitionKeyRange))]
    internal partial class CosmosSerializerContext : JsonSerializerContext
    {
    }
}
