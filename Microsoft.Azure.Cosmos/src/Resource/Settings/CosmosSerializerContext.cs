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
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AzureVMMetadata))]
    [JsonSerializable(typeof(ConsistencyConfig))]
    [JsonSerializable(typeof(GatewayConnectionConfig))]
    [JsonSerializable(typeof(OtherConnectionConfig))]
    [JsonSerializable(typeof(SqlQuerySpec))]
    [JsonSerializable(typeof(SqlParameterCollection))]
    [JsonSerializable(typeof(PartitionKeyDefinition))]
    [JsonSerializable(typeof(IndexingPolicy))]
    [JsonSerializable(typeof(GeospatialConfig))]
    [JsonSerializable(typeof(UniqueKeyPolicy))]
    [JsonSerializable(typeof(ConflictResolutionPolicy))]
    [JsonSerializable(typeof(ClientEncryptionPolicy))]
    [JsonSerializable(typeof(VectorEmbeddingPolicy))]
    [JsonSerializable(typeof(Collection<ComputedProperty>))]
    [JsonSerializable(typeof(List<CompositeContinuationToken>))]
    [JsonSerializable(typeof(CompositeContinuationToken))]
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
    [JsonSerializable(typeof(PartitionKeyRange))]
    [JsonSerializable(typeof(FeedResource_PartitionKeyRange))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(Collection<string>))]
    [JsonSerializable(typeof(PartitionedQueryExecutionInfo))]
    [JsonSerializable(typeof(HybridSearchQueryInfo))]
    [JsonSerializable(typeof(Documents.Routing.Range<string>))]
    [JsonSerializable(typeof(QueryInfo))]
    [JsonSerializable(typeof(IReadOnlyList<SortOrder>))]
    [JsonSerializable(typeof(CosmosQueryExecutionInfo))]
    [JsonSerializable(typeof(HashIndex))]
    [JsonSerializable(typeof(RangeIndex))]
    [JsonSerializable(typeof(SpatialIndex))]
    [JsonSerializable(typeof(Error))]
    internal partial class CosmosSerializerContext : JsonSerializerContext
    {
    }
}
