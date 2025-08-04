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
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(AccountConsistency))]
    [JsonSerializable(typeof(AccountProperties))]
    [JsonSerializable(typeof(AzureVMMetadata))]
    [JsonSerializable(typeof(ClientEncryptionPolicy))]
    [JsonSerializable(typeof(Collection<AccountRegion>))]
    [JsonSerializable(typeof(Collection<ComputedProperty>))]
    [JsonSerializable(typeof(Collection<string>))]
    [JsonSerializable(typeof(CompositeContinuationToken))]
    [JsonSerializable(typeof(ConflictResolutionPolicy))]
    [JsonSerializable(typeof(ConsistencyConfig))]
    [JsonSerializable(typeof(ContainerProperties))]
    [JsonSerializable(typeof(CosmosQueryExecutionInfo))]
    [JsonSerializable(typeof(DatabaseProperties))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(Documents.Routing.Range<string>))]
    [JsonSerializable(typeof(FeedResource_Address))]
    [JsonSerializable(typeof(FeedResource_PartitionKeyRange))]
    [JsonSerializable(typeof(FullTextPolicy))]
    [JsonSerializable(typeof(GatewayConnectionConfig))]
    [JsonSerializable(typeof(GeospatialConfig))]
    [JsonSerializable(typeof(HybridSearchQueryInfo))]
    [JsonSerializable(typeof(IndexingPolicy))]
    [JsonSerializable(typeof(IndexUtilizationInfo))]
    [JsonSerializable(typeof(IReadOnlyList<SortOrder>))]
    [JsonSerializable(typeof(List<CompositeContinuationToken>))]
    [JsonSerializable(typeof(OtherConnectionConfig))]
    [JsonSerializable(typeof(PartitionKeyDefinition))]
    [JsonSerializable(typeof(PartitionKeyRange))]
    [JsonSerializable(typeof(PartitionedQueryExecutionInfo))]
    [JsonSerializable(typeof(PartitionedQueryExecutionInfoInternal))]
    [JsonSerializable(typeof(PatchSpec))]
    [JsonSerializable(typeof(QueryInfo))]
    [JsonSerializable(typeof(ReadPolicy))]
    [JsonSerializable(typeof(ReplicationPolicy))]
    [JsonSerializable(typeof(SqlQuerySpec))]
    [JsonSerializable(typeof(SqlParameterCollection))]
    [JsonSerializable(typeof(StoredProcedureProperties))]
    [JsonSerializable(typeof(ThroughputProperties))]
    [JsonSerializable(typeof(TriggerProperties))]
    [JsonSerializable(typeof(UniqueKeyPolicy))]
    [JsonSerializable(typeof(UserDefinedFunctionProperties))]
    [JsonSerializable(typeof(VectorEmbeddingPolicy))]
    internal partial class CosmosSerializerContext : JsonSerializerContext
    {
    }
}
