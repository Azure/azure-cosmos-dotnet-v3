//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System.Text.Json;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Pre-encode all strings used on serialization to improve performance.
    /// </summary>
    internal static class JsonEncodedStrings
    {
        public readonly static JsonEncodedText Query = JsonEncodedText.Encode("query");
        public readonly static JsonEncodedText Parameters = JsonEncodedText.Encode("parameters");
        public readonly static JsonEncodedText CosmosSerializer = JsonEncodedText.Encode("CosmosSerializer");
        public readonly static JsonEncodedText ApplicationName = JsonEncodedText.Encode("ApplicationName");
        public readonly static JsonEncodedText GatewayModeMaxConnectionLimit = JsonEncodedText.Encode("GatewayModeMaxConnectionLimit");
        public readonly static JsonEncodedText RequestTimeout = JsonEncodedText.Encode("RequestTimeout");
        public readonly static JsonEncodedText ConnectionMode = JsonEncodedText.Encode("ConnectionMode");
        public readonly static JsonEncodedText ConsistencyLevel = JsonEncodedText.Encode("ConsistencyLevel");
        public readonly static JsonEncodedText MaxRetryAttemptsOnRateLimitedRequests = JsonEncodedText.Encode("MaxRetryAttemptsOnRateLimitedRequests");
        public readonly static JsonEncodedText MaxRetryWaitTimeOnRateLimitedRequests = JsonEncodedText.Encode("MaxRetryWaitTimeOnRateLimitedRequests");
        public readonly static JsonEncodedText IdleTcpConnectionTimeout = JsonEncodedText.Encode("IdleTcpConnectionTimeout");
        public readonly static JsonEncodedText OpenTcpConnectionTimeout = JsonEncodedText.Encode("OpenTcpConnectionTimeout");
        public readonly static JsonEncodedText MaxRequestsPerTcpConnection = JsonEncodedText.Encode("MaxRequestsPerTcpConnection");
        public readonly static JsonEncodedText MaxTcpConnectionsPerEndpoint = JsonEncodedText.Encode("MaxTcpConnectionsPerEndpoint");
        public readonly static JsonEncodedText LimitToEndpoint = JsonEncodedText.Encode("LimitToEndpoint");
        public readonly static JsonEncodedText AllowBulkExecution = JsonEncodedText.Encode("AllowBulkExecution");
        public readonly static JsonEncodedText Name = JsonEncodedText.Encode("name");
        public readonly static JsonEncodedText Value = JsonEncodedText.Encode("value");
        public readonly static JsonEncodedText Code = JsonEncodedText.Encode(Constants.Properties.Code);
        public readonly static JsonEncodedText Message = JsonEncodedText.Encode(Constants.Properties.Message);
        public readonly static JsonEncodedText ErrorDetails = JsonEncodedText.Encode(Constants.Properties.ErrorDetails);
        public readonly static JsonEncodedText AdditionalErrorInfo = JsonEncodedText.Encode(Constants.Properties.AdditionalErrorInfo);
        public readonly static JsonEncodedText OfferVersion = JsonEncodedText.Encode(Constants.Properties.OfferVersion);
        public readonly static JsonEncodedText ResourceLink = JsonEncodedText.Encode(Constants.Properties.ResourceLink);
        public readonly static JsonEncodedText OfferType = JsonEncodedText.Encode(Constants.Properties.OfferType);
        public readonly static JsonEncodedText OfferResourceId = JsonEncodedText.Encode(Constants.Properties.OfferResourceId);
        public readonly static JsonEncodedText OfferContent = JsonEncodedText.Encode(Constants.Properties.OfferContent);
        public readonly static JsonEncodedText SelfLink = JsonEncodedText.Encode(Constants.Properties.SelfLink);
        public readonly static JsonEncodedText Id = JsonEncodedText.Encode(Constants.Properties.Id);
        public readonly static JsonEncodedText RId = JsonEncodedText.Encode(Constants.Properties.RId);
        public readonly static JsonEncodedText OfferThroughput = JsonEncodedText.Encode(Constants.Properties.OfferThroughput);
        public readonly static JsonEncodedText OfferIsRUPerMinuteThroughputEnabled = JsonEncodedText.Encode(Constants.Properties.OfferIsRUPerMinuteThroughputEnabled);
        public readonly static JsonEncodedText LastModified = JsonEncodedText.Encode(Constants.Properties.LastModified);
        public readonly static JsonEncodedText ETag = JsonEncodedText.Encode(Constants.Properties.ETag);
        public readonly static JsonEncodedText Type = JsonEncodedText.Encode("type");
        public readonly static JsonEncodedText DefaultConsistencyLevel = JsonEncodedText.Encode(Constants.Properties.DefaultConsistencyLevel);
        public readonly static JsonEncodedText MaxStalenessPrefix = JsonEncodedText.Encode(Constants.Properties.MaxStalenessPrefix);
        public readonly static JsonEncodedText MaxStalenessIntervalInSeconds = JsonEncodedText.Encode(Constants.Properties.MaxStalenessIntervalInSeconds);
        public readonly static JsonEncodedText WritableLocations = JsonEncodedText.Encode(Constants.Properties.WritableLocations);
        public readonly static JsonEncodedText ReadableLocations = JsonEncodedText.Encode(Constants.Properties.ReadableLocations);
        public readonly static JsonEncodedText UserConsistencyPolicy = JsonEncodedText.Encode(Constants.Properties.UserConsistencyPolicy);
        public readonly static JsonEncodedText AddressesLink = JsonEncodedText.Encode(Constants.Properties.AddressesLink);
        public readonly static JsonEncodedText UserReplicationPolicy = JsonEncodedText.Encode(Constants.Properties.UserReplicationPolicy);
        public readonly static JsonEncodedText SystemReplicationPolicy = JsonEncodedText.Encode(Constants.Properties.SystemReplicationPolicy);
        public readonly static JsonEncodedText ReadPolicy = JsonEncodedText.Encode(Constants.Properties.ReadPolicy);
        public readonly static JsonEncodedText QueryEngineConfiguration = JsonEncodedText.Encode(Constants.Properties.QueryEngineConfiguration);
        public readonly static JsonEncodedText EnableMultipleWriteLocations = JsonEncodedText.Encode(Constants.Properties.EnableMultipleWriteLocations);
        public readonly static JsonEncodedText DatabaseAccountEndpoint = JsonEncodedText.Encode(Constants.Properties.DatabaseAccountEndpoint);
        public readonly static JsonEncodedText Path = JsonEncodedText.Encode(Constants.Properties.Path);
        public readonly static JsonEncodedText Order = JsonEncodedText.Encode(Constants.Properties.Order);
        public readonly static JsonEncodedText OperationType = JsonEncodedText.Encode(Constants.Properties.OperationType);
        public readonly static JsonEncodedText ResourceType = JsonEncodedText.Encode(Constants.Properties.ResourceType);
        public readonly static JsonEncodedText SourceResourceId = JsonEncodedText.Encode(Constants.Properties.SourceResourceId);
        public readonly static JsonEncodedText Content = JsonEncodedText.Encode(Constants.Properties.Content);
        public readonly static JsonEncodedText ConflictLSN = JsonEncodedText.Encode(Constants.Properties.ConflictLSN);
        public readonly static JsonEncodedText Mode = JsonEncodedText.Encode(Constants.Properties.Mode);
        public readonly static JsonEncodedText ConflictResolutionPath = JsonEncodedText.Encode(Constants.Properties.ConflictResolutionPath);
        public readonly static JsonEncodedText ConflictResolutionProcedure = JsonEncodedText.Encode(Constants.Properties.ConflictResolutionProcedure);
        public readonly static JsonEncodedText IndexingPolicy = JsonEncodedText.Encode(Constants.Properties.IndexingPolicy);
        public readonly static JsonEncodedText UniqueKeyPolicy = JsonEncodedText.Encode(Constants.Properties.UniqueKeyPolicy);
        public readonly static JsonEncodedText ConflictResolutionPolicy = JsonEncodedText.Encode(Constants.Properties.ConflictResolutionPolicy);
        public readonly static JsonEncodedText DefaultTimeToLive = JsonEncodedText.Encode(Constants.Properties.DefaultTimeToLive);
        public readonly static JsonEncodedText PartitionKey = JsonEncodedText.Encode(Constants.Properties.PartitionKey);
        public readonly static JsonEncodedText PartitionKeyDefinitionVersion = JsonEncodedText.Encode(Constants.Properties.PartitionKeyDefinitionVersion);
        public readonly static JsonEncodedText Paths = JsonEncodedText.Encode(Constants.Properties.Paths);
        public readonly static JsonEncodedText PartitionKind = JsonEncodedText.Encode(Constants.Properties.PartitionKind);
        public readonly static JsonEncodedText SystemKey = JsonEncodedText.Encode(Constants.Properties.SystemKey);
        public readonly static JsonEncodedText Indexes = JsonEncodedText.Encode(Constants.Properties.Indexes);
        public readonly static JsonEncodedText IndexKind = JsonEncodedText.Encode(Constants.Properties.IndexKind);
        public readonly static JsonEncodedText DataType = JsonEncodedText.Encode(Constants.Properties.DataType);
        public readonly static JsonEncodedText Precision = JsonEncodedText.Encode(Constants.Properties.Precision);
        public readonly static JsonEncodedText Automatic = JsonEncodedText.Encode(Constants.Properties.Automatic);
        public readonly static JsonEncodedText IndexingMode = JsonEncodedText.Encode(Constants.Properties.IndexingMode);
        public readonly static JsonEncodedText IncludedPaths = JsonEncodedText.Encode(Constants.Properties.IncludedPaths);
        public readonly static JsonEncodedText ExcludedPaths = JsonEncodedText.Encode(Constants.Properties.ExcludedPaths);
        public readonly static JsonEncodedText CompositeIndexes = JsonEncodedText.Encode(Constants.Properties.CompositeIndexes);
        public readonly static JsonEncodedText SpatialIndexes = JsonEncodedText.Encode(Constants.Properties.SpatialIndexes);
        public readonly static JsonEncodedText PermissionMode = JsonEncodedText.Encode(Constants.Properties.PermissionMode);
        public readonly static JsonEncodedText Token = JsonEncodedText.Encode(Constants.Properties.Token);
        public readonly static JsonEncodedText ResourcePartitionKey = JsonEncodedText.Encode(Constants.Properties.ResourcePartitionKey);
        public readonly static JsonEncodedText PrimaryReadCoefficient = JsonEncodedText.Encode(Constants.Properties.PrimaryReadCoefficient);
        public readonly static JsonEncodedText SecondaryReadCoefficient = JsonEncodedText.Encode(Constants.Properties.SecondaryReadCoefficient);
        public readonly static JsonEncodedText MaxReplicaSetSize = JsonEncodedText.Encode(Constants.Properties.MaxReplicaSetSize);
        public readonly static JsonEncodedText MinReplicaSetSize = JsonEncodedText.Encode(Constants.Properties.MinReplicaSetSize);
        public readonly static JsonEncodedText AsyncReplication = JsonEncodedText.Encode(Constants.Properties.AsyncReplication);
        public readonly static JsonEncodedText Types = JsonEncodedText.Encode(Constants.Properties.Types);
        public readonly static JsonEncodedText Body = JsonEncodedText.Encode(Constants.Properties.Body);
        public readonly static JsonEncodedText TriggerType = JsonEncodedText.Encode(Constants.Properties.TriggerType);
        public readonly static JsonEncodedText TriggerOperation = JsonEncodedText.Encode(Constants.Properties.TriggerOperation);
        public readonly static JsonEncodedText UniqueKeys = JsonEncodedText.Encode(Constants.Properties.UniqueKeys);
        public readonly static JsonEncodedText PermissionsLink = JsonEncodedText.Encode(Constants.Properties.PermissionsLink);
    }
}
