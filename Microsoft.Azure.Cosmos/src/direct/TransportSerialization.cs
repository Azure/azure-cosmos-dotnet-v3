//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Runtime.CompilerServices;
    using Microsoft.Azure.Cosmos.Core.Trace;
#if COSMOSCLIENT
    using Microsoft.Azure.Cosmos.Rntbd;
#endif
    using Microsoft.Azure.Documents.Collections;
    using RequestPool = RntbdConstants.RntbdEntityPool<RntbdConstants.Request, RntbdConstants.RequestIdentifiers>;

    internal static class TransportSerialization
    {
        // Path format
        internal static readonly char[] UrlTrim = { '/' };

        internal static readonly IReadOnlyDictionary<string, Action<object, DocumentServiceRequest, RntbdConstants.Request>> AddHeaders = new Dictionary<string, Action<object, DocumentServiceRequest, RntbdConstants.Request>>(StringComparer.OrdinalIgnoreCase)
        {
            // Properties only scenarios
            { WFConstants.BackendHeaders.BinaryId , (value, documentServiceRequest, rntbdRequest) => TransportSerialization.AddBinaryIdIfPresent(value, rntbdRequest) },
            { WFConstants.BackendHeaders.TransactionCommit, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.TransactionCommit, rntbdRequest.transactionCommit, rntbdRequest) },
            { WFConstants.BackendHeaders.MergeStaticId, (value, documentServiceRequest, rntbdRequest) => TransportSerialization.AddMergeStaticIdIfPresent(value, rntbdRequest) },
            { WFConstants.BackendHeaders.EffectivePartitionKey, (value, documentServiceRequest, rntbdRequest) => TransportSerialization.AddEffectivePartitionKeyIfPresent(value, rntbdRequest) },
            { WFConstants.BackendHeaders.TransactionId, TransportSerialization.AddTransactionMetaData },
            { WFConstants.BackendHeaders.RetriableWriteRequestId, TransportSerialization.AddRetriableWriteRequestMetadata},
            { WFConstants.BackendHeaders.IsRetriedWriteRequest,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.IsRetriedWriteRequest, rntbdRequest.isRetriedWriteRequest, rntbdRequest) },
            { WFConstants.BackendHeaders.RetriableWriteRequestStartTimestamp, TransportSerialization.AddRetriableWriteRequestStartTimestampMetadata},

            // Headers supported by both
            { HttpConstants.HttpHeaders.HttpDate,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.AddHttpDateHeader(headerValue, documentServiceRequest, rntbdRequest) },
            { HttpConstants.HttpHeaders.XDate,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.XDate, rntbdRequest.date, rntbdRequest) },
            { HttpConstants.HttpHeaders.Continuation, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.Continuation, rntbdRequest.continuationToken, rntbdRequest) },
            { HttpConstants.HttpHeaders.IfMatch, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.AddIfMatchHeader(headerValue, documentServiceRequest, rntbdRequest) },
            { HttpConstants.HttpHeaders.IfNoneMatch, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.AddIfNoneMatchHeader(headerValue, documentServiceRequest, rntbdRequest) },
            { HttpConstants.HttpHeaders.IfModifiedSince, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.IfModifiedSince,  rntbdRequest.ifModifiedSince, rntbdRequest) },
            { HttpConstants.HttpHeaders.A_IM, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.A_IM, rntbdRequest.a_IM, rntbdRequest) },
            { WFConstants.BackendHeaders.IsFanoutRequest, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.IsFanoutRequest, rntbdRequest.isFanout, rntbdRequest) },
            { HttpConstants.HttpHeaders.CollectionRemoteStorageSecurityIdentifier, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.CollectionRemoteStorageSecurityIdentifier, rntbdRequest.collectionRemoteStorageSecurityIdentifier, rntbdRequest) },
            { WFConstants.BackendHeaders.ResourceTypes, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.ResourceTypes, rntbdRequest.resourceTypes, rntbdRequest) },

            { HttpConstants.HttpHeaders.EnableScanInQuery, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.EnableScanInQuery, rntbdRequest.enableScanInQuery, rntbdRequest) },
            { HttpConstants.HttpHeaders.EmitVerboseTracesInQuery,(headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, rntbdRequest.emitVerboseTracesInQuery, rntbdRequest) },
            { HttpConstants.HttpHeaders.CanCharge, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.CanCharge, rntbdRequest.canCharge, rntbdRequest) },
            { HttpConstants.HttpHeaders.CanThrottle, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.CanThrottle, rntbdRequest.canThrottle, rntbdRequest) },
            { HttpConstants.HttpHeaders.ProfileRequest, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.ProfileRequest, rntbdRequest.profileRequest, rntbdRequest) },
            { HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, rntbdRequest.enableLowPrecisionOrderBy, rntbdRequest) },
            { HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates, rntbdRequest.supportSpatialLegacyCoordinates, rntbdRequest) },
            { HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere, rntbdRequest.usePolygonsSmallerThanAHemisphere, rntbdRequest) },
            { HttpConstants.HttpHeaders.EnableLogging,(headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.EnableLogging, rntbdRequest.enableLogging, rntbdRequest) },
            { HttpConstants.HttpHeaders.PopulateQuotaInfo, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PopulateQuotaInfo, rntbdRequest.populateQuotaInfo, rntbdRequest) },
            { HttpConstants.HttpHeaders.PopulateResourceCount, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PopulateResourceCount, rntbdRequest.populateResourceCount, rntbdRequest) },
            { HttpConstants.HttpHeaders.DisableRUPerMinuteUsage, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.DisableRUPerMinuteUsage, rntbdRequest.disableRUPerMinuteUsage, rntbdRequest) },
            { HttpConstants.HttpHeaders.PopulateQueryMetrics, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PopulateQueryMetrics, rntbdRequest.populateQueryMetrics, rntbdRequest) },
            { WFConstants.BackendHeaders.ForceQueryScan, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.ForceQueryScan, rntbdRequest.forceQueryScan, rntbdRequest) },
            { HttpConstants.HttpHeaders.PopulatePartitionStatistics, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PopulatePartitionStatistics, rntbdRequest.populatePartitionStatistics, rntbdRequest) },
            { HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo, rntbdRequest.populateCollectionThroughputInfo, rntbdRequest) },
            { WFConstants.BackendHeaders.ShareThroughput, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.ShareThroughput, rntbdRequest.shareThroughput, rntbdRequest) },
            { HttpConstants.HttpHeaders.IsReadOnlyScript,(headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.IsReadOnlyScript, rntbdRequest.isReadOnlyScript, rntbdRequest) },
            #if !COSMOSCLIENT
            { HttpConstants.HttpHeaders.IsAutoScaleRequest, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.IsAutoScaleRequest, rntbdRequest.isAutoScaleRequest, rntbdRequest) },
            #endif
            { HttpConstants.HttpHeaders.CanOfferReplaceComplete, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.CanOfferReplaceComplete, rntbdRequest.canOfferReplaceComplete, rntbdRequest) },
            { HttpConstants.HttpHeaders.IgnoreSystemLoweringMaxThroughput, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.IgnoreSystemLoweringMaxThroughput, rntbdRequest.ignoreSystemLoweringMaxThroughput, rntbdRequest) },
            { WFConstants.BackendHeaders.ExcludeSystemProperties, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.ExcludeSystemProperties, rntbdRequest.excludeSystemProperties, rntbdRequest) },
            { WFConstants.BackendHeaders.UseSystemBudget, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.UseSystemBudget, rntbdRequest.useSystemBudget, rntbdRequest) },
            { WFConstants.BackendHeaders.IsUserRequest, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue,  WFConstants.BackendHeaders.IsUserRequest, rntbdRequest.isUserRequest, rntbdRequest) },
            { HttpConstants.HttpHeaders.PreserveFullContent,(headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PreserveFullContent, rntbdRequest.preserveFullContent, rntbdRequest) },
            { HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest, rntbdRequest.isRUPerGBEnforcementRequest, rntbdRequest) },
            { HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest, rntbdRequest.isofferStorageRefreshRequest, rntbdRequest) },
            { HttpConstants.HttpHeaders.ForceSideBySideIndexMigration, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.ForceSideBySideIndexMigration,rntbdRequest.forceSideBySideIndexMigration, rntbdRequest) },
            { HttpConstants.HttpHeaders.MigrateOfferToManualThroughput, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.MigrateOfferToManualThroughput, rntbdRequest.migrateOfferToManualThroughput, rntbdRequest) },
            { HttpConstants.HttpHeaders.MigrateOfferToAutopilot, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.MigrateOfferToAutopilot, rntbdRequest.migrateOfferToAutopilot, rntbdRequest) },
            { HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics, (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics, rntbdRequest.getAllPartitionKeyStatistics, rntbdRequest) },
            { HttpConstants.HttpHeaders.TruncateMergeLogRequest,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.TruncateMergeLogRequest, rntbdRequest.truncateMergeLogRequest, rntbdRequest) },

            { HttpConstants.HttpHeaders.PageSize, TransportSerialization.AddPageSize},
            { HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB, TransportSerialization.AddResponseContinuationTokenLimitInKb },
            { WFConstants.BackendHeaders.RemoteStorageType, TransportSerialization.AddRemoteStorageType },
            { WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes, TransportSerialization.AddCollectionChildResourceNameLimitInBytes },
            { WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB, TransportSerialization.AddCollectionChildResourceContentLengthLimitInKB },
            { WFConstants.BackendHeaders.UniqueIndexNameEncodingMode, TransportSerialization.AddUniqueIndexNameEncodingMode },
            { WFConstants.BackendHeaders.UniqueIndexReIndexingState, TransportSerialization.AddUniqueIndexReIndexingState },
            { HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned, TransportSerialization.AddUpdateMaxthroughputEverProvisioned },
            { HttpConstants.HttpHeaders.IndexingDirective, TransportSerialization.AddIndexingDirectiveHeader },
            { HttpConstants.HttpHeaders.MigrateCollectionDirective, TransportSerialization.AddMigrateCollectionDirectiveHeader },
            { HttpConstants.HttpHeaders.ConsistencyLevel, TransportSerialization.AddConsistencyLevelHeader },
            { WFConstants.BackendHeaders.FanoutOperationState, TransportSerialization.AddFanoutOperationStateHeader },
            { HttpConstants.HttpHeaders.SystemDocumentType, TransportSerialization.AddSystemDocumentTypeHeader },
            { HttpConstants.HttpHeaders.ContentSerializationFormat, TransportSerialization.AddContentSerializationFormat },
            { HttpConstants.HttpHeaders.Prefer, TransportSerialization.AddReturnPreferenceIfPresent },
            { HttpConstants.HttpHeaders.EnumerationDirection,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.AddEnumerationDirection(headerValue, rntbdRequest) },
            { HttpConstants.HttpHeaders.ReadFeedKeyType, TransportSerialization.AddStartAndEndKeys },
            // StartId, EndId, StartEpk, EndEpk are all set by ReadFeedKeyType header in AddStartAndEndKeys
            { HttpConstants.HttpHeaders.StartId, (a, b, c) => { } },
            { HttpConstants.HttpHeaders.EndId, (a, b, c) => { }  },
            { HttpConstants.HttpHeaders.StartEpk, (a, b, c) => { }  },
            { HttpConstants.HttpHeaders.EndEpk, (a, b, c) => { }  },

            { HttpConstants.HttpHeaders.Authorization,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.Authorization, rntbdRequest.authorizationToken, rntbdRequest)},
            { HttpConstants.HttpHeaders.SessionToken,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.SessionToken, rntbdRequest.sessionToken, rntbdRequest)},
            { HttpConstants.HttpHeaders.PreTriggerInclude,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PreTriggerInclude, rntbdRequest.preTriggerInclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PreTriggerExclude,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PreTriggerExclude, rntbdRequest.preTriggerExclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PostTriggerInclude,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PostTriggerInclude, rntbdRequest.postTriggerInclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PostTriggerExclude,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PostTriggerExclude, rntbdRequest.postTriggerExclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PartitionKey,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PartitionKey, rntbdRequest.partitionKey, rntbdRequest)},
            { HttpConstants.HttpHeaders.PartitionKeyRangeId,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.PartitionKeyRangeId, rntbdRequest.partitionKeyRangeId, rntbdRequest)},
            { HttpConstants.HttpHeaders.ResourceTokenExpiry,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.ResourceTokenExpiry, rntbdRequest.resourceTokenExpiry, rntbdRequest)},
            { HttpConstants.HttpHeaders.FilterBySchemaResourceId,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.FilterBySchemaResourceId, rntbdRequest.filterBySchemaRid, rntbdRequest)},
            { HttpConstants.HttpHeaders.ShouldBatchContinueOnError,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.ShouldBatchContinueOnError, rntbdRequest.shouldBatchContinueOnError, rntbdRequest)},
            { HttpConstants.HttpHeaders.IsBatchOrdered,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.IsBatchOrdered, rntbdRequest.isBatchOrdered, rntbdRequest)},
            { HttpConstants.HttpHeaders.IsBatchAtomic,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.IsBatchAtomic, rntbdRequest.isBatchAtomic, rntbdRequest)},
            { WFConstants.BackendHeaders.CollectionPartitionIndex,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.CollectionPartitionIndex, rntbdRequest.collectionPartitionIndex, rntbdRequest)},
            { WFConstants.BackendHeaders.CollectionServiceIndex,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.CollectionServiceIndex, rntbdRequest.collectionServiceIndex, rntbdRequest)},
            { WFConstants.BackendHeaders.ResourceSchemaName,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.ResourceSchemaName, rntbdRequest.resourceSchemaName, rntbdRequest)},
            { WFConstants.BackendHeaders.BindReplicaDirective,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.BindReplicaDirective, rntbdRequest.bindReplicaDirective, rntbdRequest)},
            { WFConstants.BackendHeaders.PrimaryMasterKey,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.PrimaryMasterKey, rntbdRequest.primaryMasterKey, rntbdRequest)},
            { WFConstants.BackendHeaders.SecondaryMasterKey,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.SecondaryMasterKey, rntbdRequest.secondaryMasterKey, rntbdRequest)},
            { WFConstants.BackendHeaders.PrimaryReadonlyKey,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.PrimaryReadonlyKey, rntbdRequest.primaryReadonlyKey, rntbdRequest)},
            { WFConstants.BackendHeaders.SecondaryReadonlyKey,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.SecondaryReadonlyKey, rntbdRequest.secondaryReadonlyKey, rntbdRequest)},
            { WFConstants.BackendHeaders.PartitionCount,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.PartitionCount, rntbdRequest.partitionCount, rntbdRequest)},
            { WFConstants.BackendHeaders.CollectionRid,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.CollectionRid, rntbdRequest.collectionRid, rntbdRequest)},
            { HttpConstants.HttpHeaders.GatewaySignature,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.GatewaySignature, rntbdRequest.gatewaySignature, rntbdRequest)},
            { HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, rntbdRequest.remainingTimeInMsOnClientRequest, rntbdRequest)},
            { HttpConstants.HttpHeaders.ClientRetryAttemptCount,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.ClientRetryAttemptCount, rntbdRequest.clientRetryAttemptCount, rntbdRequest)},
            { HttpConstants.HttpHeaders.TargetLsn,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.TargetLsn, rntbdRequest.targetLsn, rntbdRequest)},
            { HttpConstants.HttpHeaders.TargetGlobalCommittedLsn,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, rntbdRequest.targetGlobalCommittedLsn, rntbdRequest)},
            { HttpConstants.HttpHeaders.TransportRequestID,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.TransportRequestID, rntbdRequest.transportRequestID, rntbdRequest)},
            { HttpConstants.HttpHeaders.RestoreMetadataFilter,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.RestoreMetadataFilter, rntbdRequest.restoreMetadataFilter, rntbdRequest)},
            { WFConstants.BackendHeaders.RestoreParams,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.RestoreParams, rntbdRequest.restoreParams, rntbdRequest)},
            { WFConstants.BackendHeaders.PartitionResourceFilter,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.PartitionResourceFilter, rntbdRequest.partitionResourceFilter, rntbdRequest)},
            { WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation, rntbdRequest.enableDynamicRidRangeAllocation, rntbdRequest)},
            { WFConstants.BackendHeaders.SchemaOwnerRid,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.SchemaOwnerRid, rntbdRequest.schemaOwnerRid, rntbdRequest)},
            { WFConstants.BackendHeaders.SchemaHash,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.SchemaHash, rntbdRequest.schemaHash, rntbdRequest)},
            { HttpConstants.HttpHeaders.IsClientEncrypted,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.IsClientEncrypted, rntbdRequest.isClientEncrypted, rntbdRequest)},
            { WFConstants.BackendHeaders.TimeToLiveInSeconds,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.TimeToLiveInSeconds, rntbdRequest.timeToLiveInSeconds, rntbdRequest)},

            { WFConstants.BackendHeaders.BinaryPassthroughRequest,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.BinaryPassthroughRequest, rntbdRequest.binaryPassthroughRequest, rntbdRequest)},
            { WFConstants.BackendHeaders.AllowTentativeWrites,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.AllowTentativeWrites, rntbdRequest.allowTentativeWrites, rntbdRequest)},
            { HttpConstants.HttpHeaders.IncludeTentativeWrites,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.IncludeTentativeWrites, rntbdRequest.includeTentativeWrites, rntbdRequest)},

            { HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds, rntbdRequest.maxPollingIntervalMilliseconds, rntbdRequest)},
            { WFConstants.BackendHeaders.PopulateLogStoreInfo,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.PopulateLogStoreInfo, rntbdRequest.populateLogStoreInfo, rntbdRequest)},
            { WFConstants.BackendHeaders.MergeCheckPointGLSN,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.MergeCheckPointGLSN, rntbdRequest.mergeCheckpointGlsnKeyName, rntbdRequest)},
            { WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount, rntbdRequest.populateUnflushedMergeEntryCount, rntbdRequest)},
            { WFConstants.BackendHeaders.AddResourcePropertiesToResponse,  (headerValue, documentServiceRequest, rntbdRequest) =>TransportSerialization.FillTokenWithValue(headerValue, WFConstants.BackendHeaders.AddResourcePropertiesToResponse, rntbdRequest.addResourcePropertiesToResponse, rntbdRequest)},
            { HttpConstants.HttpHeaders.ChangeFeedStartFullFidelityIfNoneMatch,  (headerValue, documentServiceRequest, rntbdRequest) => TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.ChangeFeedStartFullFidelityIfNoneMatch, rntbdRequest.changeFeedStartFullFidelityIfNoneMatch, rntbdRequest)},

            // will be null in case of direct, which is fine - BE will use the value from the connection context message.
            // When this is used in Gateway, the header value will be populated with the proxied HTTP request's header, and
            // BE will respect the per-request value.
            { HttpConstants.HttpHeaders.Version,  (headerValue, documentServiceRequest, rntbdRequest) =>TransportSerialization.FillTokenWithValue(headerValue, HttpConstants.HttpHeaders.Version, rntbdRequest.clientVersion, rntbdRequest) },

        };

        internal class RntbdHeader
        {
            public RntbdHeader(StatusCodes status, Guid activityId)
            {
                this.Status = status;
                this.ActivityId = activityId;
            }

            public StatusCodes Status { get; private set; }
            public Guid ActivityId { get; private set; }
        }

        internal static byte[] BuildRequest(
            DocumentServiceRequest request,
            string replicaPath,
            ResourceOperation resourceOperation,
            Guid activityId,
            out int headerSize,
            out int bodySize)
        {
            RntbdConstants.RntbdOperationType operationType = GetRntbdOperationType(resourceOperation.operationType);
            RntbdConstants.RntbdResourceType resourceType = GetRntbdResourceType(resourceOperation.resourceType);

            using RequestPool.EntityOwner owner = RequestPool.Instance.Get();
            RntbdConstants.Request rntbdRequest = owner.Entity;

            rntbdRequest.replicaPath.value.valueBytes = BytesSerializer.GetBytesForString(replicaPath, rntbdRequest);
            rntbdRequest.replicaPath.isPresent = true;

            TransportSerialization.AddResourceIdOrPathHeaders(request, rntbdRequest);
            TransportSerialization.AddEntityId(request, rntbdRequest);

            // special-case headers (ones that don't come from request.headers, or ones that are a merge of
            // merging multiple request.headers, or ones that are parsed from a string to an enum).
            if (request.Properties != null)
            {
                foreach (KeyValuePair<string, object> keyValuePair in request.Properties)
                {
                    if (TransportSerialization.AddHeaders.TryGetValue(keyValuePair.Key, out Action<object, DocumentServiceRequest, RntbdConstants.Request> setHeader))
                    {
                        setHeader(keyValuePair.Value, request, rntbdRequest);
                    }
                }
            }

            // Headers take priority over properties
            foreach (string headerKey in request.Headers.Keys())
            {
                if (TransportSerialization.AddHeaders.TryGetValue(headerKey, out Action<object, DocumentServiceRequest, RntbdConstants.Request> setHeader))
                {
                    setHeader(request.Headers[headerKey], request, rntbdRequest);
                }
            }

            int metadataLength = sizeof(uint) + sizeof(ushort) + sizeof(ushort) + BytesSerializer.GetSizeOfGuid();
            int headerAndMetadataLength = metadataLength;

            int allocationLength = 0;

            int bodyLength = 0;
            CloneableStream clonedStream = null;
            if (request.CloneableBody != null)
            {
                clonedStream = request.CloneableBody.Clone();
                bodyLength = (int)clonedStream.Length;
            }

            byte[] contextMessage;
            using (clonedStream)
            {
                if (bodyLength > 0)
                {
                    allocationLength += sizeof(uint);
                    allocationLength += (int)bodyLength;

                    rntbdRequest.payloadPresent.value.valueByte = 0x01;
                    rntbdRequest.payloadPresent.isPresent = true;
                }
                else
                {
                    rntbdRequest.payloadPresent.value.valueByte = 0x00;
                    rntbdRequest.payloadPresent.isPresent = true;
                }

                // Once all metadata tokens are set, we can calculate the length.
                headerAndMetadataLength += rntbdRequest.CalculateLength(); // metadata tokens
                allocationLength += headerAndMetadataLength;

                contextMessage = new byte[allocationLength];

                BytesSerializer writer = new BytesSerializer(contextMessage);

                // header
                writer.Write((uint)headerAndMetadataLength);
                writer.Write((ushort)resourceType);
                writer.Write((ushort)operationType);
                writer.Write(activityId);
                int actualWritten = metadataLength;

                // metadata
                rntbdRequest.SerializeToBinaryWriter(ref writer, out int tokensLength);
                actualWritten += tokensLength;

                if (actualWritten != headerAndMetadataLength)
                {
                    DefaultTrace.TraceCritical(
                        "Bug in RNTBD token serialization. Calculated header size: {0}. Actual header size: {1}",
                        headerAndMetadataLength, actualWritten);
                    throw new InternalServerErrorException();
                }

                if (bodyLength > 0)
                {
                    ArraySegment<byte> buffer = clonedStream.GetBuffer();
                    writer.Write((UInt32)bodyLength);
                    writer.Write(buffer);
                }
            }

            headerSize = headerAndMetadataLength;
            bodySize = sizeof(UInt32) + bodyLength;

            const int HeaderSizeWarningThreshold = 128 * 1024;
            const int BodySizeWarningThreshold = 2 * 1024 * 1024;
            if (headerSize > HeaderSizeWarningThreshold)
            {
                DefaultTrace.TraceWarning(
                    "The request header is large. Header size: {0}. Warning threshold: {1}. " +
                    "RID: {2}. Resource type: {3}. Operation: {4}. Address: {5}",
                    headerSize, HeaderSizeWarningThreshold, request.ResourceAddress,
                    request.ResourceType, resourceOperation, replicaPath);
            }
            if (bodySize > BodySizeWarningThreshold)
            {
                DefaultTrace.TraceWarning(
                    "The request body is large. Body size: {0}. Warning threshold: {1}. " +
                    "RID: {2}. Resource type: {3}. Operation: {4}. Address: {5}",
                    bodySize, BodySizeWarningThreshold, request.ResourceAddress,
                    request.ResourceType, resourceOperation, replicaPath);
            }

            return contextMessage;
        }

        internal static byte[] BuildContextRequest(Guid activityId, UserAgentContainer userAgent, RntbdConstants.CallerId callerId)
        {
            byte[] activityIdBytes = activityId.ToByteArray();

            RntbdConstants.ConnectionContextRequest request = new RntbdConstants.ConnectionContextRequest();
            request.protocolVersion.value.valueULong = RntbdConstants.CurrentProtocolVersion;
            request.protocolVersion.isPresent = true;

            request.clientVersion.value.valueBytes = HttpConstants.Versions.CurrentVersionUTF8;
            request.clientVersion.isPresent = true;

            request.userAgent.value.valueBytes = userAgent.UserAgentUTF8;
            request.userAgent.isPresent = true;

            request.callerId.isPresent = false;
            if (callerId == RntbdConstants.CallerId.Gateway)
            {
                request.callerId.value.valueByte = (byte)callerId;
                request.callerId.isPresent = true;
            }

            int length = sizeof(UInt32) + sizeof(UInt16) + sizeof(UInt16) + activityIdBytes.Length; // header
            length += request.CalculateLength(); // tokens

            byte[] contextMessage = new byte[length];

            BytesSerializer writer = new BytesSerializer(contextMessage);

            // header
            writer.Write(length);
            writer.Write((ushort)RntbdConstants.RntbdResourceType.Connection);
            writer.Write((ushort)RntbdConstants.RntbdOperationType.Connection);
            writer.Write(activityIdBytes);

            // metadata
            request.SerializeToBinaryWriter(ref writer, out _);

            return contextMessage;
        }

        internal static StoreResponse MakeStoreResponse(
            StatusCodes status,
            Guid activityId,
            RntbdConstants.Response response,
            Stream body,
            string serverVersion)
        {
            StoreResponse storeResponse = new StoreResponse()
            {
                Headers = new StoreResponseNameValueCollection()
            };

            TransportSerialization.AddResponseStringHeaderIfPresent(response.lastStateChangeDateTime, HttpConstants.HttpHeaders.LastStateChangeUtc, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.continuationToken, HttpConstants.HttpHeaders.Continuation, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.eTag, HttpConstants.HttpHeaders.ETag, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.retryAfterMilliseconds, HttpConstants.HttpHeaders.RetryAfterInMilliseconds, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.storageMaxResoureQuota, HttpConstants.HttpHeaders.MaxResourceQuota, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.storageResourceQuotaUsage, HttpConstants.HttpHeaders.CurrentResourceQuotaUsage, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.collectionPartitionIndex, WFConstants.BackendHeaders.CollectionPartitionIndex, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.collectionServiceIndex, WFConstants.BackendHeaders.CollectionServiceIndex, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.LSN, WFConstants.BackendHeaders.LSN, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.itemCount, HttpConstants.HttpHeaders.ItemCount, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.schemaVersion, HttpConstants.HttpHeaders.SchemaVersion, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.ownerFullName, HttpConstants.HttpHeaders.OwnerFullName, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.ownerId, HttpConstants.HttpHeaders.OwnerId, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.databaseAccountId, WFConstants.BackendHeaders.DatabaseAccountId, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.quorumAckedLSN, WFConstants.BackendHeaders.QuorumAckedLSN, storeResponse.Headers);
            TransportSerialization.AddResponseByteHeaderIfPresent(response.requestValidationFailure, WFConstants.BackendHeaders.RequestValidationFailure, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.subStatus, WFConstants.BackendHeaders.SubStatus, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.collectionUpdateProgress, HttpConstants.HttpHeaders.CollectionIndexTransformationProgress, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.currentWriteQuorum, WFConstants.BackendHeaders.CurrentWriteQuorum, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.currentReplicaSetSize, WFConstants.BackendHeaders.CurrentReplicaSetSize, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.collectionLazyIndexProgress, HttpConstants.HttpHeaders.CollectionLazyIndexingProgress, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.partitionKeyRangeId, WFConstants.BackendHeaders.PartitionKeyRangeId, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.logResults, HttpConstants.HttpHeaders.LogResults, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.xpRole, WFConstants.BackendHeaders.XPRole, storeResponse.Headers);
            TransportSerialization.AddResponseByteHeaderIfPresent(response.isRUPerMinuteUsed, WFConstants.BackendHeaders.IsRUPerMinuteUsed, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.queryMetrics, WFConstants.BackendHeaders.QueryMetrics, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.queryExecutionInfo, WFConstants.BackendHeaders.QueryExecutionInfo, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.indexUtilization, WFConstants.BackendHeaders.IndexUtilization, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.globalCommittedLSN, WFConstants.BackendHeaders.GlobalCommittedLSN, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.numberOfReadRegions, WFConstants.BackendHeaders.NumberOfReadRegions, storeResponse.Headers);
            TransportSerialization.AddResponseBoolHeaderIfPresent(response.offerReplacePending, WFConstants.BackendHeaders.OfferReplacePending, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.itemLSN, WFConstants.BackendHeaders.ItemLSN, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.restoreState, WFConstants.BackendHeaders.RestoreState, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.collectionSecurityIdentifier, WFConstants.BackendHeaders.CollectionSecurityIdentifier, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.transportRequestID, HttpConstants.HttpHeaders.TransportRequestID, storeResponse.Headers);
            TransportSerialization.AddResponseBoolHeaderIfPresent(response.shareThroughput, WFConstants.BackendHeaders.ShareThroughput, storeResponse.Headers);
            TransportSerialization.AddResponseBoolHeaderIfPresent(response.disableRntbdChannel, HttpConstants.HttpHeaders.DisableRntbdChannel, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.serverDateTimeUtc, HttpConstants.HttpHeaders.XDate, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.localLSN, WFConstants.BackendHeaders.LocalLSN, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.quorumAckedLocalLSN, WFConstants.BackendHeaders.QuorumAckedLocalLSN, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.itemLocalLSN, WFConstants.BackendHeaders.ItemLocalLSN, storeResponse.Headers);
            TransportSerialization.AddResponseBoolHeaderIfPresent(response.hasTentativeWrites, WFConstants.BackendHeaders.HasTentativeWrites, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.sessionToken, HttpConstants.HttpHeaders.SessionToken, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.replicatorLSNToGLSNDelta, WFConstants.BackendHeaders.ReplicatorLSNToGLSNDelta, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.replicatorLSNToLLSNDelta, WFConstants.BackendHeaders.ReplicatorLSNToLLSNDelta, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.vectorClockLocalProgress, WFConstants.BackendHeaders.VectorClockLocalProgress, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.minimumRUsForOffer, WFConstants.BackendHeaders.MinimumRUsForOffer, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.xpConfigurationSesssionsCount, WFConstants.BackendHeaders.XPConfigurationSessionsCount, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.unflushedMergeLogEntryCount, WFConstants.BackendHeaders.UnflushedMergLogEntryCount, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.resourceName, WFConstants.BackendHeaders.ResourceId, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.timeToLiveInSeconds, WFConstants.BackendHeaders.TimeToLiveInSeconds, storeResponse.Headers);
            TransportSerialization.AddResponseBoolHeaderIfPresent(response.replicaStatusRevoked, WFConstants.BackendHeaders.ReplicaStatusRevoked, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.softMaxAllowedThroughput, WFConstants.BackendHeaders.SoftMaxAllowedThroughput, storeResponse.Headers);
            TransportSerialization.AddResponseDoubleHeaderIfPresent(response.backendRequestDurationMilliseconds, HttpConstants.HttpHeaders.BackendRequestDurationMilliseconds, storeResponse.Headers);

            if (response.requestCharge.isPresent)
            {
                storeResponse.Headers[HttpConstants.HttpHeaders.RequestCharge] = string.Format(CultureInfo.InvariantCulture, "{0:0.##}", response.requestCharge.value.valueDouble);
            }

            if (response.indexingDirective.isPresent)
            {
                string indexingDirective;
                switch (response.indexingDirective.value.valueByte)
                {
                    case (byte)RntbdConstants.RntbdIndexingDirective.Default:
                        indexingDirective = IndexingDirectiveStrings.Default;
                        break;
                    case (byte)RntbdConstants.RntbdIndexingDirective.Exclude:
                        indexingDirective = IndexingDirectiveStrings.Exclude;
                        break;
                    case (byte)RntbdConstants.RntbdIndexingDirective.Include:
                        indexingDirective = IndexingDirectiveStrings.Include;
                        break;
                    default:
                        throw new Exception();
                }

                storeResponse.Headers[HttpConstants.HttpHeaders.IndexingDirective] = indexingDirective;
            }

            storeResponse.Headers[HttpConstants.HttpHeaders.ServerVersion] = serverVersion;

            storeResponse.Headers[HttpConstants.HttpHeaders.ActivityId] = activityId.ToString();

            storeResponse.ResponseBody = body;
            storeResponse.Status = (int)status;

            return storeResponse;
        }

        private static void AddHttpDateHeader(object value, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!(value is string httpDateString))
            {
                throw new ArgumentOutOfRangeException(HttpConstants.HttpHeaders.HttpDate);
            }

            // XDate takes priority over HttpDate
            string xDateHeader = request.Headers[HttpConstants.HttpHeaders.XDate];
            if (string.IsNullOrEmpty(xDateHeader) &&
                !string.IsNullOrEmpty(httpDateString))
            {
                rntbdRequest.date.value.valueBytes = BytesSerializer.GetBytesForString(httpDateString, rntbdRequest);
                rntbdRequest.date.isPresent = true;
            }
        }

        internal static RntbdHeader DecodeRntbdHeader(byte[] header)
        {
            StatusCodes status = (StatusCodes)BitConverter.ToUInt32(header, 4);
            Guid activityId = BytesSerializer.ReadGuidFromBytes(new ArraySegment<byte>(header, 8, 16));
            return new RntbdHeader(status, activityId);
        }

        private static void AddResponseByteHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = token.value.valueByte.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void AddResponseBoolHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = (token.value.valueByte != 0).ToString().ToLowerInvariant();
            }
        }

        private static unsafe void AddResponseStringHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = BytesSerializer.GetStringFromBytes(token.value.valueBytes);
            }
        }

        private static void AddResponseULongHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = token.value.valueULong.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void AddResponseDoubleHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = token.value.valueDouble.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void AddResponseFloatHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = token.value.valueFloat.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void AddResponseLongLongHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = token.value.valueLongLong.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static RntbdConstants.RntbdOperationType GetRntbdOperationType(OperationType operationType)
        {
            switch (operationType)
            {
                case OperationType.Create:
                    return RntbdConstants.RntbdOperationType.Create;
                case OperationType.Delete:
                    return RntbdConstants.RntbdOperationType.Delete;
                case OperationType.ExecuteJavaScript:
                    return RntbdConstants.RntbdOperationType.ExecuteJavaScript;
                case OperationType.Query:
                    return RntbdConstants.RntbdOperationType.Query;
                case OperationType.Read:
                    return RntbdConstants.RntbdOperationType.Read;
                case OperationType.ReadFeed:
                    return RntbdConstants.RntbdOperationType.ReadFeed;
                case OperationType.Replace:
                    return RntbdConstants.RntbdOperationType.Replace;
                case OperationType.SqlQuery:
                    return RntbdConstants.RntbdOperationType.SQLQuery;
                case OperationType.Patch:
                    return RntbdConstants.RntbdOperationType.Patch;
                case OperationType.Head:
                    return RntbdConstants.RntbdOperationType.Head;
                case OperationType.HeadFeed:
                    return RntbdConstants.RntbdOperationType.HeadFeed;
                case OperationType.Upsert:
                    return RntbdConstants.RntbdOperationType.Upsert;
                case OperationType.BatchApply:
                    return RntbdConstants.RntbdOperationType.BatchApply;
                case OperationType.Batch:
                    return RntbdConstants.RntbdOperationType.Batch;
                case OperationType.CompleteUserTransaction:
                    return RntbdConstants.RntbdOperationType.CompleteUserTransaction;
#if !COSMOSCLIENT
            case OperationType.Crash:
                return RntbdConstants.RntbdOperationType.Crash;
            case OperationType.Pause:
                return RntbdConstants.RntbdOperationType.Pause;
            case OperationType.Recreate:
                return RntbdConstants.RntbdOperationType.Recreate;
            case OperationType.Recycle:
                return RntbdConstants.RntbdOperationType.Recycle;
            case OperationType.Resume:
                return RntbdConstants.RntbdOperationType.Resume;
            case OperationType.Stop:
                return RntbdConstants.RntbdOperationType.Stop;
            case OperationType.ForceConfigRefresh:
                return RntbdConstants.RntbdOperationType.ForceConfigRefresh;
            case OperationType.Throttle:
                return RntbdConstants.RntbdOperationType.Throttle;
            case OperationType.PreCreateValidation:
                return RntbdConstants.RntbdOperationType.PreCreateValidation;
            case OperationType.GetSplitPoint:
                return RntbdConstants.RntbdOperationType.GetSplitPoint;
            case OperationType.AbortSplit:
                return RntbdConstants.RntbdOperationType.AbortSplit;
            case OperationType.CompleteSplit:
                return RntbdConstants.RntbdOperationType.CompleteSplit;
            case OperationType.CompleteMergeOnMaster:
                return RntbdConstants.RntbdOperationType.CompleteMergeOnMaster;
            case OperationType.CompleteMergeOnTarget:
                 return RntbdConstants.RntbdOperationType.CompleteMergeOnTarget;
            case OperationType.OfferUpdateOperation:
                return RntbdConstants.RntbdOperationType.OfferUpdateOperation;
            case OperationType.OfferPreGrowValidation:
                return RntbdConstants.RntbdOperationType.OfferPreGrowValidation;
            case OperationType.BatchReportThroughputUtilization:
                return RntbdConstants.RntbdOperationType.BatchReportThroughputUtilization;
            case OperationType.AbortPartitionMigration:
                return RntbdConstants.RntbdOperationType.AbortPartitionMigration;
            case OperationType.CompletePartitionMigration:
                return RntbdConstants.RntbdOperationType.CompletePartitionMigration;
            case OperationType.PreReplaceValidation:
                return RntbdConstants.RntbdOperationType.PreReplaceValidation;
            case OperationType.MigratePartition:
                return RntbdConstants.RntbdOperationType.MigratePartition;
            case OperationType.MasterReplaceOfferOperation:
                return RntbdConstants.RntbdOperationType.MasterReplaceOfferOperation;
            case OperationType.ProvisionedCollectionOfferUpdateOperation:
                return RntbdConstants.RntbdOperationType.ProvisionedCollectionOfferUpdateOperation;
            case OperationType.InitiateDatabaseOfferPartitionShrink:
                return RntbdConstants.RntbdOperationType.InitiateDatabaseOfferPartitionShrink;
            case OperationType.CompleteDatabaseOfferPartitionShrink:
                return RntbdConstants.RntbdOperationType.CompleteDatabaseOfferPartitionShrink;
            case OperationType.EnsureSnapshotOperation:
                return RntbdConstants.RntbdOperationType.EnsureSnapshotOperation;
            case OperationType.GetSplitPoints:
                return RntbdConstants.RntbdOperationType.GetSplitPoints;
            case OperationType.ForcePartitionBackup:
                return RntbdConstants.RntbdOperationType.ForcePartitionBackup;
            case OperationType.MasterInitiatedProgressCoordination:
                return RntbdConstants.RntbdOperationType.MasterInitiatedProgressCoordination;
            case OperationType.MetadataCheckAccess:
                return RntbdConstants.RntbdOperationType.MetadataCheckAccess;
#endif
                case OperationType.AddComputeGatewayRequestCharges:
                    return RntbdConstants.RntbdOperationType.AddComputeGatewayRequestCharges;
                default:
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, "Invalid operation type: {0}", operationType),
                        "operationType");
            }
        }

        private static RntbdConstants.RntbdResourceType GetRntbdResourceType(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.Attachment:
                    return RntbdConstants.RntbdResourceType.Attachment;
                case ResourceType.Collection:
                    return RntbdConstants.RntbdResourceType.Collection;
                case ResourceType.Conflict:
                    return RntbdConstants.RntbdResourceType.Conflict;
                case ResourceType.Database:
                    return RntbdConstants.RntbdResourceType.Database;
                case ResourceType.Document:
                    return RntbdConstants.RntbdResourceType.Document;
                case ResourceType.Record:
                    return RntbdConstants.RntbdResourceType.Record;
                case ResourceType.Permission:
                    return RntbdConstants.RntbdResourceType.Permission;
                case ResourceType.StoredProcedure:
                    return RntbdConstants.RntbdResourceType.StoredProcedure;
                case ResourceType.Trigger:
                    return RntbdConstants.RntbdResourceType.Trigger;
                case ResourceType.User:
                    return RntbdConstants.RntbdResourceType.User;
                case ResourceType.ClientEncryptionKey:
                    return RntbdConstants.RntbdResourceType.ClientEncryptionKey;
                case ResourceType.UserDefinedType:
                    return RntbdConstants.RntbdResourceType.UserDefinedType;
                case ResourceType.UserDefinedFunction:
                    return RntbdConstants.RntbdResourceType.UserDefinedFunction;
                case ResourceType.Offer:
                    return RntbdConstants.RntbdResourceType.Offer;
                case ResourceType.DatabaseAccount:
                    return RntbdConstants.RntbdResourceType.DatabaseAccount;
                case ResourceType.PartitionKeyRange:
                    return RntbdConstants.RntbdResourceType.PartitionKeyRange;
                case ResourceType.Schema:
                    return RntbdConstants.RntbdResourceType.Schema;
                case ResourceType.BatchApply:
                    return RntbdConstants.RntbdResourceType.BatchApply;
                case ResourceType.ComputeGatewayCharges:
                    return RntbdConstants.RntbdResourceType.ComputeGatewayCharges;
                case ResourceType.PartitionKey:
                    return RntbdConstants.RntbdResourceType.PartitionKey;
                case ResourceType.PartitionedSystemDocument:
                    return RntbdConstants.RntbdResourceType.PartitionedSystemDocument;
                case ResourceType.RoleDefinition:
                    return RntbdConstants.RntbdResourceType.RoleDefinition;
                case ResourceType.RoleAssignment:
                    return RntbdConstants.RntbdResourceType.RoleAssignment;
                case ResourceType.Transaction:
                    return RntbdConstants.RntbdResourceType.Transaction;
#if !COSMOSCLIENT
                case ResourceType.Module:
                return RntbdConstants.RntbdResourceType.Module;
            case ResourceType.ModuleCommand:
                return RntbdConstants.RntbdResourceType.ModuleCommand;
            case ResourceType.Replica:
                return RntbdConstants.RntbdResourceType.Replica;
            case ResourceType.PartitionSetInformation:
                return RntbdConstants.RntbdResourceType.PartitionSetInformation;
            case ResourceType.XPReplicatorAddress:
                return RntbdConstants.RntbdResourceType.XPReplicatorAddress;
            case ResourceType.MasterPartition:
                return RntbdConstants.RntbdResourceType.MasterPartition;
            case ResourceType.ServerPartition:
                return RntbdConstants.RntbdResourceType.ServerPartition;
            case ResourceType.Topology:
                return RntbdConstants.RntbdResourceType.Topology;
            case ResourceType.RestoreMetadata:
                return RntbdConstants.RntbdResourceType.RestoreMetadata;
            case ResourceType.RidRange:
                return RntbdConstants.RntbdResourceType.RidRange;
            case ResourceType.VectorClock:
                return RntbdConstants.RntbdResourceType.VectorClock;
            case ResourceType.Snapshot:
                    return RntbdConstants.RntbdResourceType.Snapshot;
#endif
                default:
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, "Invalid resource type: {0}", resourceType),
                        "resourceType");
            }
        }

        private static void AddIfMatchHeader(object headerValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (request.OperationType != OperationType.Read &&
                request.OperationType != OperationType.ReadFeed &&
                headerValue is string match &&
                !string.IsNullOrEmpty(match))
            {
                rntbdRequest.match.value.valueBytes = BytesSerializer.GetBytesForString(match, rntbdRequest);
                rntbdRequest.match.isPresent = true;
            }
        }

        private static void AddIfNoneMatchHeader(object headerValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if ((request.OperationType == OperationType.Read ||
                request.OperationType == OperationType.ReadFeed) &&
                headerValue is string match &&
                !string.IsNullOrEmpty(match))
            {
                rntbdRequest.match.value.valueBytes = BytesSerializer.GetBytesForString(match, rntbdRequest);
                rntbdRequest.match.isPresent = true;
            }
        }

        private static void AddResourceIdOrPathHeaders(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.ResourceId))
            {
                // name based can also have ResourceId because gateway might generate it.
                rntbdRequest.resourceId.value.valueBytes = ResourceId.Parse(request.ResourceType, request.ResourceId);
                rntbdRequest.resourceId.isPresent = true;
            }

            if (request.IsNameBased)
            {
                // Assumption: format is like "dbs/dbName/colls/collName/docs/docName" or "/dbs/dbName/colls/collName",
                // not "apps/appName/partitions/partitionKey/replicas/replicaId/dbs/dbName"
                string[] fragments = request.ResourceAddress.Split(
                    TransportSerialization.UrlTrim, StringSplitOptions.RemoveEmptyEntries);

                if (fragments.Length >= 2)
                {
                    switch (fragments[0])
                    {
                        case Paths.DatabasesPathSegment:
                            rntbdRequest.databaseName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[1], rntbdRequest);
                            rntbdRequest.databaseName.isPresent = true;
                            break;
                        case Paths.SnapshotsPathSegment:
                            rntbdRequest.snapshotName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[1], rntbdRequest);
                            rntbdRequest.snapshotName.isPresent = true;
                            break;
                        case Paths.RoleDefinitionsPathSegment:
                            rntbdRequest.roleDefinitionName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[1], rntbdRequest);
                            rntbdRequest.roleDefinitionName.isPresent = true;
                            break;
                        case Paths.RoleAssignmentsPathSegment:
                            rntbdRequest.roleAssignmentName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[1], rntbdRequest);
                            rntbdRequest.roleAssignmentName.isPresent = true;
                            break;
                        default:
                            throw new BadRequestException();
                    }
                }

                if (fragments.Length >= 4)
                {
                    switch (fragments[2])
                    {
                        case Paths.CollectionsPathSegment:
                            rntbdRequest.collectionName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[3], rntbdRequest);
                            rntbdRequest.collectionName.isPresent = true;
                            break;
                        case Paths.ClientEncryptionKeysPathSegment:
                            rntbdRequest.clientEncryptionKeyName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[3], rntbdRequest);
                            rntbdRequest.clientEncryptionKeyName.isPresent = true;
                            break;
                        case Paths.UsersPathSegment:
                            rntbdRequest.userName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[3], rntbdRequest);
                            rntbdRequest.userName.isPresent = true;
                            break;
                        case Paths.UserDefinedTypesPathSegment:
                            rntbdRequest.userDefinedTypeName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[3], rntbdRequest);
                            rntbdRequest.userDefinedTypeName.isPresent = true;
                            break;
                    }
                }

                if (fragments.Length >= 6)
                {
                    switch (fragments[4])
                    {
                        case Paths.DocumentsPathSegment:
                            rntbdRequest.documentName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                            rntbdRequest.documentName.isPresent = true;
                            break;
                        case Paths.StoredProceduresPathSegment:
                            rntbdRequest.storedProcedureName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                            rntbdRequest.storedProcedureName.isPresent = true;
                            break;
                        case Paths.PermissionsPathSegment:
                            rntbdRequest.permissionName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                            rntbdRequest.permissionName.isPresent = true;
                            break;
                        case Paths.UserDefinedFunctionsPathSegment:
                            rntbdRequest.userDefinedFunctionName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                            rntbdRequest.userDefinedFunctionName.isPresent = true;
                            break;
                        case Paths.TriggersPathSegment:
                            rntbdRequest.triggerName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                            rntbdRequest.triggerName.isPresent = true;
                            break;
                        case Paths.ConflictsPathSegment:
                            rntbdRequest.conflictName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                            rntbdRequest.conflictName.isPresent = true;
                            break;
                        case Paths.PartitionKeyRangesPathSegment:
                            rntbdRequest.partitionKeyRangeName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                            rntbdRequest.partitionKeyRangeName.isPresent = true;
                            break;
                        case Paths.SchemasPathSegment:
                            rntbdRequest.schemaName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                            rntbdRequest.schemaName.isPresent = true;
                            break;
                        case Paths.PartitionedSystemDocumentsPathSegment:
                            rntbdRequest.systemDocumentName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                            rntbdRequest.systemDocumentName.isPresent = true;
                            break;
                    }
                }

                if (fragments.Length >= 8)
                {
                    switch (fragments[6])
                    {
                        case Paths.AttachmentsPathSegment:
                            rntbdRequest.attachmentName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[7], rntbdRequest);
                            rntbdRequest.attachmentName.isPresent = true;
                            break;
                    }
                }
            }
        }

        private static void AddBinaryIdIfPresent(object binaryPayload, RntbdConstants.Request rntbdRequest)
        {
            if (!(binaryPayload is byte[] binaryData))
            {
                throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.BinaryId);
            }

            rntbdRequest.binaryId.value.valueBytes = binaryData;
            rntbdRequest.binaryId.isPresent = true;
        }

        private static void AddReturnPreferenceIfPresent(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                if (string.Equals(headerValue, HttpConstants.HttpHeaderValues.PreferReturnMinimal, StringComparison.OrdinalIgnoreCase))
                {
                    rntbdRequest.returnPreference.value.valueByte = (byte)0x01;
                    rntbdRequest.returnPreference.isPresent = true;
                }
                else if (string.Equals(headerValue, HttpConstants.HttpHeaderValues.PreferReturnRepresentation, StringComparison.OrdinalIgnoreCase))
                {
                    rntbdRequest.returnPreference.value.valueByte = (byte)0x00;
                    rntbdRequest.returnPreference.isPresent = true;
                }
            }
        }

        private static void AddEffectivePartitionKeyIfPresent(object binaryPayload, RntbdConstants.Request rntbdRequest)
        {
            if (!(binaryPayload is byte[] binaryData))
            {
                throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.EffectivePartitionKey);
            }

            rntbdRequest.effectivePartitionKey.value.valueBytes = binaryData;
            rntbdRequest.effectivePartitionKey.isPresent = true;
        }

        private static void AddMergeStaticIdIfPresent(object binaryPayload, RntbdConstants.Request rntbdRequest)
        {
            if (!(binaryPayload is byte[] binaryData))
            {
                throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.MergeStaticId);
            }

            rntbdRequest.mergeStaticId.value.valueBytes = binaryData;
            rntbdRequest.mergeStaticId.isPresent = true;
        }
        private static void AddEntityId(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.EntityId))
            {
                rntbdRequest.entityId.value.valueBytes = BytesSerializer.GetBytesForString(request.EntityId, rntbdRequest);
                rntbdRequest.entityId.isPresent = true;
            }
        }

        private static void AddIndexingDirectiveHeader(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                RntbdConstants.RntbdIndexingDirective rntbdDirective = RntbdConstants.RntbdIndexingDirective.Invalid;
                IndexingDirective directive;
                if (!Enum.TryParse(headerValue, true, out directive))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        headerValue, typeof(IndexingDirective).Name));
                }

                switch (directive)
                {
                    case IndexingDirective.Default:
                        rntbdDirective = RntbdConstants.RntbdIndexingDirective.Default;
                        break;
                    case IndexingDirective.Exclude:
                        rntbdDirective = RntbdConstants.RntbdIndexingDirective.Exclude;
                        break;
                    case IndexingDirective.Include:
                        rntbdDirective = RntbdConstants.RntbdIndexingDirective.Include;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            headerValue, typeof(IndexingDirective).Name));
                }

                rntbdRequest.indexingDirective.value.valueByte = (byte)rntbdDirective;
                rntbdRequest.indexingDirective.isPresent = true;
            }
        }

        private static void AddMigrateCollectionDirectiveHeader(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                MigrateCollectionDirective directive;
                if (!Enum.TryParse(headerValue, true, out directive))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        headerValue, typeof(MigrateCollectionDirective).Name));
                }

                RntbdConstants.RntbdMigrateCollectionDirective rntbdDirective;
                switch (directive)
                {
                    case MigrateCollectionDirective.Freeze:
                        rntbdDirective = RntbdConstants.RntbdMigrateCollectionDirective.Freeze;
                        break;
                    case MigrateCollectionDirective.Thaw:
                        rntbdDirective = RntbdConstants.RntbdMigrateCollectionDirective.Thaw;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            headerValue, typeof(MigrateCollectionDirective).Name));
                }

                rntbdRequest.migrateCollectionDirective.value.valueByte = (byte)rntbdDirective;
                rntbdRequest.migrateCollectionDirective.isPresent = true;
            }
        }

        private static void AddConsistencyLevelHeader(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                ConsistencyLevel consistencyLevel;
                if (!Enum.TryParse<ConsistencyLevel>(headerValue, true, out consistencyLevel))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        headerValue, typeof(ConsistencyLevel).Name));
                }

                RntbdConstants.RntbdConsistencyLevel rntbdConsistencyLevel;
                switch (consistencyLevel)
                {
                    case ConsistencyLevel.Strong:
                        rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.Strong;
                        break;
                    case ConsistencyLevel.BoundedStaleness:
                        rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.BoundedStaleness;
                        break;
                    case ConsistencyLevel.Session:
                        rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.Session;
                        break;
                    case ConsistencyLevel.Eventual:
                        rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.Eventual;
                        break;
                    case ConsistencyLevel.ConsistentPrefix:
                        rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.ConsistentPrefix;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            headerValue, typeof(ConsistencyLevel).Name));
                }

                rntbdRequest.consistencyLevel.value.valueByte = (byte)rntbdConsistencyLevel;
                rntbdRequest.consistencyLevel.isPresent = true;
            }
        }

        private static void AddPageSize(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                int valueInt;
                if (!Int32.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueInt))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidPageSize, headerValue));
                }

                if (valueInt == -1)
                {
                    rntbdRequest.pageSize.value.valueULong = UInt32.MaxValue;
                }
                else if (valueInt >= 0)
                {
                    rntbdRequest.pageSize.value.valueULong = (UInt32)valueInt;
                }
                else
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidPageSize, headerValue));
                }

                rntbdRequest.pageSize.isPresent = true;
            }
        }

        private static void AddUpdateMaxthroughputEverProvisioned(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                int valueInt;
                if (!Int32.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueInt))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidUpdateMaxthroughputEverProvisioned, headerValue));
                }

                if (valueInt >= 0)
                {
                    rntbdRequest.updateMaxThroughputEverProvisioned.value.valueULong = (UInt32)valueInt;
                }
                else
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidUpdateMaxthroughputEverProvisioned, headerValue));
                }

                rntbdRequest.updateMaxThroughputEverProvisioned.isPresent = true;
            }
        }

        private static void AddResponseContinuationTokenLimitInKb(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                if (!Int32.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valueInt))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidPageSize, headerValue));
                }

                if (valueInt >= 0)
                {
                    rntbdRequest.responseContinuationTokenLimitInKb.value.valueULong = (UInt32)valueInt;
                }
                else
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidResponseContinuationTokenLimit, headerValue));
                }

                rntbdRequest.responseContinuationTokenLimitInKb.isPresent = true;
            }
        }

        private static void AddRemoteStorageType(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                RntbdConstants.RntbdRemoteStorageType rntbdRemoteStorageType = RntbdConstants.RntbdRemoteStorageType.Invalid;
                if (!Enum.TryParse(headerValue, true, out RemoteStorageType remoteStorageType))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        headerValue, typeof(RemoteStorageType).Name));
                }

                switch (remoteStorageType)
                {
                    case RemoteStorageType.Standard:
                        rntbdRemoteStorageType = RntbdConstants.RntbdRemoteStorageType.Standard;
                        break;
                    case RemoteStorageType.Premium:
                        rntbdRemoteStorageType = RntbdConstants.RntbdRemoteStorageType.Premium;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            headerValue, typeof(RemoteStorageType).Name));
                }

                rntbdRequest.remoteStorageType.value.valueByte = (byte)rntbdRemoteStorageType;
                rntbdRequest.remoteStorageType.isPresent = true;
            }
        }

        private static void AddCollectionChildResourceNameLimitInBytes(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                if (!Int32.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out rntbdRequest.collectionChildResourceNameLimitInBytes.value.valueLong))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        headerValue,
                        WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes));
                }

                rntbdRequest.collectionChildResourceNameLimitInBytes.isPresent = true;
            }
        }

        private static void AddCollectionChildResourceContentLengthLimitInKB(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                if (!Int32.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out rntbdRequest.collectionChildResourceContentLengthLimitInKB.value.valueLong))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        headerValue,
                        WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB));
                }

                rntbdRequest.collectionChildResourceContentLengthLimitInKB.isPresent = true;
            }
        }

        private static void AddUniqueIndexNameEncodingMode(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                if (!Byte.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out rntbdRequest.uniqueIndexNameEncodingMode.value.valueByte))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        headerValue,
                        WFConstants.BackendHeaders.UniqueIndexNameEncodingMode));
                }

                rntbdRequest.uniqueIndexNameEncodingMode.isPresent = true;
            }
        }

        private static void AddUniqueIndexReIndexingState(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                if (!Byte.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out rntbdRequest.uniqueIndexReIndexingState.value.valueByte))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        headerValue,
                        WFConstants.BackendHeaders.UniqueIndexReIndexingState));
                }

                rntbdRequest.uniqueIndexReIndexingState.isPresent = true;
            }
        }

        private static void AddEnumerationDirection(object enumerationDirectionObject, RntbdConstants.Request rntbdRequest)
        {
            byte headerByteValue;
            if (enumerationDirectionObject is string enumerationDirectionString)
            {
                if (string.IsNullOrEmpty(enumerationDirectionString))
                {
                    return;
                }

                RntbdConstants.RntdbEnumerationDirection rntdbEnumerationDirection = RntbdConstants.RntdbEnumerationDirection.Invalid;
                if (!Enum.TryParse(enumerationDirectionString, true, out EnumerationDirection enumerationDirection))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        enumerationDirectionString, nameof(EnumerationDirection)));
                }

                switch (enumerationDirection)
                {
                    case EnumerationDirection.Forward:
                        rntdbEnumerationDirection = RntbdConstants.RntdbEnumerationDirection.Forward;
                        break;
                    case EnumerationDirection.Reverse:
                        rntdbEnumerationDirection = RntbdConstants.RntdbEnumerationDirection.Reverse;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            enumerationDirectionString, typeof(EnumerationDirection).Name));
                }

                headerByteValue = (byte)rntdbEnumerationDirection;
            }
            else
            {
                byte? scanDirection = enumerationDirectionObject as byte?;
                if (scanDirection == null)
                {
                    throw new BadRequestException(
                        String.Format(
                            CultureInfo.CurrentUICulture,
                            RMResources.InvalidEnumValue,
                            HttpConstants.HttpHeaders.EnumerationDirection,
                            nameof(EnumerationDirection)));
                }

                headerByteValue = scanDirection.Value;
            }

            rntbdRequest.enumerationDirection.value.valueByte = headerByteValue;
            rntbdRequest.enumerationDirection.isPresent = true;
        }

        private static void AddStartAndEndKeys(object headerValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerValue is string headerValueString)
            {
                TransportSerialization.AddStartAndEndKeysFromHeaders(headerValueString, request, rntbdRequest);
                return;
            }

            if (!(headerValue is byte))
            {
                throw new ArgumentOutOfRangeException(HttpConstants.HttpHeaders.ReadFeedKeyType);
            }

            rntbdRequest.readFeedKeyType.value.valueByte = (byte)headerValue;
            rntbdRequest.readFeedKeyType.isPresent = true;
            RntbdConstants.RntdbReadFeedKeyType readFeedKeyType = (RntbdConstants.RntdbReadFeedKeyType)headerValue;

            if (readFeedKeyType == RntbdConstants.RntdbReadFeedKeyType.ResourceId)
            {
                TransportSerialization.SetBytesValue(request, HttpConstants.HttpHeaders.StartId, rntbdRequest.StartId);
                TransportSerialization.SetBytesValue(request, HttpConstants.HttpHeaders.EndId, rntbdRequest.EndId);
            }
            else if (readFeedKeyType == RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKey)
            {

                TransportSerialization.SetBytesValue(request, HttpConstants.HttpHeaders.StartEpk, rntbdRequest.StartEpk);
                TransportSerialization.SetBytesValue(request, HttpConstants.HttpHeaders.EndEpk, rntbdRequest.EndEpk);
            }
        }

        private static void AddStartAndEndKeysFromHeaders(string headerValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(headerValue))
            {
                RntbdConstants.RntdbReadFeedKeyType rntdbReadFeedKeyType = RntbdConstants.RntdbReadFeedKeyType.Invalid;
                if (!Enum.TryParse(headerValue, true, out ReadFeedKeyType readFeedKeyType))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        headerValue, nameof(ReadFeedKeyType)));
                }

                switch (readFeedKeyType)
                {
                    case ReadFeedKeyType.ResourceId:
                        rntdbReadFeedKeyType = RntbdConstants.RntdbReadFeedKeyType.ResourceId;
                        break;
                    case ReadFeedKeyType.EffectivePartitionKey:
                        rntdbReadFeedKeyType = RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKey;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            headerValue, typeof(ReadFeedKeyType).Name));
                }

                rntbdRequest.readFeedKeyType.value.valueByte = (byte)rntdbReadFeedKeyType;
                rntbdRequest.readFeedKeyType.isPresent = true;
            }

            string startId = request.Headers[HttpConstants.HttpHeaders.StartId];
            if (!string.IsNullOrEmpty(startId))
            {
                rntbdRequest.StartId.value.valueBytes = System.Convert.FromBase64String(startId);
                rntbdRequest.StartId.isPresent = true;
            }

            string endId = request.Headers[HttpConstants.HttpHeaders.EndId];
            if (!string.IsNullOrEmpty(endId))
            {
                rntbdRequest.EndId.value.valueBytes = System.Convert.FromBase64String(endId);
                rntbdRequest.EndId.isPresent = true;
            }

            string startEpk = request.Headers[HttpConstants.HttpHeaders.StartEpk];
            if (!string.IsNullOrEmpty(startEpk))
            {
                rntbdRequest.StartEpk.value.valueBytes = System.Convert.FromBase64String(startEpk);
                rntbdRequest.StartEpk.isPresent = true;
            }

            string endEpk = request.Headers[HttpConstants.HttpHeaders.EndEpk];
            if (!string.IsNullOrEmpty(endEpk))
            {
                rntbdRequest.EndEpk.value.valueBytes = System.Convert.FromBase64String(endEpk);
                rntbdRequest.EndEpk.isPresent = true;
            }
        }

        private static void SetBytesValue(DocumentServiceRequest request, string headerName, RntbdToken token)
        {
            if (request.Properties.TryGetValue(headerName, out object requestObject))
            {
                if (!(requestObject is byte[] endEpk))
                {
                    throw new ArgumentOutOfRangeException(headerName);
                }

                token.value.valueBytes = endEpk;
                token.isPresent = true;
            }
        }

        private static void AddContentSerializationFormat(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                RntbdConstants.RntbdContentSerializationFormat rntbdContentSerializationFormat = RntbdConstants.RntbdContentSerializationFormat.Invalid;

                if (!Enum.TryParse<ContentSerializationFormat>(headerValue, true, out ContentSerializationFormat contentSerializationFormat))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        headerValue, nameof(ContentSerializationFormat)));
                }

                switch (contentSerializationFormat)
                {
                    case ContentSerializationFormat.JsonText:
                        rntbdContentSerializationFormat = RntbdConstants.RntbdContentSerializationFormat.JsonText;
                        break;
                    case ContentSerializationFormat.CosmosBinary:
                        rntbdContentSerializationFormat = RntbdConstants.RntbdContentSerializationFormat.CosmosBinary;
                        break;
                    case ContentSerializationFormat.HybridRow:
                        rntbdContentSerializationFormat = RntbdConstants.RntbdContentSerializationFormat.HybridRow;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            headerValue, nameof(ContentSerializationFormat)));
                }

                rntbdRequest.contentSerializationFormat.value.valueByte = (byte)rntbdContentSerializationFormat;
                rntbdRequest.contentSerializationFormat.isPresent = true;
            }
        }

        private static void FillTokenWithValue(object headerValue, string headerName, RntbdToken token, RntbdConstants.Request rntbdRequest)
        {
            if (headerValue == null)
            {
                return;
            }

            string headerStringValue = null;
            if (headerValue is string valueString)
            {
                headerStringValue = valueString;
                if (string.IsNullOrEmpty(headerStringValue))
                {
                    return;
                }
            }

            switch (token.GetTokenType())
            {
                case RntbdTokenTypes.SmallString:
                case RntbdTokenTypes.String:
                case RntbdTokenTypes.ULongString:
                    if (headerStringValue == null)
                    {
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, headerValue, headerName));
                    }

                    token.value.valueBytes = BytesSerializer.GetBytesForString(headerStringValue, rntbdRequest);
                    break;
                case RntbdTokenTypes.ULong:
                    uint valueULong;
                    if (headerStringValue != null)
                    {
                        if (!uint.TryParse(headerStringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueULong))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, headerStringValue, headerName));
                        }
                    }
                    else
                    {
                        if (!(headerValue is uint uintValue))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, headerValue, headerName));
                        }

                        valueULong = uintValue;
                    }

                    token.value.valueULong = valueULong;
                    break;
                case RntbdTokenTypes.Long:
                    int valueLong;
                    if (headerStringValue != null)
                    {
                        if (!int.TryParse(headerStringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueLong))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, headerStringValue, headerName));
                        }
                    }
                    else
                    {
                        if (!(headerValue is int intValue))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, headerValue, headerName));
                        }

                        valueLong = intValue;
                    }

                    token.value.valueLong = valueLong;
                    break;
                case RntbdTokenTypes.Double:
                    double valueDouble;
                    if (headerStringValue != null)
                    {
                        if (!double.TryParse(headerStringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueDouble))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, headerStringValue, headerName));
                        }
                    }
                    else
                    {
                        if (!(headerValue is double doubleValue))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, headerValue, headerName));
                        }

                        valueDouble = doubleValue;
                    }

                    token.value.valueDouble = valueDouble;
                    break;
                case RntbdTokenTypes.LongLong:
                    long valueLongLong;
                    if (headerStringValue != null)
                    {
                        if (!long.TryParse(headerStringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueLongLong))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, headerStringValue, headerName));
                        }
                    }
                    else
                    {
                        if (!(headerValue is long longLongValue))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, headerValue, headerName));
                        }

                        valueLongLong = longLongValue;
                    }

                    token.value.valueLongLong = valueLongLong;
                    break;
                case RntbdTokenTypes.Byte:
                    bool valueBool;
                    if (headerStringValue != null)
                    {
                        valueBool = string.Equals(headerStringValue, bool.TrueString, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        if (!(headerValue is bool boolValue))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, headerValue, headerName));
                        }

                        valueBool = boolValue;
                    }

                    token.value.valueByte = valueBool ? (byte)0x01 : (byte)0x00;
                    break;
                default:
                    Debug.Assert(false, "Recognized header has neither special-case nor default handling to convert"
                        + " from header string to RNTBD token.");
                    throw new BadRequestException();
            }

            token.isPresent = true;
        }

        private static void AddFanoutOperationStateHeader(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                if (!Enum.TryParse(headerValue, true, out FanoutOperationState state))
                {
                    throw new BadRequestException(
                        String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue, headerValue, nameof(FanoutOperationState)));
                }

                RntbdConstants.RntbdFanoutOperationState rntbdState;
                switch (state)
                {
                    case FanoutOperationState.Started:
                        rntbdState = RntbdConstants.RntbdFanoutOperationState.Started;
                        break;

                    case FanoutOperationState.Completed:
                        rntbdState = RntbdConstants.RntbdFanoutOperationState.Completed;
                        break;

                    default:
                        throw new BadRequestException(
                            String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue, headerValue, nameof(FanoutOperationState)));
                }

                rntbdRequest.FanoutOperationState.value.valueByte = (byte)rntbdState;
                rntbdRequest.FanoutOperationState.isPresent = true;
            }
        }

        private static void AddSystemDocumentTypeHeader(object headerObjectValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (headerObjectValue is string headerValue &&
                !string.IsNullOrEmpty(headerValue))
            {
                RntbdConstants.RntbdSystemDocumentType rntbdSystemDocumentType = RntbdConstants.RntbdSystemDocumentType.Invalid;
                if (!Enum.TryParse(headerValue, true, out SystemDocumentType systemDocumentType))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        headerValue, nameof(SystemDocumentType)));
                }

                switch (systemDocumentType)
                {
                    case SystemDocumentType.PartitionKey:
                        rntbdSystemDocumentType = RntbdConstants.RntbdSystemDocumentType.PartitionKey;
                        break;
                    case SystemDocumentType.MaterializedViewLeaseDocument:
                        rntbdSystemDocumentType = RntbdConstants.RntbdSystemDocumentType.MaterializedViewLeaseDocument;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            headerValue, typeof(SystemDocumentType).Name));
                }

                rntbdRequest.systemDocumentType.value.valueByte = (byte)rntbdSystemDocumentType;
                rntbdRequest.systemDocumentType.isPresent = true;
            }
        }

        private static void AddTransactionMetaData(object transactionIdValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (request.Properties.TryGetValue(WFConstants.BackendHeaders.TransactionFirstRequest, out object isFirstRequestValue))
            {
                // read transaction id
                if (!(transactionIdValue is byte[] transactionId))
                {
                    throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.TransactionId);
                }

                // read initial transactional request flag
                bool? isFirstRequest = isFirstRequestValue as bool?;
                if (!isFirstRequest.HasValue)
                {
                    throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.TransactionFirstRequest);
                }

                // set transaction id and initial request flag
                rntbdRequest.transactionId.value.valueBytes = transactionId;
                rntbdRequest.transactionId.isPresent = true;

                rntbdRequest.transactionFirstRequest.value.valueByte = ((bool)isFirstRequest) ? (byte)0x01 : (byte)0x00;
                rntbdRequest.transactionFirstRequest.isPresent = true;
            }
        }

        private static void AddRetriableWriteRequestMetadata(object retriableWriteRequestId, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!(retriableWriteRequestId is byte[] requestId))
            {
                throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.RetriableWriteRequestId);
            }

            rntbdRequest.retriableWriteRequestId.value.valueBytes = requestId;
            rntbdRequest.retriableWriteRequestId.isPresent = true;
        }

        private static void AddRetriableWriteRequestStartTimestampMetadata(object retriableWriteRequestStartTimestamp, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!UInt64.TryParse(retriableWriteRequestStartTimestamp.ToString(), out UInt64 requestStartTimestamp) || requestStartTimestamp <= 0)
            {
                throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.RetriableWriteRequestStartTimestamp);
            }

            rntbdRequest.retriableWriteRequestStartTimestamp.value.valueULongLong = requestStartTimestamp;
            rntbdRequest.retriableWriteRequestStartTimestamp.isPresent = true;
        }

        private static void AddUseSystemBudget(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.UseSystemBudget];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.useSystemBudget, headerValue);
        }

        private static void AddRntdbTokenBytesForString(RntbdConstants.Request rntbdRequest, RntbdToken rntbdToken, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                rntbdToken.value.valueBytes = BytesSerializer.GetBytesForString(value, rntbdRequest);
                rntbdToken.isPresent = true;
            }
        }

        private static void AddRntdbTokenBool(RntbdToken rntbdToken, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (string.Equals(bool.TrueString, value))
                {
                    rntbdToken.value.valueByte = (byte)0x01;
                }
                else
                {
                    rntbdToken.value.valueByte = (byte)0x00;
                }

                rntbdToken.isPresent = true;
            }
        }

        private static void AddRntdbTokenBytesFromBase64String(RntbdConstants.Request rntbdRequest, RntbdToken rntbdToken, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                rntbdToken.value.valueBytes = System.Convert.FromBase64String(value);
                rntbdToken.isPresent = true;
            }
        }
    }
}