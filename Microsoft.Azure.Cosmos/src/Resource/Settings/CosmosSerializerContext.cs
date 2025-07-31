//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(PartitionKeyDefinition))]
    [JsonSerializable(typeof(IndexingPolicy))]
    [JsonSerializable(typeof(GeospatialConfig))]
    [JsonSerializable(typeof(UniqueKeyPolicy))]
    [JsonSerializable(typeof(ConflictResolutionPolicy))]
    //[JsonSerializable(typeof(ClientEncryptionPolicy))]
    [JsonSerializable(typeof(VectorEmbeddingPolicy))]
    [JsonSerializable(typeof(Collection<ComputedProperty>))]
    [JsonSerializable(typeof(FullTextPolicy))]
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
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(Collection<string>))]
    internal partial class CosmosSerializerContext : JsonSerializerContext
    {
    }
}
