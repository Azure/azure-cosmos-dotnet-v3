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
        internal static readonly IReadOnlyDictionary<string, Action<object, DocumentServiceRequest, RntbdConstants.Request>> AddProperties = new Dictionary<string, Action<object, DocumentServiceRequest, RntbdConstants.Request>>(StringComparer.OrdinalIgnoreCase)
        {
            { WFConstants.BackendHeaders.BinaryId , (value, documentServiceRequest, rntbdRequest) => TransportSerialization.AddBinaryIdIfPresent(value, rntbdRequest) },
            { WFConstants.BackendHeaders.TransactionCommit, (value, documentServiceRequest, rntbdRequest) => TransportSerialization.AddTransactionCompletionFlag(value, rntbdRequest) },
            { WFConstants.BackendHeaders.MergeStaticId, (value, documentServiceRequest, rntbdRequest) => TransportSerialization.AddMergeStaticIdIfPresent(value, rntbdRequest) },
            { WFConstants.BackendHeaders.EffectivePartitionKey, (value, documentServiceRequest, rntbdRequest) => TransportSerialization.AddEffectivePartitionKeyIfPresent(value, rntbdRequest) },
            { HttpConstants.HttpHeaders.EnumerationDirection, (value, documentServiceRequest, rntbdRequest) => TransportSerialization.AddEnumerationDirectionFromProperties(value, rntbdRequest) },
            { HttpConstants.HttpHeaders.ReadFeedKeyType, TransportSerialization.AddStartAndEndKeys },
            { WFConstants.BackendHeaders.TransactionId, TransportSerialization.AddTransactionMetaData },
            { WFConstants.BackendHeaders.RetriableWriteRequestId, TransportSerialization.AddRetriableWriteRequestMetadata},
            { WFConstants.BackendHeaders.IsRetriedWriteRequest, TransportSerialization.AddIsRetriedWriteRequestMetadata},
            { WFConstants.BackendHeaders.RetriableWriteRequestStartTimestamp, TransportSerialization.AddRetriableWriteRequestStartTimestampMetadata},

            { HttpConstants.HttpHeaders.Authorization,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.Authorization, rntbdRequest.authorizationToken, rntbdRequest)},
            { HttpConstants.HttpHeaders.SessionToken,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.SessionToken, rntbdRequest.sessionToken, rntbdRequest)},
            { HttpConstants.HttpHeaders.PreTriggerInclude,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.PreTriggerInclude, rntbdRequest.preTriggerInclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PreTriggerExclude,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.PreTriggerExclude, rntbdRequest.preTriggerExclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PostTriggerInclude,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.PostTriggerInclude, rntbdRequest.postTriggerInclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PostTriggerExclude,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.PostTriggerExclude, rntbdRequest.postTriggerExclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PartitionKey,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.PartitionKey, rntbdRequest.partitionKey, rntbdRequest)},
            { HttpConstants.HttpHeaders.PartitionKeyRangeId,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.PartitionKeyRangeId, rntbdRequest.partitionKeyRangeId, rntbdRequest)},
            { HttpConstants.HttpHeaders.ResourceTokenExpiry,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.ResourceTokenExpiry, rntbdRequest.resourceTokenExpiry, rntbdRequest)},
            { HttpConstants.HttpHeaders.FilterBySchemaResourceId,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.FilterBySchemaResourceId, rntbdRequest.filterBySchemaRid, rntbdRequest)},
            { HttpConstants.HttpHeaders.ShouldBatchContinueOnError,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.ShouldBatchContinueOnError, rntbdRequest.shouldBatchContinueOnError, rntbdRequest)},
            { HttpConstants.HttpHeaders.IsBatchOrdered,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.IsBatchOrdered, rntbdRequest.isBatchOrdered, rntbdRequest)},
            { HttpConstants.HttpHeaders.IsBatchAtomic,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.IsBatchAtomic, rntbdRequest.isBatchAtomic, rntbdRequest)},
            { WFConstants.BackendHeaders.CollectionPartitionIndex,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.CollectionPartitionIndex, rntbdRequest.collectionPartitionIndex, rntbdRequest)},
            { WFConstants.BackendHeaders.CollectionServiceIndex,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.CollectionServiceIndex, rntbdRequest.collectionServiceIndex, rntbdRequest)},
            { WFConstants.BackendHeaders.ResourceSchemaName,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.ResourceSchemaName, rntbdRequest.resourceSchemaName, rntbdRequest)},
            { WFConstants.BackendHeaders.BindReplicaDirective,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.BindReplicaDirective, rntbdRequest.bindReplicaDirective, rntbdRequest)},
            { WFConstants.BackendHeaders.PrimaryMasterKey,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.PrimaryMasterKey, rntbdRequest.primaryMasterKey, rntbdRequest)},
            { WFConstants.BackendHeaders.SecondaryMasterKey,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.SecondaryMasterKey, rntbdRequest.secondaryMasterKey, rntbdRequest)},
            { WFConstants.BackendHeaders.PrimaryReadonlyKey,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.PrimaryReadonlyKey, rntbdRequest.primaryReadonlyKey, rntbdRequest)},
            { WFConstants.BackendHeaders.SecondaryReadonlyKey,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.SecondaryReadonlyKey, rntbdRequest.secondaryReadonlyKey, rntbdRequest)},
            { WFConstants.BackendHeaders.PartitionCount,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.PartitionCount, rntbdRequest.partitionCount, rntbdRequest)},
            { WFConstants.BackendHeaders.CollectionRid,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.CollectionRid, rntbdRequest.collectionRid, rntbdRequest)},
            { HttpConstants.HttpHeaders.GatewaySignature,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.GatewaySignature, rntbdRequest.gatewaySignature, rntbdRequest)},
            { HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, rntbdRequest.remainingTimeInMsOnClientRequest, rntbdRequest)},
            { HttpConstants.HttpHeaders.ClientRetryAttemptCount,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.ClientRetryAttemptCount, rntbdRequest.clientRetryAttemptCount, rntbdRequest)},
            { HttpConstants.HttpHeaders.TargetLsn,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.TargetLsn, rntbdRequest.targetLsn, rntbdRequest)},
            { HttpConstants.HttpHeaders.TargetGlobalCommittedLsn,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, rntbdRequest.targetGlobalCommittedLsn, rntbdRequest)},
            { HttpConstants.HttpHeaders.TransportRequestID,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.TransportRequestID, rntbdRequest.transportRequestID, rntbdRequest)},
            { HttpConstants.HttpHeaders.RestoreMetadataFilter,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.RestoreMetadataFilter, rntbdRequest.restoreMetadataFilter, rntbdRequest)},
            { WFConstants.BackendHeaders.RestoreParams,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.RestoreParams, rntbdRequest.restoreParams, rntbdRequest)},
            { WFConstants.BackendHeaders.PartitionResourceFilter,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.PartitionResourceFilter, rntbdRequest.partitionResourceFilter, rntbdRequest)},
            { WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation, rntbdRequest.enableDynamicRidRangeAllocation, rntbdRequest)},
            { WFConstants.BackendHeaders.SchemaOwnerRid,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.SchemaOwnerRid, rntbdRequest.schemaOwnerRid, rntbdRequest)},
            { WFConstants.BackendHeaders.SchemaHash,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.SchemaHash, rntbdRequest.schemaHash, rntbdRequest)},
            { HttpConstants.HttpHeaders.IsClientEncrypted,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.IsClientEncrypted, rntbdRequest.isClientEncrypted, rntbdRequest)},
            { WFConstants.BackendHeaders.TimeToLiveInSeconds,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.TimeToLiveInSeconds, rntbdRequest.timeToLiveInSeconds, rntbdRequest)},

            { WFConstants.BackendHeaders.BinaryPassthroughRequest,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.BinaryPassthroughRequest, rntbdRequest.binaryPassthroughRequest, rntbdRequest)},
            { WFConstants.BackendHeaders.AllowTentativeWrites,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.AllowTentativeWrites, rntbdRequest.allowTentativeWrites, rntbdRequest)},
            { HttpConstants.HttpHeaders.IncludeTentativeWrites,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.IncludeTentativeWrites, rntbdRequest.includeTentativeWrites, rntbdRequest)},

            { HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds, rntbdRequest.maxPollingIntervalMilliseconds, rntbdRequest)},
            { WFConstants.BackendHeaders.PopulateLogStoreInfo,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.PopulateLogStoreInfo, rntbdRequest.populateLogStoreInfo, rntbdRequest)},
            { WFConstants.BackendHeaders.MergeCheckPointGLSN,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.MergeCheckPointGLSN, rntbdRequest.mergeCheckpointGlsnKeyName, rntbdRequest)},
            { WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount,  (value, documentServiceRequest, rntbdRequest) => FillTokenFromProperties(value, documentServiceRequest, WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount, rntbdRequest.populateUnflushedMergeEntryCount, rntbdRequest)},

            // will be null in case of direct, which is fine - BE will use the value from the connection context message.
            // When this is used in Gateway, the header value will be populated with the proxied HTTP request's header, and
            // BE will respect the per-request value.
            { HttpConstants.HttpHeaders.Version,  (value, documentServiceRequest, rntbdRequest) =>TransportSerialization.FillTokenFromProperties(value, documentServiceRequest, HttpConstants.HttpHeaders.Version, rntbdRequest.clientVersion, rntbdRequest) },
        };

        internal static readonly IReadOnlyDictionary<string, Action<DocumentServiceRequest, RntbdConstants.Request>> AddHeaders = new Dictionary<string, Action<DocumentServiceRequest, RntbdConstants.Request>>(StringComparer.OrdinalIgnoreCase)
        {
            { HttpConstants.HttpHeaders.XDate, TransportSerialization.AddDateHeader },
            { HttpConstants.HttpHeaders.HttpDate, TransportSerialization.AddDateHeader },
            { HttpConstants.HttpHeaders.Continuation, (documentServiceRequest, rntbdRequest) => TransportSerialization.AddRntdbTokenBytesForString(rntbdRequest, rntbdRequest.continuationToken, documentServiceRequest.Headers[HttpConstants.HttpHeaders.Continuation])},
            { HttpConstants.HttpHeaders.IfMatch, TransportSerialization.AddMatchHeader },
            { HttpConstants.HttpHeaders.IfNoneMatch, TransportSerialization.AddMatchHeader },
            { HttpConstants.HttpHeaders.IfModifiedSince, TransportSerialization.AddIfModifiedSinceHeader },
            { HttpConstants.HttpHeaders.A_IM, TransportSerialization.AddA_IMHeader },
            { HttpConstants.HttpHeaders.IndexingDirective, TransportSerialization.AddIndexingDirectiveHeader },
            { HttpConstants.HttpHeaders.MigrateCollectionDirective, TransportSerialization.AddMigrateCollectionDirectiveHeader },
            { HttpConstants.HttpHeaders.ConsistencyLevel, TransportSerialization.AddConsistencyLevelHeader },
            { WFConstants.BackendHeaders.IsFanoutRequest, TransportSerialization.AddIsFanout },
            { HttpConstants.HttpHeaders.EnableScanInQuery, TransportSerialization.AddAllowScanOnQuery },
            { HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, TransportSerialization.AddEmitVerboseTracesInQuery },
            { HttpConstants.HttpHeaders.CanCharge, TransportSerialization.AddCanCharge },
            { HttpConstants.HttpHeaders.CanThrottle, TransportSerialization.AddCanThrottle },
            { HttpConstants.HttpHeaders.ProfileRequest, TransportSerialization.AddProfileRequest },
            { HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, TransportSerialization.AddEnableLowPrecisionOrderBy },
            { HttpConstants.HttpHeaders.PageSize, TransportSerialization.AddPageSize },
            { HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates, TransportSerialization.AddSupportSpatialLegacyCoordinates },
            { HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere, TransportSerialization.AddUsePolygonsSmallerThanAHemisphere },
            { HttpConstants.HttpHeaders.EnableLogging, TransportSerialization.AddEnableLogging },
            { HttpConstants.HttpHeaders.PopulateQuotaInfo, TransportSerialization.AddPopulateQuotaInfo },
            { HttpConstants.HttpHeaders.PopulateResourceCount, TransportSerialization.AddPopulateResourceCount },
            { HttpConstants.HttpHeaders.DisableRUPerMinuteUsage, TransportSerialization.AddDisableRUPerMinuteUsage },
            { HttpConstants.HttpHeaders.PopulateQueryMetrics, TransportSerialization.AddPopulateQueryMetrics },
            { HttpConstants.HttpHeaders.ForceQueryScan, TransportSerialization.AddQueryForceScan },
            { HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB, TransportSerialization.AddResponseContinuationTokenLimitInKb },
            { HttpConstants.HttpHeaders.PopulatePartitionStatistics, TransportSerialization.AddPopulatePartitionStatistics },
            { WFConstants.BackendHeaders.RemoteStorageType, TransportSerialization.AddRemoteStorageType },
            { HttpConstants.HttpHeaders.CollectionRemoteStorageSecurityIdentifier, TransportSerialization.AddCollectionRemoteStorageSecurityIdentifier },
            { WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes, TransportSerialization.AddCollectionChildResourceNameLimitInBytes },
            { WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB, TransportSerialization.AddCollectionChildResourceContentLengthLimitInKB },
            { WFConstants.BackendHeaders.UniqueIndexNameEncodingMode, TransportSerialization.AddUniqueIndexNameEncodingMode },
            { WFConstants.BackendHeaders.UniqueIndexReIndexingState, TransportSerialization.AddUniqueIndexReIndexingState },
            { HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo, TransportSerialization.AddPopulateCollectionThroughputInfo },
            { WFConstants.BackendHeaders.ShareThroughput, TransportSerialization.AddShareThroughput },
            { HttpConstants.HttpHeaders.IsReadOnlyScript, TransportSerialization.AddIsReadOnlyScript },
#if !COSMOSCLIENT
            { HttpConstants.HttpHeaders.IsAutoScaleRequest, TransportSerialization.AddIsAutoScaleRequest },
#endif
            { HttpConstants.HttpHeaders.CanOfferReplaceComplete, TransportSerialization.AddCanOfferReplaceComplete },
            { HttpConstants.HttpHeaders.IgnoreSystemLoweringMaxThroughput, TransportSerialization.AddIgnoreSystemLoweringMaxThroughput },
            { WFConstants.BackendHeaders.ExcludeSystemProperties, TransportSerialization.AddExcludeSystemProperties },
            { HttpConstants.HttpHeaders.EnumerationDirection, TransportSerialization.AddEnumerationDirectionFromHeaders },
            { WFConstants.BackendHeaders.FanoutOperationState, TransportSerialization.AddFanoutOperationStateHeader },
            { HttpConstants.HttpHeaders.ReadFeedKeyType, TransportSerialization.AddStartAndEndKeysFromHeaders },
            { HttpConstants.HttpHeaders.StartId, TransportSerialization.AddStartIdFromHeaders },
            { HttpConstants.HttpHeaders.EndId, TransportSerialization.AddEndIdFromHeaders },
            { HttpConstants.HttpHeaders.StartEpk, TransportSerialization.AddStartEpkFromHeaders },
            { HttpConstants.HttpHeaders.EndEpk, TransportSerialization.AddEndEpkFromHeaders },
            { HttpConstants.HttpHeaders.ContentSerializationFormat, TransportSerialization.AddContentSerializationFormat },
            { WFConstants.BackendHeaders.IsUserRequest, TransportSerialization.AddIsUserRequest },
            { HttpConstants.HttpHeaders.PreserveFullContent, TransportSerialization.AddPreserveFullContent },
            { HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest, TransportSerialization.AddIsRUPerGBEnforcementRequest },
            { HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest, TransportSerialization.AddIsOfferStorageRefreshRequest },
            { HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics, TransportSerialization.AddGetAllPartitionKeyStatistics },
            { HttpConstants.HttpHeaders.ForceSideBySideIndexMigration, TransportSerialization.AddForceSideBySideIndexMigration },
            { HttpConstants.HttpHeaders.MigrateOfferToManualThroughput, TransportSerialization.AddIsMigrateOfferToManualThroughputRequest },
            { HttpConstants.HttpHeaders.MigrateOfferToAutopilot, TransportSerialization.AddIsMigrateOfferToAutopilotRequest },
            { HttpConstants.HttpHeaders.SystemDocumentType, TransportSerialization.AddSystemDocumentTypeHeader },

            { WFConstants.BackendHeaders.ResourceTypes, TransportSerialization.AddResourceTypes },
            { HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned, TransportSerialization.AddUpdateMaxthroughputEverProvisioned },
            { WFConstants.BackendHeaders.UseSystemBudget, TransportSerialization.AddUseSystemBudget },
            { HttpConstants.HttpHeaders.Prefer, TransportSerialization.AddReturnPreferenceIfPresent },
            { HttpConstants.HttpHeaders.TruncateMergeLogRequest, TransportSerialization.AddTruncateMergeLogRequest},

            { HttpConstants.HttpHeaders.Authorization,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.Authorization, rntbdRequest.authorizationToken, rntbdRequest)},
            { HttpConstants.HttpHeaders.SessionToken,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.SessionToken, rntbdRequest.sessionToken, rntbdRequest)},
            { HttpConstants.HttpHeaders.PreTriggerInclude,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.PreTriggerInclude, rntbdRequest.preTriggerInclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PreTriggerExclude,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.PreTriggerExclude, rntbdRequest.preTriggerExclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PostTriggerInclude,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.PostTriggerInclude, rntbdRequest.postTriggerInclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PostTriggerExclude,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.PostTriggerExclude, rntbdRequest.postTriggerExclude, rntbdRequest)},
            { HttpConstants.HttpHeaders.PartitionKey,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.PartitionKey, rntbdRequest.partitionKey, rntbdRequest)},
            { HttpConstants.HttpHeaders.PartitionKeyRangeId,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.PartitionKeyRangeId, rntbdRequest.partitionKeyRangeId, rntbdRequest)},
            { HttpConstants.HttpHeaders.ResourceTokenExpiry,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.ResourceTokenExpiry, rntbdRequest.resourceTokenExpiry, rntbdRequest)},
            { HttpConstants.HttpHeaders.FilterBySchemaResourceId,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.FilterBySchemaResourceId, rntbdRequest.filterBySchemaRid, rntbdRequest)},
            { HttpConstants.HttpHeaders.ShouldBatchContinueOnError,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.ShouldBatchContinueOnError, rntbdRequest.shouldBatchContinueOnError, rntbdRequest)},
            { HttpConstants.HttpHeaders.IsBatchOrdered,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.IsBatchOrdered, rntbdRequest.isBatchOrdered, rntbdRequest)},
            { HttpConstants.HttpHeaders.IsBatchAtomic,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.IsBatchAtomic, rntbdRequest.isBatchAtomic, rntbdRequest)},
            { WFConstants.BackendHeaders.CollectionPartitionIndex,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.CollectionPartitionIndex, rntbdRequest.collectionPartitionIndex, rntbdRequest)},
            { WFConstants.BackendHeaders.CollectionServiceIndex,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.CollectionServiceIndex, rntbdRequest.collectionServiceIndex, rntbdRequest)},
            { WFConstants.BackendHeaders.ResourceSchemaName,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.ResourceSchemaName, rntbdRequest.resourceSchemaName, rntbdRequest)},
            { WFConstants.BackendHeaders.BindReplicaDirective,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.BindReplicaDirective, rntbdRequest.bindReplicaDirective, rntbdRequest)},
            { WFConstants.BackendHeaders.PrimaryMasterKey,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.PrimaryMasterKey, rntbdRequest.primaryMasterKey, rntbdRequest)},
            { WFConstants.BackendHeaders.SecondaryMasterKey,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.SecondaryMasterKey, rntbdRequest.secondaryMasterKey, rntbdRequest)},
            { WFConstants.BackendHeaders.PrimaryReadonlyKey,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.PrimaryReadonlyKey, rntbdRequest.primaryReadonlyKey, rntbdRequest)},
            { WFConstants.BackendHeaders.SecondaryReadonlyKey,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.SecondaryReadonlyKey, rntbdRequest.secondaryReadonlyKey, rntbdRequest)},
            { WFConstants.BackendHeaders.PartitionCount,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.PartitionCount, rntbdRequest.partitionCount, rntbdRequest)},
            { WFConstants.BackendHeaders.CollectionRid,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.CollectionRid, rntbdRequest.collectionRid, rntbdRequest)},
            { HttpConstants.HttpHeaders.GatewaySignature,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.GatewaySignature, rntbdRequest.gatewaySignature, rntbdRequest)},
            { HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, rntbdRequest.remainingTimeInMsOnClientRequest, rntbdRequest)},
            { HttpConstants.HttpHeaders.ClientRetryAttemptCount,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.ClientRetryAttemptCount, rntbdRequest.clientRetryAttemptCount, rntbdRequest)},
            { HttpConstants.HttpHeaders.TargetLsn,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.TargetLsn, rntbdRequest.targetLsn, rntbdRequest)},
            { HttpConstants.HttpHeaders.TargetGlobalCommittedLsn,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, rntbdRequest.targetGlobalCommittedLsn, rntbdRequest)},
            { HttpConstants.HttpHeaders.TransportRequestID,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.TransportRequestID, rntbdRequest.transportRequestID, rntbdRequest)},
            { HttpConstants.HttpHeaders.RestoreMetadataFilter,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.RestoreMetadataFilter, rntbdRequest.restoreMetadataFilter, rntbdRequest)},
            { WFConstants.BackendHeaders.RestoreParams,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.RestoreParams, rntbdRequest.restoreParams, rntbdRequest)},
            { WFConstants.BackendHeaders.PartitionResourceFilter,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.PartitionResourceFilter, rntbdRequest.partitionResourceFilter, rntbdRequest)},
            { WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation, rntbdRequest.enableDynamicRidRangeAllocation, rntbdRequest)},
            { WFConstants.BackendHeaders.SchemaOwnerRid,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.SchemaOwnerRid, rntbdRequest.schemaOwnerRid, rntbdRequest)},
            { WFConstants.BackendHeaders.SchemaHash,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.SchemaHash, rntbdRequest.schemaHash, rntbdRequest)},
            { HttpConstants.HttpHeaders.IsClientEncrypted,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.IsClientEncrypted, rntbdRequest.isClientEncrypted, rntbdRequest)},
            { WFConstants.BackendHeaders.TimeToLiveInSeconds,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.TimeToLiveInSeconds, rntbdRequest.timeToLiveInSeconds, rntbdRequest)},

            { WFConstants.BackendHeaders.BinaryPassthroughRequest,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.BinaryPassthroughRequest, rntbdRequest.binaryPassthroughRequest, rntbdRequest)},
            { WFConstants.BackendHeaders.AllowTentativeWrites,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.AllowTentativeWrites, rntbdRequest.allowTentativeWrites, rntbdRequest)},
            { HttpConstants.HttpHeaders.IncludeTentativeWrites,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.IncludeTentativeWrites, rntbdRequest.includeTentativeWrites, rntbdRequest)},

            { HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds, rntbdRequest.maxPollingIntervalMilliseconds, rntbdRequest)},
            { WFConstants.BackendHeaders.PopulateLogStoreInfo,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.PopulateLogStoreInfo, rntbdRequest.populateLogStoreInfo, rntbdRequest)},
            { WFConstants.BackendHeaders.MergeCheckPointGLSN,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.MergeCheckPointGLSN, rntbdRequest.mergeCheckpointGlsnKeyName, rntbdRequest)},
            { WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount,  (documentServiceRequest, rntbdRequest) => FillTokenFromHeader(documentServiceRequest, WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount, rntbdRequest.populateUnflushedMergeEntryCount, rntbdRequest)},

            // will be null in case of direct, which is fine - BE will use the value from the connection context message.
            // When this is used in Gateway, the header value will be populated with the proxied HTTP request's header, and
            // BE will respect the per-request value.
            { HttpConstants.HttpHeaders.Version,  (documentServiceRequest, rntbdRequest) =>TransportSerialization.FillTokenFromHeader(documentServiceRequest, HttpConstants.HttpHeaders.Version, rntbdRequest.clientVersion, rntbdRequest) },
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

        internal static byte[] BuildRequest(DocumentServiceRequest request, string replicaPath,
            ResourceOperation resourceOperation, Guid activityId, out int headerSize,
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
            foreach (string headerKey in request.Headers.Keys())
            {
                if (TransportSerialization.AddHeaders.TryGetValue(headerKey, out Action<DocumentServiceRequest, RntbdConstants.Request> setHeader))
                {
                    setHeader(request, rntbdRequest);
                }
            }

            if (request.Properties != null)
            {
                foreach (KeyValuePair<string, object> keyValuePair in request.Properties)
                {
                    if (TransportSerialization.AddProperties.TryGetValue(keyValuePair.Key, out Action<object, DocumentServiceRequest, RntbdConstants.Request> setProperty))
                    {
                        setProperty(keyValuePair.Value, request, rntbdRequest);
                    }
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

        private static void AddMatchHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string match = null;
            switch (request.OperationType)
            {
                case OperationType.Read:
                case OperationType.ReadFeed:
                    match = request.Headers[HttpConstants.HttpHeaders.IfNoneMatch];
                    break;
                default:
                    match = request.Headers[HttpConstants.HttpHeaders.IfMatch];
                    break;
            }

            if (!string.IsNullOrEmpty(match))
            {
                rntbdRequest.match.value.valueBytes = BytesSerializer.GetBytesForString(match, rntbdRequest);
                rntbdRequest.match.isPresent = true;
            }
        }

        private static void AddIfModifiedSinceHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.IfModifiedSince];
            TransportSerialization.AddRntdbTokenBytesForString(rntbdRequest, rntbdRequest.ifModifiedSince, headerValue);
        }

        private static void AddA_IMHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.A_IM];
            TransportSerialization.AddRntdbTokenBytesForString(rntbdRequest, rntbdRequest.a_IM, headerValue);
        }

        private static void AddDateHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = Helpers.GetDateHeader(request.Headers);
            TransportSerialization.AddRntdbTokenBytesForString(rntbdRequest, rntbdRequest.date, headerValue);
        }

        private static void AddContinuation(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Continuation;
            TransportSerialization.AddRntdbTokenBytesForString(rntbdRequest, rntbdRequest.continuationToken, headerValue);
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

        private static void AddReturnPreferenceIfPresent(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string value = request.Headers[HttpConstants.HttpHeaders.Prefer];

            if (!string.IsNullOrEmpty(value))
            {
                if (string.Equals(value, HttpConstants.HttpHeaderValues.PreferReturnMinimal, StringComparison.OrdinalIgnoreCase))
                {
                    rntbdRequest.returnPreference.value.valueByte = (byte)0x01;
                    rntbdRequest.returnPreference.isPresent = true;
                }
                else if (string.Equals(value, HttpConstants.HttpHeaderValues.PreferReturnRepresentation, StringComparison.OrdinalIgnoreCase))
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

        private static void AddIndexingDirectiveHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.IndexingDirective]))
            {
                RntbdConstants.RntbdIndexingDirective rntbdDirective = RntbdConstants.RntbdIndexingDirective.Invalid;
                IndexingDirective directive;
                if (!Enum.TryParse(request.Headers[HttpConstants.HttpHeaders.IndexingDirective], true, out directive))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.IndexingDirective], typeof(IndexingDirective).Name));
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
                            request.Headers[HttpConstants.HttpHeaders.IndexingDirective], typeof(IndexingDirective).Name));
                }

                rntbdRequest.indexingDirective.value.valueByte = (byte)rntbdDirective;
                rntbdRequest.indexingDirective.isPresent = true;
            }
        }

        private static void AddMigrateCollectionDirectiveHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.MigrateCollectionDirective]))
            {
                RntbdConstants.RntbdMigrateCollectionDirective rntbdDirective = RntbdConstants.RntbdMigrateCollectionDirective.Invalid;
                MigrateCollectionDirective directive;
                if (!Enum.TryParse(request.Headers[HttpConstants.HttpHeaders.MigrateCollectionDirective], true, out directive))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.MigrateCollectionDirective], typeof(MigrateCollectionDirective).Name));
                }

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
                            request.Headers[HttpConstants.HttpHeaders.MigrateCollectionDirective], typeof(MigrateCollectionDirective).Name));
                }

                rntbdRequest.migrateCollectionDirective.value.valueByte = (byte)rntbdDirective;
                rntbdRequest.migrateCollectionDirective.isPresent = true;
            }
        }

        private static void AddConsistencyLevelHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel]))
            {
                RntbdConstants.RntbdConsistencyLevel rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.Invalid;
                ConsistencyLevel consistencyLevel;
                if (!Enum.TryParse<ConsistencyLevel>(request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel], true, out consistencyLevel))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel], typeof(ConsistencyLevel).Name));
                }

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
                            request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel], typeof(ConsistencyLevel).Name));
                }

                rntbdRequest.consistencyLevel.value.valueByte = (byte)rntbdConsistencyLevel;
                rntbdRequest.consistencyLevel.isPresent = true;
            }
        }

        private static void AddIsFanout(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.IsFanoutRequest];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.isFanout, headerValue);
        }

        private static void AddAllowScanOnQuery(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.EnableScanInQuery];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.enableScanInQuery, headerValue);
        }

        private static void AddEnableLowPrecisionOrderBy(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.enableLowPrecisionOrderBy, headerValue);
        }

        private static void AddEmitVerboseTracesInQuery(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.EmitVerboseTracesInQuery];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.emitVerboseTracesInQuery, headerValue);
        }

        private static void AddCanCharge(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.CanCharge];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.canCharge, headerValue);
        }

        private static void AddCanThrottle(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.CanThrottle];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.canThrottle, headerValue);
        }

        private static void AddProfileRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.ProfileRequest];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.profileRequest, headerValue);
        }

        private static void AddPageSize(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string value = request.Headers[HttpConstants.HttpHeaders.PageSize];

            if (!string.IsNullOrEmpty(value))
            {
                int valueInt;
                if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueInt))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidPageSize, value));
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
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidPageSize, value));
                }

                rntbdRequest.pageSize.isPresent = true;
            }
        }

        private static void AddEnableLogging(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.EnableLogging];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.enableLogging, headerValue);
        }

        private static void AddSupportSpatialLegacyCoordinates(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.supportSpatialLegacyCoordinates, headerValue);
        }

        private static void AddUsePolygonsSmallerThanAHemisphere(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.usePolygonsSmallerThanAHemisphere, headerValue);
        }

        private static void AddPopulateQuotaInfo(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.PopulateQuotaInfo];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.populateQuotaInfo, headerValue);
        }

        private static void AddPopulateResourceCount(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.PopulateResourceCount];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.populateResourceCount, headerValue);
        }

        private static void AddPopulatePartitionStatistics(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.PopulatePartitionStatistics];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.populatePartitionStatistics, headerValue);
        }

        private static void AddDisableRUPerMinuteUsage(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.DisableRUPerMinuteUsage];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.disableRUPerMinuteUsage, headerValue);
        }

        private static void AddPopulateQueryMetrics(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.PopulateQueryMetrics];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.populateQueryMetrics, headerValue);
        }

        private static void AddQueryForceScan(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.ForceQueryScan];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.forceQueryScan, headerValue);
        }

        private static void AddPopulateCollectionThroughputInfo(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.populateCollectionThroughputInfo, headerValue);
        }

        private static void AddShareThroughput(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.ShareThroughput];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.shareThroughput, headerValue);
        }

        private static void AddIsReadOnlyScript(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.IsReadOnlyScript];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.isReadOnlyScript, headerValue);
        }

#if !COSMOSCLIENT
        private static void AddIsAutoScaleRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
             string headerValue = request.Headers[HttpConstants.HttpHeaders.IsAutoScaleRequest];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.isAutoScaleRequest, headerValue);
        }
#endif

        private static void AddCanOfferReplaceComplete(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.CanOfferReplaceComplete];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.canOfferReplaceComplete, headerValue);
        }


        private static void AddIgnoreSystemLoweringMaxThroughput(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.IgnoreSystemLoweringMaxThroughput];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.ignoreSystemLoweringMaxThroughput, headerValue);
        }

        private static void AddUpdateMaxthroughputEverProvisioned(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned]))
            {
                string value = request.Headers[HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned];
                int valueInt;
                if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueInt))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidUpdateMaxthroughputEverProvisioned, value));
                }

                if (valueInt >= 0)
                {
                    rntbdRequest.updateMaxThroughputEverProvisioned.value.valueULong = (UInt32)valueInt;
                }
                else
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidUpdateMaxthroughputEverProvisioned, value));
                }

                rntbdRequest.updateMaxThroughputEverProvisioned.isPresent = true;
            }
        }

        private static void AddGetAllPartitionKeyStatistics(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.getAllPartitionKeyStatistics, headerValue);
        }

        private static void AddResponseContinuationTokenLimitInKb(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB]))
            {
                string value = request.Headers[HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB];
                if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valueInt))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidPageSize, value));
                }

                if (valueInt >= 0)
                {
                    rntbdRequest.responseContinuationTokenLimitInKb.value.valueULong = (UInt32)valueInt;
                }
                else
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidResponseContinuationTokenLimit, value));
                }

                rntbdRequest.responseContinuationTokenLimitInKb.isPresent = true;
            }
        }

        private static void AddRemoteStorageType(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[WFConstants.BackendHeaders.RemoteStorageType]))
            {
                RntbdConstants.RntbdRemoteStorageType rntbdRemoteStorageType = RntbdConstants.RntbdRemoteStorageType.Invalid;
                if (!Enum.TryParse(request.Headers[WFConstants.BackendHeaders.RemoteStorageType], true, out RemoteStorageType remoteStorageType))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[WFConstants.BackendHeaders.RemoteStorageType], typeof(RemoteStorageType).Name));
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
                            request.Headers[WFConstants.BackendHeaders.RemoteStorageType], typeof(RemoteStorageType).Name));
                }

                rntbdRequest.remoteStorageType.value.valueByte = (byte)rntbdRemoteStorageType;
                rntbdRequest.remoteStorageType.isPresent = true;
            }
        }

        private static void AddCollectionChildResourceNameLimitInBytes(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes];
            if (!string.IsNullOrEmpty(headerValue))
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

        private static void AddCollectionChildResourceContentLengthLimitInKB(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB];
            if (!string.IsNullOrEmpty(headerValue))
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

        private static void AddUniqueIndexNameEncodingMode(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.UniqueIndexNameEncodingMode];
            if (!string.IsNullOrEmpty(headerValue))
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

        private static void AddUniqueIndexReIndexingState(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.UniqueIndexReIndexingState];
            if (!string.IsNullOrEmpty(headerValue))
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

        private static void AddCollectionRemoteStorageSecurityIdentifier(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.CollectionRemoteStorageSecurityIdentifier];
            TransportSerialization.AddRntdbTokenBytesForString(rntbdRequest, rntbdRequest.collectionRemoteStorageSecurityIdentifier, headerValue);
        }

        private static void AddIsUserRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.IsUserRequest];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.isUserRequest, headerValue);
        }

        private static void AddPreserveFullContent(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.PreserveFullContent];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.preserveFullContent, headerValue);
        }

        private static void AddForceSideBySideIndexMigration(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.ForceSideBySideIndexMigration];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.forceSideBySideIndexMigration, headerValue);
        }

        private static void AddIsRUPerGBEnforcementRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.isRUPerGBEnforcementRequest, headerValue);
        }

        private static void AddIsOfferStorageRefreshRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.isofferStorageRefreshRequest, headerValue);
        }

        private static void AddIsMigrateOfferToManualThroughputRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.MigrateOfferToManualThroughput];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.migrateOfferToManualThroughput, headerValue);
        }

        private static void AddIsMigrateOfferToAutopilotRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.MigrateOfferToAutopilot];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.migrateOfferToAutopilot, headerValue);
        }

        private static void AddTruncateMergeLogRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.TruncateMergeLogRequest];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.truncateMergeLogRequest, headerValue);
        }

        private static void AddEnumerationDirectionFromProperties(object enumerationDirectionObject, RntbdConstants.Request rntbdRequest)
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
            else
            {
                rntbdRequest.enumerationDirection.value.valueByte = scanDirection.Value;
                rntbdRequest.enumerationDirection.isPresent = true;
            }
        }

        private static void AddEnumerationDirectionFromHeaders(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            // Header already set by properties
            if (rntbdRequest.enumerationDirection.isPresent)
            {
                return;
            }

            string headerValue = request.Headers[HttpConstants.HttpHeaders.EnumerationDirection];
            if (!string.IsNullOrEmpty(headerValue))
            {
                RntbdConstants.RntdbEnumerationDirection rntdbEnumerationDirection = RntbdConstants.RntdbEnumerationDirection.Invalid;
                if (!Enum.TryParse(headerValue, true, out EnumerationDirection enumerationDirection))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        headerValue, nameof(EnumerationDirection)));
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
                            headerValue, typeof(EnumerationDirection).Name));
                }

                rntbdRequest.enumerationDirection.value.valueByte = (byte)rntdbEnumerationDirection;
                rntbdRequest.enumerationDirection.isPresent = true;
            }
        }

        private static void AddStartAndEndKeys(object requestObject, DocumentServiceRequest documentServiceRequest, RntbdConstants.Request rntbdRequest)
        {
            RntbdConstants.RntdbReadFeedKeyType? readFeedKeyType = null;
            if (!(requestObject is byte))
            {
                throw new ArgumentOutOfRangeException(HttpConstants.HttpHeaders.ReadFeedKeyType);
            }

            rntbdRequest.readFeedKeyType.value.valueByte = (byte)requestObject;
            rntbdRequest.readFeedKeyType.isPresent = true;
            readFeedKeyType = (RntbdConstants.RntdbReadFeedKeyType)requestObject;

            if (readFeedKeyType == RntbdConstants.RntdbReadFeedKeyType.ResourceId)
            {
                TransportSerialization.SetBytesValue(documentServiceRequest, HttpConstants.HttpHeaders.StartId, rntbdRequest.StartId);
                TransportSerialization.SetBytesValue(documentServiceRequest, HttpConstants.HttpHeaders.EndId, rntbdRequest.EndId);
            }
            else if (readFeedKeyType == RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKey)
            {

                TransportSerialization.SetBytesValue(documentServiceRequest, HttpConstants.HttpHeaders.StartEpk, rntbdRequest.StartEpk);
                TransportSerialization.SetBytesValue(documentServiceRequest, HttpConstants.HttpHeaders.EndEpk, rntbdRequest.EndEpk);
            }
        }

        private static void AddStartAndEndKeysFromHeaders(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.ReadFeedKeyType]))
            {
                RntbdConstants.RntdbReadFeedKeyType rntdbReadFeedKeyType = RntbdConstants.RntdbReadFeedKeyType.Invalid;
                if (!Enum.TryParse(request.Headers[HttpConstants.HttpHeaders.ReadFeedKeyType], true, out ReadFeedKeyType readFeedKeyType))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.ReadFeedKeyType], nameof(ReadFeedKeyType)));
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
                            request.Headers[HttpConstants.HttpHeaders.ReadFeedKeyType], typeof(ReadFeedKeyType).Name));
                }

                rntbdRequest.readFeedKeyType.value.valueByte = (byte)rntdbReadFeedKeyType;
                rntbdRequest.readFeedKeyType.isPresent = true;
            }
        }

        private static void AddStartIdFromHeaders(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string startId = request.Headers[HttpConstants.HttpHeaders.StartId];
            if (!string.IsNullOrEmpty(startId))
            {
                rntbdRequest.StartId.value.valueBytes = System.Convert.FromBase64String(startId);
                rntbdRequest.StartId.isPresent = true;
            }
        }

        private static void AddEndIdFromHeaders(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string endId = request.Headers[HttpConstants.HttpHeaders.EndId];
            if (!string.IsNullOrEmpty(endId))
            {
                rntbdRequest.EndId.value.valueBytes = System.Convert.FromBase64String(endId);
                rntbdRequest.EndId.isPresent = true;
            }
        }

        private static void AddStartEpkFromHeaders(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string startEpk = request.Headers[HttpConstants.HttpHeaders.StartEpk];
            if (!string.IsNullOrEmpty(startEpk))
            {
                rntbdRequest.StartEpk.value.valueBytes = System.Convert.FromBase64String(startEpk);
                rntbdRequest.StartEpk.isPresent = true;
            }
        }

        private static void AddEndEpkFromHeaders(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
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

        private static void AddContentSerializationFormat(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.ContentSerializationFormat]))
            {
                RntbdConstants.RntbdContentSerializationFormat rntbdContentSerializationFormat = RntbdConstants.RntbdContentSerializationFormat.Invalid;

                if (!Enum.TryParse<ContentSerializationFormat>(request.Headers[HttpConstants.HttpHeaders.ContentSerializationFormat], true, out ContentSerializationFormat contentSerializationFormat))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.ContentSerializationFormat], nameof(ContentSerializationFormat)));
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
                            request.Headers[HttpConstants.HttpHeaders.ContentSerializationFormat], nameof(ContentSerializationFormat)));
                }

                rntbdRequest.contentSerializationFormat.value.valueByte = (byte)rntbdContentSerializationFormat;
                rntbdRequest.contentSerializationFormat.isPresent = true;
            }
        }

        private static void FillTokenFromHeader(DocumentServiceRequest request, string headerName, RntbdToken token, RntbdConstants.Request rntbdRequest)
        {
            string value = request.Headers[headerName];
            TransportSerialization.FillTokenWithValue(value, request, headerName, token, rntbdRequest);
        }

        private static void FillTokenFromProperties(object propertyValue, DocumentServiceRequest request, string headerName, RntbdToken token, RntbdConstants.Request rntbdRequest)
        {
            // The token was already set by the headers
            if (token.isPresent)
            {
                return;
            }

            string value = (string)propertyValue;
            TransportSerialization.FillTokenWithValue(value, request, headerName, token, rntbdRequest);
        }

        private static void FillTokenWithValue(string value, DocumentServiceRequest request, string headerName, RntbdToken token, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(value))
            {
                switch (token.GetTokenType())
                {
                    case RntbdTokenTypes.SmallString:
                    case RntbdTokenTypes.String:
                    case RntbdTokenTypes.ULongString:
                        token.value.valueBytes = BytesSerializer.GetBytesForString(value, rntbdRequest);
                        break;
                    case RntbdTokenTypes.ULong:
                        if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint valueULong))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, value, headerName));
                        }

                        token.value.valueULong = valueULong;
                        break;
                    case RntbdTokenTypes.Long:
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valueLong))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, value, headerName));
                        }

                        token.value.valueLong = valueLong;
                        break;
                    case RntbdTokenTypes.Double:
                        token.value.valueDouble = double.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case RntbdTokenTypes.LongLong:
                        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long valueLongLong))
                        {
                            throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, value, headerName));
                        }

                        token.value.valueLongLong = valueLongLong;
                        break;
                    case RntbdTokenTypes.Byte:
                        token.value.valueByte = value.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase) ? (byte)0x01 : (byte)0x00;
                        break;
                    default:
                        Debug.Assert(false, "Recognized header has neither special-case nor default handling to convert"
                            + " from header string to RNTBD token.");
                        throw new BadRequestException();
                }

                token.isPresent = true;
            }
        }

        private static void AddExcludeSystemProperties(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.ExcludeSystemProperties];
            TransportSerialization.AddRntdbTokenBool(rntbdRequest.excludeSystemProperties, headerValue);
        }

        private static void AddFanoutOperationStateHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string value = request.Headers[WFConstants.BackendHeaders.FanoutOperationState];
            if (!string.IsNullOrEmpty(value))
            {
                if (!Enum.TryParse(value, true, out FanoutOperationState state))
                {
                    throw new BadRequestException(
                        String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue, value, nameof(FanoutOperationState)));
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
                            String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue, value, nameof(FanoutOperationState)));
                }

                rntbdRequest.FanoutOperationState.value.valueByte = (byte)rntbdState;
                rntbdRequest.FanoutOperationState.isPresent = true;
            }
        }

        private static void AddResourceTypes(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.ResourceTypes];
            if (!string.IsNullOrEmpty(headerValue))
            {
                rntbdRequest.resourceTypes.value.valueBytes = BytesSerializer.GetBytesForString(headerValue, rntbdRequest);
                rntbdRequest.resourceTypes.isPresent = true;
            }
        }

        private static void AddSystemDocumentTypeHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.SystemDocumentType]))
            {
                RntbdConstants.RntbdSystemDocumentType rntbdSystemDocumentType = RntbdConstants.RntbdSystemDocumentType.Invalid;
                if (!Enum.TryParse(request.Headers[HttpConstants.HttpHeaders.SystemDocumentType], true, out SystemDocumentType systemDocumentType))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.SystemDocumentType], nameof(SystemDocumentType)));
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
                            request.Headers[HttpConstants.HttpHeaders.SystemDocumentType], typeof(SystemDocumentType).Name));
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

        private static void AddTransactionCompletionFlag(object isCommit, RntbdConstants.Request rntbdRequest)
        {
            bool? boolData = isCommit as bool?;
            if (!boolData.HasValue)
            {
                throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.TransactionFirstRequest);
            }

            rntbdRequest.transactionCommit.value.valueByte = ((bool)boolData) ? (byte)0x01 : (byte)0x00;
            rntbdRequest.transactionCommit.isPresent = true;
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

        private static void AddIsRetriedWriteRequestMetadata(object isRetriedWriteRequestValue, DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            bool? isRetriedWriteRequest = isRetriedWriteRequestValue as bool?;
            if (!isRetriedWriteRequest.HasValue)
            {
                throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.IsRetriedWriteRequest);
            }

            rntbdRequest.isRetriedWriteRequest.value.valueByte = ((bool)isRetriedWriteRequest) ? (byte)0x01 : (byte)0x00;
            rntbdRequest.isRetriedWriteRequest.isPresent = true;
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
    }
}