//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ContainerProperties))]
    [JsonSerializable(typeof(PatchSpec))]
    [JsonSerializable(typeof(ThroughputProperties))]
    [JsonSerializable(typeof(TriggerProperties))]
    [JsonSerializable(typeof(StoredProcedureProperties))]
    [JsonSerializable(typeof(UserDefinedFunctionProperties))]
    [JsonSerializable(typeof(DatabaseProperties))]
    [JsonSerializable(typeof(AccountProperties))]
    [JsonSerializable(typeof(Collection<AccountRegion>))]
    [JsonSerializable(typeof(AccountConsistency))]
    [JsonSerializable(typeof(ReplicationPolicy))]
    [JsonSerializable(typeof(ReadPolicy))]
    [JsonSerializable(typeof(FeedResource_Address))]
    [JsonSerializable(typeof(FeedResource_PartitionKeyRange))]
    internal partial class CosmosSerializerContext : JsonSerializerContext
    {
    }
}
